# WORK ORDER 1111 — No harness has EVER loaded a raid scene; the whole assault is code-verified only

**Status:** CLOSED — owner-tested 2026-08-21.
**Minted:** 2026-08-16 (CLI seat) — banner bumped 1109 -> 1112 in the SAME edit (1109, 1110 minted alongside)
**Lane:** Test harness / AutoPilot. Disjoint from WO-1109 and WO-1110's runtime files.
**Provenance:** SME readiness audit of the raid pillar, 2026-08-16.

---

## 1. The gap

Grep for `GoRaid` / `RaidBase_` across `DevTools/`, `Editor/AutoPilot/`, `Editor/Regression/` and
`tools/` finds **only** the DevPanel's manual entry (`DevPanelController.cs:1533`) and string
constants. **No automated harness — editor, headless, or fleet — has ever loaded a `RaidBase_*` scene.**

AutoPilot screenshots the two **pre-raid panels** (`AutoPilotDriver.cs:6277-6318`) and stops there.

**Everything from BEGIN ASSAULT onward is (b) exists but never exercised:** hero spawn, deploy tray,
troop pathing to the spire, spire damage, victory screen, return home.

The existing raid tests are real but stop short of the scene:
- `RaidScoringRegression.cs` — pure-math assertions + a source-lint.
- `RaidDeployUiRegression.cs` — frame zones + the scout-report contract.
- `Assets/Tests/EditMode/Raid*` — VM/scoring unit tests.

## 2. Why this is the root ticket, not a nice-to-have

**WO-1109 (every raid spawns the emergency pill-hero) would have been caught by ONE headless raid
load** — the emergency path's first line is a `FlowTrace.Fail`, so a single automated entry would have
printed the alarm. The defect survived because nothing ever entered a raid except a human.

This is the §12 lesson applied to coverage: the cheapest possible instrument (load the scene, read the
trace) was never run, so a loud, self-announcing fault stayed invisible for the life of the pillar.

## 3. Build

An AutoPilot phase (or a headless regression) that:
1. Enters a `RaidBase_*` scene via the real `SceneRouter.GoRaid` path — **not** by direct scene load,
   or it will not exercise the hero-carry seam that WO-1109 is about.
2. Asserts **no `FlowTrace.Fail` lines** during entry (this alone is the WO-1109 oracle).
3. Deploys at least one troop through `RaidDeployController`'s real path and asserts it spawns.
4. Reaches a terminal state and asserts the return to town with the army reconciled.

⚠ **Must run under `-nographics`, so screenshots prove nothing** (canon: fleet `break_*.png` are blank).
Assert on `break-log.jsonl` + `FlowTrace` lines, and make any violation emit `FlowTrace.Fail` — that is
the only signal level the break-log captures.

⚠ **All three scenes are already in EditorBuildSettings** (`RaidBase_raider_camp_small`,
`RaidBase_fortified_garrison`, `RaidBase_mage_enclave`) and each carries a spire, a garrison spawner
and a baked NavMesh, so a harness CAN load them today — nothing is blocking this but the writing.
(`RaidBase_IronBastion.unity` exists but is **not** in build settings and is unreachable — do not
target it.)

## 4. ⚠ The full-army gate will block a naive harness

`RaidSelectionScreen.cs:92-106` refuses to open unless `deployable + queued >= MaxArmySize` (**10**,
`ArmyStorage.cs:43`). A harness must seed a full army first, or it will land in the drillmaster panel
and report a false pass on "raids unreachable."

## 5. Acceptance

- A headless run enters a raid, deploys, terminates and returns — with a marker.
- The run FAILS if any `FlowTrace.Fail` occurs during raid entry (so WO-1109 cannot regress).
- Registered in `DataRegression.RunAll` or the fleet, and its marker is distinct per `CLAUDE.md` §8
  (never reuse `REGRESSION_OK` — the 2026-08-02 lesson where three entry points shared one marker and
  the gate judged the wrong suite).

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `RaidHeroCarryRegression.cs:25` — no harness loads RaidBase_*. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.

> **OWNER RULING 2026-08-21 (verbal):** "1111 is closed and tested perfect." Owner has exercised the raid path directly; the harness gap is no longer the blocker it was written as.
