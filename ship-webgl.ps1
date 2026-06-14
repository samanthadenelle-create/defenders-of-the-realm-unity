# =============================================================================
# ship-webgl.ps1 - one command: build the WebGL player, then auto-push to itch.io.
#
# Wraps build-webgl.ps1 (-NoBrotli, itch-compatible) and, the instant the build
# succeeds, runs `butler push Builds\WebGL <target>:<channel>`. The build and the
# push can't truly run concurrently (the push uploads the build's output), but
# this collapses both into a single fire-and-forget command.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File .\ship-webgl.ps1
#   powershell -ExecutionPolicy Bypass -File .\ship-webgl.ps1 -SkipBuild   # push an existing Builds\WebGL
#   powershell -ExecutionPolicy Bypass -File .\ship-webgl.ps1 -Target "denellestudios/some-other-game" -Channel html5
#
# PREREQUISITES:
#   - WebGL Build Support module installed (see build-webgl.ps1 header)
#   - butler installed + logged in (butler login). Auto-located on PATH or in
#     %USERPROFILE%\butler\butler.exe.
#
# NOTE: keep this file ASCII-only (Windows PowerShell 5.1 reads BOM-less files as
# ANSI; non-ASCII chars corrupt and break parse).
# =============================================================================

param(
    # itch.io target  user/game-slug  (the live page is the SINGULAR slug).
    [string]$Target  = 'denellestudios/defenders-of-the-realm-defend-the-tower',
    # itch.io channel - 'html5' marks it a browser-playable build.
    [string]$Channel = 'html5',
    # Reuse the existing Builds\WebGL output instead of rebuilding (push only).
    [switch]$SkipBuild,
    # Ship full C# exception stack traces to the browser console (diagnostic builds).
    [switch]$DebugExceptions
)

$ErrorActionPreference = 'Stop'
$proj   = $PSScriptRoot
$outDir = Join-Path $proj 'Builds\WebGL'
$index  = Join-Path $outDir 'index.html'

# --- 1) Locate butler ---------------------------------------------------------
$butler = (Get-Command butler.exe -ErrorAction SilentlyContinue).Source
if (-not $butler) {
    $fallback = Join-Path $env:USERPROFILE 'butler\butler.exe'
    if (Test-Path $fallback) { $butler = $fallback }
}
if (-not $butler) {
    Write-Error "butler not found (not on PATH, not at $env:USERPROFILE\butler\butler.exe). Install it + run 'butler login' first."
    exit 2
}
Write-Host "[ship] butler: $butler"
Write-Host "[ship] target: ${Target}:${Channel}"

# --- 2) Build (unless -SkipBuild) ---------------------------------------------
if ($SkipBuild) {
    Write-Host "[ship] -SkipBuild: reusing existing $outDir"
    if (-not (Test-Path $index)) {
        Write-Error "[ship] -SkipBuild but no build found at $index. Drop -SkipBuild to build first."
        exit 1
    }
} else {
    $buildScript = Join-Path $proj 'build-webgl.ps1'
    if (-not (Test-Path $buildScript)) { Write-Error "[ship] build-webgl.ps1 not found at $buildScript."; exit 1 }

    Write-Host "[ship] Building WebGL (Brotli + decompressionFallback, itch-safe). First IL2CPP compile can take 30-60 min..."
    # commit 73796b7: itch REJECTS the -NoBrotli uncompressed build (WebGL.data ~223MB > itch per-file limit -> blank page).
    # WebGLBuild.cs now always ships Brotli + decompressionFallback=true (Unity decompresses .br in-JS: itch-serveable, ~half size).
    $buildArgs = @()              # was -NoBrotli (uncompressed -> too large for itch); now Brotli+fallback (small + itch-serveable)
    if ($DebugExceptions) { $buildArgs += '-DebugExceptions' }
    & powershell -ExecutionPolicy Bypass -File $buildScript @buildArgs
    $buildExit = $LASTEXITCODE

    if ($buildExit -ne 0 -or -not (Test-Path $index)) {
        Write-Error "[ship] BUILD FAILED (exit=$buildExit, index exists=$(Test-Path $index)). NOT pushing. See Builds\webgl-build.log."
        if ($buildExit -ne 0) { exit $buildExit } else { exit 1 }
    }
    Write-Host "[ship] Build OK -> $index"
}

# --- 3) Push to itch ----------------------------------------------------------
Write-Host "[ship] Pushing $outDir -> ${Target}:${Channel} ..."
& $butler push $outDir "${Target}:${Channel}"
$pushExit = $LASTEXITCODE

if ($pushExit -ne 0) {
    Write-Error "[ship] PUSH FAILED (butler exit=$pushExit). Build is fine at $outDir; retry the push."
    exit $pushExit
}

Write-Host ""
Write-Host "[ship] DONE. Build pushed + itch is processing it (live in a minute or two)."
Write-Host "[ship] Status any time:  $butler status ${Target}:${Channel}"
exit 0
