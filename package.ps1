param([string]$OutputDirectory, [switch]$Demo)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $revision = @(& git rev-parse --verify HEAD)
    if ($LASTEXITCODE -ne 0 -or $revision.Count -ne 1) { throw 'Packaging requires the committed Git checkout' }
    $commit = $revision[0].Trim()
    $state = @(& git status --porcelain=v1)
    if ($LASTEXITCODE -ne 0 -or $state.Count) { throw 'Commit or preserve working changes before packaging so the source archive matches the executable' }
    $destination = if ($OutputDirectory) { $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory) } else { Join-Path $PSScriptRoot ('release-output/preview-' + $commit.Substring(0,7)) }
    if (Test-Path -LiteralPath $destination) { throw 'Package destination exists; choose a new directory to preserve earlier builds' }
    New-Item -ItemType Directory -Path $destination | Out-Null
    $source = Join-Path $destination 'KeePassHQ-source.zip'
    & git archive --format=zip --output=$source $commit
    if ($LASTEXITCODE -ne 0) { throw 'Corresponding source packaging failed' }
    # Build the immutable commit archive, so edits elsewhere in the working tree
    # cannot change the executable after its source revision has been selected.
    $buildRoot = Join-Path $PSScriptRoot ('test-output/package-' + [guid]::NewGuid().ToString('N'))
    Expand-Archive -LiteralPath $source -DestinationPath $buildRoot
    & powershell -NoProfile -File (Join-Path $buildRoot 'validate.ps1') -Render
    if ($LASTEXITCODE -ne 0) { throw 'Extracted source validation failed' }
    $app = Join-Path $destination 'KeePassHQ'
    New-Item -ItemType Directory -Path $app | Out-Null
    Copy-Item -LiteralPath (Join-Path $buildRoot 'bin/KeePassHQ.exe') -Destination $app
    foreach ($name in @('README.md','FEATURES.md','SECURITY.md','CHANGELOG.md','CONTRIBUTING.md','THIRD-PARTY.md','LICENSE','docs','assets','third_party')) {
        Copy-Item -LiteralPath (Join-Path $buildRoot $name) -Destination $app -Recurse
    }
    if ($Demo) {
        & powershell -NoProfile -File (Join-Path $buildRoot 'scripts/make-demo.ps1') -RepositoryRoot $buildRoot -Destination $app
        if ($LASTEXITCODE -ne 0) { throw 'Fictional demo generation or readback failed' }
    }
    $exe = Join-Path $app 'KeePassHQ.exe'
    $hash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
    Copy-Item -LiteralPath (Join-Path $buildRoot 'test-output/test-results.txt') -Destination (Join-Path $app 'TEST-RESULTS.txt')
    Copy-Item -LiteralPath (Join-Path $buildRoot 'test-output/validation.txt') -Destination (Join-Path $app 'BUILD-VALIDATION.txt')
    $info = @('KeePassHQ 1.2 - local portable preview',('Source commit: ' + $commit),'Source repository: https://github.com/w3medialabs/keepasshq','Unsigned; Windows x64; .NET Framework 4.8 or later',('Executable SHA-256: ' + $hash),'The matching KeePassHQ-source.zip is alongside the portable ZIP.')
    if ($Demo) { $info += 'Demo.kdbx is fictional. Password: demo. Never store real credentials in it.' }
    $info | Set-Content -LiteralPath (Join-Path $app 'BUILD-INFO.txt') -Encoding UTF8
    $portable = Join-Path $destination 'KeePassHQ-1.2-preview-portable.zip'
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::Open($portable,[IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in Get-ChildItem -LiteralPath $app -Recurse -Force -File) {
            $relative = 'KeePassHQ/' + $file.FullName.Substring($app.Length + 1).Replace('\','/')
            [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$relative,[IO.Compression.CompressionLevel]::Optimal)
        }
    } finally { $archive.Dispose() }
    @('KeePassHQ/KeePassHQ.exe','KeePassHQ-1.2-preview-portable.zip','KeePassHQ-source.zip') | ForEach-Object {
        (Get-FileHash -LiteralPath (Join-Path $destination $_) -Algorithm SHA256).Hash + '  ' + $_
    } | Set-Content -LiteralPath (Join-Path $destination 'SHA256SUMS.txt') -Encoding ASCII
    $zip = [IO.Compression.ZipFile]::OpenRead($portable)
    try {
        $names = @($zip.Entries | ForEach-Object FullName)
        foreach ($required in @('KeePassHQ/KeePassHQ.exe','KeePassHQ/LICENSE','KeePassHQ/THIRD-PARTY.md','KeePassHQ/TEST-RESULTS.txt','KeePassHQ/BUILD-VALIDATION.txt','KeePassHQ/docs/HELP.md','KeePassHQ/docs/REPORTING.md','KeePassHQ/third_party/Bootstrap-Icons-LICENSE.txt','KeePassHQ/third_party/KeePass/KeePass-2.62-source-mirror.zip')) {
            if ($required -notin $names) { throw "Portable package entry missing: $required" }
        }
        if ($Demo -and 'KeePassHQ/Demo.kdbx' -notin $names) { throw 'Fictional demo missing from portable ZIP' }
        if ($names | Where-Object { $_ -match 'bin-diagnostics|test-output|\.(key|keyx|settings|bak|tmp|pdb|pem|pfx|p12)$|\.vdo-draft|ProtectedUserKey\.bin' }) { throw 'Unintended runtime artifact in portable ZIP' }
        if ($names | Where-Object { $_ -match '\.kdbx$' -and (!$Demo -or $_ -ne 'KeePassHQ/Demo.kdbx') }) { throw 'Unintended database in portable ZIP' }
        $payloads=@('KeePassHQ/KeePassHQ.exe');if($Demo){$payloads+='KeePassHQ/Demo.kdbx'}
        foreach ($relative in $payloads) {
            $stream = $zip.GetEntry($relative).Open(); $digest = [Security.Cryptography.SHA256]::Create()
            try { $actual = [BitConverter]::ToString($digest.ComputeHash($stream)).Replace('-','') }
            finally { $stream.Dispose(); $digest.Dispose() }
            if ($actual -ne (Get-FileHash -LiteralPath (Join-Path $destination $relative) -Algorithm SHA256).Hash) { throw "Portable payload hash mismatch: $relative" }
        }
    } finally { $zip.Dispose() }
    Write-Output ('PASS portable package inventory, corresponding source and checksums: ' + $destination)
    Write-Output $exe
}
finally { Pop-Location }
