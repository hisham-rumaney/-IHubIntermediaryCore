using System.ComponentModel.DataAnnotations;

namespace IHub.Application.DTOs;

public sealed class CreateActivityRequest : IValidatableObject
{
    [Required]
    [StringLength(50)]
    [RegularExpression("^[A-Za-z0-9][A-Za-z0-9._-]*$")]
    public string SourceSystem { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    [RegularExpression("(?s).*\\S.*")]
    public string ExternalActivityId { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    [RegularExpression("(?s).*\\S.*")]
    public string IntermediaryId { get; set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; set; }

    [Range(typeof(decimal), "0.01", "9999999999999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal Amount { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (OccurredAt == default)
        {
            yield return new ValidationResult("OccurredAt is required.", [nameof(OccurredAt)]);
        }

        if (decimal.Round(Amount, 2) != Amount)
        {
            yield return new ValidationResult("Amount must fit NUMERIC(18,2).", [nameof(Amount)]);
        }
    }
}
