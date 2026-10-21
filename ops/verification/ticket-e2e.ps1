# 5C / Scenario 2 — Complete ticket journey through the whole architecture.
# create -> PostgreSQL -> Outbox -> Kafka -> consumer -> AUTOMATION (mutates ticket) ; then
# assign -> Outbox -> Kafka -> consumer -> NOTIFICATION (assignee's feed). Verifies final state in
# each system. The automation escalation (High -> Urgent) is only observable if the async consumer
# actually processed THIS ticket.created event, so it doubles as proof the pipeline ran end to end.
. "$PSScriptRoot\_common.ps1"

function Wait-For { param([scriptblock]$Test,[int]$Seconds=40)
    $end=(Get-Date).AddSeconds($Seconds)
    while((Get-Date) -lt $end){ if(& $Test){return $true}; Start-Sleep -Seconds 2 }
    return [bool](& $Test)
}

Write-Host "== ticket-e2e : $script:Api =="

$owner  = Invoke-Login 'rahul@technova.com' 'technova'
$agent  = Invoke-Login 'sweta@technova.com' 'technova'
$HO = $owner.accessToken
$agentId = (Invoke-Api -Path '/api/auth/me' -Token $agent.accessToken).Json.userId

$customerId = (Invoke-Api -Path '/api/customers?pageSize=1' -Token $HO).Json.items[0].id

# --- Automation rule: on ticket.created where Priority=High(2) -> escalate to Urgent(3) ---
$rule = Invoke-Api -Method Post -Path '/api/automation-rules' -Token $HO -Body @{
    name='Escalate High'; description='5C'; triggerEvent='ticket.created'; priority=$null; isEnabled=$true
    conditions=@{ priority=2; category=$null; status=$null; subjectContains=$null }
    actions=@(@{ type=0; priority=3; status=$null; teamId=$null; assigneeUserId=$null })   # 0 = SetPriority
}
Check 'automation rule created (High -> Urgent)' ($rule.Status -eq 201) "HTTP $($rule.Status)"

# --- Create a High/Billing ticket; capture trace id ---
$subj = "E2E journey $([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())"
$create = Invoke-Api -Method Post -Path '/api/tickets' -Token $HO -Body @{
    customerId=$customerId; subject=$subj; description='full journey'; priority=2; category=1; teamId=$null; assigneeUserId=$null
}
$ticketId = $create.Json.id
$traceId  = $create.Headers['X-Correlation-Id']
Check 'ticket created (201) with id + correlation/trace id' ($create.Status -eq 201 -and $ticketId -and $traceId) "trace=$traceId"

# --- PostgreSQL: the ticket row exists at creation (source of truth) ---
$dbRow = Invoke-Psql "SELECT count(*) FROM tickets WHERE ""Id""='$ticketId'"
Check 'PostgreSQL: ticket row persisted' ($dbRow -eq '1') "count=$dbRow"

# --- Outbox + Kafka + Automation: ticket escalates High -> Urgent only after the consumer runs ---
$escalated = Wait-For { ((Invoke-Api -Path "/api/tickets/$ticketId" -Token $HO).Json.priority) -eq 3 } 40
Check 'Kafka consumer ran AUTOMATION: priority escalated High->Urgent' $escalated "now=$((Invoke-Api -Path "/api/tickets/$ticketId" -Token $HO).Json.priority)"

# --- Outbox: this ticket's events were published (ProcessedAt set), none left pending ---
$published = Wait-For { ((Invoke-Psql "SELECT count(*) FROM outbox_messages WHERE ""Payload""::text LIKE '%$ticketId%' AND ""ProcessedAt"" IS NOT NULL") -as [int]) -ge 1 } 30
Check 'Outbox: ticket.created event published to Kafka' $published
$pending = Invoke-Psql "SELECT count(*) FROM outbox_messages WHERE ""Payload""::text LIKE '%$ticketId%' AND ""ProcessedAt"" IS NULL"
Check 'Outbox: no ticket events left unpublished' ($pending -eq '0') "pending=$pending"

# --- Kafka consumption recorded idempotently ---
$proc = Invoke-Psql "SELECT count(*) FROM processed_messages WHERE ""Topic""='scaledesk.ticket.created'"
Check 'Kafka: ticket.created consumed (processed_messages marker)' ((($proc -as [int]) -ge 1)) "count=$proc"

# --- Assign -> notification for the agent (consumer projection), observed via the agent's feed ---
$assign = Invoke-Api -Method Patch -Path "/api/tickets/$ticketId/assign" -Token $HO -Body @{ assigneeUserId=$agentId; teamId=$null }
Check 'ticket assigned to agent (200)' ($assign.Status -eq 200) "HTTP $($assign.Status)"

$gotNotif = Wait-For {
    $n = (Invoke-Api -Path '/api/notifications?take=50' -Token $agent.accessToken).Json
    @($n | Where-Object { $_.type -eq 'ticket.assigned' -and $_.relatedTicketId -eq $ticketId }).Count -ge 1
} 30
Check 'Kafka consumer created NOTIFICATION for assignee' $gotNotif

# --- Realtime (SignalR): delivery-only; DB is source of truth. Confirm the durable state is correct. ---
$final = Invoke-Api -Path "/api/tickets/$ticketId" -Token $HO
Check 'final ticket state durable (assignee set, status advanced)' ($final.Json.assigneeUserId -eq $agentId) "assignee=$($final.Json.assigneeUserId)"

Write-Host "  trace/correlation id for this journey: $traceId  (searchable in Jaeger :16686)"
if (-not (Complete-Scenario 'Ticket journey (DB->Outbox->Kafka->Automation->Notification)')) { exit 1 }
