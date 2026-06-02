using JobScheduler.DAL.DynamoDB.Models;

namespace JobScheduler.DAL.DynamoDB.Repositories;

/// <summary>
/// UC 2.1 execution queue access (DynamoDB).
/// </summary>
public interface IExecutionQueueRepository
{
    Task PutAsync(ExecutionQueueItem item, CancellationToken cancellationToken = default);

    Task<ExecutionQueueItem?> GetAsync(string queueId, string scheduledFor, CancellationToken cancellationToken = default);

    /// <summary>Query <c>PendingExecutionsIndex</c> by <paramref name="queueStatus"/> (e.g. pending), ascending priority.</summary>
    Task<IReadOnlyList<ExecutionQueueItem>> QueryByQueueStatusAsync(string queueStatus, int? limit = null, CancellationToken cancellationToken = default);

    /// <summary>Query <c>WorkerAssignmentsIndex</c> by worker id, descending <c>assignedAt</c>.</summary>
    Task<IReadOnlyList<ExecutionQueueItem>> QueryByAssignedWorkerAsync(string assignedWorkerId, int? limit = null, CancellationToken cancellationToken = default);

    /// <summary>Conditional update: pending → assigned. Returns false if not pending.</summary>
    Task<bool> TryClaimAsync(string queueId, string scheduledFor, string workerId, string assignedAtIso, CancellationToken cancellationToken = default);

    Task DeleteAsync(string queueId, string scheduledFor, CancellationToken cancellationToken = default);
}
