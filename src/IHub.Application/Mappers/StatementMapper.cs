using IHub.Application.DTOs;
using IHub.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace IHub.Application.Mappers;

public sealed record StatementData(
    string IntermediaryId,
    DateTimeOffset Start,
    DateTimeOffset End,
    decimal GrossTotal)
{
    public string Currency { get; init; } = Activity.CurrencyZar;
}

[Mapper]
public partial class StatementMapper
{
    public partial StatementResponse ToResponse(StatementData data);
}
