# Stage 5D — provision an authenticated token pool for k6.
#
# Logs in the seeded Agent users (2 per load tenant) and, for each, captures a same-tenant customerId
# and a handful of ticketIds, so k6's create/update/assign/comment operations hit real rows without
# cross-tenant noise. Output: ops/load/ctx.json, read by every k6 scenario via open('./ctx.json').
#
# Access tokens come back with a 600-minute TTL (docker-compose.load.yml) so they never expire mid-ramp.
$ErrorActionPreference = 'Stop'
$api = if ($env:SD_API) { $env:SD_API } else { 'http://localhost:8080' }
$pw  = 'Passw0rd!'

function Invoke-Api {
    param([string]$Method='GET', [string]$Path, [string]$Token, $Body)
    $headers = @{}
    if ($Token) { $headers['Authorization'] = "Bearer $Token" }
    $p = @{ Uri="$api$Path"; Method=$Method; Headers=$headers; UseBasicParsing=$true; TimeoutSec=30 }
    if ($null -ne $Body) { $p['ContentType']='application/json'; $p['Body']=($Body | ConvertTo-Json -Depth 8) }
    $resp = Invoke-WebRequest @p
    $json = $null; if ($resp.Content) { try { $json = $resp.Content | ConvertFrom-Json } catch {} }
    return $json
}

Write-Host "== tokenpool : $api =="
$pool = New-Object System.Collections.ArrayList

for ($t=1; $t -le 20; $t++) {
    $slug = "load-tenant-$t"
    for ($n=1; $n -le 2; $n++) {
        $email = "agent$n@$slug.test"
        $tok = Invoke-Api -Method Post -Path '/api/auth/login' -Body @{ email=$email; password=$pw; organizationSlug=$slug }
        if (-not $tok -or -not $tok.accessToken) { throw "login failed for $email" }
        $at = $tok.accessToken

        $me       = Invoke-Api -Path '/api/auth/me' -Token $at
        $cust     = Invoke-Api -Path '/api/customers?pageSize=1' -Token $at
        $tickets  = Invoke-Api -Path '/api/tickets?pageSize=20&status=Open' -Token $at
        $ticketIds = @($tickets.items | ForEach-Object { $_.id })
        $customerId = $cust.items[0].id

        $null = $pool.Add([pscustomobject]@{
            email = $email; slug = $slug; tenantId = $me.tenantId; token = $at
            customerId = $customerId; ticketIds = $ticketIds
        })
    }
    Write-Host "  provisioned tenant $slug (users 1..2)"
}

$out = "$PSScriptRoot\..\ctx.json"   # ops/load/ctx.json — mounted into k6 as /scripts/ctx.json
# Write WITHOUT a BOM so JSON.parse (and any other consumer) reads it cleanly.
[System.IO.File]::WriteAllText($out, ($pool | ConvertTo-Json -Depth 6), (New-Object System.Text.UTF8Encoding($false)))
Write-Host ("ctx.json written: {0} users, sample tickets/user >= {1}" -f $pool.Count, (($pool | ForEach-Object { $_.ticketIds.Count } | Measure-Object -Minimum).Minimum))
