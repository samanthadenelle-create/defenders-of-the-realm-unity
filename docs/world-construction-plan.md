> ⚠ **STALE — targets the ABANDONED `Village.unity` / VillageSceneBuilder home.** Home hub is now `MainCastle_Hall`; `Village2` = raid target. Kept for history. Live reality: `CANON_GROUND_TRUTH_2026-06-26.md`.

# World Construction Plan — Outward-In Build Order

> Phased plan for authoring the Elarion world. **Build the outer rings first; leave the
> shared center (castle / moat / plaza) for LAST**, so center rework can't tear up finished
> outer work. Grounded in the existing builders — `VillageSceneBuilder` (interior) and
> `ExteriorTerrainBuilder` (wilderness) — plus WO-107 (climate regions) and the NORTH_STAR
> delivery ladder (Tower → Town → Explore → Settle → Build).

---

## 1. Current layout map (concentric rings)

Two editor builders author the scene today. `VillageSceneBuilder.BuildVillage()` owns the
interior + walls and, at its tail, calls `ExteriorTerrainBuilder.BuildExterior()` for the
outer landscape. Reading the actual constants in code (not the spec), the world is already
concentric:

| Ring | Extent (world units) | Owner method | Contents | State |
|---|---|---|---|---|
| **R4 Outer wilderness / horizon** | 300×300 terrain; landmarks at Z≈±230, X≈±228 | `ExteriorTerrainBuilder.BuildExterior` | Unity Terrain w/ 4 directional biomes (N forest→snow, E farmland, S "the Wound", W river valley), splatmaps, ~320 instanced trees, rock scatter, dawn skybox + fog, 3 distant landmarks | **Stable** — self-contained, no village-script deps |
| **R3 Biome / climate zones** | ±~48 to ±~128 from center (WO-107) | *new* `BuildClimateZones` (proposed, WO-107) | 4 sectored zones (Ashwood/Goldfields/Stoneback/Mirewood) keyed to the cardinal gates; `ZoneManager` drives per-zone fog/ambient/rain | **Planned, not built** |
| **R2 Approaches + spawns** | gate line out ~12 m; `WaveSpawnPoint`-N/E/S/W | `BuildApproaches` | Approach lanes + one `WaveSpawnPoint` per gate (`spawn-0..3`), wired into `WaveManager` | **Built** |
| **R2 Wall perimeter (visual)** | poly curtain at X=±42 / Z=±33; corner towers, mid towers, cardinal gates | `BuildWallPerimeter` (WO-101/104) | Polyperfect stone curtain wall, `Tower_Castle_Round` corners, square mid towers, `Gate_Medieval_*` | **Stable** |
| **R2 Wall ring (gameplay)** | KayKit ring at X=±28 / Z=±21 (`WallHalfX/Z`), south bow +4 | `BuildWallRing` + `BuildGates` + `ClearWallsNearGates` | `WallSegment` colliders/repair (KayKit mesh hidden; poly perimeter is the visual) + 4 cardinal gate gaps | **Stable** |
| **R1 Ground floor** | hex grass to ±(WallHalf + 14) | `BuildGroundFloor` | Flat Y=0 hex-grass field, interior + 1-hex seam beyond walls | **Stable** |
| **R0 CENTER — plaza / roads / keep / Heart** | plaza ~6×5 hex at origin; `+`-roads to gates | `BuildPlaza`, `BuildRoads`, `BuildElarion`, `BuildKeep`, `BuildBuildings`, `BuildCityDressing` | Plaza paving, N-S/E-W road cross, Heart of Elarion (0,0,0), Keep, 5 gameplay buildings, dressing | **IN FLUX** — actively reworked; the castle/plaza rebuild |

> ⚠ **Coordinate reconciliation flag.** WO-107 assumes a moat outer edge at ±48/±39 (from
> WO-104) and zone content from there to ±128/±119. The *committed code* places the poly
> curtain at **±42 / ±33** and the gameplay walls at **WallHalfX=28 / WallHalfZ=21**. Whoever
> implements `BuildClimateZones` must read the live `WallHalfX/Z` + perimeter constants, not
> the spec's numbers, or R3 zones will overlap the moat. **Do not hardcode ±48/±39.**

---

## 2. The outward-in build order (phased, outer → center)

Each phase is a ring. Earlier phases sit physically *outside* the center, so they can be
authored, reviewed, and rebaked without the in-flux castle rework touching them.

