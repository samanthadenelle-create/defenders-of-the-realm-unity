# WORK ORDER 26 — Larger, More-Navigable City

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
**Applying this requires re-running the village scene builder**
(`Defenders > Week 3 > Build Village Scene`, i.e.
`-executeMethod DeNelle.Editor.VillageSceneBuilder.BuildVillage`). That step is a
**hard rule: owner-gated** and is NOT performed by this work order. This document
only specifies the parameter/value changes; do not edit code or re-bake the scene
on the basis of this file alone.

**Date:** 2026-05-24
**Author:** level-architecture pass
**Problem (owner):** In the Village the player "collides every few steps" — the
town is too cramped/small to roam. Make the city LARGER and more navigable while
keeping the curated aesthetic, the 4 cardinal gates, and the Heart-centered layout.

---

## 1. Diagnosis — WHY it feels cramped

What "collision" means here (`HeroLocomotion.cs`): each frame the hero does a
`Physics.CapsuleCast` (radius **0.4 m**, capsule from y=0.4 to y=1.6) along its
move vector; on any hit it clamps the step to `hit.distance - 0.06`. So the hero
stops dead against **any collider** in its path. The relevant colliders are:

- the curtain-wall sections (`WallSegment`, perimeter only — fine),
- the per-building **footprint BoxColliders** added by
  `AddBuildingFootprintCollider` (`VillageSceneBuilder.cs` line ~2312).

Gates, fences, props, the cathedral, trees, and road tiles all have colliders
stripped, so they are NOT the cause. The cramping is caused by the **building
footprint boxes** in too small a ring, packed too tightly. Four concrete causes:

### Cause A — Wall ring is small for a 3.0× town
`WallLayout.WallHalfX = 28`, `WallHalfZ = 21` → interior **≈ 56 m × 42 m**
(south bows to −25). Buildings are placed at `BuildingScale = 3.0×`
(`VillageSceneBuilder.cs` line 95). At 3.0× a ~1.2 m KayKit house becomes ~3.6 m
and its footprint box is ~3.6 m wide. With 5 gameplay buildings + ~13 dressing
buildings + the cathedral footprint inside a 56×42 box, the *negative space* the
hero can actually walk through is narrow. The N-S half-depth (21 m) is the binding
constraint — the buildings + central cathedral + plaza eat most of it.

### Cause B — Footprint colliders are larger than the visible mesh
`AddBuildingFootprintCollider` sizes the BoxCollider from the **full combined
renderer bounds** (`ComputeMeshBounds` encapsulates every child Renderer). For
KayKit buildings that includes roof eaves, chimneys, towers, windmill sails, and
market awnings that overhang the building's actual walkable base. So the collider
the hero hits is **wider than the building looks** — the player "collides" with
empty air a half-metre out from the wall. There is no inset/shrink applied.

### Cause C — Buildings packed with near-zero (or negative) clearance
From the placement tables (`Buildings[]` line ~1020 and the `DressDef`s line
~1172), the worst offenders, center-to-center (each footprint ~3.6 m wide ⇒ needs
> ~4 m center spacing just to not touch, and we want a ≥ 3 m *gap*, i.e. ≥ ~6.6 m
centers):

| Pair | Center dist | Approx clearance |
|---|---|---|
| Workshop (16, 12.5) ↔ Townhall (16, 12.0) | **0.5 m** | **overlapping** |
| Home-B2 (−32,−23) ↔ Home-A2 (−30,−18) | ~5.4 m | ~1.8 m |
| Workshop (16,12.5) ↔ Farm-area / Tavern (16,−12) column | shares X=16 | tight lane |
| Home cluster SW (−14..−32, −8..−23) | several ~5–8 m | < 3 m in places |

The Workshop/Townhall pair literally sits on the same spot — two footprint boxes
fused into one large obstacle in the NE. The SW residential cluster is dense
enough that the hero (plus its pet pack, which wanders to `WanderRadiusOuter =
6.5 m` / `MaxLeashDistance = 11 m`, see `PetHeroLeash.cs`) cannot thread between
homes.

