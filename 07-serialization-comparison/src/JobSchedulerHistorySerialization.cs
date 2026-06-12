using System.Text.Json;
using System.Text.Json.Serialization;
using Google.Protobuf;
using JobScheduler.BL.Contracts;
using JobScheduler.JobOrchestrator.Grpc.History;
using STJ = System.Text.Json.JsonSerializer;

namespace SerializationComparison;

/// <summary>
/// Maps the lab's <see cref="JobHistoryRecord"/> list to the JobScheduler UC 2.3 wire models and measures
/// JSON / Google.Protobuf the same way as the baseline tool.
/// </summary>
public static class JobSchedulerHistorySerialization
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Same synthetic data as <see cref="TestDataGenerator"/> → JobScheduler compact history page.</summary>
    public static ExecutionHistoryPageJson ToPage(List<JobHistoryRecord> records, string jobId = "00000000-0000-0000-0000-000000000001")
    {
        var events = records.Select(MapRow).ToList();
        return new ExecutionHistoryPageJson
        {
            JobId = jobId,
            Next = null,
            Events = events
        };
    }

    private static ExecutionHistoryRowJson MapRow(JobHistoryRecord r)
    {
        var started = r.StartedAt.ToString("O");
        var completed = r.StartedAt.AddMilliseconds(r.DurationMs).ToString("O");
        ExecutionResultJson? result;
        if (r.ErrorMessage is not null)
        {
            result = new ExecutionResultJson
            {
                Status = "failure",
                StatusCode = 500,
                ErrorCode = "ERR"
            };
        }
        else
        {
            result = new ExecutionResultJson
            {
                Status = "success",
                StatusCode = 200
            };
        }

        return new ExecutionHistoryRowJson
        {
            QueueId = $"q-{r.EventId:D9}",
            ScheduledFor = started,
            ScheduleId = $"z-{r.EventId}",
            QueueStatus = r.Status,
            Priority = 5,
            RetryCount = 0,
            MaxRetries = 3,
            StartedAt = started,
            CompletedAt = completed,
            TimeoutAt = null,
            AssignedWorkerId = null,
            Context = null,
            Result = result
        };
    }

    public static byte[] SerializeJson(ExecutionHistoryPageJson page) =>
        STJ.SerializeToUtf8Bytes(page, JsonOptions);

    public static ExecutionHistoryPageJson? DeserializeJson(byte[] data) =>
        STJ.Deserialize<ExecutionHistoryPageJson>(data, JsonOptions);

    public static GetExecutionHistoryPageResponse ToProtoResponse(ExecutionHistoryPageJson page)
    {
        var resp = new GetExecutionHistoryPageResponse { JobId = page.JobId };
        if (!string.IsNullOrEmpty(page.Next))
            resp.NextCursor = page.Next;

        foreach (var e in page.Events)
        {
            var row = new ExecutionHistoryRow
            {
                QueueId = e.QueueId,
                ScheduledFor = e.ScheduledFor,
                ScheduleId = e.ScheduleId,
                QueueStatus = e.QueueStatus,
                Priority = e.Priority,
                RetryCount = e.RetryCount,
                MaxRetries = e.MaxRetries
            };
            if (!string.IsNullOrEmpty(e.StartedAt))
                row.StartedAt = e.StartedAt;
            if (!string.IsNullOrEmpty(e.CompletedAt))
                row.CompletedAt = e.CompletedAt;
            if (!string.IsNullOrEmpty(e.TimeoutAt))
                row.TimeoutAt = e.TimeoutAt;
            if (!string.IsNullOrEmpty(e.AssignedWorkerId))
                row.AssignedWorkerId = e.AssignedWorkerId;

            if (e.Context != null)
            {
                var c = new ExecutionHistoryContext
                {
                    Environment = e.Context.Environment ?? "",
                    TriggerSource = e.Context.TriggerSource ?? "",
                    UserId = e.Context.UserId ?? ""
                };
                if (e.Context.Parameters is { Count: > 0 })
                {
                    foreach (var kv in e.Context.Parameters)
                        c.Parameters[kv.Key] = kv.Value;
                }

                row.Context = c;
            }

            if (e.Result != null)
            {
                var res = new ExecutionHistoryResult { Status = e.Result.Status ?? "" };
                if (!string.IsNullOrEmpty(e.Result.ErrorCode))
                    res.ErrorCode = e.Result.ErrorCode;
                if (e.Result.StatusCode.HasValue)
                    res.StatusCode = e.Result.StatusCode.Value;
                if (e.Result.ResponseSizeBytes.HasValue)
                    res.ResponseSizeBytes = e.Result.ResponseSizeBytes.Value;
                if (!string.IsNullOrEmpty(e.Result.ErrorMessage))
                    res.ErrorMessage = e.Result.ErrorMessage;
                if (!string.IsNullOrEmpty(e.Result.ErrorStack))
                    res.ErrorStack = e.Result.ErrorStack;
                row.Result = res;
            }

            resp.Events.Add(row);
        }

        return resp;
    }

    public static byte[] SerializeProto(GetExecutionHistoryPageResponse message) => message.ToByteArray();

    public static GetExecutionHistoryPageResponse DeserializeProto(byte[] data) =>
        GetExecutionHistoryPageResponse.Parser.ParseFrom(data);
}
