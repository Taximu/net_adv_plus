# JobScheduler.DAL

**JobScheduler.DAL** is the data access layer for the online job scheduler: **PostgreSQL** for job configuration (UC 1.1 — create/manage jobs and schedules) with **read/write splitting**, and **Amazon DynamoDB** for the execution queue and worker registry (UC 2.1 — run jobs at scheduled times).

## What’s in this folder

| Path | Purpose |
|------|---------|
| [`docs/partitioning-strategy.md`](./docs/partitioning-strategy.md) | **Module 04_02:** partitioning vs sharding decision, growth/patterns, HASH `job_schedules` DDL reference, verification queries. |
| [`docs/consistency-requirements.md`](./docs/consistency-requirements.md) | Use-case consistency analysis and mapping to `ConsistencyLevel`. |
| [`docs/consistency-integration-test-execution-log.md`](./docs/consistency-integration-test-execution-log.md) | **Attached execution logs:** how to run routing integration tests and example log shapes (Postgres + DynamoDB). |
| [`src/JobScheduler.DAL/`](./src/JobScheduler.DAL/README.md) | Library project: connection factory, `DatabaseOptions` / `DynamoDbOptions`, repositories, `UnitOfWork`, DynamoDB Local **Docker Compose** and **setup scripts**. |
| [`tests/JobScheduler.DAL.Postgres.Tests/`](./tests/JobScheduler.DAL.Postgres.Tests/README.md) | Integration tests for **PostgreSQL** — mainly `IJobScheduleRepository` CRUD. |
| [`tests/JobScheduler.DAL.DynamoDb.Tests/`](./tests/JobScheduler.DAL.DynamoDb.Tests/README.md) | Integration tests for **DynamoDB Local** — execution queue and worker repositories. |

**Design and replication decisions** (volume, consistency, SQL vs NoSQL, replication): [`../replication_task.md`](../replication_task.md).

**Partitioning / sharding (Module 04_02 follow-on):** strategy, growth assumptions, HASH partitions on `job_schedules`, and verification SQL — [`docs/partitioning-strategy.md`](./docs/partitioning-strategy.md). DDL and seed volume live under [`../postgres-replication/init-scripts/`](../postgres-replication/init-scripts/).

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download) (projects target `net10.0`). Docker Engine is required for Testcontainers-based integration tests. If optional `docker pull` fails on your engine (e.g. Rancher Desktop / `lease does not exist`), fixtures continue and Testcontainers still starts containers; set **`DAL_TESTCONTAINERS_EXPLICIT_PULL=0`** to skip the pre-pull step.

## Registration (host app)

```csharp
// UC 1.1 — PostgreSQL (job definitions, schedules, read/write split)
services.AddDataAccessLayer(configuration);

// UC 2.1 — DynamoDB (execution queue, workers)
services.AddDynamoDbDataAccessLayer(configuration);
```

Configuration sections: **`Database`** and **`DynamoDB`** — see [`src/JobScheduler.DAL/appsettings.json`](./src/JobScheduler.DAL/appsettings.json) for examples.

---

## Running tests

**All DAL-related tests in this repo** are in the solution file at the repository root. From **`net_adv_plus`** (repo root):

```powershell
dotnet test .\net_adv_plus.slnx
```

That runs:

1. **`JobScheduler.DAL.Postgres.Tests`** — PostgreSQL + Dapper schedule repository.  
2. **`JobScheduler.DAL.DynamoDb.Tests`** — DynamoDB Local + AWS SDK through the DAL.  
3. **`PostgreSql.Replication.Tests`** (under `postgres-replication/`) — streaming replication behaviour (primary vs replicas); not the DAL assembly, but part of the same solution.

### Prerequisites (integration tests)

| Requirement | Applies to |
|-------------|------------|
| **Docker** daemon running | All three test projects above. |
| **`docker`** on **`PATH`** | Used to **pull** images before containers start (`postgres:15.1`, `amazon/dynamodb-local:latest`, Bitnami PostgreSQL for replication). |
| **Network** to pull images | First run can take several minutes. |

Optional: **`DYNAMODB_LOCAL_SERVICE_URL`** — point tests at an existing DynamoDB Local instead of starting a container (see DynamoDB test README).

Optional: **`REPLICATION_TESTS_USE_EXTERNAL=1`** — use your own Postgres stack for replication tests instead of Testcontainers (see [`../postgres-replication/INTEGRATION_TESTS.md`](../postgres-replication/INTEGRATION_TESTS.md)).

### Run only DAL test projects

From repo root:

```powershell
dotnet test .\JobScheduler.DAL\tests\JobScheduler.DAL.Postgres.Tests\JobScheduler.DAL.Postgres.Tests.csproj
dotnet test .\JobScheduler.DAL\tests\JobScheduler.DAL.DynamoDb.Tests\JobScheduler.DAL.DynamoDb.Tests.csproj
```

