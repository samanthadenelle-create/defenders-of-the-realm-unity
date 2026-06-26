> ⚠ **SUPERSEDED — retired "Avalon"/"Keep"/"Blaise" naming + the abandoned Village.unity scene.** Kept for history. Live reality: `CANON_GROUND_TRUTH_2026-06-26.md`.

# Avalon Village — Interior Layout Spec (Unity / KayKit Hexagon Pack)

**Status:** Canonical design for the Avalon village close-up scene in the Unity port. Replaces the previous "rigid square" placeholder with a shaped castle-town built from KayKit Medieval Hexagon Pack assets.
**Owner:** DeNelle Studios
**Date:** 2026-05-18
**Spec source:** Owner direction 2026-05-18 — _"the dream was castle and area by castle wall. the shape can be creative as long as spacious enough for a city."_ + Unity agent's creative-direction memo.

---

## 1. The vision in one paragraph

Avalon is a **shaped castle-town**, not a tight defensive square. At its heart stand two anchors side-by-side: **Elarion** — the sentient world-tree, sacred, violet-veined — and the **Keeper's Keep**, a small medieval castle from which the realm is watched. Around them, a generous walled town: residential quarter, market plaza, the five spec buildings of the gameplay loop (Crystal Mine, Pet House, Workshop, Farm, Arcane Tower) plus a believable city dressing (houses, smithy, tavern, church, well). A curtain wall encloses the town with four cardinal gates — the only ways in or out, and the four points the Hollow Ones attack. Beyond the gates lie the approach lanes where waves spawn.

The mood is **lived-in fairy-tale**, not military fortress. Wide streets, mossy stone, small plots of garden. The Folk live here; the Keeper tends them.

## 2. What's canon-locked vs creatively free

**Canon-locked (do not deviate):**
- A walled town with **4 cardinal gates** (north, south, east, west) — fixed wave threat-points per `docs/four-cardinal-gates-spec.md`
- The Heart (Elarion) at the centre — sacred world-tree, violet emissive
- 5 named spec buildings inside the walls: Crystal Mine, Pet House, Arcane Tower, Workshop, Farm (per `CLAUDE.md` and existing React scene)
- Canon names: **Avalon** the town, **Elarion** the tree, **Blaise** the mage, **Alduin the Mournful** the antagonist
- All copy in narrative-bible voice

