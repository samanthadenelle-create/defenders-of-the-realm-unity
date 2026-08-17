> ⚠ **UNRESOLVED NUMBER COLLISION — WO-137 is claimed by more than one file and OWNERSHIP IS NOT DECIDED.**
> Co-claimants: `WORK_ORDER_137_castle_rampart_rebake.md`, `WORK_ORDER_137_catalog_data_model_and_defensive_content.md`
> Both files were added in the SAME commit (first-on-disk is a dead tie) and neither is cited by any other doc, RESULT file, or commit message — there is no evidence on either side.
> Flagged (not resolved) by the 2026-08-16 Sunday board-grooming pass — a wrong ownership call is worse
> than a flagged unknown, so this needs an **owner ruling**. Nothing renumbered or deleted. Until it is
> ruled, cite this WO by FILENAME, never by bare number.

# WORK ORDER 137 — Village Rebake After Castle/Rampart Build (WO-136)

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-30
**Priority:** High — required after WO-136 lands; the castle/rampart isn't real in-build until this bakes
**Lane:** Architect (batchmode execution only — no code changes)
**Depends on:** WO-136 (castle structure + ramparts + collision + moat/drawbridges) committed and compiling

> Sequencing: this is a **separate, later bake** from the one CLI is running now. The current
> bake reflects the wall-stripped state. After WO-136 authors the new castle geometry, the
> scene must be rebuilt again so the new walls, rampart upper level, collision, and moat
> appear in `Village.unity` — and so the **two-level NavMesh** bakes correctly.

---

## Goal

Rebuild `Village.unity` from `VillageSceneBuilder` so the WO-136 castle fortification —
curtain walls, corner/gate towers, walkable ramparts + stairs, on-wall collision at the
correct height, moat ring, and 4 drawbridges — is reflected in the baked scene with a
valid NavMesh.

## Command

Run in batchmode (editor closed):

```
Defenders > Week 3 > Build Village Scene
(batchmode: DeNelle.Editor.VillageSceneBuilder.BuildVillage)
```

## Preconditions

- **WO-136 is committed and `VillageSceneBuilder.cs` compiles** (brace check passed).
- No other agent is mid-edit on `VillageSceneBuilder.cs` (CLAUDE.md §9).
- Unity editor is fully closed (project lock — CLAUDE.md §3).
- The earlier (wall-stripped) bake CLI is running now has finished — do not overlap bakes.
- `DOTR_SKIP_NAVMESH` is **unset** (a real two-level NavMesh bake is required here; the skip flag is crash-bisect only).

## NavMesh note (two-level — from WO-109a)

The rampart adds a walkable upper surface, so this bake must connect ground ↔ wall-top:
- Off-mesh links / layered bake enabled so stairs link the two levels.
- Finer voxel for stair ramps (per WO-109a: `voxelSize ≈ 0.15`).
- Enemy NavMesh area must still **exclude** the wall top (enemies don't path up the walls);
  hero/defender agents can.

## Acceptance

- [ ] `Village.unity` rebuilt via batchmode (no hand-edits)
- [ ] Builder compiles before bake; no console errors during build
- [ ] Castle present: curtain walls, corner + gate towers, ramparts + stairs, moat + 4 drawbridges
- [ ] Collision verified on the real wall at correct height (barrier Y=0→wall-top; walkway surface at wall-top; parapet fall-off)
- [ ] Two-level NavMesh baked: hero reaches the rampart via stairs/towers; can't fall through
- [ ] Enemy paths resolve spawn-0..3 → Heart across the drawbridges; enemies do NOT path onto the wall top
- [ ] Scene opens clean; interior buildings (Workshop, Crystal Mine, Pet House, Arcane Tower, Farm, Market) intact
- [ ] No purple/magenta materials (polyperfect atlas)
- [ ] Git commit the rebuilt scene
- [ ] `WORK_ORDER_137_castle_rampart_rebake.RESULT.md` written when complete
```
