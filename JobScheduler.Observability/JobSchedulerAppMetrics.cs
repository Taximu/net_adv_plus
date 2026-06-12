using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;

namespace JobScheduler.Observability;

/// <summary>
/// Low-cardinality application metrics (bounded <c>task_family</c> / <c>retry_bucket</c>) for Prometheus/Grafana.
/// Record methods are best-effort and never throw to the caller.
/// </summary>
public sealed class JobSchedulerAppMetrics
{
    public const string MeterName = "JobScheduler.App";

    /// <summary>Elapsed wall time after which an in-flight task is counted as long-running (gauge).</summary>
    public static readonly TimeSpan LongRunningThreshold = TimeSpan.FromSeconds(30);

    private readonly Meter _meter = new(MeterName, "1.0.0");

    private readonly ConcurrentDictionary<long, (string TaskFamily, DateTimeOffset StartedUtc)> _active = new();

    private long _nextWorkId;

    public JobSchedulerAppMetrics()
    {
        TaskExecutionsTotal = _meter.CreateCounter<long>(
            "jobscheduler_task_executions_total",
            description: "Background task units completed (Kafka lifecycle handling, peek batches).");

        TaskExecutionDurationSeconds = _meter.CreateHistogram<double>(
            "jobscheduler_task_execution_duration_seconds",
            unit: "s",
            description: "Duration of a single background task unit (e.g. one lifecycle message or one peek cycle).");

        RetryBacklogObservationsTotal = _meter.CreateCounter<long>(
            "jobscheduler_retry_backlog_observations_total",
            description: "Count of pending queue rows observed per peek, tagged by retry count bucket (not per execution id).");

        FailedTasksTotal = _meter.CreateCounter<long>(
            "jobscheduler_failed_tasks_total",
            description: "Cumulative background tasks that ended without success (error, HTTP error, consume error).");

        TasksProcessedTotal = _meter.CreateCounter<long>(
            "jobscheduler_tasks_processed_total",
            description: "Cumulative background tasks finished (success or failure); use increase(...[1h]) for throughput per hour.");

        _meter.CreateObservableGauge(
            "jobscheduler_long_running_tasks_current",
            ObserveLongRunningCountsSafe,
            unit: "1",
            description: "In-flight background tasks whose elapsed time exceeds the long-running threshold (30s).");

        _meter.CreateObservableGauge(
            "jobscheduler_dlq_visible_messages",
            static () => new Measurement<long>(0),
            unit: "1",
            description: "Placeholder for broker DLQ depth. This codebase uses DynamoDB queue rows only — no DLQ; value is always 0.");
    }

    public Counter<long> TaskExecutionsTotal { get; }

    public Histogram<double> TaskExecutionDurationSeconds { get; }

    public Counter<long> RetryBacklogObservationsTotal { get; }

    public Counter<long> FailedTasksTotal { get; }

    public Counter<long> TasksProcessedTotal { get; }

    /// <summary>Begins tracking a background work unit for long-running gauge and completion metrics.</summary>
    public TrackedBackgroundWork BeginTrackedWork(string taskFamily)
    {
        var id = Interlocked.Increment(ref _nextWorkId);
        _active[id] = (taskFamily, DateTimeOffset.UtcNow);
        return new TrackedBackgroundWork(this, id, taskFamily, Stopwatch.StartNew());
    }

    private IEnumerable<Measurement<int>> ObserveLongRunningCountsSafe()
    {
        try
        {
            return ObserveLongRunningCounts().ToArray();
        }
        catch (Exception)
        {
            return Array.Empty<Measurement<int>>();
        }
    }

    private IEnumerable<Measurement<int>> ObserveLongRunningCounts()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var family in TrackedBackgroundWork.KnownTaskFamilies)
        {
            var count = 0;
            foreach (var kv in _active)
            {
                var (tf, started) = kv.Value;
                if (tf != family)
                    continue;
                if (now - started > LongRunningThreshold)
                    count++;
            }

            yield return new Measurement<int>(count, new KeyValuePair<string, object?>("task_family", family));
        }
    }

    internal void CompleteTrackedWork(long id, string taskFamily, string outcome, TimeSpan duration)
    {
        try
        {
            _active.TryRemove(id, out _);

            var success = string.Equals(outcome, "success", StringComparison.OrdinalIgnoreCase);

            TaskExecutionsTotal.Add(
                1,
                new KeyValuePair<string, object?>("task_family", taskFamily),
                new KeyValuePair<string, object?>("outcome", outcome));

            TasksProcessedTotal.Add(
                1,
                new KeyValuePair<string, object?>("task_family", taskFamily),
                new KeyValuePair<string, object?>("outcome", success ? "success" : "failure"));

            if (!success)
            {
                FailedTasksTotal.Add(1, new KeyValuePair<string, object?>("task_family", taskFamily));
            }

            if (duration > TimeSpan.Zero)
            {
                TaskExecutionDurationSeconds.Record(
                    duration.TotalSeconds,
                    new KeyValuePair<string, object?>("task_family", taskFamily));
            }
        }
        catch (Exception)
        {
            // Metrics must never affect execution.
        }
    }

    /// <summary>Legacy path when work is not wrapped in <see cref="TrackedBackgroundWork"/>.</summary>
    public void RecordBackgroundTask(string taskFamily, string outcome, TimeSpan duration)
    {
        try
        {
            TaskExecutionsTotal.Add(
                1,
                new KeyValuePair<string, object?>("task_family", taskFamily),
                new KeyValuePair<string, object?>("outcome", outcome));

            var success = string.Equals(outcome, "success", StringComparison.OrdinalIgnoreCase);
            TasksProcessedTotal.Add(
                1,
                new KeyValuePair<string, object?>("task_family", taskFamily),
                new KeyValuePair<string, object?>("outcome", success ? "success" : "failure"));

            if (!success)
                FailedTasksTotal.Add(1, new KeyValuePair<string, object?>("task_family", taskFamily));

            if (duration > TimeSpan.Zero)
            {
                TaskExecutionDurationSeconds.Record(
                    duration.TotalSeconds,
                    new KeyValuePair<string, object?>("task_family", taskFamily));
            }
        }
        catch (Exception)
        {
            // Metrics must never affect execution.
        }
    }

    public void RecordPeekRetryHistogram(IReadOnlyList<RetryBucketCount>? buckets)
    {
        if (buckets is null || buckets.Count == 0)
            return;

        try
        {
            foreach (var b in buckets)
            {
                if (b.Count <= 0)
                    continue;
                RetryBacklogObservationsTotal.Add(
                    b.Count,
                    new KeyValuePair<string, object?>("retry_bucket", b.BucketLabel));
            }
        }
        catch (Exception)
        {
            // Metrics must never affect execution.
        }
    }

    public readonly record struct RetryBucketCount(string BucketLabel, int Count);
}
