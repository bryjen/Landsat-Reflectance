module FsLandsatApi.Services.UsgsSceneService

open System
open System.IO
open System.Net.Http
open System.Net.Http.Headers
open System.Text
open FsLandsatApi.Json.Usgs.SceneSearch
open FsToolkit.ErrorHandling
open Microsoft.Extensions.Logging
open FsLandsatApi.Services.UsgsTokenService
open Microsoft.FSharp.Control

type UsgsSceneService(
    logger: ILogger<UsgsSceneService>,
    usgsTokenService: UsgsTokenService) =
    
    member this.GetScenes(path: int, row: int, results: int) =
        result {
            // TODO: Replace with IHttpClientFactory, when 'fixed'
            use httpClient = new HttpClient()
            httpClient.BaseAddress <- Uri("https://m2m.cr.usgs.gov/api/api/json/stable/")
            httpClient.DefaultRequestHeaders.Accept.Add(MediaTypeWithQualityHeaderValue("application/json"))
            
            let sceneSearchRequest = createSceneSearchRequest path row results
            use requestContent = new StringContent(sceneSearchRequest, Encoding.UTF8, "application/json")
            
            let! authToken = usgsTokenService.GetToken() |> Async.RunSynchronously
            httpClient.DefaultRequestHeaders.Add("X-Auth-Token", authToken)
            
            let responseTask = httpClient.PostAsync("scene-search", requestContent)
            responseTask.Wait()
            let response = responseTask.Result
            
            use streamReader = new StreamReader(response.Content.ReadAsStream())
            let responseContent = streamReader.ReadToEnd()
            return! tryParseSceneSearchResponse responseContent
        }