using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace JobScheduler.DAL.DynamoDb.Tests;

/// <summary>
/// Creates ExecutionQueue / WorkerNodes on a fresh DynamoDB Local instance (same shape as setup-dynamodb-local.ps1).
/// </summary>
internal static class DynamoDbLocalSchema
{
    public static async Task EnsureTablesAsync(IAmazonDynamoDB client, CancellationToken cancellationToken = default)
    {
        var names = (await client.ListTablesAsync(cancellationToken)).TableNames;
        if (!names.Contains("ExecutionQueue"))
            await CreateExecutionQueueAsync(client, cancellationToken);
        if (!names.Contains("WorkerNodes"))
            await CreateWorkerNodesAsync(client, cancellationToken);

        try
        {
            await client.UpdateTimeToLiveAsync(
                new UpdateTimeToLiveRequest
                {
                    TableName = "ExecutionQueue",
                    TimeToLiveSpecification = new TimeToLiveSpecification { Enabled = true, AttributeName = "ttl" }
                },
                cancellationToken);
        }
        catch
        {
            // Local may already have TTL enabled
        }
    }

    private static async Task CreateExecutionQueueAsync(IAmazonDynamoDB client, CancellationToken cancellationToken)
    {
        var req = new CreateTableRequest
        {
            TableName = "ExecutionQueue",
            BillingMode = BillingMode.PAY_PER_REQUEST,
            AttributeDefinitions =
            [
                new AttributeDefinition("queueId", ScalarAttributeType.S),
                new AttributeDefinition("scheduledFor", ScalarAttributeType.S),
                new AttributeDefinition("queueStatus", ScalarAttributeType.S),
                new AttributeDefinition("priority", ScalarAttributeType.N),
                new AttributeDefinition("assignedWorkerId", ScalarAttributeType.S),
                new AttributeDefinition("assignedAt", ScalarAttributeType.S)
            ],
            KeySchema =
            [
                new KeySchemaElement("queueId", KeyType.HASH),
                new KeySchemaElement("scheduledFor", KeyType.RANGE)
            ],
            GlobalSecondaryIndexes =
            [
                new GlobalSecondaryIndex
                {
                    IndexName = "PendingExecutionsIndex",
                    KeySchema =
                    [
                        new KeySchemaElement("queueStatus", KeyType.HASH),
                        new KeySchemaElement("priority", KeyType.RANGE)
                    ],
                    Projection = new Projection
                    {
                        ProjectionType = ProjectionType.INCLUDE,
                        NonKeyAttributes = ["jobId", "scheduleId", "scheduledFor", "executionContext"]
                    }
                },
                new GlobalSecondaryIndex
                {
                    IndexName = "WorkerAssignmentsIndex",
                    KeySchema =
                    [
                        new KeySchemaElement("assignedWorkerId", KeyType.HASH),
                        new KeySchemaElement("assignedAt", KeyType.RANGE)
                    ],
                    Projection = new Projection { ProjectionType = ProjectionType.ALL }
                }
            ]
        };

        await client.CreateTableAsync(req, cancellationToken);
        await WaitTableActiveAsync(client, "ExecutionQueue", cancellationToken);
    }

    private static async Task CreateWorkerNodesAsync(IAmazonDynamoDB client, CancellationToken cancellationToken)
    {
        var req = new CreateTableRequest
        {
            TableName = "WorkerNodes",
            BillingMode = BillingMode.PAY_PER_REQUEST,
            AttributeDefinitions =
            [
                new AttributeDefinition("workerId", ScalarAttributeType.S),
                new AttributeDefinition("registeredAt", ScalarAttributeType.S)
            ],
            KeySchema =
            [
                new KeySchemaElement("workerId", KeyType.HASH),
                new KeySchemaElement("registeredAt", KeyType.RANGE)
            ]
        };

        await client.CreateTableAsync(req, cancellationToken);
        await WaitTableActiveAsync(client, "WorkerNodes", cancellationToken);
    }

    private static async Task WaitTableActiveAsync(IAmazonDynamoDB client, string tableName, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddMinutes(2);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var d = await client.DescribeTableAsync(new DescribeTableRequest { TableName = tableName }, cancellationToken);
            if (string.Equals(d.Table.TableStatus, TableStatus.ACTIVE, StringComparison.OrdinalIgnoreCase))
                return;
            await Task.Delay(200, cancellationToken);
        }

        throw new TimeoutException($"Table {tableName} did not become ACTIVE.");
    }
}
