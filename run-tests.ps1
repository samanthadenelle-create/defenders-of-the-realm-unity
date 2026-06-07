# =============================================================================
# run-tests.ps1 - run the Unity Test Framework suite headless (WO-306 Tier-2 gate).
#
# Tier 1 = CompileGate.Run (does it COMPILE -> COMPILE_GATE_OK).
# Tier 2 = THIS            (does it RUN / PASS -> TESTS_OK).
# Run both before push / end of day.
#
# Mirrors run-unity-method.ps1's fork-aware wait (Unity.exe relaunch quirk): ignore
# the wrapper exit code, poll until no Unity process remains, then judge from the
# NUnit results XML (test-run result="Passed", failed=0) - NOT the exit code.
#
# Editor must be CLOSED (single-Unity project lock).
# ASCII-only on purpose: Windows PowerShell 5.1 reads BOM-less files as ANSI, so
# smart-quotes / em-dashes corrupt and break the parse.
#
# Usage: powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 [-Platform EditMode|PlayMode]
# =============================================================================
param(
    [ValidateSet('EditMode','PlayMode')][string]$Platform = 'EditMode',
    [int]$TimeoutMin = 30
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
$log     = Join-Path $logDir "run-tests-$Platform.log"
$results = Join-Path $logDir "test-results-$Platform.xml"
if (Test-Path $log)     { Remove-Item $log -Force }
if (Test-Path $results) { Remove-Item $results -Force }

# -runTests drives the Test Runner and quits itself (no -quit).
$unityArgs = @('-batchmode', '-projectPath', $proj, '-runTests',
               '-testPlatform', $Platform, '-testResults', $results, '-logFile', $log)
Write-Host "[test] editor=$($chosen.Name)  platform=$Platform"
Write-Host "[test] results=$results"
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
if (Test-Path $lock) { try { Remove-Item $lock -Force -ErrorAction Stop; Write-Host "[test] removed stale Temp\UnityLockfile" } catch {} }

# --- judge from the NUnit results XML (authoritative, not the exit code) -------
$ok = $false; $total = 0; $pass = 0; $fail = 0
if (Test-Path $results) {
    try {
        [xml]$xml = Get-Content $results
        $run = $xml.'test-run'
        if ($run) {
            $total = [int]$run.total
            $pass  = [int]$run.passed
            $fail  = [int]$run.failed
            $ok    = ($run.result -eq 'Passed' -and $fail -eq 0 -and $total -gt 0)
        }
    } catch { Write-Host "[test] could not parse results XML: $($_.Exception.Message)" }
}
Write-Host "[test] wrapperExit=$wrapperExit timedOut=$timedOut total=$total passed=$pass failed=$fail"
Write-Host "[test] --- log tail (30) ---"
if (Test-Path $log) { Get-Content $log -Tail 30 }

if ($ok -and -not $timedOut) { Write-Host "[test] TESTS_OK :: $pass/$total passed"; exit 0 }
Write-Host "[test] TESTS_FAILED (or no results) - see log + $results"
exit 1
