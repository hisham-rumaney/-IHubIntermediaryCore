using IHub.Application.DTOs;

namespace IHub.Application.Interfaces.Repositories;

public interface IStatementRepository
{
    Task<StatementResponse> GetStatementAsync(string intermediaryId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default);
}