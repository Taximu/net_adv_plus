# =========================================
# Start local PostgreSQL streaming replication stack
# (docker-compose-postgres-replication.yml — Bitnami primary + 3 read replicas)
# =========================================

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "PostgreSQL streaming replication (local)" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $ScriptDir) { $ScriptDir = "." }
Set-Location $ScriptDir
Write-Host "Working directory: $ScriptDir" -ForegroundColor Gray

$ComposeFile = "docker-compose-postgres-replication.yml"
$PgPassword = "StrongP@ssw0rd"

Write-Host "[1/3] Recreating stack (removes volumes)..." -ForegroundColor Yellow
docker compose -f $ComposeFile down -v 2>$null

Write-Host "[2/3] Starting all services..." -ForegroundColor Yellow
docker compose -f $ComposeFile up -d
if ($LASTEXITCODE -ne 0) {
    Write-Host "docker compose failed. If you use Docker Compose V1, run: docker-compose -f $ComposeFile up -d" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "[3/3] Waiting for replicas (first boot: three base backups, often 60–120s)..." -ForegroundColor Yellow
$deadline = (Get-Date).AddMinutes(4)
$healthy = $false
while ((Get-Date) -lt $deadline) {
    $r1 = docker inspect -f "{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}" postgres-replica1 2>$null
    $r2 = docker inspect -f "{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}" postgres-replica2 2>$null
    if ($r1 -eq "healthy" -and $r2 -eq "healthy") {
        $healthy = $true
        break
    }
    Start-Sleep -Seconds 5
}

if (-not $healthy) {
    Write-Host "Replicas not healthy yet. Check: docker compose -f $ComposeFile ps" -ForegroundColor Yellow
}

function Invoke-PrimarySql([string]$Sql) {
    docker exec postgres-primary bash -lc "export PGPASSWORD='$PgPassword' && psql -h 127.0.0.1 -U job_admin -d job_config_db -t -A -c `"$Sql`""
}

function Invoke-ReplicaSql([string]$Container, [string]$Sql) {
    docker exec $Container bash -lc "export PGPASSWORD='$PgPassword' && psql -h 127.0.0.1 -U job_admin -d job_config_db -t -A -c `"$Sql`""
}

Write-Host ""
Write-Host "=== user row counts (primary vs replicas) ===" -ForegroundColor Green
try {
    $p = (Invoke-PrimarySql "SELECT COUNT(*) FROM users;").Trim()
    Write-Host "  postgres-primary:  $p" -ForegroundColor Gray
    $s = (Invoke-ReplicaSql "postgres-standby" "SELECT COUNT(*) FROM users;").Trim()
    Write-Host "  postgres-standby:  $s" -ForegroundColor Gray
    $a = (Invoke-ReplicaSql "postgres-replica1" "SELECT COUNT(*) FROM users;").Trim()
    Write-Host "  postgres-replica1: $a" -ForegroundColor Gray
    $b = (Invoke-ReplicaSql "postgres-replica2" "SELECT COUNT(*) FROM users;").Trim()
    Write-Host "  postgres-replica2: $b" -ForegroundColor Gray
}
catch {
    Write-Host "  (Could not query yet — wait and retry, or check container logs.)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Connection strings:" -ForegroundColor Yellow
Write-Host '  Write: Host=localhost;Port=5432;Database=job_config_db;Username=job_admin;Password=StrongP@ssw0rd' -ForegroundColor Gray
Write-Host '  Read:  ports 5433 (standby), 5434 (replica1), 5435 (replica2) — same user/password' -ForegroundColor Gray
Write-Host ""
Write-Host "Stop: docker compose -f $ComposeFile down -v" -ForegroundColor Gray
