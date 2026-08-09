# WORK ORDER 22 — Village wall + gate geometry (builder)

**Status:** CLOSED - SUPERSEDED (reconciled 2026-08-09 from the tree - the target scene `Village.unity` is DELETED from the tree; CLAUDE.md sec.7 canon. No commit references WO-22)

**Date:** 2026-05-24 (filed from owner playtest triage). **Authority:** #35 + WO-025.
**Priority:** High — visible structural errors. **Depends on:** WO-05.
**Class:** curated-scene / `VillageSceneBuilder` + `WallLayout` geometry — **must re-run/patch the scene builder** (which is normally hard-ruled "do not run"); this WO is the explicit authorization to fix the wall/gate layout and re-bake the village.

## Bugs (owner playtest 2026-05-24) — all the procedural wall/gate layout
- **#6 Force-field shows as a box / stone mesh, not a sheet in the gap.** `VillageSceneBuilder.BuildGates()` never creates a force-field sheet on real gates ("No force-field shimmer either", ~lines 642-647); `WireGateForceFields()` (~2609-2640) finds no `ForceFieldShimmer` child and falls back to the **stone `wall_straight_gate` mesh renderer** — so `Gate._forceFieldRenderer` drives `_Collapse` on a stone mesh that doesn't run `ForceFieldGate.shader`. **Fix:** in `BuildGates`/`WireGateForceFields`, add a thin child quad/cube named `ForceFieldShimmer` (~2.8 wide × ~4 tall × ~0.05 thin, center local (0,2,0), thin axis along wall thickness) filling the doorway gap, and assign `ForceFieldGate.mat` to it. (Pairs with WO-08 collapse + the WO-#8 alpha fix already applied.)
- **#7 South→SE gate doesn't touch the wall.** `WallLayout.cs` south-bow + short SE-leg run math + per-run `cornerInset` + the gate-mesh `1.43×` stretch don't align the gate edge with the SE segment → gap. **Fix:** reconcile the run/inset/gap arithmetic for the south bow + SE leg (and/or the gate stretch).
- **#E SE corner lets the village spawn OUTSIDE the wall** and **#F buildings spawn IN the wall.** Building placement vs wall footprint isn't clamped to the interior on the SE side. **Fix:** constrain building placement inside the wall ring (esp. SE corner).
- **#G NW walls structurally incorrect / rounded curves.** Wall-segment layout on the NW side is malformed. **Fix:** correct the NW run geometry in `WallLayout`.

## Acceptance criteria
1. Each gate shows a translucent **force-field sheet filling the doorway gap** (running `ForceFieldGate.mat`), and `Gate._forceFieldRenderer` points at that sheet (not the stone mesh).
2. All four gates meet their flanking wall segments with no gap (incl. South→SE).
3. No building spawns inside or outside the wall ring (SE corner included).
4. NW wall run is straight/correct (no rounded/broken curves).
5. `.\build-windows.ps1` clean; eyes-on in Village confirms the geometry.
6. `WORK_ORDER_22_*.RESULT.md` with before/after screenshots.

Key files: `Assets/Editor/VillageSceneBuilder.cs` (BuildGates/WireGateForceFields/building placement), `Assets/_Modules/Village/Walls/WallLayout.cs`, `Assets/_Modules/Village/Gates/Gate.cs`.
