using JobScheduler.DAL.Connection;
using JobScheduler.DAL.Consistency;
using JobScheduler.DAL.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit.Abstractions;

namespace JobScheduler.DAL.Postgres.Tests;

/// <summary>
/// Integration tests: capture <see cref="PostgresConnectionFactory"/> debug lines to prove
/// <see cref="ConsistencyLevel"/> maps to primary vs replica round-robin as designed.
/// </summary>
[Collection("PostgresConsistency")]
public sealed class PostgresConsistencyRoutingTests
{
    private readonly PostgresConsistencyRoutingFixture _fixture;
    private readonly ITestOutputHelper _output;

    public PostgresConsistencyRoutingTests(PostgresConsistencyRoutingFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task GetActiveJobsAsync_Strong_logs_primary_read_not_replica_round_robin()
    {
        _fixture.RoutingLogCapture.Clear();
        using var scope = _fixture.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IJobDefinitionRepository>();

        _ = await repo.GetActiveJobsAsync(ConsistencyLevel.Strong);

        var logs = _fixture.RoutingLogCapture.Snapshot();
        DumpLogs(nameof(GetActiveJobsAsync_Strong_logs_primary_read_not_replica_round_robin), logs);

        var combined = string.Join(Environment.NewLine, logs);
        Assert.Contains("PostgresPrimaryReadOpened", combined);
        Assert.Contains("ConsistencyLevel=Strong", combined);
        Assert.Contains("Role=Primary", combined);
        Assert.Contains("Operation=Read", combined);
        Assert.DoesNotContain("PostgresReadOpened", combined);
    }

    [Fact]
    public async Task GetByUserIdAsync_Eventual_logs_replica_and_alternates_replica_index()
    {
        _fixture.RoutingLogCapture.Clear();
        using var scope = _fixture.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IJobDefinitionRepository>();

        _ = await repo.GetByUserIdAsync(_fixture.SeedUserId, ConsistencyLevel.Eventual);
        _ = await repo.GetByUserIdAsync(_fixture.SeedUserId, ConsistencyLevel.Eventual);
        _ = await repo.GetByUserIdAsync(_fixture.SeedUserId, ConsistencyLevel.Eventual);

        var logs = _fixture.RoutingLogCapture.Snapshot();
        DumpLogs(nameof(GetByUserIdAsync_Eventual_logs_replica_and_alternates_replica_index), logs);

        var combined = string.Join(Environment.NewLine, logs);
        Assert.Contains("PostgresReadOpened", combined);
        Assert.Contains("ConsistencyLevel=Eventual", combined);
        Assert.Contains("Role=Replica", combined);
        Assert.Contains("ReplicaIndex=0", combined);
        Assert.Contains("ReplicaIndex=1", combined);
    }

    [Fact]
    public async Task GetByIdAsync_Strong_logs_primary_read()
    {
        _fixture.RoutingLogCapture.Clear();
        using var scope = _fixture.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IJobDefinitionRepository>();

        _ = await repo.GetByIdAsync(_fixture.SeedJobId, ConsistencyLevel.Strong);

        var logs = _fixture.RoutingLogCapture.Snapshot();
        DumpLogs(nameof(GetByIdAsync_Strong_logs_primary_read), logs);

        var combined = string.Join(Environment.NewLine, logs);
        Assert.Contains("PostgresPrimaryReadOpened", combined);
        Assert.Contains("ConsistencyLevel=Strong", combined);
        Assert.Contains("Role=Primary", combined);
    }

    [Fact]
    public async Task GetWriteConnectionAsync_logs_write_opened_on_primary()
    {
        _fixture.RoutingLogCapture.Clear();
        using var scope = _fixture.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();

        await using var connection = (NpgsqlConnection)await factory.GetWriteConnectionAsync();

        var logs = _fixture.RoutingLogCapture.Snapshot();
        DumpLogs(nameof(GetWriteConnectionAsync_logs_write_opened_on_primary), logs);

        var combined = string.Join(Environment.NewLine, logs);
        Assert.Contains("PostgresWriteOpened", combined);
        Assert.Contains("Operation=Write", combined);
        Assert.Contains("Role=Primary", combined);
    }

    private void DumpLogs(string testName, IReadOnlyList<string> logs)
    {
        _output.WriteLine($"--- {testName} (captured routing logs) ---");
        foreach (var line in logs)
            _output.WriteLine(line);
        _output.WriteLine("--- end ---");
    }
}
