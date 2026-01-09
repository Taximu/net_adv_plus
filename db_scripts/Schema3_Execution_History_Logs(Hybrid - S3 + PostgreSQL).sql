-- Database: job_history_db (PostgreSQL for metadata, S3 for logs)
-- Service: Job Reporter
-- Primary Use Case: UC 2.3 - View Job Execution History

-- Execution Metadata Table (PostgreSQL)
CREATE TABLE execution_metadata (
    execution_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    job_id UUID NOT NULL,
    schedule_id UUID,
    queue_id VARCHAR(100), -- Reference to DynamoDB ExecutionQueue
    
    -- Basic Info
    execution_type VARCHAR(20) NOT NULL, -- 'scheduled', 'manual', 'api'
    trigger_source VARCHAR(50), -- 'ui', 'api', 'webhook'
    
    -- Timing
    scheduled_at TIMESTAMPTZ,
    queued_at TIMESTAMPTZ,
    started_at TIMESTAMPTZ NOT NULL,
    completed_at TIMESTAMPTZ,
    
    -- Duration calculations (computed columns)
    queue_duration_ms INT GENERATED ALWAYS AS (
        CASE WHEN queued_at IS NOT NULL AND started_at IS NOT NULL
             THEN EXTRACT(EPOCH FROM (started_at - queued_at)) * 1000
             ELSE NULL
        END
    ) STORED,
    
    execution_duration_ms INT GENERATED ALWAYS AS (
        CASE WHEN started_at IS NOT NULL AND completed_at IS NOT NULL
             THEN EXTRACT(EPOCH FROM (completed_at - started_at)) * 1000
             ELSE NULL
        END
    ) STORED,
    
    -- Status
    status VARCHAR(20) NOT NULL, -- 'success', 'failure', 'timeout', 'cancelled'
    exit_code INT,
    
    -- Performance Metrics
    cpu_time_ms INT,
    memory_peak_mb DECIMAL(10,2),
    network_bytes_sent BIGINT,
    network_bytes_received BIGINT,
    io_read_bytes BIGINT,
    io_write_bytes BIGINT,
    
    -- External References
    s3_log_path TEXT, -- Path to S3 logs
    s3_output_path TEXT, -- Path to job outputs
    
    -- Worker Info
    worker_id VARCHAR(100),
    worker_type VARCHAR(50),
    
    -- Audit
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    
    -- Indexes for UC 2.3 queries
    INDEX idx_execution_metadata_job_id (job_id),
    INDEX idx_execution_metadata_schedule_id (schedule_id),
    INDEX idx_execution_metadata_started_at (started_at),
    INDEX idx_execution_metadata_status (status),
    INDEX idx_execution_metadata_job_status_time 
        (job_id, status, started_at),
    
    -- Partitioning by month for performance
    PARTITION BY RANGE (started_at)
);

-- Create monthly partitions
CREATE TABLE execution_metadata_2024_01 
    PARTITION OF execution_metadata
    FOR VALUES FROM ('2024-01-01') TO ('2024-02-01');

-- Execution Summary Table (pre-aggregated for dashboards)
CREATE TABLE execution_summary_daily (
    summary_date DATE NOT NULL,
    job_id UUID NOT NULL,
    user_id UUID NOT NULL,
    
    -- Aggregated metrics
    total_executions INT NOT NULL DEFAULT 0,
    successful_executions INT NOT NULL DEFAULT 0,
    failed_executions INT NOT NULL DEFAULT 0,
    timed_out_executions INT NOT NULL DEFAULT 0,
    
    -- Performance aggregates
    avg_execution_duration_ms DECIMAL(10,2),
    p95_execution_duration_ms DECIMAL(10,2),
    p99_execution_duration_ms DECIMAL(10,2),
    
    -- Error analysis
    most_common_error_code VARCHAR(100),
    most_common_error_message TEXT,
    error_count INT,
    
    -- Resource usage
    avg_cpu_time_ms DECIMAL(10,2),
    avg_memory_peak_mb DECIMAL(10,2),
    
    -- Timestamps
    last_execution_at TIMESTAMPTZ,
    first_execution_at TIMESTAMPTZ,
    
    -- Primary key
    PRIMARY KEY (summary_date, job_id),
    
    -- Foreign key (to job_definitions in config_db)
    FOREIGN KEY (job_id) REFERENCES job_config_db.job_definitions(job_id),
    FOREIGN KEY (user_id) REFERENCES job_config_db.users(user_id),
    
    -- Indexes
    INDEX idx_execution_summary_user_date (user_id, summary_date),
    INDEX idx_execution_summary_date (summary_date)
);

-- Error Patterns Table (for analytics)
CREATE TABLE error_patterns (
    pattern_id BIGSERIAL PRIMARY KEY,
    job_id UUID NOT NULL,
    error_code VARCHAR(100) NOT NULL,
    error_message_pattern TEXT NOT NULL, -- Regex pattern
    error_type VARCHAR(50) NOT NULL, -- 'network', 'authentication', 'timeout', 'validation'
    severity VARCHAR(20) NOT NULL, -- 'critical', 'warning', 'info'
    
    -- Pattern metadata
    first_seen_at TIMESTAMPTZ NOT NULL,
    last_seen_at TIMESTAMPTZ NOT NULL,
    occurrence_count INT DEFAULT 1,
    
    -- Resolution tracking
    is_acknowledged BOOLEAN DEFAULT FALSE,
    acknowledged_by VARCHAR(100),
    acknowledged_at TIMESTAMPTZ,
    resolution_notes TEXT,
    
    -- Indexes
    INDEX idx_error_patterns_job_id (job_id),
    INDEX idx_error_patterns_error_type (error_type),
    INDEX idx_error_patterns_severity (severity),
    INDEX idx_error_patterns_last_seen (last_seen_at)
);

-- S3 Log Structure (for actual log storage)
# s3://job-execution-logs/{year}/{month}/{day}/{hour}/
# ├── {execution_id}-stdout.log.gz
# ├── {execution_id}-stderr.log.gz
# ├── {execution_id}-metrics.json
# └── {execution_id}-output.json