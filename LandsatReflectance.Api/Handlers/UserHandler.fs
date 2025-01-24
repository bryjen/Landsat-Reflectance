module LandsatReflectance.Api.Handlers.UserHandler

open System
open System.IO
open System.Text.Json
open System.Security.Claims

open System.Threading.Tasks
open LandsatReflectance.Api.Services
open Microsoft.FSharp.Core
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Options
open Microsoft.AspNetCore.Http.Json
open Microsoft.Extensions.DependencyInjection

open Giraffe

open FsToolkit.ErrorHandling

open FsLandsatApi.Utils.JwtTokens
open LandsatReflectance.Api.Options
open LandsatReflectance.Api.Models.User
open LandsatReflectance.Api.Models.ApiResponse
open LandsatReflectance.Api.Utils.PasswordHashing
open LandsatReflectance.Api.Services.DbUserService



[<CLIMutable>]
type LoginData =
    { AccessToken: string
      RefreshToken: string }
    
    

[<AutoOpen>]
module private Helpers = 
    let getRequiredQueryParameter (ctx: HttpContext) (paramName: string) =
        match ctx.TryGetQueryStringValue paramName with
        | None -> Error $"Could not find the required query parameter \"{paramName}\""
        | Some value -> Ok value

    let tryParseToInt (paramName: string) (paramValue: string) =
        match Int32.TryParse(paramValue) with
        | true, value -> Ok value
        | false, _ -> Error $"Could not parse the value of  \"{paramName}\" to an int"
        
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
            
        
        
[<RequireQualifiedAccess>]
module UserLoginPost =
    [<CLIMutable>]
    type LoginUserRequest =
        { Email: string
          Password: string }
    
    let rec handler (next: HttpFunc) (ctx: HttpContext) : HttpFuncResult =
        let requestId: Guid =
            match ctx.Items.TryGetValue("requestId") with
            | true, value -> value :?> Guid
            | false, _ -> Guid.Empty
        
        let rawData : TaskResult<LoginData, string> = 
            taskResult {
                let dbUserService = ctx.RequestServices.GetRequiredService<DbUserService>()
                let authTokenOptions = ctx.RequestServices.GetRequiredService<IOptions<AuthTokenOptions>>().Value
                let jsonSerializerOptions = ctx.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions
                
                use requestBodyReader = new StreamReader(ctx.Request.Body)
                let! requestBody = requestBodyReader.ReadToEndAsync()
                let registerUserRequest = JsonSerializer.Deserialize<LoginUserRequest>(requestBody, jsonSerializerOptions)
                
                let! user = dbUserService.TryGetUserByCredentials(registerUserRequest.Email, registerUserRequest.Password)
                return! tryCreateLoginData dbUserService authTokenOptions user
            }
            
        Task.bind (transformRawData next ctx requestId) rawData
        
        
    and private tryCreateLoginData (dbUserService: DbUserService) (authTokenOptions: AuthTokenOptions) (user: User) =
        taskResult {
            let! refreshGuid = dbUserService.GenerateNewRefreshGuid(user.Email)
            let accessToken = createAccessToken authTokenOptions.SigningKey user
            let refreshToken = createRefreshToken authTokenOptions.SigningKey refreshGuid user
            return { AccessToken = accessToken
                     RefreshToken = refreshToken }
        }
        
    and private transformRawData (next: HttpFunc) (ctx: HttpContext) (requestId: Guid) (result: Result<LoginData, string>) : HttpFuncResult =
        match result with
        | Ok loginUserResponse -> 
            let asApiResponseObj =
                { RequestGuid = requestId
                  ErrorMessage = None
                  Data = Some loginUserResponse }
            (Successful.ok (json<ApiResponse<LoginData>> asApiResponseObj)) next ctx
        | Error error ->
            let asApiResponseObj =
                { RequestGuid = requestId
                  ErrorMessage = Some error
                  Data = None }
            (Successful.ok (json<ApiResponse<obj>> asApiResponseObj)) next ctx
        
       
       
