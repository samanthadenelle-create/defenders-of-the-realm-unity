# WEAPON CATALOG — the consolidated, source-cited weapon registry

**Built:** 2026-08-16 · **Branch:** `wip/village2-and-f8-tickets` · **Method:** read-only enumeration of the
runtime data + the resolver code. Every fact below carries its `file:line` so any single row is
re-verifiable at a glance (project memory `audit-outputs-as-known-dictionaries`).

**Scope of "weapon":** every row in the runtime weapon catalog (`weapons.json`), which includes
shields and arrows because the schema carries them as weapon rows — plus the accessory ladder
(`accessories.json`) as a separate table. **96 weapon rows. 10 accessories.**

> **⚠ THIS IS A DERIVED REGISTRY, NOT A DESIGN DOC.** Where the code and a name disagree, the code
> wins and the row says so. Where source is silent the tag is `Unassigned`, never a guess.

---

## 0. The four authorities (read these before trusting any other doc)

| Authority | What it decides | Path |
|---|---|---|
| Runtime weapon catalog | which weapons exist at all | `Assets/Resources/Data/Canonical/weapons.json` |
| Mesh resolver | which mesh a row puts in the hand | `Assets/_Modules/Village/Hero/EquipmentController.cs:201-229` (IdMap), `:3116-3193` (fallback chain) |
| Icon resolver | which picture the bag shows | `Assets/_Modules/Village/Hero/ItemIconCatalog.cs:58-106` |
| Obtainability | whether a player can ever hold it | `vendors.json` + `VendorStockResolver.cs:528-580` · `gear-recipes.json` · `GearLoadout.cs:76-101` · `BattleArena.cs:2992-3005` / `EnemyOutpost.cs:784-798` |

### 0.1 ⚠ The Resources copy is the runtime catalog; StreamingAssets is a 435-row LIBRARY

`Assets/Resources/Data/Canonical/weapons.json` has **96** rows.
`Assets/StreamingAssets/Data/Canonical/weapons.json` has **435** rows.
They are **not** in sync, and that is by design: `CanonicalJson` loads **Resources first, StreamingAssets
only as a fallback** (`Assets/_Modules/Core/Data/CanonicalJson.cs:9-17`), and the Resources file's own
`_generated` banner names it as the curated output — *"GearCurationExporter (additive merge) from
StreamingAssets library + GearCurationPicks.json — DO NOT hand-edit"* (`weapons.json:2308`).

**Consequence:** the 339 StreamingAssets-only ids are **not in the game**. They include entire weapon
families the player can never see — `blink_claws1h_*`, `blink_crossbow2h_*`, `blink_polearm2h_*`,
`blink_scythe2h_*`, `blink_spellbook1h_*`, `blink_mace1h_*`, `blink_hammer2h_*`, `blink_wand1h_*`,
`blink_dagger1h_*`, `blink_staff2h_*` (25 each), plus `tripo_bow_a/b/c` and `tripo_wand_a`.
Everything in the table below is the **96-row runtime set**.

### 0.2 Class tags used here

Derived from the row's `job` field only (`GearCatalog.WeaponFitsClass`, `GearCatalog.cs:413`):
`knight` → **Knight** · `ranger` → **Ranger** · `mage` → **Mage** · `any` → **Shared** ·
`cleric` → **Cleric\*** · absent → **Unassigned**.

**\*Cleric is not a playable class.** The playable roster is Knight/Ranger/Mage and *"CLERIC STAYS OUT
deliberately … it has no authored kit"* (`Assets/_Modules/Core/FeatureFlags.cs:66`, pointing at
`DeNelle.Core.State.PlayableHeroes`). The two Cleric rows are therefore unreachable-by-roster in
practice even where a mechanism nominally grants them. **No row in the catalog is `Unassigned`** —
every one of the 96 authors a `job`.

---

## 1. THE CATALOG — 96 weapon rows

Columns: **id · display name · class · kind · rarity · mesh it resolves to · icon it resolves to ·
where it is obtained · citation.**

*Reading the mesh column:* `` `Axe1h_12` (Addr) `` = an Addressables prefab under address
`gear/weapon/…` (`EquipmentController.cs:831-836`, `:3163-3176`). A bare name = a Resources prop
under `Heroes/Props/Weapons/` (`EquipmentController.cs:66`).

*Reading the icon column:* `` `ItemIcons/x` (authored) `` = the row's own `iconPath`, which
`ItemIconCatalog.ForWeapon` treats as authoritative (`ItemIconCatalog.cs:64-69`). Anything else is the
**keyword+rarity guess** computed from id+name (`ItemIconCatalog.cs:72-105`) — `sheet sword_t3` means
"tier-3 sprite off the sword sheet `ItemIcons/Ud37F`", `sheet bow_t3` off `ItemIcons/inEJH`, and
`NONE -> emoji glyph` means the resolver deliberately returns null and the UI paints the row's emoji
(`ItemIconCatalog.cs:86-90`).

*Reading the obtained column:* `starter` = the authored opening kit; `shop` = survives the Forge's
per-level cap for at least one class (see §5); `craft` = a `gear-recipes.json` `outputGearId`;
`drop` = can win the arena/outpost rarity roll for some class+level (see §5.3);
**UNREACHABLE** = none of the four.

