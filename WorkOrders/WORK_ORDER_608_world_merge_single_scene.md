<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-04
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-04) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 608 — Merge Castle + Overworld into ONE scene (`Main_Castle_Overworld`)

**Status: READY TO IMPLEMENT.** Owner-approved architecture pivot (2026-07-04): one seamless scene =
castle + full outer world, continuous navmesh, seam eliminated on the ramp descent. **Supersedes the
additive-streaming model + the parked WO-453 un-stack** (do NOT apply `stash@{0}`). Perf pass (WO-6xx / Lane D)
runs AFTER the merge. Provisional number — slot into the master backlog.

## THE PIVOTAL INSIGHT (verified from code)
MainCastle_Hall and OuterWorld are **already authored at the SAME origin** (both centered at 0; terrain
1000×1000, `TerrainCenterZ=0`). The stacked/overlapping origin is exactly what caused the masked-warp
ping-pong. **The merge is the opposite of WO-453**: don't offset + warp — keep co-located, drop both into
ONE scene, bake ONE navmesh. The overlap dissolves because there is no second scene / second navmesh / warp.
⚠ **The plan depends on both worlds staying at origin — do NOT merge `stash@{0}` or any `WorldGeometry.OuterWorldOffset ≠ 0`.**

## STRATEGY — merge the saved scenes, do NOT regenerate either
MainCastle_Hall is hand-dialed (canon: never regen) and `ExteriorTerrainData.asset` is binary-corruption-prone
(canon: re-bake, never git-restore). So COMBINE, don't rebuild.

**NEW editor orchestrator `Assets/Editor/WorldMergeBuilder.cs`** (new file — NOT the §9 bottleneck):
1. Open `MainCastle_Hall.unity` (Single) + `OuterWorld.unity` (Additive).
2. Merge OuterWorld roots into the castle scene (co-located → **zero offset math**).
3. **Dedupe singletons** the castle already owns (2nd Directional Light, 2nd camera, EventSystem, audio listeners, bootstrap objects) — keep the castle's.
4. Invoke the moat at edit-time (below) so its geometry is present for the bake.
5. Save as `Assets/Scenes/Main_Castle_Overworld.unity`; add to Build Settings; register as primary start + hub.

## ✅ MOAT REMOVED — keep ONLY the 4 drawbridges (owner 2026-07-04, FINAL: "simple and correct — remove it and replace with just the 4 drawbridges")
In the merged single scene there is no seam to cross. **REMOVE the moat entirely** (water basin + `BuildOuterLip` +
hedge + bank berms — the fragile decoration) and **keep ONLY the 4 drawbridge structures.** Geometry: the plinth is
raised (`castle.liftY=3`), outer terrain is flush at y=0 within ±62 — so the 4 drawbridges are the FUNCTIONAL RAMPS
from the plinth edge (r=44, y=3) down to the ground (y=0) at the 4 gate exits. Keep them → navmesh flows
plinth → 4 bridge ramps → flat ground → terrain, one continuous surface.
`WorldMergeBuilder.BuildMergedWorldScene()` does NOT call full `CastleMoatBuilder.BuildMoat()`; it places ONLY the
4 bridge clones (owner south pose from offsets.json, yaw 0/90/180/270) via a `BuildBridgesOnly()` path / `waterAndLip:false`
param. NO water plane, NO lip, NO basin, NO water NavMeshModifierVolume. This is the ROBUST bake (the moat basin/lip
was the single most fragile piece — gone).

## BUILD-SCRIPT EXPANSION — reuse the LOCKED moat generator (now OPTIONAL — see above)
The moat/water/4-bridge/lip generator (`CastleMoatBuilder`) is owner-tuned + LOCKED — REUSE, do not re-author.
1. **Retarget the scene gate (§9 bottleneck file — ONE agent, 2-line change):** add `"Main_Castle_Overworld"`
   to `CastleMoatBuilder.TargetScene` recognition AND to `DeNelle.Core.HubScenes.IsHub(...)`.
