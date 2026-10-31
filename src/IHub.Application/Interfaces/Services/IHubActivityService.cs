using IHub.Application.DTOs;

namespace IHub.Application.Interfaces.Services;

public interface IHubActivityService
{
    Task<(bool IsCreated, ActivityResponse Response)> SubmitActivityAsync(
        CreateActivityRequest request,
        CancellationToken cancellationToken = default);

    Task<StatementResponse> GetStatementAsync(
        string intermediaryId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default);
}
