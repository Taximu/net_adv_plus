namespace JobScheduler.Api.Contracts;

public sealed record CreateScheduleRequest(
    string ScheduleName,
    string ScheduleType = "cron",
    string? CronExpression = null,
    int? IntervalSeconds = null,
    string Timezone = "UTC",
    bool IsEnabled = true,
    int Priority = 1,
    string Status = "active");