2. **Edit-time moat bake:** `WorldMergeBuilder` calls `CastleMoatBuilder.BuildMoat()` (public static; reads
   locked constants + owner south-bridge pose from `offsets.json`), then bakes. LOCKED offsets + owner pose
   preserved automatically (they come from the constants/offsets.json — change NO geometry values). The lip
   (`BuildOuterLip` r=62→65, `LipFloorY=0`) + bridge outer-end pitch-seat marry plinth (liftY=3) → deck →
   terrain; in one baked surface the seam is physically gone.

## ONE CONTINUOUS NAVMESH BAKE — `WorldMergeBuilder.BakeMergedWorldNavmesh` (batch, editor CLOSED)
Mirror `OuterWorldNavBake.Bake` reflection on `NavMeshSurface`. ONE surface at origin, `collectObjects=All`,
`useGeometry=PhysicsColliders` (Terrain collider + plinth/courtyard/bridge-deck colliders). Add a
`NavMeshModifierVolume` (Not-Walkable) over the **moat water basin ONLY** — this REPLACES the old ±62 40m
blanket `EnsureCastleNavHole` carve (that carve was a stacking workaround; a blanket carve would kill the
plinth/bridges in the single bake). Persist to `Assets/Scenes/Main_Castle_Overworld/NavMesh-*.asset` (binary).
Bridge decks connect plinth(y=3) → pitched deck → terrain(y=0) in ONE scene → one walkable mesh, **seam gone,
no runtime link/warp, no flag_14 width-clamp fragility.**

