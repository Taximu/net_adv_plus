using Confluent.Kafka;
using JobScheduler.Messaging;
using JobScheduler.Observability;
using Microsoft.Extensions.Options;

namespace JobScheduler.JobOrchestrator.Workers;

/// <summary>UC 2.1 streaming consumer — processes execution lifecycle events (observability / audit).</summary>
public sealed class ExecutionLifecycleConsumerWorker : BackgroundService
{
    private readonly ILogger<ExecutionLifecycleConsumerWorker> _logger;
    private readonly KafkaConsumerOptions _options;
    private readonly JobSchedulerAppMetrics _metrics;

    public ExecutionLifecycleConsumerWorker(
        IOptions<KafkaConsumerOptions> options,
        ILogger<ExecutionLifecycleConsumerWorker> logger,
        JobSchedulerAppMetrics metrics)
    {
        _options = options.Value;
        _logger = logger;
        _metrics = metrics;
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

                using var work = _metrics.BeginTrackedWork("lifecycle_consume");
                try
                {
                    var preview = result.Message.Value is { Length: > 512 }
                        ? result.Message.Value[..512] + "…"
                        : result.Message.Value;

                    _logger.LogInformation(
                        "Lifecycle event consumed partition={Partition} offset={Offset} key={Key} valuePreview={Preview}",
                        result.Partition.Value,
                        result.Offset.Value,
                        result.Message.Key,
                        preview);

                    work.Complete("success");
                }
                catch (Exception)
                {
                    work.Complete("error");
                    throw;
                }
            }
            catch (ConsumeException ex)
            {
                _logger.LogWarning(ex, "Lifecycle consume error");
                _metrics.RecordBackgroundTask("lifecycle_consume", "consume_error", TimeSpan.Zero);
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
