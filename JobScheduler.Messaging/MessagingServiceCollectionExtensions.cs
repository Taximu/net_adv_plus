using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobScheduler.Messaging;

public static class MessagingServiceCollectionExtensions
{
    /// <summary>Registers optional Kafka publishers for catalog + execution lifecycle topics.</summary>
    public static IServiceCollection AddJobSchedulerMessagingPublishers(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MessagingPublisherOptions>(configuration.GetSection("Messaging:Publishers"));

        var enabled = configuration.GetSection("Messaging:Publishers").GetValue("Enabled", false);

        if (enabled)
        {
            services.AddSingleton<KafkaJsonProducer>();
            services.AddSingleton<IJobCatalogEventSink, KafkaJobCatalogEventSink>();
            services.AddSingleton<IExecutionLifecycleEventSink, KafkaExecutionLifecycleEventSink>();
        }
        else
        {
            services.AddSingleton<IJobCatalogEventSink, NullJobCatalogEventSink>();
            services.AddSingleton<IExecutionLifecycleEventSink, NullExecutionLifecycleEventSink>();
        }

        return services;
    }
}
