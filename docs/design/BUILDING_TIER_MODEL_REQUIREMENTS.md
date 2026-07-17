# Building Tier Model Requirements — the owner's shopping list

**Status:** SME survey (read-only, 2026-07-16). Sourced from the actual disk + catalog, not
comments. Pairs with `docs/design/BUILDING_UPGRADE_TREES.md` (the 6-building, Tier 0→3 canon)
and `docs/design/BUILDING_PERKS_DESIGN.md` (effect mapping + WO phasing, Rev 2 section).

**What this answers:** for each of the 6 upgrade-tree buildings × Tier 0/1/2/3 (24 visual
states), does a distinct model already EXIST, can an existing asset be REUSED, or must one be
SOURCED (owner creates/buys)? Plus: how the model swaps in per tier (see the wiring doc §D2).

---

## TL;DR for the owner

- **6 base models already exist** (one per building) — Tier 0 is covered for all six. **Buy nothing for T0.**
- **The 18 upper-tier states (T1/T2/T3) have NO dedicated building models today.** That is the
  shopping list: **up to 18 new meshes** if you want a distinct model at every tier.
- **You do NOT need all 18.** Two levers already ship, free:
  1. **`StructureTierVisual`** auto-scales + gilds every tier (T1 bronze → T2 silver → T3 gold, +12%/+25% scale) with **zero new art** — so even an un-modeled tier reads as "grander." This is the graceful fallback.
  2. **Reuse existing 3-mesh ladders.** `ArcaneSpire_1/2/3` (3 distinct arcane meshes) and `Tower_Tribal_Tier1/2/3` already exist in `Resources/Structures/`. The Arcane-Tower building can adopt ArcaneSpire_1/2/3 for its T1/T2/T3 **at no cost.**
- **KayKit `Assets/Models/KayKit/medieval/` has NO level_1/2/3 building tiers** — it ships one mesh per building plus **color variants** (green/red/yellow/blue). Color variants can cheaply signal tier but don't read as "bigger/grander." polyperfect ships a **House Small→Medium→Large** size ladder usable as a generic 3-step grandeur bump.
- **Net realistic buy:** ~**10–14 hero meshes** for the tiers you want to feel bespoke (economy + arcane first), leaning on scale/tint + the ArcaneSpire reuse for the rest.

---

## Current base model per building (verified)

Each of the 6 buildings resolves to exactly ONE model today; none have tier variants. Hub
buildings are skinned by `HubStructureVisualInjector.Swaps` (`Assets/_Modules/Village/HubStructureVisualInjector.cs:60-79`); placeable rows live in `structures-catalog.json`.

| Building id | Canon name (spec) | Current base model (path) | Source | Notes |
|---|---|---|---|---|
| `lumbermill` | Lumber Mill | `Assets/Resources/Structures/lumbermill.fbx` | Tripo | Hub swap `Lumbermill_Wood_Storefront`. (polyperfect `Watermill_Medieval` is the placeable-collector alt.) |
| `windmill` | Windmill | `Assets/Resources/Structures/farm.fbx` | Tripo | Hub swap `Windmill_Food_Storefront` currently uses **farm.fbx**, not a windmill. `Windmill_Medieval.prefab` + KayKit `windmill.gltf`/`watermill.gltf` also on disk. |
| `forge` | Forge (Magic/Essence) | `Assets/Resources/Structures/Forge.fbx` | Tripo | Hub swap `Blacksmith_Weapons_Storefront` → Forge.fbx. |
| `armorer` | Armorer (Blacksmith) | `Assets/Resources/Structures/armorer.fbx` | Tripo | Hub swap `Forge_Armor_Storefront` → armorer.fbx. `House_Medieval_Medium` + KayKit `blacksmith.gltf` alts. |
| `barracks` | Barracks | `Assets/Resources/Structures/barracks.fbx` | Tripo | Hub swap `CastleBarracks` (unlock-gated). KayKit `barracks.gltf` alt. |
| `arcane-tower` | Arcane Tower | `Assets/Resources/Structures/arcane tower.fbx` (+ `ArcaneTower_Albedo` tex) | Tripo | Hub swap `ArcaneTower_MagicUpgrades`. **3-mesh `ArcaneSpire_1/2/3.fbx` ladder already exists** and is a strong tier-reuse candidate. |

---

## The 24-state model table (shop from this)

Legend — **EXISTS**: distinct model on disk. **REUSE**: an existing asset can serve (name it).
**SCALE/TINT**: covered "for free" by `StructureTierVisual` (no mesh needed). **NEEDS MODEL**:
owner sources a new mesh for a bespoke read.

### 1. Lumbermill (Wood)
| Tier | Tier name | Asset status | Existing path / plan |
|---|---|---|---|
| T0 | Basic Lumber Camp | **EXISTS** | `Resources/Structures/lumbermill.fbx` |
| T1 | Sawmill | NEEDS MODEL (or SCALE/TINT) | reuse base + auto bronze/scale; buy a sawmill mesh for bespoke |
| T2 | Timber Hall | NEEDS MODEL (or SCALE/TINT) | buy a larger timber-hall mesh; else silver/scale |
| T3 | Ancient Grove Mill | NEEDS MODEL (or SCALE/TINT) | buy a grand "ancient grove" mesh; else gold/scale |

