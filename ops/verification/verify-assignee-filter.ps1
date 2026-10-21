$ErrorActionPreference = 'Stop'
function Get-Json($url, $token) {
    $p = @{ Uri = $url; UseBasicParsing = $true; TimeoutSec = 30 }
    if ($token) { $p.Headers = @{ Authorization = "Bearer $token" } }
    (Invoke-WebRequest @p).Content | ConvertFrom-Json
}
$login = (Invoke-WebRequest -Uri 'http://localhost:8080/api/auth/demo' -Method Post -ContentType 'application/json' -Body '{}' -UseBasicParsing -TimeoutSec 30).Content | ConvertFrom-Json
$tok = $login.accessToken
$all = Get-Json 'http://localhost:8080/api/tickets?page=1&pageSize=100' $tok
$one = $all.items | Where-Object { $_.assigneeUserId } | Select-Object -First 1
$f = Get-Json "http://localhost:8080/api/tickets?page=1&pageSize=100&assignee=$($one.assigneeUserId)" $tok
$foreign = @($f.items | Where-Object { $_.assigneeUserId -ne $one.assigneeUserId }).Count
Write-Host ("total={0}  filtered={1}  foreignRowsInResult={2}" -f $all.totalCount, $f.totalCount, $foreign)
if ($f.totalCount -lt $all.totalCount -and $foreign -eq 0) { Write-Host 'ASSIGNEE FILTER: PASS' } else { Write-Host 'ASSIGNEE FILTER: FAIL'; exit 1 }
