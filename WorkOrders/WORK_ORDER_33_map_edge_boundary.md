# WORK ORDER 33 — Map Edge Boundary (Invisible Walls + Visual Barrier)

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-26
**Author:** Bug triage — playtest screenshot
**Priority:** High — hero can walk off the terrain edge into the black void; enemies
              have no boundary either

---

## Problem

The 300×300 world-unit exterior terrain has a hard, unguarded edge. The hero (and
NavMeshAgent enemies) can walk past the terrain boundary into a black void with no
floor. Screenshot shows the hero standing at the terrain drop-off looking back at
the village, with pure black below.

Two things are needed:
1. **Invisible collider walls** that physically stop the hero and enemies at the
   terrain perimeter (immediate gameplay fix)
2. **Visual barrier** that makes the boundary readable — player should see "this is
   the edge of the world" and not be surprised by the wall

---

## Terrain Dimensions

From `ExteriorTerrainBuilder.cs`:
```csharp
private const float TerrainSizeXZ = 300f;   // terrain is 300×300 wu centred at origin
```

Edge = ±150 units from centre (world X and Z). Invisible walls should sit at **±142 u**
(8 m inside the edge) so the visual cliff ring is flush with the terrain boundary and
the wall is hidden behind it.

---

## Fix — Part 1: Invisible Boundary Walls

Add to `ExteriorTerrainBuilder.BuildExterior()` a new call:
```csharp
BuildBoundaryWalls(root.transform);
```

Implement `BuildBoundaryWalls`:

```csharp
/// <summary>
/// Four tall invisible BoxColliders at the terrain perimeter. Stops the
/// hero's CapsuleCast (HeroLocomotion) and NavMeshAgent enemies from
/// leaving the playable area. 8 m inside the visual cliff ring so the
/// barrier is hidden behind the rocks.
/// </summary>
private static void BuildBoundaryWalls(Transform parent)
{
    const float HalfMap  = 142f;    // TerrainSizeXZ/2 - 8m margin
    const float WallH    = 40f;     // tall enough to catch any jump/slope
    const float WallD    = 4f;      // depth (inward) of the invisible slab
    const float WallLen  = 296f;    // span of each wall face (covers full edge)

    // Four sides: North (+Z), South (-Z), East (+X), West (-X)
    var walls = new[]
    {
        // (centre position,              size)
        (new Vector3(0,  WallH*0.5f,  HalfMap), new Vector3(WallLen, WallH, WallD)),
        (new Vector3(0,  WallH*0.5f, -HalfMap), new Vector3(WallLen, WallH, WallD)),
        (new Vector3( HalfMap, WallH*0.5f, 0),  new Vector3(WallD, WallH, WallLen)),
        (new Vector3(-HalfMap, WallH*0.5f, 0),  new Vector3(WallD, WallH, WallLen)),
    };

    var root = new GameObject("BoundaryWalls");
    root.transform.SetParent(parent, false);

    foreach (var (centre, size) in walls)
    {
        var go = new GameObject("BoundaryWall");
        go.transform.SetParent(root.transform, false);
        go.transform.position = centre;
        var col = go.AddComponent<BoxCollider>();
        col.size = size;
        col.center = Vector3.zero;
        // Layer: "BoundaryWall" (or Default) — enemies + hero collide with it,
        // but it's invisible and never has a Renderer.
    }
}
```

**NavMesh boundary**: the Unity Terrain's built-in NavMesh area stops at the terrain
edge automatically. The invisible walls are for the hero's `CapsuleCast`-based movement
and any kinematic enemies. No extra NavMesh work needed — `NavMeshAgent` enemies
hitting an out-of-NavMesh area will already stop.

---

## Fix — Part 2: Visual Barrier (KayKit Cliff Ring)

The KayKit Terrain Pack includes modular cliff pieces:
```
Assets/Models/KayKit/*/Hill_Cliff_A_InnerCorner_Color*.fbx
Assets/Models/KayKit/*/Hill_Cliff_B_Side_Color*.fbx
```

