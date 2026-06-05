using JobScheduler.DAL.Models;

namespace JobScheduler.BL.Services;

public interface IUserSchedulesService
{
    Task<IReadOnlyList<JobSchedule>> ListSchedulesForJobAsync(Guid userId, Guid jobId, CancellationToken cancellationToken = default);

    Task<JobSchedule> CreateScheduleAsync(Guid userId, JobSchedule schedule, CancellationToken cancellationToken = default);
}
