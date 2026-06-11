using ProtoBuf;
using System.Runtime.Serialization;

namespace SerializationComparison;

/// <summary>
/// Protobuf serialization implementation for job execution history.
/// Uses protobuf-net for contract-first schema definition.
/// </summary>
public static class ProtobufSerializer
{
    /// <summary>
    /// Serializes a list of job history records to Protobuf bytes.
    /// </summary>
    public static byte[] Serialize(List<JobHistoryRecord> records)
    {
        var proto = new JobHistoryListProto
        {
            Records = records.Select(r => new JobHistoryRecordProto
            {
                EventId = r.EventId,
                JobName = r.JobName ?? string.Empty,
                Status = r.Status ?? string.Empty,
                StartedAtMs = r.StartedAt.ToUnixTimeMilliseconds(),
                DurationMs = r.DurationMs,
                ErrorMessage = r.ErrorMessage ?? string.Empty
            }).ToList()
        };

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, proto);
        return stream.ToArray();
    }

    /// <summary>
    /// Deserializes Protobuf bytes back to job history records.
    /// </summary>
    public static List<JobHistoryRecord> Deserialize(byte[] data)
    {
        using var stream = new MemoryStream(data);
        var proto = Serializer.Deserialize<JobHistoryListProto>(stream);
        return proto.Records.Select(r => new JobHistoryRecord(
            r.EventId,
            r.JobName,
            DateTimeOffset.FromUnixTimeMilliseconds(r.StartedAtMs),
            r.Status,
            r.DurationMs,
            r.ErrorMessage
        )).ToList();
    }
}

[ProtoContract]
public class JobHistoryListProto
{
    [ProtoMember(1)]
    public List<JobHistoryRecordProto> Records { get; set; } = new();
}

[ProtoContract]
public class JobHistoryRecordProto
{
    [ProtoMember(1)] public int EventId { get; set; }
    [ProtoMember(2)] public string JobName { get; set; } = string.Empty;
    [ProtoMember(3)] public string Status { get; set; } = string.Empty;
    [ProtoMember(4)] public long StartedAtMs { get; set; }
    [ProtoMember(5)] public int DurationMs { get; set; }
    [ProtoMember(6)] public string ErrorMessage { get; set; } = string.Empty;
}
