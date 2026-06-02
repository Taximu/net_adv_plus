using Xunit;

namespace PostgreSql.Replication.Tests;

/// <summary>
/// Runs replication tests sequentially so DDL on the primary is visible before replica-only assertions.
/// </summary>
[CollectionDefinition("PostgresReplication", DisableParallelization = true)]
public class PostgresReplicationCollection : ICollectionFixture<PostgresReplicationInfrastructureFixture>
{
}
