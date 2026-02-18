param(
    [string]$ChecklistPath = "docs/operations/go-live-checklist.md"
)

if (-not (Test-Path $ChecklistPath)) {
    Write-Error "Checklist file not found: $ChecklistPath"
    exit 1
}

$content = Get-Content $ChecklistPath
$checkedCount = ($content | Select-String -Pattern '^- \[x\]').Count
$signoffCount = ($content | Select-String -Pattern '^- Sign-off:').Count

if ($checkedCount -lt 100) {
    Write-Host "NO-GO: checklist completion is $checkedCount/100"
    exit 1
}

if ($signoffCount -lt 10) {
    Write-Host "NO-GO: sign-off count is $signoffCount/10"
    exit 1
}

Write-Host "GO: criteria met (items=$checkedCount, sign-offs=$signoffCount)"
