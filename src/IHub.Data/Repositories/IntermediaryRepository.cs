using IHub.Application.Interfaces.Repositories;
using IHub.Data.Persistence;
using IHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IHub.Data.Repositories;

public sealed class IntermediaryRepository(IUnitOfWork unitOfWork) : IIntermediaryRepository
{
    private readonly DbSet<Intermediary> intermediaries = unitOfWork.Context.Set<Intermediary>();

    public Task<bool> ExistsAsync(string intermediaryId, CancellationToken cancellationToken = default) =>
        intermediaries.AnyAsync(x => x.IntermediaryId == intermediaryId, cancellationToken);
}
