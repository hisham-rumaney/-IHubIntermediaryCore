using IHub.Application.Interfaces.Repositories;
using IHub.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IHub.Data.Repositories;

public sealed class IntermediaryRepository(IUnitOfWork unitOfWork) : IIntermediaryRepository
{
    public Task<bool> ExistsAsync(string intermediaryId, CancellationToken cancellationToken = default) =>
        unitOfWork.Context.Intermediaries.AnyAsync(x => x.IntermediaryId == intermediaryId, cancellationToken);
}
