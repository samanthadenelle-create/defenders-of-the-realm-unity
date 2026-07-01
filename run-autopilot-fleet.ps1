# =============================================================================
# run-autopilot-fleet.ps1 - launch a FLEET of headless AutoPilot player instances
# in parallel, then aggregate their breaks into one ranked ticket list.
#
# WHY A FLEET: a player build (the .exe) needs NO Unity license, so dozens of
# instances can run concurrently on one machine. Each gets a distinct --seed (to
# explore different paths) and a --run=<i> (to namespace its output so they don't
# clobber a shared break-log). After all exit, the existing editor-side emitter
# (AutoPilotTickets.Emit) scans every run's break-log, dedupes, and RANKS each
# ticket by how many DISTINCT runs reproduced it.
#
# COVERAGE NOTE: -nographics means NO rendering -> UI Toolkit picking + visuals
# won't resolve headless. This is logic / flow / crash coverage only, by design.
#
# ASCII-only on purpose: Windows PowerShell 5.1 reads BOM-less files as ANSI, so
# smart-quotes / em-dashes corrupt the parse. PS 5.1 compatible (no '&&', no
# ternary). Mirrors the style of build-windows.ps1 / run-unity-method.ps1.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File .\run-autopilot-fleet.ps1 -Count 20
#   powershell -ExecutionPolicy Bypass -File .\run-autopilot-fleet.ps1 `
#       -Count 8 -SeedStart 100 -TimeoutMin 10
# =============================================================================
param(
    [int]$Count = 8,
    [int]$SeedStart = 1,
    [string]$ExePath = 'Builds\Windows\DefendersOfTheRealm.exe',
    [int]$TimeoutMin = 8,
    [switch]$Graphics   # render WITH a graphics device so the per-panel UI shots are not blank
)

$ErrorActionPreference = 'Stop'
$proj = $PSScriptRoot

# --- resolve + verify the player exe -----------------------------------------
if (-not [System.IO.Path]::IsPathRooted($ExePath)) { $ExePath = Join-Path $proj $ExePath }
if (-not (Test-Path $ExePath)) {
    Write-Error "Player exe not found at '$ExePath'. Build it first (build-windows.ps1) or pass -ExePath."
    exit 2
}
Write-Host "[fleet] exe   = $ExePath"
Write-Host "[fleet] count = $Count   seedStart = $SeedStart   timeoutMin = $TimeoutMin"
Write-Host "[fleet] (player builds need no license; -nographics = logic/flow/crash coverage only)"

# --- clean stale run logs so the aggregation reflects ONLY this fleet ----------
# The BreakCaptureHarness APPENDS to each run's break-log.jsonl (it does not
# truncate at run start), and old --run=<i> folders from prior fleets/sessions
# persist. Without this wipe the editor-side emitter re-reads pre-fix history and
# re-reports ALREADY-FIXED issues every fleet forever -> corrupted truth/coverage
# metrics (a fixed bug never appears "resolved"). Wipe before launching so each
# fleet's ranked tickets reflect ONLY this fleet's fresh runs.
$pdp = Join-Path $env:USERPROFILE 'AppData\LocalLow\DeNelle\Defenders of the Realm'
$runsDir = Join-Path $pdp 'autopilot-runs'
$rootBreak = Join-Path $pdp 'break-log.jsonl'

# DATA-LOSS GUARD (2026-06-20): the wipe below destroyed the owner's MANUAL F8 captures
# (kind:"flagged" notes + flag_*.png) when a fleet ran while she had pending tickets. NEVER
# delete a manual capture. If the root break-log has any flagged (F8) entries, ARCHIVE the
# break-log + flag screenshots into QA_F8_ARCHIVE before wiping, so a fleet can never lose them.
if ((Test-Path $rootBreak) -and (Select-String -Path $rootBreak -Pattern '"kind":"flagged"' -Quiet -ErrorAction SilentlyContinue)) {
    $stamp = (Get-Date -Format 'yyyy-MM-dd_HHmmss')
    $arch  = Join-Path $PSScriptRoot ("QA_F8_ARCHIVE\fleet-preserved_" + $stamp)
    New-Item -ItemType Directory -Force -Path $arch | Out-Null
    Copy-Item $rootBreak (Join-Path $arch 'break-log.jsonl') -Force -ErrorAction SilentlyContinue
    Get-ChildItem -Path $pdp -Filter 'flag_*.png' -ErrorAction SilentlyContinue | Copy-Item -Destination $arch -Force -ErrorAction SilentlyContinue
    Write-Host "[fleet] ARCHIVED manual F8 captures -> $arch (never wipe a ticket)."
}