| id | name | class | kind | rarity | mesh | icon | obtained | citation |
|---|---|---|---|---|---|---|---|---|
| `mage_oak` | Oakheart Staff | Mage | staff | uncommon | `staff_A` | NONE -> emoji glyph | shop + drop | weapons.json:6 · mesh EquipmentController.cs:206 |
| `mage_arcane` | Arcane Scepter | Mage | staff | rare | `staff_B` | NONE -> emoji glyph | shop + drop | weapons.json:21 · mesh EquipmentController.cs:207 |
| `mage_void` | Voidcaller Staff | Mage | staff | epic | `staff_C` | NONE -> emoji glyph | shop + drop | weapons.json:38 · mesh EquipmentController.cs:208 |
| `knight_starter` | Squire's Blade | Knight | sword | common | `sword_A` | sheet sword_t1 | starter + drop | weapons.json:55 · mesh EquipmentController.cs:212 |
| `knight_shield_starter` | Squire's Heater | Knight | shield | common | `shield_A` | sheet sword_t1 | starter | weapons.json:70 · mesh EquipmentController.cs:3130-3153 |
| `knight_iron` | Iron Longsword | Knight | sword | uncommon | `sword_D` | sheet sword_t2 | craft + drop | weapons.json:85 · mesh EquipmentController.cs:213 |
| `knight_oath` | Oathkeeper | Knight | sword | rare | `sword_F` | sheet sword_t3 | craft + drop | weapons.json:100 · mesh EquipmentController.cs:214 |
| `knight_dawn` | Dawnbreaker | Knight | sword | epic | `sword_G` | sheet sword_t4 | drop | weapons.json:117 · mesh EquipmentController.cs:215 |
| `ranger_starter` | Hunter's Shortbow | Ranger | bow | common | `bow_A` | sheet bow_t1 | starter + drop | weapons.json:134 · mesh EquipmentController.cs:221 |
| `ranger_arrow_plain` | Field Arrows | Ranger | arrow | common | `bow_A` | sheet bow_t1 | **UNREACHABLE** | weapons.json:148 · mesh EquipmentController.cs:3130-3153 |
| `ranger_arrow_fire` | Emberhead | Ranger | arrow | uncommon | `bow_A` | sheet bow_t2 | drop | weapons.json:165 · mesh EquipmentController.cs:3130-3153 |
| `ranger_arrow_poison` | Venomtip | Ranger | arrow | uncommon | `bow_A` | sheet bow_t2 | **UNREACHABLE** | weapons.json:186 · mesh EquipmentController.cs:3130-3153 |
| `ranger_arrow_frost` | Rimeshot | Ranger | arrow | uncommon | `bow_A` | sheet bow_t2 | **UNREACHABLE** | weapons.json:207 · mesh EquipmentController.cs:3130-3153 |
| `cleric_starter` | Acolyte's Mace | Cleric* | sword | common | `sword_A` | sheet sword_t1 | drop | weapons.json:228 · mesh EquipmentController.cs:3130-3153 |
| `aegis_emberbrand` | Emberbrand, the Rekindled | Knight | sword | legendary | `sword_G` | sheet sword_t5 | shop + craft + drop | weapons.json:243 · mesh EquipmentController.cs:216 |
| `aegis_heartwood_longbow` | Heartwood Longbow | Ranger | bow | legendary | `bow_C` | sheet bow_t5 | shop + craft + drop | weapons.json:261 · mesh EquipmentController.cs:225 |
| `aegis_aetherstaff` | Aetherstaff | Mage | staff | legendary | `staff_D` | NONE -> emoji glyph | shop + craft + drop | weapons.json:278 · mesh EquipmentController.cs:209 |
| `aegis_hallowed_censer` | The Hallowed Censer | Cleric* | hammer | legendary | `hammer_A` | NONE -> emoji glyph | shop + craft + drop | weapons.json:296 · mesh EquipmentController.cs:228 |
| `tripo_axe_a` | Reaver's Hatchet | Knight | axe | common | `axe_A` | sheet sword_t1 | **UNREACHABLE** | weapons.json:314 (row+prefabPath) |
| `tripo_dagger_a` | Bramblefang | Ranger | dagger | uncommon | `dagger_A` | `ItemIcons/tripo_dagger_a` (authored) | drop | weapons.json:337 (row+prefabPath) |
| `tripo_hammer_a` | Wardstone Maul | Knight | hammer | epic | `hammer_A` | `ItemIcons/tripo_hammer_a` (authored) | **UNREACHABLE** | weapons.json:361 (row+prefabPath) |
| `tripo_shield_a` | Oakband Heater | Shared | shield | common | `shield_A` | `ItemIcons/tripo_shield_a` (authored) | drop | weapons.json:386 (row+prefabPath) |
| `tripo_staff_a` | Emberglass Staff | Mage | staff | common | `staff_A` | `ItemIcons/tripo_staff_a` (authored) | **UNREACHABLE** | weapons.json:411 (row+prefabPath) |
| `tripo_staff_b` | Tideglass Staff | Mage | staff | uncommon | `staff_B` | `ItemIcons/tripo_staff_b` (authored) | shop | weapons.json:434 (row+prefabPath) |
| `tripo_staff_c` | Sparkwood Staff | Mage | staff | rare | `staff_C` | `ItemIcons/tripo_staff_c` (authored) | shop | weapons.json:457 (row+prefabPath) |
| `tripo_staff_d` | Heartglass Rod | Mage | staff | common | `staff_D` | `ItemIcons/tripo_staff_d` (authored) | **UNREACHABLE** | weapons.json:482 (row+prefabPath) |
| `tripo_sword_a` | Wardens' Edge | Knight | sword | common | `sword_A` | `ItemIcons/tripo_sword_a` (authored) | **UNREACHABLE** | weapons.json:505 (row+prefabPath) |
| `tripo_sword_d` | Footman's Cut | Knight | sword | uncommon | `sword_D` | `ItemIcons/tripo_sword_d` (authored) | **UNREACHABLE** | weapons.json:529 (row+prefabPath) |
| `tripo_sword_f` | Vigil Longsword | Knight | sword | rare | `sword_F` | `ItemIcons/tripo_sword_f` (authored) | **UNREACHABLE** | weapons.json:553 (row+prefabPath) |
| `tripo_sword_g` | Dawnward Greatblade | Knight | sword | epic | `sword_G` | `ItemIcons/tripo_sword_g` (authored) | **UNREACHABLE** | weapons.json:578 (row+prefabPath) |
| `blink_axe1h_12` | Reaver's Hatchet | Knight | axe | common | `Axe1h_12` (Addr) | `ItemIcons/blink_axe1h_12` (authored) | shop | weapons.json:603 (row+prefabPath) |
| `blink_axe1h_14` | Woodsman's Axe | Knight | axe | common | `Axe1h_14` (Addr) | `ItemIcons/blink_axe1h_14` (authored) | **UNREACHABLE** | weapons.json:628 (row+prefabPath) |
| `blink_axe1h_16` | Footman's Waraxe | Knight | axe | uncommon | `Axe1h_16` (Addr) | `ItemIcons/blink_axe1h_16` (authored) | shop | weapons.json:653 (row+prefabPath) |
| `blink_axe1h_18` | Ironband Hatchet | Knight | axe | uncommon | `Axe1h_18` (Addr) | `ItemIcons/blink_axe1h_18` (authored) | shop | weapons.json:678 (row+prefabPath) |
| `blink_axe1h_20` | Emberbite Axe | Knight | axe | rare | `Axe1h_20` (Addr) | `ItemIcons/blink_axe1h_20` (authored) | shop | weapons.json:703 (row+prefabPath) |
| `blink_axe1h_24` | Cinderfall Waraxe | Knight | axe | epic | `Axe1h_24` (Addr) | `ItemIcons/blink_axe1h_24` (authored) | shop | weapons.json:730 (row+prefabPath) |
| `blink_axe2h_04` | Splitting Maul | Knight | axe | common | `Axe2h_04` (Addr) | `ItemIcons/blink_axe2h_04` (authored) | **UNREACHABLE** | weapons.json:757 (row+prefabPath) |
| `blink_axe2h_09` | Timberfell Greataxe | Knight | axe | common | `Axe2h_09` (Addr) | `ItemIcons/blink_axe2h_09` (authored) | **UNREACHABLE** | weapons.json:782 (row+prefabPath) |
| `blink_axe2h_12` | Ironwood Splitter | Knight | axe | uncommon | `Axe2h_12` (Addr) | `ItemIcons/blink_axe2h_12` (authored) | **UNREACHABLE** | weapons.json:807 (row+prefabPath) |
| `blink_axe2h_15` | Bulwark Greataxe | Knight | axe | uncommon | `Axe2h_15` (Addr) | `ItemIcons/blink_axe2h_15` (authored) | **UNREACHABLE** | weapons.json:832 (row+prefabPath) |
| `blink_axe2h_20` | Emberfall Cleaver | Knight | axe | rare | `Axe2h_20` (Addr) | `ItemIcons/blink_axe2h_20` (authored) | shop | weapons.json:857 (row+prefabPath) |
| `blink_axe2h_22` | Ashen Reaver | Knight | axe | rare | `Axe2h_22` (Addr) | `ItemIcons/blink_axe2h_22` (authored) | **UNREACHABLE** | weapons.json:884 (row+prefabPath) |
| `blink_axe2h_23` | Dawnward Executioner | Knight | axe | epic | `Axe2h_23` (Addr) | `ItemIcons/blink_axe2h_23` (authored) | **UNREACHABLE** | weapons.json:911 (row+prefabPath) |
| `blink_axe2h_24` | Emberhand Sundering Axe | Knight | axe | epic | `Axe2h_24` (Addr) | `ItemIcons/blink_axe2h_24` (authored) | **UNREACHABLE** | weapons.json:938 (row+prefabPath) |
| `blink_axe2h_25` | Wardstone Cleaver | Knight | axe | epic | `Axe2h_25` (Addr) | `ItemIcons/blink_axe2h_25` (authored) | **UNREACHABLE** | weapons.json:965 (row+prefabPath) |
| `blink_bow2h_01` | Greenwarden Shortbow | Ranger | bow | common | `Bow2h_01` (Addr) | `ItemIcons/blink_bow2h_01` (authored) | shop | weapons.json:992 (row+prefabPath) |
| `blink_bow2h_03` | Snarewood Bow | Ranger | bow | common | `Bow2h_03` (Addr) | `ItemIcons/blink_bow2h_03` (authored) | shop | weapons.json:1016 (row+prefabPath) |
| `blink_bow2h_04` | Fieldwarden Bow | Ranger | bow | common | `Bow2h_04` (Addr) | `ItemIcons/blink_bow2h_04` (authored) | **UNREACHABLE** | weapons.json:1040 (row+prefabPath) |
| `blink_bow2h_05` | Copse Hunter's Bow | Ranger | bow | common | `Bow2h_05` (Addr) | `ItemIcons/blink_bow2h_05` (authored) | **UNREACHABLE** | weapons.json:1064 (row+prefabPath) |
| `blink_bow2h_08` | Forester's Recurve | Ranger | bow | uncommon | `Bow2h_08` (Addr) | `ItemIcons/blink_bow2h_08` (authored) | shop | weapons.json:1088 (row+prefabPath) |
| `blink_bow2h_09` | Yewbranch Bow | Ranger | bow | uncommon | `Bow2h_09` (Addr) | `ItemIcons/blink_bow2h_09` (authored) | shop | weapons.json:1112 (row+prefabPath) |
| `blink_bow2h_13` | Thicketwarden Recurve | Ranger | bow | uncommon | `Bow2h_13` (Addr) | `ItemIcons/blink_bow2h_13` (authored) | **UNREACHABLE** | weapons.json:1136 (row+prefabPath) |
| `blink_bow2h_17` | Longwatch Bow | Ranger | bow | uncommon | `Bow2h_17` (Addr) | `ItemIcons/blink_bow2h_17` (authored) | **UNREACHABLE** | weapons.json:1160 (row+prefabPath) |
| `blink_bow2h_18` | Glade Longbow | Ranger | bow | rare | `Bow2h_18` (Addr) | `ItemIcons/blink_bow2h_18` (authored) | shop + drop | weapons.json:1184 (row+prefabPath) |
| `blink_bow2h_19` | Leafsong Recurve | Ranger | bow | rare | `Bow2h_19` (Addr) | `ItemIcons/blink_bow2h_19` (authored) | shop | weapons.json:1210 (row+prefabPath) |
| `blink_bow2h_20` | Stormcopse Longbow | Ranger | bow | rare | `Bow2h_20` (Addr) | `ItemIcons/blink_bow2h_20` (authored) | **UNREACHABLE** | weapons.json:1236 (row+prefabPath) |
| `blink_bow2h_21` | Wardenroot Bow | Ranger | bow | rare | `Bow2h_21` (Addr) | `ItemIcons/blink_bow2h_21` (authored) | **UNREACHABLE** | weapons.json:1262 (row+prefabPath) |
| `blink_bow2h_23` | Eclipse Heartbow | Ranger | bow | epic | `Bow2h_23` (Addr) | `ItemIcons/blink_bow2h_23` (authored) | shop + drop | weapons.json:1288 (row+prefabPath) |
| `blink_bow2h_24` | Heartwood Sentinel Longbow | Ranger | bow | epic | `Bow2h_24` (Addr) | `ItemIcons/blink_bow2h_24` (authored) | **UNREACHABLE** | weapons.json:1314 (row+prefabPath) |
| `blink_bow2h_25` | Thornsong Warbow | Ranger | bow | epic | `Bow2h_25` (Addr) | `ItemIcons/blink_bow2h_25` (authored) | **UNREACHABLE** | weapons.json:1340 (row+prefabPath) |
| `blink_shield1h_01` | Warden's Kiteshield | Shared | shield | uncommon | `Shield1h_01` (Addr) | `ItemIcons/blink_shield1h_01` (authored) | shop + drop | weapons.json:1366 (row+prefabPath) |
| `blink_shield1h_02` | Oathbearer Bulwark | Shared | shield | rare | `Shield1h_02` (Addr) | `ItemIcons/blink_shield1h_02` (authored) | shop + drop | weapons.json:1392 (row+prefabPath) |
| `blink_shield1h_03` | Aegis Wall | Shared | shield | epic | `Shield1h_03` (Addr) | `ItemIcons/blink_shield1h_03` (authored) | shop + drop | weapons.json:1420 (row+prefabPath) |
| `blink_shield1h_04` | Oakband Heater | Shared | shield | common | `Shield1h_04` (Addr) | `ItemIcons/blink_shield1h_04` (authored) | shop | weapons.json:1448 (row+prefabPath) |
| `blink_shield1h_05` | Levy Buckler | Shared | shield | common | `Shield1h_05` (Addr) | `ItemIcons/blink_shield1h_05` (authored) | shop | weapons.json:1474 (row+prefabPath) |
| `blink_shield1h_06` | Banded Roundshield | Shared | shield | common | `Shield1h_06` (Addr) | `ItemIcons/blink_shield1h_06` (authored) | **UNREACHABLE** | weapons.json:1500 (row+prefabPath) |
| `blink_shield1h_07` | Watchman's Targe | Shared | shield | common | `Shield1h_07` (Addr) | `ItemIcons/blink_shield1h_07` (authored) | **UNREACHABLE** | weapons.json:1526 (row+prefabPath) |
| `blink_shield1h_08` | Plank Heater | Shared | shield | common | `Shield1h_08` (Addr) | `ItemIcons/blink_shield1h_08` (authored) | **UNREACHABLE** | weapons.json:1552 (row+prefabPath) |
| `blink_shield1h_09` | Militia Buckler | Shared | shield | common | `Shield1h_09` (Addr) | `ItemIcons/blink_shield1h_09` (authored) | **UNREACHABLE** | weapons.json:1578 (row+prefabPath) |
| `blink_shield1h_12` | Garrison Kiteshield | Shared | shield | uncommon | `Shield1h_12` (Addr) | `ItemIcons/blink_shield1h_12` (authored) | shop | weapons.json:1604 (row+prefabPath) |
| `blink_shield1h_15` | Bastion Targe | Shared | shield | uncommon | `Shield1h_15` (Addr) | `ItemIcons/blink_shield1h_15` (authored) | **UNREACHABLE** | weapons.json:1630 (row+prefabPath) |
| `blink_shield1h_16` | Oathweld Wardshield | Shared | shield | rare | `Shield1h_16` (Addr) | `ItemIcons/blink_shield1h_16` (authored) | shop | weapons.json:1656 (row+prefabPath) |
| `blink_shield1h_18` | Vigil Kiteshield | Shared | shield | rare | `Shield1h_18` (Addr) | `ItemIcons/blink_shield1h_18` (authored) | **UNREACHABLE** | weapons.json:1684 (row+prefabPath) |
| `blink_shield1h_19` | Emberwatch Bulwark | Shared | shield | rare | `Shield1h_19` (Addr) | `ItemIcons/blink_shield1h_19` (authored) | **UNREACHABLE** | weapons.json:1712 (row+prefabPath) |
| `blink_shield1h_20` | Heartguard Kiteshield | Shared | shield | rare | `Shield1h_20` (Addr) | `ItemIcons/blink_shield1h_20` (authored) | **UNREACHABLE** | weapons.json:1740 (row+prefabPath) |
| `blink_shield1h_21` | Oathbound Tower Shield | Shared | shield | rare | `Shield1h_21` (Addr) | `ItemIcons/blink_shield1h_21` (authored) | **UNREACHABLE** | weapons.json:1768 (row+prefabPath) |
| `blink_shield1h_24` | Oathweld Bastion Wall | Shared | shield | epic | `Shield1h_24` (Addr) | `ItemIcons/blink_shield1h_24` (authored) | **UNREACHABLE** | weapons.json:1796 (row+prefabPath) |
| `blink_shield1h_25` | Dawnward Bulwark | Shared | shield | epic | `Shield1h_25` (Addr) | `ItemIcons/blink_shield1h_25` (authored) | **UNREACHABLE** | weapons.json:1824 (row+prefabPath) |
| `blink_sword1h_01` | Recruit's Sword | Knight | sword | common | `Sword1h_01` (Addr) | `ItemIcons/blink_sword1h_01` (authored) | **UNREACHABLE** | weapons.json:1852 (row+prefabPath) |
| `blink_sword1h_02` | Garrison Blade | Knight | sword | uncommon | `Sword1h_02` (Addr) | `ItemIcons/blink_sword1h_02` (authored) | **UNREACHABLE** | weapons.json:1877 (row+prefabPath) |
| `blink_sword1h_03` | Emberwatch Sword | Knight | sword | rare | `Sword1h_03` (Addr) | `ItemIcons/blink_sword1h_03` (authored) | **UNREACHABLE** | weapons.json:1902 (row+prefabPath) |
| `blink_sword1h_07` | Levy Blade | Knight | sword | common | `Sword1h_07` (Addr) | `ItemIcons/blink_sword1h_07` (authored) | **UNREACHABLE** | weapons.json:1929 (row+prefabPath) |
| `blink_sword1h_10` | Watchman's Arming Sword | Knight | sword | common | `Sword1h_10` (Addr) | `ItemIcons/blink_sword1h_10` (authored) | **UNREACHABLE** | weapons.json:1954 (row+prefabPath) |
| `blink_sword1h_11` | Ashford Arming Sword | Knight | sword | uncommon | `Sword1h_11` (Addr) | `ItemIcons/blink_sword1h_11` (authored) | **UNREACHABLE** | weapons.json:1979 (row+prefabPath) |
| `blink_sword1h_12` | Bastion Sideblade | Knight | sword | uncommon | `Sword1h_12` (Addr) | `ItemIcons/blink_sword1h_12` (authored) | **UNREACHABLE** | weapons.json:2004 (row+prefabPath) |
| `blink_sword1h_14` | Emberhand Vigil | Knight | sword | rare | `Sword1h_14` (Addr) | `ItemIcons/blink_sword1h_14` (authored) | **UNREACHABLE** | weapons.json:2029 (row+prefabPath) |
| `blink_sword1h_16` | Cinderguard Blade | Knight | sword | rare | `Sword1h_16` (Addr) | `ItemIcons/blink_sword1h_16` (authored) | **UNREACHABLE** | weapons.json:2056 (row+prefabPath) |
| `blink_sword1h_21` | Dawnward Edge | Knight | sword | epic | `Sword1h_21` (Addr) | `ItemIcons/blink_sword1h_21` (authored) | **UNREACHABLE** | weapons.json:2083 (row+prefabPath) |
| `blink_sword1h_22` | Emberhand Reliquary Blade | Knight | sword | epic | `Sword1h_22` (Addr) | `ItemIcons/blink_sword1h_22` (authored) | **UNREACHABLE** | weapons.json:2110 (row+prefabPath) |
| `blink_sword2h_01` | Field Greatsword | Knight | sword | common | `Sword2h_01` (Addr) | `ItemIcons/blink_sword2h_01` (authored) | **UNREACHABLE** | weapons.json:2137 (row+prefabPath) |
| `blink_sword2h_09` | Militia Longblade | Knight | sword | common | `Sword2h_09` (Addr) | `ItemIcons/blink_sword2h_09` (authored) | **UNREACHABLE** | weapons.json:2162 (row+prefabPath) |
| `blink_sword2h_10` | Ironwood Greatsword | Knight | sword | uncommon | `Sword2h_10` (Addr) | `ItemIcons/blink_sword2h_10` (authored) | **UNREACHABLE** | weapons.json:2187 (row+prefabPath) |
| `blink_sword2h_22` | Bulwark Claymore | Knight | sword | uncommon | `Sword2h_22` (Addr) | `ItemIcons/blink_sword2h_22` (authored) | **UNREACHABLE** | weapons.json:2212 (row+prefabPath) |
| `blink_sword2h_24` | Emberfall Greatsword | Knight | sword | rare | `Sword2h_24` (Addr) | `ItemIcons/blink_sword2h_24` (authored) | **UNREACHABLE** | weapons.json:2237 (row+prefabPath) |
| `blink_sword2h_25` | Dawnbreak Warblade | Knight | sword | epic | `Sword2h_25` (Addr) | `ItemIcons/blink_sword2h_25` (authored) | **UNREACHABLE** | weapons.json:2264 (row+prefabPath) |
| `knight_flameblade` | Flameblade | Knight | sword | uncommon | `sword_A` | sheet sword_t2 | shop + drop | weapons.json:2291 · mesh EquipmentController.cs:3130-3153 |

