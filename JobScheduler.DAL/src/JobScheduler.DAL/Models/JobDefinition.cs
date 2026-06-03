namespace JobScheduler.DAL.Models;

public class JobDefinition
{
    public Guid JobId { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string JobType { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? ApiEndpoint { get; set; }
    public string? HttpMethod { get; set; }
    public string? RequestHeaders { get; set; }
    public string? RequestBodyTemplate { get; set; }
    public string? AuthType { get; set; }
    public string? AuthConfig { get; set; }
    public int TimeoutSeconds { get; set; } = 3600;
    public int MaxRetries { get; set; } = 3;
    public decimal RetryBackoffMultiplier { get; set; } = 1.5m;
    public int[]? RetryableStatusCodes { get; set; }
    public Guid? ParentJobId { get; set; }
    public string Status { get; set; } = "draft";
    public int Version { get; set; } = 1;
    public string[]? Tags { get; set; }
    public string? Metadata { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? UpdatedBy { get; set; }
}
