module LandsatReflectance.Api.Services.PredictionService

open System
open System.Globalization
open System.IO
open System.Net.Http
open System.Net.Http.Headers
open System.Text
open System.Text.Json
open System.Text.Json.Nodes
open System.Threading.Tasks
open CsvHelper
open CsvHelper.Configuration
open FsToolkit.ErrorHandling
open LandsatReflectance.Api.Json.Usgs.SceneSearch
open LandsatReflectance.Api.Models.Usgs.Scene
open LandsatReflectance.Api.Services.UsgsTokenService
open LandsatReflectance.Api.Utils.UsgsHttpClient
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging

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
        
        
type public NormalDistributionParameters =
    { Landsat8Mean: TimeSpan
      Landsat8StdDev: TimeSpan
      Landsat9Mean: TimeSpan
      Landsat9StdDev: TimeSpan }

[<CLIMutable>]
type public PathRow =
    { Path: int
      Row: int }
    
[<CLIMutable>]
type public PredictionStateEntry =
    { Path: int
      Row: int
      PredictedSatellite: int
      PredictedDateTimeUtc: DateTime
      Landsat8Mean: TimeSpan
      Landsat8StdDev: TimeSpan
      Landsat9Mean: TimeSpan
      Landsat9StdDev: TimeSpan }
        
    
    
