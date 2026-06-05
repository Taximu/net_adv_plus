namespace JobScheduler.Api.Contracts;

public sealed record CreateJobRequest(string Name, string? JobType = null, string? CreatedBy = null);
