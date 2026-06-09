namespace JobScheduler.Messaging;

public interface IExecutionLifecycleEventSink
{
    Task PublishExecutionEnqueuedAsync(ExecutionEnqueuedEventPayload payload, CancellationToken cancellationToken = default);
}
