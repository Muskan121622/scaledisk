# 5C / Scenario 4 (FLAGSHIP) — Kafka outage: no event lost, everything drains after recovery.
# The transactional outbox decouples the WRITE path from the async hop. We hard-kill the broker,
# create a ticket (which MUST still persist to Postgres + buffer as a Pending outbox row), confirm
# the app keeps serving, then restart Kafka and prove the buffered event is published and consumed
# exactly as if the broker had never gone away. This is the "prove it survives failure" centrepiece.
. "$PSScriptRoot\_common.ps1"

function Wait-For { param([scriptblock]$Test,[int]$Seconds=30)
    $end=(Get-Date).AddSeconds($Seconds)
    while((Get-Date) -lt $end){ if(& $Test){return $true}; Start-Sleep -Seconds 2 }
    return [bool](& $Test)
}
function KafkaHealth { docker inspect --format '{{.State.Health.Status}}' scaledesk-kafka 2>$null }

Write-Host "== kafka-failure : $script:Api =="

$owner = Invoke-Login 'rahul@technova.com' 'technova'
$HO = $owner.accessToken
$customerId = (Invoke-Api -Path '/api/customers?pageSize=1' -Token $HO).Json.items[0].id

# ---- Break it: stop the broker. ----
Write-Host '  stopping Kafka (simulating a broker outage)...'
docker stop scaledesk-kafka 2>$null | Out-Null
Wait-For { (KafkaHealth) -ne 'healthy' } 20 | Out-Null
Check 'Kafka container is DOWN' ((KafkaHealth) -ne 'healthy') "state=$(KafkaHealth)"

# ---- Write path is independent of the async hop. ----
$subj = "Outage test $([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())"
$create = Invoke-Api -Method Post -Path '/api/tickets' -Token $HO -Body @{
    customerId=$customerId; subject=$subj; description='during outage'; priority=1; category=0; teamId=$null; assigneeUserId=$null
}
$ticketId = $create.Json.id
Check 'ticket CREATE still succeeds while Kafka is down (201)' ($create.Status -eq 201) "HTTP $($create.Status)"

# The app keeps serving reads on the source of truth (Postgres); the broker is not a hard dependency.
$live = Invoke-Api -Path '/health/live'
$read = Invoke-Api -Path "/api/tickets/$ticketId" -Token $HO
Check 'app still serves (health/live + read your write) during outage' ($live.Ok -and $read.Status -eq 200) "live=$($live.Status) read=$($read.Status)"

# Source of truth: the row landed in Postgres despite the broker being dead.
$dbRow = Invoke-Psql "SELECT count(*) FROM tickets WHERE ""Id""='$ticketId'"
Check 'PostgreSQL: ticket persisted during outage (no data loss)' ($dbRow -eq '1') "count=$dbRow"

# Outbox: the event is BUFFERED as Pending (not lost, not yet published).
$pendSql = "SELECT count(*) FROM outbox_messages WHERE ""Payload""::text LIKE '%$ticketId%' AND ""ProcessedAt"" IS NULL"
$pendingNow = (Invoke-Psql $pendSql) -as [int]
Check 'Outbox: event buffered as PENDING during outage' ($pendingNow -ge 1) "pending=$pendingNow"
$eventId = (Invoke-Psql "SELECT ""Id"" FROM outbox_messages WHERE ""EventType""='ticket.created' AND ""Payload""::text LIKE '%$ticketId%' ORDER BY ""OccurredAt"" DESC LIMIT 1")

# ---- Heal it: restart the broker. ----
Write-Host '  restarting Kafka (broker recovery)...'
docker start scaledesk-kafka 2>$null | Out-Null
$healthy = Wait-For { (KafkaHealth) -eq 'healthy' } 60
Check 'Kafka container is HEALTHY again' $healthy "state=$(KafkaHealth)"

# Drain: the outbox processor republishes the buffered event to the recovered broker (ProcessedAt set).
$drained = Wait-For { ((Invoke-Psql "SELECT count(*) FROM outbox_messages WHERE ""Id""='$eventId' AND ""ProcessedAt"" IS NOT NULL") -as [int]) -eq 1 } 60
Check 'Outbox drained the buffered event to Kafka after recovery' $drained

# No ticket events left unpublished for this ticket.
$pendingAfter = (Invoke-Psql "SELECT count(*) FROM outbox_messages WHERE ""Payload""::text LIKE '%$ticketId%' AND ""ProcessedAt"" IS NULL") -as [int]
Check 'Outbox: nothing left pending after recovery (zero loss)' ($pendingAfter -eq 0) "pending=$pendingAfter"

# The consumer (never restarted) picks the recovered event up and records it idempotently.
$consumed = Wait-For { ((Invoke-Psql "SELECT count(*) FROM processed_messages WHERE ""DeduplicationKey""='evt:$eventId'") -as [int]) -ge 1 } 40
Check 'Kafka consumer processed the recovered event (projection ran downstream)' $consumed "event=$eventId"

if (-not (Complete-Scenario 'Kafka outage -> outbox buffers -> drains on recovery')) { exit 1 }
