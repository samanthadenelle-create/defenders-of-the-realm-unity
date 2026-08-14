# WORK ORDER 108 — Player Build Mode: Give the Player VillageSceneBuilder's Power

**Status:** DONE

> **DONE - verified in HEAD 2026-08-14 (phantom sweep).** The work is present at BuildModeController.cs:442/1851/1983/3255/3384 + SaveSchema.cs:321.
> Status had read READY because the landing commit did not flip this line in the same commit
> (CLAUDE.md §2), so the DERIVED board (BOARD.html) kept re-serving finished work.
> _Prior status line, preserved: Status: ⭐ READY TO IMPLEMENT — **TOP PRIORITY after the playtest regressions (WO-166)**_

**Date:** 2026-05-29 · **promoted to build-ready 2026-05-30**
**Priority:** NORTH STAR KEYSTONE — the CREATE verb. The single most important gap. **Build this NEXT**
once the village is playable. Per `VISION_GAP_ANALYSIS`: the bones are all built; THIS is the layer that
turns the pile of systems into the North Star game. Node settlements (WO-159), the territory loop, and
the async arena all unlock ON TOP of this.
**Scope:** Large — but mostly REUSE + one new data spine (see Build-Ready Update below).
**Depends on:** the primitives below (all verified built 2026-05-30).
**North Star reference:** NORTH_STAR.md §"The core verb that got lost: CREATE"

---

## ⭐ BUILD-READY UPDATE (2026-05-30) — reconcile + the delta this WO predates

The detailed design below (PlacementGrid / BuildModeController / palette / place-move-sell) is sound —
**keep it.** Since it was written, two things landed that change the build to **reuse, not greenfield**,
plus the load-bearing data model was specified. Read this section first, then the design below.

### Primitives now CONFIRMED built (verified in code 2026-05-30) — generalize, don't write fresh
| Need | Built | Reuse |
|---|---|---|
| Ghost placement (marker, snap, overlap, spend) | `TowerPlacementSystem` (`StartPlacing`/`SnapToGrid`/`IsValidSurface`/`CanPlace`/`PlaceTower`) | generalize `TowerData` → `CatalogEntry` |
| One creation path (data-driven) | `StructureFactory` + `CatalogRegistry` + `CatalogEntry` (WO-148, post-dates this WO) | the place/load `Create` call |
| Catalog data model (look ⊥ behavior + rules) | `Core/Catalog/*` (`CatalogType`/`RepoProps`/`PlacementRules`) | the buildable defs (use INSTEAD of a new `BuildableItem` type — reconcile to `CatalogEntry`) |
| Palette UI + cost gating + code-fallback | `BuildMenu` (`ShowCodeFallbackMenu`/`CrystalBalance`) | generalize to place/move/rotate/sell palette |
| Footprint + gate-clearance | `VillageSceneBuilder.AddBuildingFootprintCollider` / `ValidateBuildingGateClearance` | mirror on placed objects |

> ⚠ The design below invents `BuildableItem` + a fresh placement loop. **Reconcile:** drive the palette
> from `CatalogEntry`/`CatalogRegistry` and the placement from a generalized `TowerPlacementSystem` +
> `StructureFactory.Create` — do NOT fork a parallel catalog or placement system (WO-148 already built it).

### THE LOAD-BEARING NEW PIECE — the persisted base-layout data spine (verified MISSING in GameState)
This is the one real new thing; everything else is reuse. From `docs/build-mode-architecture.md` §2:
1. **`PlacedStructureData`** (`DeNelle.Village.BuildMode`, serializable): `{ string itemId; int cellX;
   int cellZ; int yawSteps(0..3); int level; }` — **grid cells + discrete yaw, NOT world transforms**
   (compact, snap-clean, server-replayable for async raids).
2. **`GameState.BaseLayout = List<PlacedStructureData>`** — one additive field. Bump `SaveSchema` v11→v12
   (nullable; old saves load empty), add the `v11→v12` migrator step (seed empty). **Coordinate the
   schema bump with the SaveMigrator owner.**
3. **`BaseLayoutLoader`** (runtime) — twin of `VillageSceneBuilder.BuildBuildings`: reads `BaseLayout`,
   `itemId → CatalogEntry → prefab` via `StructureFactory`, places at `grid.CellToWorld(cell)` +
   `yawSteps*90`, footprint collider, `PlacedStructure` component. **Empty `BaseLayout` → fall through to
   the default VillageSceneBuilder village (the seed).** First build-mode entry seeds `BaseLayout` from
   the default village so the player edits their familiar town, not a blank plot.

### Phasing (each shippable — architecture doc §6)
- **P0 — data spine:** `PlacedStructureData` + `GameState.BaseLayout` + v12 migration + `BaseLayoutLoader`.
  No UI. Village becomes data-driven (identical look). *The principle-#1 backbone.*
- **P1 — place + persist:** `PlacementGrid` (cell occupancy) + `BuildModeController.Enter/Exit` + palette
  + place + cost + save. Place-only, survives reload.
