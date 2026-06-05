using Amazon.DynamoDBv2.Model;
using JobScheduler.DAL.DynamoDB;
using Microsoft.Extensions.DependencyInjection;

namespace JobScheduler.DAL.DynamoDb.Tests;

[Collection("DynamoDbDal")]
public class DynamoDbConnectivityTests
{
    private readonly DynamoDbDalFixture _fixture;

    public DynamoDbConnectivityTests(DynamoDbDalFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task DynamoDB_Local_responds_and_ExecutionQueue_table_exists()
    {
        using var test = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = test.Token;

        await _fixture.EnsureLocalRespondsAsync(ct);
        var factory = _fixture.Services.GetRequiredService<IDynamoDbContextFactory>();
        var client = factory.CreateClient();

        var tables = await client.ListTablesAsync(ct);
        Assert.Contains("ExecutionQueue", tables.TableNames);
        Assert.Contains("WorkerNodes", tables.TableNames);
    }

    [Fact]
    public async Task DescribeTable_includes_pending_and_worker_GSIs()
    {
        using var test = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = test.Token;

        await _fixture.EnsureLocalRespondsAsync(ct);
        var factory = _fixture.Services.GetRequiredService<IDynamoDbContextFactory>();
        var client = factory.CreateClient();

        var desc = await client.DescribeTableAsync(new DescribeTableRequest { TableName = "ExecutionQueue" }, ct);
        var names = desc.Table.GlobalSecondaryIndexes.Select(g => g.IndexName).ToHashSet();
        Assert.Contains("PendingExecutionsIndex", names);
        Assert.Contains("WorkerAssignmentsIndex", names);
    }
}
