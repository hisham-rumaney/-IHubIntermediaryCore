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

    public static void RecordAccepted() => AcceptedActivities.Add(1);
    public static void RecordDuplicate() => DuplicateSubmissions.Add(1);
}