# WORK ORDER 104 — Castle Fortification Rebuild: Curtain Walls, Round Towers, Moat + 4 Drawbridges

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-29
**Priority:** High — world quality, architect lane (fully independent of gameplay code)
**Scope:** Large — `VillageSceneBuilder.cs` environment changes only. No gameplay code.
**Depends on:** WO-101 Phase A (polyperfect swap — done), WO-103 (rebake — queued)
**Assets:** All from `Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/`

---

## Goal

Replace the current placeholder wall ring with a full castle fortification: modular
crenellated stone walls, round corner towers, drawbridges at all 4 gates, and a moat
ring using the polyperfect water terrain tiles. The result should feel like a real
medieval stronghold worth defending.

No gameplay systems are touched. This is a pure `VillageSceneBuilder.cs` + scene rebuild.

---

## Reconciliation rule

**Check `PIPELINE_STATE.md` and `docs/polyperfect-asset-catalog.md` before touching
any file.** Wave, combat, and store systems are already built — this WO only touches
`VillageSceneBuilder.cs` and the scene it produces. NavMesh must remain intact after
the rebuild (enemy approach corridors unchanged).

---

## Arena dimensions (from WO-101)

- Interior: 84 × 66 m
- Heart of Elarion: centre `(0, 0, 0)`
- Wall perimeter: `x = ±42`, `z = ±33`
- Gates: South `(0, 0, −33)`, East `(+42, 0, 0)`, West `(−42, 0, 0)`, North `(0, 0, +33)`

---

## 1. Curtain Walls — replace current placeholder wall segments

Use `Wall_Stone_3x3_A`, `Wall_Stone_3x3_B` (moss), `Wall_Stone_3x3_C` (battle-worn).
Mix variants for visual storytelling:

- **North/East walls:** primarily `_A` (pristine — less enemy contact historically)
- **South/West walls:** mix `_B` and `_C` (more battle damage — primary enemy lanes)

Use `Wall_Stone_Window_3x3m_A` (arrow slits) every **3rd segment** for visual interest
and the suggestion of archers on the ramparts.

Use `Wall_Stone_Corner_A` at the 4 wall corners — these are superseded visually by the
round towers placed on top (see §2), but fill the geometry gap at ground level.

**Placement rules:**
- Wall height: `y = 0`, standard ground-plane placement
- Segment pitch: every 3 m around the perimeter
- Skip a **6 m gap** at each gate position (centred on the gate coords) for the gate
  structure + drawbridge clearance

**Segment counts (approximate):**
```
South wall (z = −33):  x = −42 → −3 and x = +3 → +42  (skip 6 m at x=0)  → ~26 segments
North wall (z = +33):  same range and skip                                   → ~26 segments
East wall  (x = +42):  z = −33 → −3 and z = +3 → +33  (skip 6 m at z=0)   → ~20 segments
West wall  (x = −42):  same range and skip                                   → ~20 segments
```

---

## 2. Corner Towers (4×)

`Tower_Castle_Round` at all four corners. These are the primary visual anchors —
crenellated, round, imposing. They read clearly from the camera angles used in the
village overview shot.

```
NE: (+42, 0, +33)
NW: (−42, 0, +33)
SE: (+42, 0, −33)
SW: (−42, 0, −33)
```

Scale: `(1, 1, 1)`. No rotation needed — round towers are symmetric.

---

## 3. Mid-Wall Watchtowers (4×)

`Tower_Castle_Square` (keep-style, slightly smaller than the round corner towers) at
wall midpoints. Each flanks one of the 4 gates — the gate sits in the 6 m gap, the
watchtower marks its position from a distance.

```
North mid:  (0, 0, +33)
South mid:  (0, 0, −33)  ← flanks main gate
East mid:   (+42, 0, 0)
West mid:   (−42, 0, 0)
```

Scale: `(1, 1, 1)`.

> **Note:** South mid-wall tower is intentionally the most prominent — it marks the
> main hero entrance and is the first thing a player sees from the south approach road.