---

## What the tests verify

### `JobScheduler.DAL.Postgres.Tests` (15 tests)

| Area | What is checked |
|------|------------------|
| **UC 1.1 — Create a job** | **`Uc11CreateJobCatalogDalTests`**: **`IJobDefinitionRepository`** — **`ExistsByNameAsync`(..., `Strong`)** before insert, **`CreateAsync`**, **`GetByIdAsync`(..., `Strong`)**, **`GetByUserIdAsync`(..., `Eventual`)**; **`GetActiveJobsAsync`(`Strong`)** includes seeded active job. |
| **`IJobScheduleRepository.CreateAsync`** | Insert returns a row with a non-empty `schedule_id`; `GetByIdAsync` reads the same cron/name. |
| **`UpdateAsync`** | Name, priority, and `is_enabled` persist; `GetByIdAsync` reflects updates. |
| **`DeleteAsync`** | Row removed; second delete returns `false`; `GetByIdAsync` is null. |
| **`GetByJobIdAsync`** | New schedule appears in the list for the seeded job. |
| **Consistency routing logs** | **`PostgresConsistencyRoutingTests`**: captures **`PostgresConnectionFactory`** **Debug** lines — **Strong** vs **Eventual** (replica **ReplicaIndex** round-robin) vs **Write**. See **[`docs/consistency-integration-test-execution-log.md`](./docs/consistency-integration-test-execution-log.md)**. |

**Mechanics:** [Testcontainers.PostgreSql](https://www.nuget.org/packages/Testcontainers.PostgreSql) starts **PostgreSQL 15.1**, applies the same schema as [`../postgres-replication/init-scripts/02-schema.sql`](../postgres-replication/init-scripts/02-schema.sql), seeds one **user** and **job_definition** for FKs. Dapper is configured for **snake_case** columns and **`DateOnly` / `TimeOnly`** (same as `AddDataAccessLayer` at runtime). The fixture registers a **`ListCapturingLoggerProvider`** plus an optional **simple console** logger (filtered to **`JobScheduleRepository`** at **Debug**) so partition lines show in **`dotnet test`** output; set **`DAL_PG_TESTS_SUPPRESS_PARTITION_CONSOLE=1`** to hide them.

### `JobScheduler.DAL.DynamoDb.Tests` (11 tests)

| Area | What is checked |
|------|------------------|
| **UC 2.1 — Execute at scheduled time** | **`Uc21ScheduledExecutionQueueDalTests`**: enqueue → **`GetAsync`(..., `Strong`)** → **`QueryByQueueStatusAsync`(..., `Eventual`)** → **`TryClaimAsync`** → verify with **`Strong`** read. **`ExecutionQueueRepositoryTests`** exercises the same repository with explicit **`ConsistencyLevel`** on each read path. |
| **Connectivity / schema** | Local responds; tables **ExecutionQueue** and **WorkerNodes** exist; GSIs **PendingExecutionsIndex** and **WorkerAssignmentsIndex** on `ExecutionQueue`. |
| **`IExecutionQueueRepository`** | Put/get (strong read); query by `queueStatus`; **conditional claim** (`TryClaimAsync`) and second claim fails; query by assigned worker. |
| **`IWorkerNodeRepository`** | Put/get/delete roundtrip. |
| **Dynamo consistency routing logs** | **`DynamoDbConsistencyRoutingTests`**: **`GetItem`** logs **ConsistentRead** true/false by **`ConsistencyLevel`**; **GSI Query** logs **PollPending** / **Eventual**. See **[`docs/consistency-integration-test-execution-log.md`](./docs/consistency-integration-test-execution-log.md)**. |

**Mechanics:** Testcontainers runs **`amazon/dynamodb-local`**, creates tables in code (aligned with `setup-dynamodb-local.ps1`), **30s** per-test timeout, **12s** health probe, **`ClientTimeoutSeconds: 15`** and **`MaxErrorRetry = 0`** so a missing endpoint fails fast.

### `PostgreSql.Replication.Tests` (5 tests, outside `JobScheduler.DAL/` folder)

Validates **physical streaming replication** (Bitnami stack or Testcontainers): primary not in recovery, replicas in recovery, WAL visibility, read-only on replicas, `pg_last_wal_receive_lsn()`. See [`../postgres-replication/INTEGRATION_TESTS.md`](../postgres-replication/INTEGRATION_TESTS.md).

---

## Build (library only)

From repo root:

```powershell
dotnet build .\JobScheduler.DAL\src\JobScheduler.DAL\JobScheduler.DAL.csproj
```

More API and connection examples: [`src/JobScheduler.DAL/README.md`](./src/JobScheduler.DAL/README.md).
