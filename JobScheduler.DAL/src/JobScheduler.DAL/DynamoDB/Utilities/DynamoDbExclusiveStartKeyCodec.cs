using System.Text.Json;
using Amazon.DynamoDBv2.Model;

namespace JobScheduler.DAL.DynamoDB.Utilities;

/// <summary>
/// URL-safe opaque pagination tokens for DynamoDB <c>ExclusiveStartKey</c> (UC 2.3 history API).
/// </summary>
public static class DynamoDbExclusiveStartKeyCodec
{
    public static string Encode(Dictionary<string, AttributeValue> key)
    {
        if (key is not { Count: > 0 })
            throw new ArgumentException("Key must be non-empty.", nameof(key));

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var kv in key.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                writer.WritePropertyName(kv.Key);
                WriteAttributeValue(writer, kv.Value);
            }

            writer.WriteEndObject();
        }

        return ToBase64Url(stream.ToArray());
    }

    public static Dictionary<string, AttributeValue>? Decode(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var bytes = FromBase64Url(token);
        using var doc = JsonDocument.Parse(bytes);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new FormatException("Invalid pagination token.");

        var map = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);
        foreach (var prop in root.EnumerateObject())
            map[prop.Name] = ReadAttributeValue(prop.Value);

        return map;
    }

    private static void WriteAttributeValue(Utf8JsonWriter writer, AttributeValue v)
    {
        writer.WriteStartObject();
        if (v.S is not null)
        {
            writer.WriteString("S", v.S);
        }
        else if (v.N is not null)
        {
            writer.WriteString("N", v.N);
        }
        else if (v.B is not null)
        {
            writer.WriteBase64String("B", v.B.ToArray());
        }
        else if (v.IsBOOLSet)
        {
            writer.WriteBoolean("BOOL", v.BOOL);
        }
        else if (v.NULL)
        {
            writer.WriteBoolean("NULL", true);
        }
        else if (v.M is { Count: > 0 })
        {
            writer.WritePropertyName("M");
            writer.WriteStartObject();
            foreach (var kv in v.M.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                writer.WritePropertyName(kv.Key);
                WriteAttributeValue(writer, kv.Value);
            }

            writer.WriteEndObject();
        }
        else if (v.L is { Count: > 0 })
        {
            writer.WritePropertyName("L");
            writer.WriteStartArray();
            foreach (var item in v.L)
                WriteAttributeValue(writer, item);

            writer.WriteEndArray();
        }
        else if (v.SS is { Count: > 0 })
        {
            writer.WritePropertyName("SS");
            writer.WriteStartArray();
            foreach (var s in v.SS.Order(StringComparer.Ordinal))
                writer.WriteStringValue(s);
            writer.WriteEndArray();
        }
        else if (v.NS is { Count: > 0 })
        {
            writer.WritePropertyName("NS");
            writer.WriteStartArray();
            foreach (var n in v.NS.Order(StringComparer.Ordinal))
                writer.WriteStringValue(n);
            writer.WriteEndArray();
        }
        else
        {
            writer.WriteBoolean("NULL", true);
        }

        writer.WriteEndObject();
    }

    private static AttributeValue ReadAttributeValue(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object)
            throw new FormatException("Expected DynamoDB AttributeValue object.");

        foreach (var prop in el.EnumerateObject())
        {
            return prop.Name switch
            {
                "S" => new AttributeValue { S = prop.Value.GetString() },
                "N" => new AttributeValue { N = prop.Value.GetString() },
                "BOOL" => new AttributeValue { BOOL = prop.Value.GetBoolean() },
                "NULL" => new AttributeValue { NULL = prop.Value.GetBoolean() },
                "B" => new AttributeValue { B = new MemoryStream(prop.Value.GetBytesFromBase64()) },
                "M" => new AttributeValue { M = ReadMap(prop.Value) },
                "L" => new AttributeValue { L = ReadList(prop.Value) },
                "SS" => new AttributeValue { SS = prop.Value.EnumerateArray().Select(e => e.GetString()!).ToList() },
                "NS" => new AttributeValue { NS = prop.Value.EnumerateArray().Select(e => e.GetString()!).ToList() },
                _ => throw new FormatException($"Unsupported DynamoDB type: {prop.Name}")
            };
        }

        return new AttributeValue { NULL = true };
    }

    private static Dictionary<string, AttributeValue> ReadMap(JsonElement el)
    {
        var m = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);
        foreach (var p in el.EnumerateObject())
            m[p.Name] = ReadAttributeValue(p.Value);

        return m;
    }

    private static List<AttributeValue> ReadList(JsonElement el)
    {
        var list = new List<AttributeValue>();
        foreach (var item in el.EnumerateArray())
            list.Add(ReadAttributeValue(item));

        return list;
    }

    private static string ToBase64Url(ReadOnlySpan<byte> data)
    {
        var b64 = Convert.ToBase64String(data);
        return b64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static byte[] FromBase64Url(string token)
    {
        var s = token.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2:
                s += "==";
                break;
            case 3:
                s += "=";
                break;
        }

        return Convert.FromBase64String(s);
    }
}
