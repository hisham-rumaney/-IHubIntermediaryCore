using IHub.Api.Exceptions;
using IHub.Api.Health;
using IHub.Api.Middleware;
using IHub.Data;
using IHub.Services;
using IHub.Services.Telemetry;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");
builder.Logging.AddOpenTelemetry(options =>
{
    options.IncludeFormattedMessage = true;
    options.IncludeScopes = true;
});

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation())
    .WithMetrics(metrics => metrics
        .AddMeter(ActivityMetrics.MeterName)
        .AddAspNetCoreInstrumentation()
        .AddPrometheusExporter());

builder.Services.AddDataLayer(builder.Configuration);
builder.Services.AddServicesLayer();

var app = builder.Build();

await app.Services.InitializeDatabaseAsync();

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseExceptionHandler();
app.UseHttpsRedirection();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    },
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString().ToLowerInvariant()
        });
    }
});

app.MapPrometheusScrapingEndpoint("/metrics");
app.MapControllers();

app.Run();

public partial class Program { }
