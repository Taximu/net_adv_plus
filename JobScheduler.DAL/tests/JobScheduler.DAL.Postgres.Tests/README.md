# JobScheduler.DAL — PostgreSQL integration tests

Covers **`IJobScheduleRepository`** create / read / update / delete against a real PostgreSQL instance (same `job_schedules` schema as `postgres-replication/init-scripts/02-schema.sql`).

## What is verified

| Test | What it checks |
|------|----------------|
| `CreateAsync_then_GetByIdAsync_roundtrips` | Insert + read by id; cron expression stored. |
| `CreateAsync_UpdateAsync_persists_changes` | Update name, priority, `is_enabled`; read reflects changes. |
| `CreateAsync_DeleteAsync_removes_row` | Delete succeeds once; row gone; second delete returns false. |
| `CreateAsync_GetByJobIdAsync_lists_schedule` | Schedule appears in `GetByJobIdAsync` for the seeded job. |

## How it runs

- **Testcontainers** starts an official **`postgres:15.1`** container (`Testcontainers.PostgreSql`).
- **`docker pull`** runs first (same pattern as DynamoDB tests).
- The schema file is copied to build output (`postgres-schema/02-schema.sql`) and applied on startup; one **user** and **job_definition** row are seeded for FK constraints.

## Requirements

- **Docker** running and **`docker`** on `PATH`.

## Run

From repo root:

```powershell
dotnet test .\JobScheduler.DAL\tests\JobScheduler.DAL.Postgres.Tests\JobScheduler.DAL.Postgres.Tests.csproj
```

`DapperNpgsqlConfiguration` (snake_case column names + `DateOnly` / `TimeOnly` handlers) is registered in the fixture before repositories are used; `AddDataAccessLayer` registers the same in production apps.
