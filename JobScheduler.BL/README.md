# JobScheduler.BL

Business layer for the job scheduler: **PostgreSQL (UC 1.1)** and **DynamoDB execution queue (UC 2.1)**. **HTTP/API callers do not pass `ConsistencyLevel`**: services map each use case to the correct DAL level (including **read-after-write** via `ConsistencyManager` after user mutations on SQL paths).

## Registration

```csharp
using JobScheduler.BL.Extensions;
using JobScheduler.DAL.Extensions;

services.AddDataAccessLayer(configuration);
services.AddDynamoDbDataAccessLayer(configuration);
services.AddJobSchedulerBusinessLogic();
```

## Services

| Service | Consistency behavior |
|---------|----------------------|
| `IUserJobsService` | **Eventual** list/get when outside cooldown; **Strong** (primary read) after `CreateDraftJobAsync` for the same user key while cooldown applies. **Strong** for `ExistsByNameAsync` before insert. |
| `ISchedulerCatalogService` | **Strong** for `GetActiveJobsForWorkerAsync` (worker must see authoritative configuration). |
| `IUserSchedulesService` | **Strong** ownership check on job; list uses **Strong** or **Eventual** like user jobs; `CreateScheduleAsync` tracks write for cooldown. |
| `IExecutionOrchestrationService` (UC 2.1) | **Eventual** GSI poll for `PeekPendingExecutionsAsync`; **Strong** `GetItem` for `GetExecutionAsync`; writes via `PutAsync`. |

See **[`../JobScheduler.DAL/docs/consistency-requirements.md`](../JobScheduler.DAL/docs/consistency-requirements.md)** for rationale.
