# 5C / Scenario 1 — Authentication lifecycle & abuse resistance.
# Proves: register -> login -> refresh ROTATION (old token dies, new lives) -> reuse REJECTED,
# plus generic invalid-credentials (no user enumeration) and progressive lockout.
# Respects the 10 req/min "auth" rate limit by splitting across two fixed windows (one 62s pause).
. "$PSScriptRoot\_common.ps1"

Write-Host "== auth-e2e : $script:Api =="

# ---- Window A: happy path + rotation + reuse + invalid creds (<= 10 calls) ----
$ts   = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$slug = "e2e$ts"
$email = "owner$ts@e2e.test"

$reg = Invoke-Api -Method Post -Path '/api/auth/register' -Body @{
    organizationName = 'E2E Co'; organizationSlug = $slug; email = $email
    password = $script:Pw; firstName = 'Ed'; lastName = 'Two'
}
Check 'register returns 201 + tokens' ($reg.Status -eq 201 -and $reg.Json.accessToken) "HTTP $($reg.Status)"

$login = Invoke-Login 'rahul@technova.com' 'technova'
Check 'login seeded user issues access+refresh' ($login.accessToken -and $login.refreshToken)

# Rotation + theft response, on the throwaway session (isolated from the seeded users).
$old = $reg.Json.refreshToken
$rot = Invoke-Api -Method Post -Path '/api/auth/refresh' -Body @{ refreshToken = $old }
$new = $rot.Json.refreshToken
Check 'refresh rotates: new pair issued, old superseded' ($rot.Status -eq 200 -and $new -and $new -ne $old) "HTTP $($rot.Status)"

# Replay the OLD (already-rotated) token: a theft signal. Must be rejected AND revoke the whole family.
$reuse = Invoke-Api -Method Post -Path '/api/auth/refresh' -Body @{ refreshToken = $old }
Check 'reusing OLD refresh token is REJECTED (401)' ($reuse.Status -eq 401) "HTTP $($reuse.Status)"

# Hardening proof: reuse revoked the ENTIRE token family, so the legitimate NEW token is now dead too
# (a stolen-token replay logs out the real session, forcing re-authentication).
$fam = Invoke-Api -Method Post -Path '/api/auth/refresh' -Body @{ refreshToken = $new }
Check 'family revoked: the NEW refresh token is ALSO dead after reuse (401)' ($fam.Status -eq 401) "HTTP $($fam.Status)"

$badPw = Invoke-Api -Method Post -Path '/api/auth/login' -Body @{ email='rahul@technova.com'; password='wrong-pw'; organizationSlug='technova' }
Check 'invalid password -> 401 invalid_credentials' ($badPw.Status -eq 401) "HTTP $($badPw.Status)"

$unknown = Invoke-Api -Method Post -Path '/api/auth/login' -Body @{ email='ghost@nowhere.com'; password=$script:Pw; organizationSlug='technova' }
Check 'unknown user -> same 401 (no enumeration)' ($unknown.Status -eq 401) "HTTP $($unknown.Status)"

# ---- Window B: progressive lockout on the throwaway tenant (6 calls, fresh window) ----
Write-Host '  ... waiting out the auth rate-limit window (62s) before lockout test ...'
Start-Sleep -Seconds 62

$locked = $false
for ($i = 1; $i -le 5; $i++) {
    $r = Invoke-Api -Method Post -Path '/api/auth/login' -Body @{ email=$email; password='nope'; organizationSlug=$slug }
    if ($r.Status -eq 423) { $locked = $true; break }   # may lock before the 5th if window shared
}
Check '5 failed attempts trigger lockout' ($locked -or (Invoke-Api -Method Post -Path '/api/auth/login' -Body @{ email=$email; password=$script:Pw; organizationSlug=$slug }).Status -eq 423)

# Correct password while locked must STILL be refused (423), proving the lock gates auth.
$whileLocked = Invoke-Api -Method Post -Path '/api/auth/login' -Body @{ email=$email; password=$script:Pw; organizationSlug=$slug }
Check 'correct password while locked -> 423' ($whileLocked.Status -eq 423) "HTTP $($whileLocked.Status)"

if (-not (Complete-Scenario 'Authentication lifecycle')) { exit 1 }
