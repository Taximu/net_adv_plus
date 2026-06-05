using JobScheduler.DAL.Consistency;
using JobScheduler.DAL.Models;
using JobScheduler.DAL.Repositories;

namespace JobScheduler.BL.Services;

public sealed class SchedulerCatalogService(IJobDefinitionRepository jobs) : ISchedulerCatalogService
{
    public async Task<IReadOnlyList<JobDefinition>> GetActiveJobsForWorkerAsync(CancellationToken cancellationToken = default)
    {
        var rows = await jobs.GetActiveJobsAsync(ConsistencyLevel.Strong, cancellationToken).ConfigureAwait(false);
        return [.. rows];
    }
}
