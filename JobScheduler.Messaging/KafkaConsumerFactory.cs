using Confluent.Kafka;

namespace JobScheduler.Messaging;

public static class KafkaConsumerFactory
{
    public static IConsumer<string, string> CreateConsumer(KafkaConsumerOptions options)
    {
        if (!Enum.TryParse<AutoOffsetReset>(options.AutoOffsetReset, ignoreCase: true, out var offsetReset))
            offsetReset = AutoOffsetReset.Earliest;

        var config = new ConsumerConfig
        {
            BootstrapServers = options.BootstrapServers,
            GroupId = options.GroupId,
            AutoOffsetReset = offsetReset,
            EnableAutoCommit = true,
            EnablePartitionEof = true
        };

        return new ConsumerBuilder<string, string>(config).Build();
    }
}
