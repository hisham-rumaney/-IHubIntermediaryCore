using IHub.Application.Interfaces.Repositories;
using IHub.Application.DTOs;
using IHub.Data.Persistence;
using IHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IHub.Data.Repositories;

public sealed class StatementRepository(IHubDbContext dbContext) : IStatementRepository
{
    public async Task<StatementResponse> GetStatementAsync(string intermediaryId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default)
    {
        var grossTotal = await dbContext.Activities
            .AsNoTracking()
            .Where(x => x.IntermediaryId == intermediaryId
                && x.OccurredAt >= start
                && x.OccurredAt < end)
            .SumAsync(x => x.Amount, cancellationToken);

        var lastUpdated = await dbContext.Activities
            .AsNoTracking()
            .Where(x => x.IntermediaryId == intermediaryId
                && x.OccurredAt >= start
                && x.OccurredAt < end)
            .MaxAsync(x => (DateTimeOffset?)x.IngestedAt, cancellationToken);

        return new StatementResponse
        {
            IntermediaryId = intermediaryId,
            Currency = Activity.CurrencyZar,
            Start = start,
            End = end,
            GrossTotal = grossTotal,
            LastUpdated = lastUpdated
        };
    }
}
