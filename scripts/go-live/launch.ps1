param(
    [string]$Version = "current"
)

Write-Host "Starting launch procedure for version: $Version"
& powershell -ExecutionPolicy Bypass -File "scripts/go-live/check-criteria.ps1"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Step 2: Trigger blue-green deployment"
Write-Host "./scripts/blue-green/deploy-green.sh $Version"
Write-Host "./scripts/blue-green/switch-to-green.sh"

Write-Host "Step 3: Execute smoke tests"
Write-Host "./scripts/smoke-tests.sh"
Write-Host "Launch procedure completed"
