# =============================================================================
# morning-ship-chain.ps1 - the whole remaining ship list in one command:
#     EXE  ->  APK  ->  Firebase App Distribution  ->  WebGL  ->  Vercel
#
# WHY THIS EXISTS (2026-08-08): the 08-07 overnight run finished every code and
# canon task but could not produce a single player build. Commit charge sat at
# 119.5 GB of a 127.8 GB limit with NO Unity process running and only ~31 GB
# attributable to any process or kernel pool - roughly 88 GB leaked and
# unreclaimable, so there was nothing to kill. Unity died with
# "Fatal Error! Could not allocate memory: System out of memory!".
# A reboot is the only fix. This script is what to run after that reboot.
#
# Usage:  powershell -ExecutionPolicy Bypass -File .\morning-ship-chain.ps1
#         ... -SkipExe            (APK onward only)
#         ... -SkipWeb            (stop after the Firebase release)
#         ... -MinFreeCommitGB 50 (override the memory gate)
#
# NOTE: keep this file ASCII-only. Windows PowerShell 5.1 reads BOM-less files
# as ANSI, so non-ASCII chars (em-dashes, smart quotes) corrupt and break parse.
# =============================================================================

param(
    [switch]$SkipExe,
    [switch]$SkipApk,
    [switch]$SkipFirebase,
    [switch]$SkipWeb,
    [double]$MinFreeCommitGB = 60,
    [string]$Notes = ""
)

$ErrorActionPreference = 'Stop'
$proj = $PSScriptRoot
Set-Location $proj

function Say([string]$m) { Write-Host "[chain] $m" }
function Die([string]$m, [int]$code) { Write-Host ""; Write-Host "[chain] *** STOPPED: $m ***"; exit $code }

# --- 0) MEMORY GATE ----------------------------------------------------------
# Fail in two seconds instead of forty minutes. A player build that starts on a
# machine still carrying the leak does not fail fast - Unity grinds through
# script compilation, asset import and half of IL2CPP before it hits the wall,
# so the cost of NOT checking is most of an hour and a corrupted output dir.
$c        = Get-Counter '\Memory\Committed Bytes','\Memory\Commit Limit'
$used     = ($c.CounterSamples | Where-Object { $_.Path -like '*committed bytes' }).CookedValue / 1GB
$limit    = ($c.CounterSamples | Where-Object { $_.Path -like '*commit limit' }).CookedValue / 1GB
$freeGB   = $limit - $used
Say ("commit charge {0:N1} GB of {1:N1} GB  ->  {2:N1} GB headroom" -f $used, $limit, $freeGB)

if ($freeGB -lt $MinFreeCommitGB) {
    Write-Host ""
    Write-Host ("  Headroom is {0:N1} GB; a player build needs roughly {1} GB." -f $freeGB, $MinFreeCommitGB)
    Write-Host "  If you have ALREADY rebooted and still see this, the leak is not from the"
    Write-Host "  overnight run - check for a driver/service holding commit:"
    Write-Host ""
    Write-Host "    Get-CimInstance Win32_Process | Sort PageFileUsage -Desc | Select -First 10 Name,PageFileUsage"
    Write-Host ""
    Write-Host "  If the process sum is far BELOW the commit charge, it is a kernel leak again"
    Write-Host "  and only a reboot clears it. Override with -MinFreeCommitGB if you disagree."
    Die "not enough commit headroom to build" 90
}

# A batchmode build cannot take the project lock while the editor is open.
if (Get-Process -Name 'Unity' -ErrorAction SilentlyContinue) {
    Die "the Unity editor is running - close it first (project lock)" 3
}

$exePath = Join-Path $proj 'Builds\Windows\DefendersOfTheRealm.exe'
$apkPath = Join-Path $proj 'Builds\Android\DefendersOfTheRealm.apk'
$webIdx  = Join-Path $proj 'Builds\WebGL\index.html'

# Capture BEFORE timestamps. Every verification below compares against these
# rather than merely testing existence - on 08-07 a failed exe build left the
# previous day's artifact in place and its payload inspected as perfectly
# healthy (207 DLLs, Assembly-CSharp present) because it WAS a healthy build,
# just the wrong one. Existence proves nothing. Freshness does.
function StampOf([string]$p) { if (Test-Path $p) { (Get-Item $p).LastWriteTimeUtc } else { [datetime]::MinValue } }
$exeWas = StampOf $exePath
$apkWas = StampOf $apkPath
$webWas = StampOf $webIdx

# --- 1) WINDOWS EXE ----------------------------------------------------------
if (-not $SkipExe) {
    Say "1/4 Windows x64 player ..."
    & powershell -ExecutionPolicy Bypass -File (Join-Path $proj 'build-windows.ps1')
    if ((StampOf $exePath) -le $exeWas) {
        Die "exe not refreshed (still $exeWas UTC). See Builds\build.log" 11
    }
    Say ("     OK  {0:N1} MB  {1}" -f ((Get-Item $exePath).Length/1MB), (Get-Item $exePath).LastWriteTime)
} else { Say "1/4 Windows exe SKIPPED" }

