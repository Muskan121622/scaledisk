# 5C / Scenario 6 — Redis outage: cache is an ACCELERATOR, not a dependency.
# The only cached read-path is the SLA-policy snapshot resolved while creating a ticket (cache-aside).
# We stop Redis and prove: (a) readiness honestly flips to Degraded (honest health reporting), but
# (b) the app KEEPS SERVING writes+reads — the cache swallows the outage and falls back to Postgres —
# and (c) full recovery once Redis returns. The process itself never crashes (AbortOnConnectFail=false).
. "$PSScriptRoot\_common.ps1"

function Wait-For { param([scriptblock]$Test,[int]$Seconds=30)
    $end=(Get-Date).AddSeconds($Seconds)
    while((Get-Date) -lt $end){ if(& $Test){return $true}; Start-Sleep -Seconds 2 }
    return [bool](& $Test)
}
function RedisHealth { docker inspect --format '{{.State.Health.Status}}' scaledesk-redis 2>$null }
# The readiness endpoint returns the aggregate status WORD as its body (Healthy/Degraded/Unhealthy).
# NOTE (honest): .NET's health middleware maps only UNHEALTHY to 503; DEGRADED returns HTTP 200, so the
# outage signal lives in the body text, which is what we assert on.
function ReadyText { try { (Invoke-WebRequest "$script:Api/health/ready" -UseBasicParsing -TimeoutSec 6).Content.Trim() } catch { $s=$_.Exception.Response.GetResponseStream(); (New-Object System.IO.StreamReader($s)).ReadToEnd().Trim() } }

Write-Host "== redis-failure : $script:Api =="

$owner = Invoke-Login 'rahul@technova.com' 'technova'
$HO = $owner.accessToken
$customerId = (Invoke-Api -Path '/api/customers?pageSize=1' -Token $HO).Json.items[0].id

# Warm the cache: one normal create (SLA snapshot now cached in Redis).
$base = Invoke-Api -Method Post -Path '/api/tickets' -Token $HO -Body @{
    customerId=$customerId; subject="redis warm $([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())"; description='warm'; priority=1; category=0; teamId=$null; assigneeUserId=$null
}
Check 'baseline create with Redis UP works (201)' ($base.Status -eq 201) "HTTP $($base.Status)"

# ---- Break it: stop Redis. ----
Write-Host '  stopping Redis (simulating a cache outage)...'
docker stop scaledesk-redis 2>$null | Out-Null
Wait-For { (RedisHealth) -ne 'healthy' } 20 | Out-Null

# Honest health: readiness body reports Degraded once the multiplexer notices the socket close.
# docker stop's SIGTERM grace lets Redis serve a few more seconds, so poll briefly.
$degraded = Wait-For { (ReadyText) -match 'Degraded|Unhealthy' } 25
Check 'readiness reports Degraded while Redis is down (honest signal)' $degraded "ready=$(ReadyText)"

# Liveness is unaffected (no external deps) -> the orchestrator restarts nothing.
$live = Invoke-Api -Path '/health/live'
Check 'liveness still OK (app not crashed)' ($live.Ok -and $live.Status -eq 200) "live HTTP $($live.Status)"

# The app STAYS UP: create a ticket. The SLA lookup can't hit Redis, so it recomputes from Postgres.
$subj = "During redis outage $([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())"
$create = Invoke-Api -Method Post -Path '/api/tickets' -Token $HO -Body @{
    customerId=$customerId; subject=$subj; description='no cache'; priority=1; category=0; teamId=$null; assigneeUserId=$null
}
$ticketId = $create.Json.id
Check 'ticket CREATE still succeeds while Redis is down (graceful degradation)' ($create.Status -eq 201) "HTTP $($create.Status)"
Check 'SLA still applied from Postgres (responseDueAt set, cache miss recomputed)' ($null -ne $create.Json.responseDueAt) "dueAt=$($create.Json.responseDueAt)"

# Reads keep working off the source of truth.
$list = Invoke-Api -Path '/api/tickets?pageSize=5' -Token $HO
Check 'reads served during outage (list from Postgres)' ($list.Status -eq 200) "HTTP $($list.Status)"
$dbRow = Invoke-Psql "SELECT count(*) FROM tickets WHERE ""Id""='$ticketId'"
Check 'PostgreSQL: outage-period write persisted' ($dbRow -eq '1') "count=$dbRow"

# ---- Heal it: restart Redis. ----
Write-Host '  restarting Redis (cache recovery)...'
docker start scaledesk-redis 2>$null | Out-Null
Wait-For { (RedisHealth) -eq 'healthy' } 40 | Out-Null
$readyUp = Wait-For { (ReadyText) -eq 'Healthy' } 30
Check 'readiness returns to Healthy after Redis recovery' $readyUp "ready=$(ReadyText)"

$after = Invoke-Api -Method Post -Path '/api/tickets' -Token $HO -Body @{
    customerId=$customerId; subject="after redis recovery $([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())"; description='back'; priority=1; category=0; teamId=$null; assigneeUserId=$null
}
Check 'full write path restored after recovery (201)' ($after.Status -eq 201) "HTTP $($after.Status)"

if (-not (Complete-Scenario 'Redis outage -> reads degrade to Postgres -> recovers')) { exit 1 }
