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
# STANDING LANES (-Lane): a NAMED, one-command fleet run whose pass is judged by a
# marker the bot itself prints, not by "the fleet finished". A lane exists so a
# coverage question that must be answered EVERY night is one word on a command line
# instead of a paragraph somebody has to remember. See the $Lanes table below.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File .\run-autopilot-fleet.ps1 -Count 20
#   powershell -ExecutionPolicy Bypass -File .\run-autopilot-fleet.ps1 `
#       -Count 8 -SeedStart 100 -TimeoutMin 10
#   powershell -ExecutionPolicy Bypass -File .\run-autopilot-fleet.ps1 -Lane freshsave-ftue
# =============================================================================
param(
    [int]$Count = 8,
    [int]$SeedStart = 1,
    [string]$ExePath = 'Builds\Windows\DefendersOfTheRealm.exe',
    [int]$TimeoutMin = 8,
    [switch]$Graphics,  # render WITH a graphics device so the per-panel UI shots are not blank
    [string]$Phases = '',  # optional comma list; driver runs ONLY matching phases (substring, case-insensitive) - fast single-purpose capture runs
    [string]$Dungeon = '', # optional dungeon/portal id for the DungeonLoop phase
    [int]$Width = 0,    # -Graphics only; 0 => the capture default below
    [int]$Height = 0,
    [string]$Lane = ''  # named standing lane (see $Lanes); sets Phases + defaults and judges by the lane's own marker
)

$ErrorActionPreference = 'Stop'
$proj = $PSScriptRoot

# =============================================================================
# STANDING LANES
# -----------------------------------------------------------------------------
# WHY THIS TABLE EXISTS (WO-1500, 2026-09-07). Across ALL FIVE fleet logs captured
# on 2026-09-06 there were ZERO [Flow:Onboard*] lines: every run booted a RETURNING
# save, so every fresh-save assertion in the driver went N/A and the fleet reported
# green while asserting nothing about the first ten minutes. The only artefacts of
# minute one were PNGs from 2026-09-01. The FIX IS NOT A LONGER SWEEP - the phases
# existed; nothing ran them on a town that had just been founded. A lane is that
# missing thing: a named run, a fixed phase filter, and a MARKER the run must print.
#
# THE MARKER IS THE VERDICT, on a FRESH per-instance player.log. This repo's runners
# exit 0 on refusals and FAILs (CLAUDE.md sec.8), and the fleet's own completion
# check only proves the bot finished - a lane whose phase went N/A finishes perfectly.
# Marker absence is a FAILURE, never an unknown.
#
# Each lane sets Phases + the defaults its coverage needs; anything the CALLER passed
# explicitly still wins (PSBoundParameters), so a lane is a preset, not a cage.
$Lanes = @{
    'freshsave-ftue' = @{
        Phases     = 'FreshSaveFtue'
        Marker     = 'FRESH_SAVE_FTUE_OK'
        # ONE instance: the lane founds a New Game and reads process-scoped state
        # (TutorialFlow.RanThisSession), so N concurrent runs add no coverage.
        Count      = 1
        TimeoutMin = 6
        # -Graphics because the acceptance for this coverage is PNGs a human opens:
        # a -nographics run writes flat-black frames (UiCaptureCoverageRegression's
        # 2026-08-04 evidence), and a black FTUE shot is worse than none.
        Graphics   = $true
        Why        = 'the fresh-save FTUE: found a new town, walk the guide beats, prove the first welcome-back claims nothing'
    }
}

