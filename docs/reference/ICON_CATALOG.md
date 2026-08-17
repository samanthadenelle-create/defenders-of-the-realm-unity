# ICON CATALOG — the single icon registry

**Status:** durable canonical registry (per the `audit-outputs-as-known-dictionaries` rule — this is a
dictionary of source-cited facts, not a one-off report)
**Scanned:** 2026-08-16 · **Branch:** `wip/village2-and-f8-tickets` · **Method:** read-only, enumerated
from the working tree. No Unity run, no gate, no commit.
**Supersedes nothing.** It EXTENDS two existing registries and defers to both on their own ground:

| Existing doc | Its ground | Where it beats this file |
|---|---|---|
| [`docs/ITEM_ICON_AND_RESOURCE_ASSET_MAP.md`](../ITEM_ICON_AND_RESOURCE_ASSET_MAP.md) | *catalog-id → sprite* for currency, materials, consumables, collector props | the 3D collector stack props + the owner's recorded picks |
| [`docs/reference/WEAPON_CATALOG.md`](WEAPON_CATALOG.md) | the 96-row runtime weapon set: mesh, obtainability, flavour | **the curation pipeline (§0.1) — it had the `Resources`-vs-`StreamingAssets` relationship right, and this file's §1.3 was corrected against it** |

This file is the *icon-asset → who-uses-it → what-class* registry, exhaustive over the icon folders in
§0.2. Where it and `WEAPON_CATALOG.md` overlap on weapons, that file is the authority.

Every row carries a citation (`file:line` or an asset path) so any single fact is re-verifiable at a
glance rather than re-derived.

---

## 0. Scope + boundary

### 0.1 Tag vocabulary

| Tag | Means | Evidence accepted |
|---|---|---|
| **Knight** | belongs to the knight class ladder | `"job": "knight"` in a gear catalog row; a `knight.*` concept/talent/ability key; `Talents/knight/**` |
| **Ranger** | ranger ladder | `"job": "ranger"`; `ranger.*` key; `Talents/ranger/**` |
| **Mage** | mage ladder | `"job": "mage"`; `mage.*` key; `Talents/wizard/**` (the mage tree's folder is named `wizard`) |
| **Shared** | deliberately class-agnostic | `"job": "any"`; a `shared.*` / `universal.*` key; a generic verb concept (`heal`, `dash`, …); UI chrome with no class |
| **Cleric** | *(a fifth job the taxonomy does not cover)* | `"job": "cleric"` — see §0.3 |
| **Unassigned** | source does not say | recorded as-is. **Not a guess placeholder — it is the honest answer.** |

**No tag was inferred from a filename looking archer-ish.** Where the only signal was the art's
subject matter, the row is `Unassigned`.

### 0.2 What is enumerated here (exhaustive)

| Folder | Image files | Enumerated |
|---|---|---|
| `Assets/Resources/ItemIcons/` | 484 PNG + 8 JPG sheets | ✅ every file, plus all 109 sliced sub-sprites |
| `Assets/Resources/RpgUi/` | 435 PNG across 20 roles | ✅ every file |
| `Assets/Resources/Talents/` | 83 PNG | ✅ every file |
| `Assets/Resources/HudIcons/` | 64 PNG/JPG | ✅ every file |
| `Assets/Resources/ProjectileIcons/` | 2 JPG sheets | ✅ both |
| **Total in scope** | **1 076 files** | |

### 0.3 What is deliberately OUT of scope (with counts, so the boundary is a number and not a shrug)

These are portrait / model-render / world art, not icons. Counted from
`Assets/Resources/**/*.png|jpg` on 2026-08-16:

`Arena` 14 · `Dungeon` 1 · `Dungeons` 1 · `Echoes` 6 · `Enemies` 37 · `Harvest` 4 · `Heroes` 33 ·
`NPCs` 12 · `PatriciaLight` 8 · `PetPortraits` 3 · `Pets` 9 · `Portraits` 36 · `Raids` 1 · `Signs` 12 ·
`Structures` 69 · `Textures` 2 · `Title` 2 · `UI` 1 · `VFX` 44 · `Walls` 12 — **307 files**.

Also out of scope: the gitignored source packs (`Assets/Blink/Art/Icons`, KayKit, polyperfect) — only
the mirrored copies under `Resources/` ship. See `docs/asset-inventory/README.md`.

**Cleric.** `weapons.json` carries a fifth job, `cleric`, on 2 live rows (`cleric_starter` :228,
`aegis_hallowed_censer` :296) and 25 library-only ones (`blink_mace1h_01..25`). The brief's four-tag
taxonomy has no slot for it. Those rows are tagged **Cleric** rather than forced into Shared, because
calling a censer "Shared" would be a wrong tag, and a wrong tag is worse than a missing one.

**Cleric is not a playable class**, so every Cleric-tagged icon in this registry is unreachable in
practice regardless of its wiring: the roster is Knight/Ranger/Mage and *"CLERIC STAYS OUT deliberately
… it has no authored kit"* (`Assets/_Modules/Core/FeatureFlags.cs:66`, pointing at
`DeNelle.Core.State.PlayableHeroes:20-26`). That covers `blink_mace1h_01..25`, `cleric_starter`,
`aegis_hallowed_censer` and the four `HudIcons/Healer/*` ability icons (§4.5).

---

## 1. How resolution actually works (the most useful part)

### 1.1 Gear: authored-first, then a keyword+rarity guess

`Assets/_Modules/Village/Hero/ItemIconCatalog.cs`

```
ForWeapon(w)   :58
  └─ LoadAuthoredIcon(w.iconPath)        :64   →  Resources.Load<Sprite>(iconPath)   :249-253
       ├─ non-null → RETURN IT (authoritative)  :66-69
       └─ null → EnsureLoaded()                 :71
            keyword match on (id + " " + name).ToLowerInvariant()   :72
            rarity → tier 1..5 (common..legendary)                  :73, :346-356
            dagger/knife/dirk        → mat_dagger | mat_dirk        :77-78
            bow/recurve/long/short   → bow_t<tier>                  :79-80
            wand/staff/scepter/rod   → **null on purpose** (no tiered staff sheet; caller draws ✦) :86-90
            sword/blade/axe/hammer…  → sword_t<tier>                :93-96
            else by job: ranger→bow_t, mage→null, cleric→null, default→sword_t  :99-105
ForArmor(a)    :109  — same shape: authored :113-118, then shield/helm/gauntlet/belt/chest keywords :124-161
ForConsumable  :165  — NO authored step; pure keyword → potion_*/mat_*            :171-197
ForMaterial    :209  — authored :212-217, then the row's authored CATEGORY → mat_* :220-241
                       (deliberately does NOT run the potion keyword mapper — F8-641)
```

**An `iconPath` on a catalog row is authoritative. Everything else is a guess.** The guess is a
*category* guess, never an identity guess: a legendary greatsword and a legendary mace both land on
`sword_t5`. That is why the ratio below is the health metric for this catalog.

The dispatcher that picks which `For*` runs is `Assets/_Modules/Village/Hero/GearIconCatalog.cs:41-43`
(`IconRoleWeapon` → `ForWeapon`, `IconRoleArmor` → `ForArmor`, `IconRolePotion` → `ForConsumable`).
**There is no `ForAccessory`** — rings and amulets bypass `ItemIconCatalog` entirely and load their
authored path through the generic `Resources.Load<Sprite>(spec.IconPath)` at
`Assets/_Modules/Core/UI/ElarionUiKitDetailCard.cs:257-258`.

### 1.2 Abilities: id first, then EFFECT, then one hard default

`Assets/_Modules/Core/UI/ConceptIconResolver.cs` + `Assets/_Modules/Village/HUD/HudModelProducers.cs`

```
AbilityLoadoutProducer.Poll
  └─ ConceptIconResolver.ResolveKey(def.Id, def.Effect)      HudModelProducers.cs:594
       1. Resolve(def.Id)      → concept-icons.json row?  ConceptIconResolver.cs:79-95
       2. Resolve(def.Effect)  → concept-icons.json row?
       (no row → returns null SILENTLY, :86-87 — misses are not logged)
  └─ icon = resolvedKey ?? def.Effect ?? def.Id             :595
  └─ knight.q ONLY: icon = "text:Dodge/\nAttack"            :602-606
View: HudKitController.cs:1657 → UiStyle.Icon(key) → ConceptIconResolver.Resolve
      ActionSlotHandle.SetIcon → if (s == null) s = ConceptIconResolver.DefaultSprite()
                                                     ElarionUiKitObsidian.cs:923
DefaultSprite() = the "default" block = icons/icon_combat   concept-icons.json:4-7
```

Three things follow, and all three are load-bearing:

1. **`verb` is never used to pick an icon.** `def.Verb` only becomes the medallion caption
   (`HudKitController.cs:1669`). A verb without a concept row buys you nothing.
2. **There is no class-art step on the Obsidian action bar.** Class art (`HudIcons/<Class>/<class>`)
   is the portrait path (`ElarionUiKit.cs:2130-2142`) and the *legacy* ATB bar
   (`BattleHudUgui.cs:60,96-127`) only.
3. **The `effect` fallback is why the mage's Q is a sword** — see §5.1.

### 1.3 The dual-copy JSON — CURATED output vs LIBRARY, not a sync bug

`Assets/_Modules/Core/Data/CanonicalJson.cs:9-17` — **`Resources.Load<TextAsset>` FIRST, StreamingAssets
only as a fallback.** Every catalog routes through it (`GearCatalog.cs:648-651`).

| Catalog | `Resources/Data/Canonical` | `StreamingAssets/Data/Canonical` | Identical? |
|---|---|---|---|
| `weapons.json` | **96 rows** ← *this is what ships* | 435 rows (library) | no — **by design** |
| `armor.json` | **24 rows** ← *ships* | 45 rows (library) | no — **by design** |
| `accessories.json` | 10 | 10 | yes |
| `materials.json` | 28 | 28 | yes |
| `consumables.json` | 17 | 17 | yes |
| `concept-icons.json` | 246 lines | 246 lines | yes (byte-identical) |
| `hero-talents.json` | 1703 lines | 1703 lines | yes |
| `abilities.json` | 773 lines | 773 lines | yes |
| `talent-icon-map.json` | 589 lines | 589 lines | yes |

> ### ⚠ READ THIS BEFORE "FIXING" ANY GEAR ICON
> For **`weapons.json` and `armor.json` only**, the two copies are deliberately different and the
> `Resources` copy is **GENERATED — do not hand-edit it.** Its own banner says so:
> *"GearCurationExporter (additive merge) from StreamingAssets library + GearCurationPicks.json — DO NOT
> hand-edit"* (`Assets/Resources/Data/Canonical/weapons.json:2308`, `armor.json:500`).
>
> The pipeline is: **StreamingAssets = the full library** → the owner's picks in
> `Assets/Editor/GearCurationPicks.json` (67 picks, 65 `included:true`) → the menu item
> `Defenders/Gear/Export Curated Catalog -> Resources`
> (`Assets/Editor/Catalog/GearCurationExporter.cs:64`, paths at `:55-59`) → **Resources = the curated
> runtime set**. `armor.json:3` states the same contract in words: *"AUTHORITATIVE RUNTIME curated set …
> StreamingAssets is the library superset."*
>
> **So the 356 `blink_*` icons that only the StreamingAssets rows name are not a bug and not a sync gap.
> They are art staged in the library and deliberately not curated in yet.** Bringing one into the game is
> an owner curation decision (add the id to `GearCurationPicks.json`, re-export), never a JSON merge.
> This corrects an earlier reading of mine; `docs/reference/WEAPON_CATALOG.md:25-38` had it right first
> and is the authority on the weapon side.

---

## 2. Health metric — authored vs fallback

Counted over the **live (`Resources`) catalogs only**, because those are what ship (§1.3). For weapons and
armor that means the **curated** set — the right denominator, since a library row nobody picked cannot
show a wrong icon to a player.

| Catalog | Rows | Author an `iconPath` | Fall back | Authored % |
|---|---|---|---|---|
| `weapons.json` | 96 | 76 | 20 | **79.2 %** |
| `armor.json` | 24 | 24 *(6 of them point at a file that does not exist — §5.2)* | 0 *(6 effectively)* | **100 % / 75 % real** |
| `accessories.json` | 10 | 10 | 0 | **100 %** |
| `materials.json` | 28 | 12 | 16 | **42.9 %** |
| `consumables.json` | 17 | 8 | 9 | **47.1 %** |
| **All item rows** | **175** | **130** *(124 of which resolve)* | **45** *(51 effectively)* | **74.3 % / 70.9 % real** |

| Ability rows | Count | Resolved by |
|---|---|---|
| Own `id` has a concept row | 18 / 42 | **authored** |
| Falls through to the `effect` row | 20 / 42 | **fallback** (category-level, shared with every other ability of that shape) |
| Falls all the way to `DefaultSprite()` | 4 / 42 | **nothing** — paints `icons/icon_combat` |

**Ability authored rate: 42.9 %.** Talent nodes are the healthy outlier: **83 / 83 = 100 %** authored,
1:1, no duplicates (`talent-icon-map.json` — 83 skills, 83 distinct `blinkSource`, 83 distinct
`iconPath`).

---

## 3. The registry

Sections are ordered by tag, then by source folder, per the "one registry, split within the file"
instruction. `LIVE` = wired by a `Resources/` catalog and reachable in a build. `LIBRARY-ONLY` = named only
by the dead `StreamingAssets/` copy (§1.3). `ORPHAN` = referenced by no catalog at all.

### 3.1 `Resources/ItemIcons/` — 59 standalone authored PNGs

Every one of these is reached by an authored `iconPath`, and each is named exactly for its catalog row id.
That naming convention is what makes an orphan detectable by a plain name diff.

| Icon asset (`Resources/ItemIcons/`) | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `amulet_elarion` | Shared | `amulet_elarion` — accessories.json | `Assets/Resources/Data/Canonical/accessories.json:133` | LIVE · authored |
| `amulet_heartstone` | Shared | `amulet_heartstone` — accessories.json | `Assets/Resources/Data/Canonical/accessories.json:150` | LIVE · authored |
| `amulet_lastpressing` | Shared | `amulet_lastpressing` — accessories.json | `Assets/Resources/Data/Canonical/accessories.json:117` | LIVE · authored |
| `amulet_oathward` | Shared | `amulet_oathward` — accessories.json | `Assets/Resources/Data/Canonical/accessories.json:100` | LIVE · authored |
| `amulet_travelers` | Shared | `amulet_travelers` — accessories.json | `Assets/Resources/Data/Canonical/accessories.json:85` | LIVE · authored |
| `armor_knight_common` | Knight | `armor_knight_common` — armor.json | `Assets/Resources/Data/Canonical/armor.json:98` | LIVE · authored |
| `armor_knight_epic` | Knight | `armor_knight_epic` — armor.json | `Assets/Resources/Data/Canonical/armor.json:154` | LIVE · authored |
| `armor_knight_legendary` | Knight | `armor_knight_legendary` — armor.json | `Assets/Resources/Data/Canonical/armor.json:174` | LIVE · authored |
| `armor_knight_rare` | Knight | `armor_knight_rare` — armor.json | `Assets/Resources/Data/Canonical/armor.json:134` | LIVE · authored |
| `armor_knight_uncommon` | Knight | `armor_knight_uncommon` — armor.json | `Assets/Resources/Data/Canonical/armor.json:116` | LIVE · authored |
| `armor_mage_epic` | Mage | `armor_mage_epic` — armor.json | `Assets/Resources/Data/Canonical/armor.json:351` | LIVE · authored |
| `armor_mage_legendary` | Mage | `armor_mage_legendary` — armor.json | `Assets/Resources/Data/Canonical/armor.json:371` | LIVE · authored |
| `armor_mage_rare` | Mage | `armor_mage_rare` — armor.json | `Assets/Resources/Data/Canonical/armor.json:331` | LIVE · authored |
| `armor_mage_uncommon` | Mage | `armor_mage_uncommon` — armor.json | `Assets/Resources/Data/Canonical/armor.json:312` | LIVE · authored |
| `armor_ranger_common` | Ranger | `armor_ranger_common` — armor.json | `Assets/Resources/Data/Canonical/armor.json:194` | LIVE · authored |
| `armor_ranger_epic` | Ranger | `armor_ranger_epic` — armor.json | `Assets/Resources/Data/Canonical/armor.json:252` | LIVE · authored |
| `armor_ranger_legendary` | Ranger | `armor_ranger_legendary` — armor.json | `Assets/Resources/Data/Canonical/armor.json:272` | LIVE · authored |
| `armor_ranger_rare` | Ranger | `armor_ranger_rare` — armor.json | `Assets/Resources/Data/Canonical/armor.json:232` | LIVE · authored |
| `armor_ranger_uncommon` | Ranger | `armor_ranger_uncommon` — armor.json | `Assets/Resources/Data/Canonical/armor.json:213` | LIVE · authored |
| `cons_arcane_clarity` | Unassigned | `cons_arcane_clarity` — consumables.json | `Assets/Resources/Data/Canonical/consumables.json:123` | LIVE · authored |
| `cons_elarion_blessing` | Unassigned | `cons_elarion_blessing` — consumables.json | `Assets/Resources/Data/Canonical/consumables.json:159` | LIVE · authored |
| `cons_emberfire_bomb` | Unassigned | `cons_emberfire_bomb` — consumables.json | `Assets/Resources/Data/Canonical/consumables.json:99` | LIVE · authored |
| `cons_heartward_draught` | Unassigned | `cons_heartward_draught` — consumables.json | `Assets/Resources/Data/Canonical/consumables.json:147` | LIVE · authored |
| `cons_ironbark_tonic` | Unassigned | `cons_ironbark_tonic` — consumables.json | `Assets/Resources/Data/Canonical/consumables.json:87` | LIVE · authored |
| `cons_mending_salve` | Unassigned | `cons_mending_salve` — consumables.json | `Assets/Resources/Data/Canonical/consumables.json:75` | LIVE · authored |
| `cons_suppressing_smoke` | Unassigned | `cons_suppressing_smoke` — consumables.json | `Assets/Resources/Data/Canonical/consumables.json:135` | LIVE · authored |
| `cons_swiftstep_elixir` | Unassigned | `cons_swiftstep_elixir` — consumables.json | `Assets/Resources/Data/Canonical/consumables.json:111` | LIVE · authored |
| `ing_aether_shard` | Unassigned | `ing_aether_shard` — materials.json | `Assets/Resources/Data/Canonical/materials.json:58` | LIVE · authored |
| `ing_cloth_scrap` | Unassigned | `ing_cloth_scrap` — materials.json | `Assets/Resources/Data/Canonical/materials.json:85` | LIVE · authored |
| `ing_elarion_petal` | Unassigned | `ing_elarion_petal` — materials.json | `Assets/Resources/Data/Canonical/materials.json:112` | LIVE · authored |
| `ing_ember_crystal` | Unassigned | `ing_ember_crystal` — materials.json | `Assets/Resources/Data/Canonical/materials.json:31` | LIVE · authored |
| `ing_heartstone_crystal` | Unassigned | `ing_heartstone_crystal` — materials.json | `Assets/Resources/Data/Canonical/materials.json:103` | LIVE · authored |
| `ing_ironroot` | Unassigned | `ing_ironroot` — materials.json | `Assets/Resources/Data/Canonical/materials.json:22` | LIVE · authored |
| `ing_moonbloom` | Unassigned | `ing_moonbloom` — materials.json | `Assets/Resources/Data/Canonical/materials.json:13` | LIVE · authored |
| `ing_oil_flask` | Unassigned | `ing_oil_flask` — materials.json | `Assets/Resources/Data/Canonical/materials.json:76` | LIVE · authored |
| `ing_quickfoot` | Unassigned | `ing_quickfoot` — materials.json | `Assets/Resources/Data/Canonical/materials.json:94` | LIVE · authored |
| `ing_shadowcap` | Unassigned | `ing_shadowcap` — materials.json | `Assets/Resources/Data/Canonical/materials.json:49` | LIVE · authored |
| `ing_spring_water` | Unassigned | `ing_spring_water` — materials.json | `Assets/Resources/Data/Canonical/materials.json:67` | LIVE · authored |
| `ing_starbloom` | Unassigned | `ing_starbloom` — materials.json | `Assets/Resources/Data/Canonical/materials.json:40` | LIVE · authored |
| `ring_embercoil` | Shared | `ring_embercoil` — accessories.json | `Assets/Resources/Data/Canonical/accessories.json:35` | LIVE · authored |
| `ring_firstlight` | Shared | `ring_firstlight` — accessories.json | `Assets/Resources/Data/Canonical/accessories.json:68` | LIVE · authored |
| `ring_heartward` | Shared | `ring_heartward` — accessories.json | `Assets/Resources/Data/Canonical/accessories.json:51` | LIVE · authored |
| `ring_iron` | Shared | `ring_iron` — accessories.json | `Assets/Resources/Data/Canonical/accessories.json:6` | LIVE · authored |
| `ring_steadfast` | Shared | `ring_steadfast` — accessories.json | `Assets/Resources/Data/Canonical/accessories.json:20` | LIVE · authored |
| `tripo_bow_a` | Unassigned | — nothing | `Assets/Resources/ItemIcons/tripo_bow_a.png` | **ORPHAN** |
| `tripo_bow_b` | Unassigned | — nothing | `Assets/Resources/ItemIcons/tripo_bow_b.png` | **ORPHAN** |
| `tripo_bow_c` | Unassigned | — nothing | `Assets/Resources/ItemIcons/tripo_bow_c.png` | **ORPHAN** |
| `tripo_dagger_a` | Ranger | `tripo_dagger_a` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:337` | LIVE · authored |
| `tripo_hammer_a` | Knight | `tripo_hammer_a` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:361` | LIVE · authored |
| `tripo_shield_a` | Shared | `tripo_shield_a` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:386` | LIVE · authored |
| `tripo_staff_a` | Mage | `tripo_staff_a` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:411` | LIVE · authored |
| `tripo_staff_b` | Mage | `tripo_staff_b` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:434` | LIVE · authored |
| `tripo_staff_c` | Mage | `tripo_staff_c` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:457` | LIVE · authored |
| `tripo_staff_d` | Mage | `tripo_staff_d` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:482` | LIVE · authored |
| `tripo_sword_a` | Knight | `tripo_sword_a` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:505` | LIVE · authored |
| `tripo_sword_d` | Knight | `tripo_sword_d` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:529` | LIVE · authored |
| `tripo_sword_f` | Knight | `tripo_sword_f` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:553` | LIVE · authored |
| `tripo_sword_g` | Knight | `tripo_sword_g` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:578` | LIVE · authored |
| `tripo_wand_a` | Unassigned | — nothing | `Assets/Resources/ItemIcons/tripo_wand_a.png` | **ORPHAN** |


### 3.2 `Resources/ItemIcons/blink_<weapon>_NN.png` — 400 Blink weapon icons (16 families x 25)

Mirrored from the gitignored Blink pack. **The tag comes from the `job` field on the catalog row that
names the icon** — for most of these the only row that names them lives in the StreamingAssets library
copy, so the tag is sourced from real data but the wiring is dead (§1.3, §4).

#### `blink_axe1h` — 25 icons · tag **Knight** · 6 LIVE / 19 library-only

| Icon asset (`Resources/ItemIcons/`) | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `blink_axe1h_01` | Knight | `blink_axe1h_01` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:606` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_axe1h_02` | Knight | `blink_axe1h_02` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:631` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_axe1h_03` | Knight | `blink_axe1h_03` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:656` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_axe1h_04` | Knight | `blink_axe1h_04` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:681` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_axe1h_05` | Knight | `blink_axe1h_05` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:706` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_axe1h_06` | Knight | `blink_axe1h_06` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:731` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_axe1h_07` | Knight | `blink_axe1h_07` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:756` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_axe1h_08` | Knight | `blink_axe1h_08` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:781` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_axe1h_09` | Knight | `blink_axe1h_09` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:806` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_axe1h_10` | Knight | `blink_axe1h_10` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:831` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_axe1h_11` | Knight | `blink_axe1h_11` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:856` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_axe1h_12` | Knight | `blink_axe1h_12` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:603` | LIVE · authored |
| `blink_axe1h_13` | Knight | `blink_axe1h_13` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:906` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_axe1h_14` | Knight | `blink_axe1h_14` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:628` | LIVE · authored |
| `blink_axe1h_15` | Knight | `blink_axe1h_15` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:956` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_axe1h_16` | Knight | `blink_axe1h_16` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:653` | LIVE · authored |
| `blink_axe1h_17` | Knight | `blink_axe1h_17` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:1006` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_axe1h_18` | Knight | `blink_axe1h_18` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:678` | LIVE · authored |
| `blink_axe1h_19` | Knight | `blink_axe1h_19` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:1056` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_axe1h_20` | Knight | `blink_axe1h_20` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:703` | LIVE · authored |
| `blink_axe1h_21` | Knight | `blink_axe1h_21` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:1108` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_axe1h_22` | Knight | `blink_axe1h_22` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:1133` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_axe1h_23` | Knight | `blink_axe1h_23` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:1158` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_axe1h_24` | Knight | `blink_axe1h_24` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:730` | LIVE · authored |
| `blink_axe1h_25` | Knight | `blink_axe1h_25` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:1210` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |

#### `blink_axe2h` — 25 icons · tag **Knight** · 9 LIVE / 16 library-only

