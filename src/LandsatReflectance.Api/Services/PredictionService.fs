module LandsatReflectance.Api.Services.PredictionService

open System
open System.IO
open System.Net.Http
open System.Text
open System.Text.Json
open System.Text.Json.Nodes
open FsLandsatApi.Models.PredictionsState
open FsToolkit.ErrorHandling
open LandsatReflectance.Api.Json.Usgs.SceneSearch
open LandsatReflectance.Api.Models.Usgs.Scene
open LandsatReflectance.Api.Services.UsgsTokenService
open LandsatReflectance.Api.Utils.UsgsHttpClient
open Microsoft.Extensions.DependencyInjection

type public Prediction =
    { Path: int
      Row: int
      PredictedSatellite: int
      PredictedTimeUtc: DateTime
      PredictedTimeUtcWeightedMean: TimeSpan
      PredictedTimeUtcWeightedStdDev: TimeSpan }
    
    
    
/// Type that represents a 'compressed' SceneData object, contains specific time information related to/or required to
/// calculate a prediction.
type private SceneDataTime =
    { Path: int
      Row: int
      Satellite: int
      PublishDateUtc: DateTime }
    
let rec private simplifySceneData (sceneData: SceneData) : SceneDataTime option =
    option {
        let! path, row, satellite = filterMetadataInfo sceneData
        return 
            { Row = row
              Path = path
              Satellite = satellite
              PublishDateUtc = sceneData.PublishDate.UtcDateTime }
    }

and private filterMetadataInfo (sceneData: SceneData) =
    option {
        let metadataMap =
            sceneData.Metadata 
            |> Array.map (fun metadata -> (metadata.FieldName, metadata.Value))
            |> Map.ofArray
            
        let! path =
            Map.tryFind "WRS Path" metadataMap
            |> Option.bind (fun jsonElement -> match jsonElement.ValueKind with | JsonValueKind.String -> Some (jsonElement.GetString()) | _ -> None)
            |> Option.map _.Trim()
            |> Option.bind (fun str -> try Some (int str) with | _ -> None)
            
        let! row =
            Map.tryFind "WRS Row" metadataMap
            |> Option.bind (fun jsonElement -> match jsonElement.ValueKind with | JsonValueKind.String -> Some (jsonElement.GetString()) | _ -> None)
            |> Option.map _.Trim()
            |> Option.bind (fun str -> try Some (int str) with | _ -> None)
            
        let! satellite =
            Map.tryFind "Satellite" metadataMap
            |> Option.bind (fun jsonElement -> match jsonElement.ValueKind with | JsonValueKind.Number -> Some jsonElement | _ -> None)
            |> Option.bind (fun jsonElement -> match jsonElement.TryGetInt32() with | true, i -> Some i | _ -> None)
            
        return (path, row, satellite)
    }
    
