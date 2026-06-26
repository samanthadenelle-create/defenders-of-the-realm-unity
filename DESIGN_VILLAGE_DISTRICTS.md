> ⚠ **STALE — targets the ABANDONED `Village.unity` / VillageSceneBuilder home.** Home hub is now `MainCastle_Hall`; `Village2` = raid target. Kept for history. Live reality: `CANON_GROUND_TRUTH_2026-06-26.md`.

# DESIGN — Elarion Village District Zoning Spec

## ⭐ CANONICAL TEMPLATE (owner concept art, 2026-05-31) — "use this as a template, NOT crowded"
The owner's reference image is the definitive layout. All zoning below conforms to it.

**Quadrant districts (clean grid, generous grass spacing — NOT crowded, ~3–4 buildings per quadrant):**
| Quadrant | District | Buildings (from art) | Roof color |
|---|---|---|---|
| **NW** | **Blacksmith / Crafting** | Blacksmith, Armorer, Lumbermill (+ workshops) | blue |
| **NE** | **COMMERCE** (upgrade-gated corner ✅) | Commerce Hall, Market, Pet Shop, Jeweler | red/pink |
| **SW** | **Tavern / Social** | Tavern (+ cluster) | mixed |
| **SE** | **Residential / Homes** | Healer's Hut, NPC House, **Hero's Home** (WO-161), houses | purple |

- **Center:** World-Tree in a **clean circular stone-ring plaza** — fully clear/sacred, nothing inside.
- **Wall:** rectangular, towers at every corner AND mid-wall; four cardinal gates (N/E/S/W).
- **Roads:** grid connecting gates + dividing quadrants. Color-coded roofs per district.
- **Density:** "NOT crowded" governs — cut toward **~half** the current 29 buildings + −20% building shrink.

**FUTURE / VISION (owner 2026-05-31 — upgrade-unlocked, NOT v1):** Armorer, Jeweler, Pet Shop (≠ Pet House),
Healer's Hut, NPC House, Hero's Home, Commerce Hall. These are what the districts **grow into as the seat
tier upgrades** (ties the Commerce-on-upgrades ladder). Roadmap, not the immediate build — source meshes later.

**NOW (freeze 2):** reorganize the **existing** roster into the 4 quadrants — don't source new meshes:
- NW Blacksmith/Craft: Forge(Blacksmith), Lumbermill, Barracks, Arcane Library
- NE Commerce: Market, Crystal Mine (Commerce Hall/Jeweler = future)
- SW Tavern/Social: Tavern
- SE Residential: Houses (one = "Hero's Home" placeholder), Pet House nearby
- Center: Heart (townhall) only, clear plaza (Shrine + Well move out)
- Farm/Granary: south apron or a quadrant edge

**Commerce-in-NE confirmed** (resolved the open SE-vs-NE question to NE per the art).

---

**Status:** DRAFT for owner review (architect, 2026-05-31). Planning-ready coordinate spec — now conformed to the canonical template above.
**Read alongside:** `DESIGN_ELARION_CITY.md` (roster + persistence), `DESIGN_CORE_LOOP_AND_STRUCTURE.md`
(the seat-tier gate), `CityManifest.draft.README.md` (the data the builder reads).
**Drives:** the revision of `CityManifest.json` — every district below becomes a position cluster in the
manifest. This spec is the layout; the manifest is the encoding.

> **Purpose:** give the owner concrete coordinates + dimensions to plan placement from, anchored on the two
> headline requirements — (1) the **Tree of Life center stays sacred and clear**, (2) **Commerce lives in ONE
> corner and grows on seat upgrades** from a single stall into a market quarter.

---

## 0. Verified coordinate frame (ground truth — do not drift)

All values confirmed in source this session. Cite these, not memory.

