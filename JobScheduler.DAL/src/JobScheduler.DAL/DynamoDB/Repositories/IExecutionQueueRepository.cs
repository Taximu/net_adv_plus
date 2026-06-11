using JobScheduler.DAL.Consistency;
using JobScheduler.DAL.DynamoDB.Models;

namespace JobScheduler.DAL.DynamoDB.Repositories;

/// <summary>
/// UC 2.1 execution queue access (DynamoDB).
/// </summary>
public interface IExecutionQueueRepository
{
    Task PutAsync(ExecutionQueueItem item, CancellationToken cancellationToken = default);

    /// <summary>
    /// <see cref="ConsistencyLevel.Strong"/> uses DynamoDB <c>ConsistentRead=true</c>;
    /// <see cref="ConsistencyLevel.Eventual"/> uses <c>ConsistentRead=false</c>.
    /// </summary>
    Task<ExecutionQueueItem?> GetAsync(
        string queueId,
        string scheduledFor,
        CancellationToken cancellationToken = default,
        ConsistencyLevel consistencyLevel = ConsistencyLevel.Strong);

    /// <summary>
    /// Queries a GSI — DynamoDB does not offer strongly consistent reads on GSIs; behavior is always eventually consistent.
    /// Pass <see cref="ConsistencyLevel.Eventual"/> from BL; <see cref="ConsistencyLevel.Strong"/> is accepted for API symmetry but is not enforceable on the index.
    /// </summary>
    Task<IReadOnlyList<ExecutionQueueItem>> QueryByQueueStatusAsync(
        string queueStatus,
        int? limit = null,
        ConsistencyLevel consistencyLevel = ConsistencyLevel.Eventual,
        CancellationToken cancellationToken = default);

    /// <summary>Query <c>WorkerAssignmentsIndex</c> by worker id, descending <c>assignedAt</c>. GSI — eventually consistent reads only.</summary>
    Task<IReadOnlyList<ExecutionQueueItem>> QueryByAssignedWorkerAsync(
        string assignedWorkerId,
        int? limit = null,
        ConsistencyLevel consistencyLevel = ConsistencyLevel.Eventual,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// UC 2.3 — query <c>JobExecutionsIndex</c> by <paramref name="jobId"/> (GSI partition key), ordered by <c>scheduledFor</c>.
    /// GSI reads are eventually consistent regardless of <paramref name="consistencyLevel"/>.
    /// </summary>
    Task<(IReadOnlyList<ExecutionQueueItem> Items, string? NextPaginationToken)> QueryByJobIdAsync(
        string jobId,
        int limit,
        string? paginationToken,
        bool scanIndexForward = false,
        ConsistencyLevel consistencyLevel = ConsistencyLevel.Eventual,
        CancellationToken cancellationToken = default);

    /// <summary>Conditional update: pending → assigned. Returns false if not pending.</summary>
    Task<bool> TryClaimAsync(string queueId, string scheduledFor, string workerId, string assignedAtIso, CancellationToken cancellationToken = default);

    Task DeleteAsync(string queueId, string scheduledFor, CancellationToken cancellationToken = default);
}
