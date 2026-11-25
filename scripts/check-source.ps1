$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$files = if ((Test-Path -LiteralPath (Join-Path $root '.git')) -and (Get-Command git -ErrorAction SilentlyContinue)) {
    @(& git -C $root ls-files --cached --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect repository inventory' }
} else {
    # An extracted source archive has no Git metadata. Inspect its source surface.
    @(Get-ChildItem -LiteralPath $root -Force -File | ForEach-Object Name)
    foreach ($directory in @('.github', 'assets', 'docs', 'scripts', 'src', 'third_party')) {
        $path = Join-Path $root $directory
        if (Test-Path -LiteralPath $path) {
            Get-ChildItem -LiteralPath $path -Recurse -Force -File | Where-Object {
                $_.FullName -notmatch '[\\/](bin|bin-diagnostics|test-output|release-output|render-[^\\/]+|synthetic-[^\\/]+)[\\/]'
            } | ForEach-Object { $_.FullName.Substring($root.Length + 1) }
        }
    }
}
$sensitive = '(?:gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|AKIA[0-9A-Z]{16}|-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----|C:\\Users\\|C:\\W3ML\\|/[U]sers/|/[h]ome/|/mnt[/]data)'
foreach ($relative in $files) {
    if ($relative -match '(?i)(?:\.(?:kdbx|kdb|key|keyx|pem|pfx|p12|settings|pdb|bak|tmp)$|\.vdo-draft|ProtectedUserKey\.bin|(?:^|[\\/])\.env(?:\.|$)|(?:^|[\\/])(?:bin|bin-diagnostics|test-output|release-output)[\\/])') {
        throw "Runtime or sensitive artifact in source inventory: $relative"
    }
    $path = Join-Path $root $relative
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing source file: $relative" }
    if ($relative -notmatch '\.(md|txt|cs|xaml|manifest|ps1|yml|svg)$') { continue }
    $content = Get-Content -LiteralPath $path -Raw -Encoding UTF8
    if ($content -match $sensitive) { throw "Secret or personal-path pattern in source: $relative" }
    if ($relative -like '*.md') {
        $links = @([regex]::Matches($content, '\]\(([^)#]+)(?:#[^)]*)?\)') | ForEach-Object { $_.Groups[1].Value })
        $links += @([regex]::Matches($content, '<img[^>]+src="([^"]+)"') | ForEach-Object { $_.Groups[1].Value })
        foreach ($link in $links) {
            if ($link -match '^(https?://|mailto:|#)') { continue }
            if (!(Test-Path -LiteralPath (Join-Path (Split-Path $path -Parent) $link))) { throw "Broken documentation link in ${relative}: $link" }
        }
    }
}
$mirror = Join-Path $root 'third_party/KeePass/KeePass-2.62-source-mirror.zip'
if ((Get-FileHash -LiteralPath $mirror -Algorithm SHA256).Hash -ne 'E0E692D04B86FF2F509507A540692DD2B33730B208CE89A7A2FA18EEC38D87D8') {
    throw 'Corresponding KeePass source mirror hash mismatch; review dependency provenance'
}
foreach ($asset in @('Brand.png', 'KeePassHQ.ico')) {
    if ((Get-FileHash (Join-Path $root "assets/$asset")).Hash -ne (Get-FileHash (Join-Path $root "src/KeePassHQ/$asset")).Hash) { throw "Runtime branding differs from its public asset: $asset" }
}
Write-Output 'PASS source inventory, secret/path patterns, documentation links, upstream source hash and runtime branding'
