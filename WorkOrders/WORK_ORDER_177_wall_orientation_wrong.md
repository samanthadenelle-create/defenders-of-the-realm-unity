<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 177 — Wall segment orientation wrong (leaning / 180° off)

**Status: READY TO IMPLEMENT**
**Priority:** HIGH — visible playtest defect; wall reads as a tilted flat slab, not an upright wall.
**Date:** 2026-05-31
**Lane:** Architect / World — `VillageSceneBuilder.cs` wall build (single-writer, Agent 1) + rebake.
**Source:** owner playtest — *"wall is wrong, 180 degrees."* Screenshot: a single wall segment **leaning
over / tilted**, flat and featureless, not standing vertical.

---

## Symptom
A wall segment renders **leaning (tilted off vertical)** and reads as a plain slab — not an upright,
outward-facing curtain-wall piece. Owner: rotation is **180° wrong** (wrong yaw, or a stray pitch/roll
tipping it over).

## ⚠ UPDATE 2026-05-31 — TWO MORE wall/gate defects (same builder pass — fold in)
Owner playtest: **(a) "I can walk THROUGH the wall"** and **(b) "south gate is wrong."**
- **(a) Walk-through wall = the WO-136 barrier collision is NOT effective.** Either the WallBarrier boxes
  (WO-136, on the visible wall line, Y=0→wall-top) aren't being baked in the current rebuild, or they're
  mis-positioned/mis-sized so the hero passes through. **Verify the barrier colliders exist on the new
  wall sections and actually block the hero** (walk into a solid wall run → blocked). This is the WO-136
  "collision on the real wall" requirement not yet holding in the live bake — confirm it's implemented +
  baked, not just spec'd.
- **(b) South gate is wrong** — the south entrance currently renders as a **portcullis/dungeon-style
  arch with a brown stepped ramp**, NOT the intended cardinal **castle gatehouse + drawbridge** (WO-104/
  136/166). It looks like the wrong prefab (a dungeon door / `CastleDoorController` arch) is being placed
  at the south gate instead of `Gate_Medieval_Medium` + the drawbridge deck. **Fix the south gate to match
  the other cardinal gates** (correct gate prefab, drawbridge, passable opening) — see WO-166 (gates) /
  WO-167 (gate pillar). These three (wall lean + walk-through + south gate) are **one wall/gate pass.**

## Likely cause (located in code)
Wall orientation is set in `BuildWallPerimeter` / `BuildWallRing`:
- `VillageSceneBuilder.cs:130-131` — `WallStraightYawFix = 90f`, `WallCornerYawFix = 180f`.
- Applied at `:662-663` — `visual.transform.localRotation = Quaternion.Euler(0f, corner ? WallCornerYawFix
  : WallStraightYawFix, 0f)`.
- The segment also gets the `WallLayout.Rotation` on its parent `go`, then the visual is stretched
  (`FitWallVisualToRun`) along its run axis.

**Two candidates (CLI verify which):**
1. **Yaw 180° off** — the `WallStraightYawFix` (or the composed `WallLayout.Rot` + fix) faces the segment
   the wrong way, so after the run-axis stretch the piece reads flat/leaning instead of standing across
   the wall line. Owner's "180 degrees" points here.
2. **Stray pitch/roll** — if the recent wall rebuilds (WO-136/166) introduced a non-Y rotation, the
   segment tips off vertical (the *leaning* look). Wall pieces should rotate on **Y only**; X/Z = 0.

## Fix
- Ensure wall segments stand **upright (X/Z rotation = 0)** and face **outward along the wall line** —
  correct the yaw fix (and/or how `WallLayout.Rotation` composes with `WallStraightYawFix`) so the long
  axis runs along the wall and the face points out. Fix the constant or the composition, not per-segment.
- Verify on **all four walls** (the yaw differs per side — N/S vs E/W) — the fix must be correct for every
  side, not just the one visible in the shot.
- Re-confirm after the `FitWallVisualToRun` stretch (the stretch is applied in the visual's local space —
  make sure the corrected rotation still fits flush with no gaps/overlaps).

## Reconcile / coordinate
- This is the **same wall-build code** WO-136 (castle wall) and WO-166 (gates) touch. **Fold this
  orientation fix into that single wall/gate pass** (Agent 1, the VillageSceneBuilder single-writer) —
  don't make it a separate colliding edit. If WO-136's wall rebuild is in progress, fix the orientation
  there.
- Wall **collision** (WO-136 barrier) is separate from the visual rotation — fixing the visual yaw must
  not move the barrier off the wall line.

## Acceptance criteria
1. Wall segments stand **upright** (no lean/tilt; X/Z rotation = 0) and face outward along the wall line.
2. Correct on **all four walls** (per-side yaw right); pieces abut flush after the run-axis stretch (no gaps/overlaps).
3. **Barrier collision works — the hero CANNOT walk through a solid wall run** (WO-136 barrier baked + positioned on the visible wall line; verify by walking into the wall → blocked).
4. **South gate matches the other cardinal gates** — correct `Gate_Medieval` prefab + drawbridge + passable opening, NOT a dungeon-portcullis arch / brown ramp (reconcile WO-166/167).
5. Visual rotation fix doesn't displace the barrier collider; single-writer (Agent 1) on `VillageSceneBuilder.cs`; brace balance; editor-closed rebake.

## Done checklist (CLAUDE.md §10)
- [ ] Wall yaw/orientation corrected (constant or composition); segments upright + outward-facing
- [ ] Verified all 4 walls; flush abutment post-stretch; collision unmoved
- [ ] Folded into the WO-136/166 wall-gate pass (no colliding separate edit)
- [ ] Brace balance; editor-closed rebake
- [ ] `WORK_ORDER_177_wall_orientation_wrong.RESULT.md` when complete
