# Stage 7 / J3 — Customer Portal journey verification.
# Register (link-by-email) -> login -> own-ticket list -> create -> staff visibility ->
# internal-note invisibility -> customer reply -> close-only-when-resolved -> ownership 404 ->
# scope matrix -> refresh rotation + reuse detection -> logout.
# NOTE: hits the "auth" rate limit bucket (10/min/IP) ~6 times — space consecutive runs ~45s.
$ErrorActionPreference = 'Stop'
$base = 'http://127.0.0.1:8080'
$pass = 0; $fail = 0

function Call($method, $path, $token, $body) {
    $headers = @{}
    if ($token) { $headers['Authorization'] = "Bearer $token" }
    try {
        $params = @{ Uri = "$base$path"; Method = $method; Headers = $headers; UseBasicParsing = $true }
        if ($null -ne $body) {
            $params['ContentType'] = 'application/json'
            $params['Body'] = ($body | ConvertTo-Json -Depth 6)
        }
        $r = Invoke-WebRequest @params
        # /health/* answers plain text ("Healthy"), so never let JSON parsing decide success here.
        $json = $null
        try { $json = $r.Content | ConvertFrom-Json } catch { $json = $null }
        return @{ Status = $r.StatusCode; Json = $json; Raw = $r.Content }
    } catch {
        $resp = $_.Exception.Response
        if ($null -eq $resp) { throw }
        $reader = New-Object System.IO.StreamReader($resp.GetResponseStream())
        $raw = $reader.ReadToEnd()
        $status = [int]$resp.StatusCode
        $json = if ($raw) { try { $raw | ConvertFrom-Json } catch { $null } } else { $null }
        return @{ Status = $status; Json = $json; Raw = $raw }
    }
}

function Check($name, $ok, $detail) {
    if ($ok) { Write-Host "PASS  $name" -ForegroundColor Green; $script:pass++ }
    else { Write-Host "FAIL  $name  -> $detail" -ForegroundColor Red; $script:fail++ }
}

# Reads a (claim) from the JWT payload — the host uses MapInboundClaims=false, so names are verbatim.
function Get-Claim($token, $claim) {
    $p = $token.Split('.')[1].Replace('-','+').Replace('_','/')
    switch ($p.Length % 4) { 2 { $p += '==' } 3 { $p += '=' } }
    ([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($p)) | ConvertFrom-Json).$claim
}

# 0) Wait for API health
$healthy = $false
for ($i = 0; $i -lt 30; $i++) {
    try {
        $h = Call 'GET' '/health/ready' $null $null
        if ($h.Status -eq 200) { $healthy = $true; break }
    } catch { Start-Sleep -Seconds 2 }
    Start-Sleep -Seconds 2
}
Check 'api healthy' $healthy 'timeout waiting for /health/ready'
if (-not $healthy) { exit 1 }

# 1) Staff session (the "other side" of the journey)
$staff = Call 'POST' '/api/auth/demo' $null @{}
Check 'staff demo login 200' ($staff.Status -eq 200) "got $($staff.Status)"
$stok = $staff.Json.accessToken

# 2) Portal register — claims the seeded customer priya@acme-retail.example by email.
#    Idempotent re-run: the user now exists, so register verifies the password instead.
$reg = Call 'POST' '/api/portal/register' $null @{
    organizationSlug = 'demo'; email = 'priya@acme-retail.example'
    password = 'PortalPass2026!'; firstName = 'Priya'; lastName = 'Nair'
}
Check 'portal register 201 + tokens' ($reg.Status -eq 201 -and $reg.Json.accessToken -and $reg.Json.refreshToken) "got $($reg.Status) $($reg.Raw)"

# 3) Portal login
$por = Call 'POST' '/api/portal/login' $null @{
    organizationSlug = 'demo'; email = 'priya@acme-retail.example'; password = 'PortalPass2026!'
}
Check 'portal login 200' ($por.Status -eq 200) "got $($por.Status) $($por.Raw)"
$ptok = $por.Json.accessToken
$prefresh = $por.Json.refreshToken
$cir = Get-Claim $ptok 'cid'
Check 'portal token carries cid + no role claim' ($cir -and -not (Get-Claim $ptok 'role')) "cid=$cir"

# 4) Own-ticket list is pinned to the caller
$list = Call 'GET' '/api/portal/tickets?page=1&pageSize=100' $ptok $null
Check 'GET /api/portal/tickets 200 + paged' ($list.Status -eq 200 -and $null -ne $list.Json.totalCount) "got $($list.Status) $($list.Raw)"
$mine = @($list.Json.items)
Check 'portal sees only own tickets' ($mine.Count -ge 2 -and ($mine | Where-Object { $_.customerId -ne $cir }).Count -eq 0) "count=$($mine.Count)"

# 5) Portal intake — no self-routing / self-assignment possible
$new = Call 'POST' '/api/portal/tickets' $ptok @{
    subject = 'Portal verify ticket'; description = 'Created by verify-portal.ps1'
    priority = 2; category = 0
}
Check 'POST /api/portal/tickets 201' ($new.Status -eq 201) "got $($new.Status) $($new.Raw)"
$tid = $new.Json.id
Check 'ticket pinned to caller cid, unrouted' ($new.Json.customerId -eq $cir -and $null -eq $new.Json.teamId -and $null -eq $new.Json.assigneeUserId) ($new | ConvertTo-Json -Compress)