### Cause D — Streets are only ~2 tiles wide and don't form clear corridors
`BuildRoads` lays a 2-tile-wide (`±HexWidth*0.5` ⇒ ~**1.7 m** total paved) N-S
spine and E-W cross. That is narrower than the hero+pet envelope and the buildings
crowd right up to the road, so even the "streets" feel like slots.

**Net:** small ring (A) + oversized boxes (B) + tight packing (C) + thin streets
(D). Fixing all four gives roomy roaming.

---

## 2. Design — the larger city

Keep the shape (rectangle wider E-W, south bow-out), the 4 cardinal gates, the
Heart/cathedral at center, and the quarter identities (Residential SW, Market S,
Workshop NE, Farm/Orchard E). **Scale the footprint up ~1.5× and re-space the
buildings onto clear streets and plazas.**

### Layout sketch (top-down, N = +Z)

```
            N gate (0, +33)
   Home  .   .   Church .   .   Workshop
   cluster    [ N-S spine, 4 m wide ]    Townhall
   (NW/SW)   .   .   .   .   .   .   .   Blacksmith
W gate ----  CATHEDRAL (center, 16 m)  ---- E gate
(-42,0)     plaza 12 m radius, walkable   (+42,0)
   Pet     .   .   .   .   .   .   .   Farm / Orchard
   House   [ E-W cross, 4 m wide ]
   Market  Arcane    .   .   .   Tavern
            Tower
            S gate (0, -33)  (bow face)
```

Streets are continuous: from any gate the hero walks a 4 m-wide paved road to the
central plaza without crossing a footprint. Buildings sit **behind** the road
edge, set back so their (right-sized) footprints leave ≥ 3 m clear lane.

---

## 3. Concrete changes — `WallLayout.cs`

Enlarge the ring ~1.5× E-W and N-S (N-S grows more, since it was the binding
constraint). The gate gaps, wall thickness, and section length stay as-is so the
KayKit pieces and gate-clear sweep keep working.

| Const | Old | New | Why |
|---|---|---|---|
| `WallHalfX` | `28f` | `42f` | E-W interior 56 m → **84 m**. Room for the E-W building bands + 4 m streets. |
| `WallHalfZ` | `21f` | `33f` | N-S interior 42 m → **66 m**. This was the tight axis; +57% gives the cathedral + plaza + N/S bands breathing room. |
| `SouthBowDepth` | `4f` | `6f` | Keep the orchard/farm frontage proportional to the bigger ring. |
| `SouthBowHalfWidth` | `9f` | `13f` | Widen the bow face so the S gate run is still centered and the bow reads at the new scale. |
| `GateHalfWidth` | `1.4f` | `1.4f` | UNCHANGED — gate mesh + force-field sizing depend on it. |
| `GateGapHalf` | `=GateHalfWidth+0.6` | unchanged formula | Gate gap stays 4 m. |
| `SectionLen` | `5.2f` | `5.2f` | UNCHANGED — runs auto-split, more sections is fine. |
| `WallThickness` | `0.62f` | `0.62f` | UNCHANGED — collider/visual sizing depends on it. |

> NOTE: `WallHalfX`, `WallHalfZ`, and `SouthBowDepth` are **mirrored** as
> `const`s in `VillageSceneBuilder.cs` (lines 121–123:
> `WallHalfX = 28f; WallHalfZ = 21f; SouthBowDepth = 4f;`). They MUST be updated
> to the same new values (`42f` / `33f` / `6f`) or the ground floor, roads,
> approaches, and gate-clear sweep will desync from the wall. Update **both files
> together.**

Ground floor (`BuildGroundFloor`) and approach lanes derive their extents from
these consts, so they grow automatically. No other change needed there.

---

## 4. Concrete changes — `VillageSceneBuilder.cs`

### 4.1 Mirror the wall consts (REQUIRED — see note above)
Lines 121–123:
```
WallHalfX:      28f -> 42f
WallHalfZ:      21f -> 33f
SouthBowDepth:   4f ->  6f
```

### 4.2 Right-size the footprint colliders (`AddBuildingFootprintCollider`, ~2312)
Shrink the box to the building's walkable base so the hero stops at the wall, not
at the eaves. Two changes inside the method, after computing `col.size`:

