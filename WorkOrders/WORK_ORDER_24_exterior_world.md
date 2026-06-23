# WORK ORDER 24 — Exterior world / zones beyond the village (architecture)

**Date:** 2026-05-24 (filed from owner playtest triage). **Authority:** #35 + WO-025.
**Priority:** Medium (feature scope, not breakage). **Depends on:** WO-05.
**Class:** architecture decision + curated-scene/builder — file, do not auto-edit. (= recovery-work-orders Agent 7.)

## Bug (#D) — "once you leave the village there is no map / zones / anything connected"
Current state: an exterior **does** exist but is purely cosmetic. `VillageSceneBuilder.BuildVillage()` calls `ExteriorTerrainBuilder.BuildExterior()` — a single ~300×300 Unity Terrain (biomes, elevation, splatmaps, instanced trees/rocks, dawn skybox/fog, distant non-interactive landmark silhouettes), baked into `Village.unity` as `ExteriorRoot` (+ `ExteriorTerrainData.asset`). There is **no zone system, no out-of-village scene transitions** (only dungeon entrances + the ATB battle leave the village), no chunk streaming, no enemy-world progression. The per-gate "approaches" are short paved road stubs used as wave-spawn lanes, not gateways. So crossing the walls = cosmetic terrain with nothing to do; the world ends at the village.

This is **feature scope, not a regression** — a playable exterior was never built.

## Decision needed (recovery Agent 7)
Choose the architecture before implementing:
- (a) single expanded Village scene, (b) additive scene loading per zone, (c) chunk-based dynamic loading + pooling + distance activation. Mind mobile/WebGL performance; avoid a heavy always-loaded map.

## Acceptance criteria (once direction chosen)
1. A connected, traversable area beyond the walls (per chosen architecture) with at least one transition.
2. No breakage of core village references; performant.
3. `WORK_ORDER_24_*.RESULT.md` with the chosen architecture + what shipped.

Key files: `Assets/Editor/ExteriorTerrainBuilder.cs`, `Assets/Editor/VillageSceneBuilder.cs` (BuildApproaches), `Assets/_Modules/Core/SceneRouter.cs`, `docs/recovery-work-orders.md` (Agent 7).