# 6) Staff side sees what the customer filed
$asStaff = Call 'GET' "/api/tickets/$tid" $stok $null
Check 'staff can open portal ticket 200' ($asStaff.Status -eq 200 -and $asStaff.Json.subject -eq 'Portal verify ticket') "got $($asStaff.Status) $($asStaff.Raw)"

# 7) Internal note is invisible to the portal
$note = Call 'POST' "/api/tickets/$tid/comments" $stok @{ body = 'INTERNAL: secret escalation'; isInternal = $true }
Check 'staff adds internal note 201' ($note.Status -eq 201) "got $($note.Status) $($note.Raw)"
$pcomments = Call 'GET' "/api/portal/tickets/$tid/comments" $ptok $null
Check 'portal comments 200 + internal hidden' ($pcomments.Status -eq 200 -and `
    -not (@($pcomments.Json | Where-Object { $_.isInternal -or $_.body -like '*INTERNAL*' }).Count)) ($pcomments | ConvertTo-Json -Compress)

# 8) Customer reply is attributed to the customer, never internal
$reply = Call 'POST' "/api/portal/tickets/$tid/comments" $ptok @{ body = 'Thanks, any update?' }
Check 'portal reply 201 attributed to cid' ($reply.Status -eq 201 -and $null -eq $reply.Json.authorUserId -and $reply.Json.customerId -eq $cir -and -not $reply.Json.isInternal) "got $($reply.Status) $($reply.Raw)"

# 9) Close handshake: only own + only when resolved
$early = Call 'POST' "/api/portal/tickets/$tid/close" $ptok $null
Check 'close while open -> 400 not_resolved' ($early.Status -eq 400 -and $early.Json.type -like '*not_resolved*') "got $($early.Status) $($early.Raw)"
$res = Call 'PATCH' "/api/tickets/$tid/status" $stok @{ status = 4 }
Check 'staff resolves ticket 200 status=Resolved(4)' ($res.Status -eq 200 -and $res.Json.status -eq 4) "got $($res.Status) $($res.Raw)"
$closed = Call 'POST' "/api/portal/tickets/$tid/close" $ptok $null
Check 'customer closes resolved ticket 200 status=Closed(5)' ($closed.Status -eq 200 -and $closed.Json.status -eq 5) "got $($closed.Status) $($closed.Raw)"

# 10) Someone else's ticket is a 404, not a 403 (no row enumeration)
$all = Call 'GET' '/api/tickets?page=1&pageSize=100' $stok $null
$f2 = @($all.Json.items | Where-Object { $_.customerId -ne $cir } | Select-Object -First 1)
if ($f2.Count -eq 1) {
    $fh = Call 'GET' "/api/portal/tickets/$($f2[0].id)" $ptok $null
    Check 'portal GET foreign ticket -> 404' ($fh.Status -eq 404) "got $($fh.Status) $($fh.Raw)"
} else {
    Check 'portal GET foreign ticket -> 404' $false 'no foreign ticket found in staff list'
}

# 11) Scope matrix — each session type is confined to its URL space
$sx1 = Call 'GET' '/api/portal/tickets' $stok $null
Check 'staff token on /api/portal -> 403' ($sx1.Status -eq 403) "got $($sx1.Status)"
$sx2 = Call 'GET' '/api/tickets' $ptok $null
Check 'portal token on /api/tickets -> 403' ($sx2.Status -eq 403) "got $($sx2.Status)"
$sx3 = Call 'GET' '/api/auth/me' $ptok $null
Check 'portal token on /api/auth/me -> 403' ($sx3.Status -eq 403) "got $($sx3.Status)"

# 12) Refresh rotation + reuse detection (kills the family, so it goes last)
$rot = Call 'POST' '/api/portal/refresh' $null @{ refreshToken = $prefresh }
Check 'portal refresh rotates tokens 200' ($rot.Status -eq 200 -and $rot.Json.refreshToken -ne $prefresh) "got $($rot.Status) $($rot.Raw)"
$reuse = Call 'POST' '/api/portal/refresh' $null @{ refreshToken = $prefresh }
Check 'replaying old refresh token -> 401 invalid_refresh' ($reuse.Status -eq 401 -and $reuse.Json.type -like '*invalid_refresh*') "got $($reuse.Status) $($reuse.Raw)"

# 13) Logout revokes the live refresh token
$lo = Call 'POST' "/api/portal/logout?refreshToken=$($rot.Json.refreshToken)" $rot.Json.accessToken $null
Check 'portal logout 204' ($lo.Status -eq 204) "got $($lo.Status) $($lo.Raw)"
$after = Call 'POST' '/api/portal/refresh' $null @{ refreshToken = $rot.Json.refreshToken }
Check 'refresh after logout -> 401' ($after.Status -eq 401) "got $($after.Status)"

Write-Host ''
Write-Host ("RESULT: {0} passed, {1} failed" -f $pass, $fail) -ForegroundColor $(if ($fail -eq 0) { 'Green' } else { 'Red' })
exit $(if ($fail -eq 0) { 0 } else { 1 })
