using JobScheduler.BL.Contracts;
using JobScheduler.DAL.Consistency;
using JobScheduler.DAL.DynamoDB.Models;
using JobScheduler.DAL.DynamoDB.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace JobScheduler.BL.Services;

/// <summary>
/// UC 2.3 — eventually consistent history reads with optional short-TTL cache to absorb read bursts
/// (many concurrent downloaders hitting the same job/cursor).
/// </summary>
public sealed class JobExecutionHistoryService : IJobExecutionHistoryService
{
    private const int MinLimit = 1;
    private const int MaxLimit = 200;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(8);

    private readonly IExecutionQueueRepository _executions;
    private readonly IMemoryCache _cache;
    private readonly ILogger<JobExecutionHistoryService> _logger;

    public JobExecutionHistoryService(
        IExecutionQueueRepository executions,
        IMemoryCache cache,
        ILogger<JobExecutionHistoryService> logger)
    {
        _executions = executions;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ExecutionHistoryPageJson> GetExecutionHistoryPageAsync(
        Guid jobId,
        int limit,
        string? paginationToken,
        bool fullDetails,
        CancellationToken cancellationToken = default)
    {
        var clamped = Math.Clamp(limit, MinLimit, MaxLimit);
        var jobKey = jobId.ToString("D");
        var cacheKey = $"uc23:hist:{jobKey}:{clamped}:{paginationToken ?? ""}:{fullDetails}";

        if (_cache.TryGetValue(cacheKey, out ExecutionHistoryPageJson? cached) && cached is not null)
        {
            _logger.LogDebug("UC2.3 history cache hit jobId={JobId}", jobKey);
            return cached;
        }

        var (items, next) = await _executions
            .QueryByJobIdAsync(jobKey, clamped, paginationToken, scanIndexForward: false, ConsistencyLevel.Eventual, cancellationToken)
            .ConfigureAwait(false);

        var page = new ExecutionHistoryPageJson
        {
            JobId = jobKey,
            Next = next,
            Events = items.Select(i => MapRow(i, fullDetails)).ToList()
        };

        _cache.Set(
            cacheKey,
            page,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheTtl
            });

        return page;
    }

    private static ExecutionHistoryRowJson MapRow(ExecutionQueueItem i, bool fullDetails)
    {
        ExecutionContextJson? ctx = null;
        if (fullDetails && i.ExecutionContext != null)
        {
            ctx = new ExecutionContextJson
            {
                Environment = i.ExecutionContext.Environment,
                TriggerSource = i.ExecutionContext.TriggerSource,
                UserId = i.ExecutionContext.UserId,
                Parameters = i.ExecutionContext.Parameters
            };
        }

        ExecutionResultJson? res = null;
        if (i.ExecutionResult != null)
        {
            var r = i.ExecutionResult;
            res = fullDetails
                ? new ExecutionResultJson
                {
                    Status = r.Status,
                    StatusCode = r.StatusCode,
                    ResponseSizeBytes = r.ResponseSizeBytes,
                    ErrorCode = r.ErrorCode,
                    ErrorMessage = r.ErrorMessage,
                    ErrorStack = r.StackTrace
                }
                : new ExecutionResultJson
                {
                    Status = r.Status,
                    StatusCode = r.StatusCode,
                    ResponseSizeBytes = r.ResponseSizeBytes,
                    ErrorCode = r.ErrorCode
                };
        }

        return new ExecutionHistoryRowJson
        {
            QueueId = i.QueueId,
            ScheduledFor = i.ScheduledFor,
            ScheduleId = i.ScheduleId,
            QueueStatus = i.QueueStatus,
            Priority = i.Priority,
            RetryCount = i.RetryCount,
            MaxRetries = i.MaxRetries,
            StartedAt = i.StartedAt,
            CompletedAt = i.CompletedAt,
            TimeoutAt = i.TimeoutAt,
            AssignedWorkerId = i.AssignedWorkerId,
            Context = ctx,
            Result = res
        };
    }
}
