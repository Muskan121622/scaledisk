# Stage 7 / J1 — Super Admin (platform) journey verification.
# Checks: platform login, cross-tenant reads, scope enforcement both directions,
# and suspend -> login-403 -> resume -> login-200 for the demo tenant.
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

# 0) Wait for API health
$healthy = $false
for ($i = 0; $i -lt 30; $i++) {
    try {
        $h = Call 'GET' '/health/ready' $null $null
        if ($h.Status -eq 200) { $healthy = $true; break }
    } catch { Start-Sleep -Seconds 2 }
    Start-Sleep -Seconds 2
}
Check 'api healthy (auto-migrate + seed ran)' $healthy 'timeout waiting for /health/ready'
if (-not $healthy) { exit 1 }

# 1) Platform login
$pl = Call 'POST' '/api/auth/platform/login' $null @{ email = 'platform@scaledesk.app'; password = 'DemoPass2026!' }
Check 'platform login returns 200' ($pl.Status -eq 200) "got $($pl.Status) $($pl.Raw)"
$ptok = $pl.Json.accessToken
Check 'platform session carries access token' ([bool]$ptok) 'accessToken missing'

# 2) Platform-only endpoints with platform token
$stats = Call 'GET' '/api/platform/stats' $ptok $null
Check 'GET /api/platform/stats 200' ($stats.Status -eq 200) "got $($stats.Status) $($stats.Raw)"
Check 'stats show >=3 tenants' ($stats.Json.totalTenants -ge 3) ($stats.Json | ConvertTo-Json -Compress)
$tenants = Call 'GET' '/api/platform/tenants?page=1&pageSize=50' $ptok $null
Check 'GET /api/platform/tenants 200 + paged' ($tenants.Status -eq 200 -and $tenants.Json.totalCount -ge 3) "got $($tenants.Status)"
$demo = @($tenants.Json.items | Where-Object { $_.slug -eq 'demo' })
Check 'demo tenant visible in platform list' ($demo.Count -eq 1) ($demo | ConvertTo-Json -Compress)
$demoId = $demo[0].id

$staff = Call 'POST' '/api/auth/demo' $null @{}
Check 'demo (staff) login 200' ($staff.Status -eq 200) "got $($staff.Status)"
$stok = $staff.Json.accessToken
$one = Call "GET" "/api/platform/tenants/$demoId" $ptok $null
Check 'GET /api/platform/tenants/{id} 200' ($one.Status -eq 200) "got $($one.Status) $($one.Raw)"
Check 'tenant detail carries user/ticket counts' ($one.Json.userCount -ge 2 -and $one.Json.ticketCount -ge 1) ($one | ConvertTo-Json -Compress)

# 3) Scope enforcement, both directions
$leak1 = Call 'GET' '/api/platform/stats' $stok $null
Check 'staff token on /api/platform -> 403' ($leak1.Status -eq 403) "got $($leak1.Status) $($leak1.Raw)"
$leak2 = Call 'GET' '/api/tickets' $ptok $null
Check 'platform token on /api/tickets -> 403' ($leak2.Status -eq 403) "got $($leak2.Status) $($leak2.Raw)"
$badLogin = Call 'POST' '/api/auth/platform/login' $null @{ email = 'demo@scaledesk.app'; password = 'DemoPass2026!' }
Check 'staff account cannot use platform/login (401)' ($badLogin.Status -eq 401) "got $($badLogin.Status) $($badLogin.Raw)"

# 4) Suspend -> blocked login -> resume -> ok login
$susp = Call 'POST' "/api/platform/tenants/$demoId/suspend" $ptok $null
Check 'suspend demo tenant -> 200 status=Suspended(1)' ($susp.Status -eq 200 -and $susp.Json.status -eq 1) "got $($susp.Status) $($susp.Raw)"
$blocked = Call 'POST' '/api/auth/demo' $null @{}
Check 'staff login while suspended -> 403 tenant_suspended' ($blocked.Status -eq 403 -and $blocked.Json.type -like '*tenant_suspended*') "got $($blocked.Status) $($blocked.Raw)"
$resume = Call 'POST' "/api/platform/tenants/$demoId/resume" $ptok $null
Check 'resume demo tenant -> 200 status=Active(0)' ($resume.Status -eq 200 -and $resume.Json.status -eq 0) "got $($resume.Status) $($resume.Raw)"
$okAgain = Call 'POST' '/api/auth/demo' $null @{}
Check 'staff login after resume -> 200' ($okAgain.Status -eq 200) "got $($okAgain.Status) $($okAgain.Raw)"

Write-Host ''
Write-Host ("RESULT: {0} passed, {1} failed" -f $pass, $fail) -ForegroundColor $(if ($fail -eq 0) { 'Green' } else { 'Red' })
exit $(if ($fail -eq 0) { 0 } else { 1 })
