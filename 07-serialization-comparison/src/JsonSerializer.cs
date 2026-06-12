using System.Text.Json;
using System.Text.Json.Serialization;

namespace SerializationComparison;

/// <summary>
/// JSON serialization implementation for job execution history.
/// Baseline format for comparison.
/// </summary>
public static class JsonSerializer
{
    private static readonly System.Text.Json.JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false
    };

    /// <summary>
    /// Serializes a list of job history records to JSON bytes.
    /// </summary>
    public static byte[] Serialize(List<JobHistoryRecord> records) =>
        System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(records, Options);

    /// <summary>
    /// Deserializes JSON bytes back to job history records.
    /// </summary>
    public static List<JobHistoryRecord>? Deserialize(byte[] data) =>
        System.Text.Json.JsonSerializer.Deserialize<List<JobHistoryRecord>>(data, Options);
}