| Icon asset (`Resources/ItemIcons/`) | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `blink_axe2h_01` | Knight | `blink_axe2h_01` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:1235` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_axe2h_02` | Knight | `blink_axe2h_02` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:1260` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_axe2h_03` | Knight | `blink_axe2h_03` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:1287` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_axe2h_04` | Knight | `blink_axe2h_04` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:757` | LIVE · authored |
| `blink_axe2h_05` | Knight | `blink_axe2h_05` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:1337` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_axe2h_06` | Knight | `blink_axe2h_06` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:1362` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_axe2h_07` | Knight | `blink_axe2h_07` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:1387` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_axe2h_08` | Knight | `blink_axe2h_08` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:1412` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_axe2h_09` | Knight | `blink_axe2h_09` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:782` | LIVE · authored |
| `blink_axe2h_10` | Knight | `blink_axe2h_10` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:1462` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_axe2h_11` | Knight | `blink_axe2h_11` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:1487` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_axe2h_12` | Knight | `blink_axe2h_12` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:807` | LIVE · authored |
| `blink_axe2h_13` | Knight | `blink_axe2h_13` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:1537` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_axe2h_14` | Knight | `blink_axe2h_14` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:1562` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_axe2h_15` | Knight | `blink_axe2h_15` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:832` | LIVE · authored |
| `blink_axe2h_16` | Knight | `blink_axe2h_16` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:1612` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_axe2h_17` | Knight | `blink_axe2h_17` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:1637` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_axe2h_18` | Knight | `blink_axe2h_18` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:1662` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_axe2h_19` | Knight | `blink_axe2h_19` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:1687` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_axe2h_20` | Knight | `blink_axe2h_20` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:857` | LIVE · authored |
| `blink_axe2h_21` | Knight | `blink_axe2h_21` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:1739` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_axe2h_22` | Knight | `blink_axe2h_22` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:884` | LIVE · authored |
| `blink_axe2h_23` | Knight | `blink_axe2h_23` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:911` | LIVE · authored |
| `blink_axe2h_24` | Knight | `blink_axe2h_24` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:938` | LIVE · authored |
| `blink_axe2h_25` | Knight | `blink_axe2h_25` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:965` | LIVE · authored |

#### `blink_bow2h` — 25 icons · tag **Ranger** · 15 LIVE / 10 library-only

| Icon asset (`Resources/ItemIcons/`) | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `blink_bow2h_01` | Ranger | `blink_bow2h_01` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:992` | LIVE · authored |
| `blink_bow2h_02` | Ranger | `blink_bow2h_02` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:1896` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_bow2h_03` | Ranger | `blink_bow2h_03` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1016` | LIVE · authored |
| `blink_bow2h_04` | Ranger | `blink_bow2h_04` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1040` | LIVE · authored |
| `blink_bow2h_05` | Ranger | `blink_bow2h_05` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1064` | LIVE · authored |
| `blink_bow2h_06` | Ranger | `blink_bow2h_06` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:1992` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_bow2h_07` | Ranger | `blink_bow2h_07` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:2016` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_bow2h_08` | Ranger | `blink_bow2h_08` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1088` | LIVE · authored |
| `blink_bow2h_09` | Ranger | `blink_bow2h_09` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1112` | LIVE · authored |
| `blink_bow2h_10` | Ranger | `blink_bow2h_10` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:2088` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_bow2h_11` | Ranger | `blink_bow2h_11` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:2112` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_bow2h_12` | Ranger | `blink_bow2h_12` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:2136` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_bow2h_13` | Ranger | `blink_bow2h_13` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1136` | LIVE · authored |
| `blink_bow2h_14` | Ranger | `blink_bow2h_14` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:2184` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_bow2h_15` | Ranger | `blink_bow2h_15` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:2208` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_bow2h_16` | Ranger | `blink_bow2h_16` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:2232` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_bow2h_17` | Ranger | `blink_bow2h_17` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1160` | LIVE · authored |
| `blink_bow2h_18` | Ranger | `blink_bow2h_18` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1184` | LIVE · authored |
| `blink_bow2h_19` | Ranger | `blink_bow2h_19` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1210` | LIVE · authored |
| `blink_bow2h_20` | Ranger | `blink_bow2h_20` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1236` | LIVE · authored |
| `blink_bow2h_21` | Ranger | `blink_bow2h_21` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1262` | LIVE · authored |
| `blink_bow2h_22` | Ranger | `blink_bow2h_22` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:2384` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_bow2h_23` | Ranger | `blink_bow2h_23` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1288` | LIVE · authored |
| `blink_bow2h_24` | Ranger | `blink_bow2h_24` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1314` | LIVE · authored |
| `blink_bow2h_25` | Ranger | `blink_bow2h_25` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1340` | LIVE · authored |

#### `blink_claws1h` — 25 icons · tag **Knight** · 0 LIVE / 25 library-only

| Icon asset (`Resources/ItemIcons/`) | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `blink_claws1h_01` | Knight | `blink_claws1h_01` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:2486` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_claws1h_02` | Knight | `blink_claws1h_02` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:2511` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_claws1h_03` | Knight | `blink_claws1h_03` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:2536` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_claws1h_04` | Knight | `blink_claws1h_04` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:2561` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_claws1h_05` | Knight | `blink_claws1h_05` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:2586` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_claws1h_06` | Knight | `blink_claws1h_06` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:2611` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_claws1h_07` | Knight | `blink_claws1h_07` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:2636` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_claws1h_08` | Knight | `blink_claws1h_08` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:2661` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_claws1h_09` | Knight | `blink_claws1h_09` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:2686` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_claws1h_10` | Knight | `blink_claws1h_10` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:2711` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_claws1h_11` | Knight | `blink_claws1h_11` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:2736` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_claws1h_12` | Knight | `blink_claws1h_12` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:2761` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_claws1h_13` | Knight | `blink_claws1h_13` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:2786` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_claws1h_14` | Knight | `blink_claws1h_14` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:2811` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_claws1h_15` | Knight | `blink_claws1h_15` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:2836` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_claws1h_16` | Knight | `blink_claws1h_16` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:2861` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_claws1h_17` | Knight | `blink_claws1h_17` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:2886` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_claws1h_18` | Knight | `blink_claws1h_18` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:2911` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_claws1h_19` | Knight | `blink_claws1h_19` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:2936` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_claws1h_20` | Knight | `blink_claws1h_20` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:2961` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_claws1h_21` | Knight | `blink_claws1h_21` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:2986` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_claws1h_22` | Knight | `blink_claws1h_22` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3011` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_claws1h_23` | Knight | `blink_claws1h_23` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3036` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_claws1h_24` | Knight | `blink_claws1h_24` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3061` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_claws1h_25` | Knight | `blink_claws1h_25` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3086` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |

#### `blink_crossbow2h` — 25 icons · tag **Ranger** · 0 LIVE / 25 library-only

| Icon asset (`Resources/ItemIcons/`) | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `blink_crossbow2h_01` | Ranger | `blink_crossbow2h_01` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3111` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_crossbow2h_02` | Ranger | `blink_crossbow2h_02` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3135` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_crossbow2h_03` | Ranger | `blink_crossbow2h_03` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3159` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_crossbow2h_04` | Ranger | `blink_crossbow2h_04` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3183` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_crossbow2h_05` | Ranger | `blink_crossbow2h_05` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3207` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_crossbow2h_06` | Ranger | `blink_crossbow2h_06` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3231` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_crossbow2h_07` | Ranger | `blink_crossbow2h_07` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3255` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_crossbow2h_08` | Ranger | `blink_crossbow2h_08` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3279` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_crossbow2h_09` | Ranger | `blink_crossbow2h_09` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3303` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_crossbow2h_10` | Ranger | `blink_crossbow2h_10` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3327` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_crossbow2h_11` | Ranger | `blink_crossbow2h_11` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3351` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_crossbow2h_12` | Ranger | `blink_crossbow2h_12` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3375` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_crossbow2h_13` | Ranger | `blink_crossbow2h_13` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3399` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_crossbow2h_14` | Ranger | `blink_crossbow2h_14` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3423` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_crossbow2h_15` | Ranger | `blink_crossbow2h_15` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3447` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_crossbow2h_16` | Ranger | `blink_crossbow2h_16` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3471` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_crossbow2h_17` | Ranger | `blink_crossbow2h_17` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3495` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_crossbow2h_18` | Ranger | `blink_crossbow2h_18` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3519` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_crossbow2h_19` | Ranger | `blink_crossbow2h_19` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3543` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_crossbow2h_20` | Ranger | `blink_crossbow2h_20` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3567` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_crossbow2h_21` | Ranger | `blink_crossbow2h_21` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3591` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_crossbow2h_22` | Ranger | `blink_crossbow2h_22` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3615` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_crossbow2h_23` | Ranger | `blink_crossbow2h_23` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3639` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_crossbow2h_24` | Ranger | `blink_crossbow2h_24` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3663` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_crossbow2h_25` | Ranger | `blink_crossbow2h_25` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3687` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |

#### `blink_dagger1h` — 25 icons · tag **Ranger** · 0 LIVE / 25 library-only

| Icon asset (`Resources/ItemIcons/`) | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `blink_dagger1h_01` | Ranger | `blink_dagger1h_01` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3711` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_dagger1h_02` | Ranger | `blink_dagger1h_02` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3736` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_dagger1h_03` | Ranger | `blink_dagger1h_03` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3761` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_dagger1h_04` | Ranger | `blink_dagger1h_04` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3786` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_dagger1h_05` | Ranger | `blink_dagger1h_05` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3811` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_dagger1h_06` | Ranger | `blink_dagger1h_06` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3836` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_dagger1h_07` | Ranger | `blink_dagger1h_07` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3861` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_dagger1h_08` | Ranger | `blink_dagger1h_08` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3886` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_dagger1h_09` | Ranger | `blink_dagger1h_09` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3911` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_dagger1h_10` | Ranger | `blink_dagger1h_10` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3936` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_dagger1h_11` | Ranger | `blink_dagger1h_11` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3961` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_dagger1h_12` | Ranger | `blink_dagger1h_12` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:3986` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_dagger1h_13` | Ranger | `blink_dagger1h_13` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4011` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_dagger1h_14` | Ranger | `blink_dagger1h_14` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4036` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_dagger1h_15` | Ranger | `blink_dagger1h_15` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4061` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_dagger1h_16` | Ranger | `blink_dagger1h_16` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4086` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_dagger1h_17` | Ranger | `blink_dagger1h_17` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4111` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_dagger1h_18` | Ranger | `blink_dagger1h_18` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4136` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_dagger1h_19` | Ranger | `blink_dagger1h_19` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4161` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_dagger1h_20` | Ranger | `blink_dagger1h_20` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4186` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_dagger1h_21` | Ranger | `blink_dagger1h_21` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4211` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_dagger1h_22` | Ranger | `blink_dagger1h_22` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4236` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_dagger1h_23` | Ranger | `blink_dagger1h_23` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4261` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_dagger1h_24` | Ranger | `blink_dagger1h_24` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4286` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_dagger1h_25` | Ranger | `blink_dagger1h_25` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4311` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |

#### `blink_hammer2h` — 25 icons · tag **Knight** · 0 LIVE / 25 library-only

| Icon asset (`Resources/ItemIcons/`) | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `blink_hammer2h_01` | Knight | `blink_hammer2h_01` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4336` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_hammer2h_02` | Knight | `blink_hammer2h_02` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4361` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_hammer2h_03` | Knight | `blink_hammer2h_03` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4386` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_hammer2h_04` | Knight | `blink_hammer2h_04` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4411` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_hammer2h_05` | Knight | `blink_hammer2h_05` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4436` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_hammer2h_06` | Knight | `blink_hammer2h_06` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4461` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_hammer2h_07` | Knight | `blink_hammer2h_07` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4486` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_hammer2h_08` | Knight | `blink_hammer2h_08` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4511` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_hammer2h_09` | Knight | `blink_hammer2h_09` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4536` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_hammer2h_10` | Knight | `blink_hammer2h_10` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4561` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_hammer2h_11` | Knight | `blink_hammer2h_11` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4586` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_hammer2h_12` | Knight | `blink_hammer2h_12` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4611` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_hammer2h_13` | Knight | `blink_hammer2h_13` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4636` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_hammer2h_14` | Knight | `blink_hammer2h_14` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4661` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_hammer2h_15` | Knight | `blink_hammer2h_15` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4686` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_hammer2h_16` | Knight | `blink_hammer2h_16` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4711` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_hammer2h_17` | Knight | `blink_hammer2h_17` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4736` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_hammer2h_18` | Knight | `blink_hammer2h_18` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4761` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_hammer2h_19` | Knight | `blink_hammer2h_19` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4786` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_hammer2h_20` | Knight | `blink_hammer2h_20` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4811` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_hammer2h_21` | Knight | `blink_hammer2h_21` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4836` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_hammer2h_22` | Knight | `blink_hammer2h_22` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4861` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_hammer2h_23` | Knight | `blink_hammer2h_23` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4886` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_hammer2h_24` | Knight | `blink_hammer2h_24` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4911` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_hammer2h_25` | Knight | `blink_hammer2h_25` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4936` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |

#### `blink_mace1h` — 25 icons · tag **Cleric** · 0 LIVE / 25 library-only

| Icon asset (`Resources/ItemIcons/`) | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `blink_mace1h_01` | Cleric | `blink_mace1h_01` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4961` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_mace1h_02` | Cleric | `blink_mace1h_02` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:4986` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_mace1h_03` | Cleric | `blink_mace1h_03` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5013` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_mace1h_04` | Cleric | `blink_mace1h_04` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5038` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_mace1h_05` | Cleric | `blink_mace1h_05` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5063` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_mace1h_06` | Cleric | `blink_mace1h_06` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5088` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_mace1h_07` | Cleric | `blink_mace1h_07` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5113` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_mace1h_08` | Cleric | `blink_mace1h_08` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5138` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_mace1h_09` | Cleric | `blink_mace1h_09` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5163` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_mace1h_10` | Cleric | `blink_mace1h_10` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5188` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_mace1h_11` | Cleric | `blink_mace1h_11` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5213` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_mace1h_12` | Cleric | `blink_mace1h_12` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5238` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_mace1h_13` | Cleric | `blink_mace1h_13` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5263` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_mace1h_14` | Cleric | `blink_mace1h_14` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5288` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_mace1h_15` | Cleric | `blink_mace1h_15` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5313` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_mace1h_16` | Cleric | `blink_mace1h_16` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5338` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_mace1h_17` | Cleric | `blink_mace1h_17` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5363` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_mace1h_18` | Cleric | `blink_mace1h_18` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5388` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_mace1h_19` | Cleric | `blink_mace1h_19` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5413` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_mace1h_20` | Cleric | `blink_mace1h_20` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5438` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_mace1h_21` | Cleric | `blink_mace1h_21` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5463` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_mace1h_22` | Cleric | `blink_mace1h_22` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5488` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_mace1h_23` | Cleric | `blink_mace1h_23` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5513` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_mace1h_24` | Cleric | `blink_mace1h_24` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5538` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_mace1h_25` | Cleric | `blink_mace1h_25` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5563` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |

#### `blink_polearm2h` — 25 icons · tag **Knight** · 0 LIVE / 25 library-only

| Icon asset (`Resources/ItemIcons/`) | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `blink_polearm2h_01` | Knight | `blink_polearm2h_01` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5588` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_polearm2h_02` | Knight | `blink_polearm2h_02` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5613` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_polearm2h_03` | Knight | `blink_polearm2h_03` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5638` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_polearm2h_04` | Knight | `blink_polearm2h_04` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5663` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_polearm2h_05` | Knight | `blink_polearm2h_05` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5688` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_polearm2h_06` | Knight | `blink_polearm2h_06` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5713` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_polearm2h_07` | Knight | `blink_polearm2h_07` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5738` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_polearm2h_08` | Knight | `blink_polearm2h_08` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5763` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_polearm2h_09` | Knight | `blink_polearm2h_09` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5788` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_polearm2h_10` | Knight | `blink_polearm2h_10` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5813` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_polearm2h_11` | Knight | `blink_polearm2h_11` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5838` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_polearm2h_12` | Knight | `blink_polearm2h_12` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5863` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_polearm2h_13` | Knight | `blink_polearm2h_13` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5888` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_polearm2h_14` | Knight | `blink_polearm2h_14` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5913` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_polearm2h_15` | Knight | `blink_polearm2h_15` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5938` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_polearm2h_16` | Knight | `blink_polearm2h_16` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5963` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_polearm2h_17` | Knight | `blink_polearm2h_17` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:5988` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_polearm2h_18` | Knight | `blink_polearm2h_18` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:6013` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_polearm2h_19` | Knight | `blink_polearm2h_19` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:6038` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_polearm2h_20` | Knight | `blink_polearm2h_20` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:6063` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_polearm2h_21` | Knight | `blink_polearm2h_21` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:6088` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_polearm2h_22` | Knight | `blink_polearm2h_22` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:6113` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_polearm2h_23` | Knight | `blink_polearm2h_23` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:6138` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_polearm2h_24` | Knight | `blink_polearm2h_24` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:6163` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_polearm2h_25` | Knight | `blink_polearm2h_25` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:6188` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |

#### `blink_scythe2h` — 25 icons · tag **Knight** · 0 LIVE / 25 library-only

| Icon asset (`Resources/ItemIcons/`) | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `blink_scythe2h_01` | Knight | `blink_scythe2h_01` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:6213` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_scythe2h_02` | Knight | `blink_scythe2h_02` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:6238` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_scythe2h_03` | Knight | `blink_scythe2h_03` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:6263` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_scythe2h_04` | Knight | `blink_scythe2h_04` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:6288` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_scythe2h_05` | Knight | `blink_scythe2h_05` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:6313` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_scythe2h_06` | Knight | `blink_scythe2h_06` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:6338` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_scythe2h_07` | Knight | `blink_scythe2h_07` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:6363` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_scythe2h_08` | Knight | `blink_scythe2h_08` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:6388` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_scythe2h_09` | Knight | `blink_scythe2h_09` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:6413` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_scythe2h_10` | Knight | `blink_scythe2h_10` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:6438` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_scythe2h_11` | Knight | `blink_scythe2h_11` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:6463` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_scythe2h_12` | Knight | `blink_scythe2h_12` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:6488` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_scythe2h_13` | Knight | `blink_scythe2h_13` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:6513` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_scythe2h_14` | Knight | `blink_scythe2h_14` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:6538` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_scythe2h_15` | Knight | `blink_scythe2h_15` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:6563` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_scythe2h_16` | Knight | `blink_scythe2h_16` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:6588` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_scythe2h_17` | Knight | `blink_scythe2h_17` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:6613` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_scythe2h_18` | Knight | `blink_scythe2h_18` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:6638` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_scythe2h_19` | Knight | `blink_scythe2h_19` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:6663` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_scythe2h_20` | Knight | `blink_scythe2h_20` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:6688` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_scythe2h_21` | Knight | `blink_scythe2h_21` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:6713` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_scythe2h_22` | Knight | `blink_scythe2h_22` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:6738` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_scythe2h_23` | Knight | `blink_scythe2h_23` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:6763` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_scythe2h_24` | Knight | `blink_scythe2h_24` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:6788` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_scythe2h_25` | Knight | `blink_scythe2h_25` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:6813` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |

#### `blink_shield1h` — 25 icons · tag **Shared** · 18 LIVE / 7 library-only

| Icon asset (`Resources/ItemIcons/`) | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `blink_shield1h_01` | Shared | `blink_shield1h_01` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1366` | LIVE · authored |
| `blink_shield1h_02` | Shared | `blink_shield1h_02` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1392` | LIVE · authored |
| `blink_shield1h_03` | Shared | `blink_shield1h_03` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1420` | LIVE · authored |
| `blink_shield1h_04` | Shared | `blink_shield1h_04` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1448` | LIVE · authored |
| `blink_shield1h_05` | Shared | `blink_shield1h_05` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1474` | LIVE · authored |
| `blink_shield1h_06` | Shared | `blink_shield1h_06` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1500` | LIVE · authored |
| `blink_shield1h_07` | Shared | `blink_shield1h_07` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1526` | LIVE · authored |
| `blink_shield1h_08` | Shared | `blink_shield1h_08` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1552` | LIVE · authored |
| `blink_shield1h_09` | Shared | `blink_shield1h_09` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1578` | LIVE · authored |
| `blink_shield1h_10` | Shared | `blink_shield1h_10` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:7076` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_shield1h_11` | Shared | `blink_shield1h_11` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:7102` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_shield1h_12` | Shared | `blink_shield1h_12` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1604` | LIVE · authored |
| `blink_shield1h_13` | Shared | `blink_shield1h_13` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:7154` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_shield1h_14` | Shared | `blink_shield1h_14` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:7180` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_shield1h_15` | Shared | `blink_shield1h_15` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1630` | LIVE · authored |
| `blink_shield1h_16` | Shared | `blink_shield1h_16` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1656` | LIVE · authored |
| `blink_shield1h_17` | Shared | `blink_shield1h_17` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:7260` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_shield1h_18` | Shared | `blink_shield1h_18` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1684` | LIVE · authored |
| `blink_shield1h_19` | Shared | `blink_shield1h_19` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1712` | LIVE · authored |
| `blink_shield1h_20` | Shared | `blink_shield1h_20` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1740` | LIVE · authored |
| `blink_shield1h_21` | Shared | `blink_shield1h_21` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1768` | LIVE · authored |
| `blink_shield1h_22` | Shared | `blink_shield1h_22` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:7398` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_shield1h_23` | Shared | `blink_shield1h_23` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:7424` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_shield1h_24` | Shared | `blink_shield1h_24` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1796` | LIVE · authored |
| `blink_shield1h_25` | Shared | `blink_shield1h_25` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1824` | LIVE · authored |

#### `blink_spellbook1h` — 25 icons · tag **Mage** · 0 LIVE / 25 library-only

| Icon asset (`Resources/ItemIcons/`) | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `blink_spellbook1h_01` | Mage | `blink_spellbook1h_01` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:7506` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_spellbook1h_02` | Mage | `blink_spellbook1h_02` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:7530` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_spellbook1h_03` | Mage | `blink_spellbook1h_03` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:7554` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_spellbook1h_04` | Mage | `blink_spellbook1h_04` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:7578` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_spellbook1h_05` | Mage | `blink_spellbook1h_05` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:7602` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_spellbook1h_06` | Mage | `blink_spellbook1h_06` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:7626` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_spellbook1h_07` | Mage | `blink_spellbook1h_07` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:7650` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_spellbook1h_08` | Mage | `blink_spellbook1h_08` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:7674` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_spellbook1h_09` | Mage | `blink_spellbook1h_09` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:7698` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_spellbook1h_10` | Mage | `blink_spellbook1h_10` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:7722` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_spellbook1h_11` | Mage | `blink_spellbook1h_11` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:7746` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_spellbook1h_12` | Mage | `blink_spellbook1h_12` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:7770` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_spellbook1h_13` | Mage | `blink_spellbook1h_13` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:7794` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_spellbook1h_14` | Mage | `blink_spellbook1h_14` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:7818` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_spellbook1h_15` | Mage | `blink_spellbook1h_15` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:7842` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_spellbook1h_16` | Mage | `blink_spellbook1h_16` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:7866` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_spellbook1h_17` | Mage | `blink_spellbook1h_17` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:7890` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_spellbook1h_18` | Mage | `blink_spellbook1h_18` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:7914` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_spellbook1h_19` | Mage | `blink_spellbook1h_19` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:7938` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_spellbook1h_20` | Mage | `blink_spellbook1h_20` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:7962` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_spellbook1h_21` | Mage | `blink_spellbook1h_21` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:7986` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_spellbook1h_22` | Mage | `blink_spellbook1h_22` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:8010` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_spellbook1h_23` | Mage | `blink_spellbook1h_23` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:8034` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_spellbook1h_24` | Mage | `blink_spellbook1h_24` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:8058` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_spellbook1h_25` | Mage | `blink_spellbook1h_25` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:8082` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |

#### `blink_staff2h` — 25 icons · tag **Mage** · 0 LIVE / 25 library-only

| Icon asset (`Resources/ItemIcons/`) | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `blink_staff2h_01` | Mage | `blink_staff2h_01` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:8106` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_staff2h_02` | Mage | `blink_staff2h_02` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:8133` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_staff2h_03` | Mage | `blink_staff2h_03` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:8157` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_staff2h_04` | Mage | `blink_staff2h_04` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:8181` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_staff2h_05` | Mage | `blink_staff2h_05` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:8205` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_staff2h_06` | Mage | `blink_staff2h_06` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:8229` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_staff2h_07` | Mage | `blink_staff2h_07` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:8253` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_staff2h_08` | Mage | `blink_staff2h_08` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:8277` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_staff2h_09` | Mage | `blink_staff2h_09` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:8301` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_staff2h_10` | Mage | `blink_staff2h_10` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:8325` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_staff2h_11` | Mage | `blink_staff2h_11` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:8349` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_staff2h_12` | Mage | `blink_staff2h_12` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:8373` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_staff2h_13` | Mage | `blink_staff2h_13` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:8397` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_staff2h_14` | Mage | `blink_staff2h_14` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:8421` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_staff2h_15` | Mage | `blink_staff2h_15` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:8445` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_staff2h_16` | Mage | `blink_staff2h_16` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:8469` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_staff2h_17` | Mage | `blink_staff2h_17` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:8493` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_staff2h_18` | Mage | `blink_staff2h_18` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:8517` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_staff2h_19` | Mage | `blink_staff2h_19` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:8541` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_staff2h_20` | Mage | `blink_staff2h_20` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:8565` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_staff2h_21` | Mage | `blink_staff2h_21` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:8589` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_staff2h_22` | Mage | `blink_staff2h_22` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:8613` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_staff2h_23` | Mage | `blink_staff2h_23` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:8637` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_staff2h_24` | Mage | `blink_staff2h_24` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:8661` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_staff2h_25` | Mage | `blink_staff2h_25` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:8685` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |

#### `blink_sword1h` — 25 icons · tag **Knight** · 11 LIVE / 14 library-only

| Icon asset (`Resources/ItemIcons/`) | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `blink_sword1h_01` | Knight | `blink_sword1h_01` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1852` | LIVE · authored |
| `blink_sword1h_02` | Knight | `blink_sword1h_02` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1877` | LIVE · authored |
| `blink_sword1h_03` | Knight | `blink_sword1h_03` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1902` | LIVE · authored |
| `blink_sword1h_04` | Knight | `blink_sword1h_04` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:8786` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_sword1h_05` | Knight | `blink_sword1h_05` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:8811` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_sword1h_06` | Knight | `blink_sword1h_06` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:8836` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_sword1h_07` | Knight | `blink_sword1h_07` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1929` | LIVE · authored |
| `blink_sword1h_08` | Knight | `blink_sword1h_08` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:8886` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_sword1h_09` | Knight | `blink_sword1h_09` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:8911` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_sword1h_10` | Knight | `blink_sword1h_10` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1954` | LIVE · authored |
| `blink_sword1h_11` | Knight | `blink_sword1h_11` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:1979` | LIVE · authored |
| `blink_sword1h_12` | Knight | `blink_sword1h_12` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:2004` | LIVE · authored |
| `blink_sword1h_13` | Knight | `blink_sword1h_13` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:9011` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_sword1h_14` | Knight | `blink_sword1h_14` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:2029` | LIVE · authored |
| `blink_sword1h_15` | Knight | `blink_sword1h_15` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:9063` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_sword1h_16` | Knight | `blink_sword1h_16` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:2056` | LIVE · authored |
| `blink_sword1h_17` | Knight | `blink_sword1h_17` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:9115` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_sword1h_18` | Knight | `blink_sword1h_18` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:9140` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_sword1h_19` | Knight | `blink_sword1h_19` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:9165` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_sword1h_20` | Knight | `blink_sword1h_20` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:9190` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_sword1h_21` | Knight | `blink_sword1h_21` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:2083` | LIVE · authored |
| `blink_sword1h_22` | Knight | `blink_sword1h_22` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:2110` | LIVE · authored |
| `blink_sword1h_23` | Knight | `blink_sword1h_23` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:9269` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_sword1h_24` | Knight | `blink_sword1h_24` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:9294` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_sword1h_25` | Knight | `blink_sword1h_25` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:9319` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |

#### `blink_sword2h` — 25 icons · tag **Knight** · 6 LIVE / 19 library-only

| Icon asset (`Resources/ItemIcons/`) | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `blink_sword2h_01` | Knight | `blink_sword2h_01` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:2137` | LIVE · authored |
| `blink_sword2h_02` | Knight | `blink_sword2h_02` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:9369` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_sword2h_03` | Knight | `blink_sword2h_03` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:9394` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_sword2h_04` | Knight | `blink_sword2h_04` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:9419` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_sword2h_05` | Knight | `blink_sword2h_05` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:9444` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_sword2h_06` | Knight | `blink_sword2h_06` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:9469` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_sword2h_07` | Knight | `blink_sword2h_07` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:9494` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_sword2h_08` | Knight | `blink_sword2h_08` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:9519` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_sword2h_09` | Knight | `blink_sword2h_09` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:2162` | LIVE · authored |
| `blink_sword2h_10` | Knight | `blink_sword2h_10` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:2187` | LIVE · authored |
| `blink_sword2h_11` | Knight | `blink_sword2h_11` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:9594` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_sword2h_12` | Knight | `blink_sword2h_12` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:9619` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_sword2h_13` | Knight | `blink_sword2h_13` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:9644` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_sword2h_14` | Knight | `blink_sword2h_14` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:9669` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_sword2h_15` | Knight | `blink_sword2h_15` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:9694` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_sword2h_16` | Knight | `blink_sword2h_16` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:9719` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_sword2h_17` | Knight | `blink_sword2h_17` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:9744` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_sword2h_18` | Knight | `blink_sword2h_18` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:9769` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_sword2h_19` | Knight | `blink_sword2h_19` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:9794` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_sword2h_20` | Knight | `blink_sword2h_20` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:9819` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_sword2h_21` | Knight | `blink_sword2h_21` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:9844` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_sword2h_22` | Knight | `blink_sword2h_22` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:2212` | LIVE · authored |
| `blink_sword2h_23` | Knight | `blink_sword2h_23` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:9894` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_sword2h_24` | Knight | `blink_sword2h_24` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:2237` | LIVE · authored |
| `blink_sword2h_25` | Knight | `blink_sword2h_25` — weapons.json | `Assets/Resources/Data/Canonical/weapons.json:2264` | LIVE · authored |

