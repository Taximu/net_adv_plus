using JobScheduler.DAL.Models;
using JobScheduler.DAL.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace JobScheduler.DAL.Postgres.Tests;

[Collection("PostgresDal")]
public sealed class JobScheduleRepositoryTests
{
    private readonly PostgresDalFixture _fixture;

    public JobScheduleRepositoryTests(PostgresDalFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task CreateAsync_then_GetByIdAsync_roundtrips()
    {
        using var scope = _fixture.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IJobScheduleRepository>();

        var schedule = new JobSchedule
        {
            JobId = _fixture.SeedJobId,
            ScheduleName = $"dal-create-{Guid.NewGuid():N}",
            ScheduleType = "cron",
            CronExpression = "0 3 * * *",
            Timezone = "UTC",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            IsEnabled = true,
            Priority = 4,
            Status = "active",
            NextExecutionAt = DateTime.UtcNow.AddHours(1)
        };

        var created = await repo.CreateAsync(schedule);
        try
        {
            Assert.NotEqual(Guid.Empty, created.ScheduleId);
            Assert.Equal(schedule.ScheduleName, created.ScheduleName);

            var loaded = await repo.GetByIdAsync(created.ScheduleId);
            Assert.NotNull(loaded);
            Assert.Equal(created.ScheduleId, loaded!.ScheduleId);
            Assert.Equal("0 3 * * *", loaded.CronExpression);
        }
        finally
        {
            await repo.DeleteAsync(created.ScheduleId);
        }
    }

    [Fact]
    public async Task CreateAsync_UpdateAsync_persists_changes()
    {
        using var scope = _fixture.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IJobScheduleRepository>();

        var created = await repo.CreateAsync(new JobSchedule
        {
            JobId = _fixture.SeedJobId,
            ScheduleName = $"dal-upd-{Guid.NewGuid():N}",
            ScheduleType = "interval",
            IntervalSeconds = 3600,
            Timezone = "UTC",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            IsEnabled = true,
            Priority = 2,
            Status = "active"
        });

        try
        {
            created.ScheduleName = $"{created.ScheduleName}-renamed";
            created.Priority = 9;
            created.IsEnabled = false;
            var updated = await repo.UpdateAsync(created);
            Assert.Equal(created.ScheduleName, updated.ScheduleName);
            Assert.Equal(9, updated.Priority);
            Assert.False(updated.IsEnabled);

            var loaded = await repo.GetByIdAsync(created.ScheduleId);
            Assert.Equal(created.ScheduleName, loaded!.ScheduleName);
            Assert.Equal(9, loaded.Priority);
        }
        finally
        {
            await repo.DeleteAsync(created.ScheduleId);
        }
    }

    [Fact]
    public async Task CreateAsync_DeleteAsync_removes_row()
    {
        using var scope = _fixture.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IJobScheduleRepository>();

        var created = await repo.CreateAsync(new JobSchedule
        {
            JobId = _fixture.SeedJobId,
            ScheduleName = $"dal-del-{Guid.NewGuid():N}",
            ScheduleType = "cron",
            CronExpression = "0 0 * * *",
            Timezone = "UTC",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            IsEnabled = true,
            Priority = 1,
            Status = "active"
        });

        Assert.True(await repo.DeleteAsync(created.ScheduleId));
        Assert.Null(await repo.GetByIdAsync(created.ScheduleId));
        Assert.False(await repo.DeleteAsync(created.ScheduleId));
    }

    [Fact]
    public async Task CreateAsync_GetByJobIdAsync_lists_schedule()
    {
        using var scope = _fixture.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IJobScheduleRepository>();

        var created = await repo.CreateAsync(new JobSchedule
        {
            JobId = _fixture.SeedJobId,
            ScheduleName = $"dal-list-{Guid.NewGuid():N}",
            ScheduleType = "cron",
            CronExpression = "15 4 * * *",
            Timezone = "Europe/Berlin",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            IsEnabled = true,
            Priority = 6,
            Status = "active"
        });

        try
        {
            var list = await repo.GetByJobIdAsync(_fixture.SeedJobId);
            Assert.Contains(list, s => s.ScheduleId == created.ScheduleId);
        }
        finally
        {
            await repo.DeleteAsync(created.ScheduleId);
        }
    }
}
