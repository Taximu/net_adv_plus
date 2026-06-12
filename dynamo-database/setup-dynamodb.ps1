# =========================================
# DynamoDB Setup for UC 2.1
# Executions Queue + Worker Nodes
# Aligns with: db_scripts/Schema2_Job_Execution_Queue(NoSQL - DynamoDB).json
# =========================================

Write-Host "Setting up DynamoDB Tables for UC 2.1" -ForegroundColor Cyan

$awsCheck = Get-Command aws -ErrorAction SilentlyContinue
if (-not $awsCheck) {
    Write-Host "ERROR: AWS CLI not found. Please install AWS CLI first." -ForegroundColor Red
    exit 1
}

$region = "us-east-1"
$null = aws sts get-caller-identity --query "Account" --output text 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: AWS credentials not configured or sts:AssumeRole failed." -ForegroundColor Red
    exit 1
}
$accountId = aws sts get-caller-identity --query "Account" --output text

Write-Host "Region: $region" -ForegroundColor Gray
Write-Host "Account: $accountId" -ForegroundColor Gray

# Unix epoch seconds for TTL: items expire ~48h from now (DynamoDB deletes after this time)
$unixEpoch = [datetime]::new(1970, 1, 1, 0, 0, 0, [DateTimeKind]::Utc)
$ttlEpoch = [string][int64]([datetime]::UtcNow - $unixEpoch).TotalSeconds + (48 * 3600)

function Test-DynamoTableExists {
    param([string]$TableName)
    aws dynamodb describe-table --table-name $TableName --region $region 2>$null | Out-Null
    return ($LASTEXITCODE -eq 0)
}

# =========================================
# Table 1: ExecutionQueue
# =========================================

Write-Host "`n[1/2] ExecutionQueue table..." -ForegroundColor Yellow

