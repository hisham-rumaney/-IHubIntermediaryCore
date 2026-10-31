using IHub.Application.DTOs;
using IHub.Domain.Entities;
using Riok.Mapperly.Abstractions;

namespace IHub.Application.Mappers;

[Mapper]
public partial class ActivityMapper
{
    [MapperIgnoreTarget(nameof(Activity.Id))]
    [MapperIgnoreTarget(nameof(Activity.Currency))]
    [MapperIgnoreTarget(nameof(Activity.IngestedAt))]
    [MapProperty(nameof(CreateActivityRequest.SourceSystem), nameof(Activity.SourceSystem), Use = nameof(NormalizeSourceSystem))]
    [MapProperty(nameof(CreateActivityRequest.IntermediaryId), nameof(Activity.IntermediaryId), Use = nameof(NormalizeIntermediaryId))]
    [MapProperty(nameof(CreateActivityRequest.OccurredAt), nameof(Activity.OccurredAt), Use = nameof(NormalizeOccurredAt))]
    public partial Activity ToActivity(CreateActivityRequest request);

    [MapperIgnoreSource(nameof(Activity.Id))]
    public partial ActivityResponse ToResponse(Activity activity);

    private static string NormalizeSourceSystem(string value) => value.Trim().ToUpperInvariant();

    private static string NormalizeIntermediaryId(string value) => value.Trim();

    private static DateTimeOffset NormalizeOccurredAt(DateTimeOffset value) => value.ToUniversalTime();
}
