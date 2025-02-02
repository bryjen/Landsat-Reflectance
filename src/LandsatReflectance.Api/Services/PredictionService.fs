module FsLandsatApi.Services.PredictionService

open System
open System.IO
open System.Net.Http
open System.Text
open FsToolkit.ErrorHandling
open LandsatReflectance.Api.Json.Usgs.SceneSearch
open LandsatReflectance.Api.Models.Usgs.Scene
open LandsatReflectance.Api.Services
open LandsatReflectance.Api.Services.UsgsTokenService
open LandsatReflectance.Api.Utils
open LandsatReflectance.Api.Utils.UsgsHttpClient

type PredictedSatellite =
    | Landsat8
    | Landsat9
    
type Prediction =
    { Path: int
      Row: int
      PredictedSatellite: PredictedSatellite
      PredictedTimeUtc: DateTime
      PredictedTimeUtcVariance: TimeSpan }
    
    
/// Type that represents a 'compressed' SceneData object, contains specific time information related to/or required to
/// calculate a prediction.
type private SceneDataTime =
    { Path: int
      Row: int
      Satellite: int
      PublishDateUtc: DateTime }
    
let simplifySceneData (sceneData: SceneData) : SceneDataTime =
    { Path = sceneData.Metadata }
    
    
    
type PredictionService(
    usgsHttpClient: UsgsHttpClient,
    usgsTokenService: UsgsTokenService) =
    
    member private this.GetScenes(path: int, row: int, results: int, skip: int, minCloudCover: int, maxCloudCover: int) =
        taskResult {
            let sceneSearchRequest = createSceneSearchRequest path row results skip minCloudCover maxCloudCover
            use requestContent = new StringContent(sceneSearchRequest, Encoding.UTF8, "application/json")
            
            let! authToken = usgsTokenService.GetToken()
            usgsHttpClient.HttpClient.DefaultRequestHeaders.Add("X-Auth-Token", authToken)
            
            use requestContent = new StringContent(sceneSearchRequest, Encoding.UTF8, "application/json")
            let! response = usgsHttpClient.HttpClient.PostAsync("scene-search", requestContent)
            use streamReader = new StreamReader(response.Content.ReadAsStream())
            let! responseContent = streamReader.ReadToEndAsync()
            
            return!
                tryParseSceneSearchResponse responseContent
                |> Result.map (Array.map simplifySceneData)
        }
        |> ignore
        failwith "todo"
        
    member this.GetPrediction(path: int, row: int) =
        failwith "todo"