type public PredictionsState() =
    
    static member Something(
        outputPath: string,
        renamethisthing: int -> int -> TaskResult<Prediction * NormalDistributionParameters, string>,
        logger: ILogger,
        pathRowArr: PathRow array)
            // : TaskResult<Map<int * int, PredictionStateEntry>, string> =
            : Task<Map<int * int, PredictionStateEntry>> =
                
        let saveInterval = 500
        let mutable map: Map<int * int, PredictionStateEntry> = Map.ofList []
        let mutable mapCount = 0
        
        let offset = 2289
        let start = 0 + offset
        for i in start .. pathRowArr.Length - 1 do
            let pathRow = pathRowArr[i]
            let path = pathRow.Path
            let row = pathRow.Row
            let percent = (float i) / (float pathRowArr.Length)
            
            taskResult {
                try 
                    let! prediction, normalDistributionParameters = renamethisthing path row
                    let predictionStateEntry =
                        { Path = path
                          Row = row
                          PredictedSatellite = prediction.PredictedSatellite
                          PredictedDateTimeUtc = prediction.PredictedTimeUtc
                          Landsat8Mean = normalDistributionParameters.Landsat8Mean
                          Landsat8StdDev = normalDistributionParameters.Landsat8StdDev
                          Landsat9Mean = normalDistributionParameters.Landsat9Mean
                          Landsat9StdDev = normalDistributionParameters.Landsat9StdDev }
                    
                    // map <- map.Add ((path, row), predictionStateEntry)
                    logger.LogInformation($"[{DateTime.Now}]\t({path}, {row}) ({percent:P2}%%)\t(landsat {predictionStateEntry.PredictedSatellite}) {predictionStateEntry.PredictedDateTimeUtc}")
                    return predictionStateEntry 
                with
                | ex ->
                    return! (Error ex.Message)
            }
            |> _.GetAwaiter()
            |> _.GetResult()
            |> function
                | Ok predictionStateEntry ->
                    map <- map.Add ((path, row), predictionStateEntry)
                    mapCount <- mapCount + 1
                    
                    if (mapCount + 1) % saveInterval = 0 then
                        let values = Seq.toArray map.Values
                        use writer = new StreamWriter(outputPath)
                        use csv = new CsvWriter(writer, CultureInfo.InvariantCulture)
                        csv.WriteRecords(values)
                        logger.LogInformation($"[{DateTime.Now}]\tSaved {values.Length} entries, {percent:P2}%% done. Saved at i = {i}")
                | Error errorMsg ->
                    logger.LogWarning($"[{DateTime.Now}]\t({path}, {row}) ({percent:P2}%%)\tFAILED with message \"{errorMsg}\"")
                    
        Task.FromResult map
        
        (*
        taskResult {
            let mutable map: Map<int * int, PredictionStateEntry> = Map.ofList []
            
            for i in 0 .. pathRowArr.Length - 1 do
                let pathRow = pathRowArr[i]
                let path = pathRow.Path
                let row = pathRow.Row
                
                try 
                    let! prediction, normalDistributionParameters = renamethisthing path row
                    let predictionStateEntry =
                        { Path = path
                          Row = row
                          PredictedSatellite = prediction.PredictedSatellite
                          PredictedDateTimeUtc = prediction.PredictedTimeUtc
                          Landsat8Mean = normalDistributionParameters.Landsat8Mean
                          Landsat8StdDev = normalDistributionParameters.Landsat8StdDev
                          Landsat9Mean = normalDistributionParameters.Landsat9Mean
                          Landsat9StdDev = normalDistributionParameters.Landsat9StdDev }
                    
                    map <- map.Add ((path, row), predictionStateEntry)
                    logger.LogInformation($"[{DateTime.Now}]\t({path}, {row})\t(landsat {predictionStateEntry.PredictedSatellite}) {predictionStateEntry.PredictedDateTimeUtc}")
                    
                    if (i + 1) % saveInterval = 0 then
                        let values = Seq.toArray map.Values
                        use writer = new StreamWriter(outputPath)
                        use csv = new CsvWriter(writer, CultureInfo.InvariantCulture)
                        csv.WriteRecords(values)
                        logger.LogInformation($"[{DateTime.Now}]\tSaved {values.Length} entries, {((double i) / (float pathRowArr.Length))} done")
                with
                | ex ->
                    logger.LogWarning($"[{DateTime.Now}]\t({path}, {row})\tFAILED with message \"{ex.Message}\"")
                
            return map
        }
        *) 
    
    static member Init(serviceProvider: IServiceProvider) =
        taskResult {
            let logger = serviceProvider.GetRequiredService<ILogger<PredictionsState>>()
            
            let bootstrapFilePath = "./Data/bootstrapPathRowData.csv"
            let predictionDataFilePath = "./Data/predictionData.csv"
            
            match File.Exists(bootstrapFilePath), File.Exists(predictionDataFilePath) with
            | true, true ->
                logger.LogInformation($"Found path/row data file \"{predictionDataFilePath}\".")
                // load prediction data file 
                failwith "todo"
            | true, false ->
                logger.LogInformation($"Found bootstrap file \"{bootstrapFilePath}\", path/row data file missing at \"{predictionDataFilePath}\".")
                logger.LogInformation("Initializing path/row data file.")
                
                let csvReaderConfig = CsvConfiguration(CultureInfo.InvariantCulture)
                csvReaderConfig.PrepareHeaderForMatch <- _.Header.ToLower()
                csvReaderConfig.HeaderValidated <- null
                csvReaderConfig.MissingFieldFound <- null
                
                use reader = new StreamReader(bootstrapFilePath)
                use csv = new CsvReader(reader, csvReaderConfig)
                let pathRows = csv.GetRecords<PathRow>() |> Seq.toArray
                
                logger.LogInformation($"Fetched {pathRows.Length} entries from bootstrap file.")
                
                let usgsTokenService = serviceProvider.GetRequiredService<UsgsTokenService>()
                let usgsHttpClient = serviceProvider.GetRequiredService<UsgsHttpClient>()
                let partialApplication = (fun path row -> PredictionService.GetPredictionAndNormalDistributionParameters(usgsTokenService, usgsHttpClient, path, row, 10))
                let! map = PredictionsState.Something(predictionDataFilePath, partialApplication, logger, pathRows)
                
                let values = Seq.toArray map.Values
                use writer = new StreamWriter(predictionDataFilePath)
                use csv = new CsvWriter(writer, CultureInfo.InvariantCulture)
                csv.WriteRecords(values)
                
                logger.LogInformation($"[{DateTime.Now}]\tSaved {values.Length} entries.")
                logger.LogInformation($"[{DateTime.Now}]\tFinished initialization")
                
                
                // init data file from bootstrap
                failwith "todo"
            | false, true ->
                logger.LogInformation($"Found path/row data file \"{predictionDataFilePath}\".")
                logger.LogWarning($"Could not find the bootstrap file at \"{bootstrapFilePath}\".")
                // log warning
                // load prediction data file 
                failwith "todo"
            | false, false ->
                // TODO: Add a way to fix this; link back somewhere to the repo
                failwith $"Missing path/row data file, and no bootstrap file was found at \"{bootstrapFilePath}\"."
        }
    
 
