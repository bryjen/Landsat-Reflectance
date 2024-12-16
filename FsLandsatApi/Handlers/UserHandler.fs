module FsLandsatApi.Handlers.UserHandler

open System
open System.IO
open System.IdentityModel.Tokens.Jwt
open System.Security.Claims
open System.Text
open System.Text.Json

open FsLandsatApi.Models.User
open FsLandsatApi.Options
open Microsoft.AspNetCore.Http.Json
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
        
        
[<AutoOpen>]
module private Tokens =
    let createJwtToken (secret: string) (user: User) =
        let signingKey = SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        let credentials = SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
        
        let claims = [|
            Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            Claim(JwtRegisteredClaimNames.Email, user.Email)
        |]
        
        // TODO: Properly setup issuer/audience before publishing
        let token = JwtSecurityToken(
            issuer = "FlatEarthers",
            audience = "FlatEarthers",
            claims = claims,
            notBefore = DateTime.UtcNow,
            expires = DateTime.UtcNow.AddHours(1),
            signingCredentials = credentials)
        
        JwtSecurityTokenHandler().WriteToken(token)
        
        
[<RequireQualifiedAccess>]
module UserLoginPost =
    [<CLIMutable>]
    type LoginUserRequest =
        { Email: string
          Password: string }
    
    let handler (next: HttpFunc) (ctx: HttpContext) : HttpFuncResult =
        task {
            let requestId: Guid =
                match ctx.Items.TryGetValue("requestId") with
                | true, value -> value :?> Guid
                | false, _ -> Guid.Empty
            
            let dbUserService = ctx.RequestServices.GetRequiredService<DbUserService>()
            let authTokenOptions = ctx.RequestServices.GetRequiredService<IOptions<AuthTokenOptions>>().Value
            let jsonSerializerOptions = ctx.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions
            
            use requestBodyReader = new StreamReader(ctx.Request.Body)
            let! requestBody = requestBodyReader.ReadToEndAsync()
            let registerUserRequest = JsonSerializer.Deserialize<LoginUserRequest>(requestBody, jsonSerializerOptions)
            
            let email = registerUserRequest.Email
            let password = registerUserRequest.Password
            
            return!
                dbUserService.TryGetUserByCredentials(email, password)
                |> Result.map (createJwtToken authTokenOptions.SigningKey)
                |> function
                    | Ok loginToken -> 
                        let asApiResponseObj =
                            { RequestGuid = requestId
                              ErrorMessage = None
                              Data = Some loginToken }
                        (Successful.ok (json<ApiResponse<string>> asApiResponseObj)) next ctx
                    | Error error ->
                        let asApiResponseObj =
                            { RequestGuid = requestId
                              ErrorMessage = Some error
                              Data = None }
                        (Successful.ok (json<ApiResponse<obj>> asApiResponseObj)) next ctx
        }
       
       
[<RequireQualifiedAccess>]
module UserCreatePost =
    [<CLIMutable>]
    type CreateUserRequest =
        { Email: string
          Password: string
          IsEmailEnabled: bool }
    
    let handler (next: HttpFunc) (ctx: HttpContext) : HttpFuncResult =
        task {
            let requestId: Guid =
                match ctx.Items.TryGetValue("requestId") with
                | true, value -> value :?> Guid
                | false, _ -> Guid.Empty
            
            let dbUserService = ctx.RequestServices.GetRequiredService<DbUserService>()
            let authTokenOptions = ctx.RequestServices.GetRequiredService<IOptions<AuthTokenOptions>>().Value
            let jsonSerializerOptions = ctx.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions
            
            use requestBodyReader = new StreamReader(ctx.Request.Body)
            let! requestBody = requestBodyReader.ReadToEndAsync()
            let createRequest = JsonSerializer.Deserialize<CreateUserRequest>(requestBody, jsonSerializerOptions)
           
            return!
                dbUserService.TryCreateUser(createRequest.Email, createRequest.Password, createRequest.IsEmailEnabled)
                |> Result.map (createJwtToken authTokenOptions.SigningKey)
                |> function
                    | Ok loginToken -> 
                        let asApiResponseObj =
                            { RequestGuid = requestId
                              ErrorMessage = None
                              Data = Some loginToken }
                        (Successful.ok (json<ApiResponse<string>> asApiResponseObj)) next ctx
                    | Error error ->
                        let asApiResponseObj =
                            { RequestGuid = requestId
                              ErrorMessage = Some error
                              Data = None }
                        (Successful.ok (json<ApiResponse<obj>> asApiResponseObj)) next ctx
        }
       
        
[<RequireQualifiedAccess>]
module private UserPatch =
    let handler (next: HttpFunc) (ctx: HttpContext) =
        failwith "todo"
        
[<RequireQualifiedAccess>]
module private UserDelete =
    let handler (next: HttpFunc) (ctx: HttpContext) =
        failwith "todo"
