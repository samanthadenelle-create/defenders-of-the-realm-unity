**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 233 — Collision & NavMesh Sweep

**Status: READY TO IMPLEMENT**
**Author:** UI (creative lane)
**WO Number:** 233
**Date:** 2026-06-02
**Closes:** DEF-101, DEF-25, DEF-11, DEF-19
**Depends on:** WO-232 rebake complete (wall geometry must be final before colliders are placed)

---

## Summary

All four issues share a root: VillageSceneBuilder is creating phantom colliders, misplacing gate clearances, and not excluding the right geometry from NavMesh baking. One focused pass through VillageSceneBuilder + a single rebake fixes them all.

---

## DEF-25: Yellow NavMesh plane covering right half of village

A visible yellow quad is being placed as a NavMesh obstacle or surface — likely a `NavMeshSurface` or `NavMeshObstacle` component on a GameObject that was never intended to be visible, or a debug plane left in the scene.

1. In VillageSceneBuilder, search for any `GameObject` creation that adds a `MeshRenderer` + `NavMesh` component together — this is likely the culprit.
2. Remove or set the MeshRenderer to disabled on any purely-NavMesh helper objects.
3. Verify `IsNonWalkableMoatPiece` / `IsUnderPerimeterGate` exclusion logic is correctly excluding gate arches from NavMesh voxelisation.
4. Rebake NavMesh after fix.

---

## DEF-11: Invisible colliders blocking hero movement

Multiple phantom `BoxCollider` / `MeshCollider` volumes exist in the open field — near Healer's Cottage and in the village centre. These are ghost colliders from previous scene builds that were not cleaned up on rebuild.

1. In VillageSceneBuilder's scene-clear step (the part that destroys old GameObjects before rebuilding), confirm it destroys ALL previously spawned collider objects — not just the named building roots.
2. Add a targeted cleanup: find all GameObjects tagged `GeneratedCollider` (or equivalent) and destroy them before each build pass.
3. If no tag system exists, search for `col_` or `Collider_` prefixed objects and include them in the wipe.

---

## DEF-19: Hero clips through wall and gate geometry

Wall and gate meshes have no solid physics collider, or their collider is set to `isTrigger = true`.

1. In VillageSceneBuilder wall/gate placement code, confirm a `MeshCollider` (or `BoxCollider`) is added to each wall segment and gate GameObject with `isTrigger = false`.
2. If colliders exist but are triggers, change `isTrigger = false`.
3. Ensure `NavMeshObstacle` and physics `Collider` are not confused — an obstacle alone does not block the hero's `CharacterController`.

---

## DEF-101: Building overlaps gate — blocks enemy path and player exit

Farm building (and others) placed within 6m of a gate opening.

1. In `VillageSceneBuilder.cs` building placement logic, add a gate-clearance check:
   - After calculating a building position, check distance to all gate positions.
   - If `distance < 6f`, discard and re-sample position.
2. Also confirm enemy `SpawnPoint` tags are placed 12m outside each gate (per CLAUDE.md §7). Add them if missing — `WaveManager` requires them.

---

## Rebake

After all code changes, run one full rebake:
`Defenders > Week 3 > Build Village Scene`

Then re-bake NavMesh separately if the Unity editor has a manual NavMesh bake step.

---

## Acceptance criteria

- [ ] Yellow NavMesh plane gone — no visible quad in the village
- [ ] Hero can walk freely through open field near Healer's Cottage and village centre
- [ ] Hero cannot walk through walls or gate geometry
- [ ] No building footprint within 6m of any gate
- [ ] Four `SpawnPoint` tags placed 12m outside each cardinal gate
- [ ] Brace balance check passed on every `.cs` file edited

---

## What NOT to touch

- `Village.unity` — do not hand-edit
- Enemy AI logic / WaveManager wave composition
- Any ATB or dungeon files

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
