<#
    Run ToDoist. Builds the exe first if it is missing.
    Keep this file pure ASCII: Windows PowerShell 5.1 reads .ps1 without a BOM as ANSI.

    Usage:  powershell -ExecutionPolicy Bypass -File .\run.ps1
            powershell -ExecutionPolicy Bypass -File .\run.ps1 -Rebuild
#>
[CmdletBinding()]
param(
    [switch]$Rebuild
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$exe  = Join-Path $root 'bin\ToDoist.exe'

if ($Rebuild -or -not (Test-Path -LiteralPath $exe)) {
    & (Join-Path $root 'build.ps1')
}

Write-Host "Starting $exe"
Start-Process -FilePath $exe