- **Inset the X/Z extents by a fixed margin** so roof/awning overhang is excluded:
  multiply the local `size.x` and `size.z` by **0.8** (a 20% inset ≈ removes the
  typical KayKit eave overhang) — keep `size.y` full so it still reads as solid.
- **Clamp a minimum** so tiny meshes still block: `Mathf.Max(value, 1.2f)` on X/Z.

Resulting `col.size` line becomes (conceptually):
```
col.size = new Vector3(
    Mathf.Max(1.2f, (ls.x!=0 ? sz.x/ls.x : sz.x) * 0.8f),
    (ls.y!=0 ? sz.y/ls.y : sz.y),
    Mathf.Max(1.2f, (ls.z!=0 ? sz.z/ls.z : sz.z) * 0.8f));
```
This alone removes most "collide in open air" complaints (Cause B).

### 4.3 Re-space the 5 gameplay buildings (`Buildings[]`, ~1020)
Spread onto the larger ring with ≥ 6.5 m center spacing from any neighbour and
set back from the streets. Keep quadrant identities and `Fbx`/`Type`/`Id`.

| Id | Old (X, Z) | New (X, Z) | Note |
|---|---|---|---|
| crystal-mine | (−38, 14) | (−54, 20) | Stays OUTSIDE the (now larger) W/N wall, pushed out to match new `WallHalfX=42`. |
| pet-house | (−17, −10.5) | (−24, −16) | SW, clear of homes; near W-S lane. |
| arcane-tower | (6, −12.5) | (8, −20) | S-central, set back from the S spine. |
| workshop | (16, 12.5) | (22, 20) | NE artisan band. |
| farm (windmill) | (19, −1) | (30, −4) | E open ground, by the orchard. |

### 4.4 Re-space the dressing buildings (`BuildCityDressing`, ~1172)
Spread to fill the bigger ring; keep quarters. Target ≥ 3 m gap (≥ ~6.5 m centers)
between any two footprints, and keep a 4 m-wide clear band over each road.

**Residential SW** (`residentialDefs`):
| Name | Old (X, Z) | New (X, Z) |
|---|---|---|
| Home-A1 | (−30, −8) | (−40, −10) |
| Home-A2 | (−30, −18) | (−40, −22) |
| Home-A3 | (−14, −22) | (−18, −30) |
| Home-B1 | (−22, −23) | (−30, −30) |
| Home-B2 | (−32, −23) | (−40, −32) |
| Home-B3 | (−14, −14) | (−18, −18) |
| Well | (−23, −16) | (−30, −20) |

**Market S:**
| Name | Old (X, Z) | New (X, Z) |
|---|---|---|
| Market | (−4, −13) | (−10, −22) |
| Tavern | (16, −12) | (22, −20) |
| Church | (−3, 14) | (−8, 22) |

**Workshop NE** (FIXES the Workshop/Townhall overlap, Cause C):
| Name | Old (X, Z) | New (X, Z) |
|---|---|---|
| Blacksmith | (30, 13) | (40, 22) |
| Townhall | (16, 12) | (32, 12) |  ← was 0.5 m from Workshop; now ~13 m E and 8 m S of it |
| WorkshopYard centre | (27, 13) | (36, 22) |

**Orchard E:**
| Name | Old (X, Z) | New (X, Z) |
|---|---|---|
| BuildOrchard centre | (26, −1) | (38, −6) |
| FarmersHut | (31, −14) | (40, −24) |

> Sanity: with `WallHalfX=42`/`WallHalfZ=33`, every new interior position above is
> ≥ ~2 m inside the wall line (accounting for the ~1.8 m footprint half-width) and
> no two footprints are within 3 m. The NE Workshop(22,20)/Townhall(32,12)/
> Blacksmith(40,22) triangle now has ≥ 10 m legs.

