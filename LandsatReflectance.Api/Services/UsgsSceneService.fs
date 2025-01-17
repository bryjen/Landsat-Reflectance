﻿module LandsatReflectance.Api.Services.UsgsSceneService

open System
open System.IO
open System.Text
open System.Net.Http

open FsToolkit.ErrorHandling
open FsToolkit.ErrorHandling.Operator.TaskResult
open Microsoft.FSharp.Control

open LandsatReflectance.Api.Models.Usgs.Scene
open LandsatReflectance.Api.Utils.UsgsHttpClient
open LandsatReflectance.Api.Json.Usgs.SceneSearch
open LandsatReflectance.Api.Services.UsgsTokenService



let simplifySceneData (sceneData: SceneData) =
    match Array.tryHead sceneData.BrowseInfos with
    | None ->
          { BrowseName = None
            BrowsePath = None
            OverlayPath = None
            ThumbnailPath = None
            
            EntityId = sceneData.EntityId
            DisplayId = sceneData.DisplayId
            PublishDate = sceneData.PublishDate }
    | Some value -> 
          { BrowseName = Some value.BrowseName
            BrowsePath = Some value.BrowsePath
            OverlayPath = Some value.OverlayPath
            ThumbnailPath = Some value.ThumbnailPath
            
            EntityId = sceneData.EntityId
            DisplayId = sceneData.DisplayId
            PublishDate = sceneData.PublishDate }


type UsgsSceneService(
    usgsHttpClient: UsgsHttpClient,
    usgsTokenService: UsgsTokenService) =
    
    member this.GetScenes(path: int, row: int, results: int) =
        task {
            let sceneSearchRequest = createSceneSearchRequest path row results
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
