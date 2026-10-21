# Shared helpers for the ScaleDesk 5C production-verification suite (Windows PowerShell 5.1 safe).
# Dot-source from each scenario:  . "$PSScriptRoot\_common.ps1"
# Everything talks to the host-run API (:5102) and the compose data plane; Postgres assertions run
# as the superuser via `docker exec` so they can observe cross-tenant rows (outbox/processed) that
# RLS hides from the app's least-privilege role.
$ErrorActionPreference = 'Stop'
$script:Api      = if ($env:SD_API) { $env:SD_API } else { 'http://localhost:5102' }
$script:Pw       = 'Passw0rd!'
$script:PgCont   = 'scaledesk-postgres'

# --- HTTP -------------------------------------------------------------------
# Returns a structured result and NEVER throws on a non-2xx status, so callers can assert on
# 401/403/404 as *expected* outcomes. On transport failure it rethrows (that is a real error).
function Invoke-Api {
    param(
        [string]$Method = 'GET',
        [string]$Path,
        [string]$Token,
        $Body
    )
    $headers = @{}
    if ($Token) { $headers['Authorization'] = "Bearer $Token" }
    $params = @{ Uri = "$script:Api$Path"; Method = $Method; Headers = $headers; UseBasicParsing = $true; TimeoutSec = 30 }
    if ($null -ne $Body) {
        $params['ContentType'] = 'application/json'
        $params['Body'] = ($Body | ConvertTo-Json -Depth 8)
    }
    try {
        $resp = Invoke-WebRequest @params
        $json = $null
        if ($resp.Content) { try { $json = $resp.Content | ConvertFrom-Json } catch { $json = $null } }
        return @{ Status = [int]$resp.StatusCode; Ok = $true; Json = $json; Raw = $resp.Content; Headers = $resp.Headers }
    }
    catch {
        $r = $_.Exception.Response
        if ($null -eq $r) { throw }   # no HTTP response -> genuine transport error
        $stream = $r.GetResponseStream(); $reader = New-Object System.IO.StreamReader($stream)
        $content = $reader.ReadToEnd()
        $json = $null; if ($content) { try { $json = $content | ConvertFrom-Json } catch {} }
        return @{ Status = [int]$r.StatusCode; Ok = $false; Json = $json; Raw = $content; Headers = $r.Headers }
    }
}

function Invoke-Login {
    param([string]$Email, [string]$Slug)
    # The "auth" policy is a deliberate 10 req/IP/min fixed window. When scenarios run back-to-back the
    # window can still be hot from a neighbour's logins, so a 429 here is EXPECTED throttling, not a
    # failure: wait for the window to roll and retry. Other non-2xx (bad creds, locked) is a real error.
    for ($try = 1; $try -le 6; $try++) {
        $r = Invoke-Api -Method Post -Path '/api/auth/login' -Body @{ email = $Email; password = $script:Pw; organizationSlug = $Slug }
        if ($r.Ok) { return $r.Json }
        if ($r.Status -eq 429) { Write-Host "  (rate-limited; waiting out the auth window, retry $try/6)"; Start-Sleep -Seconds 30; continue }
        throw "login failed for $Email@$Slug -> HTTP $($r.Status): $($r.Raw)"
    }
    throw "login for $Email@$Slug stayed rate-limited (429) after retries"
}

# --- Postgres (superuser, bypasses RLS on purpose for assertions) -----------
# SQL is piped via stdin (docker exec -i) rather than passed with -c, because Windows native
# arg-passing strips the double-quotes this schema needs around its PascalCase column names.
function Invoke-Psql {
    param([string]$Sql)
    $out = $Sql | docker exec -i $script:PgCont psql -U scaledesk -d scaledesk -tA 2>$null
    return ($out | Out-String).Trim()
}

# Queries as the LEAST-PRIVILEGE app role with app.tenant_id bound, so PostgreSQL RLS (not the EF
# filter) is what decides visibility. This bypasses the application entirely: a row is returned only
# if the database's row-level policy allows it. Used to prove defense-in-depth isolation.
function Invoke-PsqlTenant {
    param([string]$TenantId, [string]$Sql)
    # Bind app.tenant_id via PGOPTIONS at connect time (not a SET statement) so psql echoes only
    # the query result, and the RLS policy's current_setting('app.tenant_id') is populated.
    $out = $Sql | docker exec -i -e PGPASSWORD=scaledesk_app_pw -e "PGOPTIONS=-c app.tenant_id=$TenantId" $script:PgCont psql -U scaledesk_app -d scaledesk -tA 2>$null
    return ($out | Out-String).Trim()
}

# --- Assertions / reporting -------------------------------------------------
$script:Checks = New-Object System.Collections.ArrayList
function Check {
    param([string]$Name, [bool]$Condition, [string]$Detail = '')
    $null = $script:Checks.Add([pscustomobject]@{ Name = $Name; Passed = [bool]$Condition; Detail = $Detail })
    $tag = if ($Condition) { 'PASS' } else { 'FAIL' }
    Write-Host ("  [{0}] {1}{2}" -f $tag, $Name, $(if ($Detail) { "  ($Detail)" } else { '' }))
}
function Complete-Scenario {
    param([string]$Scenario)
    $failed = @($script:Checks | Where-Object { -not $_.Passed })
    Write-Host ''
    if ($failed.Count -eq 0) {
        Write-Host "[PASS] $Scenario  ($($script:Checks.Count) checks)"
        return $true
    }
    Write-Host "[FAIL] $Scenario  ($($failed.Count)/$($script:Checks.Count) checks failed)"
    $failed | ForEach-Object { Write-Host "        - $($_.Name)  $($_.Detail)" }
    return $false
}
