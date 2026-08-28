# =============================================================================
# r2-ship.ps1 - THE one way content reaches players. Push, then PROVE it is hosted.
# -----------------------------------------------------------------------------
# Usage (from repo root or anywhere - it resolves the root itself):
#   powershell -File tools\r2-ship.ps1                 # push + verify, BLOCKS on failure
#   powershell -File tools\r2-ship.ps1 -Target WebGL   # Pi/WebGL push + verify
#   powershell -File tools\r2-ship.ps1 -WarnOnly       # push + verify, warns instead of failing
#   powershell -File tools\r2-ship.ps1 -VerifyOnly     # prove only, upload nothing
#
# Exit codes: 0 = R2_PARITY_OK (or -WarnOnly with a failure). 16 = parity failed.
#
# STOP WHY THIS FILE EXISTS - the failure it is built to make impossible.
#
# Enemy and structure ART is served REMOTELY from R2. It is NOT in the APK. There is
# no local fallback: Assets/Resources/Enemies and Assets/Resources/Structures no
# longer exist. So an APK whose bundles were never uploaded installs perfectly,
# launches perfectly, and shows tinted capsules where the enemies should be and
# placeholders where the buildings should be - WITH NO ERROR ON SCREEN. The player
# just sees a broken world.
#
# !! BUNDLE NAMES ARE CONTENT-HASHED. Every content build produces new filenames, so
# EVERY build needs ITS OWN push. A push from a previous build can never cover this
# one. That single sentence is the whole trap: the bucket looks full, the previous
# build works, and the new one is broken.
#
# THIS HAS NOW HAPPENED THREE TIMES:
#   2026-08-18  an APK sat ready to install whose enemy bundle had never been
#               uploaded. Caught by hand. Commit 16e22dba3 conceded in its own body:
#               "NO GATE COULD HAVE CAUGHT THIS."
#   2026-08-19  a real Android APK shipped carrying StandaloneWindows64 content,
#               every other marker green (WO-1124).
#   2026-08-20  the owner played a build where EVERY enemy was a capsule. The CLI
#               re-grouped and re-packed enemy content that day, which re-hashed
#               every bundle, and never pushed. Two wrong causes were proposed
#               (a duplicated [BuildTarget] token, then a stale content build) before
#               the DEVICE named it in one line:
#                 RemoteProviderException : Unable to load asset bundle from :
#                   https://pub-...r2.dev/Android/enemy_art_assets_enemyfam-hollow_...bundle
#                 UnityWebRequest result : ProtocolError : HTTP/1.1 404 Not Found
#               Owner ruling that day: "wire the r2 push into the ship chain."
#
# STOP AND THE REASON IT IS ONE FILE. Before this, the push+verify pair was COPY-PASTED
# into overnight-apk-build.ps1 and morning-ship-chain.ps1, and they had ALREADY
# drifted apart: overnight pushed then verified; morning ONLY VERIFIED and then told
# a human to go push by hand. A gate whose remedy is "a human remembers to run a
# second command" is not a gate - that is precisely the step that got skipped on
# 08-20. Same fact in three files is how this repo lost a WO number block and a
# dependency table; here it would keep costing whole play sessions.
#
# STOP PUSH THE PARENT, ALWAYS. '--push ServerData/Android' FLATTENS the keys to the
# bucket root, where the game never looks. It reports R2_PUSH_OK while uploading 103
# objects nobody can read - observed on 2026-08-20. The correct form is
# '--push ServerData'. Verify, however, needs the EXPLICIT target
# ('--verify-catalog ServerData/Android') because ServerData holds both Android and
# StandaloneWindows64 and the tool refuses to guess. The two commands genuinely take
# different arguments; that asymmetry is why they are hard-coded here exactly once.
#
# STOP JUDGE BY THE MARKER, NEVER THE EXIT CODE. The runners in this repo exit 0 on
# refusals and FAILs (memory: gates-report-success-without-proving-it). Marker
# absence on a fresh log is a FAILURE, not an unknown.
# =============================================================================

