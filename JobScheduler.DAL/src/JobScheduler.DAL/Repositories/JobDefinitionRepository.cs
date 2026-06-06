using Dapper;
using JobScheduler.DAL.Connection;
using JobScheduler.DAL.Consistency;
using JobScheduler.DAL.Models;

namespace JobScheduler.DAL.Repositories;

public class JobDefinitionRepository : IJobDefinitionRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public JobDefinitionRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<JobDefinition> CreateAsync(JobDefinition job, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO job_definitions (
                job_id, user_id, name, description, job_type, category,
                api_endpoint, http_method, request_headers, request_body_template,
                auth_type, auth_config, timeout_seconds, max_retries,
                retry_backoff_multiplier, retryable_status_codes, status,
                tags, metadata, created_by
            ) VALUES (
                @JobId, @UserId, @Name, @Description, @JobType, @Category,
                @ApiEndpoint, @HttpMethod, NULLIF(@RequestHeaders, '')::jsonb, @RequestBodyTemplate,
                @AuthType, NULLIF(@AuthConfig, '')::jsonb, @TimeoutSeconds, @MaxRetries,
                @RetryBackoffMultiplier, @RetryableStatusCodes, @Status,
                @Tags, NULLIF(@Metadata, '')::jsonb, @CreatedBy
            )
            RETURNING *;";

        using var connection = await _connectionFactory.GetWriteConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QuerySingleAsync<JobDefinition>(new CommandDefinition(sql, job, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<JobDefinition?> GetByIdAsync(Guid jobId, ConsistencyLevel consistencyLevel, CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT * FROM job_definitions WHERE job_id = @JobId";
        using var connection = await _connectionFactory.GetReadConnectionAsync(consistencyLevel, cancellationToken).ConfigureAwait(false);
        return await connection.QueryFirstOrDefaultAsync<JobDefinition>(
                new CommandDefinition(sql, new { JobId = jobId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<JobDefinition> UpdateAsync(JobDefinition job, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE job_definitions SET
                user_id = @UserId, name = @Name, description = @Description, job_type = @JobType, category = @Category,
                api_endpoint = @ApiEndpoint, http_method = @HttpMethod, request_headers = NULLIF(@RequestHeaders, '')::jsonb, request_body_template = @RequestBodyTemplate,
                auth_type = @AuthType, auth_config = NULLIF(@AuthConfig, '')::jsonb, timeout_seconds = @TimeoutSeconds, max_retries = @MaxRetries,
                retry_backoff_multiplier = @RetryBackoffMultiplier, retryable_status_codes = @RetryableStatusCodes, status = @Status,
                tags = @Tags, metadata = NULLIF(@Metadata, '')::jsonb, updated_by = @UpdatedBy
            WHERE job_id = @JobId
            RETURNING *;";

        using var connection = await _connectionFactory.GetWriteConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QuerySingleAsync<JobDefinition>(new CommandDefinition(sql, job, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        const string sql = @"DELETE FROM job_definitions WHERE job_id = @JobId";
        using var connection = await _connectionFactory.GetWriteConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.ExecuteAsync(new CommandDefinition(sql, new { JobId = jobId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
        return rows > 0;
    }

    public async Task<JobDefinition> UpdateStatusAsync(Guid jobId, string status, string updatedBy, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE job_definitions SET status = @Status, updated_by = @UpdatedBy, updated_at = CURRENT_TIMESTAMP
            WHERE job_id = @JobId
            RETURNING *;";

        using var connection = await _connectionFactory.GetWriteConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QuerySingleAsync<JobDefinition>(
                new CommandDefinition(sql, new { JobId = jobId, Status = status, UpdatedBy = updatedBy }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<IEnumerable<JobDefinition>> GetByUserIdAsync(Guid userId, ConsistencyLevel consistencyLevel, CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT * FROM job_definitions WHERE user_id = @UserId ORDER BY created_at DESC";
        using var connection = await _connectionFactory.GetReadConnectionAsync(consistencyLevel, cancellationToken).ConfigureAwait(false);
        return await connection.QueryAsync<JobDefinition>(new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<IEnumerable<JobDefinition>> GetByStatusAsync(string status, ConsistencyLevel consistencyLevel, CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT * FROM job_definitions WHERE status = @Status ORDER BY created_at DESC";
        using var connection = await _connectionFactory.GetReadConnectionAsync(consistencyLevel, cancellationToken).ConfigureAwait(false);
        return await connection.QueryAsync<JobDefinition>(new CommandDefinition(sql, new { Status = status }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<IEnumerable<JobDefinition>> GetActiveJobsAsync(ConsistencyLevel consistencyLevel, CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT * FROM job_definitions WHERE status = 'active' ORDER BY created_at DESC";
        using var connection = await _connectionFactory.GetReadConnectionAsync(consistencyLevel, cancellationToken).ConfigureAwait(false);
        return await connection.QueryAsync<JobDefinition>(new CommandDefinition(sql, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task<bool> ExistsByNameAsync(Guid userId, string name, ConsistencyLevel consistencyLevel, CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT EXISTS (SELECT 1 FROM job_definitions WHERE user_id = @UserId AND name = @Name)";
        using var connection = await _connectionFactory.GetReadConnectionAsync(consistencyLevel, cancellationToken).ConfigureAwait(false);
        return await connection.QuerySingleAsync<bool>(
                new CommandDefinition(sql, new { UserId = userId, Name = name }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }
}
