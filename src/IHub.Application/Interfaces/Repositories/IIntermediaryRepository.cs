namespace IHub.Application.Interfaces.Repositories;

public interface IIntermediaryRepository
{
    Task<bool> ExistsAsync(string intermediaryId, CancellationToken cancellationToken = default);
}