**Creatively free (Unity agent's call within reason):**
- Wall **shape** — rectangular or gently irregular; NOT a tight square. Generous interior.
- Wall **size** — large enough for a real city, not just a courtyard. Target ~30 hexes wide east-west, ~24 hexes north-south.
- **Interior layout** — building positions, road branching, plaza placement
- **City dressing** — which "extra" buildings to add (houses, market, blacksmith, tavern, church, well, townhall) and where
- **Outer landscape** — what's outside the walls beyond the approach lanes
- The **Keeper's Keep** addition (see §3) is a creative augmentation beyond the strict React-scene spec; logged as a creative decision per Unity agent's memo

## 3. The Heart-and-Keep — the two centerpieces

The village has **two side-by-side landmarks** at its centre, not one:

### 3.1 Elarion — the Heart (canon, sacred)
- World-tree, ancient. The reason Avalon exists.
- Use the largest pine/oak mesh from the **KayKit Forest Nature Pack** if available; otherwise a hexagon-pack tree scaled to ~3× normal
- Custom violet emissive material on the crystalline veins running up the trunk (`#9d6fff`, intensity 0.6, soft pulse 0.2 Hz)
- Stands on a small raised mound (use `hex_grass_hill` or a custom flat hex with slight elevation)
- A 6-hex ring of standing stones around it — props from the pack if available (`prop_stone_pillar` or similar)
- **NO building on this hex** — the tree IS the centerpiece

### 3.2 The Keeper's Keep (creative addition, logged decision)
- A small medieval castle — `building_castle` from the KayKit Hexagon pack
- Placed **adjacent to Elarion**, slightly south-east (~2 hexes offset) so the two landmarks frame the central plaza without competing for the dead-centre spot
- The Keeper's home; the operational seat of the watch
- Smaller than its real-world referents — readable as "fortified manor house," not "imperial palace"
- Optional: a small banner (`prop_banner_avalon` if available or `prop_flag` recolored to violet)

### 3.3 The plaza
- A paved open space between Elarion and the Keep
- 4-6 hexes of `hex_stone` or `hex_paved` ground tiles
- Roads radiate out from this plaza in 4 cardinal directions (one to each gate)
- This is where the Wardens (when v1.1 ships the Wardens spec) gather; where the player's onboarding tour starts (per `docs/first-watch-spec.md`)

## 4. The curtain wall — shape, dimensions, materials

### 4.1 Shape
- **Rectangular, slightly wider east-west than north-south.** Target 30 × 24 hexes interior.
- Corners use `wall_corner_A` (or `wall_corner_B` for variety; pick one and stay consistent per corner so symmetry reads)
- Straight runs use `wall_straight` pieces
- **No diagonal walls** — KayKit hex pack walls are axis-aligned; trying to do diagonal runs would create geometry chaos. Rectangular is the cleanest readable shape.

### 4.2 Optional shape variation (creative)
If "tight rectangle" feels too generic, add **one or two creative variations**:
- A slight **bow-out on the south side** to accommodate the orchard / Farm building plot (add 2-3 hexes of frontage)
- A **small bastion at the NE corner** where the Workshop and Arcane Tower live (extra wall_corner pieces forming a defensive bulge)
- **No more than two such variations** — too many and the shape reads as random, not designed

### 4.3 The four gates
- One `wall_straight_gate` placed at the centre of each cardinal side
- Heart-bound force-field per `docs/gate-design-spec.md` + `docs/four-cardinal-gates-spec.md`
- Twin pillars + violet shimmer plane (existing design; KayKit's gate mesh provides the pillars natively)
- Each gate is the **only break in the wall** on its side — no extra postern gates, no side passages
- Wall sections must seat **flush against gate pillars** per the T59 lesson learned in the React build — the KayKit hex pack's modular straight + gate pieces solve this geometrically (they're designed to align)

### 4.4 Wall tiering (for upgrade visuals)
The React spec defines wall tiers (Wooden / Stone / Steel / Warded) per `WALL_TIERS` in code. In Unity, mirror via material swap on the same mesh:
- Tier 0 (Wooden Fence): wood-grain material
- Tier 1 (Stone): mossy stone material
- Tier 2 (Steel): darker stone with iron banding
- Tier 3 (Warded): warm aged-stone base + violet emissive rune band per `docs/launch-triage-2026-05-18.md` T58

## 5. The five named gameplay buildings — assignments + locations

Per the existing React Village3D pattern, five named buildings are the player's interactable surfaces. Each gets a KayKit Hexagon FBX assignment + a quadrant placement.

| Gameplay name | KayKit asset | Quadrant | Why there |
| --- | --- | --- | --- |
| **Crystal Mine** | `building_mine` | Northwest (rocky district) | Mines belong near stone/hills. NW is also "approach to the cold mountains" thematically (toward D5 in the realm map). |
| **Pet House** | `building_stables` | Southwest (creek-side) | Stables-style building. Cozy, lived-in, near where the pets can run. |
| **Arcane Tower** | `building_tower_A` _(fallback: `building_shrine` or `building_watchtower`)_ | South-central (near the Keep) | Tower silhouette is visually prominent. South-of-Keep places it visible from the title-screen camera. |
| **Workshop** | `building_workshop` | Northeast (artisan district) | Workshops belong near the smithy. NE = "approach to the forest" → wood + crafting context. |
| **Farm** | `building_windmill` _(fallback: `building_watermill` if a windmill mesh is unavailable)_ | East (open ground) | Windmills want clear wind. East side near the wall, with cropland between mill and wall. |

Each building sits on a **2×2 hex plot** of grass with a low fence (`fence_wood_short` or `fence_stone_low`) marking the property line. The fence isn't a collider — it's dressing.

## 6. City dressing — the lived-in details

The five gameplay buildings + Heart + Keep is the canon core. The village ALSO needs lived-in dressing so it feels like a real town, not a game-board with five oddly-isolated structures.

### 6.1 Residential cluster (southwest quadrant)
- 4-6 small houses: `building_home_A` × 3, `building_home_B` × 2-3 (rotate for variety)
- Tightly grouped along a side street, with small garden plots (`fence_wood_short` around each)
- 1 well (`building_well`) at the cluster's centre — the Folk gather here
- Hex ground = mix of `hex_grass` + `hex_dirt_path` for foot-traffic-worn paths

### 6.2 Market quarter (south, near the plaza)
- 1 market (`building_market`) on the south side of the plaza
- 1 tavern (`building_tavern`) on the south-east corner of the plaza
- 1 church (`building_church`) — small, on the north side of the plaza, NOT competing with Elarion or the Keep
- A short row of market stalls along the south wall (if `prop_market_stall` exists; otherwise omit)
- Cobble-paved (`hex_stone`) ground here, not grass

### 6.3 Workshop quarter (northeast, near the Workshop building)
- 1 blacksmith (`building_blacksmith`) adjacent to the Workshop
- A small fenced yard between them with anvils / lumber / tools as props
- 1 townhall (`building_townhall`) at the NE corner of the plaza — small civic building

### 6.4 Farm / orchard (east, outside the building footprint but inside the walls)
- The Farm building (windmill) sits on a 2×2 plot
- Around it, 6-10 hexes of `hex_grass_orchard` or `hex_grass_crops` (apple trees / wheat fields)
- A small farmer's hut (`building_home_A`) on the orchard's edge

### 6.5 Northern open ground
- Mostly open `hex_grass` between the Crystal Mine and the Workshop
- A few scattered trees from the Forest Nature Pack
- 1 `building_shrine` (if not used as Arcane Tower fallback) — small wayside shrine

## 7. Road network — how the town connects

### 7.1 The cross-axis
- **North-South spine**: from the N gate through the central plaza past the Keep to the S gate. Main thoroughfare. Wide (2-hex wide stone paving).
- **East-West cross**: from the E gate through the plaza to the W gate. Same width. Crosses the N-S spine at the plaza.
- These two roads form a `+` shape with the plaza at the centre.

### 7.2 Secondary roads
- Branch off the main cross to reach each gameplay building plot
- Narrower (1-hex wide, dirt path tile `hex_grass_road_straight`)
- Curved where convenient — the KayKit pack has road segments designed for non-straight paths

### 7.3 Plaza geometry
- The intersection of N-S spine + E-W cross is the plaza (4-6 hexes)
- Elarion stands at the plaza's centre-west
- Keep stands at the plaza's centre-east
- The plaza's stone tiles are slightly lighter than the road stone, marking it as ceremonial space

## 8. Approach lanes (outside the walls)

Each gate's approach is the buffer zone between the wall and the wave spawn point. These are NOT part of the walled town but ARE part of the playable scene.

### 8.1 Each approach
- **3-5 hexes of `hex_grass_road_straight`** extending outward from each gate
- Light foliage on either side (`hex_grass` with scattered tree props)
- A few low rocks / fences for cover dressing
- Ends at a clear **wave spawn zone** — a 3×3 hex grass plot where enemies materialize

### 8.2 Per-gate flavour
- **North approach** — light forest. Trees thicken toward the northern horizon. Thematically: the road to the Wolfwarden's Vigil + the cold mountains (per realm map).
- **East approach** — open farmland. Wheat / apple orchards continue beyond the wall.
- **South approach** — wider road. Thematically the road to the Wound (per realm map endgame). A few barren patches as you walk away.
- **West approach** — a small river crossing if the bridge mesh exists in the pack. Mira's path leads west.

### 8.3 Wave spawn points
- One per gate, 5 hexes beyond the gate centre
- Invisible markers in Unity (empty GameObject with `WaveSpawnPoint.cs` script)
- The Hollow Ones materialize here, then walk the approach road toward the gate

## 9. Outer landscape — biomes, elevation, natural architecture

**Wide creative latitude here — owner directive 2026-05-18: _"exterior map should create biomes if possible. with height and depth and natural ground architecture. even random roads and paths are ok. creative is good."_**

This is the realm beyond Avalon's walls. The four approach lanes (§8) anchor the cardinal directions; everything else is a real landscape with elevation, biome variety, water, and stone — not a flat tiled plane.

### 9.1 The render approach — hybrid hex + Unity Terrain

The interior + approach lanes stay on the **hex tile grid** for visual consistency with the walled town. **Beyond the approach lanes, the landscape transitions to Unity's Terrain system** (heightmapped 3D terrain with textures, trees, detail meshes). This hybrid gives the best of both:

- Hex tiles inside walls = readable game-board feel, consistent with the design's geometric origins
- Unity Terrain outside = real elevation, rolling hills, natural-looking topology, no grid artifacts

The transition is **soft** — the last ring of hex tiles fades into the terrain via a 1-hex-deep blending zone (matching grass color + slight elevation match). The seam should not be visible from gameplay camera angles.

**Terrain extents:** ~300×300 world units around the village (the village itself is ~30×24 hexes × 10u = 300×240u, so terrain extends ~equal distance in every direction beyond walls). Enough for a felt-real outer world; not so much it tanks FPS.

### 9.2 Biome distribution by direction

Each cardinal direction beyond Avalon's wall expresses the realm-map narrative (per `data/realm-map.json`). The Unity agent picks the visual register; this is the contract:

**North (toward Cold-Wandered's Pack + Wolfwarden's Vigil):**
- Gradually rising elevation — gentle slopes within 50u, then steeper hills
- Biome transitions: temperate forest → pine forest → bare rock → snow line (at terrain edge)
- Stone outcrops poking through grass (use the Forest Pack's rock meshes or scaled hexagon-pack mountain props)
- A small **mountain ridge** silhouetted at the northern horizon — sets visual scale
- Snow texture on the highest elevations only

**East (toward the Apothecary's Vault + Healer's Garden):**
- Gentle rolling **farmland** — soft elevation changes (5-10u variation)
- Crop fields (use a custom wheat-detail-mesh on terrain OR `hex_grass_crops` extending past walls)
- Apple orchards (Forest Pack apple trees if available; else clusters of small trees)
- A few **stone walls** crossing the landscape (`fence_stone_low` snaking through the fields) — implies historical land division
- Gradual visual warmth — sunlight feels stronger here

**South (toward At the Edge / the Wound):**
- Gradually descending elevation — the land sinks toward the Wound
- Biome transitions: grass → yellowing barren → dark cracked stone → cosmic-edge (terrain darkens)
- Sparse dead trees (Forest Pack's autumn / bare tree variants)
- The very edge of the visible terrain has a **subtle violet haze** suggesting the Wound's distant pull
- A single **distant standing stone** or broken statue at the southern horizon — narrative seed

**West (toward Last Keeper's Walk / Mira's path):**
- Mixed elevation — small hills broken by a **river valley** running roughly north-south
- A **river** crosses the western approach lane (use Unity's water shader OR a strip of `hex_water` extending beyond walls)
- A small **stone bridge** at the W gate's approach (Forest or Hexagon pack bridge prop)
- Mist / volumetric fog clings to the river bottom — Mira's quieter, contemplative register
- A few **standing stones** along the ridge above the river — markers from before the town

### 9.3 Height + depth — natural ground architecture

Real elevation, not flat terrain. Use Unity's **Terrain Tools** with the following palette:

**Macro elevation:**
- **Avalon sits at world Y=0** — neutral baseline
- North terrain rises gradually to Y=+15 to +30 over the visible distance
- East terrain stays mostly Y=0 with gentle ±5u rolls
- South terrain descends to Y=-10 to -15 (the land sinks toward the Wound)
- West terrain varies most: ridges at Y=+10, river valley at Y=-5, opposite ridge at Y=+12

**Micro features:**
- **Boulders + rock outcrops** — Forest Pack rock meshes scattered on slopes
- **Cliff edges** in 1-2 strategic places (where terrain elevation changes >10u over <5u distance — Unity Terrain handles this)
- **Small ponds** in low spots (water shader patches, ~2-3 per quadrant)
- **Eroded gullies** running downhill (terrain Paint Texture using a dark dirt brush)
- **Cave entrances** suggested at 1-2 cliff faces (just visual — not interactable in v1)

**Texturing:**
- Base grass texture (warm green, matches village interior)
- Cliff / exposed stone texture (warm tan-grey, matches Warded Wall palette)
- Mud / path texture for foot-worn routes
- Snow texture for elevations above Y=+20 (north only)
- Dark / dead texture for elevations below Y=-8 (south only, toward Wound)
- Blend zones soft (Unity's terrain texture splatmaps)

### 9.4 Natural paths + roads — creative latitude

In addition to the four formal **approach lanes** (paved, gate-aligned, used by enemy waves):

**Add 3-5 "natural" paths** that wander the exterior — owner-approved as creative additions:
- **Animal track** — narrow, organic curves; from N gate's approach, branching off to follow a ridge line east
- **Old smuggler's path** — across the western river valley, doesn't connect to any specific destination; just exists in the landscape
- **Pilgrim's road** — from S gate's approach, winding south-west toward where the Last Keeper's Walk dungeon would be (per realm-map narrative); fades into the distance
- **Hunter's trail** — through the eastern orchards, between trees; just visible foot-traffic, not paved
- **Crystal vein path** — in the NW corner near the (interior) Crystal Mine, a worn path leading to a (visible-but-not-interactable) cliffside ore deposit

These paths are **NOT for gameplay** — enemies don't use them, the player can't traverse them for fast-travel. They're **landscape storytelling**. Use the Terrain Detail tool to paint dirt textures along curved bezier paths. The Unity agent picks the exact routes.

### 9.5 Distance + atmospheric perspective

**Skybox:**
- Soft dawn lighting (sun at low angle, ~15° above horizon, slightly warm)
- Procedural skybox with gentle pink-violet tint near horizon, soft blue overhead
- The Heart-Wing dragon silhouette from `T53` may optionally appear in the sky as a distant flying creature (Cinemachine background animation, slow drift)

**Volumetric fog:**
- Light atmospheric haze (Unity's Built-in fog or HDRP volumetric clouds if URP supports them at the target API level)
- Fog density gradient — denser in the south (toward the Wound), lighter in the east
- Distant terrain reads softer, closer terrain crisper — the natural depth cue

**Distant landmarks:**
- A single **mountain peak** silhouetted at the northern horizon (terrain feature OR painted skybox)
- A **distant tower** suggested in the west (Mira's place — just a silhouette, not modeled)
- A **dark crack** in the southern horizon (the Wound's edge — subtle, easy to miss on first look, narratively loaded for players who notice)

### 9.6 Forest / vegetation pass

- Use **Unity Terrain's tree painter** (efficient instanced rendering) for forest density
- Pine trees densest in the NE quadrant (forest biome)
- Apple / fruit trees clustered in the east (orchard biome)
- Sparse dead trees in the south (barren biome)
- Bare / no trees in the north on rock outcrops
- Western valley has streamside willows + birch

Tree count budget: ~200-400 tree instances. Unity's instanced tree rendering handles this without FPS impact.

### 9.7 Wildlife / ambient life (optional, low priority)

If time allows after Week 5-6 main content lands:
- 1-2 deer wandering the eastern orchards (Forest Pack character if available; loop a slow walking animation)
- A flock of crows occasionally crossing the southern barren (particle-based or simple flapping mesh)
- Smoke rising from one of the village houses' chimneys (Unity Particle System)

None of these are gameplay-critical. Pure ambient detail. Skip if scope pressure.

### 9.8 The seam — making exterior + interior feel like one world

**Critical detail for immersion:**
- The grass texture inside the walls matches the grass texture immediately outside the walls (same material; same colour values)
- The elevation at the wall's base = Y=0 exactly, transitioning to the terrain's heightmap smoothly (use a 1-2 hex blend zone)
- The lighting (directional sun) is shared — no shadow discontinuities at the wall line
- The fog density is continuous across the wall boundary

If the player walks up to a gate and looks out, the exterior must feel like a continuation of the interior, not a different scene loaded behind a portal.

## 10. Building inventory — the full asset map

For Unity agent's quick reference. All from KayKit Medieval Hexagon Pack 1.0.1:

| Asset | Quantity | Purpose |
| --- | --- | --- |
| `building_castle` | 1 | The Keeper's Keep (centre, beside Elarion) |
| `building_mine` | 1 | Crystal Mine (NW) |
| `building_stables` | 1 | Pet House (SW) |
| `building_tower_A` | 1 | Arcane Tower (S of Keep) |
| `building_workshop` | 1 | Workshop (NE) |
| `building_windmill` | 1 | Farm (E) |
| `building_home_A` | 3 | Residential cluster |
| `building_home_B` | 3 | Residential cluster (variety) |
| `building_market` | 1 | Plaza south side |
| `building_tavern` | 1 | Plaza SE corner |
| `building_church` | 1 | Plaza north side |
| `building_blacksmith` | 1 | NE artisan district (near Workshop) |
| `building_well` | 1 | Residential cluster centre |
| `building_townhall` | 1 | NE plaza corner |
| `building_shrine` | 1 | Northern open ground (wayside shrine) |
| `building_home_A` (small farmer's hut) | 1 | Orchard edge |
| `wall_straight` | ~40 | Curtain wall straight runs |
| `wall_corner_A` | 4 | Curtain wall corners |
| `wall_straight_gate` | 4 | The four cardinal gates |
| `hex_grass` | ~200 | Default ground |
| `hex_stone` / `hex_paved` | ~30 | Plaza + main roads |
| `hex_dirt_path` | ~20 | Foot-traffic-worn paths |
| `hex_grass_road_straight` | ~10 | Approach lanes |
| `hex_grass_orchard` / `hex_grass_crops` | ~8 | Farm orchard |
| `fence_wood_short` / `fence_stone_low` | ~15 | Building plot fences |
| `prop_stone_pillar` | ~6 | Standing stones around Elarion |
| Forest pack tree (large) | 1 | Elarion the Heart |
| Forest pack tree (small) | ~10 | Scattered foliage |

**Estimated total** ≈ 350 prefab instances, well within Unity's mobile-friendly draw-call budget given KayKit's atlas-texture optimization.

## 11. ASCII top-down sketch

```
                            (N approach + spawn — forest fades north)
                                      [N GATE]
                                          │
                ┌─────────────────────────│─────────────────────────┐
                │  hex_grass                              hex_grass │
                │     ┌──Mine──┐      ┌── workshop yard ─┐         │
                │     │ Mine   │      │ Workshop  Smithy │         │
                │     └────────┘      │      ┌──────┐    │         │
                │      (NW quad)      └──────│ Hall │────┘   ●     │
                │                            └──────┘       shrine │
                │            ┌─────  PLAZA  ─────┐                 │
        [W]───── ─── ── ── ─Elarion──+── Keep ── ─── ── ── ── ──[E]
                │            └──╤── (cross)──╤───┘     ┌─Windmill─┐│
                │       Church  │            │  Tavern │  Farm    ││
                │      ┌─────┐  │            │ ┌─────┐ │  +       ││
                │      │home │  │   Market   │ │home │ │ orchard  ││
                │      └─────┘  │  ┌──────┐  │ └─────┘ └──────────┘│
                │       (SW quad)│ │ Market│ │  (Plaza-S)          │
                │       Pet House└─└──────┘─┘ Arcane Tower          │
                │      ┌─────┐               ┌─────┐               │
                │      │     │     Well      │Tower│               │
                │      └─────┘     ●         └─────┘               │
                │   home  home                                      │
                └─────────────────────────│─────────────────────────┘
                                          │
                                      [S GATE]
                            (S approach + spawn — barren toward Wound)
```

## 12. Acceptance criteria

A reviewer (owner) opens Unity Editor, loads the Village scene, presses Play. They should see:

1. A walled town of clear shape (rectangular-ish, generously sized, NOT a tight square)
2. Four cardinal gates with violet force-field pillars, visibly the only breaks in the wall
3. **Two centerpieces**: Elarion (the tree) and the Keep (the castle), side-by-side at the centre
4. A central plaza connecting four roads to the four gates
5. The five named gameplay buildings present and visually distinct: Crystal Mine, Pet House, Workshop, Arcane Tower, Farm — placeable, tappable for build/upgrade UI (when wired)
6. A residential cluster of 4-6 houses with a well, in one quadrant
7. A market quarter (market + tavern + church) around the plaza
8. Approach lanes beyond each gate ending at a clear wave-spawn zone
9. Outer landscape (sparse trees, hills, skybox) suggesting a continuous world
10. FPS holds 60 on the Seeker target hardware during a stationary scene render
11. All assets sourced from the imported KayKit Hexagon pack + Forest Nature pack — no missing-mesh placeholders
12. Camera (Cinemachine) can fly through the village without clipping into walls or buildings

## 13. Decisions log entries (for `docs/unity-decisions.md`)

Per the v2 Unity port spec's sync protocol, log these creative decisions:

```
| 2026-05-18 | Castle Keep adjacent to Elarion | Tree alone at centre | Two anchors frame plaza better; "Keeper's home" was implied but unspeced in React | Yes — Keep can be relocated or removed |
| 2026-05-18 | Wall shape: shaped rectangle (~30×24 hex) not tight square | Tight square | Owner-directed creative latitude per docs/avalon-village-layout-spec.md §2 | Yes — wall can be re-shaped freely |
| 2026-05-18 | Residential cluster in SW quadrant | Distribute houses evenly | "Village quarter" reads more lived-in than scattered homes | Yes |
| 2026-05-18 | Forest pack tree used for Elarion centerpiece | Hexagon pack tree | Forest pack has larger, more visually anchoring tree meshes | Yes — swap to hex pack tree if Forest pack unavailable |
```

## 14. Open questions for the owner

1. **Castle Keep size**: should it be a single 2×2 hex footprint (modest manor) or a 3×3 hex footprint (proper small castle)? Default: 2×2.
2. **Plaza paving pattern**: simple stone, or fancy patterned (using two stone tile variants alternating)? Default: simple stone for clarity.
3. **The Heart's standing-stone ring**: 6 stones (compass directions) or 12 stones (more ceremonial)? Default: 6.
4. **Time-of-day**: dawn, midday, dusk, or dynamic? Default: soft dawn for "fairy-tale just-awake" register.
5. **Outer landscape draw distance**: how far does the visible terrain extend beyond the walls? Default: ~10-15 hexes of meaningful terrain, then skybox haze.
6. **Approach lane variation**: should each gate's approach have distinct flavour per realm-map direction (north→forest, east→farm, south→barren, west→river)? Default: yes — reinforces the realm-map narrative.

## 15. Sequencing

This spec is for the **Unity Village scene** (`Village.unity`). It does NOT change the React Village3D code (which stays as-is for v1 launch per the v1 scope lock).

Build order in the Unity stream:
- **Week 3 (current)**: Implement this spec. Replaces the existing "rigid square" VillageSceneBuilder.cs output.
- After Build: open Editor, walk through with acceptance criteria from §12, capture screenshot to `docs/screenshot-village-week3.png`.
- Log creative decisions per §13 in `docs/unity-decisions.md`.
- Move on to Week 4 (gameplay systems — wave manager, hero abilities, breach detection).

---

_The Folk made it spacious. The walls hold what matters. By lantern. By oath. By Heart._
