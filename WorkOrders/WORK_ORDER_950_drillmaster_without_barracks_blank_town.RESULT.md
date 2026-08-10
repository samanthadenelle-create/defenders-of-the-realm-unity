# WO-950 RESULT — Drillmaster + teach + phantom footprint on a blank town

**Status:** IMPLEMENTED — owner felt-verify owed
**Landed:** 2026-08-10 (implemented by the wave-3 lane; verified, gated and committed by the CLI seat)

## What changed and why

Two defects from one felt-report, one root: a baked `CastleBarracks` standing when it shouldn't.

1. **The scene-load path had no blank-town gate.** `BarracksNpcInjector.Inject()` runs from
   `OnSceneLoaded` — BEFORE the deferred `StructureSingleton.EnforceAll` sweep — so it found the
   still-active bake, seated the drillmaster, and burned the `barracks_intro` once-teach on a save
   whose `EverBuiltStructureIds` was empty. Only the 1 Hz poll carried the WO-834 gate. The same
   `MayBakedTwinSurface("barracks")` early-return now guards the BAKED fallback branch
   (`BarracksNpcInjector.cs:216-224`). A PLACED barracks is exempt by construction — the placed-scan
   above claims it and never reaches this branch.
2. **A suppressed twin kept solid colliders.** `SetActive(false)` leaves every collider's enabled
   flag true and the skin-hide path hides renderers only, so a gate-suppressed barracks was an
   invisible building the hero walked into at ~(16,0,-4). Suppression now strips every collider
   (solid AND trigger) plus every `NavMeshObstacle`, idempotently, on every sweep
   (`HubStructureVisualInjector.cs:428-452`); surfacing restores them and re-asserts the Ticket #10
   rule that a `setLocalPos` row keeps its baked body collider down (`:459-493`).
3. **The mis-burned once-teach self-heals.** The gate-refusal path clears `barracks_intro`
   (`BarracksNpcInjector.cs:289-310`). A closed gate PROVES the drillmaster never legitimately
   seated on that save (a placement opens the gate forever via `MarkEverBuilt`), and it double-guards
   on `IsPlayerBuilt`, so a legitimate burn is untouchable. Persist is play-mode only, so a headless
   suite can never clobber a real save.
4. **Orphan reap.** If the ordering ever seats a drillmaster before the sweep, the 1 Hz poll reaps it
   and re-arms the teach (`:128-140`).

Ownership stayed singular: the rule lives in `StructureSingleton`; this seam only QUERIES that
authority earlier than its deferred sweep runs.

## Files

- `Assets/_Modules/Village/NPCs/BarracksNpcInjector.cs`
- `Assets/_Modules/Village/HubStructureVisualInjector.cs`
- `Assets/_Modules/Village/BuildMode/StructureSingleton.cs`
- `Assets/Editor/Regression/BarracksBlankTownRegression.cs` (new) + registration in
  `Assets/Editor/Regression/DataRegression.cs`

## Gate (real, this run)

- `Builds/gate-settle4.log` → `COMPILE_GATE_OK :: scripts compiled clean`, zero `error CS`
- `Builds/regression-settle3.log` → `REGRESSION_OK 143/143 suites` (`[barracks-blanktown]` green)

## Oracle — what it proves

`BarracksBlankTownRegression` (`BARRACKS_BLANKTOWN_OK`), six probes: the catalog authority
(`barracks` is a swept singleton authoring `CastleBarracks`); the pure `MayBakedTwinSurface` truth
table; a blank-town fixture where the live gate refuses, `Enforce` deactivates the twin leaving ZERO
enabled colliders and no live nav obstacle, and a seeded mis-burn is cleared; a legitimate burn
(ever-built, or a `BaseLayout` record) being untouchable; suppress/restore discipline including the
Ticket #10 exception; and source lints on all three seams.

## Honest limits

- The suite never CALLS `Inject()` (private, instance, scene-bound). That item is proven by the pure
  gate plus a source lint — control-flow drift that kept the string could pass.
- "No NPC appears" and "no toast fires" are play-mode facts, not asserted headlessly.
- The poll's orphan-reap branch is not executed by any probe.
- **The baked navmesh hole is NOT fixed here.** With zero enabled colliders, any remaining block at
  that footprint is the merged-world bake, which ran with the twin standing. The suppression line now
  prints `navmesh-walkable-within-1.5m=<bool>` so the next report names its own cause; a rebake is a
  separate lane.

## Owner felt-verify

1. Current blank-town save: NO drillmaster near the barracks anchor, NO "Elarion needs soldiers" toast.
2. Walk ~(16,0,-4): is the invisible wall gone? If not, read the new navmesh line — that is the bake.
3. Build a real Barracks: the drillmaster seats at the PLACED building and the teach fires once, there.
