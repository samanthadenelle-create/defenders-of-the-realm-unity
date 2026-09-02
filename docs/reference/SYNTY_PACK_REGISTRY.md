# SYNTY PACK REGISTRY

**Created:** 2026-09-02 · **Method:** read-only audit of the working tree at `feat/synty-art-retheme`.
**Scope:** `Assets/Synty/` — what is in it, what the game actually consumes, what it is worth, and how
to spend the rest of it.

This is a durable registry, not a one-off report (memory `audit-outputs-as-known-dictionaries`).
Every count below was computed from the tree, not estimated. Where a number is uncertain it says so.

---

## 0. AT A GLANCE

| Fact | Value | How measured |
|---|---|---|
| Packs on disk | 2 (+1 helper) | `ls Assets/Synty/` |
| `PolygonFantasyKingdom` | 305 MB | `du -sm` |
| `PolygonGeneric` | 156 MB | `du -sm` |
| `SyntyPackageHelper` | ~1 MB | `du -sm` |
| Total prefabs | **2682** | `find Assets/Synty -name "*.prefab" \| wc -l` |
| Total FBX | 2736 | `find ... -name "*.fbx"` |
| Materials | 160 | `find ... -name "*.mat"` |
| Textures (PNG) | 193 | `find ... -name "*.png"` |
| Shader Graphs | 14 | `find ... -name "*.shadergraph"` |
| **Synty prefabs referenced anywhere in the project** | **27 of 2682 (1.0%)** | GUID intersect, see §2 |

---

## 1. WHAT IS IN THERE — by what a game needs

### 1.1 Verified naming convention

The prefixes actually present (measured, not assumed — `SM_Env_` and `SM_Item_` exist, and
`PolygonGeneric` uses a **double-token** `SM_Gen_<Domain>_` form that a single-prefix survey misses):

| Prefix | Count | Meaning |
|---|---|---|
| `SM_Prop_` | 815 | Props / dressing |
| `SM_Bld_` | 707 | Buildings + castle architecture |
| `SM_Gen_` | 404 | PolygonGeneric (splits into `_Env_` 224, `_Prop_` 108, `_Chr_` 44, `_Bld_` 25, `_Wep_` 3) |
| `SM_Item_` | 266 | Small handheld / clutter items |
| `SM_Env_` | 189 | Nature / terrain dressing |
| `SM_Wep_` | 187 | Weapons **and siege engines** |
| `SM_Chr_` | 32 | Characters (22 unique + 10 `_Attach_` accessories) |
| `SM_Generic_` | 31 | Skybox / distant-scenery pieces |
| `SM_Veh_` | 11 | Carts, wagon, boat, wheelbarrow |
| `FX_*` / `LightRay_*` | 40 | Particle FX |

### 1.2 Category map (path + count)

**Fortification / castle kit — 348 prefabs.** `PolygonFantasyKingdom/Prefabs/Castle/`
Token breakdown: `Roof` 91, `Wall` 87, `Battlements` 39, `Floor` 18, `Pillar` 16, `Hoarding` 12,
**`DestroyedWall` 12**, `Drawbridge` 2, `Archway`, `Balcony`, `Door`, plus loose `SM_Bld_Keep_Pillar_*`,
`SM_Bld_Walkway_Stone/Wood_*`, `SM_Bld_Stairs_*`.
Representative: `SM_Bld_Castle_Wall_01`, `SM_Bld_Castle_Wall_Gate_01`, `SM_Bld_Castle_Battlements_01`,
`SM_Bld_Castle_Wall_Tower_S/M/L_01`, `SM_Bld_Castle_Hoarding_Wood_Wall_01`.
*This is a modular kit — pieces snap into a ring, they are not one-shot models.*

**Buildings — 305 prefabs.** `PolygonFantasyKingdom/Prefabs/Buildings/`
Includes **26 assembled `Presets/`** (`SM_Bld_Preset_*_Optimized`) — whole finished buildings, mesh-merged.
Representative presets: `Hut_01`, `House_01_A`, `House_02_A`, `House_03`, `House_06`, `Blacksmith_01`,
`Stables_01`, `Tavern_01`, `Church_01_B`, `Tower_01`, `House_Windmill_01`.
Also `SM_Bld_Tent_*` (9 — Large/Open/Round/Single/Small/Square, several with `_Burnt` variants),
`SM_Bld_Wooden_Tower_01/02`, `SM_Bld_House_Stairs_*` with railings.
**The `_Optimized` presets are the ones already in use — they are the intended consumption unit.**

