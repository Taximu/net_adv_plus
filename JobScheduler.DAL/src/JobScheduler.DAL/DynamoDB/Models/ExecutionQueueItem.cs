namespace JobScheduler.DAL.DynamoDB.Models;

/// <summary>
/// Execution queue row (UC 2.1). Keys: <see cref="QueueId"/> + <see cref="ScheduledFor"/> (ISO-8601 UTC string).
/// </summary>
public class ExecutionQueueItem
{
    public string QueueId { get; set; } = string.Empty;
    public string ScheduledFor { get; set; } = string.Empty;

    public string JobId { get; set; } = string.Empty;
    public string ScheduleId { get; set; } = string.Empty;
    public string QueueStatus { get; set; } = "pending";
    public int Priority { get; set; } = 5;
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; } = 3;

    public ExecutionContextSnapshot? ExecutionContext { get; set; }

    public string? AssignedWorkerId { get; set; }
    public string? AssignedAt { get; set; }
    public string? WorkerHeartbeat { get; set; }

    public string? StartedAt { get; set; }
    public string? CompletedAt { get; set; }
    public string? TimeoutAt { get; set; }

    public ExecutionResultSnapshot? ExecutionResult { get; set; }

    /// <summary>Unix epoch seconds for DynamoDB TTL.</summary>
    public long? Ttl { get; set; }
}

public class ExecutionContextSnapshot
{
    public string? Environment { get; set; }
    public string? TriggerSource { get; set; }
    public string? UserId { get; set; }
    public IReadOnlyDictionary<string, string>? Parameters { get; set; }
}

public class ExecutionResultSnapshot
{
    public string? Status { get; set; }
    public int? StatusCode { get; set; }
    public long? ResponseSizeBytes { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? StackTrace { get; set; }
}
