using System.Data;
using System.Data.Common;
using JobScheduler.DAL.Connection;
using Npgsql;

namespace JobScheduler.DAL.UnitOfWork;

public class UnitOfWork : IUnitOfWork
{
    private readonly IDbConnectionFactory _connectionFactory;
    private IDbConnection? _connection;
    private DbTransaction? _transaction;
    private bool _disposed;

    public IDbTransaction Transaction => _transaction ?? throw new InvalidOperationException("Transaction not started");

    public UnitOfWork(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task BeginAsync(CancellationToken cancellationToken = default)
    {
        _connection = await _connectionFactory.GetWriteConnectionAsync(cancellationToken).ConfigureAwait(false);
        if (_connection is not NpgsqlConnection npg)
            throw new InvalidOperationException("Unit of work requires an Npgsql write connection.");
        _transaction = await npg.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null) throw new InvalidOperationException("Transaction not started");
        await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        await DisposeAsync();
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null) throw new InvalidOperationException("Transaction not started");
        await _transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        await DisposeAsync();
    }

    public void Dispose() => DisposeAsync().GetAwaiter().GetResult();

    private Task DisposeAsync()
    {
        if (_disposed)
            return Task.CompletedTask;

        _transaction?.Dispose();
        _connection?.Dispose();
        _disposed = true;
        return Task.CompletedTask;
    }
}
