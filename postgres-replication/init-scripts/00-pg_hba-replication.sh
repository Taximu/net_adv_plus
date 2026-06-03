#!/usr/bin/env sh
# Allow physical replication (pg_hba "all" does not match replication connections).
# Bitnami uses POSTGRESQL_DATA_DIR; official postgres image uses PGDATA.
set -e
DATA_DIR="${POSTGRESQL_DATA_DIR:-${PGDATA:-}}"
if [ -z "$DATA_DIR" ] || [ ! -f "${DATA_DIR}/pg_hba.conf" ]; then
  echo "00-pg_hba-replication.sh: missing data dir or pg_hba.conf (DATA_DIR=${DATA_DIR:-empty})" >&2
  exit 1
fi
printf '\n# Streaming replication (local dev)\nhost replication replicator all scram-sha-256\n' >> "${DATA_DIR}/pg_hba.conf"
