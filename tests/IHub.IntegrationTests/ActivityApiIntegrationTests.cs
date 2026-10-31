using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IHub.Application.DTOs;
using IHub.Data.Persistence;
using IHub.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IHub.IntegrationTests;

public sealed class ActivityApiIntegrationTests : IAsyncLifetime
{
    private static readonly string[] ExistingIntermediaryIds =
    [
        "BROKER-1", "BROKER-2", "BROKER-3", "BROKER-4", "BROKER-5", "BROKER-6",
        "BROKER-7", "BROKER-8", "BROKER-9", "BROKER-10", "BROKER-UTC", "BROKER-METRICS"
    ];

    private readonly CustomWebApplicationFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IHubDbContext>();
        dbContext.Intermediaries.AddRange(ExistingIntermediaryIds.Select(intermediaryId => new Intermediary
        {
            IntermediaryId = intermediaryId,
            CreatedAtUtc = DateTimeOffset.UtcNow
        }));
        await dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task SuccessfulIngestion_IsReturnedInStatement()
    {
        var response = await PostActivityAsync("PolicyAdmin", "ACT-10001", "BROKER-1", "2026-10-15T10:30:00Z", 1250.50m);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var statement = await GetStatementAsync("BROKER-1", "2026-10-01T00:00:00Z", "2026-11-01T00:00:00Z");
        Assert.Equal(1250.50m, statement.GrossTotal);
        Assert.Equal("ZAR", statement.Currency);
        Assert.NotNull(statement.LastUpdated);
    }

    [Fact]
    public async Task NonUtcTimestamps_AreStoredAndReturnedInUtc()
    {
        var creation = await PostActivityAsync("PolicyAdmin", "ACT-OFFSET", "BROKER-UTC", "2026-10-15T12:30:00+02:00", 75m);
        Assert.Equal(HttpStatusCode.Created, creation.StatusCode);

        var activity = (await creation.Content.ReadFromJsonAsync<ActivityResponse>())!;
        Assert.Equal(TimeSpan.Zero, activity.OccurredAt.Offset);
        Assert.Equal(DateTimeOffset.Parse("2026-10-15T10:30:00Z"), activity.OccurredAt);

        var statement = await GetStatementAsync(
            "BROKER-UTC",
            "2026-10-01T02:00:00+02:00",
            "2026-11-01T02:00:00+02:00");

        Assert.Equal(TimeSpan.Zero, statement.Start.Offset);
        Assert.Equal(TimeSpan.Zero, statement.End.Offset);
        Assert.Equal(DateTimeOffset.Parse("2026-10-01T00:00:00Z"), statement.Start);
        Assert.Equal(DateTimeOffset.Parse("2026-11-01T00:00:00Z"), statement.End);
        Assert.Equal(75m, statement.GrossTotal);
    }

    [Fact]
    public async Task IdenticalDuplicate_ContributesOnce()
    {
        var first = await PostActivityAsync("PolicyAdmin", "ACT-DUP", "BROKER-2", "2026-10-20T09:00:00Z", 100m);
        var duplicate = await PostActivityAsync("PolicyAdmin", "ACT-DUP", "BROKER-2", "2026-10-20T09:00:00Z", 100m);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        var statement = await GetStatementAsync("BROKER-2", "2026-10-01T00:00:00Z", "2026-11-01T00:00:00Z");
        Assert.Equal(100m, statement.GrossTotal);
    }

    [Fact]
    public async Task ConflictingDuplicate_DoesNotOverwriteOriginal()
    {
        await PostActivityAsync("PolicyAdmin", "ACT-CONFLICT", "BROKER-3", "2026-10-15T10:00:00Z", 90m);
        var conflict = await PostActivityAsync("PolicyAdmin", "ACT-CONFLICT", "BROKER-3", "2026-10-15T10:00:00Z", 120m);

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var statement = await GetStatementAsync("BROKER-3", "2026-10-01T00:00:00Z", "2026-11-01T00:00:00Z");
        Assert.Equal(90m, statement.GrossTotal);
    }

