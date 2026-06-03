using Dapper;
using JobScheduler.DAL.Connection;
using JobScheduler.DAL.Models;

namespace JobScheduler.DAL.Repositories;

public class JobScheduleRepository : IJobScheduleRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public JobScheduleRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<JobSchedule?> GetByIdAsync(Guid scheduleId)
    {
        const string sql = @"SELECT * FROM job_schedules WHERE schedule_id = @ScheduleId";
        using var connection = await _connectionFactory.GetReadConnectionAsync();
        return await connection.QueryFirstOrDefaultAsync<JobSchedule>(sql, new { ScheduleId = scheduleId });
    }

    public async Task<IEnumerable<JobSchedule>> GetByJobIdAsync(Guid jobId)
    {
        const string sql = @"SELECT * FROM job_schedules WHERE job_id = @JobId ORDER BY created_at DESC";
        using var connection = await _connectionFactory.GetReadConnectionAsync();
        return await connection.QueryAsync<JobSchedule>(sql, new { JobId = jobId });
    }

    public async Task<JobSchedule> CreateAsync(JobSchedule schedule)
    {
        if (schedule.ScheduleId == Guid.Empty)
            schedule.ScheduleId = Guid.NewGuid();

        const string sql = """
            INSERT INTO job_schedules (
                schedule_id, job_id, schedule_name, schedule_type, cron_expression, interval_seconds,
                timezone, start_date, end_date, start_time, end_time,
                is_enabled, allow_overlap, max_concurrent_executions, priority,
                next_execution_at, last_execution_at, execution_count, status
            ) VALUES (
                @ScheduleId, @JobId, @ScheduleName, @ScheduleType, @CronExpression, @IntervalSeconds,
                @Timezone, @StartDate, @EndDate, @StartTime, @EndTime,
                @IsEnabled, @AllowOverlap, @MaxConcurrentExecutions, @Priority,
                @NextExecutionAt, @LastExecutionAt, @ExecutionCount, @Status
            )
            RETURNING *;
            """;

        using var connection = await _connectionFactory.GetWriteConnectionAsync();
        return await connection.QuerySingleAsync<JobSchedule>(sql, ToWriteParameters(schedule));
    }

    public async Task<JobSchedule> UpdateAsync(JobSchedule schedule)
    {
        const string sql = """
            UPDATE job_schedules SET
                job_id = @JobId,
                schedule_name = @ScheduleName,
                schedule_type = @ScheduleType,
                cron_expression = @CronExpression,
                interval_seconds = @IntervalSeconds,
                timezone = @Timezone,
                start_date = @StartDate,
                end_date = @EndDate,
                start_time = @StartTime,
                end_time = @EndTime,
                is_enabled = @IsEnabled,
                allow_overlap = @AllowOverlap,
                max_concurrent_executions = @MaxConcurrentExecutions,
                priority = @Priority,
                next_execution_at = @NextExecutionAt,
                last_execution_at = @LastExecutionAt,
                execution_count = @ExecutionCount,
                status = @Status,
                updated_at = CURRENT_TIMESTAMP
            WHERE schedule_id = @ScheduleId
            RETURNING *;
            """;

        using var connection = await _connectionFactory.GetWriteConnectionAsync();
        return await connection.QuerySingleAsync<JobSchedule>(sql, ToWriteParameters(schedule));
    }

    public async Task<bool> DeleteAsync(Guid scheduleId)
    {
        const string sql = @"DELETE FROM job_schedules WHERE schedule_id = @ScheduleId";
        using var connection = await _connectionFactory.GetWriteConnectionAsync();
        var rows = await connection.ExecuteAsync(sql, new { ScheduleId = scheduleId });
        return rows > 0;
    }

    /// <summary>Dapper does not bind <see cref="DateOnly"/> / <see cref="TimeOnly"/> on the entity as parameters; map to types Npgsql accepts.</summary>
    private static object ToWriteParameters(JobSchedule schedule) => new
    {
        schedule.ScheduleId,
        schedule.JobId,
        schedule.ScheduleName,
        schedule.ScheduleType,
        schedule.CronExpression,
        schedule.IntervalSeconds,
        schedule.Timezone,
        StartDate = schedule.StartDate.ToDateTime(TimeOnly.MinValue).Date,
        EndDate = schedule.EndDate.HasValue ? schedule.EndDate.Value.ToDateTime(TimeOnly.MinValue).Date : (DateTime?)null,
        StartTime = schedule.StartTime.HasValue ? schedule.StartTime.Value.ToTimeSpan() : (TimeSpan?)null,
        EndTime = schedule.EndTime.HasValue ? schedule.EndTime.Value.ToTimeSpan() : (TimeSpan?)null,
        schedule.IsEnabled,
        schedule.AllowOverlap,
        schedule.MaxConcurrentExecutions,
        schedule.Priority,
        schedule.NextExecutionAt,
        schedule.LastExecutionAt,
        schedule.ExecutionCount,
        schedule.Status
    };
}
