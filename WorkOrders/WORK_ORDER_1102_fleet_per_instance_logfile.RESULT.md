# WORK ORDER 1102 - RESULT

**Date:** 2026-08-16  **Seat:** edit-only implementation agent (ps1/sh/docs lane; no .cs, no Unity, no commit - CLI reconciles + commits)
**Status:** IMPLEMENTED - pending committer gate (syntax-verified: PS 5.1 parser clean, `bash -n` clean; a live `-Count 2` fleet is the committer/PO proof)

## What was built

### 1. `run-autopilot-fleet.ps1` - per-instance `-logFile` (the fix itself)

In the launch loop (before `Start-Process`), each instance now gets:

- `$runDir = Join-Path $runsDir "$i"` - the SAME `<i>` namespacing the
  `BreakCaptureHarness` uses for `--run=<i>` (`persistentDataPath\autopilot-runs\<i>`,
  verified against `BreakCaptureHarness.ResolveOutDir`), so `player.log` lands NEXT TO
  that run's `break-log.jsonl`.
- `New-Item -ItemType Directory -Force $runDir` - the harness creates the folder at
  startup, but `-logFile` is consumed by the player BEFORE the harness runs, so the
  script creates it first. (The stale-run wipe happens earlier in the script, so these
  are fresh dirs.)
- `-logFile "<runDir>\player.log"` appended to the arg array, with EXPLICIT quotes
  around the path: PS 5.1 `Start-Process -ArgumentList` joins elements without quoting
  and the path contains spaces ("Echoes of Elarion").

### 2. `run-autopilot-fleet.ps1` - named post-run assertion

After the wait/kill loop, before aggregation: for each run `0..Count-1`, if
`autopilot-runs\<i>\player.log` is missing OR zero bytes, print
`FLEET_PLAYERLOG_MISSING run=<i>` plus a summary warning; on full success print
`FLEET_PLAYERLOG_OK <n>/<n>`. Warn-only - the exit code still chains from
`AutoPilotTickets.Emit` exactly as before.

### 3. `.claude/skills/run-defenders/harvest.sh`

- Header comment corrected: Step/Warn now land in `autopilot-runs/<i>/player.log`
  (WO-1102), not the unreliable shared root Player.log.
- New section `=== per-instance player.log (Step-level FlowTrace, WO-1102) ===` lists
  each `$RUNDIR/*/player.log` with byte size + `[Flow:*]` line count, and says so
  explicitly when none exist (pre-WO-1102 fleet).

### 4. `.claude/skills/run-defenders/SKILL.md`

The known-gotcha ("Player.log is overwritten per fleet instance -> unreliable for
fleets") rewritten as of 2026-08-16: Step lines now land per instance at
`autopilot-runs/<i>/player.log`, the fleet prints `FLEET_PLAYERLOG_MISSING run=<i>`
on loss, and the old warning is kept as an explicit *(History: ...)* note.

## Preserved (WO "What NOT to touch")

- F8 data-loss guard (archive-before-wipe), stale-run wipe, `-Graphics` panel-shot
  clearing and windowed/size logic, `--phases` pass-through, ticket-emit chaining and
  its exit code: all untouched.
- No FlowTrace level mapping changes; `AutoPilotTickets.Emit` reads break-log/summary
  and is unaffected by the extra file in each run folder (no .cs touched).

## How the reviewer proves it

1. `powershell -ExecutionPolicy Bypass -File .\run-autopilot-fleet.ps1 -Count 2 -TimeoutMin 4 -Phases DungeonLoop`
2. See `FLEET_PLAYERLOG_OK 2/2` in the fleet output (or a named
   `FLEET_PLAYERLOG_MISSING run=<i>` line - never silence).
3. Open `%USERPROFILE%\AppData\LocalLow\DeNelle\Echoes of Elarion\autopilot-runs\0\player.log`
   and `...\1\player.log` - both non-empty, each containing that session's `[Flow:*]`
   Step lines; the root `Player.log` mtime no longer moves during a fleet.
4. `bash .claude/skills/run-defenders/harvest.sh` - the new per-instance section lists
   both files with their `[Flow:*]` counts.

## Verification done by this seat (no Unity, per lane)

- PS 5.1 tokenizer parse of the edited `run-autopilot-fleet.ps1`: clean (0 errors).
- `bash -n harvest.sh`: clean.
- ASCII-only additions to the .ps1 (PS 5.1 ANSI-read guard respected).
- Harness namespacing cross-checked at source
  (`Assets/_Modules/Core/Diagnostics/BreakCaptureHarness.cs` `ResolveOutDir`).