- **P2 — full edit verbs:** move / rotate / sell / upgrade + ghost polish + full validation.
- **P3 — server-authority seam (LATER):** keep placement validation pure/headless for async-raid re-verify.

### Critical constraints (the traps — these OVERRIDE any conflicting detail below)
- **Charge AFTER commit, ONE wallet** (WO-131): spend the canonical `GameState` wallet only on a committed
  valid placement; never a second/session balance. (See `RESOURCE_ECONOMY_DESIGN` Step 0 wallet-merge.)
- **NavMesh:** placed footprints = path blockers → use **NavMeshObstacle carving** (no per-place rebake on
  mobile); gate-clearance guarantees a spawn→Heart lane. Rebake-on-Exit only if carving is too heavy.
- **Spend from the PERSISTED wallet** (`GameState`), not the session-only `EconomyService` mirror.
- **Code-built UI, NO UXML** (repo rule; `BuildMenu` has the code-fallback already).
- **Do NOT edit `VillageSceneBuilder`** — it stays the default-village seed; build mode replaces its
  *output* at runtime when a `BaseLayout` exists. Do NOT fork catalog/placement (WO-148 built them).
- **Mobile:** 3m cells + discrete 90° rotation; one-finger place, two-finger cam (Lean.Touch driver).

---

---

## The gap (from NORTH_STAR.md)

