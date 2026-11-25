param([switch]$Diagnostics, [string]$OutputDirectory, [string]$KeePassPath)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'src\KeePassHQ\build.ps1') @PSBoundParameters
