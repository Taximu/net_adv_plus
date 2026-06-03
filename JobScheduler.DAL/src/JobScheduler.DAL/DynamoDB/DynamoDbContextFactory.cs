using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using JobScheduler.DAL.Configuration;
using Microsoft.Extensions.Options;

namespace JobScheduler.DAL.DynamoDB;

public sealed class DynamoDbContextFactory : IDynamoDbContextFactory
{
    private readonly IAmazonDynamoDB _client;
    private bool _disposed;

    public DynamoDbContextFactory(IOptions<DynamoDbOptions> options)
    {
        var o = options.Value;
        if (!string.IsNullOrWhiteSpace(o.ServiceUrl))
        {
            var creds = new BasicAWSCredentials(o.AccessKeyId, o.SecretAccessKey);
            var cfg = new AmazonDynamoDBConfig { ServiceURL = o.ServiceUrl.TrimEnd('/') };
            ApplyTimeout(cfg, o);
            _client = new AmazonDynamoDBClient(creds, cfg);
        }
        else
        {
            var region = string.IsNullOrWhiteSpace(o.Region) ? "us-east-1" : o.Region;
            var cfg = new AmazonDynamoDBConfig
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(region)
            };
            ApplyTimeout(cfg, o);
            _client = new AmazonDynamoDBClient(cfg);
        }
    }

    private static void ApplyTimeout(AmazonDynamoDBConfig cfg, DynamoDbOptions o)
    {
        if (o.ClientTimeoutSeconds is > 0 and var sec)
        {
            cfg.Timeout = TimeSpan.FromSeconds(sec);
            cfg.MaxErrorRetry = 0;
        }
    }

    public IAmazonDynamoDB CreateClient() => _client;

    public void Dispose()
    {
        if (_disposed) return;
        _client.Dispose();
        _disposed = true;
    }
}
