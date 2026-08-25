#requires -Version 5.1
# =============================================================================
# install-apk-to-seeker.ps1 — one-command build + sideload to the Solana Seeker
# -----------------------------------------------------------------------------
# Usage:
#   .\install-apk-to-seeker.ps1                 # build + install
#   .\install-apk-to-seeker.ps1 -Build:$false   # skip build, just install
#   .\install-apk-to-seeker.ps1 -Install:$false # build only, no adb push
#
# Prerequisites:
#   1. Unity Hub -> 6000.4.8f1 -> Add Modules -> Android Build Support (with
#      OpenJDK + SDK + NDK). The script aborts if AndroidPlayer is missing.
#   2. Seeker connected via USB with Developer Options + USB Debugging on.
#      First connect prompts "Allow USB debugging?" on the phone — accept.
# =============================================================================

[CmdletBinding()]
param(
    [bool]$Build = $true,
    [bool]$Install = $true
)

$ErrorActionPreference = 'Stop'

$pinned = '6000.4.8f1'
$unity = "C:\Program Files\Unity\Hub\Editor\$pinned\Editor\Unity.exe"
$proj  = Split-Path -Parent $PSCommandPath
$apk   = Join-Path $proj 'Builds\Android\DefendersOfTheRealm.apk'
$androidModule = "C:\Program Files\Unity\Hub\Editor\$pinned\Editor\Data\PlaybackEngines\AndroidPlayer"
$adb   = Join-Path $androidModule 'SDK\platform-tools\adb.exe'

# --- Preflight ---------------------------------------------------------------
if (-not (Test-Path $unity)) {
    Write-Error "Unity not found at $unity"
    exit 1
}
if (-not (Test-Path $androidModule)) {
    Write-Error @"
Android Build Support module is NOT installed for Unity $pinned.
Open Unity Hub -> Installs -> click the gear on $pinned -> Add Modules ->
check 'Android Build Support' (and its OpenJDK + SDK + NDK children).
After the ~2 GB download finishes, re-run this script.
"@
    exit 1
}

# --- Build -------------------------------------------------------------------
if ($Build) {
    Write-Host "=== Building APK (this can take 5-15 min the first time, Gradle pulls deps) ===" -ForegroundColor Cyan

    # Clear any stale UnityLockfile so the build can start cleanly.
    $lock = Join-Path $proj 'Temp\UnityLockfile'
    if (Test-Path $lock) { Remove-Item $lock -Force }

    # Clear prior APK so we can detect a silent build failure.
    if (Test-Path $apk) { Remove-Item $apk -Force }

    $log = Join-Path $proj 'Builds\build-android.log'
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $proj 'run-unity-method.ps1') `
        -Method 'DeNelle.Editor.AndroidBuild.BuildSeekerApk' `
        -LogName 'build-android.log' `
        -BuildTarget Android `
        -ExpectMarker '[AndroidBuild] SUCCEEDED' `
        -TimeoutMin 120
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Android build runner failed with exit code $LASTEXITCODE; marker [AndroidBuild] SUCCEEDED was not proven in a fresh log."
        exit $LASTEXITCODE
    }

    if (-not (Test-Path $apk)) {
        Write-Error "Build did not produce an APK at $apk."
        Write-Host "Last 25 lines of $log :" -ForegroundColor Yellow
        if (Test-Path $log) { Get-Content $log -Tail 25 }
        exit 1
    }
    $apkSize = (Get-Item $apk).Length / 1MB
    Write-Host ("APK built: {0} ({1:N1} MB)" -f $apk, $apkSize) -ForegroundColor Green
}

# --- Install -----------------------------------------------------------------
if ($Install) {
    if (-not (Test-Path $adb)) {
        Write-Error "adb.exe not found at $adb. Confirm the Android SDK platform-tools were installed by the Unity module."
        exit 1
    }
    if (-not (Test-Path $apk)) {
        Write-Error "APK not present at $apk. Run with -Build:`$true first."
        exit 1
    }

    Write-Host "=== Checking for connected devices ===" -ForegroundColor Cyan
    # ---------------------------------------------------------------------
    # START THE DAEMON FIRST, AND SWALLOW ITS STDERR. (fixed 2026-08-21)
    # When no adb daemon is running, `adb devices` prints "* daemon not
    # running; starting now at tcp:5037" TO STDERR. Windows PowerShell 5.1
    # wraps a native command's stderr in an ErrorRecord, which under this
    # script's error preference is TERMINATING - so the whole install aborted
    # on a purely informational line, AFTER a 5-15 minute APK build. This bit
    # twice (2026-08-21, both times at line ~90).
    # start-server is idempotent; 2>$null keeps its chatter out of $devices.
    # 2>$null is NOT enough - PS 5.1 still raises the ErrorRecord. The only
    # reliable fix is to relax the preference around the native calls.
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        cmd /c "`"$adb`" start-server" 2>&1 | Out-Null
        $devices = cmd /c "`"$adb`" devices" 2>&1
    } finally {
        $ErrorActionPreference = $prevEap
    }
    Write-Host ($devices -join "`n")
    if (($devices | Select-String -Pattern '\sdevice$').Count -eq 0) {
        Write-Error @"
No Android device in 'device' state. Confirm:
  - Seeker is plugged in via USB
  - USB debugging is enabled (Settings > Developer Options > USB debugging)
  - First-connect prompt 'Allow USB debugging?' was accepted on the phone
  - If you see 'unauthorized', tap 'Allow' on the phone and rerun
"@
        exit 1
    }

    # --- CONTENT SHIP (push + parity) ----------------------------------------
    # An APK whose remote bundles are not in the bucket installs perfectly and then shows
    # no buildings and no enemies, with no error on screen. Enemy/structure ART is remote;
    # there is no local fallback. Bundle names are content-hashed, so EVERY build needs
    # its own push - a previous build's push cannot cover this one.
    #
    # NOW PUSHES, does not only check (owner ruling 2026-08-20: "wire the r2 push into
    # the ship chain"). The push/verify rules live once, in tools/r2-ship.ps1.
    #
    # -WarnOnly is deliberate HERE and only here: sideloading a knowingly-offline or
    # experimental build from this script is legitimate, so a mismatch must not block
    # the install - but the owner must never be surprised by it. The distribution
    # chains (morning-ship-chain.ps1, overnight-apk-build.ps1) BLOCK instead.
    & powershell -NoProfile -File (Join-Path $PSScriptRoot 'tools/r2-ship.ps1') -WarnOnly

    Write-Host "=== Installing APK to Seeker ===" -ForegroundColor Cyan
    & $adb install -r $apk
    if ($LASTEXITCODE -ne 0) {
        Write-Error "adb install returned $LASTEXITCODE."
        exit 1
    }

    Write-Host "=== Done ===" -ForegroundColor Green
    Write-Host "Launch 'Echoes of Elarion' from the Seeker's app drawer."
    Write-Host "To pull live logs: $adb logcat -s Unity"
}
