using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace JobScheduler.Observability;

public static class ObservabilityExtensions
{
    /// <summary>
    /// Registers OpenTelemetry metrics (ASP.NET Core, HttpClient, .NET runtime, process CPU/memory) and Prometheus exposition.
    /// </summary>
    /// <param name="services">Host services.</param>
    /// <param name="serviceName">Value for OpenTelemetry resource <c>service.name</c> (e.g. JobScheduler.Api).</param>
    public static IServiceCollection AddJobSchedulerObservability(this IServiceCollection services, string serviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        services.AddSingleton<JobSchedulerAppMetrics>();

        services.AddOpenTelemetry()
            .ConfigureResource(rb => rb.AddService(serviceName))
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(JobSchedulerAppMetrics.MeterName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation()
                    .AddPrometheusExporter();
            });

        return services;
    }

    /// <summary>Maps the Prometheus scrape endpoint (default path <c>/metrics</c>).</summary>
    public static WebApplication MapJobSchedulerMetrics(this WebApplication app)
    {
        app.MapPrometheusScrapingEndpoint();
        return app;
    }
}
