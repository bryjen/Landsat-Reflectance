module FsLandsatApi.Extensions

open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging

open FsLandsatApi.Options
open Microsoft.Extensions.Options

        
type IServiceCollection with
    member services.TryAddUsgsOptions(logger: ILogger, anyOptionInvalid: bool ref) = 
        match UsgsOptions.CreateUsgsOptions() with
        | Ok usgsOptions ->
            usgsOptions.Print(logger.LogInformation)
            services.AddSingleton<IOptions<UsgsOptions>>(Options.Create(usgsOptions)) |> ignore
        | Error errorList ->
            anyOptionInvalid.contents <- true
            Array.iter (fun error -> logger.LogError(error.ToString())) errorList
        
        services
