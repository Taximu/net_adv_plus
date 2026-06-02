# PostgreSQL replication integration tests

These tests assert **physical streaming replication**: the primary is not in recovery, standbys are in recovery, WAL replay makes writes visible on replicas, replicas reject writes, and standbys report a non-null `pg_last_wal_receive_lsn()`.

## Default: Testcontainers (no manual compose)

`dotnet test` on `PostgreSql.Replication.Tests` starts **three Bitnami PostgreSQL containers** on a private Docker network (same image and credentials as `docker-compose.streaming-for-tests.yml`). The fixture runs `docker pull` for the image first, then maps ephemeral host ports and sets `PG_*_CONNECTION_STRING` for the test process.

**Requirements**

- **Docker** daemon running (Docker Desktop or Linux engine).
- **`docker`** on `PATH` (used to pull images before container create).
- First run may take **several minutes** while images download.

**Optional: use your own stack instead**

Set **`REPLICATION_TESTS_USE_EXTERNAL=1`** to skip Testcontainers. Then supply connection strings (or rely on defaults that match the compose file below):

| Variable | Purpose |
|----------|---------|
| `PG_PRIMARY_CONNECTION_STRING` | Primary (writer) |
| `PG_REPLICA1_CONNECTION_STRING` | First replica |
| `PG_REPLICA2_CONNECTION_STRING` | Second replica |

Defaults match `docker-compose.streaming-for-tests.yml` (`job_admin` / `StrongTestPass123`, ports **5432 / 5434 / 5435**).

## Manual compose (optional, same as before)

`docker-compose-postgres-replication.yml` starts multiple Postgres instances, but **empty data directories plus `primary_conninfo` alone do not create streaming standbys** (you would need `pg_basebackup` / `standby.signal`, etc.). `setup-working.ps1` instead **restores dumps** to each node, so those nodes are **independent databases**, not WAL replicas.

For a **manual** replication stack aligned with the tests:

```bash
cd postgres-replication
docker compose -f docker-compose.streaming-for-tests.yml up -d
```

Wait until health checks are green (first boot often **60–90 seconds**). Free host ports **5432, 5434, 5435** first.

Images use **`bitnamilegacy/postgresql`** (pinned tag) because the short `bitnami/postgresql:15` tag is no longer published on Docker Hub for general use.

Then run with external mode:

```powershell
$env:REPLICATION_TESTS_USE_EXTERNAL = "1"
dotnet test tests/PostgreSql.Replication.Tests/PostgreSql.Replication.Tests.csproj --logger "console;verbosity=normal"
```

Tear down:

```bash
docker compose -f docker-compose.streaming-for-tests.yml down -v
```

## Tests (summary)

| Test | What it proves |
|------|----------------|
| `Primary_reports_not_in_recovery` | Writer is a primary |
| `Each_replica_reports_in_recovery` | Read nodes are standbys |
| `Insert_on_primary_eventually_visible_on_all_replicas` | WAL replay path works |
| `Replica_rejects_writes_to_permanent_tables` | Standby is read-only (SQLSTATE `25006`) |
| `Each_replica_reports_non_null_wal_receive_lsn` | Standbys report a WAL receive LSN (receiving from primary) |

If you later wire **true** streaming into `docker-compose-postgres-replication.yml`, point the same environment variables at those ports and these tests should pass unchanged (adjust passwords in env vars if they differ from the Bitnami test stack).
