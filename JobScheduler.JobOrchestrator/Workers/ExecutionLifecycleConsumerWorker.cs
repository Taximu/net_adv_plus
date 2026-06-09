using Confluent.Kafka;
using JobScheduler.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobScheduler.JobOrchestrator.Workers;

/// <summary>UC 2.1 streaming consumer — processes execution lifecycle events (observability / audit).</summary>
public sealed class ExecutionLifecycleConsumerWorker : BackgroundService
{
    private readonly ILogger<ExecutionLifecycleConsumerWorker> _logger;
    private readonly KafkaConsumerOptions _options;

    public ExecutionLifecycleConsumerWorker(IOptions<KafkaConsumerOptions> options, ILogger<ExecutionLifecycleConsumerWorker> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "JobOrchestrator lifecycle consumer starting brokers={Brokers} group={Group} topic={Topic}",
            _options.BootstrapServers,
            _options.GroupId,
            _options.Topic);

        await Task.Yield();

        using var consumer = KafkaConsumerFactory.CreateConsumer(_options);
        consumer.Subscribe(_options.Topic);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(TimeSpan.FromSeconds(5));
                if (result is null)
                    continue;

                if (result.IsPartitionEOF)
                    continue;

                var preview = result.Message.Value is { Length: > 512 }
                    ? result.Message.Value[..512] + "…"
                    : result.Message.Value;

                _logger.LogInformation(
                    "Lifecycle event consumed partition={Partition} offset={Offset} key={Key} valuePreview={Preview}",
                    result.Partition.Value,
                    result.Offset.Value,
                    result.Message.Key,
                    preview);
            }
            catch (ConsumeException ex)
            {
                _logger.LogWarning(ex, "Lifecycle consume error");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        try
        {
            consumer.Close();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Consumer close");
        }
    }
}
