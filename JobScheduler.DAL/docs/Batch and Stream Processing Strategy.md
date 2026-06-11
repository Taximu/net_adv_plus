## UC 1.1 — Create a New Job

### Recommended processing model: **Hybrid (online OLTP + optional event stream)**

| Layer | Type | Role |
|--------|------|------|
| **Authoring path** | **Real-time (synchronous)** | User/API creates or updates job metadata; the system must enforce uniqueness, ownership, and read-after-write semantics on the catalog. |
| **Downstream propagation** | **Stream (asynchronous)** | Optional: notify search, analytics, billing, other bounded contexts without blocking the API. |
| **Analytics / compliance** | **Batch** | Periodic consolidation of catalog snapshots, cost reports, or data warehouse loads. |

**Justification:** Job creation is a **low-latency, consistency-sensitive** operation (PostgreSQL: primary writes, strong reads for invariants such as name uniqueness, read-after-write routing in the business layer). That is classic **interactive OLTP**, not a batch window. A **pure batch** model (for example, ingesting jobs once per hour) would violate UX and integrity expectations. A **pure stream** model without a durable system of record would push uniqueness and referential rules into consumers and invite divergence. **Hybrid** keeps the database as the source of truth and uses events only for **side effects** that tolerate seconds of delay and idempotent handling.

---

### If you add stream processing: events, schemas, purpose

These are **domain events** published **after** a successful commit (outbox or transactional publisher pattern recommended).

| Event name | Purpose | Suggested schema (conceptual) |
|------------|---------|------------------------------|
| **`job.definition.created`** | New draft or active job row committed; drive projections, audit, notifications. | `eventId` (UUID), `eventType`, `occurredAt` (ISO-8601), `tenantId` / `userId`, `jobId`, `name`, `jobType`, `status`, `correlationId` |
| **`job.definition.updated`** | Configuration changed; invalidate caches, revalidate dependent schedules. | Same envelope + `changedFields[]` or full `payload` snapshot version |
| **`job.schedule.attached`** (if schedule is created in the same journey) | Scheduler/orchestrator materializes next run windows. | `scheduleId`, `jobId`, `cron` or `nextRunAt`, `timezone` |

**Envelope pattern (all events):** `specversion`, `type`, `source`, `id`, `time`, `datacontenttype`, `data` — aligns with **CloudEvents** for interoperability.

**Consumers (when streaming):** notification service, audit log indexer, data lake ingest, anti-abuse scoring, optional **CQRS read models** for dashboards at eventual consistency.

---

### If you rely on batch processing for UC 1.1: triggers and frequency

Batch is **secondary** for UC 1.1.

| Batch job | Trigger | Typical frequency |
|-----------|---------|---------------------|
| **Warehouse / BI export** | Cron or orchestrator (Airflow, Step Functions) | Daily or hourly |
| **Replica lag / health reports** | Metrics alarm + scheduled report | Hourly |
| **Full-text / vector index rebuild** | Low priority catch-up | Daily (if not stream-driven incremental) |

**Not** used for: validating uniqueness at insert time, or returning the created job to the client (those stay on the **real-time** path).

---

### Consumer services (UC 1.1)

| Service | Responsibility |
|---------|----------------|
| **Job Scheduler API + business layer** | Authoritative create/update; maps reads to Strong/Eventual per consistency requirements. |
| **Catalog projection workers** (optional, stream-backed) | Update Redis/OpenSearch from `job.definition.*` events. |
| **Audit / SIEM forwarder** (optional) | Append-only compliance trail from the same events. |
| **Batch ETL** | Bulk extract from PostgreSQL (or from object storage exports) to analytics. |

---

### Messaging infrastructure (UC 1.1)

**Minimum viable:** **No broker** — HTTP + PostgreSQL satisfies UC 1.1 alone.

**When you add async integration, justified choices:**

| Option | When to choose | Configuration sketch |
|--------|----------------|------------------------|
| **Apache Kafka** | Many subscribers, high throughput, replay, strict ordering **per partition key** (`jobId` or `userId`) | Topic `job.catalog.events`; **partitions** = 12–48 (scale with producer TPS); **key** = `userId` for per-user ordering; **consumer groups** e.g. `catalog-search-indexer`, `catalog-audit-writer`; `retention.ms` 7–30d; compaction optional on keyed changelog topics if you model state. |
| **Amazon SNS + SQS** | Fan-out to a small number of queues, AWS-native, simpler ops | One SNS topic per domain; **SQS standard queues** per consumer; **visibility timeout** tuned to handler duration; **DLQ** per queue; no ordering unless you use FIFO with `MessageGroupId` = `userId`. |
| **RabbitMQ** | Central broker on-prem / Kubernetes, flexible routing | **Exchange** `job.catalog` (topic); **queues** bound by routing keys `job.definition.created`, etc.; **prefetch** tuned per consumer; HA **quorum queues** if you need durability. |

**Justification summary:** Kafka fits **high fan-out, replay, and ordered per-key** pipelines; SNS/SQS fits **AWS-centric, modest cardinality** fan-out; RabbitMQ fits **traditional enterprise** routing. For expected coursework scale (on the order of single-digit to hundreds of creates per second), **SNS+SQS** or a **single compacted Kafka topic** are both defensible; the decisive factor is whether you need **replay** (Kafka) vs **simplicity** (SQS).

---

## UC 2.1 — Execute a Job at a Scheduled Time

### Recommended processing model: **Hybrid (time-driven micro-batch + near-real-time execution)**

