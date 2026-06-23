# WORK ORDER 27 — RESULT (enemy spawn world / playable loop)

**Executed:** 2026-05-25 under Standing Authority #35, owner-authorized ("playable loop first").
**Editor:** Unity 6000.4.8f1. **Status:** built + bake-verified; runtime loop pending owner playtest.

## What was done

### 0. Root-cause fix that unblocked ALL re-bakes (KayKit asset paths)
Re-baking the village placeholdered **everything** (5,082 placeholder primitives) because the
builders load KayKit art by **hardcoded path** (`Assets/Models/KayKit/<pack>/…`), but the packs
sat one level too high (`Assets/Models/<pack>/`) and **5 of 6 were missing locally**. The editor
and the built player hid this — they resolve the committed scene by GUID, which is path-independent
— so only a re-bake exposed it.

Fix: created `Assets/Models/KayKit/`, **moved** the present Medieval Hexagon pack in (preserving its
`.meta` GUIDs so the committed scene still resolves), and **copied** the 5 missing packs from the
owner's Downloads collection (Skeletons 1.1, Forest Nature 1.0, Dungeon Remastered 1.1, Character
Animations 1.1, Mystery Monthly Series 5 — the last holds the hero + townsfolk models). After this,
`CaptureVillage` (FixAllMaterials → BuildVillage → BuildExterior) re-bakes with **0 placeholders**.

### 1. `VillageSceneBuilder.BuildApproaches` — the spawn world (§3.2)
Replaced the ~10 m, 5-hex, 2-tile stub with a real march corridor per cardinal gate:
- Spawn point pushed from `gatePos + outward*(7*step)` (~10–12 m) to **`gatePos + outward*ApproachLength`** (`ApproachLength = 40f`, a world-unit const → identical for N/S and E/W).
- Paved road corridor laid the **full 40 m**, **5 tiles wide (~8 m)** (`{-2,-1,0,1,2}·HexWidth` lateral), looping `i` until `i*step ≥ 40`.
- Spawn apron grown from 3×3 to **11×11 (~16×16 m)** flat grass pad centred on the spawn point — room for a 12-enemy batch; overlaps the corridor end so apron+corridor are one continuous surface.
- `WaveSpawnPoint.Configure(...)` contract unchanged (id/index/direction/gatePosition); only the marker's world position moved out.

### 2. NavMesh — automatic (§3.3)
The corridor + apron are parented under the existing nav-static **`Approaches`** root, which is already
in `BakeVillageNavMesh`'s `navStaticRoots`, and the bake runs after `BuildApproaches`. So the longer
corridors + bigger aprons bake automatically. Bake log: **"NavMesh baked — 5966 renderer object(s)
marked Navigation Static (Ground/Roads/Approaches walkable, Walls/Gates/Buildings obstacles)."**

### 3. Terrain flattening (§3.1) — NOT NEEDED
The spec assumed biome heights rose near the wall. They don't: `ExteriorTerrainBuilder` holds the
terrain **flat at Y=0 within ±150 (X) / ±120 (Z)** (`SeamWeight=1` there), and biomes only rise
beyond that. The 40 m corridors reach only ~z73/x82 — well inside the flat zone — so the corridor
tiles already sit flush on flat ground. No `ExteriorTerrainBuilder` change was required.

## Bake verification (clean)
`BuildVillage complete — 5063 ground tiles, 60 wall sections/corners, 4 cardinal gates, 831
plaza/road tiles, 5 gameplay buildings, 13 dressing buildings, 14 props, **4 wave spawn points**,
1 Elarion + 1 Keep. … NavMesh wired. **0 placeholder primitive(s).**` Enemy prefab OK; force-field
material OK; 4/4 ambient townsfolk. The exterior review PNG shows the four corridors + spawn aprons
radiating out the cardinal gates (the "plus shape"). 0 compile errors.

## Acceptance criteria — status
1. Spawn distance 40 m: ✅ (`ApproachLength=40`, marker at `gatePos+outward*40`).
2. Wave 1 spawns in the field (north apron): ⏳ owner playtest.
3. Navigable march (isOnNavMesh, paths apron→gate→Heart): ✅ bake (continuous nav-static corridor); ⏳ runtime confirm.
4. Baked corridor coverage (plus-shape): ✅ (exterior PNG + bake log).
5. Attack the gate (contact damage → breach <25%): ⏳ owner playtest (gate code unchanged).
6. Breach → ATB: ⏳ owner playtest (WaveManager/SceneRouter unchanged).
7. Cosmetic terrain intact + non-navmesh: ✅ (terrain excluded from bake; flat under corridors).
8. No interior desync / WallHalf-relative: ✅ (all distances gate/WallHalf-relative).

## Note on WO-26 (larger village)
The enlarged ring (WallHalfX=42 / WallHalfZ=33) was applied in the same re-bake (its builder edits
were already in place) and renders with real art — kept rather than reverted since it was already
baked and looks good. Owner had tabled WO-26 polish for post-MVP; revert is one `git checkout` if
the original-size village is preferred for MVP.

## Files changed
`Assets/Editor/VillageSceneBuilder.cs` (`BuildApproaches` + `ApproachLength` const),
`Assets/Editor/SceneScreenshot.cs` (`CaptureVillageOnly` + ortho capture helper for verification),
`Assets/Scenes/Village.unity`, `Assets/Scenes/Village/NavMesh.asset`, generated building/enemy
prefabs, exterior terrain assets (re-bake outputs). KayKit packs relocated under
`Assets/Models/KayKit/` (gitignored — local only).
