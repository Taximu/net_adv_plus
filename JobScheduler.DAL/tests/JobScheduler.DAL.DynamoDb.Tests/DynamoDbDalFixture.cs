using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using JobScheduler.DAL.Configuration;
using JobScheduler.DAL.DynamoDB;
using JobScheduler.DAL.DynamoDB.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JobScheduler.DAL.DynamoDb.Tests;

/// <summary>
/// Shared DynamoDB Local: starts a Testcontainers <c>amazon/dynamodb-local</c> instance unless
/// <c>DYNAMODB_LOCAL_SERVICE_URL</c> is set (then uses that endpoint and expects tables to exist).
/// </summary>
public sealed class DynamoDbDalFixture : IAsyncLifetime
{
    private IContainer? _container;
    public ServiceProvider Services { get; private set; } = null!;

    /// <summary>Captured <see cref="ExecutionQueueRepository"/> / <see cref="WorkerNodeRepository"/> debug lines (routing / ConsistentRead).</summary>
    public MultiCategoryLogCaptureProvider RoutingLogCapture { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var serviceUrl = Environment.GetEnvironmentVariable("DYNAMODB_LOCAL_SERVICE_URL");
        if (string.IsNullOrWhiteSpace(serviceUrl))
        {
            const string dynamoImage = "amazon/dynamodb-local:latest";
            using (var pullCts = new CancellationTokenSource(TimeSpan.FromMinutes(5)))
                await DockerImagePullHelper.PullIfNeededAsync(dynamoImage, pullCts.Token).ConfigureAwait(false);

            _container = new ContainerBuilder()
                .WithImage(dynamoImage)
                .WithPortBinding(8000, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(8000))
                .Build();

            await _container.StartAsync();
            serviceUrl = $"http://{_container.Hostname}:{_container.GetMappedPublicPort(8000)}";
        }

        var services = new ServiceCollection();
        var routingCategories = new[]
        {
            typeof(ExecutionQueueRepository).FullName!,
            typeof(WorkerNodeRepository).FullName!
        };
        var routingCapture = new MultiCategoryLogCaptureProvider(routingCategories);
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Warning);
            foreach (var c in routingCategories)
                builder.AddFilter(c, LogLevel.Debug);
            builder.AddProvider(routingCapture);
        });
        RoutingLogCapture = routingCapture;
        services.Configure<DynamoDbOptions>(o =>
        {
            o.ServiceUrl = serviceUrl.Trim();
            o.Region = "us-east-1";
            o.AccessKeyId = "local";
            o.SecretAccessKey = "local";
            o.ExecutionQueueTableName = "ExecutionQueue";
            o.WorkerNodesTableName = "WorkerNodes";
            o.ClientTimeoutSeconds = 15;
        });
        services.AddSingleton<IDynamoDbContextFactory, DynamoDbContextFactory>();
        services.AddScoped<IExecutionQueueRepository, ExecutionQueueRepository>();
        services.AddScoped<IWorkerNodeRepository, WorkerNodeRepository>();
        Services = services.BuildServiceProvider();

        using var linked = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var factory = Services.GetRequiredService<IDynamoDbContextFactory>();
        await DynamoDbLocalSchema.EnsureTablesAsync(factory.CreateClient(), linked.Token);
    }

    public async Task EnsureLocalRespondsAsync(CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(TimeSpan.FromSeconds(12));
        var factory = Services.GetRequiredService<IDynamoDbContextFactory>();
        var client = factory.CreateClient();
        await client.ListTablesAsync(linked.Token);
    }

    public async Task DisposeAsync()
    {
        Services?.Dispose();
        if (_container != null)
            await _container.DisposeAsync();
    }
}
