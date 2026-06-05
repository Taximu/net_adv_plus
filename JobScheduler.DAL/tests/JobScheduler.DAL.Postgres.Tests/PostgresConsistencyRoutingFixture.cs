using Dapper;
using JobScheduler.DAL.Configuration;
using JobScheduler.DAL.Connection;
using JobScheduler.DAL.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Testcontainers.PostgreSql;

namespace JobScheduler.DAL.Postgres.Tests;

/// <summary>
/// PostgreSQL with <b>two identical read connection strings</b> so round-robin exposes <c>ReplicaIndex</c> 0 and 1 in logs
/// (same host/port as primary when using one container — the index still proves routing).
/// </summary>
public sealed class PostgresConsistencyRoutingFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    public ServiceProvider Services { get; private set; } = null!;
    public MultiCategoryLogCaptureProvider RoutingLogCapture { get; private set; } = null!;

    public Guid SeedUserId { get; private set; }
    public Guid SeedJobId { get; private set; }

    public async Task InitializeAsync()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        DapperNpgsqlConfiguration.RegisterDateAndTimeHandlers();

        using var pullCts = new CancellationTokenSource(TimeSpan.FromMinutes(8));
        await DockerImagePullHelper.PullIfNeededAsync(PostgreSqlBuilder.PostgreSqlImage, pullCts.Token)
            .ConfigureAwait(false);

        _container = new PostgreSqlBuilder()
            .WithDatabase("job_config_db")
            .WithUsername("job_admin")
            .WithPassword("StrongTestPass123")
            .Build();

        await _container.StartAsync().ConfigureAwait(false);

        var writeCs = _container.GetConnectionString();
        var sb = new NpgsqlConnectionStringBuilder(writeCs) { Pooling = false, Timeout = 60 };
        writeCs = sb.ConnectionString;

        var schemaPath = Path.Combine(AppContext.BaseDirectory, "postgres-schema", "02-schema.sql");
        if (!File.Exists(schemaPath))
            throw new FileNotFoundException($"Expected schema at {schemaPath}.", schemaPath);

        var schemaSql = await File.ReadAllTextAsync(schemaPath).ConfigureAwait(false);
        await using (var conn = new NpgsqlConnection(writeCs))
        {
            await conn.OpenAsync().ConfigureAwait(false);
            await using (var cmd = new NpgsqlCommand(schemaSql, conn) { CommandTimeout = 120 })
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        SeedJobId = Guid.NewGuid();
        SeedUserId = Guid.NewGuid();
        await using (var conn = new NpgsqlConnection(writeCs))
        {
            await conn.OpenAsync().ConfigureAwait(false);
            await conn.ExecuteAsync(
                """
                INSERT INTO users (user_id, email, username, full_name, role, password_hash)
                VALUES (@UserId, 'consistency-routing@local', 'consistency_routing', 'DAL', 'user', 'x');
                INSERT INTO job_definitions (job_id, user_id, name, description, job_type, status, created_by)
                VALUES (@JobId, @UserId, 'routing test job', 'test', 'api_call', 'active', 'consistency_routing');
                """,
                new { UserId = SeedUserId, JobId = SeedJobId }).ConfigureAwait(false);
        }

        var factoryCategory = typeof(PostgresConnectionFactory).FullName!;
        RoutingLogCapture = new MultiCategoryLogCaptureProvider(new[] { factoryCategory });

        var services = new ServiceCollection();
        services.AddOptions();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Warning);
            builder.AddFilter(factoryCategory, LogLevel.Debug);
            builder.AddProvider(RoutingLogCapture);
        });
        services.Configure<DatabaseOptions>(o =>
        {
            o.PostgresWriteConnectionString = writeCs;
            o.PostgresReadConnectionStrings = new List<string> { writeCs, writeCs };
        });
        services.AddSingleton<IDbConnectionFactory, PostgresConnectionFactory>();
        services.AddScoped<IJobDefinitionRepository, JobDefinitionRepository>();
        Services = services.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        Services?.Dispose();
        if (_container != null)
            await _container.DisposeAsync().ConfigureAwait(false);
    }
}
