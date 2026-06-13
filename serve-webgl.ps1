# =============================================================================
# serve-webgl.ps1 - serve the local WebGL build for fast in-browser testing.
#
# WHY: testing a WebGL build no longer needs an itch round-trip. Build it, serve
# it locally, open the URL, and verify it works IN THE BROWSER before pushing to
# the community. Catches WebGL-only breakage (editor-fine, browser-broken) fast.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File .\serve-webgl.ps1
#   powershell -ExecutionPolicy Bypass -File .\serve-webgl.ps1 -Port 9000
#
# Then open the printed URL (give a big payload 30-60s to load). Ctrl+C to stop.
# Requires a build at Builds\WebGL (run build-webgl.ps1 first) + python on PATH.
# Keep ASCII-only (Windows PowerShell 5.1 BOM-less = ANSI).
# =============================================================================
param([int]$Port = 8000)

$dir   = Join-Path $PSScriptRoot 'Builds\WebGL'
$index = Join-Path $dir 'index.html'

if (-not (Test-Path $index)) {
    Write-Error "[serve] No WebGL build at $dir (no index.html). Run build-webgl.ps1 first."
    exit 1
}

Write-Host "[serve] WebGL build -> http://localhost:$Port"
Write-Host "[serve] (large payload? give it 30-60s to load. Ctrl+C to stop.)"
python -m http.server $Port --directory $dir