> *"The village today is builder-generated (VillageSceneBuilder authors a fixed layout).
> That is the inverse of the vision. The north star is to hand that build power to the
> player. The primitives already exist (BuildMenu places buildings, walls are modular,
> there's a plot/grid); a CoC-style build mode is essentially 'let the player do what
> VillageSceneBuilder does.'"*

The fixed layout is the right scaffold for now — but Build Mode is where the game becomes
**the player's base**, not the developer's demo.

---

## Goal

A dedicated Build Mode that the player enters from the village (tap the hammer icon or
a "Build" button on the HUD). In Build Mode:
- A palette of structures appears (walls, towers, gates, mines, decorations)
- The player taps a structure to select it, taps the ground to place it
- Placed structures persist (saved to GameState)
- The player can move or sell (remove) existing structures
- Exiting Build Mode returns to the village with the new layout baked in

This is CoC's village edit screen. The village becomes the player's canvas.

---

## Architecture

### Build Mode entry/exit

```
Village HUD → tap Build icon → BuildModeController.Enter()
    → freeze WaveManager (no waves during building)
    → camera pulls back to top-down overview
    → show BuildPalette UI
    → enable grid overlay

BuildPalette → player selects structure → ghost preview follows finger/mouse
Player taps valid tile → PlaceStructure(prefab, gridPos)
    → instantiate structure
    → register in PlacementGrid
    → save to GameState.PlacedStructures[]

Exit Build Mode → BuildModeController.Exit()
    → hide palette
    → restore camera
    → resume WaveManager
```

---

## 1. `PlacementGrid.cs`

**Path:** `Assets/_Modules/Village/BuildMode/PlacementGrid.cs`

Manages a 2D grid of occupied/free cells. Grid cell size = 3m (matches polyperfect
modular wall segments). Grid bounds = village interior (84×66m = 28×22 cells).

```csharp
namespace DeNelle.Village
{
    public class PlacementGrid : MonoBehaviour
    {
        public static PlacementGrid Instance { get; private set; }

        public float cellSize = 3f;
        public int   gridWidth  = 28;   // 84m / 3m
        public int   gridHeight = 22;   // 66m / 3m

        private bool[,] _occupied;

        public bool CanPlace(Vector2Int cell, Vector2Int footprint);
        public void Occupy(Vector2Int cell, Vector2Int footprint, string structureId);
        public void Free(Vector2Int cell, Vector2Int footprint);
        public Vector3 SnapToGrid(Vector3 worldPos);
        public Vector2Int WorldToCell(Vector3 worldPos);
        public Vector3 CellToWorld(Vector2Int cell);

        /// <summary>Shows/hides the grid overlay mesh.</summary>
        public void SetGridVisible(bool visible);
    }
}
```

---

## 2. `BuildModeController.cs`

**Path:** `Assets/_Modules/Village/BuildMode/BuildModeController.cs`

```csharp
namespace DeNelle.Village
{
    public class BuildModeController : MonoBehaviour
    {
        public static BuildModeController Instance { get; private set; }

        public bool IsActive { get; private set; }

        [Header("Camera")]
        public float buildModeHeight  = 35f;   // Camera Y in build mode
        public float normalHeight     = 18f;

        [Header("References")]
        public BuildPaletteUI paletteUI;
        public PlacementGrid  grid;

        public void Enter();   // Freeze waves, pull camera back, show palette
        public void Exit();    // Save layout, restore camera, resume waves

        public void BeginPlace(BuildableItem item);   // Start ghost preview
        public void ConfirmPlace(Vector3 worldPos);   // Snap + place
        public void CancelPlace();
        public void SelectExisting(PlacedStructure s); // Tap to move/sell
        public void SellSelected();
        public void MoveSelected(Vector3 newPos);
    }
}
```

---

## 3. `BuildPaletteUI.cs`

**Path:** `Assets/_Modules/Village/BuildMode/BuildPaletteUI.cs`

Code-built UI (no UXML — builds in memory). Scrollable horizontal strip of buildable
items at the bottom of the screen. Each card shows: icon, name, crystal cost.

```csharp
namespace DeNelle.Village
{
    public class BuildPaletteUI : MonoBehaviour
    {
        [System.Serializable]
        public struct BuildableItem
        {
            public string       id;
            public string       displayName;
            public Sprite       icon;
            public GameObject   prefab;
            public Vector2Int   footprint;   // cells (e.g. wall = 1×1, tower = 2×2)
            public int          crystalCost;
        }

        public BuildableItem[] items;

        public void Show();
        public void Hide();
        // Raises BuildModeController.BeginPlace(item) on tap
    }
}
```

**Default palette contents** (expand over time):
| Item | Prefab | Footprint | Cost |
|---|---|---|---|
| Stone Wall | SM_Wall_Stone_3x3_A | 1×1 | 25 crystals |
| Round Tower | SM_Tower_Castle_Round | 2×2 | 150 crystals |
| Watchtower | SM_Tower_Medieval_Wood | 1×2 | 80 crystals |
| Gate | SM_Gate_Medieval_Small | 2×1 | 100 crystals |
| Crystal Mine | SM_House_Medieval_Small | 2×2 | 200 crystals |
| Archery Tower | SM_Tower_Medieval_Big | 2×2 | 120 crystals |

---

## 4. `PlacedStructure.cs`

**Path:** `Assets/_Modules/Village/BuildMode/PlacedStructure.cs`

Runtime component on every player-placed GameObject. Stores metadata for
save/load and the sell/move flow.

```csharp
namespace DeNelle.Village
{
    public class PlacedStructure : MonoBehaviour
    {
        public string      itemId;
        public Vector2Int  gridCell;
        public Vector2Int  footprint;
        public int         sellValue;   // crystals returned on sell (50% of cost)

        public PlacedStructureData ToSaveData();
        public static PlacedStructure FromSaveData(PlacedStructureData d);
    }

    [System.Serializable]
    public struct PlacedStructureData
    {
        public string  itemId;
        public int     cellX;
        public int     cellZ;
        public float   yawDeg;
    }
}
```

---

## 5. GameState integration — persist the layout

In `GameState.cs`, add:

```csharp
public List<PlacedStructureData> PlacedStructures = new();
```

On `BuildModeController.Exit()`:
```csharp
GameStateService.Instance.State.PlacedStructures = grid.GetAllPlacements();
GameStateService.Instance.Save();
```

On village load (after wave setup):
```csharp
foreach (var data in GameStateService.Instance.State.PlacedStructures)
    BuildModeController.Instance?.RestoreFromSave(data);
```

---

## 6. Ghost preview

When the player selects an item from the palette, a semi-transparent ghost of the
prefab follows the cursor/finger. Color: green = valid placement, red = blocked/occupied.

```csharp
// GhostPreview.cs
public class GhostPreview : MonoBehaviour
{
    public void SetPrefab(GameObject prefab);
    public void MoveTo(Vector3 snappedWorldPos);
    public void SetValid(bool valid);   // Green/red tint via MaterialPropertyBlock
    public void Hide();
}
```

---

## Wall-tier integration (WO-109 hook)

Build Mode is where wall tiers become visible. The palette shows the tiers the player
has unlocked: Wood Wall (default) → Stone Wall (unlocked after wave 3) → Reinforced
(unlocked after wave 7). This is the spend driver: players upgrade their walls with
harvest resources.

---

## Files to Create

| File | Path |
|---|---|
| `PlacementGrid.cs` | `Assets/_Modules/Village/BuildMode/` |
| `BuildModeController.cs` | `Assets/_Modules/Village/BuildMode/` |
| `BuildPaletteUI.cs` | `Assets/_Modules/Village/BuildMode/` |
| `PlacedStructure.cs` | `Assets/_Modules/Village/BuildMode/` |
| `GhostPreview.cs` | `Assets/_Modules/Village/BuildMode/` |

**Edit (minimal):**
- `GameState.cs` — add `PlacedStructures` list
- `GameStateService.cs` — save/load `PlacedStructures`

**Do NOT touch:** VillageSceneBuilder, Village.unity, WaveManager, any ATB/monetization.

---

## Acceptance Criteria

- [ ] Tapping "Build" on the HUD enters Build Mode — waves pause, camera pulls back
- [ ] Palette shows at least 4 buildable items with icons and costs
- [ ] Selecting an item shows a ghost that follows the cursor/finger
- [ ] Ghost turns green on valid tiles, red on occupied/out-of-bounds
- [ ] Tapping a valid tile places the structure and deducts crystals
- [ ] Placed structures persist across sessions (saved to GameState)
- [ ] Tapping a placed structure highlights it + shows Move/Sell options
- [ ] Sell returns 50% crystal cost
- [ ] Exiting Build Mode restores camera and resumes wave system
- [ ] All placed structures survive a scene reload (loaded from GameState on start)
