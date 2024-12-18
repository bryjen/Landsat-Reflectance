module FsLandsatApi.Handlers.UserTargetsHandler

open System
open System.IO
open System.IdentityModel.Tokens.Jwt
open System.Security.Claims
open System.Text
open System.Text.Json

open FsLandsatApi.Models.User
open FsLandsatApi.Options
open FsLandsatApi.Services.DbUserTargetService
open Microsoft.AspNetCore.Http.Json
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Options
open Microsoft.FSharp.Core
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Identity
open Microsoft.Extensions.DependencyInjection

open Giraffe

open FsToolkit.ErrorHandling

open FsLandsatApi.Models.ApiResponse
open FsLandsatApi.Services.DbUserService
open Microsoft.IdentityModel.Tokens

let private getRequestId (ctx: HttpContext) =
    match ctx.Items.TryGetValue("requestId") with
    | true, value -> value :?> Guid
    | false, _ -> Guid.Empty
    
    
/// 'Target' without the attached 'UserId', to prevent user ids from being exposed to end users.
[<CLIMutable>]
type SimplifiedTarget =
    { Id: Guid
      Path: int
      Row: int
      Latitude: double
      Longitude: double
      MinCloudCoverFilter: double
      MaxCloudCoverFilter: double
      NotificationOffset: TimeSpan }
with
    static member FromTarget(target: Target) =
        { Id = target.Id
          Path = target.Path
          Row = target.Row
          Latitude = target.Latitude
          Longitude = target.Longitude
          MinCloudCoverFilter = target.MinCloudCoverFilter
          MaxCloudCoverFilter = target.MaxCloudCoverFilter
          NotificationOffset = target.NotificationOffset }


[<AutoOpen>]
module private TokenHelpers =
    let tryGetUserEmail (ctx: HttpContext): Result<string, string> =
        try
            match ctx.User.Identity.IsAuthenticated with
            | true ->
                let emailClaimOption = ctx.User.Claims |> Seq.filter (fun (claim: Claim) -> claim.Type = ClaimTypes.Email) |> Seq.tryHead
                match emailClaimOption with
                | Some value ->
                    Ok value.Value
                | None ->
                    Error "Auth error: Could not infer the user email from the auth payload"
            | false ->
                Error "User is not authenticated"
        with
        | ex ->
            Error ex.Message

module UserTargetsGet =
    let handler (next: HttpFunc) (ctx: HttpContext) : HttpFuncResult =
        task {
            let requestId = getRequestId ctx
            
            let loggerFactory = ctx.RequestServices.GetRequiredService<ILoggerFactory>()
            let logger = loggerFactory.CreateLogger("FsLandsatApi.Handlers.UserTargetsHandler.UserTargetsPost")
            
            let dbUserTargetService = ctx.RequestServices.GetRequiredService<DbUserTargetService>()
            
            return! 
                tryGetUserEmail ctx
                |> Result.bind (fun email -> dbUserTargetService.TryGetTargets(email, requestId))
                |> Result.map (List.map SimplifiedTarget.FromTarget)
                |> function
                    | Ok targets ->
                        let asApiResponseObj = { RequestGuid = requestId; ErrorMessage = None; Data = Some targets }
                        logger.LogInformation($"[{requestId}] Successfully fetched \"{List.length targets}\" target(s)")
                        logger.LogDebug($"[{requestId}] {JsonSerializer.Serialize(targets)}")
                        (Successful.ok (json<ApiResponse<SimplifiedTarget list>> asApiResponseObj)) next ctx
                    | Error errorMsg -> 
                        let asApiResponseObj = { RequestGuid = requestId; ErrorMessage = Some errorMsg; Data = None }
                        logger.LogInformation($"[{requestId}] Failed to get targets: \"{errorMsg}\"")
                        (Successful.ok (json<ApiResponse<string>> asApiResponseObj)) next ctx
        }
        
        
module UserTargetsPost =
    [<CLIMutable>]
    type CreateTargetRequest =
        { UserEmail: string  // Email that the target is attached to
          Path: int
          Row: int
          Latitude: double
          Longitude: double
          MinCloudCoverFilter: double
          MaxCloudCoverFilter: double
          NotificationOffset: TimeSpan }
    with
        member this.CreateTargetDto(userId: Guid) =
            { UserId = userId
              Id = Guid.NewGuid()
              Path = this.Path
              Row = this.Row
              Latitude = this.Latitude
              Longitude = this.Longitude
              MinCloudCoverFilter = this.MinCloudCoverFilter
              MaxCloudCoverFilter = this.MaxCloudCoverFilter
              NotificationOffset = this.NotificationOffset }
    
    let handler (next: HttpFunc) (ctx: HttpContext) : HttpFuncResult =
        task {
            let requestId = getRequestId ctx
            
            let loggerFactory = ctx.RequestServices.GetRequiredService<ILoggerFactory>()
            let logger = loggerFactory.CreateLogger("FsLandsatApi.Handlers.UserTargetsHandler.UserTargetsPost")
            
            let dbUserService = ctx.RequestServices.GetRequiredService<DbUserService>()
            let dbUserTargetService = ctx.RequestServices.GetRequiredService<DbUserTargetService>()
            let jsonSerializerOptions = ctx.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions
            
            use requestBodyReader = new StreamReader(ctx.Request.Body)
            let! requestBody = requestBodyReader.ReadToEndAsync()
            let requestModel = JsonSerializer.Deserialize<CreateTargetRequest>(requestBody, jsonSerializerOptions)
            
            return! 
                tryGetUserEmail ctx
                |> Result.bind dbUserService.TryGetUserByEmail
                |> Result.map (fun user -> requestModel.CreateTargetDto(user.Id))
                |> Result.bind (fun target -> dbUserTargetService.TryAddTarget(target, requestId))
                |> Result.map SimplifiedTarget.FromTarget
                |> function
                    | Ok target ->
                        let asApiResponseObj = { RequestGuid = requestId; ErrorMessage = None; Data = Some target }
                        logger.LogInformation($"[{requestId}] Successfully created target \"{target.Id}\"")
                        logger.LogDebug($"[{requestId}] {JsonSerializer.Serialize(target)}")
                        (Successful.ok (json<ApiResponse<SimplifiedTarget>> asApiResponseObj)) next ctx
                    | Error errorMsg -> 
                        let asApiResponseObj = { RequestGuid = requestId; ErrorMessage = Some errorMsg; Data = None }
                        logger.LogInformation($"[{requestId}] Failed to create a target: \"{errorMsg}\"")
                        (Successful.ok (json<ApiResponse<string>> asApiResponseObj)) next ctx
        }
        
module UserTargetsPatch =
    let handler (next: HttpFunc) (ctx: HttpContext) : HttpFuncResult =
        let requestId = getRequestId ctx
        failwith "todo"
        
module UserTargetsDelete =
    let handler (next: HttpFunc) (ctx: HttpContext) : HttpFuncResult =
        let requestId = getRequestId ctx
        failwith "todo"
