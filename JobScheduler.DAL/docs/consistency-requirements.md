# Consistency requirements (UC 1.1 & UC 2.1)

This document ties **business use cases** from the **Online Job Scheduler** domain to **consistency expectations** and to concrete mechanisms in the DAL. Consistency is a **consequence of requirements**, not a tunable preference.

**Domain references (repo):**

- [`../../Diagrams/png/Online%20Job%20Scheduler%20Domain%20Entities.png`](../../Diagrams/png/Online%20Job%20Scheduler%20Domain%20Entities.png) — entities (users, jobs, schedules, execution queue, workers).
- [`../../Diagrams/png/solution-component-diagram.png`](../../Diagrams/png/solution-component-diagram.png) — conceptual components (API, scheduler, workers, PostgreSQL catalog, DynamoDB execution plane).

---

## UC 1.1 — Create / manage a job (PostgreSQL catalog)

Job definitions and schedules live on **PostgreSQL** (primary + optional read replicas). The DAL exposes **`ConsistencyLevel`** on **read** repository methods; writes always go to the **primary**.

### Summary

| Use case | Criticality | Short replication lag OK? | Required consistency | Rationale |
|----------|-------------|---------------------------|------------------------|-----------|
| **Worker/scheduler: authoritative job catalog** (`GetActiveJobsAsync`, resolve job before run) | High — stale config can cause wrong or unsafe execution | **No** | **Strong** | Must read **latest committed** state from the **leader** (primary connection). |
| **Uniqueness / invariant checks** (`ExistsByNameAsync` before insert) | High — duplicates break integrity and UX | **No** | **Strong** | Predicate must see all committed writes relevant to that key. |
| **Dashboard: list my jobs** (`GetByUserIdAsync`) | Medium | **Yes** for typical UX | **Eventual** | Replicas improve read capacity; slightly stale list is acceptable. |
| **Browse by status** (`GetByStatusAsync`) | Medium | **Yes** | **Eventual** | Operational view; not the sole gate for execution. |
| **Same user right after save** (UI session) | High for **that actor’s** view; global linearizability not required | **No** for own edits immediately after write | **Strong** (during cooldown) | **`ConsistencyManager`** forces **primary** reads for a short window after **`TrackWrite`** (see below). |
| **Schedules for a job** (`GetByJobIdAsync`) | Medium; higher after user edits | Mixed | **Eventual** by default; **Strong** when cooldown applies | Same pattern as job list. |
| **Point read schedule** (`GetByIdAsync`) | Varies by caller | Varies | Caller passes **Strong** or **Eventual** | BL chooses per use case. |

### PostgreSQL implementation (database-native)

| `ConsistencyLevel` | Routing | Native behavior |
|--------------------|---------|-----------------|
| **Strong** | Primary connection | Latest committed rows on the leader. |
| **Eventual** | Round-robin **replica** connection strings (or primary if none configured) | **Asynchronous physical replication** — classic eventual consistency on standbys. |

**Read-after-own-write (optional coursework pattern):** there is no separate enum value. After **`ConsistencyManager.TrackWrite`**, the BL uses **`ConsistencyLevel.Strong`** for the same user’s reads until the cooldown expires, then **`Eventual`** again — same routing as any **Strong** read.

### Cooldown after write (`ConsistencyManager`)

After a successful **create/update** for an actor (e.g. user id string), the BL calls **`ConsistencyManager.TrackWrite(userKey)`**. While **`IsReadAfterWriteApplicable`** is true, the BL upgrades the next reads for that actor to **`ConsistencyLevel.Strong`** (primary). After **`CooldownPeriod`**, reads fall back to **Eventual** for cost and throughput.

This matches the coursework pattern (memory cache + short window) without exposing consistency to HTTP clients.

---

## UC 2.1 — Execute a job at a scheduled time (DynamoDB)

The **execution queue** and **worker registry** use **Amazon DynamoDB**. Consistency here is expressed with the **same enum** where it maps to **AWS-native** read semantics; global secondary index (GSI) queries have **fixed** eventual characteristics per DynamoDB rules.

### Summary

| Use case | Criticality | “Lag” / staleness | Required consistency | Rationale |
|----------|---------------|-------------------|----------------------|-----------|
| **Coordinator: read one queue item before claim / retry** (`GetItem` by PK) | High — wrong state → double work or skipped execution | Not acceptable for decision on **this item** | **Strong** (DAL default for `GetAsync`) | **`GetItem` + `ConsistentRead=true`** — linearizable read for that key (DynamoDB docs). |
| **Worker: poll pending work** (`Query` on `PendingExecutionsIndex`) | Medium — may briefly miss a just-written item on GSI | **Yes** at index level | **Eventual** (only option) | **GSI queries do not support `ConsistentRead`**. Staleness is bounded; workers poll repeatedly. |
| **List assignments for a worker** (`Query` on `WorkerAssignmentsIndex`) | Medium | **Yes** | **Eventual** | Same as GSI query semantics. |
| **Enqueue / register** (`PutItem`) | High for durability | N/A (write) | Write acknowledged by DynamoDB | No separate read; **`PutItem`** is durable per AWS API contract. |
| **Claim item** (`UpdateItem` + condition) | High — exactly-once assignment per successful claim | N/A | **Strong / linearizable per item** | **Conditional update** serializes concurrent writers on the same item. |

### DynamoDB implementation (database-native)

| Operation | `ConsistencyLevel` parameter | AWS mapping |
|-----------|------------------------------|-------------|
| **`GetAsync` (queue item or worker node)** | **Strong** → `ConsistentRead=true` | Strongly consistent read for the base table key. |
| **`GetAsync`** | **Eventual** → `ConsistentRead=false` | Eventually consistent read (cheaper; use only when stale item is acceptable). |
| **`QueryByQueueStatusAsync` / `QueryByAssignedWorkerAsync`** | **Eventual** (default; **Strong** is not supported by DynamoDB on GSI) | Logged at **Debug** if a caller passes **Strong** — behavior remains GSI eventual. |
| **`TryClaimAsync`** | N/A | **Conditional `UpdateItem`** — native per-item atomicity. |

---

## Cross–use case note

UC 2.1 **execution** should use **UC 1.1** job configuration loaded with **Strong** (or equivalent) from PostgreSQL when the runner needs authoritative **what to execute**; the **queue row** in DynamoDB is a separate store with the consistency rules above.
