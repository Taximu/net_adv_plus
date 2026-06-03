using JobScheduler.DAL.Models;

namespace JobScheduler.DAL.Repositories;

public interface IJobDefinitionRepository
{
    Task<JobDefinition> CreateAsync(JobDefinition job);
    Task<JobDefinition> UpdateAsync(JobDefinition job);
    Task<bool> DeleteAsync(Guid jobId);
    Task<JobDefinition> UpdateStatusAsync(Guid jobId, string status, string updatedBy);
    
    Task<JobDefinition?> GetByIdAsync(Guid jobId);
    Task<IEnumerable<JobDefinition>> GetByUserIdAsync(Guid userId);
    Task<IEnumerable<JobDefinition>> GetByStatusAsync(string status);
    Task<IEnumerable<JobDefinition>> GetActiveJobsAsync();
    Task<bool> ExistsByNameAsync(Guid userId, string name);
}
