# JobScheduler.DAL — DynamoDB Local integration tests

These tests call **DynamoDB Local** through the real DAL (`AddDynamoDbDataAccessLayer`, repositories). They do **not** validate AWS multi-AZ replication (Local is a single emulator).

## Default: Testcontainers (recommended for `dotnet test`)

The shared fixture starts **`amazon/dynamodb-local`** via Testcontainers, runs `docker pull` first (so the image exists before create), creates **ExecutionQueue** and **WorkerNodes** (same shape as `setup-dynamodb-local.ps1`), then runs assertions against a random mapped port.

**Requirements**

- **Docker** daemon running.
- **`docker`** on `PATH`.

**Optional: use an existing DynamoDB Local**

Set **`DYNAMODB_LOCAL_SERVICE_URL`** (for example `http://localhost:8000`) to skip the container. Tables must already exist (run compose + `setup-dynamodb-local.ps1` from `JobScheduler.DAL/src/JobScheduler.DAL` if needed):

```powershell
cd JobScheduler.DAL/src/JobScheduler.DAL
docker compose -f docker-compose-dynamodb.yml up -d
powershell -File .\setup-dynamodb-local.ps1
$env:DYNAMODB_LOCAL_SERVICE_URL = "http://localhost:8000"
dotnet test ..\..\tests\JobScheduler.DAL.DynamoDb.Tests\JobScheduler.DAL.DynamoDb.Tests.csproj
```

## Run (from repo `JobScheduler.DAL` folder)

```powershell
dotnet test .\tests\JobScheduler.DAL.DynamoDb.Tests\JobScheduler.DAL.DynamoDb.Tests.csproj
```

## What is verified

- List/describe tables and GSIs (`PendingExecutionsIndex`, `WorkerAssignmentsIndex`)
- `IExecutionQueueRepository`: put/get, query by `queueStatus`, conditional claim, query by worker
- `IWorkerNodeRepository`: put/get/delete

Tests use a **30s** per-test cancellation budget and a **12s** health check for `ListTables`.

The DAL sets **`ClientTimeoutSeconds: 15`** in the test fixture and **`MaxErrorRetry = 0`** when that option is set, so unreachable endpoints fail quickly instead of hanging.