    [Fact]
    public async Task SameExternalIdFromDifferentSources_IsAcceptedSeparately()
    {
        Assert.Equal(HttpStatusCode.Created, (await PostActivityAsync("PolicyAdmin", "ACT-SHARED", "BROKER-4", "2026-10-15T10:00:00Z", 10m)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await PostActivityAsync("Claims", "ACT-SHARED", "BROKER-4", "2026-10-15T10:00:00Z", 20m)).StatusCode);

        var statement = await GetStatementAsync("BROKER-4", "2026-10-01T00:00:00Z", "2026-11-01T00:00:00Z");
        Assert.Equal(30m, statement.GrossTotal);
    }

    [Fact]
    public async Task OutOfOrderActivities_AreIncludedByOccurredAt()
    {
        await PostActivityAsync("PolicyAdmin", "ACT-LATE-1", "BROKER-5", "2026-10-25T10:00:00Z", 15m);
        await PostActivityAsync("PolicyAdmin", "ACT-LATE-2", "BROKER-5", "2026-10-03T10:00:00Z", 25m);

        var statement = await GetStatementAsync("BROKER-5", "2026-10-01T00:00:00Z", "2026-11-01T00:00:00Z");
        Assert.Equal(40m, statement.GrossTotal);
    }

    [Fact]
    public async Task StatementWindow_IsStartInclusiveAndEndExclusive()
    {
        await PostActivityAsync("PolicyAdmin", "ACT-START", "BROKER-6", "2026-10-01T00:00:00Z", 10m);
        await PostActivityAsync("PolicyAdmin", "ACT-MIDDLE", "BROKER-6", "2026-10-15T00:00:00Z", 20m);
        await PostActivityAsync("PolicyAdmin", "ACT-END", "BROKER-6", "2026-11-01T00:00:00Z", 40m);

        var statement = await GetStatementAsync("BROKER-6", "2026-10-01T00:00:00Z", "2026-11-01T00:00:00Z");
        Assert.Equal(30m, statement.GrossTotal);
    }

    [Fact]
    public async Task IngestionWithoutCurrency_UsesZar()
    {
        Assert.Equal(HttpStatusCode.Created, (await PostActivityAsync("PolicyAdmin", "ACT-ZAR", "BROKER-7", "2026-10-15T10:00:00Z", 10m)).StatusCode);

        var statement = await GetStatementAsync("BROKER-7", "2026-10-01T00:00:00Z", "2026-11-01T00:00:00Z");
        Assert.Equal(10m, statement.GrossTotal);
        Assert.Equal("ZAR", statement.Currency);
    }

    [Fact]
    public async Task UnknownIntermediary_IsNotCreatedOrIngested()
    {
        const string intermediaryId = "BROKER-UNKNOWN";
        var response = await PostActivityAsync("PolicyAdmin", "ACT-UNKNOWN-INTERMEDIARY", intermediaryId, "2026-10-15T10:00:00Z", 10m);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IHubDbContext>();
        Assert.False(await dbContext.Intermediaries.AnyAsync(x => x.IntermediaryId == intermediaryId));
        Assert.False(await dbContext.Activities.AnyAsync(x => x.ExternalActivityId == "ACT-UNKNOWN-INTERMEDIARY"));
    }

    [Fact]
    public async Task EmptyStatement_ReturnsZeroAndNullLastUpdated()
    {
        var statement = await GetStatementAsync("BROKER-EMPTY", "2026-10-01T00:00:00Z", "2026-11-01T00:00:00Z");

        Assert.Equal(0m, statement.GrossTotal);
        Assert.Null(statement.LastUpdated);
    }

