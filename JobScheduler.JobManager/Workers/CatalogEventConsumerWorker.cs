using System.Text;
using Confluent.Kafka;
using JobScheduler.Messaging;
using Microsoft.Extensions.Options;

namespace JobScheduler.JobManager.Workers;

/// <summary>UC 1.1 streaming consumer — processes catalog domain events (projection / audit style logging).</summary>
public sealed class CatalogEventConsumerWorker : BackgroundService
{
    private readonly ILogger<CatalogEventConsumerWorker> _logger;
    private readonly KafkaConsumerOptions _options;

    public CatalogEventConsumerWorker(IOptions<KafkaConsumerOptions> options, ILogger<CatalogEventConsumerWorker> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "JobManager catalog consumer starting brokers={Brokers} group={Group} topic={Topic}",
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
                {
                    _logger.LogDebug("EOF partition {Partition}", result.Partition.Value);
                    continue;
                }

                var preview = result.Message.Value is { Length: > 512 }
                    ? result.Message.Value[..512] + "…"
                    : result.Message.Value;

                _logger.LogInformation(
                    "Catalog event consumed partition={Partition} offset={Offset} key={Key} valuePreview={Preview}",
                    result.Partition.Value,
                    result.Offset.Value,
                    result.Message.Key,
                    preview);
            }
            catch (ConsumeException ex)
            {
                _logger.LogWarning(ex, "Consume error");
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