/// <summary>
/// Service class offering functionality related to scene predictions, as well as containing the 'current' state of
/// predictions and how close we are to them. This enables us to notify users.
/// </summary>
and public PredictionService(serviceScopeFactory: IServiceScopeFactory) as this =
    
    let results = 25
    
    let scope = serviceScopeFactory.CreateScope()
    let usgsHttpClient = scope.ServiceProvider.GetRequiredService<UsgsHttpClient>()
    let usgsTokenService = scope.ServiceProvider.GetRequiredService<UsgsTokenService>()
    
    // let predictionsState = PredictionsState.Init(scope.ServiceProvider)
    
    
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
        
    member internal this.GetPredictionAndNormalDistributionParameters(path: int, row: int) : TaskResult<Prediction * NormalDistributionParameters, string> =
        taskResult {
            let! authToken = usgsTokenService.GetToken()
                             |> TaskResult.mapError _.Message 
            usgsHttpClient.HttpClient.DefaultRequestHeaders.Add("X-Auth-Token", authToken)
            
            let! scenes = getScenes usgsHttpClient.HttpClient path row results
                          |> TaskResult.mapError _.Message
                          
            // Perform normal distribution parameters estimation
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
            let normalDistributionParameters = 
                { Landsat8Mean = landsat8Prediction.PredictedTimeUtcWeightedMean
                  Landsat8StdDev = landsat8Prediction.PredictedTimeUtcWeightedStdDev
                  Landsat9Mean = landsat9Prediction.PredictedTimeUtcWeightedMean
                  Landsat9StdDev  = landsat9Prediction.PredictedTimeUtcWeightedStdDev }
                
                
            // Perform prediction
            let! predictedSatellite, publishDates = filterScenesBySatellite scenes
            let prediction = performCoreCalculation path row predictedSatellite publishDates   
                
                
            return prediction, normalDistributionParameters
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
        
    static member internal GetPredictionAndNormalDistributionParameters(usgsTokenService: UsgsTokenService, usgsHttpClient: UsgsHttpClient, path: int, row: int, results: int) : TaskResult<Prediction * NormalDistributionParameters, string> =
        taskResult {
            try
                let! authToken = usgsTokenService.GetToken()
                                 |> TaskResult.mapError _.Message
                                 
                // usgsHttpClient.HttpClient.DefaultRequestHeaders.Add("X-Auth-Token", authToken)
                // usgsHttpClient.HttpClient.DefaultRequestHeaders.Authorization <- AuthenticationHeaderValue("X-Auth-Token", authToken)
                
                let authHeaderKey = "X-Auth-Token"
                if not (usgsHttpClient.HttpClient.DefaultRequestHeaders.Contains(authHeaderKey)) then
                    usgsHttpClient.HttpClient.DefaultRequestHeaders.Add("X-Auth-Token", authToken)
                
                let! scenes = getScenes usgsHttpClient.HttpClient path row results
                              |> TaskResult.mapError _.Message
                              
                // Perform normal distribution parameters estimation
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
                let normalDistributionParameters = 
                    { Landsat8Mean = landsat8Prediction.PredictedTimeUtcWeightedMean
                      Landsat8StdDev = landsat8Prediction.PredictedTimeUtcWeightedStdDev
                      Landsat9Mean = landsat9Prediction.PredictedTimeUtcWeightedMean
                      Landsat9StdDev  = landsat9Prediction.PredictedTimeUtcWeightedStdDev }
                    
                    
                // Perform prediction
                let! predictedSatellite, publishDates = filterScenesBySatellite scenes
                let prediction = performCoreCalculation path row predictedSatellite publishDates   
                    
                    
                return prediction, normalDistributionParameters
            with
            | ex ->
                return! (Error $"[GetPredictionAndNormalDistributionParameters] Failed with exception message \"{ex.Message}\".")
        }
