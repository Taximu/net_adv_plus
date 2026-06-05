using JobScheduler.DAL.Consistency;
using JobScheduler.DAL.Models;
using JobScheduler.DAL.Repositories;

namespace JobScheduler.BL.Services;

public sealed class UserSchedulesService : IUserSchedulesService
{
    private readonly IJobDefinitionRepository _jobs;
    private readonly IJobScheduleRepository _schedules;
    private readonly ConsistencyManager _consistency;

    public UserSchedulesService(
        IJobDefinitionRepository jobs,
        IJobScheduleRepository schedules,
        ConsistencyManager consistency)
    {
        _jobs = jobs;
        _schedules = schedules;
        _consistency = consistency;
    }

    public async Task<IReadOnlyList<JobSchedule>> ListSchedulesForJobAsync(Guid userId, Guid jobId, CancellationToken cancellationToken = default)
    {
        var owner = await _jobs.GetByIdAsync(jobId, ConsistencyLevel.Strong, cancellationToken).ConfigureAwait(false);
        if (owner is null || owner.UserId != userId)
            return Array.Empty<JobSchedule>();

        var level = _consistency.IsReadAfterWriteApplicable(UserKey(userId))
            ? ConsistencyLevel.Strong
            : ConsistencyLevel.Eventual;

        var rows = await _schedules.GetByJobIdAsync(jobId, level, cancellationToken).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<JobSchedule> CreateScheduleAsync(Guid userId, JobSchedule schedule, CancellationToken cancellationToken = default)
    {
        var owner = await _jobs.GetByIdAsync(schedule.JobId, ConsistencyLevel.Strong, cancellationToken).ConfigureAwait(false);
        if (owner is null || owner.UserId != userId)
            throw new InvalidOperationException("Job not found or access denied.");

        var created = await _schedules.CreateAsync(schedule, cancellationToken).ConfigureAwait(false);
        _consistency.TrackWrite(UserKey(userId));
        return created;
    }

    private static string UserKey(Guid userId) => userId.ToString("N");
}
