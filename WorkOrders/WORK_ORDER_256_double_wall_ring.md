<!-- era-sweep-2026-08-17 -->
> ### ⛔ ERA SWEEP 2026-08-17 — CLOSED as OBSOLETE (deleted system)
> **Dead thing:** Village.unity. **Git first-add:** 2026-06-22.
> **Evidence:** `Assets/Scenes/Village.unity` is absent from disk and from `git ls-files`; acceptance is a `VillageSceneBuilder` wall-ring change landed by rebaking that scene.
> Only the `**Status:**` line was rewritten. The body below is UNTOUCHED — CLAUDE.md §15, *"frozen, never rewrite"*.
> **TO REVIVE:** nothing was deleted and not one line of the body below was changed. If this work is still wanted, re-date the WO (add a `**Minted:** <today>` line), re-point it at the live scene/system, and set `**Status:** READY TO IMPLEMENT`.

> ⚠ **UNRESOLVED NUMBER COLLISION — WO-256 is claimed by more than one file and OWNERSHIP IS NOT DECIDED.**
> Co-claimants: `WORK_ORDER_256_blue_ring_removal.md`, `WORK_ORDER_256_double_wall_ring.md`
> Both files were added in the SAME commit (first-on-disk is a dead tie) and neither is cited by any other doc, RESULT file, or commit message — there is no evidence on either side. Both are also still READY.
> Flagged (not resolved) by the 2026-08-16 Sunday board-grooming pass — a wrong ownership call is worse
> than a flagged unknown, so this needs an **owner ruling**. Nothing renumbered or deleted. Until it is
> ruled, cite this WO by FILENAME, never by bare number.

# WO-256: Double wall ring — remove BuildWallRing, keep BuildWallPerimeter only
**Linear:** [DEF-106](https://linear.app/defenders-of-the-realm/issue/DEF-106/double-wall-ring-buildwallring-buildwallperimeter-both-active-causing)
**Lane:** World/Environment
**Status:** CLOSED — OBSOLETE: Village.unity no longer exists (era sweep 2026-08-17)
**Priority:** Medium

## Acceptance Criteria
- [ ] `BuildWallRing()` and `BuildGates()` calls removed from `VillageSceneBuilder.cs`
- [ ] Only `BuildWallPerimeter` / `BuildCastleFortification` builds the wall
- [ ] Scene rebaked — no double-wall z-fighting visible at any angle in Play mode
- [ ] Wall renders as a single clean ring with no overlapping geometry
- [ ] Brace balance check passed

## Files to Edit
- `Assets/Editor/VillageSceneBuilder.cs` — remove old `BuildWallRing()` and `BuildGates()` calls

## Do NOT Touch
- Village.unity (never hand-edit — rebake via batchmode after fix)
- Files outside World/Environment lane

## Dependencies
- VSB is serialization bottleneck — coordinate with WO-244, WO-246, WO-249, WO-255
- Related to WO-232 (rendering sweep), WO-233 (collision/NavMesh sweep)
