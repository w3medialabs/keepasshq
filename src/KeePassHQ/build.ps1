param([switch]$Diagnostics, [string]$OutputDirectory, [string]$KeePassPath)
$ErrorActionPreference = 'Stop'
$sourceRoot = $PSScriptRoot
$outputRoot = if ($OutputDirectory) { $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory) } else { Join-Path $sourceRoot $(if ($Diagnostics) { 'bin-diagnostics' } else { 'bin' }) }
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$framework = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
if (!$KeePassPath) {
    $candidates = @((Join-Path $sourceRoot 'Dependency\KeePass.exe'),(Join-Path $sourceRoot 'dependency-2.62\KeePass.exe'),'C:\Program Files\KeePass Password Safe 2\KeePass.exe')
    $KeePassPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (!$KeePassPath -or !(Test-Path -LiteralPath $KeePassPath)) { throw 'The verified KeePass 2.62 dependency is required; see docs/BUILD.md.' }
$KeePassPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($KeePassPath)
if ([Reflection.AssemblyName]::GetAssemblyName($KeePassPath).Version -ne [Version]'2.62.0.0') { throw 'KeePassHQ 1.2 is tested with KeePass 2.62. Update the dependency pin and rerun the audit before changing versions.' }
if ((Get-FileHash -LiteralPath $KeePassPath -Algorithm SHA256).Hash -ne 'AFB89B74AE1B8519F483D600095F4DF8D86628178AF6ACDC5F2B23888C89C3F3') { throw 'KeePass dependency hash mismatch. Do not build using an unverified binary.' }
$references = @('System.dll','System.Core.dll','System.Xml.dll','System.Drawing.dll','System.Xaml.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll',"$framework\WPF\WindowsBase.dll","$framework\WPF\PresentationCore.dll","$framework\WPF\PresentationFramework.dll",$keePassPath)
$arguments = @('/nologo','/target:winexe','/platform:x64','/optimize+','/checked+','/warn:4',('/out:' + (Join-Path $outputRoot 'KeePassHQ.exe')),('/resource:' + $keePassPath + ',KeePass.exe'),('/resource:' + (Join-Path $sourceRoot 'Theme.xaml') + ',Theme.xaml'),('/resource:' + (Join-Path $sourceRoot 'Brand.png') + ',Brand.png'))
if ($Diagnostics) { $arguments += '/define:DIAGNOSTICS' }
$iconRoot = Join-Path $sourceRoot '../../assets/bootstrap-icons'
foreach ($icon in Get-ChildItem -LiteralPath $iconRoot -Filter '*.svg') { $arguments += '/resource:' + $icon.FullName + ',Bootstrap.' + $icon.Name }
if (Test-Path -LiteralPath (Join-Path $sourceRoot 'KeePassHQ.ico')) { $arguments += '/win32icon:' + (Join-Path $sourceRoot 'KeePassHQ.ico') }
$arguments += '/win32manifest:' + (Join-Path $sourceRoot 'app.manifest')
foreach ($reference in $references) { $arguments += '/reference:' + $reference }
$arguments += @(Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' | ForEach-Object FullName)
& "$framework\csc.exe" @arguments
if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }
Write-Output (Join-Path $outputRoot 'KeePassHQ.exe')