    [Fact]
    public async Task InvalidActivityAndWindow_AreRejectedWithoutChangingTotals()
    {
        var invalidActivity = await PostActivityAsync("PolicyAdmin", "ACT-INVALID", "BROKER-8", "2026-10-15T10:00:00Z", -1m);
        var invalidWindow = await _client.GetAsync("/intermediaries/BROKER-8/statement?start=2026-10-10T00:00:00Z&end=2026-10-10T00:00:00Z");

        Assert.Equal(HttpStatusCode.BadRequest, invalidActivity.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidWindow.StatusCode);
        await AssertHasTraceIdAsync(invalidActivity);
        await AssertHasTraceIdAsync(invalidWindow);
        var statement = await GetStatementAsync("BROKER-8", "2026-10-01T00:00:00Z", "2026-11-01T00:00:00Z");
        Assert.Equal(0m, statement.GrossTotal);
    }

    [Fact]
    public async Task ConcurrentDuplicateRequests_AreHandledByTheInMemoryHost()
    {
        await PostActivityAsync("PolicyAdmin", "ACT-SEED-DUP", "BROKER-9", "2026-10-15T09:00:00Z", 1m);
        var submissions = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            PostActivityAsync("PolicyAdmin", "ACT-CONCURRENT", "BROKER-9", "2026-10-15T10:00:00Z", 50m)));

        Assert.All(submissions, response => Assert.True(response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK));
        var statement = await GetStatementAsync("BROKER-9", "2026-10-01T00:00:00Z", "2026-11-01T00:00:00Z");
        Assert.InRange(statement.GrossTotal, 51m, 401m);
    }

    [Fact]
    public async Task ConcurrentDistinctActivities_AreAllIncluded()
    {
        await PostActivityAsync("PolicyAdmin", "ACT-SEED-DISTINCT", "BROKER-10", "2026-10-15T09:00:00Z", 1m);
        var submissions = await Task.WhenAll(Enumerable.Range(0, 8).Select(index =>
            PostActivityAsync("PolicyAdmin", $"ACT-DISTINCT-{index}", "BROKER-10", "2026-10-15T10:00:00Z", 5m)));

        Assert.All(submissions, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        var statement = await GetStatementAsync("BROKER-10", "2026-10-01T00:00:00Z", "2026-11-01T00:00:00Z");
        Assert.Equal(41m, statement.GrossTotal);
    }

    [Fact]
    public async Task HealthAndMetricsEndpoints_AreAvailable()
    {
        var healthResponse = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);
        Assert.Contains("healthy", await healthResponse.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var activityResponse = await PostActivityAsync("PolicyAdmin", "ACT-METRICS", "BROKER-METRICS", "2026-10-15T10:00:00Z", 10m);
        Assert.Equal(HttpStatusCode.Created, activityResponse.StatusCode);

        var metricsResponse = await _client.GetAsync("/metrics");
        Assert.Equal(HttpStatusCode.OK, metricsResponse.StatusCode);
        var metrics = await metricsResponse.Content.ReadAsStringAsync();
        Assert.Contains("ihub_activities_accepted_total", metrics, StringComparison.Ordinal);
    }

    private Task<HttpResponseMessage> PostActivityAsync(
        string sourceSystem,
        string externalActivityId,
        string intermediaryId,
        string occurredAt,
        decimal amount)
    {
        return _client.PostAsJsonAsync("/activities", new CreateActivityRequest
        {
            SourceSystem = sourceSystem,
            ExternalActivityId = externalActivityId,
            IntermediaryId = intermediaryId,
            OccurredAt = DateTimeOffset.Parse(occurredAt),
            Amount = amount
        });
    }

    private async Task<StatementResponse> GetStatementAsync(string intermediaryId, string start, string end)
    {
        var response = await _client.GetAsync($"/intermediaries/{intermediaryId}/statement?start={Uri.EscapeDataString(start)}&end={Uri.EscapeDataString(end)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<StatementResponse>())!;
    }

    private static async Task AssertHasTraceIdAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var traceId = problem.RootElement.GetProperty("traceId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(traceId));
    }
}

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"ihub-tests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("DatabaseProvider", "InMemory");
        builder.UseSetting("InMemoryDatabaseName", _databaseName);
    }
}