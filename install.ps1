<#
    Creates a desktop shortcut for ToDoist (icon is taken from the exe).
    Keep this file pure ASCII.

    Usage:  powershell -ExecutionPolicy Bypass -File .\install.ps1
#>
[CmdletBinding()]
param(
    [switch]$StartMenu
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$exe  = Join-Path $root 'bin\ToDoist.exe'

if (-not (Test-Path -LiteralPath $exe)) {
    & (Join-Path $root 'build.ps1')
}

$folders = @([Environment]::GetFolderPath('Desktop'))
if ($StartMenu) {
    $folders += (Join-Path ([Environment]::GetFolderPath('Programs')) 'ToDoist')
}

$shell = New-Object -ComObject WScript.Shell
foreach ($folder in $folders) {
    if (-not (Test-Path -LiteralPath $folder)) {
        New-Item -ItemType Directory -Force -Path $folder | Out-Null
    }

    $link = Join-Path $folder 'ToDoist.lnk'
    $shortcut = $shell.CreateShortcut($link)
    $shortcut.TargetPath = $exe
    $shortcut.WorkingDirectory = Split-Path -Parent $exe
    $shortcut.IconLocation = $exe + ',0'
    $shortcut.Description = 'ToDoist - a simple glass task list for today'
    $shortcut.Save()

    Write-Host "Shortcut created: $link"
}
