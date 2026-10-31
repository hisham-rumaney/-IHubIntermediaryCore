namespace IHub.Domain.Entities;

public sealed class Intermediary
{
    public string IntermediaryId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
