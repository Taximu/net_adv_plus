using System.Data;

namespace JobScheduler.DAL.UnitOfWork;

public interface IUnitOfWork : IDisposable
{
    IDbTransaction Transaction { get; }
    Task BeginAsync(CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
