# Stage 5D — run-load orchestrator.
#
# Drives the whole measurement in order: bring up topology -> provision token pool -> single-user
# baseline -> per-concurrency-level mixed runs (10/25/50/100/250/500), each correlated with an infra
# metrics window. k6 runs as a CONTAINER joined to the compose network (no host install) and targets
# http://api:8080 directly. Real summaries are captured verbatim to ops/load/results/*.k6.log and
# parsed into results/ramp.csv + an on-screen table.
#
# NOTE (honesty): k6, the API and every datastore share one laptop, so absolute req/s is machine-bound
# and NOT production throughput. The value here is the SHAPE (where p95 bends) and which subsystem
# saturates first. See ops/load/README.md.
$ErrorActionPreference = 'Continue'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Set-Location $root

$loadDir   = "$PSScriptRoot"
$res       = "$loadDir\results"
$ctxJson   = "$loadDir\ctx.json"
$metricsOut= "$res\metrics.csv"
$network   = 'scaledesk_default'
$k6img     = 'grafana/k6'
if (-not (Test-Path $res)) { New-Item -ItemType Directory -Force -Path $res | Out-Null }

function ToMs([string]$v) {  # k6 renders durations in us / ms / s depending on magnitude
    if ($v -match '^([\d.]+)(ms|µs|us|s)$') {
        $n = [double]$matches[1]
        switch ($matches[2]) { 'ms' { return $n } 'µs' { return [math]::Round($n/1000,3) } 'us' { return [math]::Round($n/1000,3) } 's' { return $n*1000 } }
    }
    return $v
}
function Parse-K6 { param([string]$Path,[string]$Label,[int]$Vus)
    $txt = Get-Content $Path -Raw
    $o = [pscustomobject]@{ level=$Label; vus=$Vus; req_per_s=''; total=''; p50=''; p95=''; p99=''; err_pct=''; breach='' }
    if ($txt -match 'http_reqs\.+\s*:\s*(\d+(?:\.\d+)?)\s+([\d.]+)/s') { $o.total=$matches[1]; $o.req_per_s=$matches[2] }
    if ($txt -match '(?m)^.*http_req_duration\.+\s*:.*avg=(\S+)\s+min=(\S+)\s+med=(\S+)\s+p\(90\)=(\S+)\s+p\(95\)=(\S+)\s+p\(99\)=(\S+)') {
        $o.p50 = ToMs $matches[3]; $o.p95 = ToMs $matches[5]; $o.p99 = ToMs $matches[6]
    }
    if ($txt -match 'http_req_failed\.+\s*:\s*([\d.]+)%') { $o.err_pct = $matches[1] }
    $o
}

function Start-Collect { param([string]$Stage,[int]$Secs)
    Start-Process powershell -WindowStyle Hidden -PassThru -ArgumentList @(
        '-NoProfile','-ExecutionPolicy','Bypass','-File',"$loadDir\metrics\collect.ps1",
        '-Stage',$Stage,'-Duration',$Secs,'-Interval','3','-Out',$metricsOut)
}

function Run-K6 { param([string]$Script,[string]$Label,[int]$Vus,[int]$Dur,[bool]$Collect)
    $log = "$res\$Label.k6.log"
    $dargs = @('run','--rm','--network',$network,'-v',"${loadDir}:/scripts",
               '-e','BASE=http://api:8080','-e','CTX=/scripts/ctx.json')
    if ($Vus -gt 0) { $dargs += @('-e',"VUS=$Vus",'-e',"DUR=${Dur}s") }
    $dargs += @($k6img,'run',"/scripts/k6/$Script")
    Write-Host "  k6: $Script ($Label) vus=$Vus dur=${Dur}s"
    $proc = $null
    if ($Collect) { $proc = Start-Collect -Stage $Label -Secs ($Dur + 15) }
    Start-Sleep -Seconds 4                       # let the sampler get ahead of the load
    & docker @dargs 2>&1 | Tee-Object -FilePath $log | Out-Null
    $code = $LASTEXITCODE
    if ($proc) {
        $t0 = Get-Date
        while ((Get-Date) -lt $t0.AddSeconds(20)) { if ($proc.HasExited) { break }; Start-Sleep -Seconds 1 }
        if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
    }
    $row = Parse-K6 -Path $log -Label $Label -Vus $Vus
    $row.breach = if ($code -ne 0) { 'SLO-BREACH' } else { 'ok' }
    $row
}

Write-Host '== Stage 5D run-load =='
# 0) prerequisites: image + topology + pool.
docker image inspect $k6img 2>$null | Out-Null; if ($LASTEXITCODE -ne 0) { Write-Host "pulling $k6img"; docker pull $k6img 2>$null | Out-Null }
& powershell -NoProfile -ExecutionPolicy Bypass -File "$loadDir\up-for-load.ps1"
if ($LASTEXITCODE -ne 0) { Write-Host 'FAILED: up-for-load'; exit 1 }
& powershell -NoProfile -ExecutionPolicy Bypass -File "$loadDir\auth\tokenpool.ps1"
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $ctxJson)) { Write-Host 'FAILED: tokenpool'; exit 1 }

$rows = New-Object System.Collections.ArrayList

# 1) Baseline: single VU, no contention (cold reference).
Write-Host "`n-- baseline --"
$b = Run-K6 -Script 'baseline.js' -Label 'baseline' -Vus 0 -Dur 0 -Collect $true
$null = $rows.Add($b)

# 2) Concurrency ramp, discrete per-level runs so infra lines up with each level.
Write-Host "`n-- ramp --"
$levels = @(10, 25, 50, 100, 250, 500)
foreach ($lv in $levels) {
    $r = Run-K6 -Script 'mixed.js' -Label "ramp_$lv" -Vus $lv -Dur 45 -Collect $true
    $null = $rows.Add($r)
    Write-Host ("    level {0,3}: {1} req/s  p50={2}ms  p95={3}ms  p99={4}ms  err={5}%  [{6}]" -f $lv,$r.req_per_s,$r.p50,$r.p95,$r.p99,$r.err_pct,$r.breach)
    # Run the FULL curve, not stop-at-first-breach: the p95<1s line is a flag, not a halt.
    # Only a catastrophic result (no data collected, or a blow-up error rate) aborts the sweep.
    if ([string]::IsNullOrEmpty($r.req_per_s) -or ($r.err_pct -ne '' -and [double]$r.err_pct -ge 25)) {
        Write-Host "    catastrophic at $lv VUs (no throughput or err>=25%) -> stopping"; break
    }
    Start-Sleep -Seconds 12                     # cooldown: let outbox/Kafka drain between levels
}

# 3) Persist + print the table.
$rows | Select-Object level,vus,req_per_s,p50,p95,p99,err_pct,breach | Export-Csv -Path "$res\ramp.csv" -NoTypeInformation -Encoding UTF8
Write-Host "`n== ScaleDesk Load Test =="
$rows | Select-Object level,vus,req_per_s,p50,p95,p99,err_pct,breach | Format-Table -AutoSize
Write-Host "artifacts: $res\ramp.csv, $res\metrics.csv, $res\*.k6.log"
exit 0
