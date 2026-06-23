# WORK ORDER 168 — NavMesh: unseal the curtain-wall gate openings

**Status: READY TO IMPLEMENT**
**Lane:** World/Environment (VillageSceneBuilder — serialization bottleneck, single-owner)
**Owner sign-off:** Samantha — reported "in scene Village all gates too small or navmesh isn't right, I cannot exit."

---

## Problem

The hero cannot walk out through any cardinal gate.

Root cause is **NavMesh**, not gate scale. Two changes collided:

1. **2026-05-30:** `HeroLocomotion` was switched to move as a **`NavMeshAgent`**
   (`Assets/_Modules/Village/Hero/HeroLocomotion.cs:83-95`, `214-215`). The hero is now
   constrained to the baked NavMesh — physics colliders (the gate force-field blocker and
   the wing-blocker BoxColliders) no longer gate movement. **Only NavMesh presence does.**

2. The **outer perimeter gatehouses** (`BuildWallPerimeter`: `Gate-North-Main`,
   `Gate-South-Main`, `Gate-East-Side`, `Gate-West-Side`, scaled `gateTarget = 10`) are
   parented under the **`Walls`** root. `BakeVillageNavMesh` marks the entire `Walls`
   subtree `NavigationStatic`, so each gate-arch mesh **voxelizes solid across its own
   opening** — sealing the gate on the NavMesh.

This was invisible while the hero moved by free transform (it just walked through the open
arch); the move to NavMeshAgent exposed it. The recent `gateTarget` 6→10 bump made the
solid plug *larger*, not the cause.

The existing `WO-27` comment in `BakeVillageNavMesh` already documents this exact failure
mode — but its mitigation only excluded the **inner** `Gates` root, not these newer
perimeter gatehouses.

## Fix (already written by UI — needs build-verify + bake)

`Assets/Editor/VillageSceneBuilder.cs`:

- Added helper `IsUnderPerimeterGate(Transform t, Transform stopAt)` (just above
  `BakeVillageNavMesh`) — walks parents up to the subtree root, returns true if any
  ancestor name starts with `"Gate-"`.
- In `BakeVillageNavMesh`'s nav-static sweep, skip renderers for which
  `IsUnderPerimeterGate(r.transform, sub)` is true — so the scale-10 gate arches are left
  OUT of the bake, mirroring the inner `Gates` exclusion. Curtain wall segments flanking
  each opening stay nav-static (still bound the gap); Ground/Approaches keep the lane
  walkable through the opening + across the drawbridge.

Brace gate: **PASS** (511/511 open/close).

## CLI tasks

1. Build-verify `VillageSceneBuilder.cs` compiles (batchmode).
2. **Rebake the village** — `DeNelle.Editor.VillageSceneBuilder.BuildVillage`
   (Defenders > Week 3 > Build Village Scene). Editor must be CLOSED during the bake.
3. Confirm in the bake log: `[VillageSceneBuilder] NavMesh baked` line present, and the
   `marked` count is slightly LOWER than the previous bake (gate-arch renderers now skipped).
4. Commit code + rebaked `Village.unity` together (sole-committer rule).

## Acceptance criteria

- [ ] `VillageSceneBuilder.cs` compiles clean in batchmode.
- [ ] Village rebakes without error; NavMesh bake log line present.
- [ ] In the editor (owner/Tricia playtest): hero walks **out** through all 4 cardinal
      gates onto the drawbridge and into the approach lane, and back in.
- [ ] Enemies still path **in** through a destroyed/open gate (WO-27 behaviour intact) —
      i.e. the opening is walkable from both sides, the flanking wall segments still block.
- [ ] No regression: hero still can't walk through solid curtain-wall segments or buildings.

## What NOT to touch

- Do not change `gateTarget` / gate scale — scale is correct; this is a bake issue.
- Do not re-add the perimeter gate arches to `navStaticRoots`.
- Do not hand-edit `Village.unity` — rebuild via the builder only.
- Leave the inner `Gates`-root exclusion as-is.

## Follow-up (separate, not blocking)

- The uncommitted `[WALLDIAG]` `Debug.Log` diagnostic in `BuildWallPerimeter`'s `Wall`
  lambda (`VillageSceneBuilder.cs` ~line 2930) can be stripped once the wall-debris
  investigation is closed. Not part of this WO.
- Owner also noted the north gate area reads **very dark** — lighting polish, tracked
  separately.
