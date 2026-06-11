namespace JobScheduler.Messaging;

public sealed class NullExecutionLifecycleEventSink : IExecutionLifecycleEventSink
{
    public Task PublishExecutionEnqueuedAsync(ExecutionEnqueuedEventPayload payload, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
