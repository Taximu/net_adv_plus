# Partitioning / sharding strategy (PostgreSQL job configuration)

This document satisfies the Module **04_02** follow-on: strategy, justification, applied DDL, and how to verify row placement. The physical database layout from Module 04_02 remains a **single primary** with **streaming replicas** (`postgres-replication/docker-compose-postgres-replication.yml`); partitioning is implemented **inside that instance** on the hottest configuration table.

---

## 1. Data growth and access patterns

### Expected growth

| Area | Order of magnitude | Notes |
|------|--------------------|--------|
| **Users** | Low thousands to tens of thousands | B2B-style tenants; modest compared to schedules. |
| **Job definitions** | ~1–10 per power user, bounded by quotas (`max_jobs`) | Metadata rows, moderate width. |
| **Job schedules** | **Highest cardinality** | Each job can have many schedules (cron + intervals, environments); recurring inserts/updates from UI and automation. |
| **Parameters / dependencies** | Proportional to jobs and schedules | Smaller than schedule fan-out. |

**Rough rate (illustrative):** tens to low hundreds of **new or updated schedule rows per tenant per month**, and aggregate **hundreds to thousands of schedule rows per day** at scale across all tenants—dominated by `job_schedules` (and later execution history, which is out of scope for this schema file).

### Query patterns

1. **By job:** `WHERE job_id = @jobId` — list or edit all schedules for one job (common in the job editor).
2. **By schedule id:** `WHERE schedule_id = @id` — point reads/updates/deletes (repository default).
3. **Time-oriented:** `WHERE next_execution_at BETWEEN …` (and partial indexes on enabled/active) — scheduler/worker polling windows.
4. **User-anchored:** usually via `job_definitions.user_id`, not directly on `job_schedules`.

Schedules are append-heavy and updated on cadence changes; the table is the main **write** and **range-scan** target for “what runs next,” while **job_id** drives ownership queries.

---

## 2. Partitioning vs sharding

### Decision: **native table partitioning** (single database instance), not multi-instance sharding

| Approach | Fit for this project |
|----------|----------------------|
| **Partitioning (chosen)** | One PostgreSQL primary already holds the authoritative catalog; replicas replay the same WAL. Declarative partitioning splits **one logical table** into multiple on-disk child tables **without** separate connection pools or cross-shard queries. |
| **Sharding (multiple DB instances)** | Would require a **shard router**, **no foreign keys** across shards (or async reconciliation), and **distributed** job_id/schedule_id rules. Justified only when a **single** PostgreSQL instance can no longer hold working set + IOPS **and** operational complexity is accepted. |

**Shard count if we ever shard:** start with **4–8 logical shards** (power-of-two helps rebalancing), keyed by **`user_id` or `job_id`** so all schedules for one job stay collocated—exact count would follow load tests and instance size.

### Strategy and key (implemented)

- **Strategy:** **HASH** partitioning.
- **Partition key:** **`schedule_id` (UUID)**.
- **Number of partitions:** **4** (`job_schedules_p0` … `job_schedules_p3`), using `FOR VALUES WITH (MODULUS 4, REMAINDER k)`.

**Why HASH on `schedule_id` (vs RANGE on time)?**

- PostgreSQL requires every **PRIMARY KEY** and **UNIQUE** constraint to include **all** partition key columns.
- The natural row identifier is **`schedule_id`**. HASH(`schedule_id`) allows **`PRIMARY KEY (schedule_id)`** unchanged, so the DAL and FKs from `schedule_dependencies` stay simple.
- **RANGE** on `start_date` or `next_execution_at` would typically force a **composite primary key** (e.g. `(schedule_id, start_date)`), complicating ORM/Dapper models and lookups unless we always filter by the range key.
- UUIDs hash to a **uniform** distribution across four partitions, which is ideal for demo and for **write** spreading when many schedules are created in parallel.

**Trade-off:** `job_id` filters alone do not prune partitions; the planner may scan all four unless additional indexing or future **denormalized shard key** is introduced. For catalog-scale data under one instance, this is an acceptable balance; at extreme scale, consider **RANGE** by tenant or **LIST** by `user_id` bucket with a composite PK design.

---

## 3. Applied configuration (DDL)

Implemented in:

`postgres-replication/init-scripts/02-schema.sql`

Summary:

```sql
CREATE TABLE job_schedules ( ... , PRIMARY KEY (schedule_id) )
  PARTITION BY HASH (schedule_id);

CREATE TABLE job_schedules_p0 PARTITION OF job_schedules FOR VALUES WITH (MODULUS 4, REMAINDER 0);
-- … p1, p2, p3
```

