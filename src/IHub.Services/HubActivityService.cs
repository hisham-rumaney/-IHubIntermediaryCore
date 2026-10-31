using IHub.Application.DTOs;
using IHub.Application.Interfaces.Persistence;
using IHub.Application.Interfaces.Repositories;
using IHub.Application.Interfaces.Services;
using IHub.Domain.Entities;
using IHub.Domain.Exceptions;
using IHub.Services.Telemetry;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace IHub.Services;

public sealed class HubActivityService(
    IActivityRepository activityRepository,
    IIntermediaryRepository intermediaryRepository,
    IStatementRepository statementRepository,
    IUnitOfWork unitOfWork,
    ILogger<HubActivityService> logger) : IHubActivityService
{
    public async Task<(bool IsCreated, ActivityResponse Response)> SubmitActivityAsync(CreateActivityRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await SubmitActivityCoreAsync(request, cancellationToken);
        }
        catch (DuplicateActivityConflictException)
        {
            ActivityMetrics.RecordConflict();
            ActivityMetrics.RecordIngestionFailure();
            logger.LogWarning("Activity submission conflicted. SourceSystem={SourceSystem} ExternalActivityId={ExternalActivityId} IntermediaryId={IntermediaryId} Outcome={Outcome}",
                request?.SourceSystem, request?.ExternalActivityId, request?.IntermediaryId, "conflict");
            throw;
        }
        catch (DomainException exception)
        {
            ActivityMetrics.RecordIngestionFailure();
            logger.LogWarning("Activity submission rejected. SourceSystem={SourceSystem} ExternalActivityId={ExternalActivityId} IntermediaryId={IntermediaryId} ErrorType={ErrorType} Outcome={Outcome}",
                request?.SourceSystem, request?.ExternalActivityId, request?.IntermediaryId, exception.GetType().Name, "rejected");
            throw;
        }
        catch (Exception exception)
        {
            ActivityMetrics.RecordIngestionFailure();
            logger.LogError(exception, "Activity ingestion failed. SourceSystem={SourceSystem} ExternalActivityId={ExternalActivityId} IntermediaryId={IntermediaryId}",
                request?.SourceSystem, request?.ExternalActivityId, request?.IntermediaryId);
            throw;
        }
    }

    private async Task<(bool IsCreated, ActivityResponse Response)> SubmitActivityCoreAsync(CreateActivityRequest request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        var sourceSystem = request.SourceSystem.Trim().ToUpperInvariant();
        var intermediaryId = request.IntermediaryId.Trim();
        var occurredAt = request.OccurredAt.ToUniversalTime();
        var existing = await activityRepository.GetByExternalIdentityAsync(sourceSystem, request.ExternalActivityId, cancellationToken);
        if (existing is not null)
        {
            var samePayload = existing.IntermediaryId == intermediaryId
                && existing.OccurredAt == occurredAt
                && existing.Amount == request.Amount;

            if (!samePayload)
            {
                throw new DuplicateActivityConflictException(request.ExternalActivityId);
            }

            ActivityMetrics.RecordDuplicate();
            logger.LogInformation("Identical activity duplicate. SourceSystem={SourceSystem} ExternalActivityId={ExternalActivityId} IntermediaryId={IntermediaryId} Outcome={Outcome}",
                sourceSystem, request.ExternalActivityId, intermediaryId, "duplicate");
            return (false, MapResponse(existing));
        }

        await intermediaryRepository.EnsureExistsAsync(intermediaryId, cancellationToken);

        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            SourceSystem = sourceSystem,
            ExternalActivityId = request.ExternalActivityId,
            IntermediaryId = intermediaryId,
            OccurredAt = occurredAt,
            Amount = request.Amount,
            Currency = Activity.CurrencyZar,
            IngestedAt = DateTimeOffset.UtcNow
        };

        try
        {
            await activityRepository.AddAsync(activity, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ActivityIdentityConstraintException)
        {
            var duplicate = await activityRepository.GetByExternalIdentityAsync(sourceSystem, request.ExternalActivityId, cancellationToken);
            if (duplicate is not null)
            {
                var samePayload = duplicate.IntermediaryId == intermediaryId
                    && duplicate.OccurredAt == occurredAt
                    && duplicate.Amount == request.Amount;

                if (!samePayload)
                {
                    throw new DuplicateActivityConflictException(request.ExternalActivityId);
                }

                ActivityMetrics.RecordDuplicate();
                logger.LogInformation("Identical concurrent activity duplicate. SourceSystem={SourceSystem} ExternalActivityId={ExternalActivityId} IntermediaryId={IntermediaryId} Outcome={Outcome}",
                    sourceSystem, request.ExternalActivityId, intermediaryId, "duplicate");
                return (false, MapResponse(duplicate));
            }

            throw;
        }

        ActivityMetrics.RecordAccepted();
        ActivityMetrics.RecordProcessingDelay(activity.OccurredAt, activity.IngestedAt);
        logger.LogInformation("Activity accepted. SourceSystem={SourceSystem} ExternalActivityId={ExternalActivityId} IntermediaryId={IntermediaryId} Outcome={Outcome}",
            sourceSystem, activity.ExternalActivityId, intermediaryId, "accepted");
        return (true, MapResponse(activity));
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
        return await statementRepository.GetStatementAsync(intermediaryId, start, end, cancellationToken);
    }

    private static void ValidateRequest(CreateActivityRequest request)
    {
        if (request is null)
        {
            throw new InvalidActivityException("Activity body is required.");
        }

        if (string.IsNullOrWhiteSpace(request.SourceSystem))
        {
            throw new InvalidActivityException("Source system is required.");
        }

        var normalizedSourceSystem = request.SourceSystem.Trim();
        if (normalizedSourceSystem.Length > 50 || !Regex.IsMatch(normalizedSourceSystem, "^[A-Za-z0-9][A-Za-z0-9._-]*$"))
        {
            throw new InvalidActivityException("Source system must be a code of up to 50 letters, digits, dots, underscores or hyphens.");
        }

        if (string.IsNullOrWhiteSpace(request.ExternalActivityId) || request.ExternalActivityId.Length > 200)
        {
            throw new InvalidActivityException("External activity ID is required and must be 200 characters or fewer.");
        }

        if (string.IsNullOrWhiteSpace(request.IntermediaryId) || request.IntermediaryId.Trim().Length > 200)
        {
            throw new InvalidActivityException("Intermediary ID is required and must be 200 characters or fewer.");
        }

        if (request.Amount <= 0m)
        {
            throw new InvalidActivityException("Amount must be greater than zero.");
        }

        if (request.Amount > 9999999999999999.99m || decimal.Round(request.Amount, 2) != request.Amount)
        {
            throw new InvalidActivityException("Amount must fit NUMERIC(18,2).");
        }

        if (request.OccurredAt == default)
        {
            throw new InvalidActivityException("OccurredAt is required.");
        }
    }

    private static ActivityResponse MapResponse(Activity activity)
    {
        return new ActivityResponse
        {
            SourceSystem = activity.SourceSystem,
            ExternalActivityId = activity.ExternalActivityId,
            IntermediaryId = activity.IntermediaryId,
            OccurredAt = activity.OccurredAt,
            Amount = activity.Amount,
            Currency = activity.Currency,
            IngestedAt = activity.IngestedAt
        };
    }
}