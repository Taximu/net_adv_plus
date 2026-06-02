using JobScheduler.DAL.Models;

namespace JobScheduler.DAL.Repositories;

public interface IJobScheduleRepository
{
    Task<JobSchedule?> GetByIdAsync(Guid scheduleId);
    Task<IEnumerable<JobSchedule>> GetByJobIdAsync(Guid jobId);

    /// <summary>Inserts a schedule (UC 1.1 — job + schedule authoring). Uses write connection.</summary>
    Task<JobSchedule> CreateAsync(JobSchedule schedule);

    /// <summary>Updates an existing schedule. Uses write connection.</summary>
    Task<JobSchedule> UpdateAsync(JobSchedule schedule);

    /// <summary>Deletes a schedule (dependencies cascade per schema). Uses write connection.</summary>
    Task<bool> DeleteAsync(Guid scheduleId);
}
