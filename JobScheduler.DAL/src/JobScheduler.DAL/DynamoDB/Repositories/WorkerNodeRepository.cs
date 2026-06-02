using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using JobScheduler.DAL.Configuration;
using JobScheduler.DAL.DynamoDB.Models;
using Microsoft.Extensions.Options;

namespace JobScheduler.DAL.DynamoDB.Repositories;

public sealed class WorkerNodeRepository : IWorkerNodeRepository
{
    private readonly IAmazonDynamoDB _db;
    private readonly string _table;

    public WorkerNodeRepository(IDynamoDbContextFactory factory, IOptions<DynamoDbOptions> options)
    {
        _db = factory.CreateClient();
        _table = options.Value.WorkerNodesTableName;
    }

    public async Task PutAsync(WorkerNode node, CancellationToken cancellationToken = default)
    {
        await _db.PutItemAsync(new PutItemRequest
        {
            TableName = _table,
            Item = ToItem(node)
        }, cancellationToken);
    }

    public async Task<WorkerNode?> GetAsync(string workerId, string registeredAt, CancellationToken cancellationToken = default)
    {
        var response = await _db.GetItemAsync(new GetItemRequest
        {
            TableName = _table,
            ConsistentRead = true,
            Key = Key(workerId, registeredAt)
        }, cancellationToken);

        return response.Item == null || response.Item.Count == 0 ? null : FromItem(response.Item);
    }

    public async Task DeleteAsync(string workerId, string registeredAt, CancellationToken cancellationToken = default)
    {
        await _db.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _table,
            Key = Key(workerId, registeredAt)
        }, cancellationToken);
    }

    private static Dictionary<string, AttributeValue> Key(string workerId, string registeredAt) => new()
    {
        ["workerId"] = new AttributeValue { S = workerId },
        ["registeredAt"] = new AttributeValue { S = registeredAt }
    };

    private static Dictionary<string, AttributeValue> ToItem(WorkerNode n)
    {
        var m = new Dictionary<string, AttributeValue>
        {
            ["workerId"] = new AttributeValue { S = n.WorkerId },
            ["registeredAt"] = new AttributeValue { S = n.RegisteredAt }
        };

        if (!string.IsNullOrEmpty(n.WorkerType))
            m["workerType"] = new AttributeValue { S = n.WorkerType };
        if (!string.IsNullOrEmpty(n.InstanceType))
            m["instanceType"] = new AttributeValue { S = n.InstanceType };
        if (!string.IsNullOrEmpty(n.IpAddress))
            m["ipAddress"] = new AttributeValue { S = n.IpAddress };
        if (!string.IsNullOrEmpty(n.AvailabilityZone))
            m["availabilityZone"] = new AttributeValue { S = n.AvailabilityZone };

        m["maxConcurrentJobs"] = new AttributeValue { N = n.MaxConcurrentJobs.ToString() };
        m["currentJobCount"] = new AttributeValue { N = n.CurrentJobCount.ToString() };
        m["totalJobsProcessed"] = new AttributeValue { N = n.TotalJobsProcessed.ToString() };

        if (!string.IsNullOrEmpty(n.LastHeartbeat))
            m["lastHeartbeat"] = new AttributeValue { S = n.LastHeartbeat };
        if (!string.IsNullOrEmpty(n.Status))
            m["status"] = new AttributeValue { S = n.Status };
        if (n.CpuUtilization.HasValue)
            m["cpuUtilization"] = new AttributeValue { N = n.CpuUtilization.Value.ToString() };
        if (n.MemoryUtilization.HasValue)
            m["memoryUtilization"] = new AttributeValue { N = n.MemoryUtilization.Value.ToString() };

        if (n.SupportedJobTypes is { Count: > 0 })
            m["supportedJobTypes"] = new AttributeValue { SS = n.SupportedJobTypes.ToList() };

        if (n.Tags is { Count: > 0 })
        {
            var tm = new Dictionary<string, AttributeValue>();
            foreach (var kv in n.Tags)
                tm[kv.Key] = new AttributeValue { S = kv.Value };
            m["tags"] = new AttributeValue { M = tm };
        }

        if (!string.IsNullOrEmpty(n.LastUpdatedAt))
            m["lastUpdatedAt"] = new AttributeValue { S = n.LastUpdatedAt };

        return m;
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