### 1.1 Ids the mesh resolver knows that the catalog does not contain

`EquipmentController.IdMap` maps four ids that **do not exist in `weapons.json`** — dead map entries:

| id in IdMap | maps to mesh | citation | status |
|---|---|---|---|
| `mage_starter` | `wand_A` | `EquipmentController.cs:205` | **not in weapons.json** — removed 2026-08-02 (`GearCatalog.cs:296`) |
| `ranger_yew` | `bow_B` | `EquipmentController.cs:222` | **not in weapons.json** |
| `ranger_storm` | `bow_C` | `EquipmentController.cs:223` | **not in weapons.json** |
| `ranger_eclipse` | `bow_C` | `EquipmentController.cs:224` | **not in weapons.json** |

⚠ **Correction to a widely-repeated claim.** The "three bows all resolve to `bow_C`" collision
(`ranger_storm` + `ranger_eclipse` + `aegis_heartwood_longbow`) is **two-thirds phantom**: only
`aegis_heartwood_longbow` is a live catalog row. `bow_C` therefore has exactly **one** live consumer.
The real collisions are listed in §2 and they are worse.

---

## 2. FINDING 1 — mesh collisions (distinct weapons that look identical in the hand)

Computed by running the full resolver chain over all 96 rows. **11 meshes are shared by more than one
live weapon; 28 of the 96 rows (29%) hold a mesh that at least one other weapon also holds.**

