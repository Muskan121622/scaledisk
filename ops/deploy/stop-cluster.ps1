# Stop (deallocate) the AKS nodes so you stop paying for compute between demos.
# The cluster definition stays; only the VM is deallocated -> ~Rs0 while stopped.
param(
    [string]$Name = "scaledesk-aks-demo",
    [string]$ResourceGroup = "rg-scaledesk-demo"
)
Write-Host "Stopping AKS cluster '$Name' (nodes deallocated; control-plane is Free tier)..." -ForegroundColor Cyan
az aks stop --name $Name --resource-group $ResourceGroup
Write-Host "Stopped. Bring it back with start-cluster.ps1 when you need it live." -ForegroundColor Green
