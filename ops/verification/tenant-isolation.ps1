# 5C / Scenario 3 — Tenant isolation, defense-in-depth (application EF filter + PostgreSQL RLS).
# TechNova (A) attacks Acme (B) resources across every surface, then we bypass the app entirely and
# query the DB as the least-privilege role to prove RLS enforces isolation independently of the filter.
. "$PSScriptRoot\_common.ps1"

Write-Host "== tenant-isolation : $script:Api =="

$A = Invoke-Login 'rahul@technova.com' 'technova'
$B = Invoke-Login 'dana@acme.com'      'acme'
$HA = $A.accessToken; $HB = $B.accessToken

$meA = (Invoke-Api -Path '/api/auth/me' -Token $HA).Json
$meB = (Invoke-Api -Path '/api/auth/me' -Token $HB).Json
Check 'two distinct tenants resolved' ($meA.tenantId -ne $meB.tenantId) "A=$($meA.tenantId) B=$($meB.tenantId)"

# --- Enumerate Acme (B) resources to target ---
$bTicket  = (Invoke-Api -Path '/api/tickets?pageSize=1'   -Token $HB).Json.items[0].id
$bCustomer= (Invoke-Api -Path '/api/customers?pageSize=1' -Token $HB).Json.items[0].id
$bSla     = (Invoke-Api -Path '/api/sla-policies'         -Token $HB).Json[0].id
$bTeam    = (Invoke-Api -Method Post -Path '/api/teams' -Token $HB -Body @{ name="Iso Team $([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())"; description='for isolation test' }).Json.id
Check 'Acme resources captured (ticket/customer/sla/team)' ($bTicket -and $bCustomer -and $bSla -and $bTeam)

# --- TechNova (A) attacks every Acme (B) surface: all must be denied (404, foreign id invisible) ---
Check 'A GET B ticket -> blocked'        ((Invoke-Api -Path "/api/tickets/$bTicket" -Token $HA).Status -eq 404)
Check 'A PATCH B ticket status -> blocked' ((Invoke-Api -Method Patch -Path "/api/tickets/$bTicket/status" -Token $HA -Body @{ status=1 }).Status -eq 404)
Check 'A COMMENT on B ticket -> blocked'  ((Invoke-Api -Method Post -Path "/api/tickets/$bTicket/comments" -Token $HA -Body @{ body='intrusion'; isInternal=$false }).Status -eq 404)
Check 'A ASSIGN B ticket -> blocked'      ((Invoke-Api -Method Patch -Path "/api/tickets/$bTicket/assign" -Token $HA -Body @{ assigneeUserId=$null; teamId=$null }).Status -eq 404)
Check 'A GET B customer -> blocked'       ((Invoke-Api -Path "/api/customers/$bCustomer" -Token $HA).Status -eq 404)
Check 'A UPDATE B customer -> blocked'    ((Invoke-Api -Method Put -Path "/api/customers/$bCustomer" -Token $HA -Body @{ name='Hacked'; email='h@x.io'; phone=$null; company=$null; tags=@(); metadata=$null }).Status -eq 404)
Check 'A DELETE B customer -> blocked'    ((Invoke-Api -Method Delete -Path "/api/customers/$bCustomer" -Token $HA).Status -eq 404)
Check 'A GET B team -> blocked'           ((Invoke-Api -Path "/api/teams/$bTeam" -Token $HA).Status -eq 404)
Check 'A UPDATE B sla policy -> blocked'  ((Invoke-Api -Method Put -Path "/api/sla-policies/$bSla" -Token $HA -Body @{ name='Hijack'; priority=1; responseDueInMinutes=1; resolutionDueInMinutes=1; isDefault=$false }).Status -eq 404)
Check 'A DELETE B sla policy -> blocked'  ((Invoke-Api -Method Delete -Path "/api/sla-policies/$bSla" -Token $HA).Status -eq 404)

# --- Layer 1 (EF filter): B's ticket must never appear in A's listing ---
$aTickets = (Invoke-Api -Path '/api/tickets?pageSize=100' -Token $HA).Json.items.id
Check 'B ticket absent from A listing (EF filter)' ($aTickets -notcontains $bTicket)

# --- Layer 2 (PostgreSQL RLS): bypass the app, query as the least-privilege role ---
$rlsA = Invoke-PsqlTenant $meA.tenantId "SELECT count(*) FROM tickets WHERE ""Id""='$bTicket'"
Check 'RLS: tenant A session cannot see B ticket row' ($rlsA -eq '0') "count=$rlsA"

$rlsB = Invoke-PsqlTenant $meB.tenantId "SELECT count(*) FROM tickets WHERE ""Id""='$bTicket'"
Check 'RLS positive control: tenant B sees its own ticket' ($rlsB -eq '1') "count=$rlsB"

$rlsUnset = ('SELECT count(*) FROM tickets' | docker exec -i -e PGPASSWORD=scaledesk_app_pw $script:PgCont psql -U scaledesk_app -d scaledesk -tA 2>$null | Out-String).Trim()
Check 'RLS: no tenant bound -> zero rows (default deny)' ($rlsUnset -eq '0') "count=$rlsUnset"

# The superuser (RLS-exempt) sees B's ticket — proving the row EXISTS, so the blocks are real.
$super = Invoke-Psql "SELECT count(*) FROM tickets WHERE ""Id""='$bTicket'"
Check 'control: superuser sees B ticket exists (blocks are meaningful)' ($super -eq '1') "count=$super"

if (-not (Complete-Scenario 'Tenant isolation (EF filter + RLS)')) { exit 1 }
