# =========================================
# DynamoDB Local Setup for Development
# Mirrors: setup-dynamodb.ps1 (minus AWS-only: PITR, deletion protection)
# Schema: db_scripts/Schema2_Job_Execution_Queue(NoSQL - DynamoDB).json
# =========================================

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $ScriptDir) { $ScriptDir = $PWD.Path }

Write-Host "Setting up DynamoDB Local..." -ForegroundColor Cyan

$composeFile = Join-Path $ScriptDir "docker-compose-dynamodb.yml"
if (-not (Test-Path $composeFile)) {
    Write-Host "ERROR: Missing $composeFile" -ForegroundColor Red
    exit 1
}

$dockerCompose = Get-Command docker -ErrorAction SilentlyContinue
if (-not $dockerCompose) {
    Write-Host "ERROR: Docker not found." -ForegroundColor Red
    exit 1
}

# Prefer Docker Compose V2 plugin
$useComposeV2 = $true
docker compose version 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) {
    $useComposeV2 = $false
}

Push-Location $ScriptDir
try {
    if ($useComposeV2) {
        docker compose -f $composeFile up -d
    }
    else {
        $dc = Get-Command docker-compose -ErrorAction SilentlyContinue
        if (-not $dc) {
            Write-Host "ERROR: Neither 'docker compose' nor 'docker-compose' is available." -ForegroundColor Red
            exit 1
        }
        docker-compose -f $composeFile up -d
    }
}
finally {
    Pop-Location
}

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Failed to start DynamoDB Local containers." -ForegroundColor Red
    exit 1
}

$endpointUrl = "http://localhost:8000"

Write-Host "Waiting for DynamoDB Local to accept connections..." -ForegroundColor Yellow
$ready = $false
for ($i = 0; $i -lt 30; $i++) {
    aws dynamodb list-tables --endpoint-url $endpointUrl 2>$null | Out-Null
    if ($LASTEXITCODE -eq 0) {
        $ready = $true
        break
    }
    Start-Sleep -Seconds 1
}
if (-not $ready) {
    Write-Host "ERROR: DynamoDB Local did not become ready at $endpointUrl" -ForegroundColor Red
    exit 1
}

$awsCheck = Get-Command aws -ErrorAction SilentlyContinue
if (-not $awsCheck) {
    Write-Host "ERROR: AWS CLI not found (needed to create tables against local endpoint)." -ForegroundColor Red
    exit 1
}

# TTL epoch ~48h from now (same semantics as cloud script)
$unixEpoch = [datetime]::new(1970, 1, 1, 0, 0, 0, [DateTimeKind]::Utc)
$ttlEpoch = [string][int64]([datetime]::UtcNow - $unixEpoch).TotalSeconds + (48 * 3600)

function Test-DynamoTableExistsLocal {
    param([string]$TableName)
    aws dynamodb describe-table --table-name $TableName --endpoint-url $endpointUrl 2>$null | Out-Null
    return ($LASTEXITCODE -eq 0)
}

# =========================================
# Table 1: ExecutionQueue
# =========================================

Write-Host "`n[1/2] ExecutionQueue table..." -ForegroundColor Yellow

if (Test-DynamoTableExistsLocal -TableName "ExecutionQueue") {
    Write-Host "  ExecutionQueue already exists — skipping create-table." -ForegroundColor Yellow
}
else {
    # All key attributes for base table + GSIs must be in attribute-definitions
    aws dynamodb create-table `
        --table-name ExecutionQueue `
        --attribute-definitions `
            AttributeName=queueId,AttributeType=S `
            AttributeName=scheduledFor,AttributeType=S `
            AttributeName=queueStatus,AttributeType=S `
            AttributeName=priority,AttributeType=N `
            AttributeName=assignedWorkerId,AttributeType=S `
            AttributeName=assignedAt,AttributeType=S `
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
                }
            ]" `
        --billing-mode PAY_PER_REQUEST `
        --endpoint-url $endpointUrl

    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: create-table ExecutionQueue failed." -ForegroundColor Red
        exit 1
    }

    aws dynamodb wait table-exists --table-name ExecutionQueue --endpoint-url $endpointUrl
    Write-Host "  ExecutionQueue created" -ForegroundColor Green
}

# =========================================
# Table 2: WorkerNodes
# =========================================

Write-Host "`n[2/2] WorkerNodes table..." -ForegroundColor Yellow

if (Test-DynamoTableExistsLocal -TableName "WorkerNodes") {
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
        --endpoint-url $endpointUrl

    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: create-table WorkerNodes failed." -ForegroundColor Red
        exit 1
    }

    aws dynamodb wait table-exists --table-name WorkerNodes --endpoint-url $endpointUrl
    Write-Host "  WorkerNodes created" -ForegroundColor Green
}

# =========================================
# TTL (supported on DynamoDB Local)
# =========================================

Write-Host "`nEnabling TTL on ExecutionQueue (attribute: ttl)..." -ForegroundColor Yellow
aws dynamodb update-time-to-live `
    --table-name ExecutionQueue `
    --time-to-live-specification "Enabled=true, AttributeName=ttl" `
    --endpoint-url $endpointUrl

if ($LASTEXITCODE -ne 0) {
    Write-Host "WARNING: update-time-to-live failed (may already be enabled)." -ForegroundColor Yellow
}

# =========================================
# Seed sample data (same shape as setup-dynamodb.ps1)
# =========================================

Write-Host "`nSeeding sample data (TTL = $ttlEpoch)..." -ForegroundColor Yellow

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

aws dynamodb put-item --table-name ExecutionQueue --item $pendingItem --endpoint-url $endpointUrl
aws dynamodb put-item --table-name ExecutionQueue --item $assignedItem --endpoint-url $endpointUrl
aws dynamodb put-item --table-name ExecutionQueue --item $completedItem --endpoint-url $endpointUrl
aws dynamodb put-item --table-name WorkerNodes --item $workerItem --endpoint-url $endpointUrl

Write-Host "`n=== ExecutionQueue (scan) ===" -ForegroundColor Yellow
aws dynamodb scan --table-name ExecutionQueue --endpoint-url $endpointUrl --output table

Write-Host "`n=== WorkerNodes (scan) ===" -ForegroundColor Yellow
aws dynamodb scan --table-name WorkerNodes --endpoint-url $endpointUrl --output table

Write-Host "`n=========================================" -ForegroundColor Green
Write-Host "DynamoDB Local setup complete!" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green
Write-Host "Endpoint: $endpointUrl" -ForegroundColor Gray
Write-Host "Admin UI: http://localhost:8001" -ForegroundColor Gray
Write-Host "Note: PITR and deletion protection are not used locally (AWS-only)." -ForegroundColor DarkGray
