namespace JobScheduler.DAL.Consistency;

/// <summary>
/// Declares how a read should be routed relative to PostgreSQL primary vs replicas.
/// </summary>
public enum ConsistencyLevel
{
    /// <summary>Read from the primary (leader) — latest committed state.</summary>
    Strong,

    /// <summary>Read from a round-robin replica — may lag behind the primary.</summary>
    Eventual
}
