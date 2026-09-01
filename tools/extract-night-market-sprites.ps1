param(
    [string]$Source = 'C:\Users\Elden\Downloads\ChatGPT Image Aug 28, 2026, 07_47_51 PM.png',
    [string]$Output = 'Assets\Resources\UI\NightMarket'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $root $Output
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$sourceBitmap = [System.Drawing.Bitmap]::FromFile($Source)

function Export-Crop {
    param([string]$Name, [int]$X, [int]$Y, [int]$Width, [int]$Height)

    if ($X -lt 0 -or $Y -lt 0 -or $X + $Width -gt $sourceBitmap.Width -or $Y + $Height -gt $sourceBitmap.Height) {
        throw "Crop '$Name' lies outside $($sourceBitmap.Width)x$($sourceBitmap.Height)."
    }

    $rect = [System.Drawing.Rectangle]::new($X, $Y, $Width, $Height)
    $crop = $sourceBitmap.Clone($rect, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $minX = $crop.Width; $minY = $crop.Height; $maxX = -1; $maxY = -1
        for ($py = 0; $py -lt $crop.Height; $py++) {
            for ($px = 0; $px -lt $crop.Width; $px++) {
                if ($crop.GetPixel($px, $py).A -gt 8) {
                    if ($px -lt $minX) { $minX = $px }
                    if ($py -lt $minY) { $minY = $py }
                    if ($px -gt $maxX) { $maxX = $px }
                    if ($py -gt $maxY) { $maxY = $py }
                }
            }
        }
        if ($maxX -lt $minX -or $maxY -lt $minY) { throw "Crop '$Name' contains no visible pixels." }

        $pad = 4
        $minX = [Math]::Max(0, $minX - $pad); $minY = [Math]::Max(0, $minY - $pad)
        $maxX = [Math]::Min($crop.Width - 1, $maxX + $pad); $maxY = [Math]::Min($crop.Height - 1, $maxY + $pad)
        $trimRect = [System.Drawing.Rectangle]::new($minX, $minY, $maxX - $minX + 1, $maxY - $minY + 1)
        $trimmed = $crop.Clone($trimRect, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $path = Join-Path $outDir ($Name + '.png')
            $trimmed.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
            Write-Host "SPRITE_OK $Name $($trimmed.Width)x$($trimmed.Height)"
        } finally { $trimmed.Dispose() }
    } finally { $crop.Dispose() }
}

try {
    # Coordinates are deliberately explicit against the owner's immutable 1448x1086 source atlas.
    # Runtime values and copy are excluded: these files are art, never commerce authority.
    Export-Crop 'night-market-wordmark' 360 0 670 188
    Export-Crop 'hanging-lantern' 0 0 112 185
    Export-Crop 'covenant-plaque' 112 0 248 180
    Export-Crop 'wallet-frame' 1020 18 225 132
    Export-Crop 'network-frame' 1240 20 208 132
    Export-Crop 'featured-starters-hand' 18 195 340 205
    Export-Crop 'resource-pack-1' 375 220 240 175
    Export-Crop 'resource-pack-2' 620 220 240 175
    Export-Crop 'permanent-builder' 870 220 230 175
    Export-Crop 'folks-thanks' 375 540 215 100
    Export-Crop 'starters-hand' 595 540 215 100
    Export-Crop 'timber-wagon' 815 535 135 105
    Export-Crop 'ingot-crate' 815 630 135 105
    Export-Crop 'quarry-cart' 815 725 135 105
    Export-Crop 'wood-icon' 1110 470 125 105
    Export-Crop 'iron-icon' 1210 470 125 105
    Export-Crop 'crystal-icon' 1310 465 125 115
    Export-Crop 'stone-icon' 1100 570 125 105
    Export-Crop 'coin-icon' 1210 560 125 115
    Export-Crop 'gift-icon' 1310 755 125 115
    Export-Crop 'calendar-icon' 1100 760 105 105
    Export-Crop 'season-track-icon' 1310 655 125 105
    Export-Crop 'builder-crest' 1310 875 135 140
} finally {
    $sourceBitmap.Dispose()
}

Copy-Item -LiteralPath $Source -Destination (Join-Path $outDir '_source-atlas.png') -Force
Write-Host "NIGHT_MARKET_SPRITES_OK out=$outDir"
