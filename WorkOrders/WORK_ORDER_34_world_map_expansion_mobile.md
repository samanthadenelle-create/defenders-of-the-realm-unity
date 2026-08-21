# WORK ORDER 34 — World Map Expansion (Mobile-Optimised Streaming Terrain)

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
**Date:** 2026-05-26
**Author:** Architect pass — owner playtest request
**Priority:** Medium-High — current 300×300 wu map is visually blank outside the village; owner wants a larger explorable world with memory efficiency on mobile

---

## Problem

The exterior terrain is a flat 300×300 wu (≈ 1.7 MB heightmap) with the village
in the centre. Everything outside the castle walls is a green plane with no
environmental detail. On the left side of playtest screenshots the map simply
ends in black void (WO-33 adds the boundary wall, but the emptiness beyond
still reads as unfinished).

The owner wants:
1. A **larger explorable map** (no hard numeric target stated — target ~1 km²,
   i.e. 1 000×1 000 wu)
2. **KayKit props spawning around the map** from the existing asset catalog
   (forest, nature, terrain packs)
3. **Memory efficient on mobile** — the project targets Android/iOS

---

## Architecture: Zone-Streamed World

### Coordinate system

Keep the village at world origin. Divide the world into **50×50 wu tiles**.
A 1 000×1 000 world = 20×20 = **400 tiles**. On mobile only tiles within a
**3-tile radius** (9 tiles) of the player are loaded at once.

```
Total world     : 1 000 × 1 000 wu
Tile size        :    50 ×    50 wu
Tiles total      :   400
Tiles loaded     :     9  (3×3 around player, ~22 500 wu²)
Village footprint:   1 tile (or 3×3 for the inner layout)
```

### Memory budget per tile (mobile target: ≤ 150 MB total GPU)

| Category | Budget |
|---|---|
| Terrain mesh (all loaded tiles) | ≤ 30 MB |
| KayKit prop meshes (shared atlas) | ≤ 40 MB |
| Prop instance buffers | ≤ 10 MB |
| Textures (terrain + props) | ≤ 50 MB |
| NavMesh partial bake | ≤ 15 MB |
| Headroom | ≤ 5 MB |

---

## Implementation Plan

### Phase 1 — WorldTile data model

**New file**: `Assets/Editor/WorldMapBuilder.cs`

```csharp
/// <summary>
/// Bakes the world map: divides the 1000×1000 wu space into 50-wu tiles,
/// assigns a biome + prop density to each tile, and writes
/// StreamingAssets/Data/WorldMap/tile-{x}-{z}.json for runtime streaming.
/// </summary>
[MenuItem("Defenders/World/Build World Map")]
public static void BuildWorldMap() { ... }
```

**New file**: `Assets/StreamingAssets/Data/WorldMap/world-manifest.json`

```json
{
  "version": 1,
  "worldSizeWu": 1000,
  "tileSizeWu": 50,
  "biomeMap": "StreamingAssets/Data/WorldMap/biome-map.png"
}
```

Each tile JSON:
```json
{
  "tx": 5, "tz": 3,
  "biome": "forest",
  "elevation": 0.2,
  "props": [
    { "id": "tree-oak-a", "x": 12.3, "z": 7.8, "yRot": 45.0, "scale": 1.0 },
    ...
  ]
}
```

### Phase 2 — Runtime tile streamer

**New file**: `Assets/_Modules/World/WorldTileStreamer.cs`

```csharp
/// <summary>
/// Loads/unloads 50×50 wu tiles as the hero moves. Maintains a 3×3 window
/// of active tiles centred on the player's tile. Tiles outside the window are
/// pooled (GameObjects deactivated, not destroyed) so re-entry is instant.
/// </summary>
public sealed class WorldTileStreamer : MonoBehaviour
{
    [SerializeField] private Transform _hero;
    [SerializeField] private int       _loadRadius = 1;      // 3×3 = radius 1
    [SerializeField] private int       _tileSize   = 50;

    private readonly Dictionary<Vector2Int, WorldTile> _active = new();
    private readonly Queue<WorldTile>                  _pool   = new();
    ...
}
```

