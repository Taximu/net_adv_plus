namespace JobScheduler.Messaging;

public interface IJobCatalogEventSink
{
    Task PublishJobDefinitionCreatedAsync(JobCatalogCreatedEventPayload payload, CancellationToken cancellationToken = default);
}