### 4.5 Widen the streets (`BuildRoads` / `LayRoadPair`, ~704 / ~733)
Make the spine and cross **4 m wide** (3 tiles) so the hero+pet envelope fits with
margin:
- In `LayRoadPair`, change `lateral` from `{ -HexWidth*0.5f, HexWidth*0.5f }`
  (2 tiles ≈ 1.7 m) to `{ -HexWidth, 0f, HexWidth }` (3 tiles ≈ 3.5–4 m).
- The road arm loops already run to `WallHalf* - 1f`, so they extend to the new
  wall line automatically once the consts change.

### 4.6 Plaza (`BuildPlaza`, ~669) — keep central, enlarge slightly
The cathedral is 16 m and has its colliders stripped (`StripColliders`), so it does
NOT block the hero — the plaza is genuinely walkable around it. Enlarge the paved
block from `row −3..3 / col −4..4` to `row −4..4 / col −5..5` so the paving reaches
the wider spine/cross junction. No collider added (unchanged). Keep the Heart/
cathedral at center (`site = (0,0,1)`, scaled 16 m) unchanged.

### 4.7 Prop density — light reduction (already mostly stripped)
Most clutter is already removed (plot fences disabled line ~1106; workshop-yard
props removed ~1297; northern trees removed ~1224; approach boulders removed
~1447). Remaining colliding-relevant props: **none** — orchard trees, haybales,
standing stones all go through `PlaceProp`/`InstantiateModel` which strip
colliders. No further prop cull is required for navigability; leave the orchard +
haybale dressing for aesthetic. (If desired, the orchard grid at `BuildOrchard`
can drop from 4×3 to 3×2 trees, purely cosmetic.)

### 4.8 Hero spawn (`BuildHero`, ~2956)
Spawn stays in the open plaza. Current `(5, 0, 0)` is fine in the larger ring
(nearest new building, arcane-tower at (8,−20), is ~20 m away). No change required,
but optionally move to `(6, 0, 4)` to sit squarely on the widened plaza paving.

---

## 5. Acceptance criteria

A re-built scene passes when:

1. **Straight-line plaza walk:** the hero can walk a straight line clear across the
   central plaza (from the N-spine entrance to the S-spine entrance, ≥ 30 m)
   without a single CapsuleCast block (cathedral has no collider; no building
   footprint intrudes on the spine).
2. **Gate-to-gate roaming:** the hero can walk from each of the 4 gates to the
   plaza along its street without colliding, on a corridor ≥ 4 m wide.
3. **Min clearance:** no two building footprint colliders are closer than **3 m**
   (center spacing ≥ ~6.5 m given ~3.6 m boxes). Specifically the NE
   Workshop/Townhall/Blacksmith no longer overlap.
4. **Footprint ≤ mesh:** each building's footprint BoxCollider X/Z is ≤ the
   building's visible base (20% inset applied); the hero no longer stops in open
   air beside a wall.
5. **Interior size:** walled interior is ≈ **84 m × 66 m** (`WallHalfX=42`,
   `WallHalfZ=33`), measurably larger than the prior 56 × 42.
6. **Systems intact:** 4 cardinal gates still open and walk-through; wave spawn
   points still sit beyond each gate (they derive from `WallLayout.Gates`, so they
   follow the new ring); pets still leash to the hero within 6.5 m / 11 m without
   getting stuck on a footprint; the cathedral remains centered.
7. **No desync:** `WallLayout` consts and the mirrored `VillageSceneBuilder` consts
   (`WallHalfX`/`WallHalfZ`/`SouthBowDepth`) match exactly.

---

## 6. Applying (OWNER-GATED — do NOT run as part of this work order)

> **Hard rule:** Implementing this requires re-running the village scene builder,
> which re-bakes the entire Village scene (and exterior terrain + NavMesh). That is
> **owner-gated**. This work order is design + spec only. To apply, the owner runs:
>
> `Defenders > Week 3 > Build Village Scene`
> (or `-executeMethod DeNelle.Editor.VillageSceneBuilder.BuildVillage`)
>
> after the `WallLayout.cs` + `VillageSceneBuilder.cs` edits above are made and the
> `DeNelle.Village` assembly recompiles. The build is idempotent (it nukes and
> rebuilds `VillageRoot`), so it is safe to re-run while iterating on the numbers.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
