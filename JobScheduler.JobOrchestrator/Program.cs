using JobScheduler.JobOrchestrator;
using JobScheduler.JobOrchestrator.Workers;
using JobScheduler.Messaging;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

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

var host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
