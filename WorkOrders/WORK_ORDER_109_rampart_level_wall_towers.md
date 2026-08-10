# WORK ORDER 109 — Rampart Level: Walkable Wall Tops + Wall-Top Tower Spec

**Status:** CLOSED — SUPERSEDED by WO-904 (owner-approved sweep 2026-08-09: old-castle walkable walls; WO-904 owns fortifications)
**Date:** 2026-05-30
**Priority:** High — core defensive depth, feeds player build mode (WO-108)
**Scope:** Medium — wall collider pass + NavMesh upper layer + tower prefab swap
**Depends on:** WO-104 (castle walls built), WO-108 (player build mode palette)
**North Star:** Defensive placement strategy — WHERE you build towers matters

---

## ⚠ Implementation split (owner-ratified 2026-05-29) — route as three parts

This WO has three parts with different dependencies. Route them separately so the
build-mode piece does not block the rampart from shipping with the castle:

- **109a — Rampart second tier (architect lane, depends on WO-104 only):** walkable
  wall-top BoxColliders + NavMesh upper-layer volume + `Stairs_Medieval_Stone` in
  `BuildCastleFortification()`. This is the physical second floor. Ships in the
  serialized architect lane immediately after the castle (WO-104).
- **109b — Tower elevation range bonus (combat/code lane, no builder):** the +40%
  range-on-elevation logic in `Tower.cs`/`TowerCombat.cs`. Pure code; runs in the
  combat silo in parallel, no scene-builder contact.
- **109c — Player-placeable Wall Tower (DEFERRED — needs WO-108):** the `wall_tower`
  palette item + `BuildZone.WallTop` restriction in `PlacementGrid`/`BuildPaletteUI`.
  **Do NOT implement until WO-108 (player build mode) lands** — the palette/grid it
  edits does not exist yet. The rampart + elevated towers are fully usable without it.

---

## Vision

The wall perimeter is not just a barrier — it's a second floor. The player
climbs via rampart stairs, walks the battlements, and places smaller defensive
towers along the top for elevated range. Ground-level towers cover short range;
wall-top towers cover the approach from above. Two layers of defense = the CoC
depth that rewards smart base design.

---

## Part 1 — Walkable Wall Top

### Wall geometry at Y = 3m

`Wall_Stone_3x3_A` segments are 3m tall. The top surface sits at Y ≈ 3.0m.
For the hero to walk there:

1. **Add a thin BoxCollider on the wall-top surface** per wall segment.
   In `BuildCastleFortification()` (WO-104), after placing each wall segment:
   ```csharp
   // Add a walkable surface collider at the top of the wall
   var topCollider = wallGo.AddComponent<BoxCollider>();
   topCollider.center = new Vector3(0f, 1.6f, 0f);   // top of a 3m wall
   topCollider.size   = new Vector3(3f, 0.1f, 1.0f); // thin walkable strip
   ```

2. **NavMesh upper area volume** — add a `NavMeshModifierVolume` over the entire
   wall perimeter at Y = 3–4m range, Area = `Walkable`. This bakes the wall top
   into the NavMesh as a connected walkable surface.
   ```
   NavMeshModifierVolume:
     Center:   (0, 3.5, 0) in world space
     Size:     (full perimeter bounds, height 1m)
     Area:     Walkable
   ```

3. **Connect ground ↔ rampart via stairs** — `Stairs_Medieval_Stone` placed at
   each of the 8 stair positions from WO-104. The stair mesh must have a walkable
   ramp collider (BoxCollider at 45° or a ramp-shaped MeshCollider marked
   `walkable = true` in NavMesh bake settings).

### NavMesh bake note
Two-level NavMesh requires the bake to use **Off-Mesh Links** or a layered bake.
In the VillageSceneBuilder NavMesh bake call, add:
```csharp
// Enable off-mesh link generation so Unity can connect ground and wall top
buildSettings.overrideVoxelSize  = true;
buildSettings.voxelSize          = 0.15f;   // finer for stair ramps
buildSettings.overrideTileSize   = false;
```

