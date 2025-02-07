// For more information see https://aka.ms/fsharp-console-apps

open FsToolkit.ErrorHandling
open Microsoft.AspNetCore.Builder
open LandsatReflectance.Api.Services.PredictionService
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.DependencyInjection
open Program


let builder = WebApplication.CreateBuilder([||])
configureBuilder builder |> ignore
builder.WebHost.UseTestServer() |> ignore

let app = builder.Build()
configureApp app |> ignore
app.StartAsync().GetAwaiter().GetResult()

let testServer = app.GetTestServer()
let httpClient = testServer.CreateClient()

taskResult {
    let scope = testServer.Services.CreateScope()
    return! PredictionsState.Init(scope.ServiceProvider)
    return ()
}
|> _.GetAwaiter()
|> _.GetResult()
|> function
    | Ok _ -> printfn "pass"
    | Error errorMsg -> printfn $"fail: \"{errorMsg}\""