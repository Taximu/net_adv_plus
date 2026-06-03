namespace PostgreSql.Replication.Tests;

/// <summary>
/// Connection strings for replication integration tests.
/// Defaults match <c>docker-compose.streaming-for-tests.yml</c> when no Testcontainers env is set.
/// When <see cref="PostgresReplicationInfrastructureFixture"/> runs, it sets <c>PG_*_CONNECTION_STRING</c> to ephemeral ports.
/// Set <c>REPLICATION_TESTS_USE_EXTERNAL=1</c> to skip container startup and use only env/defaults (manual compose).
/// </summary>
public static class ReplicationConnectionOptions
{
    public static string Primary =>
        Environment.GetEnvironmentVariable("PG_PRIMARY_CONNECTION_STRING")
        ?? "Host=localhost;Port=5432;Database=job_config_db;Username=job_admin;Password=StrongTestPass123;Pooling=false;Timeout=30";

    public static IReadOnlyList<string> Replicas
    {
        get
        {
            var r1 = Environment.GetEnvironmentVariable("PG_REPLICA1_CONNECTION_STRING");
            var r2 = Environment.GetEnvironmentVariable("PG_REPLICA2_CONNECTION_STRING");
            if (!string.IsNullOrWhiteSpace(r1) && !string.IsNullOrWhiteSpace(r2))
                return new[] { r1.Trim(), r2.Trim() };
            if (!string.IsNullOrWhiteSpace(r1))
                return new[] { r1.Trim() };

            return new[]
            {
                "Host=localhost;Port=5434;Database=job_config_db;Username=job_admin;Password=StrongTestPass123;Pooling=false;Timeout=30",
                "Host=localhost;Port=5435;Database=job_config_db;Username=job_admin;Password=StrongTestPass123;Pooling=false;Timeout=30",
            };
        }
    }
}
