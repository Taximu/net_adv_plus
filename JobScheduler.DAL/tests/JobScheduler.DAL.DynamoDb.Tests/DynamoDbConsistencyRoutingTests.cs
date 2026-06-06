using JobScheduler.DAL.Consistency;
using JobScheduler.DAL.DynamoDB.Models;
using JobScheduler.DAL.DynamoDB.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace JobScheduler.DAL.DynamoDb.Tests;

/// <summary>
/// Captures DynamoDB repository debug lines to prove <see cref="ConsistencyLevel"/> maps to
/// <c>ConsistentRead</c> on <c>GetItem</c> vs eventual GSI <c>Query</c> behavior.
/// </summary>
[Collection("DynamoDbDal")]
public sealed class DynamoDbConsistencyRoutingTests
{
    private readonly DynamoDbDalFixture _fixture;
    private readonly ITestOutputHelper _output;

    public DynamoDbConsistencyRoutingTests(DynamoDbDalFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task GetItem_default_Strong_logs_consistent_read_true()
    {
        using var test = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = test.Token;
        await _fixture.EnsureLocalRespondsAsync(ct);

        using var scope = _fixture.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IExecutionQueueRepository>();

        var id = $"consistency-get-strong-{Guid.NewGuid():N}";
        var scheduled = DateTime.UtcNow.ToString("O");
        try
        {
            await repo.PutAsync(new ExecutionQueueItem
            {
                QueueId = id,
                ScheduledFor = scheduled,
                JobId = "j",
                ScheduleId = "s",
                QueueStatus = "pending",
                Priority = 1,
                MaxRetries = 3
            }, ct);

            _fixture.RoutingLogCapture.Clear();
            _ = await repo.GetAsync(id, scheduled, ct);

            var logs = _fixture.RoutingLogCapture.Snapshot();
            DumpLogs(nameof(GetItem_default_Strong_logs_consistent_read_true), logs);

            var combined = string.Join(Environment.NewLine, logs);
            Assert.Contains("DynamoDB GetItem", combined);
            Assert.Contains("ConsistencyLevel=Strong", combined);
            Assert.Contains("ConsistentRead=True", combined);
        }
        finally
        {
            await repo.DeleteAsync(id, scheduled, ct);
        }
    }

    [Fact]
    public async Task GetItem_Eventual_logs_consistent_read_false()
    {
        using var test = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = test.Token;
        await _fixture.EnsureLocalRespondsAsync(ct);

        using var scope = _fixture.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IExecutionQueueRepository>();

        var id = $"consistency-get-eventual-{Guid.NewGuid():N}";
        var scheduled = DateTime.UtcNow.ToString("O");
        try
        {
            await repo.PutAsync(new ExecutionQueueItem
            {
                QueueId = id,
                ScheduledFor = scheduled,
                JobId = "j",
                ScheduleId = "s",
                QueueStatus = "pending",
                Priority = 2,
                MaxRetries = 3
            }, ct);

            _fixture.RoutingLogCapture.Clear();
            _ = await repo.GetAsync(id, scheduled, ct, ConsistencyLevel.Eventual);

            var logs = _fixture.RoutingLogCapture.Snapshot();
            DumpLogs(nameof(GetItem_Eventual_logs_consistent_read_false), logs);

            var combined = string.Join(Environment.NewLine, logs);
            Assert.Contains("ConsistencyLevel=Eventual", combined);
            Assert.Contains("ConsistentRead=False", combined);
        }
        finally
        {
            await repo.DeleteAsync(id, scheduled, ct);
        }
    }

    [Fact]
    public async Task Query_pending_Eventual_logs_gsi_poll_without_consistent_read()
    {
        using var test = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = test.Token;
        await _fixture.EnsureLocalRespondsAsync(ct);

        using var scope = _fixture.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IExecutionQueueRepository>();

        var id = $"consistency-query-{Guid.NewGuid():N}";
        var scheduled = DateTime.UtcNow.ToString("O");
        try
        {
            await repo.PutAsync(new ExecutionQueueItem
            {
                QueueId = id,
                ScheduledFor = scheduled,
                JobId = "j",
                ScheduleId = "s",
                QueueStatus = "pending",
                Priority = 3,
                MaxRetries = 3
            }, ct);

            _fixture.RoutingLogCapture.Clear();
            _ = await repo.QueryByQueueStatusAsync("pending", limit: 50, ConsistencyLevel.Eventual, ct);

            var logs = _fixture.RoutingLogCapture.Snapshot();
            DumpLogs(nameof(Query_pending_Eventual_logs_gsi_poll_without_consistent_read), logs);

            var combined = string.Join(Environment.NewLine, logs);
            Assert.Contains("DynamoDB Query", combined);
            Assert.Contains("PendingExecutionsIndex", combined);
            Assert.Contains("Operation=PollPending", combined);
            Assert.Contains("ConsistencyLevel=Eventual", combined);
            Assert.DoesNotContain("ConsistentRead=True", combined);
        }
        finally
        {
            await repo.DeleteAsync(id, scheduled, ct);
        }
    }

    private void DumpLogs(string testName, IReadOnlyList<string> logs)
    {
        _output.WriteLine($"--- {testName} (captured Dynamo routing logs) ---");
        foreach (var line in logs)
            _output.WriteLine(line);
        _output.WriteLine("--- end ---");
    }
}