| Constant | Value | Source |
|---|---|---|
| `WallHalfX` (E-W, X) | **±28** | `WallLayout.cs:126` |
| `WallHalfZ` (N-S, Z) | **±21** | `WallLayout.cs:129` |
| `SouthBowDepth` | **4** → south face at **Z = -25** | `WallLayout.cs:132` |
| `SouthBowHalfWidth` | **9** (bow face spans X ∈ [-9, +9]) | `WallLayout.cs:135` |
| Gates (inner wall openings) | **N (0,+21) · E (+28,0) · S (0,-25) · W (-28,0)** | `WallLayout.cs:251-274` |
| `GateGapHalf` | **≈2.0** (gate opening half-width) | `WallLayout.cs:141` |
| Heart of Elarion / Tree of Life | authored at **(0, 0, 1)** | `Content.cs:116` (`BuildElarion`) |
| Standing-stone ring radius | **4.4** (6 stones around Heart) | `Content.cs:268` |
| Plaza paving | ~**±8m** (col -5..+5 × HexWidth, row -4..+4 × HexDepth) | `Content.cs:21-27` |
| Main roads | 2-tile cross, plaza edge → each gate along X and Z | `Content.cs:59-75` |
| Outer **visual** wall (cosmetic) | ±42 (X) / ±33 (Z) | `VillageSceneBuilder.Walls.cs` per README |

**Interior buildable envelope:**
- **E-W (X):** -28 → +28 = **56 m**
- **N-S (Z):** -21 → +21 = **42 m**, plus the **south bow apron** (Z -21 → -25, X ∈ [-9,+9]) ≈ a 18×4 m
  bonus pocket. Max N-S envelope on the centerline ≈ **46 m**; usable interior ≈ **56 × 42** (call it **44** with the bow).

> **Gate-clearance caveat (verify before placing):** `Content.cs`'s `ValidateBuildingGateClearance`
> (`Content.cs:462`) checks the **OUTER** ring gates (0,±33)/(±42,0) at 8 m — NOT the inner 28/21
> openings enemies actually walk. **All placements in this spec also clear the INNER gate openings by ≥6 m**
> (the lane enemies use). Keep both rings in mind; the inner one is the real navmesh corridor.

---

## 1. Top-down grid sketch (56 × 44 interior, N = +Z up)

```
                          NORTH gate (0,+21)
        X=-28                  │                    X=+28
  Z=+21 ┌────────────────[ N GATE ]────────────────┐ Z=+21
        │ NW  CORNER        │  road-NS  │       NE  │
        │ ░ PRODUCTION ░    │           │  ░ HOMES ░│
        │ ░ (Forge/Lumber)░ │           │ ░(houses)░│
        │                   │           │           │
 W GATE ┤····· road-EW ·····●  HEART   ●····road-EW·├ E GATE
(-28,0) │                  ( Tree of Life )         │ (+28,0)
        │                   │  (0,0,1)  │           │
        │ ░ RESIDENTIAL ░   │           │ ░COMMERCE░│
        │ ░ (cottages)  ░   │  road-NS  │ ░ CORNER ░│
  Z=-21 └──────────┐  [ S GATE ]  ┌──────────────────┘ Z=-21
        SW CORNER  └──[bow X-9..+9]──┘   SE CORNER
                       Z=-25 (south bow apron: Farm/Granary)

  ● = plaza edge (~±8m)      ░ = district fill      road = 2-tile paved cross
  Sacred clearing = disc radius 9 around (0,0,1) — NO buildings inside it.
```

**The cross of roads** (N-S spine + E-W cross from plaza to all four gates) splits the interior into
**four quadrants**. Each quadrant is a district corner. The Heart sits at the crossing, ringed by the
sacred clearing.

---

## 2. TIGHT vs EXPAND — the call (with numbers)

**Owner's density modifiers in play:** −30% placement density and −20% building footprint shrink.

### The arithmetic
- Interior gross area: 56 × 42 ≈ **2,352 m²** (+ ~70 m² bow ≈ **2,420 m²**).
- Sacred clearing (disc r=9 around Heart): π·9² ≈ **254 m²** carved out and unbuildable.
- Road cross (two 2-tile-wide ≈ 3.4 m arms, ~56 m and ~46 m long, minus the plaza overlap):
  ≈ **300 m²** of circulation that can't hold buildings.
- Plaza ring inside the clearing already counted.
- **Net buildable after clearing + roads ≈ 2,420 − 254 − 300 ≈ 1,866 m²**, split across **4 quadrant
  corners** ≈ **~465 m² each**.

