using System.Diagnostics;
using IHub.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace IHub.Api.Exceptions;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
        logger.LogError(exception, "Unhandled request exception for {Path} TraceId={TraceId}", httpContext.Request.Path, traceId);

        var (statusCode, title) = exception switch
        {
            InvalidActivityException => (StatusCodes.Status400BadRequest, "Invalid activity"),
            InvalidStatementWindowException => (StatusCodes.Status400BadRequest, "Invalid statement window"),
            IntermediaryNotFoundException => (StatusCodes.Status404NotFound, "Intermediary not found"),
            DuplicateActivityConflictException or ActivityIdentityConstraintException =>
                (StatusCodes.Status409Conflict, "Activity conflict"),
            DomainException => (StatusCodes.Status400BadRequest, "Request failed"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error")
        };

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = statusCode == StatusCodes.Status500InternalServerError
                ? "An unexpected error occurred."
                : exception.Message,
            Instance = httpContext.Request.Path
        };
        problemDetails.Extensions["traceId"] = traceId;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
