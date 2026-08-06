# =============================================================================
# run-unity-method.ps1 - invoke a single Unity Editor static method in batchmode
# and wait for the REAL editor to finish.
#
# Unity.exe forks/relaunches on launch (see memory: unity-batchmode-relaunch-
# quirk), so the process this script starts can return early with a blank exit
# code while the actual editor keeps working. We therefore ignore the wrapper
# exit code and poll until no 'Unity' process remains, then judge success from
# the log (compile errors / exceptions / "Aborting batchmode").
#
# ASCII-only on purpose: Windows PowerShell 5.1 reads BOM-less files as ANSI,
# so smart-quotes / em-dashes corrupt and break the parse.
#
# Usage: powershell -ExecutionPolicy Bypass -File .\run-unity-method.ps1 `
#            -Method DeNelle.Editor.TripoAssetPostprocessor.ForceReextractAll `
#            -LogName tripo-extract.log
# =============================================================================
param(
    [Parameter(Mandatory=$true)][string]$Method,
    [Parameter(Mandatory=$true)][string]$LogName,
    [int]$TimeoutMin = 30,
    # Force the ACTIVE BUILD TARGET for this run (e.g. Win64, Android, WebGL).
    # WHY THIS EXISTS (2026-08-05): an APK build leaves the project's active target on
    # Android. A later DESKTOP build then dies with "Native extension for Android target
    # not found" and an SBP/Addressables failure - and because the wrapper judges by log
    # text rather than a marker, it reads as a generic failure rather than a target
    # mismatch. Pass -BuildTarget Win64 after any Android build. Omitted => whatever the
    # project's active target happens to be, i.e. today's behaviour, unchanged.
    [string]$BuildTarget = ''
)

$ErrorActionPreference = 'Stop'
$proj       = $PSScriptRoot
$hubEditors = 'C:\Program Files\Unity\Hub\Editor'
$pinned     = '6000.4.8f1'

# --- locate editor ------------------------------------------------------------
$candidates = Get-ChildItem $hubEditors -Directory -ErrorAction SilentlyContinue |
    Where-Object { Test-Path (Join-Path $_.FullName 'Editor\Unity.exe') }
if (-not $candidates) { Write-Error "No Unity editor under '$hubEditors'."; exit 2 }
$chosen = $candidates | Where-Object { $_.Name -eq $pinned } | Select-Object -First 1
if (-not $chosen) { $chosen = $candidates | Where-Object { $_.Name -like '6000.*' } | Sort-Object Name -Descending | Select-Object -First 1 }
if (-not $chosen) { $chosen = $candidates | Sort-Object Name -Descending | Select-Object -First 1 }
$unity = Join-Path $chosen.FullName 'Editor\Unity.exe'

# --- refuse if an editor is already open (project lock) -----------------------
if (Get-Process -Name 'Unity' -ErrorAction SilentlyContinue) {
    Write-Error "A 'Unity' editor process is already running - close it before batchmode (project lock)."
    exit 3
}

$logDir = Join-Path $proj 'Builds'
New-Item -ItemType Directory -Path $logDir -Force | Out-Null
$log = Join-Path $logDir $LogName
# Tolerate the check-then-delete race (2026-07-13 fleet aggregation: Test-Path passed,
# the file vanished before Remove-Item ran, and the emitter aborted with exit 1).
if (Test-Path $log) { Remove-Item $log -Force -ErrorAction SilentlyContinue }

$unityArgs = @('-batchmode', '-quit', '-projectPath', $proj, '-executeMethod', $Method, '-logFile', $log)
if ($BuildTarget -ne '') { $unityArgs = @('-buildTarget', $BuildTarget) + $unityArgs }
Write-Host "[run] editor=$($chosen.Name)  method=$Method  buildTarget=$(if ($BuildTarget -ne '') { $BuildTarget } else { '(project active)' })"
Write-Host "[run] log=$log"
& $unity @unityArgs | Out-Null
$wrapperExit = $LASTEXITCODE

# --- wait for the real (possibly relaunched) editor to finish -----------------
$deadline = (Get-Date).AddMinutes($TimeoutMin)
Start-Sleep -Seconds 6
while ((Get-Date) -lt $deadline) {
    if (-not (Get-Process -Name 'Unity' -ErrorAction SilentlyContinue)) {
        Start-Sleep -Seconds 4   # settle: catch a relaunch spawning a new PID
        if (-not (Get-Process -Name 'Unity' -ErrorAction SilentlyContinue)) { break }
    }
    Start-Sleep -Seconds 10
}
$timedOut = (Get-Date) -ge $deadline

# Clear the stale editor lockfile so the next batchmode launch is not blocked.
$lock = Join-Path $proj 'Temp\UnityLockfile'
if (Test-Path $lock) { try { Remove-Item $lock -Force -ErrorAction Stop; Write-Host "[run] removed stale Temp\UnityLockfile" } catch { Write-Host "[run] could not remove lockfile: $($_.Exception.Message)" } }

# --- judge success from the log ----------------------------------------------
$succeeded = $false; $compileErr = $false; $license = $false
if (Test-Path $log) {
    $succeeded  = [bool](Select-String -Path $log -Pattern 'Exiting batchmode successfully|terminate with return code 0' -Quiet -ErrorAction SilentlyContinue)
    $compileErr = [bool](Select-String -Path $log -Pattern 'error CS\d+' -Quiet -ErrorAction SilentlyContinue)
    $license    = [bool](Select-String -Path $log -Pattern 'HandshakeResponse reported an error|No valid Unity Editor license|ResponseCode: 505|Unsupported protocol version' -Quiet -ErrorAction SilentlyContinue)
}
Write-Host "[run] wrapperExit=$wrapperExit timedOut=$timedOut succeeded=$succeeded license=$license compileErrors=$compileErr"
Write-Host "[run] --- log tail (45) ---"
if (Test-Path $log) { Get-Content $log -Tail 45 }

# A transient 505 license-handshake line can appear even on a fully successful
# batchmode run, so the explicit success marker is authoritative. Only treat the
# license error as fatal when the run did NOT reach a clean exit.
if ($succeeded -and -not $compileErr) { exit 0 }
if ($license) { Write-Host "[run] *** LICENSE ERROR (run did not complete) - needs interactive Hub refresh or reboot; do NOT kill processes. ***"; exit 7 }
exit 1
