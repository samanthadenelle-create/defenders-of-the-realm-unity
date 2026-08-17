<!-- era-sweep-2026-08-17 -->
> ### ⛔ ERA SWEEP 2026-08-17 — CLOSED as OBSOLETE (deleted system)
> **Dead thing:** OuterWorld.unity. **Git first-add:** 2026-06-22.
> **Evidence:** `Assets/Scenes/OuterWorld.unity` is absent from disk and from `git ls-files`; the WO is a new `OuterWorldBuilder` pass and states "no new scenes — all in `Village.unity`'s exterior", so BOTH of its target scenes are gone.
> Only the `**Status:**` line was rewritten. The body below is UNTOUCHED — CLAUDE.md §15, *"frozen, never rewrite"*.
> **TO REVIVE:** nothing was deleted and not one line of the body below was changed. If this work is still wanted, re-date the WO (add a `**Minted:** <today>` line), re-point it at the live scene/system, and set `**Status:** READY TO IMPLEMENT`.

# WORK ORDER 142 — The Outer World: A Lived-In Realm Beyond the Walls

**Status:** CLOSED — OBSOLETE: OuterWorld.unity no longer exists (era sweep 2026-08-17)
**Date:** 2026-05-30
**Priority:** High — world feel / "lived-in" pass; the visible payoff of Rung 3 (Defend + Explore)
**Lane:** World/Environment (architect lane) — serialization-sensitive, single-toucher
**Scope:** Large — new `OuterWorldBuilder` editor pass over the existing exterior terrain + a reusable wandering-content system; phased so no single bake is an unbakeable blob
**Depends on:**
- WO-107 (climate regions + `ZoneManager` + the 4-direction map) — **direct parent, reconcile heavily**
- WO-112 (ward-tether / exploration reach) — wards are the gameplay spine; this WO is the *world they sit in*
- Existing `ExteriorTerrainBuilder` (the 300×300 terrain + biomes already exist — **build ON it**)
- `AmbientNPC` / `TownsfolkController` (the village wander pattern — **reuse, do not reinvent**)

**North Star:** `docs/NORTH_STAR.md` delivery ladder Rung 3 ("Defend + Explore — a world beyond the walls", next) and Rung 4 ("place your base"). This WO makes that world *read as inhabited and ancient* the moment the Keeper steps through a gate.

**Canon source:** `docs/regions-narrative-and-npcs.md` (the four marches, their NPCs, the tonal gradient warmth-in / dread-out), `docs/narrative-bible.md` (the Withering, the Wound, ward-stones, the Folk).

**Catalog:** `docs/polyperfect-asset-catalog.md` — every mesh cited below is verified there. On a missing mesh: `Debug.LogWarning`, never error (packs are gitignored; a clone may not have them).

---

## Vision

The walls of Elarion are no longer the edge of the world. Step through any gate and the
land tells you people *lived* here — and that something is taking it back. Roads run out
from the gates and fray into the wild. Carts sit abandoned where the road closed. A
shepherd's camp still smokes on the east road; a drowned fence-line breaks the surface of
the south mire. And everywhere, **ruins** — a fallen watchtower, a roofless croft, a
ward-circle of broken standing stones — say the first Folk built further out than the
Keeper can now hold. The world is not a backdrop. It is the evidence of the war.

This WO does **not** invent new geography or new gameplay. WO-107 already shaped the four
biomes; WO-112 already gives the player a reason to walk into them. WO-142 **dresses** that
ground so it feels lived-in: roads, scenery, wandering life, points of interest, signs of
habitation, and scattered ruins — built by a new dedicated `OuterWorldBuilder` that runs
*after* the existing terrain pass, never by hand-editing the scene or touching the frozen
VillageSceneBuilder.

> The bible's dial — **warmth in, dread out** — is the through-line. East dressing is
> intact, working, peopled. North dressing is the same vocabulary half-eaten by the rot.

---

## 0. What already exists — RECONCILE, DON'T DUPLICATE

This is the project's #1 trap. Read this before writing a line.

