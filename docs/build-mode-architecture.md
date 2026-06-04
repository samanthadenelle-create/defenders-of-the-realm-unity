# Build Mode Architecture (WO-108) — the CREATE verb

> Implementation architecture for player base-building: the player places/arranges
> their own walls, towers, mines, and buildings on their plot, then defends what they
> built. This is the inverse of today's `VillageSceneBuilder` (designer-authored layout) —
> **build mode is "let the player do what VillageSceneBuilder does."**
>
> Grounded in `docs/NORTH_STAR.md` (the CREATE verb) and `docs/ARCHITECTURE_NORTH_STAR.md`
> principle #1 (data-driven, not hand-authored) and #2 (server-authoritative for persistent
> state). This doc is implementation-grade for a CLI session; it reconciles with existing
> infra and never blind-replaces it.

---

## 1. Reconciliation — what already exists vs the gap

The primitives WO-108 calls for are **already half-built**. Build mode is a thin data-editing
layer over them, not a new placement engine.

| Capability | Already exists | Where | Gap |
|---|---|---|---|
| Ghost-marker placement | **Yes** — green/red ghost, snap-to-grid, overlap test, spend | `Buildings/TowerPlacementSystem.cs` | Generalize from `TowerData`-only to any buildable; add rotate/footprint |
| Snap-to-grid | **Yes** — `SnapToGrid(hit)` rounds to `_gridSize` | `TowerPlacementSystem` | Promote to a shared grid with cell occupancy |
| Overlap / clearance validation | **Yes** — `OverlapSphereNonAlloc` vs Tower/Building layers; gate-clearance guard | `TowerPlacementSystem.CanPlace`, `VillageSceneBuilder.ValidateBuildingGateClearance` | Reuse both as the placement validity rules |
| Footprint colliders (path-around AABB) | **Yes** — computed from mesh bounds | `VillageSceneBuilder.AddBuildingFootprintCollider`, `Building.EnsureBlocker` | Reuse verbatim on player-placed objects |
| Build palette UI + cost gating | **Yes** — card list, affordability, crystal spend, code-fallback menu | `Buildings/UI/BuildMenu.cs` | Add a place/move/rotate/sell palette mode |
| Buildable definitions | **Yes** — `BuildingDef`/`BuildingCatalog` (data-driven from `buildings.json`) | `Building.Configure(BuildingDef)` | Extend catalog to walls/towers/mines; add footprint + sell value |
| Economy (multi-resource spend) | **Yes** — `EconomyService.CanAfford/TrySpend/Grant` | `Village/EconomyService.cs` | Use for build/sell; note it's **session-only** today |
| Prefab catalog (the palette art) | **Yes** — polyperfect `_M` Medieval prefabs, mobile-light | `VillageSceneBuilder.PolyMedievalDir` etc. | Catalog them as `BuildableDef` entries by asset path |
| Persisted state | **Yes** — versioned 41-field save (v11) + migrator | `Core/State/GameState.cs`, `SaveSchema.cs`, `SaveMigrator.cs` | **The real gap: persist a player layout (see §2)** |

**The single real gap is the data model + its persistence** — there is no serialized
"this is the player's base layout" today. Everything else is reuse/generalization.

> ⚠ **Hard constraint — do NOT edit `VillageSceneBuilder.cs`.** It is the designer-authored
> *default* layout and another agent owns it. Build mode reads from it (footprint-collider
> helper, gate-clearance constants) only as a *pattern to mirror in new files*, never as a
> dependency to modify. The default village stays the seed layout a new player starts from.

---

## 2. The data model — a base layout IS data (principle #1)

This is the load-bearing decision. A player base is a **serializable list of placed objects**,
and the builder/loader *instantiates the scene from that data*. Build mode is a UI that edits
this list — nothing more. Get this right and ladder rungs 4–6 are content, not rewrites.