**Props / dressing — 791 (FK) + 108 (Generic) = 899.**
`PolygonFantasyKingdom/Prefabs/Props/` splits into `Furniture/ 116`, `BattleGround/ 46`,
`DeadBodies/ 44`, `Banners/ 43`, `Paths/ 27`, `Preset/ 16`, plus ~499 loose.
Loose tokens: `Sign` 37, `Flag` 36, `Table` 28, `Path` 27, **`Destroyed` 18**, `Arrow` 18, `Animal` 18,
`Bracket` 17, `Bar` 17, `Hanging` 16, `Rug` 15, `Painting` 15, `Fireworks` 14, `Market` 13, `Candle` 12,
`Statue` 11, `Poster` 11, `PlanterBox` 11, `Chair` 11, `Battle` 11.

**Environment / nature — 189 (FK) + 224 (Generic) = 413.**
FK `Prefabs/Environments/`: `Flowers` 24, `Bush` 23, `Tree` 22, `Planter` 20, `Ground` 16, `Path` 15,
`Rock` 12, `Ivy` 12, `Grass` 5, `StoneWall` 4, `Hedge` 4, `Fern` 4, `River` 3, `Reeds` 3, `Lily` 3.
Generic `Prefabs/Environment/`: `Ground` 43, `Road` 26, `Ivy` 16, `Rock` 15, `Bush` 14, `Grass` 11,
`Tree` 9, `Flowers` 8, `Dirt` 8, `Cliff` 7, `Vines` 5, `Hill` 5, `Stalactite` 4, `Mountain` 3.

**Items / clutter — 266.** `PolygonFantasyKingdom/Prefabs/Items/`
`Bottle` 28, `Meat` 23, `Wargames` 17, `Candlestick` 12, `Ring` 10, `Necklace` 10, `Hammer` 7, `Dish` 7,
`Key` 6, `Jug` 6, `Cheese` 6, `Bracelet` 6, `Pie` 5, `Lock` 5, `Jar` 5, `Gem` 5, `Crystal` 5, `Book` 5.

**Characters — 22 unique + 10 attachments (FK), 44 more (Generic).**
`PolygonFantasyKingdom/Prefabs/Characters/`: Bartender, Blacksmith (F/M), Fairy, FortuneTeller, Headsman,
Hermit, Jester, King, Mage, Merchant, Monk, Nun, Peasant (F/M), Priest, Prince, Princess, Queen, Rider,
Soldier (F/M).
**Verified rigged:** `SM_Chr_King_01.prefab` contains a full bone hierarchy (`Shoulder_R`, `Elbow_L`,
`Jaw`, `Finger_*`, `Toes_R`) — these are skinned humanoid meshes, not static props. *Not verified: whether
the rig maps cleanly to the project's existing humanoid Avatar. Treat that as an open question, not a
given.*

**Weapons + siege — 187.** `Prefabs/Weapons/ 183` + `Prefabs/SiegeEngines/ 15`.
`Mod` 72 (modular weapon parts), `Sword` 18, `Spear` 12, `Axe` 11, `Staff` 10, `Shield` 10, `Sceptre` 9,
`Dagger` 7, `Mace` 6, `Hammer` 6, `Ballista` 5, `Knife` 4, `Trebuchet` 1, `Mortar` 1, `Rammer` 2.

**Vehicles — 12.** `Cart` 7, `Wheelbarrow`, `TraderWagon`, `Boat`, `Arrow`.

**FX — 40.** `FX_Fire` 6, `FX_Smoke` 4, `FX_Catapult` 4, `FX_Fog` 3, `FX_Dust` 3, `FX_Rain` 2,
`FX_Magic` 2, `FX_Arrow` 2, plus singles: `Blood`, `Candle`, `CandleFlame`, `Flies`, `Fountain`, `Leaves`,
`Snow`, `SunBeam(s)`, `WaterWheel`, `Waterfall`, `Wind`, `LightRay_Round/Cube`.

