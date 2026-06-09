namespace JobScheduler.JobOrchestrator;

public sealed class OrchestratorBatchOptions
{
    public const string ConfigurationSectionPath = "Orchestrator:Batch";

    public string ApiBaseUrl { get; set; } = "http://localhost:5000";

    public int IntervalSeconds { get; set; } = 45;

    public int PendingPeekLimit { get; set; } = 25;
}
