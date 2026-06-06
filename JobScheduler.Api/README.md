# JobScheduler.Api

Minimal ASP.NET Core **presentation** host for **`JobScheduler.BL`**. Endpoints do not accept consistency parameters; routing appears in **Debug** logs from **`PostgresConnectionFactory`** and DynamoDB repositories (see `Logging:LogLevel` in `appsettings.json`).

## Run

```powershell
dotnet run --project JobScheduler.Api/JobScheduler.Api.csproj
```

Then open **Swagger UI**: `http://localhost:5088/swagger` (OpenAPI JSON: `/swagger/v1/swagger.json`). Try each operation from the UI to demo request/response bodies.

**PostgreSQL:** `Database` must match your primary + replicas; **`user_id`** values must exist in `users` (FK on `job_definitions`).

**DynamoDB (UC 2.1):** set `DynamoDB:ServiceUrl` for local (e.g. `http://localhost:8000`) and ensure tables exist per `JobScheduler.DAL` setup scripts.

## Endpoints — UC 1.1 (PostgreSQL)

- `GET /api/users/{userId}/jobs` — list jobs (eventual vs read-after-write chosen in BL).
- `POST /api/users/{userId}/jobs` — body: `{ "name": "...", "jobType": "api_call", "createdBy": "demo" }`.
- `GET /api/users/{userId}/jobs/{jobId}`.
- `GET /api/users/{userId}/jobs/{jobId}/schedules` — list schedules.
- `POST /api/users/{userId}/jobs/{jobId}/schedules` — create schedule (**201 Created** + `Location`).
- `GET /api/internal/scheduler/jobs/active` — **Strong** catalog for workers.

## Endpoints — UC 2.1 (DynamoDB execution queue)

- `GET /api/internal/execution/queue/pending?limit=100` — poll pending items (**eventual** GSI query in BL/DAL).
- `GET /api/internal/execution/queue/item?queueId=...&scheduledFor=...` — **strongly consistent** `GetItem` for one row.
- `POST /api/internal/execution/queue/items` — enqueue body as **`ExecutionQueueItem`** JSON (**201 Created** + `Location` query URL).

Sample log output (grep **`UC2.1`** for DynamoDB UC 2.1 lines, **`Postgres`** factory events for UC 1.1): **[`../JobScheduler.DAL/docs/consistency-demo-logs.md`](../JobScheduler.DAL/docs/consistency-demo-logs.md)**.
