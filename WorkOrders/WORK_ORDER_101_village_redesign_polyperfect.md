# WORK ORDER 101 — Village Rebuild: Polyperfect Low-Poly Asset Swap

**Status:** CLOSED — SUPERSEDED (owner-approved sweep 2026-08-09: Village.unity deleted; merged world + player-built town)
**Date:** 2026-05-29
**Priority:** High — file size blocker for Seeker APK + visual polish
**Scope:** Medium — mesh-only swap in `VillageSceneBuilder.cs`. No gameplay code changes.
**Catalog reference:** `docs/polyperfect-asset-catalog.md`
**Pipeline reference:** `PIPELINE_STATE.md` §4 (Village — "building swap to polyperfect = SPEC/ready")

---

## Goal

Replace every heavy Tripo mesh in the village with a polyperfect Low Poly Ultimate Pack
equivalent. No gameplay systems are touched. This is a pure art/environment swap that:

- Reduces build size by an estimated **400–500 MB**
- Makes the Seeker APK viable (current Tripo meshes exceed D3D12 upload buffer)
- Establishes a consistent low-poly art style across Village + Dungeon scenes
- Uses assets already imported and gitignored at `Assets/polyperfect/`

---

## Reconciliation rule

**Check `PIPELINE_STATE.md` and `docs/polyperfect-asset-catalog.md` before touching
any file.** The store, animation, and wave systems are already built — this WO only
touches `VillageSceneBuilder.cs` and the scene assets it references.

---

## Current state (what to replace)

| Building (code name) | Current mesh | Size | Replace with |
|---|---|---|---|
| Crystal Mine | Tripo mine mesh | ~29 MB | `SM_House_Medieval_Small` + `SM_Well` |
| Pet House | Tripo PetHome | ~54 MB | `SM_Stables_Medieval` |
| Arcane Tower | Tripo tower | ~29 MB | `SM_Tower_Medieval_Big` |
| Workshop | Tripo forge | ~29 MB | `SM_House_Medieval_Medium` |
| Farm | Tripo farm | ~29 MB | `SM_Farm_House` + `SM_Windmill_Medieval` |
| Market / Interactable | Tripo market | ~29 MB | `SM_House_Medieval_Large` + `SM_Marketplace_Stand_Simple` |
| Cathedral / Heart | Tripo Cathedral | ~84 MB | Keep existing Elarion tree (already replaced per FIX_NOTES) |
| Wall perimeter | placeholder/Tripo | varies | `SM_Wall_Medieval_Stone` segments + towers |
| Gates (4×) | placeholder | varies | `SM_Gate_Medieval_Medium` (main) + `SM_Gate_Medieval_Small` (×3) |

---

## Layout spec

### Arena dimensions
- Interior: 84 × 66 m (per WO-26 spec)
- Heart of Elarion: centre `(0, 0, 0)`
- Wall perimeter: 42 × 33 m ring, wall segments every 3 m

### Wall & gate placement

```
North wall:   z = +33  → Wall_Medieval_Stone segments
South wall:   z = -33  → Wall_Medieval_Stone + Gate_Medieval_Medium (main entrance at x=0)
East wall:    x = +42  → Wall_Medieval_Stone + Gate_Medieval_Small (x=42, z=0)
West wall:    x = -42  → Wall_Medieval_Stone + Gate_Medieval_Small (x=-42, z=0)

Corner towers (Tower_Castle_Round):
  NE: (+42, 0, +33)
  NW: (-42, 0, +33)
  SE: (+42, 0, -33)
  SW: (-42, 0, -33)

Wall towers (Tower_Medieval_Wood — mid-wall watchtowers):
  N mid:  (0, 0, +33)
  E mid:  (+42, 0, 0)
  W mid:  (-42, 0, 0)

Main gate:   (0, 0, -33)  → Gate_Medieval_Medium + Drawbridge_Medieval
Side gates:  (±42, 0, 0), (0, 0, +33) → Gate_Medieval_Small
```

### Building placement

