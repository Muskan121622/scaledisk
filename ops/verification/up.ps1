# Idempotent bring-up for the 5C verification stack. Safe to re-run any time Docker Desktop or the
# API dies (this environment is flaky). Brings up the data plane, then launches the host API detached.
# NOTE: ErrorActionPreference is Continue on purpose: Docker writes routine progress to stderr, and
# 'Stop' would misread that as fatal. Failures are signalled via explicit exit codes instead.
$ErrorActionPreference = 'Continue'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Set-Location $root

function DockerUp {
    try { $v = docker version --format '{{.Server.Version}}' 2>$null; if ($v) { return $true } } catch {}
    Write-Host 'Docker daemon down -> starting Docker Desktop'
    Start-Process 'C:\Program Files\Docker\Docker\Docker Desktop.exe' -ErrorAction SilentlyContinue
    for ($i=0; $i -lt 40; $i++) { Start-Sleep -Seconds 5
        try { $v = docker version --format '{{.Server.Version}}' 2>$null; if ($v) { "daemon up ($v)"; return $true } } catch {}
    }
    return $false
}

if (-not (DockerUp)) { Write-Host 'FAILED: docker daemon'; exit 1 }

Write-Host 'compose up data plane...'
docker compose up -d postgres redis kafka jaeger 2>$null | Out-Null
for ($i=0; $i -lt 40; $i++) { Start-Sleep -Seconds 4
    $p = docker inspect --format '{{.State.Health.Status}}' scaledesk-postgres 2>$null
    $r = docker inspect --format '{{.State.Health.Status}}' scaledesk-redis 2>$null
    $k = docker inspect --format '{{.State.Health.Status}}' scaledesk-kafka 2>$null
    if ($p -eq 'healthy' -and $r -eq 'healthy' -and $k -eq 'healthy') { "containers healthy"; break }
}

# Ensure the host port-proxy actually forwards 5433 -> container (the flaky part on Windows).
$pg = (Test-NetConnection 127.0.0.1 -Port 5433 -WarningAction SilentlyContinue).TcpTestSucceeded
if (-not $pg) { Write-Host 'WARN: host :5433 not forwarding yet'; exit 1 }

# Provision integration-event topics BEFORE the consumer starts. If we relied on broker auto-create,
# a topic first published AFTER the consumer subscribed would not be picked up until librdkafka's
# metadata refresh (default 5 min), stalling that event type. Pre-creating makes consumption immediate.
Write-Host 'provisioning Kafka topics'
$types = @('ticket.created','ticket.assigned','ticket.status_changed','ticket.resolved','sla.breached',
           'customer.created','team.created','member.invited','user.email_verification_requested')
foreach ($t in $types) {
    docker exec scaledesk-kafka /opt/kafka/bin/kafka-topics.sh --bootstrap-server localhost:9092 `
        --create --if-not-exists --topic "scaledesk.$t" --partitions 1 --replication-factor 1 2>$null | Out-Null
}

# Launch the API detached only if it is not already serving.
$live = (Test-NetConnection 127.0.0.1 -Port 5102 -WarningAction SilentlyContinue).TcpTestSucceeded
if (-not $live) {
    Write-Host 'launching API detached'
    Start-Process 'powershell' -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-File',"$root\ops\verification\run-api-5c.ps1" `
        -RedirectStandardOutput "$root\api-5c.out.log" -RedirectStandardError "$root\api-5c.err.log" -WindowStyle Hidden | Out-Null
}
for ($i=0; $i -lt 25; $i++) { Start-Sleep -Seconds 3
    try { $h = Invoke-RestMethod 'http://localhost:5102/health/ready' -TimeoutSec 5; if ($h -eq 'Healthy') { "READY: $h"; exit 0 } } catch {}
}
Write-Host 'WARN: API not Healthy within window'; exit 1
