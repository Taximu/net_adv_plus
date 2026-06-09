using JobScheduler.JobManager.Workers;
using JobScheduler.Messaging;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<KafkaConsumerOptions>(
    builder.Configuration.GetSection("Messaging:Consumers:Catalog"));
builder.Services.PostConfigure<KafkaConsumerOptions>(options =>
{
    options.Topic = MessagingTopics.JobCatalogEvents;
    if (string.IsNullOrWhiteSpace(options.GroupId))
        options.GroupId = "job-manager-catalog";
});

builder.Services.AddHostedService<CatalogEventConsumerWorker>();

var host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
