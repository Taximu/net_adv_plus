using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using SerializationComparison;

// Run benchmarks
if (args.Contains("--benchmark"))
{
    BenchmarkRunner.Run<SerializationBenchmark>();
    return;
}

// Quick comparison mode (no BenchmarkDotNet overhead, useful for Docker)
Console.WriteLine("=== Serialization Format Comparison ===");
Console.WriteLine();

var recordCounts = new[] { 100, 500, 1000 };

Console.WriteLine("| Format    | Records | Payload (bytes) | Serialize (ms) | Deserialize (ms) | Size Ratio |");
Console.WriteLine("|-----------|---------|-----------------|----------------|------------------|------------|");

foreach (var count in recordCounts)
{
    var data = TestDataGenerator.Generate(count);

    // JSON measurement
    var (jsonBytes, jsonSerMs, jsonDeserMs) = MeasureFormat(
        () => JsonSerializer.Serialize(data),
        bytes => JsonSerializer.Deserialize(bytes));

    // Protobuf measurement
    var (pbBytes, pbSerMs, pbDeserMs) = MeasureFormat(
        () => ProtobufSerializer.Serialize(data),
        bytes => ProtobufSerializer.Deserialize(bytes));

    var sizeRatio = pbBytes > 0 ? (double)jsonBytes / pbBytes : 0;

    PrintRow("JSON", count, jsonBytes, jsonSerMs, jsonDeserMs, "1.0x (baseline)");
    PrintRow("Protobuf", count, pbBytes, pbSerMs, pbDeserMs, $"{sizeRatio:F1}x larger JSON");
    Console.WriteLine();
}

Console.WriteLine("=== JobScheduler UC 2.3 implementation (compact JSON + grpc messages) ===");
Console.WriteLine("(Same synthetic records; JSON = ExecutionHistoryPageJson; Protobuf = GetExecutionHistoryPageResponse)");
Console.WriteLine();
Console.WriteLine("| Format    | Records | Payload (bytes) | Serialize (ms) | Deserialize (ms) | Size Ratio |");
Console.WriteLine("|-----------|---------|-----------------|----------------|------------------|------------|");

foreach (var count in recordCounts)
{
    var data = TestDataGenerator.Generate(count);
    var page = JobSchedulerHistorySerialization.ToPage(data);

    var (jsJsonBytes, jsJsonSer, jsJsonDeser) = MeasureFormat(
        () => JobSchedulerHistorySerialization.SerializeJson(page),
        bytes => JobSchedulerHistorySerialization.DeserializeJson(bytes)!);

    var protoMessage = JobSchedulerHistorySerialization.ToProtoResponse(page);
    var (jsPbBytes, jsPbSer, jsPbDeser) = MeasureFormat(
        () => JobSchedulerHistorySerialization.SerializeProto(protoMessage),
        bytes => JobSchedulerHistorySerialization.DeserializeProto(bytes));

    var implRatio = jsPbBytes > 0 ? (double)jsJsonBytes / jsPbBytes : 0;
    PrintRow("JS JSON", count, jsJsonBytes, jsJsonSer, jsJsonDeser, "1.0x (baseline)");
    PrintRow("JS Proto", count, jsPbBytes, jsPbSer, jsPbDeser, $"{implRatio:F1}x larger JSON");
    Console.WriteLine();
}

Console.WriteLine();
Console.WriteLine("=== Analysis ===");
Console.WriteLine("Baseline (1st table): protobuf-net vs camelCase JSON on the small JobHistoryRecord list.");
Console.WriteLine("JobScheduler (2nd table): Google.Protobuf vs compact JSON on UC 2.3 wire models (same row counts).");
Console.WriteLine("Copy numbers into 07-serialization-comparison/docs/comparison-report.md for your submission.");
Console.WriteLine();
Console.WriteLine("To run full BenchmarkDotNet analysis: dotnet run --configuration Release -- --benchmark");

static (int byteCount, double serMs, double deserMs) MeasureFormat<T>(
    Func<byte[]> serialize,
    Func<byte[], T> deserialize)
{
    // Warmup
    for (int i = 0; i < 3; i++) { serialize(); }
    var bytes = serialize();
    for (int i = 0; i < 3; i++) { deserialize(bytes); }

    // Measure
    const int iterations = 20;
    var sw = System.Diagnostics.Stopwatch.StartNew();
    for (int i = 0; i < iterations; i++) serialize();
    sw.Stop();
    var serMs = sw.Elapsed.TotalMilliseconds / iterations;

    sw.Restart();
    for (int i = 0; i < iterations; i++) deserialize(bytes);
    sw.Stop();
    var deserMs = sw.Elapsed.TotalMilliseconds / iterations;

    return (bytes.Length, serMs, deserMs);
}

static void PrintRow(string format, int count, int bytes, double serMs, double deserMs, string ratio)
{
    Console.WriteLine($"| {format,-9} | {count,7} | {bytes,15:N0} | {serMs,14:F2} | {deserMs,16:F2} | {ratio,-10} |");
}

[MemoryDiagnoser]
[SimpleJob]
public class SerializationBenchmark
{
    private List<JobHistoryRecord> _data100 = null!;
    private List<JobHistoryRecord> _data500 = null!;
    private List<JobHistoryRecord> _data1000 = null!;
    private byte[] _json100 = null!;
    private byte[] _pb100 = null!;
    private byte[] _json1000 = null!;
    private byte[] _pb1000 = null!;

    [GlobalSetup]
    public void Setup()
    {
        _data100 = TestDataGenerator.Generate(100);
        _data500 = TestDataGenerator.Generate(500);
        _data1000 = TestDataGenerator.Generate(1000);
        _json100 = JsonSerializer.Serialize(_data100);
        _pb100 = ProtobufSerializer.Serialize(_data100);
        _json1000 = JsonSerializer.Serialize(_data1000);
        _pb1000 = ProtobufSerializer.Serialize(_data1000);
    }

    [Benchmark] public byte[] Json_Serialize_100() => JsonSerializer.Serialize(_data100);
    [Benchmark] public byte[] Json_Serialize_1000() => JsonSerializer.Serialize(_data1000);
    [Benchmark] public byte[] Protobuf_Serialize_100() => ProtobufSerializer.Serialize(_data100);
    [Benchmark] public byte[] Protobuf_Serialize_1000() => ProtobufSerializer.Serialize(_data1000);
    [Benchmark] public List<JobHistoryRecord>? Json_Deserialize_100() => JsonSerializer.Deserialize(_json100);
    [Benchmark] public List<JobHistoryRecord>? Json_Deserialize_1000() => JsonSerializer.Deserialize(_json1000);
    [Benchmark] public List<JobHistoryRecord> Protobuf_Deserialize_100() => ProtobufSerializer.Deserialize(_pb100);
    [Benchmark] public List<JobHistoryRecord> Protobuf_Deserialize_1000() => ProtobufSerializer.Deserialize(_pb1000);
}
