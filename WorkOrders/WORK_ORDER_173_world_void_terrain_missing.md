# WORK ORDER 173 — The World is a black void: exterior terrain missing / scene-split orphan

**Status: READY TO IMPLEMENT — ⛔ P0 BLOCKER (owner-flagged 2026-05-31)**
**Priority:** ⛔ **P0 — TOP OF THE QUEUE.** The game looks broken: owner *"where is world?"* — village on a
tiny lit grass patch in black void; the entire outer world (terrain, regions, mine nodes) is absent. This
is the most visible "the build is broken" issue — **fix before all other castle/world work.**
**Date:** 2026-05-31
**Lane:** Architect / World — `ExteriorTerrainBuilder` / `OuterWorldBuilder` / `WorldSceneLoader` + bakes.
CLI; UI spec.
**Source:** owner playtest screenshot — village = small grass square in a black void; only a distant
tree-line at the horizon; no regions/nodes/ground.

---

## Root cause (diagnosed from code)

The two-scene split (Village.unity + OuterWorld.unity) created a **terrain orphan**:

1. **The exterior terrain (the 300×300 world ground) is built INTO `Village.unity`**, by
   `ExteriorTerrainBuilder.BuildExterior` (it loads/saves **Village.unity**, `:180`). It is NOT in
   OuterWorld.unity.
2. **`OuterWorldBuilder` assumes "the terrain already exists"** (its header: *"Runs AFTER the terrain
   exists; it adds region anchors + [nodes]"*) — it places regions/mine nodes but **builds no ground.**
3. So after the split: OuterWorld.unity has **regions + nodes but no terrain to sit on**, and the recent
   Village rebakes (WO-136/150/157 castle + strip passes) appear to have **stripped or not regenerated the
   exterior terrain** in Village → the black void. The only surviving exterior bit is the distant tree-line.

**Net:** the ground the whole outer world was designed to sit on is gone from the baked Village, and the
OuterWorld content has nothing under it. Hence "where is world?"

## The fix — decide where terrain lives, then regenerate it

**Option A (recommended) — terrain moves to OuterWorld.unity (true to the split).** The outer-world
ground belongs with the outer-world content. Retarget `ExteriorTerrainBuilder.BuildExterior` to build the
terrain into **OuterWorld.unity** (alongside the regions/nodes `OuterWorldBuilder` adds), so the additive
WorldSceneLoader brings the **whole** outer world (terrain + regions + nodes) in over the village. Village
keeps only its interior + walls.
- Then: bake OuterWorld (terrain + regions + nodes together), verify WorldSceneLoader loads it at play, and
  the village sits in a real landscape, not void.

**Option B — terrain stays in Village, just regenerate it.** If terrain should remain in Village, the
rebakes are dropping it — re-run `ExteriorTerrainBuilder.BuildExterior` after the castle rebake so the
300×300 terrain is back in Village.unity, and ensure the strip passes (WO-150/157) don't nuke
`ExteriorRoot`. (Simpler now, but keeps terrain split from the OuterWorld content it serves — A is cleaner
long-term.)

**Either way, also:**
- Confirm `WorldSceneLoader` actually loads OuterWorld at play (it gates on `active.name == "Village"` and
  the scene being in Build Settings — both look set; verify the log line `"OuterWorld loaded additively"`
  fires, and that OuterWorld isn't loading but empty/under the void).
- Confirm **skybox + lighting/fog** render in the play build (the black void above the grass may also be a
  missing skybox/ambient-light in the loaded scene set — check RenderSettings survive the additive load).

## Recommendation
**Option A** — move the exterior terrain into OuterWorld.unity so the split is coherent (outer world = its
terrain + regions + nodes, loaded as one), and the village stops floating in void. This also stops future
Village rebakes from wiping the world's ground.

## Acceptance criteria
1. On play, the village sits in a **visible landscape** (terrain + biomes), not a black void; the horizon
   tree-line connects to ground.
2. The **regions (4) + mine nodes** are present and sit on the terrain (OuterWorld content visible).
3. Terrain ownership decided (A: in OuterWorld / B: regenerated in Village) and **survives a Village rebake**
   (a castle rebake must not wipe the world ground again).
4. `WorldSceneLoader` loads OuterWorld additively at play (log confirms); skybox/lighting/fog render (no void).
5. Enemy spawn→Heart + the village interior unaffected; brace balance; editor-closed bakes.

## Done checklist (CLAUDE.md §10)
- [ ] Terrain ownership decided + implemented (recommend A: terrain → OuterWorld.unity)
- [ ] Exterior terrain regenerated/baked; village in a real landscape, no void
- [ ] Regions + mine nodes visible on terrain; WorldSceneLoader load confirmed
- [ ] Skybox/lighting/fog render; terrain survives a Village rebake
- [ ] Brace balance; editor-closed bakes
- [ ] `WORK_ORDER_173_world_void_terrain_missing.RESULT.md` when complete