| System | Where | What it already does | How WO-142 relates |
|---|---|---|---|
| **`ExteriorTerrainBuilder`** | `Assets/Editor/ExteriorTerrainBuilder.cs` | 300×300 Unity Terrain centred on village, 4 directional biomes (N rising forest, E rolling farmland, S barren sink, W river valley), 5 splat layers, **5 decorative "natural" paths painted into the mud layer**, ~320 instanced KayKit trees, rock scatter (currently `boulderTarget=0` — disabled near gates), distant landmarks, dawn skybox + fog. Writes into a single `ExteriorRoot` GameObject in `Village.unity`, **idempotent**. | **THE FOUNDATION.** WO-142 adds a sibling pass that *reads the same height field* and places props/roads/NPCs/ruins on top. Do NOT fork the terrain. Do NOT re-implement biomes. Reuse its `WorldHeightAt` / `SteepnessAt` / `SeamWeight` math (lift into the new builder or make them `internal` and share). |
| **WO-107 `ZoneManager`** | `Assets/_Modules/Environment/ZoneManager.cs` (per WO-107 spec) | Names the 4 directions Goldfields/Stoneback/Mirewood/Ashwood; drives per-zone ambient light / fog / weather. | WO-142's regions ARE these four. Reuse the region identity (and WO-112's `March` enum if present). Region dressing keys off the same N/E/S/W classification the terrain builder already uses. |
| **WO-112 ward-stones** | `Assets/_Modules/Environment/Ward*.cs` (per WO-112) | The exploration reach spine; ward-stones placed along each march. | WO-142 does NOT place wards (that's WO-112's VillageSceneBuilder step). WO-142 places the *points-of-interest and ruins that sit near* wards — a ward at the edge of a ruined ward-circle, a camp by the first ward, etc. Coordinate placement so they read as one scene. |
| **`AmbientNPC`** | `Assets/_Modules/Village/NPCs/AmbientNPC.cs` | Wander-on-NavMesh OR idle, proximity speech bubble, archetype dialogue, Animator drive, white-pill tint safety-net. Configured by reflection from an editor builder; `SetHero` hands in the Keeper. | **REUSE VERBATIM** for outer-world wanderers and travellers. The only new behaviour needed is an optional path-following / road-patrol mode (see §4) — add it as a *new mode on AmbientNPC or a thin subclass*, never a parallel NPC class. |
| **`TownsfolkController`** | `Assets/_Modules/Village/NPCs/TownsfolkController.cs` | Scene coordinator: hands the Keeper transform + reduced-motion to every `AmbientNPC` under it. | Add an equivalent sub-root for the outer world (`OuterWorldFolk`) and reuse `TownsfolkController` on it (it just `GetComponentsInChildren<AmbientNPC>`). No new controller class needed. |
| **`NpcPackBuild`** | `Assets/Editor/NpcPackBuild.cs` | Reflection prefab-assembly pattern: Editor asmdef adds `DeNelle.Village.*` components by full type name, builds Animator controllers, saves prefabs into `Assets/Resources/NPCs`. | **The template** for how `OuterWorldBuilder` instantiates NPC/wildlife prefabs and wires Village components without an asmdef reference. |
| **`DungeonStubBuilder`** | `Assets/Editor/DungeonStubBuilder.cs` | The "ship a playable placeholder, validate the loop, polish later" pattern (`DungeonStubParams` → `Build()`). | **The phasing template.** WO-142 ships region dressing in stub-quality first, polishes per-region after owner validates. |
| **Polyperfect catalog** | `docs/polyperfect-asset-catalog.md` | All scenery/ruin/nature/road meshes (`SM_<Name>.fbx` under `_M/Meshes_M/<Category>_M/`). | The mesh source for all WO-142 dressing. Every mesh cited below is verified there. KayKit Forest Pack (trees/rocks) is already wired by `ExteriorTerrainBuilder`. |

