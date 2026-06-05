# Integration test execution logs (consistency routing)

This file is the **“attach execution logs”** artifact for the coursework: it describes **automated** integration tests that **capture the same debug log lines** you would see when running the API against PostgreSQL replicas and DynamoDB Local.

## How to run (and see logs in test output)

From the repo root:

```powershell
dotnet test .\JobScheduler.DAL\tests\JobScheduler.DAL.Postgres.Tests\JobScheduler.DAL.Postgres.Tests.csproj --filter "FullyQualifiedName~PostgresConsistencyRoutingTests" --logger "console;verbosity=detailed"
```

```powershell
dotnet test .\JobScheduler.DAL\tests\JobScheduler.DAL.DynamoDb.Tests\JobScheduler.DAL.DynamoDb.Tests.csproj --filter "FullyQualifiedName~DynamoDbConsistencyRoutingTests" --logger "console;verbosity=detailed"
```

Each test uses **xUnit `ITestOutputHelper`** to print captured lines under a `--- test name ---` banner (shown in **detailed** console / TRX output).

## What is asserted (PostgreSQL)

| Test | Proves |
|------|--------|
| `GetActiveJobsAsync_Strong_logs_primary_read_not_replica_round_robin` | **Strong** → `PostgresPrimaryReadOpened`, **Primary**, no `PostgresReadOpened` |
| `GetByUserIdAsync_Eventual_logs_replica_and_alternates_replica_index` | **Eventual** → `PostgresReadOpened`, **Replica**, **ReplicaIndex** alternates **0** and **1** (two logical read endpoints in config; same DB in tests) |
| `GetByIdAsync_Strong_logs_primary_read` | **Strong** on `GetById` → `PostgresPrimaryReadOpened`, **ConsistencyLevel=Strong** |
| `GetWriteConnectionAsync_logs_write_opened_on_primary` | Opens write connection → `PostgresWriteOpened`, **Operation=Write** |

### Example captured lines (shape)

```text
PostgresPrimaryReadOpened: ConsistencyLevel=Strong Role=Primary Host=127.0.0.1 Port=328xx ReplicaIndex= Operation=Read
PostgresReadOpened: ConsistencyLevel=Eventual Role=Replica Host=127.0.0.1 Port=328xx ReplicaIndex=0 Operation=Read
PostgresReadOpened: ConsistencyLevel=Eventual Role=Replica Host=127.0.0.1 Port=328xx ReplicaIndex=1 Operation=Read
PostgresWriteOpened: ConsistencyLevel=Strong Role=Primary Host=127.0.0.1 Port=328xx ReplicaIndex= Operation=Write
```

(`ReplicaIndex=` may render empty for `null` on primary reads depending on the logging formatter.)

## What is asserted (DynamoDB UC 2.1)

| Test | Proves |
|------|--------|
| `GetItem_default_Strong_logs_consistent_read_true` | Default **GetItem** path logs **Strong** + **ConsistentRead=True** |
| `GetItem_Eventual_logs_consistent_read_false` | **Eventual** maps to **ConsistentRead=False** |
| `Query_pending_Eventual_logs_gsi_poll_without_consistent_read` | GSI **Query** logs **PollPending** / **Eventual** and never claims **ConsistentRead=True** for that path |

### Example captured lines (shape)

```text
DynamoDB GetItem Table=ExecutionQueue Operation=Read QueueId=... ConsistencyLevel=Strong ConsistentRead=True
DynamoDB GetItem Table=ExecutionQueue Operation=Read QueueId=... ConsistencyLevel=Eventual ConsistentRead=False
DynamoDB Query Table=ExecutionQueue Index=PendingExecutionsIndex Operation=PollPending ConsistencyLevel=Eventual
```

## Source locations

- **Capture helper:** `JobScheduler.DAL.Postgres.Tests/MultiCategoryLogCaptureProvider.cs` (and DynamoDb.Tests copy).
- **PostgreSQL fixture/tests:** `PostgresConsistencyRoutingFixture.cs`, `PostgresConsistencyRoutingTests.cs`, collection `PostgresConsistencyDalCollection.cs`.
- **DynamoDB:** `DynamoDbDalFixture.cs` (`RoutingLogCapture`), `DynamoDbConsistencyRoutingTests.cs`.

See also **[`consistency-demo-logs.md`](consistency-demo-logs.md)** for manual API runs.