### Does the full district plan fit?
Roster target is ~28–36 placements (`DESIGN_ELARION_CITY.md` §3): Heart + 6 inner production + market +
tavern + 8–12 houses + 2–3 outer + 4 corner towers. With the **−20% shrink** a polyperfect `_M` building
normalized to ~7 m becomes ~5.6 m footprint (~31 m² incl. its plot fence at 3.4×3.4). At **−30% density**
you're placing ~20–25 buildings, not 36.

- 25 buildings × ~31 m² ≈ **775 m²** of building footprint.
- Against ~1,866 m² net buildable → **~42% packing**. **It fits — but it is genuinely tight** once you
  add the lived-in prop layer (stalls, wells, carts, fences, trees) the design calls for, plus the
  Commerce corner needs room to **grow two tiers** without a re-bake reshuffling its neighbors.

### Recommendation — **MODEST EXPAND.** Go to **WallHalfX = 32, WallHalfZ = 24** (keep the bow).

| | Current 28/21 | Proposed 32/24 |
|---|---|---|
| Interior E-W | 56 m | **64 m** (+8) |
| Interior N-S | 42 m (46 w/ bow) | **48 m** (52 w/ bow) |
| Gross area | ~2,420 m² | **~3,140 m²** (+30%) |
| Net buildable (after clearing+roads) | ~1,866 m² | **~2,500 m²** |
| Per-quadrant | ~465 m² | **~625 m²** |

**Why this size, not bigger:** +30% area gives every district breathing room and lets the **Commerce
corner reserve a full T1→T4 growth pad** (see §4) without crowding Production, while staying small enough
that the perimeter **rampart walk stays short** (the owner wants to walk the whole wall-top — a smaller
ring is faster to traverse and cheaper to garrison).

**Trade-offs of expanding (be honest):**
- **More terrain to fill** — +30% ground means more props/houses to avoid the "buildings on a lawn" look
  the design warns against. Budget ~6–10 extra filler placements/props.
- **Navmesh + bake cost** — larger walkable area = a bigger NavMesh surface and a longer bake. Marginal at
  this scale (still one small scene), but real.
- **Touches `WallLayout.cs` + a rebake** — changing `WallHalfX/Z` is a one-line-each edit, but it shifts
  every gate/road/wall position, so it **must** go through CLI as a work order with a full village rebake
  (per CLAUDE.md §3 — UI never fires batchmode). All §3/§4 coordinates below are given for **BOTH** the
  current 28/21 frame and the proposed 32/24 frame so the owner can pick without a re-spec.

> **If the owner prefers to stay TIGHT (28/21):** the plan still fits at −30%/−20%, but **cap houses at 8**,
> keep Commerce to **T1→T3** (drop the T4 quarter), and accept a denser, more cramped feel. The expand is a
> recommendation, not a blocker.

---

## 3. District map

Quadrant assignment uses the road cross as the divider. **N = +Z.** Corner = the wall corner that quadrant
hugs. Bounds are inset ~2 m off the wall line and ~3 m off the road centerlines so nothing voxels into the
wall collider or the enemy lane.

