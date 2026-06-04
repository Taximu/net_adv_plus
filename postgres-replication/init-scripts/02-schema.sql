-- Users Table
CREATE TABLE users (
    user_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email VARCHAR(255) UNIQUE NOT NULL,
    username VARCHAR(100) NOT NULL,
    full_name VARCHAR(255),
    role VARCHAR(50) NOT NULL DEFAULT 'user',
    status VARCHAR(20) NOT NULL DEFAULT 'active',
    preferences JSONB DEFAULT '{
        "timezone": "UTC",
        "notification_channels": ["email"],
        "default_retry_count": 3,
        "default_timeout_minutes": 60
    }',
    max_jobs INT DEFAULT 100,
    max_concurrent_jobs INT DEFAULT 10,
    jobs_created_count INT DEFAULT 0,
    password_hash VARCHAR(255) NOT NULL,
    mfa_enabled BOOLEAN DEFAULT FALSE,
    last_login_at TIMESTAMPTZ,
    api_key VARCHAR(255) UNIQUE,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ
);

-- Job Definitions Table
CREATE TABLE job_definitions (
    job_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL,
    name VARCHAR(200) NOT NULL,
    description TEXT,
    job_type VARCHAR(50) NOT NULL,
    category VARCHAR(100),
    api_endpoint TEXT,
    http_method VARCHAR(10),
    request_headers JSONB DEFAULT '{}',
    request_body_template TEXT,
    auth_type VARCHAR(50),
    auth_config JSONB,
    timeout_seconds INT DEFAULT 3600,
    max_retries INT DEFAULT 3,
    retry_backoff_multiplier DECIMAL(3,2) DEFAULT 1.5,
    retryable_status_codes INT[] DEFAULT '{500,502,503,504}',
    parent_job_id UUID,
    status VARCHAR(20) NOT NULL DEFAULT 'draft',
    version INT NOT NULL DEFAULT 1,
    tags TEXT[] DEFAULT '{}',
    metadata JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    created_by VARCHAR(100) NOT NULL,
    updated_by VARCHAR(100),
    FOREIGN KEY (user_id) REFERENCES users(user_id),
    FOREIGN KEY (parent_job_id) REFERENCES job_definitions(job_id),
    UNIQUE(user_id, name)
);

-- Job Parameters Table
CREATE TABLE job_parameters (
    parameter_id BIGSERIAL PRIMARY KEY,
    job_id UUID NOT NULL,
    parameter_set VARCHAR(50) NOT NULL DEFAULT 'default',
    parameter_name VARCHAR(100) NOT NULL,
    parameter_value TEXT NOT NULL,
    parameter_type VARCHAR(50) NOT NULL DEFAULT 'string',
    is_sensitive BOOLEAN DEFAULT FALSE,
    is_required BOOLEAN DEFAULT TRUE,
    description TEXT,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (job_id) REFERENCES job_definitions(job_id) ON DELETE CASCADE,
    UNIQUE(job_id, parameter_set, parameter_name)
);

-- Job Schedules Table (declarative partitioning: HASH by schedule_id, 4 partitions)
-- Rationale: partition key must appear in PK; schedule_id is the natural row id and spreads rows evenly for UUIDs.
CREATE TABLE job_schedules (
    schedule_id UUID NOT NULL DEFAULT gen_random_uuid(),
    job_id UUID NOT NULL,
    schedule_name VARCHAR(200) NOT NULL,
    schedule_type VARCHAR(20) NOT NULL,
    cron_expression VARCHAR(100),
    interval_seconds INT,
    timezone VARCHAR(50) DEFAULT 'UTC',
    start_date DATE NOT NULL,
    end_date DATE,
    start_time TIME,
    end_time TIME,
    is_enabled BOOLEAN DEFAULT TRUE,
    allow_overlap BOOLEAN DEFAULT FALSE,
    max_concurrent_executions INT DEFAULT 1,
    priority INT DEFAULT 5,
    next_execution_at TIMESTAMPTZ,
    last_execution_at TIMESTAMPTZ,
    execution_count INT DEFAULT 0,
    status VARCHAR(20) DEFAULT 'active',
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (job_id) REFERENCES job_definitions(job_id) ON DELETE CASCADE,
    PRIMARY KEY (schedule_id)
) PARTITION BY HASH (schedule_id);

CREATE TABLE job_schedules_p0 PARTITION OF job_schedules FOR VALUES WITH (MODULUS 4, REMAINDER 0);
CREATE TABLE job_schedules_p1 PARTITION OF job_schedules FOR VALUES WITH (MODULUS 4, REMAINDER 1);
CREATE TABLE job_schedules_p2 PARTITION OF job_schedules FOR VALUES WITH (MODULUS 4, REMAINDER 2);
CREATE TABLE job_schedules_p3 PARTITION OF job_schedules FOR VALUES WITH (MODULUS 4, REMAINDER 3);

-- Schedule Dependencies Table
CREATE TABLE schedule_dependencies (
    dependency_id BIGSERIAL PRIMARY KEY,
    schedule_id UUID NOT NULL,
    depends_on_schedule_id UUID NOT NULL,
    dependency_type VARCHAR(20) NOT NULL,
    wait_time_seconds INT DEFAULT 0,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (schedule_id) REFERENCES job_schedules(schedule_id) ON DELETE CASCADE,
    FOREIGN KEY (depends_on_schedule_id) REFERENCES job_schedules(schedule_id) ON DELETE CASCADE,
    CONSTRAINT no_self_dependency CHECK (schedule_id != depends_on_schedule_id)
);

-- Indexes
CREATE INDEX idx_users_email ON users(email);
CREATE INDEX idx_users_status ON users(status);
CREATE INDEX idx_job_definitions_user_id ON job_definitions(user_id);
CREATE INDEX idx_job_definitions_status ON job_definitions(status);
CREATE INDEX idx_job_definitions_job_type ON job_definitions(job_type);
CREATE INDEX idx_job_parameters_job_id ON job_parameters(job_id);
CREATE INDEX idx_job_schedules_next_execution ON job_schedules(next_execution_at) WHERE is_enabled = TRUE AND status = 'active';
CREATE INDEX idx_job_schedules_job_id ON job_schedules(job_id);
CREATE INDEX idx_job_schedules_enabled_status ON job_schedules(is_enabled, status, next_execution_at);