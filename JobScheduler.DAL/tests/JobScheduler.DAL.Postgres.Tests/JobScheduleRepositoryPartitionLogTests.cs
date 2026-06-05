using System.Text.RegularExpressions;
using JobScheduler.DAL.Consistency;
using JobScheduler.DAL.Models;
using JobScheduler.DAL.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace JobScheduler.DAL.Postgres.Tests;

[Collection("PostgresDal")]
public sealed class JobScheduleRepositoryPartitionLogTests
{
    private static readonly Regex PhysicalPartitionPattern = new(
        @"PhysicalPartition=job_schedules_p[0-3]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly PostgresDalFixture _fixture;

    public JobScheduleRepositoryPartitionLogTests(PostgresDalFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task CreateAsync_emits_debug_log_with_physical_child_partition_name()
    {
        _fixture.JobScheduleLogCapture.Clear();
        using var scope = _fixture.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IJobScheduleRepository>();

        var created = await repo.CreateAsync(new JobSchedule
        {
            JobId = _fixture.SeedJobId,
            ScheduleName = $"dal-log-create-{Guid.NewGuid():N}",
            ScheduleType = "cron",
            CronExpression = "0 5 * * *",
            Timezone = "UTC",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            IsEnabled = true,
            Priority = 3,
            Status = "active"
        });

        try
        {
            var logs = string.Join(Environment.NewLine, _fixture.JobScheduleLogCapture.Snapshot());
            Assert.Contains("JobSchedule Create:", logs);
            Assert.Matches(PhysicalPartitionPattern, logs);
        }
        finally
        {
            await repo.DeleteAsync(created.ScheduleId);
        }
    }

    [Fact]
    public async Task GetByIdAsync_emits_debug_log_with_physical_partition_when_row_exists()
    {
        _fixture.JobScheduleLogCapture.Clear();
        using var scope = _fixture.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IJobScheduleRepository>();

        var created = await repo.CreateAsync(new JobSchedule
        {
            JobId = _fixture.SeedJobId,
            ScheduleName = $"dal-log-get-{Guid.NewGuid():N}",
            ScheduleType = "interval",
            IntervalSeconds = 900,
            Timezone = "UTC",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            IsEnabled = true,
            Priority = 4,
            Status = "active"
        });

        try
        {
            _fixture.JobScheduleLogCapture.Clear();
            _ = await repo.GetByIdAsync(created.ScheduleId, ConsistencyLevel.Eventual);
            var logs = string.Join(Environment.NewLine, _fixture.JobScheduleLogCapture.Snapshot());
            Assert.Contains("JobSchedule GetById:", logs);
            Assert.Contains("RowFound=true", logs);
            Assert.Matches(PhysicalPartitionPattern, logs);
        }
        finally
        {
            await repo.DeleteAsync(created.ScheduleId);
        }
    }

    [Fact]
    public async Task GetByJobIdAsync_emits_debug_log_with_partition_histogram()
    {
        _fixture.JobScheduleLogCapture.Clear();
        using var scope = _fixture.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IJobScheduleRepository>();

        var created = await repo.CreateAsync(new JobSchedule
        {
            JobId = _fixture.SeedJobId,
            ScheduleName = $"dal-log-hist-{Guid.NewGuid():N}",
            ScheduleType = "cron",
            CronExpression = "30 6 * * *",
            Timezone = "UTC",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            IsEnabled = true,
            Priority = 5,
            Status = "active"
        });

        try
        {
            _fixture.JobScheduleLogCapture.Clear();
            _ = await repo.GetByJobIdAsync(_fixture.SeedJobId, ConsistencyLevel.Eventual);
            var logs = string.Join(Environment.NewLine, _fixture.JobScheduleLogCapture.Snapshot());
            Assert.Contains("JobSchedule GetByJobId:", logs);
            Assert.Contains("RowsPerPhysicalPartition=", logs);
            Assert.Contains("job_schedules_p", logs);
        }
        finally
        {
            await repo.DeleteAsync(created.ScheduleId);
        }
    }
}