| mesh | rows that resolve to it | why it collides |
|---|---|---|
| **`bow_A`** (5) | `ranger_starter`, `ranger_arrow_plain`, `ranger_arrow_fire`, `ranger_arrow_poison`, `ranger_arrow_frost` | the 4 **arrow** rows have no `prefabPath` and no IdMap entry, so `id.StartsWith("ranger")` hands them a bow (`EquipmentController.cs:3140`) |
| **`sword_A`** (4) | `knight_starter`, `cleric_starter`, `tripo_sword_a`, `knight_flameblade` | `knight_starter`+`tripo_sword_a` author it; `cleric_starter` (a **mace**) and `knight_flameblade` (a **fire** brand) fall through to the `DEFAULT-SWORD` arm (`EquipmentController.cs:3153`) |
| **`sword_G`** (3) | `knight_dawn`, `aegis_emberbrand`, `tripo_sword_g` | epic and **legendary** share one mesh — the top-of-ladder upgrade is invisible (`EquipmentController.cs:215-216`) |
| **`staff_A`** (2) | `mage_oak`, `tripo_staff_a` | uncommon ↔ common share a mesh |
| **`staff_B`** (2) | `mage_arcane`, `tripo_staff_b` | rare ↔ uncommon |
| **`staff_C`** (2) | `mage_void`, `tripo_staff_c` | epic ↔ rare |
| **`staff_D`** (2) | `aegis_aetherstaff`, `tripo_staff_d` | **legendary ↔ common** — the worst pairing in the file |
| **`sword_D`** (2) | `knight_iron`, `tripo_sword_d` | both uncommon |
| **`sword_F`** (2) | `knight_oath`, `tripo_sword_f` | both rare |
| **`shield_A`** (2) | `knight_shield_starter`, `tripo_shield_a` | both common |
| **`hammer_A`** (2) | `aegis_hallowed_censer`, `tripo_hammer_a` | a **legendary censer** and an **epic maul** are the same object |