---

## 4. Gates (4×) — with flanking wall ends

| Gate | Prefab | Position | Notes |
|---|---|---|---|
| South (main) | `Gate_Medieval_Medium` | `(0, 0, −33)` | Primary hero entrance + main enemy lane |
| East | `Gate_Medieval_Small` | `(+42, 0, 0)` | Side gate |
| West | `Gate_Medieval_Small` | `(−42, 0, 0)` | Side gate |
| North | `Gate_Medieval_Small` | `(0, 0, +33)` | Side gate |

Each gate gets `Wall_Stone_End_3x3m_A` butted flush against both sides of the gate
opening — this caps the curtain wall cleanly and frames the gate arch.

---

## 5. Drawbridges (4×) — one per gate

`Drawbridge_Medieval` placed just outside each gate, bridging the moat. This is the
centrepiece visual element of the fortification — a moat crossed only by 4 lowered
drawbridges, one per approach lane.

```
South drawbridge:  ( 0,   0, −36)   — 3 m outside south gate
East drawbridge:   (+45,  0,  0)    — 3 m outside east gate
West drawbridge:   (−45,  0,  0)    — 3 m outside west gate
North drawbridge:  ( 0,   0, +36)   — 3 m outside north gate
```

Rotate each drawbridge to face outward from its gate (Y-axis rotation):

| Drawbridge | Y Rotation |
|---|---|
| South | 0° |
| North | 180° |
| East | 90° |
| West | 270° |

---

## 5b. Interactive Drawbridge — `DrawbridgeController` (lowers on hero approach)

**Upgrade from static:** each drawbridge starts **raised** and **rotates down** when the
hero approaches, via a `DrawbridgeController` MonoBehaviour + an approach trigger.

> ⚠ **Lane note:** the placement (§5) stays architect-lane (`VillageSceneBuilder`), but
> the **controller is gameplay code** — a separate script the CLI builds under the
> brace/compile gate. Don't author it inside `VillageSceneBuilder`.

**Design-in (these are the bugs this pattern always hits):**
1. **Collider follows the rotation.** The blocker collider must rotate/disable in lockstep
   with the visual — else the hero walks through a raised bridge or bonks a lowered one.
   Drive both from the controller (or bake the walkable surface in the down state only).
2. **Depends on the hero `"Player"` tag → bounced item ② / WO-105.** The approach trigger
   tests the hero by tag. Until WO-105 re-lands the `tag = "Player"` line in
   `VillageSceneBuilder`, the trigger never fires and the bridge never moves. **Fix WO-105
   first, or this can't be tested.**
3. **Rotate around the hinge, not the centre.** Pivot on the base edge (an empty parented
   at the hinge), not the mesh centre — and beware the Tripo/polyperfect off-centre-pivot
   trap, or it swings through the ground.
4. **Reconcile — no third door controller.** Sit beside `Buildings/DoorController.cs`
   (canonical) and `CastleDoorController.cs`; reuse their approach-trigger logic. Duplicate
   door controllers have broken this compile before.
5. **NavMesh only if pathed.** If enemies/hero NavMesh-path across it, the down state must
   be walkable in the baked mesh (same trick the gates use). Cosmetic-only → ignore.

---

## 6. Moat Ring — `Terrain_Plane_Lake` tiles

The moat is a 6 m-wide ring of water terrain tiles surrounding the curtain wall
exterior. It forms a continuous band between the wall base and the outer approach ground.

```
Moat ring inner edge: x = ±42, z = ±33  (flush with wall base)
Moat ring outer edge: x = ±48, z = ±39  (6 m outside the wall)
Tile size: 3×3 m — tile every 3 m around the full perimeter
```

**Tile type:** `Terrain_Plane_Lake`

**Corner fills:** At the four outer corners `(±48, 0, ±39)`, place `Terrain_Plane_Lake`
tiles rotated 45° to fill the diagonal gap cleanly.

