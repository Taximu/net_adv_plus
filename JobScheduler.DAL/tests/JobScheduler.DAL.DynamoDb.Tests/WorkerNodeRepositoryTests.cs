using JobScheduler.DAL.DynamoDB.Models;
using JobScheduler.DAL.DynamoDB.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JobScheduler.DAL.DynamoDb.Tests;

[Collection("DynamoDbDal")]
public class WorkerNodeRepositoryTests
{
    private readonly DynamoDbDalFixture _fixture;

    public WorkerNodeRepositoryTests(DynamoDbDalFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Put_and_Get_roundtrip()
    {
        using var test = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = test.Token;

        await _fixture.EnsureLocalRespondsAsync(ct);
        using var scope = _fixture.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IWorkerNodeRepository>();

        var id = $"worker-dal-{Guid.NewGuid():N}";
        var registered = DateTime.UtcNow.ToString("O");
        var node = new WorkerNode
        {
            WorkerId = id,
            RegisteredAt = registered,
            WorkerType = "test",
            InstanceType = "local",
            AvailabilityZone = "local-1a",
            MaxConcurrentJobs = 5,
            CurrentJobCount = 0,
            TotalJobsProcessed = 0,
            LastHeartbeat = DateTime.UtcNow.ToString("O"),
            Status = "healthy",
            LastUpdatedAt = DateTime.UtcNow.ToString("O")
        };

        try
        {
            await repo.PutAsync(node, ct);
            var loaded = await repo.GetAsync(id, registered, ct);
            Assert.NotNull(loaded);
            Assert.Equal(id, loaded!.WorkerId);
            Assert.Equal("healthy", loaded.Status);
            Assert.Equal(5, loaded.MaxConcurrentJobs);
        }
        finally
        {
            await repo.DeleteAsync(id, registered, ct);
        }
    }
}