**Root cause, in one line:** the `tripo_*` band was authored as a parallel ladder over the *same eleven*
Resources props the `mage_*`/`knight_*`/`aegis_*` band already claims, so every Resources-backed weapon
in the game has a twin. The 65 `blink_*` rows are the only band with **1:1 mesh identity** — each maps
to its own Addressables prefab.

**Ladder-invisibility roll-up (the player-felt version):**

* Knight sword ladder `knight_starter → knight_iron → knight_oath → knight_dawn → aegis_emberbrand`
  shows **4 distinct meshes across 5 tiers** — the legendary looks exactly like the epic.
* Mage staff ladder `tripo_staff_a → mage_oak → mage_arcane → mage_void → aegis_aetherstaff` shows
  **4 meshes across 5 tiers**, and the legendary shares with a **common** (`tripo_staff_d`).
* Ranger bow ladder `ranger_starter → aegis_heartwood_longbow` is the only clean one (`bow_A` → `bow_C`),
  but `bow_B` sits on disk unused (§4).

---

## 3. FINDING 2 — icon vs mesh disagreement

Icon and mesh are chosen by **two independent resolvers** that never consult each other:
`ItemIconCatalog.ForWeapon` (`ItemIconCatalog.cs:58`) and `EquipmentController.Resolve`
(`EquipmentController.cs:3116`). Nothing enforces agreement.

**Authoring rate: 76 of 96 rows (79%) author an `iconPath`; 20 (21%) fall back to the keyword+rarity
guess.** All 76 authored paths resolve to a real file under `Assets/Resources/ItemIcons/` — **zero
broken `iconPath`s**. All 7 sprite sheets named in `ItemIconCatalog.cs:42-51` exist on disk
(`Ud37F`, `inEJH`, `WRdWM`, `VxBVb`, `bRUz5`, `CtQcX`, `jdRCa`).

### 3.1 The 20 rows with no authored icon, and what the guess gives them

| id | class | true kind | mesh in hand | icon the bag shows | diverges? |
|---|---|---|---|---|---|
| `mage_oak` | Mage | staff | `staff_A` | none → emoji glyph | **yes** — no picture at all |
| `mage_arcane` | Mage | staff | `staff_B` | none → emoji glyph | **yes** |
| `mage_void` | Mage | staff | `staff_C` | none → emoji glyph | **yes** |
| `aegis_aetherstaff` | Mage | staff | `staff_D` | none → emoji glyph | **yes** — a *legendary* with no art |
| `aegis_hallowed_censer` | Cleric\* | hammer | `hammer_A` | none → emoji glyph | **yes** |
| `knight_starter` | Knight | sword | `sword_A` | `sword_t1` | no |
| `knight_iron` | Knight | sword | `sword_D` | `sword_t2` | no |
| `knight_oath` | Knight | sword | `sword_F` | `sword_t3` | no |
| `knight_dawn` | Knight | sword | `sword_G` | `sword_t4` | no |
| `aegis_emberbrand` | Knight | sword | `sword_G` | `sword_t5` | **kind ok, tier lies** — t5 art on the same mesh as the t4 row |
| `knight_flameblade` | Knight | sword | `sword_A` | `sword_t2` | **yes** — t2 sword art over the t1 starter mesh |
| `knight_shield_starter` | Knight | **shield** | `shield_A` | **`sword_t1`** | **YES — a shield that paints a SWORD.** `ForWeapon` has no shield branch at all (`ItemIconCatalog.cs:76-105`); the shield keywords live only in `ForArmor` (`:124-140`), which never sees a weapons.json row |
| `cleric_starter` | Cleric\* | **mace** | `sword_A` (fallback) | `sword_t1` | **yes** — a mace that is a sword in both hand and bag |
| `ranger_starter` | Ranger | bow | `bow_A` | `bow_t1` | no |
| `ranger_arrow_plain` | Ranger | **arrow** | `bow_A` | `bow_t1` | **yes** — ammo rendered as a bow |
| `ranger_arrow_fire` | Ranger | **arrow** | `bow_A` | `bow_t2` | **yes** |
| `ranger_arrow_poison` | Ranger | **arrow** | `bow_A` | `bow_t2` | **yes** |
| `ranger_arrow_frost` | Ranger | **arrow** | `bow_A` | `bow_t2` | **yes** |
| `aegis_heartwood_longbow` | Ranger | bow | `bow_C` | `bow_t5` | no |
| `tripo_axe_a` | Knight | axe | `axe_A` | `sword_t1` | **yes** — an axe painted as a sword (`"axe"` is routed into the sword sheet, `ItemIconCatalog.cs:93-96`) |

**Count: 14 of the 20 unauthored rows paint something that is not what the hero holds.** The five
staff/censer rows show no art at all by deliberate design (`ItemIconCatalog.cs:86-90` — the comment
says a sword silhouette for a staff would be "wrong visually", so it prefers the glyph).

### 3.2 The systemic gap

There is **no tiered staff/wand sheet and no shield branch in `ForWeapon`**. Any future magic or
off-hand row that does not author an `iconPath` inherits the same two defects. The 76 authored rows
(`tripo_*` and `blink_*`) sidestep the whole mapper — which is why the honest fix is "author an
`iconPath` on every row", not "add more keywords".

---

## 4. FINDING 3 — meshes on disk vs meshes wired

### 4.1 Resources props — `Assets/Resources/Heroes/Props/Weapons/`

17 assets on disk; **14 are reachable from a live weapon row.**

| on disk | used by | status |
|---|---|---|
| `axe_A.fbx` | `tripo_axe_a` | used |
| `bow_A.fbx` | `ranger_starter` + 4 arrow rows | used |
| `bow_B.fbx` | — | **UNUSED.** Its only reference is `ranger_yew`, an IdMap id with no catalog row (`EquipmentController.cs:222`) |
| `bow_C.fbx` | `aegis_heartwood_longbow` | used (once) |
| `dagger_A.fbx` | `tripo_dagger_a` | used |
| `hammer_A.fbx` | `aegis_hallowed_censer`, `tripo_hammer_a` | used |
| `shield_A.fbx` | `knight_shield_starter`, `tripo_shield_a` | used |
| `staff_A.fbx` | `mage_oak`, `tripo_staff_a` | used |
| `staff_B.fbx` | `mage_arcane`, `tripo_staff_b` | used |
| `staff_C.fbx` | `mage_void`, `tripo_staff_c` | used |
| `staff_D.fbx` | `aegis_aetherstaff`, `tripo_staff_d` | used |
| `sword_A.prefab` | `knight_starter`, `cleric_starter`, `tripo_sword_a`, `knight_flameblade` | used (the only `.prefab`; the rest are raw FBX) |
| `sword_D.fbx` | `knight_iron`, `tripo_sword_d` | used |
| `sword_F.fbx` | `knight_oath`, `tripo_sword_f` | used |
| `sword_G.fbx` | `knight_dawn`, `aegis_emberbrand`, `tripo_sword_g` | used |
| `wand_A.fbx` | — | **UNUSED.** Only `mage_starter` (removed from the catalog) points at it (`EquipmentController.cs:205`) |
| `_tripobak_sword_A.fbx` | — | **UNUSED** — a backup artefact, not a catalog target |

