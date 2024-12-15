module FsLandsatApi.Routing

open FsLandsatApi.Handlers.SceneHandler
open FsLandsatApi.Handlers.UserHandler
open FsLandsatApi.Middleware.RequestIdMiddleware
open FsLandsatApi.Models.ApiResponse
open FsLandsatApi.Models.Usgs.Scene
open Giraffe.EndpointRouting
open Giraffe
open Giraffe.OpenApi
open Microsoft.OpenApi.Models
open Microsoft.AspNetCore.Builder



let intSchema: OpenApiSchema = OpenApiSchema(Type = "integer", Format = "int32")

[<RequireQualifiedAccess>]
module private EndpointOpenApiConfigs =
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
                o))
        
    let POST_userEndpointConfig = 
        OpenApiConfig(
            responseBodies = [| ResponseBody(typeof<ApiResponse<string>>) |],
            requestBody = RequestBody(typeof<LoginPostMethod.LoginUserRequest>),
            configureOperation = (fun o ->
                o.Tags.Clear()
                let tag = OpenApiTag()
                tag.Name <- "user"
                o.Tags.Add(tag)
                
                o.OperationId <- "POST_user"
                o.Summary <- "Attempts to login a user, given credentials"
                o))
        
    let POST_createUserEndpointConfig = 
        OpenApiConfig(
            responseBodies = [| ResponseBody(typeof<ApiResponse<string>>) |],
            requestBody = RequestBody(typeof<CreatePostMethod.CreateUserRequest>),
            configureOperation = (fun o ->
                o.Tags.Clear()
                let tag = OpenApiTag()
                tag.Name <- "user"
                o.Tags.Add(tag)
                
                o.OperationId <- "POST_createUser"
                o.Summary <- "Attempts to create a user"
                o))

let sceneEndpoint: Routers.Endpoint = 
    Routers.route "/scene" (requestIdMiddleware >=> sceneHandler)
    |> addOpenApi EndpointOpenApiConfigs.sceneEndpointConfig
    
let userEndpoint: Routers.Endpoint = 
    Routers.route "/user" (requestIdMiddleware >=> userHandler)
    |> addOpenApi EndpointOpenApiConfigs.POST_userEndpointConfig
    
let createUserEndpoint: Routers.Endpoint = 
    Routers.route "/user/create" (requestIdMiddleware >=> userCreateHandler)
    |> addOpenApi EndpointOpenApiConfigs.POST_createUserEndpointConfig
    
    
let endpoints: Routers.Endpoint list = [
    Routers.GET [
        sceneEndpoint
    ]
    Routers.POST [
        userEndpoint
        createUserEndpoint
    ]
]
