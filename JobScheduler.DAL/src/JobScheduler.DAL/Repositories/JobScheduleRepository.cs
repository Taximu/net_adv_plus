using System.Data;
using Dapper;
using JobScheduler.DAL.Connection;
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

    public async Task<JobSchedule?> GetByIdAsync(Guid scheduleId, Guid? schedulePartitionKey = null)
    {
        var partitionKey = ResolveSchedulePartitionKey(scheduleId, schedulePartitionKey);

        const string sql = """
            SELECT * FROM job_schedules
            WHERE schedule_id = @SchedulePartitionKey;
            """;
        using var connection = await _connectionFactory.GetReadConnectionAsync();
        var row = await connection.QueryFirstOrDefaultAsync<JobSchedule>(sql, new { SchedulePartitionKey = partitionKey });
        if (row is null)
        {
            _logger.LogDebug(
                "JobSchedule GetById: ScheduleId={ScheduleId}, SchedulePartitionKey={SchedulePartitionKey}, PhysicalPartition=(none), RowFound=false",
                scheduleId,
                partitionKey);
            return null;
        }

        var physical = await GetPhysicalPartitionNameAsync(connection, partitionKey).ConfigureAwait(false);
        _logger.LogDebug(
            "JobSchedule GetById: ScheduleId={ScheduleId}, SchedulePartitionKey={SchedulePartitionKey}, PhysicalPartition={PhysicalPartition}, RowFound=true",
            scheduleId,
            partitionKey,
            physical ?? "(unknown)");
        return row;
    }

    public async Task<IEnumerable<JobSchedule>> GetByJobIdAsync(Guid jobId)
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

        using var connection = await _connectionFactory.GetReadConnectionAsync();
        var histogram = (await connection
                .QueryAsync<PartitionHistogramRow>(histogramSql, new { JobId = jobId })
                .ConfigureAwait(false))
            .ToList();

        var summary = histogram.Count == 0
            ? "(no rows)"
            : string.Join(", ", histogram.Select(h => $"{h.PartitionName}={h.RowCount}"));

        _logger.LogDebug(
            "JobSchedule GetByJobId: JobId={JobId} (predicate is not HASH partition key schedule_id; planner may touch multiple partitions). RowsPerPhysicalPartition=[{PartitionHistogram}]",
            jobId,
            summary);

        return await connection.QueryAsync<JobSchedule>(rowsSql, new { JobId = jobId }).ConfigureAwait(false);
    }

    public async Task<JobSchedule> CreateAsync(JobSchedule schedule)
    {
        if (schedule.ScheduleId == Guid.Empty)
            schedule.ScheduleId = Guid.NewGuid();

        var partitionKey = schedule.ScheduleId;

        const string sql = """
            INSERT INTO job_schedules (
                schedule_id, job_id, schedule_name, schedule_type, cron_expression, interval_seconds,
                timezone, start_date, end_date, start_time, end_time,
                is_enabled, allow_overlap, max_concurrent_executions, priority,
                next_execution_at, last_execution_at, execution_count, status
            ) VALUES (
                @SchedulePartitionKey, @JobId, @ScheduleName, @ScheduleType, @CronExpression, @IntervalSeconds,
                @Timezone, @StartDate, @EndDate, @StartTime, @EndTime,
                @IsEnabled, @AllowOverlap, @MaxConcurrentExecutions, @Priority,
                @NextExecutionAt, @LastExecutionAt, @ExecutionCount, @Status
            )
            RETURNING *;
            """;

        using var connection = await _connectionFactory.GetWriteConnectionAsync();
        var parameters = ToWriteParameters(schedule, partitionKey);
        var created = await connection.QuerySingleAsync<JobSchedule>(sql, parameters).ConfigureAwait(false);

        var physical = await GetPhysicalPartitionNameAsync(connection, partitionKey).ConfigureAwait(false);
        _logger.LogDebug(
            "JobSchedule Create: ScheduleId={ScheduleId}, SchedulePartitionKey={SchedulePartitionKey}, PhysicalPartition={PhysicalPartition}",
            created.ScheduleId,
            partitionKey,
            physical ?? "(unknown)");

        return created;
    }

    public async Task<JobSchedule> UpdateAsync(JobSchedule schedule, Guid? schedulePartitionKey = null)
    {
        var partitionKey = ResolveSchedulePartitionKey(schedule.ScheduleId, schedulePartitionKey);

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
            WHERE schedule_id = @SchedulePartitionKey
            RETURNING *;
            """;

        using var connection = await _connectionFactory.GetWriteConnectionAsync();
        var parameters = ToWriteParameters(schedule, partitionKey);
        var updated = await connection.QuerySingleAsync<JobSchedule>(sql, parameters).ConfigureAwait(false);

        var physical = await GetPhysicalPartitionNameAsync(connection, partitionKey).ConfigureAwait(false);
        _logger.LogDebug(
            "JobSchedule Update: ScheduleId={ScheduleId}, SchedulePartitionKey={SchedulePartitionKey}, PhysicalPartition={PhysicalPartition}",
            updated.ScheduleId,
            partitionKey,
            physical ?? "(unknown)");

        return updated;
    }

    public async Task<bool> DeleteAsync(Guid scheduleId, Guid? schedulePartitionKey = null)
    {
        var partitionKey = ResolveSchedulePartitionKey(scheduleId, schedulePartitionKey);

        using var connection = await _connectionFactory.GetWriteConnectionAsync();
        var physicalBefore = await GetPhysicalPartitionNameAsync(connection, partitionKey).ConfigureAwait(false);

        const string sql = """
            DELETE FROM job_schedules
            WHERE schedule_id = @SchedulePartitionKey;
            """;
        var rows = await connection.ExecuteAsync(sql, new { SchedulePartitionKey = partitionKey }).ConfigureAwait(false);

        if (rows > 0)
        {
            _logger.LogDebug(
                "JobSchedule Delete: ScheduleId={ScheduleId}, SchedulePartitionKey={SchedulePartitionKey}, PhysicalPartitionBeforeDelete={PhysicalPartition}, RowsDeleted={RowsDeleted}",
                scheduleId,
                partitionKey,
                physicalBefore ?? "(unknown)",
                rows);
        }
        else
        {
            _logger.LogDebug(
                "JobSchedule Delete: ScheduleId={ScheduleId}, SchedulePartitionKey={SchedulePartitionKey}, PhysicalPartitionBeforeDelete={PhysicalPartition}, RowsDeleted=0",
                scheduleId,
                partitionKey,
                physicalBefore ?? "(none)");
        }

        return rows > 0;
    }

    private static Guid ResolveSchedulePartitionKey(Guid scheduleId, Guid? schedulePartitionKey)
    {
        var key = schedulePartitionKey ?? scheduleId;
        if (schedulePartitionKey.HasValue && schedulePartitionKey.Value != scheduleId)
        {
            throw new ArgumentException(
                "schedulePartitionKey must equal scheduleId / JobSchedule.ScheduleId for HASH(schedule_id) partitioning.",
                nameof(schedulePartitionKey));
        }

        return key;
    }

    private static async Task<string?> GetPhysicalPartitionNameAsync(IDbConnection connection, Guid schedulePartitionKey)
    {
        const string sql = """
            SELECT tableoid::regclass::text
            FROM job_schedules
            WHERE schedule_id = @SchedulePartitionKey;
            """;
        return await connection.QuerySingleOrDefaultAsync<string?>(sql, new { SchedulePartitionKey = schedulePartitionKey })
            .ConfigureAwait(false);
    }

    /// <summary>Dapper does not bind <see cref="DateOnly"/> / <see cref="TimeOnly"/> on the entity as parameters; map to types Npgsql accepts.</summary>
    private static object ToWriteParameters(JobSchedule schedule, Guid schedulePartitionKey) => new
    {
        SchedulePartitionKey = schedulePartitionKey,
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
