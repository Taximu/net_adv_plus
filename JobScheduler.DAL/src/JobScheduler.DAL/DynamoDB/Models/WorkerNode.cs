namespace JobScheduler.DAL.DynamoDB.Models;

/// <summary>
/// Worker registration (UC 2.1). Keys: <see cref="WorkerId"/> + <see cref="RegisteredAt"/> (ISO-8601 UTC string).
/// </summary>
public class WorkerNode
{
    public string WorkerId { get; set; } = string.Empty;
    public string RegisteredAt { get; set; } = string.Empty;

    public string? WorkerType { get; set; }
    public string? InstanceType { get; set; }
    public string? IpAddress { get; set; }
    public string? AvailabilityZone { get; set; }

    public int MaxConcurrentJobs { get; set; } = 10;
    public int CurrentJobCount { get; set; }
    public long TotalJobsProcessed { get; set; }

    public string? LastHeartbeat { get; set; }
    public string? Status { get; set; }
    public int? CpuUtilization { get; set; }
    public int? MemoryUtilization { get; set; }

    public IReadOnlyCollection<string>? SupportedJobTypes { get; set; }
    public IReadOnlyDictionary<string, string>? Tags { get; set; }

    public string? LastUpdatedAt { get; set; }
}