**Skybox / distance — 31.** `SM_Generic_*`: `Ground` 9, `Tree` 4, `Mountains` 4, `Cloud` 4, `Grass` 3,
`Water` 2, `CloudRing`, `TreeStump`, `TreeDead`.

**UI — none.** Neither pack ships UI sprites or icons. Do not plan on them.

**Damage states — 32 `destroy*` + 11 `burnt*` + 1 `damaged*` prefabs** (case-insensitive filename match
across both packs). Includes `SM_Bld_Castle_DestroyedWall_*` (12), `SM_Prop_Destroyed_*` (18),
`SM_Bld_Tent_*_Burnt`, `SM_Prop_Destroyed_House_Piece_*_Burnt`.

### 1.3 Materials and shaders — Synty is NOT the Polyperfect situation

- Materials are **atlas-driven**: 160 materials over 193 textures, and the buildings share a handful of
  atlases (`PolygonFantasyKingdom_01_A/B/C` … `_04_C`, plus `Roof_*`, `Wall_*`, `Flag_*` variants).
  Few materials across hundreds of meshes = few draw calls. This is the main technical reason the pack
  is cheap to render on mobile.
- Materials bind to **Synty's own Shader Graphs**, not Standard. Verified:
  `PolygonFantasyKingdom_Mat_01_A.mat` → `m_Shader: {guid: 0730dae39bc73f34796280af9875ce14}` →
  `Assets/Synty/PolygonGeneric/Shaders/Generic_Basic.shadergraph`.
- **Consequence: there is no URP fix-up pass needed.** Shader Graph is URP-native. Unlike the Polyperfect
  pack (CLAUDE.md §4, `Defenders/Art/Fix Polyperfect URP Materials`), Synty materials render correctly
  as imported. One caveat observed in the same material: `m_InvalidKeywords: - _NORMALMAP`, i.e. some
  keywords are stale relative to the graph. Cosmetic, not a pink-material failure — but worth a
  greyscale screenshot check rather than an assumption.

---

## 2. WHAT IS ALREADY USED — measured, not assumed

### 2.1 Method (so the number is checkable)

Addressables groups store **GUIDs, not paths**, so a path grep proves nothing. The audit:

1. Parsed every `Assets/Synty/**/*.{prefab,fbx,mat}.meta` → GUID map.
   Result: 2682 prefab GUIDs, 2736 FBX GUIDs, 160 material GUIDs.
2. Read **every** `.asset` / `.unity` / `.prefab` / `.json` / `.mat` / `.controller` / `.txt` file under
   `Assets/` **except** `Assets/Synty/` itself — **12,946 files** — and extracted every 32-hex GUID.
3. Intersected.

> **Method warning for the next seat.** The first pass of this audit excluded any directory *named*
> `Synty`, which silently also excluded `Assets/StructureContent/Synty/` — the very folder holding the
> re-theme, and it under-reported usage as 5 instead of 27. **Exclude by full path
> (`Assets/Synty`), never by directory basename.**

### 2.2 Result

**27 distinct Synty prefabs are referenced anywhere in the project — 1.0% of 2682.**
**Zero** Synty FBX or materials are referenced directly (everything goes through the prefabs, as intended).

Two consumers, and only two:

**(a) The hub scene — 5 prefabs, direct instances.** `Assets/Scenes/Main_Castle_Overworld.unity`
(the WO-1290 castle perimeter ring):
`SM_Bld_Castle_Wall_01`, `SM_Bld_Castle_Wall_Arrowslit_01`, `SM_Bld_Castle_Battlements_01`,
`SM_Bld_Castle_Wall_Gate_01`, `SM_Bld_Castle_Wall_Tower_M_01`.

**(b) 30 re-wrap prefabs under `Assets/StructureContent/Synty/`**, each a prefab variant whose
`m_SourcePrefab` is a Synty prefab. They consume **25 distinct** Synty prefabs (some shared):

