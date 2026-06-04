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

## Manual compose (optional)

### `docker-compose-postgres-replication.yml` (app passwords + init-scripts)

Uses **official `postgres:15`** (not Bitnami): primary on **5432**, streaming replicas on **5434** and **5435**; `job_admin` / **`StrongP@ssw0rd`**; `./init-scripts` run on the **primary** only. Replicas use **`replica-entrypoint.sh`** (`pg_basebackup` + `standby.signal`), not initdb, so they match the primary. First boot can take **~60–120 seconds** while two replicas complete base backups. Init applies **`job_schedules` HASH partitioning** (four child tables) and seed data with **76** schedule rows for partition distribution demos — see **`JobScheduler.DAL/docs/partitioning-strategy.md`**.

```bash
cd postgres-replication
docker compose -f docker-compose-postgres-replication.yml up -d
```

Compose **`name: postgres-official-replication`** keeps volumes and state separate from the Bitnami streaming project.

`setup-working.ps1` recreates the stack and prints row counts when replicas are healthy.

**External tests** against this file: set `PG_PRIMARY_CONNECTION_STRING` and replica env vars to ports **5434** and **5435** with password **`StrongP@ssw0rd`**.

### `docker-compose.streaming-for-tests.yml` (integration-test credentials)

The **primary** bind-mounts only `01-init-replication-user.sql`, `02-schema.sql`, and `03-seed-data.sql` into `/docker-entrypoint-initdb.d` (not the whole `init-scripts` folder: `00-pg_hba-replication.sh` is for the official `postgres` image and **CRLF on Windows** breaks the shebang under Linux). Replicas do not run those scripts; they receive the same tables via streaming replication.

```bash
cd postgres-replication
docker compose -f docker-compose.streaming-for-tests.yml up -d
```

Wait until health checks are green (first boot often **60–90 seconds**). Free host ports **5432, 5434, 5435** first (stop the official stack or anything else bound there).

This file sets Compose **`name: postgres-streaming-for-tests`** so its project is separate from **`docker-compose-postgres-replication.yml`** (`name: postgres-official-replication`). Container names are generated from the project name (no fixed `container_name`), so logs look like: `docker compose -f docker-compose.streaming-for-tests.yml logs postgres-primary`.

**If the Bitnami primary exits during init** with `00-pg_hba-replication.sh` in the logs, Compose almost certainly **merged** this file with `docker-compose-postgres-replication.yml` (for example `COMPOSE_FILE` lists both YAMLs, or `docker compose -f A -f B`). That adds a bind mount of the **entire** `./init-scripts` directory; `00-pg_hba-replication.sh` is only for the **official** image. The script now **skips** under Bitnami, but you should still use **one** compose file per stack. Remove stale fixed-name containers if you see name conflicts: `docker rm -f postgres-primary-streaming` (legacy).

Images use **`bitnamilegacy/postgresql`** (pinned tag) because the short `bitnami/postgresql:15` tag is no longer published on Docker Hub for general use.

Then run with external mode:

```powershell
$env:REPLICATION_TESTS_USE_EXTERNAL = "1"
dotnet test tests/PostgreSql.Replication.Tests/PostgreSql.Replication.Tests.csproj --logger "console;verbosity=normal"
```

Tear down:

```bash
docker compose -f docker-compose.streaming-for-tests.yml down -v
# or
docker compose -f docker-compose-postgres-replication.yml down -v
```

## Tests (summary)

| Test | What it proves |
|------|----------------|
| `Primary_reports_not_in_recovery` | Writer is a primary |
| `Each_replica_reports_in_recovery` | Read nodes are standbys |
| `Insert_on_primary_eventually_visible_on_all_replicas` | WAL replay path works |
| `Replica_rejects_writes_to_permanent_tables` | Standby is read-only (SQLSTATE `25006`) |
| `Each_replica_reports_non_null_wal_receive_lsn` | Standbys report a WAL receive LSN (receiving from primary) |

Both compose files provide **true** streaming replication; use the env vars above with the password that matches the file you started (`StrongTestPass123` vs `StrongP@ssw0rd`).