**Headline placements (owner's two requirements):**
- **Sacred center** = a clear disc, radius **9**, around the Tree of Life at (0,0,1).
- **Commerce = the SE corner** (recommended — see §6 for why, and the alt).

### District table

| District | Corner / region | Bounds (28/21 frame) | Approx size | Contents | Upgrade-gated? |
|---|---|---|---|---|---|
| **Sacred Center** | origin | disc **r = 9** around (0,0,1) | ~254 m² clearing | Tree of Life / Heart-seat, 6-stone ring (r4.4), plaza paving, Heart prop set (Altar, Pillars, Statues, Candlesticks) — **NOTHING else** | The seat **silhouette grows** with tier (the one object that visibly levels) |
| **Production (NW)** | NW corner | X ∈ [-26,-4], Z ∈ [+4,+19] | ~330 m² | **Forge** (now at +20,-10 → **move here**), **Lumbermill**, **Barracks**, **Arcane Tower** (now -20,-10 → move to NW or keep W) | Buildings unlock by seat tier (T1 Forge/Lumber → T2 Arcane → T3 Barracks slots) |
| **Residential (SW)** | SW corner | X ∈ [-26,-4], Z ∈ [-19,-4] | ~330 m² | 6–10 **houses/cottages** (`House_Medieval_Small/Medium` mix), **Tavern/Inn**, well, planters | House count raises with seat tier (population density) |
| **Commerce (SE)** | SE corner | **reserved growth pad** X ∈ [+6,+26], Z ∈ [-19,-4] | ~280 m² reserved | **Tiered** — single stall → shop → market row → market quarter. See **§4**. | **YES — the headline upgrade ladder** (gated on seat tier) |
| **Homes/Civic (NE)** | NE corner | X ∈ [+6,+26], Z ∈ [+4,+19] | ~280 m² | **Pet House** (keep at +20,+10), more houses, storage, ambient NPC hub | Pet House present T1; extra homes by tier |
| **South Bow Apron** | S, below SW/SE | X ∈ [-9,+9], Z ∈ [-25,-21] | ~70 m² | **Farm + Granary + Windmill** (food → army cap) — the orchard the bow was shaped for | Granary capacity by tier (raises squad/food cap) |
| **Defensive Ramparts** | wall-tops, full perimeter | the curtain wall ring (28/21) at height | walkable top | Towers (4 corner anchors, `building_watchtower`), siege defenses (WO-181), garrison firing positions — **NOT on the ground** | Tier-gated: T1 arrow tower → T2 rampart access → T3 siege (per Core-Loop §3) |

**32/24 frame deltas:** shift every district's outer bound by +4 X and +3 Z (e.g. Production NW becomes
X ∈ [-30,-4], Z ∈ [+4,+22]). The Commerce growth pad gains the room for T4 (§4).

### What moves where (existing 5 buildings → districts)

| Building | Current pos | District | New pos (28/21) | New pos (32/24) | Note |
|---|---|---|---|---|---|
| Pet House | (20, 10) | Homes/Civic NE | **(20, 10)** keep | (22, 12) | Already well-placed. |
| Arcane Tower | (-20, -10) | Production (move N) | **(-22, +8)** | (-25, +9) | Pull into NW production cluster, or leave on W edge. |
| Forge | (20, -10) | Production NW | **(-12, +14)** | (-13, +16) | Cross the map into the production quarter. |
| Farm | (-15, 14) | South Bow Apron | **(0, -23)** | (0, -24) | Move to the bow it was designed to frame; +Windmill secondary. |
| Market | (15, -20) | **Commerce SE (T1 anchor)** | **(18, -16)** | (20, -18) | Becomes the Commerce district seed — see §4. |

---

## 4. Commerce upgrade ladder (the headline feature)

**Concept:** Commerce occupies **one reserved corner pad** (SE). It is **empty at game start except a single
stall**, and **expands outward within that pad** as the player raises the Heart-seat tier. Because the whole
pad is reserved up front, growth **never reshuffles** Production/Residential — a re-bake just fills more of
the same corner. This is the manifest-friendly way to do "growing district": all four tiers' slots are
authored; a `minSeatTier` flag hides the higher ones until unlocked.

**Reserved Commerce pad (28/21):** X ∈ [+6, +26], Z ∈ [-19, -4] — a ~20 × 15 m corner block, inset off the
E wall (X=28), the S wall (Z=-21), and the E-W / S-N roads. **Origin of growth:** the T1 stall at the pad's
inner corner nearest the plaza, expanding toward the SE wall corner.

| Tier | Unlocks at seat | What appears | Footprint added | Coordinates (28/21) | Coordinates (32/24) |
|---|---|---|---|---|---|
| **T1 — Stall** | Seat **T1** (start) | A **single** `Marketplace_Stand_Simple` + a vendor NPC | ~3 × 3 m | **(12, -10)** (inner corner, by the road) | (13, -11) |
| **T2 — Shop** | Seat **T2** | Add **`House_Medieval_Large`** (the Market building) + a 2nd stall + awning | +~6 × 6 m | building **(18, -14)**, stall2 **(15, -11)** | (20, -16) / (16, -12) |
| **T3 — Market Row** | Seat **T3** | Add **2–3 more stalls** in a row + well + cart/wagon props → a shopping street along the SE | +~12 × 4 m row | stalls at **(20,-9),(22,-12),(20,-15)** | (23,-10),(25,-13),(23,-16) |
| **T4 — Market Quarter** | Seat **T4** | Add a **2nd building** (Tavern/Trade Hall `House_Medieval_Big`) + banners + paved market square infill; the corner reads as a full quarter | +~8 × 8 m | hall **(24, -17)**, square infill SE corner | hall **(28, -19)** *(needs the 32/24 expand — see §2)* |

