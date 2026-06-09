namespace JobScheduler.Messaging;

public sealed record ExecutionEnqueuedEventPayload(
    string QueueId,
    string ScheduledFor,
    string JobId,
    string ScheduleId,
    string QueueStatus,
    int Priority,
    DateTimeOffset OccurredAt);
