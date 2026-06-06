using System.Data;
using Dapper;
using JobScheduler.DAL.Connection;
using JobScheduler.DAL.Consistency;
using JobScheduler.DAL.Models;
using Microsoft.Extensions.Logging;

namespace JobScheduler.DAL.Repositories;


public class JobScheduleRepository : IJobScheduleRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<JobScheduleRepository> _logger;

    public JobScheduleRepository(IDbConnectionFactory connectionFactory, ILogger<JobScheduleRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<JobSchedule?> GetByIdAsync(
        Guid scheduleId,
        ConsistencyLevel consistencyLevel,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT * FROM job_schedules
            WHERE schedule_id = @ScheduleId;
            """;
        using var connection = await _connectionFactory.GetReadConnectionAsync(consistencyLevel, cancellationToken).ConfigureAwait(false);
        var row = await connection.QueryFirstOrDefaultAsync<JobSchedule>(
                new CommandDefinition(sql, new { ScheduleId = scheduleId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        if (row is null)
        {
            _logger.LogDebug(
                "JobSchedule GetById: ScheduleId={ScheduleId}, PhysicalPartition=(none), RowFound=false",
                scheduleId);
            return null;
        }

        var physical = await GetPhysicalPartitionNameAsync(connection, scheduleId, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug(
            "JobSchedule GetById: ScheduleId={ScheduleId}, PhysicalPartition={PhysicalPartition}, RowFound=true",
            scheduleId,
            physical ?? "(unknown)");
        return row;
    }

    public async Task<IEnumerable<JobSchedule>> GetByJobIdAsync(Guid jobId, ConsistencyLevel consistencyLevel, CancellationToken cancellationToken = default)
    {
        const string histogramSql = """
            SELECT tableoid::regclass::text AS partition_name, COUNT(*)::bigint AS row_count
            FROM job_schedules
            WHERE job_id = @JobId
            GROUP BY tableoid
            ORDER BY 1;
            """;

        const string rowsSql = """
            SELECT * FROM job_schedules
            WHERE job_id = @JobId
            ORDER BY created_at DESC;
            """;

        using var connection = await _connectionFactory.GetReadConnectionAsync(consistencyLevel, cancellationToken).ConfigureAwait(false);
        var histogram = (await connection
                .QueryAsync<PartitionHistogramRow>(
                    new CommandDefinition(histogramSql, new { JobId = jobId }, cancellationToken: cancellationToken))
                .ConfigureAwait(false))
            .ToList();

        var summary = histogram.Count == 0
            ? "(no rows)"
            : string.Join(", ", histogram.Select(h => $"{h.PartitionName}={h.RowCount}"));

        _logger.LogDebug(
            "JobSchedule GetByJobId: JobId={JobId} (predicate is not HASH partition key schedule_id; planner may touch multiple partitions). RowsPerPhysicalPartition=[{PartitionHistogram}]",
            jobId,
            summary);

        return await connection.QueryAsync<JobSchedule>(new CommandDefinition(rowsSql, new { JobId = jobId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<JobSchedule> CreateAsync(JobSchedule schedule, CancellationToken cancellationToken = default)
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

        using var connection = await _connectionFactory.GetWriteConnectionAsync(cancellationToken).ConfigureAwait(false);
        var parameters = ToWriteParameters(schedule);
        var created = await connection.QuerySingleAsync<JobSchedule>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        var physical = await GetPhysicalPartitionNameAsync(connection, schedule.ScheduleId, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug(
            "JobSchedule Create: ScheduleId={ScheduleId}, PhysicalPartition={PhysicalPartition}",
            created.ScheduleId,
            physical ?? "(unknown)");

        return created;
    }

    public async Task<JobSchedule> UpdateAsync(JobSchedule schedule, CancellationToken cancellationToken = default)
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

        using var connection = await _connectionFactory.GetWriteConnectionAsync(cancellationToken).ConfigureAwait(false);
        var parameters = ToWriteParameters(schedule);
        var updated = await connection.QuerySingleAsync<JobSchedule>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        var physical = await GetPhysicalPartitionNameAsync(connection, schedule.ScheduleId, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug(
            "JobSchedule Update: ScheduleId={ScheduleId}, PhysicalPartition={PhysicalPartition}",
            updated.ScheduleId,
            physical ?? "(unknown)");

        return updated;
    }

    public async Task<bool> DeleteAsync(Guid scheduleId, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.GetWriteConnectionAsync(cancellationToken).ConfigureAwait(false);
        var physicalBefore = await GetPhysicalPartitionNameAsync(connection, scheduleId, cancellationToken).ConfigureAwait(false);

        const string sql = """
            DELETE FROM job_schedules
            WHERE schedule_id = @ScheduleId;
            """;
        var rows = await connection.ExecuteAsync(new CommandDefinition(sql, new { ScheduleId = scheduleId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (rows > 0)
        {
            _logger.LogDebug(
                "JobSchedule Delete: ScheduleId={ScheduleId}, PhysicalPartitionBeforeDelete={PhysicalPartition}, RowsDeleted={RowsDeleted}",
                scheduleId,
                physicalBefore ?? "(unknown)",
                rows);
        }
        else
        {
            _logger.LogDebug(
                "JobSchedule Delete: ScheduleId={ScheduleId}, PhysicalPartitionBeforeDelete={PhysicalPartition}, RowsDeleted=0",
                scheduleId,
                physicalBefore ?? "(none)");
        }

        return rows > 0;
    }

    private static async Task<string?> GetPhysicalPartitionNameAsync(IDbConnection connection, Guid scheduleId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT tableoid::regclass::text
            FROM job_schedules
            WHERE schedule_id = @ScheduleId;
            """;
        return await connection.QuerySingleOrDefaultAsync<string?>(
                new CommandDefinition(sql, new { ScheduleId = scheduleId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
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

    private sealed class PartitionHistogramRow
    {
        public string PartitionName { get; init; } = "";
        public long RowCount { get; init; }
    }
}
