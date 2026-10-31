using IHub.Application.Interfaces.Persistence;
using IHub.Data.Persistence;
using IHub.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace IHub.Data.Repositories;

public sealed class UnitOfWork(IHubDbContext dbContext) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (FindPostgresException(exception) is { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "UQ_Activities_SourceSystem_ExternalActivityId" })
        {
            throw new ActivityIdentityConstraintException();
        }
    }

    private static PostgresException? FindPostgresException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            if (current is PostgresException postgresException)
            {
                return postgresException;
            }
        }

        return null;
    }
}