#### `blink_wand1h` — 25 icons · tag **Mage** · 0 LIVE / 25 library-only

| Icon asset (`Resources/ItemIcons/`) | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `blink_wand1h_01` | Mage | `blink_wand1h_01` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:9973` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_wand1h_02` | Mage | `blink_wand1h_02` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:9997` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_wand1h_03` | Mage | `blink_wand1h_03` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:10021` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_wand1h_04` | Mage | `blink_wand1h_04` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:10045` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_wand1h_05` | Mage | `blink_wand1h_05` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:10069` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_wand1h_06` | Mage | `blink_wand1h_06` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:10093` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_wand1h_07` | Mage | `blink_wand1h_07` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:10117` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_wand1h_08` | Mage | `blink_wand1h_08` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:10141` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_wand1h_09` | Mage | `blink_wand1h_09` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:10165` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_wand1h_10` | Mage | `blink_wand1h_10` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:10189` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_wand1h_11` | Mage | `blink_wand1h_11` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:10213` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_wand1h_12` | Mage | `blink_wand1h_12` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:10237` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_wand1h_13` | Mage | `blink_wand1h_13` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:10261` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_wand1h_14` | Mage | `blink_wand1h_14` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:10285` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_wand1h_15` | Mage | `blink_wand1h_15` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:10309` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_wand1h_16` | Mage | `blink_wand1h_16` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:10333` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_wand1h_17` | Mage | `blink_wand1h_17` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:10357` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_wand1h_18` | Mage | `blink_wand1h_18` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:10381` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_wand1h_19` | Mage | `blink_wand1h_19` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:10405` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_wand1h_20` | Mage | `blink_wand1h_20` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:10429` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_wand1h_21` | Mage | `blink_wand1h_21` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:10453` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_wand1h_22` | Mage | `blink_wand1h_22` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:10477` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_wand1h_23` | Mage | `blink_wand1h_23` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:10501` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_wand1h_24` | Mage | `blink_wand1h_24` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:10525` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_wand1h_25` | Mage | `blink_wand1h_25` — weapons.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/weapons.json:10549` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |



### 3.3 `Resources/ItemIcons/blink_armor_*.png` — 25 Blink armour icons · tag **Shared**

All 25 carry `"job": "any"` on their catalog row — Shared by source, not by inference.

| Icon asset (`Resources/ItemIcons/`) | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `blink_armor_basic1` | Shared | `blink_armor_basic1` — armor.json | `Assets/Resources/Data/Canonical/armor.json:473` | LIVE · authored |
| `blink_armor_basic10` | Shared | `blink_armor_basic10` — armor.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/armor.json:412` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_armor_basic2` | Shared | `blink_armor_basic2` — armor.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/armor.json:439` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_armor_basic3` | Shared | `blink_armor_basic3` — armor.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/armor.json:466` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_armor_basic4` | Shared | `blink_armor_basic4` — armor.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/armor.json:493` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_armor_basic5` | Shared | `blink_armor_basic5` — armor.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/armor.json:520` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_armor_basic6` | Shared | `blink_armor_basic6` — armor.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/armor.json:547` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_armor_basic7` | Shared | `blink_armor_basic7` — armor.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/armor.json:574` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_armor_basic8` | Shared | `blink_armor_basic8` — armor.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/armor.json:601` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_armor_basic9` | Shared | `blink_armor_basic9` — armor.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/armor.json:628` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_armor_bear` | Shared | `blink_armor_bear` — armor.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/armor.json:655` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_armor_beasthunter` | Shared | `blink_armor_beasthunter` — armor.json | `Assets/Resources/Data/Canonical/armor.json:419` | LIVE · authored |
| `blink_armor_bird` | Shared | `blink_armor_bird` — armor.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/armor.json:709` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_armor_boar` | Shared | `blink_armor_boar` — armor.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/armor.json:736` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_armor_centurion` | Shared | `blink_armor_centurion` — armor.json | `Assets/Resources/Data/Canonical/armor.json:392` | LIVE · authored |
| `blink_armor_demonhunter` | Shared | `blink_armor_demonhunter` — armor.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/armor.json:790` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_armor_dragonhunter` | Shared | `blink_armor_dragonhunter` — armor.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/armor.json:819` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_armor_dragonic` | Shared | `blink_armor_dragonic` — armor.json | `Assets/Resources/Data/Canonical/armor.json:446` | LIVE · authored |
| `blink_armor_engineer` | Shared | `blink_armor_engineer` — armor.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/armor.json:875` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_armor_hydra` | Shared | `blink_armor_hydra` — armor.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/armor.json:904` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_armor_landwarrior` | Shared | `blink_armor_landwarrior` — armor.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/armor.json:931` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_armor_lionguard` | Shared | `blink_armor_lionguard` — armor.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/armor.json:958` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_armor_minotaur` | Shared | `blink_armor_minotaur` — armor.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/armor.json:987` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_armor_pantherknight` | Shared | `blink_armor_pantherknight` — armor.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/armor.json:1014` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |
| `blink_armor_savage` | Shared | `blink_armor_savage` — armor.json (**StreamingAssets only**) | `Assets/StreamingAssets/Data/Canonical/armor.json:1043` | **LIBRARY-ONLY** — not curated into the runtime set (§1.3) |


### 3.4 `Resources/ItemIcons/*.jpg` — 8 sliced sheets, 109 sub-sprites

These are the **fallback** art. `ItemIconCatalog.EnsureLoaded` (`:258-301`) indexes every sub-sprite
by name via `Resources.LoadAll<Sprite>` (`:268`), so a sheet sprite is addressed by NAME, never by an
authored path. **Sub-sprite names were enumerated from the `name:` entries inside each `*.jpg.meta`**
spritesheet block — they are authored there and readable without opening Unity.

