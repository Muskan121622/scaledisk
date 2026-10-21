#requires -Version 5.1
<#
.SYNOPSIS
    Bring up ScaleDesk in the mode a frontend developer actually wants:
      - Postgres + Redis + Kafka + Jaeger in Docker (compose data plane)
      - API running on the host (dotnet run, :5102) so hot reload works
      - Vite dev server on :5173, proxying /api and /hubs to the host API
    The demo tenant is created automatically on first API start by DbSeeder, so
    open http://localhost:5173 and click "Try the demo" — no signup required.
.NOTES
    Idempotent. Re-running just re-attaches the same processes. To reset state:
      docker compose down -v
#>

[CmdletBinding()]
param(
    [switch]$SkipCompose,          # if the data plane is already up
    [switch]$SkipWeb,              # run only the API (useful for backend work)
    [string]$ApiPort = '5102',
    [string]$WebPort = '5173'
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
Write-Host "ScaleDesk dev root: $root" -ForegroundColor Cyan

if (-not $SkipCompose) {
    Write-Host "`n[1/3] data plane (postgres · redis · kafka · jaeger)" -ForegroundColor Cyan
    Push-Location $root
    docker compose up -d postgres redis kafka jaeger
    if ($LASTEXITCODE -ne 0) { Pop-Location; throw 'docker compose up failed' }
    Pop-Location
    Write-Host 'waiting for postgres to accept connections...' -ForegroundColor DarkGray
    for ($i = 0; $i -lt 30; $i++) {
        # Top-level `container_name:` in docker-compose.yml pins this to scaledesk-postgres,
        # which survives across compose-file edits (unlike the auto-generated <project>-postgres-1).
        docker exec scaledesk-postgres pg_isready -U scaledesk 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) { break }
        Start-Sleep -Seconds 1
    }
}

Write-Host "`n[2/3] API on the host (dotnet run, :$ApiPort)" -ForegroundColor Cyan
Push-Location $root
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = "http://localhost:$ApiPort"
# The container connection string points at the app role, which cannot run migrations —
# the local dev API uses the superuser against the mapped :5433 port.
$env:ConnectionStrings__Postgres = 'Host=localhost;Port=5433;Username=scaledesk;Password=scaledesk'
$env:Database__AutoMigrate = 'true'
$env:MSBUILDDISABLENODEREUSE = '1'

$apiJob = Start-Process -PassThru -NoNewWindow -FilePath 'dotnet' -ArgumentList @(
    'run', '--project', 'src/ScaleDesk.Api', '-c', 'Debug', '--no-launch-profile'
)
Pop-Location

Write-Host "waiting for API to become ready on :$ApiPort ..." -ForegroundColor DarkGray
$ready = $false
for ($i = 0; $i -lt 40; $i++) {
    try {
        $r = Invoke-WebRequest -Uri "http://localhost:$ApiPort/health/ready" -UseBasicParsing -TimeoutSec 2
        if ($r.StatusCode -eq 200) { $ready = $true; break }
    } catch { Start-Sleep -Seconds 1 }
}
if (-not $ready) { Write-Warning 'API did not become ready in 40s — check the dotnet output above.' }
else { Write-Host "API ready. DbSeeder has created tenant 'demo' — owner demo@scaledesk.app." -ForegroundColor Green }

if (-not $SkipWeb) {
    Write-Host "`n[3/3] Vite dev server on :$WebPort (proxies /api + /hubs to :$ApiPort)" -ForegroundColor Cyan
    Push-Location (Join-Path $root 'web')
    if (-not (Test-Path 'node_modules')) {
        Write-Host 'installing web deps (one-off, may take 60-90s)' -ForegroundColor DarkGray
        npm install --no-audit --no-fund --loglevel=error
    }
    $env:VITE_API_TARGET = "http://localhost:$ApiPort"
    Write-Host "`nScaleDesk is up:" -ForegroundColor Green
    Write-Host "  · Frontend  http://localhost:$WebPort" -ForegroundColor White
    Write-Host "  · API       http://localhost:$ApiPort" -ForegroundColor White
    Write-Host "  · Try demo  click 'Try the demo' on the landing page" -ForegroundColor White
    Write-Host "  · Direct    demo@scaledesk.app / DemoPass2026! · slug 'demo'" -ForegroundColor DarkGray
    npm run dev -- --port $WebPort
    Pop-Location
}

Write-Host "`nStopping API PID $($apiJob.Id)" -ForegroundColor DarkGray
try { Stop-Process -Id $apiJob.Id -ErrorAction SilentlyContinue } catch {}
