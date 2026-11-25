param([Parameter(Mandatory=$true)][string]$RepositoryRoot, [Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference = 'Stop'
$path = Join-Path $Destination 'Demo.kdbx'
if (Test-Path -LiteralPath $path) { throw 'A demo already exists; it will not be overwritten' }
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
$dependency = Join-Path $RepositoryRoot 'src/KeePassHQ/Dependency/KeePass.exe'
if ((Get-FileHash -LiteralPath $dependency -Algorithm SHA256).Hash -ne 'AFB89B74AE1B8519F483D600095F4DF8D86628178AF6ACDC5F2B23888C89C3F3') { throw 'Unverified KeePass dependency' }
[void][Reflection.Assembly]::LoadFrom($dependency)
[KeePassLib.Native.NativeLib]::AllowNative = $false
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $RepositoryRoot 'bin-diagnostics/KeePassHQ.exe'))
[void]$assembly.GetType('VaultOrganizer.Tests',$true).GetMethod('CreateFixture',[Reflection.BindingFlags]'Public,Static').Invoke($null,@($Destination,$false))
$hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
$key = New-Object KeePassLib.Keys.CompositeKey
$key.AddUserKey((New-Object KeePassLib.Keys.KcpPassword -ArgumentList 'demo'))
$db = New-Object KeePassLib.PwDatabase
try {
    $db.Open([KeePassLib.Serialization.IOConnectionInfo]::FromPath($path),$key,$null)
    if (@($db.RootGroup.GetEntries($true)).Count -ne 3) { throw 'Unexpected fictional demo contents' }
} finally { $db.Close() }
if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $hash) { throw 'Demo changed during readback' }
Write-Output 'PASS fictional Demo.kdbx opens with password demo, contains three entries and remains unchanged'
