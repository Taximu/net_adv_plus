# =========================================
# Start local PostgreSQL streaming replication stack
# (docker-compose-postgres-replication.yml — official postgres:15 primary + 2 read replicas)
# =========================================

$ErrorActionPreference = "Stop"

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

function Invoke-NodeSql([string]$Container, [string]$Sql) {
    # Single-line SQL only; stderr discarded so psql errors don't clutter host
    $escaped = $Sql -replace '"', '\"'
    docker exec $Container bash -lc "export PGPASSWORD='$PgPassword' && psql -v ON_ERROR_STOP=1 -h 127.0.0.1 -U job_admin -d job_config_db -t -A -c `"$escaped`" 2>/dev/null"
    # Caller uses $LASTEXITCODE from docker exec; stdout is this function's return value
}

function Wait-NodeReady([string]$Label, [string]$Container, [string]$Sql, [int]$TimeoutSec) {
    Write-Host "  Waiting for $Label (timeout ${TimeoutSec}s)..." -ForegroundColor Gray
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    $started = Get-Date
    $lastProgress = $started
    while ((Get-Date) -lt $deadline) {
        $null = Invoke-NodeSql $Container $Sql
        if ($LASTEXITCODE -eq 0) {
            $elapsed = [int]((Get-Date) - $started).TotalSeconds
            Write-Host "  $Label ready (${elapsed}s)." -ForegroundColor Green
            return $true
        }
        Start-Sleep -Seconds 5
        $now = Get-Date
        if ((($now - $lastProgress).TotalSeconds) -ge 30) {
            $elapsed = [int](($now - $started).TotalSeconds)
            Write-Host "    ... still waiting for $Label (${elapsed}s elapsed; first boot base backup is slow)" -ForegroundColor DarkGray
            $lastProgress = $now
        }
    }
    Write-Host "  $Label not ready within ${TimeoutSec}s." -ForegroundColor Yellow
    return $false
}

Write-Host "[3/3] Waiting for init + replication (first boot: base backups to replicas, often 2–8 min)..." -ForegroundColor Yellow
Write-Host "  (Compose has no HEALTHCHECK; we poll SQL until ""users"" exists on each node.)" -ForegroundColor Gray
Write-Host "  Replicas stay quiet for minutes while copying the primary — progress lines every 30s." -ForegroundColor Gray

# Primary: init scripts create public.users
Wait-NodeReady "postgres-primary" "postgres-primary" "SELECT 1 FROM public.users LIMIT 1;" 120 | Out-Null

$replicas = @(
    @{ Name = "postgres-replica1"; Port = "5434" },
    @{ Name = "postgres-replica2"; Port = "5435" }
)
foreach ($r in $replicas) {
    # Replicas replay DDL/DML from primary; wait until catalog matches
    Wait-NodeReady $r.Name $r.Name "SELECT 1 FROM public.users LIMIT 1;" 480 | Out-Null
}

Write-Host ""
Write-Host "=== user row counts (primary vs replicas) ===" -ForegroundColor Green
try {
    $p = (Invoke-NodeSql "postgres-primary" "SELECT COUNT(*) FROM users;")
    if ($LASTEXITCODE -eq 0) { Write-Host "  postgres-primary:  $($p.Trim())" -ForegroundColor Gray }
    else { Write-Host "  postgres-primary:  (query failed)" -ForegroundColor Red }

    foreach ($r in $replicas) {
        $v = (Invoke-NodeSql $r.Name "SELECT COUNT(*) FROM users;")
        if ($LASTEXITCODE -eq 0) { Write-Host "  $($r.Name): $($v.Trim())" -ForegroundColor Gray }
        else { Write-Host "  $($r.Name): (query failed)" -ForegroundColor Red }
    }
}
catch {
    Write-Host "  (Unexpected error: $($_.Exception.Message))" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Connection strings:" -ForegroundColor Yellow
Write-Host '  Write: Host=localhost;Port=5432;Database=job_config_db;Username=job_admin;Password=StrongP@ssw0rd' -ForegroundColor Gray
Write-Host '  Read:  ports 5434 (replica1), 5435 (replica2) — same user/password' -ForegroundColor Gray
Write-Host ""
Write-Host "Stop: docker compose -f $ComposeFile down -v" -ForegroundColor Gray