```
Crystal Mine:   (-20, 0, +15)  → House_Medieval_Small + Well (2 m south of building)
Pet House:      (+20, 0, +15)  → Stables_Medieval
Arcane Tower:   (-20, 0, -15)  → Tower_Medieval_Big
Workshop:       (+20, 0, -15)  → House_Medieval_Medium
Farm:           (0, 0, +25)    → Farm_House + Windmill_Medieval (5 m east)
Market:         (0, 0, -20)    → House_Medieval_Large + Marketplace_Stand_Simple (×2 flanking)
Dungeon portal: (-8, 0, -30) and (+8, 0, -30) → Gate_Medieval_Small as portal arch frame
```

### Plaza & ground

```
Heart plaza:         Floor_Stone_3x3m_A tiles, 12×12 m centred on (0,0,0)
Main road (S gate):  Stone_Brick path from (0,0,-33) to (0,0,-6)   → 3 m wide
Cross road (E–W):    Stone_Brick path from (-20,0,0) to (+20,0,0)  → 3 m wide
Outer ground:        Terrain_Plane_Plain filling interior
Approach (outside):  Ground_Cracked_Dirt from each gate, 40 m out (enemy spawn lane)
Slope at walls:      Terrain_Plane_Slope1 along wall exterior base
```

---

## Prop dressing (per building)

### Crystal Mine
```
SM_Well                  position: (-20, 0, +13)
SM_Torche_Wall (×2)      on mine building walls
SM_Rock_Large (×2)       scattered south of building
SM_Timber                leaning against wall
SM_Stone_Big (×3)        ground scatter
```

### Pet House / Stables
```
SM_Fence_Stone           perimeter ring, radius 6 m
SM_Hay_Pile (×2)         inside fence
SM_Bucket_Milk           near stable door
SM_Torche_Wall (×2)      on building walls
SM_Horse or SM_Dog       ambient animal
```

### Arcane Tower
```
SM_Torche_Wall (×4)      one per tower face
SM_Statue_Knight (×2)    flanking tower entrance
SM_Candlestick (×2)      base of tower
SM_Book_Open             on a rock near entrance
SM_Pillar_Ionic (×2)     framing the tower door
```

### Workshop
```
SM_Anvil                 outside front door
SM_Wheelbarrow           left of entrance
SM_Crate_Box (×3)        stacked against wall
SM_Torche_Wall (×2)      on building walls
SM_Table_Crafting_Wood   visible through window
SM_Hammer                leaning on wall
```

### Farm
```
SM_Farm_Silo             10 m east of Farm_House
SM_Windmill_Medieval     15 m east (separate GO, animated if desired)
SM_Scarecrow             in field area
SM_Haystack (×2)         field scatter
SM_Farm_Flower_Bed (×3)  near house
SM_Fence_Picket          field border, 3 sides
SM_Hen / SM_Cow          ambient animals
```

### Heart of Elarion Plaza
```
SM_Altar                 at base of tree (0, 0, 2)
SM_Candlestick (×4)      compass points around altar
SM_Candle_Big (×4)       closer ring
SM_Rock_Pillar (×6)      standing stones in outer ring, radius 8 m
SM_Fountain              at (0, 0, 6) — south of altar on the main road
SM_Statue_Knight (×2)    flanking north path to altar
```

---

## Nature dressing

```
Trees (ring outside walls, every 8–10 m):
  - Tree_Oak (primary — warm canopy)
  - Tree_Conifer (north wall — darker, denser)
  - Tree_Birch (east/west — lighter, near gates)
  - Tree_Dead (northwest corner — corrupted land flavor)
  - Tree_Dead_Log_A/B (fallen logs on enemy approach roads, south + east)

Rock scatter (outside walls on enemy approaches):
  - Rock_Sharp (×6) — jagged rocks flanking south approach
  - Rock_Large (×4) — boulders at NE and NW approach corners
  - Stone_Round (×8+) — path-edge dressing throughout

Interior courtyard:
  - Bush_Medium (×4) near plaza corners
  - Bush_Small (×6) building yard scatter
  - Bench_Wood (×4) near market and plaza
```

---

