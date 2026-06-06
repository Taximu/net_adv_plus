#!/usr/bin/env bash
# Official postgres image runs initdb on an empty PGDATA, which never becomes a streaming standby.
# This entrypoint runs pg_basebackup once, then starts postgres as a physical replica.
set -euo pipefail

: "${PGDATA:=/var/lib/postgresql/data}"

if [[ -f "${PGDATA}/PG_VERSION" && ! -f "${PGDATA}/standby.signal" ]]; then
  echo "replica-entrypoint: ${PGDATA} is a standalone cluster (initdb), not a replica (missing standby.signal)." >&2
  echo "replica-entrypoint: Remove volumes and recreate: docker compose -f docker-compose-postgres-replication.yml down -v" >&2
  exit 1
fi

if [[ ! -f "${PGDATA}/PG_VERSION" ]]; then
  echo "replica-entrypoint: waiting for primary init (postgres-primary, ${POSTGRES_DB:-job_config_db}, public.users) ..."
  for ((i = 1; i <= 180; i++)); do
    if PGPASSWORD="${POSTGRES_PASSWORD}" psql -h postgres-primary -p 5432 -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" \
      -tAc 'select 1 from public.users limit 1' 2>/dev/null | grep -qx 1; then
      echo "replica-entrypoint: primary schema is ready."
      break
    fi
    if ((i == 180)); then
      echo "replica-entrypoint: timeout waiting for primary init." >&2
      exit 1
    fi
    sleep 2
  done

  mkdir -p "${PGDATA}"
  chown postgres:postgres "${PGDATA}"
  chmod 0700 "${PGDATA}"

  echo "replica-entrypoint: pg_basebackup from postgres-primary (first run can take several minutes) ..."
  gosu postgres env PGPASSWORD='ReplicaPass123' \
    pg_basebackup \
      -h postgres-primary \
      -p 5432 \
      -U replicator \
      -D "${PGDATA}" \
      -Fp \
      -Xs \
      -P \
      -R
fi

exec gosu postgres postgres "$@"
