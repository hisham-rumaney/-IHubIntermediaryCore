namespace IHub.Application.Interfaces.Repositories;

public interface IIntermediaryRepository
{
    Task EnsureExistsAsync(string intermediaryId, CancellationToken cancellationToken = default);
}