if (Test-Path $runsDir) { Remove-Item $runsDir -Recurse -Force -ErrorAction SilentlyContinue }
if (Test-Path $rootBreak) { Remove-Item $rootBreak -Force -ErrorAction SilentlyContinue }
Write-Host "[fleet] cleaned stale run logs under '$pdp' (fresh aggregation slate)."

# --- launch N headless instances ---------------------------------------------
$procs = @()
for ($i = 0; $i -lt $Count; $i++) {
    $seed = $SeedStart + $i
    # -Graphics => WINDOWED real-rendering run (no -batchmode/-nographics) so ScreenCapture
    # writes real frames (batchmode has no backbuffer => black). Default => headless batch.
    $args = if ($Graphics) { @() } else { @('-batchmode', '-nographics') }
    $w = if ($Graphics) { '1280' } else { '800' }
    $h = if ($Graphics) { '720' }  else { '600' }
    $args += @('-screen-width', $w, '-screen-height', $h, '--autopilot', "--run=$i", "--seed=$seed")
    $p = Start-Process -FilePath $ExePath -ArgumentList $args -PassThru
    $procs += $p
    Write-Host "[fleet] launched run=$i seed=$seed pid=$($p.Id)"
}

# --- wait for all to exit (or kill any still alive past the timeout) ----------
$deadline = (Get-Date).AddMinutes($TimeoutMin)
while ($true) {
    $alive = @()
    foreach ($p in $procs) {
        try {
            $live = Get-Process -Id $p.Id -ErrorAction SilentlyContinue
            if ($live) { $alive += $p }
        } catch { }
    }
    if ($alive.Count -eq 0) { Write-Host "[fleet] all instances exited."; break }
    if ((Get-Date) -ge $deadline) {
        Write-Host "[fleet] TIMEOUT after $TimeoutMin min - killing $($alive.Count) straggler(s)."
        foreach ($p in $alive) {
            try { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue; Write-Host "[fleet] killed pid=$($p.Id)" } catch { }
        }
        break
    }
    Start-Sleep -Seconds 5
}

# --- aggregate every run's breaks into one ranked ticket list -----------------
# Reuses the existing editor emitter; it now scans persistentDataPath/autopilot-
# runs/*/break-log.jsonl (one folder per --run) plus the root, dedupes, and ranks
# by distinct-run reproduction count.
Write-Host "[fleet] aggregating -> AutoPilotTickets.Emit (this opens the editor in batchmode)"
$runner = Join-Path $proj 'run-unity-method.ps1'
& powershell -ExecutionPolicy Bypass -File $runner `
    -Method 'DeNelle.Editor.AutoPilotTickets.Emit' `
    -LogName 'autopilot-fleet-tickets.log'
$emitExit = $LASTEXITCODE

$ticketsMd = Join-Path $proj 'Builds\autopilot-tickets.md'
$ticketsJson = Join-Path $proj 'Builds\autopilot-tickets.json'
Write-Host "[fleet] emitter exit = $emitExit"
if (Test-Path $ticketsMd) {
    Write-Host "[fleet] ranked tickets -> $ticketsMd"
    Write-Host "[fleet]                  $ticketsJson"
} else {
    Write-Host "[fleet] WARNING: no ticket file produced - see Builds\autopilot-fleet-tickets.log"
}
exit $emitExit
