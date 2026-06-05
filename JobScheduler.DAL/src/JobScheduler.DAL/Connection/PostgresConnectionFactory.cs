using System.Data;
using JobScheduler.DAL.Configuration;
using JobScheduler.DAL.Consistency;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace JobScheduler.DAL.Connection;

public class PostgresConnectionFactory : IDbConnectionFactory
{
    private readonly DatabaseOptions _options;
    private readonly ILogger<PostgresConnectionFactory> _logger;
    private int _roundRobinCounter;

    public PostgresConnectionFactory(IOptions<DatabaseOptions> options, ILogger<PostgresConnectionFactory> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IDbConnection> GetWriteConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new NpgsqlConnection(_options.PostgresWriteConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        LogEndpoint("PostgresWriteOpened", ConsistencyLevel.Strong, "Primary", connection, replicaIndex: null, operation: "Write");
        return connection;
    }

    public Task<IDbConnection> GetReadConnectionAsync(CancellationToken cancellationToken = default) =>
        GetReadConnectionAsync(ConsistencyLevel.Eventual, cancellationToken);

    public async Task<IDbConnection> GetReadConnectionAsync(ConsistencyLevel consistencyLevel, CancellationToken cancellationToken = default)
    {
        switch (consistencyLevel)
        {
            case ConsistencyLevel.Strong:
                return await OpenPrimaryReadConnectionAsync(consistencyLevel, cancellationToken).ConfigureAwait(false);
            case ConsistencyLevel.Eventual:
                return await OpenReplicaReadConnectionAsync(cancellationToken).ConfigureAwait(false);
            default:
                throw new ArgumentOutOfRangeException(nameof(consistencyLevel), consistencyLevel, null);
        }
    }

    private async Task<IDbConnection> OpenPrimaryReadConnectionAsync(ConsistencyLevel level, CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(_options.PostgresWriteConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        LogEndpoint("PostgresPrimaryReadOpened", level, "Primary", connection, replicaIndex: null, operation: "Read");
        return connection;
    }

    private async Task<IDbConnection> OpenReplicaReadConnectionAsync(CancellationToken cancellationToken)
    {
        if (_options.PostgresReadConnectionStrings is not { Count: > 0 })
            return await OpenPrimaryReadConnectionAsync(ConsistencyLevel.Eventual, cancellationToken).ConfigureAwait(false);

        var replicaIndex = Math.Abs(Interlocked.Increment(ref _roundRobinCounter) % _options.PostgresReadConnectionStrings.Count);
        var connectionString = _options.PostgresReadConnectionStrings[replicaIndex];
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        LogEndpoint("PostgresReadOpened", ConsistencyLevel.Eventual, "Replica", connection, replicaIndex, operation: "Read");
        return connection;
    }

    private void LogEndpoint(string eventName, ConsistencyLevel level, string role, NpgsqlConnection connection, int? replicaIndex, string operation)
    {
        var host = connection.Host;
        var port = connection.Port;
        _logger.LogDebug(
            "{EventName}: ConsistencyLevel={ConsistencyLevel} Role={Role} Host={Host} Port={Port} ReplicaIndex={ReplicaIndex} Operation={Operation}",
            eventName,
            level,
            role,
            host,
            port,
            replicaIndex,
            operation);
    }
}
