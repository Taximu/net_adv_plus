using System.Globalization;
using Npgsql;
using Xunit;

namespace PostgreSql.Replication.Tests;

/// <summary>
/// Validates PostgreSQL physical streaming replication: primary vs standby recovery,
/// WAL replay visibility on replicas, and read-only enforcement on standbys.
/// </summary>
[Collection("PostgresReplication")]
public class ReplicationTests
{
    // Ensures PostgresReplicationInfrastructureFixture.InitializeAsync runs before tests.
    private readonly PostgresReplicationInfrastructureFixture _infra;

    public ReplicationTests(PostgresReplicationInfrastructureFixture infra) => _infra = infra;
    private const string ReadOnlySqlState = "25006"; // cannot execute INSERT in a read-only transaction
    private const string ProbeTableSql = """
        CREATE TABLE IF NOT EXISTS replication_probe (
            run_id UUID PRIMARY KEY,
            marker TEXT NOT NULL,
            created_at TIMESTAMPTZ NOT NULL DEFAULT now()
        );
        """;

    [Fact]
    public async Task Primary_reports_not_in_recovery()
    {
        await using var conn = new NpgsqlConnection(ReplicationConnectionOptions.Primary);
        await conn.OpenAsync();
        var inRecovery = await ScalarBoolAsync(conn, "SELECT pg_is_in_recovery();");
        Assert.False(inRecovery);
    }

    [Fact]
    public async Task Each_replica_reports_in_recovery()
    {
        foreach (var cs in ReplicationConnectionOptions.Replicas)
        {
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            var inRecovery = await ScalarBoolAsync(conn, "SELECT pg_is_in_recovery();");
            Assert.True(inRecovery, $"Expected standby at [{Redact(cs)}] to be in recovery.");
        }
    }

    [Fact]
    public async Task Insert_on_primary_eventually_visible_on_all_replicas()
    {
        var runId = Guid.NewGuid();
        const string marker = "replication-test-marker";

        await using (var primary = new NpgsqlConnection(ReplicationConnectionOptions.Primary))
        {
            await primary.OpenAsync();
            await using (var cmd = new NpgsqlCommand(ProbeTableSql, primary))
                await cmd.ExecuteNonQueryAsync();
            await using var insert = new NpgsqlCommand(
                "INSERT INTO replication_probe (run_id, marker) VALUES (@run_id, @marker);",
                primary);
            insert.Parameters.AddWithValue("run_id", runId);
            insert.Parameters.AddWithValue("marker", marker);
            await insert.ExecuteNonQueryAsync();
        }

        foreach (var replicaCs in ReplicationConnectionOptions.Replicas)
        {
            var deadline = DateTime.UtcNow.AddSeconds(45);
            var seen = false;
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    await using var replica = new NpgsqlConnection(replicaCs);
                    await replica.OpenAsync();
                    await using var q = new NpgsqlCommand(
                        "SELECT marker FROM replication_probe WHERE run_id = @run_id;",
                        replica);
                    q.Parameters.AddWithValue("run_id", runId);
                    var found = (string?)await q.ExecuteScalarAsync();
                    if (found == marker)
                    {
                        seen = true;
                        break;
                    }
                }
                catch
                {
                    // Table or row not yet visible on standby
                }

                await Task.Delay(200);
            }

            Assert.True(seen, $"Row did not replicate to standby [{Redact(replicaCs)}] within timeout.");

            await using (var verify = new NpgsqlConnection(replicaCs))
            {
                await verify.OpenAsync();
                await using var q = new NpgsqlCommand(
                    "SELECT marker FROM replication_probe WHERE run_id = @run_id;",
                    verify);
                q.Parameters.AddWithValue("run_id", runId);
                var found = (string?)await q.ExecuteScalarAsync();
                Assert.Equal(marker, found);
            }
        }

        await using (var cleanup = new NpgsqlConnection(ReplicationConnectionOptions.Primary))
        {
            await cleanup.OpenAsync();
            await using var del = new NpgsqlCommand("DELETE FROM replication_probe WHERE run_id = @run_id;", cleanup);
            del.Parameters.AddWithValue("run_id", runId);
            await del.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task Replica_rejects_writes_to_permanent_tables()
    {
        await EnsureProbeTableExistsOnReplicasAsync();

        var replicaCs = ReplicationConnectionOptions.Replicas[0];
        await using var conn = new NpgsqlConnection(replicaCs);
        await conn.OpenAsync();

        var ex = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var cmd = new NpgsqlCommand(
                "INSERT INTO replication_probe (run_id, marker) VALUES (@run_id, @marker);",
                conn);
            cmd.Parameters.AddWithValue("run_id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("marker", "should-fail");
            await cmd.ExecuteNonQueryAsync();
        });

        Assert.Equal(ReadOnlySqlState, ex.SqlState);
    }

    [Fact]
    public async Task Each_replica_reports_non_null_wal_receive_lsn()
    {
        // Proves the standby is receiving WAL from the primary without relying on pg_stat_replication
        // visibility (which can differ by role / vendor image).
        foreach (var replicaCs in ReplicationConnectionOptions.Replicas)
        {
            await using var conn = new NpgsqlConnection(replicaCs);
            await conn.OpenAsync();
            var receivesWal = await ScalarBoolAsync(conn, "SELECT pg_last_wal_receive_lsn() IS NOT NULL;");
            Assert.True(receivesWal, $"Expected pg_last_wal_receive_lsn() on [{Redact(replicaCs)}].");
        }
    }

    private static async Task EnsureProbeTableExistsOnReplicasAsync()
    {
        await using (var primary = new NpgsqlConnection(ReplicationConnectionOptions.Primary))
        {
            await primary.OpenAsync();
            await using (var cmd = new NpgsqlCommand(ProbeTableSql, primary))
                await cmd.ExecuteNonQueryAsync();
        }

        var replicaCs = ReplicationConnectionOptions.Replicas[0];
        var deadline = DateTime.UtcNow.AddSeconds(45);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await using var conn = new NpgsqlConnection(replicaCs);
                await conn.OpenAsync();
                if (await ScalarBoolAsync(conn, "SELECT to_regclass('public.replication_probe') IS NOT NULL;"))
                    return;
            }
            catch
            {
                // DDL not yet replayed
            }

            await Task.Delay(200);
        }

        throw new InvalidOperationException("replication_probe was not visible on replica within timeout.");
    }

    private static async Task<bool> ScalarBoolAsync(NpgsqlConnection conn, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        var o = await cmd.ExecuteScalarAsync();
        return ToBool(o);
    }

    private static bool ToBool(object? o) => o switch
    {
        bool b => b,
        string s => s.Equals("t", StringComparison.OrdinalIgnoreCase) || s.Equals("true", StringComparison.OrdinalIgnoreCase),
        null => false,
        _ => Convert.ToBoolean(o, CultureInfo.InvariantCulture)
    };

    private static string Redact(string connectionString)
    {
        var parts = connectionString.Split(';');
        return string.Join(";", parts.Select(p =>
            p.StartsWith("Password=", StringComparison.OrdinalIgnoreCase) ? "Password=***" : p));
    }
}
