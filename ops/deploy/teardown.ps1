# Full teardown: destroy every billable resource so the student credit / bill drops to Rs0.
# Runs `terraform destroy` in infra/azure (always) and infra/aws (only if it has state).
param([switch]$Yes)

function Destroy-Terraform([string]$dir) {
    if (-not (Test-Path (Join-Path $dir ".terraform"))) {
        Write-Host "  (skipping $dir - nothing initialized)" -ForegroundColor DarkGray
        return
    }
    Write-Host "Destroying $dir ..." -ForegroundColor Yellow
    Push-Location $dir
    if ($Yes) { terraform destroy -auto-approve } else { terraform destroy }
    Pop-Location
}

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)  # repo root
Destroy-Terraform (Join-Path $root "infra\azure")
Destroy-Terraform (Join-Path $root "infra\aws")
Write-Host "Teardown complete. Billable footprint should now be ~Rs0." -ForegroundColor Green
