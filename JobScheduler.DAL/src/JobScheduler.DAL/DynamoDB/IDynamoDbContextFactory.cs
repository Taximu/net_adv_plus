using Amazon.DynamoDBv2;

namespace JobScheduler.DAL.DynamoDB;

/// <summary>
/// Provides the shared <see cref="IAmazonDynamoDB"/> client (mirrors <see cref="Connection.IDbConnectionFactory"/> for SQL).
/// </summary>
public interface IDynamoDbContextFactory : IDisposable
{
    IAmazonDynamoDB CreateClient();
}
