using System.Diagnostics;

namespace IHub.Api.Middleware;

public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await next(context);
        }
        finally
        {
            var statusCode = context.Response.StatusCode;
            var logLevel = statusCode >= StatusCodes.Status500InternalServerError
                ? LogLevel.Error
                : statusCode >= StatusCodes.Status400BadRequest
                    ? LogLevel.Warning
                    : LogLevel.Information;

            logger.Log(logLevel,
                "HTTP request completed. TraceId={TraceId} Method={Method} Path={Path} StatusCode={StatusCode} DurationMs={DurationMs}",
                Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier,
                context.Request.Method,
                context.Request.Path,
                statusCode,
                stopwatch.Elapsed.TotalMilliseconds);
        }
    }
}