[<AutoOpen>]
module private PredictionServiceHelpers =
    let createPredictionSceneSearchRequest
        (path: int)
        (row: int)
        (results: int)
        : string =
        // Creating metadata filter
        let pathMetadataValueFilterJsonObj = JsonObject()
        pathMetadataValueFilterJsonObj["filterType"] <- JsonValue.Create("value")
        pathMetadataValueFilterJsonObj["filterId"] <- JsonValue.Create("5e83d14fb9436d88")
        pathMetadataValueFilterJsonObj["value"] <- JsonValue.Create(path.ToString())
        pathMetadataValueFilterJsonObj["operand"] <- JsonValue.Create("=")
        
        let rowMetadataValueFilterJsonObj = JsonObject()
        rowMetadataValueFilterJsonObj["filterType"] <- JsonValue.Create("value")
        rowMetadataValueFilterJsonObj["filterId"] <- JsonValue.Create("5e83d14ff1eda1b8")
        rowMetadataValueFilterJsonObj["value"] <- JsonValue.Create(row.ToString())
        rowMetadataValueFilterJsonObj["operand"] <- JsonValue.Create("=")
        
        let metadataFilterJsonObj = JsonObject()
        metadataFilterJsonObj["filterType"] <- JsonValue.Create("and")
        metadataFilterJsonObj["childFilters"] <- JsonValue.Create([| pathMetadataValueFilterJsonObj; rowMetadataValueFilterJsonObj |])
        
        
        let sceneFilterJsonObj = JsonObject()
        sceneFilterJsonObj["metadataFilter"] <- metadataFilterJsonObj
        
        
        let requestJsonObj = JsonObject()
        requestJsonObj["datasetName"] <- JsonValue.Create("landsat_ot_c2_l2")
        requestJsonObj["maxResults"] <- JsonValue.Create(results.ToString())
        requestJsonObj["useCustomization"] <- JsonValue.Create(false)
        requestJsonObj["sceneFilter"] <- sceneFilterJsonObj

        requestJsonObj.ToString()
        
    
    let getScenes (httpClient: HttpClient) path row results =
        taskResult {
            let sceneSearchRequest = createPredictionSceneSearchRequest path row results
            use requestContent = new StringContent(sceneSearchRequest, Encoding.UTF8, "application/json")
            let! response = httpClient.PostAsync("scene-search", requestContent)
            use streamReader = new StreamReader(response.Content.ReadAsStream())
            let! responseContent = streamReader.ReadToEndAsync()
            return!
                tryParseSceneSearchResponse responseContent
                |> Result.map (Array.map simplifySceneData)
                |> Result.map (Array.filter Option.isSome)
                |> Result.map (Array.map Option.get)
                |> Result.map (Array.sortByDescending _.PublishDateUtc)
        }
        
    let filterScenesBySatellite (sceneDataTimes: SceneDataTime array) =
        result {
            let! latest =
                match sceneDataTimes.Length with
                | i when i > 0 -> Ok sceneDataTimes[0]
                | _ -> Error "[filterScenesBySatellite] 'sceneDataTimes' has no values"
                
            let! satelliteToFilterBy =
                match latest.Satellite with
                | 8 -> Ok 9
                | 9 -> Ok 8
                | _ -> Error $"[filterScenesBySatellite] 'latest' has an unknown satellite value: \"{latest.Satellite}\""
                
            let publishDatesUtc =
                sceneDataTimes
                |> Array.filter (fun sceneDataTime -> sceneDataTime.Satellite = satelliteToFilterBy)
                |> Array.map _.PublishDateUtc
                
            return (satelliteToFilterBy, publishDatesUtc)
        }
        
    let filterOutliers (timespans: TimeSpan array) =
        let median (arr: float array) =
            match arr.Length with
            | l when l % 2 = 0 -> arr[l / 2]
            | l -> (arr[l / 2] + arr[(l / 2) + 1]) / 2.0
           
        let split (arr: float array) : float array * float array =
            let l2 = arr.Length / 2
            match arr.Length with
            | l when l % 2 = 0 -> 
                (arr[0..l2-1], arr[l2..arr.Length-1])
            | _ ->
                (arr[0..l2-1], arr[l2+1..arr.Length-1])
           
            
        let timespansAsTicks =
            timespans
            |> Array.map (fun (ts: TimeSpan) -> float ts.Ticks) 
            |> Array.sortBy id
        let first, second =  split timespansAsTicks
        let q1 = median first
        let q3 = median second
        let iqr = q3 - q1
        let multiplier = 0.5  // adjust for tolerance
        
        timespansAsTicks
        |> Array.filter (fun ticks -> ticks > q1 - (multiplier * iqr) && ticks < q3 + (multiplier * iqr))
        |> Array.map int64
        |> Array.map TimeSpan.FromTicks
        
        
    // GPT code !!!
    let calculateWeightedStats data bins =
        let minVal, maxVal = Array.min data, Array.max data
        let binSize = (maxVal - minVal) / float bins

        let binValue x = Math.Floor((x - minVal) / binSize)

        let bins = 
            data 
            |> Array.groupBy binValue 
            |> Array.map (fun (bin, values) -> ((bin * binSize) + minVal, float (Array.length values)))

        let totalCount = bins |> Array.sumBy snd
        let weights = bins |> Array.map (fun (center, count) -> center, count / totalCount)

        let weightedMean = weights |> Array.sumBy (fun (center, weight) -> center * weight)
        let weightedVariance = 
            weights 
            |> Array.sumBy (fun (center, weight) -> weight * (center - weightedMean) ** 2.0)

        let weightedStdDev = sqrt weightedVariance
        weightedMean, weightedStdDev
        
        
    let performCoreCalculation path row predictedSatellite (publishDates: DateTime array) =
        let timespans = 
            publishDates
            |> Array.pairwise
            |> Array.map (fun tuple -> (fst tuple) - (snd tuple))
            |> filterOutliers
            
        let meanTimespan =
            timespans
            |> Array.averageBy (fun timespan -> float timespan.Ticks)
            |> int64
            |> TimeSpan.FromTicks
            
        let latestPublishDate = publishDates[0]  // we know it's not null since we checked for it in 'filterScenesBySatellite'
        let predictedTimeUtc = latestPublishDate + meanTimespan
        
        let timespansTicks =
            timespans
            |> Array.map _.Ticks
            |> Array.map float
            
        let weightedMean, weightedStdDev = calculateWeightedStats timespansTicks 100
        { Path = path
          Row = row
          PredictedSatellite = predictedSatellite
          PredictedTimeUtc = predictedTimeUtc
          PredictedTimeUtcWeightedMean = weightedMean |> int64 |> TimeSpan.FromTicks
          PredictedTimeUtcWeightedStdDev = weightedStdDev |> int64 |> TimeSpan.FromTicks }
        
        
        
    
 
