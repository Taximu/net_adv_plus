using JobScheduler.DAL.Consistency;
using JobScheduler.DAL.DynamoDB.Models;

namespace JobScheduler.DAL.DynamoDB.Repositories;

/// <summary>
/// Worker registry (DynamoDB) for UC 2.1.
/// </summary>
public interface IWorkerNodeRepository
{
    Task PutAsync(WorkerNode node, CancellationToken cancellationToken = default);

    /// <summary>
    /// <see cref="ConsistencyLevel.Strong"/> uses <c>ConsistentRead=true</c>; <see cref="ConsistencyLevel.Eventual"/> uses <c>ConsistentRead=false</c>.
    /// </summary>
    Task<WorkerNode?> GetAsync(
        string workerId,
        string registeredAt,
        CancellationToken cancellationToken = default,
        ConsistencyLevel consistencyLevel = ConsistencyLevel.Strong);

    Task DeleteAsync(string workerId, string registeredAt, CancellationToken cancellationToken = default);
}
