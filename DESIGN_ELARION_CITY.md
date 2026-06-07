# DESIGN — Elarion City Layout & Persistent Populate Manifest

**Status:** CANONICAL (owner-driven, 2026-05-31). The city is **empty repeatedly** — this doc + WO-189 fix it durably.
**Read with:** `DESIGN_CORE_LOOP_AND_STRUCTURE.md` (the loop) · `docs/polyperfect-asset-catalog.md` (real prefabs).

---

## 0. WHY THE CITY KEEPS GOING EMPTY (root cause — read first)

`VillageSceneBuilder` regenerates the village on every batchmode **rebake**. Anything not encoded as
**data the builder reads** is wiped on the next bake. Hand-placed buildings/props never survive →
the city reverts to bare grass + a couple procedural structures, over and over.

**The catalog already maps every building to a real prefab** (`docs/polyperfect-asset-catalog.md` §2).
The assets exist. The builder simply isn't instructed to place most of them.

**THE FIX (non-negotiable):** a **City Manifest** — a data list of every building + prop with a
position/rotation — that `VillageSceneBuilder` consumes on **every** build. Editing the city = editing
the manifest. A rebake then *reproduces* the city exactly. It cannot regress. (Ties WO-148 catalog
factory, WO-149 base persistence, WO-152 city IA.) → implemented by **WO-189**.

---

## 1. Vibe & space division

**Tight medieval timber keep-town** inside the curtain wall — dense, lived-in, cobbled paths, market
stalls, banners. *(Assumption from the timber-framed assets already in scene; flip to spread-settlement
if preferred.)*

**Ground vs wall-top (owner 2026-05-31):** the **ground is the lived-in city** (production + houses +
market + decoration). **Defenses live on WIDE walkable ramparts up top**, not cluttering the ground.
Rampart must be wide enough to walk the **entire perimeter** and place defenses (→ WO-181 updated).

---

## 2. Layout — radial from the Heart (0,0,0)

- **Center — Heart of Elarion (the upgradeable seat / Town Hall).** Stone plaza (`Floor_Stone_3x3m_A`),
  `Altar` + `Candlestick`×4 + `Statue_Knight` flanking + `Pillar_Ionic`×4 (catalog §2 Heart set).
- **Inner ring — civic/production:** Barracks (trains squads), Forge, Arcane Library/Tower, Lumbermill,
  Granary/Farm, Pet House. These feed the loop, so they sit closest to the seat.
- **Mid ring — market + homes:** market stalls, well, houses/cottages for population density.
- **Outer ring (against walls):** stables, storage, watch elements. Defenses go on the **rampart above**, not here.
- **Four solid-bridge entries (N/S/E/W)** (WO-188). A **cobbled main road** (`Stone_Brick`) runs from
  each bridge to the Heart → forms a cross/axis that organizes the districts.
- **Negative space filled** with trees (`Tree_Oak`/`Tree_Birch` near Heart), planters, fences, banners, torches.

---

## 3. Building roster — grounded in real prefabs (target ~28–36 placements)

| Building | Loop purpose | Prefab (polyperfect _M) | Zone | Count |
|---|---|---|---|---|
| Heart of Elarion | the seat / upgrade sink | Heart set (Altar + pillars + statues) | center | 1 |
| Barracks | train squads (food-capped army) | `House_Medieval_Big` + weapon-rack props | inner | 1 |
| Forge | gear/upgrades | `House_Medieval_Medium` + `Anvil`/`Hammer` | inner | 1 |
| Arcane Library/Tower | talents/spells | `Tower_Medieval_Big` + `Book_Open`/`Scroll` | inner | 1 |
| Lumbermill | wood income | `House_Medieval_Medium` + `Timber`/`Axe` | inner | 1 |
| Granary + Farm | **food → army cap** | `Farm_House` + `Windmill_Medieval` + `Farm_Silo`/`Haystack` | inner | 1–2 |
| Pet House | companion | `Stables_Medieval` + `Hay_Pile`/`Fence_Stone` | inner | 1 |
| Crystal Mine | crystal income | `House_Medieval_Small` + `Well` | mid | 1 |
| Market / Shop | store, social | `House_Medieval_Large` + `Marketplace_Stand_Simple`×3 | mid | 1 + stalls |
| Tavern / Inn | NPC hub, bonds | `House_Medieval_Big` | mid | 1 |
| Houses / Cottages | **population density** | `House_Medieval_Small/Medium` mix | mid–outer | 8–12 |
| Stables / Storage | flavor | `Stables_Medieval`, crates | outer | 2–3 |
| Corner towers | rampart anchors | `Tower_Castle_Round`/`Square` | wall corners | 4 |

### Tower meshes (owner 2026-05-31 — simpler archer tower)
The current ornate tower is polyperfect `Tower_Medieval_Big` — too busy. **Use KayKit Medieval Hexagon
Pack** (`KayKit Medieval Hexagon Pack 1.0.1/Assets/fbx(unity)/`) for clean low-poly towers:
- **Archer / defense tower → `building_watchtower`** (open platform, simplest, "archers shoot from here"). Fallback `building_tower_A`.
- Weaponized variants → `building_tower_cannon` / `building_tower_catapult` (for rampart siege defenses, WO-181).
- **Heart-seat candidates → `building_townhall` or `building_shrine`/`building_church`** (sanctuary fits "the core you defend"). Decide w/ seat naming.

## 4. Prop / decoration pass (the "lived-in" layer — what's been missing)

Cobbled roads (`Stone_Brick`), market stalls + awnings, `Well`, `Barrel`/`Crate_Box`/`Jar`, `Wheelbarrow`,
`Cart`/`Wagon`, `Haystack`/`Hay_Pile`, `Fence_Picket`/`Fence_Stone`, **banners**, `Torche_Wall` lining
roads, `Farm_Flower_Bed`/planters, `Bench_Wood`, `Statue_Knight`, trees. **Density target: it should feel
like a town you live in, not 3 buildings on a lawn.**

## 5. NPC / lived-in layer

Townsfolk + Wardens at buildings (ties existing daily quests "Tend a building", "Bond Rank"), ambient
NPCs walking the roads (WO-116 barks). Animals (`Cow`/`Hen`) at the farm/stables.

## 6. Persistence requirement (the whole point)

The manifest is the **single source** `VillageSceneBuilder` reads. No hand-placement that a rebake wipes.
Adding/moving anything = editing the manifest. This is what stops the empty-city regression for good.
→ **WO-189** builds the manifest + makes the builder consume it + places the full roster above.