---

## Part 2 — Wall-Top Tower Respec

### Problem with current "Build Tower"

`Tower_Medieval_Big` is the current build-mode tower. It's 6–8m tall and
designed as a standalone ground structure. On a 3m wall it looks wrong (too
tall, wrong proportions) and is hard to skin as a cosmetic item.

### Replacement: Two-tier tower system

| Tier | Prefab | Placement | Size | Use |
|---|---|---|---|---|
| **Ground Tower** | `Tower_Medieval_Big` | Ground level, standalone | Large | Long-range, high HP |
| **Wall Tower** | `Tower_Medieval_Wood` | Wall top only | Small/medium | Mid-range, lower HP, cheaper |
| **Corner Bastion** | `Tower_Castle_Round` | Corner wall positions | Large | Auto-placed by WO-104, not player-built |

The **Wall Tower** (`Tower_Medieval_Wood`) is:
- 2–3m tall — proportional to the 3m wall
- Simpler mesh — easier UV unwrap for skin variants
- Cheaper to build (50 crystals vs 150 for ground tower)
- Restricted to wall-top grid zone (build mode palette shows it only when player
  is on the rampart or has selected a wall-top plot)

### Skin-friendly design

`Tower_Medieval_Wood` uses the polyperfect single-atlas material. To skin it:
- Swap the atlas UV region (tint mask approach) — one material, different tint
- Or swap the full material (2 materials max per tower: base + accent)
- No separate mesh variants needed — `CosmeticApplier` handles the swap

Add to `BuildPaletteUI.items[]` in WO-108:
```csharp
new BuildableItem
{
    id          = "wall_tower",
    displayName = "Wall Tower",
    prefab      = Resources.Load<GameObject>("polyperfect/Tower_Medieval_Wood"),
    footprint   = new Vector2Int(1, 1),
    crystalCost = 50,
    zoneRestriction = BuildZone.WallTop   // only shows when on rampart
}
```

---

## Part 3 — Tower Range Bonus for Elevation

In `TowerCombat.cs` (or `TowerData.cs`), add an elevation multiplier:
```csharp
// Towers on the wall top get +40% detection range
float elevationBonus = transform.position.y > 2.5f ? 1.4f : 1.0f;
float effectiveRange = baseRange * elevationBonus;
```

This makes wall placement a meaningful strategic choice, not just aesthetic.

---

## Files to Edit

| File | Action |
|---|---|
| `Assets/Editor/VillageSceneBuilder.cs` | Add wall-top BoxColliders + NavMeshModifierVolume in `BuildCastleFortification()` |
| `Assets/_Modules/Village/BuildMode/BuildPaletteUI.cs` | Add `wall_tower` item with `BuildZone.WallTop` restriction |
| `Assets/_Modules/Village/Buildings/Tower.cs` or `TowerCombat.cs` | Add elevation range bonus |
| `Assets/_Modules/Village/BuildMode/PlacementGrid.cs` | Add `BuildZone` enum (Ground, WallTop) + zone check |

**Do NOT touch:** Village.unity (baked via builder), WaveManager, ATB, monetization.

---

## Acceptance Criteria

- [ ] Hero can walk up `Stairs_Medieval_Stone` from ground to wall top (Y ≈ 3m)
- [ ] Hero can walk the full rampart perimeter without falling through
- [ ] `Tower_Medieval_Wood` appears in build palette only when on/near wall top
- [ ] Wall Tower placed on wall top gets +40% range vs ground placement
- [ ] `Tower_Medieval_Big` remains available at ground level, unchanged
- [ ] `CosmeticApplier` can tint Wall Tower material independently of other structures
- [ ] NavMesh bake covers both ground and wall-top walkable surfaces
- [ ] Enemies do NOT path up the walls (enemy NavMesh area excludes wall top)
