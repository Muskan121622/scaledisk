# ScaleDesk — role/journey verification (all five journeys, single sweep)
# J1 Platform: super admin cross-tenant view + suspend -> blocked login -> resume
# J2 Admin:    invite agent -> token from dev email log -> accept -> agent login
# J3 Portal:   customer register -> file ticket -> staff reply <-> customer read
# J4 Agent:    work a ticket end-to-end + confirm admin actions are forbidden
# J5 Isolation: fresh tenant must not see demo-tenant data
# Run: powershell -File ops\verification\verify-journeys.ps1   (stack must be up)
# NOTE: ~9-10 "auth"-bucket calls (10/min/IP); the Auth-Call wrapper waits out any 429.

$ErrorActionPreference = 'Continue'
$base = 'http://localhost:8080'
$ts   = Get-Date -Format 'MMddHHmmss'
$agentEmail = "riya.verify.$ts@test.local"
$agentPass  = 'AgentPass2026!'
$results = @()

function Check($name, $expect, $actual) {
    $script:results += [pscustomobject]@{ Check = $name; Expected = $expect; Actual = "$actual"; Pass = ("$actual" -eq "$expect") }
    Write-Host ("{0,-58} exp={1,-6} got={2,-6} {3}" -f $name, $expect, $actual, $(if ("$actual" -eq "$expect") { 'PASS' } else { 'FAIL' }))
}

function Call($method, $path, $token, $body) {
    $p = @{ Uri = "$base$path"; Method = $method; UseBasicParsing = $true; TimeoutSec = 30 }
    if ($token)     { $p.Headers = @{ Authorization = "Bearer $token" } }
    if ($null -ne $body) { $p.ContentType = 'application/json'; $p.Body = ($body | ConvertTo-Json -Depth 6) }
    try {
        $r = Invoke-WebRequest @p
        $json = if ($r.Content) { $r.Content | ConvertFrom-Json } else { $null }
        return @{ code = [int]$r.StatusCode; json = $json }
    } catch {
        $resp = $_.Exception.Response
        if (-not $resp) { return @{ code = -1; json = $null; err = $_.Exception.Message } }
        $content = try { $_.ErrorDetails.Message } catch { $null }
        $json = if ($content) { try { $content | ConvertFrom-Json } catch { $null } } else { $null }
        return @{ code = [int]$resp.StatusCode; json = $json }
    }
}

# Parse a claim out of the JWT payload (host uses MapInboundClaims=false -> names verbatim).
function Get-Claim($token, $claim) {
    $p = $token.Split('.')[1].Replace('-','+').Replace('_','/')
    switch ($p.Length % 4) { 2 { $p += '==' } 3 { $p += '=' } }
    ([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($p)) | ConvertFrom-Json).$claim
}
# Every /api/auth + /api/portal login/register hits the 10-req/min "auth" fixed window.
# A 429 here is expected throttling, not a failure: wait it out and retry. Other codes pass through.
function Auth-Call($path, $body) {
    for ($try = 1; $try -le 5; $try++) {
        $r = Call 'POST' $path $null $body
        if ($r.code -eq 429) { Write-Host "  (auth rate-limited; waiting 30s, retry $try/5)"; Start-Sleep -Seconds 30; continue }
        return $r
    }
    return $r
}

Write-Host "`n=== J1: Super admin (platform) - cross-tenant view + suspend/resume ===" -ForegroundColor Cyan
$plat = Auth-Call '/api/auth/platform/login' @{ email = 'platform@scaledesk.app'; password = 'DemoPass2026!' }
Check 'platform login' 200 $plat.code
$ptok = $plat.json.accessToken
$pstats = Call GET '/api/platform/stats' $ptok $null
Check 'GET /api/platform/stats' 200 $pstats.code
$ptenants = Call GET '/api/platform/tenants?page=1&pageSize=50' $ptok $null
Check 'GET /api/platform/tenants (cross-tenant)' 200 $ptenants.code
$demoRow = @($ptenants.json.items | Where-Object { $_.slug -eq 'demo' })
Check 'demo tenant listed platform-wide' 1 $demoRow.Count
$demoTenantId = $demoRow[0].id
$staffProbe = Auth-Call '/api/auth/demo' $null
$leak = Call GET '/api/platform/stats' $staffProbe.json.accessToken $null
Check 'staff token on /api/platform -> 403' 403 $leak.code
$susp = Call POST "/api/platform/tenants/$demoTenantId/suspend" $ptok $null
Check 'suspend demo tenant' 200 $susp.code
$suspLogin = Auth-Call '/api/auth/demo' $null
Check 'demo staff login while suspended -> 403' 403 $suspLogin.code
$resume = Call POST "/api/platform/tenants/$demoTenantId/resume" $ptok $null
Check 'resume demo tenant' 200 $resume.code
$resumeLogin = Auth-Call '/api/auth/demo' $null
Check 'demo staff login after resume -> 200' 200 $resumeLogin.code

