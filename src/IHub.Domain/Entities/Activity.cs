using System.ComponentModel.DataAnnotations;

namespace IHub.Domain.Entities;

public sealed class Activity
{
    public const string CurrencyZar = "ZAR";

    public Guid Id { get; set; }

    [Required]
    public string SourceSystem { get; set; } = string.Empty;

    [Required]
    public string ExternalActivityId { get; set; } = string.Empty;

    [Required]
    public string IntermediaryId { get; set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; set; }

    public decimal Amount { get; set; }

    [Required]
    public string Currency { get; set; } = CurrencyZar;

    public DateTimeOffset IngestedAt { get; set; }
}
