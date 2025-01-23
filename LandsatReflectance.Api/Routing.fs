module LandsatReflectance.Api.Routing

open LandsatReflectance.Api.Handlers.UserHandler.RefreshTokenLoginPost
open Microsoft.OpenApi.Models

open Giraffe.EndpointRouting
open Giraffe
open Giraffe.OpenApi

open LandsatReflectance.Api.Handlers.SceneHandler
open LandsatReflectance.Api.Handlers.UserHandler
open LandsatReflectance.Api.Handlers.UserTargetsHandler
open LandsatReflectance.Api.Handlers.UserTargetsHandler.UserTargetsPatch
open LandsatReflectance.Api.Handlers.UserTargetsHandler.UserTargetsPost
open LandsatReflectance.Api.Middleware.RequestIdMiddleware
open LandsatReflectance.Api.Models.ApiResponse
open LandsatReflectance.Api.Models.Usgs.Scene



let intSchema: OpenApiSchema = OpenApiSchema(Type = "integer", Format = "int32")

let stringSchema: OpenApiSchema = OpenApiSchema(Type = "string", Format = "string")

[<RequireQualifiedAccess>]
module private EndpointOpenApiConfigs =
    let PATCH_userTargetsEndpointConfig = 
        OpenApiConfig(
            responseBodies = [| ResponseBody(typeof<ApiResponse<SimplifiedTarget>>) |],
            requestBody = RequestBody(typeof<PatchTargetRequest>),
            configureOperation = (fun o ->
                o.Tags.Clear()
                let tag = OpenApiTag()
                tag.Name <- "targets"
                o.Tags.Add(tag)
                
                o.OperationId <- "PATCH_target"
                o.Summary <- "Attempts to edit a user's target.b"
                
                let securityRequirement = OpenApiSecurityRequirement()
                securityRequirement.Add(
                    OpenApiSecurityScheme(
                        Reference = OpenApiReference(
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        )
                    ),
                    [|  |] 
                )
                o.Security <- [| securityRequirement |]
                
                let pathParam = OpenApiParameter()
                pathParam.Name <- "target-id"
                pathParam.In <- ParameterLocation.Query
                pathParam.Required <- true
                pathParam.Schema <- stringSchema
                o.Parameters.Add(pathParam)
                
                o))
    
    let DELETE_userTargetsEndpointConfig = 
        OpenApiConfig(
            responseBodies = [| ResponseBody(typeof<ApiResponse<SimplifiedSceneData array>>) |],
            configureOperation = (fun o ->
                o.Tags.Clear()
                let tag = OpenApiTag()
                tag.Name <- "targets"
                o.Tags.Add(tag)
                
                o.OperationId <- "DELETE_target"
                o.Summary <- "Attempts to delete a user's target."
                
                
                let securityRequirement = OpenApiSecurityRequirement()
                securityRequirement.Add(
                    OpenApiSecurityScheme(
                        Reference = OpenApiReference(
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        )
                    ),
                    [|  |] 
                )
                o.Security <- [| securityRequirement |]
                
                let pathParam = OpenApiParameter()
                pathParam.Name <- "target-id"
                pathParam.In <- ParameterLocation.Query
                pathParam.Required <- true
                pathParam.Schema <- stringSchema
                o.Parameters.Add(pathParam)
                
                o))
        
        
    let GET_sceneDataStrEndpointConfig = 
        OpenApiConfig(
            responseBodies = [| ResponseBody(typeof<ApiResponse<SimplifiedSceneData array>>) |],
            configureOperation = (fun o ->
                o.Tags.Clear()
                let tag = OpenApiTag()
                tag.Name <- "scene"
                o.Tags.Add(tag)
                
                o.OperationId <- "GET_scene_data_str"
                o.Summary <- "Fetches an image as a base 64 encoded string."
                
                let resultsParam = OpenApiParameter()
                resultsParam.Name <- "product-id"
                resultsParam.In <- ParameterLocation.Query
                resultsParam.Schema <- stringSchema
                o.Parameters.Add(resultsParam)
                
                o))
    
    let sceneEndpointConfig = 
        OpenApiConfig(
            responseBodies = [| ResponseBody(typeof<ApiResponse<SimplifiedSceneData array>>) |],
            configureOperation = (fun o ->
                o.Tags.Clear()
                let tag = OpenApiTag()
                tag.Name <- "scene"
                o.Tags.Add(tag)
                
                o.OperationId <- "GET_scene"
                o.Summary <- "Fetches scene information"
                o.Description <-"Fetches landsat scene information from the **\"landsat_ot_c2_l2\"** database related to a specific path and row."
                
                
                (*
                let securityRequirement = OpenApiSecurityRequirement()
                securityRequirement.Add(
                    OpenApiSecurityScheme(
                        Reference = OpenApiReference(
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        )
                    ),
                    [|  |] 
                )
                o.Security <- [| securityRequirement |]
                *)
                
                let pathParam = OpenApiParameter()
                pathParam.Name <- "path"
                pathParam.In <- ParameterLocation.Query
                pathParam.Required <- true
                pathParam.Schema <- intSchema
                o.Parameters.Add(pathParam)
                
                let rowParam = OpenApiParameter()
                rowParam.Name <- "row"
                rowParam.In <- ParameterLocation.Query
                rowParam.Required <- true
                rowParam.Schema <- intSchema
                o.Parameters.Add(rowParam)
                
                let resultsParam = OpenApiParameter()
                resultsParam.Name <- "results"
                resultsParam.In <- ParameterLocation.Query
                resultsParam.Schema <- intSchema
                o.Parameters.Add(resultsParam)
                
                let resultsParam = OpenApiParameter()
                resultsParam.Name <- "skip"
                resultsParam.In <- ParameterLocation.Query
                resultsParam.Schema <- intSchema
                o.Parameters.Add(resultsParam)
                
                let resultsParam = OpenApiParameter()
                resultsParam.Name <- "min-cc"
                resultsParam.In <- ParameterLocation.Query
                resultsParam.Schema <- intSchema
                o.Parameters.Add(resultsParam)
                
                let resultsParam = OpenApiParameter()
                resultsParam.Name <- "max-cc"
                resultsParam.In <- ParameterLocation.Query
                resultsParam.Schema <- intSchema
                o.Parameters.Add(resultsParam)
                
                o))
        
    let POST_userEndpointConfig = 
        OpenApiConfig(
            responseBodies = [| ResponseBody(typeof<ApiResponse<LoginData>>) |],
            requestBody = RequestBody(typeof<UserLoginPost.LoginUserRequest>),
            configureOperation = (fun o ->
                o.Tags.Clear()
                let tag = OpenApiTag()
                tag.Name <- "user"
                o.Tags.Add(tag)
                
                o.OperationId <- "POST_user"
                o.Summary <- "Attempts to login a user, given credentials."
                o))
        
    let POST_createUserEndpointConfig = 
        OpenApiConfig(
            responseBodies = [| ResponseBody(typeof<ApiResponse<LoginData>>) |],
            requestBody = RequestBody(typeof<UserCreatePost.CreateUserRequest>),
            configureOperation = (fun o ->
                o.Tags.Clear()
                let tag = OpenApiTag()
                tag.Name <- "user"
                o.Tags.Add(tag)
                
                o.OperationId <- "POST_createUser"
                o.Summary <- "Attempts to create a user."
                o))
        
    let GET_userTargetsEndpointConfig = 
        OpenApiConfig(
            responseBodies = [| ResponseBody(typeof<ApiResponse<SimplifiedTarget list>>) |],
            configureOperation = (fun o ->
                o.Tags.Clear()
                let tag = OpenApiTag()
                tag.Name <- "targets"
                o.Tags.Add(tag)
                
                let securityRequirement = OpenApiSecurityRequirement()
                securityRequirement.Add(
                    OpenApiSecurityScheme(
                        Reference = OpenApiReference(
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        )
                    ),
                    [|  |] 
                )
                o.Security <- [| securityRequirement |]
                
                o.OperationId <- "GET_targets"
                o.Summary <- "Gets all emails bound to a specified user."
                o))
    
    let POST_userTargetsEndpointConfig = 
        OpenApiConfig(
            responseBodies = [| ResponseBody(typeof<ApiResponse<SimplifiedTarget>>) |],
            requestBody = RequestBody(typeof<CreateTargetRequest>),
            configureOperation = (fun o ->
                o.Tags.Clear()
                let tag = OpenApiTag()
                tag.Name <- "targets"
                o.Tags.Add(tag)
                
                let securityRequirement = OpenApiSecurityRequirement()
                securityRequirement.Add(
                    OpenApiSecurityScheme(
                        Reference = OpenApiReference(
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        )
                    ),
                    [|  |] 
                )
                o.Security <- [| securityRequirement |]
                
                o.OperationId <- "POST_targets"
                o.Summary <- "Attempts to create a target."
                o))
        
    let DELETE_userEndpointConfig = 
        OpenApiConfig(
            responseBodies = [| ResponseBody(typeof<ApiResponse<string>>) |],
            configureOperation = (fun o ->
                o.Tags.Clear()
                let tag = OpenApiTag()
                tag.Name <- "user"
                o.Tags.Add(tag)
                
                let securityRequirement = OpenApiSecurityRequirement()
                securityRequirement.Add(
                    OpenApiSecurityScheme(
                        Reference = OpenApiReference(
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        )
                    ),
                    [|  |] 
                )
                o.Security <- [| securityRequirement |]
                
                o.OperationId <- "DELETE_user"
                o.Summary <- "Attempts to delete a user"
                o.Description <- "The email of the account to delete is specified in the **auth token**, which is sent as part of the request."
                o))
        
    let PATCH_userEndpointConfig = 
        OpenApiConfig(
            responseBodies = [| ResponseBody(typeof<ApiResponse<string>>) |],
            requestBody = RequestBody(typeof<UserPatch.PatchUserRequest>),
            configureOperation = (fun o ->
                o.Tags.Clear()
                let tag = OpenApiTag()
                tag.Name <- "user"
                o.Tags.Add(tag)
                
                let securityRequirement = OpenApiSecurityRequirement()
                securityRequirement.Add(
                    OpenApiSecurityScheme(
                        Reference = OpenApiReference(
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        )
                    ),
                    [|  |] 
                )
                o.Security <- [| securityRequirement |]
                
                o.OperationId <- "PATCH_user"
                o.Summary <- "Attempts to edit a user."
                o))
        
    let POST_userRefreshTokenLoginEndpointConfig = 
        OpenApiConfig(
            responseBodies = [| ResponseBody(typeof<ApiResponse<string>>) |],
            requestBody = RequestBody(typeof<RefreshTokenLoginRequest>),
            configureOperation = (fun o ->
                o.Tags.Clear()
                let tag = OpenApiTag()
                tag.Name <- "user"
                o.Tags.Add(tag)
                
                let securityRequirement = OpenApiSecurityRequirement()
                securityRequirement.Add(
                    OpenApiSecurityScheme(
                        Reference = OpenApiReference(
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        )
                    ),
                    [|  |] 
                )
                o.Security <- [| securityRequirement |]
                
                o.OperationId <- "POST_user_refresh_token_login"
                o.Summary <- "Attempts generate an access token given a refresh tokenn."
                o))
        
        
        
