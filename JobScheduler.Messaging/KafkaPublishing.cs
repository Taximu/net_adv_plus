using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobScheduler.Messaging;

internal static class KafkaJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string SerializeEnvelope(string eventType, DateTimeOffset occurredAt, object data) =>
        JsonSerializer.Serialize(new { eventType, occurredAt, data }, Options);
}

/// <summary>Shared Kafka producer for fire-and-forget JSON events (development / demo throughput).</summary>
public sealed class KafkaJsonProducer : IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaJsonProducer> _logger;

    public KafkaJsonProducer(IOptions<MessagingPublisherOptions> options, ILogger<KafkaJsonProducer> logger)
    {
        _logger = logger;
        var o = options.Value;
        var config = new ProducerConfig
        {
            BootstrapServers = o.BootstrapServers,
            ClientId = o.ClientId,
            Acks = Acks.Leader,
            EnableIdempotence = false,
            MessageSendMaxRetries = 2,
            RequestTimeoutMs = 10_000
        };
        _producer = new ProducerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => _logger.LogWarning("Kafka producer error: {Reason}", e.Reason))
            .Build();
    }

    public async Task ProduceJsonAsync(string topic, string key, string json, CancellationToken cancellationToken)
    {
        try
        {
            await _producer.ProduceAsync(
                    topic,
                    new Message<string, string> { Key = key, Value = json },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogWarning(ex, "Kafka produce failed topic={Topic} key={Key}", topic, key);
            throw;
        }
    }

    public void Dispose() => _producer.Dispose();
}

public sealed class KafkaJobCatalogEventSink : IJobCatalogEventSink
{
    private readonly KafkaJsonProducer _producer;
    private readonly ILogger<KafkaJobCatalogEventSink> _logger;

    public KafkaJobCatalogEventSink(KafkaJsonProducer producer, ILogger<KafkaJobCatalogEventSink> logger)
    {
        _producer = producer;
        _logger = logger;
    }

    public async Task PublishJobDefinitionCreatedAsync(JobCatalogCreatedEventPayload payload, CancellationToken cancellationToken = default)
    {
        var json = KafkaJsonSerializer.SerializeEnvelope("job.definition.created", payload.OccurredAt, payload);
        try
        {
            await _producer
                .ProduceJsonAsync(MessagingTopics.JobCatalogEvents, payload.UserId.ToString("N"), json, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Catalog event publish skipped after failure (job {JobId})", payload.JobId);
        }
    }
}

public sealed class KafkaExecutionLifecycleEventSink : IExecutionLifecycleEventSink
{
    private readonly KafkaJsonProducer _producer;
    private readonly ILogger<KafkaExecutionLifecycleEventSink> _logger;

    public KafkaExecutionLifecycleEventSink(KafkaJsonProducer producer, ILogger<KafkaExecutionLifecycleEventSink> logger)
    {
        _producer = producer;
        _logger = logger;
    }

    public async Task PublishExecutionEnqueuedAsync(ExecutionEnqueuedEventPayload payload, CancellationToken cancellationToken = default)
    {
        var json = KafkaJsonSerializer.SerializeEnvelope("execution.enqueued", payload.OccurredAt, payload);
        try
        {
            await _producer
                .ProduceJsonAsync(MessagingTopics.ExecutionLifecycle, payload.QueueId, json, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lifecycle event publish skipped after failure (queue {QueueId})", payload.QueueId);
        }
    }
}
