# Polyperfect Low Poly Ultimate Pack — Asset Catalog
# Defenders of the Realm

**Pack location:** `Assets/polyperfect/Low Poly Ultimate Pack/`
**Pack size:** ~246 MB (entire pack — gitignored, re-import on clone)
**Loadable unit:** the **`.prefab`** in `_M/Prefabs_M/<Category>_M/<Name>.prefab`
(reference these, not the raw mesh; names are bare `<Name>`, NOT `SM_<Name>`).
**Unity version verified:** 6000.4.8f1 · URP compatible ✓
**Last cataloged / verified from disk:** 2026-06-13

> **Why this pack:** Replaces the heavy Tripo village meshes (Cathedral 84 MB,
> PetHome 54 MB, LumberMill 52 MB, etc.) with low-poly equivalents. The entire
> pack weighs less than the Cathedral alone. Mesh-only swap — no code changes.

> **⚠ Loadability:** polyperfect is **gitignored AND outside `Assets/Resources`**,
> so `Resources.Load` / `VisualFactory.Skin(path)` **cannot reach these prefabs**.
> To use one at runtime it must be **mirrored into `Assets/Resources/Structures/`**
> first (that is exactly what the Resources/Structures set in `docs/MODEL_CATALOG.md`
> §2 is). In-editor builders (VillageSceneBuilder etc.) reference them by GUID directly.

---

## Path conventions & verified inventory (2026-06-13)