| Re-wrap (`Assets/StructureContent/Synty/…`) | Synty source |
|---|---|
| `farm.prefab` | `Buildings/Presets/SM_Bld_Preset_Hut_01_Optimized` |
| `PetHouse2.prefab` | `Buildings/Presets/SM_Bld_Preset_Hut_02_Optimized` |
| `House_Medieval_Medium.prefab` | `Buildings/Presets/SM_Bld_Preset_House_01_A_Optimized` |
| `store.prefab` | `Buildings/Presets/SM_Bld_Preset_House_02_A_Optimized` |
| `jeweler.prefab` | `Buildings/Presets/SM_Bld_Preset_House_03_Optimized` |
| `lumbermill.prefab` | `Buildings/Presets/SM_Bld_Preset_House_06_Optimized` |
| `Forge.prefab`, `armorer.prefab` | `Buildings/Presets/SM_Bld_Preset_Blacksmith_01_Optimized` |
| `barracks.prefab` | `Buildings/Presets/SM_Bld_Preset_Stables_01_Optimized` |
| `ShopAndCrafting.prefab` | `Buildings/Presets/SM_Bld_Preset_Tavern_01_Optimized` |
| `ArcaneSpire_1.prefab`, `arcane tower.prefab` | `Buildings/Presets/SM_Bld_Preset_Tower_01_Optimized` |
| `ArcaneSpire_3.prefab` | `Buildings/Presets/SM_Bld_Preset_Church_01_B_Optimized` |
| `Watermill_Medieval.prefab`, `Windmill_Medieval.prefab` | `Buildings/Presets/SM_Bld_Preset_House_Windmill_01_Optimized` |
| `ArcaneSpire_2.prefab`, `Tower_Wooden_Watchtower_L3.prefab` | `Castle/SM_Bld_Castle_Wall_Tower_L_01` |
| `Tower_Wooden_Watchtower_L2.prefab` | `Castle/SM_Bld_Castle_Wall_Tower_M_01` |
| `Tower_Wooden_Watchtower.prefab` | `Castle/SM_Bld_Castle_Wall_Tower_S_01` |
| `Wall_Medieval_Stone.prefab` | `Castle/SM_Bld_Castle_Wall_01` |
| `Wall_Medieval_Wood.prefab` | `Castle/SM_Bld_Castle_Hoarding_Wood_Wall_01` |
| `Gate_Medieval_Medium.prefab` | `Castle/SM_Bld_Castle_Wall_Gate_01` |
| `Well.prefab` | `Props/SM_Prop_Well_01` |
| `Torche_Wall.prefab` | `Props/SM_Prop_Torch_01` |
| `HealingCaravan.prefab` | `Vehicles/SM_Veh_TraderWagon_01` |
| `Catapult.prefab` | `SiegeEngines/SM_Wep_Catapult_01` |
| `Ballista.prefab`, `Ballista_L1.prefab` | `SiegeEngines/SM_Wep_Ballista_Mobile_01` |
| `Ballista_L2.prefab` | `SiegeEngines/SM_Wep_Ballista_Mounted_01` |
| `Ballista_L3.prefab` | `SiegeEngines/SM_Wep_Trebuchet_01` |

All 30 re-wraps are registered in `Assets/AddressableAssetsData/AssetGroups/Structure_Art.asset`
(71 entries total, 44 distinct addresses — see the defect in §5.1).

### 2.3 Untouched categories

**Zero** references exist to: all 899 props, all 413 environment prefabs, all 266 items, all 76
characters, all 187 weapons, all 40 FX, all 31 skybox pieces, and ~680 of the 707 `SM_Bld_` pieces
(the modular castle kit is used only as 6 piece types out of 348).

---

## 3. THE VALUE VERDICT — honest

**Was it worth the money? Yes, but almost none of the value has been collected yet.**

- **99.0% of the pack is unused** (2655 of 2682 prefabs). That is not waste — it is unspent inventory.
- **Relevance is unusually high for this specific game.** The honest failure mode with a big art pack is
  that most of it is for a different genre. That is not the case here. `PolygonFantasyKingdom` is a
  medieval-castle-and-town pack and this is a medieval castle-defence town-builder. The largest
  categories — 348 castle/fortification pieces, 305 buildings, 899 props, 413 environment — map
  one-to-one onto surfaces this game already has.
- **The genuinely low-relevance slice is small.** Honestly discounting: the 266 `Items` (handheld clutter
  — the game has no first-person/inventory-in-world surface for most of it), the 72 `SM_Wep_Mod_*`
  modular weapon parts (no weapon-assembly system exists), and parts of `PolygonGeneric` that duplicate
  FK coverage. Call it roughly 350–450 prefabs, ~15%, with no near-term home. Everything else has a
  plausible use.