### Phase A — R4 Outer wilderness (DONE / maintain)
- **Adds:** the 300×300 terrain, biomes, trees, skybox, fog, distant landmarks.
- **Owner:** `ExteriorTerrainBuilder.BuildExterior` (already called at the tail of `BuildVillage`).
- **Safe because:** takes *zero* compile-time dependency on village MonoBehaviours; only adds
  terrain + props under `ExteriorRoot`. Its seam plateau (`SeamWeight`, `VillageHalfX/Z` =
  150/120) holds Y=0 under the whole footprint, so center geometry floats on a flat seam no
  matter how the plaza changes. **No action unless biomes need re-tuning.**

### Phase B — R3 Climate / zone regions (WO-107, NEXT)
- **Adds:** four sectored outer regions between the wall perimeter and the wilderness —
  Ashwood (N), Goldfields (E), Stoneback (W), Mirewood (S) — each with its terrain-plane
  palette, foliage, props, and a `ZoneManager` entry driving fog/ambient/rain. Maps the
  `spawn-0..3` directions to zone identities so attack direction tells a story.
- **Owner:** *new* `VillageSceneBuilder.BuildClimateZones(exteriorRoot)`, called from
  `BuildVillage()` **after** `BuildApproaches` / `BuildWallPerimeter` and **before** the
  exterior terrain call (or folded into a new sub-root). Plus `ZoneManager.cs` under
  `Assets/_Modules/Environment/`.
- **Safe because:** all content lands *outside* the wall ring (radius > R2). It never reads or
  writes plaza/keep transforms. Sits on the Phase-A seam; if the center moves, zones don't.

### Phase C — R2 Perimeter polish (walls / gates / towers / spawns)
- **Adds:** any wall-tier visuals (wood→stone→reinforced per NORTH_STAR), tower upgrades,
  gate force-fields, refined approach lanes. The defensive shell the player sees from inside.
- **Owner:** `BuildWallRing`, `BuildWallPerimeter`, `BuildGates`, `ClearWallsNearGates`,
  `BuildApproaches`, `WireGateForceFields`.
- **Safe because:** the perimeter is a closed ring around — but not inside — the plaza. Gate
  *positions* are the one coupling to the center (roads must meet gates); keep gate world
  positions stable while the plaza interior is reworked, and this ring stays decoupled.

### Phase D — R0 CENTER, LAST (castle / moat / plaza / keep / Heart / buildings)
- **Adds / reworks:** plaza paving, road cross, Heart of Elarion, Keep, the 5 gameplay
  buildings, city dressing, and the moat (when added).
- **Owner:** `BuildPlaza`, `BuildRoads`, `BuildElarion`, `BuildKeep`, `BuildBuildings`,
  `BuildCityDressing`.
- **Why last:** this is the actively-reworked region. Doing it last means every outer ring is
  already locked, so center churn never invalidates finished outer work — only the final,
  smallest ring iterates.

---

## 3. Why center-last (the rationale)

The castle / plaza / keep / moat is the **shared, in-flux** region — it is being redesigned
right now (castle arch already removed 2026-05-20; keep + plaza under review). Every other
ring is geometrically *outside* it and depends on it only through two stable seams:

1. **The Y=0 seam plateau** (Phase A) — outer terrain is held flat under the whole footprint,
   so plaza height/layout changes never crack the terrain join.
2. **Gate world positions** (Phase C) — the only place roads (center) meet the perimeter.

Keep those two seams fixed and the center can be torn up and rebuilt arbitrarily without
forcing a single re-author of R1–R4. If we built the center *first*, every later perimeter or
zone pass risks landing on geometry that the next castle revision moves — repeated rework.
Outward-in inverts that: **finish what won't move, iterate what will, last.**

This also matches the delivery ladder: R4/R3 are the **Explore** rung (a world beyond the
walls); R2 is **Defend the Town**; R0 is the base the player ultimately *re-authors* in the
Settle/Build rungs — the most volatile surface, correctly sequenced last.

---

## 4. Climate / zone regions (WO-107) → terrain catalog → ladder

WO-107's four regions map onto the polyperfect terrain-plane catalog
(`docs/polyperfect-asset-catalog.md`) as **sectored** outer zones (one cardinal quadrant
each), nested between the wall perimeter and the wilderness terrain:

| Zone (gate) | Feel | Terrain planes (catalog) | Spawn → enemy |
|---|---|---|---|
| **North — Ashwood** | corrupted, sunken, fog | `Terrain_Plane_Valley1–4` | `spawn-3` |
| **East — Goldfields** | warm rolling grassland | `Terrain_Plane_Plain` + `Terrain_Plane_Hill1/2` | `spawn-1` |
| **West — Stoneback** | rocky highland, +elevation | `Terrain_Plane_Hill3/4` + `Terrain_Plane_Slope1–4` | `spawn-2` |
| **South — Mirewood** | swamp, water, murk | `Terrain_Plane_Lake` + `Terrain_Plane_Valley3/4` | `spawn-0` |

