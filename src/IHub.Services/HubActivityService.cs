using IHub.Application.DTOs;
using IHub.Application.Interfaces.Repositories;
using IHub.Application.Interfaces.Services;
using IHub.Application.Mappers;
using IHub.Data.Persistence;
using IHub.Domain.Exceptions;
using IHub.Services.Telemetry;
using Microsoft.Extensions.Logging;

namespace IHub.Services;

public sealed class HubActivityService(
    IActivityRepository activityRepository,
    IIntermediaryRepository intermediaryRepository,
    IUnitOfWork unitOfWork,
    ILogger<HubActivityService> logger) : IHubActivityService
{
    private static readonly ActivityMapper Mapper = new();
    private static readonly StatementMapper StatementMapper = new();

    public async Task<(bool IsCreated, ActivityResponse Response)> SubmitActivityAsync(CreateActivityRequest request, CancellationToken cancellationToken = default)
    {
        var sourceSystem = request.SourceSystem.Trim().ToUpperInvariant();
        var intermediaryId = request.IntermediaryId.Trim();
        var occurredAt = request.OccurredAt.ToUniversalTime();
        var raceDuplicateResponse = await GetDuplicateResponseAsync(
            sourceSystem, request.ExternalActivityId, intermediaryId, occurredAt, request.Amount, cancellationToken);
        if (raceDuplicateResponse is not null)
        {
            return (false, raceDuplicateResponse);
        }

        if (!await intermediaryRepository.ExistsAsync(intermediaryId, cancellationToken))
        {
            throw new IntermediaryNotFoundException(intermediaryId);
        }

        var activity = Mapper.ToActivity(request);
        activity.Id = Guid.NewGuid();
        activity.IngestedAt = DateTimeOffset.UtcNow;

        try
        {
            await activityRepository.AddAsync(activity, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ActivityIdentityConstraintException)
        {
            // Another request may have inserted this activity after the initial lookup.
            var duplicateResponse = await GetDuplicateResponseAsync(
                sourceSystem, request.ExternalActivityId, intermediaryId, occurredAt, request.Amount, cancellationToken);
            if (duplicateResponse is not null)
            {
                return (false, duplicateResponse);
            }

            throw;
        }
        ActivityMetrics.RecordAccepted();
        logger.LogInformation("Activity accepted. SourceSystem={SourceSystem} ExternalActivityId={ExternalActivityId} IntermediaryId={IntermediaryId} Outcome={Outcome}",
            sourceSystem, activity.ExternalActivityId, intermediaryId, "accepted");
        return (true, Mapper.ToResponse(activity));
    }

    private async Task<ActivityResponse?> GetDuplicateResponseAsync(
        string sourceSystem,
        string externalActivityId,
        string intermediaryId,
        DateTimeOffset occurredAt,
        decimal amount,
        CancellationToken cancellationToken)
    {
        var existingActivity = await activityRepository.GetByExternalIdentityAsync(
            sourceSystem, externalActivityId, cancellationToken);
        if (existingActivity is null)
        {
            return null;
        }

        var isSamePayload = existingActivity.IntermediaryId == intermediaryId
            && existingActivity.OccurredAt == occurredAt
            && existingActivity.Amount == amount;
        if (!isSamePayload)
        {
            throw new DuplicateActivityConflictException(externalActivityId);
        }

        ActivityMetrics.RecordDuplicate();
        logger.LogInformation("Identical activity duplicate. SourceSystem={SourceSystem} ExternalActivityId={ExternalActivityId} IntermediaryId={IntermediaryId} Outcome={Outcome}",
            sourceSystem, externalActivityId, intermediaryId, "duplicate");
        return Mapper.ToResponse(existingActivity);
    }

    public async Task<StatementResponse> GetStatementAsync(string intermediaryId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(intermediaryId))
        {
            throw new InvalidActivityException("Intermediary ID is required.");
        }

        start = start.ToUniversalTime();
        end = end.ToUniversalTime();

        if (start >= end)
        {
            throw new InvalidStatementWindowException();
        }

        logger.LogInformation("Calculating statement for {IntermediaryId} from {Start} through {End}", intermediaryId, start, end);
        var grossTotal = await activityRepository.GetGrossTotalAsync(intermediaryId, start, end, cancellationToken);
        return StatementMapper.ToResponse(new StatementData(intermediaryId, start, end, grossTotal));
    }

}