> **T4 needs the expand.** At 28/21 the SE corner runs out of room for a second full building; the
> recommended 32/24 footprint is what makes the **Market Quarter** tier physically fit. **If staying tight,
> Commerce tops out at T3 (Market Row).** This is the single strongest reason to take the modest expand.

**Manifest encoding for the ladder:** each Commerce entry in `CityManifest.json` carries
`"district": "commerce"` and a new `"minSeatTier": 1|2|3|4` field. The builder places only entries whose
`minSeatTier ≤ currentSeatTier`. (CLI: this adds one optional int field to the buildings schema in
`CityManifest.draft.README.md` §Schema — coordinate via a WO; do not edit the builder from UI.)

---

## 5. How this maps to `CityManifest.json`

Districts become **position clusters** in the manifest — the manifest is the durable list the builder reads
on every rebake (`DESIGN_ELARION_CITY.md` §0/§6). This spec is the source for that revision:

1. **Add a `district` tag** to every `buildings[]` entry (`"sacred" | "production" | "residential" |
   "commerce" | "homes" | "bow" | "rampart"`). The README schema already lists `district` as a field —
   populate it from §3.
2. **Add `minSeatTier`** (int, default 1) to support the Commerce ladder (§4) and tier-gated production.
3. **Re-cluster the existing 5 buildings** to their new district coordinates per §3's move table.
4. **Commerce entries** = the 4-tier stack from §4 (all authored, gated by `minSeatTier`).
5. **Towers/defenses** stay rampart-side (`district: "rampart"`), not on the ground — matches
   `DESIGN_ELARION_CITY.md` §1 (defenses live on the wall-top) and WO-181.
6. If the owner takes the **32/24 expand**, the manifest's `meta` grounding constants update to the new
   half-extents and **all positions shift** — regenerate the cluster coordinates from the 32/24 columns
   above. This is a CLI work order (touches `WallLayout.cs` + a full rebake).

> **No hand-placement.** Per the empty-city root cause, every position here lands in the manifest, never in
> a hand-edited scene. A rebake then reproduces the districts exactly.

---

## 6. Open choices for the owner

1. **EXPAND or TIGHT?** Recommendation: **expand to 32/24** (unlocks Commerce T4, gives every district
   breathing room; costs +30% terrain to fill + a `WallLayout.cs` edit + rebake). Tight stays at 28/21 and
   caps Commerce at T3. **— DECIDE FIRST; everything else has both columns.**
2. **Which corner is Commerce?** Recommended **SE** (near the S gate = the "main entrance / bridge" face,
   so arriving traffic hits the market first; and it pairs with the bow's Farm/Granary for a
   food+trade "south economy" read). **Alt: NE** (pairs with the Pet House civic hub). **— OWNER CALL.**
3. **Where does Arcane Tower live** — pulled into the NW Production cluster, or kept on the **W edge** as a
   standalone landmark facing the W gate? (It's a tall silhouette — could anchor a corner.)
4. **House count** at T1 vs late tiers — start with how many cottages? (Affects how "lived-in" the village
   reads on day one; suggest 4 at T1 → 10 by T4.)
5. **Sacred clearing radius** — **9 m** proposed (comfortably clears the 4.4 stone ring + ~8 m plaza +
   breathing room). Tighten to **7** for a denser town, or widen to **11** for a more reverent, open
   center. **— OWNER CALL.**
6. **Bow apron contents** — Farm + Granary + Windmill proposed. Confirm this is the food district (it
   raises the squad/food cap per Core-Loop §4a), vs. using the bow for an orchard/decoration grove only.
7. **`minSeatTier` field** — approve adding it to the manifest schema (needed for the Commerce ladder + any
   tier-gated building). Low-risk additive field; CLI wires it.
```
