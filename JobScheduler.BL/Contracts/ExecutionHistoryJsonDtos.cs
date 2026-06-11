using System.Text.Json.Serialization;

namespace JobScheduler.BL.Contracts;

/// <summary>
/// UC 2.3 compact JSON page (short property names, nulls omitted at serialization).
/// </summary>
public sealed class ExecutionHistoryPageJson
{
    [JsonPropertyName("j")]
    public string JobId { get; init; } = "";

    /// <summary>Opaque continuation token (DynamoDB <c>ExclusiveStartKey</c>).</summary>
    [JsonPropertyName("n")]
    public string? Next { get; init; }

    [JsonPropertyName("e")]
    public IReadOnlyList<ExecutionHistoryRowJson> Events { get; init; } = Array.Empty<ExecutionHistoryRowJson>();
}

/// <summary>Single execution row — baseline JSON with abbreviated keys for smaller payloads.</summary>
public sealed class ExecutionHistoryRowJson
{
    [JsonPropertyName("q")]
    public string QueueId { get; init; } = "";

    [JsonPropertyName("f")]
    public string ScheduledFor { get; init; } = "";

    [JsonPropertyName("z")]
    public string ScheduleId { get; init; } = "";

    [JsonPropertyName("s")]
    public string QueueStatus { get; init; } = "";

    [JsonPropertyName("p")]
    public int Priority { get; init; }

    [JsonPropertyName("rc")]
    public int RetryCount { get; init; }

    [JsonPropertyName("mr")]
    public int MaxRetries { get; init; }

    [JsonPropertyName("sa")]
    public string? StartedAt { get; init; }

    [JsonPropertyName("ca")]
    public string? CompletedAt { get; init; }

    [JsonPropertyName("ta")]
    public string? TimeoutAt { get; init; }

    [JsonPropertyName("aw")]
    public string? AssignedWorkerId { get; init; }

    /// <summary>Present when <c>full=true</c> or when a snapshot exists.</summary>
    [JsonPropertyName("x")]
    public ExecutionContextJson? Context { get; init; }

    [JsonPropertyName("r")]
    public ExecutionResultJson? Result { get; init; }
}

public sealed class ExecutionContextJson
{
    [JsonPropertyName("e")]
    public string? Environment { get; init; }

    [JsonPropertyName("t")]
    public string? TriggerSource { get; init; }

    [JsonPropertyName("u")]
    public string? UserId { get; init; }

    [JsonPropertyName("pm")]
    public IReadOnlyDictionary<string, string>? Parameters { get; init; }
}

public sealed class ExecutionResultJson
{
    [JsonPropertyName("st")]
    public string? Status { get; init; }

    [JsonPropertyName("sc")]
    public int? StatusCode { get; init; }

    [JsonPropertyName("sz")]
    public long? ResponseSizeBytes { get; init; }

    [JsonPropertyName("ec")]
    public string? ErrorCode { get; init; }

    /// <summary>Omitted in compact mode unless <c>full=true</c>.</summary>
    [JsonPropertyName("em")]
    public string? ErrorMessage { get; init; }

    /// <summary>Omitted in compact mode unless <c>full=true</c>.</summary>
    [JsonPropertyName("et")]
    public string? ErrorStack { get; init; }
}
