module LandsatReflectance.Api.Services.UsgsSceneService

open System
open System.IO
open System.Text
open System.Net.Http

open System.Text.Json
open FsToolkit.ErrorHandling
open Microsoft.FSharp.Control

open LandsatReflectance.Api.Models.Usgs.Scene
open LandsatReflectance.Api.Utils.UsgsHttpClient
open LandsatReflectance.Api.Json.Usgs.SceneSearch
open LandsatReflectance.Api.Services.UsgsTokenService



let rec simplifySceneData (sceneData: SceneData) =
    match Array.tryHead sceneData.BrowseInfos with
    | None ->
          { BrowseName = None
            BrowsePath = None
            OverlayPath = None
            ThumbnailPath = None
            Metadata = simplifyMetadata sceneData }
    | Some value -> 
          { BrowseName = Some value.BrowseName
            BrowsePath = Some value.BrowsePath
            OverlayPath = Some value.OverlayPath
            ThumbnailPath = Some value.ThumbnailPath
            Metadata = simplifyMetadata sceneData }
          
          
and simplifyMetadata (sceneData: SceneData) =
    let metadataMap =
        sceneData.Metadata 
        |> Array.map (fun metadata -> (metadata.FieldName, metadata.Value))
        |> Map.ofArray
        
    let l1ProductIdOption =
        Map.tryFind "Landsat Product Identifier L1" metadataMap
        |> Option.bind (fun jsonElement -> match jsonElement.ValueKind with | JsonValueKind.String -> Some (jsonElement.GetString()) | _ -> None)
        |> Option.map _.Trim()
        
    let l2ProductIdOption =
        Map.tryFind "Landsat Product Identifier L2" metadataMap
        |> Option.bind (fun jsonElement -> match jsonElement.ValueKind with | JsonValueKind.String -> Some (jsonElement.GetString()) | _ -> None)
        |> Option.map _.Trim()
        
    let l1CloudCoverOption =
        Map.tryFind "Scene Cloud Cover L1" metadataMap
        |> Option.bind (fun jsonElement -> match jsonElement.ValueKind with | JsonValueKind.String -> Some (jsonElement.GetString()) | _ -> None)
        |> Option.map _.Trim()
        |> Option.bind (fun str -> try Some (float str) with | _ -> None)
        
    let satelliteOption =
        Map.tryFind "Satellite" metadataMap
        |> Option.bind (fun jsonElement -> match jsonElement.ValueKind with | JsonValueKind.Number -> Some jsonElement | _ -> None)
        |> Option.bind (fun jsonElement -> match jsonElement.TryGetInt32() with | true, i -> Some i | _ -> None) 
        
    
    { EntityId = sceneData.EntityId
      DisplayId = sceneData.DisplayId
      PublishDate = sceneData.PublishDate
      
      L1ProductId = l1ProductIdOption
      L2ProductId = l2ProductIdOption
      L1CloudCover = l1CloudCoverOption
      CloudCoverInt = Some sceneData.CloudCoverInt
      Satellite = satelliteOption }


type UsgsSceneService(
    usgsHttpClient: UsgsHttpClient,
    usgsTokenService: UsgsTokenService) =
    
    member this.GetScenes(path: int, row: int, results: int, skip: int, minCloudCover: int, maxCloudCover: int) =
        task {
            let sceneSearchRequest = createSceneSearchRequest path row results skip minCloudCover maxCloudCover
            use requestContent = new StringContent(sceneSearchRequest, Encoding.UTF8, "application/json")
            
            let! authTokenResult = usgsTokenService.GetToken()
            return 
                match authTokenResult with
                | Ok authToken ->
                    usgsHttpClient.HttpClient.DefaultRequestHeaders.Add("X-Auth-Token", authToken)
                    
                    let responseTask = usgsHttpClient.HttpClient.PostAsync("scene-search", requestContent)
                    responseTask.Wait()
                    let response = responseTask.Result
                    
                    use streamReader = new StreamReader(response.Content.ReadAsStream())
                    let responseContent = streamReader.ReadToEnd()
                    
                    tryParseSceneSearchResponse responseContent
                    |> Result.map (Array.map simplifySceneData)
                | Error errorValue ->
                    Error errorValue
        }
        
    member this.GetSceneAsBase64String(productId: string) =
        let getBases64String (authToken: string) =
            task {
                try
                    use httpClient = new HttpClient()
                    httpClient.DefaultRequestHeaders.Add("X-Auth-Token", authToken)
                    
                    let imgUrl = $"https://landsatlook.usgs.gov/gen-browse?size=rrb&type=refl&product_id={productId}"
                    let! imgBytes = httpClient.GetByteArrayAsync(imgUrl)
                    return Ok (Convert.ToBase64String(imgBytes))
                with
                | ex ->
                    return Error ex.Message
            }
        
        usgsTokenService.GetToken()
        |> TaskResult.mapError _.Message
        |> TaskResult.bind getBases64String