Write-Host "`n=== J2: Admin invites an agent (email-verification-through-acceptance) ===" -ForegroundColor Cyan
$demo = Auth-Call '/api/auth/demo' $null
Check 'demo login (admin)' 200 $demo.code
$admin = $demo.json.accessToken

$inv = Call POST '/api/invitations' $admin @{ email = $agentEmail; role = 3 }
Check 'POST /api/invitations (role=Agent)' 201 $inv.code

Start-Sleep -Seconds 2
$logLine = (docker logs scaledesk-api --since 1m 2>&1 | Out-String)
$m = [regex]::Match($logLine, 'accept-invite\?token=([A-Za-z0-9\-_\.=]+)')
if (-not $m.Success) { Write-Host 'Could not find invite token in api logs - aborting J2/J4' -ForegroundColor Red; $accept = $null }
else {
    $inviteToken = $m.Groups[1].Value
    $accept = Call POST '/api/invitations/accept' $null @{ token = $inviteToken; firstName = 'Riya'; lastName = 'Verma'; password = $agentPass }
    Check 'POST /api/invitations/accept (anonymous, token)' 200 $accept.code
    if ($accept.json) { Write-Host ("  accepted as: {0} / role {1} / tenant {2}" -f $accept.json.email, $accept.json.role, $accept.json.tenantName) }

    $alogin = Auth-Call '/api/auth/login' @{ email = $agentEmail; password = $agentPass; organizationSlug = 'demo' }
    Check 'agent login (real credentials)' 200 $alogin.code
    $agentTok = $alogin.json.accessToken

    $me = Call GET '/api/auth/me' $agentTok $null
    Write-Host ("  agent role={0} perms={1}" -f $me.json.role, ($me.json.permissions -join ','))
}

Write-Host "`n=== J3: Customer portal <-> staff (two-way conversation) ===" -ForegroundColor Cyan
$creg = Auth-Call '/api/portal/register' @{ organizationSlug = 'demo'; email = 'priya@acme-retail.example'; password = 'PortalPass2026!'; firstName = 'Priya'; lastName = 'Nair' }
Check 'portal register (link-by-email)' 201 $creg.code
$clogin = Auth-Call '/api/portal/login' @{ organizationSlug = 'demo'; email = 'priya@acme-retail.example'; password = 'PortalPass2026!' }
Check 'portal login' 200 $clogin.code
$ctok = $clogin.json.accessToken
$cid  = Get-Claim $ctok 'cid'
Check 'portal token carries cid claim' 'True' ([bool]$cid)
$ct = Call POST '/api/portal/tickets' $ctok @{ subject = "Portal journey $ts"; description = 'Filed via verify-journeys J3.'; priority = 2; category = 0 }
Check 'customer files ticket (same intake pipeline)' 201 $ct.code
$ctId = $ct.json.id
$asStaff = Call GET "/api/tickets/$ctId" $admin $null
Check 'staff sees the portal ticket' 200 $asStaff.code
$inote  = Call POST "/api/tickets/$ctId/comments" $admin @{ body = "INTERNAL-${ts} - not customer-visible"; isInternal = $true }
Check 'staff posts internal note' 201 $inote.code
$preply = Call POST "/api/tickets/$ctId/comments" $admin @{ body = "We are on it ($ts)"; isInternal = $false }
Check 'staff posts public reply' 201 $preply.code
$cmts = Call GET "/api/portal/tickets/$ctId/comments" $ctok $null
Check 'customer reads ticket comments' 200 $cmts.code
$sawPublic   = @($cmts.json | Where-Object { $_.body -eq "We are on it ($ts)" }).Count
$sawInternal = @($cmts.json | Where-Object { $_.isInternal -or $_.body -like "INTERNAL-*" }).Count
Check 'customer sees staff public reply' 1 $sawPublic
Check 'internal note hidden from customer' 0 $sawInternal
$foreignList = Call GET '/api/tickets?page=1&pageSize=100' $admin $null
$foreign = @($foreignList.json.items | Where-Object { $_.customerId -and $_.customerId -ne $cid } | Select-Object -First 1)
if ($foreign.Count -ge 1) {
    $fget = Call GET "/api/portal/tickets/$($foreign[0].id)" $ctok $null
    Check 'customer cannot open foreign ticket (404)' 404 $fget.code
} else { Write-Host '  (no foreign-customer ticket found; skipping isolation check)' }
$scross = Call GET '/api/tickets' $ctok $null
Check 'portal token on staff /api/tickets -> 403' 403 $scross.code

