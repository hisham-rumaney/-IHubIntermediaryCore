namespace IHub.Application.DTOs;

public sealed class ActivityResponse
{
    public string SourceSystem { get; set; } = string.Empty;
    public string ExternalActivityId { get; set; } = string.Empty;
    public string IntermediaryId { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTimeOffset IngestedAt { get; set; }
}
