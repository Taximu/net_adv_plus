using JobScheduler.DAL.Consistency;
using JobScheduler.DAL.DynamoDB.Models;
using JobScheduler.DAL.DynamoDB.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace JobScheduler.DAL.DynamoDb.Tests;

/// <summary>
/// UC 2.1 — Execute a job at a scheduled time: execution queue DAL primitives
/// (<see cref="IExecutionQueueRepository"/>) with explicit <see cref="ConsistencyLevel"/> on reads where supported.
/// </summary>
[Collection("DynamoDbDal")]
public class ExecutionQueueRepositoryTests
{
    private readonly DynamoDbDalFixture _fixture;

    public ExecutionQueueRepositoryTests(DynamoDbDalFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Put_and_Get_roundtrip_with_strong_read()
    {
        using var test = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = test.Token;

        await _fixture.EnsureLocalRespondsAsync(ct);
        using var scope = _fixture.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IExecutionQueueRepository>();

        var id = $"dal-test-{Guid.NewGuid():N}";
        var scheduled = DateTime.UtcNow.ToString("O");
        var item = new ExecutionQueueItem
        {
            QueueId = id,
            ScheduledFor = scheduled,
            JobId = "job-dal-test",
            ScheduleId = "sched-dal-test",
            QueueStatus = "pending",
            Priority = 7,
            RetryCount = 0,
            MaxRetries = 3,
            ExecutionContext = new ExecutionContextSnapshot
            {
                Environment = "test",
                TriggerSource = "integration-test",
                UserId = "user-dal"
            }
        };

        try
        {
            await repo.PutAsync(item, ct);
            var loaded = await repo.GetAsync(id, scheduled, ct, ConsistencyLevel.Strong);
            Assert.NotNull(loaded);
            Assert.Equal(id, loaded!.QueueId);
            Assert.Equal(scheduled, loaded.ScheduledFor);
            Assert.Equal("pending", loaded.QueueStatus);
            Assert.Equal(7, loaded.Priority);
            Assert.Equal("integration-test", loaded.ExecutionContext?.TriggerSource);
        }
        finally
        {
            await repo.DeleteAsync(id, scheduled, ct);
        }
    }

    [Fact]
    public async Task QueryByQueueStatus_returns_put_item()
    {
        using var test = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = test.Token;

        await _fixture.EnsureLocalRespondsAsync(ct);
        using var scope = _fixture.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IExecutionQueueRepository>();

        var id = $"dal-query-{Guid.NewGuid():N}";
        var scheduled = DateTime.UtcNow.ToString("O");
        var item = new ExecutionQueueItem
        {
            QueueId = id,
            ScheduledFor = scheduled,
            JobId = "job-q",
            ScheduleId = "sched-q",
            QueueStatus = "pending",
            Priority = 2,
            RetryCount = 0,
            MaxRetries = 3
        };

        try
        {
            await repo.PutAsync(item, ct);
            var pending = await repo.QueryByQueueStatusAsync("pending", limit: 500, ConsistencyLevel.Eventual, ct);
            Assert.Contains(pending, x => x.QueueId == id && x.ScheduledFor == scheduled);
        }
        finally
        {
            await repo.DeleteAsync(id, scheduled, ct);
        }
    }

    [Fact]
    public async Task TryClaimAsync_sets_assigned_and_second_claim_fails()
    {
        using var test = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = test.Token;

        await _fixture.EnsureLocalRespondsAsync(ct);
        using var scope = _fixture.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IExecutionQueueRepository>();

        var id = $"dal-claim-{Guid.NewGuid():N}";
        var scheduled = DateTime.UtcNow.ToString("O");
        var item = new ExecutionQueueItem
        {
            QueueId = id,
            ScheduledFor = scheduled,
            JobId = "job-claim",
            ScheduleId = "sched-claim",
            QueueStatus = "pending",
            Priority = 4,
            RetryCount = 0,
            MaxRetries = 3
        };

        try
        {
            await repo.PutAsync(item, ct);
            var assignedAt = DateTime.UtcNow.ToString("O");
            var ok = await repo.TryClaimAsync(id, scheduled, "worker-dal-test", assignedAt, ct);
            Assert.True(ok);

            var loaded = await repo.GetAsync(id, scheduled, ct, ConsistencyLevel.Strong);
            Assert.NotNull(loaded);
            Assert.Equal("assigned", loaded!.QueueStatus);
            Assert.Equal("worker-dal-test", loaded.AssignedWorkerId);

            var ok2 = await repo.TryClaimAsync(id, scheduled, "other-worker", DateTime.UtcNow.ToString("O"), ct);
            Assert.False(ok2);
        }
        finally
        {
            await repo.DeleteAsync(id, scheduled, ct);
        }
    }

    [Fact]
    public async Task QueryByAssignedWorker_returns_claimed_item()
    {
        using var test = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = test.Token;

        await _fixture.EnsureLocalRespondsAsync(ct);
        using var scope = _fixture.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IExecutionQueueRepository>();

        var id = $"dal-wk-{Guid.NewGuid():N}";
        var scheduled = DateTime.UtcNow.ToString("O");
        const string workerId = "worker-assignment-query";
        var item = new ExecutionQueueItem
        {
            QueueId = id,
            ScheduledFor = scheduled,
            JobId = "job-w",
            ScheduleId = "sched-w",
            QueueStatus = "pending",
            Priority = 5,
            RetryCount = 0,
            MaxRetries = 3
        };

        try
        {
            await repo.PutAsync(item, ct);
            var at = DateTime.UtcNow.ToString("O");
            await repo.TryClaimAsync(id, scheduled, workerId, at, ct);

            var list = await repo.QueryByAssignedWorkerAsync(workerId, limit: 100, ConsistencyLevel.Eventual, ct);
            Assert.Contains(list, x => x.QueueId == id);
        }
        finally
        {
            await repo.DeleteAsync(id, scheduled, ct);
        }
    }

    [Fact]
    public async Task QueryByJobId_returns_items_for_partition_newest_first()
    {
        using var test = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = test.Token;

        await _fixture.EnsureLocalRespondsAsync(ct);
        using var scope = _fixture.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IExecutionQueueRepository>();

        var jobId = $"job-hist-{Guid.NewGuid():N}";
        var q1 = $"dal-hist-a-{Guid.NewGuid():N}";
        var q2 = $"dal-hist-b-{Guid.NewGuid():N}";
        var older = "2026-01-02T10:00:00.0000000Z";
        var newer = "2026-06-02T10:00:00.0000000Z";

        try
        {
            await repo.PutAsync(
                new ExecutionQueueItem
                {
                    QueueId = q1,
                    ScheduledFor = older,
                    JobId = jobId,
                    ScheduleId = "s1",
                    QueueStatus = "completed",
                    Priority = 5,
                    RetryCount = 0,
                    MaxRetries = 3,
                    StartedAt = older,
                    CompletedAt = older
                },
                ct);

            await repo.PutAsync(
                new ExecutionQueueItem
                {
                    QueueId = q2,
                    ScheduledFor = newer,
                    JobId = jobId,
                    ScheduleId = "s2",
                    QueueStatus = "completed",
                    Priority = 5,
                    RetryCount = 0,
                    MaxRetries = 3,
                    StartedAt = newer,
                    CompletedAt = newer
                },
                ct);

            var (page, next) = await repo.QueryByJobIdAsync(jobId, limit: 10, paginationToken: null, scanIndexForward: false, ConsistencyLevel.Eventual, ct);
            Assert.Null(next);
            Assert.Equal(2, page.Count);
            Assert.Equal(newer, page[0].ScheduledFor);
            Assert.Equal(older, page[1].ScheduledFor);
        }
        finally
        {
            await repo.DeleteAsync(q1, older, ct);
            await repo.DeleteAsync(q2, newer, ct);
        }
    }

    [Fact]
    public async Task QueryByJobId_invalid_cursor_throws_ArgumentException()
    {
        using var test = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = test.Token;

        await _fixture.EnsureLocalRespondsAsync(ct);
        using var scope = _fixture.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IExecutionQueueRepository>();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repo.QueryByJobIdAsync("any-job", 5, "not-a-valid-token", false, ConsistencyLevel.Eventual, ct));
    }
}
