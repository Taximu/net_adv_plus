namespace JobScheduler.DAL.Configuration;

/// <summary>
/// UC 2.1 — DynamoDB (ExecutionQueue, WorkerNodes). Bind from configuration section "DynamoDB".
/// </summary>
public class DynamoDbOptions
{
    /// <summary>AWS region when <see cref="ServiceUrl"/> is not set (e.g. us-east-1).</summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>Optional local endpoint (e.g. http://localhost:8000). When set, <see cref="AccessKeyId"/> / <see cref="SecretAccessKey"/> are used with basic credentials.</summary>
    public string? ServiceUrl { get; set; }

    /// <summary>Credentials for DynamoDB Local (letters/numbers per local rules). Ignored when <see cref="ServiceUrl"/> is empty.</summary>
    public string AccessKeyId { get; set; } = "local";

    public string SecretAccessKey { get; set; } = "local";

    public string ExecutionQueueTableName { get; set; } = "ExecutionQueue";

    public string WorkerNodesTableName { get; set; } = "WorkerNodes";

    /// <summary>When set, bounds HTTP timeout for each request (helps tests fail fast when Local is down).</summary>
    public int? ClientTimeoutSeconds { get; set; }
}
