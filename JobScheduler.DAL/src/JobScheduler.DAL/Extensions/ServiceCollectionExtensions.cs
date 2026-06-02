using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using JobScheduler.DAL.Connection;
using JobScheduler.DAL.Repositories;
using JobScheduler.DAL.UnitOfWork;
using JobScheduler.DAL.Configuration;
using JobScheduler.DAL.DynamoDB;
using JobScheduler.DAL.DynamoDB.Repositories;
using Dapper;
using JobScheduler.DAL;

namespace JobScheduler.DAL.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>UC 1.1 — PostgreSQL job configuration DAL.</summary>
    public static IServiceCollection AddDataAccessLayer(this IServiceCollection services, IConfiguration configuration)
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        DapperNpgsqlConfiguration.RegisterDateAndTimeHandlers();
        services.Configure<DatabaseOptions>(configuration.GetSection("Database"));
        services.AddScoped<IDbConnectionFactory, PostgresConnectionFactory>();
        services.AddScoped<IJobDefinitionRepository, JobDefinitionRepository>();
        services.AddScoped<IJobScheduleRepository, JobScheduleRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork.UnitOfWork>();
        return services;
    }

    /// <summary>UC 2.1 — DynamoDB execution queue + worker nodes.</summary>
    public static IServiceCollection AddDynamoDbDataAccessLayer(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DynamoDbOptions>(configuration.GetSection("DynamoDB"));
        services.AddSingleton<IDynamoDbContextFactory, DynamoDbContextFactory>();
        services.AddScoped<IExecutionQueueRepository, ExecutionQueueRepository>();
        services.AddScoped<IWorkerNodeRepository, WorkerNodeRepository>();
        return services;
    }
}
