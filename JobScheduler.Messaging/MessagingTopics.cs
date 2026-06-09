namespace JobScheduler.Messaging;

/// <summary>Kafka topic names aligned with <c>JobScheduler.DAL/docs/Batch and Stream Processing Strategy.md</c> and <c>messaging/docker-compose.yml</c> bootstrap.</summary>
public static class MessagingTopics
{
    public const string JobCatalogEvents = "job.catalog.events";
    public const string ExecutionLifecycle = "execution.lifecycle";
}