[CmdletBinding()]
param(
    [ValidateSet('Android', 'WebGL')]
    [string]$Target = 'Android',
    # Warn and continue instead of failing. For deliberately-offline or experimental
    # sideloads, where a mismatched bucket is a known and accepted state.
    [switch]$WarnOnly,
    # Prove only - upload nothing. Use when you know the push already happened and
    # you want the proof without the transfer.
    [switch]$VerifyOnly
)

$ErrorActionPreference = 'Stop'

# Repo root = this script's parent's parent (tools\r2-ship.ps1 -> tools -> root).
$root    = Split-Path -Parent $PSScriptRoot
$sync    = Join-Path $root 'tools\r2_sync.py'
$logDir  = Join-Path $root 'Builds'
$pushLog = Join-Path $logDir 'r2-push.log'
$verLog  = Join-Path $logDir 'r2-parity.log'

if (-not (Test-Path $sync)) {
    Write-Host "R2_SHIP_FAIL: tools\r2_sync.py not found at $sync" -ForegroundColor Red
    exit 16
}
if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir | Out-Null }

# ---- 1. PUSH (the parent - see the header) -----------------------------------
if (-not $VerifyOnly) {
    & python $sync --ensure-cors
    Write-Host "[r2-ship] pushing ServerData (the PARENT - never ServerData/Android) ..." -ForegroundColor Cyan
    try {
        & python $sync --push ServerData *>&1 | Tee-Object -FilePath $pushLog
    } catch {
        # A throw here is not fatal on its own: the verify below is the authority on
        # whether the bucket actually holds what this build needs. Record and continue.
        "R2_PUSH_THREW $($_.Exception.Message)" | Out-File -Encoding ascii -Append $pushLog
        Write-Host "[r2-ship] push threw: $($_.Exception.Message) - continuing to verify, which is the authority." -ForegroundColor Yellow
    }
    if ((Test-Path $pushLog) -and (Select-String -Path $pushLog -Pattern 'R2_PUSH_OK' -Quiet)) {
        Write-Host "  $((Select-String -Path $pushLog -Pattern 'R2_PUSH_OK' | Select-Object -First 1).Line.Trim())"
    }
} else {
    Write-Host "[r2-ship] -VerifyOnly: uploading nothing." -ForegroundColor Yellow
}

# ---- 2. VERIFY (explicit target - see the header) ----------------------------
Write-Host "[r2-ship] verifying every remote object this build's catalog names ..." -ForegroundColor Cyan
if (Test-Path $verLog) { Remove-Item $verLog -Force }   # a STALE log must never read as a pass
try {
    & python $sync --verify-catalog "ServerData/$Target" *>&1 | Tee-Object -FilePath $verLog
} catch {
    "R2_PARITY_THREW $($_.Exception.Message)" | Out-File -Encoding ascii -Append $verLog
}

$ok = (Test-Path $verLog) -and (Select-String -Path $verLog -Pattern 'R2_PARITY_OK' -Quiet)

if ($ok) {
    $line = (Select-String -Path $verLog -Pattern 'R2_PARITY_OK' | Select-Object -First 1).Line.Trim()
    Write-Host "  $line" -ForegroundColor Green
    exit 0
}

Write-Host ""
Write-Host "  R2 CONTENT PARITY FAILED." -ForegroundColor Red
Write-Host "  This build references remote bundles the bucket does not hold."
Write-Host "  On device that is tinted-capsule enemies and placeholder buildings," -ForegroundColor Red
Write-Host "  with NO error shown to the player." -ForegroundColor Red
Write-Host "  Bundle names are content-hashed, so a push from an earlier build cannot cover this one."
Write-Host "  See $verLog"
Write-Host ""

if ($WarnOnly) {
    Write-Host "  -WarnOnly was set: continuing anyway. Do not treat this build as shippable." -ForegroundColor Yellow
    exit 0
}
exit 16