/// <summary>
/// Service class offering functionality related to scene predictions, as well as containing the 'current' state of
/// predictions and how close we are to them. This enables us to notify users.
/// </summary>
type public PredictionService(serviceScopeFactory: IServiceScopeFactory) as this =
    
    let results = 25
    
    let scope = serviceScopeFactory.CreateScope()
    let usgsHttpClient = scope.ServiceProvider.GetRequiredService<UsgsHttpClient>()
    let usgsTokenService = scope.ServiceProvider.GetRequiredService<UsgsTokenService>()
    
    let predictionsState = PredictionsState.Init(scope.ServiceProvider)
    
    
    interface IDisposable with
        member _.Dispose() =
            scope.Dispose()
            ()
    
    member this.GetPrediction(path: int, row: int) =
        taskResult {
            let! authToken = usgsTokenService.GetToken()
                             |> TaskResult.mapError _.Message 
            usgsHttpClient.HttpClient.DefaultRequestHeaders.Add("X-Auth-Token", authToken)
            
            let! scenes = getScenes usgsHttpClient.HttpClient path row results
                          |> TaskResult.mapError _.Message 
            let! predictedSatellite, publishDates = filterScenesBySatellite scenes
         
            return performCoreCalculation path row predictedSatellite publishDates   
        }

    member internal this.GetNormalProbabilityDistributionMetrics(path: int, row: int) =
        taskResult {
            let! authToken = usgsTokenService.GetToken()
                             |> TaskResult.mapError _.Message 
            usgsHttpClient.HttpClient.DefaultRequestHeaders.Add("X-Auth-Token", authToken)
            
            let! scenes = getScenes usgsHttpClient.HttpClient path row results
                          |> TaskResult.mapError _.Message
                          
            let publishDatesUtcLandsat8 =
                scenes
                |> Array.filter (fun sceneDataTime -> sceneDataTime.Satellite = 8)
                |> Array.map _.PublishDateUtc
                
            let publishDatesUtcLandsat9 =
                scenes
                |> Array.filter (fun sceneDataTime -> sceneDataTime.Satellite = 9)
                |> Array.map _.PublishDateUtc
                
            let landsat8Prediction = performCoreCalculation path row 8 publishDatesUtcLandsat8
            let landsat9Prediction = performCoreCalculation path row 9 publishDatesUtcLandsat9
             
            return    
                { Landsat8Mean = landsat8Prediction.PredictedTimeUtcWeightedMean
                  Landsat8StdDev = landsat8Prediction.PredictedTimeUtcWeightedStdDev
                  Landsat9Mean = landsat9Prediction.PredictedTimeUtcWeightedMean
                  Landsat9StdDev  = landsat9Prediction.PredictedTimeUtcWeightedStdDev }
        }