| Quality tier | Prefab folder |
|---|---|
| `_M` Standard (use this — CLAUDE.md §4) | `_M/Prefabs_M/<Category>_M/<Name>.prefab` |
| `_T` Tribal (owner's tower/outpost art) | `_T/Prefabs_T/Tribal_T/<Name>.prefab` |
| `_H` / `_L` quality tiers | parallel `Prefabs_H` / `Prefabs_L` (rarely used here) |

**`_M/Prefabs_M` total: 3,080 prefabs** across 41 category folders. Per-category
counts (verified from disk): Empire 291, Scifi 287, People 243, Buildings 237,
Apocalypse 202, Furniture 195, Nature 177, Props 115, Tiles 108, Vehicles 91,
Tiles_NB 85, **Fantasy 82**, Terrains 68, Electronics 66, Construction 58, Food 56,
Weapons 55, Tools 46, Music 42, **Medieval 41**, Egypt 38, Drinks 38, **Tribal 36**,
Landmarks 36, Japan 32, Dishes 32, Amusement_Park 29, Pirates 28, Beach 28,
**Animals 28**, Resources 25, Survival 24, Sport 23, Western 21, India 20, Farm 19,
WW2 18, Military 16, Zulu 14, Space 14, Racing 14, Roman 2.

All models share a single atlas texture — excellent for draw-call batching on Seeker.

---

## 1. Village Perimeter — Walls, Towers, Gates

### Stone wall system (modular, snaps at 3×3 m)
All in `_M/Meshes_M/` (no category subfolder for these):

| FBX name | Use |
|---|---|
| `Wall_Medieval_Stone` | Main perimeter wall segment |
| `Wall_Medieval_Wood` | Inner/secondary wooden palisade |
| `Wall_Stone_3x3_A/B/C` | Modular stone wall variants (A=plain, B=moss, C=battle-worn) |
| `Wall_Stone_Corner_A/B/C` | 90° corner pieces for each variant |
| `Wall_Stone_End_3x3m_A/B/C` | Wall end cap (left + right variants exist) |
| `Wall_Stone_Window_3x3m_A/B/C` | Wall with arrow-slit window |
| `Wall_Stone_Door_3x3m_A/B/C` | Wall with doorway |
| `Wall_Wood_Horizontal_3x3m` | Wooden wall segment (horizontal planks) |
| `Wall_Wood_Vertical_3x3m` | Wooden wall segment (vertical planks) |
| `Wall_Wood_Horizontal_Corner` | Wooden corner |
| `Wall_Wood_Vertical_Corner` | Wooden corner (vertical) |
| `Wall_Wood_Horizontal_End_3m` | Wooden end cap |
| `Wall_Wood_Door_Horizontal_3x3m` | Wooden wall with door |
| `Wall_Wood_Window_Horizontal_3x3m` | Wooden wall with window |
| `Fence_Stone` | Low stone fence for inner districts |
| `Fence_Stone_Gate` | Stone fence with gate |
| `Fence_Stone_Tower` | Small fence tower (corner watchtower) |
| `Fence_Stone_Metal` | Stone + metal railing fence |

### Towers — Medieval_M (corner + watchtower)

These 4 are the ones **mirrored into `Resources/Structures`** (see MODEL_CATALOG §2).

| Prefab name (Medieval_M) | Use |
|---|---|
| `Tower_Castle_Round` | Round crenellated tower — **archer tower base+L2** (BUG#22 fix) |
| `Tower_Castle_Square` | Square keep — ArcherTower/FrostTower `.asset` L2 art |
| `Tower_Medieval_Big` | Large standalone tower — wizard / arcane tower, archer L3 |
| `Tower_Medieval_Wood` | Small wooden watchtower — **the old BUG#22 lumber-pile look; do NOT re-use for archer** |

### Tribal towers — `_T/Prefabs_T/Tribal_T/` (owner's preferred tower/outpost ladder)

The owner authors towers/outposts from the **Tribal_T** set. Full Tribal_T prefab
list (37, verified 2026-06-13): tower ladder `Tower_Tribal_Tier1..Tier4`; huts
`Hut_Tribal_Tier1..Tier4`; bridges `Bridge_Tribal_Tier1..Tier4`; `Gate_Tribal`,
`Ramp_Tribal`, `Furnace_Tribal`, `Flag_Stand_Tribal`, `Torch_Standing_Tribal`,
`Table_Stone_Tribal`, `Cookingpot_Tribal`, `Vase_Tribal_Big/Small`,
`Totem_Dinosaur_Tribal`, `Trex`, `Spine_Trex`, `Skull_Triceratops`,
`Sm_Bone_Pile_Prehistoric`, `Meat_Haunch_A/B`, `Egg_Fried`; weapons
`Axe_Tribal`, `Club_Tribal`, `Spear_Tribal`, `Hammer_Tribal`, `Pickaxe_Tribal`,
`Flint_Tribal`, `Shaman_Staff_Tribal`.

> **Tower_Tribal ladder** (`Tier1→Tier4`) is the intended Archer-Tower upgrade art
> per the `_bug22` note in `structures-catalog.json`. **Not yet mirrored into
> Resources** → not runtime-loadable. To adopt: copy `Tower_Tribal_Tier1..Tier3`
> into `Assets/Resources/Structures/` (Tier4 exceeds the maxLevel=3 cap) and
> repoint `tower_ground_archer.visualPrefabPath` / `upgradeVisualPath`.

> NOTE: a near-identical Tribal set also exists in `_M/Prefabs_M/Tribal_M/` (36
> prefabs, same tower-tier names) — use the `_T` set the owner standardized on.

### Gates (main entrance + secondary)

| FBX name | Use |
|---|---|
| `Gate_Medieval_Medium` | Main south gate |
| `Gate_Medieval_Small` | Side gates (east/west/north) |
| `Drawbridge_Medieval` | Drawbridge over moat at main gate |
| `Bridge_Medieval_Stone` | Stone bridge approach road |
| `Stairs_Medieval_Stone` | Wall rampart access stairs |

---

## 2. Village Buildings — Gameplay Structures

Each maps to one of the five gameplay buildings in `VillageSceneBuilder`.

| Building (code) | Polyperfect FBX | Status | Notes |
|---|---|---|---|
| `gate_stone` | `Gate_Medieval_Medium` | ✅ Locked | In Resources |
| `mill` (Farm) | `Farm_House` + `Windmill_Medieval` | ✅ Locked | In Resources |
| `tower_ground_archer` L1 | `Tower_Medieval_Wood` | ✅ Locked | In Resources |
| `tower_ground_archer` L2 | `Tower_Castle_Round` | ✅ Locked | Upgrade swap |
| `wall_wood` | `Wall_Medieval_Wood` | ✅ Locked | In Resources |
| `wall_stone` | `Wall_Medieval_Stone` | ✅ Locked | In Resources |
| `pet-house` (Echo Hollow) | `Stables_Medieval` | 🔧 Repoint | copy→Resources/Structures |
| `workshop` | `House_Medieval_Medium` | 🔧 Repoint | copy→Resources/Structures |
| `market` | `House_Medieval_Large` + `Marketplace_Stand_Simple` (front props) | 🔧 Repoint | House = structure; stands = stalls |
| `lumbermill` (Sawmill) | `Watermill_Medieval` | 🔧 Repoint | Better fit than House_Medieval_Small |
| `forge` (Armorer) | `House_Medieval_Medium` | 🔧 Repoint | No blacksmith mesh in pack — dress with Tools_M props |
| `arcane-tower` | `Tower_Medieval_Big` | 🔧 Repoint | Already in Resources (mage L2) |
| `mine_crystal` | `Well` + `House_Medieval_Small` | 🔧 Repoint | Well = shaft landmark; house = surface hut |
| `deco_torch` | `Torche_Wall` | 🔧 Repoint | copy→Resources/Structures |
| `tower_wall_wizard` L1 | PatriciaLight/tower2 (DTT) | ⚠️ Keep | Imposing apex — leave on DTT tower |
| `tower_catapult` | `Catapult` (Medieval_M) | ⚠️ Pick → **locked** | Direct match |
| `tower_siege_tower` | `Ballista` (Medieval_M) | ⚠️ Pick → **locked** | Distinct silhouette from catapult |
| `wall_corner` | rotate `Wall_Medieval_Stone` | ❌ No corner mesh | Rotate-a-straight until better pack acquired |
| Dungeon entrance | `Castle_Medieval` (arch only) or `Gate_Medieval_Medium` | — | Portal framing |
| Tavern / Inn | `House_Medieval_Big` | — | Largest house variant |

### Prop dressing per building — lightweight (≤4 props each)

**`mine_crystal` — Well + House_Medieval_Small:**
`Stone_Big` ×2, `Rocks_Small` scatter — Well shape sells the shaft, keep bare

**`workshop` — House_Medieval_Medium:**
`Anvil` (Tools_M), `Torche_Wall` ×2 — Anvil is the identifier, nothing else needed

**`forge` (Armorer) — House_Medieval_Medium:**
`Anvil` (Tools_M), `Hammer` (Tools_M), `Torche_Wall` ×2 — same base as Workshop, different tool combo distinguishes them

**`market` — House_Medieval_Large + Marketplace_Stand_Simple:**
`Marketplace_Stand_Simple` ×2 (front), `Torche_Wall` ×1 — stands do the visual work

**`lumbermill` (Sawmill) — Watermill_Medieval:**
`Timber` ×2, `Tree_Dead_Log_A` ×1 — wheel shape sells it, logs confirm the function

**`pet-house` (Echo Hollow) — Stables_Medieval:**
`Hay_Pile` (Farm_M), `Fence_Stone` surround, `Torche_Wall` ×2 — warm and enclosed

**Farm — Farm_House + Windmill_Medieval:**
`Farm_Silo`, `Haystack` (Farm_M), `Scarecrow` (Farm_M), `Fence_Picket` — windmill is the hero, props are light scatter

**`arcane-tower` — Tower_Medieval_Big:**
`Torche_Wall` ×4, `Statue_Knight` (Fantasy_M), `Scroll` (Fantasy_M) — tall tower sells itself; keep props to 3

**Heart of Elarion (central):**
`Altar` (Fantasy_M), `Candlestick` ×4, `Statue_Knight` flanking, `Pillar_Ionic` (Empire) ×4

---

## 3. Ground & Terrain

| FBX name | Use |
|---|---|
| `Terrain_Plane_Plain` | Flat interior village floor |
| `Terrain_Plane_Hill1–4` | Gentle hills outside walls |
| `Terrain_Plane_Slope1–4` | Slope transitions at wall base |
| `Terrain_Plane_Valley1–4` | Low ground (moat area, approach roads) |
| `Terrain_Plane_Lake` | Moat / water feature at main gate |
| `Floor_Stone_3x3m_A` | Plaza/courtyard stone paving (near Heart) |
| `Floor_Plain_3x3m` | Interior building floors |
| `Ground_Cracked_Dirt` | Worn approach paths, spawn areas |
| `Stone_Brick` (Medieval_M) | Path material / cobblestone road |
| `Hexagon_Land` (Terrains_M) | Hex tile approach if hexagonal layout desired |

---

## 4. Nature & Environment Dressing

### Trees (pick 3–4 for visual consistency)

| FBX name | Vibe |
|---|---|
| `Tree_Oak` | Default forest canopy — warm |
| `Tree_Oak_Orange` | Autumn variant — seasonal flavor |
| `Tree_Conifer` | Dark/northern edge of the village |
| `Tree_Birch` | Light elegant trees near the Heart |
| `Tree_Dead` | Outside walls, corrupted lands approach |
| `Tree_Dead_Log_A/B` | Fallen log scatter on approach roads |
| `Tree_Fir_Snow` | Winter/ice biome (FrostGolem dungeon area) |

### Rocks & stones

| FBX name | Use |
|---|---|
| `Rock_Large` | Boulders at wall base, terrain breaks |
| `Rock_Pillar` | Standing stones near the Heart altar |
| `Rock_Sharp` | Jagged rocks on enemy approach routes |
| `Stone_Big` | Mid-size scatter stones |
| `Stone_Round` | Path edge dressing |
| `Rocks_Small` | Ground scatter (×many) |

### Atmosphere

| FBX name | Use |
|---|---|
| `Torche` / `Torche_Wall` (Fantasy_M) | Primary torch lighting throughout |
| `Fire` (Survival_M) | Campfire at guard posts |
| `Fireplace` (Survival_M) | Interior warmth in buildings |
| `Candle_Big` / `Candlestick` (Fantasy_M) | Interior candle dressing |
| `Fountain` (Props_M) | Village square centerpiece |
| `Well` (Medieval_M) | Water source near Farm/Mine |

---

## 5. Dungeon Kit (Fantasy_M) — all scenes

The full dungeon kit lives in `Fantasy_M` and the standalone `Dungeon_*` prefixed FBXs.

### Walls & floors
`Dungeon_Wall_Stone`, `Dungeon_Wall_Dirt`, `Dungeon_Wall_Prison`,
`Dungeon_Wall_Window_Stone`, `Dungeon_Floor_Stone`, `Dungeon_Floor_Dirt`,
`Dungeon_Floor_Wood`, `Dungeon_Floor_Hole`

### Pillars & stairs
`Dungeon_Pillar_Stone_Round`, `Dungeon_Pillar_Stone_Round_Tall`,
`Dungeon_Pillar_Stone_Square`, `Dungeon_Pillar_Stone_Corner`,
`Dungeon_Stairs_Stone`, `Dungeon_Stairs_Stone_Carpet`

### Doors
`Dungeon_Door_Stone`, `Dungeon_Door_Prison`, `Door_Wood`, `Door_Prison`

### Interior dressing
`Coffin_Wood`, `Skull_Human`, `Skull_Human_Candle`, `Torture_Cage`,
`Library_Books`, `Library_Empty`, `Table_Wood`, `Table_Broken_Wood`,
`Chair_Wood`, `Chest`, `Crate_Box`, `Jar_Big`, `Jar_Medium`,
`Carpet`, `Goblet`, `Scroll`, `Map`, `Book_Closed`, `Book_Open`,
`Books_Pile`, `Potion`, `Potion_Globe`, `Rubble_Stone`

---

## 6. Siege Weapons (Defend the Tower arena)

| FBX name | Use |
|---|---|
| `Catapult` (Medieval_M) | Arena defensive weapon prop |
| `Ballista` (Medieval_M) | Arena wall-mounted weapon prop |
| `Cannon` (Medieval_M) | Heavy siege cannon prop |
| `Cannonball` / `Cannonballs` | Scattered ammo props near cannons |
| `Stakes` (Medieval_M) | Anti-infantry stakes in arena approach |
| `Flag_Medieval` / `Flag_Medieval_Big` | Arena banners |
| `Carriage` (Medieval_M) | Blockade / cover prop |

---

## 7. NPCs & People (People_M)

Relevant medieval/fantasy characters already in the pack:

| FBX name | Suggested NPC role |
|---|---|
| `Man_Knight` | Village guard, Heart defender |
| `Man_Knight_Soldier` | Patrol soldier |
| `Man_Monk` / `Man_Monk_Old` | Quest-giver, Elder NPC |
| `Man_Lord` | Town leader NPC |
| `Man_Farm` | Farmer villager |
| `Woman_Farm` | Farmer villager |
| `Man_Sir` | Merchant |
| `Man_Servant` | Innkeeper |
| `Skeleton` / `Skeleton_Soldier` | Undead enemy variant (complements KayKit skeletons) |

---

## 8. Animals (Animals_M) — 28 prefabs (verified names 2026-06-13)

Full list: `Bear_Brown`, `Bear_Polar`, `Butterfly`, `Chick`, `Cow`, `Crab`,
`Deer`, `Dog`, `Elephant`, `Fish`, `Fish_Swarm`, `Frog`, `Hen`, `Horse`,
`Penguin`, `Pig`, `Pigeon`, `Rat`, `Scorpion`, `Seagull`, `Shark`, `Sheep_White`,
`Snake`, `Spider`, `Starfish`, `Stegosaurus`, `T_Rex`, `Wolf`.

| Prefab name | Use |
|---|---|
| `Wolf` | Feral Wolf enemy visual candidate (NOTE: no beast rig wired yet — `feral-wolf` currently reuses `Skeleton_Rogue`) |
| `Horse` | Village stable dressing |
| `Cow` / `Hen` / `Pig` / `Sheep_White` | Farm life dressing |
| `Dog` | Village companion |
| `Deer` | Forest/exterior ambient |
| `Bear_Brown` / `Bear_Polar` | Forest hazard / enemy candidate (was listed as `Bear` — wrong name) |
| `T_Rex` / `Stegosaurus` | Tribal/prehistoric outpost dressing (pairs with Tribal_T set) |

---

## 9. Off-theme (shelve)

Skip for Defenders: Amusement Park, Apocalypse (blood/gore), Beach, Sci-fi,
Space, WW2, Military (guns), Drinks, Electronics, Racing, Landmarks
(modern), Restaurant, Egypt (unless future realm), India, Japan (unless
future realm), Roman/Empire.

---

## File size comparison

| Asset set | Size | Notes |
|---|---|---|
| Tripo Cathedral alone | 84 MB | Single building |
| Tripo PetHome | 54 MB | Single building |
| Tripo LumberMill | 52 MB | Single building |
| Tripo pets (3×) | ~280 MB | 3 pet meshes |
| **Polyperfect entire pack** | **246 MB** | 3,000+ models, all atlas-textured |
| Polyperfect village subset | ~15–20 MB estimated | ~60 specific FBXs used |

**Estimated build size reduction after swap: 400–500 MB.**
