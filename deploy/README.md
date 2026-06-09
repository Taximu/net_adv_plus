# Deploy: API + messaging + workers

This stack runs **PostgreSQL** (UC 1.1 catalog), **DynamoDB Local** (UC 2.1 queue), **Redpanda** (Kafka-compatible streaming), **JobScheduler.Api** (produces catalog + lifecycle events when messaging is enabled), **JobScheduler.JobManager** (catalog topic consumer), and **JobScheduler.JobOrchestrator** (lifecycle topic consumer + periodic batch peek of pending executions via the API).

## Prerequisites

- Docker Desktop with Compose v2 (`docker compose version`)
- Ports **5000**, **55432**, **8888**, **19092**, **18000**, **19644** free (or edit `docker-compose.yml`)

## Start

From this directory:

```powershell
docker compose up -d --build
```

First boot can take several minutes (image pulls + .NET publish).

## URLs

| Service | URL / endpoint |
|---------|----------------|
| Swagger | http://localhost:5000/swagger |
| Redpanda Console | http://localhost:8888 |
| Postgres (host) | `localhost:55432`, database `job_config_db`, user `job_admin`, password `StrongPass123` |
| DynamoDB Local (host) | http://localhost:18000 |

**DynamoDB tables:** The `dynamodb-init` image is built from `deploy/dynamodb/Dockerfile.init` and runs **`bootstrap.py`** (Python **boto3**) against DynamoDB Local with dummy keys. **No AWS CLI on your PC, no AWS account, and no cloud AWS access** — only Docker build/run.

**Note:** Compose uses a **different** Postgres password than `JobScheduler.Api/appsettings.json` on your host (`StrongPass123` here vs `StrongP@ssw0rd` in the file). Only the **containerized** API is configured for the compose password via environment variables.

## Verify streaming

1. Open Swagger → `POST /api/users/{userId}/guid/jobs` with a body like `{ "name": "Kafka Demo Job", "jobType": "api_call", "createdBy": "docker" }`.  
   Use a real `userId` from seed data (query `users` in Postgres, or use any UUID from the seeded `job_definitions` owner — e.g. inspect `03-seed-data.sql` / DB).
2. Check **JobManager** logs: `docker compose logs -f jobmanager` — you should see `Catalog event consumed`.
3. `POST /api/internal/execution/queue/items` with an `ExecutionQueueItem` JSON body — **JobOrchestrator** logs should show a lifecycle message and batch peek counts.

## Stop / reset

```powershell
docker compose down
```

Remove volumes (wipe Postgres + Dynamo + Redpanda data):

```powershell
docker compose down -v
```

## Compose feature note

`api` waits on `dynamodb-init` and `topic-init` with `condition: service_completed_successfully`. That requires a recent Docker Compose implementation. If `docker compose up` errors on conditions, upgrade Docker Desktop or remove those `condition` lines and start `api` manually after init containers finish.

