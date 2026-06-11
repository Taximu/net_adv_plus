using Amazon.DynamoDBv2.Model;
using JobScheduler.DAL.DynamoDB.Utilities;

namespace JobScheduler.DAL.DynamoDb.Tests;

public class DynamoDbExclusiveStartKeyCodecTests
{
    [Fact]
    public void Encode_decode_roundtrip_for_string_keys()
    {
        var key = new Dictionary<string, AttributeValue>(StringComparer.Ordinal)
        {
            ["jobId"] = new AttributeValue { S = "550e8400-e29b-41d4-a716-446655440000" },
            ["scheduledFor"] = new AttributeValue { S = "2026-06-02T10:00:00Z" },
            ["queueId"] = new AttributeValue { S = "queue-abc" }
        };

        var token = DynamoDbExclusiveStartKeyCodec.Encode(key);
        var decoded = DynamoDbExclusiveStartKeyCodec.Decode(token);
        Assert.NotNull(decoded);
        Assert.Equal(key["jobId"].S, decoded!["jobId"].S);
        Assert.Equal(key["scheduledFor"].S, decoded["scheduledFor"].S);
        Assert.Equal(key["queueId"].S, decoded["queueId"].S);
    }

    [Fact]
    public void Decode_null_returns_null()
    {
        Assert.Null(DynamoDbExclusiveStartKeyCodec.Decode(null));
        Assert.Null(DynamoDbExclusiveStartKeyCodec.Decode("   "));
    }
}
