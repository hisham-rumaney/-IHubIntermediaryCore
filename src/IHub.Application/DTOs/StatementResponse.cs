namespace IHub.Application.DTOs;

public sealed class StatementResponse
{
    public string IntermediaryId { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public DateTimeOffset Start { get; init; }
    public DateTimeOffset End { get; init; }
    public decimal GrossTotal { get; init; }
}