# --- 2) ANDROID APK ----------------------------------------------------------
# run-unity-method needs -BuildTarget Win64 after an Android build or the next
# desktop/Addressables run fails on a target mismatch; the APK direction is
# handled by AndroidBuild itself.
if (-not $SkipApk) {
    Say "2/4 Seeker APK (IL2CPP/ARM64, release-signed) ..."
    & powershell -ExecutionPolicy Bypass -File (Join-Path $proj 'run-unity-method.ps1') `
        -Method DeNelle.Editor.AndroidBuild.BuildSeekerApk -LogName apk-build.log -TimeoutMin 120
    if ((StampOf $apkPath) -le $apkWas) {
        Die "apk not refreshed (still $apkWas UTC). See Builds\apk-build.log" 12
    }
    # Release signing is a WARNING-only fallback inside AndroidBuild: a missing or
    # incomplete keystore.properties silently debug-signs, and a debug-signed APK
    # cannot update a tester's install in place - they get a confusing
    # signature-mismatch failure days later, not now. Assert it here.
    $alog = Join-Path $proj 'Builds\apk-build.log'
    if (Test-Path $alog) {
        if (Select-String -Path $alog -Pattern 'DEBUG signing' -Quiet) {
            Die "APK was DEBUG-signed - testers cannot update in place. Fix keystore.properties and rebuild" 13
        }
        if (-not (Select-String -Path $alog -Pattern 'RELEASE signing' -Quiet)) {
            Die "no RELEASE signing line in apk-build.log - cannot prove the APK is properly signed" 13
        }
    }
    Say ("     OK  {0:N0} MB  {1}  (release-signed)" -f ((Get-Item $apkPath).Length/1MB), (Get-Item $apkPath).LastWriteTime)
} else { Say "2/4 APK SKIPPED" }

# --- 2b) R2 CONTENT PARITY (PROD-011) ---------------------------------------
# WHY THIS EXISTS: Structure_Art and Enemy_Art are REMOTE Addressables groups
# served from R2, and their bundle names are CONTENT-HASHED - so every build
# emits new bundle names that must be uploaded. Miss the upload and the game
# does NOT crash: StructureAssetLoader finds the address registered, the remote
# load returns null, and Assets/Resources/Structures + /Enemies no longer exist
# as a fallback, so the player gets PLACEHOLDER GEOMETRY. Silent to the player,
# invisible to every other gate. On 2026-08-18 an APK sat on disk ready to
# install whose enemy bundle had never been uploaded at all; it was caught by
# hand. 16e22dba3 conceded in its own body: "NO GATE COULD HAVE CAUGHT THIS."
# This is that gate. It runs AFTER the APK (the Addressables content build
# happens inside BuildSeekerApk) and BEFORE distribution.
if (-not $SkipApk) {
    Say "2b/4 R2 content parity ..."
    # EXPLICIT TARGET FOLDER (2026-08-19): ServerData holds BOTH Android and StandaloneWindows64,
    # and bare --verify-catalog refuses to guess ("FAIL: cannot pick a build target"). Bare form =
    # no marker = this chain Dies at the parity step for a reason unrelated to parity.
    $parityLog = Join-Path $proj 'Builds\r2-parity.log'
    & python (Join-Path $proj 'tools\r2_sync.py') --verify-catalog ServerData/Android *>&1 | Tee-Object -FilePath $parityLog
    # Judge by the MARKER on a fresh log, never the exit code - this project's
    # runners exit 0 on refusals (memory: gates-report-success-without-proving-it).
    if (-not (Test-Path $parityLog)) { Die "r2 parity produced no log - cannot prove content is hosted" 16 }
    if (-not (Select-String -Path $parityLog -Pattern 'R2_PARITY_OK' -Quiet)) {
        Write-Host ""
        Write-Host "  The APK references remote bundles that are NOT in the bucket."
        Write-Host "  Players would see placeholder buildings/enemies with no error."
        Write-Host "  FIX:  python tools\r2_sync.py --push ServerData     <-- the PARENT folder"
        Write-Host "        (never 'ServerData/Android' - that flattens keys to the bucket root)"
        Write-Host "  Then re-run this chain. See Builds\r2-parity.log"
        Die "R2 content parity FAILED - refusing to distribute a build whose content is not hosted" 16
    }
    Say "     OK  $((Select-String -Path $parityLog -Pattern 'R2_PARITY_OK' | Select-Object -First 1).Line.Trim())"
} else { Say "2b/4 R2 parity SKIPPED (no APK built)" }

# --- 3) FIREBASE APP DISTRIBUTION -------------------------------------------
if (-not $SkipFirebase) {
    if (-not (Test-Path $apkPath)) { Die "no APK to distribute" 14 }
    Say "3/4 Firebase App Distribution ..."
    $n = if ($Notes) { $Notes } else { "Build $((Get-Item $apkPath).LastWriteTime.ToString('yyyy-MM-dd HH:mm'))" }
    & powershell -ExecutionPolicy Bypass -File (Join-Path $proj 'distribute-android.ps1') -Notes $n
    if ($LASTEXITCODE -ne 0) { Die "firebase distribute failed (exit $LASTEXITCODE)" 15 }
    Say "     OK  release pushed to the testers group"
} else { Say "3/4 Firebase SKIPPED" }

# --- 4) WEBGL + VERCEL -------------------------------------------------------
if (-not $SkipWeb) {
    Say "4/4 WebGL player + Vercel preview ..."
    & powershell -ExecutionPolicy Bypass -File (Join-Path $proj 'build-webgl.ps1')
    if ((StampOf $webIdx) -le $webWas) {
        Die "WebGL not refreshed (still $webWas UTC). See Builds\webgl-build.log" 16
    }
    Say "     OK  Builds\WebGL refreshed - deploy with: .\overnight-webgl-deploy.ps1 (or vercel deploy)"
} else { Say "4/4 WebGL SKIPPED" }

Write-Host ""
Say "CHAIN COMPLETE."
Say "Artifacts:"
foreach ($p in @($exePath, $apkPath, $webIdx)) {
    if (Test-Path $p) { Say ("  {0,-46} {1}" -f (Resolve-Path $p -Relative), (Get-Item $p).LastWriteTime) }
}
exit 0
