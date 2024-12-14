module FsLandsatApi.Routing

open FsLandsatApi.Handlers.SceneHandler
open FsLandsatApi.Middleware.RequestIdMiddleware
open FsLandsatApi.Models.ApiResponse
open FsLandsatApi.Models.Usgs.Scene
open Giraffe.EndpointRouting
open Microsoft.AspNetCore.Http
open Giraffe
open Giraffe.OpenApi
open Microsoft.OpenApi.Models


let intSchema: OpenApiSchema = OpenApiSchema(Type = "integer", Format = "int32")

[<RequireQualifiedAccess>]
module private EndpointOpenApiConfigs =
    let sceneEndpointConfig = 
        OpenApiConfig(
            responseBodies = [| ResponseBody(typeof<ApiResponse<SimplifiedSceneData array>>) |],
            configureOperation = (fun o ->
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

let sceneEndpoint: Routers.Endpoint = 
    Routers.route "/scene" (requestIdMiddleware >=> sceneHandler)
    |> addOpenApi EndpointOpenApiConfigs.sceneEndpointConfig
    
    
let endpoints: Routers.Endpoint list = [
    Routers.GET [
        sceneEndpoint
    ]
]