| Sheet | Contents | Sub-sprites | Loaded by | Tag |
|---|---|---|---|---|
| `Ud37F.jpg` | swords | 5 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:44` | Shared |
| `inEJH.jpg` | bows | 5 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:45` | Shared |
| `WRdWM.jpg` | shields | 5 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:46` | Shared |
| `VxBVb.jpg` | armour | 18 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:47` | Shared |
| `bRUz5.jpg` | potions | 8 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:48` | Shared |
| `CtQcX.jpg` | crafting | 24 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:49` | Shared |
| `jdRCa.jpg` | crafting | 32 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:50` | Shared |
| `0D5St.jpg` | misc grid (`misc_r1c1`..`misc_r2c6`) | 12 | **NOBODY** — it is sliced and shipped but absent from the `Sheets[]` array (`ItemIconCatalog.cs:42-51`), so none of its 12 sub-sprites is ever indexed | Unassigned |

#### Every sub-sprite, and what selects it

| Sub-sprite | Sheet | Tag | Selected by | Citation |
|---|---|---|---|---|
| `sword_t1` | `Ud37F.jpg` | Knight (the job-knight + unknown-job default) | sword/blade/greatsword/axe/hammer/mace/maul keyword, or job knight/unknown; rarity tier 1 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:93-96,104` |
| `sword_t2` | `Ud37F.jpg` | Knight (the job-knight + unknown-job default) | sword/blade/greatsword/axe/hammer/mace/maul keyword, or job knight/unknown; rarity tier 2 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:93-96,104` |
| `sword_t3` | `Ud37F.jpg` | Knight (the job-knight + unknown-job default) | sword/blade/greatsword/axe/hammer/mace/maul keyword, or job knight/unknown; rarity tier 3 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:93-96,104` |
| `sword_t4` | `Ud37F.jpg` | Knight (the job-knight + unknown-job default) | sword/blade/greatsword/axe/hammer/mace/maul keyword, or job knight/unknown; rarity tier 4 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:93-96,104` |
| `sword_t5` | `Ud37F.jpg` | Knight (the job-knight + unknown-job default) | sword/blade/greatsword/axe/hammer/mace/maul keyword, or job knight/unknown; rarity tier 5 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:93-96,104` |
| `bow_t1` | `inEJH.jpg` | Ranger (only reachable via `job: ranger` / bow keyword) | bow/recurve/longbow/shortbow keyword, or job ranger; rarity tier 1 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:79-80,101` |
| `bow_t2` | `inEJH.jpg` | Ranger (only reachable via `job: ranger` / bow keyword) | bow/recurve/longbow/shortbow keyword, or job ranger; rarity tier 2 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:79-80,101` |
| `bow_t3` | `inEJH.jpg` | Ranger (only reachable via `job: ranger` / bow keyword) | bow/recurve/longbow/shortbow keyword, or job ranger; rarity tier 3 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:79-80,101` |
| `bow_t4` | `inEJH.jpg` | Ranger (only reachable via `job: ranger` / bow keyword) | bow/recurve/longbow/shortbow keyword, or job ranger; rarity tier 4 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:79-80,101` |
| `bow_t5` | `inEJH.jpg` | Ranger (only reachable via `job: ranger` / bow keyword) | bow/recurve/longbow/shortbow keyword, or job ranger; rarity tier 5 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:79-80,101` |
| `shield_wooden` | `WRdWM.jpg` | Shared | shield/aegis/buckler/ward keyword + "wood", else rarity tier 1 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:126,133` |
| `shield_steel` | `WRdWM.jpg` | Shared | shield keyword + steel/iron/heater, else rarity tier 2 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:127,134` |
| `shield_rune` | `WRdWM.jpg` | Shared | shield keyword + rune/enchant, else rarity tier 3 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:128,135` |
| `shield_dragon` | `WRdWM.jpg` | Shared | shield keyword + dragon/drake/wyrm, else rarity tier 4 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:129,136` |
| `shield_magical` | `WRdWM.jpg` | Shared | shield keyword + magic/arcane/glow, else rarity tier 5 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:130,137` |
| `pauldron_a` | `VxBVb.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `helm_a` | `VxBVb.jpg` | Shared | helm/helmet/hood/crown/cap/coif keyword; rarity-indexed across 5 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:143-144,333-343` |
| `helm_b` | `VxBVb.jpg` | Shared | helm/helmet/hood/crown/cap/coif keyword; rarity-indexed across 5 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:143-144,333-343` |
| `helm_c` | `VxBVb.jpg` | Shared | helm/helmet/hood/crown/cap/coif keyword; rarity-indexed across 5 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:143-144,333-343` |
| `helm_d` | `VxBVb.jpg` | Shared | helm/helmet/hood/crown/cap/coif keyword; rarity-indexed across 5 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:143-144,333-343` |
| `helm_e` | `VxBVb.jpg` | Shared | helm/helmet/hood/crown/cap/coif keyword; rarity-indexed across 5 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:143-144,333-343` |
| `chest_a` | `VxBVb.jpg` | Shared | plate/mail/chain/leather/hide/cloth/robe/cuirass/chest keyword AND the unknown-armour catch-all; rarity-indexed across 6 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:155-161,333-343` |
| `chest_b` | `VxBVb.jpg` | Shared | plate/mail/chain/leather/hide/cloth/robe/cuirass/chest keyword AND the unknown-armour catch-all; rarity-indexed across 6 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:155-161,333-343` |
| `chest_c` | `VxBVb.jpg` | Shared | plate/mail/chain/leather/hide/cloth/robe/cuirass/chest keyword AND the unknown-armour catch-all; rarity-indexed across 6 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:155-161,333-343` |
| `chest_d` | `VxBVb.jpg` | Shared | plate/mail/chain/leather/hide/cloth/robe/cuirass/chest keyword AND the unknown-armour catch-all; rarity-indexed across 6 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:155-161,333-343` |
| `chest_e` | `VxBVb.jpg` | Shared | plate/mail/chain/leather/hide/cloth/robe/cuirass/chest keyword AND the unknown-armour catch-all; rarity-indexed across 6 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:155-161,333-343` |
| `gauntlet_a` | `VxBVb.jpg` | Shared | gauntlet/glove/bracer keyword | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:147-148` |
| `belt_a` | `VxBVb.jpg` | Shared | belt/girdle/sash/fauld keyword; rarity-indexed across 4 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:151-152,333-343` |
| `belt_b` | `VxBVb.jpg` | Shared | belt/girdle/sash/fauld keyword; rarity-indexed across 4 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:151-152,333-343` |
| `belt_c` | `VxBVb.jpg` | Shared | belt/girdle/sash/fauld keyword; rarity-indexed across 4 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:151-152,333-343` |
| `belt_d` | `VxBVb.jpg` | Shared | belt/girdle/sash/fauld keyword; rarity-indexed across 4 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:151-152,333-343` |
| `chest_f` | `VxBVb.jpg` | Shared | plate/mail/chain/leather/hide/cloth/robe/cuirass/chest keyword AND the unknown-armour catch-all; rarity-indexed across 6 | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:155-161,333-343` |
| `gauntlet_b` | `VxBVb.jpg` | Shared | gauntlet/glove/bracer keyword | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:147-148` |
| `potion_health` | `bRUz5.jpg` | Shared | health/heal/hp/regen/life/vitality keyword | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:171-172` |
| `potion_mana` | `bRUz5.jpg` | Shared | mana/aether/ether/arcane/magic keyword (1st choice); also the generic-potion 3rd choice | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:173-174,194` |
| `potion_mana_b` | `bRUz5.jpg` | Shared | mana keyword, 2nd choice | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:174` |
| `potion_poison` | `bRUz5.jpg` | Shared | poison/venom/toxic/toxin keyword | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:177-178` |
| `potion_strength` | `bRUz5.jpg` | Shared | strength/might/power/rage/berserk keyword | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:175-176` |
| `potion_strength_b` | `bRUz5.jpg` | Shared | strength keyword, 2nd choice | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:176` |
| `potion_poison_b` | `bRUz5.jpg` | Shared | poison keyword, 2nd choice | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:178` |
| `potion_fire` | `bRUz5.jpg` | Shared | fire/flame/burn/bomb/oil/incendiary keyword | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:179-180` |
| `mat_herb` | `CtQcX.jpg` | Shared | herb/leaf/plant/root/flora keyword, or authored category herb|fungus|petal|plant|flora | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:181-182,222-226` |
| `mat_crystal_a` | `CtQcX.jpg` | Shared | crystal/gem/shard keyword, or authored category crystal|gem | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:183-184,227-228` |
| `mat_crystal_b` | `CtQcX.jpg` | Shared | same as mat_crystal_a, 2nd choice | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:184,228` |
| `mat_scroll_a` | `CtQcX.jpg` | Shared | scroll/tome/parchment keyword, or authored category scroll|parchment | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:187-188,234-235` |
| `mat_crystal_c` | `CtQcX.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `mat_crystal_d` | `CtQcX.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `mat_herb_b` | `CtQcX.jpg` | Shared | same as mat_herb, 2nd choice | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:182,226` |
| `mat_crystal_e` | `CtQcX.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `mat_rune_a` | `CtQcX.jpg` | Shared | rune/runestone/stone keyword, or authored category stone|rune | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:185-186,232-233` |
| `mat_crystal_f` | `CtQcX.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `potion_a` | `CtQcX.jpg` | Shared | generic potion/elixir/draught/tonic/flask/brew keyword, 1st choice | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:193-194` |
| `potion_b` | `CtQcX.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `mat_rune_b` | `CtQcX.jpg` | Shared | same as mat_rune_a, 2nd choice | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:186,233` |
| `mat_rune_c` | `CtQcX.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `mat_rune_d` | `CtQcX.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `mat_pouch_a` | `CtQcX.jpg` | Shared | authored category dust|resin|cloth | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:238-240` |
| `potion_c` | `CtQcX.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `potion_d` | `CtQcX.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `mat_dagger` | `CtQcX.jpg` | Shared | dagger/knife/dirk/stiletto keyword, 1st choice | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:77-78` |
| `mat_hammer_a` | `CtQcX.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `mat_pouch_b` | `CtQcX.jpg` | Shared | authored category dust|resin|cloth, 2nd choice | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:240` |
| `mat_pouch_c` | `CtQcX.jpg` | Shared | authored category dust|resin|cloth, 3rd choice | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:240` |
| `mat_dirk` | `CtQcX.jpg` | Shared | dagger keyword, 2nd choice | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:78` |
| `mat_hammer_b` | `CtQcX.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `potion_e` | `jdRCa.jpg` | Shared | generic potion keyword, 2nd choice | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:194` |
| `potion_f` | `jdRCa.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `potion_g` | `jdRCa.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `potion_h` | `jdRCa.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `potion_i` | `jdRCa.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `potion_j` | `jdRCa.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `mat_herb_c` | `jdRCa.jpg` | Shared | same as mat_herb, 3rd choice | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:182,226` |
| `mat_crystal_g` | `jdRCa.jpg` | Shared | same as mat_crystal_a, 3rd choice | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:184,228` |
| `potion_k` | `jdRCa.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `potion_l` | `jdRCa.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `potion_m` | `jdRCa.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `potion_n` | `jdRCa.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `mat_herb_d` | `jdRCa.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `mat_herb_e` | `jdRCa.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `mat_herb_f` | `jdRCa.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `mat_scroll_b` | `jdRCa.jpg` | Shared | same as mat_scroll_a, 2nd choice | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:188,235` |
| `potion_o` | `jdRCa.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `potion_p` | `jdRCa.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `mat_rune_e` | `jdRCa.jpg` | Shared | same as mat_rune_a, 3rd choice | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:186,233` |
| `mat_crystal_h` | `jdRCa.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `mat_crystal_i` | `jdRCa.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `mat_crystal_j` | `jdRCa.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `mat_scroll_c` | `jdRCa.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `mat_scroll_d` | `jdRCa.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `potion_q` | `jdRCa.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `potion_r` | `jdRCa.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `mat_rune_f` | `jdRCa.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `mat_rune_g` | `jdRCa.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `mat_rune_h` | `jdRCa.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `mat_ore` | `jdRCa.jpg` | Shared | ore/ingot/metal/bar keyword, or authored category metal|ore|ingot | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:189-190,229-231` |
| `mat_ingot_a` | `jdRCa.jpg` | Shared | same as mat_ore, 2nd choice | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:190,231` |
| `mat_ingot_b` | `jdRCa.jpg` | Shared | same as mat_ore, 3rd choice | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:190,231` |
| `misc_r1c1` | `0D5St.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `misc_r1c2` | `0D5St.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `misc_r1c3` | `0D5St.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `misc_r1c4` | `0D5St.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `misc_r1c5` | `0D5St.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `misc_r1c6` | `0D5St.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `misc_r2c1` | `0D5St.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `misc_r2c2` | `0D5St.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `misc_r2c3` | `0D5St.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `misc_r2c4` | `0D5St.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `misc_r2c5` | `0D5St.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |
| `misc_r2c6` | `0D5St.jpg` | Shared | **nothing** — no branch names it | *(unreferenced)* |


### 3.5 `Resources/Talents/` — 83 talent icons · 100% authored, 1:1

The healthiest corner of the whole icon set: 83 files, 83 skills, 83 distinct `iconPath`, 83 distinct
`blinkSource`, **zero duplicates**. Loaded generically at
`Assets/_Modules/Village/Talents/HeroSkillTreePanelMvvm.cs:1778-1785` (`Resources.Load<Sprite>(path)`).
Tag = the tree the node belongs to. **`Talents/wizard/` IS the mage tree** — the folder is named
`wizard`, the ids are `mage.*`. That mismatch is real and worth knowing before a grep.

The `blinkSource` column is **provenance, not a runtime path**: it names the PNG in the gitignored
`Assets/Blink/Art/Icons` pack that `Assets/Editor/TalentIconImporter.cs:13-14` copied in. It is the only
record of WHY each pick was made, and every row also carries a `why` naming the silhouette.

#### `Talents/knight/` — 32 icons · tag **Knight**

| Icon asset | Tag | Used by (skill id / name) | Citation | Blink source (provenance) |
|---|---|---|---|---|
| `Talents/knight/knight_01.png` | Knight | `knight.t1n1` / Iron Resolve | `Assets/Resources/Data/Canonical/talent-icon-map.json:8` | `Classes/Warrior/Guardian/Guardian1.png` |
| `Talents/knight/knight_02.png` | Knight | `knight.t1n2` / Thunderbolt | `Assets/Resources/Data/Canonical/talent-icon-map.json:15` | `Classes/Elementalist/Electromancer/Electromancer1.png` |
| `Talents/knight/knight_03.png` | Knight | `knight.t1n3` / Guardian Stance | `Assets/Resources/Data/Canonical/talent-icon-map.json:22` | `Classes/Warrior/Guardian/Guardian6.png` |
| `Talents/knight/knight_04.png` | Knight | `knight.t1n4` / Mending Salve | `Assets/Resources/Data/Canonical/talent-icon-map.json:29` | `Classes/HolyDarkness/Priest/Priest4.png` |
| `Talents/knight/knight_05.png` | Knight | `knight.t1n5` / Throwing Spear | `Assets/Resources/Data/Canonical/talent-icon-map.json:36` | `Classes/Assassin/Ranger/Ranger8.png` |
| `Talents/knight/knight_06.png` | Knight | `knight.t2n1` / Shield Slam | `Assets/Resources/Data/Canonical/talent-icon-map.json:43` | `Classes/Warrior/Guardian/Guardian2.png` |
| `Talents/knight/knight_07.png` | Knight | `knight.t2n2` / Emberbrand Throw | `Assets/Resources/Data/Canonical/talent-icon-map.json:50` | `Classes/Elementalist/Pyromancer/Pyromancer1.png` |
| `Talents/knight/knight_08.png` | Knight | `knight.t2n3` / Warden's Roar | `Assets/Resources/Data/Canonical/talent-icon-map.json:57` | `Classes/Warrior/Barbarian/Barbarian3.png` |
| `Talents/knight/knight_09.png` | Knight | `knight.t2n4` / Pinning Spear | `Assets/Resources/Data/Canonical/talent-icon-map.json:64` | `Classes/Assassin/Hunter/Hunter8.png` |
| `Talents/knight/knight_10.png` | Knight | `knight.t2n5` / Bulwark | `Assets/Resources/Data/Canonical/talent-icon-map.json:71` | `Classes/Warrior/Guardian/Guardian10.png` |
| `Talents/knight/knight_11.png` | Knight | `knight.t3n1` / Suppressing Volley | `Assets/Resources/Data/Canonical/talent-icon-map.json:78` | `Classes/Warrior/Guardian/Guardian5.png` |
| `Talents/knight/knight_12.png` | Knight | `knight.t3n2` / Oathmend | `Assets/Resources/Data/Canonical/talent-icon-map.json:85` | `Classes/HolyDarkness/Priest/Priest2.png` |
| `Talents/knight/knight_13.png` | Knight | `knight.t3n3` / Legendary Vanguard | `Assets/Resources/Data/Canonical/talent-icon-map.json:92` | `Classes/HolyDarkness/Paladin/Paladin2.png` |
| `Talents/knight/knight_14.png` | Knight | `knight.t3n4` / Retaliation Surge | `Assets/Resources/Data/Canonical/talent-icon-map.json:99` | `Classes/Warrior/Guardian/Guardian8.png` |
| `Talents/knight/knight_15.png` | Knight | `knight.t3n5` / Sweeping Cut | `Assets/Resources/Data/Canonical/talent-icon-map.json:106` | `Classes/Warrior/Barbarian/Barbarian1.png` |
| `Talents/knight/knight_16.png` | Knight | `knight.t4n1` / Eternal Aegis | `Assets/Resources/Data/Canonical/talent-icon-map.json:113` | `Classes/Warrior/Guardian/Guardian4.png` |
| `Talents/knight/knight_17.png` | Knight | `knight.t4n2` / Second Wind | `Assets/Resources/Data/Canonical/talent-icon-map.json:120` | `Classes/HolyDarkness/Priest/Priest1.png` |
| `Talents/knight/knight_18.png` | Knight | `knight.t4n3` / Last Stand | `Assets/Resources/Data/Canonical/talent-icon-map.json:127` | `Classes/Warrior/Guardian/Guardian7.png` |
| `Talents/knight/knight_19.png` | Knight | `knight.t4n4` / Holy Retribution | `Assets/Resources/Data/Canonical/talent-icon-map.json:134` | `Classes/HolyDarkness/Paladin/Paladin5.png` |
| `Talents/knight/knight_20.png` | Knight | `knight.t4n5` / Champion's Combo | `Assets/Resources/Data/Canonical/talent-icon-map.json:141` | `Classes/Warrior/Berserker/Berserker4.png` |
| `Talents/knight/knight_21.png` | Knight | `knight.t2n6` / Venombrand | `Assets/Resources/Data/Canonical/talent-icon-map.json:148` | `Classes/Assassin/Rogue/Rogue7.png` |
| `Talents/knight/knight_22.png` | Knight | `knight.s1n1` / Provider's Bond | `Assets/Resources/Data/Canonical/talent-icon-map.json:155` | `Classes/Symbiose/Druid/Druid3.png` |
| `Talents/knight/knight_23.png` | Knight | `knight.s1n2` / Deep Reserves | `Assets/Resources/Data/Canonical/talent-icon-map.json:162` | `Classes/Symbiose/Enchanter/Enchanter5.png` |
| `Talents/knight/knight_24.png` | Knight | `knight.s2n1` / Master Mason | `Assets/Resources/Data/Canonical/talent-icon-map.json:169` | `Classes/Elementalist/Geomancer/Geomancer2.png` |
| `Talents/knight/knight_25.png` | Knight | `knight.s2n2` / Foreman's Pace | `Assets/Resources/Data/Canonical/talent-icon-map.json:176` | `Classes/Symbiose/Enchanter/Enchanter2.png` |
| `Talents/knight/knight_26.png` | Knight | `knight.s3n1` / Salvager | `Assets/Resources/Data/Canonical/talent-icon-map.json:183` | `Classes/Symbiose/Enchanter/Enchanter8.png` |
| `Talents/knight/knight_27.png` | Knight | `knight.s4n1` / Bountiful Banners | `Assets/Resources/Data/Canonical/talent-icon-map.json:190` | `Classes/HolyDarkness/Paladin/Paladin8.png` |
| `Talents/knight/knight_28.png` | Knight | `knight.b1n1` / Keen Ballistics | `Assets/Resources/Data/Canonical/talent-icon-map.json:197` | `Classes/Assassin/Hunter/Hunter2.png` |
| `Talents/knight/knight_29.png` | Knight | `knight.b2n1` / Farsight Emplacements | `Assets/Resources/Data/Canonical/talent-icon-map.json:204` | `Classes/Assassin/Hunter/Hunter5.png` |
| `Talents/knight/knight_30.png` | Knight | `knight.b2n2` / Hardened Ramparts | `Assets/Resources/Data/Canonical/talent-icon-map.json:211` | `Classes/Warrior/Guardian/Guardian12.png` |
| `Talents/knight/knight_31.png` | Knight | `knight.b3n1` / Standing Orders | `Assets/Resources/Data/Canonical/talent-icon-map.json:218` | `Classes/Warrior/Dragonknight/Dragonknight3.png` |
| `Talents/knight/knight_32.png` | Knight | `knight.b4n1` / Warden of Elarion | `Assets/Resources/Data/Canonical/talent-icon-map.json:225` | `Classes/HolyDarkness/Paladin/Paladin10.png` |

#### `Talents/ranger/` — 20 icons · tag **Ranger**

| Icon asset | Tag | Used by (skill id / name) | Citation | Blink source (provenance) |
|---|---|---|---|---|
| `Talents/ranger/ranger_01.png` | Ranger | `ranger.t1n1` / Quick Draw | `Assets/Resources/Data/Canonical/talent-icon-map.json:232` | `Classes/Assassin/Ranger/Ranger4.png` |
| `Talents/ranger/ranger_02.png` | Ranger | `ranger.t1n2` / Hunter's Mark | `Assets/Resources/Data/Canonical/talent-icon-map.json:239` | `Classes/Assassin/Hunter/Hunter1.png` |
| `Talents/ranger/ranger_03.png` | Ranger | `ranger.t1n3` / Tumble Step | `Assets/Resources/Data/Canonical/talent-icon-map.json:246` | `Classes/Assassin/Ranger/Ranger3.png` |
| `Talents/ranger/ranger_04.png` | Ranger | `ranger.t1n4` / Nature's Gift | `Assets/Resources/Data/Canonical/talent-icon-map.json:253` | `Classes/Symbiose/Druid/Druid1.png` |
| `Talents/ranger/ranger_05.png` | Ranger | `ranger.t1n5` / Arrow Storm Prep | `Assets/Resources/Data/Canonical/talent-icon-map.json:260` | `Classes/Assassin/Ranger/Ranger2.png` |
| `Talents/ranger/ranger_06.png` | Ranger | `ranger.t2n1` / Windstrider Boots | `Assets/Resources/Data/Canonical/talent-icon-map.json:267` | `Classes/Assassin/Rogue/Rogue3.png` |
| `Talents/ranger/ranger_07.png` | Ranger | `ranger.t2n2` / Venomcraft | `Assets/Resources/Data/Canonical/talent-icon-map.json:274` | `Classes/Assassin/Rogue/Rogue6.png` |
| `Talents/ranger/ranger_08.png` | Ranger | `ranger.t2n3` / Eagle Vision | `Assets/Resources/Data/Canonical/talent-icon-map.json:281` | `Classes/Assassin/Hunter/Hunter4.png` |
| `Talents/ranger/ranger_09.png` | Ranger | `ranger.t2n4` / Deep Freeze | `Assets/Resources/Data/Canonical/talent-icon-map.json:288` | `Classes/Elementalist/Cryomancer/Cryomancer2.png` |
| `Talents/ranger/ranger_10.png` | Ranger | `ranger.t2n5` / Shadow Veil | `Assets/Resources/Data/Canonical/talent-icon-map.json:295` | `Classes/Assassin/Rogue/Rogue1.png` |
| `Talents/ranger/ranger_11.png` | Ranger | `ranger.t3n1` / Bloodbound Draw | `Assets/Resources/Data/Canonical/talent-icon-map.json:302` | `Classes/HolyDarkness/Priest/Priest6.png` |
| `Talents/ranger/ranger_12.png` | Ranger | `ranger.t3n2` / Emberhead | `Assets/Resources/Data/Canonical/talent-icon-map.json:309` | `Classes/Elementalist/Pyromancer/Pyromancer4.png` |
| `Talents/ranger/ranger_13.png` | Ranger | `ranger.t3n3` / Leafcloak | `Assets/Resources/Data/Canonical/talent-icon-map.json:316` | `Classes/Symbiose/Druid/Druid5.png` |
| `Talents/ranger/ranger_14.png` | Ranger | `ranger.t3n4` / Beast Companion | `Assets/Resources/Data/Canonical/talent-icon-map.json:323` | `Classes/Symbiose/Beastmaster/BeastMaster1.png` |
| `Talents/ranger/ranger_15.png` | Ranger | `ranger.t3n5` / Precision Strike | `Assets/Resources/Data/Canonical/talent-icon-map.json:330` | `Classes/Assassin/Ranger/Ranger1.png` |
| `Talents/ranger/ranger_16.png` | Ranger | `ranger.t4n1` / Storm of Arrows | `Assets/Resources/Data/Canonical/talent-icon-map.json:337` | `Classes/Assassin/Ranger/Ranger10.png` |
| `Talents/ranger/ranger_17.png` | Ranger | `ranger.t4n2` / Windstrider Legend | `Assets/Resources/Data/Canonical/talent-icon-map.json:344` | `Classes/Assassin/Ranger/Ranger12.png` |
| `Talents/ranger/ranger_18.png` | Ranger | `ranger.t4n3` / Phantom Hunter | `Assets/Resources/Data/Canonical/talent-icon-map.json:351` | `Classes/Assassin/Ranger/Ranger5.png` |
| `Talents/ranger/ranger_19.png` | Ranger | `ranger.t4n4` / Nature's Fury | `Assets/Resources/Data/Canonical/talent-icon-map.json:358` | `Classes/Symbiose/Druid/Druid8.png` |
| `Talents/ranger/ranger_20.png` | Ranger | `ranger.t4n5` / Elarion's Arrow | `Assets/Resources/Data/Canonical/talent-icon-map.json:365` | `Classes/Assassin/Ranger/Ranger15.png` |

#### `Talents/wizard/` — 20 icons · tag **Mage**

| Icon asset | Tag | Used by (skill id / name) | Citation | Blink source (provenance) |
|---|---|---|---|---|
| `Talents/wizard/wizard_01.png` | Mage | `mage.t1n1` / Arcane Focus | `Assets/Resources/Data/Canonical/talent-icon-map.json:372` | `Classes/Elementalist/Arcanist/Arcanist1.png` |
| `Talents/wizard/wizard_02.png` | Mage | `mage.t1n2` / Mana Flow | `Assets/Resources/Data/Canonical/talent-icon-map.json:379` | `Classes/Elementalist/Arcanist/Arcanist5.png` |
| `Talents/wizard/wizard_03.png` | Mage | `mage.t1n3` / Warded Flesh | `Assets/Resources/Data/Canonical/talent-icon-map.json:386` | `Classes/Elementalist/Arcanist/Arcanist3.png` |
| `Talents/wizard/wizard_04.png` | Mage | `mage.t1n4` / Spellweaver | `Assets/Resources/Data/Canonical/talent-icon-map.json:393` | `Classes/Elementalist/Arcanist/Arcanist2.png` |
| `Talents/wizard/wizard_05.png` | Mage | `mage.t1n5` / Rune Binding | `Assets/Resources/Data/Canonical/talent-icon-map.json:400` | `Classes/Elementalist/Arcanist/Arcanist8.png` |
| `Talents/wizard/wizard_06.png` | Mage | `mage.t2n1` / Aether Surge | `Assets/Resources/Data/Canonical/talent-icon-map.json:407` | `Classes/Elementalist/Electromancer/Electromancer4.png` |
| `Talents/wizard/wizard_07.png` | Mage | `mage.t2n2` / Manaweave | `Assets/Resources/Data/Canonical/talent-icon-map.json:414` | `Classes/Elementalist/Arcanist/Arcanist6.png` |
| `Talents/wizard/wizard_08.png` | Mage | `mage.t2n3` / Arcane Shield | `Assets/Resources/Data/Canonical/talent-icon-map.json:421` | `Classes/Elementalist/Arcanist/Arcanist4.png` |
| `Talents/wizard/wizard_09.png` | Mage | `mage.t2n4` / Flame Mastery | `Assets/Resources/Data/Canonical/talent-icon-map.json:428` | `Classes/Elementalist/Pyromancer/Pyromancer3.png` |
| `Talents/wizard/wizard_10.png` | Mage | `mage.t2n5` / Blink Mastery | `Assets/Resources/Data/Canonical/talent-icon-map.json:435` | `Classes/Elementalist/Arcanist/Arcanist10.png` |
| `Talents/wizard/wizard_11.png` | Mage | `mage.t3n1` / Cataclysm Prep | `Assets/Resources/Data/Canonical/talent-icon-map.json:442` | `Classes/Elementalist/Pyromancer/Pyromancer8.png` |
| `Talents/wizard/wizard_12.png` | Mage | `mage.t3n2` / Spell Echo | `Assets/Resources/Data/Canonical/talent-icon-map.json:449` | `Classes/Elementalist/Arcanist/Arcanist12.png` |
| `Talents/wizard/wizard_13.png` | Mage | `mage.t3n3` / Aether Form | `Assets/Resources/Data/Canonical/talent-icon-map.json:456` | `Classes/Elementalist/Arcanist/Arcanist9.png` |
| `Talents/wizard/wizard_14.png` | Mage | `mage.t3n4` / Runic Overload | `Assets/Resources/Data/Canonical/talent-icon-map.json:463` | `Classes/Elementalist/Electromancer/Electromancer8.png` |
| `Talents/wizard/wizard_15.png` | Mage | `mage.t3n5` / Void Rift | `Assets/Resources/Data/Canonical/talent-icon-map.json:470` | `Classes/HolyDarkness/Cultist/Cultist6.png` |
| `Talents/wizard/wizard_16.png` | Mage | `mage.t4n1` / Cataclysm | `Assets/Resources/Data/Canonical/talent-icon-map.json:477` | `Classes/Elementalist/Pyromancer/Pyromancer12.png` |
| `Talents/wizard/wizard_17.png` | Mage | `mage.t4n2` / Aetherweaver Ascension | `Assets/Resources/Data/Canonical/talent-icon-map.json:484` | `Classes/Elementalist/Arcanist/Arcanist15.png` |
| `Talents/wizard/wizard_18.png` | Mage | `mage.t4n3` / Eternal Arcana | `Assets/Resources/Data/Canonical/talent-icon-map.json:491` | `Classes/Elementalist/Arcanist/Arcanist18.png` |
| `Talents/wizard/wizard_19.png` | Mage | `mage.t4n4` / Reality Rift | `Assets/Resources/Data/Canonical/talent-icon-map.json:498` | `Classes/HolyDarkness/Cultist/Cultist10.png` |
| `Talents/wizard/wizard_20.png` | Mage | `mage.t4n5` / Elarion's Legacy | `Assets/Resources/Data/Canonical/talent-icon-map.json:505` | `Classes/Elementalist/Arcanist/Arcanist20.png` |

#### `Talents/shared/` — 11 icons · tag **Shared**

| Icon asset | Tag | Used by (skill id / name) | Citation | Blink source (provenance) |
|---|---|---|---|---|
| `Talents/shared/shared_01.png` | Shared | `shared.n1` / Vitality | `Assets/Resources/Data/Canonical/talent-icon-map.json:512` | `Classes/HolyDarkness/Priest/Priest3.png` |
| `Talents/shared/shared_02.png` | Shared | `shared.n2` / Resilience | `Assets/Resources/Data/Canonical/talent-icon-map.json:519` | `Classes/Warrior/Guardian/Guardian9.png` |
| `Talents/shared/shared_03.png` | Shared | `shared.n3` / Wisdom Surge | `Assets/Resources/Data/Canonical/talent-icon-map.json:526` | `Classes/Elementalist/Arcanist/Arcanist7.png` |
| `Talents/shared/shared_04.png` | Shared | `shared.n4` / Battle Instinct | `Assets/Resources/Data/Canonical/talent-icon-map.json:533` | `Classes/Warrior/Berserker/Berserker2.png` |
| `Talents/shared/shared_05.png` | Shared | `shared.n5` / Aether Bond | `Assets/Resources/Data/Canonical/talent-icon-map.json:540` | `Classes/Elementalist/Arcanist/Arcanist11.png` |
| `Talents/shared/shared_06.png` | Shared | `shared.n6` / Legendary Resolve | `Assets/Resources/Data/Canonical/talent-icon-map.json:547` | `Classes/HolyDarkness/Paladin/Paladin12.png` |
| `Talents/shared/shared_07.png` | Shared | `shared.n7` / Swift Recovery | `Assets/Resources/Data/Canonical/talent-icon-map.json:554` | `Classes/HolyDarkness/Priest/Priest8.png` |
| `Talents/shared/shared_08.png` | Shared | `shared.n8` / Elarion's Blessing | `Assets/Resources/Data/Canonical/talent-icon-map.json:561` | `Classes/HolyDarkness/Paladin/Paladin1.png` |
| `Talents/shared/shared_09.png` | Shared | `shared.n9` / Arcane Bolt | `Assets/Resources/Data/Canonical/talent-icon-map.json:568` | `Classes/Elementalist/Arcanist/Arcanist17.png` |
| `Talents/shared/shared_10.png` | Shared | `shared.n10` / Mend | `Assets/Resources/Data/Canonical/talent-icon-map.json:575` | `Classes/HolyDarkness/Priest/Priest5.png` |
| `Talents/shared/shared_11.png` | Shared | `shared.n11` / Dash | `Assets/Resources/Data/Canonical/talent-icon-map.json:582` | `Classes/Assassin/Rogue/Rogue4.png` |



### 3.6 `Resources/RpgUi/spellicons/` — 160 Blink spell icons

Mirrored by `Assets/Editor/BlinkIconImporter.cs:94` as `spellicons/<ArchetypeGroup>/<Class>/<Class><N>`.
**Only 8 of the pack's 25 classes are mirrored** (Hunter, Arcanist, Electromancer, Pyromancer, Paladin,
Barbarian, Deathknight, Guardian) x 20 = 160. A `concept-icons.json` row naming any other class
(`Priest*`, `Rogue*`, `Ranger*`, `Cultist*`, `Druid*`, `Berserker*`, `Enchanter*`, `Geomancer*`,
`Cryomancer*`, `Dragonknight*`, `BeastMaster*`) resolves to **null** and falls silently to the default —
those names exist only as `blinkSource` provenance in `talent-icon-map.json`, pointing at the gitignored
pack, not at anything under `Resources/`.

**Tag = `Unassigned` for the art itself, and that is the honest answer.** A Blink class folder is not one
of our classes: `Guardian13` serves a knight ability *and* the class-agnostic `invuln` concept; `Hunter8`
serves `knight.ranged-poke`, a KNIGHT skill. The class lives on the CONSUMER, never on the file. Where a
row has a consumer, read the tag off that consumer.

| Icon asset | Tag | Runtime consumer (concept-icons row) | Provenance-only (talent blinkSource) | Citation | Status |
|---|---|---|---|---|---|
| `RpgUi/spellicons/Elementalist/Arcanist/Arcanist1.png` | Unassigned | — | `mage.t1n1` -> `Talents/wizard/wizard_01` | `Assets/Resources/RpgUi/spellicons/Elementalist/Arcanist/Arcanist1.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Elementalist/Arcanist/Arcanist2.png` | Unassigned | — | `mage.t1n4` -> `Talents/wizard/wizard_04` | `Assets/Resources/RpgUi/spellicons/Elementalist/Arcanist/Arcanist2.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Elementalist/Arcanist/Arcanist3.png` | Unassigned | — | `mage.t1n3` -> `Talents/wizard/wizard_03` | `Assets/Resources/RpgUi/spellicons/Elementalist/Arcanist/Arcanist3.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Elementalist/Arcanist/Arcanist4.png` | Unassigned | — | `mage.t2n3` -> `Talents/wizard/wizard_08` | `Assets/Resources/RpgUi/spellicons/Elementalist/Arcanist/Arcanist4.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Elementalist/Arcanist/Arcanist5.png` | Unassigned | — | `mage.t1n2` -> `Talents/wizard/wizard_02` | `Assets/Resources/RpgUi/spellicons/Elementalist/Arcanist/Arcanist5.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Elementalist/Arcanist/Arcanist6.png` | Unassigned | `universal.arcane-bolt` (:137) | `mage.t2n2` -> `Talents/wizard/wizard_07` | `Assets/Resources/Data/Canonical/concept-icons.json` | LIVE · authored |
| `RpgUi/spellicons/Elementalist/Arcanist/Arcanist7.png` | Unassigned | — | `shared.n3` -> `Talents/shared/shared_03` | `Assets/Resources/RpgUi/spellicons/Elementalist/Arcanist/Arcanist7.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Elementalist/Arcanist/Arcanist8.png` | Unassigned | — | `mage.t1n5` -> `Talents/wizard/wizard_05` | `Assets/Resources/RpgUi/spellicons/Elementalist/Arcanist/Arcanist8.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Elementalist/Arcanist/Arcanist9.png` | Unassigned | — | `mage.t3n3` -> `Talents/wizard/wizard_13` | `Assets/Resources/RpgUi/spellicons/Elementalist/Arcanist/Arcanist9.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Elementalist/Arcanist/Arcanist10.png` | Unassigned | — | `mage.t2n5` -> `Talents/wizard/wizard_10` | `Assets/Resources/RpgUi/spellicons/Elementalist/Arcanist/Arcanist10.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Elementalist/Arcanist/Arcanist11.png` | Unassigned | — | `shared.n5` -> `Talents/shared/shared_05` | `Assets/Resources/RpgUi/spellicons/Elementalist/Arcanist/Arcanist11.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Elementalist/Arcanist/Arcanist12.png` | Unassigned | — | `mage.t3n2` -> `Talents/wizard/wizard_12` | `Assets/Resources/RpgUi/spellicons/Elementalist/Arcanist/Arcanist12.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Elementalist/Arcanist/Arcanist13.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Elementalist/Arcanist/Arcanist13.png` | **ORPHAN** |
| `RpgUi/spellicons/Elementalist/Arcanist/Arcanist14.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Elementalist/Arcanist/Arcanist14.png` | **ORPHAN** |
| `RpgUi/spellicons/Elementalist/Arcanist/Arcanist15.png` | Unassigned | — | `mage.t4n2` -> `Talents/wizard/wizard_17` | `Assets/Resources/RpgUi/spellicons/Elementalist/Arcanist/Arcanist15.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Elementalist/Arcanist/Arcanist16.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Elementalist/Arcanist/Arcanist16.png` | **ORPHAN** |
| `RpgUi/spellicons/Elementalist/Arcanist/Arcanist17.png` | Unassigned | — | `shared.n9` -> `Talents/shared/shared_09` | `Assets/Resources/RpgUi/spellicons/Elementalist/Arcanist/Arcanist17.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Elementalist/Arcanist/Arcanist18.png` | Unassigned | — | `mage.t4n3` -> `Talents/wizard/wizard_18` | `Assets/Resources/RpgUi/spellicons/Elementalist/Arcanist/Arcanist18.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Elementalist/Arcanist/Arcanist19.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Elementalist/Arcanist/Arcanist19.png` | **ORPHAN** |
| `RpgUi/spellicons/Elementalist/Arcanist/Arcanist20.png` | Unassigned | — | `mage.t4n5` -> `Talents/wizard/wizard_20` | `Assets/Resources/RpgUi/spellicons/Elementalist/Arcanist/Arcanist20.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Warrior/Barbarian/Barbarian1.png` | Unassigned | — | `knight.t3n5` -> `Talents/knight/knight_15` | `Assets/Resources/RpgUi/spellicons/Warrior/Barbarian/Barbarian1.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Warrior/Barbarian/Barbarian2.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Barbarian/Barbarian2.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Barbarian/Barbarian3.png` | Unassigned | — | `knight.t2n3` -> `Talents/knight/knight_08` | `Assets/Resources/RpgUi/spellicons/Warrior/Barbarian/Barbarian3.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Warrior/Barbarian/Barbarian4.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Barbarian/Barbarian4.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Barbarian/Barbarian5.png` | Unassigned | `knight.wardens-roar` (:105) | — | `Assets/Resources/Data/Canonical/concept-icons.json` | LIVE · authored |
| `RpgUi/spellicons/Warrior/Barbarian/Barbarian6.png` | Unassigned | `aoe` (:37) | — | `Assets/Resources/Data/Canonical/concept-icons.json` | LIVE · authored |
| `RpgUi/spellicons/Warrior/Barbarian/Barbarian7.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Barbarian/Barbarian7.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Barbarian/Barbarian8.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Barbarian/Barbarian8.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Barbarian/Barbarian9.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Barbarian/Barbarian9.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Barbarian/Barbarian10.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Barbarian/Barbarian10.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Barbarian/Barbarian11.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Barbarian/Barbarian11.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Barbarian/Barbarian12.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Barbarian/Barbarian12.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Barbarian/Barbarian13.png` | Unassigned | `cleave` (:33)<br>`knight.sweeping-cut` (:109) | — | `Assets/Resources/Data/Canonical/concept-icons.json` | LIVE · authored |
| `RpgUi/spellicons/Warrior/Barbarian/Barbarian14.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Barbarian/Barbarian14.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Barbarian/Barbarian15.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Barbarian/Barbarian15.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Barbarian/Barbarian16.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Barbarian/Barbarian16.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Barbarian/Barbarian17.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Barbarian/Barbarian17.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Barbarian/Barbarian18.png` | Unassigned | `knight.champions-combo` (:125) | — | `Assets/Resources/Data/Canonical/concept-icons.json` | LIVE · authored |
| `RpgUi/spellicons/Warrior/Barbarian/Barbarian19.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Barbarian/Barbarian19.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Barbarian/Barbarian20.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Barbarian/Barbarian20.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Deathknight/Deathknight1.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Deathknight/Deathknight1.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Deathknight/Deathknight2.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Deathknight/Deathknight2.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Deathknight/Deathknight3.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Deathknight/Deathknight3.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Deathknight/Deathknight4.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Deathknight/Deathknight4.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Deathknight/Deathknight5.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Deathknight/Deathknight5.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Deathknight/Deathknight6.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Deathknight/Deathknight6.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Deathknight/Deathknight7.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Deathknight/Deathknight7.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Deathknight/Deathknight8.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Deathknight/Deathknight8.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Deathknight/Deathknight9.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Deathknight/Deathknight9.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Deathknight/Deathknight10.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Deathknight/Deathknight10.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Deathknight/Deathknight11.png` | Unassigned | `blink` (:57)<br>`universal.dash` (:141) | — | `Assets/Resources/Data/Canonical/concept-icons.json` | LIVE · authored |
| `RpgUi/spellicons/Warrior/Deathknight/Deathknight12.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Deathknight/Deathknight12.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Deathknight/Deathknight13.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Deathknight/Deathknight13.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Deathknight/Deathknight14.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Deathknight/Deathknight14.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Deathknight/Deathknight15.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Deathknight/Deathknight15.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Deathknight/Deathknight16.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Deathknight/Deathknight16.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Deathknight/Deathknight17.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Deathknight/Deathknight17.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Deathknight/Deathknight18.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Deathknight/Deathknight18.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Deathknight/Deathknight19.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Deathknight/Deathknight19.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Deathknight/Deathknight20.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Deathknight/Deathknight20.png` | **ORPHAN** |
| `RpgUi/spellicons/Elementalist/Electromancer/Electromancer1.png` | Unassigned | — | `knight.t1n2` -> `Talents/knight/knight_02` | `Assets/Resources/RpgUi/spellicons/Elementalist/Electromancer/Electromancer1.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Elementalist/Electromancer/Electromancer2.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Elementalist/Electromancer/Electromancer2.png` | **ORPHAN** |
| `RpgUi/spellicons/Elementalist/Electromancer/Electromancer3.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Elementalist/Electromancer/Electromancer3.png` | **ORPHAN** |
| `RpgUi/spellicons/Elementalist/Electromancer/Electromancer4.png` | Unassigned | — | `mage.t2n1` -> `Talents/wizard/wizard_06` | `Assets/Resources/RpgUi/spellicons/Elementalist/Electromancer/Electromancer4.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Elementalist/Electromancer/Electromancer5.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Elementalist/Electromancer/Electromancer5.png` | **ORPHAN** |
| `RpgUi/spellicons/Elementalist/Electromancer/Electromancer6.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Elementalist/Electromancer/Electromancer6.png` | **ORPHAN** |
| `RpgUi/spellicons/Elementalist/Electromancer/Electromancer7.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Elementalist/Electromancer/Electromancer7.png` | **ORPHAN** |
| `RpgUi/spellicons/Elementalist/Electromancer/Electromancer8.png` | Unassigned | — | `mage.t3n4` -> `Talents/wizard/wizard_14` | `Assets/Resources/RpgUi/spellicons/Elementalist/Electromancer/Electromancer8.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Elementalist/Electromancer/Electromancer9.png` | Unassigned | `knight.thunderbolt` (:97) | — | `Assets/Resources/Data/Canonical/concept-icons.json` | LIVE · authored |
| `RpgUi/spellicons/Elementalist/Electromancer/Electromancer10.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Elementalist/Electromancer/Electromancer10.png` | **ORPHAN** |
| `RpgUi/spellicons/Elementalist/Electromancer/Electromancer11.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Elementalist/Electromancer/Electromancer11.png` | **ORPHAN** |
| `RpgUi/spellicons/Elementalist/Electromancer/Electromancer12.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Elementalist/Electromancer/Electromancer12.png` | **ORPHAN** |
| `RpgUi/spellicons/Elementalist/Electromancer/Electromancer13.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Elementalist/Electromancer/Electromancer13.png` | **ORPHAN** |
| `RpgUi/spellicons/Elementalist/Electromancer/Electromancer14.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Elementalist/Electromancer/Electromancer14.png` | **ORPHAN** |
| `RpgUi/spellicons/Elementalist/Electromancer/Electromancer15.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Elementalist/Electromancer/Electromancer15.png` | **ORPHAN** |
| `RpgUi/spellicons/Elementalist/Electromancer/Electromancer16.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Elementalist/Electromancer/Electromancer16.png` | **ORPHAN** |
| `RpgUi/spellicons/Elementalist/Electromancer/Electromancer17.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Elementalist/Electromancer/Electromancer17.png` | **ORPHAN** |
| `RpgUi/spellicons/Elementalist/Electromancer/Electromancer18.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Elementalist/Electromancer/Electromancer18.png` | **ORPHAN** |
| `RpgUi/spellicons/Elementalist/Electromancer/Electromancer19.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Elementalist/Electromancer/Electromancer19.png` | **ORPHAN** |
| `RpgUi/spellicons/Elementalist/Electromancer/Electromancer20.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Elementalist/Electromancer/Electromancer20.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Guardian/Guardian1.png` | Unassigned | `knight.shield-bash` (:93)<br>`knockback` (:65) | `knight.t1n1` -> `Talents/knight/knight_01` | `Assets/Resources/Data/Canonical/concept-icons.json` | LIVE · authored |
| `RpgUi/spellicons/Warrior/Guardian/Guardian2.png` | Unassigned | — | `knight.t2n1` -> `Talents/knight/knight_06` | `Assets/Resources/RpgUi/spellicons/Warrior/Guardian/Guardian2.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Warrior/Guardian/Guardian3.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Guardian/Guardian3.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Guardian/Guardian4.png` | Unassigned | — | `knight.t4n1` -> `Talents/knight/knight_16` | `Assets/Resources/RpgUi/spellicons/Warrior/Guardian/Guardian4.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Warrior/Guardian/Guardian5.png` | Unassigned | `taunt` (:69) | `knight.t3n1` -> `Talents/knight/knight_11` | `Assets/Resources/Data/Canonical/concept-icons.json` | LIVE · authored |
| `RpgUi/spellicons/Warrior/Guardian/Guardian6.png` | Unassigned | — | `knight.t1n3` -> `Talents/knight/knight_03` | `Assets/Resources/RpgUi/spellicons/Warrior/Guardian/Guardian6.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Warrior/Guardian/Guardian7.png` | Unassigned | — | `knight.t4n3` -> `Talents/knight/knight_18` | `Assets/Resources/RpgUi/spellicons/Warrior/Guardian/Guardian7.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Warrior/Guardian/Guardian8.png` | Unassigned | — | `knight.t3n4` -> `Talents/knight/knight_14` | `Assets/Resources/RpgUi/spellicons/Warrior/Guardian/Guardian8.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Warrior/Guardian/Guardian9.png` | Unassigned | — | `shared.n2` -> `Talents/shared/shared_02` | `Assets/Resources/RpgUi/spellicons/Warrior/Guardian/Guardian9.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Warrior/Guardian/Guardian10.png` | Unassigned | — | `knight.t2n5` -> `Talents/knight/knight_10` | `Assets/Resources/RpgUi/spellicons/Warrior/Guardian/Guardian10.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Warrior/Guardian/Guardian11.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Guardian/Guardian11.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Guardian/Guardian12.png` | Unassigned | — | `knight.b2n2` -> `Talents/knight/knight_30` | `Assets/Resources/RpgUi/spellicons/Warrior/Guardian/Guardian12.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Warrior/Guardian/Guardian13.png` | Unassigned | `invuln` (:53)<br>`knight.eternal-aegis` (:117) | — | `Assets/Resources/Data/Canonical/concept-icons.json` | LIVE · authored |
| `RpgUi/spellicons/Warrior/Guardian/Guardian14.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Guardian/Guardian14.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Guardian/Guardian15.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Guardian/Guardian15.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Guardian/Guardian16.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Guardian/Guardian16.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Guardian/Guardian17.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Guardian/Guardian17.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Guardian/Guardian18.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Guardian/Guardian18.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Guardian/Guardian19.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Guardian/Guardian19.png` | **ORPHAN** |
| `RpgUi/spellicons/Warrior/Guardian/Guardian20.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Warrior/Guardian/Guardian20.png` | **ORPHAN** |
| `RpgUi/spellicons/Assassin/Hunter/Hunter1.png` | Unassigned | — | `ranger.t1n2` -> `Talents/ranger/ranger_02` | `Assets/Resources/RpgUi/spellicons/Assassin/Hunter/Hunter1.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Assassin/Hunter/Hunter2.png` | Unassigned | — | `knight.b1n1` -> `Talents/knight/knight_28` | `Assets/Resources/RpgUi/spellicons/Assassin/Hunter/Hunter2.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Assassin/Hunter/Hunter3.png` | Unassigned | `knight.snare-arrow` (:81)<br>`snare` (:29) | — | `Assets/Resources/Data/Canonical/concept-icons.json` | LIVE · authored |
| `RpgUi/spellicons/Assassin/Hunter/Hunter4.png` | Unassigned | — | `ranger.t2n3` -> `Talents/ranger/ranger_08` | `Assets/Resources/RpgUi/spellicons/Assassin/Hunter/Hunter4.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Assassin/Hunter/Hunter5.png` | Unassigned | — | `knight.b2n1` -> `Talents/knight/knight_29` | `Assets/Resources/RpgUi/spellicons/Assassin/Hunter/Hunter5.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Assassin/Hunter/Hunter6.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Assassin/Hunter/Hunter6.png` | **ORPHAN** |
| `RpgUi/spellicons/Assassin/Hunter/Hunter7.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Assassin/Hunter/Hunter7.png` | **ORPHAN** |
| `RpgUi/spellicons/Assassin/Hunter/Hunter8.png` | Unassigned | `knight.ranged-poke` (:73) | `knight.t2n4` -> `Talents/knight/knight_09` | `Assets/Resources/Data/Canonical/concept-icons.json` | LIVE · authored |
| `RpgUi/spellicons/Assassin/Hunter/Hunter9.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Assassin/Hunter/Hunter9.png` | **ORPHAN** |
| `RpgUi/spellicons/Assassin/Hunter/Hunter10.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Assassin/Hunter/Hunter10.png` | **ORPHAN** |
| `RpgUi/spellicons/Assassin/Hunter/Hunter11.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Assassin/Hunter/Hunter11.png` | **ORPHAN** |
| `RpgUi/spellicons/Assassin/Hunter/Hunter12.png` | Unassigned | `ranger.q` (:89) | — | `Assets/Resources/Data/Canonical/concept-icons.json` | LIVE · authored |
| `RpgUi/spellicons/Assassin/Hunter/Hunter13.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Assassin/Hunter/Hunter13.png` | **ORPHAN** |
| `RpgUi/spellicons/Assassin/Hunter/Hunter14.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Assassin/Hunter/Hunter14.png` | **ORPHAN** |
| `RpgUi/spellicons/Assassin/Hunter/Hunter15.png` | Unassigned | `knight.suppressing-volley` (:85) | — | `Assets/Resources/Data/Canonical/concept-icons.json` | LIVE · authored |
| `RpgUi/spellicons/Assassin/Hunter/Hunter16.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Assassin/Hunter/Hunter16.png` | **ORPHAN** |
| `RpgUi/spellicons/Assassin/Hunter/Hunter17.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Assassin/Hunter/Hunter17.png` | **ORPHAN** |
| `RpgUi/spellicons/Assassin/Hunter/Hunter18.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Assassin/Hunter/Hunter18.png` | **ORPHAN** |
| `RpgUi/spellicons/Assassin/Hunter/Hunter19.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Assassin/Hunter/Hunter19.png` | **ORPHAN** |
| `RpgUi/spellicons/Assassin/Hunter/Hunter20.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Assassin/Hunter/Hunter20.png` | **ORPHAN** |
| `RpgUi/spellicons/HolyDarkness/Paladin/Paladin1.png` | Unassigned | — | `shared.n8` -> `Talents/shared/shared_08` | `Assets/Resources/RpgUi/spellicons/HolyDarkness/Paladin/Paladin1.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/HolyDarkness/Paladin/Paladin2.png` | Unassigned | — | `knight.t3n3` -> `Talents/knight/knight_13` | `Assets/Resources/RpgUi/spellicons/HolyDarkness/Paladin/Paladin2.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/HolyDarkness/Paladin/Paladin3.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/HolyDarkness/Paladin/Paladin3.png` | **ORPHAN** |
| `RpgUi/spellicons/HolyDarkness/Paladin/Paladin4.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/HolyDarkness/Paladin/Paladin4.png` | **ORPHAN** |
| `RpgUi/spellicons/HolyDarkness/Paladin/Paladin5.png` | Unassigned | `heal` (:17)<br>`knight.mending-salve` (:77) | `knight.t4n4` -> `Talents/knight/knight_19` | `Assets/Resources/Data/Canonical/concept-icons.json` | LIVE · authored |
| `RpgUi/spellicons/HolyDarkness/Paladin/Paladin6.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/HolyDarkness/Paladin/Paladin6.png` | **ORPHAN** |
| `RpgUi/spellicons/HolyDarkness/Paladin/Paladin7.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/HolyDarkness/Paladin/Paladin7.png` | **ORPHAN** |
| `RpgUi/spellicons/HolyDarkness/Paladin/Paladin8.png` | Unassigned | — | `knight.s4n1` -> `Talents/knight/knight_27` | `Assets/Resources/RpgUi/spellicons/HolyDarkness/Paladin/Paladin8.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/HolyDarkness/Paladin/Paladin9.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/HolyDarkness/Paladin/Paladin9.png` | **ORPHAN** |
| `RpgUi/spellicons/HolyDarkness/Paladin/Paladin10.png` | Unassigned | — | `knight.b4n1` -> `Talents/knight/knight_32` | `Assets/Resources/RpgUi/spellicons/HolyDarkness/Paladin/Paladin10.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/HolyDarkness/Paladin/Paladin11.png` | Unassigned | `meteor` (:41) | — | `Assets/Resources/Data/Canonical/concept-icons.json` | LIVE · authored |
| `RpgUi/spellicons/HolyDarkness/Paladin/Paladin12.png` | Unassigned | `knight.second-wind` (:121) | `shared.n6` -> `Talents/shared/shared_06` | `Assets/Resources/Data/Canonical/concept-icons.json` | LIVE · authored |
| `RpgUi/spellicons/HolyDarkness/Paladin/Paladin13.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/HolyDarkness/Paladin/Paladin13.png` | **ORPHAN** |
| `RpgUi/spellicons/HolyDarkness/Paladin/Paladin14.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/HolyDarkness/Paladin/Paladin14.png` | **ORPHAN** |
| `RpgUi/spellicons/HolyDarkness/Paladin/Paladin15.png` | Unassigned | `universal.mend` (:133) | — | `Assets/Resources/Data/Canonical/concept-icons.json` | LIVE · authored |
| `RpgUi/spellicons/HolyDarkness/Paladin/Paladin16.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/HolyDarkness/Paladin/Paladin16.png` | **ORPHAN** |
| `RpgUi/spellicons/HolyDarkness/Paladin/Paladin17.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/HolyDarkness/Paladin/Paladin17.png` | **ORPHAN** |
| `RpgUi/spellicons/HolyDarkness/Paladin/Paladin18.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/HolyDarkness/Paladin/Paladin18.png` | **ORPHAN** |
| `RpgUi/spellicons/HolyDarkness/Paladin/Paladin19.png` | Unassigned | `healovertime` (:49)<br>`knight.oathmend` (:113) | — | `Assets/Resources/Data/Canonical/concept-icons.json` | LIVE · authored |
| `RpgUi/spellicons/HolyDarkness/Paladin/Paladin20.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/HolyDarkness/Paladin/Paladin20.png` | **ORPHAN** |
| `RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer1.png` | Unassigned | `dot` (:45)<br>`knight.emberbrand-throw` (:101) | `knight.t2n2` -> `Talents/knight/knight_07` | `Assets/Resources/Data/Canonical/concept-icons.json` | LIVE · authored |
| `RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer2.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer2.png` | **ORPHAN** |
| `RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer3.png` | Unassigned | — | `mage.t2n4` -> `Talents/wizard/wizard_09` | `Assets/Resources/RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer3.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer4.png` | Unassigned | — | `ranger.t3n2` -> `Talents/ranger/ranger_12` | `Assets/Resources/RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer4.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer5.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer5.png` | **ORPHAN** |
| `RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer6.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer6.png` | **ORPHAN** |
| `RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer7.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer7.png` | **ORPHAN** |
| `RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer8.png` | Unassigned | — | `mage.t3n1` -> `Talents/wizard/wizard_11` | `Assets/Resources/RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer8.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer9.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer9.png` | **ORPHAN** |
| `RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer10.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer10.png` | **ORPHAN** |
| `RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer11.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer11.png` | **ORPHAN** |
| `RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer12.png` | Unassigned | — | `mage.t4n1` -> `Talents/wizard/wizard_16` | `Assets/Resources/RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer12.png` | mirrored-only — the shipped copy of that pick lives in `Resources/Talents/` |
| `RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer13.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer13.png` | **ORPHAN** |
| `RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer14.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer14.png` | **ORPHAN** |
| `RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer15.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer15.png` | **ORPHAN** |
| `RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer16.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer16.png` | **ORPHAN** |
| `RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer17.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer17.png` | **ORPHAN** |
| `RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer18.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer18.png` | **ORPHAN** |
| `RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer19.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer19.png` | **ORPHAN** |
| `RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer20.png` | Unassigned | — | — | `Assets/Resources/RpgUi/spellicons/Elementalist/Pyromancer/Pyromancer20.png` | **ORPHAN** |


### 3.7 `Resources/RpgUi/` — the other 18 roles, 275 files

`RpgUiCatalog` maps a role generically: **role `X` -> `Resources/RpgUi/X`**, indexed by sprite name
(`Assets/_Modules/Core/UI/RpgUiCatalog.cs:44,324`). Roles the JSON uses with no C# constant
(`abilities`, `spellicons`, `emblem`, `classslot`, `currency`, `decoration`) work precisely because that
lookup is generic - there is no role whitelist to add to.

Almost all of this is **UI chrome with no class**, so the tag is **Shared** by construction rather than by
guess. The two exceptions are `emblem` and `classslot`, which are named for BLINK pack classes and not
ours; those are **Unassigned**.

#### `RpgUi/icons/` — 11 files · tag **Shared** · concept-icons `role: icons` + `UiStyle` glyph lookups

| Icon asset | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `RpgUi/icons/icon_combat.png` | Shared | `combat` (:153) - concept-icons.json | `Assets/Resources/RpgUi/icons/icon_combat.png` | LIVE · authored (data-driven) |
| `RpgUi/icons/icon_compass.png` | Shared | `compass` (:157) - concept-icons.json | `Assets/Resources/RpgUi/icons/icon_compass.png` | LIVE · authored (data-driven) |
| `RpgUi/icons/icon_energy_sword.png` | Shared | `energy-sword` (:241) - concept-icons.json | `Assets/Resources/RpgUi/icons/icon_energy_sword.png` | LIVE · authored (data-driven) |
| `RpgUi/icons/icon_heart.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/icons/icon_heart.png` | code-referenced |
| `RpgUi/icons/icon_inventory.png` | Shared | `bag` (:189)<br>`inventory` (:185) - concept-icons.json | `Assets/Resources/RpgUi/icons/icon_inventory.png` | LIVE · authored (data-driven) |
| `RpgUi/icons/icon_quest.png` | Shared | `quest` (:193) - concept-icons.json | `Assets/Resources/RpgUi/icons/icon_quest.png` | LIVE · authored (data-driven) |
| `RpgUi/icons/icon_settings.png` | Shared | `settings` (:169) - concept-icons.json | `Assets/Resources/RpgUi/icons/icon_settings.png` | LIVE · authored (data-driven) |
| `RpgUi/icons/icon_shield.png` | Shared | `parry` (:13)<br>`shield` (:149) - concept-icons.json | `Assets/Resources/RpgUi/icons/icon_shield.png` | LIVE · authored (data-driven) |
| `RpgUi/icons/icon_sword.png` | Shared | `sword` (:145)<br>`thrust` (:9) - concept-icons.json | `Assets/Resources/RpgUi/icons/icon_sword.png` | LIVE · authored (data-driven) |
| `RpgUi/icons/icon_talk.png` | Shared | `talk` (:161) - concept-icons.json | `Assets/Resources/RpgUi/icons/icon_talk.png` | LIVE · authored (data-driven) |
| `RpgUi/icons/icon_tree.png` | Shared | `tree` (:165) - concept-icons.json | `Assets/Resources/RpgUi/icons/icon_tree.png` | LIVE · authored (data-driven) |

#### `RpgUi/abilities/` — 5 files · tag **Shared** · concept-icons `role: abilities` - the only 5 hand-made ability sprites in the repo

| Icon asset | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `RpgUi/abilities/attack_sword.png` | Shared | `attack` (:237)<br>`strike` (:25) - concept-icons.json | `Assets/Resources/RpgUi/abilities/attack_sword.png` | LIVE · authored (data-driven) |
| `RpgUi/abilities/charge_knight.png` | Shared | `charge` (:21)<br>`knight.q` (:129) - concept-icons.json | `Assets/Resources/RpgUi/abilities/charge_knight.png` | LIVE · authored (data-driven) |
| `RpgUi/abilities/heal_cross.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/abilities/heal_cross.png` | code-referenced |
| `RpgUi/abilities/run_dash.png` | Shared | `dash` (:61) - concept-icons.json | `Assets/Resources/RpgUi/abilities/run_dash.png` | LIVE · authored (data-driven) |
| `RpgUi/abilities/shield_bash.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/abilities/shield_bash.png` | code-referenced |

#### `RpgUi/potion/` — 3 files · tag **Shared** · concept-icons `role: potion`

| Icon asset | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `RpgUi/potion/potion_fire.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/potion/potion_fire.png` | code-referenced |
| `RpgUi/potion/potion_health.png` | Shared | `elixir` (:177)<br>`potion` (:173) - concept-icons.json | `Assets/Resources/RpgUi/potion/potion_health.png` | LIVE · authored (data-driven) |
| `RpgUi/potion/potion_mana.png` | Shared | `mana` (:181) - concept-icons.json | `Assets/Resources/RpgUi/potion/potion_mana.png` | LIVE · authored (data-driven) |

#### `RpgUi/currency/` — 5 files · tag **Shared** · concept-icons `role: currency` - bank / HUD / collect floats

| Icon asset | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `RpgUi/currency/currency_crystal.png` | Shared | `crystal` (:213)<br>`crystals` (:217) - concept-icons.json | `Assets/Resources/RpgUi/currency/currency_crystal.png` | LIVE · authored (data-driven) |
| `RpgUi/currency/currency_food.png` | Shared | `food` (:209)<br>`foods` (:229) - concept-icons.json | `Assets/Resources/RpgUi/currency/currency_food.png` | LIVE · authored (data-driven) |
| `RpgUi/currency/currency_gold.png` | Shared | `gold` (:197)<br>`golds` (:233) - concept-icons.json | `Assets/Resources/RpgUi/currency/currency_gold.png` | LIVE · authored (data-driven) |
| `RpgUi/currency/currency_iron.png` | Shared | `iron` (:205)<br>`irons` (:225) - concept-icons.json | `Assets/Resources/RpgUi/currency/currency_iron.png` | LIVE · authored (data-driven) |
| `RpgUi/currency/currency_wood.png` | Shared | `wood` (:201)<br>`woods` (:221) - concept-icons.json | `Assets/Resources/RpgUi/currency/currency_wood.png` | LIVE · authored (data-driven) |

#### `RpgUi/emblem/` — 25 files · tag **Unassigned** · Blink class emblems (`Assets/Editor/BlinkIconImporter.cs:107`)

| Icon asset | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `RpgUi/emblem/Arcanist.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/emblem/Arcanist.png` | code-referenced |
| `RpgUi/emblem/Barbarian.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/emblem/Barbarian.png` | code-referenced |
| `RpgUi/emblem/Beastmaster.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/emblem/Beastmaster.png` | code-referenced |
| `RpgUi/emblem/Berserker.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/emblem/Berserker.png` | code-referenced |
| `RpgUi/emblem/Brawler.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/emblem/Brawler.png` | code-referenced |
| `RpgUi/emblem/Cryomancer.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/emblem/Cryomancer.png` | code-referenced |
| `RpgUi/emblem/Cultist.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/emblem/Cultist.png` | code-referenced |
| `RpgUi/emblem/Deathknight.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/emblem/Deathknight.png` | code-referenced |
| `RpgUi/emblem/DemonHunter.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/emblem/DemonHunter.png` | code-referenced |
| `RpgUi/emblem/Dragonknight.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/emblem/Dragonknight.png` | code-referenced |
| `RpgUi/emblem/Druid.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/emblem/Druid.png` | code-referenced |
| `RpgUi/emblem/Electromancer.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/emblem/Electromancer.png` | code-referenced |
| `RpgUi/emblem/Enchanter.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/emblem/Enchanter.png` | code-referenced |
| `RpgUi/emblem/Geomancer.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/emblem/Geomancer.png` | code-referenced |
| `RpgUi/emblem/Guardian.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/emblem/Guardian.png` | code-referenced |
| `RpgUi/emblem/Hunter.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/emblem/Hunter.png` | code-referenced |
| `RpgUi/emblem/Medium.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/emblem/Medium.png` | code-referenced |
| `RpgUi/emblem/Necromancer.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/emblem/Necromancer.png` | code-referenced |
| `RpgUi/emblem/Paladin.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/emblem/Paladin.png` | code-referenced |
| `RpgUi/emblem/Priest.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/emblem/Priest.png` | code-referenced |
| `RpgUi/emblem/Pyromancer.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/emblem/Pyromancer.png` | code-referenced |
| `RpgUi/emblem/Ranger.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/emblem/Ranger.png` | code-referenced |
| `RpgUi/emblem/Rogue.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/emblem/Rogue.png` | code-referenced |
| `RpgUi/emblem/Shaman.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/emblem/Shaman.png` | code-referenced |
| `RpgUi/emblem/Shapeshifter.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/emblem/Shapeshifter.png` | code-referenced |

#### `RpgUi/classslot/` — 28 files · tag **Unassigned** · Blink themed action-bar slot frames (`Assets/Editor/BlinkIconImporter.cs:117`)

| Icon asset | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `RpgUi/classslot/Slot_Arcanist.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/classslot/Slot_Arcanist.png` | code-referenced |
| `RpgUi/classslot/Slot_Barbarian.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/classslot/Slot_Barbarian.png` | code-referenced |
| `RpgUi/classslot/Slot_Beastmaster.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/classslot/Slot_Beastmaster.png` | code-referenced |
| `RpgUi/classslot/Slot_Berserker.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/classslot/Slot_Berserker.png` | code-referenced |
| `RpgUi/classslot/Slot_Brawler.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/classslot/Slot_Brawler.png` | code-referenced |
| `RpgUi/classslot/Slot_Cryomancer.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/classslot/Slot_Cryomancer.png` | code-referenced |
| `RpgUi/classslot/Slot_Cultist.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/classslot/Slot_Cultist.png` | code-referenced |
| `RpgUi/classslot/Slot_Deathknight.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/classslot/Slot_Deathknight.png` | code-referenced |
| `RpgUi/classslot/Slot_DemonHunter.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/classslot/Slot_DemonHunter.png` | code-referenced |
| `RpgUi/classslot/Slot_Dragonknight.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/classslot/Slot_Dragonknight.png` | code-referenced |
| `RpgUi/classslot/Slot_Druid.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/classslot/Slot_Druid.png` | code-referenced |
| `RpgUi/classslot/Slot_Electromancer.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/classslot/Slot_Electromancer.png` | code-referenced |
| `RpgUi/classslot/Slot_Enchanter.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/classslot/Slot_Enchanter.png` | code-referenced |
| `RpgUi/classslot/Slot_Geomancer.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/classslot/Slot_Geomancer.png` | code-referenced |
| `RpgUi/classslot/Slot_Guardian.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/classslot/Slot_Guardian.png` | code-referenced |
| `RpgUi/classslot/Slot_Hunter.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/classslot/Slot_Hunter.png` | code-referenced |
| `RpgUi/classslot/Slot_Medium.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/classslot/Slot_Medium.png` | code-referenced |
| `RpgUi/classslot/Slot_Necromancer.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/classslot/Slot_Necromancer.png` | code-referenced |
| `RpgUi/classslot/Slot_Paladin.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/classslot/Slot_Paladin.png` | code-referenced |
| `RpgUi/classslot/Slot_Priest.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/classslot/Slot_Priest.png` | code-referenced |
| `RpgUi/classslot/Slot_Pyromancer.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/classslot/Slot_Pyromancer.png` | code-referenced |
| `RpgUi/classslot/Slot_Ranger.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/classslot/Slot_Ranger.png` | code-referenced |
| `RpgUi/classslot/Slot_Rogue.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/classslot/Slot_Rogue.png` | code-referenced |
| `RpgUi/classslot/Slot_Shaman.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/classslot/Slot_Shaman.png` | code-referenced |
| `RpgUi/classslot/Slot_Shapeshifter.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/classslot/Slot_Shapeshifter.png` | code-referenced |
| `RpgUi/classslot/Slot1.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/classslot/Slot1.png` | code-referenced |
| `RpgUi/classslot/Slot2.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/classslot/Slot2.png` | code-referenced |
| `RpgUi/classslot/Slot3.png` | Unassigned | UI-kit lookup by name | `Assets/Resources/RpgUi/classslot/Slot3.png` | code-referenced |

#### `RpgUi/hud/` — 39 files · tag **Shared** · HUD chrome - bars, nameplates, crosshairs (`RpgUiCatalog.cs:68`)

| Icon asset | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `RpgUi/hud/bar_cast_1.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/bar_cast_1.png` | code-referenced |
| `RpgUi/hud/bar_cast_2.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/bar_cast_2.png` | code-referenced |
| `RpgUi/hud/bar_cast_3.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/bar_cast_3.png` | code-referenced |
| `RpgUi/hud/bar_cast_fill.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/bar_cast_fill.png` | code-referenced |
| `RpgUi/hud/bar_energy.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/bar_energy.png` | code-referenced |
| `RpgUi/hud/bar_health.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/bar_health.png` | code-referenced |
| `RpgUi/hud/bar_mana.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/bar_mana.png` | code-referenced |
| `RpgUi/hud/bar_stamina.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/bar_stamina.png` | code-referenced |
| `RpgUi/hud/bar_stat_bg.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/bar_stat_bg.png` | code-referenced |
| `RpgUi/hud/bar_stat_fill.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/bar_stat_fill.png` | code-referenced |
| `RpgUi/hud/bar_xp.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/bar_xp.png` | code-referenced |
| `RpgUi/hud/chat_core.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/chat_core.png` | code-referenced |
| `RpgUi/hud/chat_tab.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/chat_tab.png` | code-referenced |
| `RpgUi/hud/crosshair_1.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/crosshair_1.png` | code-referenced |
| `RpgUi/hud/crosshair_2.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/crosshair_2.png` | code-referenced |
| `RpgUi/hud/crosshair_3.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/crosshair_3.png` | code-referenced |
| `RpgUi/hud/hud_arc_1.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/hud_arc_1.png` | code-referenced |
| `RpgUi/hud/hud_arc_2.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/hud_arc_2.png` | code-referenced |
| `RpgUi/hud/hud_block.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/hud_block.png` | code-referenced |
| `RpgUi/hud/hud_collapse.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/hud_collapse.png` | code-referenced |
| `RpgUi/hud/hud_core.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/hud_core.png` | code-referenced |
| `RpgUi/hud/hud_core_diablo.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/hud_core_diablo.png` | code-referenced |
| `RpgUi/hud/hud_expand.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/hud_expand.png` | code-referenced |
| `RpgUi/hud/hud_interaction.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/hud_interaction.png` | code-referenced |
| `RpgUi/hud/nameplate_bar.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/nameplate_bar.png` | code-referenced |
| `RpgUi/hud/nameplate_boss.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/nameplate_boss.png` | code-referenced |
| `RpgUi/hud/nameplate_enemy_bg.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/nameplate_enemy_bg.png` | code-referenced |
| `RpgUi/hud/nameplate_health.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/nameplate_health.png` | code-referenced |
| `RpgUi/hud/nameplate_health_enemy.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/nameplate_health_enemy.png` | code-referenced |
| `RpgUi/hud/nameplate_health_neutral.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/nameplate_health_neutral.png` | code-referenced |
| `RpgUi/hud/nameplate_mana.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/nameplate_mana.png` | code-referenced |
| `RpgUi/hud/nameplate_party.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/nameplate_party.png` | code-referenced |
| `RpgUi/hud/nameplate_portrait.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/nameplate_portrait.png` | code-referenced |
| `RpgUi/hud/nameplate_rare.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/nameplate_rare.png` | code-referenced |
| `RpgUi/hud/portrait_border.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/portrait_border.png` | code-referenced |
| `RpgUi/hud/quest_tracker.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/quest_tracker.png` | code-referenced |
| `RpgUi/hud/quest_tracker_bar.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/quest_tracker_bar.png` | code-referenced |
| `RpgUi/hud/stat_orb.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/stat_orb.png` | code-referenced |
| `RpgUi/hud/target_core.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/hud/target_core.png` | code-referenced |

#### `RpgUi/button/` — 50 files · tag **Shared** · button faces, sliders, toggles (`RpgUiCatalog.cs:57`)

| Icon asset | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `RpgUi/button/arrow.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/arrow.png` | code-referenced |
| `RpgUi/button/button_confirm.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/button_confirm.png` | code-referenced |
| `RpgUi/button/button_deny.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/button_deny.png` | code-referenced |
| `RpgUi/button/button_exit.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/button_exit.png` | code-referenced |
| `RpgUi/button/button_frame.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/button_frame.png` | code-referenced |
| `RpgUi/button/button_gold.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/button_gold.png` | code-referenced |
| `RpgUi/button/button1_gray.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/button1_gray.png` | code-referenced |
| `RpgUi/button/button1_green.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/button1_green.png` | code-referenced |
| `RpgUi/button/button1_red.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/button1_red.png` | code-referenced |
| `RpgUi/button/button1_yellow.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/button1_yellow.png` | code-referenced |
| `RpgUi/button/button2_gray.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/button2_gray.png` | code-referenced |
| `RpgUi/button/button2_green.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/button2_green.png` | code-referenced |
| `RpgUi/button/button2_red.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/button2_red.png` | code-referenced |
| `RpgUi/button/button2_yellow.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/button2_yellow.png` | code-referenced |
| `RpgUi/button/button3_gray.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/button3_gray.png` | code-referenced |
| `RpgUi/button/button3_green.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/button3_green.png` | code-referenced |
| `RpgUi/button/button3_red.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/button3_red.png` | code-referenced |
| `RpgUi/button/button3_yellow.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/button3_yellow.png` | code-referenced |
| `RpgUi/button/button4_gray.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/button4_gray.png` | code-referenced |
| `RpgUi/button/button4_green.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/button4_green.png` | code-referenced |
| `RpgUi/button/button4_red.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/button4_red.png` | code-referenced |
| `RpgUi/button/button4_yellow.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/button4_yellow.png` | code-referenced |
| `RpgUi/button/button5_gray.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/button5_gray.png` | code-referenced |
| `RpgUi/button/button5_green.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/button5_green.png` | code-referenced |
| `RpgUi/button/button5_red.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/button5_red.png` | code-referenced |
| `RpgUi/button/button5_yellow.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/button5_yellow.png` | code-referenced |
| `RpgUi/button/chat_element_1.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/chat_element_1.png` | code-referenced |
| `RpgUi/button/chat_element_2.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/chat_element_2.png` | code-referenced |
| `RpgUi/button/chat_element_3.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/chat_element_3.png` | code-referenced |
| `RpgUi/button/chat_element_4.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/chat_element_4.png` | code-referenced |
| `RpgUi/button/close_normal.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/close_normal.png` | code-referenced |
| `RpgUi/button/close_off.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/close_off.png` | code-referenced |
| `RpgUi/button/close_on.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/close_on.png` | code-referenced |
| `RpgUi/button/dropdown_1.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/dropdown_1.png` | code-referenced |
| `RpgUi/button/dropdown_2.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/dropdown_2.png` | code-referenced |
| `RpgUi/button/dropdown_3.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/dropdown_3.png` | code-referenced |
| `RpgUi/button/map_unzoom.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/map_unzoom.png` | code-referenced |
| `RpgUi/button/map_zoom.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/map_zoom.png` | code-referenced |
| `RpgUi/button/notif_btn_1.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/notif_btn_1.png` | code-referenced |
| `RpgUi/button/notif_btn_2.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/notif_btn_2.png` | code-referenced |
| `RpgUi/button/obsidian_gray.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/obsidian_gray.png` | code-referenced |
| `RpgUi/button/obsidian_green.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/obsidian_green.png` | code-referenced |
| `RpgUi/button/obsidian_red.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/obsidian_red.png` | code-referenced |
| `RpgUi/button/obsidian_yellow.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/obsidian_yellow.png` | code-referenced |
| `RpgUi/button/popup.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/popup.png` | code-referenced |
| `RpgUi/button/slider_bg.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/slider_bg.png` | code-referenced |
| `RpgUi/button/slider_fill.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/slider_fill.png` | code-referenced |
| `RpgUi/button/slider_handle.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/slider_handle.png` | code-referenced |
| `RpgUi/button/toggle_off.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/toggle_off.png` | code-referenced |
| `RpgUi/button/toggle_on.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/button/toggle_on.png` | code-referenced |

#### `RpgUi/bars/` — 6 files · tag **Shared** · coloured bar fills and frames (`RpgUiCatalog.cs:53`)

| Icon asset | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `RpgUi/bars/bar_fill_blue.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/bars/bar_fill_blue.png` | code-referenced |
| `RpgUi/bars/bar_fill_green.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/bars/bar_fill_green.png` | code-referenced |
| `RpgUi/bars/bar_fill_red.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/bars/bar_fill_red.png` | code-referenced |
| `RpgUi/bars/bar_frame_blue.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/bars/bar_frame_blue.png` | code-referenced |
| `RpgUi/bars/bar_frame_green.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/bars/bar_frame_green.png` | code-referenced |
| `RpgUi/bars/bar_frame_red.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/bars/bar_frame_red.png` | code-referenced |

#### `RpgUi/frame/` — 17 files · tag **Shared** · panel frames (`RpgUiCatalog.cs:61`)

| Icon asset | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `RpgUi/frame/frame_character.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/frame/frame_character.png` | code-referenced |
| `RpgUi/frame/frame_core.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/frame/frame_core.png` | code-referenced |
| `RpgUi/frame/frame_core_2.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/frame/frame_core_2.png` | code-referenced |
| `RpgUi/frame/frame_crafting.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/frame/frame_crafting.png` | code-referenced |
| `RpgUi/frame/frame_dialogue.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/frame/frame_dialogue.png` | code-referenced |
| `RpgUi/frame/frame_dialogue_2.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/frame/frame_dialogue_2.png` | code-referenced |
| `RpgUi/frame/frame_element.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/frame/frame_element.png` | code-referenced |
| `RpgUi/frame/frame_inventory.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/frame/frame_inventory.png` | code-referenced |
| `RpgUi/frame/frame_loot.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/frame/frame_loot.png` | code-referenced |
| `RpgUi/frame/frame_merchant.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/frame/frame_merchant.png` | code-referenced |
| `RpgUi/frame/frame_options.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/frame/frame_options.png` | code-referenced |
| `RpgUi/frame/frame_pet.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/frame/frame_pet.png` | code-referenced |
| `RpgUi/frame/frame_quest.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/frame/frame_quest.png` | code-referenced |
| `RpgUi/frame/frame_settings.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/frame/frame_settings.png` | code-referenced |
| `RpgUi/frame/frame_stats.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/frame/frame_stats.png` | code-referenced |
| `RpgUi/frame/frame_talent.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/frame/frame_talent.png` | code-referenced |
| `RpgUi/frame/frame_textbg.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/frame/frame_textbg.png` | code-referenced |

#### `RpgUi/panel/` — 11 files · tag **Shared** · panel bodies (`RpgUiCatalog.cs:58`)

| Icon asset | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `RpgUi/panel/panel_bar.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/panel/panel_bar.png` | code-referenced |
| `RpgUi/panel/panel_grid.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/panel/panel_grid.png` | code-referenced |
| `RpgUi/panel/panel_inventory.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/panel/panel_inventory.png` | code-referenced |
| `RpgUi/panel/panel_portrait.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/panel/panel_portrait.png` | code-referenced |
| `RpgUi/panel/panel_quest.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/panel/panel_quest.png` | code-referenced |
| `RpgUi/panel/panel_tab.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/panel/panel_tab.png` | code-referenced |
| `RpgUi/panel/panel_talent.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/panel/panel_talent.png` | code-referenced |
| `RpgUi/panel/panel_vendor.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/panel/panel_vendor.png` | code-referenced |
| `RpgUi/panel/panel_window.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/panel/panel_window.png` | code-referenced |
| `RpgUi/panel/panel_window_dark.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/panel/panel_window_dark.png` | code-referenced |
| `RpgUi/panel/profile_frame.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/panel/profile_frame.png` | code-referenced |

#### `RpgUi/slot/` — 22 files · tag **Shared** · inventory / talent slots + rarity rings (`RpgUiCatalog.cs:59`)

| Icon asset | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `RpgUi/slot/rarity_1.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/slot/rarity_1.png` | code-referenced |
| `RpgUi/slot/rarity_2.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/slot/rarity_2.png` | code-referenced |
| `RpgUi/slot/rarity_3.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/slot/rarity_3.png` | code-referenced |
| `RpgUi/slot/rarity_4.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/slot/rarity_4.png` | code-referenced |
| `RpgUi/slot/rarity_5.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/slot/rarity_5.png` | code-referenced |
| `RpgUi/slot/slot_action.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/slot/slot_action.png` | code-referenced |
| `RpgUi/slot/slot_armor.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/slot/slot_armor.png` | code-referenced |
| `RpgUi/slot/slot_armor_2.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/slot/slot_armor_2.png` | code-referenced |
| `RpgUi/slot/slot_character.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/slot/slot_character.png` | code-referenced |
| `RpgUi/slot/slot_item.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/slot/slot_item.png` | code-referenced |
| `RpgUi/slot/slot_socket.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/slot/slot_socket.png` | code-referenced |
| `RpgUi/slot/slot_talent.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/slot/slot_talent.png` | code-referenced |
| `RpgUi/slot/slot_talent_1.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/slot/slot_talent_1.png` | code-referenced |
| `RpgUi/slot/slot_talent_2.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/slot/slot_talent_2.png` | code-referenced |
| `RpgUi/slot/slot_talent_3.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/slot/slot_talent_3.png` | code-referenced |
| `RpgUi/slot/slot_talent_4.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/slot/slot_talent_4.png` | code-referenced |
| `RpgUi/slot/slot_talent_5.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/slot/slot_talent_5.png` | code-referenced |
| `RpgUi/slot/slot_talent_6.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/slot/slot_talent_6.png` | code-referenced |
| `RpgUi/slot/talent_1.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/slot/talent_1.png` | code-referenced |
| `RpgUi/slot/talent_2.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/slot/talent_2.png` | code-referenced |
| `RpgUi/slot/talent_3.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/slot/talent_3.png` | code-referenced |
| `RpgUi/slot/talent_4.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/slot/talent_4.png` | code-referenced |

#### `RpgUi/element/` — 30 files · tag **Shared** · small UI elements (`RpgUiCatalog.cs:69`)

| Icon asset | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `RpgUi/element/arrow_box.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/element/arrow_box.png` | code-referenced |
| `RpgUi/element/arrow_box_on.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/element/arrow_box_on.png` | code-referenced |
| `RpgUi/element/border_socket_1.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/element/border_socket_1.png` | code-referenced |
| `RpgUi/element/border_socket_2.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/element/border_socket_2.png` | code-referenced |
| `RpgUi/element/border_socket_3.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/element/border_socket_3.png` | code-referenced |
| `RpgUi/element/border_socket_4.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/element/border_socket_4.png` | code-referenced |
| `RpgUi/element/check.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/element/check.png` | code-referenced |
| `RpgUi/element/cross.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/element/cross.png` | code-referenced |
| `RpgUi/element/element_bar.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/element/element_bar.png` | code-referenced |
| `RpgUi/element/element_bar_1.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/element/element_bar_1.png` | code-referenced |
| `RpgUi/element/element_bar_5.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/element/element_bar_5.png` | code-referenced |
| `RpgUi/element/element_bar_5_fill.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/element/element_bar_5_fill.png` | code-referenced |
| `RpgUi/element/element_stat.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/element/element_stat.png` | code-referenced |
| `RpgUi/element/element_tab.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/element/element_tab.png` | code-referenced |
| `RpgUi/element/enchant_element.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/element/enchant_element.png` | code-referenced |
| `RpgUi/element/enchant_slot.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/element/enchant_slot.png` | code-referenced |
| `RpgUi/element/handle.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/element/handle.png` | code-referenced |
| `RpgUi/element/loading_bg.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/element/loading_bg.png` | code-referenced |
| `RpgUi/element/loading_fill.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/element/loading_fill.png` | code-referenced |
| `RpgUi/element/menu_btn_1.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/element/menu_btn_1.png` | code-referenced |
| `RpgUi/element/menu_btn_2.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/element/menu_btn_2.png` | code-referenced |
| `RpgUi/element/menu_btn_3.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/element/menu_btn_3.png` | code-referenced |
| `RpgUi/element/notif_1.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/element/notif_1.png` | code-referenced |
| `RpgUi/element/notif_2.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/element/notif_2.png` | code-referenced |
| `RpgUi/element/notif_4.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/element/notif_4.png` | code-referenced |
| `RpgUi/element/rotate.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/element/rotate.png` | code-referenced |
| `RpgUi/element/scroll_bg.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/element/scroll_bg.png` | code-referenced |
| `RpgUi/element/scroll_up.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/element/scroll_up.png` | code-referenced |
| `RpgUi/element/togglebox_off.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/element/togglebox_off.png` | code-referenced |
| `RpgUi/element/togglebox_on.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/element/togglebox_on.png` | code-referenced |

#### `RpgUi/crown/` — 4 files · tag **Shared** · tier crowns (`RpgUiCatalog.cs:66`)

| Icon asset | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `RpgUi/crown/crown_perfect.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/crown/crown_perfect.png` | code-referenced |
| `RpgUi/crown/tier1.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/crown/tier1.png` | code-referenced |
| `RpgUi/crown/tier2.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/crown/tier2.png` | code-referenced |
| `RpgUi/crown/tier3.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/crown/tier3.png` | code-referenced |

#### `RpgUi/badge/` — 1 files · tag **Shared** · level badge (`RpgUiCatalog.cs:56`)

| Icon asset | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `RpgUi/badge/badge_level.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/badge/badge_level.png` | code-referenced |

#### `RpgUi/decoration/` — 2 files · tag **Shared** · talent-tree decoration (role `decoration`, no C# constant)

| Icon asset | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `RpgUi/decoration/deco_talent_1.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/decoration/deco_talent_1.png` | code-referenced |
| `RpgUi/decoration/deco_talent_2.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/decoration/deco_talent_2.png` | code-referenced |

#### `RpgUi/silhouette/` — 3 files · tag **Shared** · character silhouettes (`RpgUiCatalog.cs:62`)

| Icon asset | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `RpgUi/silhouette/sil_female.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/silhouette/sil_female.png` | code-referenced |
| `RpgUi/silhouette/sil_male.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/silhouette/sil_male.png` | code-referenced |
| `RpgUi/silhouette/sil_pet.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/silhouette/sil_pet.png` | code-referenced |

#### `RpgUi/prefab_deps/` — 13 files · tag **Shared** · textures the mirrored Blink prefabs depend on (`Assets/Editor/BlinkPrefabMirror.cs:49-52`)

| Icon asset | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `RpgUi/prefab_deps/Chat_Element_1.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/prefab_deps/Chat_Element_1.png` | code-referenced |
| `RpgUi/prefab_deps/Chat_Element_2.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/prefab_deps/Chat_Element_2.png` | code-referenced |
| `RpgUi/prefab_deps/Chat_Element_3.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/prefab_deps/Chat_Element_3.png` | code-referenced |
| `RpgUi/prefab_deps/Chat_Element_4.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/prefab_deps/Chat_Element_4.png` | code-referenced |
| `RpgUi/prefab_deps/Diablo_Art_1.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/prefab_deps/Diablo_Art_1.png` | code-referenced |
| `RpgUi/prefab_deps/HUD_Art_1.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/prefab_deps/HUD_Art_1.png` | code-referenced |
| `RpgUi/prefab_deps/HUD_Art_2.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/prefab_deps/HUD_Art_2.png` | code-referenced |
| `RpgUi/prefab_deps/HUD_Art_3.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/prefab_deps/HUD_Art_3.png` | code-referenced |
| `RpgUi/prefab_deps/Minimap_Art.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/prefab_deps/Minimap_Art.png` | code-referenced |
| `RpgUi/prefab_deps/MinimapExample.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/prefab_deps/MinimapExample.png` | code-referenced |
| `RpgUi/prefab_deps/Orc_Race.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/prefab_deps/Orc_Race.png` | code-referenced |
| `RpgUi/prefab_deps/Tracking_Off.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/prefab_deps/Tracking_Off.png` | code-referenced |
| `RpgUi/prefab_deps/Tracking_On.png` | Shared | UI-kit lookup by name | `Assets/Resources/RpgUi/prefab_deps/Tracking_On.png` | code-referenced |



### 3.8 `Resources/HudIcons/` — 64 files

**This is the LEGACY icon path**, entirely separate from the Obsidian action bar and from
`ConceptIconResolver`. The per-class ability icons here are consumed by the legacy ATB battle HUD through
a hard-coded C# table: `Assets/_Modules/BattleATB/BattleHudUgui.cs:60` (`"HudIcons/" + key`), the table at
`:96-117`, `AbilityIconFor` at `:120-127`. Class portraits: `Assets/_Modules/Core/UI/ElarionUiKit.cs:2130-2142`.
Building-upgrade art: `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradePanelMvvm.cs:1608`.

**These are the only per-class icons in the repo whose tag comes from a folder name** - and here the
folder name IS the evidence, because `BattleHudUgui`s table is keyed by class and indexes into exactly
these folders. `HudIcons/Wizard/` is the MAGE set (same wizard/mage naming split as `Talents/wizard/`).
`HudIcons/Healer/` is the Cleric set - a fifth job the four-tag taxonomy does not cover.

| Icon asset | Tag | Used by | Citation | Status |
|---|---|---|---|---|
| `HudIcons/BuildingUpgrades/Arcane_Tower_T1_Arcane_Basics.jpg` | Shared | building-upgrade perk rows | `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradePanelMvvm.cs:1608` | referenced |
| `HudIcons/BuildingUpgrades/Arcane_Tower_T1_Mana_Attunement.jpg` | Shared | building-upgrade perk rows | `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradePanelMvvm.cs:1608` | referenced |
| `HudIcons/BuildingUpgrades/Arcane_Tower_T1_Warding_Runes.jpg` | Shared | building-upgrade perk rows | `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradePanelMvvm.cs:1608` | referenced |
| `HudIcons/BuildingUpgrades/Barracks_T1_Basic_Combat_Drill.jpg` | Shared | building-upgrade perk rows | `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradePanelMvvm.cs:1608` | referenced |
| `HudIcons/BuildingUpgrades/Barracks_T1_Expanded_Capacity.jpg` | Shared | building-upgrade perk rows | `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradePanelMvvm.cs:1608` | referenced |
| `HudIcons/BuildingUpgrades/Barracks_T1_Swift_Recruitment.jpg` | Shared | building-upgrade perk rows | `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradePanelMvvm.cs:1608` | referenced |
| `HudIcons/BuildingUpgrades/Blacksmith_T1_Reinforced_Plating.jpg` | Shared | building-upgrade perk rows | `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradePanelMvvm.cs:1608` | referenced |
| `HudIcons/BuildingUpgrades/Blacksmith_T1_Sharpened_Edges.jpg` | Shared | building-upgrade perk rows | `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradePanelMvvm.cs:1608` | referenced |
| `HudIcons/BuildingUpgrades/Blacksmith_T1_Sturdy_Shields.jpg` | Shared | building-upgrade perk rows | `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradePanelMvvm.cs:1608` | referenced |
| `HudIcons/BuildingUpgrades/Forge_T1_Efficient_Smelting.jpg` | Shared | building-upgrade perk rows | `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradePanelMvvm.cs:1608` | referenced |
| `HudIcons/BuildingUpgrades/Forge_T1_Quality_Forging.jpg` | Shared | building-upgrade perk rows | `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradePanelMvvm.cs:1608` | referenced |
| `HudIcons/BuildingUpgrades/Forge_T1_Resource_Conservation.jpg` | Shared | building-upgrade perk rows | `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradePanelMvvm.cs:1608` | referenced |
| `HudIcons/BuildingUpgrades/Lumber_Mill_T1_Construction_Aid.jpg` | Shared | building-upgrade perk rows | `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradePanelMvvm.cs:1608` | referenced |
| `HudIcons/BuildingUpgrades/Lumber_Mill_T1_Efficient_Processing.jpg` | Shared | building-upgrade perk rows | `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradePanelMvvm.cs:1608` | referenced |
| `HudIcons/BuildingUpgrades/Lumber_Mill_T1_Improved_Logging.jpg` | Shared | building-upgrade perk rows | `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradePanelMvvm.cs:1608` | referenced |
| `HudIcons/BuildingUpgrades/Upgrade.png` | Shared | building-upgrade perk rows | `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradePanelMvvm.cs:1608` | referenced |
| `HudIcons/Elarion.png` | Shared | HUD chrome | `Assets/Resources/HudIcons/Elarion.png` | referenced |
| `HudIcons/food.png` | Shared | HUD chrome | `Assets/Resources/HudIcons/food.png` | referenced |
| `HudIcons/Healer/healer.jpg` | Cleric | legacy ATB battle HUD ability / portrait table | `Assets/_Modules/BattleATB/BattleHudUgui.cs:96-127` | referenced |
| `HudIcons/Healer/Healer_Group_Heal.jpg` | Cleric | legacy ATB battle HUD ability / portrait table | `Assets/_Modules/BattleATB/BattleHudUgui.cs:96-127` | **ORPHAN** — no name reference found in any .cs or .json |
| `HudIcons/Healer/Healer_Heal.jpg` | Cleric | legacy ATB battle HUD ability / portrait table | `Assets/_Modules/BattleATB/BattleHudUgui.cs:96-127` | **ORPHAN** — no name reference found in any .cs or .json |
| `HudIcons/Healer/Healer_Holy.jpg` | Cleric | legacy ATB battle HUD ability / portrait table | `Assets/_Modules/BattleATB/BattleHudUgui.cs:96-127` | **ORPHAN** — no name reference found in any .cs or .json |
| `HudIcons/Healer/Healer_Smite.jpg` | Cleric | legacy ATB battle HUD ability / portrait table | `Assets/_Modules/BattleATB/BattleHudUgui.cs:96-127` | **ORPHAN** — no name reference found in any .cs or .json |
| `HudIcons/hud_build.jpg` | Shared | HUD chrome | `Assets/Resources/HudIcons/hud_build.jpg` | **ORPHAN** — no name reference found in any .cs or .json |
| `HudIcons/hud_compass.png` | Shared | HUD chrome | `Assets/Resources/HudIcons/hud_compass.png` | referenced |
| `HudIcons/hud_crystal.png` | Shared | HUD chrome | `Assets/Resources/HudIcons/hud_crystal.png` | referenced |
| `HudIcons/hud_food.png` | Shared | HUD chrome | `Assets/Resources/HudIcons/hud_food.png` | **ORPHAN** — no name reference found in any .cs or .json |
| `HudIcons/hud_gold.png` | Shared | HUD chrome | `Assets/Resources/HudIcons/hud_gold.png` | **ORPHAN** — no name reference found in any .cs or .json |
| `HudIcons/hud_heart.png` | Shared | HUD chrome | `Assets/Resources/HudIcons/hud_heart.png` | **ORPHAN** — no name reference found in any .cs or .json |
| `HudIcons/hud_intel.jpg` | Shared | HUD chrome | `Assets/Resources/HudIcons/hud_intel.jpg` | **ORPHAN** — no name reference found in any .cs or .json |
| `HudIcons/hud_invasion_handle.png` | Shared | HUD chrome | `Assets/Resources/HudIcons/hud_invasion_handle.png` | **ORPHAN** — no name reference found in any .cs or .json |
| `HudIcons/hud_invasion_medal.png` | Shared | HUD chrome | `Assets/Resources/HudIcons/hud_invasion_medal.png` | **ORPHAN** — no name reference found in any .cs or .json |
| `HudIcons/hud_invasion_medallion.png` | Shared | HUD chrome | `Assets/Resources/HudIcons/hud_invasion_medallion.png` | **ORPHAN** — no name reference found in any .cs or .json |
| `HudIcons/hud_inventory.jpg` | Shared | HUD chrome | `Assets/Resources/HudIcons/hud_inventory.jpg` | referenced |
| `HudIcons/hud_iron.png` | Shared | HUD chrome | `Assets/Resources/HudIcons/hud_iron.png` | referenced |
| `HudIcons/hud_music.png` | Shared | HUD chrome | `Assets/Resources/HudIcons/hud_music.png` | referenced |
| `HudIcons/hud_quest.png` | Shared | HUD chrome | `Assets/Resources/HudIcons/hud_quest.png` | referenced |
| `HudIcons/hud_raid.jpg` | Shared | HUD chrome | `Assets/Resources/HudIcons/hud_raid.jpg` | **ORPHAN** — no name reference found in any .cs or .json |
| `HudIcons/hud_settings.jpg` | Shared | HUD chrome | `Assets/Resources/HudIcons/hud_settings.jpg` | referenced |
| `HudIcons/hud_strip_bar.png` | Shared | HUD chrome | `Assets/Resources/HudIcons/hud_strip_bar.png` | **ORPHAN** — no name reference found in any .cs or .json |
| `HudIcons/hud_talk.jpg` | Shared | HUD chrome | `Assets/Resources/HudIcons/hud_talk.jpg` | referenced |
| `HudIcons/hud_wave_clock.png` | Shared | HUD chrome | `Assets/Resources/HudIcons/hud_wave_clock.png` | referenced |
| `HudIcons/hud_wave_plate.png` | Shared | HUD chrome | `Assets/Resources/HudIcons/hud_wave_plate.png` | **ORPHAN** — no name reference found in any .cs or .json |
| `HudIcons/hud_wood.png` | Shared | HUD chrome | `Assets/Resources/HudIcons/hud_wood.png` | referenced |
| `HudIcons/Knight/knight.jpg` | Knight | legacy ATB battle HUD ability / portrait table | `Assets/_Modules/BattleATB/BattleHudUgui.cs:96-127` | referenced |
| `HudIcons/Knight/Knight_Charge.jpg` | Knight | legacy ATB battle HUD ability / portrait table | `Assets/_Modules/BattleATB/BattleHudUgui.cs:96-127` | referenced |
| `HudIcons/Knight/Knight_Cleave.jpg` | Knight | legacy ATB battle HUD ability / portrait table | `Assets/_Modules/BattleATB/BattleHudUgui.cs:96-127` | referenced |
| `HudIcons/Knight/knight_parry.jpg` | Knight | legacy ATB battle HUD ability / portrait table | `Assets/_Modules/BattleATB/BattleHudUgui.cs:96-127` | referenced |
| `HudIcons/Knight/knight_thrust.jpg` | Knight | legacy ATB battle HUD ability / portrait table | `Assets/_Modules/BattleATB/BattleHudUgui.cs:96-127` | referenced |
| `HudIcons/player_frame_bg.png` | Shared | HUD chrome | `Assets/Resources/HudIcons/player_frame_bg.png` | referenced |
| `HudIcons/player_hp_fill.png` | Shared | HUD chrome | `Assets/Resources/HudIcons/player_hp_fill.png` | referenced |
| `HudIcons/player_mp_fill.png` | Shared | HUD chrome | `Assets/Resources/HudIcons/player_mp_fill.png` | referenced |
| `HudIcons/population.png` | Shared | HUD chrome | `Assets/Resources/HudIcons/population.png` | referenced |
| `HudIcons/Ranger/ranger.jpg` | Ranger | legacy ATB battle HUD ability / portrait table | `Assets/_Modules/BattleATB/BattleHudUgui.cs:96-127` | referenced |
| `HudIcons/Ranger/Ranger_Barrage.jpg` | Ranger | legacy ATB battle HUD ability / portrait table | `Assets/_Modules/BattleATB/BattleHudUgui.cs:96-127` | referenced |
| `HudIcons/Ranger/Ranger_Poison_Arrow.jpg` | Ranger | legacy ATB battle HUD ability / portrait table | `Assets/_Modules/BattleATB/BattleHudUgui.cs:96-127` | referenced |
| `HudIcons/Ranger/Ranger_Ranged_Attack.jpg` | Ranger | legacy ATB battle HUD ability / portrait table | `Assets/_Modules/BattleATB/BattleHudUgui.cs:96-127` | referenced |
| `HudIcons/Ranger/ranger_rapid_fire.jpg` | Ranger | legacy ATB battle HUD ability / portrait table | `Assets/_Modules/BattleATB/BattleHudUgui.cs:96-127` | referenced |
| `HudIcons/Upgrade.png` | Shared | HUD chrome | `Assets/Resources/HudIcons/Upgrade.png` | referenced |
| `HudIcons/Wizard/wizard.jpg` | Mage | legacy ATB battle HUD ability / portrait table | `Assets/_Modules/BattleATB/BattleHudUgui.cs:96-127` | referenced |
| `HudIcons/Wizard/Wizard_Fireball.jpg` | Mage | legacy ATB battle HUD ability / portrait table | `Assets/_Modules/BattleATB/BattleHudUgui.cs:96-127` | referenced |
| `HudIcons/Wizard/Wizard_Lightining.jpg` | Mage | legacy ATB battle HUD ability / portrait table | `Assets/_Modules/BattleATB/BattleHudUgui.cs:96-127` | referenced |
| `HudIcons/Wizard/Wizard_Meteor.jpg` | Mage | legacy ATB battle HUD ability / portrait table | `Assets/_Modules/BattleATB/BattleHudUgui.cs:96-127` | referenced |
| `HudIcons/Wizard/Wizard_Plasma.jpg` | Mage | legacy ATB battle HUD ability / portrait table | `Assets/_Modules/BattleATB/BattleHudUgui.cs:96-127` | referenced |


### 3.9 `Resources/ProjectileIcons/` — 2 sliced sheets · tag **Shared**

| Icon asset | Tag | Used by | Citation | Authored / fallback |
|---|---|---|---|---|
| `projectiles_arrows_magic.jpg` | Shared | `ProjectileArtCatalog` | `Assets/_Modules/Village/Buildings/ProjectileArtCatalog.cs:10,51` | authored (sheet path is a code constant) |
| `projectiles_spell_vfx_lifecycle.jpg` | Shared | `ProjectileArtCatalog` | `Assets/_Modules/Village/Buildings/ProjectileArtCatalog.cs:10,52` | authored (sheet path is a code constant) |

Sliced by `Assets/Editor/ProjectileArtSlicer.cs:5`. Sub-sprite names not enumerated here — they are
generated by the slicer, not authored in a catalog, and no catalog row addresses one by name.

---


## 4. Orphans — icon art on disk that nothing reaches

Split into two very different kinds, because the fix is completely different.

### 4.1 The 356 UNCURATED icons — fully wired, in the library, awaiting an owner pick

**These are not missing art, not missing wiring, and not a bug.** Each is named by an `iconPath` in
`Assets/StreamingAssets/Data/Canonical/weapons.json` or `armor.json` — the **library**. `CanonicalJson`
reads `Resources` first (`Assets/_Modules/Core/Data/CanonicalJson.cs:9-17`), and the `Resources` copy is
the **curated subset** produced by `GearCurationExporter` from `Assets/Editor/GearCurationPicks.json`.
So these rows do not execute *because nobody picked them yet* (§1.3).

**The gate is a creative decision, not an engineering one.** They are listed here so the choice is
visible and cheap to make, not because anything is broken.

The per-file rows are in §3.2 / §3.3, every one marked **LIBRARY-ONLY** with the exact StreamingAssets line that
names it. Summary by family:

| Family | Tag (from the `job` on the library row) | On disk | LIVE (curated) | LIBRARY-ONLY | Where the un-curated rows already live |
|---|---|---|---|---|---|
| `blink_axe1h` | Knight | 25 | 6 | **19** | `blink_axe1h` rows already authored + icon-pathed in the `StreamingAssets` library |
| `blink_axe2h` | Knight | 25 | 9 | **16** | `blink_axe2h` rows already authored + icon-pathed in the `StreamingAssets` library |
| `blink_bow2h` | Ranger | 25 | 15 | **10** | `blink_bow2h` rows already authored + icon-pathed in the `StreamingAssets` library |
| `blink_claws1h` | Knight | 25 | 0 | **25** | `blink_claws1h` rows already authored + icon-pathed in the `StreamingAssets` library |
| `blink_crossbow2h` | Ranger | 25 | 0 | **25** | `blink_crossbow2h` rows already authored + icon-pathed in the `StreamingAssets` library |
| `blink_dagger1h` | Ranger | 25 | 0 | **25** | `blink_dagger1h` rows already authored + icon-pathed in the `StreamingAssets` library |
| `blink_hammer2h` | Knight | 25 | 0 | **25** | `blink_hammer2h` rows already authored + icon-pathed in the `StreamingAssets` library |
| `blink_mace1h` | Cleric | 25 | 0 | **25** | `blink_mace1h` rows already authored + icon-pathed in the `StreamingAssets` library |
| `blink_polearm2h` | Knight | 25 | 0 | **25** | `blink_polearm2h` rows already authored + icon-pathed in the `StreamingAssets` library |
| `blink_scythe2h` | Knight | 25 | 0 | **25** | `blink_scythe2h` rows already authored + icon-pathed in the `StreamingAssets` library |
| `blink_shield1h` | Shared | 25 | 18 | **7** | `blink_shield1h` rows already authored + icon-pathed in the `StreamingAssets` library |
| `blink_spellbook1h` | Mage | 25 | 0 | **25** | `blink_spellbook1h` rows already authored + icon-pathed in the `StreamingAssets` library |
| `blink_staff2h` | Mage | 25 | 0 | **25** | `blink_staff2h` rows already authored + icon-pathed in the `StreamingAssets` library |
| `blink_sword1h` | Knight | 25 | 11 | **14** | `blink_sword1h` rows already authored + icon-pathed in the `StreamingAssets` library |
| `blink_sword2h` | Knight | 25 | 6 | **19** | `blink_sword2h` rows already authored + icon-pathed in the `StreamingAssets` library |
| `blink_wand1h` | Mage | 25 | 0 | **25** | `blink_wand1h` rows already authored + icon-pathed in the `StreamingAssets` library |
| `blink_armor` | Shared | 25 | 4 | **21** | `blink_armor_*` rows already authored + icon-pathed in the `StreamingAssets` library |
| **Total** | | **425** | **69** | **356** | |

> **To bring any of these into the game - the ONLY correct route:**
>
> 1. Add the id to `Assets/Editor/GearCurationPicks.json` with `"included": true`.
> 2. Run `Defenders/Gear/Export Curated Catalog -> Resources`
>    (`Assets/Editor/Catalog/GearCurationExporter.cs:64`).
> 3. Gate + verify. The exporter does an **additive merge**, so nothing already curated is lost.
>
> **Never hand-edit `Assets/Resources/Data/Canonical/weapons.json` or `armor.json`, and never copy the
> StreamingAssets file over them.** Both carry a DO-NOT-hand-edit banner (`weapons.json:2308`,
> `armor.json:500`); a hand-edit is silently reverted by the next export. The two copies are also not a
> clean superset relationship - live `weapons.json` carries `blink_shield1h_10/11/13/14/17` at different
> rarities than the library, which is the curation layer doing its job, not drift.

### 4.2 The 4 TRUE orphans — art on disk that no catalog anywhere names

These four PNGs are referenced by **zero** `iconPath` in either copy. In each case the matching catalog
row **already exists** and simply authors no `iconPath`, so the fix is one field per row.

| Icon asset | Tag | Why it is orphaned | Most likely intended consumer | Citation |
|---|---|---|---|---|
| `tripo_bow_a` | Ranger | its catalog row exists but authors no `iconPath` | add `"iconPath": "ItemIcons/tripo_bow_a"` to the row (and add the row to the LIVE copy, where it is absent entirely) | `Assets/StreamingAssets/Data/Canonical/weapons.json:10590` |
| `tripo_bow_b` | Ranger | its catalog row exists but authors no `iconPath` | add `"iconPath": "ItemIcons/tripo_bow_b"` to the row (and add the row to the LIVE copy, where it is absent entirely) | `Assets/StreamingAssets/Data/Canonical/weapons.json:10612` |
| `tripo_bow_c` | Ranger | its catalog row exists but authors no `iconPath` | add `"iconPath": "ItemIcons/tripo_bow_c"` to the row (and add the row to the LIVE copy, where it is absent entirely) | `Assets/StreamingAssets/Data/Canonical/weapons.json:10634` |
| `tripo_wand_a` | Mage | its catalog row exists but authors no `iconPath` | add `"iconPath": "ItemIcons/tripo_wand_a"` to the row (and add the row to the LIVE copy, where it is absent entirely) | `Assets/StreamingAssets/Data/Canonical/weapons.json:10656` |

Every one of the other 11 `tripo_*` PNGs already authors its `iconPath` and is LIVE. These four are the
stragglers from the same batch - the `tripo_bow_*` set is the exact gap that leaves the RANGER ladder
thinner than the knight and mage ones.

> **On `blink_bow2h_03`:** it was reported as unreferenced. **It is not.** It is a LIVE, authored,
> Ranger-tagged weapon row today — `Assets/Resources/Data/Canonical/weapons.json:1016` authors
> `"iconPath": "ItemIcons/blink_bow2h_03"` with `"job": "ranger"`, rarity common.
> Ten of its 25 siblings ARE orphaned-by-shelving — `_02 _06 _07 _10 _11 _12 _14 _15 _16 _22` — which is
> the true version of that observation, and `tripo_bow_a/b/c` (§4.2) is the other half of it.

### 4.3 `Resources/RpgUi/spellicons/` — 100 of 160 unreferenced

Nothing in any `.cs` or `.json` names these. `concept-icons.json` reaches only **20 distinct** spellicons
sprites; a further 40 appear as `blinkSource` **provenance** in `talent-icon-map.json` (which describes
the gitignored pack, not this folder - the shipped copy of those picks lives in `Resources/Talents/`).

**Most likely intended consumer for all 100: the 24 ability ids that have no `concept-icons` row
(§5.1).** The mage is the acute case - 20 idle `Pyromancer*` and 16 idle `Arcanist*` icons sit next to a
mage whose Q renders a sword.

| Class folder | Unreferenced | Referenced | Idle names |
|---|---|---|---|
| `Elementalist/Arcanist` | 4 | 16 | `Arcanist13`, `Arcanist14`, `Arcanist16`, `Arcanist19` |
| `Warrior/Barbarian` | 14 | 6 | `Barbarian2`, `Barbarian4`, `Barbarian7`, `Barbarian8`, `Barbarian9`, `Barbarian10`, `Barbarian11`, `Barbarian12`, `Barbarian14`, `Barbarian15`, `Barbarian16`, `Barbarian17`, `Barbarian19`, `Barbarian20` |
| `Warrior/Deathknight` | 19 | 1 | `Deathknight1`, `Deathknight2`, `Deathknight3`, `Deathknight4`, `Deathknight5`, `Deathknight6`, `Deathknight7`, `Deathknight8`, `Deathknight9`, `Deathknight10`, `Deathknight12`, `Deathknight13`, `Deathknight14`, `Deathknight15`, `Deathknight16`, `Deathknight17`, `Deathknight18`, `Deathknight19`, `Deathknight20` |
| `Elementalist/Electromancer` | 16 | 4 | `Electromancer2`, `Electromancer3`, `Electromancer5`, `Electromancer6`, `Electromancer7`, `Electromancer10`, `Electromancer11`, `Electromancer12`, `Electromancer13`, `Electromancer14`, `Electromancer15`, `Electromancer16`, `Electromancer17`, `Electromancer18`, `Electromancer19`, `Electromancer20` |
| `Warrior/Guardian` | 9 | 11 | `Guardian3`, `Guardian11`, `Guardian14`, `Guardian15`, `Guardian16`, `Guardian17`, `Guardian18`, `Guardian19`, `Guardian20` |
| `Assassin/Hunter` | 12 | 8 | `Hunter6`, `Hunter7`, `Hunter9`, `Hunter10`, `Hunter11`, `Hunter13`, `Hunter14`, `Hunter16`, `Hunter17`, `Hunter18`, `Hunter19`, `Hunter20` |
| `HolyDarkness/Paladin` | 11 | 9 | `Paladin3`, `Paladin4`, `Paladin6`, `Paladin7`, `Paladin9`, `Paladin13`, `Paladin14`, `Paladin16`, `Paladin17`, `Paladin18`, `Paladin20` |
| `Elementalist/Pyromancer` | 15 | 5 | `Pyromancer2`, `Pyromancer5`, `Pyromancer6`, `Pyromancer7`, `Pyromancer9`, `Pyromancer10`, `Pyromancer11`, `Pyromancer13`, `Pyromancer14`, `Pyromancer15`, `Pyromancer16`, `Pyromancer17`, `Pyromancer18`, `Pyromancer19`, `Pyromancer20` |

### 4.4 `Resources/RpgUi/` other roles — 31 unreferenced

| Icon asset | Tag | Why it matters | Most likely intended consumer |
|---|---|---|---|
| `RpgUi/abilities/heal_cross.png` | Shared | one of only **5** purpose-made ability sprites in the repo, and it is idle | the `heal` concept row (`concept-icons.json:17-20`), which currently points at the generic `spellicons/Paladin5` |
| `RpgUi/abilities/shield_bash.png` | Knight | purpose-made, named for a real knight skill, idle | `knight.shield-bash` (`concept-icons.json:93-96`), which currently points at `spellicons/Guardian1` - shared with the generic `knockback` |
| `RpgUi/emblem/Brawler.png` | Unassigned | Blink class emblem for a class we do not have | none - pack completeness only |
| `RpgUi/emblem/Shapeshifter.png` | Unassigned | as above | none - pack completeness only |
| `RpgUi/classslot/Slot2..Slot3` (2) | Shared | themed action-bar slot frames; only `Slot1` is referenced | the action-bar slot chrome (`ElarionUiKitObsidian` `ActionSlotHandle`) |
| `RpgUi/classslot/Slot_<Class>` (25) | Unassigned | one themed slot frame per Blink class, none consumed | a class-themed action bar; only 3 of the 25 classes map to our jobs at all |

### 4.5 `Resources/HudIcons/` — 15 unreferenced

| Icon asset | Tag | Most likely intended consumer |
|---|---|---|
| `Healer/Healer_Heal.jpg` | Cleric | `BattleHudUgui.AbilityIconBySlot` (`:96-117`) has entries for **Knight, Ranger, Mage only** — there is no `HeroClass.Cleric` row, so all four Healer ability icons are unreachable even though `Healer/healer.jpg` (the portrait) is used. **Correctly so:** Cleric is deliberately not a playable class (`FeatureFlags.cs:66`), so this is art ahead of a class that does not exist yet, not a wiring miss |
| `Healer/Healer_Group_Heal.jpg` | Cleric | as above |
| `Healer/Healer_Holy.jpg` | Cleric | as above |
| `Healer/Healer_Smite.jpg` | Cleric | as above |
| `hud_build.jpg` | Shared | the Build action-bar face - the bar now draws its faces from `hud-areas.json`, not these |
| `hud_food.png` / `hud_gold.png` / `hud_heart.png` | Shared | superseded by `RpgUi/currency/currency_*` via the `concept-icons` currency rows |
| `hud_intel.jpg` / `hud_raid.jpg` | Shared | retired action-bar faces |
| `hud_invasion_handle.png` / `hud_invasion_medal.png` / `hud_invasion_medallion.png` | Shared | invasion/raid banner chrome with no current call site |
| `hud_strip_bar.png` / `hud_wave_plate.png` | Shared | wave-HUD chrome with no current call site |

### 4.6 Sliced sub-sprites no branch ever selects — 13

| Sub-sprite(s) | Sheet | Tag | Why it is unreachable | Most likely intended consumer |
|---|---|---|---|---|
| `misc_r1c1`..`misc_r1c6`, `misc_r2c1`..`misc_r2c6` (12) | `0D5St.jpg` | Unassigned | **the whole sheet is absent from the `Sheets[]` array** (`ItemIconCatalog.cs:42-51`), so `EnsureLoaded` never calls `Resources.LoadAll` on it and none of its 12 names enters `_byName` | unknown — the generic `misc_rNcN` slice names carry no subject, so I cannot say what these are without looking at the image. **Honestly Unassigned.** Adding `"ItemIcons/0D5St"` to `Sheets[]` costs one line and would at least make them addressable |
| `pauldron_a` | `VxBVb.jpg` | Shared | `ForArmor` has branches for shield / helm / gauntlet / belt / chest (`:124-161`) but **no pauldron branch**, and no keyword list contains "pauldron" or "spaulder" | a shoulder-slot armour piece; there is no shoulder slot in `armor.json` today, so this is art ahead of the data |

**Note on method + its limit:** "unreferenced" here means *no name match in any `.cs` or `.json` under
`Assets/`*. A `.unity` scene or `.prefab` can reference a sprite by **GUID**, which this scan does not
follow. For `Resources/`-loaded art that is a small risk (the whole point of `Resources.Load` is a string
path), but it is a real one, and I would rather state the boundary than imply a certainty I do not have.

---

## 5. Missing — things that resolve to a fallback, or to nothing

### 5.1 Abilities — 24 of 42 have no `concept-icons` row

`ConceptIconResolver.Resolve` returns null **silently** when there is no row (`:86-87`, "misses are
expected/normal"), so every one of these is invisible in the log. This table is the only place they are
visible. Resolution order is `id` then `effect` then the hard default
(`HudModelProducers.cs:594-595`, `ElarionUiKitObsidian.cs:923`; §1.2).

| Ability id | Tag | `effect` | Row on the `id`? | What the player actually sees | Citation |
|---|---|---|---|---|---|
| `mage.fireball` | Mage | `strike` | no | `abilities/attack_sword` via the shared `strike` row (:25) | `Assets/Resources/Data/Canonical/abilities.json:17` |
| `mage.shell` | Mage | `shield` | no | `icons/icon_shield` via the shared `shield` row (:149) | `Assets/Resources/Data/Canonical/abilities.json:35` |
| `mage.drain` | Mage | `drainshot` | no | **`icons/icon_combat`** - the hard default; nothing matched | `Assets/Resources/Data/Canonical/abilities.json:50` |
| `mage.poison` | Mage | `dot` | no | `spellicons/Pyromancer1` via the shared `dot` row (:45) | `Assets/Resources/Data/Canonical/abilities.json:65` |
| `knight.q` | Knight | `dash` | **yes** | the literal TEXT "Dodge/Attack" - a knight-only override at `HudModelProducers.cs:602-606` that beats the icon entirely | `Assets/Resources/Data/Canonical/abilities.json:95` |
| `knight.w` | Knight | `knockback` | no | `spellicons/Guardian1` via the shared `knockback` row (:65) | `Assets/Resources/Data/Canonical/abilities.json:111` |
| `knight.e` | Knight | `gracebuff` | no | **`icons/icon_combat`** - the hard default; nothing matched | `Assets/Resources/Data/Canonical/abilities.json:127` |
| `knight.r` | Knight | `meteor` | no | `spellicons/Paladin11` via the shared `meteor` row (:41) | `Assets/Resources/Data/Canonical/abilities.json:146` |
| `ranger.q` | Ranger | `strike` | **yes** | `spellicons/Hunter12` (authored) | `Assets/Resources/Data/Canonical/abilities.json:176` |
| `ranger.w` | Ranger | `snare` | no | `spellicons/Hunter3` via the shared `snare` row (:29) | `Assets/Resources/Data/Canonical/abilities.json:194` |
| `ranger.healing-shot` | Ranger | `drainshot` | no | **`icons/icon_combat`** - the hard default; nothing matched | `Assets/Resources/Data/Canonical/abilities.json:212` |
| `ranger.r` | Ranger | `aoe` | no | `spellicons/Barbarian6` via the shared `aoe` row (:37) | `Assets/Resources/Data/Canonical/abilities.json:227` |
| `knight.ranged-poke` | Knight | `strike` | **yes** | `spellicons/Hunter8` (authored) | `Assets/Resources/Data/Canonical/abilities.json:251` |
| `knight.mending-salve` | Knight | `heal` | **yes** | `spellicons/Paladin5` (authored) | `Assets/Resources/Data/Canonical/abilities.json:269` |
| `knight.snare-arrow` | Knight | `snare` | **yes** | `spellicons/Hunter3` (authored) | `Assets/Resources/Data/Canonical/abilities.json:286` |
| `knight.suppressing-volley` | Knight | `cleave` | **yes** | `spellicons/Hunter15` (authored) | `Assets/Resources/Data/Canonical/abilities.json:304` |
| `knight.shield-bash` | Knight | `snare` | **yes** | `spellicons/Guardian1` (authored) | `Assets/Resources/Data/Canonical/abilities.json:321` |
| `knight.thunderbolt` | Knight | `strike` | **yes** | `spellicons/Electromancer9` (authored) | `Assets/Resources/Data/Canonical/abilities.json:337` |
| `knight.emberbrand-throw` | Knight | `dot` | **yes** | `spellicons/Pyromancer1` (authored) | `Assets/Resources/Data/Canonical/abilities.json:355` |
| `knight.wardens-roar` | Knight | `taunt` | **yes** | `spellicons/Barbarian5` (authored) | `Assets/Resources/Data/Canonical/abilities.json:376` |
| `knight.sweeping-cut` | Knight | `cleave` | **yes** | `spellicons/Barbarian13` (authored) | `Assets/Resources/Data/Canonical/abilities.json:394` |
| `knight.oathmend` | Knight | `healOverTime` | **yes** | `spellicons/Paladin19` (authored) | `Assets/Resources/Data/Canonical/abilities.json:410` |
| `knight.eternal-aegis` | Knight | `invuln` | **yes** | `spellicons/Guardian13` (authored) | `Assets/Resources/Data/Canonical/abilities.json:428` |
| `knight.second-wind` | Knight | `heal` | **yes** | `spellicons/Paladin12` (authored) | `Assets/Resources/Data/Canonical/abilities.json:445` |
| `knight.champions-combo` | Knight | `cleave` | **yes** | `spellicons/Barbarian18` (authored) | `Assets/Resources/Data/Canonical/abilities.json:462` |
| `mage.frost-nova` | Mage | `aoe` | no | `spellicons/Barbarian6` via the shared `aoe` row (:37) | `Assets/Resources/Data/Canonical/abilities.json:484` |
| `mage.arcane-bolt` | Mage | `strike` | no | `abilities/attack_sword` via the shared `strike` row (:25) | `Assets/Resources/Data/Canonical/abilities.json:502` |
| `mage.manaweave` | Mage | `manaweave` | no | **`icons/icon_combat`** - the hard default; nothing matched | `Assets/Resources/Data/Canonical/abilities.json:520` |
| `mage.void-rift` | Mage | `aoe` | no | `spellicons/Barbarian6` via the shared `aoe` row (:37) | `Assets/Resources/Data/Canonical/abilities.json:536` |
| `mage.blink` | Mage | `blink` | no | `spellicons/Deathknight11` via the shared `blink` row (:57) | `Assets/Resources/Data/Canonical/abilities.json:552` |
| `mage.cataclysm` | Mage | `meteor` | no | `spellicons/Paladin11` via the shared `meteor` row (:41) | `Assets/Resources/Data/Canonical/abilities.json:567` |
| `mage.thunder` | Mage | `strike` | no | `abilities/attack_sword` via the shared `strike` row (:25) | `Assets/Resources/Data/Canonical/abilities.json:583` |
| `mage.heal` | Mage | `heal` | no | `spellicons/Paladin5` via the shared `heal` row (:17) | `Assets/Resources/Data/Canonical/abilities.json:598` |
| `mage.meteor` | Mage | `meteor` | no | `spellicons/Paladin11` via the shared `meteor` row (:41) | `Assets/Resources/Data/Canonical/abilities.json:615` |
| `ranger.hunters-mark` | Ranger | `strike` | no | `abilities/attack_sword` via the shared `strike` row (:25) | `Assets/Resources/Data/Canonical/abilities.json:638` |
| `ranger.tumble-step` | Ranger | `blink` | no | `spellicons/Deathknight11` via the shared `blink` row (:57) | `Assets/Resources/Data/Canonical/abilities.json:653` |
| `ranger.multishot` | Ranger | `cleave` | no | `spellicons/Barbarian13` via the shared `cleave` row (:33) | `Assets/Resources/Data/Canonical/abilities.json:668` |
| `ranger.precision-strike` | Ranger | `strike` | no | `abilities/attack_sword` via the shared `strike` row (:25) | `Assets/Resources/Data/Canonical/abilities.json:683` |
| `ranger.storm-of-arrows` | Ranger | `aoe` | no | `spellicons/Barbarian6` via the shared `aoe` row (:37) | `Assets/Resources/Data/Canonical/abilities.json:698` |
| `universal.arcane-bolt` | Shared | `strike` | **yes** | `spellicons/Arcanist6` (authored) | `Assets/Resources/Data/Canonical/abilities.json:720` |
| `universal.mend` | Shared | `heal` | **yes** | `spellicons/Paladin15` (authored) | `Assets/Resources/Data/Canonical/abilities.json:738` |
| `universal.dash` | Shared | `blink` | **yes** | `spellicons/Deathknight11` (authored) | `Assets/Resources/Data/Canonical/abilities.json:755` |

**18 authored by id · 20 by shared effect row · 4 to the hard default.**

#### 5.1a The headline: the mage has no mage art

`mage.fireball` (`abilities.json:17`) has a `verb` ("Cast") but **no `concept-icons` row**, and there is
no `mage.q` row either. Its `effect` is `"strike"`, and `concept-icons.json:25-28` maps `strike` ->
`abilities/attack_sword`.

> **The mage Q therefore paints `Assets/Resources/RpgUi/abilities/attack_sword.png` - literally a sword.**
> The observation was correct; this is the mechanism. `verb` is never consulted for an icon
> (`HudModelProducers.cs:594` passes only `def.Id` and `def.Effect`); it only becomes the medallion
> caption at `HudKitController.cs:1669`, so the mage Q medallion reads "Cast" under a sword.

And it is not one slot. The whole mage default bar:

| Slot | Ability | Resolves to | Reads as |
|---|---|---|---|
| Q | `mage.fireball` | `abilities/attack_sword` (via `strike`) | a **sword** |
| W | `mage.shell` | `icons/icon_shield` (via `shield`, `concept-icons.json:149-152`) | a **shield** |
| E | `mage.drain` | `icons/icon_combat` - the hard default (no `drainshot` row) | **crossed swords** |
| R | `mage.poison` | `spellicons/Pyromancer1` (via `dot`) | the only mage-flavoured icon on the bar, and it is shared with `knight.emberbrand-throw` |

**Three of the mage's four default abilities render knight iconography.** Knight and ranger do not have
this problem: `knight.q` gets a text override and 11 knight skills author `id` rows; `ranger.q` authors
its own row (`concept-icons.json:89-92` → `spellicons/Hunter12`, the WO-1105 bow pick).

The art to fix it is already in the build: 20 `Pyromancer*` and 16 `Arcanist*` icons sit unreferenced in
`Resources/RpgUi/spellicons/` (§4.3). `HudIcons/Wizard/Wizard_Fireball.jpg` also exists — but that folder
is the legacy ATB path, not a `concept-icons` role, so the correct fix is a `spellicons` row in
`concept-icons.json`, not a `HudIcons` one.

### 5.2 Gear rows whose authored `iconPath` points at a file that does not exist

**Authored is not the same as resolving.** `Resources.Load<Sprite>` returns null for these and
`ItemIconCatalog` falls straight through to the keyword branch, exactly as if no `iconPath` had been
written - and nothing logs it (`ItemIconCatalog.cs:249-253` -> `:64-69` / `:113-118`).

| Row | Tag | Authored path | Actually shows | Citation |
|---|---|---|---|---|
| `armor_cloth` | Shared | `ItemIcons/armor_cloth` | `chest_a` (cloth/robe keyword, common) | `Assets/Resources/Data/Canonical/armor.json:19` |
| `armor_leather` | Shared | `ItemIcons/armor_leather` | `chest_b` (leather keyword, uncommon) | `armor.json:37` |
| `armor_chain` | Shared | `ItemIcons/armor_chain` | `chest_c` (chain keyword, rare) | `armor.json:55` |
| `armor_plate` | Shared | `ItemIcons/armor_plate` | `chest_e` (plate keyword, epic) | `armor.json:74` |
| `aegis_plate` | Shared | `ItemIcons/aegis_plate` | `chest_f` (plate keyword, legendary) | `armor.json:93` |
| `armor_mage_common` | **Mage** | `ItemIcons/armor_mage_common` | `chest_a` (armour catch-all, common) | `armor.json:308` |

The last one is the odd one out and the most fixable: `armor_mage_{uncommon,rare,epic,legendary}` all
exist on disk. **Only the `common` tier of the mage armour ladder was never rendered.** Knight and ranger
both have all five tiers. The same row is broken in the library copy too (`StreamingAssets/.../armor.json:184`).

### 5.3 Catalog rows with no `iconPath` at all

| Catalog | Rows with no `iconPath` | Ids | What they show instead |
|---|---|---|---|
| `weapons.json` | 20 | `mage_oak`, `mage_arcane`, `mage_void`, `knight_starter`, `knight_shield_starter`, `knight_iron`, `knight_oath`, `knight_dawn`, `ranger_starter`, `ranger_arrow_plain`, `ranger_arrow_fire`, `ranger_arrow_poison`, `ranger_arrow_frost`, `cleric_starter`, `aegis_emberbrand`, `aegis_heartwood_longbow`, `aegis_aetherstaff`, `aegis_hallowed_censer`, `tripo_axe_a`, `knight_flameblade` | keyword+rarity guess into `sword_t*`/`bow_t*`, or **null on purpose** for staves/wands (`ItemIconCatalog.cs:86-90`) |
| `materials.json` | 16 | `HealthHerb`, `BoneFragment`, `ManaCrystalShard`, `ArcaneDust`, `IronScrap`, `quench_oil`, `heartwood_core`, `reforged_steel`, `oathweld_plating`, `heartwood_bough`, `last_pressing`, `aether_catalyst`, `dry-reed`, `oil-soaked-cloth`, `ember-resin`, `ing_rough_stone` | `mat_*` sheet sprite chosen by the row's authored `category` (`ItemIconCatalog.cs:220-241`), or the row's glyph |
| `consumables.json` | 9 | `minor-heal-potion`, `greater-heal-potion`, `cons_mana_draught`, `traveler-rations`, `scout-tent-kit`, `cons_field_poultice`, `cons_hearthfire_stew`, `cons_wardens_campfire`, `cons_purifying_draught` | `ForConsumable` has **no authored step at all** (`ItemIconCatalog.cs:165`) - pure keyword match, so these are guesses even if an `iconPath` were added |
| `armor.json` | 0 | - | - |
| `accessories.json` | 0 | - | - |

> **`ForConsumable` is the one resolver with no authored-first step.** `ForWeapon`, `ForArmor` and
> `ForMaterial` all check `iconPath` first; `ForConsumable(id, name)` (`:165-198`) never does - it takes
> only an id and a name. So the 8 consumables that DO author an `iconPath`
> (`cons_mending_salve` and friends) get it honoured only through the generic `ItemVM.IconPath` path
> (`ElarionUiKitDetailCard.cs:257-258`), never through `GearIconCatalog.cs:43`. That asymmetry is worth
> knowing before anyone "fixes" a consumable icon by adding an `iconPath` and finding it ignored.

---

## 6. Collisions

One icon serving several distinct things. Not all of these are bugs - a generic verb concept sharing art
with the ability that has that shape is often deliberate. The ones that are probably unintended are
called out.

### 6.1 Probably UNINTENDED — the same ability drawn two different ways

**The sharpest finding in this section.** 16 abilities appear in BOTH the talent tree and the action bar,
and 15 of the 16 use a **different icon in each place**. The player learns the skill from one picture and
then hunts for a different picture on the bar.

| Ability | Tag | Talent-tree icon (`talent-icon-map.json`) | Action-bar icon (`concept-icons.json`) | Same? |
|---|---|---|---|---|
| `knight.thunderbolt` | Knight | `Talents/knight/knight_02` (from `Electromancer1`) | `spellicons/Electromancer9` | **NO** |
| `knight.mending-salve` | Knight | `Talents/knight/knight_04` (from `Priest4`) | `spellicons/Paladin5` | **NO** |
| `knight.ranged-poke` | Knight | `Talents/knight/knight_05` (from `Ranger8`) | `spellicons/Hunter8` | **NO** |
| `knight.shield-bash` | Knight | `Talents/knight/knight_06` (from `Guardian2`) | `spellicons/Guardian1` | **NO** |
| `knight.emberbrand-throw` | Knight | `Talents/knight/knight_07` (from `Pyromancer1`) | `spellicons/Pyromancer1` | YES |
| `knight.wardens-roar` | Knight | `Talents/knight/knight_08` (from `Barbarian3`) | `spellicons/Barbarian5` | **NO** |
| `knight.snare-arrow` | Knight | `Talents/knight/knight_09` (from `Hunter8`) | `spellicons/Hunter3` | **NO** |
| `knight.suppressing-volley` | Knight | `Talents/knight/knight_11` (from `Guardian5`) | `spellicons/Hunter15` | **NO** |
| `knight.oathmend` | Knight | `Talents/knight/knight_12` (from `Priest2`) | `spellicons/Paladin19` | **NO** |
| `knight.sweeping-cut` | Knight | `Talents/knight/knight_15` (from `Barbarian1`) | `spellicons/Barbarian13` | **NO** |
| `knight.eternal-aegis` | Knight | `Talents/knight/knight_16` (from `Guardian4`) | `spellicons/Guardian13` | **NO** |
| `knight.second-wind` | Knight | `Talents/knight/knight_17` (from `Priest1`) | `spellicons/Paladin12` | **NO** |
| `knight.champions-combo` | Knight | `Talents/knight/knight_20` (from `Berserker4`) | `spellicons/Barbarian18` | **NO** |
| `universal.arcane-bolt` | Shared | `Talents/shared/shared_09` (from `Arcanist17`) | `spellicons/Arcanist6` | **NO** |
| `universal.mend` | Shared | `Talents/shared/shared_10` (from `Priest5`) | `spellicons/Paladin15` | **NO** |
| `universal.dash` | Shared | `Talents/shared/shared_11` (from `Rogue4`) | `spellicons/Deathknight11` | **NO** |

**15 diverge, 1 agree.** Only `knight.emberbrand-throw` uses one picture in both places
(`Pyromancer1`). Every citation above is `Assets/Resources/Data/Canonical/talent-icon-map.json` (by skill
id) and `.../concept-icons.json` (by ability id).

> **Root cause, and it is structural rather than careless.** The two files draw from different pools.
> `talent-icon-map.json` can name **any** class in the Blink pack, because `TalentIconImporter` copies that
> one PNG into `Resources/Talents/` — which is how `Priest4`, `Rogue4`, `Berserker4` and `Ranger8` reach a
> build. `concept-icons.json` can only name a class that `BlinkIconImporter.cs:94` mirrored wholesale into
> `Resources/RpgUi/spellicons/`, and **only 8 of the 25 classes were mirrored** (§3.6). So when the talent
> author picked `Priest4` for Mending Salve, the bar author literally could not match it and reached for the
> nearest mirrored equivalent, `Paladin5`. Seven of the fifteen divergences are exactly this
> (`Priest1/2/4/5`, `Rogue4`, `Berserker4`, `Ranger8` — all unmirrored classes).
>
> That makes this a *presentation* inconsistency across two independently-authored data files, not a code
> bug: neither file is wrong on its own terms, they were simply never reconciled and no oracle compares
> them. Two clean fixes exist — mirror the remaining classes into `spellicons`, or point `concept-icons` at
> the already-mirrored `Talents/*` copy. A regression asserting
> `talent(abilityId).icon == concept(abilityId).icon` would pin it permanently either way.

### 6.2 Probably INTENDED — generic verb sharing art with its one implementing ability

These are one-icon-two-keys by design: a category verb plus the single skill that has that shape.

| Icon | Serves | Verdict |
|---|---|---|
| `spellicons/Paladin5` | `heal` (:17) + `knight.mending-salve` (:77) | intended - but see `RpgUi/abilities/heal_cross.png` sitting unused (4.4) |
| `spellicons/Hunter3` | `snare` (:29) + `knight.snare-arrow` (:81) | intended |
| `spellicons/Barbarian13` | `cleave` (:33) + `knight.sweeping-cut` (:109) | intended |
| `spellicons/Paladin19` | `healovertime` (:49) + `knight.oathmend` (:113) | intended |
| `spellicons/Guardian13` | `invuln` (:53) + `knight.eternal-aegis` (:117) | intended |
| `spellicons/Guardian1` | `knockback` (:65) + `knight.shield-bash` (:93) | **questionable** - a purpose-made `RpgUi/abilities/shield_bash.png` exists and is unused (4.4) |
| `spellicons/Pyromancer1` | `dot` (:45) + `knight.emberbrand-throw` (:101) | intended for knight - but `mage.poison` also lands here via `dot`, so a mage ULTIMATE shares art with a knight tier-2 throw |
| `spellicons/Deathknight11` | `blink` (:57) + `universal.dash` (:141) | intended - but `mage.blink` AND `ranger.tumble-step` also land here via `blink`, so three distinct movement skills share one picture |
| `icons/icon_sword` | `thrust` (:9) + `sword` (:145) | intended (verb + noun alias) |
| `icons/icon_shield` | `parry` (:13) + `shield` (:149) | intended (verb + noun alias) |
| `abilities/charge_knight` | `charge` (:21) + `knight.q` (:129) | intended |
| `abilities/attack_sword` | `strike` (:25) + `attack` (:237) | intended as an alias - but see 6.3 |
| `icons/icon_inventory` | `inventory` (:185) + `bag` (:189) | intended (synonym alias) |
| `potion/potion_health` | `potion` (:173) + `elixir` (:177) | intended (synonym alias) |
| `currency/currency_*` (5) | singular + plural key for each (`wood`/`woods`, `iron`/`irons`, `food`/`foods`, `gold`/`golds`, `crystal`/`crystals`) | intended (pluralisation alias) |

### 6.3 The worst collision: `abilities/attack_sword` serves 8 distinct abilities

Because it backs the `strike` effect, every ability whose effect is `strike` and whose id has no row of
its own lands on the same sprite:

| Ability | Tag | Why it lands here |
|---|---|---|
| `mage.fireball` | Mage | no id row, effect `strike` |
| `mage.arcane-bolt` | Mage | no id row, effect `strike` |
| `mage.thunder` | Mage | no id row, effect `strike` |
| `ranger.hunters-mark` | Ranger | no id row, effect `strike` |
| `ranger.precision-strike` | Ranger | no id row, effect `strike` |
| `ranger.q` | Ranger | **escapes** - authors its own row (`spellicons/Hunter12`) |
| `knight.ranged-poke` | Knight | **escapes** - authors its own row (`spellicons/Hunter8`) |
| `knight.thunderbolt` | Knight | **escapes** - authors its own row (`spellicons/Electromancer9`) |
| the `attack` concept key | Shared | direct alias (`concept-icons.json:237-240`) |

**Five abilities across two classes, all drawn as one sword.** Same pattern, smaller, for `aoe` ->
`spellicons/Barbarian6` (`ranger.r`, `mage.frost-nova`, `mage.void-rift`, `ranger.storm-of-arrows` = 4
abilities across 2 classes) and `meteor` -> `spellicons/Paladin11` (`knight.r`, `mage.cataclysm`,
`mage.meteor` = 3 across 2 classes).

### 6.4 Non-collisions worth recording (so nobody re-checks)

- **`talent-icon-map.json` is collision-free**: 83 skills, 83 distinct `blinkSource`, 83 distinct
  `iconPath`. WO-1023 fixed the two that once duplicated (`Rogue7` and `Arcanist1`), and the reasoning is
  recorded inline at `talent-icon-map.json:278` and `:572`.
- **`ItemIcons` stems are 1:1 with catalog ids** - no two rows share a PNG, in either copy.
- **The `sword_t1..t5` / `chest_a..f` sheet sprites are collisions by design** - they are the fallback
  tier ladder, not identities. Every weapon of a given rarity that isn't authored lands on the same one;
  that IS the fallback working as written (ItemIconCatalog.cs:321-330), and it is precisely why 
  the authored ratio as the health metric.

---






---

## 7. How to add an icon correctly

**The rule in one line: author an `iconPath`. Never rely on the keyword fallback.**

### 7.1 A gear item (weapon / armor / accessory)

1. Put the PNG in `Assets/Resources/ItemIcons/`. Name it **exactly the row's `id`** — the whole
   catalog's convention, and it is what makes an orphan detectable by a name diff.
2. Set `"iconPath": "ItemIcons/<id>"` on the row (Resources-relative, **no extension** —
   `ItemIconCatalog.cs:249-253` calls `Resources.Load<Sprite>` with it verbatim).
3. **Which file you edit depends on the catalog — this is the step people get wrong.**

   | Catalog | Edit | Then |
   |---|---|---|
   | `weapons.json`, `armor.json` | `Assets/StreamingAssets/...` (the library) **only** | add the id to `Assets/Editor/GearCurationPicks.json` with `"included": true` and run `Defenders/Gear/Export Curated Catalog -> Resources`. **The `Resources` copy is generated — hand-edits are silently reverted** (`weapons.json:2308`, `armor.json:500`). |
   | `accessories.json`, `materials.json`, `consumables.json`, `concept-icons.json`, `abilities.json`, `hero-talents.json`, `talent-icon-map.json` | **both** copies, kept identical | Resources wins at load (§1.3); StreamingAssets is the desktop fallback. Editing only one leaves a build-dependent bug. |
4. Do **not** name a weapon "…blade" and hope. The keyword branch (`ItemIconCatalog.cs:93-96`) maps
   every sword/axe/hammer/mace to the same 5 `sword_t*` sprites; two different epics become the same
   picture, and the player reads that as a bug.
5. Staves and wands with no `iconPath` return **null on purpose** (`:86-90`) and draw the ✦ glyph.
   That is a designed honest-blank, not a miss — but an authored icon beats it every time.

### 7.2 An ability icon (action bar)

1. Add a row keyed by the **ability id**, not the effect:
   `"mage.fireball": { "role": "spellicons", "name": "Pyromancer12" }` in
   `Assets/Resources/Data/Canonical/concept-icons.json` (and the StreamingAssets twin).
2. `role` is a folder under `Resources/RpgUi/` — the mapping is generic, `role X → Resources/RpgUi/X`
   (`RpgUiCatalog.cs:44,324`). `name` is the file's stem.
3. The art must already be mirrored into `Resources/RpgUi/spellicons/<Group>/<Class>/` by
   `Assets/Editor/BlinkIconImporter.cs:94`. Only 8 of the 25 Blink classes are mirrored today (§3.6) —
   picking a `Cultist*`/`Rogue*`/`Ranger*` name will resolve to null and silently fall to the default.
4. **Pick by silhouette, never by colour** — the owner is red/green colourblind; that constraint is
   written into `concept-icons.json:3` and every existing pick names its shape. Greyscale check is the
   gate.
5. Only add an `"override": true` if the concept must beat a caller's own richer art
   (`ConceptIconResolver.cs:136-152`). Default is absent/false.

### 7.3 A talent node icon

1. Add the skill to `Assets/Resources/Data/Canonical/talent-icon-map.json` with `iconPath`
   (`Talents/<tree>/<tree>_NN`), `blinkSource` (the pack-relative source PNG) and a `why` naming the
   **silhouette**.
2. Run `tools/apply_talent_icon_map.py` (named at `talent-icon-map.json:5`), which stamps the
   `iconPath` into `hero-talents.json`, then `Assets/Editor/TalentIconImporter.cs:13-14` mirrors the
   art into `Resources/Talents/`.
3. **`blinkSource` must stay unique.** It is 1:1 across all 83 skills today; WO-1023 exists purely
   because two nodes once shared one, and a duplicated icon is a recognition failure.

### 7.4 The three things that silently swallow an icon

| Symptom | Real cause | Where |
|---|---|---|
| Item shows a generic sword/potion | no `iconPath` → keyword+rarity guess | `ItemIconCatalog.cs:71-105` |
| Ability shows the wrong-class art | no `id` row → `effect` row shared with every ability of that shape | `HudModelProducers.cs:594` |
| Ability shows the crossed-swords blob | no `id` row **and** no `effect` row → `DefaultSprite()` | `ElarionUiKitObsidian.cs:923` |

None of these logs an error. `ConceptIconResolver.Resolve` returns null *silently* by design
(`:86-87`, "misses are expected/normal"), and `ItemIconCatalog`'s fallback is a successful return, not
a warning. **A missing icon is invisible to the log — only this registry makes it visible.**

---

## 8. Maintenance

Per §15 of `CLAUDE.md`, update this file in the same breath as the change. Re-derive it with:

- file lists — `Get-ChildItem Assets/Resources/{ItemIcons,RpgUi,Talents,HudIcons,ProjectileIcons} -Recurse -Filter *.png`
- sheet sub-sprites — `Select-String '^\s+name:\s+(.+)$'` over each `*.jpg.meta` (the spritesheet rects
  carry the authored names; this is how §3.4 was enumerated without opening Unity)
- catalog references — `Select-String '"iconPath"\s*:\s*"ItemIcons/([^"]+)"'` over
  `Assets/Resources/Data/Canonical/*.json`

The one check worth automating into a regression: **every authored `iconPath` resolves to a file on
disk**, which would have caught the 6 broken armor rows in §5.2 the day they were written.
