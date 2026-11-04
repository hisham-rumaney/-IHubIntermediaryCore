namespace IHub.Data.Persistence;

public interface IUnitOfWork
{
    IHubDbContext Context { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
