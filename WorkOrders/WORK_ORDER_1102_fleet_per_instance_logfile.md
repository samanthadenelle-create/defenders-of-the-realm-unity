# WORK ORDER 1102 - Fleet instances discard Step-level stdout (no per-instance -logFile)

**Status:** CLOSED 2026-08-27 — owner felt-tested PASS on APK 2026.08.27.343739.
**Minted:** 2026-08-16 (orchestrator; banner bumped 1102 -> 1103 in the same edit)
**Silo:** QA harness / autopilot fleet
**Lane:** tools + DevTools only. Disjoint from Hero/Equipment, HUD, VFX lanes.

## Provenance - proven live, 2026-08-16

During the WO-994 diagnosis, a 2-instance `run-autopilot-fleet.ps1 -Phases DungeonLoop` run
executed TWO full dungeon->town ports against a build carrying Step-level FlowTrace probes
(`WO-994 registryProbe/reapplyCtx/seatWrite/shieldPose`). ZERO of those lines reached disk:

- `run-autopilot-fleet.ps1:127` - `Start-Process -FilePath $ExePath -ArgumentList $args` with
  **no `-logFile` argument**, so every instance targets the default
  `%USERPROFILE%\AppData\LocalLow\DeNelle\Echoes of Elarion\Player.log`.
- With N>1 the instances contend; after the run the root `Player.log` mtime had NOT moved
  (07:13 before vs 07:35 fleet end) - the trace was simply lost.
- `break-log.jsonl` (per-run, namespaced, reliable) captures ERROR-LEVEL ONLY by design -
  `FlowTrace.Step`/`Warn` never land there. So for any diagnosis riding Step-level probes the
  fleet currently produces evidence for its own phases but destroys the system-under-test's trace.
- The known workaround (used today) is `-Count 1`, which forfeits the fleet's parallelism.

## Fix

1. In `run-autopilot-fleet.ps1`, pass `-logFile "<runLogsDir>\<i>\player.log"` per instance
   (same `<i>` namespacing as `--run=<i>`; the run dir already exists for break-log/summary).
2. Teach `harvest.sh` + `AutoPilotTickets.Emit` to find the per-instance player.log next to the
   break-log (harvest may only need a mention; Emit reads break-log/summary and can stay as-is,
   but must not break on the new file's presence).
3. Update `.claude/skills/run-defenders/SKILL.md`'s known-gotchas note ("Player.log is
   overwritten per fleet instance -> unreliable for fleets") to describe the new per-instance
   location - the gotcha becomes historical.
4. Regression: extend the harness self-checks (or add a small script assertion) that after a
   `-Count 2` fleet, BOTH `autopilot-runs\0\player.log` and `...\1\player.log` exist and are
   non-empty. Without this the flag can silently regress.

## What NOT to touch

- Do not change FlowTrace log levels to smuggle Step lines into break-log - the level mapping
  is load-bearing (INSTRUMENTATION_STANDARD sec 5).
- Do not change the F8 data-loss guard, the stale-run wipe, or the `-Graphics` panel-shot logic.

## Acceptance

- `-Count 2 -Phases DungeonLoop` produces two per-instance player.log files each containing the
  session's `[Flow:*]` Step lines; root Player.log contention is gone; existing markers
  (`AUTOPILOT_TICKETS_OK`, summaries) unchanged.
