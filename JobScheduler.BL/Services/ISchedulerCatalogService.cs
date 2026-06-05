using JobScheduler.DAL.Models;

namespace JobScheduler.BL.Services;

/// <summary>Internal scheduler views — always use authoritative reads.</summary>
public interface ISchedulerCatalogService
{
    Task<IReadOnlyList<JobDefinition>> GetActiveJobsForWorkerAsync(CancellationToken cancellationToken = default);
}
