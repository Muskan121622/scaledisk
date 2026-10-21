# ScaleDesk — application-layer tenant-isolation attack test (spec §40 / Stage 5A).
#
# Proves the FIRST boundary (EF Core global query filter): a user authenticated into one tenant
# receives 404 for another tenant's ticket id — the row is invisible, not merely unauthorized.
# (The DB-level boundary is proved separately by rls-attack.sql.)
#
# Usage (needs the API + seeded TechNova/Acme tenants running):
#   powershell -NoProfile -ExecutionPolicy Bypass -File ops/verification/http-attack.ps1 -BaseUri http://localhost:5080
param(
    [string]$BaseUri = 'http://localhost:5080',
    [string]$Password = 'Passw0rd!'
)
$ErrorActionPreference = 'Stop'

function Get-Token([string]$email, [string]$slug) {
    $body = @{ email = $email; password = $Password; organizationSlug = $slug } | ConvertTo-Json
    (Invoke-RestMethod -Uri "$BaseUri/api/auth/login" -Method Post -ContentType 'application/json' -Body $body).accessToken
}
function Get-Tickets([string]$token) {
    (Invoke-RestMethod -Uri "$BaseUri/api/tickets" -Headers @{ Authorization = "Bearer $token" }).items
}
function Get-Status([string]$id, [string]$token) {
    try {
        $r = Invoke-WebRequest -Uri "$BaseUri/api/tickets/$id" -Headers @{ Authorization = "Bearer $token" } -UseBasicParsing
        return [int]$r.StatusCode
    } catch { return [int]$_.Exception.Response.StatusCode }
}

$tokA = Get-Token 'rahul@technova.com' 'technova'
$tokB = Get-Token 'dana@acme.com' 'acme'

$techNovaTickets = Get-Tickets $tokA
$acmeTickets     = Get-Tickets $tokB

Write-Host "=== TechNova (rahul) sees $($techNovaTickets.Count) ticket(s) ==="
$techNovaTickets | ForEach-Object { Write-Host ("   - {0} :: {1}" -f $_.id, $_.subject) }
Write-Host "=== Acme (dana) sees $($acmeTickets.Count) ticket(s) ==="
$acmeTickets | ForEach-Object { Write-Host ("   - {0} :: {1}" -f $_.id, $_.subject) }

$acmeId = $acmeTickets[0].id
$techNovaId = $techNovaTickets[0].id

$codeA = Get-Status $acmeId $tokA
$codeB = Get-Status $techNovaId $tokB

Write-Host ""
Write-Host ("[ATTACK] TechNova -> GET /api/tickets/{0} (Acme's ticket)  => HTTP {1}  (expected 404)" -f $acmeId, $codeA)
Write-Host ("[ATTACK] Acme     -> GET /api/tickets/{0} (TechNova's ticket) => HTTP {1}  (expected 404)" -f $techNovaId, $codeB)

if ($codeA -eq 404 -and $codeB -eq 404) {
    Write-Host "`nRESULT: PASS - cross-tenant reads are invisible at the application layer."
    exit 0
} else {
    Write-Host "`nRESULT: FAIL - a cross-tenant read was not hidden."
    exit 1
}
