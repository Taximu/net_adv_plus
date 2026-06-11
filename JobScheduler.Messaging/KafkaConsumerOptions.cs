namespace JobScheduler.Messaging;

public sealed class KafkaConsumerOptions
{
    public string BootstrapServers { get; set; } = "localhost:19092";

    public string GroupId { get; set; } = "jobscheduler-consumer";

    public string Topic { get; set; } = MessagingTopics.JobCatalogEvents;

    /// <summary>e.g. earliest, latest</summary>
    public string AutoOffsetReset { get; set; } = "earliest";
}
