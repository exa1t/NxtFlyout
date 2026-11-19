<#
.SYNOPSIS
    Renders the README banner and feature cards from cards.html into docs\promo\*.png.

.DESCRIPTION
    Opens cards.html once per card in headless Microsoft Edge at 2x scale and saves a
    screenshot. Run it after editing cards.html or replacing a screenshot:
        powershell -ExecutionPolicy Bypass -File .\docs\promo\render.ps1
#>
$ErrorActionPreference = 'Stop'

$edge = @(
    (Join-Path ${env:ProgramFiles(x86)} 'Microsoft\Edge\Application\msedge.exe'),
    (Join-Path $env:ProgramFiles 'Microsoft\Edge\Application\msedge.exe')
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $edge) { throw 'Microsoft Edge was not found.' }

$page = (Resolve-Path (Join-Path $PSScriptRoot 'cards.html')).Path
$url = 'file:///' + ($page -replace '\\', '/')

# Card id, width, height (in CSS pixels; the PNG is twice that).
$cards = @(
    @('banner', 1000, 260),
    @('media', 800, 420),
    @('upnext', 800, 420),
    @('volume', 800, 420),
    @('lockkeys', 800, 420),
    @('widget', 800, 420),
    @('stats', 800, 420),
    @('settings', 1000, 760)
)

function Render([string]$pageUrl, [string]$name, [int]$width, [int]$height) {
    $out = Join-Path $PSScriptRoot "$name.png"
    if (Test-Path $out) { Remove-Item $out }

    # Edge reports "N bytes written" on stderr, which Windows PowerShell would treat as an error.
    $ErrorActionPreference = 'Continue'
    & $edge --headless=new --disable-gpu --hide-scrollbars --allow-file-access-from-files `
        --force-device-scale-factor=2 "--window-size=$width,$height" "--screenshot=$out" `
        $pageUrl 2>&1 | Out-Null
    $ErrorActionPreference = 'Stop'

    if (-not (Test-Path $out)) { throw "Rendering $name failed." }
    Write-Host "Rendered $name.png"
}

foreach ($card in $cards) {
    $id, $width, $height = $card
    Render "$url`?card=$id" $id $width $height
}

# The hero collage at the top of the README has a page of its own.
$hero = 'file:///' + ((Resolve-Path (Join-Path $PSScriptRoot 'hero.html')).Path -replace '\\', '/')
Render $hero 'hero' 1200 520
