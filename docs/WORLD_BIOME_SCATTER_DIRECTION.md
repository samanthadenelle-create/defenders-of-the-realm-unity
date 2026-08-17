# World Biome / Scatter Creative Direction (WO-449)

> ## ⚠ PARTIALLY SUPERSEDED 2026-08-17 (WO-1101 implementation / WO-1044 canon). READ THIS BOX FIRST.
> This document is still useful as a *prefab shopping list*, and it is **wrong in three ways that
> produce broken code or broken canon**. All three were verified against disk and against HEAD.
>
> **1. ⛔ Its §Tints section (below, "Tints = the elemental realization") is RETIRED as a
> differentiator.** It prescribes separating the biomes by COLOUR TINT over shared prefabs
> ("same prefabs serve Mirewood brown-rot vs Ashwood grey-ash via tint only"). The owner is
> **red/green colourblind**, and WO-1044 §1 — **all eleven rulings owner-approved 2026-08-17** —
> authors the four marches in **VALUE, TEXTURE and LIGHT**. Hue-only differentiation is unusable to
> her and fails a greyscale check outright. Use tint hooks only to shift *value*, never as the
> differentiator. The measured contract now lives in
> `Assets/_Modules/Core/World/TerrainLayerSet.cs` and is asserted by
> `Assets/Editor/Regression/TerrainLayerRegression.cs` (ΔL ≥ 0.15 between adjacent marches).
>
> **2. ⛔ Two prefab-path errors here resolve to NULL.** (a) The stated base
> `.../Nature_M/` in §"Per-biome picks" is **one folder short for every Nature name** — the real
> layout is `Nature_M/<Sub>_M/<Name>.prefab` (e.g. `Nature_M/Stones_M/Rock_Large.prefab`), so
> concatenating base + name returns null for all of them. (b) **`Stones_Small` does not exist** —
> the real prefab is **`Stone_Small`**. Also mis-homed here: `Bone_Pile_Prehistoric` is in
> `Tribal_M/`; `Rubble_Stone` / `Skull_Human` / `Gravestone` / `Gravestone_Round` are in
> `Fantasy_M/`, not `Nature_M/`. **Verify every name against disk before you write it.**
>
> **3. ⚠ "Blink = characters/UI only (no nature). Don't look there." is FALSE.**
> `Assets/Blink/Art/Textures/` ships ~160 PBR **terrain layer** sets (BaseColor + Normal + Height +
> AO + Roughness at 2048) across nine themed packs. WO-1101 curated eight of them into the tracked
> folder `Assets/Generated/Terrain/Layers/` — that is where the world's ground art now comes from.
>
> **4. ⚠ Polyperfect is GITIGNORED (`.gitignore:182`), as is Blink (`.gitignore:350`) — zero tracked
> files each; `Assets/Quaternius/` is absent from this machine entirely.** Prefer the **git-tracked**
> KayKit packs for anything a build must contain, or curate a tracked copy. A tracked asset pointing
> into a gitignored pack renders on one machine and is colourless everywhere else — the "pink floor"
> failure class (CLAUDE.md §12).
>
> **Ashwood's ground was INVERTED in shipped code and is now canon-correct.** WO-1044 §1 authors it
> as near-black trunks on a **pale** powdery ground ("ink on ash"); the old `Exterior_Dead` layer was
> L=0.176 — dark ground. The pale ground is now enforced by a regression floor. Read row "Ashwood"
> below as *dead and skeletal in SILHOUETTE*, not as dark ground.
>
> **Verified-present KayKit ground cover (checked on disk 2026-08-17,**
> `Assets/Models/KayKit/KayKit Forest Nature Pack 1.0/Assets/fbx(unity)/Color1/`, 198 FBX**):**
> `Grass_1_A..D` and `Grass_2_A..D` (each with a `_Singlesided` variant — use those as detail cards),
> `Bush_1_A..G`, `Bush_2_A..F`, `Bush_3_A..C`, `Bush_4_A..F`, `Rock_1..Rock_6` series, `Tree_1..Tree_7`,
> `Tree_Bare_1/2`. Every FBX is suffixed `_Color1.fbx`.

