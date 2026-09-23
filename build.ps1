<#
    ToDoist - build script (no MSBuild required).
    Uses the built-in C# compiler from .NET Framework 4.x plus WPF assemblies from the GAC.

    IMPORTANT: this file must stay pure ASCII. Windows PowerShell 5.1 reads .ps1 files
    without a BOM as ANSI (CP1251 on Russian Windows), which corrupts UTF-8 text.
    The application sources themselves are UTF-8 and compiled with /codepage:65001.

    Usage:  powershell -ExecutionPolicy Bypass -File .\build.ps1
            powershell -ExecutionPolicy Bypass -File .\build.ps1 -SkipTests
#>
[CmdletBinding()]
param(
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$root    = $PSScriptRoot
$srcDir  = Join-Path $root 'src'
$testDir = Join-Path $root 'tests'
$binDir  = Join-Path $root 'bin'

function Find-Csc {
    $candidates = @(
        (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
        (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
    )
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) { return $candidate }
    }
    throw 'csc.exe (.NET Framework 4.x) not found. Please install .NET Framework 4.8.'
}

function Find-Reference([string]$name) {
    $frameworkDir = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
    $local = Join-Path $frameworkDir ($name + '.dll')
    if (Test-Path -LiteralPath $local) { return $local }

    $gacRoots = @('GAC_MSIL', 'GAC_64', 'GAC_32') |
        ForEach-Object { Join-Path $env:WINDIR ('Microsoft.NET\assembly\' + $_) }
    foreach ($gacRoot in $gacRoots) {
        $assemblyDir = Join-Path $gacRoot $name
        if (Test-Path -LiteralPath $assemblyDir) {
            $dll = Get-ChildItem -LiteralPath $assemblyDir -Recurse -Filter ($name + '.dll') -File |
                Select-Object -First 1
            if ($dll) { return $dll.FullName }
        }
    }
    throw "Assembly '$name' was not found in the .NET Framework folder or in the GAC."
}

function Invoke-Csc([string[]]$Arguments) {
    $compiler = Find-Csc
    & $compiler @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Compilation failed with exit code $LASTEXITCODE."
    }
}

$commonArgs = @(
    '/nologo',
    '/platform:anycpu',
    '/optimize+',
    '/warn:4',
    '/codepage:65001'
)

# WPF assemblies: WindowsBase / PresentationCore / PresentationFramework / System.Xaml
$wpfReferences = @(
    'System', 'System.Core', 'System.Runtime.Serialization',
    'WindowsBase', 'PresentationCore', 'PresentationFramework', 'System.Xaml'
)

New-Item -ItemType Directory -Force -Path $binDir | Out-Null

# ---------- application icon ----------
$iconPath = Join-Path $srcDir 'ToDoist.ico'
$iconTool = Join-Path $root 'tools\IconMaker.cs'

if (-not (Test-Path -LiteralPath $iconPath) -and (Test-Path -LiteralPath $iconTool)) {
    $iconExe = Join-Path $binDir 'IconMaker.exe'
    Write-Host 'Building IconMaker.exe ...'
    $iconArgs = $commonArgs + @('/target:exe', ('/out:' + $iconExe),
        ('/r:' + (Find-Reference 'System.Drawing')), $iconTool)
    Invoke-Csc $iconArgs

    Write-Host 'Drawing application icon ...'
    & $iconExe $iconPath
    if ($LASTEXITCODE -ne 0) {
        throw "IconMaker failed with exit code $LASTEXITCODE."
    }
}

# ---------- application ----------
$sources = @(Get-ChildItem -LiteralPath $srcDir -Filter '*.cs' -File | Sort-Object Name |
    ForEach-Object { $_.FullName })
if ($sources.Count -eq 0) { throw "No C# sources in '$srcDir'." }

$refArgs = @($wpfReferences | ForEach-Object { '/r:' + (Find-Reference $_) })
$appOut  = Join-Path $binDir 'ToDoist.exe'

$appArgs = $commonArgs + @(
    '/target:winexe',
    ('/out:' + $appOut),
    ('/resource:' + (Join-Path $srcDir 'MainWindow.xaml') + ',MainWindow.xaml')
)

$manifest = Join-Path $srcDir 'app.manifest'
if (Test-Path -LiteralPath $manifest) {
    $appArgs += ('/win32manifest:' + $manifest)
}

if (Test-Path -LiteralPath $iconPath) {
    $appArgs += ('/win32icon:' + $iconPath)
}

$appArgs += $refArgs + $sources

Write-Host 'Building ToDoist.exe ...'
Invoke-Csc $appArgs
Write-Host ('Build OK: {0} ({1:N0} bytes)' -f $appOut, (Get-Item -LiteralPath $appOut).Length)

# ---------- tests ----------
if (-not $SkipTests) {
    $testSources = @(
        (Join-Path $testDir 'StorageTests.cs'),
        (Join-Path $testDir 'BackdropTests.cs'),
        (Join-Path $testDir 'TextShadowTests.cs'),
        (Join-Path $srcDir 'TaskItem.cs'),
        (Join-Path $srcDir 'AppData.cs'),
        (Join-Path $srcDir 'Log.cs'),
        (Join-Path $srcDir 'Storage.cs'),
        (Join-Path $srcDir 'BackdropSupport.cs'),
        (Join-Path $srcDir 'GlassSupport.cs'),
        (Join-Path $srcDir 'TextShadowSupport.cs')
    )
    foreach ($file in $testSources) {
        if (-not (Test-Path -LiteralPath $file)) { throw "Missing test source: $file" }
    }

    $testOut  = Join-Path $binDir 'StorageTests.exe'
    $testRefs = @('System', 'System.Core', 'System.Runtime.Serialization', 'System.Xml') |
        ForEach-Object { '/r:' + (Find-Reference $_) }
    $testArgs = $commonArgs + @('/target:exe', ('/out:' + $testOut)) + $testRefs + $testSources

    Write-Host 'Building StorageTests.exe ...'
    Invoke-Csc $testArgs
    Write-Host ('Tests build OK: {0}' -f $testOut)
}

Write-Host 'Done.'
