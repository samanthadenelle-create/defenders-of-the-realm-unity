<!-- era-sweep-2026-08-17 -->
> ### ⛔ ERA SWEEP 2026-08-17 — CLOSED as OBSOLETE (deleted system)
> **Dead thing:** Village.unity + OuterWorld.unity. **Git first-add:** 2026-06-22.
> **Evidence:** `Assets/Scenes/Village.unity` is absent from disk and from `git ls-files` and `Assets/Scenes/OuterWorld.unity` is absent from disk and from `git ls-files`; the bug IS the coplanarity between the two now-merged scenes' floors.
> Only the `**Status:**` line was rewritten. The body below is UNTOUCHED — CLAUDE.md §15, *"frozen, never rewrite"*.
> **TO REVIVE:** nothing was deleted and not one line of the body below was changed. If this work is still wanted, re-date the WO (add a `**Minted:** <today>` line), re-point it at the live scene/system, and set `**Status:** READY TO IMPLEMENT`.

# WORK ORDER 192 — Ground Z-Fighting (village floor vs world terrain coplanar)

**Status:** CLOSED — OBSOLETE: Village.unity + OuterWorld.unity no longer exists (era sweep 2026-08-17)
**Lane:** A (Village Scene — SERIAL, `VillageSceneBuilder` + `ExteriorTerrainBuilder` + NavMesh bake)
**Source:** owner playtest — dark diamond holes in interior grass. Architect-assessed 2026-05-31.
**Priority:** P1 (visible interior rendering bug)

## Cause (confirmed in code)
Diamond holes = **z-fighting** between two coplanar Y≈0 surfaces under the village footprint: the hex floor
(`BuildGroundFloor`, tiles at Y=0 / collider top ~0.015, `VillageSceneBuilder.cs:586/611`) and the exterior
terrain plateau (`ExteriorTerrainBuilder` holds the footprint flat at Y=0; `TerrainBaseDepth=0.5` was a hack
to dodge exactly this z-fight, `ExteriorTerrainBuilder.cs:110-117`).

**Good news:** the hex floor is **already removed** ("Ground floor — DROPPED (owner 2026-05-31)",
`VillageSceneBuilder.cs:327-333`). So the owner's "remove village tiles" fix is effectively in place — the
holes the owner saw are likely a **stale build** from before that landed. This WO **finishes it safely.**

## Recommended fix (architect — Option A, confirm + finish)
1. **Keep an invisible walkable floor for navmesh.** With the hex floor gone and terrain in a SEPARATE
   additive scene (`OuterWorld.unity`), the Village navmesh bake could have **no walkable surface inside the
   walls** → hero/enemies can't path the interior. Add a single **Y=0 collider plane under `Ground`,
   renderer DISABLED, flagged NavigationStatic** (no renderer = no z-fight, but the Village bake still has a
   floor). *(Alternative: bake interior navmesh into OuterWorld — heavier; the invisible plane is the cheap insurance.)*
2. **Drop `TerrainBaseDepth` toward 0** (`ExteriorTerrainBuilder.cs:115`) — the 0.5 m step existed only to
   dodge the now-deleted tiles; with no tiles the plateau sits flush at Y=0 and the lip at the wall base goes away.
3. **Re-bake order (per WO-173):** Village navmesh, then `OuterWorldBuilder` → `ExteriorTerrainBuilder.BuildExterior`
   (so regions/nodes/terrain sit at the corrected Y). Editor closed.

## Acceptance
- No diamond holes / z-flicker in the interior ground on a FRESH build.
- Hero + enemy NavMeshAgents path freely inside the walls (interior floor walkable).
- Walls/moat/buildings still seat at Y=0; gate openings walkable; terrain flush at wall base.
- Does not re-break WO-173 (world still renders; terrain survives a Village rebake).

## Risk / watch
- **Interior void:** deleting the floor WITHOUT the invisible walkable plane = empty interior navmesh (the one
  thing that re-breaks the village). The plane is mandatory.
- Re-run `BuildExterior` AFTER `OuterWorldBuilder` if `TerrainBaseDepth` changed, or regions sit at old Y.
- `WorldSceneLoader.DiagTerrain` already repaints splatmaps at runtime (baked alphamaps don't persist to
  player builds) — unrelated fragility, keep on radar.

## Gate
Brace check; green build; folds into the Batch A village bake; commit `feat: implement WO-192 — ground coplanar z-fight fix`. Screenshot interior for UI validation.
