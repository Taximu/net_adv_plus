# Use Case 1.1: Create a New Job

**Affected entity:** Job (primary), User (read for validation)

## 1. Formulate system component requirements

### Expected data volume (number of records, data size in GB)

5–10 GB for ~1M jobs (job definition + associated metadata: JSONB fields, indexes, etc.).

### Expected load (read/write requests per second)

| Deployment size | Writes (creations/sec) | Reads (listings/sec) | Read:write ratio |
|:----------------|:-----------------------|:---------------------|:-----------------|
| Small (100k users) | 1–5 | 10–50 | ~10:1 |
| Medium (1M users) | 10–50 | 100–500 | ~10:1 |
| Large (10M users) | 100–500 | 1000–5000 | ~10:1 |

### Consistency requirements

- **Strong** for job creation and immediate read-after-create.
- **Eventual** for listings and analytics where brief lag is acceptable.

### Availability requirements

| Aspect | Value | Justification |
|--------|-------|----------------|
| Availability | 99.9% (~43 min/month) | Standard SaaS for customer-facing job management |
| Acceptable downtime | ≤ 43 min/month | Single outage < 1 h recoverable via retries |
| Peak hours | 99.5% during business hours | Degradation outside peak more tolerable |
| RPO | 5 minutes | Async read replicas → bounded staleness / loss window |
| RTO | 15 minutes | Automated failover to standby / promoted replica |

### Geographic distribution

Single region with **3 AZs** for HA. Multi-region optional for global users or data-sovereignty; single region is usually enough for ~99.9% at reasonable cost.

---

## 2. Select the most suitable database and justify your choice

**Choice: SQL — PostgreSQL**

| Question | Answer |
|----------|--------|
| **SQL or NoSQL?** | **SQL (PostgreSQL)** — ACID transactions, strong consistency, unique constraints, foreign keys, and richer queries fit job definitions and validation. NoSQL would push integrity and constraints into application code. |
| **Advantages** | ACID, referential integrity, JSONB, partial indexes, mature replication, predictable query model |
| **Disadvantages** | Schema migrations; read-replica lag (~100–500 ms); vertical scaling limits (acceptable at 1–500 writes/sec for this use case) |
| **Deployment** | **Cloud** — e.g. Amazon **RDS PostgreSQL**: Multi-AZ, backups, lower ops burden |
| **Configuration (example)** | PostgreSQL 15+, `db.t4g.large` or `db.r6g.large`, gp3, **Multi-AZ on**, **2 cross-AZ read replicas**, 30-day backups, deletion protection |

---

## 3. Design the replication strategy for the selected database

### Why replication is required

Read scalability for listings (10–5000 req/s depending on scale), **99.9%** availability, resilience to AZ failure, and isolating reporting/admin reads from the write path.

### Replication strategy (e.g. Amazon RDS PostgreSQL)

| Mechanism | Role |
|-----------|------|
| **Multi-AZ** | **Synchronous** standby in another AZ — **RPO ≈ 0** on failover, automatic promotion (~35–60 s). |
| **Read replicas** | **Asynchronous**, cross-AZ — primary does not wait; scales read-heavy dashboards and listings. |

### Configuration parameters

| Parameter | Setting |
|-----------|---------|
| Multi-AZ | Enabled (synchronous standby) |
| Read replicas | 2 (async, cross-AZ) |
| Backup retention | 30 days |
| Deletion protection | Enabled |
| Failover | Automatic to Multi-AZ standby |
| Replica lag target | < 500 ms (acceptable for eventual reads) |

### Read/write splitting

| Path | Target | Notes |
|------|--------|--------|
| INSERT / UPDATE / DELETE | **Primary** | All writes |
| Listings, dashboards, most reads | **Replicas** (e.g. round-robin) | Eventual consistency OK |
| Read-after-write (same session) | **Primary** | Avoids stale read right after create |

---

# Use Case 2.1: Execute a Job At a Scheduled Time

**Affected components:** Job Orchestrator + Job Runner (ExecutionQueue, WorkerNodes)

## 1. Formulate system component requirements

| Aspect | Value / justification |
|--------|------------------------|
| **Component** | Job Orchestrator + Job Runner (ExecutionQueue, WorkerNodes) |
| **Expected data volume** | ExecutionQueue: ~10M pending × ~0.5 KB ≈ **5 GB** active (TTL 48 h). WorkerNodes: 100–1000 workers × ~0.2 KB → negligible. |
| **Expected load** | **Reads:** 1000–2000 req/s (scan pending jobs). **Writes:** 500–1000 req/s (claims, status updates). |
| **Consistency** | **Strong** for claiming (conditional writes — no double execution). **Eventual** for scanning pending queue where throughput matters. |
| **Availability** | **99.95%** (~22 min/month) — missed executions hit the business directly. |
| **Geography** | Single region, **3 AZs** (typical for DynamoDB); cross-region optional for DR. |

---

