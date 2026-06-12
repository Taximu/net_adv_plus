using JobScheduler.BL.Services;
using Microsoft.Extensions.DependencyInjection;

namespace JobScheduler.BL.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers business services that choose consistency levels for DAL calls (APIs stay unaware).
    /// Call <see cref="DAL.Extensions.ServiceCollectionExtensions.AddDataAccessLayer"/> and
    /// <see cref="DAL.Extensions.ServiceCollectionExtensions.AddDynamoDbDataAccessLayer"/> so SQL + DynamoDB repositories resolve.
    /// </summary>
    public static IServiceCollection AddJobSchedulerBusinessLogic(this IServiceCollection services)
    {
        services.AddScoped<IUserJobsService, UserJobsService>();
        services.AddScoped<ISchedulerCatalogService, SchedulerCatalogService>();
        services.AddScoped<IUserSchedulesService, UserSchedulesService>();
        services.AddScoped<IExecutionOrchestrationService, ExecutionOrchestrationService>();
        services.AddScoped<IJobExecutionHistoryService, JobExecutionHistoryService>();
        return services;
    }
}
