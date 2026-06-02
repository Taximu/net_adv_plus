# =========================================
# Working PostgreSQL Setup with Data on All Instances
# Use this for DAL testing with read/write splitting
# =========================================

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Working PostgreSQL Setup" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

# Get the correct directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $ScriptDir) { $ScriptDir = "." }
Set-Location $ScriptDir

Write-Host "Working directory: $ScriptDir" -ForegroundColor Gray

# Step 1: Clean everything
Write-Host "[1/5] Cleaning up existing containers..." -ForegroundColor Yellow
docker-compose -f docker-compose-postgres-replication.yml down -v 2>$null

# Step 2: Start primary only
Write-Host "[2/5] Starting primary database..." -ForegroundColor Yellow
docker-compose -f docker-compose-postgres-replication.yml up -d postgres-primary

Write-Host "  Waiting for primary to initialize (30 seconds)..." -ForegroundColor Gray
Start-Sleep -Seconds 30

# Verify primary has data
$userCount = docker exec -u postgres postgres-primary psql -U job_admin -d job_config_db -t -c "SELECT COUNT(*) FROM users;" 2>$null
Write-Host "  Primary users count: $($userCount.Trim())" -ForegroundColor Green

# Step 3: Export data from primary
Write-Host "[3/5] Exporting schema and data from primary..." -ForegroundColor Yellow
docker exec -u postgres postgres-primary pg_dump -U job_admin -d job_config_db --schema-only > schema.sql
docker exec -u postgres postgres-primary pg_dump -U job_admin -d job_config_db --data-only > data.sql
Write-Host "  Export complete" -ForegroundColor Gray

# Step 4: Start standby and replicas
Write-Host "[4/5] Starting standby and replicas..." -ForegroundColor Yellow
docker-compose -f docker-compose-postgres-replication.yml up -d postgres-standby postgres-replica1 postgres-replica2

Write-Host "  Waiting for containers to start (15 seconds)..." -ForegroundColor Gray
Start-Sleep -Seconds 15

# Step 5: Restore data to standby and replicas
Write-Host "[5/5] Restoring data to standby and replicas..." -ForegroundColor Yellow

# Function to restore data to a container
function Restore-Data {
    param($ContainerName)
    Write-Host "  Restoring to $ContainerName..." -ForegroundColor Gray
    
    # Copy SQL files to container
    docker cp schema.sql ${ContainerName}:/tmp/schema.sql 2>$null
    docker cp data.sql ${ContainerName}:/tmp/data.sql 2>$null
    
    # Restore schema and data (ignore errors if tables already exist)
    docker exec -u postgres ${ContainerName} psql -U job_admin -d job_config_db -f /tmp/schema.sql 2>$null
    docker exec -u postgres ${ContainerName} psql -U job_admin -d job_config_db -f /tmp/data.sql 2>$null
    
    # Verify
    $count = docker exec -u postgres ${ContainerName} psql -U job_admin -d job_config_db -t -c "SELECT COUNT(*) FROM users;" 2>$null
    Write-Host "    $ContainerName users count: $($count.Trim())" -ForegroundColor Green
}

Restore-Data "postgres-standby"
Restore-Data "postgres-replica1"
Restore-Data "postgres-replica2"

# Clean up temporary files
Remove-Item schema.sql -Force -ErrorAction SilentlyContinue
Remove-Item data.sql -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "=========================================" -ForegroundColor Green
Write-Host "VERIFICATION" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Green

Write-Host ""
Write-Host "=== Data Counts (Primary) ===" -ForegroundColor Yellow
docker exec -u postgres postgres-primary psql -U job_admin -d job_config_db -c "SELECT COUNT(*) FROM users;"

Write-Host ""
Write-Host "=== Data Counts (Standby) ===" -ForegroundColor Yellow
docker exec -u postgres postgres-standby psql -U job_admin -d job_config_db -c "SELECT COUNT(*) FROM users;"

Write-Host ""
Write-Host "=== Data Counts (Replica1) ===" -ForegroundColor Yellow
docker exec -u postgres postgres-replica1 psql -U job_admin -d job_config_db -c "SELECT COUNT(*) FROM users;"

Write-Host ""
Write-Host "=== Data Counts (Replica2) ===" -ForegroundColor Yellow
docker exec -u postgres postgres-replica2 psql -U job_admin -d job_config_db -c "SELECT COUNT(*) FROM users;"

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Setup Complete!" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Connection Strings for DAL (appsettings.json):" -ForegroundColor Yellow
Write-Host '{'
Write-Host '  "Database": {'
Write-Host '    "PostgresWriteConnectionString": "Host=localhost;Port=5432;Database=job_config_db;Username=job_admin;Password=StrongP@ssw0rd",' -ForegroundColor Gray
Write-Host '    "PostgresReadConnectionStrings": [' -ForegroundColor Gray
Write-Host '      "Host=localhost;Port=5434;Database=job_config_db;Username=job_admin;Password=StrongP@ssw0rd",' -ForegroundColor Gray
Write-Host '      "Host=localhost;Port=5435;Database=job_config_db;Username=job_admin;Password=StrongP@ssw0rd"' -ForegroundColor Gray
Write-Host '    ]' -ForegroundColor Gray
Write-Host '  }' -ForegroundColor Gray
Write-Host '}' -ForegroundColor Gray
Write-Host ""
Write-Host "To stop all containers:" -ForegroundColor Yellow
Write-Host "  docker-compose -f docker-compose-postgres-replication.yml down" -ForegroundColor Gray