| Phase | Type | Role |
|--------|------|------|
| **Schedule evaluation** | **Batch / micro-batch** | Repeatedly find executions whose `scheduledTime <= now` (or equivalent) and transition them to “due” / enqueue. |
| **Dispatch to workers** | **Near-real-time** | Workers must claim and run work with low latency once due; polling interval defines a small batch of candidates seen per tick. |
| **Execution telemetry** | **Stream (recommended)** | Emit `execution.started`, `execution.succeeded` / `failed` for observability and SLA dashboards. |

**Justification:** “At scheduled time” is inherently **discrete in time**: something must **wake up** on a cadence (cron, Step Functions, Kubernetes CronJob, or a loop inside a scheduler service). That is **not** continuous stream ingestion of external facts; it is **time-triggered batch slices**. The **handoff to runners** behaves like a **work queue**: low end-to-end latency, competing consumers, and **at-most-once or effectively-once** semantics via conditional claims — which matches the **DynamoDB** execution design (GSI query for pending work at eventual consistency, **strong read** on the base item where needed, **conditional update** for claims). Optional **Kafka/Pulsar** can wrap this for **cross-system** fan-out without replacing the claim store.

---

### If you add stream processing: events, schemas, purpose

| Event name | Purpose | Suggested schema (conceptual) |
|------------|---------|------------------------------|
| **`execution.enqueued`** | Item written to execution plane; metrics on backlog depth. | `executionId`, `jobId`, `scheduleId`, `runAt`, `priority`, `queueStatus` |
| **`execution.claimed`** | Worker won the claim; drives “active runs” dashboards. | `executionId`, `workerId`, `claimedAt` |
| **`execution.started` / `succeeded` / `failed`** | SLA, billing, user notifications | `executionId`, `durationMs`, `httpStatus` or `errorCode`, `attempt` |
| **`execution.deadlettered`** | Permanent failure after retries | `executionId`, `reason`, `lastError` |

**Note:** The **queue row in DynamoDB** is the operational truth; **stream events** are **derived** (DynamoDB Streams, or application publish after `PutItem` / successful `UpdateItem`) for observability, not the sole claim mechanism.

---

### Batch processing: triggers and frequency (UC 2.1)

| Trigger | What runs | Frequency |
|---------|-----------|-------------|
| **Wall-clock tick** | Scheduler evaluates cron / next-run table and **enqueues** DynamoDB items (or inserts SQL “due” rows then enqueues) | **Every 15–60 s** for minute-level schedules; **≤ 1 s** only if sub-minute SLAs justify cost |
| **Worker poll loop** | Query pending GSIs + conditional claim | **100 ms–2 s** jittered interval per worker (micro-batch of items per query page) |
| **Retry / backoff sweep** | Requeue failed attempts under policy | **1–5 min** or event-driven on status transitions |
| **Compaction / TTL** | DynamoDB TTL on execution items | **Managed by DynamoDB** (no explicit cron) |
| **End-of-day reconciliation** | Detect stuck `running` without heartbeat | **Hourly** batch |

---

### Consumer services (UC 2.1)

| Service | Responsibility |
|---------|----------------|
| **Scheduler / orchestrator** | Computes due work, writes execution queue items (idempotent enqueue). |
| **Job runner / worker pool** | Polls or consumes messages, **loads job definition from PostgreSQL with strong consistency** before execution (see cross–use case note in [`consistency-requirements.md`](consistency-requirements.md)). |
| **Heartbeat / registry service** | Worker liveness (worker registry in the domain model). |
| **Observability pipeline** (optional stream) | Metrics, tracing, alerting from execution lifecycle events. |

---

### Messaging infrastructure (UC 2.1)

**Baseline (coursework direction):** **DynamoDB as the queue primitive** — no separate broker is required for correctness of claim and status; “messaging” is **implicit** in table items + GSIs.

**When to add a dedicated broker or stream:**

| Option | Fit | Configuration sketch |
|--------|-----|----------------------|
| **Amazon SQS** | Simple decoupling of “due work” from “runner fleet”; visibility timeout ≈ max job duration; **DLQ** for poison messages; **FIFO** only if strict global ordering per job is required (often unnecessary). | Queue `execution-dispatch`; **visibility timeout** 5–15 min (tunable); **long polling** 20 s; **redrive policy** to DLQ. |
| **Apache Kafka** | High throughput, multiple subscribers (billing, audit, runners), replay | Topic `execution.lifecycle` or `execution.commands`; **partitions** by `jobId` or hash of `executionId` for parallelism; **group** e.g. `runners-pool-1`; idempotent consumer + offset store; **retention** bounded if the database remains the system of record for claims. |
| **Amazon EventBridge** | **Time-based rules** (cron) invoking Lambda/Step Functions to **enqueue** | Rule per tenant tier or single rule invoking batch enqueue; **dead-letter** on failed targets. |
| **Redis Streams / RabbitMQ** | Smaller deployments, low-latency dispatch | Stream `execution:pending`; consumer groups per runner deployment. |

**Partitions / consumer groups (if using Kafka for dispatch):**

- **Partition key:** `jobId` if you must serialize all runs of the same job; **`executionId`** if maximum parallelism across jobs matters and per-job serialization is enforced in DynamoDB/SQL instead.
- **Consumer groups:** separate groups for **runners** vs **analytics** so each reads the full stream independently.
- **Ordering:** guaranteed **only within a partition**; global order across all jobs is usually unnecessary.
---