**Load sequence per tile**:
1. Read `tile-{tx}-{tz}.json` (StreamingAssets, async)
2. Generate a lightweight mesh plane (50×50 wu, 8×8 vert grid = 81 verts)
3. Sample the shared biome heightmap for per-vertex Y
4. Spawn KayKit props from the `PropSpawnPool` (see Phase 4)
5. Activate — all within one frame budget (≤ 2 ms via `IEnumerator` spread)

**Unload sequence**:
1. Deactivate all prop GameObjects (return to pool)
2. Deactivate the tile mesh (return to pool)
3. Keep tile JSON data cached in RAM (tiny, fast re-read)

### Phase 3 — Biome terrain mesh

Replace the monolithic Unity `Terrain` (currently 300×300 wu) with a
**PlaneMesh** per tile. Benefits on mobile:
- No Terrain API overhead (Terrain.SetHeights, SplatMap, etc.)
- Mesh shared across tiles: one `Mesh` template, scale+position per tile
- `MeshCollider` baked once per biome shape — reused via `SharedMesh`

```csharp
private static Mesh BuildTileMesh(float[,] heights, int res = 8)
{
    // res×res grid, heights sampled from biome_map at tile UV
    // → 81 verts, 128 triangles per tile
}
```

**Biome map** (`biome-map.png`, 20×20 px = 1 px per tile):
Each pixel encodes biome type in R channel:
- `0` = grassland
- `64` = forest (KayKit Forest Nature Pack)
- `128` = highlands (KayKit Terrain Pack cliffs/rocks)
- `192` = wetland (reeds, puddles, sparse trees)
- `255` = village (reserved — the existing village tile at tile 10,10)

### Phase 4 — KayKit prop spawn pool

**New file**: `Assets/_Modules/World/PropSpawnPool.cs`

```csharp
/// <summary>
/// Object pool for KayKit environment props. Maintains per-prefab pools;
/// returns a dormant instance (SetActive false) on release so the GPU
/// never drops + re-uploads the mesh.
///
/// Prop budget per tile: 8–24 objects depending on biome density.
/// All props share a single KayKit atlas material → 1 draw call per tile.
/// </summary>
public sealed class PropSpawnPool : MonoBehaviour
{
    [SerializeField] private PropPoolEntry[] _entries;  // prefab + pool size
    private readonly Dictionary<string, Queue<GameObject>> _pools = new();

    public GameObject Get(string propId, Vector3 pos, float yRot, float scale) { ... }
    public void Release(GameObject go) { ... }
}
```

**KayKit props to include from catalog** (mobile-friendly, low poly):

| Biome | Props | Max per tile |
|---|---|---|
| Grassland | `Bush_Small_A/B`, `Flower_A/B`, `Grass_Patch_A` | 12 |
| Forest | `Tree_Oak_A/B/C`, `Tree_Pine_A/B`, `Bush_Large_A`, `Mushroom_A/B` | 10 |
| Highlands | `Rock_A/B/C/D`, `Hill_Cliff_B_Side`, `Dead_Tree_A` | 8 |
| Wetland | `Reed_A/B`, `Lily_A`, `Tree_Willow_A`, `Rock_B` | 12 |

All KayKit models already import at ≤ 500 tris each. Full 9-tile window = ≤ 216
prop objects, all using the KayKit shared atlas = **1 draw call**.

### Phase 5 — Distance fog (hide unloaded tiles)

Add a URP Volume override to the village scene:

