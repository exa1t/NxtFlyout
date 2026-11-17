<#
.SYNOPSIS
    Publishes NxtFlyout and builds its installer.

.DESCRIPTION
    1. Runs the unit tests.
    2. Publishes a self-contained x64 build to artifacts\publish (no .NET install needed on the target PC).
    3. Compiles installer\NxtFlyout.iss with Inno Setup into artifacts\installer\NxtFlyout-Setup-<version>.exe.

    Requires the .NET 8 SDK (or newer) and Inno Setup 6.3 or later:
        winget install JRSoftware.InnoSetup

.PARAMETER FrameworkDependent
    Publish a small build that needs the .NET 8 Desktop Runtime on the target PC,
    instead of bundling the runtime.

.PARAMETER SkipTests
    Do not run the unit tests first.

.EXAMPLE
    .\installer\build-installer.ps1
#>
[CmdletBinding()]
param(
    [switch]$FrameworkDependent,
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\NxtFlyout\NxtFlyout.csproj'
$publishDir = Join-Path $root 'artifacts\publish'
$script = Join-Path $PSScriptRoot 'NxtFlyout.iss'

# The version lives in one place: Directory.Build.props.
[xml]$props = Get-Content (Join-Path $root 'Directory.Build.props')
$version = ($props.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ } | Select-Object -First 1)
if (-not $version) { throw 'Could not read <Version> from Directory.Build.props.' }
Write-Host "Building NxtFlyout $version" -ForegroundColor Cyan

if (-not $SkipTests) {
    dotnet test (Join-Path $root 'NxtFlyout.sln') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Unit tests failed.' }
}

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

$selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }
dotnet publish $project -c Release -r win-x64 --self-contained $selfContained -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

$candidates = @(
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
)
$iscc = $candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) {
    throw "Inno Setup 6 was not found. Install it with 'winget install JRSoftware.InnoSetup' and run this script again."
}

& $iscc "/DAppVersion=$version" $script
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup failed.' }

Write-Host "Installer: $(Join-Path $root "artifacts\installer\NxtFlyout-Setup-$version.exe")" -ForegroundColor Green
