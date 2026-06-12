using System.Diagnostics;

namespace JobScheduler.Observability;

/// <summary>
/// Disposable scope for one background task (bounded <c>task_family</c>). Top-level type so IDE/analyzers resolve API reliably.
/// </summary>
public sealed class TrackedBackgroundWork : IDisposable
{
    internal static readonly string[] KnownTaskFamilies = ["lifecycle_consume", "pending_peek_batch"];

    private readonly JobSchedulerAppMetrics _owner;
    private readonly long _id;
    private readonly string _taskFamily;
    private readonly Stopwatch _sw;
    private bool _completed;

    internal TrackedBackgroundWork(JobSchedulerAppMetrics owner, long id, string taskFamily, Stopwatch sw)
    {
        _owner = owner;
        _id = id;
        _taskFamily = taskFamily;
        _sw = sw;
    }

    /// <param name="outcome">Use <c>success</c>, <c>http_error</c>, <c>error</c>, or <c>consume_error</c>.</param>
    public void Complete(string outcome)
    {
        if (_completed)
            return;
        _completed = true;
        _owner.CompleteTrackedWork(_id, _taskFamily, outcome, _sw.Elapsed);
    }

    public void Dispose()
    {
        if (_completed)
            return;
        Complete("error");
    }
}
