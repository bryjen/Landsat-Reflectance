open System
open System.Net.Http
open System.Net.Http.Headers
open System.Text.Json
open FsLandsatApi.Handlers.NotFoundHandler
open FsLandsatApi.Services.UsgsSceneService
open FsLandsatApi.Services.UsgsTokenService
open FsLandsatApi.Utils
open FsLandsatApi.Utils.UsgsHttpClient
open Giraffe.EndpointRouting
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

open Giraffe

open FsLandsatApi.Extensions

// Need to create custom type that wraps around 'System.Text.JsonSerializer' cause the program just doesn't work for
// some reason.
type AppJsonSerializer() =
    interface Json.ISerializer with
        member this.SerializeToString<'T>(x: 'T) =
            JsonSerializer.Serialize<'T>(x)
            
        member this.SerializeToBytes<'T>(x: 'T) =
            JsonSerializer.SerializeToUtf8Bytes<'T>(x)
            
        member this.SerializeToStreamAsync<'T> (x: 'T) stream =
            JsonSerializer.SerializeAsync(stream, x)
        
        member this.Deserialize<'T>(bytes: byte array): 'T =
            JsonSerializer.Deserialize<'T>(bytes)
            
        member this.Deserialize<'T>(json: string): 'T =
            JsonSerializer.Deserialize<'T>(json)
            
        member this.DeserializeAsync(stream) =
            JsonSerializer.DeserializeAsync(stream).AsTask()


let configureApp (app: IApplicationBuilder) =
    app.UseRouting()
       .UseSwagger()
       .UseSwaggerUI()
       .UseGiraffe(FsLandsatApi.Routing.endpoints)
       .UseGiraffe(notFoundHandler)
    
    
    
let configureServices (services: IServiceCollection) =
    let provider = services.BuildServiceProvider()
    let logger = provider.GetService<ILoggerFactory>().CreateLogger("ServiceConfiguration")
    
    
    // Configure option types
    let anyOptionInvalid = ref false
    services.TryAddUsgsOptions(logger, anyOptionInvalid) |> ignore
    
    if anyOptionInvalid.Value then
        failwith "Startup configuration failed. Could not initialize some options"
        
        
    // Configure http clients
    services.AddHttpClient<UsgsHttpClient>(fun httpClient ->
        httpClient.BaseAddress <- Uri("https://m2m.cr.usgs.gov/api/api/json/stable/")
        httpClient.DefaultRequestHeaders.Accept.Add(MediaTypeWithQualityHeaderValue("application/json")))
    |> ignore
    
    services.AddSingleton<UsgsTokenService>() |> ignore
    services.AddTransient<UsgsSceneService>() |> ignore
        
    services.AddGiraffe |> ignore
    services.AddSingleton<Json.ISerializer>(fun serviceProvider -> AppJsonSerializer() :> Json.ISerializer) |> ignore
    
    services.AddRouting() |> ignore
    services.AddEndpointsApiExplorer() |> ignore
    services.AddSwaggerGen() |> ignore
    ()


[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    configureServices builder.Services
    
    let app = builder.Build()
    
    if app.Environment.IsDevelopment() then
        app.UseDeveloperExceptionPage() |> ignore

    configureApp app
    app.Run()
    
    0 // Exit code
