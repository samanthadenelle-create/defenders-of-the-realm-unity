**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK_ORDER_472 — Bot/Dev instant-spawn capability (reach deep systems headless)

**Status: PART A SHIPPED · PART B READY TO IMPLEMENT** · Owner request 2026-06-21.

## Problem
Headless/dev autopilot bots can't reach deep systems (Village2 garrison, `Garrison_*` outposts,
ATB combat, the south-OuterWorld→garrison seam) within their time budget — the chaotic walk burns
the whole run traversing. So real bugs in those systems (e.g. ticket #2 bare-pill garrison-arrival,
#4 Village2 magenta) never get exercised or captured. We need a **bot/dev-only instant-spawn** so a
run lands directly in the system under test. **Must NEVER exist in a release player build.**

## Part A — `--scene=<name>` boot override  ✅ SHIPPED (commit d28491c0)
- `AutoPilotInstaller` parses `--scene=Village2` (or `AUTOPILOT_SCENE` env); threads it through
  `AutoPilotDriver.Begin(..., startScene)`; `BootToGameplay` `LoadScene`s that target instead of
  `MainCastle_Hall`. Target must be in Build Settings to load by name (throws + logged otherwise).
- Gated dev/headless only: the whole `DeNelle.DevTools` autopilot path is `DEVELOPMENT_BUILD || UNITY_EDITOR`.
- Usage: `DefendersOfTheRealm.exe -batchmode -nographics --autopilot --scene=Village2 --run=v2 --seed=3 -logFile <full.log>`

## Part B — Bot-only in-world instant-spawn points (teleport pads)  ▶ READY
Booting a scene directly (Part A) tests a scene's *own* init, but NOT a cross-scene **arrival via a seam**
(ticket #2 is specifically the south-OuterWorld→`Garrison_troll_outpost` warp, where the carried hero is
lost → emergency purple pill). Part B reproduces *arrival* deterministically:

- A dev/bot-gated registry of named warp targets, e.g. `garrison-south`, `village2-center`,
  `outpost-troll`, resolved to a world position (+ scene if cross-scene).
- An AutoPilot phase (`WarpToNamedPoint`) and/or a dev-panel button that jumps the hero there via the
  EXISTING seam/warp path (NOT a raw transform set) — so the real `HeroControlEnsurer` /
  `SceneTransitionTrigger.RepositionPlayerAfterLoad` / `HeroLocomotion.WarpTo` arrival logic runs and the
  #2 instrumentation (already shipped, commit 6a9a5fd7) captures body/onMesh/pill state.
- HARD GATE: only when `AutoPilotInstaller.Requested()` (bot) OR `FeatureFlags.DevHotkeys` (dev opt-in).
  Reuse the existing kill-switch pattern (`EnemyFamilyTestSpawner` gates its 'J' on `DevHotkeys`).

### Files (Part B)
- `Assets/_Modules/DevTools/AutoPilotDriver.cs` — add `WarpToNamedPoint` phase + a small named-point table
  (or read from a dev-only JSON). Reuse `--scene` to load the cross-scene target first, then warp.
- `Assets/_Modules/DevTools/DevPanelController.cs` — optional dev-panel buttons for the same targets.
- (If a real seam test is wanted) gate `ff.outposttravel` on for the bot so the actual `OutpostConnector_*`
  seam is built and traversed, rather than a synthetic warp.

## Acceptance
- `--scene=Village2` boots a headless bot directly into Village2; the real garrison spawns; logs capture
  per-enemy shader/worldUp + hero arrival state. (Part A — verify with a `--scene=Village2` run.)
- A named warp point lands the hero at a garrison via the real warp path; if the carried hero is lost the
  `[Flow:Hero] EMERGENCY pill spawned in scene 'Garrison_*'` line fires (proves #2 root) — else it confirms
  the hero arrives intact. (Part B.)
- Neither capability is reachable in a release player build (gate-verified).

## NOT to touch
Release gameplay spawn/flow; the production seam logic (only EXERCISE it); no raw transform teleports that
bypass `WarpTo` (that would mask the very bug we're hunting).

## INSTRUMENT-FIRST (§12)
The arrival instrumentation already exists (WarpTo SamplePosition HIT/MISS + isOnNavMesh; SpawnEmergencyHero
scene Fail; EnemyFactory worldUp). Part B just DRIVES it deterministically. Confirm #2 by the captured
`[Flow:Seam]`/`[Flow:Hero]` lines, not by inference.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