**Status: CREATIVE DIRECTION** (asset-surveyed 2026-06-17 — "look at every asset before picking", owner mandate).
Feeds WO-449 (world layout). The single source for the OuterWorld biome zones + their scatter. Asset survey
verified against the real prefab files (the old `docs/polyperfect-asset-catalog.md` had stale/nonexistent names).

## The elemental zone system (folds the "biome zones / fire / water" idea into WO-449)
Life at the center, the four elements on the cardinals, difficulty escalating with corruption. The camp ring
(`CampSystem.cs:95-101`) already encodes this; this formalizes it. Hook to the existing `ZoneManager`.

| Zone | Cardinal · tier | Element | Anchor | Reads as |
|---|---|---|---|---|
| **Heart** (home) | center (0,0,0) | **Life** | the Heart-Tree | lush, verdant, surviving |
| **Goldfields** | E · 1 | **Earth / grain** | (95,0,10) | golden sunlit plains, abundance |
| **Stoneback** | W · 2 | **Stone** | (-95,0,-10) | boulder highland |
| **Mirewood** | S · 3 | **Water / mire** | (12,0,-95) | swamp, fern-choked, drowned |
| **Ashwood** | N · 4 | **Fire / ash** | (-12,0,95) | burnt, scorched, skeletal, dead |

Future depth (NOT now, capture only): elemental zones can later drive enemy types, resistances, tower bonuses,
resource flavors. For Tier-1 it's **visual identity + the existing tier difficulty** — nothing new to build.

## Asset reality (verified)
- **Polyperfect `Low Poly Ultimate Pack/_M/Prefabs_M/Nature_M/` is the ENTIRE scatter library** — low-poly,
  single shared atlas (great GPU-instancing/batching for mobile/WebGL). Animals in `Animals_M/` (28).
- **Quaternius** = architecture kit only (no nature). **Blink** = characters/UI only (no nature). Don't look there.

## Per-biome picks (exact prefabs; base = `.../Nature_M/`)
**Heart / Home (Life):** Trees `Tree_Oak, Tree_Beech, Tree_Birch, Tree_Round_Apple, Tree_Maple` · bushes
`Bush_Big, Shrub_Round, Grass_Clumb, Grass_Tall` · flowers `Roses, Carnations, Flower_Red, Sunflower` ·
`Mushroom_Boletus` · animals `Deer, Butterfly, Pigeon, Dog`, farm fringe `Sheep_White, Cow, Hen`.

**Goldfields (Earth/grain) — sparse, grass is the hero:** trees (lone) `Tree_Round, Tree_Lime, Tree_Poplar,
Tree_Oak` · grass `Grass_Long, Grass_Tall, Grass_Basic` (dense) · golden accents `Wheat_Plant, Cotton,
Sunflower` · light rock `Stone_Flat, Rocks_Tiny` · herds `Deer, Sheep_White, Cow, Hen, Butterfly`.

**Stoneback (Stone) — rocks are the hero:** `Rock_Large, Rock_Sharp, Rock_Pillar, Rock_Terrasse,
Stone_Big_Tall, Stone_Pointy, Stone_Large` + `Stones_Small` scatter · hardy trees `Tree_Conifer, Tree_Fir,
Tree_Spruce` · `Shrub, Bush_Small`; dry edge `Aloe_Vera, Cactus_Basic` · mineral `Crystals, Gem` near clusters
(mining tie-in) · animals `Bear_Brown, Wolf, Snake, Scorpion`; `Bone_Pile_Prehistoric` dressing.