$LaneMarker = ''
if ($Lane -ne '') {
    $laneKey = $Lane.ToLowerInvariant()
    if (-not $Lanes.ContainsKey($laneKey)) {
        # Write-HOST, not Write-Error: $ErrorActionPreference is 'Stop' at the top of this
        # file, so Write-Error TERMINATES here and the `exit 5` below never runs - the caller
        # then sees exit 0 for a refusal, which is the exact failure shape CLAUDE.md section 8
        # warns about. Measured 2026-09-07 by running `-Lane nope`: exit was 0.
        Write-Host ("[fleet] FLEET_LANE_UNKNOWN '$Lane'. Known lanes: " + (($Lanes.Keys | Sort-Object) -join ', '))
        exit 5
    }
    $cfg = $Lanes[$laneKey]
    $LaneMarker = $cfg.Marker
    if (-not $PSBoundParameters.ContainsKey('Phases'))     { $Phases     = $cfg.Phases }
    if (-not $PSBoundParameters.ContainsKey('Count'))      { $Count      = $cfg.Count }
    if (-not $PSBoundParameters.ContainsKey('TimeoutMin')) { $TimeoutMin = $cfg.TimeoutMin }
    # Assigning $true to a [switch] leaves a plain bool behind, which every `if ($Graphics)`
    # below reads identically - but it has no .IsPresent, so never print that here.
    if ((-not $PSBoundParameters.ContainsKey('Graphics')) -and $cfg.Graphics) { $Graphics = $true }
    $laneGfx = $false
    if ($Graphics) { $laneGfx = $true }
    Write-Host "[fleet] LANE '$laneKey' - $($cfg.Why)"
    Write-Host "[fleet] LANE phases='$Phases' count=$Count timeoutMin=$TimeoutMin graphics=$laneGfx marker='$LaneMarker'"
}

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
# persistentDataPath = LocalLow\<companyName>\<productName>; productName became "Echoes of
# Elarion" on 2026-08-08. Prefer the new folder, fall back to the legacy one (a fleet run by an
# older player still needs wiping/archiving, and archiving the WRONG folder loses F8 captures).
$pdp = Join-Path $env:USERPROFILE 'AppData\LocalLow\DeNelle\Echoes of Elarion'
$pdpLegacy = Join-Path $env:USERPROFILE 'AppData\LocalLow\DeNelle\Defenders of the Realm'
if ((-not (Test-Path $pdp)) -and (Test-Path $pdpLegacy)) { $pdp = $pdpLegacy }
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

# --- capture runs only: clear the stale UI_REVIEW shots ------------------------
# Same reasoning as the run-log wipe above, applied to the review artefacts. If a
# panel fails to open on THIS run, the reviewer must see MISSING (which gets
# chased) rather than last week's shot or a leftover blank (which gets reviewed
# and believed). Only a -Graphics run does this: a logic/flow fleet writes no
# shots at all now, so it must never destroy the good ones either.
if ($Graphics) {
    $shotsDir = Join-Path $pdp 'ui-shots'
    if (Test-Path $shotsDir) {
        $stale = @(Get-ChildItem -Path $shotsDir -Filter 'panel_*.png' -File -ErrorAction SilentlyContinue)
        if ($stale.Count -gt 0) {
            $blank = @($stale | Where-Object { $_.Length -lt 40000 })
            $archive = Join-Path $shotsDir '_stale'
            New-Item -ItemType Directory -Force -Path $archive | Out-Null
            foreach ($f in $stale) {
                # A flat frame is evidence of nothing - delete it. Anything with real
                # pixels is somebody's evidence, so park it rather than destroy it.
                if ($f.Length -lt 40000) {
                    Remove-Item $f.FullName -Force -ErrorAction SilentlyContinue
                } else {
                    Move-Item $f.FullName (Join-Path $archive $f.Name) -Force -ErrorAction SilentlyContinue
                }
            }
            Write-Host ("[fleet] cleared {0} stale panel_*.png ({1} blank deleted, {2} real parked under _stale\)." -f $stale.Count, $blank.Count, ($stale.Count - $blank.Count))
        }
    }
}

