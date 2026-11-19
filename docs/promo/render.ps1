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

foreach ($card in $cards) {
    $id, $width, $height = $card
    $out = Join-Path $PSScriptRoot "$id.png"
    if (Test-Path $out) { Remove-Item $out }

    # Edge reports "N bytes written" on stderr, which Windows PowerShell would treat as an error.
    $ErrorActionPreference = 'Continue'
    & $edge --headless=new --disable-gpu --hide-scrollbars --allow-file-access-from-files `
        --force-device-scale-factor=2 "--window-size=$width,$height" "--screenshot=$out" `
        "$url`?card=$id" 2>&1 | Out-Null
    $ErrorActionPreference = 'Stop'

    if (-not (Test-Path $out)) { throw "Rendering $id failed." }
    Write-Host "Rendered $id.png"
}