- **The technical fit is better than the existing packs.** Atlas-shared materials, URP-native Shader
  Graphs, no fix-up pass, and pre-assembled `_Optimized` building presets. The Polyperfect pack needs a
  URP repair menu item to even render; Synty does not.

**The single biggest untapped category: the 348-piece modular castle/fortification kit** — specifically
the **12 `SM_Bld_Castle_DestroyedWall_*` pieces plus 39 `Battlements` and 12 `Hoarding` pieces**. This is
a castle-defence game whose walls take damage, and the pack ships purpose-built damaged-wall geometry
that nothing in the project currently uses. Six piece types out of 348 are wired.

Runner-up: **413 environment prefabs**, which is exactly what WO-1292 is waiting on.

---

## 4. HOW TO USE IT — ranked by player-felt impact per unit of effort

Ranked highest value-per-effort first. Each is grounded in a real WO, a real empty surface, or a
measured gap.

### 4.1 Finish WO-1292 environment dressing — READY, blocked only on WO-1291

**Effort: low. Impact: high — this is the frame the owner actually looks at.**
`WorkOrders/WORK_ORDER_1292_synty_environment_dressing.md` is **READY TO IMPLEMENT**, blocked on
WO-1291 (IN PROGRESS: 30 of 33 addresses swapped, 3 unmapped). The hub scene still carries ~140
`Rock_*_Color1` Polyperfect/Quaternius instances that visibly read as a different pack from the
now-Synty buildings — a mixed-pack look is more damaging than either pack alone.
**Replaces:** ~140 rock instances + paths + banners. **Assets available:** 413 environment prefabs,
27 `Props/Paths/`, 43 `Props/Banners/`.
**To build:** a name-mapped scripted swap preserving transforms (the WO forbids hand-editing `.unity`,
CLAUDE.md §3), then NavMesh re-bake. Nothing new architecturally.

### 4.2 Damaged-wall visual states for the defence loop

**Effort: medium. Impact: high — it makes damage legible, which is the core loop's feedback.**
The pack ships **12 `SM_Bld_Castle_DestroyedWall_*`** pieces plus `Hoarding` (12) and `Battlements` (39)
that are dimensionally interchangeable with the `SM_Bld_Castle_Wall_01` pieces already placed in
`Main_Castle_Overworld.unity` by WO-1290. Today a damaged wall is not communicated by geometry.
**Replaces:** nothing — this is net-new feedback on an existing system (`WallRepairController`,
`WallSegment`, and `StructureBurn` already exist and are modified in the current working tree).
**To build:** a health-threshold → mesh-swap on the wall segment. The presentation layer must own it,
never the wall object (CLAUDE.md, HP B2B: presentation is a separate layer). Pair it with the
`FX_Fire`/`FX_Smoke`/`FX_Dust` prefabs already in the pack.

### 4.3 Vendor and town NPCs from the 22 rigged characters

**Effort: medium. Impact: high — the town reads as inhabited instead of empty.**
Canon already calls for "movable functional storefronts + **vendor NPCs**" (CLAUDE.md §8), and
`TalkPromptRegistry` / `TalkHudBridge` / the conditional `Talk` action-bar face already exist and are
gated on `TalkPromptRegistry.Count > 0` (CLAUDE.md §7) — i.e. **the talk seam is built and starving for
actors.** The pack's characters map almost comically well onto the existing structures: Blacksmith →
`armorer`/`Forge`, Merchant → `store`, Bartender → `ShopAndCrafting`, Mage → `arcane tower`,
Peasant → `farm`/`lumbermill`, Soldier → `barracks`, King/Queen → keep.
**Assets:** 22 unique FK characters + 44 in PolygonGeneric + 10 `_Attach_` accessories.
**To build:** confirm the Synty rig maps to the project's humanoid Avatar (**unverified — check this
first, it is the whole risk**), then a per-structure NPC anchor + idle animation + a `TalkPrompt`
registration. Reuse `EchoWorldPresence`'s one-owner lifecycle shape (CLAUDE.md §7) — one owner, one
spawner, do not add a second.

### 4.4 Siege engines as real defence structures