## Implementation steps for CLI

### Step 1 — Update `VillageSceneBuilder.cs` building placements

For each of the 5 gameplay buildings, update the mesh reference in
`VillageSceneBuilder.BuildBuildings()` (or equivalent method):

```csharp
// Replace Tripo resource-load path with polyperfect path
// Old pattern: Resources.Load<GameObject>("TripoStructures/PetHome")
// New pattern: Resources.Load<GameObject>("polyperfect/SM_Stables_Medieval")
// OR: use direct AssetDatabase path at build time via Editor builder
```

The safest approach is to update the builder to reference the polyperfect
prefabs from `Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/` —
use the `_M` prefabs which have pre-configured import settings.

### Step 2 — Build wall perimeter

Add `BuildWallPerimeter()` method to `VillageSceneBuilder.cs`:

```csharp
private void BuildWallPerimeter()
{
    // North/South walls: place Wall_Medieval_Stone every 3m along z=±33
    // East/West walls:   place Wall_Medieval_Stone every 3m along x=±42
    // Corner towers:     Tower_Castle_Round at (±42, 0, ±33)
    // Mid-wall towers:   Tower_Medieval_Wood at wall midpoints
    // Gates:             Gate_Medieval_Medium at south (0,0,-33)
    //                    Gate_Medieval_Small at east, west, north
    // Drawbridge:        Drawbridge_Medieval just outside south gate
}
```

### Step 3 — Update ground/terrain

Replace flat `Terrain.CreateTerrainGameObject` with polyperfect tile placement:
- `Terrain_Plane_Plain` for interior
- `Floor_Stone_3x3m_A` for central plaza (12×12 grid)
- `Stone_Brick` for the two main roads
- `Ground_Cracked_Dirt` for approach lanes outside each gate (40 m)

### Step 4 — Add prop dressing

Add `DressBuilding(BuildingType, Vector3 position)` helper that places
the prop set listed above for each building type.

### Step 5 — Remove Tripo references

After verifying the new build renders correctly, remove all `TripoStructures/`
resource loads and delete the Tripo mesh FBX files from the project.
This is the file-size payoff step — do NOT do this before Step 4 is verified.

### Step 6 — Rebuild + verify

Run `Defenders → Build → Windows x64 Player` and confirm:
- Village loads without `level3 corrupted` crash
- All 5 buildings visible with correct meshes
- Wall perimeter encloses the arena correctly
- No purple/magenta materials (polyperfect uses atlas — should be fine)
- No D3D12 upload buffer warnings in Player.log

---

## Materials

Polyperfect ships with a single atlas material. For URP:
1. Import `URP_LowPolyUltimatePack.unitypackage` (already in pack root)
2. This creates the URP-compatible `PolyperfectMaterial` with the atlas texture
3. Assign to all polyperfect mesh renderers — one material, zero draw-call overhead

---

## Acceptance criteria

- [ ] Village builds and loads without crash (no `level3 corrupted` error)
- [ ] All 5 gameplay buildings visible using polyperfect meshes
- [ ] Wall perimeter encloses the arena with correct gate positions
- [ ] No Tripo mesh references remaining in `VillageSceneBuilder.cs`
- [ ] Player.log shows no D3D12 upload buffer warnings
- [ ] Build size reduced by at least 300 MB vs. current Tripo build
- [ ] Heart of Elarion (existing tree) intact — not replaced
- [ ] All building interactables (`MarketplaceInteractor`, store triggers) still fire
- [ ] WaveManager spawn points align with the approach lanes outside gates
- [ ] NavMesh bake covers full interior + 40 m approach corridors per gate

---

## Files to edit

| File | Action |
|---|---|
| `Assets/Editor/VillageSceneBuilder.cs` | Edit — swap building meshes, add wall builder, update ground |
| `Assets/polyperfect/` (existing) | Read — pull prefabs from `_M/Prefabs_M/` |
| `Assets/Resources/TripoStructures/` | Delete (after verification) |
| `Assets/Scenes/Village.unity` | Rebuild via builder — do NOT hand-edit |
