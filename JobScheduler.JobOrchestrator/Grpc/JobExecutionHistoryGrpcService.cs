using Grpc.Core;
using JobScheduler.BL.Contracts;
using JobScheduler.BL.Services;
using JobScheduler.JobOrchestrator.Grpc.History;

namespace JobScheduler.JobOrchestrator.Grpc;

/// <summary>UC 2.3 — Protobuf/gRPC surface mirroring the JSON history endpoint.</summary>
public sealed class JobExecutionHistoryGrpcService : JobExecutionHistory.JobExecutionHistoryBase
{
    private readonly IJobExecutionHistoryService _history;

    public JobExecutionHistoryGrpcService(IJobExecutionHistoryService history) => _history = history;

    public override async Task<GetExecutionHistoryPageResponse> GetExecutionHistoryPage(
        GetExecutionHistoryPageRequest request,
        ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.JobId) || !Guid.TryParse(request.JobId, out var jobId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "job_id must be a valid GUID."));

        var limit = request.Limit == 0 ? 50 : request.Limit;

        try
        {
            var page = await _history
                .GetExecutionHistoryPageAsync(jobId, limit, request.PaginationToken, request.FullDetails, context.CancellationToken)
                .ConfigureAwait(false);
            return Map(page);
        }
        catch (ArgumentException)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "invalid_pagination_token"));
        }
    }

    private static GetExecutionHistoryPageResponse Map(ExecutionHistoryPageJson page)
    {
        var resp = new GetExecutionHistoryPageResponse { JobId = page.JobId };
        if (!string.IsNullOrEmpty(page.Next))
            resp.NextCursor = page.Next;

        foreach (var row in page.Events)
            resp.Events.Add(MapRow(row));

        return resp;
    }

    private static ExecutionHistoryRow MapRow(ExecutionHistoryRowJson row)
    {
        var r = new ExecutionHistoryRow
        {
            QueueId = row.QueueId,
            ScheduledFor = row.ScheduledFor,
            ScheduleId = row.ScheduleId,
            QueueStatus = row.QueueStatus,
            Priority = row.Priority,
            RetryCount = row.RetryCount,
            MaxRetries = row.MaxRetries
        };

        if (!string.IsNullOrEmpty(row.StartedAt))
            r.StartedAt = row.StartedAt;
        if (!string.IsNullOrEmpty(row.CompletedAt))
            r.CompletedAt = row.CompletedAt;
        if (!string.IsNullOrEmpty(row.TimeoutAt))
            r.TimeoutAt = row.TimeoutAt;
        if (!string.IsNullOrEmpty(row.AssignedWorkerId))
            r.AssignedWorkerId = row.AssignedWorkerId;

        if (row.Context != null)
        {
            var ctx = new ExecutionHistoryContext
            {
                Environment = row.Context.Environment ?? "",
                TriggerSource = row.Context.TriggerSource ?? "",
                UserId = row.Context.UserId ?? ""
            };
            if (row.Context.Parameters is { Count: > 0 })
            {
                foreach (var kv in row.Context.Parameters)
                    ctx.Parameters[kv.Key] = kv.Value;
            }

            r.Context = ctx;
        }

        if (row.Result != null)
        {
            var res = new ExecutionHistoryResult { Status = row.Result.Status ?? "" };
            if (!string.IsNullOrEmpty(row.Result.ErrorCode))
                res.ErrorCode = row.Result.ErrorCode;
            if (row.Result.StatusCode.HasValue)
                res.StatusCode = row.Result.StatusCode.Value;
            if (row.Result.ResponseSizeBytes.HasValue)
                res.ResponseSizeBytes = row.Result.ResponseSizeBytes.Value;
            if (!string.IsNullOrEmpty(row.Result.ErrorMessage))
                res.ErrorMessage = row.Result.ErrorMessage;
            if (!string.IsNullOrEmpty(row.Result.ErrorStack))
                res.ErrorStack = row.Result.ErrorStack;
            r.Result = res;
        }

        return r;
    }
}