Indexes declared on `job_schedules` are attached to each partition by PostgreSQL.

Seed volume (76 schedule rows: 4 narrative + 72 bulk) lives in:

`postgres-replication/init-scripts/03-seed-data.sql`

---

## 4. Verification queries (optional screenshot for coursework)

After `docker compose` brings up the primary (or any DB where the init scripts ran), run:

```sql
-- Row counts per physical partition (child table)
SELECT tableoid::regclass AS partition_name, COUNT(*) AS rows
FROM job_schedules
GROUP BY tableoid
ORDER BY 1;
```

Expect **non-zero counts in several partitions** (exact split varies with UUID hash).

```sql
SELECT COUNT(*) AS total_schedules FROM job_schedules;
-- Expect 76 with default seed (4 + 72).
```

### Catalog / UI (parent + partitions, without indexes)

`pg_class` lists **tables and indexes**. A name pattern like `job_schedules%` also matches **`job_schedules_pkey`** (the PK index, `relkind = 'i'`), which is why you may see only `job_schedules` and `job_schedules_pkey` unless you restrict `relkind` or use inheritance.

**Partition key on the parent:**

```sql
SELECT pg_get_partkeydef('public.job_schedules'::regclass);
```

**Parent → child partition names:**

```sql
SELECT parent.relname AS parent_table,
       child.relname  AS partition_table
FROM pg_inherits
JOIN pg_class parent ON parent.oid = inhparent
JOIN pg_class child  ON child.oid = inhrelid
JOIN pg_namespace n  ON n.oid = parent.relnamespace
WHERE n.nspname = 'public'
  AND parent.relname = 'job_schedules'
ORDER BY partition_table;
```

**Same idea in one query on `pg_class` (tables only: `p` = partitioned parent, `r` + `relispartition` = leaf partition):**

```sql
SELECT c.relname,
       CASE c.relkind
         WHEN 'p' THEN 'partitioned parent'
         WHEN 'r' THEN CASE WHEN c.relispartition THEN 'partition' ELSE 'ordinary table' END
       END AS object_type,
       CASE
         WHEN c.relkind = 'p' THEN pg_get_partkeydef(c.oid)
         WHEN c.relispartition THEN pg_get_expr(c.relpartbound, c.oid)
       END AS partition_def
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'public'
  AND c.relkind IN ('p', 'r')
  AND (c.relname = 'job_schedules' OR c.relname ~ '^job_schedules_p[0-9]+$')
ORDER BY object_type DESC, c.relname;
```

---

## 5. References in repo

| Artifact | Role |
|----------|------|
| `postgres-replication/init-scripts/02-schema.sql` | Partitioned `job_schedules` DDL |
| `postgres-replication/init-scripts/03-seed-data.sql` | 50+ schedule rows for distribution demo |
| `JobScheduler.DAL/README.md` | Link to this document |

No application connection-string change is required: the DAL still targets one database; partitioning is transparent to `SELECT`/`INSERT`/`UPDATE`/`DELETE` on `job_schedules`.

---

## 6. DAL (Module follow-on)

`IJobScheduleRepository` / `JobScheduleRepository`:

- **Partition key** for HASH partitioning is **`schedule_id`**. Optional parameters `schedulePartitionKey` on **GetById**, **Update**, and **Delete** must equal the row’s `schedule_id` when supplied; they bind explicitly in SQL as `@SchedulePartitionKey` so the predicate matches the partition key column. **GetById** / **GetByJobId** also take **`ConsistencyLevel`** for read routing (see **`consistency-requirements.md`**).
- **`GetByJobIdAsync`** still filters by `job_id` (not the HASH key); **Debug** logs include a **per–physical-partition row count** histogram for that query.
- After **Create**, **Update**, **GetById** (hit), and **Delete**, **Debug** logs include **`PhysicalPartition`** (PostgreSQL `tableoid::regclass`, e.g. `job_schedules_p2`). Enable **Debug** logging for category `JobScheduler.DAL.Repositories.JobScheduleRepository` (or raise minimum level) to see these entries.
- **Integration tests:** `JobScheduler.DAL.Postgres.Tests` → **`JobScheduleRepositoryPartitionLogTests`** captures those lines via **`ListCapturingLoggerProvider`** and asserts **`PhysicalPartition=job_schedules_p[0-3]`** and **GetByJobId** histogram markers (`dotnet test` on the Postgres test project).