**Transition tiles:** Place `Terrain_Plane_Valley1` or `Terrain_Plane_Valley2` in the
band immediately outside the moat outer edge (between moat and outer approach terrain).
This creates a natural depression / earthwork berm look — the moat sits in a channel
rather than appearing to float at grade.

**Drawbridge clearance:** Do not place moat tiles in the 6 m spans at each gate
position — the drawbridge prefab covers this gap.

---

## 7. Rampart Stairs (8×)

`Stairs_Medieval_Stone` for internal wall access. Two per long wall side, flanking each
gate from the interior. These are purely visual but establish that defenders can reach
the ramparts.

```
South inner (flanking gate): (−6, 0, −33)  and  (+6, 0, −33)
North inner:                 (−6, 0, +33)  and  (+6, 0, +33)
East inner:                  (+42, 0, −8)  and  (+42, 0, +8)
West inner:                  (−42, 0, −8)  and  (−42, 0, +8)
```

---

## 8. Approach Roads (outside moat)

Stone bridge segments from each drawbridge outward 12 m to the enemy spawn points.
These are the invasion corridors — enemies march in along these roads.

```
South: (0, 0, −36) → (0, 0, −48)   — 4× stone bridge segments heading south
East:  (+45, 0, 0) → (+57, 0, 0)   — 4× heading east
West:  (−45, 0, 0) → (−57, 0, 0)   — 4× heading west
North: (0, 0, +36) → (0, 0, +48)   — 4× heading north
```

Prefab per segment: `Bridge_Medieval_Stone`

Flank each approach road with `Ground_Cracked_Dirt` tiles — enemy advance lane
dressing that reads as churned, war-worn earth.

---

## Implementation — VillageSceneBuilder.cs

All changes go in a new method `BuildCastleFortification(Transform root)` which
**replaces** the existing `BuildWallPerimeter()` call in `BuildVillage()`.

### Method structure

```csharp
private static void BuildCastleFortification(Transform root)
{
    BuildCurtainWalls(root);      // Step 1 — modular wall segments with variants
    BuildCornerTowers(root);      // Step 2 — Tower_Castle_Round × 4
    BuildWatchtowers(root);       // Step 3 — Tower_Castle_Square × 4
    BuildGatesWithFlanks(root);   // Step 4 — gates + Wall_Stone_End_3x3m_A flanks
    BuildDrawbridges(root);       // Step 5 — Drawbridge_Medieval × 4
    BuildMoatRing(root);          // Step 6 — Terrain_Plane_Lake ring
    BuildRampartStairs(root);     // Step 7 — Stairs_Medieval_Stone × 8
    BuildApproachRoads(root);     // Step 8 — Bridge_Medieval_Stone + cracked dirt
}
```

### Prefab loading pattern

Match the pattern used elsewhere in `VillageSceneBuilder` — load via
`AssetDatabase.LoadAssetAtPath<GameObject>` at editor-build time:

```csharp
private static readonly string PolyBase =
    "Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/";

private static GameObject LoadPoly(string prefabName)
{
    var path = PolyBase + prefabName + ".prefab";
    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
    if (prefab == null)
        Debug.LogWarning($"[WO-104] Prefab not found (polyperfect gitignored?): {path}");
    return prefab;
}
```

Log a **warning** (not an error) on any missing prefab — polyperfect is gitignored and
may not be present in all environments. The builder should continue and skip missing
prefabs gracefully rather than throwing.

### BuildCurtainWalls implementation notes

Iterate around the perimeter in 3 m steps. At each step:
1. Determine which wall variant to use (index mod 3 → `_A`, `_B`, `_C`; force `_Window`
   on every 3rd segment).
2. Check if the position falls within the 6 m gate gap for that wall side — if so, skip.
3. Set rotation: North/South walls → Y=0°, East/West walls → Y=90°.

