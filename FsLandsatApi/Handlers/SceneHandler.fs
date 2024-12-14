module FsLandsatApi.Handlers.SceneHandler

open System
open FsLandsatApi.Models.Usgs.Scene
open FsLandsatApi.Services.UsgsSceneService
open FsToolkit.ErrorHandling
open Microsoft.AspNetCore.Http

open FsLandsatApi.Models.ApiResponse

open Giraffe
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Microsoft.FSharp.Core

let private assertValidHttpMethod (ctx: HttpContext) =
    match ctx.Request.Method with
    | "GET" ->
        Ok ()
    | method ->
        Error { RequestGuid = Guid.NewGuid()
                ErrorMessage = Some $"Incorrect HTTP method '{method}'"
                Data = None }
        
let private getRequiredQueryParameter (ctx: HttpContext) (paramName: string) =
    match ctx.TryGetQueryStringValue paramName with
    | None -> Error $"Could not find the required query parameter \"{paramName}\""
    | Some value -> Ok value

let private tryParseToInt (paramName: string) (paramValue: string) =
    match Int32.TryParse(paramValue) with
    | true, value -> Ok value
    | false, _ -> Error $"Could not parse the value of  \"{paramName}\" to an int"

let private tryGetParameters (ctx: HttpContext) =
    result {
        let! path = getRequiredQueryParameter ctx "path" |> Result.bind (tryParseToInt "path")
        let! row = getRequiredQueryParameter ctx "row" |> Result.bind (tryParseToInt "row")
        let! results =
            match ctx.TryGetQueryStringValue "results" with
            | None -> Ok 10
            | Some value -> tryParseToInt "results" value
            
        return path, row, results
    }

let sceneHandler: HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let requestId: Guid =
                match ctx.Items.TryGetValue("requestId") with
                | true, value -> value :?> Guid
                | false, _ -> Guid.Empty
                
            let loggerFactory = ctx.RequestServices.GetRequiredService<ILoggerFactory>()
            let logger = loggerFactory.CreateLogger("FsLandsatApi.Handlers.SceneHandler")
            
            match tryGetParameters ctx with
            | Ok (path, row, results) ->
                let sceneService = ctx.RequestServices.GetRequiredService<UsgsSceneService>()
                let! sceneDataResult = sceneService.GetScenes(path, row, results)
                match sceneDataResult with
                | Ok sceneData ->
                    let successfulResponse =
                        { RequestGuid = requestId
                          ErrorMessage = None
                          Data = Some sceneData }
                    logger.LogInformation($"[{requestId}] Successful \"GET\", returned {sceneData.Length} item(s)")
                    return! (Successful.ok (json<ApiResponse<SimplifiedSceneData array>> successfulResponse)) next ctx
                    
                | Error sceneRequestError ->
                    let unsuccessfulResponse =
                        { RequestGuid = Guid.NewGuid()
                          ErrorMessage = Some (sceneRequestError.ToString())
                          Data = None }
                    logger.LogInformation($"[{requestId}] Failed \"GET\". Bad request with error message \"{sceneRequestError.ToString()}\"")
                    return! (Successful.ok (json<ApiResponse<string>> unsuccessfulResponse)) next ctx
                    
            | Error getParametersError ->
                let unsuccessfulResponse =
                    { RequestGuid = Guid.NewGuid()
                      ErrorMessage = Some getParametersError
                      Data = None }
                logger.LogInformation($"[{requestId}] Failed \"GET\". Bad request with error message \"{getParametersError}\"")
                return! (Successful.ok (json<ApiResponse<string>> unsuccessfulResponse)) next ctx
        }