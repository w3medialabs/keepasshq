param([switch]$Render)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    & .\scripts\check-source.ps1
    & .\build.ps1 -OutputDirectory .\bin
    & .\build.ps1 -Diagnostics -OutputDirectory .\bin-diagnostics
    $releasePath = (Resolve-Path .\bin\KeePassHQ.exe).Path
    $diagnosticsPath = (Resolve-Path .\bin-diagnostics\KeePassHQ.exe).Path
    $results = Join-Path $PSScriptRoot 'test-output'
    New-Item -ItemType Directory -Path $results -Force | Out-Null

    # Read assembly metadata/resources only; never invoke the release entry point.
    $releaseBytes = [IO.File]::ReadAllBytes($releasePath)
    $diagnosticBytes = [IO.File]::ReadAllBytes($diagnosticsPath)
    $releaseMetadata = [Text.Encoding]::ASCII.GetString($releaseBytes)
    $diagnosticMetadata = [Text.Encoding]::ASCII.GetString($diagnosticBytes)
    foreach ($name in @('Tests', 'IsolatedClipboard', 'BeforeRecoveryPublish', 'AfterSaveWrite',
        'BeforeSaveReplace', 'AfterSaveReplace', 'BeforeDraftPublish', 'BeforeCreatePublish', 'RenderDemo',
        'TestWorkflow', 'TestMultipleDatabases', 'AuditChecks', 'TestAuditWorkflow', 'TestPasswordReveal', 'IsolatedDraftRoot')) {
        $token = $name + [char]0
        if ($releaseMetadata.Contains($token) -or !$diagnosticMetadata.Contains($token)) {
            throw "Release/diagnostics metadata boundary failed: $name"
        }
    }
    foreach ($mode in @('--self-test', '--render-demo')) {
        $token = [Text.Encoding]::ASCII.GetString([Text.Encoding]::Unicode.GetBytes($mode))
        if ($releaseMetadata.Contains($token) -or !$diagnosticMetadata.Contains($token)) {
            throw "Release/diagnostics command boundary failed: $mode"
        }
    }
    $assembly = [Reflection.Assembly]::Load($releaseBytes)
    $resource = $assembly.GetManifestResourceStream('KeePass.exe')
    if (!$resource) { throw 'Embedded KeePass resource missing' }
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $embeddedHash = [BitConverter]::ToString($sha.ComputeHash($resource)).Replace('-', '') }
    finally { $resource.Dispose(); $sha.Dispose() }
    if ($embeddedHash -ne (Get-FileHash .\src\KeePassHQ\Dependency\KeePass.exe -Algorithm SHA256).Hash) {
        throw 'Embedded dependency differs from the pinned build input'
    }

    $process = Start-Process -FilePath $diagnosticsPath -ArgumentList @('--self-test', ('"' + $results + '"')) -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Synthetic validation failed: $($process.ExitCode); see test-output/test-results.txt" }
    Get-Content (Join-Path $results 'test-results.txt')
    if ($Render) {
        $renderRoot = Join-Path $results 'renders'
        New-Item -ItemType Directory -Path $renderRoot -Force | Out-Null
        foreach ($mode in @('viewer', 'organizer', 'generator', 'gap', 'unlock', 'create', 'dual', 'recovery', 'reminder', 'open', 'settings', 'toast', 'compact', 'tooltip')) {
            $image = Join-Path $renderRoot ($mode + '.png')
            $process = Start-Process -FilePath $diagnosticsPath -ArgumentList @('--render-demo', ('"' + $image + '"'), $mode) -WindowStyle Hidden -Wait -PassThru
            if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $image)) { throw "Synthetic render failed: $mode" }
        }
    }
    @('PASS release diagnostics exclusion and embedded dependency integrity',
        ('Release SHA-256: ' + (Get-FileHash $releasePath -Algorithm SHA256).Hash),
        ('Embedded KeePass SHA-256: ' + $embeddedHash)) | Set-Content (Join-Path $results 'validation.txt')
    Get-Content (Join-Path $results 'validation.txt')
}
finally { Pop-Location }
