using System.Data;
using Microsoft.Extensions.Options;
using Npgsql;
using JobScheduler.DAL.Configuration;

namespace JobScheduler.DAL.Connection;

public class PostgresConnectionFactory : IDbConnectionFactory
{
    private readonly DatabaseOptions _options;
    private int _roundRobinCounter = 0;

    public PostgresConnectionFactory(IOptions<DatabaseOptions> options)
    {
        _options = options.Value;
    }

    public async Task<IDbConnection> GetWriteConnectionAsync()
    {
        var connection = new NpgsqlConnection(_options.PostgresWriteConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task<IDbConnection> GetReadConnectionAsync()
    {
        if (_options.PostgresReadConnectionStrings == null || 
            _options.PostgresReadConnectionStrings.Count == 0)
        {
            return await GetWriteConnectionAsync();
        }

        var replicaIndex = Interlocked.Increment(ref _roundRobinCounter) % _options.PostgresReadConnectionStrings.Count;
        var connectionString = _options.PostgresReadConnectionStrings[Math.Abs(replicaIndex)];
        
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }
}
