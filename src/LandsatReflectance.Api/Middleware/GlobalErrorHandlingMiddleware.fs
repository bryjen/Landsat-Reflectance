module LandsatReflectance.Api.Middleware.GlobalErrorHandlingMiddleware

open System

open System.Collections.Generic
open System.IO
open System.Threading.Tasks
open Giraffe.ViewEngine
open Microsoft.AspNetCore.Http

open Giraffe

open LandsatReflectance.Api.Models.ApiResponse
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Primitives

let getLogger (ctx: HttpContext) =
    try
        let loggerFactory = ctx.RequestServices.GetRequiredService<ILoggerFactory>()
        let logger = loggerFactory.CreateLogger("LandsatReflectance.Api.Middleware.GlobalErrorHandlingMiddleware")
        logger.LogWarning
    with
    | _ ->
        let log (message: string) = printfn $"{message}"
        log
        

let rec globalErrorHandlingMiddleware (next: HttpFunc) (ctx: HttpContext) : HttpFuncResult =
    let requestId: Guid =
        match ctx.Items.TryGetValue("requestId") with
        | true, value -> value :?> Guid
        | false, _ -> Guid.Empty
        
    try
        next ctx
    with
    | ex ->
        task {
            let apiResponse: ApiResponse<string> =
                { RequestGuid = requestId
                  ErrorMessage = Some "An internal error occurred."
                  Data = None }
                
            do! logHttpCtx ctx ex requestId
            let httpHandler : HttpHandler = setStatusCode 500 >=> (json<ApiResponse<string>> apiResponse)
            return! httpHandler earlyReturn ctx
        }
        
        
and logHttpCtx ctx ex requestId : Task =
        task {
            let loggerFactory = ctx.RequestServices.GetRequiredService<ILoggerFactory>()
            let logger = loggerFactory.CreateLogger("LandsatReflectance.Api.Middleware.GlobalErrorHandlingMiddleware")
                
            logger.LogWarning $"[{requestId}] An uncaught exception occurred with message \"{ex.Message}\". Check the debug log for more information."
            logger.LogDebug $"[{requestId}] Uncaught exception stack trace: {ex.ToString()}"
            
            
            logger.LogDebug $"[{requestId}] Uncaught exception uri: \"{ctx.GetRequestUrl()}\"."
            
            use bodyStream = new StreamReader(ctx.Request.Body)
            let! body = bodyStream.ReadToEndAsync()
            logger.LogDebug $"[{requestId}] Uncaught exception request body: \"{body}\"."
            
            let headers = Seq.map (fun (tuple: KeyValuePair<string, StringValues>) -> $"\t{tuple.Key} - {tuple.Value}") ctx.Request.Headers
            let asSingleString = String.Join("\n", headers)
            logger.LogDebug $"[{requestId}] Uncaught exception request headers:\n{asSingleString}."
            
            logger.LogDebug $"[{requestId}] Uncaught exception extra info: \"{ctx.Request.Method}\"; \"{ctx.Request.Protocol}\"; \"{ctx.Request.Host}\"."
        }
    


let generateSyntheticInternalErrorHandler (next: HttpFunc) (ctx: HttpContext) : HttpFuncResult =
    failwith "Intentionally generated uncaught error"
