using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JobScheduler.DAL.Postgres.Tests;

/// <summary>
/// Captures formatted log lines for an explicit set of logger category names (full type names).
/// Used to assert routing / consistency debug output from <c>PostgresConnectionFactory</c> etc.
/// </summary>
public sealed class MultiCategoryLogCaptureProvider : ILoggerProvider
{
    private readonly HashSet<string> _categories;
    private readonly ConcurrentQueue<string> _entries = new();

    public MultiCategoryLogCaptureProvider(IEnumerable<string> categoryFullNames)
    {
        _categories = new HashSet<string>(categoryFullNames, StringComparer.Ordinal);
    }

    public void Clear() => _entries.Clear();

    public IReadOnlyList<string> Snapshot() => _entries.ToArray();

    public ILogger CreateLogger(string categoryName) =>
        _categories.Contains(categoryName)
            ? new CaptureLogger(_entries)
            : NullLogger.Instance;

    public void Dispose()
    {
    }

    private sealed class CaptureLogger : ILogger
    {
        private readonly ConcurrentQueue<string> _entries;

        public CaptureLogger(ConcurrentQueue<string> entries) => _entries = entries;

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
