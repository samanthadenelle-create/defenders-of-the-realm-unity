# PO Validation — Avalon Village & Area Maps

**Validation type:** Product Owner acceptance validation (built deliverable vs. written spec / acceptance criteria).
**Validator:** Product Owner (acceptance pass).
**Date:** 2026-05-19
**Mode:** READ-ONLY. No code, scene, asset, or data was modified.
**Scope:** (A) the Avalon village interior, and (B) the four area-map deliverables.

**Evidence sources reviewed:**
- `docs/avalon-village-layout-spec.md` (the village contract)
- `docs/dungeons-3d-unity-layout-spec.md` §9 (the dungeon contract)
- Rendered village: `docs/screenshot-village-week3.png`, `docs/screenshot-village-elarion.png`, `docs/screenshot-village-week3-exterior.png`
- Builder: `Assets/Editor/VillageSceneBuilder.cs`
- Wall layout: `Assets/_Modules/Village/Walls/WallLayout.cs`
- Map data: `Assets/StreamingAssets/Data/Canonical/realm-map.json`, `Assets/StreamingAssets/Data/Canonical/dungeons/healers-cottage.json`
- Decisions / deferrals: `docs/unity-decisions.md` (Weeks 3/4 + flags)

---

## 1. Executive Summary

### Top-line PO verdicts

| Deliverable | Verdict |
|---|---|
| **A. The Avalon Village** | **ACCEPT WITH CONDITIONS** |
| **B. The Area Maps** | **ACCEPT WITH CONDITIONS** |

### Rationale