let notLoggedIn =
    RequestErrors.UNAUTHORIZED
        "Bearer"
        "Some Realm"
        "You must be logged in."

let mustBeLoggedIn: HttpHandler = requiresAuthentication notLoggedIn
    
    
let endpoints: Routers.Endpoint list = [
    Routers.GET [
        
        // No auth requirements for scene endpoints because we would still like the user to be able to see scene information
        // without being logged in.
        
        Routers.route "/scene-data-str" (GET >=> requestIdMiddleware >=> SceneDataStr.handler)
        |> addOpenApi EndpointOpenApiConfigs.GET_sceneDataStrEndpointConfig
        
        Routers.route "/scene" (GET >=> requestIdMiddleware >=> SceneHandler.handler)
        |> addOpenApi EndpointOpenApiConfigs.sceneEndpointConfig
        
        Routers.route "/user/targets" (GET >=> mustBeLoggedIn >=> requestIdMiddleware >=> UserTargetsGet.handler)
        |> addOpenApi EndpointOpenApiConfigs.GET_userTargetsEndpointConfig
    ]
    
    Routers.POST [
        Routers.route "/user" (POST >=> requestIdMiddleware >=> UserLoginPost.handler)
        |> addOpenApi EndpointOpenApiConfigs.POST_userEndpointConfig
        
        Routers.route "/user/create" (POST >=> requestIdMiddleware >=> UserCreatePost.handler)
        |> addOpenApi EndpointOpenApiConfigs.POST_createUserEndpointConfig
        
        Routers.route "/user/refresh-token-login" (POST >=> requestIdMiddleware >=> RefreshTokenLoginPost.handler)
        |> addOpenApi EndpointOpenApiConfigs.POST_userRefreshTokenLoginEndpointConfig
        
        Routers.route "/user/targets" (POST >=> mustBeLoggedIn >=> requestIdMiddleware >=> UserTargetsPost.handler)
        |> addOpenApi EndpointOpenApiConfigs.POST_userTargetsEndpointConfig
    ]
    
    Routers.PATCH [
        Routers.route "/user" (PATCH >=> mustBeLoggedIn >=> requestIdMiddleware >=> UserPatch.handler)
        |> addOpenApi EndpointOpenApiConfigs.PATCH_userEndpointConfig
        
        Routers.route "/user/targets" (PATCH >=> mustBeLoggedIn >=> requestIdMiddleware >=> UserTargetsPatch.handler)
        |> addOpenApi EndpointOpenApiConfigs.PATCH_userTargetsEndpointConfig
    ]
    
    Routers.DELETE [
        Routers.route "/user" (DELETE >=> mustBeLoggedIn >=> requestIdMiddleware >=> UserDelete.handler)
        |> addOpenApi EndpointOpenApiConfigs.DELETE_userEndpointConfig
        
        Routers.route "user/targets" (DELETE >=> mustBeLoggedIn >=> requestIdMiddleware >=> UserTargetsDelete.handler)
        |> addOpenApi EndpointOpenApiConfigs.DELETE_userTargetsEndpointConfig
    ]
]
