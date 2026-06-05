using JobScheduler.DAL.Consistency;
using JobScheduler.DAL.DynamoDB.Models;
using JobScheduler.DAL.DynamoDB.Repositories;

namespace JobScheduler.BL.Services;

public sealed class ExecutionOrchestrationService(IExecutionQueueRepository queue) : IExecutionOrchestrationService
{
    public Task<IReadOnlyList<ExecutionQueueItem>> PeekPendingExecutionsAsync(int? limit = null, CancellationToken cancellationToken = default) =>
        queue.QueryByQueueStatusAsync("pending", limit, ConsistencyLevel.Eventual, cancellationToken);

    public Task<ExecutionQueueItem?> GetExecutionAsync(string queueId, string scheduledFor, CancellationToken cancellationToken = default) =>
        queue.GetAsync(queueId, scheduledFor, cancellationToken, ConsistencyLevel.Strong);

    public Task EnqueueExecutionAsync(ExecutionQueueItem item, CancellationToken cancellationToken = default) =>
        queue.PutAsync(item, cancellationToken);
}
