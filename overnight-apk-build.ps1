# overnight-apk-build.ps1 - DETACHED Seeker APK build (survives harness reaping).
# Runs the Android batchmode build, pushes content, PROVES the content is hosted,
# and writes status markers. ASCII-only (PS 5.1 parses this file BOM-less).
#
# Repo root is machine-dependent (C:\eoa on one box, D:\eoa on another) - resolve it from
# this script's own location instead of hardcoding a drive letter.
#
# -----------------------------------------------------------------------------
# WHY -BuildTarget Android IS PASSED EXPLICITLY (2026-08-19, WO-1124)
# AndroidBuild.BuildSeekerApk calls AddressablesContentBuild.EnsureBuilt BEFORE
# BuildPipeline.BuildPlayer, and BuildPlayer is what switches the active target.
# Addressables builds for the ACTIVE target, so content landed in whichever platform
# folder the editor was last on. On 2026-08-19 that was StandaloneWindows64: a real
# 476 MB Android APK shipped stamped 332462 while ServerData/Android's newest catalog
# was still yesterday's 331367. Every marker in the chain was green and the device
# would have resolved NOTHING - no buildings, no enemies, silently.
# Forcing the target at process start makes the content build land on Android.
# THIS IS A BELT, NOT THE FIX: WO-1124 moves the switch inside BuildSeekerApk so every
# caller gets it. Do not delete this line when that lands - a redundant switch is free.
#
# WHY THE PARITY GATE IS HERE (2026-08-19)
# PROD-011's gate shipped and was wired into morning-ship-chain.ps1 ONLY. This script,
# distribute-android.ps1 and install-apk-to-seeker.ps1 had no parity check at all, so
# shipping through any of them silently skipped the one gate that can prove the APK's
# content is actually hosted. R2_PUSH_OK is NOT that proof: on 2026-08-19 it reported
# "6 uploaded (175.9 MB)" for 175 MB of the WRONG PLATFORM's bundles.
# =============================================================================
Set-Location $PSScriptRoot
$status = 'Builds\overnight-apk-status.txt'
New-Item -ItemType Directory -Force -Path 'Builds' | Out-Null
$startedAt = Get-Date
"APK_START $(Get-Date -Format o)" | Out-File -Encoding ascii $status

try {
    & '.\run-unity-method.ps1' -Method DeNelle.Editor.AndroidBuild.BuildSeekerApk -LogName apk-build.log -TimeoutMin 120 -BuildTarget Android
} catch {
    "APK_THREW $($_.Exception.Message)" | Out-File -Encoding ascii -Append $status
}

# ⛔ FRESHNESS, NOT EXISTENCE (2026-08-19). This used to take the newest *.apk on disk and
# call it success. On 2026-08-19 18:48 the build FAILED (gradle could not configure the
# AdsIdentity.androidlib module), no APK was written, this glob found the 16:22 artifact,
# and the script printed APK_OK with its size - so a STALE build was installed to the
# owner's device and reported as the new one. morning-ship-chain.ps1 already carried this
# lesson in its own comments: "Existence proves nothing. Freshness does."
$apk = Get-ChildItem (Join-Path $PSScriptRoot 'Builds') -Recurse -Filter *.apk -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($apk -and $apk.LastWriteTime -lt $startedAt) {
    "APK_STALE $(Get-Date -Format o) newest apk is $($apk.FullName) dated $($apk.LastWriteTime.ToString('o')) - OLDER than this run. The build produced NO apk; see Builds/apk-build.log. DO NOT INSTALL IT." | Out-File -Encoding ascii -Append $status
    "APK_DONE $(Get-Date -Format o)" | Out-File -Encoding ascii -Append $status
    exit 1
}
if ($apk) {
    "APK_OK $(Get-Date -Format o) path=$($apk.FullName) size=$([math]::Round($apk.Length/1MB,0))MB" | Out-File -Encoding ascii -Append $status
} else {
    "APK_FAILED_NO_APK $(Get-Date -Format o) (see Builds\apk-build.log)" | Out-File -Encoding ascii -Append $status
    "APK_DONE $(Get-Date -Format o)" | Out-File -Encoding ascii -Append $status
    exit 1
}

# --- Content: push, then PROVE it is hosted -----------------------------------
# Push the PARENT folder. '--push ServerData/Android' flattens keys to the bucket
# root and the game never reads them - the tool's own docstring still teaches that
# wrong form, so it is spelled out here.
try {
    & python (Join-Path $PSScriptRoot 'tools\r2_sync.py') --push ServerData *>&1 |
        Tee-Object -FilePath 'Builds\r2-push.log'
} catch {
    "R2_PUSH_THREW $($_.Exception.Message)" | Out-File -Encoding ascii -Append $status
}

$parityLog = 'Builds\r2-parity-overnight.log'
try {
    & python (Join-Path $PSScriptRoot 'tools\r2_sync.py') --verify-catalog ServerData/Android *>&1 |
        Tee-Object -FilePath $parityLog
} catch {
    "R2_PARITY_THREW $($_.Exception.Message)" | Out-File -Encoding ascii -Append $status
}

# Judge by the MARKER on a fresh log, never the exit code - the runners in this repo
# exit 0 on refusals and FAILs (memory: gates-report-success-without-proving-it).
if ((Test-Path $parityLog) -and (Select-String -Path $parityLog -Pattern 'R2_PARITY_OK' -Quiet)) {
    $line = (Select-String -Path $parityLog -Pattern 'R2_PARITY_OK' | Select-Object -First 1).Line.Trim()
    "R2_PARITY_OK $(Get-Date -Format o) $line" | Out-File -Encoding ascii -Append $status
} else {
    "R2_PARITY_FAILED $(Get-Date -Format o) - the APK references bundles the bucket does not hold." | Out-File -Encoding ascii -Append $status
    "  DO NOT INSTALL OR DISTRIBUTE THIS BUILD. Players would see no buildings and no enemies," | Out-File -Encoding ascii -Append $status
    "  with no error on screen. Fix: python tools\r2_sync.py --push ServerData   (the PARENT)." | Out-File -Encoding ascii -Append $status
    "  See $parityLog" | Out-File -Encoding ascii -Append $status

    # WO-1124 sec.5.3: FAIL CLOSED. Until now this branch only WROTE "DO NOT INSTALL" into a
    # status file - advice, not a gate. Anything downstream (install-apk-to-seeker,
    # distribute-android, or a human reading the last line) proceeded exactly as if parity had
    # passed, which is how a build whose content the CDN does not host reaches a device with
    # every marker green. A non-zero exit is the only form of "do not install" a script can
    # actually enforce.
    Write-Host "[apk] R2_PARITY_FAILED - refusing to continue. See $parityLog"
    exit 3
}

"APK_DONE $(Get-Date -Format o)" | Out-File -Encoding ascii -Append $status
