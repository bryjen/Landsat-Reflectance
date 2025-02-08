open System
open System.IO
open System.Reflection
open System.Text
open System.Net.Http
open System.Net.Http.Headers
open FsLandsatApi.Utils
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Json
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.OpenApi.Models
open Microsoft.Extensions.Options
open Microsoft.IdentityModel.Tokens

open Giraffe
open Giraffe.EndpointRouting

open FsToolkit.ErrorHandling

open LandsatReflectance.Api.Services.PredictionService
open LandsatReflectance.Api.Extensions
open LandsatReflectance.Api.Handlers.NotFoundHandler
open LandsatReflectance.Api.Options
open LandsatReflectance.Api.Services.DbUserService
open LandsatReflectance.Api.Services.DbUserTargetService
open LandsatReflectance.Api.Services.UsgsSceneService
open LandsatReflectance.Api.Services.UsgsTokenService
open LandsatReflectance.Api.Utils.AppJsonSerializer
open LandsatReflectance.Api.Utils.UsgsHttpClient


    
let configureAppOptions (services: IServiceCollection) =
    let provider = services.BuildServiceProvider()
    let logger = provider.GetService<ILoggerFactory>().CreateLogger("ServiceConfiguration")
    
    // Configure option types
    let anyOptionInvalid = ref false
    services.TryAddUsgsOptions(logger, anyOptionInvalid) |> ignore
    services.TryAddDbOptions(logger, anyOptionInvalid) |> ignore
    services.TryAddTokenOptions(logger, anyOptionInvalid) |> ignore
    
    if anyOptionInvalid.Value then
        failwith "Startup configuration failed. Could not initialize some options"
        
    services
    
    
