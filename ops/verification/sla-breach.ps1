# 5C / Scenario 8 — SLA breach enforcement (timer-driven, async side effects).
# The breach scanner runs on a timer (Sla:IntervalSeconds=10 here) and flags tickets that lapse past
# their first-response deadline while nobody has replied. We can't wait hours in a test, so we
# fast-forward the clock legitimately: create + assign a ticket, then push its ResponseDueAt into the
# past. We assert the scanner (a) marks SlaBreached on the row, (b) emits an sla.breached event through
# the outbox -> Kafka -> consumer, (c) the consumer files a notification for the assignee, and (d) it
# is idempotent — exactly one breach, never re-escalated on later ticks.
. "$PSScriptRoot\_common.ps1"

function Wait-For { param([scriptblock]$Test,[int]$Seconds=40)
    $end=(Get-Date).AddSeconds($Seconds)
    while((Get-Date) -lt $end){ if(& $Test){return $true}; Start-Sleep -Seconds 2 }
    return [bool](& $Test)
}

Write-Host "== sla-breach : $script:Api =="

$owner = Invoke-Login 'rahul@technova.com' 'technova'
$agent = Invoke-Login 'sweta@technova.com' 'technova'
$HO = $owner.accessToken
$agentId = (Invoke-Api -Path '/api/auth/me' -Token $agent.accessToken).Json.userId
$customerId = (Invoke-Api -Path '/api/customers?pageSize=1' -Token $HO).Json.items[0].id

$subj = "SLA breach probe $([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())"
$create = Invoke-Api -Method Post -Path '/api/tickets' -Token $HO -Body @{
    customerId=$customerId; subject=$subj; description='will go overdue'; priority=2; category=1; teamId=$null; assigneeUserId=$null
}
$ticketId = $create.Json.id
$assign = Invoke-Api -Method Patch -Path "/api/tickets/$ticketId/assign" -Token $HO -Body @{ assigneeUserId=$agentId; teamId=$null }
Check 'ticket created + assigned (breach notification needs a recipient)' ($create.Status -eq 201 -and $assign.Status -eq 200) "create=$($create.Status) assign=$($assign.Status)"

# Baseline: SLA stamped, not yet breached, no first response.
# Normalize to UTC: the JSON carries a trailing Z, and [DateTime] cast yields local-kind ticks, so we
# must ToUniversalTime() on both sides before comparing (operator -lt/-gt ignores Kind).
$nowUtc = (Get-Date).ToUniversalTime()
$dueUtc = ([DateTime]$create.Json.responseDueAt).ToUniversalTime()
Check 'SLA deadline stamped at creation (future dueAt)' ($dueUtc -gt $nowUtc) "dueAt=$($create.Json.responseDueAt)"
Check 'not breached at creation' ($create.Json.slaBreached -eq $false) "slaBreached=$($create.Json.slaBreached)"

# ---- Fast-forward time: mark the response deadline as already missed (nobody replied). ----
$null = Invoke-Psql "UPDATE tickets SET ""ResponseDueAt"" = (now() - interval '5 minutes') WHERE ""Id"" = '$ticketId'::uuid"
$overdue = Invoke-Api -Path "/api/tickets/$ticketId" -Token $HO
$dueNow = ([DateTime]$overdue.Json.responseDueAt).ToUniversalTime()
Check 'deadline moved into the past (ticket is now overdue)' ($dueNow -lt ([DateTime]::UtcNow)) "dueAt=$($overdue.Json.responseDueAt)"

# ---- The scanner should catch it on its next tick and flag the row. ----
$breached = Wait-For { ((Invoke-Api -Path "/api/tickets/$ticketId" -Token $HO).Json.slaBreached) -eq $true } 40
Check 'SLA scanner marked the overdue ticket SlaBreached=true' $breached

# ---- Async side effects: an sla.breached event flowed outbox -> Kafka -> consumer -> notification. ----
$event = Invoke-Psql "SELECT count(*) FROM outbox_messages WHERE ""EventType""='sla.breached' AND ""Payload""::text LIKE '%$ticketId%' AND ""ProcessedAt"" IS NOT NULL"
Check 'Outbox: sla.breached event published to Kafka' ((($event -as [int]) -ge 1)) "published=$event"

$notifSql = "SELECT count(*) FROM notifications WHERE ""RelatedTicketId""='$ticketId' AND ""Type""='sla.breached'"
$notified = Wait-For { ((Invoke-Psql $notifSql) -as [int]) -ge 1 } 30
Check 'Consumer created sla.breached NOTIFICATION for the assignee' $notified "count=$(Invoke-Psql $notifSql)"

$inFeed = Wait-For {
    @((Invoke-Api -Path '/api/notifications?take=50' -Token $agent.accessToken).Json |
        Where-Object { $_.type -eq 'sla.breached' -and $_.relatedTicketId -eq $ticketId }).Count -ge 1
} 20
Check 'breach notification visible in the assignee feed (realtime durable via REST)' $inFeed

# ---- Idempotency: the scanner guards on SlaBreached, so extra ticks must NOT re-escalate. ----
Start-Sleep -Seconds 14   # ~1-2 more scanner intervals
$finalBreaches = (Invoke-Psql $notifSql) -as [int]
Check 'scanner is idempotent: exactly ONE breach notification (not re-escalated)' ($finalBreaches -eq 1) "count=$finalBreaches"

if (-not (Complete-Scenario 'SLA breach -> scanner -> event -> notification (idempotent)')) { exit 1 }
