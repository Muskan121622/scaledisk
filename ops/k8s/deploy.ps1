#requires -Version 5.1
# ScaleDesk k8s deploy (Stage 7E) — builds both images, loads them into the minikube
# docker driver, applies k8s/base via kustomize, waits for rollouts, wires ingress DNS.
#
#   powershell -File ops\k8s\deploy.ps1
#
# Idempotent: safe to re-run after any code change (images rebuild + rollout restart).
# Requires: docker, minikube, kubectl. First run installs/starts the cluster (slow).
$ErrorActionPreference = 'Stop'
$Repo = (Resolve-Path "$PSScriptRoot\..\..").Path
Set-Location $Repo
# minikube may live in the user-local bin (binary install, no admin) rather than system PATH.
if (Test-Path "$env:USERPROFILE\.local\bin\minikube.exe") { $env:Path = "$env:USERPROFILE\.local\bin;$env:Path" }

function Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }

# --- 1. minikube present + running (docker driver) ---
Step 'minikube status'
$evalOut = (minikube status -p scaledesk 2>&1) -join "`n"
if ($evalOut -match 'flag: minikube profile .* do(es)? not exist') {
  Step 'first run: starting minikube cluster "scaledesk" (docker driver, 6 CPUs / 6GiB)'
  # --force: this is our dedicated project cluster; claim the driver even if others exist.
  # 6144MB fits under Docker Desktop's VM allocation (it rejects requests above its total).
  minikube start -p scaledesk --driver=docker --cpus=6 --memory=6144 --force --wait=300s
  if ($LASTEXITCODE -ne 0) { throw 'minikube start failed' }
} elseif ($evalOut -notmatch 'Host.+Running|control-plane.+Running') {
  Step 'minikube stopped -> starting'
  minikube start -p scaledesk --wait=90s
  if ($LASTEXITCODE -ne 0) { throw 'minikube start failed' }
}

# --- 2. docker env points at the minikube daemon so images land in-cluster directly ---
Step 'docker-env -> minikube'
$envExpr = (minikube docker-env -p scaledesk --shell=powershell) -join '; '
Invoke-Expression ($envExpr + '; docker version --format "{{.Server.Version}}"')
if ($LASTEXITCODE -ne 0) { throw 'cannot reach the minikube docker daemon' }

# --- 3. ingress addon (nginx class the Ingress manifest expects) ---
Step 'ingress addon'
minikube addons enable ingress -p scaledesk | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'failed to enable ingress addon' }

# --- 4. build images inside the minikube daemon (cached layers across runs) ---
Step 'build api image (dotnet publish in-container, few minutes on cold cache)'
docker build -t scaledesk-api:local -f src/ScaleDesk.Api/Dockerfile $Repo
if ($LASTEXITCODE -ne 0) { throw 'api image build failed' }

Step 'build web image (npm ci + vite build in-container, nginx runtime)'
docker build -t scaledesk-web:local -f web/Dockerfile (Join-Path $Repo 'web')
if ($LASTEXITCODE -ne 0) { throw 'web image build failed' }

# --- 5. apply + rollouts ---
Step 'kubectl apply -k k8s/base'
kubectl apply -k (Join-Path $Repo 'k8s\base')
if ($LASTEXITCODE -ne 0) { throw 'kustomize apply failed' }

Step 'waiting for rollouts (first boot: migrations + seed, up to ~5 min)'
kubectl -n scaledesk rollout status statefulset/postgres --timeout=300s
kubectl -n scaledesk rollout status deployment/redis  --timeout=120s
kubectl -n scaledesk rollout status statefulset/kafka  --timeout=300s
kubectl -n scaledesk rollout status deployment/jaeger  --timeout=120s
kubectl -n scaledesk rollout status deployment/api     --timeout=600s
kubectl -n scaledesk rollout status deployment/web     --timeout=120s
if ($LASTEXITCODE -ne 0) { throw 'a rollout did not stabilize; inspect: kubectl -n scaledesk describe pod' }

# --- 6. host alias so scaledesk.local resolves (needs an elevated shell to write) ---
Step 'hosts entry for scaledesk.local'
$ip = (minikube ip -p scaledesk).Trim()
$hostsPath = "$env:SystemRoot\System32\drivers\etc\hosts"
$hosts = Get-Content $hostsPath -ErrorAction SilentlyContinue
if (-not ($hosts | Where-Object { $_ -match 'scaledesk\.local' })) {
  try {
    Add-Content $hostsPath "`n$ip scaledesk.local"
    Write-Host "    added: $ip scaledesk.local"
  } catch {
    Write-Warning 'hosts file is protected - run once in an elevated shell:  Add-Content ...hosts  OR browse via http://<minikube ip> with Host header'
  }
} else {
  Write-Host '    already present'
}

# --- 7. smoke through the ingress ---
Step 'smoke: ingress -> web -> api'
try {
  $r = Invoke-WebRequest -Uri 'http://scaledesk.local/health/ready' -UseBasicParsing -TimeoutSec 30
  Write-Host "    /health/ready -> $($r.StatusCode) $($r.Content)" -ForegroundColor Green
} catch {
  Write-Warning "scaledesk.local not reachable yet ($($_.Exception.Message)); ingress controller may still be starting - retry in ~30s"
}
try {
  $w = Invoke-WebRequest -Uri 'http://scaledesk.local/' -UseBasicParsing -TimeoutSec 15
  if ($w.Content -match '<div id="root">') { Write-Host '    SPA served at / -> ok' -ForegroundColor Green }
} catch { Write-Warning "SPA fetch failed: $($_.Exception.Message)" }

Step 'pods'
kubectl -n scaledesk get pods -o wide
Write-Host ''
Write-Host 'URL:      http://scaledesk.local   (staff /app, customer /portal, HQ /platform)' -ForegroundColor Green
Write-Host 'Login:    demo@scaledesk.app / DemoPass2026!  |  platform@scaledesk.app / DemoPass2026!'
Write-Host 'Jaeger:   kubectl -n scaledesk port-forward svc/jaeger 16686:16686'
Write-Host 'Teardown: powershell -File ops\k8s\teardown.ps1'
