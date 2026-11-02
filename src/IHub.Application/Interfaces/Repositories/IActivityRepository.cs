using IHub.Domain.Entities;

namespace IHub.Application.Interfaces.Repositories;

public interface IActivityRepository
{
    Task AddAsync(Activity activity, CancellationToken cancellationToken = default);
    Task<Activity?> GetByExternalIdentityAsync(string sourceSystem, string externalActivityId, CancellationToken cancellationToken = default);
    Task<decimal> GetGrossTotalAsync(string intermediaryId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default);
}