```csharp
// In ExteriorTerrainBuilder (or a new WorldFogSetup.cs):
var fog = volume.sharedProfile.Add<UnityEngine.Rendering.Universal.Fog>(true);
fog.enabled.value = true;
fog.color.value = new Color(0.55f, 0.62f, 0.45f);   // warm olive — matches terrain
fog.start.value = 90f;   // starts fading 90 wu from camera
fog.end.value = 160f;    // fully opaque at 160 wu (beyond 3-tile radius)
fog.fogDensity.value = 0.04f;
```

Tiles outside the loaded window are hidden by fog before they could be seen.
No pop-in artefact.

### Phase 6 — Partial NavMesh per tile

The existing village NavMesh bakes the full 300×300 terrain once. Switching to
tile meshes requires **NavMesh surface per tile** or **NavMesh links** between
tiles.

Use **Unity's NavMesh components package** (already a Unity 6 built-in):

```csharp
// On each WorldTile when activated:
var surface = tileGo.AddComponent<NavMeshSurface>();
surface.collectObjects = CollectObjects.Children;
surface.layerMask      = LayerMask.GetMask("Walkable");
surface.useGeometry    = NavMeshCollectGeometry.RenderMeshes;
surface.BuildNavMesh();  // ~0.3 ms for a 50×50 wu flat tile at runtime
```

NavMesh data per tile ≈ 15–60 KB; 9 tiles ≈ 0.5 MB — well within budget.
Enemies spawned outside the village use the tile's NavMesh surface; village
enemies continue using the existing baked surface.

---

## Integration with Existing Systems

| System | Change |
|---|---|
| `ExteriorTerrainBuilder` | Retain for the 300×300 **village tile** (tile 10,10). New WorldMapBuilder handles the outer 399 tiles. |
| WO-33 boundary walls | Keep ±142 wu walls — they guard the village tile. Remove them from world tiles (no walls between world zones). |
| Enemy spawner | Keep spawning enemies on the village tile. World tile enemies spawn from `EnemySpawnPool` per zone (Wave 8+ feature). |
| HeroLocomotion | No change — WASD already works in world space. |
| `VillageCamera` | Increase camera far clip from current value to ≥ 200 wu to see full fog gradient. |

---

## File Summary

| File | Action |
|---|---|
| `Assets/Editor/WorldMapBuilder.cs` | **New** — bake world manifest + tile JSONs |
| `Assets/_Modules/World/WorldTileStreamer.cs` | **New** — runtime tile load/unload |
| `Assets/_Modules/World/WorldTile.cs` | **New** — tile data model + mesh lifecycle |
| `Assets/_Modules/World/PropSpawnPool.cs` | **New** — KayKit prop pooling |
| `Assets/StreamingAssets/Data/WorldMap/world-manifest.json` | **New** |
| `Assets/StreamingAssets/Data/WorldMap/biome-map.png` | **New** (20×20 biome palette) |
| `Assets/Editor/ExteriorTerrainBuilder.cs` | **Edit** — restrict to village tile only |

---

## Acceptance Criteria

- [ ] Hero can walk >150 wu from the village centre without hitting a void
- [ ] KayKit props (trees, rocks, bushes) visible in all biomes beyond the village walls
- [ ] Frame time ≤ 33 ms (30 fps) on the target mobile device while crossing a tile boundary
- [ ] GPU memory stays ≤ 150 MB while 9 tiles are loaded
- [ ] Distance fog hides unloaded tile edges cleanly (no hard pop-in)
- [ ] NavMesh on each tile allows NavMeshAgent enemies to path correctly
- [ ] Village interior unaffected — existing village scene re-bake not required by this WO

---

## Week Gating

| Phase | Target week |
|---|---|
| Phase 1 — Data model + WorldMapBuilder editor tool | Week 8 |
| Phase 2 — Runtime streamer (load/unload 3×3 window) | Week 8 |
| Phase 3 — Biome tile mesh | Week 9 |
| Phase 4 — KayKit prop pool | Week 9 |
| Phase 5 — Distance fog | Week 9 |
| Phase 6 — Partial NavMesh | Week 10 |

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
