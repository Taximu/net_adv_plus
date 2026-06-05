using JobScheduler.DAL.Models;

namespace JobScheduler.BL.Services;

/// <summary>User-facing job flows — consistency is chosen internally.</summary>
public interface IUserJobsService
{
    Task<IReadOnlyList<JobDefinition>> ListMyJobsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<JobDefinition?> GetJobAsync(Guid userId, Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>Creates a minimal draft job and starts read-after-write tracking for <paramref name="userId"/>.</summary>
    Task<JobDefinition> CreateDraftJobAsync(Guid userId, string name, string jobType, string createdBy, CancellationToken cancellationToken = default);
}
