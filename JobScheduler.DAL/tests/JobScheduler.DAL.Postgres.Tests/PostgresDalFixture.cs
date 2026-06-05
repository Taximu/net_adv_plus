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
/// Single-node PostgreSQL (official image) with the same schema as streaming tests; seeds one user + job for FKs.
/// Read and write use the same connection string (validates repository SQL paths; replication split is covered elsewhere).
/// Registers <see cref="IJobDefinitionRepository"/> (UC 1.1) and <see cref="IJobScheduleRepository"/> (schedules on that job).
/// </summary>
public sealed class PostgresDalFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    public ServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// Captures <see cref="JobScheduleRepository"/> debug lines (partition / histogram). Clear before assertions.
    /// Console output of the same lines is enabled by default during tests; set env <c>DAL_PG_TESTS_SUPPRESS_PARTITION_CONSOLE=1</c> to disable.
    /// </summary>
    public ListCapturingLoggerProvider JobScheduleLogCapture { get; private set; } = null!;

    /// <summary>Pre-seeded <c>job_definitions.job_id</c> for schedule FK tests.</summary>
    public Guid SeedJobId { get; private set; }

    /// <summary>Pre-seeded <c>users.user_id</c> matching <see cref="SeedJobId"/> owner (UC 1.1 job creation tests).</summary>
    public Guid SeedUserId { get; private set; }

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
            throw new FileNotFoundException($"Expected schema at {schemaPath} (check csproj CopyToOutputDirectory).", schemaPath);

        var schemaSql = await File.ReadAllTextAsync(schemaPath).ConfigureAwait(false);
        await using (var conn = new NpgsqlConnection(writeCs))
        {
            await conn.OpenAsync().ConfigureAwait(false);
            await using (var cmd = new NpgsqlCommand(schemaSql, conn) { CommandTimeout = 120 })
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        SeedJobId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SeedUserId = userId;
        await using (var conn = new NpgsqlConnection(writeCs))
        {
            await conn.OpenAsync().ConfigureAwait(false);
            await conn.ExecuteAsync(
                """
                INSERT INTO users (user_id, email, username, full_name, role, password_hash)
                VALUES (@UserId, 'dal-pg-test@local', 'dal_pg_test', 'DAL', 'user', 'x');
                INSERT INTO job_definitions (job_id, user_id, name, description, job_type, status, created_by)
                VALUES (@JobId, @UserId, 'DAL test job', 'test', 'api_call', 'active', 'dal_pg_test');
                """,
                new { UserId = userId, JobId = SeedJobId }).ConfigureAwait(false);
        }

        var services = new ServiceCollection();
        services.AddOptions();
        var logCapture = new ListCapturingLoggerProvider();
        JobScheduleLogCapture = logCapture;

        var suppressPartitionConsole = string.Equals(
            Environment.GetEnvironmentVariable("DAL_PG_TESTS_SUPPRESS_PARTITION_CONSOLE"),
            "1",
            StringComparison.OrdinalIgnoreCase);

        services.AddLogging(builder =>
        {
            // Keep other categories quiet; partition CRUD logs are Debug on JobScheduleRepository only.
            builder.SetMinimumLevel(LogLevel.Warning);
            builder.AddFilter(typeof(JobScheduleRepository).FullName!, LogLevel.Debug);
            builder.AddProvider(logCapture);
            if (!suppressPartitionConsole)
            {
                builder.AddSimpleConsole(o =>
                {
                    o.SingleLine = true;
                    o.TimestampFormat = "HH:mm:ss ";
                });
            }
        });
        services.Configure<DatabaseOptions>(o =>
        {
            o.PostgresWriteConnectionString = writeCs;
            o.PostgresReadConnectionStrings = new List<string> { writeCs };
        });
        services.AddSingleton<IDbConnectionFactory, PostgresConnectionFactory>();
        services.AddScoped<IJobDefinitionRepository, JobDefinitionRepository>();
        services.AddScoped<IJobScheduleRepository, JobScheduleRepository>();
        Services = services.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        Services?.Dispose();
        if (_container != null)
            await _container.DisposeAsync().ConfigureAwait(false);
    }
}
