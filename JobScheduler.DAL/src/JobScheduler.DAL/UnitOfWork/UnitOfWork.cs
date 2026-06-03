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

    public async Task BeginAsync()
    {
        _connection = await _connectionFactory.GetWriteConnectionAsync();
        if (_connection is not NpgsqlConnection npg)
            throw new InvalidOperationException("Unit of work requires an Npgsql write connection.");
        _transaction = await npg.BeginTransactionAsync();
    }

    public async Task CommitAsync()
    {
        if (_transaction == null) throw new InvalidOperationException("Transaction not started");
        await _transaction.CommitAsync();
        await DisposeAsync();
    }

    public async Task RollbackAsync()
    {
        if (_transaction == null) throw new InvalidOperationException("Transaction not started");
        await _transaction.RollbackAsync();
        await DisposeAsync();
    }

    public void Dispose() => DisposeAsync().GetAwaiter().GetResult();

    private async Task DisposeAsync()
    {
        if (_disposed) return;
        _transaction?.Dispose();
        _connection?.Dispose();
        _disposed = true;
    }
}
