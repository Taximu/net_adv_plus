using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using JobScheduler.DAL.Configuration;
using JobScheduler.DAL.Consistency;
using JobScheduler.DAL.DynamoDB.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobScheduler.DAL.DynamoDB.Repositories;

/// <summary>
/// DynamoDB execution queue (UC 2.1). Debug logs use prefix <c>UC2.1</c> and structured fields
/// (<see cref="ConsistencyLevel"/>, <c>ConsistentRead</c>, <c>ScheduledFor</c>, <c>QueryCompleted</c> / <c>TryClaimCompleted</c>)
/// so operators can correlate API calls with read semantics — see <c>JobScheduler.DAL/docs/consistency-demo-logs.md</c>.
/// </summary>
public sealed class ExecutionQueueRepository : IExecutionQueueRepository
{
    private readonly IAmazonDynamoDB _db;
    private readonly string _table;
    private readonly ILogger<ExecutionQueueRepository> _logger;
    private const string PendingIndex = "PendingExecutionsIndex";
    private const string WorkerAssignmentsIndex = "WorkerAssignmentsIndex";

    public ExecutionQueueRepository(
        IDynamoDbContextFactory factory,
        IOptions<DynamoDbOptions> options,
        ILogger<ExecutionQueueRepository> logger)
    {
        _db = factory.CreateClient();
        _table = options.Value.ExecutionQueueTableName;
        _logger = logger;
    }

    public async Task PutAsync(ExecutionQueueItem item, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "UC2.1 DynamoDB PutItem Table={Table} Operation=Enqueue QueueId={QueueId} ScheduledFor={ScheduledFor} QueueStatus={QueueStatus} (write path; no ConsistentRead on PutItem)",
            _table,
            item.QueueId,
            item.ScheduledFor,
            item.QueueStatus);
        await _db.PutItemAsync(new PutItemRequest
        {
            TableName = _table,
            Item = ToItem(item)
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ExecutionQueueItem?> GetAsync(
        string queueId,
        string scheduledFor,
        CancellationToken cancellationToken = default,
        ConsistencyLevel consistencyLevel = ConsistencyLevel.Strong)
    {
        var consistentRead = MapConsistentRead(consistencyLevel);
        _logger.LogDebug(
            "UC2.1 DynamoDB GetItem Table={Table} Operation=Read QueueId={QueueId} ScheduledFor={ScheduledFor} ConsistencyLevel={ConsistencyLevel} ConsistentRead={ConsistentRead}",
            _table,
            queueId,
            scheduledFor,
            consistencyLevel,
            consistentRead);

        var response = await _db.GetItemAsync(new GetItemRequest
        {
            TableName = _table,
            ConsistentRead = consistentRead,
            Key = Key(queueId, scheduledFor)
        }, cancellationToken).ConfigureAwait(false);

        var found = response.Item != null && response.Item.Count > 0;
        _logger.LogDebug(
            "UC2.1 DynamoDB GetItemCompleted Table={Table} QueueId={QueueId} ScheduledFor={ScheduledFor} ConsistencyLevel={ConsistencyLevel} ConsistentRead={ConsistentRead} ItemFound={ItemFound}",
            _table,
            queueId,
            scheduledFor,
            consistencyLevel,
            consistentRead,
            found);

        return found ? FromItem(response.Item!) : null;
    }

    public async Task<IReadOnlyList<ExecutionQueueItem>> QueryByQueueStatusAsync(
        string queueStatus,
        int? limit,
        ConsistencyLevel consistencyLevel = ConsistencyLevel.Eventual,
        CancellationToken cancellationToken = default)
    {
        LogGsiQueryConsistencyDebug(PendingIndex, "PollPending", consistencyLevel);

        var max = limit ?? 100;
        var list = new List<ExecutionQueueItem>();
        Dictionary<string, AttributeValue>? startKey = null;

        do
        {
            var take = Math.Min(Math.Max(max - list.Count, 1), 100);
            var req = new QueryRequest
            {
                TableName = _table,
                IndexName = PendingIndex,
                KeyConditionExpression = "queueStatus = :qs",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":qs"] = new AttributeValue { S = queueStatus } },
                ScanIndexForward = true,
                Limit = take,
                ExclusiveStartKey = startKey
            };

            var page = await _db.QueryAsync(req, cancellationToken).ConfigureAwait(false);
            foreach (var item in page.Items)
            {
                list.Add(FromItem(item));
                if (list.Count >= max)
                    break;
            }

            if (list.Count >= max)
                break;

            startKey = page.LastEvaluatedKey is { Count: > 0 } ? page.LastEvaluatedKey : null;
        } while (startKey != null);

        _logger.LogDebug(
            "UC2.1 DynamoDB QueryCompleted Table={Table} Index={Index} Operation=PollPending ConsistencyLevel={ConsistencyLevel} QueueStatusFilter={QueueStatus} ReturnedCount={ReturnedCount} ConsistentRead=False",
            _table,
            PendingIndex,
            consistencyLevel,
            queueStatus,
            list.Count);

        return list;
    }