```csharp
// South wall example (z = -33, x sweeps from -42 to +42 in 3m steps)
for (float x = -42f; x <= 42f; x += 3f)
{
    if (Mathf.Abs(x) < 3f) continue; // skip 6m gate gap centred at x=0
    int idx = Mathf.RoundToInt((x + 42f) / 3f);
    string variant = (idx % 3 == 2) ? "Wall_Stone_Window_3x3m_A"
                   : (idx % 3 == 1) ? "Wall_Stone_3x3_B"
                   : "Wall_Stone_3x3_A";
    // South/West walls: prefer _B/_C — override above for south
    PlacePrefab(root, LoadPoly(variant), new Vector3(x, 0f, -33f), Quaternion.identity);
}
```

Apply similar logic for North (swap `_A` dominant), East, West walls.

### Parent hierarchy

Place everything under a `Fortification` child GameObject of `root`:

```
Village (root)
└── Fortification
    ├── CurtainWalls
    ├── CornerTowers
    ├── Watchtowers
    ├── Gates
    ├── Drawbridges
    ├── Moat
    ├── RampartStairs
    └── ApproachRoads
```

This keeps the scene hierarchy readable and allows the architect to hide/show sections
independently during construction.

---

## Call-site change in BuildVillage()

```csharp
// Before (WO-101):
BuildWallPerimeter(root);

// After (WO-104):
BuildCastleFortification(root);
```

Remove or comment out `BuildWallPerimeter()` — do not delete it until WO-104 is
verified, in case a rollback is needed.

---

## Acceptance Criteria

- [ ] 4 round corner towers visible at all wall corners `(±42, 0, ±33)`
- [ ] 4 square watchtowers at wall midpoints (one per gate flank)
- [ ] Curtain walls form a continuous ring with no gaps except at the 4 gate openings
- [ ] Arrow-slit wall variant (`Wall_Stone_Window_3x3m_A`) appears every 3rd segment
- [ ] South/West walls use predominantly `_B`/`_C` variants; North/East use `_A`
- [ ] 4 drawbridges span the moat at each gate — correctly oriented per Y-rotation table
- [ ] Moat water tiles (`Terrain_Plane_Lake`) form a continuous ring outside the walls
- [ ] Valley terrain transition tiles visible at moat outer edge (depression effect)
- [ ] Corner moat tiles placed at `(±48, 0, ±39)` at 45° to fill diagonal gaps
- [ ] `Bridge_Medieval_Stone` approach roads extend 12 m outward from each drawbridge
- [ ] `Ground_Cracked_Dirt` flanks each approach road
- [ ] 8 rampart stairs placed at inner wall positions per §7
- [ ] No purple/magenta materials (polyperfect atlas — should be fine if URP pack imported)
- [ ] NavMesh still covers interior + approach corridors (enemy paths unchanged)
- [ ] No building overlaps any wall, tower, or gate (gate-clearance assertion passes)
- [ ] Scene hierarchy uses `Fortification` parent with named sub-groups
- [ ] Missing polyperfect prefabs produce `LogWarning` only — builder does not throw
- [ ] Rebake required after implementation: run via WO-103 / `Defenders > Week 3 > Build Village Scene`

---

## Files to Edit

| File | Action |
|---|---|
| `Assets/Editor/VillageSceneBuilder.cs` | Replace `BuildWallPerimeter()` with `BuildCastleFortification()` + 8 sub-methods |
| `Assets/Scenes/Village.unity` | Rebuilt via builder — do **NOT** hand-edit |

---

## Out of scope

The following are explicitly **not** touched by this WO:

- `WaveManager.cs` — enemy spawn points, wave definitions, patrol paths
- `GateInteractor.cs` / proximity gate logic — gates remain open. (NOTE: the drawbridge is
  **no longer visual-only** — see §5b. Its `DrawbridgeController` is gameplay code (CLI lane),
  separate from this architect-lane WO, and depends on the hero `Player` tag from WO-105.)
- `NavMeshSurface` bake settings — WO-103 handles rebake after this WO lands
- Any dungeon, ATB, or combat system files
- Hero or pet scripts
- HUD or UI canvases
