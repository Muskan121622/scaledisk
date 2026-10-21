# 5C / Scenario 5 — Duplicate integration event is processed EXACTLY ONCE (idempotency).
# The outbox -> Kafka hop is at-least-once: the same LOGICAL event can be delivered more than once
# (redelivery after a crash, an outbox row re-drained, a manual re-emit). We reproduce the nastiest
# case: re-publish the SAME business event so it lands at a BRAND-NEW Kafka offset. Position-based
# (topic:partition:offset) dedup would NOT catch that — only stable event-id dedup does. We prove the
# notification projection is applied once and only once.
. "$PSScriptRoot\_common.ps1"

function Wait-For { param([scriptblock]$Test,[int]$Seconds=30)
    $end=(Get-Date).AddSeconds($Seconds)
    while((Get-Date) -lt $end){ if(& $Test){return $true}; Start-Sleep -Seconds 2 }
    return [bool](& $Test)
}

# LAG for a single-partition topic in the consumer group (describe col 6). Empty if topic unconsumed.
function Get-Lag([string]$Topic) {
    $line = docker exec scaledesk-kafka /opt/kafka/bin/kafka-consumer-groups.sh `
        --bootstrap-server localhost:9092 --describe --group scaledesk-notifications 2>$null |
        Select-String "scaledesk\.$Topic\s" | Select-Object -First 1
    if (-not $line) { return $null }
    return ( ($line.ToString() -split '\s+')[5] )
}

Write-Host "== duplicate-event : $script:Api =="

$owner = Invoke-Login 'rahul@technova.com' 'technova'
$agent = Invoke-Login 'sweta@technova.com' 'technova'
$HO = $owner.accessToken
$agentId = (Invoke-Api -Path '/api/auth/me' -Token $agent.accessToken).Json.userId
$customerId = (Invoke-Api -Path '/api/customers?pageSize=1' -Token $HO).Json.items[0].id

# --- Create + assign -> exactly one ticket.assigned event/notification ---
$subj = "DUP test $([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())"
$create = Invoke-Api -Method Post -Path '/api/tickets' -Token $HO -Body @{
    customerId=$customerId; subject=$subj; description='idempotency'; priority=1; category=0; teamId=$null; assigneeUserId=$null
}
$ticketId = $create.Json.id
$assign = Invoke-Api -Method Patch -Path "/api/tickets/$ticketId/assign" -Token $HO -Body @{ assigneeUserId=$agentId; teamId=$null }
Check 'ticket created + assigned (201/200)' ($create.Status -eq 201 -and $assign.Status -eq 200) "create=$($create.Status) assign=$($assign.Status)"

$notifSql = "SELECT count(*) FROM notifications WHERE ""RelatedTicketId""='$ticketId' AND ""Type""='ticket.assigned'"
$firstOk  = Wait-For { ((Invoke-Psql $notifSql) -as [int]) -ge 1 } 30
Check 'baseline: consumer created 1 notification for the assign' $firstOk "count=$(Invoke-Psql $notifSql)"

# Locate the ticket.assigned outbox row (its Id is the stable business event id) + confirm dedup key form.
$eventId = (Invoke-Psql "SELECT ""Id"" FROM outbox_messages WHERE ""EventType""='ticket.assigned' AND ""Payload""::text LIKE '%$ticketId%' AND ""ProcessedAt"" IS NOT NULL ORDER BY ""OccurredAt"" DESC LIMIT 1")
Check 'outbox row for the assignment has a stable event id' ($eventId -match '[0-9a-fA-F-]{36}') "id=$eventId"
$dedup1 = Invoke-Psql "SELECT count(*) FROM processed_messages WHERE ""DeduplicationKey""='evt:$eventId'"
Check 'dedup marker stored under the EVENT ID (evt:...) not the offset' ($dedup1 -eq '1') "count=$dedup1"

$baselineNotifs = (Invoke-Psql $notifSql) -as [int]
$endOffsetBefore = (Get-Lag 'ticket.assigned')

# --- Re-emit the SAME logical event: clear ProcessedAt so the outbox drains it again. It will be
#     published to Kafka a SECOND time at a NEW offset, carrying the SAME x-event-id header. ---
$null = Invoke-Psql "UPDATE outbox_messages SET ""ProcessedAt""=NULL, ""LastError""=NULL WHERE ""Id""='$eventId'"
Write-Host '  re-emitted outbox row; waiting for it to be re-published + re-consumed...'

$republished = Wait-For { ((Invoke-Psql "SELECT count(*) FROM outbox_messages WHERE ""Id""='$eventId' AND ""ProcessedAt"" IS NOT NULL") -as [int]) -eq 1 } 30
Check 'duplicate was re-published to Kafka (new delivery of same event id)' $republished

$drained = Wait-For { (Get-Lag 'ticket.assigned') -eq '0' } 20
Check 'consumer re-consumed the duplicate (group LAG back to 0)' $drained "lag=$(Get-Lag 'ticket.assigned')"

# Let any (incorrect) second projection have time to land, then assert NOTHING was duplicated.
Start-Sleep -Seconds 3
$afterNotifs = (Invoke-Psql $notifSql) -as [int]
Check 'NOTIFICATION applied EXACTLY ONCE despite the duplicate' ($afterNotifs -eq $baselineNotifs) "before=$baselineNotifs after=$afterNotifs"
$dedup2 = Invoke-Psql "SELECT count(*) FROM processed_messages WHERE ""DeduplicationKey""='evt:$eventId'"
Check 'no second dedup marker recorded (idempotent skip)' ($dedup2 -eq '1') "count=$dedup2"

Write-Host "  duplicate re-delivered at a new offset; notification stayed at $afterNotifs (expected 1)."
if (-not (Complete-Scenario 'Duplicate event processed once (event-id idempotency)')) { exit 1 }
