using IHub.Application.Interfaces.Repositories;
using IHub.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IHub.Data.Repositories;

public sealed class IntermediaryRepository(IHubDbContext dbContext) : IIntermediaryRepository
{
    public Task<bool> ExistsAsync(string intermediaryId, CancellationToken cancellationToken = default) =>
        dbContext.Intermediaries.AnyAsync(x => x.IntermediaryId == intermediaryId, cancellationToken);
}
