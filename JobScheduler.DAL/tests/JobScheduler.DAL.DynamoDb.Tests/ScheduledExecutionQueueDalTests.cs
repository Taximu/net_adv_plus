using JobScheduler.DAL.Consistency;
using JobScheduler.DAL.DynamoDB.Models;
using JobScheduler.DAL.DynamoDB.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace JobScheduler.DAL.DynamoDb.Tests;

/// <summary>
/// UC 2.1 — Execute a job at a scheduled time: single flow over the execution queue
/// (enqueue → strongly consistent read of one row → eventually consistent poll → claim).
/// </summary>
[Collection("DynamoDbDal")]
public sealed class ScheduledExecutionQueueDalTests
{
    private readonly DynamoDbDalFixture _fixture;

    public ScheduledExecutionQueueDalTests(DynamoDbDalFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Uc21_enqueue_pending_strong_getitem_eventual_poll_then_claim_and_strong_verify()
    {
        using var test = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = test.Token;

        await _fixture.EnsureLocalRespondsAsync(ct);
        using var scope = _fixture.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IExecutionQueueRepository>();

        var id = $"uc21-{Guid.NewGuid():N}";
        var scheduled = DateTime.UtcNow.ToString("O");
        var item = new ExecutionQueueItem
        {
            QueueId = id,
            ScheduledFor = scheduled,
            JobId = "job-uc21",
            ScheduleId = "sched-uc21",
            QueueStatus = "pending",
            Priority = 1,
            RetryCount = 0,
            MaxRetries = 3,
            ExecutionContext = new ExecutionContextSnapshot
            {
                Environment = "prod",
                TriggerSource = "scheduler",
                UserId = "user-uc21"
            }
        };

        try
        {
            await repo.PutAsync(item, ct);

            var coordinatorView = await repo.GetAsync(id, scheduled, ct, ConsistencyLevel.Strong);
            Assert.NotNull(coordinatorView);
            Assert.Equal("pending", coordinatorView!.QueueStatus);

            var poll = await repo.QueryByQueueStatusAsync("pending", limit: 500, ConsistencyLevel.Eventual, ct);
            Assert.Contains(poll, x => x.QueueId == id && x.ScheduledFor == scheduled);

            var assignedAt = DateTime.UtcNow.ToString("O");
            Assert.True(await repo.TryClaimAsync(id, scheduled, "worker-uc21", assignedAt, ct));

            var afterClaim = await repo.GetAsync(id, scheduled, ct, ConsistencyLevel.Strong);
            Assert.NotNull(afterClaim);
            Assert.Equal("assigned", afterClaim!.QueueStatus);
            Assert.Equal("worker-uc21", afterClaim.AssignedWorkerId);
        }
        finally
        {
            await repo.DeleteAsync(id, scheduled, ct);
        }
    }
}
