using System.Data;

namespace JobScheduler.DAL.UnitOfWork;

public interface IUnitOfWork : IDisposable
{
    IDbTransaction Transaction { get; }
    Task BeginAsync();
    Task CommitAsync();
    Task RollbackAsync();
}
