using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Npgsql;

namespace PostgreSql.Replication.Tests;

/// <summary>
/// Spins up a primary + two streaming replicas (Bitnami PostgreSQL) on a Docker network so
/// <see cref="ReplicationConnectionOptions"/> can be pointed at ephemeral ports without a manual compose stack.
/// </summary>
public sealed class PostgresReplicationInfrastructureFixture : IAsyncLifetime
{
    private const string BitnamiImage = "docker.io/bitnamilegacy/postgresql:15.10.0-debian-12-r2";

    private INetwork? _network;
    private IContainer? _primary;
    private IContainer? _replica1;
    private IContainer? _replica2;

    public async Task InitializeAsync()
    {
        if (string.Equals(Environment.GetEnvironmentVariable("REPLICATION_TESTS_USE_EXTERNAL"), "1", StringComparison.OrdinalIgnoreCase))
            return;

        using (var pullCts = new CancellationTokenSource(TimeSpan.FromMinutes(8)))
            await DockerImagePullHelper.PullIfNeededAsync(BitnamiImage, pullCts.Token).ConfigureAwait(false);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        _network = new NetworkBuilder().WithName($"pg-stream-{suffix}").Build();
        await _network.CreateAsync();

        _primary = new ContainerBuilder()
            .WithImage(BitnamiImage)
            .WithNetwork(_network)
            .WithNetworkAliases("postgres-primary")
            .WithEnvironment("POSTGRESQL_REPLICATION_MODE", "master")
            .WithEnvironment("POSTGRESQL_REPLICATION_USER", "replicator")
            .WithEnvironment("POSTGRESQL_REPLICATION_PASSWORD", "ReplicaPass123")
            .WithEnvironment("POSTGRESQL_USERNAME", "job_admin")
            .WithEnvironment("POSTGRESQL_PASSWORD", "StrongTestPass123")
            .WithEnvironment("POSTGRESQL_DATABASE", "job_config_db")
            .WithPortBinding(5432, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
            .Build();

        await _primary.StartAsync();

        var primaryPort = _primary.GetMappedPublicPort(5432);
        var primaryCs = BuildConnectionString(primaryPort);
        await WaitForPostgresAsync(primaryCs, TimeSpan.FromMinutes(3), CancellationToken.None);

        IContainer BuildReplica(string name) => new ContainerBuilder()
            .WithImage(BitnamiImage)
            .WithHostname(name)
            .WithNetwork(_network)
            .WithEnvironment("POSTGRESQL_REPLICATION_MODE", "slave")
            .WithEnvironment("POSTGRESQL_REPLICATION_USER", "replicator")
            .WithEnvironment("POSTGRESQL_REPLICATION_PASSWORD", "ReplicaPass123")
            .WithEnvironment("POSTGRESQL_MASTER_HOST", "postgres-primary")
            .WithEnvironment("POSTGRESQL_MASTER_PORT_NUMBER", "5432")
            .WithEnvironment("POSTGRESQL_USERNAME", "job_admin")
            .WithEnvironment("POSTGRESQL_PASSWORD", "StrongTestPass123")
            .WithEnvironment("POSTGRESQL_DATABASE", "job_config_db")
            .WithPortBinding(5432, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
            .Build();

        _replica1 = BuildReplica($"replica1-{suffix}");
        _replica2 = BuildReplica($"replica2-{suffix}");

        await Task.WhenAll(_replica1.StartAsync(), _replica2.StartAsync());

        var r1Port = _replica1.GetMappedPublicPort(5432);
        var r2Port = _replica2.GetMappedPublicPort(5432);

        Environment.SetEnvironmentVariable("PG_PRIMARY_CONNECTION_STRING", primaryCs);
        Environment.SetEnvironmentVariable("PG_REPLICA1_CONNECTION_STRING", BuildConnectionString(r1Port));
        Environment.SetEnvironmentVariable("PG_REPLICA2_CONNECTION_STRING", BuildConnectionString(r2Port));

        await WaitForReplicaInRecoveryAsync(BuildConnectionString(r1Port), TimeSpan.FromMinutes(4), CancellationToken.None);
        await WaitForReplicaInRecoveryAsync(BuildConnectionString(r2Port), TimeSpan.FromMinutes(4), CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        if (string.Equals(Environment.GetEnvironmentVariable("REPLICATION_TESTS_USE_EXTERNAL"), "1", StringComparison.OrdinalIgnoreCase))
            return;

        if (_replica2 != null)
            await _replica2.DisposeAsync();
        if (_replica1 != null)
            await _replica1.DisposeAsync();
        if (_primary != null)
            await _primary.DisposeAsync();
        if (_network != null)
            await _network.DeleteAsync();
    }

    private static string BuildConnectionString(int hostPort) =>
        $"Host=127.0.0.1;Port={hostPort};Database=job_config_db;Username=job_admin;Password=StrongTestPass123;Pooling=false;Timeout=30";

    private static async Task WaitForPostgresAsync(string connectionString, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync(cancellationToken);
                return;
            }
            catch
            {
                await Task.Delay(500, cancellationToken);
            }
        }

        throw new TimeoutException($"PostgreSQL did not accept connections within {timeout}: {connectionString.Split(';')[0]}...");
    }

    private static async Task WaitForReplicaInRecoveryAsync(string connectionString, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync(cancellationToken);
                await using var cmd = new NpgsqlCommand("SELECT pg_is_in_recovery();", conn);
                var o = await cmd.ExecuteScalarAsync(cancellationToken);
                if (o is true || (o is string s && s.Equals("t", StringComparison.OrdinalIgnoreCase)))
                    return;
            }
            catch
            {
                // replica still joining
            }

            await Task.Delay(500, cancellationToken);
        }

        throw new TimeoutException("Replica did not report in_recovery within timeout.");
    }
}
