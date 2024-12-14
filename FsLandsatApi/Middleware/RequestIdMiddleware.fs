module FsLandsatApi.Middleware.RequestIdMiddleware

open System
open Giraffe
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging

let requestIdMiddleware (next: HttpFunc) (ctx: HttpContext) =
    
    let loggerFactory = ctx.RequestServices.GetRequiredService<ILoggerFactory>()
    let logger = loggerFactory.CreateLogger("FsLandsatApi.Middleware.RequestIdMiddleware")
    
    let requestId = Guid.NewGuid()
    ctx.Items["requestId"] <- requestId
    
    let scheme = ctx.Request.Scheme
    let host = ctx.Request.Host.Value
    let path = ctx.Request.Path.Value
    let query = ctx.Request.QueryString.Value
    logger.LogInformation($"[{requestId}] Tagged inbound request to \"{scheme}://{host}{path}{query}\" with a request id.")
    
    next ctx