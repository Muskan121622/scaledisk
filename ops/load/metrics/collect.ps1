# Stage 5D — lightweight infrastructure metrics sampler.
#
# Runs for a fixed window, appending one CSV row per interval. Everything is read from the containers
# directly (docker stats, psql, redis-cli, kafka-consumer-groups) so it adds no monitoring stack and
# no new infrastructure. Invoke in the background next to a k6 run so each stage's HTTP curve is
# correlated with what the subsystems were doing at the same wall-clock time.
#
# Usage:  collect.ps1 -Stage ramp_100 -Duration 60 -Interval 3 -Out .\results\metrics.csv
param(
    [string]$Stage = 'auto',
    [int]$Duration = 60,
    [int]$Interval = 3,
    [string]$Out = "$PSScriptRoot\..\results\metrics.csv"
)
$ErrorActionPreference = 'Continue'
$pg  = 'scaledesk-postgres'
$rd  = 'scaledesk-redis'
$kf  = 'scaledesk-kafka'
$grp = 'scaledesk-notifications'

function PsqlSup { param([string]$Sql) (($Sql | docker exec -i $pg psql -U scaledesk -d scaledesk -tA 2>$null | Out-String).Trim()) }
function Num($s) { $d=0.0; if ([double]::TryParse(($s -replace '[^0-9.]',''), [ref]$d)) { return $d } return 0 }
function Int($s) { $n=0; if ([int]::TryParse(($s -replace '\D',''), [ref]$n)) { return $n } return 0 }
function InfoVal([string]$text,[string]$key) {
    foreach ($l in ($text -split "`n")) { if ($l -match ("^\s*{0}\s*:\s*(.+)$" -f $key)) { return $matches[1].Trim() } }
    return ''
}

$cols = 'ts,stage,api_cpu_pct,api_mem,pg_cpu_pct,pg_mem,kafka_cpu_pct,redis_cpu_pct,' +
        'pg_active_conns,pg_total_conns,pg_ungranted_locks,pg_tup_returned,pg_deadlocks,' +
        'redis_ops_per_sec,redis_hits,redis_misses,redis_hit_ratio,redis_used_memory,' +
        'kafka_lag_total,kafka_end_offset_total,outbox_pending,outbox_avg_latency_s'
if (-not (Test-Path $Out)) { $dir = Split-Path $Out -Parent; if ($dir) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }; Set-Content -Path $Out -Value $cols -Encoding UTF8 }

$end = (Get-Date).AddSeconds($Duration)
while ((Get-Date) -lt $end) {
    $ts = (Get-Date).ToUniversalTime().ToString('HH:mm:ss')

    # Container CPU/mem.
    $map = @{}
    foreach ($l in (docker stats --no-stream --format '{{.Name}}|{{.CPUPerc}}|{{.MemUsage}}' 2>$null)) {
        $f = $l.Split('|'); if ($f.Count -ge 3) { $map[$f[0]] = @{ cpu=$f[1]; mem=$f[2] } }
    }
    $api=Num($map['scaledesk-api'].cpu);   $apim=$map['scaledesk-api'].mem
    $pgc=Num($map['scaledesk-postgres'].cpu); $pgm=$map['scaledesk-postgres'].mem
    $kfc=Num($map['scaledesk-kafka'].cpu);  $kfm=$map['scaledesk-kafka'].mem
    $rds=Num($map['scaledesk-redis'].cpu);  $rdm=$map['scaledesk-redis'].mem

    # Postgres: connections, contention, cumulative tuples touched, deadlocks.
    $pgActive = Int(PsqlSup "SELECT count(*) FROM pg_stat_activity WHERE datname='scaledesk' AND state <> 'idle'")
    $pgTotal  = Int(PsqlSup "SELECT count(*) FROM pg_stat_activity WHERE datname='scaledesk'")
    $pgLocks  = Int(PsqlSup "SELECT count(*) FROM pg_locks WHERE NOT granted")
    $pgTup    = Int(PsqlSup "SELECT coalesce(sum(n_tup_returned + n_tup_fetched)::bigint,0) FROM pg_stat_database WHERE datname='scaledesk'")
    $pgDead   = Int(PsqlSup "SELECT coalesce(sum(deadlocks)::bigint,0) FROM pg_stat_database WHERE datname='scaledesk'")

    # Redis: throughput + cache efficiency (single INFO call).
    $info = (docker exec $rd redis-cli INFO 2>$null | Out-String)
    $ops   = Int(InfoVal $info 'instantaneous_ops_per_sec')
    $hits  = Int(InfoVal $info 'keyspace_hits')
    $miss  = Int(InfoVal $info 'keyspace_misses')
    $ratio = if (($hits + $miss) -gt 0) { [math]::Round(100.0 * $hits / ($hits + $miss), 1) } else { 0 }
    $used  = (InfoVal $info 'used_memory_human')

    # Kafka: per-group consumer LAG + total log-end offset (for throughput deltas).
    $lag = 0; $endOff = 0
    foreach ($l in (docker exec $kf /opt/kafka/bin/kafka-consumer-groups.sh --bootstrap-server localhost:9092 --describe --group $grp 2>$null)) {
        $f = ($l -split '\s+') | Where-Object { $_ -ne '' }
        if ($f.Count -ge 6 -and $f[2] -match '^\d+$' -and $f[4] -match '^\d+$') {
            if ($f[5] -match '^\d+$') { $lag += [int]$f[5] }        # LAG
            $endOff += [int]$f[4]                                    # LOG-END-OFFSET
        }
    }

    # Outbox backlog + end-to-end publish latency.
    $obPend = Int(PsqlSup 'SELECT count(*) FROM outbox_messages WHERE "ProcessedAt" IS NULL')
    $obLat  = (PsqlSup 'SELECT round(coalesce(extract(epoch from avg("ProcessedAt"-"OccurredAt")),0)::numeric,3) FROM outbox_messages WHERE "ProcessedAt" IS NOT NULL')

    $row = "$ts,$Stage,$api,$apim,$pgc,$pgm,$kfc,$rds,$pgActive,$pgTotal,$pgLocks,$pgTup,$pgDead,$ops,$hits,$miss,$ratio,$used,$lag,$endOff,$obPend,$obLat"
    Add-Content -Path $Out -Value $row -Encoding UTF8
    Start-Sleep -Seconds $Interval
}