## RETIRE / CHANGE (flag-gate DISABLE over delete — reversibility). Add `FeatureFlags.MergedWorld`.
| System | File | Change |
|---|---|---|
| Additive OuterWorld load | `WorldSceneLoader.cs` | no-op when active scene = `Main_Castle_Overworld` (content already in-scene; streaming would double-load). Gate `ff.MergedWorld`. Keep additive path for legacy hubs. |
| Runtime masked warp/seam | `RuntimeRegionGate.cs` | skip the castle↔outerworld crossing on `Main_Castle_Overworld` (now a walk). KEEP the RegionGate primitive for dungeon/outpost/arena. Disable, don't delete. |
| 4-side warp row | `region-gates.json` | retire ONLY the `rgate_castle_to_outerworld` row; leave others. |
| `EnsureCastleNavHole` (±62 blanket carve) | `OuterWorldNavBake.cs` | do NOT blanket-carve on merged scene → replace with moat-basin-only NavMeshModifierVolume (handled in WorldMergeBuilder; don't run the old solo bake on the merged scene). |
| `BuildSeamlessOuterWorldSeam` + `NavLink_CastleToOuterWorld_*` | `CastleHubBuilder.cs` | obsolete for merged scene; leave method for legacy two-scene path; merged scene never builds it. |

## FILES
- **NEW:** `Assets/Editor/WorldMergeBuilder.cs`, `Assets/Scenes/Main_Castle_Overworld.unity`, `Assets/Scenes/Main_Castle_Overworld/NavMesh-*.asset`.
- **EDIT (world lane, ONE agent for the bottleneck files):** `WorldSceneLoader.cs`, `RuntimeRegionGate.cs`,
  `CastleMoatBuilder.cs` (⚠ bottleneck — minimal gate edit), `region-gates.json`, `OuterWorldNavBake.cs`,
  `DeNelle.Core.HubScenes`, `FeatureFlags.cs` (add MergedWorld — CLI owns to avoid Lane-A conflict), Build Settings.
- **PRESERVE untouched:** `ExteriorTerrainData.asset` (binary; reuse), owner south pose in `offsets.json`, all
  CastleMoatBuilder locked constants, `CastleHubBuilder.BuildCastleHub` geometry.

## VERIFY
CompileGate → `WorldMergeBuilder.BakeMergedWorldNavmesh` (editor closed) → AutoPilot fleet: continuous walk
castle→ramp→outer world (no warp fires, no ping-pong), `MOAT_COMPLETE`/CHECK4-5 green on merged scene,
spawn→outer-region path complete. Then OWNER felt-verify (ten-year-old test: descend the ramp, seam invisible).

## RISKS
- Perf/memory: one always-loaded 1000×1000 terrain + trees + castle, no streaming → Perf Engineer (Lane D) after; destination = WO-545 Addressables chunking.
- Navmesh size/bake time: tune voxel; confirm headless bake completes.
- **The stash trap:** accidentally pulling `stash@{0}` (offset -2000) re-separates the worlds → verify no offset lands.
- Binary corruption: keep `.gitattributes binary` on TerrainData/NavMesh; commit navmesh binary; never git-restore (re-bake).
- Dedupe singletons (2 lights/cameras/EventSystems) or lighting/input double-drives.
- Re-verify `LipTopY` below deck-bottom at r=62 under owner pose after merged bake (locked-doc paired-change; headless capture, not eyeball).

## ⚖ APPROACH RECONCILIATION (two SME designs — LOCKED decision)
Two designs landed. **Merge method = merge the SAVED scenes (this WO's WorldMergeBuilder), NOT re-run the
builders into a fresh scene.** The spawning-lane design proposed a `MainCastleOverworldBuilder` that RE-RUNS
`ExteriorTerrainBuilder→OuterWorldBuilder→CastleHubBuilder` — that would **REGENERATE the hand-dialed
MainCastle_Hall castle = canon violation (§3 / WO-453: never regen the hand-dialed hub).** REJECTED for the
castle. Use `WorldMergeBuilder` = open + additive-merge the two saved `.unity` files (§A above).

## SPAWNING / POPULATION HALF (Lane C — WO-609, folded here; run in the world lane after the merge scene exists)
The spawning SME inventory is authoritative — key breakages the merge causes + fixes:
- **`WorldPopulationDirector`** (NEW, `Assets/_Modules/Village/World/`, `ff.worldpopulation`): self-boot DDOL in
  the merged scene; a POSITION boundary (radius >62m from origin, hysteresis 62/56, or bridge-threshold trigger
  volumes) raises `OnEnterWorld`/`OnReturnToCastle` — replaces every `sceneName=="OuterWorld"` scan. Data-driven
  `Resources/Data/world-population.json` (mineNode/cavePortal/dungeonPortal/spawnArea rows, `phase` prebuilt|onLeave);
  a THIN interpreter delegating to existing factories (OuterWorldBuilder node pattern, SceneTransitionTrigger,
  RegionMobSpawner tables) — reinvent nothing. `Guard.TryEach` + `[Flow:WorldPop]` per row.
- **Scene-name gate repointing (the real breakage surface):** ~10 files hardcode `"OuterWorld"` /
  `"MainCastle_Hall"` — `OverworldEncounterSpawner` (reps never engage), `CastleSpawnPointInjector.TargetScene`
  (wave points not injected), `RaidOutpostSystem`, `CampSystem`, `OutpostVictoryController`, `WorkerManagerBootstrap`,
  `OuterWorldBoundaryInjector`, `WorldFeelInjector`, `PlayerBot`, `ArenaContracts`, `SceneTransitionTrigger`. Route all
  to ONE shared predicate (`HubScenes.IsOverworld`/Director `InWorld`). Repoint dungeon/Outpost2-return targets +
  `SceneRouter.Castle`/`HubScenes.Names` to `Main_Castle_Overworld`.
- **Facts that HELP:** no `"SpawnPoint"` tag is used (discovery is `FindObjectsByType<WaveSpawnPoint>()`); enemies
  find the hero via `FindAnyObjectByType<HeroLocomotion>()` — both get SIMPLER with one hero in one scene; the
  combined navmesh removes the "castle safe" enemy-can't-path workaround. BattleArena is a same-scene far region
  at (5000,5000) — keep a 200m radius clear (terrain is ±500, safe).
- **Verify:** enemy paths castle↔far region; `ZoneManager` classifies castle footprint (±62) as safe (no mobs in
  castle); MineNode props seat on the terrain collider; Village2/dungeon/Outpost2-return round-trips land home.

## EXECUTION ORDER (owner priority: hero movement FIRST/exclusive; then this)
Land Lane A (hero movement build) first on an isolated gate so this large/risky navmesh merge cannot delay the
owner's #1 walkable build. Then implement WO-608 on the clean committed tree. Lane C (spawning WO-609) shares
the world/seam files → same world lane, sequenced with this (one agent on the §9 bottleneck files).
