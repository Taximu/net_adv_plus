using JobScheduler.DAL.Consistency;
using JobScheduler.DAL.Models;
using JobScheduler.DAL.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace JobScheduler.DAL.Postgres.Tests;

/// <summary>
/// UC 1.1 — Create / manage a job (PostgreSQL catalog): DAL-level flow using
/// <see cref="IJobDefinitionRepository"/> with explicit <see cref="ConsistencyLevel"/> on reads
/// (same pattern as <c>GetJobByIdAsync(id, level)</c> / <c>GetJobsAsync(level)</c> in coursework examples).
/// </summary>
[Collection("PostgresDal")]
public sealed class CreateJobCatalogDalTests
{
    private readonly PostgresDalFixture _fixture;

    public CreateJobCatalogDalTests(PostgresDalFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Uc11_create_job_uses_strong_for_name_check_then_create_then_reads_by_id_and_list()
    {
        using var scope = _fixture.Services.CreateScope();
        var jobs = scope.ServiceProvider.GetRequiredService<IJobDefinitionRepository>();
        var userId = _fixture.SeedUserId;
        var name = $"uc11-job-{Guid.NewGuid():N}";

        Assert.False(await jobs.ExistsByNameAsync(userId, name, ConsistencyLevel.Strong));

        var draft = new JobDefinition
        {
            JobId = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Description = "UC 1.1 DAL integration",
            JobType = "api_call",
            Status = "draft",
            CreatedBy = "uc11-dal-test"
        };

        var created = await jobs.CreateAsync(draft);
        try
        {
            Assert.Equal(name, created.Name);
            Assert.True(await jobs.ExistsByNameAsync(userId, name, ConsistencyLevel.Strong));

            var byId = await jobs.GetByIdAsync(created.JobId, ConsistencyLevel.Strong);
            Assert.NotNull(byId);
            Assert.Equal(name, byId!.Name);

            var list = await jobs.GetByUserIdAsync(userId, ConsistencyLevel.Eventual);
            Assert.Contains(list, j => j.JobId == created.JobId && j.Name == name);
        }
        finally
        {
            await jobs.DeleteAsync(created.JobId);
        }
    }

    [Fact]
    public async Task Uc11_get_active_jobs_strong_includes_seeded_active_job()
    {
        using var scope = _fixture.Services.CreateScope();
        var jobs = scope.ServiceProvider.GetRequiredService<IJobDefinitionRepository>();

        var active = await jobs.GetActiveJobsAsync(ConsistencyLevel.Strong);
        Assert.Contains(active, j => j.JobId == _fixture.SeedJobId);
    }
}