```csharp
// New: Assets/_Modules/Village/BuildMode/PlacedStructureData.cs  (plain serializable data)
[System.Serializable]
public struct PlacedStructureData
{
    public string itemId;   // BuildableDef id — resolves to a prefab + footprint + cost
    public int    cellX;    // grid cell, NOT raw world pos (grid-relative = portable + replayable)
    public int    cellZ;
    public int    yawSteps; // 0..3 — 90deg rotation steps (keep rotation discrete for snapping)
    public int    level;    // upgrade tier (walls wood->stone->reinforced, tower L1..n)
}
```

Store grid **cells + discrete yaw**, not world transforms — it is compact, snaps cleanly,
survives a grid-origin change, and (critically) is **replayable on a server** for async raids
(principle #3). World position is always derivable via `grid.CellToWorld(cell)`.

### Where it saves

The save layer is a **versioned, React-mirrored 41-field schema** (`SaveSchema.CurrentVersion = 11`,
`PlayerPrefs` key `dotr-save`, with a `SaveMigrator`). Adding the layout is a schema change, so:

1. Add `public List<PlacedStructureData> BaseLayout = new();` to `GameState.cs` (one new field —
   minimal, additive, the WO already greenlights this edit).
2. Bump `SaveSchema.CurrentVersion` to 12 and add the field to `PersistedState` (nullable, so
   old saves deserialize with a null/empty layout).
3. Add a `v11 -> v12` step in `SaveMigrator` that seeds `BaseLayout` empty (existing players
   keep the default `VillageSceneBuilder` village until they first enter build mode and save).

> **Server-authority seam (principle #2).** `BaseLayout` is exactly the payload that becomes
> **server-authoritative** when async raids land: the server stores the canonical layout, the
> client renders + predicts, raids run against a *snapshot* of this same struct list. Designing
> the layout as a flat, prefab-id-keyed, grid-relative data list now means the PvP rung is
> additive — the server just persists and validates the same `List<PlacedStructureData>`.
> Keep validation (cost, overlap, footprint) callable headless so the server can re-verify it.

### The loader (mirror, don't fork, VillageSceneBuilder)

A runtime `BaseLayoutLoader` reads `GameState.BaseLayout` after wave setup and instantiates one
object per entry — the **runtime twin** of `VillageSceneBuilder.BuildBuildings`. It resolves
`itemId -> BuildableDef -> prefab`, places at `CellToWorld(cell)` with `yawSteps*90`, calls
`Building.Configure(def)`, attaches a footprint collider (same helper pattern), and adds a
`PlacedStructure` component. If `BaseLayout` is empty, fall through to the existing default
village (seed). This keeps "anything builds the level from data" true on both authoring sides.

---

## 3. Grid + placement

Reuse `TowerPlacementSystem`'s proven mechanics; promote them to a shared grid that tracks
**cell occupancy** (not just radius overlap) so multi-cell footprints validate exactly.

```csharp
// New: Assets/_Modules/Village/BuildMode/PlacementGrid.cs  (singleton)
float     cellSize   = 3f;   // matches polyperfect 3x3 modular wall segments
Vector2Int gridSize  = (28, 22);  // village interior 84x66m
bool[,]   occupied;

bool      CanPlace(Vector2Int cell, Vector2Int footprint);     // all cells free + in-bounds
void      Occupy / Free(Vector2Int cell, Vector2Int footprint, string id);
Vector3   SnapToGrid(Vector3 world);  Vector2Int WorldToCell(Vector3);  Vector3 CellToWorld(Vector2Int);
void      SetGridVisible(bool);       // overlay mesh, build-mode only
```

**Validity = the AND of existing rules** (reuse, don't reinvent):
1. In-bounds + all footprint cells free (`PlacementGrid.CanPlace`).
2. Flat upward ground (`TowerPlacementSystem.IsValidSurface` — `hit.normal.y >= 0.85`).
3. Gate-lane clearance (mirror `VillageSceneBuilder.ValidateBuildingGateClearance`'s 8m rule,
   so the player can't wall off / block the enemy spawn corridor — a real exploit otherwise).
4. Affordable (`EconomyService.CanAfford(def.cost)`).

**Feedback:** the existing green/red ghost (`s_validColor`/`s_invalidColor` via
`MaterialPropertyBlock`) already does valid/invalid tinting — extend the ghost to render the
selected prefab (not a cylinder) and tint per the combined rule above.

---

## 4. Build-mode UX

A dedicated **edit vs play** mode toggle. Entering pauses the threat and turns the village into
an editable canvas; exiting saves the layout and resumes.

```csharp
// New: Assets/_Modules/Village/BuildMode/BuildModeController.cs  (singleton)
Enter()  -> freeze WaveManager (no waves while building); pull camera to top-down overview
          ; PlacementGrid.SetGridVisible(true); show BuildPalette
Exit()   -> commit BaseLayout to GameState + Save(); restore camera; resume WaveManager
BeginPlace(BuildableDef) / ConfirmPlace(cell) / CancelPlace()
SelectExisting(PlacedStructure) -> Move / Rotate / Sell / Upgrade
```

- **Palette:** reuse `BuildMenu`'s card pattern (icon + name + cost, affordability greying,
  `CrystalBalance` already wired to live `GameState`). Add a horizontal scrollable strip mode;
  populate from the `BuildableDef` catalog (walls, towers, gates, mines, decorations — drawn
  from the polyperfect `_M` prefabs that WO-101 imported *for exactly this*).
- **Place:** tap card -> ghost follows finger -> tap valid cell -> instantiate, `Occupy`,
  `EconomyService.TrySpend(cost)`, append to `BaseLayout`.
- **Move:** tap placed -> `Free` old cells -> re-ghost -> drop on new valid cells -> `Occupy`.
- **Rotate:** tap rotate -> `yawSteps = (yawSteps+1) & 3`; re-validate footprint (footprint
  swaps axes for odd steps on non-square pieces).
- **Sell/Delete:** `Free` cells, destroy object, `EconomyService.Grant(50% of cost)`, remove
  from `BaseLayout`.
- **Upgrade:** bump `level`, spend the tier cost (hooks the wall-tier system, WO-109).
- **Mobile UX:** input via the existing Lean.Touch driver pattern already vendored
  (`Assets/Plugins/CW`); tap-to-arm + drag-ghost reads cleanly on touch. One-finger place,
  two-finger camera pan/zoom in build mode. No precision-mouse assumptions.

> **UXML caveat (known repo trap):** `.uxml`-sourced UIDocuments render empty in player builds
> (see `BuildMenu.ShowCodeFallbackMenu`). **Build the palette in code** (code UIElements render
> in builds), exactly as the BuildMenu fallback does. Do not author the palette as `.uxml`.

---

## 5. Reuse map

| Build-mode need | Plugs into (existing) |
|---|---|
| Ghost + snap + overlap + spend | `TowerPlacementSystem` (generalize `TowerData` -> `BuildableDef`) |
| Palette UI + cost/affordability | `BuildMenu` card pattern + `CrystalBalance` (live `GameState`) |
| Buildable definitions | `BuildingDef` / `BuildingCatalog` (extend; data-driven) |
| Footprint path-blocker collider | `Building.EnsureBlocker` + `VillageSceneBuilder.AddBuildingFootprintCollider` pattern |
| Gate-clearance exploit guard | `VillageSceneBuilder.ValidateBuildingGateClearance` constants |
| Walls (modular, tiers) | `Walls/WallSegment` + `WallLevel` field + WO-109 wall tiers |
| Towers | `Tower` + `TowerConstructionQueue` (place can still queue-build) |
| Mines / resource nodes | `Buildings/CrystalMine` (generalized in WO-110/111) |
| Multi-resource economy | `EconomyService` (note: session-only — see Risks) |
| Persistence | `GameState` + `SaveSchema` + `SaveMigrator` (one field + v12 migration) |
| Prefab art | polyperfect `_M` catalog (mobile-light, the intended palette) |

**NavMesh consideration:** placed footprint colliders are path blockers, so enemy routing must
account for the player's layout. See Risks — this is the one cross-cutting hazard.

---

## 6. Delivery ladder slice (NORTH_STAR rungs 4 -> 6)

Each phase is independently shippable; each is a step *up* the ladder, not a big-bang.

- **Phase 0 — data seam (rung 4 "place your base").** Add `PlacedStructureData` + `BaseLayout`
  field + v12 migration + `BaseLayoutLoader`. No UI yet: prove the village can be rebuilt from
  data (seed `BaseLayout` from the current default in a one-time editor util, load from it).
  *Ships:* identical village, now data-driven. This is the principle-#1 spine.
- **Phase 1 — place + persist (rung 5 "structure your settlement", minimal).** `PlacementGrid`
  + `BuildModeController.Enter/Exit` + code-built palette + place + cost + save. Player places a
  few structures; they survive a reload. *Ships:* CoC-style edit screen, place-only.
- **Phase 2 — full edit verbs.** Move / rotate / sell / upgrade; ghost feedback polish; gate +
  footprint validation complete. *Ships:* the full single-player build experience.
- **Phase 3 — server-authoritative layout (rung 6 / PvP prereq).** `BaseLayout` becomes
  server-stored + server-validated; layouts become raidable snapshots. Reuses the same struct.
  *Designed-for now, built when the backend (WO-107) lands.*

---

## 7. Risks / open questions

1. **NavMesh rebake on edit (the main hazard).** Player-placed footprints change enemy pathing.
   Runtime NavMesh rebuild on a phone is too costly to do per placement. **Recommended:** use
   NavMeshObstacle components (carving) on placed footprints — obstacles carve the existing
   baked mesh at runtime with no full rebake, and the gate-clearance rule (§3) guarantees a
   spawn->core lane always exists. Bake the *base* navmesh once; let placements carve. Flag if
   carving proves too heavy on low-end devices — fallback is a rebake only on `Exit()` (one
   bake per edit session, not per placement).
2. **Economy persistence mismatch.** `EconomyService` is **session-only** (resets on scene
   reload, by design); `GameState.Resources.Crystals` *is* persisted. Build-mode spend must go
   through the **persisted** crystal balance (`BuildMenu` already reads `GameState`), not the
   session-only `EconomyService`, or players lose spend across reloads. Reconcile which store is
   canonical for build costs before Phase 1 — recommend `GameState` for crystals, `EconomyService`
   for in-run wood/stone until those are persisted too.
3. **Mobile UX precision.** 3m cells + discrete 90deg rotation keep touch placement forgiving;
   avoid free rotation / sub-cell placement on phones. Validate the ghost is readable at thumb
   distance (offset the ghost above the finger).
4. **Server authority / anti-cheat (principle #2).** When raids land, the client cannot be
   trusted to report its own base or its spends. The layout *and* the place/sell validation
   must be re-runnable headless on the server (principle #3 deterministic seam). Designing
   `PlacementGrid.CanPlace` + cost checks as pure functions over data (no scene dependency) now
   keeps this cheap later.
5. **Catalog scope creep.** Start the palette with ~6 buildables (wall, gate, two towers, mine,
   one decoration). The polyperfect pack is huge; resist front-loading it — add entries as the
   wall-tier (WO-109) and harvest-node (WO-110/111) systems unlock them.
6. **Default-village migration UX.** First time a player enters build mode, do we seed
   `BaseLayout` from the default `VillageSceneBuilder` layout (so they edit the familiar
   village) or hand them an empty plot? Recommend **seed from default** — preserves their
   sense of an existing town and avoids a jarring blank canvas.

---

## Bottom line

The placement engine, palette, footprint colliders, economy, and prefab catalog **already
exist**. WO-108 is overwhelmingly *reuse + generalize*, and its one true new piece is the
**data-driven base-layout model** (`List<PlacedStructureData>` in `GameState`, loaded by a
runtime twin of `VillageSceneBuilder`). Make that the spine, keep validation pure and headless,
and the CREATE verb — and the async-PvP rung after it — are additive on seams that already exist.
A re-centering, not a rebuild.
