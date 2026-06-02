using Dapper;
using JobScheduler.DAL.Connection;
using JobScheduler.DAL.Models;

namespace JobScheduler.DAL.Repositories;

public class JobDefinitionRepository : IJobDefinitionRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public JobDefinitionRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<JobDefinition> CreateAsync(JobDefinition job)
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
                @ApiEndpoint, @HttpMethod, @RequestHeaders, @RequestBodyTemplate,
                @AuthType, @AuthConfig, @TimeoutSeconds, @MaxRetries,
                @RetryBackoffMultiplier, @RetryableStatusCodes, @Status,
                @Tags, @Metadata, @CreatedBy
            )
            RETURNING *;";

        using var connection = await _connectionFactory.GetWriteConnectionAsync();
        return await connection.QuerySingleAsync<JobDefinition>(sql, job);
    }

    public async Task<JobDefinition?> GetByIdAsync(Guid jobId)
    {
        const string sql = @"SELECT * FROM job_definitions WHERE job_id = @JobId";
        using var connection = await _connectionFactory.GetReadConnectionAsync();
        return await connection.QueryFirstOrDefaultAsync<JobDefinition>(sql, new { JobId = jobId });
    }

    public async Task<JobDefinition> UpdateAsync(JobDefinition job)
    {
        const string sql = @"
            UPDATE job_definitions SET
                user_id = @UserId, name = @Name, description = @Description, job_type = @JobType, category = @Category,
                api_endpoint = @ApiEndpoint, http_method = @HttpMethod, request_headers = @RequestHeaders, request_body_template = @RequestBodyTemplate,
                auth_type = @AuthType, auth_config = @AuthConfig, timeout_seconds = @TimeoutSeconds, max_retries = @MaxRetries,
                retry_backoff_multiplier = @RetryBackoffMultiplier, retryable_status_codes = @RetryableStatusCodes, status = @Status,
                tags = @Tags, metadata = @Metadata, updated_by = @UpdatedBy
            WHERE job_id = @JobId
            RETURNING *;";

        using var connection = await _connectionFactory.GetWriteConnectionAsync();
        return await connection.QuerySingleAsync<JobDefinition>(sql, job);
    }

    public async Task<bool> DeleteAsync(Guid jobId)
    {
        const string sql = @"DELETE FROM job_definitions WHERE job_id = @JobId";
        using var connection = await _connectionFactory.GetWriteConnectionAsync();
        var rows = await connection.ExecuteAsync(sql, new { JobId = jobId });
        return rows > 0;
    }

    public async Task<JobDefinition> UpdateStatusAsync(Guid jobId, string status, string updatedBy)
    {
        const string sql = @"
            UPDATE job_definitions SET status = @Status, updated_by = @UpdatedBy, updated_at = CURRENT_TIMESTAMP
            WHERE job_id = @JobId
            RETURNING *;";

        using var connection = await _connectionFactory.GetWriteConnectionAsync();
        return await connection.QuerySingleAsync<JobDefinition>(sql, new { JobId = jobId, Status = status, UpdatedBy = updatedBy });
    }

    public async Task<IEnumerable<JobDefinition>> GetByUserIdAsync(Guid userId)
    {
        const string sql = @"SELECT * FROM job_definitions WHERE user_id = @UserId ORDER BY created_at DESC";
        using var connection = await _connectionFactory.GetReadConnectionAsync();
        return await connection.QueryAsync<JobDefinition>(sql, new { UserId = userId });
    }

    public async Task<IEnumerable<JobDefinition>> GetByStatusAsync(string status)
    {
        const string sql = @"SELECT * FROM job_definitions WHERE status = @Status ORDER BY created_at DESC";
        using var connection = await _connectionFactory.GetReadConnectionAsync();
        return await connection.QueryAsync<JobDefinition>(sql, new { Status = status });
    }

    public async Task<IEnumerable<JobDefinition>> GetActiveJobsAsync()
    {
        const string sql = @"SELECT * FROM job_definitions WHERE status = 'active' ORDER BY created_at DESC";
        using var connection = await _connectionFactory.GetReadConnectionAsync();
        return await connection.QueryAsync<JobDefinition>(sql);
    }

    public async Task<bool> ExistsByNameAsync(Guid userId, string name)
    {
        const string sql = @"SELECT EXISTS (SELECT 1 FROM job_definitions WHERE user_id = @UserId AND name = @Name)";
        using var connection = await _connectionFactory.GetReadConnectionAsync();
        return await connection.QuerySingleAsync<bool>(sql, new { UserId = userId, Name = name });
    }
}