Use `Hill_Cliff_B_Side` pieces tiled around the perimeter at radius **~148 u** to
create a visual "world's edge" cliff wall. The terrain heightmap already drops at
the edge — the cliff props cap it visually.

```csharp
private static void BuildEdgeCliffRing(Transform parent, TerrainData td)
{
    const float Radius   = 148f;
    const float TileSize = 8f;       // each Hill_Cliff_B_Side piece is ~8 wu wide
    const int   Sides    = 4;        // N/S/E/W walls

    // Load one cliff piece — Color3 (grey stone) blends with all biomes
    var model = LoadKayKitModel("Hill_Cliff_B_Side_Color3.fbx");
    if (model == null) { Debug.LogWarning("[ExteriorTerrainBuilder] Cliff ring model not found — boundary is invisible only."); return; }

    var ring = new GameObject("EdgeCliffRing");
    ring.transform.SetParent(parent, false);

    // North + South walls (run along X)
    for (float x = -Radius; x < Radius; x += TileSize)
    {
        PlaceCliffTile(ring.transform, model, new Vector3(x + TileSize*0.5f, 0f,  Radius), 0f);
        PlaceCliffTile(ring.transform, model, new Vector3(x + TileSize*0.5f, 0f, -Radius), 180f);
    }
    // East + West walls (run along Z)
    for (float z = -Radius; z < Radius; z += TileSize)
    {
        PlaceCliffTile(ring.transform, model, new Vector3( Radius, 0f, z + TileSize*0.5f), 90f);
        PlaceCliffTile(ring.transform, model, new Vector3(-Radius, 0f, z + TileSize*0.5f), 270f);
    }
    // Corners: use Hill_Cliff_A_InnerCorner pieces
    var corner = LoadKayKitModel("Hill_Cliff_A_InnerCorner_Color3.fbx");
    if (corner != null)
    {
        PlaceCliffTile(ring.transform, corner, new Vector3( Radius, 0f,  Radius),   0f);
        PlaceCliffTile(ring.transform, corner, new Vector3(-Radius, 0f,  Radius),  90f);
        PlaceCliffTile(ring.transform, corner, new Vector3(-Radius, 0f, -Radius), 180f);
        PlaceCliffTile(ring.transform, corner, new Vector3( Radius, 0f, -Radius), 270f);
    }
}
```

The cliff pieces provide their own mesh colliders — strip or keep them
(they're fine as additional visual collision support alongside the invisible wall).

---

## Fix — Part 3: Terrain Edge Height Dropoff

The terrain heightmap already descends toward the edges (biome elevation is applied
outward from the village plateau). Ensure the outer 10 u of terrain drops to below
Y=-5 so crossing the invisible wall is visually impossible even if the cliff ring
is missing:

```csharp
// Already exists in BuildHeightmap() via SampleBiomeHeight() — verify the
// outer edge height clamps below -5. If not, add:
if (worldX > 140f || worldX < -140f || worldZ > 140f || worldZ < -140f)
    biomeY = Mathf.Min(biomeY, -5f);
```

---

## Files to Edit

- `Assets/Editor/ExteriorTerrainBuilder.cs`
  - `BuildExterior()` — call `BuildBoundaryWalls(root.transform)` and `BuildEdgeCliffRing(root.transform, terrainData)` after terrain is created
  - Add `BuildBoundaryWalls()` method (§Fix 1)
  - Add `BuildEdgeCliffRing()` and `PlaceCliffTile()` helper (§Fix 2)
  - `BuildHeightmap()` — add outer-edge height clamp (§Fix 3)

---

## Acceptance Criteria

- [ ] Hero walks toward the terrain edge and is stopped cleanly — no void visible
- [ ] A ring of cliff/rock props visually marks the world boundary
- [ ] Enemies cannot path outside the terrain (NavMesh ends at terrain edge; BoundaryWall stops any edge cases)
- [ ] No visible seam where the invisible wall sits (cliff ring covers it)
- [ ] **Owner-gated re-bake required**: Defenders > Week 3 > Build Exterior after code change
