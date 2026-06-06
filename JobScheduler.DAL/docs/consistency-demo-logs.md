# Sample execution logs (consistency + routing)

Enable **Debug** logging for the categories below (see `JobScheduler.Api/appsettings.json` defaults). Then run the API and call endpoints; copy lines that prove **different consistency paths**.

```bash
dotnet run --project JobScheduler.Api/JobScheduler.Api.csproj
```

## UC 1.1 — PostgreSQL (`PostgresConnectionFactory`)

Category: `JobScheduler.DAL.Connection` (logger type `PostgresConnectionFactory`).

| Endpoint / flow | Expected consistency in logs |
|-----------------|-------------------------------|
| `POST /api/users/{userId}/jobs` | `PostgresWriteOpened` … **Primary** … **Write** |
| `GET /api/users/{userId}/jobs` (after POST, within cooldown) | `PostgresPrimaryReadOpened` … **Strong** … **Primary** |
| `GET /api/users/{userId}/jobs` (cooldown expired) | `PostgresReadOpened` … **Eventual** … **Replica** (round-robin ports if replicas configured) |
| `GET /api/internal/scheduler/jobs/active` | `PostgresPrimaryReadOpened` … **Strong** … **Primary** |

### Example lines (shape only — ports depend on `appsettings`)

```text
[12:01:00 DBG] PostgresConnectionFactory PostgresWriteOpened: ConsistencyLevel=Strong Role=Primary Host=localhost Port=5432 ReplicaIndex=null Operation=Write
[12:01:01 DBG] PostgresConnectionFactory PostgresPrimaryReadOpened: ConsistencyLevel=Strong Role=Primary Host=localhost Port=5432 ReplicaIndex=null Operation=Read
[12:01:02 DBG] PostgresConnectionFactory PostgresReadOpened: ConsistencyLevel=Eventual Role=Replica Host=localhost Port=5434 ReplicaIndex=0 Operation=Read
[12:01:03 DBG] PostgresConnectionFactory PostgresPrimaryReadOpened: ConsistencyLevel=Strong Role=Primary Host=localhost Port=5432 ReplicaIndex=null Operation=Read
```

## UC 2.1 — DynamoDB (`ExecutionQueueRepository`)

Category: `JobScheduler.DAL.DynamoDB.Repositories` (logger type `ExecutionQueueRepository`). All UC 2.1 execution-queue lines are prefixed with **`UC2.1`** so they are easy to grep alongside PostgreSQL lines.

| Endpoint / flow | Expected log content |
|-----------------|----------------------|
| `GET /api/internal/execution/queue/pending` | `UC2.1 DynamoDB Query` … `PendingExecutionsIndex` … **Eventual** … **`ConsistentRead=False`** (GSI); then **`UC2.1 DynamoDB QueryCompleted`** … `ReturnedCount=…` |
| `GET /api/internal/execution/queue/item?...` | `UC2.1 DynamoDB GetItem` … **Strong** … **`ScheduledFor=`** … **ConsistentRead=True**; then **`GetItemCompleted`** … `ItemFound=true/false` |
| `POST /api/internal/execution/queue/items` | `UC2.1 DynamoDB PutItem` … **Enqueue** … `QueueStatus` (write path) |
| Worker claim (DAL / internal) | `UC2.1 DynamoDB UpdateItem` … **TryClaim** … then **`TryClaimCompleted`** … `Success=true/false` |

### Example lines

```text
[12:02:00 DBG] ExecutionQueueRepository UC2.1 DynamoDB Query Table=ExecutionQueue Index=PendingExecutionsIndex Operation=PollPending ConsistencyLevel=Eventual ConsistentRead=False
[12:02:00 DBG] ExecutionQueueRepository UC2.1 DynamoDB QueryCompleted Table=ExecutionQueue Index=PendingExecutionsIndex Operation=PollPending ConsistencyLevel=Eventual QueueStatusFilter=pending ReturnedCount=3 ConsistentRead=False
[12:02:01 DBG] ExecutionQueueRepository UC2.1 DynamoDB GetItem Table=ExecutionQueue Operation=Read QueueId=q-1 ScheduledFor=2026-06-05T12:00:00.0000000Z ConsistencyLevel=Strong ConsistentRead=True
[12:02:01 DBG] ExecutionQueueRepository UC2.1 DynamoDB GetItemCompleted Table=ExecutionQueue QueueId=q-1 ScheduledFor=2026-06-05T12:00:00.0000000Z ConsistencyLevel=Strong ConsistentRead=True ItemFound=True
[12:02:02 DBG] ExecutionQueueRepository UC2.1 DynamoDB PutItem Table=ExecutionQueue Operation=Enqueue QueueId=q-1 ScheduledFor=2026-06-05T12:00:00.0000000Z QueueStatus=pending (write path; no ConsistentRead on PutItem)
```

If a caller mistakenly passes **Strong** to a GSI query in tests or future code, you will see a **Debug** line stating that the GSI has no strong read and the query remains eventually consistent.

## Prerequisites

- **PostgreSQL:** primary + replica connection strings as in `Database` config (e.g. `docker-compose-postgres-replication.yml`).
- **DynamoDB Local:** `DynamoDB:ServiceUrl` (e.g. `http://localhost:8000`) and tables created per `JobScheduler.DAL` DynamoDB setup scripts.

## Automated log capture (integration tests)

For **attached**, repeatable evidence without running the API manually, see **[`consistency-integration-test-execution-log.md`](consistency-integration-test-execution-log.md)** (`PostgresConsistencyRoutingTests`, `DynamoDbConsistencyRoutingTests`, `dotnet test` with detailed logger).
