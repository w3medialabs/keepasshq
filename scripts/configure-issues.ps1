param([string]$Repository = 'w3medialabs/keepasshq')
$ErrorActionPreference = 'Stop'
if (!(Get-Command gh -ErrorAction SilentlyContinue)) { throw 'Install and authenticate GitHub CLI first' }
if ($Repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') { throw 'Expected owner/repository' }
$definitions = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../.github/labels.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$seen = @{}
foreach ($label in $definitions) {
    if (!$label.name -or $label.name.Length -gt 50 -or $seen.ContainsKey($label.name) -or $label.color -notmatch '^[0-9a-fA-F]{6}$' -or $label.description.Length -gt 100) { throw 'Invalid or duplicate label definition' }
    $seen[$label.name] = $true
}
$inventory = @(& gh label list --repo $Repository --limit 1000 --json name,color,description)
if ($LASTEXITCODE -ne 0) { throw 'Could not inspect existing labels' }
$existing = $inventory | Out-String | ConvertFrom-Json
& gh api --method PATCH "repos/$Repository" -F has_issues=true --silent
if ($LASTEXITCODE -ne 0) { throw 'Could not enable GitHub Issues' }
foreach ($label in $definitions) {
    # Add only. Never delete, rename, recolor or overwrite existing labels.
    if ($existing.name -contains $label.name) { continue }
    & gh label create $label.name --repo $Repository --color $label.color --description $label.description
    if ($LASTEXITCODE -ne 0) { throw ('Could not create label: ' + $label.name) }
    Write-Output ('Created label: ' + $label.name)
}
Write-Output 'PASS Issues enabled and missing labels added; existing labels preserved. No issue was created.'
