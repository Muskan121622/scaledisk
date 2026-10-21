param([string]$Api='http://localhost:5102',[string]$Jaeger='http://localhost:16686',[string]$Email='rahul@technova.com',[string]$Org='technova')
$ErrorActionPreference='Stop'
$login = Invoke-RestMethod -Method Post -Uri "$Api/api/auth/login" -ContentType 'application/json' -Body (@{email=$Email;password='Passw0rd!';organizationSlug=$Org} | ConvertTo-Json)
$H = @{ Authorization = "Bearer $($login.accessToken)" }
$cs = Invoke-RestMethod -Uri "$Api/api/customers?pageSize=1" -Headers $H
$body = @{ customerId=$cs.items[0].id; subject='Trace me E2E'; description='OTel async-hop verification.'; priority=2; category=1; teamId=$null; assigneeUserId=$null } | ConvertTo-Json
$r = Invoke-WebRequest -UseBasicParsing -Method Post -Uri "$Api/api/tickets" -Headers $H -ContentType 'application/json' -Body $body
$traceId = $r.Headers['X-Correlation-Id']
"created ticket; trace/correlation id = $traceId"
$tr=$null
for($k=0;$k -lt 40;$k++){ Start-Sleep -Seconds 3; try { $d=@(Invoke-RestMethod -Uri "$Jaeger/api/traces/$traceId" -TimeoutSec 5).data; if($d){ $o=@($d.spans).operationName; if($o -contains 'outbox publish' -and $o -contains 'kafka consume'){$tr=$d;break} } } catch {} }
if(-not $tr){ "RESULT: FAIL - trace $traceId not found/linked in Jaeger"; exit 1 }
$ops=$tr.spans.operationName
"trace spans=$($tr.spans.Count)"
foreach($s in ($tr.spans|Sort-Object startTime)){ "   "+$s.operationName }
if(($ops -contains 'outbox publish') -and ($ops -contains 'kafka consume')){ "RESULT: PASS - one trace spans HTTP -> Postgres -> Outbox -> Kafka -> consumer." } else { "RESULT: FAIL - async hop not linked"; exit 1 }
