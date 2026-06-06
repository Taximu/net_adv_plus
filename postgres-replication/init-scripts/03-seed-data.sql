-- Users (3 records)
INSERT INTO users (user_id, email, username, full_name, role, password_hash, api_key, last_login_at) VALUES
(gen_random_uuid(), 'admin@jobscheduler.com', 'admin', 'Admin User', 'admin', 'hash_admin_123', 'ak_admin_001', NOW()),
(gen_random_uuid(), 'alice@example.com', 'alice', 'Alice Johnson', 'user', 'hash_alice_456', 'ak_alice_002', NOW()),
(gen_random_uuid(), 'bob@example.com', 'bob', 'Bob Smith', 'user', 'hash_bob_789', 'ak_bob_003', NULL);

-- Job Definitions (3 records)
INSERT INTO job_definitions (job_id, user_id, name, description, job_type, api_endpoint, http_method, status, created_by) VALUES
(gen_random_uuid(), (SELECT user_id FROM users WHERE username = 'admin'), 'Daily Sales Report', 'Generates daily sales summary', 'report', 'https://api.internal/sales/daily', 'GET', 'active', 'admin'),
(gen_random_uuid(), (SELECT user_id FROM users WHERE username = 'alice'), 'Data Backup Job', 'Backs up database to S3', 'data_pipeline', 'https://api.backup.com/run', 'POST', 'active', 'alice'),
(gen_random_uuid(), (SELECT user_id FROM users WHERE username = 'bob'), 'Health Check', 'Pings internal service', 'api_call', 'https://internal.health/ping', 'GET', 'draft', 'bob');

-- Job Parameters (5 records)
INSERT INTO job_parameters (job_id, parameter_set, parameter_name, parameter_value, parameter_type) VALUES
((SELECT job_id FROM job_definitions WHERE name = 'Data Backup Job'), 'default', 'bucket_name', 'my-backup-bucket', 'string'),
((SELECT job_id FROM job_definitions WHERE name = 'Data Backup Job'), 'default', 'retention_days', '30', 'number'),
((SELECT job_id FROM job_definitions WHERE name = 'Data Backup Job'), 'default', 'compression', 'true', 'boolean'),
((SELECT job_id FROM job_definitions WHERE name = 'Daily Sales Report'), 'default', 'date_range', 'yesterday', 'string'),
((SELECT job_id FROM job_definitions WHERE name = 'Daily Sales Report'), 'default', 'format', 'json', 'string');

-- Job Schedules (4 records)
INSERT INTO job_schedules (schedule_id, job_id, schedule_name, schedule_type, cron_expression, timezone, start_date, is_enabled, next_execution_at, priority) VALUES
(gen_random_uuid(), (SELECT job_id FROM job_definitions WHERE name = 'Daily Sales Report'), 'Daily at 2am', 'cron', '0 2 * * *', 'UTC', CURRENT_DATE, TRUE, CURRENT_TIMESTAMP + INTERVAL '1 hour', 5),
(gen_random_uuid(), (SELECT job_id FROM job_definitions WHERE name = 'Daily Sales Report'), 'Weekly on Monday', 'cron', '0 9 * * 1', 'UTC', CURRENT_DATE, TRUE, CURRENT_TIMESTAMP + INTERVAL '2 hours', 3),
(gen_random_uuid(), (SELECT job_id FROM job_definitions WHERE name = 'Data Backup Job'), 'Every 6 hours', 'interval', NULL, 'UTC', CURRENT_DATE, TRUE, CURRENT_TIMESTAMP + INTERVAL '30 minutes', 4),
(gen_random_uuid(), (SELECT job_id FROM job_definitions WHERE name = 'Health Check'), 'Every minute', 'interval', NULL, 'UTC', CURRENT_DATE, FALSE, CURRENT_TIMESTAMP + INTERVAL '1 minute', 1);

-- Schedule Dependencies (2 records)
INSERT INTO schedule_dependencies (schedule_id, depends_on_schedule_id, dependency_type, wait_time_seconds) VALUES
(
    (SELECT schedule_id FROM job_schedules WHERE schedule_name = 'Daily at 2am' LIMIT 1),
    (SELECT schedule_id FROM job_schedules WHERE schedule_name = 'Every 6 hours' LIMIT 1),
    'success',
    300
),
(
    (SELECT schedule_id FROM job_schedules WHERE schedule_name = 'Weekly on Monday' LIMIT 1),
    (SELECT schedule_id FROM job_schedules WHERE schedule_name = 'Daily at 2am' LIMIT 1),
    'completion',
    0
);

-- Bulk schedules (72 rows) to demonstrate HASH(schedule_id) spread across 4 physical partitions
INSERT INTO job_schedules (schedule_id, job_id, schedule_name, schedule_type, interval_seconds, timezone, start_date, is_enabled, next_execution_at, priority)
SELECT
    gen_random_uuid(),
    jd.job_id,
    'Partition demo schedule #' || n::TEXT,
    'interval',
    1800 + (n % 120),
    'UTC',
    CURRENT_DATE + (n % 60),
    TRUE,
    CURRENT_TIMESTAMP + (n || ' minutes')::INTERVAL,
    1 + (n % 9)
FROM generate_series(1, 72) AS n
CROSS JOIN LATERAL (
    SELECT job_id
    FROM job_definitions
    ORDER BY name
    LIMIT 1 OFFSET (n % 3)
) AS jd;