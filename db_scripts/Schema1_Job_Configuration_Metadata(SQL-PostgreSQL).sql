-- Database: job_config_db
-- Service: Job Manager
-- Primary Use Case: UC 1.1 - Create a New Job

-- Users Table
CREATE TABLE users (
    user_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email VARCHAR(255) UNIQUE NOT NULL,
    username VARCHAR(100) NOT NULL,
    full_name VARCHAR(255),
    role VARCHAR(50) NOT NULL DEFAULT 'user', -- 'admin', 'user', 'viewer'
    status VARCHAR(20) NOT NULL DEFAULT 'active', -- 'active', 'inactive', 'suspended'
    
    -- Preferences (stored as JSONB for flexibility)
    preferences JSONB DEFAULT '{
        "timezone": "UTC",
        "notification_channels": ["email"],
        "default_retry_count": 3,
        "default_timeout_minutes": 60
    }',
    
    -- Quotas
    max_jobs INT DEFAULT 100,
    max_concurrent_jobs INT DEFAULT 10,
    jobs_created_count INT DEFAULT 0,
    
    -- Security
    password_hash VARCHAR(255) NOT NULL,
    mfa_enabled BOOLEAN DEFAULT FALSE,
    last_login_at TIMESTAMPTZ,
    api_key VARCHAR(255) UNIQUE,
    
    -- Audit
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMPTZ,
    
    -- Indexes
    INDEX idx_users_email (email),
    INDEX idx_users_status (status),
    INDEX idx_users_created_at (created_at)
);

-- Job Definitions Table
CREATE TABLE job_definitions (
    job_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL,
    name VARCHAR(200) NOT NULL,
    description TEXT,
    job_type VARCHAR(50) NOT NULL, -- 'api_call', 'data_pipeline', 'report', 'etl'
    category VARCHAR(100), -- 'marketing', 'finance', 'operations'
    
    -- API Configuration (for API jobs)
    api_endpoint TEXT,
    http_method VARCHAR(10), -- 'GET', 'POST', 'PUT', 'DELETE', 'PATCH'
    request_headers JSONB DEFAULT '{}',
    request_body_template TEXT,
    auth_type VARCHAR(50), -- 'none', 'api_key', 'oauth2', 'bearer'
    auth_config JSONB, -- Encrypted configuration
    
    -- Execution Configuration
    timeout_seconds INT DEFAULT 3600,
    max_retries INT DEFAULT 3,
    retry_backoff_multiplier DECIMAL(3,2) DEFAULT 1.5,
    retryable_status_codes INT[] DEFAULT '{500, 502, 503, 504}',
    
    -- Dependencies (for job chains)
    parent_job_id UUID,
    
    -- Status
    status VARCHAR(20) NOT NULL DEFAULT 'draft', -- 'draft', 'active', 'paused', 'archived'
    version INT NOT NULL DEFAULT 1,
    
    -- Metadata
    tags TEXT[] DEFAULT '{}',
    metadata JSONB DEFAULT '{}',
    
    -- Audit
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    created_by VARCHAR(100) NOT NULL,
    updated_by VARCHAR(100),
    
    -- Foreign Keys
    FOREIGN KEY (user_id) REFERENCES users(user_id),
    FOREIGN KEY (parent_job_id) REFERENCES job_definitions(job_id),
    
    -- Indexes
    INDEX idx_job_definitions_user_id (user_id),
    INDEX idx_job_definitions_status (status),
    INDEX idx_job_definitions_job_type (job_type),
    INDEX idx_job_definitions_created_at (created_at),
    INDEX idx_job_definitions_tags USING GIN (tags),
    UNIQUE (user_id, name) -- Unique job name per user
);

-- Job Parameters Table
CREATE TABLE job_parameters (
    parameter_id BIGSERIAL PRIMARY KEY,
    job_id UUID NOT NULL,
    parameter_set VARCHAR(50) NOT NULL DEFAULT 'default', -- 'default', 'production', 'test'
    parameter_name VARCHAR(100) NOT NULL,
    parameter_value TEXT NOT NULL,
    parameter_type VARCHAR(50) NOT NULL DEFAULT 'string', -- 'string', 'number', 'boolean', 'json'
    is_sensitive BOOLEAN DEFAULT FALSE,
    is_required BOOLEAN DEFAULT TRUE,
    description TEXT,
    
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    
    -- Foreign Key
    FOREIGN KEY (job_id) REFERENCES job_definitions(job_id) ON DELETE CASCADE,
    
    -- Indexes
    INDEX idx_job_parameters_job_id (job_id),
    INDEX idx_job_parameters_parameter_set (parameter_set),
    UNIQUE (job_id, parameter_set, parameter_name)
);

-- Job Schedules Table
CREATE TABLE job_schedules (
    schedule_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    job_id UUID NOT NULL,
    schedule_name VARCHAR(200) NOT NULL,
    
    -- Schedule Configuration
    schedule_type VARCHAR(20) NOT NULL, -- 'cron', 'interval', 'once'
    cron_expression VARCHAR(100), -- For cron schedules
    interval_seconds INT, -- For interval schedules
    timezone VARCHAR(50) DEFAULT 'UTC',
    
    -- Execution Window
    start_date DATE NOT NULL,
    end_date DATE,
    start_time TIME,
    end_time TIME,
    
    -- Advanced Settings
    is_enabled BOOLEAN DEFAULT TRUE,
    allow_overlap BOOLEAN DEFAULT FALSE,
    max_concurrent_executions INT DEFAULT 1,
    priority INT DEFAULT 5, -- 1-10 (1 = highest)
    
    -- Next execution tracking
    next_execution_at TIMESTAMPTZ,
    last_execution_at TIMESTAMPTZ,
    execution_count INT DEFAULT 0,
    
    -- Status
    status VARCHAR(20) DEFAULT 'active', -- 'active', 'paused', 'completed'
    
    -- Audit
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    
    -- Foreign Key
    FOREIGN KEY (job_id) REFERENCES job_definitions(job_id) ON DELETE CASCADE,
    
    -- Indexes (optimized for UC 2.1)
    INDEX idx_job_schedules_next_execution 
        (next_execution_at) 
        WHERE is_enabled = TRUE AND status = 'active',
    INDEX idx_job_schedules_job_id (job_id),
    INDEX idx_job_schedules_status (status),
    INDEX idx_job_schedules_enabled_status 
        (is_enabled, status, next_execution_at)
);

-- Schedule Dependencies Table
CREATE TABLE schedule_dependencies (
    dependency_id BIGSERIAL PRIMARY KEY,
    schedule_id UUID NOT NULL,
    depends_on_schedule_id UUID NOT NULL,
    dependency_type VARCHAR(20) NOT NULL, -- 'success', 'completion', 'any'
    wait_time_seconds INT DEFAULT 0,
    
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    
    -- Foreign Keys
    FOREIGN KEY (schedule_id) REFERENCES job_schedules(schedule_id) ON DELETE CASCADE,
    FOREIGN KEY (depends_on_schedule_id) REFERENCES job_schedules(schedule_id) ON DELETE CASCADE,
    
    -- Constraints
    CONSTRAINT no_self_dependency CHECK (schedule_id != depends_on_schedule_id),
    
    -- Indexes
    INDEX idx_schedule_dependencies_schedule_id (schedule_id),
    INDEX idx_schedule_dependencies_depends_on (depends_on_schedule_id)
);