using JobScheduler.DAL.Consistency;
using JobScheduler.DAL.Models;
using JobScheduler.DAL.Repositories;

namespace JobScheduler.BL.Services;

public sealed class UserJobsService : IUserJobsService
{
    private readonly IJobDefinitionRepository _jobs;
    private readonly ConsistencyManager _consistency;

    public UserJobsService(IJobDefinitionRepository jobs, ConsistencyManager consistency)
    {
        _jobs = jobs;
        _consistency = consistency;
    }

    public async Task<IReadOnlyList<JobDefinition>> ListMyJobsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var level = ReadPathLevel(userId);
        var rows = await _jobs.GetByUserIdAsync(userId, level, cancellationToken).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<JobDefinition?> GetJobAsync(Guid userId, Guid jobId, CancellationToken cancellationToken = default)
    {
        var level = ReadPathLevel(userId);
        var job = await _jobs.GetByIdAsync(jobId, level, cancellationToken).ConfigureAwait(false);
        if (job is null || job.UserId != userId)
            return null;
        return job;
    }

    public async Task<JobDefinition> CreateDraftJobAsync(Guid userId, string name, string jobType, string createdBy, CancellationToken cancellationToken = default)
    {
        var userKey = UserKey(userId);
        if (await _jobs.ExistsByNameAsync(userId, name, ConsistencyLevel.Strong, cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException($"A job named '{name}' already exists for this user.");

        var job = new JobDefinition
        {
            JobId = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Description = "draft",
            JobType = jobType,
            Status = "draft",
            CreatedBy = createdBy
        };

        var created = await _jobs.CreateAsync(job, cancellationToken).ConfigureAwait(false);
        _consistency.TrackWrite(userKey);
        return created;
    }

    private ConsistencyLevel ReadPathLevel(Guid userId) =>
        _consistency.IsReadAfterWriteApplicable(UserKey(userId))
            ? ConsistencyLevel.Strong
            : ConsistencyLevel.Eventual;

    private static string UserKey(Guid userId) => userId.ToString("N");
}