module RefreshTokenLoginPost =
    [<CLIMutable>]
    type RefreshTokenLoginRequest =
        { RefreshToken: string }
        
    let rec handler (next: HttpFunc) (ctx: HttpContext) : HttpFuncResult =
        let requestId: Guid =
            match ctx.Items.TryGetValue("requestId") with
            | true, value -> value :?> Guid
            | false, _ -> Guid.Empty
        
        let rawData : TaskResult<string, string> = 
            taskResult {
                let dbUserService = ctx.RequestServices.GetRequiredService<DbUserService>()
                let authTokenOptions = ctx.RequestServices.GetRequiredService<IOptions<AuthTokenOptions>>().Value
                let jsonSerializerOptions = ctx.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions
                
                use requestBodyReader = new StreamReader(ctx.Request.Body)
                let! requestBody = requestBodyReader.ReadToEndAsync()
                let req = JsonSerializer.Deserialize<RefreshTokenLoginRequest>(requestBody, jsonSerializerOptions)
                
                let! refreshTokenParseResults = tryParseRefreshToken authTokenOptions.SigningKey req.RefreshToken
                let! userEmail = validateRefreshGuidWithDbValue dbUserService refreshTokenParseResults
                let! user = dbUserService.TryGetUserByEmail userEmail
                return createAccessToken authTokenOptions.SigningKey user
            }
            
        Task.bind (transformRawData next ctx requestId) rawData
        
        
    and private validateRefreshGuidWithDbValue (dbUserService: DbUserService) (refreshTokenParseResults: RefreshTokenParseResults) =
        taskResult {
            let! jwtGuid = dbUserService.TryGetJwtGuid(refreshTokenParseResults.Email)
            return!
                match jwtGuid with
                | Some guid when guid = refreshTokenParseResults.RefreshGuid -> Ok refreshTokenParseResults.Email
                | Some _ -> Error "Refresh token id does not match the existing one."
                | None -> Error "No refresh token id present."
        }
        
    and private transformRawData (next: HttpFunc) (ctx: HttpContext) (requestId: Guid) (accessTokenResult: Result<string, string>) : HttpFuncResult =
        match accessTokenResult with
        | Ok accessToken ->
            let asApiResponseObj =
                { RequestGuid = requestId
                  ErrorMessage = None
                  Data = Some accessToken }
            (Successful.ok (json<ApiResponse<string>> asApiResponseObj)) next ctx
        | Error error ->
            let asApiResponseObj =
                { RequestGuid = requestId
                  ErrorMessage = Some error
                  Data = None }
            (Successful.ok (json<ApiResponse<obj>> asApiResponseObj)) next ctx
        
        
       
[<RequireQualifiedAccess>]
module UserCreatePost =
    [<CLIMutable>]
    type CreateUserRequest =
        { Email: string
          FirstName: string
          LastName: string
          Password: string
          IsEmailEnabled: bool }
    
    let rec handler (next: HttpFunc) (ctx: HttpContext) : HttpFuncResult =
        let requestId: Guid =
            match ctx.Items.TryGetValue("requestId") with
            | true, value -> value :?> Guid
            | false, _ -> Guid.Empty
        
        let rawData : TaskResult<LoginData, string> = 
            taskResult {
                let dbUserService = ctx.RequestServices.GetRequiredService<DbUserService>()
                let authTokenOptions = ctx.RequestServices.GetRequiredService<IOptions<AuthTokenOptions>>().Value
                let jsonSerializerOptions = ctx.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions
                
                use requestBodyReader = new StreamReader(ctx.Request.Body)
                let! requestBody = requestBodyReader.ReadToEndAsync()
                let req = JsonSerializer.Deserialize<CreateUserRequest>(requestBody, jsonSerializerOptions)
                
                let! newUser = dbUserService.TryCreateUser(req.FirstName, req.LastName, req.Email, req.Password, req.IsEmailEnabled)
                let! loginData = tryCreateLoginData dbUserService authTokenOptions newUser
                return loginData
            }
            
        Task.bind (transformRawData next ctx requestId) rawData
        
        
    and private tryCreateLoginData (dbUserService: DbUserService) (authTokenOptions: AuthTokenOptions) (user: User) : TaskResult<LoginData, string> =
        taskResult {
            let! refreshGuidOption = dbUserService.TryGetJwtGuid(user.Email)
            let! refreshGuid =
                match refreshGuidOption with
                | Some guid -> Ok guid |> Task.FromResult
                | None -> dbUserService.GenerateNewRefreshGuid(user.Email)
            
            let accessToken = createAccessToken authTokenOptions.SigningKey user
            let refreshToken = createRefreshToken authTokenOptions.SigningKey refreshGuid user
            return { AccessToken = accessToken
                     RefreshToken = refreshToken }
        }
        
    and private transformRawData (next: HttpFunc) (ctx: HttpContext) (requestId: Guid) (loginDataResult: Result<LoginData, string>) : HttpFuncResult =
        match loginDataResult with
        | Ok loginData ->
            let asApiResponseObj =
                { RequestGuid = requestId
                  ErrorMessage = None
                  Data = Some loginData }
            (Successful.ok (json<ApiResponse<LoginData>> asApiResponseObj)) next ctx
        | Error error ->
            let asApiResponseObj =
                { RequestGuid = requestId
                  ErrorMessage = Some error
                  Data = None }
            (Successful.ok (json<ApiResponse<obj>> asApiResponseObj)) next ctx
       
        
        
