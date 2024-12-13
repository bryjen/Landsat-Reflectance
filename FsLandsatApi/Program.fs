namespace FsLandsatApi

open System
open System.Net.Http.Headers
open FsLandsatApi.Services
open FsLandsatApi.Services.UsgsSceneService
open FsLandsatApi.Services.UsgsTokenService
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

open Giraffe
open Extensions

module Program =
    
    let webApp =
        choose [
            route "/ping" >=> text "pong"
            route "/" >=> text "Hello World!"
        ]
    
    let configureApp (app: IApplicationBuilder) =
        app.UseGiraffe webApp
        
    let configureServices (services: IServiceCollection) =
        let provider = services.BuildServiceProvider()
        let logger = provider.GetService<ILoggerFactory>().CreateLogger("ServiceConfiguration")
        
        
        // Configure option types
        let anyOptionInvalid = ref false
        services.TryAddUsgsOptions(logger, anyOptionInvalid) |> ignore
        
        if anyOptionInvalid.Value then
            failwith "Startup configuration failed. Could not initialize some options"
            
            
        // Configure http clients
        services.AddHttpClient() |> ignore
        
        services.AddHttpClient("Usgs", fun httpClient ->
            httpClient.BaseAddress <- Uri("https://m2m.cr.usgs.gov/api/api/json/stable/")
            httpClient.DefaultRequestHeaders.Accept.Add(MediaTypeWithQualityHeaderValue("application/json")))
        |> ignore
        
        services.AddTransient<UsgsTokenService>() |> ignore
        services.AddTransient<UsgsSceneService>() |> ignore
            
        services.AddGiraffe |> ignore
        ()
    
    
    [<EntryPoint>]
    let main args =
        let builder = 
            Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(
                    fun webHostBuilder -> webHostBuilder.Configure(configureApp)
                                                        .ConfigureServices(configureServices)
                                                        |> ignore)
        let app = builder.Build()
        
        let something = app.Services.GetRequiredService<UsgsSceneService>()
        let somethingElse = something.GetScenes(14, 28, 10)
        
        match somethingElse with
        | Ok sceneDatas ->
            let sd = sceneDatas[0]
            printfn $"{sd.BrowseInfos}"
            printfn $"{sd.Metadata}"
            ()
        | Error errorValue ->
            ()
        
        app.Run()
        
        
        0 // Exit code
