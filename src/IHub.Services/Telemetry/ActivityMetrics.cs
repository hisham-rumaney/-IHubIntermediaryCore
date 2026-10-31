using System.Diagnostics.Metrics;

namespace IHub.Services.Telemetry;

public static class ActivityMetrics
{
    public const string MeterName = "IHub.Services";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> AcceptedActivities = Meter.CreateCounter<long>(
        "ihub.activities.accepted",
        description: "Activities successfully accepted.");
    private static readonly Counter<long> DuplicateSubmissions = Meter.CreateCounter<long>(
        "ihub.activities.duplicates",
        description: "Identical activity submissions.");
    private static readonly Counter<long> ConflictingDuplicates = Meter.CreateCounter<long>(
        "ihub.activities.conflicts",
        description: "Submissions conflicting with an existing external identity.");
    private static readonly Counter<long> FailedIngestionAttempts = Meter.CreateCounter<long>(
        "ihub.activities.ingestion.failures",
        description: "Activity ingestion attempts that failed.");
    private static readonly Counter<long> RequestErrors = Meter.CreateCounter<long>(
        "ihub.requests.errors",
        description: "HTTP requests handled with an error.");
    private static readonly Histogram<double> ProcessingDelay = Meter.CreateHistogram<double>(
        "ihub.activities.processing_delay",
        unit: "ms",
        description: "Elapsed time from occurrence to ingestion.");

    public static void RecordAccepted() => AcceptedActivities.Add(1);
    public static void RecordDuplicate() => DuplicateSubmissions.Add(1);
    public static void RecordConflict() => ConflictingDuplicates.Add(1);
    public static void RecordIngestionFailure() => FailedIngestionAttempts.Add(1);
    public static void RecordRequestError() => RequestErrors.Add(1);

    public static void RecordProcessingDelay(DateTimeOffset occurredAt, DateTimeOffset ingestedAt)
        => ProcessingDelay.Record(Math.Max(0, (ingestedAt - occurredAt).TotalMilliseconds));
}