**Weapons whose mesh does not exist: ZERO.** Every mesh key produced by the resolver has a file behind
it. The armed-hero invariant holds (`EquipmentController.cs:3096-3109`).

### 4.2 Addressables props — `gear/weapon/*`

**400 weapon prefabs are registered** in `Assets/AddressableAssetsData/AssetGroups/Gear.asset`
(16 families × 25: `Axe1h`, `Axe2h`, `Bow2h`, `Claws1h`, `Crossbow2h`, `Dagger1h`, `Hammer2h`,
`Mace1h`, `Polearm2h`, `Scythe2h`, `Shield1h`, `SpellBook1h`, `Staff2h`, `Sword1h`, `Sword2h`,
`Wand1h`). **65 are referenced by a live weapon row. 335 (84%) are built, bundled and unreachable.**

Nine entire families — `Claws1h`, `Crossbow2h`, `Dagger1h`, `Hammer2h`, `Mace1h`, `Polearm2h`,
`Scythe2h`, `SpellBook1h`, `Staff2h`, `Wand1h` — have **zero** live consumers. (`Crossbow2h`'s absence
is deliberate and pinned: *"RangedPrimaryRegression case 1 pins that no crossbow can reach the runtime
catalog"* — `WeaponBoundsOrient.cs:355-357`. The other eight are not pinned; they are simply unwired.)
Every address a live row names **does** exist — zero dangling Addressables references.

---

## 5. FINDING 4 — unreachable weapons

There are exactly **four** ways a weapon id can reach a player. All four were enumerated from source.

### 5.1 Starter grant — 2 ids
`StarterLoadout.Kits` authors **one** class: `{ "knight", new StarterKit("knight_starter",
"knight_shield_starter") }` (`GearLoadout.cs:85`). Ranger and Mage rows are explicitly **not yet
seeded** — *"deliberately NOT pre-seeded … they land WITH the data"* (`GearLoadout.cs:71-74`).
Companions get `ranger_starter` / `knight_starter` separately (`CompanionGearSetup.cs:83`, `:95`).

### 5.2 Shop — the Forge, and it is a much narrower shelf than the catalog
`vendors.json` gives the Forge `categories:["weapon"]`, `onlyEquippable:true`, **`perLevelCap:2`**,
`excludeIdPrefixes:[]`, and **no `lockedPreviewLevels`** (`vendors.json:32-45`).
`EmitCapped` buckets candidates by required level, sorts **power DESC then id ORDINAL ASC**, and keeps
**two per level** (`VendorStockResolver.cs:541-572`, comparator at `:575-580`). The weapon band ranks on
`w.damageMult` and does **not** exclude off-hand items (`VendorStockResolver.cs:318-321`).

Simulating that resolver over the catalog gives the **complete Forge shelf, ever**:

| class | level 1 | level 3 | level 6 | level 10 |
|---|---|---|---|---|
| **Knight** | `knight_flameblade`, `blink_axe1h_12` | `blink_axe1h_16`, `blink_axe1h_18` | `blink_axe1h_20`, `blink_axe2h_20` | `aegis_emberbrand`, `blink_axe1h_24` |
| **Ranger** | `blink_bow2h_01`, `blink_bow2h_03` | `blink_bow2h_08`, `blink_bow2h_09` | `blink_bow2h_18`, `blink_bow2h_19` | `aegis_heartwood_longbow`, `blink_bow2h_23` |
| **Mage** | **`blink_shield1h_04`, `blink_shield1h_05`** | `mage_oak`, `tripo_staff_b` | `mage_arcane`, `tripo_staff_c` | `aegis_aetherstaff`, `mage_void` |
| Cleric\* | `blink_shield1h_04`, `blink_shield1h_05` | `blink_shield1h_01`, `blink_shield1h_12` | `blink_shield1h_02`, `blink_shield1h_16` | `aegis_hallowed_censer`, `blink_shield1h_03` |

Three consequences fall straight out of the sort:

1. **A level-1 Mage's Forge shelf is two shields and nothing else.** Every `job:"any"` shield carries
   `damageMult: 1.0`, ties the level-1 staves, and wins the tie because `blink_shield1h_04` sorts before
   `tripo_staff_a` on ordinal. With `onlyEquippable:true` and no preview window, the mage sees no staff
   until level 3. (This is the same class of bug already recorded once at `GearCatalog.cs:293-297`, where
   a shield winning a damageMult tie left a level-1 Mage unarmed — the vendor shelf has the same tie and
   no equivalent guard.)
2. **The Knight never sees a sword at the Forge.** `blink_axe1h_*` sorts ahead of every tied `blink_sword1h_*`
   / `tripo_sword_*` on ordinal, so the entire sword band is capped out at every level.
3. **`excludeIdPrefixes` is empty for the Forge but `["blink_"]` for the Armorer** (`vendors.json:42` vs
   `:58-60`). The `blink_*` band was meant to stay off the player-facing shelf; on the weapon shelf it is
   the *only* thing on it. That asymmetry looks unintentional.

### 5.3 Drop — arena / outpost victory, winner-take-all
`BattleArena.PickArenaWeapon` and `EnemyOutpost.PickWeapon` are identical: scan every catalog weapon the
class+level can equip, keep the **single highest `damageMult` of the rolled rarity**, else fall back to
`GearCatalog.BestWeapon` (`BattleArena.cs:2992-3005`, `EnemyOutpost.cs:784-798`). **Only 22 ids** can
ever be the answer for any (class, level, rarity) triple.

**`loot-tables.json` grants NO weapons at all** — every one of its 18 tables drops `materialId` rows only
(`loot-tables.json:81-84` schema, all `drops[]` entries). "Weapon drop" is exclusively the arena/outpost
pick above.

### 5.4 Craft — 6 weapon ids
`gear-recipes.json` outputs: `knight_iron` (Fine), `knight_oath` (Master), and the four Aegis legendaries
`aegis_emberbrand` / `aegis_heartwood_longbow` / `aegis_aetherstaff` / `aegis_hallowed_censer`, all four
gated on quest `forgemasters_act4` and 5 components each (`gear-recipes.json:6-211`). Those components
(`reforged_steel`, `oathweld_plating`, `heartwood_bough`, `last_pressing`, `aether_catalyst`) drop from
exactly one table, `dungeon-deepboss`, at 0.2 `bossOnly` each (`loot-tables.json:546-602`).

### 5.5 ⛔ THE UNREACHABLE SET — 56 of 96 rows (58%)

No shop stocks them, no recipe outputs them, no starter kit grants them, and they cannot win the
arena/outpost rarity roll.

**Resources-backed (11) — these have real bespoke art and are dead:**
`tripo_axe_a`, `tripo_hammer_a`, `tripo_staff_a`, `tripo_staff_d`, `tripo_sword_a`, `tripo_sword_d`,
`tripo_sword_f`, `tripo_sword_g`, plus ammo `ranger_arrow_plain`, `ranger_arrow_poison`, `ranger_arrow_frost`.

> ⚠ **`ranger_arrow_plain` is called out by name as an intended Ranger starter** in
> `GearLoadout.cs:72-73` ("`ranger_arrow_plain` + `tripo_dagger_a` for Sylas"). The row exists; the
> `StarterLoadout` entry does not. That is the WO-861 gap, still open.

**Addressables-backed (45):**
`blink_axe1h_14` · `blink_axe2h_04/09/12/15/22/23/24/25` ·
`blink_bow2h_04/05/13/17/20/21/24/25` ·
`blink_shield1h_06/07/08/09/15/18/19/20/21/24/25` ·
`blink_sword1h_01/02/03/07/10/11/12/14/16/21/22` ·
`blink_sword2h_01/09/10/22/24/25`

**All 17 `blink_sword*` rows are unreachable** — the entire sword half of the art pack is
in the build and out of reach. So is every 2-handed axe.

### 5.6 Reachability roll-up

| bucket | rows |
|---|---|
| starter | 2 (+1 companion-only: `ranger_starter`) |
| shop (survives the Forge cap) | 30 |
| craft | 6 |
| drop (arena/outpost) | 22 |
| **union — obtainable** | **40** |
| **UNREACHABLE** | **56** |

---

## 6. FINDING 5 — grip / seat data: dialled constants vs derived

The per-family presets live in `EquipmentController.cs:111-181`. `gripPos`/`gripEuler` are applied on
the hand bone; `heldLength` is the bounds-normalize target.

| family | `gripPos` | `gripEuler` | `heldLength` | seat model | citation |
|---|---|---|---|---|---|
| Sword | `(0,0,0)` | **`(0,0,0)`** | 0.65 | **derived** — `SeatByHandle` infers the handle from mesh geometry | `:111-116` |
| Dagger | `(0,0,0)` | **`(0,0,0)`** | 0.40 | derived (`SeatByHandle`) | `:117-122` |
| Axe | `(0,0,0)` | **`(0,0,0)`** | 0.80 | derived (`SeatByHandle`) | `:123-128` |
| Hammer | `(0,0,0)` | **`(0,0,0)`** | 0.85 | derived (`SeatByHandle`) | `:129-134` |
| Staff | `(0,0,0)` | **`(0,0,0)`** | 1.30 | derived (`SeatByHandle`) | `:135-140` |
| Wand | `(0,0,0)` | **`(0,0,0)`** | 0.45 | derived (`SeatByHandle`) | `:141-146` |
| **Bow** | `(0,0,0)` | **`(0,0,0)`** | 0.92 | **derived** — `WeaponBoundsOrient.ComputeBowHeldRotation`; the zero euler is a *nudge on top*, not the seat | `:147-175` |
| **Shield** | **`(-0.05,0,0)`** | **`(-58,16,-90)`** | 0.45 | **DIALLED CONSTANT — the only one left** | `:176-181` |

**Answer to the question asked: exactly one family carries a non-zero `gripEuler`, and it is Shield.**
Everything else is zero because WO-435 replaced the per-archetype Y-offsets with geometry inference:
*"gripPos for melee is now ZERO — the grip point is DERIVED from the mesh by SeatByHandle … The old
per-archetype Y-offsets ('0.02/0.05 everywhere') were the §4 smell"* (`EquipmentController.cs:106-110`).

### 6.1 The bow, specifically (the thing being changed tonight)

The owner's rule — *"y is the longest distance on any two points of a mesh bow; the straight edge runs
parallel to the person holding it with the arm crossing that straight line perpendicular, landing with
the hand clasping on the curved edge furthest from the person"* — is implemented in two halves:

* **Half 1, the prop's own frame:** `WeaponBoundsOrient.NormalizeInto` puts the longest axis (limbs,
  nock-to-nock) on local **+Y**, the narrowest on **+X**, curve depth on **+Z**, grip on the stave
  surface at the long-axis midpoint (`HeroBowAttachment.cs:45-54`).
