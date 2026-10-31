using IHub.Application.Interfaces.Repositories;
using IHub.Domain.Entities;
using IHub.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IHub.Data.Repositories;

public sealed class ActivityRepository(IHubDbContext dbContext) : IActivityRepository
{
    public async Task AddAsync(Activity activity, CancellationToken cancellationToken = default)
    {
        await dbContext.Activities.AddAsync(activity, cancellationToken);
    }

    public async Task<Activity?> GetByExternalIdentityAsync(string sourceSystem, string externalActivityId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Activities
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SourceSystem == sourceSystem && x.ExternalActivityId == externalActivityId, cancellationToken);
    }

    public async Task<IReadOnlyList<Activity>> GetByIntermediaryAsync(string intermediaryId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Activities
            .AsNoTracking()
            .Where(x => x.IntermediaryId == intermediaryId)
            .OrderBy(x => x.OccurredAt)
            .ToListAsync(cancellationToken);
    }
}
