using System.Collections.Concurrent;
using JobScheduler.DAL.Repositories;
using Microsoft.Extensions.Logging;

namespace JobScheduler.DAL.Postgres.Tests;

/// <summary>Captures formatted <see cref="LogLevel.Debug"/> (and above) lines for <see cref="JobScheduleRepository"/> category only.</summary>
public sealed class ListCapturingLoggerProvider : ILoggerProvider
{
    private static readonly string JobScheduleRepositoryCategory =
        typeof(JobScheduleRepository).FullName!;

    private readonly ConcurrentQueue<string> _entries = new();

    public void Clear() => _entries.Clear();

    public IReadOnlyList<string> Snapshot() => _entries.ToArray();

    public ILogger CreateLogger(string categoryName) =>
        string.Equals(categoryName, JobScheduleRepositoryCategory, StringComparison.Ordinal)
            ? new ListCapturingLogger(_entries)
            : Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

    public void Dispose()
    {
    }

    private sealed class ListCapturingLogger : ILogger
    {
        private readonly ConcurrentQueue<string> _entries;

        public ListCapturingLogger(ConcurrentQueue<string> entries) => _entries = entries;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var msg = formatter(state, exception);
            if (!string.IsNullOrEmpty(msg))
                _entries.Enqueue(msg);
        }
    }
}