    public async Task<IReadOnlyList<ExecutionQueueItem>> QueryByAssignedWorkerAsync(
        string assignedWorkerId,
        int? limit,
        ConsistencyLevel consistencyLevel = ConsistencyLevel.Eventual,
        CancellationToken cancellationToken = default)
    {
        LogGsiQueryConsistencyDebug(WorkerAssignmentsIndex, "ListByWorker", consistencyLevel);

        var max = limit ?? 100;
        var list = new List<ExecutionQueueItem>();
        Dictionary<string, AttributeValue>? startKey = null;

        do
        {
            var take = Math.Min(Math.Max(max - list.Count, 1), 100);
            var req = new QueryRequest
            {
                TableName = _table,
                IndexName = WorkerAssignmentsIndex,
                KeyConditionExpression = "assignedWorkerId = :w",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":w"] = new AttributeValue { S = assignedWorkerId } },
                ScanIndexForward = false,
                Limit = take,
                ExclusiveStartKey = startKey
            };

            var page = await _db.QueryAsync(req, cancellationToken).ConfigureAwait(false);
            foreach (var item in page.Items)
            {
                list.Add(FromItem(item));
                if (list.Count >= max)
                    break;
            }

            if (list.Count >= max)
                break;

            startKey = page.LastEvaluatedKey is { Count: > 0 } ? page.LastEvaluatedKey : null;
        } while (startKey != null);

        _logger.LogDebug(
            "UC2.1 DynamoDB QueryCompleted Table={Table} Index={Index} Operation=ListByWorker ConsistencyLevel={ConsistencyLevel} AssignedWorkerId={WorkerId} ReturnedCount={ReturnedCount} ConsistentRead=False",
            _table,
            WorkerAssignmentsIndex,
            consistencyLevel,
            assignedWorkerId,
            list.Count);

        return list;
    }

    public async Task<bool> TryClaimAsync(string queueId, string scheduledFor, string workerId, string assignedAtIso, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "UC2.1 DynamoDB UpdateItem Table={Table} Operation=TryClaim QueueId={QueueId} ScheduledFor={ScheduledFor} WorkerId={WorkerId} (conditional: pending to assigned; write path)",
            _table,
            queueId,
            scheduledFor,
            workerId);
        try
        {
            await _db.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = _table,
                Key = Key(queueId, scheduledFor),
                UpdateExpression = "SET queueStatus = :assigned, assignedWorkerId = :wid, assignedAt = :ats",
                ConditionExpression = "queueStatus = :pend",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":assigned"] = new AttributeValue { S = "assigned" },
                    [":wid"] = new AttributeValue { S = workerId },
                    [":ats"] = new AttributeValue { S = assignedAtIso },
                    [":pend"] = new AttributeValue { S = "pending" }
                }
            }, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug(
                "UC2.1 DynamoDB TryClaimCompleted Table={Table} QueueId={QueueId} ScheduledFor={ScheduledFor} WorkerId={WorkerId} Success={Success}",
                _table,
                queueId,
                scheduledFor,
                workerId,
                true);
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            _logger.LogDebug(
                "UC2.1 DynamoDB TryClaimCompleted Table={Table} QueueId={QueueId} ScheduledFor={ScheduledFor} WorkerId={WorkerId} Success={Success}",
                _table,
                queueId,
                scheduledFor,
                workerId,
                false);
            return false;
        }
    }

    public async Task DeleteAsync(string queueId, string scheduledFor, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "UC2.1 DynamoDB DeleteItem Table={Table} Operation=Delete QueueId={QueueId} ScheduledFor={ScheduledFor} (write path)",
            _table,
            queueId,
            scheduledFor);
        await _db.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _table,
            Key = Key(queueId, scheduledFor)
        }, cancellationToken).ConfigureAwait(false);
    }

    private void LogGsiQueryConsistencyDebug(string indexName, string operation, ConsistencyLevel consistencyLevel)
    {
        if (consistencyLevel is ConsistencyLevel.Strong)
        {
            _logger.LogDebug(
                "UC2.1 DynamoDB Query Table={Table} Index={Index} Operation={Operation} ConsistencyLevel={ConsistencyLevel} ConsistentRead=False (GSI has no strong read; query remains eventually consistent)",
                _table,
                indexName,
                operation,
                consistencyLevel);
        }
        else
        {
            _logger.LogDebug(
                "UC2.1 DynamoDB Query Table={Table} Index={Index} Operation={Operation} ConsistencyLevel={ConsistencyLevel} ConsistentRead=False",
                _table,
                indexName,
                operation,
                consistencyLevel);
        }
    }

    private static bool MapConsistentRead(ConsistencyLevel level) =>
        level is ConsistencyLevel.Strong;

    private static Dictionary<string, AttributeValue> Key(string queueId, string scheduledFor) => new()
    {
        ["queueId"] = new AttributeValue { S = queueId },
        ["scheduledFor"] = new AttributeValue { S = scheduledFor }
    };

    private static Dictionary<string, AttributeValue> ToItem(ExecutionQueueItem i)
    {
        var m = new Dictionary<string, AttributeValue>
        {
            ["queueId"] = new AttributeValue { S = i.QueueId },
            ["scheduledFor"] = new AttributeValue { S = i.ScheduledFor },
            ["jobId"] = new AttributeValue { S = i.JobId },
            ["scheduleId"] = new AttributeValue { S = i.ScheduleId },
            ["queueStatus"] = new AttributeValue { S = i.QueueStatus },
            ["priority"] = new AttributeValue { N = i.Priority.ToString() },
            ["retryCount"] = new AttributeValue { N = i.RetryCount.ToString() },
            ["maxRetries"] = new AttributeValue { N = i.MaxRetries.ToString() }
        };

        if (i.ExecutionContext != null)
            m["executionContext"] = new AttributeValue { M = ToStringMap(i.ExecutionContext) };

        if (!string.IsNullOrEmpty(i.AssignedWorkerId))
            m["assignedWorkerId"] = new AttributeValue { S = i.AssignedWorkerId };
        if (!string.IsNullOrEmpty(i.AssignedAt))
            m["assignedAt"] = new AttributeValue { S = i.AssignedAt };
        if (!string.IsNullOrEmpty(i.WorkerHeartbeat))
            m["workerHeartbeat"] = new AttributeValue { S = i.WorkerHeartbeat };

        if (!string.IsNullOrEmpty(i.StartedAt))
            m["startedAt"] = new AttributeValue { S = i.StartedAt };
        if (!string.IsNullOrEmpty(i.CompletedAt))
            m["completedAt"] = new AttributeValue { S = i.CompletedAt };
        if (!string.IsNullOrEmpty(i.TimeoutAt))
            m["timeoutAt"] = new AttributeValue { S = i.TimeoutAt };

        if (i.ExecutionResult != null)
            m["executionResult"] = new AttributeValue { M = ToResultMap(i.ExecutionResult) };

        if (i.Ttl.HasValue)
            m["ttl"] = new AttributeValue { N = i.Ttl.Value.ToString() };

        return m;
    }

    private static Dictionary<string, AttributeValue> ToStringMap(ExecutionContextSnapshot ctx)
    {
        var d = new Dictionary<string, AttributeValue>();
        if (!string.IsNullOrEmpty(ctx.Environment))
            d["environment"] = new AttributeValue { S = ctx.Environment };
        if (!string.IsNullOrEmpty(ctx.TriggerSource))
            d["triggerSource"] = new AttributeValue { S = ctx.TriggerSource };
        if (!string.IsNullOrEmpty(ctx.UserId))
            d["userId"] = new AttributeValue { S = ctx.UserId };
        if (ctx.Parameters is { Count: > 0 })
        {
            var pm = new Dictionary<string, AttributeValue>();
            foreach (var kv in ctx.Parameters)
                pm[kv.Key] = new AttributeValue { S = kv.Value };
            d["parameters"] = new AttributeValue { M = pm };
        }
        return d;
    }

    private static Dictionary<string, AttributeValue> ToResultMap(ExecutionResultSnapshot r)
    {
        var d = new Dictionary<string, AttributeValue>();
        if (!string.IsNullOrEmpty(r.Status))
            d["status"] = new AttributeValue { S = r.Status };
        if (r.StatusCode.HasValue)
            d["statusCode"] = new AttributeValue { N = r.StatusCode.Value.ToString() };
        if (r.ResponseSizeBytes.HasValue)
            d["responseSizeBytes"] = new AttributeValue { N = r.ResponseSizeBytes.Value.ToString() };
        if (!string.IsNullOrEmpty(r.ErrorCode) || !string.IsNullOrEmpty(r.ErrorMessage) || !string.IsNullOrEmpty(r.StackTrace))
        {
            var ed = new Dictionary<string, AttributeValue>();
            if (!string.IsNullOrEmpty(r.ErrorCode))
                ed["code"] = new AttributeValue { S = r.ErrorCode };
            if (!string.IsNullOrEmpty(r.ErrorMessage))
                ed["message"] = new AttributeValue { S = r.ErrorMessage };
            if (!string.IsNullOrEmpty(r.StackTrace))
                ed["stackTrace"] = new AttributeValue { S = r.StackTrace };
            d["errorDetails"] = new AttributeValue { M = ed };
        }
        return d;
    }

    private static ExecutionQueueItem FromItem(Dictionary<string, AttributeValue> a)
    {
        var ctx = ParseContext(GetMap(a, "executionContext"));
        var res = ParseResult(GetMap(a, "executionResult"));

        return new ExecutionQueueItem
        {
            QueueId = GetS(a, "queueId") ?? "",
            ScheduledFor = GetS(a, "scheduledFor") ?? "",
            JobId = GetS(a, "jobId") ?? "",
            ScheduleId = GetS(a, "scheduleId") ?? "",
            QueueStatus = GetS(a, "queueStatus") ?? "",
            Priority = GetNInt(a, "priority") ?? 0,
            RetryCount = GetNInt(a, "retryCount") ?? 0,
            MaxRetries = GetNInt(a, "maxRetries") ?? 0,
            ExecutionContext = ctx,
            AssignedWorkerId = GetS(a, "assignedWorkerId"),
            AssignedAt = GetS(a, "assignedAt"),
            WorkerHeartbeat = GetS(a, "workerHeartbeat"),
            StartedAt = GetS(a, "startedAt"),
            CompletedAt = GetS(a, "completedAt"),
            TimeoutAt = GetS(a, "timeoutAt"),
            ExecutionResult = res,
            Ttl = GetNLong(a, "ttl")
        };
    }

    private static Dictionary<string, AttributeValue>? GetMap(Dictionary<string, AttributeValue> a, string key)
        => a.TryGetValue(key, out var v) && v.M != null ? v.M : null;

    private static ExecutionContextSnapshot? ParseContext(Dictionary<string, AttributeValue>? m)
    {
        if (m == null || m.Count == 0) return null;
        IReadOnlyDictionary<string, string>? parameters = null;
        if (m.TryGetValue("parameters", out var pm) && pm.M is { Count: > 0 } pmap)
            parameters = pmap.ToDictionary(kv => kv.Key, kv => kv.Value.S ?? "");

        return new ExecutionContextSnapshot
        {
            Environment = AttrGetS(m, "environment"),
            TriggerSource = AttrGetS(m, "triggerSource"),
            UserId = AttrGetS(m, "userId"),
            Parameters = parameters
        };
    }

    private static ExecutionResultSnapshot? ParseResult(Dictionary<string, AttributeValue>? m)
    {
        if (m == null || m.Count == 0) return null;
        var r = new ExecutionResultSnapshot
        {
            Status = AttrGetS(m, "status"),
            StatusCode = GetNInt(m, "statusCode"),
            ResponseSizeBytes = GetNLong(m, "responseSizeBytes")
        };
        var ed = GetMap(m, "errorDetails");
        if (ed != null)
        {
            r.ErrorCode = AttrGetS(ed, "code");
            r.ErrorMessage = AttrGetS(ed, "message");
            r.StackTrace = AttrGetS(ed, "stackTrace");
        }
        return r;
    }

    private static string? AttrGetS(Dictionary<string, AttributeValue> d, string k)
        => d.TryGetValue(k, out var v) ? v.S : null;

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
