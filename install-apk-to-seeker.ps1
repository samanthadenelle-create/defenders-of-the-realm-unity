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
#   1. Unity Hub -> 6000.4.7f1 -> Add Modules -> Android Build Support (with
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

$unity = 'C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe'
$proj  = Split-Path -Parent $PSCommandPath
$apk   = Join-Path $proj 'Builds\Android\DefendersOfTheRealm.apk'
$adb   = 'C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe'

# --- Preflight ---------------------------------------------------------------
if (-not (Test-Path $unity)) {
    Write-Error "Unity not found at $unity"
    exit 1
}
if (-not (Test-Path 'C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Data\PlaybackEngines\AndroidPlayer')) {
    Write-Error @"
Android Build Support module is NOT installed for Unity 6000.4.7f1.
Open Unity Hub -> Installs -> click the gear on 6000.4.7f1 -> Add Modules ->
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
    & $unity -batchmode -quit -buildTarget Android `
        -projectPath $proj `
        -executeMethod 'DeNelle.Editor.AndroidBuild.BuildSeekerApk' `
        -logFile $log

    # Wait for Unity to fully release the lock so a follow-up build doesn't race.
    while (Get-Process -Name 'Unity' -ErrorAction SilentlyContinue |
           Where-Object { $_.MainWindowTitle -eq '' }) {
        Start-Sleep -Milliseconds 500
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
    $devices = & $adb devices
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