* **Half 2, the hand-local seat:** `ComputeBowHeldRotation(hand, body)` reads the **body**, not the
  wrist: `limb = body.up`, `belly = body.forward` orthonormalized against it, then
  `Quaternion.Inverse(hand.rotation) * Quaternion.LookRotation(belly, limb)`
  (`WeaponBoundsOrient.cs:362-393`).

⚠ **The recorded history matters and is preserved in-code twice.** A zero `gripEuler` was once argued to
be "proven-correct"; it is not — `NormalizeInto` has no knowledge of the bone it is parented to, so a
zero euler maps the limb span onto the hand bone's raw +Y and the bow lies **horizontally across the
body**. That wrong conclusion is why the correct fix was reverted once
(`EquipmentController.cs:154-172`, `HeroBowAttachment.cs:55-65`). **Do not re-dial a constant here.**

Routing: on the **hero** the Bow preset is not reached at all — `DeferBowToBowAttachment` hands the held
bow to `HeroBowAttachment`. On **companions / non-rangers** `AttachLoadedProp` routes `kind==Bow` through
`ComputeBowHeldRotation` and **withholds** `ApplyGlobalWeaponYaw`, so a companion archer gets the hero's
seat (`EquipmentController.cs:165-172`, `:1055-1105`).

### 6.2 Per-mesh overrides (the Offset Forge layer, on top of everything above)

`Assets/Resources/OffsetForge/offsets.json` carries hand-tuned overrides keyed by **mesh name**, some
with `fullOverride:true` (which replaces the derived seat outright rather than composing on it):

| offset id | rot | pos | scale | fullOverride |
|---|---|---|---|---|
| `shield_A` | `(-160,-180,-84)` | `(0.12,-0.01,0)` | 1.04 | **true** |
| `sword_A` | `(117,-2,110)` | `(0.01,0.03,-0.01)` | 1.10 | false |
| `sword_D` | `(20,16,55)` | `(0,0,0)` | 0.47 | **true** |
| `sword_F` | `(0,-52,48)` | `(0,0.06,0)` | 1.00 | false |
| `sword_G` | `(0,-106,0)` | `(0,0,0)` | 1.00 | **true** |
| `shield_A@sheathed` | `(2,180,-78)` | `(-0.12,0.03,0.02)` | 1.00 | false |
| `sword_A@sheathed` | `(180,-28,-51)` | `(0.23,-0.14,0.12)` | 1.00 | false |
| `bow_A_withString` | `(0,0,-90)` | `(0,0,0)` | 1.00 | false |

**Only 8 weapon entries exist**, all Resources props. **No `blink_*` / Addressables mesh has an override**
— the 65 live Addressables weapons rely entirely on the `native` grip-at-origin trust path
(`EquipmentController.cs:191-196`, `:3176`). Note `bow_A_withString` is keyed to a mesh name that no
resolver ever produces (the resolver emits `bow_A`), so that override is **inert**.

---

## 7. Accessories — the jeweler ladder (10 rows)

All ten are `job:"any"` → **Shared**, all author an `iconPath`, none attaches a mesh
(*"No 3D mesh attachment; bonuses applied on equip as additive modifiers"* — `accessories.json:4`).
Sold at the Jeweler, which has **no `perLevelCap` and no `onlyEquippable`** (`vendors.json:64-76`), so
every row is genuinely on the shelf.

