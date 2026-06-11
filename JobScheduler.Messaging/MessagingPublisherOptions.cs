namespace JobScheduler.Messaging;

public sealed class MessagingPublisherOptions
{
    public const string ConfigurationSectionPath = "Messaging:Publishers";

    /// <summary>When false, null sinks are used and no Kafka connections are opened.</summary>
    public bool Enabled { get; set; }

    public string BootstrapServers { get; set; } = "localhost:19092";

    public string ClientId { get; set; } = "jobscheduler-api";
}
