# JobScheduler.DAL — PostgreSQL integration tests

Covers **`IJobScheduleRepository`** create / read / update / delete against a real PostgreSQL instance (same `job_schedules` schema as `postgres-replication/init-scripts/02-schema.sql`, including **HASH partitioning** by `schedule_id` into four child tables).

## What is verified

| Test | What it checks |
|------|----------------|
| `GetByIdAsync_throws_when_explicit_partition_key_mismatches_schedule_id` | Optional `schedulePartitionKey` must equal `scheduleId` (HASH key). |
| `UpdateAsync_throws_when_explicit_partition_key_mismatches_schedule` | Same validation on **Update**. |
| `CreateAsync_then_GetByIdAsync_roundtrips` | Insert + read by id; cron expression stored. |
| `CreateAsync_UpdateAsync_persists_changes` | Update name, priority, `is_enabled`; read reflects changes. |
| `CreateAsync_DeleteAsync_removes_row` | Delete succeeds once; row gone; second delete returns false. |
| `CreateAsync_emits_debug_log_with_physical_child_partition_name` | **Debug** log after **Create** contains **`PhysicalPartition=job_schedules_p0..p3`**. |
| `GetByIdAsync_emits_debug_log_with_physical_partition_when_row_exists` | **Debug** log after **GetById** (hit) contains partition name and **`RowFound=true`**. |
| `GetByJobIdAsync_emits_debug_log_with_partition_histogram` | **Debug** log contains **`RowsPerPhysicalPartition=`** and **`job_schedules_p`** (per-child counts). |

## How it runs

- **Testcontainers** starts an official **`postgres:15.1`** container (`Testcontainers.PostgreSql`).
- **`docker pull`** runs first (same pattern as DynamoDB tests).
- The schema file is copied to build output (`postgres-schema/02-schema.sql`) and applied on startup; one **user** and **job_definition** row are seeded for FK constraints. Inserts target the partitioned parent `job_schedules` transparently.
- **`PostgresDalFixture.JobScheduleLogCapture`** is a **`ListCapturingLoggerProvider`** wired via **`AddLogging`**; partition-log tests call **`Clear()`** before assertions and inspect **`Snapshot()`**.
- **Console:** by default the fixture also registers **`AddSimpleConsole`** with filters so **`JobScheduleRepository`** **Debug** lines (including **`PhysicalPartition`** / histogram) appear in **`dotnet test`** output. Set **`DAL_PG_TESTS_SUPPRESS_PARTITION_CONSOLE=1`** to turn that off (e.g. quieter CI).

## Requirements

- **Docker** running and **`docker`** on `PATH`.

## Run

From repo root:

```powershell
dotnet test .\JobScheduler.DAL\tests\JobScheduler.DAL.Postgres.Tests\JobScheduler.DAL.Postgres.Tests.csproj
```

`DapperNpgsqlConfiguration` (snake_case column names + `DateOnly` / `TimeOnly` handlers) is registered in the fixture before repositories are used; `AddDataAccessLayer` registers the same in production apps.