let configureAuth (services: IServiceCollection) =
    services.AddAuthentication(fun options ->
                options.DefaultAuthenticateScheme <- JwtBearerDefaults.AuthenticationScheme
                options.DefaultChallengeScheme <- JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(fun options ->
                // Configure 'TokenValidationParameters'
                let authTokenOptions =
                    match AuthTokenOptions.CreateTokenOptions() with
                    | Ok value -> value
                    | Error _ -> failwith "Could not get the authentication token options while configuring authentication, \"AuthTokenOptions\" could not be initialized."
                let signingKey = SymmetricSecurityKey(Encoding.UTF8.GetBytes(authTokenOptions.SigningKey));
                
                let tokenValidationParams = TokenValidationParameters()
                tokenValidationParams.ValidateIssuer <- true
                tokenValidationParams.ValidateAudience <- true
                tokenValidationParams.ValidateLifetime <- true
                tokenValidationParams.ValidateIssuerSigningKey <- true
                
                tokenValidationParams.ValidIssuer <- JwtTokens.issuer 
                tokenValidationParams.ValidAudience <- JwtTokens.audience
                tokenValidationParams.ClockSkew <- TimeSpan.FromMinutes(5.0)
                tokenValidationParams.IssuerSigningKey <- signingKey
                
                options.TokenValidationParameters <- tokenValidationParams
                
                (*
                let jwtBearerEvents = JwtBearerEvents()
                jwtBearerEvents.OnMessageReceived <- fun msgReceivedCtx ->
                    match msgReceivedCtx.Request.Headers.TryGetValue("X-Auth-Token") with
                    | true, value -> msgReceivedCtx.Token <- value
                    | false, _ -> ()
                    Task.CompletedTask
                
                options.Events <- jwtBearerEvents
                *)
                ())
    |> ignore
    
    services.AddAuthorization() |> ignore
    
    services
    
    
let configureServices (services: IServiceCollection) =
    services.ConfigureHttpJsonOptions(fun jsonOptions ->
        jsonOptions.SerializerOptions.PropertyNameCaseInsensitive <- true
        jsonOptions.SerializerOptions.WriteIndented <- true) |> ignore
    
    // TODO: Add a specific policy once the UI is deployed
    services.AddCors(fun options ->
        options.AddPolicy("AllowAll", fun policyOptions ->
            policyOptions.AllowAnyOrigin() |> ignore
            policyOptions.AllowAnyMethod() |> ignore
            policyOptions.AllowAnyHeader() |> ignore
            ())
        ())
    |> ignore
        
    // Configure http clients
    services.AddHttpClient<UsgsHttpClient>(fun httpClient ->
        httpClient.BaseAddress <- Uri("https://m2m.cr.usgs.gov/api/api/json/stable/")
        httpClient.DefaultRequestHeaders.Accept.Add(MediaTypeWithQualityHeaderValue("application/json")))
    |> ignore
    
    services.AddSingleton<UsgsTokenService>() |> ignore
    services.AddTransient<UsgsSceneService>() |> ignore
    services.AddTransient<PredictionService>() |> ignore
    services.AddScoped<DbUserService>() |> ignore
    services.AddScoped<DbUserTargetService>() |> ignore
    
    
    services.AddSingleton<PredictionsState>(fun serviceProvider ->
        let predictionState = PredictionsState()
        predictionState.Init(serviceProvider)
        |> _.GetAwaiter()
        |> _.GetResult()
        |> function
            | Ok _ -> predictionState
            | Error _ -> failwith "Failed to initialize \"PredictionsState\""
        ) |> ignore
    
        
    services.AddGiraffe() |> ignore
    services.AddSingleton<Json.ISerializer>(fun serviceProvider ->
        let jsonSerializerOptions = serviceProvider.GetRequiredService<IOptions<JsonOptions>>()
        AppJsonSerializer(jsonSerializerOptions.Value.SerializerOptions) :> Json.ISerializer) |> ignore
    
    services.AddRouting() |> ignore
    services.AddEndpointsApiExplorer() |> ignore
    services
    
    
let configureOpenApi (services: IServiceCollection) =
    services.AddSwaggerGen(fun config ->
        let openApiInfo = OpenApiInfo()
        openApiInfo.Title <- "FsLandsatApi"
        openApiInfo.Version <- "0.1.0"
        openApiInfo.Description <- "PLACEHOLDER"
        config.SwaggerDoc("v1", openApiInfo)
        
        // Auth config
        let securityScheme = OpenApiSecurityScheme()
        securityScheme.Name <- "Authorization"
        securityScheme.Type <- SecuritySchemeType.Http
        securityScheme.Scheme <- "bearer"
        securityScheme.BearerFormat <- "JWT"
        securityScheme.In <- ParameterLocation.Header
        securityScheme.Description <- "Enter the JWT token obtained from logging in."
        config.AddSecurityDefinition("Bearer", securityScheme)
        ())
    |> ignore
    services

let configureBuilder (webApplicationBuilder: WebApplicationBuilder) =
    webApplicationBuilder.Services
    |> configureAppOptions
    |> configureServices
    |> configureAuth
    |> configureOpenApi
    |> ignore
    
    webApplicationBuilder
    
let configureApp (app: IApplicationBuilder) =
    app.UseCors("AllowAll")
        
       .UseRouting()
       .UseSwagger()
       .UseSwaggerUI()
       
       .UseAuthentication()
       .UseAuthorization()
       
       .UseGiraffe(LandsatReflectance.Api.Routing.endpoints)
       .UseGiraffe(notFoundHandler)
       
    app
       
       
[<EntryPoint>]
let main args  =
    // let exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
    let exeDir = AppContext.BaseDirectory
    Directory.SetCurrentDirectory(exeDir)
    let options = WebApplicationOptions(ContentRootPath = exeDir)
    let builder = WebApplication.CreateBuilder(options)
    builder.Host.UseContentRoot(exeDir) |> ignore
    
    configureBuilder builder |> ignore
    
    let app = builder.Build()
    
    if app.Environment.IsDevelopment() then
        app.UseDeveloperExceptionPage() |> ignore
        
    // Force initialization of the predictions state at the start of the application.
    app.Services.GetRequiredService<PredictionsState>() |> ignore
    
    configureApp app |> ignore
    
    app.Run()
    0 // Exit code