### 2. Windmill (Food)
| Tier | Tier name | Asset status | Existing path / plan |
|---|---|---|---|
| T0 | Simple Windmill | **EXISTS** (but is currently `farm.fbx`) | `Resources/Structures/farm.fbx`; consider swapping to `Windmill_Medieval.prefab` / KayKit `windmill.gltf` for a truer T0 |
| T1 | Harvest Windmill | REUSE candidate | `Windmill_Medieval.prefab` or KayKit `windmill.gltf` as a distinct step from farm; else SCALE/TINT |
| T2 | Grand Mill | NEEDS MODEL (or SCALE/TINT) | buy a grand mill; else silver/scale |
| T3 | Eternal Winds | NEEDS MODEL (or SCALE/TINT) | buy an ornate wind-temple mesh; else gold/scale |

### 3. Forge (Essence / Magic Tech)
| Tier | Tier name | Asset status | Existing path / plan |
|---|---|---|---|
| T0 | Basic Forge | **EXISTS** | `Resources/Structures/Forge.fbx` |
| T1 | Arcane Forge | NEEDS MODEL (or SCALE/TINT) | buy an arcane-glow forge; else bronze/scale |
| T2 | Rune Crucible | NEEDS MODEL (or SCALE/TINT) | buy a rune-crucible mesh; else silver/scale |
| T3 | Elarion Eternal Forge | NEEDS MODEL (or SCALE/TINT) | buy a monumental forge; else gold/scale |

### 4. Armorer (Metal / Defense & Gear)
| Tier | Tier name | Asset status | Existing path / plan |
|---|---|---|---|
| T0 | Makeshift Armory | **EXISTS** | `Resources/Structures/armorer.fbx` |
| T1 | Field Armorer | REUSE candidate | KayKit `blacksmith.gltf` or `House_Medieval_Medium` as a distinct step; else SCALE/TINT |
| T2 | Master Smithy | NEEDS MODEL (or SCALE/TINT) | buy a master smithy; else silver/scale |
| T3 | Legendary Armory | NEEDS MODEL (or SCALE/TINT) | buy a legendary armory; else gold/scale |

### 5. Barracks (Companions / Military)
| Tier | Tier name | Asset status | Existing path / plan |
|---|---|---|---|
| T0 | Training Grounds | **EXISTS** | `Resources/Structures/barracks.fbx` (also KayKit `barracks.gltf`) |
| T1 | Warrior Barracks | NEEDS MODEL (or SCALE/TINT) | buy a fortified barracks; else bronze/scale |
| T2 | Veteran Hall | NEEDS MODEL (or SCALE/TINT) | buy a veteran hall; else silver/scale |
| T3 | Elite Legion Hall | NEEDS MODEL (or SCALE/TINT) | buy a legion hall; else gold/scale |

### 6. Arcane-Tower (Specialized Magic Defense)
| Tier | Tier name | Asset status | Existing path / plan |
|---|---|---|---|
| T0 | Basic Arcane Spire | **EXISTS** | `Resources/Structures/arcane tower.fbx` (or `ArcaneSpire_1.fbx`) |
| T1 | Enchanted Spire | **REUSE — EXISTS** | `Resources/Structures/ArcaneSpire_2.fbx` |
| T2 | Mystic Obelisk | **REUSE — EXISTS** | `Resources/Structures/ArcaneSpire_3.fbx` |
| T3 | Elarion Arcane Nexus | NEEDS MODEL (or SCALE/TINT) | buy the campaign-centerpiece nexus; ArcaneSpire ladder tops out at 3, so T3 is the one genuine buy here |

---

## Scorecard

- **EXISTS (buy nothing): 6** — every T0, + Arcane-Tower T1/T2 (ArcaneSpire_2/3). **8 states covered outright** if the ArcaneSpire reuse is adopted.
- **REUSE candidates (repoint existing pack asset, no purchase): 3** — Windmill T1, Armorer T1, Arcane-Tower T0 alt.
- **NEEDS MODEL for a bespoke read: 13–16** — but ALL of these degrade gracefully to `StructureTierVisual` scale+tint, so none are blocking; they're "grandeur polish" buys.

### What NOT to buy (already on disk)
- **Any Arcane-Tower T1/T2 mesh** — `ArcaneSpire_2.fbx` / `ArcaneSpire_3.fbx` exist.
- **A generic "bigger building" for a tier bump** — `StructureTierVisual` already scales +12%/+25% and gilds bronze→silver→gold for free, and polyperfect has a `House_Medieval` Small→Medium→Large ladder.
- **A "windmill" mesh** — `Windmill_Medieval.prefab` + KayKit `windmill.gltf`/`watermill.gltf` are on disk (windmill hub currently mis-uses `farm.fbx`).
- **A blacksmith mesh for Armorer** — KayKit `blacksmith.gltf` + `House_Medieval_Medium` on disk.

---

## Recommended buy order (value-first)
1. **Arcane-Tower T3 "Elarion Arcane Nexus"** — the campaign centerpiece; the one arcane tier not already covered by the ArcaneSpire ladder.
2. **Lumbermill + Windmill T1–T3** (the early economy the player sees first — highest felt-progression payoff).
3. **Forge T1–T3** (mid/late magic-tech grandeur).
4. **Armorer + Barracks T2–T3** (defense/military; T1 covered by pack reuse).

Everything not bought falls back to scale+tint automatically — so the felt "buildings grow grander"
ships even before a single new mesh lands, and each purchased mesh is a drop-in upgrade (see the
model-swap wiring below).
