namespace SerializationComparison;

/// <summary>
/// Domain model: a single job execution history record.
/// Represents data returned by GET /api/jobs/history.
/// </summary>
public record JobHistoryRecord(
    int EventId,
    string JobName,
    DateTimeOffset StartedAt,
    string Status,
    int DurationMs,
    string? ErrorMessage
);

/// <summary>
/// Generates realistic test data for serialization benchmarks.
/// </summary>
public static class TestDataGenerator
{
    private static readonly string[] Statuses = ["Completed", "Failed", "Cancelled", "Running"];
    private static readonly string[] JobNames = ["DataSync", "ReportGen", "EmailSend", "Cleanup", "IndexRebuild"];

    public static List<JobHistoryRecord> Generate(int count)
    {
        var rng = new Random(42); // Fixed seed for reproducibility
        var base_time = DateTimeOffset.UtcNow.AddHours(-count);

        return Enumerable.Range(1, count).Select(i => new JobHistoryRecord(
            EventId: i,
            JobName: $"{JobNames[i % JobNames.Length]}-{i:D5}",
            StartedAt: base_time.AddMinutes(i),
            Status: Statuses[i % Statuses.Length],
            DurationMs: rng.Next(50, 5000),
            ErrorMessage: i % 10 == 0 ? $"Transient error #{i}" : null
        )).ToList();
    }
}