## 2. Select the most suitable database and justify your choice

**Choice: NoSQL — Amazon DynamoDB**

| Question | Answer |
|----------|--------|
| **SQL or NoSQL?** | **NoSQL (DynamoDB)** — queue-shaped access patterns, very high read/write rates, TTL, and conditional updates without heavy transactional locking. |

### Why DynamoDB fits UC 2.1

| Requirement | Why DynamoDB | Why SQL is harder here |
|-------------|--------------|-------------------------|
| High read throughput (1000–2000 req/s) | Scales throughput without manual sharding | Many replicas + careful indexing and connection pooling |
| High write throughput (500–1000 req/s) | Predictable low latency at scale | Write scaling often needs sharding / partitioning design |
| TTL (48 h) | Built-in per-item TTL | Scheduled cleanup jobs + storage growth management |
| Job claiming | Native **conditional updates** (compare-and-set) | `SELECT … FOR UPDATE` / serializable patterns at scale |
| Access patterns | **GSI** e.g. pending by `queueStatus` + priority | Extra indexes and tuning per query pattern |
| Variable execution payload | Schema-flexible items | Frequent migrations or wide nullable columns |

### Advantages

| Advantage | Explanation |
|-------------|-------------|
| Auto-scaling | On-demand / provisioned modes adapt to load |
| Latency | Single-digit ms for typical key/GSI access |
| TTL | Automatic expiry of old execution records |
| Conditional writes | Safe claims without double execution |
| Multi-AZ | Data replicated across AZs by the service |
| Operations | Fully managed |
| GSI | Efficient “pending by status + priority” patterns |

### Disadvantages

| Disadvantage | Severity | Mitigation |
|--------------|----------|------------|
| No complex joins | Low | Model avoids joins for hot paths |
| Default reads eventual | Low | Use **strong** reads for claim / critical updates |
| Query patterns tied to keys/GSI | Medium | Design GSIs up front for all hot paths |
| Cost at very high scale | Low | On-demand / tuning; archiving cold data |

### Deployment: self-hosted vs cloud

| Aspect | DynamoDB (cloud) | Self-hosted alternative |
|--------|-------------------|-------------------------|
| Replication | Automatic across AZs | You design and operate |
| Scaling | Service-managed | Manual sharding / cluster ops |
| TTL | Native | Cron + deletes |
| Management | Low | High |
| SLA | AWS SLA | Your responsibility |

**Conclusion:** For this execution-queue use case, a managed NoSQL store (DynamoDB) is the practical default.

---

## 3. Design the replication strategy for the selected database

### Why replication is required (UC 2.1)

| Reason | Justification |
|--------|----------------|
| High availability | Queue must survive AZ failure (**99.95%**) |
| Read scalability | Many coordinated readers for pending scans |
| Durability | Replication across AZs limits data loss |
| No single point of failure | Automatic recovery within the region |

### How DynamoDB handles replication

DynamoDB **synchronously** replicates each write across **multiple AZs** in the region (internal quorum). You do not configure replica count like PostgreSQL; it is part of the service.

| Feature | Behavior |
|---------|----------|
| **Replication type** | Synchronous across AZs (internal quorum) |
| **Replica count** | Opaque (multi-AZ); not customer-tunable as “N replicas” |
| **Writes** | Quorum commit inside the service |
| **Reads** | **Eventual** by default; **strong** with `ConsistentRead=true` where needed |
| **Failover** | Automatic within the region |
| **Cross-region** | Optional (**Global Tables**); not required for single-region UC |

### Suggested table / account parameters

| Parameter | Setting | Justification |
|-----------|---------|----------------|
| Table class | Standard | Hot execution path, not IA |
| Billing | PAY_PER_REQUEST (or provisioned if stable) | Elastic load |
| PITR | Enabled (e.g. 35 days) | DR / mistakes |
| Encryption | KMS (customer-managed or AWS-owned per policy) | Compliance |
| Deletion protection | Enabled | Prevent accidental drop |
| TTL | Enabled (**48 h** on execution items) | Automatic trim |
| Streams | Optional `NEW_AND_OLD_IMAGES` | Audit / downstream triggers |

### Read/write consistency by operation

| Operation | Consistency | Why |
|-----------|---------------|-----|
| EnqueueAsync | Strong | Item visible for claiming immediately |
| TryClaimJobAsync | Strong | Prevent double execution |
| UpdateStatusAsync | Strong | Critical state transition |
| GetPendingJobsAsync (GSI) | Eventual | Throughput; small lag acceptable |
| GetByIdAsync | Eventual (default) | Less latency-sensitive |
| HeartbeatAsync | Strong | Avoid duplicate worker registration |

---

## 4. Setup database with replication (UC 2.1)

**DynamoDB — no manual replication layer.** Create tables (keys, GSIs, TTL attribute, streams if needed); replication and AZ placement are handled by AWS. Tune RCU/WCU or on-demand, alarms on throttles and age of oldest pending item.
