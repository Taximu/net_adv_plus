using System.Text.Json.Serialization;
using JobScheduler.BL.Extensions;
using JobScheduler.BL.Services;
using JobScheduler.DAL.Extensions;
using JobScheduler.JobOrchestrator;
using JobScheduler.JobOrchestrator.Grpc;
using JobScheduler.Observability;
using JobScheduler.JobOrchestrator.Workers;
using JobScheduler.Messaging;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddJobSchedulerObservability("JobScheduler.JobOrchestrator");

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/json"]);
});

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddDataAccessLayer(builder.Configuration);
builder.Services.AddDynamoDbDataAccessLayer(builder.Configuration);
builder.Services.AddJobSchedulerMessagingPublishers(builder.Configuration);
builder.Services.AddJobSchedulerBusinessLogic();

builder.Services.Configure<KafkaConsumerOptions>(
    builder.Configuration.GetSection("Messaging:Consumers:Lifecycle"));
builder.Services.PostConfigure<KafkaConsumerOptions>(options =>
{
    options.Topic = MessagingTopics.ExecutionLifecycle;
    if (string.IsNullOrWhiteSpace(options.GroupId))
        options.GroupId = "job-orchestrator-lifecycle";
});

builder.Services.Configure<OrchestratorBatchOptions>(
    builder.Configuration.GetSection(OrchestratorBatchOptions.ConfigurationSectionPath));

builder.Services.AddHttpClient(
    "SchedulerApi",
    (sp, client) =>
    {
        var orchestratorOptions = sp.GetRequiredService<IOptions<OrchestratorBatchOptions>>().Value;
        client.BaseAddress = new Uri(orchestratorOptions.ApiBaseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(30);
    });

builder.Services.AddHostedService<ExecutionLifecycleConsumerWorker>();
builder.Services.AddHostedService<PendingExecutionBatchPeekWorker>();

builder.Services.AddGrpc(options =>
{
    options.MaxReceiveMessageSize = 1024 * 1024;
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

builder.WebHost.ConfigureKestrel(k =>
{
    // gRPC requires HTTP/2; keep HTTP/1.1 for JSON REST on the same port (e.g. Docker http://+:8080).
    k.ConfigureEndpointDefaults(lo => lo.Protocols = HttpProtocols.Http1AndHttp2);
});

var app = builder.Build();

app.UseResponseCompression();

app.MapGrpcService<JobExecutionHistoryGrpcService>();

app.MapGet(
    "/api/internal/jobs/{jobId:guid}/executions/history",
    async (
        Guid jobId,
        int? limit,
        string? cursor,
        bool full,
        IJobExecutionHistoryService history,
        CancellationToken ct) =>
    {
        try
        {
            var page = await history
                .GetExecutionHistoryPageAsync(jobId, limit ?? 50, cursor, full, ct)
                .ConfigureAwait(false);
            return Results.Json(page);
        }
        catch (ArgumentException)
        {
            return Results.BadRequest(new { error = "invalid_cursor" });
        }
    });

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapJobSchedulerMetrics();

await app.RunAsync().ConfigureAwait(false);
