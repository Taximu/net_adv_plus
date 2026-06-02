using JobScheduler.DAL.DynamoDB.Models;

namespace JobScheduler.DAL.DynamoDB.Repositories;

/// <summary>
/// Worker registry (DynamoDB) for UC 2.1.
/// </summary>
public interface IWorkerNodeRepository
{
    Task PutAsync(WorkerNode node, CancellationToken cancellationToken = default);

    Task<WorkerNode?> GetAsync(string workerId, string registeredAt, CancellationToken cancellationToken = default);

    Task DeleteAsync(string workerId, string registeredAt, CancellationToken cancellationToken = default);
}