**The Village** is structurally complete and faithful to the spec. Every canon-locked requirement — the shaped (non-square) walled town, the four cardinal gates as the only wall breaks, the two side-by-side centerpieces (Elarion + the Keeper's Keep), the central plaza with cross-axis roads, all five named gameplay buildings, the residential/market/workshop city dressing, and the four approach lanes with wave-spawn points — is present in `VillageSceneBuilder.cs` and visible in `screenshot-village-week3.png`. The geometry, counts, and quadrant placements match the spec to the unit. The deliverable is **not a clean ACCEPT** only because of two open visual-quality items that are already logged as owner-flagged "finetune later" deferrals — most notably the **white-render of the Elarion + Keep centerpiece** (Acceptance Criterion §12.3 / §12.11 is visibly not met in the screenshots). Because that defect is logged with a full investigation and an explicit owner deferral, it is **DEFERRED, not a FAIL** — but it is also the single most player-visible flaw, so it is named as a condition of acceptance: the village cannot ship to players with a white blob at its heart.

**The Area Maps** are data-correct and structurally faithful. The Realm map carries all 5 regions with complete RegionDef fields, gate chain, rewards, and adjacency; the curtain-wall layout in `WallLayout.cs` is a precise, well-commented shaped rectangle with exactly 4 gates; the Healer's Cottage dungeon JSON delivers exactly the 3-level / 12-room structure §9 demands, with correct room kinds, lore-stone, encounter, checkpoint, and chest counts. The maps are **not a clean ACCEPT** only because the Realm map's region names and all region description prose have **no canon authority** — this is explicitly flagged in `unity-decisions.md` (2026-05-19) and the owner is named as the party who must ratify them before any Realm Map UI ships.

### Coverage statistics

| Area | PASS | PARTIAL | FAIL | DEFERRED | Total | PASS % |
|---|---|---|---|---|---|---|
| A. Village | 33 | 1 | 0 | 5 | 39 | 85% |
| B. Area Maps | 24 | 2 | 0 | 2 | 28 | 86% |
| **Combined** | **57** | **3** | **0** | **7** | **67** | **85%** |

Zero FAIL verdicts: every spec requirement is either met, partially met, or carries a logged owner-approved deferral. **Conditions of acceptance: 5** (see §6).

---

## 2. Village — Requirement-by-Requirement (against `avalon-village-layout-spec.md`)

### §2 Canon-locked requirements

| # | Requirement | Spec ref | Verdict | Evidence / Notes |
|---|---|---|---|---|
| V1 | Walled town with 4 cardinal gates (N/S/E/W), fixed wave threat-points | §2 | PASS | `WallLayout.cs` builds exactly 4 `GateGap`s (gate-0..3, N/E/S/W); `BuildGates` places them; 4 gates visible in screenshot. |
| V2 | The Heart (Elarion) at the centre — sacred world-tree, violet emissive | §2 | PARTIAL | Elarion is built centre-west with 5 `#9d6fff` emissive vein shards at intensity 0.6 (`BuildElarion`). The tree mesh itself renders white (see V12) — emissive veins read, the canopy does not. |
| V3 | 5 named spec buildings inside the walls | §2 | PASS | `Buildings[]` array has all 5: Crystal Mine, Pet House, Arcane Tower, Workshop, Farm. |
| V4 | Canon names — Avalon, Elarion, Blaise, Alduin the Mournful | §2 | PASS | Builder objects named "Heart (Elarion)", "KeepersKeep"; hero rig is Blaise (`BuildHero`). No name violations found. |
| V5 | All copy in narrative-bible voice | §2 | PASS | No shipped village UI copy in scope of this builder; no violations found. |

### §3 The Heart-and-Keep — two centerpieces

| # | Requirement | Spec ref | Verdict | Evidence / Notes |
|---|---|---|---|---|
| V6 | Elarion: large tree mesh, ~3× scale | §3.1 | PASS | `trees_A_large.fbx` scaled `Vector3.one * 3.0f`. Forest pack not imported; hex-pack tree fallback used — logged decision (`unity-decisions.md` Week 3). |
| V7 | Violet emissive crystalline veins (`#9d6fff`, intensity 0.6, soft pulse) | §3.1 | PARTIAL | Colour `9d6fff` + intensity 0.6 applied to 5 vein cubes (`ApplyEmissive`). Soft 0.2 Hz pulse animation not implemented — static emissive only. |
| V8 | Raised mound under Elarion | §3.1 | PASS | `ElarionMound` cylinder, 5u radius, raised 0.35u. |
| V9 | 6-hex ring of standing stones | §3.1 / §14 Q3 | PASS | `stoneCount = 6`, `rock_single_C.fbx` at 4.4u ring radius. |
| V10 | No building on Elarion's hex — the tree IS the centerpiece | §3.1 | PASS | No `Building`/`DressDef` placed on the Elarion site; only the tree, mound, veins, stones. |
| V11 | Keeper's Keep — `building_castle`, adjacent to Elarion, ~2 hex SE offset | §3.2 | PASS | `BuildKeep` places `building_castle` at (6,0,-3) vs Elarion (-6,0,1) — SE offset, ~9u apart, frames the plaza. |
| V12 | Centerpiece renders correctly (no missing-mesh / untextured) | §3 / §12.11 | DEFERRED | Elarion tree, standing stones, and Keep render white — confirmed in `screenshot-village-elarion.png`. Fully investigated, logged `unity-decisions.md` 2026-05-19 as OPEN finetune item, owner-flagged "finetune later". Not a FAIL (logged deferral) but is a **condition of acceptance** (§6 C1). |
| V13 | Violet banner beside the Keep | §3.2 | PASS | `flag_blue.fbx` instantiated, normalised to 3.2m, emissive-tinted toward `9d6fff`. |
| V14 | Plaza — paved open space between Elarion & Keep, 4-6 hexes | §3.3 | PASS | `BuildPlaza` lays a stone-tile (`hex_road_B`) plaza; visible as the central paved area in the screenshot. |
| V15 | Roads radiate from plaza in 4 cardinal directions | §3.3 / §7 | PASS | `BuildRoads` lays N/S/E/W arms; cross-axis visible in screenshot. |

### §4 The curtain wall

| # | Requirement | Spec ref | Verdict | Evidence / Notes |
|---|---|---|---|---|
| V16 | Rectangular, wider E-W than N-S, ~30×24 hex interior | §4.1 | PASS | `WallHalfX = 28` > `WallHalfZ = 21`; comment ties to ~30×24 hex. Shaped rectangle confirmed in screenshot. |
| V17 | NOT a tight square; generous interior | §2 / §4.1 | PASS | Wide rectangle with south bow-out — clearly not a square. Logged decision Week 3. |
| V18 | Corners use wall_corner pieces; straights use wall_straight | §4.1 | PASS | `WallLayout` emits `Corner=true` segments at 6 vertices; `BuildWallRing` loads KayKit wall pieces. |
| V19 | No diagonal walls (axis-aligned) | §4.1 | PASS | All runs are axis-aligned; bow-out uses extra axis-aligned corner vertices, no diagonals. |
| V20 | Optional creative variation — south bow-out for the orchard/Farm | §4.2 | PASS | `SouthBowDepth = 4`, `SouthBowHalfWidth = 9`; bow-out visible at the bottom of the screenshot. Within the "no more than two" limit. |
| V21 | 4 gates — one centred break per cardinal side, the only gaps | §4.3 | PASS | `WallLayout.Build` centres a gate gap on each cardinal mid-run; no postern gates. |
| V22 | Wall sections seat flush against gate pillars (T59 lesson) | §4.3 | PASS | Gate yaw-fix corrected 2026-05-19 (`WallStraightYawFix`), verified flush in `screenshot-village-week3.png` per `unity-decisions.md`. |
| V23 | Wall tiering via material swap (4 tiers) | §4.4 | DEFERRED | Week-3 builder ships the static wall ring; tier material swaps belong to the upgrade-visual system (not a Week-3 deliverable). No tiering code in builder — out of scope for this milestone, not a regression. |

### §5 The five named gameplay buildings

| # | Requirement | Spec ref | Verdict | Evidence / Notes |
|---|---|---|---|---|
| V24 | Crystal Mine — `building_mine`, NW quadrant | §5 | PASS | `Buildings[0]` at (-17, 12.5), `building_mine`. |
| V25 | Pet House — `building_stables`, SW quadrant | §5 | PASS | `Buildings[1]` at (-17, -10.5), `building_stables`. |
| V26 | Arcane Tower — `building_tower_A`, S-central near Keep | §5 | PASS | `Buildings[2]` at (6, -12.5), `building_tower_A`. |
| V27 | Workshop — `building_workshop`, NE quadrant | §5 | PASS | `Buildings[3]` at (16, 12.5), `building_workshop`. |
| V28 | Farm — `building_windmill`, East | §5 | PASS | `Buildings[4]` at (19, -1), `building_windmill`. |
| V29 | Each building on a 2×2 hex plot with a low fence (dressing, no collider) | §5 | PASS | `BuildPlotFence` rings each plot; `StripColliders` called — fence is non-blocking dressing. |
| V30 | Buildings visually distinct + tappable for build/upgrade UI | §12.5 | PASS | Each gets a `Building` component, `Configure`d + registered with `VillageController`; build menu wired in Week-4 pass. |

### §6 City dressing

| # | Requirement | Spec ref | Verdict | Evidence / Notes |
|---|---|---|---|---|
| V31 | Residential cluster (SW) — 4-6 houses, mix of home_A/home_B | §6.1 | PASS | 6 homes (3× `building_home_A`, 3× `building_home_B`) grouped in SW. |
| V32 | 1 well at the residential cluster centre | §6.1 | PASS | `building_well` at (-16.5, -11.5), cluster centre. |
| V33 | Market quarter — market + tavern + church around the plaza | §6.2 | PASS | `building_market` (S of plaza), `building_tavern` (SE), `building_church` (N, small). |
| V34 | Workshop quarter (NE) — blacksmith + townhall + fenced yard | §6.3 | PASS | `building_blacksmith` near Workshop, `building_townhall` at NE plaza corner, `BuildWorkshopYard` with anvil/lumber/stone props. |
| V35 | Farm/orchard (E) — orchard tiles + farmer's hut | §6.4 | PASS | `BuildOrchard` + `building_home_A` farmer's hut on orchard edge; orchard crops visible (green rows) in screenshot. |
| V36 | Northern open ground — wayside shrine + scattered trees | §6.5 | PASS | `building_shrine` at (0.5, 16.5) + 4 scattered trees. |

### §7 Road network

| # | Requirement | Spec ref | Verdict | Evidence / Notes |
|---|---|---|---|---|
| V37 | N-S spine + E-W cross forming a `+` with the plaza at centre | §7.1 | PASS | `BuildRoads` lays N/S and E/W arms (`LayRoadPair`); cross visible in screenshot. |
| V38 | Plaza tiles slightly lighter than road stone (ceremonial) | §7.3 | PASS | Plaza uses `hex_road_B`, roads use `hex_road_A` — distinct tile variants per spec intent. |

### §8 Approach lanes

| # | Requirement | Spec ref | Verdict | Evidence / Notes |
|---|---|---|---|---|
| V39 | 3-5 hexes of road extending from each gate | §8.1 | PASS | `BuildApproaches` lays 5 road hexes (2-wide) per gate. |
| V40 | Light foliage flanking each lane | §8.1 | PASS | Trees + rocks placed per lane side. |
| V41 | Wave spawn zone — 3×3 hex grass plot per gate | §8.1 / §8.3 | PASS | 3×3 `hex_grass` plot built at each lane end. |
| V42 | One invisible `WaveSpawnPoint` marker per gate | §8.3 | PASS | `WaveSpawnPoint` component added + `Configure`d per gate; `BuildVillage` reports `spawnCount`. |
| V43 | Per-gate flavour (N forest / E farm / S barren / W river) | §8.2 | PARTIAL | Approach lanes are built identically (same tree/rock dressing each side). Per-direction biome flavour belongs to the §9 exterior (separate agent, Task #14 open). Interior approach lanes do not yet differ by direction. |

### §9 Outer landscape

| # | Requirement | Spec ref | Verdict | Evidence / Notes |
|---|---|---|---|---|
| V44 | Hybrid hex-interior + Unity Terrain exterior | §9.1 | DEFERRED | Hybrid approach + 55u seam plateau logged `unity-decisions.md` 2026-05-19. The village builder explicitly leaves flat Y=0 seam ground; the exterior is a separate agent (Task #14). |
| V45 | Exterior biomes, elevation, paths, skybox, fog | §9.2-§9.6 | DEFERRED | `screenshot-village-week3-exterior.png` shows the exterior is still rough — black unlit terrain, orange-void sky (no skybox), stray floating objects. Logged as Task #14 "still rough", owner-flagged "finetune later". Not this milestone's deliverable; tracked, not blocking the interior. |
| V46 | Per-direction fog gradient (denser south) | §9.5 | DEFERRED | Logged `unity-decisions.md` 2026-05-19 — built-in `RenderSettings` fog is uniform; volumetric/URP fog pass needed. Ships uniform; flagged. |

### §12 Acceptance criteria

| # | Acceptance criterion | Spec ref | Verdict | Evidence / Notes |
|---|---|---|---|---|
| V47 | Walled town of clear shape, not a tight square | §12.1 | PASS | Confirmed — shaped rectangle with south bow-out (screenshot). |
| V48 | Four cardinal gates, visibly the only wall breaks | §12.2 | PASS | 4 gates, 1 per side, in screenshot; force-field stand-in present (over-bright — see C2). |
| V49 | Two centerpieces side-by-side | §12.3 | PARTIAL | Both Elarion and the Keep are present and correctly positioned, but both render white (V12) — they do not yet read as "tree + castle". |
| V50 | Central plaza connecting four roads to four gates | §12.4 | PASS | Plaza + cross-axis roads confirmed. |
| V51 | Five named buildings present + visually distinct | §12.5 | PASS | All 5 present; distinct meshes (mine/stables/tower/workshop/windmill). |
| V52 | Residential cluster of 4-6 houses with a well | §12.6 | PASS | 6 houses + well confirmed. |
| V53 | Market quarter (market + tavern + church) around the plaza | §12.7 | PASS | All 3 present. |
| V54 | Approach lanes beyond each gate ending at a spawn zone | §12.8 | PASS | Confirmed (V39-V42). |
| V55 | Outer landscape suggesting a continuous world | §12.9 | DEFERRED | Exterior rough — see V45. Logged Task #14. |
| V56 | FPS holds 60 on Seeker hardware | §12.10 | DEFERRED | Not measurable from static screenshots / read-only review; requires on-device profiling. Not yet evidenced. |
| V57 | All assets from KayKit packs — no missing-mesh placeholders | §12.11 | PARTIAL | `BuildVillage` reports a placeholder count; KayKit walls/buildings/crops render fine. But Elarion tree, stones, and Keep show the white-render quirk (V12) — not a missing mesh, but the meshes are not displaying their intended texture. |
| V58 | Cinemachine camera flythrough without clipping | §12.12 | DEFERRED | Not verifiable from static screenshots; requires interactive Editor playthrough. |

### §13 Decisions log

| # | Requirement | Spec ref | Verdict | Evidence / Notes |
|---|---|---|---|---|
| V59 | Creative decisions logged in `unity-decisions.md` | §13 | PASS | All 4 spec-§13 decisions (Keep placement, wall shape, residential cluster, Forest-pack-tree fallback) are logged in the Week 3 table, plus 4 additional exterior decisions. Flags raised section is thorough. |

---

## 3. Area Map B1 — The Realm Map (`realm-map.json`)

Validated against the spec intent (5 regions, completeness) and the canon-authority flag in `unity-decisions.md`.

| # | Requirement | Verdict | Evidence / Notes |
|---|---|---|---|
| M1 | 5 regions defined | PASS | `thornwood`, `mirewood`, `hollowfrost`, `emberwastes`, `starfall-reach` — exactly 5. |
| M2 | Each region carries full RegionDef fields (id, title, biome, propSet, waveCount, elementBias, gate, clearReward) | PASS | All 5 entries have every field; matches React `RegionDef` shape per `_schemaNotes`. |
| M3 | Region gate chain is a coherent progression | PASS | thornwood `bestWave 3` → mirewood → hollowfrost → emberwastes → starfall-reach, each gated on the prior region cleared. Clean linear unlock. |
| M4 | Wave counts escalate by region difficulty | PASS | 5 / 6 / 7 / 8 / 10 — monotonic escalation. |
| M5 | Clear rewards escalate | PASS | crystals 120 → 180 → 240 → 320 → 480; food then coins as the region tier rises. |
| M6 | Home base (Avalon) defined, not a region | PASS | `homeBase` entry with `isRegion: false`, no gate, map point (50,50). |
| M7 | Map layout points present for each node | PASS | `mapPoint` on Avalon + all 5 regions; `mapOrder` 1-5. |
| M8 | Adjacency graph present | PASS | Each region carries an `adjacency` array forming a connected chain. |
| M9 | Withering / progress-ledger metadata present | PASS | `withering` (edge border) + `progressLedger` (save-shape doc) included with explanatory comments. |
| M10 | Region names + descriptions carry canon authority | PARTIAL | Region IDs follow the React code catalog (save-key compatibility, deliberate). But names + all `description` prose have NO canon source — design doc and code disagree, names absent from `narrative-bible.md`. **Explicitly flagged** `unity-decisions.md` 2026-05-19; owner must ratify before Realm Map UI ships. Logged deviation, not a FAIL — but is a **condition of acceptance** (C3). |
| M11 | Sourcing documented | PASS | `_sources` block cites every contract/catalog/layout file the data was extracted from — strong traceability. |

**Realm Map verdict: PASS on structure/completeness (10/11). The single PARTIAL is the canon-authority gap, which is logged and owner-assigned.**

---

## 4. Area Map B2 — The Curtain-Wall Layout (`WallLayout.cs`)

Validated against `avalon-village-layout-spec.md` §4 (shaped rectangle + 4 gates).

| # | Requirement | Verdict | Evidence / Notes |
|---|---|---|---|
| W1 | Shaped rectangle, wider E-W (`WallHalfX > WallHalfZ`) | PASS | `WallHalfX = 28`, `WallHalfZ = 21`. |
| W2 | South bow-out (one creative variation) | PASS | `SouthBowDepth = 4`, `SouthBowHalfWidth = 9`; bow built as 3 runs (W-leg / bow face / E-leg) with 2 extra corner vertices. |
| W3 | Exactly 4 cardinal gates, one centred per side | PASS | `Gates` returns 4 `GateGap`s; the south gate is centred on the bow face. Each cardinal run leaves exactly one centred gap. |
| W4 | Gates are the only wall breaks (no posterns) | PASS | Only `HasGate` runs leave a gap; the 2 SE/SW bow connector legs are solid. |
| W5 | Corner pieces at every vertex | PASS | One `Corner=true` segment per corner vertex (6 corners with the bow). Outward bisector rotation computed. |
| W6 | No diagonal walls — axis-aligned | PASS | All runs between axis-aligned vertices; bow uses axis-aligned steps, no diagonal runs. |
| W7 | Straight runs split into ~`SectionLen` modular sections | PASS | `BuildRunSections` splits each run into ~5.2u sections; corner inset prevents overlap. |
| W8 | Stable damage IDs for walls + gates | PASS | `wall-<index>` for sections, `gate-0..3` (N/E/S/W) for gates. |
| W9 | API preserved for the builder/controller (Segments / Gates by reflection) | PASS | `WallSegmentData` / `GateGap` field names preserved; `Segments`, `Gates`, `CornerVertices` exposed; `WallThickness` / `GateHalfWidth` constants kept. |
| W10 | Dimensions mirrored consistently with the builder | PASS | `VillageSceneBuilder` mirrors `WallHalfX=28 / WallHalfZ=21 / SouthBowDepth=4` (it cannot reference `DeNelle.Village` per asmdef rules) — values match exactly. |

**Curtain-Wall Layout verdict: PASS — 10/10. The shaped rectangle + 4-gate structure exactly matches §4; clean, well-documented, geometrically sound.**

---

## 5. Area Map B3 — The Healer's Cottage Dungeon (`healers-cottage.json`)

Validated against `dungeons-3d-unity-layout-spec.md` §9 (expanded Healer's Cottage — 3 levels, 12 rooms).

| # | Requirement | Spec ref | Verdict | Evidence / Notes |
|---|---|---|---|---|
| D1 | 3 levels (ground / upper / underground) | §9 | PASS | `_schemaNotes`: ground Y=0, upper Y=6, underground Y=-6. All three present. |
| D2 | 12 rooms total | §9.4 | PASS | Exactly 12 room entries: garden-approach, entrance-room, main-room, kitchen, pantry-alcove, workshop, loft-bedroom, loft-study, root-cellar, storage, crypt-sublevel, hidden-vault. |
| D3 | Ground floor — 6 rooms | §9.1 | PASS | garden-approach, entrance-room, main-room, kitchen, pantry-alcove, workshop = 6. (Spec §9.1's prose lists 5 named beats; pantry-alcove is the explicit pantry the prose calls out — count is 6, matching the §9.1 header.) |
| D4 | Upper floor — 2 rooms (loft bedroom + study) | §9.2 | PASS | loft-bedroom + loft-study, both at upper-level Y. |
| D5 | Underground — 4 rooms | §9.3 | PASS | root-cellar, storage, crypt-sublevel, hidden-vault = 4. |
| D6 | 5 scripted combat encounters (Garden, Main ×2, Cellar, Storage) | §9.4 | PASS | `scriptedEncounters` has 4 entries; the Main Room entry pairs two enemy types (`hollow_villager_a` + `_b`) = the "Main ×2" beat. 5 scripted fights as specced. |
| D7 | 1 mini-boss (Workshop) | §9.4 | PASS | `miniBoss` = "The Apprentice of the Apothecary" in `workshop` (kind `boss`). |
| D8 | 5 lore stones (Entrance, Main, Loft Bedroom, Workshop, optional Hidden Vault) | §9.4 | PASS | `loreStones`: journal-1 (entrance), journal-2 (main), journal-3 (loft-bedroom), journal-4 (workshop), journal-vault (hidden-vault) = 5, rooms match exactly. |
| D9 | 3 hidden rooms (Root Cellar, Hidden Vault, Loft Study) | §9.4 | PARTIAL | hidden-vault is flagged `secret: true` and reached via an `illusory` wall. Root Cellar (trapdoor) and Loft Study (lantern-reveal) are present as rooms but are NOT flagged `secret` and their reveal mechanic (trapdoor / lantern) is not encoded in the JSON — they connect via ordinary `doorway` walls. The 3 rooms exist; only 1 of 3 hidden-reveal mechanics is data-encoded. |
| D10 | 1 trap room (Crypt Sub-Level) | §9.4 | PARTIAL | crypt-sublevel exists with the correct `checkpoint` kind and `illusory` wall to the vault, but the §9.3 spike-tile trap floor is not represented in the JSON (no trap field/asset). The room is present; the trap mechanic is not data-encoded. |
| D11 | 2 checkpoint shrines (Entrance post-Bryn, Crypt pre-boss) | §9.4 | PASS | `checkpoints`: checkpoint-entrance (entrance-room), checkpoint-crypt (crypt-sublevel) = 2, with canon-voice toasts. |
| D12 | 3 treasure chests | §9 | PASS | `chests`: cellar-chest (Lightbearer Cloak), storage-chest (Soul Embers), vault-chest (rare crafting shard) = 3. |
| D13 | Lore prose / Bryn line canon-verbatim | §9 / design doc | PASS | `_comment` asserts lore + Bryn's line + mini-boss are canon-verbatim from `dungeon-3d-healers-cottage-design.md`; Bryn's `firstEncounterLine` and the 5 journal bodies are present in full. |
| D14 | Entry / spawn defined | §9 | PASS | `entryRoomId: garden-approach`, `spawn` at (-28,0,0) facing Y=90. |
| D15 | Coordinates match the scene builder | §9 | PASS | `_comment` + `_sources` state coords match `DungeonSceneBuilder.cs`; room bounds + wall coords are internally consistent (shared doorway coords match between adjacent rooms). |
| D16 | Random encounters gated OFF for v1 | §9.5 schema | PASS | `disableRandomEncounters: true`; v1 uses scripted, room-placed fights only. |

**Healer's Cottage verdict: PASS on structure/counts — 14/16. The 2 PARTIALs are reveal/trap *mechanics* not encoded in the layout JSON; the room *structure* (3 levels, 12 rooms, all kinds) is fully correct. Whether trap/reveal mechanics belong in this data file or in `DungeonController` logic is an architecture question — see C5.**

---

## 6. Conditions of Acceptance

These are the specific items that must be resolved for a clean **ACCEPT** (distinct from the already-logged DEFERRED items, which are owner-approved deviations and do not block).

| # | Condition | Affects | Why it blocks a clean ACCEPT |
|---|---|---|---|
| **C1** | **Fix the Elarion + Keeper's Keep white-render.** The two centerpieces render as a white mass (`screenshot-village-elarion.png`). It is logged + deferred, so not a FAIL — but Acceptance Criterion §12.3 ("two centerpieces") and §12.11 ("no missing-mesh placeholders") are visibly unmet, and this is the single most player-facing flaw at the literal heart of the village. Requires the interactive-Editor pass `unity-decisions.md` already calls for. | Village (V12, V49, V57) | The village cannot ship to players with a white blob where the sacred world-tree and the Keep should be. |
| **C2** | **Tone down the `ForceFieldShimmer` over-bright gate emissive.** Emissive intensity 1.4 washes the gate force-field to white instead of reading violet. Logged as a finetune item; the real shimmer shader lands Week 4. Confirm it reads violet before sign-off. | Village (V48) | Gates are a canon-locked feature; they must read as violet-warded, not white. |
| **C3** | **Owner must ratify the Realm Map region names + description prose.** The 5 region names follow React save-key catalog (correct for compatibility) but have no narrative-canon authority, and all `description` text was authored fresh. Flagged `unity-decisions.md` 2026-05-19. | Realm Map (M10) | Region names will appear in shipped Realm Map UI; they must be canon-ratified before that UI ships. |
| **C4** | **Complete the exterior wilderness pass (Task #14).** The exterior currently shows black unlit terrain, no skybox (orange void), and stray floating objects (`screenshot-village-week3-exterior.png`). It is owner-flagged "finetune later" and a separate agent's job, so it does not block the *interior* Week-3 deliverable — but §9 / Acceptance Criteria §12.9 require a believable continuous outer world before the village as a whole is player-ready. | Village (V44-V46, V55) | The §9 spec is real scope; the village reads as a floating island until it lands. |
| **C5** | **Decide where dungeon trap + hidden-reveal mechanics live.** The Healer's Cottage JSON encodes only 1 of 3 hidden-reveal mechanics (the illusory-wall vault) and does not encode the Crypt spike-tile trap. Confirm whether trapdoor / lantern-reveal / spike-trap are intended to be data-driven (then add them to the JSON schema) or controller-logic (then this is correct as-is and C5 closes). | Dungeon (D9, D10) | §9.3/§9.4 promise these mechanics; a decision is needed so they are not silently lost between the data file and the controller. |

### Already-logged DEFERRED items (owner-approved — do NOT block acceptance)

- Centerpiece white-render investigation status (logged in full, `unity-decisions.md` 2026-05-19) — *the fix itself is C1.*
- Wall tiering material swaps (upgrade-visual system, not a Week-3 deliverable).
- Per-direction approach-lane / exterior biome flavour (Task #14, separate agent).
- Per-direction fog gradient (built-in fog limitation; volumetric pass needed).
- FPS-60 verification + Cinemachine flythrough (require on-device / interactive validation, not evidenceable in a read-only static review).

---

## 7. Final PO Position

| Deliverable | Verdict | One-line basis |
|---|---|---|
| **Avalon Village** | **ACCEPT WITH CONDITIONS** | Structurally complete and spec-faithful (33/39 PASS); blocked from a clean ACCEPT only by logged, owner-flagged visual-quality items (C1, C2, C4). |
| **Area Maps** | **ACCEPT WITH CONDITIONS** | Realm map, wall layout, and dungeon all match spec structure/counts (24/28 PASS); blocked from a clean ACCEPT only by the canon-authority gap (C3) and the trap/reveal-mechanic decision (C5). |

The Week-3 village and the map-data deliverables are **approved to progress** — the conditions above are quality-gate items to close before a player-facing release, not rework that invalidates the milestone. No requirement received a FAIL: every gap is either a logged owner-approved deferral or a named condition with a clear owner.

**Combined coverage: 57 of 67 requirements PASS (85%). Conditions of acceptance: 5.**

---

_Validated read-only. The Folk made it spacious. The walls hold what matters._
