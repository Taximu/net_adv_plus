using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using JobScheduler.DAL.Configuration;
using JobScheduler.DAL.Consistency;
using JobScheduler.DAL.DynamoDB.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobScheduler.DAL.DynamoDB.Repositories;

public sealed class WorkerNodeRepository : IWorkerNodeRepository
{
    private readonly IAmazonDynamoDB _db;
    private readonly string _table;
    private readonly ILogger<WorkerNodeRepository> _logger;

    public WorkerNodeRepository(
        IDynamoDbContextFactory factory,
        IOptions<DynamoDbOptions> options,
        ILogger<WorkerNodeRepository> logger)
    {
        _db = factory.CreateClient();
        _table = options.Value.WorkerNodesTableName;
        _logger = logger;
    }

    public async Task PutAsync(WorkerNode node, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("DynamoDB PutItem Table={Table} Operation=RegisterWorker WorkerId={WorkerId}", _table, node.WorkerId);
        await _db.PutItemAsync(new PutItemRequest
        {
            TableName = _table,
            Item = ToItem(node)
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<WorkerNode?> GetAsync(
        string workerId,
        string registeredAt,
        CancellationToken cancellationToken = default,
        ConsistencyLevel consistencyLevel = ConsistencyLevel.Strong)
    {
        var consistentRead = consistencyLevel is ConsistencyLevel.Strong;
        _logger.LogDebug(
            "DynamoDB GetItem Table={Table} Operation=ReadWorker WorkerId={WorkerId} ConsistencyLevel={ConsistencyLevel} ConsistentRead={ConsistentRead}",
            _table,
            workerId,
            consistencyLevel,
            consistentRead);

        var response = await _db.GetItemAsync(new GetItemRequest
        {
            TableName = _table,
            ConsistentRead = consistentRead,
            Key = Key(workerId, registeredAt)
        }, cancellationToken).ConfigureAwait(false);

        return response.Item == null || response.Item.Count == 0 ? null : FromItem(response.Item);
    }

    public async Task DeleteAsync(string workerId, string registeredAt, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("DynamoDB DeleteItem Table={Table} Operation=DeleteWorker WorkerId={WorkerId}", _table, workerId);
        await _db.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _table,
            Key = Key(workerId, registeredAt)
        }, cancellationToken).ConfigureAwait(false);
    }

    private static Dictionary<string, AttributeValue> Key(string workerId, string registeredAt) => new()
    {
        ["workerId"] = new AttributeValue { S = workerId },
        ["registeredAt"] = new AttributeValue { S = registeredAt }
    };

    private static Dictionary<string, AttributeValue> ToItem(WorkerNode node)
    {
        var attributes = new Dictionary<string, AttributeValue>
        {
            ["workerId"] = new AttributeValue { S = node.WorkerId },
            ["registeredAt"] = new AttributeValue { S = node.RegisteredAt }
        };

        if (!string.IsNullOrEmpty(node.WorkerType))
            attributes["workerType"] = new AttributeValue { S = node.WorkerType };
        if (!string.IsNullOrEmpty(node.InstanceType))
            attributes["instanceType"] = new AttributeValue { S = node.InstanceType };
        if (!string.IsNullOrEmpty(node.IpAddress))
            attributes["ipAddress"] = new AttributeValue { S = node.IpAddress };
        if (!string.IsNullOrEmpty(node.AvailabilityZone))
            attributes["availabilityZone"] = new AttributeValue { S = node.AvailabilityZone };

        attributes["maxConcurrentJobs"] = new AttributeValue { N = node.MaxConcurrentJobs.ToString() };
        attributes["currentJobCount"] = new AttributeValue { N = node.CurrentJobCount.ToString() };
        attributes["totalJobsProcessed"] = new AttributeValue { N = node.TotalJobsProcessed.ToString() };

        if (!string.IsNullOrEmpty(node.LastHeartbeat))
            attributes["lastHeartbeat"] = new AttributeValue { S = node.LastHeartbeat };
        if (!string.IsNullOrEmpty(node.Status))
            attributes["status"] = new AttributeValue { S = node.Status };
        if (node.CpuUtilization.HasValue)
            attributes["cpuUtilization"] = new AttributeValue { N = node.CpuUtilization.Value.ToString() };
        if (node.MemoryUtilization.HasValue)
            attributes["memoryUtilization"] = new AttributeValue { N = node.MemoryUtilization.Value.ToString() };

        if (node.SupportedJobTypes is { Count: > 0 })
            attributes["supportedJobTypes"] = new AttributeValue { SS = node.SupportedJobTypes.ToList() };

        if (node.Tags is { Count: > 0 })
        {
            var tagAttributes = new Dictionary<string, AttributeValue>();
            foreach (var kv in node.Tags)
                tagAttributes[kv.Key] = new AttributeValue { S = kv.Value };
            attributes["tags"] = new AttributeValue { M = tagAttributes };
        }

        if (!string.IsNullOrEmpty(node.LastUpdatedAt))
            attributes["lastUpdatedAt"] = new AttributeValue { S = node.LastUpdatedAt };

        return attributes;
    }

    private static WorkerNode FromItem(Dictionary<string, AttributeValue> a)
    {
        List<string>? ss = null;
        if (a.TryGetValue("supportedJobTypes", out var ssv) && ssv.SS is { Count: > 0 })
            ss = ssv.SS;

        IReadOnlyDictionary<string, string>? tags = null;
        if (a.TryGetValue("tags", out var tm) && tm.M is { Count: > 0 } mmap)
            tags = mmap.ToDictionary(kv => kv.Key, kv => kv.Value.S ?? "");

        return new WorkerNode
        {
            WorkerId = GetS(a, "workerId") ?? "",
            RegisteredAt = GetS(a, "registeredAt") ?? "",
            WorkerType = GetS(a, "workerType"),
            InstanceType = GetS(a, "instanceType"),
            IpAddress = GetS(a, "ipAddress"),
            AvailabilityZone = GetS(a, "availabilityZone"),
            MaxConcurrentJobs = GetNInt(a, "maxConcurrentJobs") ?? 0,
            CurrentJobCount = GetNInt(a, "currentJobCount") ?? 0,
            TotalJobsProcessed = GetNLong(a, "totalJobsProcessed") ?? 0,
            LastHeartbeat = GetS(a, "lastHeartbeat"),
            Status = GetS(a, "status"),
            CpuUtilization = GetNInt(a, "cpuUtilization"),
            MemoryUtilization = GetNInt(a, "memoryUtilization"),
            SupportedJobTypes = ss,
            Tags = tags,
            LastUpdatedAt = GetS(a, "lastUpdatedAt")
        };
    }

    private static string? GetS(Dictionary<string, AttributeValue> d, string k)
        => d.TryGetValue(k, out var v) ? v.S : null;

    private static int? GetNInt(Dictionary<string, AttributeValue> d, string k)
    {
        if (!d.TryGetValue(k, out var v) || v.N == null) return null;
        return int.TryParse(v.N, out var i) ? i : null;
    }

    private static long? GetNLong(Dictionary<string, AttributeValue> d, string k)
    {
        if (!d.TryGetValue(k, out var v) || v.N == null) return null;
        return long.TryParse(v.N, out var i) ? i : null;
    }
}