**Effort: low-medium. Impact: medium-high.**
Already half-done and under-exploited: `Ballista_L1/L2/L3` and `Catapult` addresses point at Synty siege
prefabs, and `SM_Wep_Trebuchet_01` is doing duty as `Ballista_L3`. The pack has **15 SiegeEngines** plus
`FX_Catapult` (4 FX prefabs) and `FX_Arrow` (2).
**To build:** wire the existing `FX_Catapult_*` / `FX_Arrow_*` to the firing hooks rather than generic
VFX. Note the VFX rule: the owner tags VFX keys; the CLI maps key → named hook verbatim and never
substitutes (memory `vfx-map-owner-tags-no-creative-pick`) — so this one needs her tags first.

### 4.5 Market stalls, banners and signage for the storefront frontages

**Effort: low. Impact: medium.**
`Props/` carries `Market` 13, `Sign` 37, `Flag` 36, `Banners/` 43, `Poster` 11, `Statue` 11,
`Furniture/` 116. The storefront/vendor model is the live monetization shape (CLAUDE.md §8).
**Constraint:** the owner is red/green colourblind — separate ownership/faction by **shape and value**,
never by hue (WO-1292 already records this; memory `owner-colorblind-delegate-visual-creative`).

### 4.6 Tents and battle-ground dressing for raid/wave staging

**Effort: low. Impact: medium.**
`SM_Bld_Tent_*` (9, several with `_Burnt` variants), `Props/BattleGround/` 46, `Props/DeadBodies/` 44,
`Props/Destroyed*` 18. A besieging army currently has no camp; wave spawn sides
(`WaveSpawnPoint`, `GateIndex` 0 N / 1 E / 2 S / 3 W) are unmarked in the world. Dressing each gate
approach with a camp makes the threat direction readable before the wave arrives.

### 4.7 Distance scenery — `SM_Generic_*` (31)

**Effort: very low. Impact: low-medium, but nearly free.**
`Mountains` 4, `Cloud` 4, `CloudRing`, `Ground` 9, `Tree` 4. The scene currently uses
`DistantMountainPeak` from another pack. Cheap horizon consistency.

### 4.8 Deliberately NOT recommended

- The **266 `Items`** and **72 `SM_Wep_Mod_*`** parts: no system consumes them, and building one to
  justify the art is the tail wagging the dog.
- Re-theming the **archer tower**: reverted to the owner's own Tripo art by her ruling 2026-09-02. Leave it.
- Undoing any of the WO-1289/1290/1291 swaps: ruled deliberate and KEPT.

---

## 5. TRAPS — what a future seat must not do

### 5.1 LIVE DEFECT: 27 duplicate Addressables addresses in `Structure_Art.asset`

**This is not historical. It is true in the tree right now.**
`Assets/AddressableAssetsData/AssetGroups/Structure_Art.asset` has **71 entries but only 44 distinct
addresses**. Twenty-seven addresses are claimed by **two** assets each — the original under
`Assets/StructureContent/` and the Synty re-wrap under `Assets/StructureContent/Synty/`, because the
re-wrap prefabs **reuse the original filenames verbatim** and the address is derived from the filename.

Examples (each address resolves to two different assets):

```
Structures/farm       -> Assets/StructureContent/farm.fbx
                      -> Assets/StructureContent/Synty/farm.prefab
Structures/barracks   -> Assets/StructureContent/barracks.fbx
                      -> Assets/StructureContent/Synty/barracks.prefab
Structures/Catapult   -> Assets/StructureContent/Catapult.prefab
                      -> Assets/StructureContent/Synty/Catapult.prefab
```

Full duplicated set: `ArcaneSpire_1/2/3`, `Ballista`, `Ballista_L1/L2/L3`, `Catapult`, `Forge`,
`Gate_Medieval_Medium`, `HealingCaravan`, `House_Medieval_Medium`, `PetHouse2`, `ShopAndCrafting`,
`Torche_Wall`, `Wall_Medieval_Stone`, `Wall_Medieval_Wood`, `Watermill_Medieval`, `Well`,
`Windmill_Medieval`, `arcane tower`, `armorer`, `barracks`, `farm`, `jeweler`, `lumbermill`, `store`.

**Which asset a duplicate address resolves to is not something to guess at.** This is the mechanism by
which a stone castle tower previously masqueraded as the wooden watchtower. Note that the three
watchtower re-wraps were given **distinct** addresses (`Structures/Synty_Tower_Castle_Wall_S/M/L`) —
**that is the correct pattern, and it is the fix for the other 27.** Do not resolve this by deleting the
originals blind: `Structures/*` addresses are live content keys.

