using JobScheduler.Api.Contracts;
using JobScheduler.BL.Extensions;
using JobScheduler.BL.Services;
using JobScheduler.DAL.Extensions;
using JobScheduler.DAL.Models;
using JobScheduler.DAL.DynamoDB.Models;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDataAccessLayer(builder.Configuration);
builder.Services.AddDynamoDbDataAccessLayer(builder.Configuration);
builder.Services.AddJobSchedulerBusinessLogic();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Job Scheduler API",
        Version = "v1",
        Description =
            "UC 1.1 — PostgreSQL job catalog. UC 2.1 — DynamoDB execution queue. "
            + "By coursework design, read/write semantics (replica vs primary, eventual vs strong DynamoDB reads) are fixed per use case inside the business layer — "
            + "this API does not accept consistency parameters and clients should not need to know them."
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Job Scheduler API v1");
    options.DocumentTitle = "Job Scheduler API";
});

app.MapGet("/api/users/{userId:guid}/jobs", async (Guid userId, IUserJobsService jobs, CancellationToken ct) =>
    Results.Ok(await jobs.ListMyJobsAsync(userId, ct).ConfigureAwait(false)));

app.MapGet("/api/users/{userId:guid}/jobs/{jobId:guid}", async (Guid userId, Guid jobId, IUserJobsService jobs, CancellationToken ct) =>
{
    var job = await jobs.GetJobAsync(userId, jobId, ct).ConfigureAwait(false);
    return job is null ? Results.NotFound() : Results.Ok(job);
});

app.MapPost("/api/users/{userId:guid}/jobs", async (Guid userId, CreateJobRequest body, IUserJobsService jobs, CancellationToken ct) =>
{
    try
    {
        var created = await jobs.CreateDraftJobAsync(
                userId,
                body.Name,
                body.JobType ?? "api_call",
                body.CreatedBy ?? "api",
                ct)
            .ConfigureAwait(false);
        return Results.Created($"/api/users/{userId}/jobs/{created.JobId}", created);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { message = ex.Message });
    }
});

app.MapGet("/api/users/{userId:guid}/jobs/{jobId:guid}/schedules", async (Guid userId, Guid jobId, IUserSchedulesService schedules, CancellationToken ct) =>
    Results.Ok(await schedules.ListSchedulesForJobAsync(userId, jobId, ct).ConfigureAwait(false)));

app.MapPost("/api/users/{userId:guid}/jobs/{jobId:guid}/schedules", async (Guid userId, Guid jobId, CreateScheduleRequest body, IUserSchedulesService schedules, CancellationToken ct) =>
{
    var isInterval = string.Equals(body.ScheduleType, "interval", StringComparison.OrdinalIgnoreCase);
    var schedule = new JobSchedule
    {
        JobId = jobId,
        ScheduleName = body.ScheduleName,
        ScheduleType = body.ScheduleType,
        CronExpression = isInterval ? null : (body.CronExpression ?? "0 0 * * *"),
        IntervalSeconds = isInterval ? body.IntervalSeconds ?? 3600 : null,
        Timezone = body.Timezone,
        StartDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
        IsEnabled = body.IsEnabled,
        Priority = body.Priority,
        Status = body.Status
    };

    try
    {
        var created = await schedules.CreateScheduleAsync(userId, schedule, ct).ConfigureAwait(false);
        return Results.Created($"/api/users/{userId}/jobs/{jobId}/schedules/{created.ScheduleId}", created);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

app.MapGet("/api/internal/scheduler/jobs/active", async (ISchedulerCatalogService catalog, CancellationToken ct) =>
    Results.Ok(await catalog.GetActiveJobsForWorkerAsync(ct).ConfigureAwait(false)));

app.MapGet("/api/internal/execution/queue/pending", async (int? limit, IExecutionOrchestrationService execution, CancellationToken ct) =>
    Results.Ok(await execution.PeekPendingExecutionsAsync(limit, ct).ConfigureAwait(false)));

app.MapGet("/api/internal/execution/queue/item", async (string queueId, string scheduledFor, IExecutionOrchestrationService execution, CancellationToken ct) =>
{
    var item = await execution.GetExecutionAsync(queueId, scheduledFor, ct).ConfigureAwait(false);
    return item is null ? Results.NotFound() : Results.Ok(item);
});

app.MapPost("/api/internal/execution/queue/items", async (ExecutionQueueItem item, IExecutionOrchestrationService execution, CancellationToken ct) =>
{
    await execution.EnqueueExecutionAsync(item, ct).ConfigureAwait(false);
    var location = $"/api/internal/execution/queue/item?queueId={Uri.EscapeDataString(item.QueueId)}&scheduledFor={Uri.EscapeDataString(item.ScheduledFor)}";
    return Results.Created(location, item);
});

app.Run();