# --- launch N headless instances ---------------------------------------------
$procs = @()
for ($i = 0; $i -lt $Count; $i++) {
    $seed = $SeedStart + $i
    # -Graphics => WINDOWED real-rendering run (no -batchmode/-nographics) so ScreenCapture
    # writes real frames (batchmode has no backbuffer => black). Default => headless batch.
    $args = if ($Graphics) { @() } else { @('-batchmode', '-nographics') }
    # Capture runs default to 2670x1200 - the Seeker's REAL surface, corrected 2026-08-05.
    # This comment used to call 2340x1080 "the Seeker's EXACT screen"; it is NOT - it is the
    # old harness size, and the two differ in BOTH scale and aspect (2.225 vs 2.167). This
    # project's recurring UI defect is fraction-anchored bands that only cull/overlap at the
    # device geometry, so a shot at the wrong size can pass a review the device would fail -
    # which is exactly how two panels shipped broken behind a green marker tonight.
    # Override with -Width/-Height to reproduce a legacy size.
    $w = if ($Graphics) { if ($Width  -gt 0) { "$Width" }  else { '2670' } } else { '800' }
    $h = if ($Graphics) { if ($Height -gt 0) { "$Height" } else { '1200' } } else { '600' }
    $args += @('-screen-width', $w, '-screen-height', $h, '--autopilot', "--run=$i", "--seed=$seed")
    if ($Phases -ne '') { $args += "--phases=$Phases" }
    if ($Dungeon -ne '') { $args += "--dungeon=$Dungeon" }
    # PER-INSTANCE -logFile (WO-1102): with no -logFile every instance targets the ONE default
    # LocalLow Player.log; N>1 contend and Step-level FlowTrace evidence is destroyed (proven
    # 2026-08-16: root Player.log mtime never moved even for -Count 1 mid-diagnosis). Redirect
    # each instance's full log NEXT TO its break-log, using the SAME <i> namespacing the
    # BreakCaptureHarness uses for --run=<i> (persistentDataPath\autopilot-runs\<i>). The
    # harness creates that folder at startup, but -logFile is consumed by the player BEFORE
    # the harness runs - so create the folder here first or Unity may drop the redirect.
    $runDir = Join-Path $runsDir "$i"
    New-Item -ItemType Directory -Force -Path $runDir | Out-Null
    # Explicit quotes: PS 5.1 Start-Process joins -ArgumentList with spaces WITHOUT quoting,
    # and this path contains spaces ("Echoes of Elarion").
    $args += @('-logFile', ('"' + (Join-Path $runDir 'player.log') + '"'))
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

# --- assert every instance actually FINISHED A RUN (WO-1102 + WO-1496) ---------
# WHAT THIS USED TO DO, AND WHY IT WAS NOT A GATE (WO-1496, 2026-09-06): it asserted
# the per-instance player.log EXISTED and was non-empty, then WARNED. A file's
# existence proves the process started and wrote one byte - it proves nothing about
# whether the bot drove the game. An instance that crashed on the first frame leaves
# a fat player.log full of the crash, and this loop called that OK. Then the script
# exited on the EMITTER's exit code, which this repo's runners return 0 for on
# refusals and FAILs alike (CLAUDE.md sec.8; memory gates-report-success-without-
# proving-it). So the fleet could not fail.
#
# It now judges by the MARKER the bot itself emits on a run it completed:
#   [Flow:Auto] AutoPilot complete            (AutoPilotDriver.cs:453, FlowTrace.Step)
# AND by that run's own summary saying the run was not aborted:
#   "aborted": false                          (AutoPilotDriver.WriteSummary -> RunSummary)
# BOTH are required, and the second is the load-bearing half: a run that trips the
# global cap or a critical phase sets _abortRun and STILL falls through to the
# "AutoPilot complete" line (RunPhase yield-breaks each remaining phase rather than
# ending the coroutine), so the marker ALONE would pass an aborted run.
$plMissing = 0
$markerMissing = 0
$abortedRuns = 0
$laneMissing = 0
for ($i = 0; $i -lt $Count; $i++) {
    $runDir = Join-Path $runsDir "$i"
    $pl = Join-Path $runDir 'player.log'
    $ok = $false
    if (Test-Path $pl) {
        $item = Get-Item $pl -ErrorAction SilentlyContinue
        if ($item -and $item.Length -gt 0) { $ok = $true }
    }
    if (-not $ok) {
        Write-Host "FLEET_PLAYERLOG_MISSING run=$i (expected non-empty '$pl' - Step-level trace lost for this instance)"
        $plMissing++
        $markerMissing++
        continue
    }

    # -Quiet + -SimpleMatch: the needle carries regex metacharacters ([ ]) and we want
    # a literal match, not a character class. FlowTrace.Step pads the message, so the
    # tag and the text are matched as two separate literals rather than one phrase.
    $hasTag = Select-String -Path $pl -Pattern '[Flow:Auto]' -SimpleMatch -Quiet -ErrorAction SilentlyContinue
    $hasDone = Select-String -Path $pl -Pattern 'AutoPilot complete' -SimpleMatch -Quiet -ErrorAction SilentlyContinue
    if (-not ($hasTag -and $hasDone)) {
        Write-Host "FLEET_MARKER_MISSING run=$i (no '[Flow:Auto] ... AutoPilot complete' in '$pl' - this instance never finished a run; a present log is not a finished run)"
        $markerMissing++
        continue
    }

    $sum = Join-Path $runDir 'autopilot-summary.json'
    if (-not (Test-Path $sum)) {
        Write-Host "FLEET_MARKER_MISSING run=$i (no '$sum' - the driver never wrote its summary, so 'complete' cannot be confirmed unaborted)"
        $markerMissing++
        continue
    }
    # FAIL CLOSED: require the POSITIVE proof '"aborted": false' (regex, so pretty-print
    # spacing cannot decide the verdict). Testing for '"aborted": true' instead would pass
    # every run whose spacing, casing or schema drifted - a gate that reports success
    # without proving it, which is the defect this whole WO is about.
    if (-not (Select-String -Path $sum -Pattern '"aborted"\s*:\s*false' -Quiet -ErrorAction SilentlyContinue)) {
        Write-Host "FLEET_RUN_ABORTED run=$i (the aborted-false field is absent from '$sum' - the run ended early (global cap / critical phase), or the summary schema changed; either way this instance's coverage is NOT proven)"
        $abortedRuns++
    }

    # --- THE LANE'S OWN VERDICT (WO-1500) -------------------------------------
    # 'AutoPilot complete' + aborted=false prove the BOT ran. They cannot prove the
    # lane's PHASE asserted anything: a phase that returns N/A (wrong scene, flag off,
    # already-Onboarded save) yields a perfectly complete, unaborted run - which is
    # exactly how five 2026-09-06 logs reported green with zero FTUE coverage in them.
    # The lane's marker is printed only on the phase's success path, so it is the one
    # line that separates "the fleet ran" from "the question got answered".
    if ($LaneMarker -ne '') {
        if (Select-String -Path $pl -Pattern $LaneMarker -SimpleMatch -Quiet -ErrorAction SilentlyContinue) {
            Write-Host "[fleet] FLEET_LANE_MARKER run=$i '$LaneMarker' present."
        } else {
            Write-Host "FLEET_LANE_MISSING run=$i (no '$LaneMarker' in '$pl' - the lane's phase did not reach its success path. Read the [Flow:Auto] lines in that log: a FAIL names the dead link, and an N/A line names the precondition that was not met. Marker absence on a fresh log is a FAILURE, not an unknown.)"
            $laneMissing++
        }
    }
}
if ($plMissing -eq 0) {
    Write-Host "[fleet] FLEET_PLAYERLOG_OK $Count/$Count per-instance player.log present and non-empty."
} else {
    Write-Host "[fleet] WARNING: $plMissing/$Count instance(s) missing a usable player.log (see FLEET_PLAYERLOG_MISSING lines above)."
}
$fleetExit = 0
if (($markerMissing -eq 0) -and ($abortedRuns -eq 0)) {
    Write-Host "[fleet] FLEET_RUNS_OK $Count/$Count instance(s) emitted 'AutoPilot complete' with aborted=false."
} else {
    Write-Host "[fleet] FLEET_RUNS_FAIL $markerMissing/$Count without a completion marker, $abortedRuns/$Count not proven unaborted - the fleet's coverage is NOT what the count says."
    # DEFERRED, NOT IMMEDIATE: the refusal is recorded here and applied after the
    # aggregation below. Exiting at this line would suppress the ranked ticket list,
    # which is precisely the evidence that explains WHY an instance never completed.
    $fleetExit = 3
}
if ($LaneMarker -ne '') {
    if ($laneMissing -eq 0) {
        Write-Host "[fleet] FLEET_LANE_OK $Count/$Count instance(s) printed '$LaneMarker' - the lane's question was actually answered."
    } else {
        Write-Host "[fleet] FLEET_LANE_FAIL $laneMissing/$Count instance(s) never printed '$LaneMarker' - the lane RAN but did not ASSERT. Do not report this lane as covered."
        # Deferred like $fleetExit above, and for the same reason: the ranked tickets
        # below carry the FlowTrace.Fail that explains which link died. Exit 5 is the
        # lane's own code so a caller can tell a lane miss from an aborted instance.
        if ($fleetExit -eq 0) { $fleetExit = 5 }
    }
}

# --- aggregate every run's breaks into one ranked ticket list -----------------
# Reuses the existing editor emitter; it now scans persistentDataPath/autopilot-
# runs/*/break-log.jsonl (one folder per --run) plus the root, dedupes, and ranks
# by distinct-run reproduction count.
Write-Host "[fleet] aggregating -> AutoPilotTickets.Emit (this opens the editor in batchmode)"
$runner = Join-Path $proj 'run-unity-method.ps1'
# Stamped BEFORE the call: the marker must be judged on a FRESH log. A stale
# autopilot-fleet-tickets.log from the previous fleet carries a perfectly good
# AUTOPILOT_TICKETS_OK and reads exactly like a pass (SUNDAY_HOUSEKEEPING sec.3 rule 3).
$emitStamp = Get-Date
& powershell -ExecutionPolicy Bypass -File $runner `
    -Method 'DeNelle.Editor.AutoPilotTickets.Emit' `
    -LogName 'autopilot-fleet-tickets.log'
$emitExit = $LASTEXITCODE

$ticketsMd = Join-Path $proj 'Builds\autopilot-tickets.md'
$ticketsJson = Join-Path $proj 'Builds\autopilot-tickets.json'
$emitLog = Join-Path $proj 'Builds\autopilot-fleet-tickets.log'
Write-Host "[fleet] emitter exit = $emitExit (NOT the verdict - this runner exits 0 on refusals and FAILs)"
if (Test-Path $ticketsMd) {
    Write-Host "[fleet] ranked tickets -> $ticketsMd"
    Write-Host "[fleet]                  $ticketsJson"
} else {
    Write-Host "[fleet] WARNING: no ticket file produced - see $emitLog"
}

# --- the emitter's verdict is its MARKER on a FRESH log, never its exit code ---
$emitOk = $false
if (Test-Path $emitLog) {
    $emitItem = Get-Item $emitLog -ErrorAction SilentlyContinue
    if ($emitItem -and ($emitItem.LastWriteTime -ge $emitStamp)) {
        if (Select-String -Path $emitLog -Pattern 'AUTOPILOT_TICKETS_OK' -SimpleMatch -Quiet -ErrorAction SilentlyContinue) {
            $emitOk = $true
        } else {
            Write-Host "[fleet] FLEET_EMIT_FAIL no AUTOPILOT_TICKETS_OK in a fresh '$emitLog' - marker absence on a fresh log is a FAILURE, not an unknown."
        }
    } else {
        Write-Host "[fleet] FLEET_EMIT_FAIL '$emitLog' is STALE (not written by this run) - the emitter produced nothing this fleet."
    }
} else {
    Write-Host "[fleet] FLEET_EMIT_FAIL no '$emitLog' at all - the emitter never ran."
}
if (-not $emitOk) {
    Write-Host "[fleet] REFUSING (exit 4). The aggregation is the fleet's only output; unproven, the run has none."
    exit 4
}
Write-Host "[fleet] FLEET_EMIT_OK AUTOPILOT_TICKETS_OK on a fresh emitter log."
if ($fleetExit -ne 0) {
    Write-Host "[fleet] REFUSING (exit $fleetExit). The aggregation above is real, but the instances behind it are not all proven - read the FLEET_MARKER_MISSING / FLEET_RUN_ABORTED lines before trusting any count in it."
    exit $fleetExit
}
exit 0