**Hard reconciliation rules:**
- The terrain, biomes, decorative paths, trees and skybox are **DONE** (`ExteriorTerrainBuilder`). WO-142 never re-bakes them — it adds a *parented prop/NPC/ruin layer* over them.
- The 5 "natural paths" already painted into the mud splat are **landscape storytelling, not gameplay routes**. WO-142's roads (§3) are a *new, visible, mesh-based* layer — but they should *follow / reinforce* the existing painted paths where sensible, not contradict them.
- One NPC behaviour family (`AmbientNPC`). One region identity (WO-107). One ward system (WO-112). One height field (`ExteriorTerrainBuilder`). WO-142 adds **only** the dressing layer.

---

## 1. Region structure

The world keeps WO-107's exact geography — **four regions, one per cardinal gate**, each 80m
out to its zone centre, classified by the same N/E/S/W rule the terrain builder uses
(`worldZ > VillageHalfZ` = north, etc.). WO-142 does not add regions; it gives each one a
*lived-in identity* matching `regions-narrative-and-npcs.md`:

| Region | Dir | Gate / Spawn | Biome (already built) | Lived-in identity (WO-142 dressing) | Dread level |
|---|---|---|---|---|---|
| **Goldfields** | E | East / `spawn-1` | rolling farmland | The last open road — working farms, a shepherd's camp, a trade cart, intact fences. **Warmest, most peopled.** | low |
| **Stoneback Ridge** | W | West / `spawn-2` | river valley → ridge | A quarry workings, a hewer's hut, cairns and standing stones on the high ground. Sparse, old. | neutral |
| **Mirewood** | S | South / `spawn-2`→`spawn-0` | barren sink | A drowned fence-line and half-sunk croft breaking the murk; a ferryman's pole-jetty; a single lit lantern. Oppressive. | heavy |
| **Corrupted Ashwood** | N | North / `spawn-3` | rising dead forest | The *same* habitation vocabulary as the others — but ruined, abandoned, overgrown: a fallen watchtower, a broken ward-circle, a warden's cold camp. **The front line.** | front |

