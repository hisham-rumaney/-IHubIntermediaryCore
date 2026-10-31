using IHub.Application.Interfaces.Repositories;
using IHub.Domain.Entities;
using IHub.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IHub.Data.Repositories;

public sealed class IntermediaryRepository(IHubDbContext dbContext) : IIntermediaryRepository
{
    public async Task EnsureExistsAsync(string intermediaryId, CancellationToken cancellationToken = default)
    {
        if (dbContext.Database.IsRelational())
        {
            var createdAtUtc = DateTimeOffset.UtcNow;
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO \"Intermediaries\" (\"IntermediaryId\", \"CreatedAtUtc\") VALUES ({intermediaryId}, {createdAtUtc}) ON CONFLICT (\"IntermediaryId\") DO NOTHING",
                cancellationToken);
        }
        else if (!await dbContext.Intermediaries.AnyAsync(x => x.IntermediaryId == intermediaryId, cancellationToken))
        {
            await dbContext.Intermediaries.AddAsync(new Intermediary
            {
                IntermediaryId = intermediaryId,
                CreatedAtUtc = DateTimeOffset.UtcNow
            }, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