Reproduce with:
`m_GUID`/`m_Address` pair extraction from `Structure_Art.asset`, then resolve each GUID against
`Assets/StructureContent/**/*.meta`.

### 5.2 Any Synty asset added to Addressables needs a content build AND an R2 push

CLAUDE.md §16 is binding and this is exactly the case it exists for. Structure and enemy art is served
from `https://pub-ab6dfaf1b3d74ca78891876611ccb832.r2.dev/[BuildTarget]` with **no local fallback**.
**Bundle names are content-hashed, so every content build needs its own push — a previous push can never
cover a new build.** The failure is silent: the game installs, launches, plays, and shows placeholder
geometry with no on-screen error.

- Sanctioned path is **one file**: `tools\r2-ship.ps1`. Do not re-inline push or verify into any chain.
- Judge by the marker on a **fresh** log (`R2_PUSH_OK`, `R2_PARITY_OK`), never the exit code.
- WO-1292's acceptance criteria already carry `R2_PARITY_OK` for this reason.

### 5.3 461 MB of raw pack must never enter the mobile build

`PolygonFantasyKingdom` 305 MB + `PolygonGeneric` 156 MB. Anything used ships through
Addressables/remote, the same as the existing structure art. Nothing Synty belongs in `Resources/` or in
a directly-referenced scene slot beyond what the perimeter ring already places.
**Corollary:** the 5 direct scene instances in `Main_Castle_Overworld.unity` (§2.2a) are *in-build*
content, not remote. Adding more Synty prefabs as direct scene instances grows the APK; adding them as
Addressables does not. Prefer the re-wrap-plus-address pattern.

### 5.4 Shaders — Synty is fine, but do not assume the Polyperfect procedure applies

Synty materials bind to Synty's own URP Shader Graphs and need **no** fix-up pass. Running the
Polyperfect URP repair over them is unnecessary and risks rebinding them to Lit. The one real
observation is stale keywords (`m_InvalidKeywords: _NORMALMAP`); verify visually with a headless capture
rather than by editing materials.

### 5.5 Method traps for whoever re-runs this audit

- **Exclude `Assets/Synty` by full path, not by directory basename** — `Assets/StructureContent/Synty`
  is a different folder and excluding it silently under-reports usage by 5x (this happened during this
  audit; see §2.1).
- **Addressables store GUIDs, not paths.** Grepping for `Assets/Synty/...` in the group assets returns
  nothing and proves nothing.
- **`PolygonGeneric` uses a two-token prefix** (`SM_Gen_Env_`, `SM_Gen_Prop_`). A first-two-token survey
  buckets all 404 as `SM_Gen` and hides the split.
- **Survey by token, not by guessed filename** (memory `search-by-token-not-by-name`).

---

## 6. PROVENANCE

All counts computed 2026-09-02 against the working tree on `feat/synty-art-retheme`.
Read-only audit: no file under `Assets/` was modified, no gate, build, batchmode, or git command was run
(the owner had Unity open and held the project lock).

Key sources:
- `Assets/Synty/` — the packs.
- `Assets/AddressableAssetsData/AssetGroups/Structure_Art.asset` — 71 entries, 44 distinct addresses.
- `Assets/StructureContent/Synty/` — 30 re-wrap prefabs.
- `Assets/Scenes/Main_Castle_Overworld.unity` — 5 direct Synty instances.
- `WorkOrders/WORK_ORDER_1290_synty_castle_walls_native_module.md` (IN PROGRESS),
  `WORK_ORDER_1291_synty_building_retheme.md` (IN PROGRESS, 30/33),
  `WORK_ORDER_1292_synty_environment_dressing.md` (READY, blocked on 1291).

**Open questions this audit did not resolve** (stated rather than guessed):
1. Whether the Synty character rigs map to the project's existing humanoid Avatar. Gates §4.3.
2. Which asset each of the 27 duplicate addresses currently resolves to at runtime. Needs a content
   build or a play-session probe, both of which need the Unity lock.
3. Whether the `m_InvalidKeywords: _NORMALMAP` entries cause any visible artifact. Needs a capture.
