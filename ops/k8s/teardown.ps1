#requires -Version 5.1
# Remove the ScaleDesk stack from the minikube cluster (cluster itself is kept;
# `minikube stop -p scaledesk` / `minikube delete -p scaledesk` for the machine).
# Also strips the scaledesk.local hosts entry if this script added it.
$ErrorActionPreference = 'Continue'
$Repo = (Resolve-Path "$PSScriptRoot\..\..").Path
Set-Location $Repo

Write-Host '==> deleting k8s/base resources' -ForegroundColor Cyan
kubectl delete -k (Join-Path $Repo 'k8s\base') --ignore-not-found

Write-Host '==> removing scaledesk.local hosts entry (if present)' -ForegroundColor Cyan
$hostsPath = "$env:SystemRoot\System32\drivers\etc\hosts"
$lines = Get-Content $hostsPath -ErrorAction SilentlyContinue
if ($lines) {
  $kept = $lines | Where-Object { $_ -notmatch 'scaledesk\.local' }
  if (@($kept).Count -ne @($lines).Count) {
    try { Set-Content $hostsPath $kept -Encoding ASCII } catch { Write-Warning 'hosts file protected - remove the scaledesk.local line manually' }
  }
}
Write-Host 'done. pods left: ' -NoNewline
kubectl -n scaledesk get pods 2>&1 | Select-Object -First 3