`Terrain_Plane_Slope1–4` are the **transition tiles** at each zone↔perimeter boundary (±1–3 m
over ~20 m) so there's no hard flat edge. Note the **biome-axis duplication**: Phase A's
`ExteriorTerrainBuilder` *already* paints N-forest / E-farmland / S-Wound / W-valley into the
horizon terrain. R3 zones should **align their cardinal identities to A's existing biomes**
(don't fight a green-east splat with a grey-west zone). Goldfields-E and Stoneback-W match A
cleanly; Ashwood-N vs A's "forest→snow" and Mirewood-S vs A's "the Wound" need a deliberate
reconcile so the two rings read as one continuous gradient, not two themes. **Ladder fit:**
distinct readable outer regions are the *Explore* pillar — the ground players venture onto to
claim resource nodes (WO-111) before the Settle rung.

---

## 5. Parallelization

| Ring | Parallel-safe? | Note |
|---|---|---|
| R4 wilderness (`ExteriorTerrainBuilder`) | **Yes** | Separate file, separate root, no village deps. An agent can iterate biomes in its own pass. |
| R3 zones + `ZoneManager` | **Mostly** | `ZoneManager.cs` is a free-standing module — fully parallel. The `BuildClimateZones` *method body* lives in `VillageSceneBuilder.cs`. |
| R2 perimeter | **Serialize** | All in `VillageSceneBuilder.cs`. |
| R0 center | **Serialize — exclusive** | The in-flux region; one agent only. |

**Hard rule (CLAUDE.md §9):** `VillageSceneBuilder.cs` is a *serialization bottleneck* — only
**one** agent/branch edits it at a time. So although rings R3/R2/R0 are conceptually
independent, every change that touches `VillageSceneBuilder.cs` must be serialized through the
single build lane (produce diffs; the lane owner integrates). Genuinely parallel work =
anything in its **own file**: `ExteriorTerrainBuilder.cs` (R4) and `ZoneManager.cs` (R3 logic).
Everything authored *into* the builder is one-at-a-time, outer ring committed before the next
inner ring starts.

---

## 6. Risks

- **NavMesh rebake scope.** `BakeVillageNavMesh` runs over the whole `VillageRoot` near the end
  of `BuildVillage`, *before* the exterior call. Adding R3 zone geometry (esp. if it carries
  colliders or raises terrain) widens the bake area and the bake time, and zone slopes can
  punch holes in the agent mesh. Keep zone props collider-stripped (as `BuildWallPerimeter`
  already does via `StripColliders`) unless they're meant to block pathing, and re-verify
  enemy paths from each `spawn-N` to the Heart after any outer-ring change. `DOTR_SKIP_NAVMESH=1`
  exists for crash-bisect builds only — never ship a zone change unbaked.
- **Corruption-on-resave (HARD).** Per CLAUDE.md §3 and project memory, `Village.unity` must
  **never** be hand-edited — it regenerates from the builders, and a manual re-save has
  corrupted its serialization before (level3 "Position out of bounds" crash). All ring work
  goes through the builder methods + a rebake; no direct scene editing, ever.
- **Biome seams between rings.** Two seam surfaces must stay clean: (a) Phase A's Y=0 plateau
  vs the hex ground floor (already tuned to `TerrainBaseDepth = 0.5` to avoid Z-fighting —
  don't regress it); (b) the new R3 zone planes vs A's terrain splat at the ±~128 boundary —
  use `Terrain_Plane_Slope` transitions and match cardinal themes (see §4) so the join reads
  continuous.
- **Coordinate drift (see §1 flag).** Implementing R3 against the WO-107 spec's ±48/±39
  instead of the live `WallHalfX/Z` + perimeter (±42/±33, ±28/±21) will overlap the moat ring.
  Read the constants from code.
- **Build-button bypass.** Bake/rebuild only via `Defenders > Week 3 > Build Village Scene`
  (batchmode `VillageSceneBuilder.BuildVillage`) in a work order for CLI — never the Build
  Profile button, never with the editor open (project lock).

---

## Headline phase order

**A. Outer wilderness (done) → B. Climate/zone regions (WO-107) → C. Perimeter walls/gates/spawns → D. CENTER castle/moat/plaza/keep — LAST.**
Outer rings lock first; only the in-flux center iterates at the end, on two fixed seams (Y=0 plateau + gate positions).
