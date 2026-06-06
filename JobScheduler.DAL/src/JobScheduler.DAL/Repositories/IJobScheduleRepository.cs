using JobScheduler.DAL.Consistency;
using JobScheduler.DAL.Models;

namespace JobScheduler.DAL.Repositories;

public interface IJobScheduleRepository
{
    /// <summary>
    /// Reads a schedule by id. <paramref name="scheduleId"/> is the PostgreSQL HASH partition key for <c>job_schedules</c>.
    /// </summary>
    Task<JobSchedule?> GetByIdAsync(Guid scheduleId, ConsistencyLevel consistencyLevel, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists schedules for a job. Predicate is <c>job_id</c> (not the HASH partition key); all child partitions may be scanned.
    /// </summary>
    Task<IEnumerable<JobSchedule>> GetByJobIdAsync(Guid jobId, ConsistencyLevel consistencyLevel, CancellationToken cancellationToken = default);

    /// <summary>Inserts a schedule (UC 1.1 — job + schedule authoring). Uses write connection. Partition is chosen from <see cref="JobSchedule.ScheduleId"/>.</summary>
    Task<JobSchedule> CreateAsync(JobSchedule schedule, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing schedule. Uses write connection. Row is located by HASH partition key <c>schedule_id</c> from <paramref name="schedule"/>.<see cref="JobSchedule.ScheduleId"/>.
    /// </summary>
    Task<JobSchedule> UpdateAsync(JobSchedule schedule, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a schedule (dependencies cascade per schema). Uses write connection. Row is located by <paramref name="scheduleId"/> (HASH key <c>schedule_id</c>).
    /// </summary>
    Task<bool> DeleteAsync(Guid scheduleId, CancellationToken cancellationToken = default);
}
