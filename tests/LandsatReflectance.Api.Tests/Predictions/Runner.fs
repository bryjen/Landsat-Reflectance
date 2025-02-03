// File used to run some code through the test framework.

module LandsatReflectance.Api.Tests.Predictions.Runner

open System.Net.Http
open LandsatReflectance.Api.Services
open LandsatReflectance.Api.Services.PredictionService
open LandsatReflectance.Api.Services.UsgsTokenService
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open NUnit.Framework
open Program

let mutable testServer: TestServer | null = null
let mutable httpClient: HttpClient | null = null

[<OneTimeSetUp>]
let Setup () =
    let builder = WebApplication.CreateBuilder([||])
    configureBuilder builder |> ignore
    builder.WebHost.UseTestServer() |> ignore
    
    let app = builder.Build()
    configureApp app |> ignore
    app.StartAsync().GetAwaiter().GetResult()
    
    testServer <- app.GetTestServer()
    httpClient <- testServer.CreateClient()
    ()
    
[<OneTimeTearDown>]
let Dispose () =
    testServer.Dispose()
    httpClient.Dispose()
    ()

[<Test>]
let Runner1 () =
    task {
        // TODO: See why below doesn't work
        // use scope = testServer.Host.Services.CreateScope()
        // let predictionService = scope.ServiceProvider.GetRequiredService<PredictionService>()
        
        let predictionService = testServer.Services.GetRequiredService<PredictionService>()
        let! predictionResult = predictionService.GetPrediction(14, 28)
        
        match predictionResult with
        | Ok prediction ->
            printfn $"{prediction.Path}\n{prediction.Row}\n{prediction.PredictedSatellite}\n{prediction.PredictedTimeUtc}\n{prediction.PredictedTimeUtcWeightedMean}\n{prediction.PredictedTimeUtcWeightedStdDev}"
            Assert.Pass()
        | Error error ->
            Assert.Fail(error)
        return ()
    }

[<Test>]
let Runner2 () =
    task {
        let predictionService = testServer.Services.GetRequiredService<PredictionService>()
        let! normalDistributionParametersResult = predictionService.GetNormalProbabilityDistributionMetrics(14, 28)
        
        match normalDistributionParametersResult with
        | Ok normalDistributionParameters ->
            printfn
                $"Landsat 8 Mean:\t\t{normalDistributionParameters.Landsat8Mean:G}\nLandsat 8 Std dev:\t{normalDistributionParameters.Landsat8StdDev:G}
                \nLandsat 9 Mean:\t\t{normalDistributionParameters.Landsat9Mean:G}\nLandsat 9 Std dev:\t{normalDistributionParameters.Landsat9StdDev:G}\n"
            Assert.Pass()
        | Error error ->
            Assert.Fail(error)
        return ()
    }
