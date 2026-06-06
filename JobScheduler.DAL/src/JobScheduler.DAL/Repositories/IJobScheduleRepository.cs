using JobScheduler.DAL.Models;

namespace JobScheduler.DAL.Repositories;

public interface IJobScheduleRepository
{
    /// <summary>
    /// Reads a schedule by id. <paramref name="scheduleId"/> is the PostgreSQL HASH partition key for <c>job_schedules</c>.
    /// </summary>
    /// <param name="schedulePartitionKey">
    /// Optional explicit partition key; must equal <paramref name="scheduleId"/> (HASH is on <c>schedule_id</c>).
    /// When omitted, <paramref name="scheduleId"/> is used as the partition predicate.
    /// </param>
    Task<JobSchedule?> GetByIdAsync(Guid scheduleId, Guid? schedulePartitionKey = null);

    /// <summary>
    /// Lists schedules for a job. Predicate is <c>job_id</c> (not the HASH partition key); all child partitions may be scanned.
    /// </summary>
    Task<IEnumerable<JobSchedule>> GetByJobIdAsync(Guid jobId);

    /// <summary>Inserts a schedule (UC 1.1 — job + schedule authoring). Uses write connection. Partition is chosen from <see cref="JobSchedule.ScheduleId"/>.</summary>
    Task<JobSchedule> CreateAsync(JobSchedule schedule);

    /// <summary>
    /// Updates an existing schedule. Uses write connection. Row is located by HASH partition key <c>schedule_id</c>.
    /// </summary>
    /// <param name="schedulePartitionKey">
    /// Optional explicit partition key; must equal <paramref name="schedule"/>.<see cref="JobSchedule.ScheduleId"/> when provided.
    /// </param>
    Task<JobSchedule> UpdateAsync(JobSchedule schedule, Guid? schedulePartitionKey = null);

    /// <summary>
    /// Deletes a schedule (dependencies cascade per schema). Uses write connection.
    /// </summary>
    /// <param name="schedulePartitionKey">
    /// Optional explicit partition key; must equal <paramref name="scheduleId"/> when provided.
    /// </param>
    Task<bool> DeleteAsync(Guid scheduleId, Guid? schedulePartitionKey = null);
}
