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
#
# -Defines (2026-08-22): owner-test scripting symbols forwarded to the PLAYER
# compilation, e.g. -Defines 'STORE_RAIL_LOCAL_TEST;MONETIZATION_LOCAL_TEST' for the
# Devnet purchase canary. Omitted => empty => monetization stays OFF, unchanged. This
# passthrough exists so an owner-test APK can be produced WITHOUT leaving the sanctioned
# chain: the alternative was a raw run-unity-method call, which skips the s16 R2
# push+verify that this script carries. Never add a second build path instead.
#
# -Tester : adds the TESTER_BUILD scripting define (owner ruling 2026-08-24). This is the APK
#   that goes to FIREBASE APP DISTRIBUTION, and it turns on owner-facing tooling that must
#   NEVER reach the Solana dApp Store - today that is the one-tap FLAG capture chip.
#   Until 2026-08-24 both destinations produced the SAME artifact (BuildOptions.None release,
#   so Debug.isDebugBuild is false on the tester build too), which is why on-device tooling
#   kept vanishing on the very device it was built for.
#   THE SWITCH IS OPT-IN ON PURPOSE: its ABSENCE is store-safe. A store APK cannot ship dev
#   tooling by someone FORGETTING a flag, only by someone explicitly ADDING one. Do not make
#   this the default, and do not invert the sense.
# =============================================================================
param([string]$Defines = '', [switch]$Tester)
if ($Tester) {
    if ([string]::IsNullOrWhiteSpace($Defines)) { $Defines = 'TESTER_BUILD' }
    else { $Defines = "$Defines;TESTER_BUILD" }
    Write-Host "[apk] TESTER build - defines: $Defines"
} else {
    Write-Host "[apk] STORE-shaped build (no TESTER_BUILD define) - defines: '$Defines'"
}
Set-Location $PSScriptRoot
$status = 'Builds\overnight-apk-status.txt'
New-Item -ItemType Directory -Force -Path 'Builds' | Out-Null
$startedAt = Get-Date
"APK_START $(Get-Date -Format o)" | Out-File -Encoding ascii $status

# WO-1173: block before creating an APK that can reach a device or store.
$schemaLog = 'Builds\schema-parity.log'
& node (Join-Path $PSScriptRoot 'tools\schema-parity.mjs') 2>&1 | Tee-Object -FilePath $schemaLog
if ($LASTEXITCODE -ne 0 -or
    -not (Select-String -Path $schemaLog -Pattern '^SCHEMA_PARITY_OK ' -Quiet)) {
    "SCHEMA_PARITY_FAILED $(Get-Date -Format o) - refusing APK build; see $schemaLog" | Out-File -Encoding ascii -Append $status
    Write-Host "[apk] SCHEMA_PARITY_OK absent - refusing to build. See $schemaLog"
    exit 4
}
"SCHEMA_PARITY_OK $(Get-Date -Format o)" | Out-File -Encoding ascii -Append $status

try {
    & '.\run-unity-method.ps1' -Method DeNelle.Editor.AndroidBuild.BuildSeekerApk -LogName apk-build.log -TimeoutMin 120 -BuildTarget Android -ExtraScriptingDefines $Defines -ExpectMarker '[AndroidBuild] SUCCEEDED'
    if ($LASTEXITCODE -ne 0) {
        throw "APK Unity build marker absent; see Builds\apk-build.log"
    }
} catch {
    "APK_THREW $($_.Exception.Message)" | Out-File -Encoding ascii -Append $status
}

# STOP FRESHNESS, NOT EXISTENCE (2026-08-19). This used to take the newest *.apk on disk and
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
# Delegated to tools/r2-ship.ps1 (owner ruling 2026-08-20). This block used to carry
# its own copy of push+verify; morning-ship-chain.ps1 carried a DIFFERENT copy that
# only verified. Same fact, two files, already drifted - so the pair now lives once.
$parityLog = 'Builds/r2-parity.log'
try {
    & powershell -NoProfile -File (Join-Path $PSScriptRoot 'tools/r2-ship.ps1')
} catch {
    "R2_SHIP_THREW $($_.Exception.Message)" | Out-File -Encoding ascii -Append $status
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
