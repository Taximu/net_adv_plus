namespace JobScheduler.Messaging;

public sealed class NullJobCatalogEventSink : IJobCatalogEventSink
{
    public Task PublishJobDefinitionCreatedAsync(JobCatalogCreatedEventPayload payload, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