[<RequireQualifiedAccess>]
module UserPatch =
    [<CLIMutable>]
    type PatchUserRequest =
        { Email: string | null
          FirstName: string | null
          LastName: string | null
          Password: string | null
          IsEmailEnabled: Nullable<bool> }
        
    let rec handler (next: HttpFunc) (ctx: HttpContext) : HttpFuncResult =
        let requestId: Guid =
            match ctx.Items.TryGetValue("requestId") with
            | true, value -> value :?> Guid
            | false, _ -> Guid.Empty
        
        let rawData : TaskResult<string, string> = 
            taskResult {
                let dbUserService = ctx.RequestServices.GetRequiredService<DbUserService>()
                let authTokenOptions = ctx.RequestServices.GetRequiredService<IOptions<AuthTokenOptions>>().Value
                let jsonSerializerOptions = ctx.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions
                
                use requestBodyReader = new StreamReader(ctx.Request.Body)
                let! requestBody = requestBodyReader.ReadToEndAsync()
                let patchUserReq = JsonSerializer.Deserialize<PatchUserRequest>(requestBody, jsonSerializerOptions)
                
                let! userEmail = tryGetUserEmail ctx
                let transformUser = transformUserWithPatch patchUserReq
                let! editedUser = dbUserService.TryEditUser(transformUser, userEmail)
                let accessToken = createAccessToken authTokenOptions.SigningKey editedUser
                return accessToken
            }
            
        Task.bind (transformRawData next ctx requestId) rawData
        
        
    and private transformUserWithPatch (patchUserReq: PatchUserRequest) (user: User): User =
        let newEmail = if isNull patchUserReq.Email then user.Email else patchUserReq.Email
        { user with
            Email = newEmail
            FirstName = if isNull patchUserReq.FirstName then user.FirstName else patchUserReq.FirstName
            LastName = if isNull patchUserReq.LastName then user.LastName else patchUserReq.LastName
            PasswordHash = if isNull patchUserReq.Password then user.PasswordHash else (hashPassword newEmail patchUserReq.Password)
            IsEmailEnabled = if patchUserReq.IsEmailEnabled.HasValue then patchUserReq.IsEmailEnabled.Value else user.IsEmailEnabled }
        
    and private transformRawData next ctx requestId result : HttpFuncResult =
        match result with
        | Ok refreshedAccessToken -> 
            let asApiResponseObj =
                { RequestGuid = requestId
                  ErrorMessage = None
                  Data = Some refreshedAccessToken }
            (Successful.ok (json<ApiResponse<string>> asApiResponseObj)) next ctx
        | Error error ->
            let asApiResponseObj =
                { RequestGuid = requestId
                  ErrorMessage = Some error
                  Data = None }
            (Successful.ok (json<ApiResponse<obj>> asApiResponseObj)) next ctx
        
        
        
[<RequireQualifiedAccess>]
module UserDelete =
    let rec handler (next: HttpFunc) (ctx: HttpContext) =
        let requestId: Guid =
            match ctx.Items.TryGetValue("requestId") with
            | true, value -> value :?> Guid
            | false, _ -> Guid.Empty
        
        let rawData : TaskResult<unit, string> = 
            taskResult {
                let dbUserService = ctx.RequestServices.GetRequiredService<DbUserService>()
                let! userEmail = tryGetUserEmail ctx
                do! dbUserService.TryDeleteUser(userEmail)
                return ()
            }
            
        Task.bind (transformRawData next ctx requestId) rawData
        

    and private transformRawData next ctx requestId result : HttpFuncResult =
        match result with
        | Ok _ -> 
            let asApiResponseObj =
                { RequestGuid = requestId
                  ErrorMessage = None
                  Data = Some "Successfully deleted user." }
            (Successful.ok (json<ApiResponse<string>> asApiResponseObj)) next ctx
        | Error error ->
            let asApiResponseObj =
                { RequestGuid = requestId
                  ErrorMessage = Some error
                  Data = None }
            (Successful.ok (json<ApiResponse<obj>> asApiResponseObj)) next ctx
