# 5C — Full production-failure & E2E verification suite. Orchestrates every scenario as an ISOLATED
# child powershell (so one failure can never abort the run), captures each one's real summary line,
# then prints a PASS/FAIL table + "N/7 scenarios passed". Raw per-scenario output is tee'd to
# ops/verification/evidence/*.log so the reported numbers are backed by reproducible artifacts.
#
# Usage:  powershell -File ops/verification/run-all.ps1
# Exit:   0 if every scenario passes, else 1.
$ErrorActionPreference = 'Continue'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Set-Location $root
$evidence = Join-Path $PSScriptRoot 'evidence'
if (-not (Test-Path $evidence)) { New-Item -ItemType Directory -Path $evidence | Out-Null }

# Scenario -> human label. Ordered: happy paths first, then fault injection (the centrepiece last-but-one).
$scenarios = @(
    @{ File='auth-e2e.ps1';        Label='Auth lifecycle + token-family revocation' }
    @{ File='ticket-e2e.ps1';      Label='Ticket journey (DB->Outbox->Kafka->Automation->Notif)' }
    @{ File='tenant-isolation.ps1';Label='Tenant isolation (EF filter + Postgres RLS)' }
    @{ File='duplicate-event.ps1'; Label='Duplicate event processed once (idempotency)' }
    @{ File='redis-failure.ps1';   Label='Redis outage -> degrade to Postgres -> recover' }
    @{ File='sla-breach.ps1';      Label='SLA breach scanner -> event -> notification' }
    @{ File='kafka-failure.ps1';   Label='FLAGSHIP: Kafka outage -> outbox buffers -> drains' }
)

Write-Host '=== ScaleDesk 5C verification suite ==='
Write-Host 'Bringing the stack up (idempotent)...'
& (Join-Path $PSScriptRoot 'up.ps1')
if ($LASTEXITCODE -ne 0) { Write-Host 'ABORT: stack did not become Healthy.'; exit 1 }
Write-Host ''

$rows = @()
foreach ($s in $scenarios) {
    $path = Join-Path $PSScriptRoot $s.File
    $name = [IO.Path]::GetFileNameWithoutExtension($s.File)
    Write-Host ("---- running {0} ----" -f $s.File)
    $start = Get-Date
    # Child process keeps its own $ErrorActionPreference and exit code; capture all streams.
    $out = (& powershell -NoProfile -ExecutionPolicy Bypass -File $path 2>&1 | Out-String)
    $code = $LASTEXITCODE
    $secs = [int]((Get-Date) - $start).TotalSeconds
    $out | Set-Content -Path (Join-Path $evidence "$name.log")

    # The scenario's own verdict line starts at column 0: "[PASS] label (N checks)" / "[FAIL] ...".
    $verdict = ($out -split "`r?`n" | Where-Object { $_ -match '^\[(PASS|FAIL)\]' } | Select-Object -Last 1)
    $checks  = if ($verdict -match '\((\d+) checks?\)') { $Matches[1] } else { '' }
    $pass    = ($code -eq 0) -and ($verdict -match '^\[PASS\]')
    $status  = if ($pass) { 'PASS' } else { 'FAIL' }
    Write-Host ("     -> [{0}] {1}  ({2} checks, {3}s)`n" -f $status, $s.Label, $checks, $secs)

    $rows += [pscustomobject]@{
        Scenario = $s.Label
        Result   = $status
        Checks   = $checks
        Seconds  = $secs
    }
    # Space scenarios out so their login bursts never collide with the 10/min "auth" rate limit.
    Start-Sleep -Seconds 12
}

Write-Host '================= RESULTS ================='
$rows | Format-Table -AutoSize | Out-String | Write-Host

$passed = @($rows | Where-Object { $_.Result -eq 'PASS' }).Count
$total  = $rows.Count
$totalChecks = ($rows | ForEach-Object { [int]$_.Checks } | Measure-Object -Sum).Sum
Write-Host ("{0}/{1} scenarios passed   ({2} checks total)" -f $passed, $total, $totalChecks)

if ($passed -eq $total) { exit 0 } else { exit 1 }
