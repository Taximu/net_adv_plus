using System.Data;
using JobScheduler.DAL.Consistency;

namespace JobScheduler.DAL.Connection;

public interface IDbConnectionFactory
{
    Task<IDbConnection> GetWriteConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>Replica round-robin (eventual) — same behavior as before split-aware reads.</summary>
    Task<IDbConnection> GetReadConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>Opens a read connection according to <paramref name="consistencyLevel"/>.</summary>
    Task<IDbConnection> GetReadConnectionAsync(ConsistencyLevel consistencyLevel, CancellationToken cancellationToken = default);
}
