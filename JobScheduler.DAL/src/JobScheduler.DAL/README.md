# JobScheduler.DAL

## Data Access Layer with Read/Write Splitting

### Architecture
- **Write operations** -> Primary database (Port 5432)
- **Read operations** are routed by **`ConsistencyLevel`** (see **[`../../docs/consistency-requirements.md`](../../docs/consistency-requirements.md)**):
  - **`Strong`** → primary (latest committed rows).
  - **`Eventual`** → read replicas (Port 5434, 5435) with round-robin when configured; otherwise primary.
- **`PostgresConnectionFactory`** logs **Debug** lines with **Host**, **Port**, **ReplicaIndex**, and **ConsistencyLevel** for each opened connection (see **[`../../docs/consistency-demo-logs.md`](../../docs/consistency-demo-logs.md)**). Set minimum level **Debug** for `JobScheduler.DAL.Connection` in the host to capture them.
- **`ConsistencyManager`** (in-memory cooldown after writes) supports BL **read-after-write** routing without exposing levels to HTTP APIs.
- **`job_schedules`** is **HASH-partitioned** by `schedule_id` (4 partitions) on the primary; replicas replay the same layout. See **[`../../docs/partitioning-strategy.md`](../../docs/partitioning-strategy.md)**.
- **`JobScheduleRepository`** binds **`@SchedulePartitionKey`** on CRUD where the partition key applies, resolves **`tableoid::regclass`** for **Debug** logs (physical child table per operation), and logs a **partition histogram** for **`GetByJobIdAsync`**. `AddDataAccessLayer` registers **`AddLogging()`**, **`AddMemoryCache()`**, and **`ConsistencyManager`** so `ILogger<>` and caching resolve in minimal hosts.

### Connection Strings
| Role | Port | Connection String |
|------|------|-------------------|
| Primary (Write) | 5432 | Host=localhost;Port=5432;Database=job_config_db;Username=job_admin;Password=StrongP@ssw0rd |
| Replica 1 (Read) | 5434 | Host=localhost;Port=5434;Database=job_config_db;Username=job_admin;Password=StrongP@ssw0rd |
| Replica 2 (Read) | 5435 | Host=localhost;Port=5435;Database=job_config_db;Username=job_admin;Password=StrongP@ssw0rd |

### Usage Example

```csharp
using JobScheduler.DAL.Consistency;

var jobRepo = serviceProvider.GetRequiredService<IJobDefinitionRepository>();
var scheduleRepo = serviceProvider.GetRequiredService<IJobScheduleRepository>();

// Write -> Primary
var newJob = await jobRepo.CreateAsync(job);
var newSchedule = await scheduleRepo.CreateAsync(new JobSchedule { JobId = newJob.JobId, ScheduleName = "Nightly", ... });

// Reads -> choose consistency per use case (BL normally decides this)
var jobs = await jobRepo.GetByUserIdAsync(userId, ConsistencyLevel.Eventual);
var schedules = await scheduleRepo.GetByJobIdAsync(newJob.JobId, ConsistencyLevel.Eventual);
var authoritative = await jobRepo.GetActiveJobsAsync(ConsistencyLevel.Strong);
```

### UC 2.1 — DynamoDB (execution queue)

- **Configuration** section `DynamoDB` in `appsettings.json` (see `Configuration/DynamoDbOptions.cs`).
- **Reads:** `GetItem` honors **`ConsistencyLevel`** via **`ConsistentRead`** (**Strong** → `true`, **Eventual** → `false`). **GSI `Query`** operations accept the enum for symmetry; DynamoDB does not support strongly consistent GSI reads — repository logs **Debug** when Strong is requested on an index query.
- **Debug logs:** `ExecutionQueueRepository` / `WorkerNodeRepository` emit **Debug** lines for each operation (table, index, `ConsistentRead`, claim paths). Enable **Debug** for `JobScheduler.DAL.DynamoDB.Repositories` in the host.
- **Local:** set `ServiceUrl` to `http://localhost:8000`, run `docker-compose-dynamodb.yml` then `setup-dynamodb-local.ps1` from this project folder.
- **AWS:** remove or leave `ServiceUrl` empty; use IAM/credential chain; run `setup-dynamodb.ps1` once per account/region.

**DI registration (mirrors `AddDataAccessLayer` for SQL):**

```csharp
services.AddDynamoDbDataAccessLayer(configuration);
```

**Types:** `IDynamoDbContextFactory` / `DynamoDbContextFactory`, `IExecutionQueueRepository`, `IWorkerNodeRepository`, models under `DynamoDB/Models/`.

### DynamoDB Local integration tests

Tests live under **`../tests/JobScheduler.DAL.DynamoDb.Tests/`**. By default they start **DynamoDB Local** with **Testcontainers** (Docker required); optional **`DYNAMODB_LOCAL_SERVICE_URL`** uses your own Local instance.

```powershell
dotnet test ..\tests\JobScheduler.DAL.DynamoDb.Tests\JobScheduler.DAL.DynamoDb.Tests.csproj
```

See **`../tests/JobScheduler.DAL.DynamoDb.Tests/README.md`** for prerequisites, env overrides, and what each test asserts.

### PostgreSQL integration tests (schedules)

**`../tests/JobScheduler.DAL.Postgres.Tests/`** — `IJobScheduleRepository` CRUD against a disposable PostgreSQL instance. Run:

```powershell
dotnet test ..\tests\JobScheduler.DAL.Postgres.Tests\JobScheduler.DAL.Postgres.Tests.csproj
```

Details: **`../tests/JobScheduler.DAL.Postgres.Tests/README.md`**. Full DAL layout, solution-wide `dotnet test`, and what each test project verifies: **[`../README.md`](../README.md)** (JobScheduler.DAL folder).
