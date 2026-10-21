# Bring the AKS nodes back up when you need a live cluster for a demo/test.
param(
    [string]$Name = "scaledesk-aks-demo",
    [string]$ResourceGroup = "rg-scaledesk-demo"
)
Write-Host "Starting AKS cluster '$Name'..." -ForegroundColor Cyan
az aks start --name $Name --resource-group $ResourceGroup
az aks get-credentials --resource-group $ResourceGroup --name $Name --overwrite-existing
Write-Host "Up. kubectl is re-authorized. Remember to stop-cluster.ps1 when done." -ForegroundColor Green
