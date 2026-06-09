namespace JobScheduler.Messaging;

public sealed record JobCatalogCreatedEventPayload(
    Guid JobId,
    Guid UserId,
    string Name,
    string JobType,
    string Status,
    string CreatedBy,
    DateTimeOffset OccurredAt);