if (Test-DynamoTableExists -TableName "ExecutionQueue") {
    Write-Host "  ExecutionQueue already exists — skipping create-table." -ForegroundColor Yellow
}
else {
    # Every attribute used in table or GSI KeySchema must appear in attribute-definitions
    aws dynamodb create-table `
        --table-name ExecutionQueue `
        --attribute-definitions `
            AttributeName=queueId,AttributeType=S `
            AttributeName=scheduledFor,AttributeType=S `
            AttributeName=queueStatus,AttributeType=S `
            AttributeName=priority,AttributeType=N `
            AttributeName=assignedWorkerId,AttributeType=S `
            AttributeName=assignedAt,AttributeType=S `
            AttributeName=jobId,AttributeType=S `
        --key-schema `
            AttributeName=queueId,KeyType=HASH `
            AttributeName=scheduledFor,KeyType=RANGE `
        --global-secondary-indexes `
            "[
                {
                    `"IndexName`": `"PendingExecutionsIndex`",
                    `"KeySchema`": [
                        {`"AttributeName`":`"queueStatus`",`"KeyType`":`"HASH`"},
                        {`"AttributeName`":`"priority`",`"KeyType`":`"RANGE`"}
                    ],
                    `"Projection`": {
                        `"ProjectionType`":`"INCLUDE`",
                        `"NonKeyAttributes`":[`"jobId`",`"scheduleId`",`"scheduledFor`",`"executionContext`"]
                    }
                },
                {
                    `"IndexName`": `"WorkerAssignmentsIndex`",
                    `"KeySchema`": [
                        {`"AttributeName`":`"assignedWorkerId`",`"KeyType`":`"HASH`"},
                        {`"AttributeName`":`"assignedAt`",`"KeyType`":`"RANGE`"}
                    ],
                    `"Projection`": {
                        `"ProjectionType`":`"ALL`"
                    }
                },
                {
                    `"IndexName`": `"JobExecutionsIndex`",
                    `"KeySchema`": [
                        {`"AttributeName`":`"jobId`",`"KeyType`":`"HASH`"},
                        {`"AttributeName`":`"scheduledFor`",`"KeyType`":`"RANGE`"}
                    ],
                    `"Projection`": {
                        `"ProjectionType`":`"ALL`"
                    }
                }
            ]" `
        --billing-mode PAY_PER_REQUEST `
        --region $region `
        --deletion-protection-enabled

    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: create-table ExecutionQueue failed." -ForegroundColor Red
        exit 1
    }

    Write-Host "  Waiting until ExecutionQueue is ACTIVE..." -ForegroundColor Gray
    aws dynamodb wait table-exists --table-name ExecutionQueue --region $region
    Write-Host "  ExecutionQueue table created" -ForegroundColor Green
}

# =========================================
# Table 2: WorkerNodes
# =========================================

Write-Host "`n[2/2] WorkerNodes table..." -ForegroundColor Yellow

if (Test-DynamoTableExists -TableName "WorkerNodes") {
    Write-Host "  WorkerNodes already exists — skipping create-table." -ForegroundColor Yellow
}
else {
    aws dynamodb create-table `
        --table-name WorkerNodes `
        --attribute-definitions `
            AttributeName=workerId,AttributeType=S `
            AttributeName=registeredAt,AttributeType=S `
        --key-schema `
            AttributeName=workerId,KeyType=HASH `
            AttributeName=registeredAt,KeyType=RANGE `
        --billing-mode PAY_PER_REQUEST `
        --region $region `
        --deletion-protection-enabled

    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: create-table WorkerNodes failed." -ForegroundColor Red
        exit 1
    }

    Write-Host "  Waiting until WorkerNodes is ACTIVE..." -ForegroundColor Gray
    aws dynamodb wait table-exists --table-name WorkerNodes --region $region
    Write-Host "  WorkerNodes table created" -ForegroundColor Green
}

# =========================================
# Enable TTL on ExecutionQueue
# =========================================

Write-Host "`nEnabling TTL on ExecutionQueue (attribute: ttl)..." -ForegroundColor Yellow

aws dynamodb update-time-to-live `
    --table-name ExecutionQueue `
    --time-to-live-specification "Enabled=true, AttributeName=ttl" `
    --region $region

if ($LASTEXITCODE -ne 0) {
    Write-Host "WARNING: update-time-to-live failed (may already be enabled)." -ForegroundColor Yellow
}
else {
    Write-Host "  TTL enabled (items use attribute ttl = epoch seconds)" -ForegroundColor Green
}

# =========================================
# Enable Point-in-Time Recovery
# =========================================

Write-Host "`nEnabling Point-in-Time Recovery..." -ForegroundColor Yellow

aws dynamodb update-continuous-backups `
    --table-name ExecutionQueue `
    --point-in-time-recovery-specification "PointInTimeRecoveryEnabled=true" `
    --region $region

aws dynamodb update-continuous-backups `
    --table-name WorkerNodes `
    --point-in-time-recovery-specification "PointInTimeRecoveryEnabled=true" `
    --region $region

Write-Host "  Point-in-Time Recovery enabled (or already on)" -ForegroundColor Green

# =========================================
# Minimal Data Seeding
# =========================================

Write-Host "`nSeeding minimal test data (TTL = $ttlEpoch, ~48h from UTC now)..." -ForegroundColor Yellow

$pendingItem = @"
{
    "queueId": {"S": "queue-001"},
    "scheduledFor": {"S": "2026-06-02T14:00:00Z"},
    "jobId": {"S": "job-001"},
    "scheduleId": {"S": "schedule-001"},
    "queueStatus": {"S": "pending"},
    "priority": {"N": "5"},
    "retryCount": {"N": "0"},
    "maxRetries": {"N": "3"},
    "executionContext": {"M": {
        "environment": {"S": "production"},
        "triggerSource": {"S": "schedule"},
        "userId": {"S": "user-001"}
    }},
    "ttl": {"N": "$ttlEpoch"}
}
"@

$assignedItem = @"
{
    "queueId": {"S": "queue-002"},
    "scheduledFor": {"S": "2026-06-02T13:00:00Z"},
    "jobId": {"S": "job-002"},
    "scheduleId": {"S": "schedule-002"},
    "queueStatus": {"S": "assigned"},
    "assignedWorkerId": {"S": "worker-001"},
    "assignedAt": {"S": "2026-06-02T12:55:00Z"},
    "priority": {"N": "3"},
    "retryCount": {"N": "1"},
    "maxRetries": {"N": "3"},
    "ttl": {"N": "$ttlEpoch"}
}
"@

$completedItem = @"
{
    "queueId": {"S": "queue-003"},
    "scheduledFor": {"S": "2026-06-02T10:00:00Z"},
    "jobId": {"S": "job-001"},
    "scheduleId": {"S": "schedule-001"},
    "queueStatus": {"S": "completed"},
    "priority": {"N": "5"},
    "retryCount": {"N": "0"},
    "maxRetries": {"N": "3"},
    "startedAt": {"S": "2026-06-02T10:00:00Z"},
    "completedAt": {"S": "2026-06-02T10:00:30Z"},
    "executionResult": {"M": {
        "status": {"S": "success"},
        "statusCode": {"N": "200"}
    }},
    "ttl": {"N": "$ttlEpoch"}
}
"@

$workerItem = @"
{
    "workerId": {"S": "worker-001"},
    "registeredAt": {"S": "2026-06-02T12:00:00Z"},
    "workerType": {"S": "primary"},
    "instanceType": {"S": "t3.medium"},
    "availabilityZone": {"S": "us-east-1a"},
    "maxConcurrentJobs": {"N": "10"},
    "currentJobCount": {"N": "2"},
    "totalJobsProcessed": {"N": "1250"},
    "lastHeartbeat": {"S": "2026-06-02T13:30:00Z"},
    "status": {"S": "healthy"},
    "lastUpdatedAt": {"S": "2026-06-02T13:30:00Z"}
}
"@

aws dynamodb put-item --table-name ExecutionQueue --item $pendingItem --region $region
aws dynamodb put-item --table-name ExecutionQueue --item $assignedItem --region $region
aws dynamodb put-item --table-name ExecutionQueue --item $completedItem --region $region
aws dynamodb put-item --table-name WorkerNodes --item $workerItem --region $region

# =========================================
# Verification
# =========================================

Write-Host "`n=========================================" -ForegroundColor Green
Write-Host "VERIFICATION" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green

Write-Host "`n=== ExecutionQueue Items ===" -ForegroundColor Yellow
aws dynamodb scan --table-name ExecutionQueue --region $region --output table

Write-Host "`n=== WorkerNodes Items ===" -ForegroundColor Yellow
aws dynamodb scan --table-name WorkerNodes --region $region --output table

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "DynamoDB Setup Complete!" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Tables:" -ForegroundColor Yellow
Write-Host "  - ExecutionQueue (TTL on ttl, PITR, 3 GSIs: PendingExecutionsIndex, WorkerAssignmentsIndex, JobExecutionsIndex)" -ForegroundColor Gray
Write-Host "  - WorkerNodes (PITR)" -ForegroundColor Gray
Write-Host ""
Write-Host "Replication is automatic (DynamoDB default, multi-AZ within region)." -ForegroundColor Green
