using JobScheduler.DAL.DynamoDB.Models;

namespace JobScheduler.BL.Services;

/// <summary>
/// UC 2.1 — execution queue orchestration. Chooses DynamoDB read semantics internally (no consistency parameter on HTTP API).
/// </summary>
public interface IExecutionOrchestrationService
{
    /// <summary>Eventually consistent GSI poll — suitable for worker discovery loops.</summary>
    Task<IReadOnlyList<ExecutionQueueItem>> PeekPendingExecutionsAsync(int? limit = null, CancellationToken cancellationToken = default);

    /// <summary>Strongly consistent <c>GetItem</c> for coordinators verifying a specific queue row.</summary>
    Task<ExecutionQueueItem?> GetExecutionAsync(string queueId, string scheduledFor, CancellationToken cancellationToken = default);

    /// <summary>Enqueues a new execution row (write path).</summary>
    Task EnqueueExecutionAsync(ExecutionQueueItem item, CancellationToken cancellationToken = default);
}