| id | name | class | kind | rarity | mesh | icon | obtained | citation |
|---|---|---|---|---|---|---|---|---|
| `ring_iron` | Iron Band | Shared | ring | common | none (no mesh by design) | `ItemIcons/ring_iron` (authored) | shop (Jeweler) | accessories.json:7 |
| `ring_steadfast` | Steadfast Ring | Shared | ring | uncommon | none | `ItemIcons/ring_steadfast` | shop + craft | accessories.json:23 · jeweler-recipes.json:257 |
| `ring_embercoil` | Embercoil Ring | Shared | ring | rare | none | `ItemIcons/ring_embercoil` | shop + craft | accessories.json:40 · jeweler-recipes.json:280 |
| `ring_heartward` | Heartward Seal | Shared | ring | epic | none | `ItemIcons/ring_heartward` | shop + craft | accessories.json:58 · jeweler-recipes.json:307 |
| `ring_firstlight` | Ring of First Light | Shared | ring | legendary | none | `ItemIcons/ring_firstlight` | shop only — **no recipe** | accessories.json:77 |
| `amulet_travelers` | Traveler's Token | Shared | amulet | common | none | `ItemIcons/amulet_travelers` | shop (Jeweler) | accessories.json:96 |
| `amulet_oathward` | Oathward Pendant | Shared | amulet | uncommon | none | `ItemIcons/amulet_oathward` | shop + craft | accessories.json:113 · jeweler-recipes.json:334 |
| `amulet_lastpressing` | Last-Pressing Focus | Shared | amulet | rare | none | `ItemIcons/amulet_lastpressing` | shop + craft | accessories.json:132 · jeweler-recipes.json:357 |
| `amulet_elarion` | Elarion Amulet | Shared | amulet | epic | none | `ItemIcons/amulet_elarion` | shop + craft | accessories.json:150 · jeweler-recipes.json:384 |
| `amulet_heartstone` | Heartstone Locket | Shared | amulet | legendary | none | `ItemIcons/amulet_heartstone` | shop only — **no recipe** (`setId:"aegis"`) | accessories.json:169 |

**The chain the prompt named is confirmed exactly:**
`ring_iron → ring_steadfast → ring_embercoil → ring_heartward` (`jeweler-recipes.json:257`, `:280`, `:307`)
— each recipe consumes the previous ring plus gems. **It stops at epic**: `ring_firstlight` (legendary)
has no recipe and no chain step, and the amulet ladder ends the same way at `amulet_heartstone`. Both
legendaries are **purchase-only**. Unlike weapons, **every accessory is obtainable**.

---

## 8. Per-instance power levels (context for "the upgrade is invisible")

`gear-levels.json` gives every owned weapon 5 in-place power levels per rarity band, e.g. common
`statMult [1.0, 1.12, 1.22, 1.30, 1.36]` (`gear-levels.json:196-217`). Improving is **instant** — no
queue, resources only (`gear-levels.json:193`). Since level is a stat multiplier and **never touches
the mesh or the icon**, §2's collisions mean a fully-levelled weapon and a fresh one are visually
identical too. Rarity is identity; level is the ladder; **neither is visible on the model.**

---

## 9. HOW TO ADD A WEAPON CORRECTLY

Four steps. Skipping any one produces a specific, already-observed failure.

**1 — Author the row** in `Assets/Resources/Data/Canonical/weapons.json`.
Required: `id`, `name`, `job` (the class gate — `GearCatalog.WeaponFitsClass`, `GearCatalog.cs:413`),
`category` (drives the grip family — `EquipmentController.VisualForCategory`, `:3179-3193`), `rarity`,
`damageMult`, `req.level`, and the `buy*` costs.
⚠ The file is **generated** — *"GearCurationExporter (additive merge) … DO NOT hand-edit"*
(`weapons.json:2308`). Add the pick upstream in `GearCurationPicks.json` and re-export, and keep the
`Assets/StreamingAssets/Data/Canonical/` copy as the library source.

**2 — Author an `iconPath`.** `"iconPath": "ItemIcons/<id>"` pointing at a real 512×512 PNG under
`Assets/Resources/ItemIcons/`. This is the only way the bag shows the right picture:
`ItemIconCatalog.ForWeapon` treats an authored path as authoritative and skips the keyword mapper
entirely (`ItemIconCatalog.cs:64-69`). **Skip it and you inherit §3's defects** — a shield paints a
sword, a staff paints nothing.

**3 — Register the mesh.** Preferred and lowest-friction: give the row a **`prefabPath`**. The resolver
derives the held mesh from the row itself, so icon and mesh stay in lockstep (`EquipmentController.cs:3156-3177`).
* Addressables prefab → `"prefabPath": "gear/weapon/<Prefab>"` + `"loadVia": "addressable"`. It is
  treated as `native` (grip-at-origin, trusted pivot) — `EquipmentController.cs:191-196`, `:3176`.
* Resources prop → `"prefabPath": "Heroes/Props/Weapons/<mesh>"`, with the asset committed there
  (`EquipmentController.cs:66`, header note `:27-40`).
* Only add an `IdMap` entry (`EquipmentController.cs:201-229`) when the row needs a mesh the catalog
  cannot name. That table is flagged `TODO data-driven: delete this once weapons.json carries
  visualMesh/grip` (`:200`), and it is what §1.1's four dead entries came from.
* **Verify the mesh is not already claimed** (§2). Reusing one makes the new weapon a visual twin.
* Author **no `gripEuler`** unless a screenshot proves it (§6). Zero + derivation is the correct default;
  a dialled constant is the recorded failure mode (`EquipmentController.cs:154-172`).

**4 — Make it obtainable.** A row with none of the four paths is dead data (§5.5 — 56 rows are).
* *Shop:* it competes for **two slots per required level**, ranked power DESC then id ordinal ASC
  (`VendorStockResolver.cs:541-580`). Check the §5.2 shelf — a tied `damageMult` and a late-sorting id
  means it never appears. This is the step most often silently missed.
* *Craft:* add a `gear-recipes.json` recipe whose `outputGearId` is the new id (`gear-recipes.json:9`).
* *Starter:* add it to `StarterLoadout.Kits` (`GearLoadout.cs:78-86`) — but only once the id exists,
  per the standing note at `GearLoadout.cs:71-74`.
* *Drop:* it must be the **highest `damageMult` of its rarity** for some class+level, or the arena and
  outpost pick will never return it (`BattleArena.cs:2996-3004`, `EnemyOutpost.cs:786-798`).
  **`loot-tables.json` cannot help — it grants materials only.**

**Verification:** `EquipmentController.Resolve` emits a `FlowTrace.Warn` naming the id whenever it falls
through to the generic sword (`:3147-3151`), and `LoadWeaponMesh` warns on a missing mesh key or missing
prop (`:3209-3225`). A clean equip capture with neither warning is the proof the wiring landed.

---

## 10. Open defects this catalog surfaces (for ticketing, in severity order)

1. **A level-1 Mage's Forge shelf is two shields and no staff** — shields tie on `damageMult` and win
   the ordinal tiebreak (§5.2). Also blocks every Knight sword.
2. **56 of 96 weapons (58%) are unreachable**, including all 17 `blink_sword*` rows and every
   2-handed axe (§5.5).
3. **335 of 400 bundled Addressables weapon prefabs (84%) have no consumer** — nine whole families,
   only one of which (`Crossbow2h`) is deliberately excluded (§4.2).
4. **28 rows share a mesh with another weapon**; the Knight and Mage ladders each show 4 meshes across
   5 tiers, and `staff_D` is shared by a legendary and a common (§2).
5. **`knight_shield_starter` paints a sword icon** — `ForWeapon` has no shield branch (§3.1).
6. **The 4 arrow rows render `bow_A` in the hand** and are icon-mapped as bows (§2, §3.1).
7. **`cleric_starter` and `knight_flameblade` hit the generic-sword fallback** — already self-reported
   by the code (`EquipmentController.cs:3145-3151`) and still unfixed.
8. **`ranger_arrow_plain` + `tripo_dagger_a` are the documented Ranger starter kit and are not wired**
   into `StarterLoadout` (`GearLoadout.cs:71-74`); Mage has no starter kit at all.
9. **`bow_B.fbx`, `wand_A.fbx` are paid-for art with no live consumer**, and the
   `bow_A_withString` Offset Forge override is keyed to a mesh name nothing emits (§4.1, §6.2).
10. **The Forge's `excludeIdPrefixes` is empty while the Armorer's is `["blink_"]`** — the placeholder
    band the Armorer hides is the Forge's entire shelf (§5.2).