Write-Host "`n=== J4: Agent works a ticket end-to-end ===" -ForegroundColor Cyan
if ($agentTok) {
    $cust = Call GET '/api/customers?page=1&pageSize=1' $admin $null
    $custId = $cust.json.items[0].id
    $t1 = Call GET '/api/tickets?page=1' $agentTok $null
    Check 'agent GET /api/tickets (tickets.read)' 200 $t1.code

    $new = Call POST '/api/tickets' $agentTok @{ customerId = $custId; subject = "Journey verify $ts"; description = 'Created by verify-journeys script.'; priority = 2; category = 0 }
    Check 'agent POST /api/tickets (create)' 201 $new.code
    $tid = $new.json.id

    $asg = Call PATCH "/api/tickets/$tid/assign" $admin @{ assigneeUserId = $me.json.userId; teamId = $null }
    Check 'admin assigns ticket to agent' 200 $asg.code

    $cmt = Call POST "/api/tickets/$tid/comments" $agentTok @{ body = 'Investigating now.'; isInternal = $false }
    Check 'agent replies (public comment)' 201 $cmt.code

    $st = Call PATCH "/api/tickets/$tid/status" $agentTok @{ status = 3 }
    Check 'agent sets status Resolved' 200 $st.code

    $ev = Call GET "/api/tickets/$tid/events" $agentTok $null
    Write-Host ("  activity events recorded: {0}" -f @($ev.json).Count)

    $sla = Call GET '/api/sla-policies' $agentTok $null
    Check 'agent GET /api/sla-policies FORBIDDEN' 403 $sla.code
    $invb = Call POST '/api/invitations' $agentTok @{ email = "hacker$ts@test.local"; role = 1 }
    Check 'agent POST /api/invitations FORBIDDEN' 403 $invb.code
}

Write-Host "`n=== J5: Fresh tenant cannot see demo data ===" -ForegroundColor Cyan
$slug = "verify$ts"
$ownerEmail = "owner.$ts@test.local"
$reg = Auth-Call '/api/auth/register' @{ organizationName = 'Verify Co'; organizationSlug = $slug; email = $ownerEmail; password = 'VerifyPass2026!'; firstName = 'Val'; lastName = 'Owner' }
Check 'register new tenant (owner auto-verified)' 201 $reg.code
$newTok = $reg.json.accessToken
if (-not $newTok) { $lg = Auth-Call '/api/auth/login' @{ email = $ownerEmail; password = 'VerifyPass2026!'; organizationSlug = $slug }; $newTok = $lg.json.accessToken }

$myTickets = Call GET '/api/tickets?page=1' $newTok $null
Check 'new tenant GET /api/tickets -> 200' 200 $myTickets.code
$visible = $myTickets.json.totalItems
if ($null -eq $visible) { $visible = @($myTickets.json.items).Count }
Check 'new tenant sees ZERO demo tickets' 0 $visible

if ($tid) {
    $steal = Call GET "/api/tickets/$tid" $newTok $null
    Check 'new tenant GET demo ticket -> 404' 404 $steal.code
}
$mySla = Call GET '/api/sla-policies' $newTok $null
Write-Host ("  new tenant sla policies visible: {0} (its own only)" -f @($mySla.json).Count)

Write-Host "`n=== SUMMARY ===" -ForegroundColor Cyan
$results | Format-Table -AutoSize
$failed = @($results | Where-Object { -not $_.Pass })
if ($failed.Count -eq 0) { Write-Host "ALL $($results.Count) CHECKS PASSED" -ForegroundColor Green }
else { Write-Host "$($failed.Count)/$($results.Count) FAILED" -ForegroundColor Red; exit 1 }