**Mirewood (Water/mire) — dense, fern understory hero:** trees `Tree_Forest, Tree_Tall, Tree_Beech` + dead
intermix `Tree_Bare, Tree_Dry` · understory `Fern_Prehistoric, Horsetail_Prehistoric(_Hight), Bush_Prehistoric,
Bush_Cycad_Prehistoric, Cycad_Triple_Prehistoric` · water `Lotus, Lotus_Leaf` · mushrooms (heavy)
`Mushroom_Toadstool(_Green), Mushroom_Boletus`, `Flower_Poisonous` · ground `Log, Logs, Stump, Tree_Dead_Log_A/B`
· animals `Frog, Snake, Rat, Spider, Crab`.

**Ashwood (Fire/ash) — all dead, hero:** `Tree_Dead, Tree_Dead_Broken, Tree_Dead_Torn_A/B, Tree_Bare, Tree_Old,
Tree_Spruce_Broken, Tree_Birch_Broken, Tree_Torn_A/B/C` · debris `Tree_Debris_A..H, Tree_Birch_Debris_A..H,
Tree_Dead_Log_A/B, Stump_Torn` · ground `Rock_Sharp, Stone_Pointy, Rubble_Stone` · macabre (sparse) `Skull_Human,
Gravestone(_Round), Bone_Pile_Prehistoric` · animals minimal — lone `Wolf, Rat, Spider`, mostly empty.

## Density / clustering
| Zone | Trees | Pattern | Ground | Animals |
|---|---|---|---|---|
| Heart | med-high | soft groves radiating from the tree, thinning out | heavy grass+flowers | small/friendly, 4–6 herds |
| Goldfields | **sparse** (lone, ~1/20–30m) | scattered solitary, NO groves | **grass hero**, light rock | herds 4–8 |
| Stoneback | sparse | **rock clusters** (3–6), trees at edges | rock hero | lone predators |
| Mirewood | **dense** | tight canopy + fern thickets, claustrophobic | **very heavy** understory | small marsh critters, no herds |
| Ashwood | med, **all dead** | irregular skeletal stands + debris fields | debris+ash, minimal | **sparse/empty**, 1–2 max |
Jitter rotation + scale 0.8–1.3× per instance. Keep clear radius around camp anchors (`DefaultCampRadius=9`)
and the village footprint (~±42 X / ±33 Z). Seeded/procedural (owner: "script to random seed from script").

## ⛔ RETIRED 2026-08-17 — Tints = the elemental realization (the only real gaps are COLOR, not assets)

> **Do not follow this section.** Superseded by WO-1044 §1 (owner-approved 2026-08-17): the marches are
> separated by **value, texture and light**, never hue. The live per-march ground contract is
> `DeNelle.Core.World.TerrainLayerSet` (measured Rec.709 luminance targets, asserted by
> `TerrainLayerRegression`). Kept below only so a future reader can see what was weighed.

- **Ashwood (fire):** darken/desaturate + char the dead-tree set → grey-black ash (same prefabs serve Mirewood
  brown-rot vs Ashwood grey-ash via tint only).
- **Goldfields (grain):** warm/golden tint on `Grass_Long/Tall` (+ literal `Wheat_Plant/Cotton`).
- **Mirewood (water):** green/moss tint on stones + the standard grass; damp read.
- Per-biome material color override is cheaper + cleaner than new assets.

## Perf (mobile-first / WebGL)
All Nature_M is low-poly, one atlas → GPU-instance / static-batch everything; one shared material. Cap dense
grass (`Grass_Long/Tall`) instance counts per cell + distance-fade (alpha overdraw is the only WebGL risk).
Keep `Rain`/`Clouds_M` particles OUT of the scatter pass. Animals = ambient dressing (idle/wander, no AI rig).

*Cross-ref:* WO-449 (world layout), `CampSystem.cs` (anchors/tiers), `ZoneManager` (zone hook),
`ExteriorTerrainBuilder.cs` (terrain), the polyperfect Nature_M pack.
