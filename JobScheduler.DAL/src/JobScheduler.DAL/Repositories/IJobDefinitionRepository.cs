using JobScheduler.DAL.Consistency;
using JobScheduler.DAL.Models;

namespace JobScheduler.DAL.Repositories;

public interface IJobDefinitionRepository
{
    Task<JobDefinition> CreateAsync(JobDefinition job, CancellationToken cancellationToken = default);
    Task<JobDefinition> UpdateAsync(JobDefinition job, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<JobDefinition> UpdateStatusAsync(Guid jobId, string status, string updatedBy, CancellationToken cancellationToken = default);

    Task<JobDefinition?> GetByIdAsync(Guid jobId, ConsistencyLevel consistencyLevel, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobDefinition>> GetByUserIdAsync(Guid userId, ConsistencyLevel consistencyLevel, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobDefinition>> GetByStatusAsync(string status, ConsistencyLevel consistencyLevel, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobDefinition>> GetActiveJobsAsync(ConsistencyLevel consistencyLevel, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(Guid userId, string name, ConsistencyLevel consistencyLevel, CancellationToken cancellationToken = default);
}
