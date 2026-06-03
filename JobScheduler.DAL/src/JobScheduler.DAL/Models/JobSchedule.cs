namespace JobScheduler.DAL.Models;

public class JobSchedule
{
    public Guid ScheduleId { get; set; }
    public Guid JobId { get; set; }
    public string ScheduleName { get; set; } = string.Empty;
    public string ScheduleType { get; set; } = string.Empty;
    public string? CronExpression { get; set; }
    public int? IntervalSeconds { get; set; }
    public string Timezone { get; set; } = "UTC";
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool AllowOverlap { get; set; } = false;
    public int MaxConcurrentExecutions { get; set; } = 1;
    public int Priority { get; set; } = 5;
    public DateTime? NextExecutionAt { get; set; }
    public DateTime? LastExecutionAt { get; set; }
    public int ExecutionCount { get; set; } = 0;
    public string Status { get; set; } = "active";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
