using IHub.Api.Exceptions;
using IHub.Api.Health;
using IHub.Api.Middleware;
using IHub.Data;
using IHub.Services;
using IHub.Services.Telemetry;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");
builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .MinimumLevel.Information()
        .Enrich.FromLogContext()
        .WriteTo.Console(new JsonFormatter());

    // Enable after an OpenTelemetry Collector is running and configured to export logs to CloudWatch Logs.
    // Set OTEL_EXPORTER_OTLP_ENDPOINT to the Collector's OTLP gRPC endpoint (for example, http://localhost:4317).
    // var otlpEndpoint = context.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
    // if (!string.IsNullOrWhiteSpace(otlpEndpoint))
    // {
    //     configuration.WriteTo.OpenTelemetry(options => options.Endpoint = otlpEndpoint);
    // }
});

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation())
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter(ActivityMetrics.MeterName)
            .AddAspNetCoreInstrumentation()
            .AddPrometheusExporter();

        // Uncomment after adding OpenTelemetry.Exporter.OpenTelemetryProtocol (see IHub.Api.csproj).
        // The Collector must be configured to export received metrics to CloudWatch.
        // metrics.AddOtlpExporter();
    });

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
