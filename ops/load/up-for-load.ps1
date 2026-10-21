# Stage 5D — bring up the LOAD-TEST topology (idempotent).
#
# Topology: the API runs as its OWN compose container (Release build, per the Dockerfile), NOT the 5C
# host process. We stop any host API on :5102 first so there is exactly one Kafka consumer + outbox
# dispatcher + SLA scanner running, and so k6 -> container CPU/memory (via docker stats) is clean.
# The API is launched with docker-compose.load.yml, which lifts rate limits, extends the JWT TTL and
# disables OTel export so the run measures the raw backend. See ops/load/README.md.
#
# ErrorActionPreference=Continue: Docker/compose write routine progress to stderr; failures are judged
# by explicit readiness/exit checks instead.
$ErrorActionPreference = 'Continue'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Set-Location $root

$apiBase   = 'http://localhost:8080'
$pgCont    = 'scaledesk-postgres'
$compose   = @('-f','docker-compose.yml','-f','docker-compose.override.yml','-f','docker-compose.load.yml')
$targetTickets = 1000000

function Invoke-PsqlSup { param([string]$Sql)
    ($Sql | docker exec -i $pgCont psql -U scaledesk -d scaledesk -tA 2>$null | Out-String).Trim()
}

function DockerUp {
    try { $v = docker version --format '{{.Server.Version}}' 2>$null; if ($v) { return $true } } catch {}
    Write-Host 'Docker daemon down -> starting Docker Desktop'
    Start-Process 'C:\Program Files\Docker\Docker\Docker Desktop.exe' -ErrorAction SilentlyContinue
    for ($i=0; $i -lt 40; $i++) { Start-Sleep -Seconds 5
        try { $v = docker version --format '{{.Server.Version}}' 2>$null; if ($v) { "daemon up ($v)"; return $true } } catch {}
    }
    return $false
}

Write-Host '== Stage 5D load bring-up =='
if (-not (DockerUp)) { Write-Host 'FAILED: docker daemon'; exit 1 }

# 1) Stop any host-run 5C API so the container is the only backend (avoids double consumers).
$hostApi = Get-NetTCPConnection -LocalPort 5102 -State Listen -ErrorAction SilentlyContinue
if ($hostApi) {
    Write-Host "stopping host API (PID $($hostApi.OwningProcess -join ','))"
    $hostApi.OwningProcess | Select-Object -Unique | ForEach-Object { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }
    Start-Sleep -Seconds 2
}

# 2) Data plane (same images/volumes as 5C; kafka-data persists topics across restarts).
Write-Host 'compose up: postgres, redis, kafka, jaeger'
docker compose @compose up -d postgres redis kafka jaeger 2>$null | Out-Null
for ($i=0; $i -lt 40; $i++) { Start-Sleep -Seconds 4
    $p = docker inspect --format '{{.State.Health.Status}}' scaledesk-postgres 2>$null
    $r = docker inspect --format '{{.State.Health.Status}}' scaledesk-redis 2>$null
    $k = docker inspect --format '{{.State.Health.Status}}' scaledesk-kafka 2>$null
    if ($p -eq 'healthy' -and $r -eq 'healthy' -and $k -eq 'healthy') { 'data plane healthy'; break }
}

# 3) Build + start the Release API on the LoadTesting profile.
Write-Host 'compose up --build: api (Release, LoadTesting profile)'
docker compose @compose up -d --build api 2>$null | Out-Null

# Wait for readiness (db + redis tagged checks).
$ready = $false
for ($i=0; $i -lt 60; $i++) { Start-Sleep -Seconds 3
    try { $h = Invoke-RestMethod "$apiBase/health/ready" -TimeoutSec 5; if (("$h").Trim() -eq 'Healthy') { $ready = $true; break } } catch {}
}
if (-not $ready) { Write-Host 'FAILED: API not Healthy within window'; exit 1 }
Write-Host "READY: $apiBase/health/ready = Healthy"

# 4) Confirm the app's own DbSeeder finished (it runs on startup) — the load seeder clones its hash.
for ($i=0; $i -lt 20; $i++) { Start-Sleep -Seconds 3
    $demo = Invoke-PsqlSup "SELECT count(*) FROM users WHERE ""Email""='rahul@technova.com'"
    if (($demo -as [int]) -ge 1) { break }
}

# 5) Seed the load dataset only if not already present (idempotent guard).
$tickets = (Invoke-PsqlSup 'SELECT count(*) FROM tickets') -as [int]
$loadTenant = (Invoke-PsqlSup "SELECT count(*) FROM tenants WHERE ""Slug"" LIKE 'load-tenant-%'") -as [int]
if ($tickets -ge $targetTickets -and $loadTenant -ge 1) {
    Write-Host "dataset already present (tickets=$tickets, loadTenants=$loadTenant); skipping seed"
} else {
    Write-Host "seeding load dataset (current tickets=$tickets) ... this runs 1M-row inserts"
    Get-Content "$PSScriptRoot\seed\load-dataset.sql" -Raw | docker exec -i $pgCont psql -U scaledesk -d scaledesk 2>$null | Out-Null
    Write-Host 'seed complete'
}

# 6) Verify scale so every downstream number rests on a known corpus.
$tickets  = (Invoke-PsqlSup 'SELECT count(*) FROM tickets') -as [int]
$tenants  = (Invoke-PsqlSup 'SELECT count(*) FROM tenants') -as [int]
$customers= (Invoke-PsqlSup 'SELECT count(*) FROM customers') -as [int]
$users    = (Invoke-PsqlSup 'SELECT count(*) FROM users') -as [int]
Write-Host ("VERIFY: tenants={0} users={1} customers={2} tickets={3}" -f $tenants,$users,$customers,$tickets)
if ($tickets -lt $targetTickets) { Write-Host "FAILED: expected >= $targetTickets tickets, got $tickets"; exit 1 }
Write-Host 'load topology ready'
exit 0
