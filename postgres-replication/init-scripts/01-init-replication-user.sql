-- Idempotent: Bitnami primary also creates POSTGRESQL_REPLICATION_USER before init scripts run.
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_catalog.pg_roles WHERE rolname = 'replicator') THEN
    CREATE USER replicator WITH REPLICATION ENCRYPTED PASSWORD 'ReplicaPass123';
  END IF;
END
$$;

GRANT CONNECT ON DATABASE job_config_db TO replicator;
GRANT USAGE ON SCHEMA public TO replicator;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO replicator;