**Connections (no new scenes — all in `Village.unity`'s exterior):**
- **To the village:** roads leave each gate and run out into the region (§3). The exterior terrain already seam-blends to Y=0 at the walls, so roads start flush at the gate threshold.
- **To dungeon portals:** existing `DungeonPortal` objects (`Assets/_Modules/Village/Buildings/DungeonPortal.cs`) sit in the exterior; WO-142 dresses *approaches* to them (a worn road spur + a signpost) so a portal reads as a destination, not a floating prop. Do NOT move or re-place portals.
- **To DTT (Defend-the-Tower):** the DTT arena is its own scene — WO-142 does not touch it. If a DTT-approach landmark is wanted in the exterior, that's a distant-landmark silhouette only (the terrain builder already owns `DistantLandmarks`); note as a follow-up, out of scope here.
- **To wards (WO-112):** each region's points-of-interest are placed to *frame* that march's ward positions (e.g. Goldfields camp near ward #1; Ashwood broken ward-circle at ward #3). Read WO-112 §5 ward positions; place POIs nearby, not on top.

---

## 2. Build approach — a SEPARATE builder, `OuterWorldBuilder`

**This is the load-bearing decision. Read CLAUDE.md §3 and §9.**

- **DO NOT hand-edit `Village.unity`.** (Corruption-on-resave history.)
- **DO NOT touch `VillageSceneBuilder.cs`.** It is the frozen serialization bottleneck; only one agent touches it, and WO-112 already has a queued `BuildWardStones()` edit there. WO-142 stays out.
- **DO NOT fire any bake from UI.** Bakes are CLI work-order lines.

**Create a new editor builder: `Assets/Editor/OuterWorldBuilder.cs`** (namespace `DeNelle.Editor`,
`public static`, menu `Defenders/Week 3/Build Outer World`, executeMethod
`DeNelle.Editor.OuterWorldBuilder.BuildOuterWorld`). It mirrors `ExteriorTerrainBuilder`'s
contract exactly:

1. Opens `Assets/Scenes/Village.unity` (errors out if missing — terrain/village must exist first).
2. Finds the existing `ExteriorRoot` + `ExteriorTerrain` (errors with a clear message if absent — run `Build Exterior Terrain` first; WO-142 depends on it).
3. **Idempotent:** destroys any prior `OuterWorldRoot` GameObject, then rebuilds from scratch. Re-runnable safely.
4. Creates one `OuterWorldRoot` containing sub-roots: `Roads`, `RegionDressing/<Region>`, `OuterWorldFolk` (with a `TownsfolkController`), `Wildlife`, `Ruins`, `PointsOfInterest`.
5. Reads ground height by **sampling the live terrain** — `Terrain.activeTerrain.SampleHeight(worldPos) + terrain.transform.position.y` — so every prop beds correctly onto the existing heightmap. (Do NOT re-derive the biome math unless sharing `ExteriorTerrainBuilder`'s helpers is cleaner; sampling the baked terrain is simpler and authoritative.)
6. Respects the same exclusion rules: nothing inside the village footprint / seam band, and **a per-gate clear corridor** (the owner's "rocks in front of door" rule — keep gate thresholds and the first ~8m of each road clear of clutter).
7. Saves the scene + assets, logs a one-line summary (counts of roads/props/NPCs/ruins per region + any missing-mesh fallbacks), exactly like `ExteriorTerrainBuilder` does.

**Ordering:** `OuterWorldBuilder` runs **after** `ExteriorTerrainBuilder` and **after** WO-112's
ward placement (so it can read ward positions). The architect-lane bake sequence becomes:
`Build Village` → `Build Exterior Terrain` → (WO-112 ward placement) → **`Build Outer World`**.
CLI owns and sequences this bake.

**Asmdef discipline:** `OuterWorldBuilder` is editor-only (`DeNelle.Editor`). It instantiates
prefabs and adds `DeNelle.Village.AmbientNPC` / `TownsfolkController` **by reflection**
(the `AddByName` / `ResolveType` pattern from `NpcPackBuild.cs`) — it does NOT add a compile-time
ref to `DeNelle.Village`. Pure prop meshes (ruins/scenery) need no Village components at all.

---

## 3. Roads + scenery (Phase B)

### Roads out of the gates
Mesh-based, visible roads — distinct from the existing painted mud paths (which stay as
soft ground-blend underneath). A road is a chain of paving/ground meshes laid along a
polyline from each gate out toward that region's first ward / POI.

- **Paving mesh:** `Stone_Brick` (Medieval_M) near the gate (dressed cobble), transitioning to `Ground_Cracked_Dirt` and then fading out into the wild (the road *frays* — it doesn't reach a clean end; that sells "the road is closing").
- **Road furniture:** `Bridge_Medieval_Stone` where a road crosses the west river / a south mire channel; `Fence_Picket` (Farm_M) and `Fence_Stone` along the Goldfields road; a `Signpost`-style marker at each road's start (use `Stakes` + a `Flag_Medieval` if no signpost mesh, or a `Timber` upright — verify in catalog, fallback gracefully).
- **Per-region road character:** Goldfields road is wide, maintained, fenced. Stoneback road is a rough switchback up the ridge. Mirewood "road" is a series of dry hummocks + a pole-jetty (no real road — the water keeps it). Ashwood road is the same as Goldfields' vocabulary but cracked, with `Tree_Dead_Log_A/B` fallen across it.
- Roads should **follow the existing painted mud paths** where alignment is natural (read `PaintNaturalPaths` control points in `ExteriorTerrainBuilder`), reinforcing rather than fighting the painted storytelling.

### Per-region scenery dressing
Scatter biome-appropriate dressing (parented under `RegionDressing/<Region>`), height-sampled
onto the terrain, away from the gate corridor:

- **Goldfields (E):** `Haystack`, `Hay_Pile`, `Scarecrow`, `Farm_Flower_Bed` (Farm_M), `Fence_Picket`, plus `Tree_Oak` / `Tree_Birch` clusters (the terrain builder already plants orchard trees here — *complement*, don't double-plant).
- **Stoneback (W):** `Rock_Large`, `Rock_Pillar` (standing stones), `Stone_Big`, `Rocks_Small` scatter, `Timber` (quarry leavings). Re-enable a *modest* rock scatter here since the gate-corridor rule now protects the doorways (the terrain builder's scatter is globally off; WO-142 adds region-scoped rocks instead).
- **Mirewood (S):** `Tree_Dead` (tall, sparse — already planted), `Tree_Dead_Log_A/B` crossing water, `Rock_Large` (read as mossy), a few low translucent water discs in the sink lows (the terrain builder's ponds are disabled globally; a *handful* of region-scoped mire pools is fine here — keep them away from the village).
- **Ashwood (N):** `Tree_Dead`, `Tree_Dead_Log_A/B`, `Rock_Sharp`, plus the corruption vocabulary (a `Rock_Pillar` half-toppled, faint emissive violet on one or two props echoing the terrain builder's `DistantWoundCrack` tint).

---

## 4. Wandering content (Phase C)

**Reuse `AmbientNPC` + `TownsfolkController`. Do not write a new NPC class.**

### Wanderers (region locals)
A few idle/wandering `AmbientNPC`s per region, placed near that region's POI, configured by
reflection exactly as `NpcPackBuild` does. They use the existing proximity-speech bubble.
Match them to the canon NPCs from `regions-narrative-and-npcs.md` §6 (use existing People_M
meshes as stand-ins; archetype tint covers a missing mesh):

| Region | Wanderer (canon NPC) | Stand-in mesh (People_M) | Mode |
|---|---|---|---|
| Goldfields | Maeren the Roadwarden | `Man_Knight_Soldier` | idle by the road / camp |
| Goldfields | Brightwheat (field-elder) | `Man_Farm` | wander the field edge |
| Stoneback | Garrick the Last Hewer | `Man_Sir` (gruff) | idle at the quarry |
| Mirewood | Vessa the Lantern-Widow | `Woman_Farm` | idle by the lantern |
| Ashwood | Old Bram (last warden) | `Man_Monk_Old` | idle at the cold camp, halting "forgetting" sway |

> NavMesh note: `AmbientNPC` wanders only if a NavMesh exists where it stands; otherwise it
> idles gracefully (no error). The exterior terrain is **not** currently NavMesh-baked. For
> Phase C, place outer-world folk as **idlers** (wander=false) so they work with no NavMesh.
> Bake an exterior NavMesh as a *later* phase if road-patrol wandering is wanted (note below).

### Travellers on roads (new optional mode)
A "the road is still used" beat: one or two figures walking a road. If an exterior NavMesh is
not baked, implement this as a **simple waypoint-walker mode** — add an optional
`SetPatrolPath(Vector3[])` to `AmbientNPC` (or a thin `RoadTraveller : AmbientNPC` subclass in
`DeNelle.Village`) that lerps along a polyline and ping-pongs, independent of NavMesh. This is
the *only* new behaviour code in this WO and it must be additive (default off; existing village
NPCs unaffected). Cross-module rules unchanged (no HUD ref; `?.` on any service call).

### Wildlife (ambient)
Ambient animals under `Wildlife`, drifting/idling per biome (reuse the `AmbientNPC` idle-sway,
or a tiny `WildlifeWanderer` that does the same lerp-walker as travellers — pick one, don't
build two). Meshes from Animals_M:
- Goldfields: `Deer`, `Hen`, `Cow` (near the farm).
- Stoneback: `Deer` (sparse), `Bear` (distant, ambient — not an enemy here).
- Mirewood: none living, or a single `Deer` carcass-stillness for dread (designer call).
- Ashwood: none — the silence *is* the content. Maybe one `Wolf` standing motionless at the tree-line (echo of the Ice Wolf lore; ambient, not hostile).

---

## 5. Points of interest + lived-in devices (Phase C, with wanderers)

Light POIs under `PointsOfInterest` — clusters of props that read as "someone is/was here":

- **Goldfields — the Shepherd's Camp:** `Fire` (Survival_M, lit), `Tent`-equivalent or `Carriage` (Medieval_M) as a parked trade cart, `Crate_Box`, `Barrel`-equivalent, a `Bench_Wood`. Maeren idles here. The warm anchor of the whole outer world.
- **Stoneback — the Quarry Workings:** `Timber` stacks, `Rock_Large` cut blocks, a `Wheelbarrow` (Tools_M), `Pickaxe`/`Hammer` (Tools_M) leaned on a rock. Garrick idles here.
- **Mirewood — the Lantern Hall (half-sunk):** a single lit lantern (`Torche` / `Candle_Big` emissive), a `Carriage` half-submerged, a pole-jetty of `Timber`. Vessa idles by the light. One warm point in the murk.
- **Ashwood — the Warden's Cold Camp:** a `Fire` that is *unlit* (cold ash), an abandoned `Crate_Box`, `Tree_Dead_Log` benches. Old Bram idles here, holding the line.

**Lived-in devices** (sprinkled along roads, not just at POIs): a `Wheelbarrow` tipped on the
road; a `Fence_Picket` run that goes nowhere; a `Scarecrow` at the field edge; a `Well`
(Medieval_M) at an old crossroads; cart-ruts reinforced by the existing painted mud paths.

---

## 6. RUINS (Phase D) — the ancient, inhabited-once layer

The owner's explicit ask: *"some ruins strewn about."* Ruins are the strongest single device
for "lived-in and ancient" — they say the Folk built **further out than the Keeper can now
hold**, and the rot took it. Place a handful per region under `Ruins`, scaled and tilted so
broken meshes read as *fallen*, not placed.

### Candidate ruin meshes (verified in `docs/polyperfect-asset-catalog.md`)
There is no dedicated "ruins" category in the pack, so ruins are **composed** from these
broken / battle-worn / rubble meshes — which is exactly how a low-poly ruin reads:

| Ruin element | Mesh(es) | Source | How to use |
|---|---|---|---|
| Broken / fallen wall | `Wall_Stone_3x3_C` (battle-worn variant), `Wall_Stone_End_3x3m_C` | `_M/Meshes_M/` | Short runs, some tilted/sunk, gaps between segments |
| Rubble piles | `Rubble_Stone` | Fantasy_M (Dungeon kit) | Scatter at the base of broken walls |
| Fallen tower | `Tower_Medieval_Wood` / `Fence_Stone_Tower` tilted + partly sunk into terrain | `_M/Meshes_M/` | One per region max; tilt 20–40°, sink the base |
| Roofless croft | `House_Medieval_Small` partly sunk + `Wall_Stone_3x3_C` fragments + `Rubble_Stone` | Medieval_M | Suggest a foundation, not a building |
| Standing-stone / ward-circle | `Rock_Pillar` ×5–7 in a ring, some toppled | Nature | The "broken ward-circle" — frames a WO-112 ward beautifully |
| Overgrown foundation | `Floor_Stone_3x3m_A` fragments + `Rocks_Small` + a `Tree_Dead`/`Tree_Oak` growing through | Mixed | The land reclaiming it |
| Fallen logs across ruin | `Tree_Dead_Log_A/B` | KayKit (already loaded by terrain builder) | Drape over rubble |
| Old grave / cairn | `Rock_Large` + `Rock_Pillar` stack | Nature | Stoneback cairns; a roadside grave |

### Per-region ruin character (warmth-in / dread-out)
- **Goldfields (E):** the *least* ruined — one old roadside shrine-stone, a tumbled fence, a single broken cart. Hints of age, not collapse.
- **Stoneback (W):** ancient cairns + a half-toppled standing-stone circle on the high ground. Old, indifferent, pre-Elarion.
- **Mirewood (S):** a drowned fence-line and a roofless croft breaking the water surface — the "sunken first valley" of the bible, glimpsed. Sink meshes deep; only the tops show.
- **Ashwood (N):** the most ruined — a fallen watchtower, a broken ward-circle (toppled `Rock_Pillar`s with one faintly violet-emissive), `Rubble_Stone` everywhere. This is the front line; the ruins are *recent*, the rot still spreading.

### Ruins as hooks (design notes, NOT in this WO's code scope)
Tag ruin roots with a stable name (`Ruin_<Region>_<NN>`) so a future WO can attach:
- **Exploration/lore:** a readable marker (reuse the `AmbientNPC` bubble or a `TownsfolkBubble` on an empty) that surfaces a bible-tone line on approach.
- **Harvest:** a ruin could later host a `CollectionPoint` (WO-111) — "salvage from the drowned hall," "corrupt-crystal in the broken ward-circle."
- **Ward framing:** the broken ward-circle ruin in each region should sit *at or beside* that march's node-ward (WO-112), so relighting the ward visually "completes" the broken circle.
Place ruins now; wire hooks later. Do NOT build the hooks in this WO.

---

## 7. Phasing — shippable, never one unbakeable blob

Each phase is its own `OuterWorldBuilder` capability + its own bake, validated by the owner
before the next. The builder stays idempotent throughout (rebuild = clean `OuterWorldRoot`).
Follows the `DungeonStubBuilder` "ship playable, validate, then polish" pattern.

| Phase | Adds | Bake gate | Acceptance signal |
|---|---|---|---|
| **A — Skeleton + regions** | `OuterWorldBuilder` scaffold, `OuterWorldRoot` + sub-roots, terrain height-sampling, gate-corridor exclusion, region classification. No content yet — just the framework + a single test prop per region to prove placement. | Bake 1 | Four test props sit correctly on the terrain, clear of gates; re-run is clean. |
| **B — Roads + scenery** | Gate roads (§3), per-region scenery dressing, region-scoped rock/mire scatter. | Bake 2 | Roads lead out of each gate; each region reads as its biome with dressing. |
| **C — Wandering + POIs** | Outer-world folk (idlers, §4), wildlife, the per-region POIs + lived-in devices (§5); optional `RoadTraveller` waypoint-walker if quick. | Bake 3 | The world feels *peopled* — a camp, a quarry, a lantern, animals; NPCs speak on approach. |
| **D — Ruins** | The ruins layer (§6), per-region character, named ruin roots for future hooks. | Bake 4 | Ruins read as ancient/fallen; Ashwood reads as the rotting front line. |

Phases A–D can each land independently; the project ships value at every gate. If time is
short, A–B alone already transforms the empty exterior into a roaded, dressed world.

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/Editor/OuterWorldBuilder.cs` | **Create** — the whole WO. Editor-only, `DeNelle.Editor`, idempotent, reflection-wires Village components (NpcPackBuild pattern). |
| `Assets/_Modules/Village/NPCs/AmbientNPC.cs` | **Edit (Phase C, optional, additive only)** — add an optional `SetPatrolPath(Vector3[])` waypoint-walker mode, default off. Existing village behaviour unchanged. |
| `Assets/_Modules/Village/NPCs/RoadTraveller.cs` | **Create (alternative to the AmbientNPC edit)** — thin `: AmbientNPC` subclass for road patrols, if cleaner than a mode flag. Pick ONE of these two; do not do both. |
| `Assets/Scenes/Village.unity` | **Rebuilt via `OuterWorldBuilder` only** — **do NOT hand-edit.** |
| `Assets/Editor/ExteriorTerrainBuilder.cs` | **Edit (ONLY IF sharing helpers)** — optionally make `WorldHeightAt` / `SteepnessAt` / `SeamWeight` `internal` so `OuterWorldBuilder` can reuse them. Prefer terrain `SampleHeight` instead and leave this file untouched. |

**What NOT to create:** a new terrain, a new biome system, a new NPC behaviour family, a new
region/zone enum, a new ward system, a second scene, a `VillageSceneBuilder` edit.

---

## What NOT to touch

- **`VillageSceneBuilder.cs`** — frozen serialization bottleneck (CLAUDE.md §9). WO-112 already
  has a queued edit there for wards. WO-142 stays entirely out.
- **`Village.unity`** — never hand-edit (CLAUDE.md §3). All WO-142 content appears only via the
  `OuterWorldBuilder` rebake.
- **The existing terrain / biomes / painted paths / trees / skybox** in `ExteriorTerrainBuilder` —
  read them, place on top of them, do not re-bake or fork them.
- **`ZoneManager` / WO-107 region identity** — reuse, don't redefine.
- **WO-112 ward placement / `WardTetherService`** — frame wards with POIs/ruins; never place or
  move wards here.
- **`DungeonPortal` objects** — dress their approaches, don't move them.
- **The DTT scene, ATB, WalletService, monetization, clan, backend** — untouched.
- **No bake fired from UI** — every bake is a CLI architect-lane work-order line, sequenced
  after terrain + wards.
- **No `System.Reflection` in runtime bridge scripts** — reflection is editor-only in
  `OuterWorldBuilder` (NpcPackBuild pattern), never in shipped MonoBehaviours.

---

## Acceptance Criteria

- [ ] `OuterWorldBuilder.cs` compiles; menu `Defenders/Week 3/Build Outer World` + executeMethod exist.
- [ ] Builder opens `Village.unity`, finds the existing `ExteriorTerrain`, errors clearly if terrain is absent.
- [ ] Idempotent: re-running destroys + rebuilds `OuterWorldRoot` cleanly (no duplication, no drift).
- [ ] All placed props bed onto the existing terrain via `SampleHeight` (no floating / buried props).
- [ ] Gate corridors + village footprint are kept clear (no "rocks/props in front of the door").
- [ ] **Phase B:** a visible mesh road leads out of each gate; each region reads as its WO-107 biome with appropriate scenery.
- [ ] **Phase C:** each region has a POI (camp / quarry / lantern hall / cold camp), ambient wildlife, and at least one `AmbientNPC` that speaks on the Keeper's approach (reusing the existing bubble — no new NPC class beyond the optional additive patrol mode).
- [ ] **Phase D:** each region has scattered ruins composed from verified broken/rubble meshes; Ashwood reads as the rotting front line; ruin roots are named `Ruin_<Region>_<NN>` for future hooks.
- [ ] Warmth-in / dread-out gradient is visible: Goldfields intact & peopled → Ashwood ruined & silent.
- [ ] No purple/magenta materials (polyperfect atlas + URP); missing meshes `LogWarning` + fallback, never error.
- [ ] No edit to `VillageSceneBuilder.cs`; no hand-edit to `Village.unity`.
- [ ] If `AmbientNPC` was edited, the change is additive (patrol mode default off) and existing village NPCs are unaffected.
- [ ] Builder logs a one-line summary (per-region road/prop/NPC/ruin counts + any fallbacks used), like `ExteriorTerrainBuilder`.
- [ ] Rebake required — queue as CLI architect-lane lines, sequenced *after* terrain + WO-112 wards.

---

## Done checklist (CLAUDE.md §10)

- [ ] Brace balance check passed on every `.cs` file touched (`OuterWorldBuilder.cs`, and `AmbientNPC.cs`/`RoadTraveller.cs` if edited).
- [ ] No `.unity` scene file hand-edited (all content via `OuterWorldBuilder` rebake).
- [ ] No new `System.Reflection` usage in any runtime/bridge script (reflection is editor-only, NpcPackBuild pattern).
- [ ] `using DeNelle.Core.Combat;` present in any file implementing `IDamageableStructure` (N/A here unless a ruin is later made attackable — out of scope).
- [ ] Null-conditional operators (`?.`) used on all cross-module service calls (none new expected; verify if patrol mode touches `CoreServices`).
- [ ] Acceptance criteria reviewed line by line.
- [ ] Bake queued for CLI (UI does not fire batchmode); sequence: Build Village → Build Exterior Terrain → WO-112 wards → **Build Outer World**.
```
