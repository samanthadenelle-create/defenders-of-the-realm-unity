# VFX CATALOG — the one registry of every effect key, what it resolves to, and who plays it

**Built:** 2026-08-16 · **Branch:** `wip/village2-and-f8-tickets` · **Method:** read-only; every row is
sourced from the file named in its citation column, parsed at source (baked `.asset` GUIDs resolved
through a full `.meta` index) — no inference (CLAUDE.md §12).
**Scope:** every key in every store, every prefab under `Assets/Resources/VFX/**`, every consumer.
This is a durable registry (project memory `audit-outputs-as-known-dictionaries`), not a one-off
report — update it in the same breath as any catalog change (CLAUDE.md §15).

---

## 0. READ THIS FIRST — which source wins, and why the question is not obvious

There are **two key spaces**, and for the string-key space **three places a key→prefab mapping is
written down**. Only one of them decides what the player sees.

### The two key spaces

| Space | API | Store | Rows |
|---|---|---|---|
| **String keys** (the live one) | `VFXManager.PlayKey("Fireball_Cast", …)` — `Assets/_Modules/Village/Vfx/VFXManager.Hovl.cs:193` | `Assets/Resources/VFX/HovlVfxCatalog.asset` | **152 baked** |
| **`VFXType` enum** (parallel, older) | `VFXManager.Play(VFXType.…)` | `Assets/Resources/VFX/VFXCatalog.asset` | **75 baked** of 95 enum members |

A bridge connects them for exactly one type: `VFXType.Aura_HeartPulse` → string key `"Aura_HeartPulse"`
(`VFXManager.Hovl.cs:128-132`). That is why `Aura_HeartPulse` is enum ordinal 40 with **no** `VFXCatalog`
row and still renders.

### The three string-key sources, ranked by authority

| Rank | Source | What it is | Authority |
|---|---|---|---|
| **1 — RUNTIME TRUTH** | `Assets/Resources/VFX/HovlVfxCatalog.asset` (152 rows) | the baked `ScriptableObject` the game loads at `VFXManager.Hovl.cs:144` | **This is what plays.** Everything below is an input to the bake. |
| **2 — WINS THE BAKE** | `Assets/Editor/VfxManualPicks.json` (136 rows, all `manual: true`) | the owner's tags from the VFX Caster window | Merged **after** the code Map and **replaces it on key collision** — `HovlVfxCatalogGenerator.cs:406-408` (`rows.RemoveAll(same key)` then `rows.Add(manual)`); declared canon at `:49-52`. |
| **3 — LOSES THE BAKE** | `HovlVfxCatalogGenerator.Map` (37 keys, `HovlVfxCatalogGenerator.cs:95-256`) | the curated code table | Survives only for keys the JSON does **not** carry. |
| *not a source* | `Assets/Editor/VfxCasterLibraryIndex.json` | a 2951-entry disk **scan** of every pack prefab; 152 carry a `key` with `catalogued:true` | **Verified this session: its 152 tagged rows are byte-identical to the 152 baked rows** — zero path disagreements against either the picks or the bake. It is a derived *view* of the bake, not a competing store. |
| *not a source* | `VFXManager.Hovl.cs:33-62` | a 27-line prose table in the file header | **DOCUMENTATION ONLY — and STALE.** It reproduces the *code Map* picks, 17 of which lost the bake. Nothing reads it. |

> ### ⛔ THE TRAP THIS CATALOG EXISTS TO CLOSE
> **19 keys exist in both the code Map and the JSON picks. 17 name DIFFERENT prefabs, and the JSON
> wins every one.** Reading `HovlVfxCatalogGenerator.cs` to answer *"which prefab is `Melee_Slash`?"*
> gives the wrong answer — the code says a Flower slash (`:169`), the JSON says a Dragon punch
> (`VfxManualPicks.json:376`), and the baked asset carries the Dragon punch. The header comment at
> `VFXManager.Hovl.cs:52` says Flower slash too, so **two of the three readable sources are wrong.**
> Full conflict list: §2.

### Canon: the owner tags, the CLI maps verbatim

Project memory `vfx-map-owner-tags-no-creative-pick`, and the ban prose at
`HovlVfxCatalogGenerator.cs:111-119`: **the owner names the prefab for a key; the CLI wires that name
and never substitutes its own pick.** A key with no owner tag is **withheld** — absent from the Map
entirely — rather than guessed at. See `Arcane_Aura`, end of §1.

---

## 1. Master table — every string key

Path shorthand: `Hovl/` = `Assets/Hovl Studio/` · `AAA/` = `…/AAA Projectiles Vol 1/Prefabs/` ·
`RPGBundle/` = `…/RPG VFX Bundle/Random effect prefabs/` · `AOE/` = `…/AOE Magic spells Vol.1/Prefabs/` ·
`MagicCircles/` = `…/Magic circles/Prefabs/` · `MapMarkers/` = `…/Map track markers VFX/Prefabs/` ·
`ParticlePack/` = `Assets/UnityTechnologies/ParticlePack/EffectExamples/` ·
`MirzaUVFX/` = `Assets/Mirza Beig/Particle Systems/Ultimate VFX/` ·
`Lana/` = `Assets/Lana Studio/Casual RPG VFX/Prefabs/` ·
`SpellsPack/` = `Assets/Spells Pack/Particles/Prefabs/` · `Res:VFX/` = `Assets/Resources/VFX/`.

Consumers column: runtime call sites, `Assets/_Modules/` prefix stripped; `data:` =
`Assets/Resources/Data/Canonical/` (the `StreamingAssets` twin of each data file is omitted — it
carries the identical rows); `_(tooling only)_` = referenced only by a regression or the proof-capture
harness; `**none**` = no reference anywhere outside the store that defines it.

154 rows = the 152 baked, plus the 2 whose prefab is missing on disk and which therefore never reach
the bake.

| Key | Winning source | Resolved prefab | On disk | Consumers | Origin | Notes |
|---|---|---|---|---|---|---|
| `Aegis_Cast` | JSON picks | `Hovl/MagicCircles/Magic shield holy` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:11` | both sources agree |
| `Aegis_Shield` | code Map | `Hovl/MagicCircles/Loop version/Magic shield holy loop` | yes | Village/HubStructureVisualInjector.cs:597, Village/Catalog/StructureFactory.cs:1017 | code-guessed<br>`HovlVfxCatalogGenerator.cs:191` |  |
| `Arcane_Cast` | JSON picks | `Hovl/AAA/Flash and hits/Flash 16 fire` | yes | Village/Buildings/TowerCombat.cs:397, Village/Enemies/EnemyTypeVfxSet.cs:87 | owner-tagged<br>`VfxManualPicks.json:25` | **CONFLICT** — code Map says `Hovl/AAA/Flash and hits/Flash 17 nova violet` (`HovlVfxCatalogGenerator.cs:108`); JSON wins |
| `Arcane_Impact` | JSON picks | `ParticlePack/Legacy Particles/Prefabs/PlasmaExplosionEffect` | yes | Village/Enemies/EnemyTypeVfxSet.cs:93, Village/Hero/WeaponVfxMap.cs:232 | owner-tagged<br>`VfxManualPicks.json:32` | **CONFLICT** — code Map says `Hovl/AAA/Flash and hits/Hit 17 nova violet` (`HovlVfxCatalogGenerator.cs:109`); JSON wins |
| `Arcane_Projectile` | JSON picks | `Hovl/RPGBundle/Buff orange shot` | yes | Village/Enemies/EnemyTypeVfxSet.cs:90 | owner-tagged<br>`VfxManualPicks.json:39` | **CONFLICT** — code Map says `Hovl/AAA/Projectile VFX loop/Projectile 17 nova violet` (`HovlVfxCatalogGenerator.cs:107`); JSON wins |
| `ArcaneTower-Baselevel_Projectile` | JSON picks | `Hovl/AAA/Projectiles 2D/2D Projectile 7 pink` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:18` |  |
| `ARcaneTower_Projectile` | JSON picks | `Hovl/RPGBundle/Buff orange shot` | yes | Village/Buildings/DefenseTower.cs:1170, Village/Buildings/DefenseTower.cs:1178 | owner-tagged<br>`VfxManualPicks.json:4` |  |
| `ArcherTower-Fire_Projectile` | JSON picks | `Hovl/AAA/Projectiles(Particle collision)/Projectile 16 fire` | yes | Village/Buildings/DefenseTower.cs:1146 | owner-tagged<br>`VfxManualPicks.json:46` |  |
| `ArcherTower-Ice_Projectile` | JSON picks | `Hovl/AAA/Projectiles(Particle collision)/Projectile 14 blue rapid` | yes | data:abilities.json:208, data:abilities.json:300, Village/Buildings/DefenseTower.cs:1147, Village/Buildings/DefenseTower.cs:1177 | owner-tagged<br>`VfxManualPicks.json:53` |  |
| `ArcherTower_Projectile` | JSON picks | `Hovl/AAA/Projectiles(Particle collision)/Projectile 13 red laser` | yes | Village/Buildings/DefenseTower.cs:1160, Village/Buildings/DefenseTower.cs:1179 | owner-tagged<br>`VfxManualPicks.json:74` |  |
| `ArcherTowerLevel1_Projectile` | JSON picks | `Hovl/AAA/Projectiles 2D/2D Projectile 21 red arrow` | yes | Village/Buildings/DefenseTower.cs:1158 | owner-tagged<br>`VfxManualPicks.json:60` |  |
| `ArcherTowerLevel2_Projectile` | JSON picks | `Hovl/AAA/Projectiles 2D/2D Projectile 20 pink arrow` | yes | Village/Buildings/DefenseTower.cs:1159 | owner-tagged<br>`VfxManualPicks.json:67` |  |
| `Aura_HeartPulse` | code Map | `Hovl/RPGBundle/Buff white twist` | yes | Village/Buildings/ArcaneTower.cs:211, Village/Buildings/ArcaneTower.cs:213, Village/Catalog/StructureFactory.cs:1018, Village/Vfx/ArcaneAura.cs:360 +3 | code-guessed<br>`HovlVfxCatalogGenerator.cs:143` |  |
| `AuraOverArcaneTower_Aura` | JSON picks | `Hovl/AAA/Projectiles 2D/2D Projectile 18 nova orange` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:81` |  |
| `BurningStructure_Aura` | JSON picks | `MirzaUVFX/Prefabs/Loop/pf_vfx-ult_demo_psys_loop_fire` | yes | Village/Buildings/StructureBurn.cs:29, Village/Buildings/StructureBurn.cs:78 | owner-tagged<br>`VfxManualPicks.json:88` | loop |
| `BurningStructure_Impact` | JSON picks | `MirzaUVFX/Prefabs/Loop/pf_vfx-ult_demo_psys_loop_fire` | yes | Village/Buildings/StructureBurn.cs:33, Village/Buildings/StructureBurn.cs:82 | owner-tagged<br>`VfxManualPicks.json:95` | loop |
| `Cathedral_Aura` | JSON picks | `Hovl/MagicCircles/Loop version/Magic circle electro loop` | yes | Village/HubStructureVisualInjector.cs:596, Village/HubStructureVisualInjector.cs:602, Village/Catalog/StructureFactory.cs:1016, Village/Catalog/StructureFactory.cs:1021 +1 | owner-tagged<br>`VfxManualPicks.json:102` |  |
| `Cleave_Impact` | JSON picks | `Hovl/AOE/Energy explosion` | yes | data:abilities.json:161, data:abilities.json:242, data:abilities.json:318, data:abilities.json:407 +4 | owner-tagged<br>`VfxManualPicks.json:109` | both sources agree |
| `Collector_Full` | code Map | `Hovl/RPGBundle/Gold dot` | yes | Village/Vfx/VFXManager.Hovl.cs:71, Village/Vfx/VFXManager.Hovl.cs:182 | code-guessed<br>`HovlVfxCatalogGenerator.cs:152` |  |
| `Damage_BreakBurst` | code Map | `Res:VFX/Damage/Damage_BreakBurst` | yes | Village/Vfx/StructureDamageVisuals.cs:260 | code-guessed<br>`HovlVfxCatalogGenerator.cs:234` |  |
| `Damage_CriticalBeacon` | code Map | `Res:VFX/Damage/Damage_CriticalBeacon` | yes | Village/Vfx/StructureDamageVisuals.cs:261 | code-guessed<br>`HovlVfxCatalogGenerator.cs:229` | loop |
| `Damage_Fire` | code Map | `Res:VFX/Damage/Damage_Fire` | yes | Village/Vfx/StructureDamageVisuals.cs:258 | code-guessed<br>`HovlVfxCatalogGenerator.cs:224` | loop |
| `Damage_Ruin` | code Map | `Res:VFX/Damage/Damage_Ruin` | yes | Village/Vfx/StructureDamageVisuals.cs:259 | code-guessed<br>`HovlVfxCatalogGenerator.cs:236` | loop |
| `Damage_Smolder` | code Map | `Res:VFX/Damage/Damage_Smolder` | yes | Village/Vfx/StructureDamageVisuals.cs:257 | code-guessed<br>`HovlVfxCatalogGenerator.cs:222` | loop |
| `Dash_Blink` | code Map | `Hovl/RPGBundle/Buff white twist` | yes | data:abilities.json:107, data:abilities.json:564, data:abilities.json:665, data:abilities.json:767 | code-guessed<br>`HovlVfxCatalogGenerator.cs:240` |  |
| `DefenseUp-Offhand(Shield)_Aura` | JSON picks | `Hovl/MagicCircles/Magic shield holy` | yes | data:abilities.json:441 | owner-tagged<br>`VfxManualPicks.json:116` |  |
| `DragonFire_Cast` | JSON picks | `Lana/Fire/Flamethrower` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:123` | loop |
| `DragonFire_Impact` | JSON picks | `Hovl/AAA/Flash and hits/Hit 20 pink arrow` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:130` |  |
| `Dungeon_Portal_Gate` | code Map | `Hovl/MagicCircles/Magic circle dark star` | yes | Village/World/DungeonWorldPortalSpawner.cs:697 | code-guessed<br>`HovlVfxCatalogGenerator.cs:129` |  |
| `Electricityimpact_Impact` | JSON picks | `Hovl/AAA/Projectiles with logic/Projectile 2 electro` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:151` |  |
| `ElectricitySpell_Cast` | JSON picks | `Hovl/AAA/Projectiles(Particle collision)/Projectile 2 electro` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:137` |  |
| `ElectricitySpell_Impact` | JSON picks | `Hovl/AAA/Projectiles with logic/Projectile 2 electro` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:144` |  |
| `Ember_Burn` | code Map | `Hovl/RPGBundle/Debuff 1` | **NO** | **none** | code-guessed<br>`HovlVfxCatalogGenerator.cs:204` | **NOT BAKED** — prefab absent, key no-ops |
| `EndGameCAstingAnimation_Impact` | JSON picks | `Lana/Range_attack/Hit_light` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:158` |  |
| `EnemyCast_Cast` | JSON picks | `SpellsPack/Projectiles/Casting/Casting_Dark_3` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:165` |  |
| `EnhamcingBuff_Cast` | JSON picks | `Hovl/AAA/Projectile VFX loop/Projectile dragon punch` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:172` |  |
| `Explosion_Impact` | JSON picks | `ParticlePack/Fire & Explosion Effects/Prefabs/BigExplosion` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:179` |  |
| `Fire_Cast` | JSON picks | `Hovl/AAA/Flash and hits/Hit 16 fire` | yes | data:abilities.json:30, data:abilities.json:370, Village/Buildings/DefenseTower.cs:1103, Village/Buildings/TowerCombat.cs:494 +1 | owner-tagged<br>`VfxManualPicks.json:200` |  |
| `Fireball_Cast` | JSON picks | `Hovl/AAA/Flash and hits/Hit 16 fire` | yes | data:motion-castings.json:223, Village/Hero/AbilityCatalog.cs:160, Village/Vfx/VFXManager.Hovl.cs:66 | owner-tagged<br>`VfxManualPicks.json:221` | **CONFLICT** — code Map says `Hovl/AAA/Flash and hits/Flash 16 fire` (`HovlVfxCatalogGenerator.cs:99`); JSON wins |
| `Fireball_Impact` | JSON picks | `Hovl/AAA/Flash and hits/Hit 25 orange explosion` | yes | data:motion-castings.json:225, Village/Hero/AbilityCatalog.cs:164, Village/Hero/WeaponVfxMap.cs:213 | owner-tagged<br>`VfxManualPicks.json:228` | **CONFLICT** — code Map says `Hovl/AAA/Flash and hits/Hit 16 fire` (`HovlVfxCatalogGenerator.cs:100`); JSON wins |
| `Fireball_Projectile` | JSON picks | `Hovl/AAA/Projectiles(Particle collision)/Projectile 25 orange explosion` | yes | data:motion-castings.json:224, Village/Vfx/VFXManager.Hovl.cs:15, Village/Vfx/VFXManager.Hovl.cs:68, Village/Vfx/VFXManager.Hovl.cs:182 | owner-tagged<br>`VfxManualPicks.json:235` | **CONFLICT** — code Map says `Hovl/AAA/Projectile VFX loop/Projectile 16 fire` (`HovlVfxCatalogGenerator.cs:98`); JSON wins |
| `FireballImpact_Impact` | JSON picks | `Hovl/AAA/Projectiles with logic/Projectile 25 orange explosion` | yes | data:abilities.json:629, Village/Enemies/Enemy.cs:1779 | owner-tagged<br>`VfxManualPicks.json:207` |  |
| `FireballTower_Projectile` | JSON picks | `Hovl/AAA/Projectiles(Particle collision)/Projectile 25 orange explosion` | yes | data:abilities.json:31, data:abilities.json:371, Village/Buildings/DefenseTower.cs:1168, Village/Buildings/DefenseTower.cs:1176 | owner-tagged<br>`VfxManualPicks.json:214` |  |
| `FireFromTower-ArcaneTowerLevel3_Aura` | JSON picks | `Hovl/RPGBundle/Buff orange shot` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:186` |  |
| `FireImpact_Impact` | JSON picks | `Hovl/AAA/Flash and hits/Hit 25 orange explosion` | yes | data:abilities.json:32, data:abilities.json:372, data:abilities.json:517, data:abilities.json:735 +2 | owner-tagged<br>`VfxManualPicks.json:193` |  |
| `Fountain_Heal_Aura` | JSON picks | `Hovl/RPGBundle/Druid aura` | yes | Village/Vfx/ArcaneAura.cs:362 | owner-tagged<br>`VfxManualPicks.json:242` | **CONFLICT** — code Map says `Hovl/RPGBundle/Buff heal` (`HovlVfxCatalogGenerator.cs:183`); JSON wins |
| `Freezing_Impact` | JSON picks | `Hovl/AAA/Projectile VFX loop/Projectile dragon punch` | yes | data:abilities.json:209, data:abilities.json:301, data:abilities.json:499, Village/Buildings/DefenseTower.cs:1115 +3 | owner-tagged<br>`VfxManualPicks.json:249` |  |
| `Freezing_Projectile` | JSON picks | `Hovl/AAA/Flash and hits/Flash 10 blue laser` | yes | data:abilities.json:207, data:abilities.json:299, data:abilities.json:498, Village/Buildings/DefenseTower.cs:1105 +1 | owner-tagged<br>`VfxManualPicks.json:256` |  |
| `Frost_Impact` | JSON picks | `Hovl/AAA/Projectile VFX loop/Projectile dragon punch` | yes | Village/Hero/WeaponVfxMap.cs:218 | owner-tagged<br>`VfxManualPicks.json:263` | **CONFLICT** — code Map says `Hovl/AAA/Flash and hits/Hit 26 blue crystal` (`HovlVfxCatalogGenerator.cs:147`); JSON wins |
| `Frost_Projectile` | JSON picks | `Hovl/AAA/Projectiles(Particle collision)/Projectile 14 blue rapid` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:270` | **CONFLICT** — code Map says `Hovl/AAA/Projectile VFX loop/Projectile 26 blue diamond` (`HovlVfxCatalogGenerator.cs:146`); JSON wins |
| `Haste_Cast` | JSON picks | `Lana/States/Aura_acceleration` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:277` |  |
| `Heal_Aura` | JSON picks | `Hovl/RPGBundle/Buff palladin` | yes | Village/Hero/HeroAbilities.cs:1178, Village/Hero/HeroAbilities.cs:2094 | owner-tagged<br>`VfxManualPicks.json:284` | **CONFLICT** — code Map says `Hovl/RPGBundle/Buff heal` (`HovlVfxCatalogGenerator.cs:177`); JSON wins |
| `Heal_Cast` | JSON picks | `Hovl/RPGBundle/Buff white twist` | yes | data:motion-castings.json:206, Village/Hero/HeroAbilities.cs:1177, Village/Hero/HeroAbilities.cs:2093, Village/Items/ConsumableUseService.cs:258 | owner-tagged<br>`VfxManualPicks.json:291` | **CONFLICT** — code Map says `Hovl/MagicCircles/Magic circle sun` (`HovlVfxCatalogGenerator.cs:176`); JSON wins |
| `HealingFountain_Aura` | JSON picks | `Hovl/RPGBundle/Druid aura` | yes | Village/Buildings/HealingFountain.cs:78 | owner-tagged<br>`VfxManualPicks.json:298` | loop |
| `Holy_Aura` | JSON picks | `Hovl/MagicCircles/Magic circle dark star` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:305` |  |
| `Holy_Impact` | JSON picks | `Lana/Range_attack/Hit_light` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:312` |  |
| `icebasedprojectile_Projectile` | JSON picks | `ParticlePack/Magic Effects/Prefabs/IceLance` | yes | Village/Buildings/DefenseTower.cs:1169 | owner-tagged<br>`VfxManualPicks.json:907` | loop |
| `IceWeaponAura_Aura` | JSON picks | `Res:VFX/Projectiles/Explosion_Ice` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:319` |  |
| `Junk-DoNotuse_Cast` | JSON picks | `Hovl/AAA/Projectile VFX loop/Projectile 16 fire` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:326` |  |
| `KnightShieldBuff_Aura` | JSON picks | `Res:VFX/Projectiles/Buff_Light` | **NO** | **none** | owner-tagged<br>`VfxManualPicks.json:333` | **NOT BAKED** — prefab absent, key no-ops |
| `KnightThrust_Impact` | JSON picks | `Hovl/RPGBundle/Dragon punch` | yes | data:abilities.json:123, data:abilities.json:333, data:motion-castings.json:309, data:motion-castings.json:346 | owner-tagged<br>`VfxManualPicks.json:340` |  |
| `KnightWeaponskill_Impact` | JSON picks | `Hovl/AOE/Energy explosion` | yes | data:abilities.json:160, data:abilities.json:317, data:abilities.json:406, data:abilities.json:474 +1 | owner-tagged<br>`VfxManualPicks.json:347` |  |
| `LevelUp_Burst` | code Map | `Hovl/RPGBundle/Lvl up` | yes | Village/Vfx/ArcaneAura.cs:92 | code-guessed<br>`HovlVfxCatalogGenerator.cs:154` |  |
| `lighteningOnSpellLand_Impact` | JSON picks | `Hovl/RPGBundle/Electro splash` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:914` |  |
| `LongCastSpell_Cast` | JSON picks | `Hovl/RPGBundle/Buff chain` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:354` |  |
| `MageMeoteorAOE_Cast` | JSON picks | `Lana/Top_down_attack/top_down_stone_line` | yes | data:abilities.json:628 | owner-tagged<br>`VfxManualPicks.json:361` |  |
| `Melee_Impact` | JSON picks | `Hovl/AAA/Projectiles 2D/2D Projectile 24 green explosion` | yes | data:motion-castings.json:177 | owner-tagged<br>`VfxManualPicks.json:368` | **CONFLICT** — code Map says `Hovl/RPGBundle/Punch Hit` (`HovlVfxCatalogGenerator.cs:170`); JSON wins |
| `Melee_Slash` | JSON picks | `Hovl/RPGBundle/Dragon punch` | yes | data:motion-castings.json:308, data:motion-castings.json:321, data:motion-castings.json:333, data:motion-castings.json:345 +1 | owner-tagged<br>`VfxManualPicks.json:375` | **CONFLICT** — code Map says `Hovl/AOE/Flower slash` (`HovlVfxCatalogGenerator.cs:169`); JSON wins |
| `Node_Aura` | JSON picks | `Res:VFX/Aura/Aura_PetLevel2` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:382` | loop |
| `NoneMageHealingCast_Cast` | JSON picks | `Hovl/RPGBundle/Buff white twist` | yes | data:abilities.json:142, data:abilities.json:282, data:abilities.json:424, data:abilities.json:458 +2 | owner-tagged<br>`VfxManualPicks.json:389` |  |
| `onweaponskillmaybe_Impact` | JSON picks | `Hovl/AAA/Flash and hits/Hit 20 pink arrow` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:921` |  |
| `Poi_Landmark` | code Map | `Hovl/MapMarkers/Marker 4 Pillar Loop` | yes | Village/Progression/CastleDefensePlansService.cs:406, Village/Vfx/PoiCalloutSystem.cs:13, Village/Vfx/PoiCalloutSystem.cs:64 | code-guessed<br>`HovlVfxCatalogGenerator.cs:255` |  |
| `Poi_NodeAura` | code Map | `Res:VFX/Aura/Aura_PetLevel2` | yes | Village/HubStructureVisualInjector.cs:599, Village/Catalog/StructureFactory.cs:1018, Village/Vfx/PoiCalloutSystem.cs:9, Village/Vfx/PoiCalloutSystem.cs:51 +2 | code-guessed<br>`HovlVfxCatalogGenerator.cs:251` | loop |
| `portal(rotate)_Aura` | JSON picks | `Hovl/MagicCircles/Magic circle dark star` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:928` |  |
| `Portal_Threshold_Aura` | JSON picks | `MirzaUVFX/Prefabs/Loop/pf_vfx-ult_demo_psys_loop_portalBlue` | yes | Core/World/PortalStructure.cs:64, Village/World/DungeonWorldPortalSpawner.cs:740 | owner-tagged<br>`VfxManualPicks.json:753` | loop |
| `Posion_Cast` | JSON picks | `SpellsPack/Variations/Spells/Nature/Spell_Nature_2_Green Variant` | yes | data:abilities.json:80 | owner-tagged<br>`VfxManualPicks.json:767` |  |
| `PosionCloud_Cast` | JSON picks | `Hovl/AAA/Flash and hits/Hit 24 green explosion` | yes | Village/Hero/WeaponVfxMap.cs:236 | owner-tagged<br>`VfxManualPicks.json:760` |  |
| `PP_BigExplosion` | JSON picks | `ParticlePack/Fire & Explosion Effects/Prefabs/BigExplosion` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:396` |  |
| `PP_BigSplash` | JSON picks | `ParticlePack/Water Effects/Prefabs/BigSplash` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:403` |  |
| `PP_Candles` | JSON picks | `ParticlePack/Misc Effects/Prefabs/Candles` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:410` | loop |
| `PP_Dissolve` | JSON picks | `ParticlePack/Misc Effects/Prefabs/Dissolve` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:417` |  |
| `PP_DissolveSolidHorizontal` | JSON picks | `ParticlePack/Misc Effects/Prefabs/DissolveSolidHorizontal` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:424` |  |
| `PP_DustExplosion` | JSON picks | `ParticlePack/Fire & Explosion Effects/Prefabs/DustExplosion` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:431` |  |
| `PP_DustMotesEffect` | JSON picks | `ParticlePack/Misc Effects/Prefabs/DustMotesEffect` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:438` | loop |
| `PP_DustStorm` | JSON picks | `ParticlePack/Smoke & Steam Effects/Prefabs/DustStorm` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:445` | loop |
| `PP_EarthShatter` | JSON picks | `ParticlePack/Magic Effects/Prefabs/EarthShatter` | yes | _(tooling only)_ Editor/Regression/VfxParticleNullSlotRegression.cs:90 | owner-tagged<br>`VfxManualPicks.json:452` |  |
| `PP_ElectricalSparks` | JSON picks | `ParticlePack/Misc Effects/Prefabs/ElectricalSparks` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:459` | loop |
| `PP_ElectricalSparksEffect` | JSON picks | `ParticlePack/Legacy Particles/Prefabs/ElectricalSparksEffect` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:466` | loop |
| `PP_EllenDissolve` | JSON picks | `ParticlePack/Misc Effects/Prefabs/EllenDissolve` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:473` |  |
| `PP_EllenRespawn` | JSON picks | `ParticlePack/Misc Effects/Prefabs/EllenRespawn` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:480` |  |
| `PP_EnergyExplosion` | JSON picks | `ParticlePack/Fire & Explosion Effects/Prefabs/EnergyExplosion` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:487` |  |
| `PP_FireBall` | JSON picks | `ParticlePack/Fire & Explosion Effects/Prefabs/FireBall` | yes | Village/Enemies/Enemy.cs:1778 | owner-tagged<br>`VfxManualPicks.json:494` | loop |
| `PP_FireFlies` | JSON picks | `ParticlePack/Misc Effects/Prefabs/FireFlies` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:501` | loop |
| `PP_FlameStream` | JSON picks | `ParticlePack/Fire & Explosion Effects/Prefabs/FlameStream` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:508` | loop |
| `PP_FlameThrower` | JSON picks | `ParticlePack/Fire & Explosion Effects/Prefabs/FlameThrower` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:515` | loop |
| `PP_FleshImpacts` | JSON picks | `ParticlePack/Weapon Effects/Prefabs/FleshImpacts` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:522` | loop |
| `PP_GoopSpray` | JSON picks | `ParticlePack/Goop Effects/Prefabs/GoopSpray` | yes | _(tooling only)_ Editor/Regression/VfxParticleNullSlotRegression.cs:91 | owner-tagged<br>`VfxManualPicks.json:529` | loop |
| `PP_GoopSprayEffect` | JSON picks | `ParticlePack/Goop Effects/Prefabs/GoopSprayEffect` | yes | _(tooling only)_ Editor/Regression/VfxParticleNullSlotRegression.cs:92 | owner-tagged<br>`VfxManualPicks.json:536` | loop |
| `PP_GoopStreamEffect` | JSON picks | `ParticlePack/Goop Effects/Prefabs/GoopStreamEffect` | yes | _(tooling only)_ Editor/Regression/VfxParticleNullSlotRegression.cs:93 | owner-tagged<br>`VfxManualPicks.json:543` | loop |
| `PP_GroundFog` | JSON picks | `ParticlePack/Smoke & Steam Effects/Prefabs/GroundFog` | yes | Village/World/DungeonWorldPortalSpawner.cs:684, Village/World/DungeonWorldPortalSpawner.cs:697 | owner-tagged<br>`VfxManualPicks.json:550` | loop |
| `PP_HeatDistortion` | JSON picks | `ParticlePack/Smoke & Steam Effects/Prefabs/HeatDistortion` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:557` | loop |
| `PP_IceLance` | JSON picks | `ParticlePack/Magic Effects/Prefabs/IceLance` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:564` | loop |
| `PP_LargeFlames` | JSON picks | `ParticlePack/Fire & Explosion Effects/Prefabs/LargeFlames` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:571` | loop |
| `PP_LightnigStormCloud` | JSON picks | `ParticlePack/Legacy Particles/Prefabs/LightnigStormCloud` | yes | _(tooling only)_ Editor/Regression/VfxParticleNullSlotRegression.cs:94 | owner-tagged<br>`VfxManualPicks.json:578` | loop |
| `PP_MediumFlames` | JSON picks | `ParticlePack/Fire & Explosion Effects/Prefabs/MediumFlames` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:585` | loop |
| `PP_MetalImpacts` | JSON picks | `ParticlePack/Weapon Effects/Prefabs/MetalImpacts` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:592` | loop |
| `PP_MuzzleFlash` | JSON picks | `ParticlePack/Weapon Effects/Prefabs/MuzzleFlash` | yes | data:abilities.json:189, data:abilities.json:264, Village/Buildings/DefenseTower.cs:1106, Village/Buildings/TowerCombat.cs:497 | owner-tagged<br>`VfxManualPicks.json:599` |  |
| `PP_PlasmaExplosionEffect` | JSON picks | `ParticlePack/Legacy Particles/Prefabs/PlasmaExplosionEffect` | yes | Village/Buildings/ArcaneTower.cs:556, Village/Buildings/DefenseTower.cs:1116, Village/Buildings/TowerCombat.cs:514 | owner-tagged<br>`VfxManualPicks.json:606` |  |
| `PP_PoisonGas` | JSON picks | `ParticlePack/Smoke & Steam Effects/Prefabs/PoisonGas` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:613` | loop |
| `PP_PressurisedSteam` | JSON picks | `ParticlePack/Smoke & Steam Effects/Prefabs/PressurisedSteam` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:620` | loop |
| `PP_RainEffect` | JSON picks | `ParticlePack/Legacy Particles/Prefabs/RainEffect` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:627` | loop |
| `PP_Respawn` | JSON picks | `ParticlePack/Misc Effects/Prefabs/Respawn` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:634` |  |
| `PP_RisingSteam` | JSON picks | `ParticlePack/Smoke & Steam Effects/Prefabs/RisingSteam` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:641` | loop |
| `PP_RocketTrail` | JSON picks | `ParticlePack/Smoke & Steam Effects/Prefabs/RocketTrail` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:648` |  |
| `PP_SandImpacts` | JSON picks | `ParticlePack/Weapon Effects/Prefabs/SandImpacts` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:655` | loop |
| `PP_SandSwirlsEffect` | JSON picks | `ParticlePack/Misc Effects/Prefabs/SandSwirlsEffect` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:662` | loop |
| `PP_Shower` | JSON picks | `ParticlePack/Water Effects/Prefabs/Shower` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:669` | loop |
| `PP_SmallExplosion` | JSON picks | `ParticlePack/Fire & Explosion Effects/Prefabs/SmallExplosion` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:676` |  |
| `PP_SmokeEffect` | JSON picks | `ParticlePack/Smoke & Steam Effects/Prefabs/SmokeEffect` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:683` | loop |
| `PP_SparksEffect` | JSON picks | `ParticlePack/Legacy Particles/Prefabs/SparksEffect` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:690` | loop |
| `PP_Steam` | JSON picks | `ParticlePack/Smoke & Steam Effects/Prefabs/Steam` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:697` | loop |
| `PP_StoneImpacts` | JSON picks | `ParticlePack/Weapon Effects/Prefabs/StoneImpacts` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:704` | loop |
| `PP_TinyExplosion` | JSON picks | `ParticlePack/Fire & Explosion Effects/Prefabs/TinyExplosion` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:711` |  |
| `PP_TinyFlames` | JSON picks | `ParticlePack/Fire & Explosion Effects/Prefabs/TinyFlames` | yes | Village/Hero/GearAuraMap.cs:56 | owner-tagged<br>`VfxManualPicks.json:718` | loop |
| `PP_WaterFall` | JSON picks | `ParticlePack/Legacy Particles/Prefabs/WaterFall` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:725` | loop |
| `PP_WaterLeak` | JSON picks | `ParticlePack/Water Effects/Prefabs/WaterLeak` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:732` | loop |
| `PP_WildFire` | JSON picks | `ParticlePack/Fire & Explosion Effects/Prefabs/WildFire` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:739` | loop |
| `PP_WoodImpacts` | JSON picks | `ParticlePack/Weapon Effects/Prefabs/WoodImpacts` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:746` | loop |
| `Raid_Explosion` | code Map | `Hovl/AOE/Meteor hit` | yes | Village/Vfx/StructureDamageVisuals.cs:54, Village/Vfx/StructureDamageVisuals.cs:256 | code-guessed<br>`HovlVfxCatalogGenerator.cs:153` |  |
| `RangedAttack-DaggerThrow_Projectile` | JSON picks | `Hovl/AAA/Projectiles 2D/2D Projectile 8 dagger` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:774` |  |
| `RangedSpell-Powerful(Longcast)_Cast` | JSON picks | `Hovl/AAA/Projectiles 2D/2D Projectile 17 nova violet` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:781` |  |
| `RangerTowerBaseProjectile_Projectile` | JSON picks | `Hovl/AAA/Projectiles 2D/2D Projectile 1 nature arrow` | yes | data:abilities.json:190, data:abilities.json:265, Village/Buildings/DefenseTower.cs:1152 | owner-tagged<br>`VfxManualPicks.json:788` |  |
| `RangerTowerlevel2Projectile_Projectile` | JSON picks | `Hovl/AAA/Projectiles 2D/2D Projectile 14 blue rapid` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:802` |  |
| `RangerTowerUpgraded_Projectile` | JSON picks | `Hovl/AAA/Projectiles with logic/Projectile 7 pink` | yes | data:abilities.json:241, Village/Buildings/DefenseTower.cs:1148 | owner-tagged<br>`VfxManualPicks.json:795` |  |
| `ShieldBuff_Cast` | JSON picks | `SpellsPack/Projectiles/Casting/Casting_Arcane` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:809` |  |
| `SimpleCast_Cast` | JSON picks | `Hovl/AAA/Flash and hits/Flash 16 fire` | yes | data:abilities.json:515, data:abilities.json:733, Village/Buildings/ArcaneTower.cs:443, Village/Buildings/DefenseTower.cs:1104 +1 | owner-tagged<br>`VfxManualPicks.json:816` |  |
| `SimpleCast_Projectile` | JSON picks | `Hovl/AAA/Flash and hits/Flash 10 blue laser` | yes | data:abilities.json:516, data:abilities.json:734, Village/Buildings/DefenseTower.cs:1203 | owner-tagged<br>`VfxManualPicks.json:823` |  |
| `Sleep_Impact` | JSON picks | `Lana/States/Character_status_sleep` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:830` | loop |
| `softhealingaura_Aura` | JSON picks | `Hovl/RPGBundle/Buff palladin` | yes | data:abilities.json:143, data:abilities.json:283, data:abilities.json:425, data:abilities.json:459 +2 | owner-tagged<br>`VfxManualPicks.json:935` |  |
| `Spear_Impact` | code Map | `Hovl/AAA/Flash and hits/Hit 11 orange arrow` | yes | data:abilities.json:191, data:abilities.json:266, Village/Buildings/DefenseTower.cs:1117, Village/Buildings/TowerCombat.cs:515 | code-guessed<br>`HovlVfxCatalogGenerator.cs:166` |  |
| `Spear_Projectile` | JSON picks | `Hovl/AAA/Projectiles 2D/2D Projectile 1 nature arrow` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:837` | **CONFLICT** — code Map says `Hovl/AAA/Projectile VFX loop/Projectile 11 orange arrow` (`HovlVfxCatalogGenerator.cs:165`); JSON wins |
| `SpecialAbilityMage_Cast` | JSON picks | `Lana/Top_down_attack/top_down_starfall_line_blue` | yes | data:abilities.json:580, Village/Hero/HeroAbilities.cs:2212 | owner-tagged<br>`VfxManualPicks.json:844` |  |
| `subtleHealinginarea(EnemySkill-Mage)_Cast` | JSON picks | `Hovl/RPGBundle/Buff orange circle` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:942` |  |
| `targetforSpell_Impact` | JSON picks | `Hovl/MapMarkers/Marker 2 Pointer Loop` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:949` |  |
| `Taunt_Aura` | code Map | `Hovl/MagicCircles/Loop version/Magic circle blood loop` | yes | data:abilities.json:391 | code-guessed<br>`HovlVfxCatalogGenerator.cs:187` |  |
| `Taunt_Roar` | code Map | `Hovl/AOE/Energy explosion` | yes | data:abilities.json:389 | code-guessed<br>`HovlVfxCatalogGenerator.cs:186` |  |
| `Thunderbolt_Cast` | JSON picks | `Hovl/AAA/Projectiles(Particle collision)/Projectile 2 electro` | yes | data:abilities.json:350 | owner-tagged<br>`VfxManualPicks.json:851` | **CONFLICT** — code Map says `Hovl/AAA/Flash and hits/Flash 2 electro` (`HovlVfxCatalogGenerator.cs:162`); JSON wins |
| `Thunderbolt_Impact` | JSON picks | `Hovl/AAA/Projectiles with logic/Projectile 2 electro` | yes | data:abilities.json:352, Village/Hero/WeaponVfxMap.cs:228 | owner-tagged<br>`VfxManualPicks.json:858` | **CONFLICT** — code Map says `Hovl/AAA/Flash and hits/Hit 2 electro` (`HovlVfxCatalogGenerator.cs:104`); JSON wins |
| `Thunderbolt_Projectile` | JSON picks | `Hovl/AAA/Projectiles(Particle collision)/Projectile 2 electro` | yes | data:abilities.json:351, Village/Hero/AbilityCatalog.cs:162 | owner-tagged<br>`VfxManualPicks.json:865` | **CONFLICT** — code Map says `Hovl/AAA/Projectile VFX loop/Projectile 2 electro` (`HovlVfxCatalogGenerator.cs:103`); JSON wins |
| `TreeofLifeAura_Aura` | JSON picks | `ParticlePack/Misc Effects/Prefabs/FireFlies` | yes | Village/HubStructureVisualInjector.cs:600, Village/Catalog/StructureFactory.cs:1019, Village/Vfx/AmbientAuraPolicy.cs:49, Village/Vfx/AmbientAuraPolicy.cs:51 | owner-tagged<br>`VfxManualPicks.json:872` | loop |
| `UpgradeStructureComplete_Aura` | JSON picks | `MirzaUVFX/Demos/Fireworks/Fireworks` | yes | Village/BuildMode/BuildModeController.cs:2498, Village/BuildMode/BuildModeController.cs:2518 | owner-tagged<br>`VfxManualPicks.json:879` |  |
| `UpgradeVisual_Aura` | JSON picks | `Lana/Orbs/Orbs_fire` | yes | Village/BuildMode/UnderConstructionVisual.cs:84, Village/BuildMode/UnderConstructionVisual.cs:298 | owner-tagged<br>`VfxManualPicks.json:886` |  |
| `Water_Projectile` | JSON picks | `Hovl/AAA/Projectiles with logic/Projectile 9 water` | yes | **none** | owner-tagged<br>`VfxManualPicks.json:893` |  |
| `Weaponskillsword_Impact` | JSON picks | `Hovl/AAA/Flash and hits/Hit 5 red` | yes | data:abilities.json:108, data:abilities.json:124, data:abilities.json:334, data:abilities.json:390 +1 | owner-tagged<br>`VfxManualPicks.json:900` |  |


### Withheld keys — registered nowhere, deliberately

| Key | State | Citation |
|---|---|---|
| `Arcane_Aura` | **WITHHELD.** No Map row, no JSON row, no baked row, no Caster tag. Its old pick was `Magic circle sun loop.prefab`, banned by the owner 2026-08-16. A replacement awaits an owner tag; the CLI must not substitute one. `PlayKey("Arcane_Aura")` degrades to the throttled `hovl-nokey` FlowTrace no-op that `ArcaneAura.cs` is documented to tolerate. | `HovlVfxCatalogGenerator.cs:110-119`; call site `ArcaneTower.cs:217` |

---

## 2. FINDING 1 — the 17 keys whose sources disagree

19 keys are written in **both** `HovlVfxCatalogGenerator.Map` and `VfxManualPicks.json`.
**2 agree; 17 disagree — and in all 17 the JSON pick is what got baked.** Verified by parsing the
baked `HovlVfxCatalog.asset` GUIDs back to paths through the `.meta` index: baked == JSON, 17/17.

| Key | code Map says (`HovlVfxCatalogGenerator.cs`) | JSON picks say (`VfxManualPicks.json`) | Baked = |
|---|---|---|---|
| `Arcane_Cast` | `AAA/Flash and hits/Flash 17 nova violet` `:108` | `AAA/Flash and hits/Flash 16 fire` `:26` | JSON |
| `Arcane_Impact` | `AAA/Flash and hits/Hit 17 nova violet` `:109` | `ParticlePack/Legacy Particles/PlasmaExplosionEffect` `:33` | JSON |
| `Arcane_Projectile` | `AAA/Projectile VFX loop/Projectile 17 nova violet` `:107` | `RPGBundle/Buff orange shot` `:40` | JSON |
| `Fireball_Cast` | `AAA/Flash and hits/Flash 16 fire` `:99` | `AAA/Flash and hits/Hit 16 fire` `:222` | JSON |
| `Fireball_Impact` | `AAA/Flash and hits/Hit 16 fire` `:100` | `AAA/Flash and hits/Hit 25 orange explosion` `:229` | JSON |
| `Fireball_Projectile` | `AAA/Projectile VFX loop/Projectile 16 fire` `:98` | `AAA/Projectiles(Particle collision)/Projectile 25 orange explosion` `:236` | JSON |
| `Fountain_Heal_Aura` | `RPGBundle/Buff heal` `:183` | `RPGBundle/Druid aura` `:243` | JSON |
| `Frost_Impact` | `AAA/Flash and hits/Hit 26 blue crystal` `:147` | `AAA/Projectile VFX loop/Projectile dragon punch` `:264` | JSON |
| `Frost_Projectile` | `AAA/Projectile VFX loop/Projectile 26 blue diamond` `:146` | `AAA/Projectiles(Particle collision)/Projectile 14 blue rapid` `:271` | JSON |
| `Heal_Aura` | `RPGBundle/Buff heal` `:177` | `RPGBundle/Buff palladin` `:285` | JSON |
| `Heal_Cast` | `MagicCircles/Magic circle sun` `:176` | `RPGBundle/Buff white twist` `:292` | JSON |
| `Melee_Impact` | `RPGBundle/Punch Hit` `:170` | `AAA/Projectiles 2D/2D Projectile 24 green explosion` `:369` | JSON |
| **`Melee_Slash`** | `AOE/Flower slash` `:169` | `RPGBundle/Dragon punch` `:376` | JSON |
| `Spear_Projectile` | `AAA/Projectile VFX loop/Projectile 11 orange arrow` `:165` | `AAA/Projectiles 2D/2D Projectile 1 nature arrow` `:838` | JSON |
| `Thunderbolt_Cast` | `AAA/Flash and hits/Flash 2 electro` `:162` | `AAA/Projectiles(Particle collision)/Projectile 2 electro` `:852` | JSON |
| `Thunderbolt_Impact` | `AAA/Flash and hits/Hit 2 electro` `:104` | `AAA/Projectiles with logic/Projectile 2 electro` `:859` | JSON |
| `Thunderbolt_Projectile` | `AAA/Projectile VFX loop/Projectile 2 electro` `:103` | `AAA/Projectiles(Particle collision)/Projectile 2 electro` `:866` | JSON |

The 2 that agree: `Aegis_Cast` (`:190` / `:12`) and `Cleave_Impact` (`:173` / `:110`).

**Why this is a real hazard, not bookkeeping.** The losing code lines carry paragraphs of design
rationale (`Frost_Projectile`'s "blue diamond", `Melee_Slash`'s "Knight melee slash") that no longer
describe anything on screen, and `VFXManager.Hovl.cs:33-62` republishes those same dead picks in a
header a reader is likely to trust. **Rule of thumb: to answer "what does key X look like?", read the
baked `HovlVfxCatalog.asset` row, or this table. Never read the C# Map.**

Secondary drift, same shape: the code Map's `isLoop:` literal is **discarded at bake time** — the
generator derives `IsLoop` from the prefab and logs the disagreement
(`HovlVfxCatalogGenerator.cs:457-474`, rationale `:440-456`). The JSON `isLoop` field is likewise
advisory; measured now, **0 of 136 JSON rows disagree with the baked flag** (all agree post-derive).

---

## 3. FINDING 2 — keys resolving to a prefab that DOES NOT EXIST

Both fail *soft*: `Build()` warns and skips the row (`HovlVfxCatalogGenerator.cs:376-381` for Map rows,
`:397-404` for manual rows), the key never reaches `HovlVfxCatalog.asset`, and
`PlayKey` degrades to a throttled `hovl-nokey` no-op (`VFXManager.Hovl.cs:208-214`).
**The generator still prints `HOVL_VFX_CATALOG_OK`** (`:344`) — a missing prefab is not gate-visible.

| Key | Names | Exists? | Baked? | Live consumer | Diagnosis |
|---|---|---|---|---|---|
| `Ember_Burn` | `Hovl/RPGBundle/Debuff 1.prefab` — `HovlVfxCatalogGenerator.cs:204` | **NO.** The pack ships `Debuff chain.prefab` and `Debuff scythe.prefab`; there is no `Debuff 1`. | no | `abilities.json:373` — `knight.emberbrand-throw` `vfxResidual` | **Dead since authoring.** Documented in place at `:194-203`, deliberately NOT repaired: naming the replacement is an owner pick. Needs an **owner tag**. |
| `KnightShieldBuff_Aura` | `Assets/Resources/VFX/Projectiles/Buff_Light.prefab` — `VfxManualPicks.json:334` | **NO** — the file is at `Assets/Resources/VFX/**Buffs**/Buff_Light.prefab` | no | `abilities.json:442` — `knight.eternal-aegis` `vfxResidual` | **Duplicate authority / one-word path split.** `StatusVfxMirrors.cs:56-57` mirrors the Spells Pack `Buff_Light` to `Resources/VFX/Buffs/`; the owner-tagged JSON row names `Resources/VFX/Projectiles/`. Both are "owner intent"; only the mirror is on disk. Needs an **owner confirmation** of which folder, then one path edit. |

Everything else resolves: **152 of 154 keys point at a prefab present on disk.**

---

## 4. FINDING 3 — the banned-effect exposure is CLOSED (as of the 2026-08-16 regen)

Two owner bans of 2026-08-16 are pinned by `BannedVfxRegression` (`Assets/Editor/Regression/BannedVfxRegression.cs:74-84`):

1. `Assets/Resources/VFX/Projectiles/Spell_Fire_6.prefab` — *"Do Not use anywhere"* (colour variants
   `_Blue/_Green/_Purple/_Yellow` deliberately out of scope, `:24-28`).
2. `Assets/Hovl Studio/Magic circles/Prefabs/Loop version/Magic circle sun loop.prefab` — *"remove"*
   (the LOOP prefab only; `Magic circle sun` / `sun sparks` / `sunS loop` are **not** banned, `:29-34`).

**Verified this session, at source, across every store:**

| Store | Rows pointing at a banned prefab |
|---|---|
| `HovlVfxCatalogGenerator.Map` (37) | **0** |
| `VfxManualPicks.json` (136) | **0** |
| `VfxCasterLibraryIndex.json` (152 tagged) | **0** — the two `Magic circle sun loop` entries (`:259-260`) and the six `Spell_Fire_6*` entries (`:2468-2477`) all carry `"key":""`, `"catalogued":false` — scanned, untagged |
| `HovlVfxCatalog.asset` (152 baked) | **0** |
| `VFXCatalog.asset` (75 baked) | **0** |
| `Assets/Resources/VFX/Projectiles/` on disk | `Spell_Fire_6.prefab` is **gone** — the mirror was removed (`SpellsPackVfxMirror.cs:26-27`) |

The three keys the ban displaced landed on their owner-tagged replacements, all present and baked:

| Key | Now resolves to | Owner tag (verbatim, 2026-08-16) |
|---|---|---|
| `Dungeon_Portal_Gate` | `Hovl/MagicCircles/Magic circle dark star` | *"Magic circle dark star.prefab — use this rotated for the portals"* (`HovlVfxCatalogGenerator.cs:120-129`) |
| `Poi_NodeAura` | `Res:VFX/Aura/Aura_PetLevel2` | *"Aura_PetLevel2 → Node Auras"* (`:242-251`) |
| `Arcane_Aura` | **withheld** — no replacement tagged | `:110-119` |

### ⚠ The structural gap that remains

`BannedVfxRegression` Case 2 scans **only the two baked `.asset` files** (`:93-97`) — it does **not**
scan `VfxManualPicks.json` or `VfxCasterLibraryIndex.json`. Case 1 scans `.cs` under `Assets/_Modules`
and `Assets/Editor` (`:90`), which excludes `.json` entirely. So today's clean state is **clean because
the bake is current**, not because the winning store is guarded:

> An owner re-tagging a banned prefab in the VFX Caster writes `VfxManualPicks.json` and the suite
> stays green until someone re-runs the generator — at which point the bake goes red, *after* the tag
> was accepted. Closing this is a one-line scope widen: add the two JSON stores to `CatalogAssets`
> (`BannedVfxRegression.cs:93-97`), whose basename scan already works on plain text.

---

## 5. FINDING 4 — casting telegraph: 6 of 7 registered schools resolve to nothing

`CastingTelegraphVfx` replaces the HUD cast bar with a school-matched `Casting_*` loop on the caster
(owner ruling 2026-08-16, `CastingTelegraphVfx.cs:7-9`). It registers **7 schools**
(`:63-73`) and loads each from a committed mirror under `Resources/VFX/Projectiles/`.
**Only `Casting_Fire` is mirrored** — confirmed by directory listing.

| School | Resources path (`:66-72`) | Mirror on disk | Result |
|---|---|---|---|
| **`arcane`** ⚠ | `VFX/Projectiles/Casting_Arcane` | **NO** | **This is `DefaultSchool` (`:91`)** — the terminal fallback for every spell that matches no keyword |
| `dark` | `VFX/Projectiles/Casting_Dark` | **NO** | warn + HUD bar |
| `fire` | `VFX/Projectiles/Casting_Fire` | **yes** | telegraph plays |
| `ice` | `VFX/Projectiles/Casting_Ice` | **NO** | warn + HUD bar |
| `light` | `VFX/Projectiles/Casting_Light` | **NO** | warn + HUD bar |
| `nature` | `VFX/Projectiles/Casting_Nature` | **NO** | warn + HUD bar |
| `storm` | `VFX/Projectiles/Casting_Storm` | **NO** | warn + HUD bar |

`Casting_Fire_2.prefab` is also mirrored but nothing loads it — the class documents `_2/_3/_4` as
"owner-retaggable alternates" (`:62`) and only ever loads the BASE variant. It is an orphan (§7).

**The failure is safe but silent-ish.** A missing mirror hits `WarnOnce` (`:155-157`) → one
`FlowTrace.Warn` per school per session, and `TryBegin` returns null so `IsTelegraphed(caster)` stays
false and **the HUD cast bar is kept** (`:22-25`, `:172-180`). Nothing breaks; the feature simply does
not exist for 6 of 7 schools, including the default. A `FlowTrace.Warn` is not an F8 capture trigger
(the harness fires on `flagged`/error/exception/softlock — CLAUDE.md §14), so this will not surface to
the owner on its own.

**To finish the feature:** mirror the six remaining `Casting_*` prefabs from
`Assets/Spells Pack/Particles/Prefabs/Projectiles/Casting/` the way `StatusVfxMirrors` does — the
Spells Pack is gitignored, so it is a **dependency mirror**, not a file copy (`StatusVfxMirrors.cs:54-57`
records that exact trap for `Buff_Light`).

---

## 6. FINDING 5 — the second key space: `VFXType` → `VFXCatalog.asset`

95 enum members (`Assets/_Modules/Village/Vfx/VFXType.cs:21`), **75 baked**, all 75 resolving to a
prefab on disk.

| Ord | VFXType | Prefab | On disk | Mode | Pool |
|---|---|---|---|---|---|
| 1 | `Impact_Physical` | `Lana/Slash/Slash_stone_once` | yes | oneshot | 4 |
| 2 | `Impact_Aether` | `Lana/Range_attack/Hit_magic` | yes | oneshot | 4 |
| 3 | `Impact_Flame` | `Res:VFX/Status/BigExplosion` | yes | oneshot | 4 |
| 4 | `Impact_Ice` | `Lana/Range_attack/Hit_frost` | yes | oneshot | 4 |
| 5 | `Impact_Heal` | `Lana/Range_attack/Hit_heart` | yes | oneshot | 4 |
| 6 | `Impact_ExplosionFire` | `SpellsPack/Projectiles/Explosion/Explosion_Fire` | yes | oneshot | 4 |
| 7 | `Impact_ExplosionAether` | `SpellsPack/Projectiles/Explosion/Explosion_Arcane` | yes | oneshot | 4 |
| 8 | `Impact_ShockwaveRing` | `Lana/Burst/Burst_rings` | yes | oneshot | 4 |
| 9 | `Impact_ShardsBurst` | `Lana/Burst/Burst_sharp` | yes | oneshot | 4 |
| 10 | `Impact_SmokeWisps` | `Lana/Burst/Poof_generic` | yes | oneshot | 4 |
| 11 | `Projectile_ArcaneBolt` | `Res:VFX/Projectiles/Projectile_Arcane` | yes | loop | 4 |
| 12 | `Projectile_FrostBolt` | `Res:VFX/Projectiles/Projectile_Ice` | yes | loop | 4 |
| 13 | `Projectile_Arrow` | `Lana/Range_attack/Projectiles_green_shuriken` | yes | oneshot | 4 |
| 14 | `Projectile_FlameArrow` | `Res:VFX/Projectiles/Projectile_Fire_3` | yes | loop | 4 |
| 18 | `Projectile_EnemyCasterBolt` | `Lana/Range_attack/Projectiles_dark_magic` | yes | oneshot | 4 |
| 19 | `Cast_MageCharge` | `Lana/Orbs/Orbs_electric` | yes | oneshot | 4 |
| 20 | `Cast_FireCharge` | `Res:VFX/Projectiles/Casting_Fire` | yes | oneshot | 4 |
| 21 | `Cast_KnightSlam` | `Lana/Burst/Flash_dubble_circle` | yes | oneshot | 4 |
| 22 | `Cast_RangerDraw` | `Lana/Orbs/Orbs_leaves` | yes | oneshot | 4 |
| 23 | `Cast_Heal` | `SpellsPack/Buffs/Buff_Nature` | yes | oneshot | 4 |
| 24 | `Cast_FrostNova` | `Lana/Area_generic/Area_generic_blue_outbreak` | yes | oneshot | 4 |
| 25 | `Cast_NecromancerSummon` | `Lana/Area_generic/Area_generic_green_outbreak` | yes | oneshot | 4 |
| 26 | `Cast_EnemyCaster` | `Lana/Orbs/Orbs_electric` | yes | oneshot | 4 |
| 27 | `Death_Skeleton` | `Lana/Burst/Poof_generic` | yes | oneshot | 4 |
| 28 | `Death_Boss` | `Res:VFX/Death/Boss_Death` | yes | oneshot | 2 |
| 29 | `Death_Brute` | `Res:VFX/Death/Death_Brute` | yes | oneshot | 6 |
| 30 | `Death_Wolf` | `Lana/Burst/Poof_water` | yes | oneshot | 4 |
| 31 | `Death_Tiefling` | `Res:VFX/Death/Death_Tiefling` | yes | oneshot | 4 |
| 32 | `Death_Generic` | `Res:VFX/Death/Death_Generic` | yes | oneshot | 8 |
| 33 | `Aura_EnemyCaster` | `Res:VFX/Aura/Aura_EnemyCaster` | yes | loop | 4 |
| 34 | `Aura_Necromancer` | `Lana/Fog/Fog_poison` | yes | loop | 4 |
| 35 | `Aura_Healer` | `Lana/Regeneration/Regeneration_health_loop` | yes | loop | 4 |
| 36 | `Aura_Flame` | `Lana/Fire/Fire_medium` | yes | loop | 4 |
| 37 | `Aura_Ice` | `Lana/Fog/Fog_frost` | yes | loop | 4 |
| 38 | `Aura_Dust` | `Res:VFX/Aura/Aura_Dust` | yes | loop | 4 |
| 39 | `Aura_SmokeReaper` | `Lana/Fog/Fog_speedSlow` | yes | loop | 4 |
| 42 | `Aura_PetLevel1` | `Res:VFX/Aura/Aura_PetLevel1` | yes | loop | 3 |
| 43 | `Aura_PetLevel2` | `Res:VFX/Aura/Aura_PetLevel2` | yes | loop | 3 |
| 44 | `Aura_PetLevel3` | `Res:VFX/Aura/Aura_PetLevel3` | yes | loop | 2 |
| 45 | `Env_TorchFlame` | `Lana/Fire/Fire_small` | yes | loop | 4 |
| 48 | `Env_DungeonPortal` | `Res:VFX/Portal/Env_DungeonPortal` | yes | loop | 3 |
| 50 | `Env_DestructionDust` | `Lana/Burst/Poof_generic` | yes | oneshot | 10 |
| 52 | `Juice_CriticalHit` | `Lana/Burst/Flash_star` | yes | oneshot | 4 |
| 53 | `Juice_KillStreak` | `Lana/Burst/Burst_rainbow_mist` | yes | oneshot | 4 |
| 54 | `Juice_WaveClear` | `Lana/States/Level_up` | yes | oneshot | 4 |
| 55 | `Juice_LevelUp` | `Lana/States/Level_up` | yes | oneshot | 4 |
| 58 | `Portal_Enter` | `Res:VFX/Portal/Portal_Enter` | yes | oneshot | 3 |
| 59 | `Portal_Exit` | `Res:VFX/Portal/Portal_Exit` | yes | oneshot | 3 |
| 60 | `WaveClear_Celebration` | `Lana/States/Level_up` | yes | oneshot | 4 |
| 61 | `LevelUp_Celebration` | `Lana/States/Level_up` | yes | oneshot | 4 |
| 62 | `Combo_Tier1` | `Lana/Burst/Flash_circle` | yes | oneshot | 4 |
| 63 | `Combo_Tier2` | `Lana/Burst/Flash_dubble_circle` | yes | oneshot | 4 |
| 68 | `Death_EnemyExplosion_Dungeon` | `Res:VFX/Death/Death_EnemyExplosion_Dungeon` | yes | oneshot | 6 |
| 69 | `Elite_Spawn` | `Res:VFX/Portal/Elite_Spawn` | yes | oneshot | 3 |
| 70 | `Elite_Death` | `Res:VFX/Death/Elite_Death` | yes | oneshot | 4 |
| 71 | `Boss_Spawn` | `Res:VFX/Portal/Boss_Spawn` | yes | oneshot | 2 |
| 72 | `Boss_Death` | `Res:VFX/Death/Boss_Death` | yes | oneshot | 2 |
| 76 | `Boss_Aura_Phase1` | `Res:VFX/Aura/Boss_Aura_Phase1` | yes | loop | 2 |
| 77 | `Boss_Aura_Phase2` | `Res:VFX/Aura/Boss_Aura_Phase2` | yes | loop | 2 |
| 78 | `Boss_Aura_Phase3` | `Res:VFX/Aura/Boss_Aura_Phase3` | yes | loop | 2 |
| 79 | `Boss_FireBreath` | `Res:VFX/Boss/Boss_FireBreath` | yes | loop | 2 |
| 80 | `Env_Candle` | `Res:VFX/Env/Env_Candle` | yes | loop | 6 |
| 81 | `Env_SteamVent` | `Res:VFX/Env/Env_SteamVent` | yes | loop | 4 |
| 82 | `Env_SteamBurst` | `Res:VFX/Env/Env_SteamBurst` | yes | loop | 4 |
| 83 | `Cast_MuzzleFlash` | `Res:VFX/Weapon/Cast_MuzzleFlash` | yes | oneshot | 8 |
| 86 | `Aura_LowHealth` | `Res:VFX/Aura/Aura_LowHealth` | yes | loop | 2 |
| 87 | `Aura_NearDeath` | `Res:VFX/Aura/Aura_NearDeath` | yes | loop | 2 |
| 88 | `Aura_HealingInProgress` | `Res:VFX/Aura/Aura_HealingInProgress` | yes | loop | 2 |
| 89 | `Aura_ItemHeal` | `Res:VFX/Aura/Aura_ItemHeal` | yes | loop | 2 |
| 90 | `Harvest_Iron` | `Res:VFX/Harvest/Harvest_Iron` | yes | loop | 3 |
| 91 | `Harvest_Wood` | `Res:VFX/Harvest/Harvest_Wood` | yes | loop | 3 |
| 92 | `Harvest_Food` | `Res:VFX/Harvest/Harvest_Food` | yes | loop | 3 |
| 93 | `Harvest_Crystal` | `Res:VFX/Harvest/Harvest_Crystal` | yes | loop | 3 |
| 94 | `Harvest_Gold` | `Res:VFX/Harvest/Harvest_Gold` | yes | loop | 3 |
| 95 | `Collector_Ready` | `Res:VFX/Harvest/Collector_Ready` | yes | loop | 4 |

**The 20 enum members with no catalog row** — `VFXManager.Play` on any of these falls through to the procedural billboard fallback:

| Ord | VFXType |
|---|---|
| 15 | `Projectile_TowerArcane` |
| 16 | `Projectile_TowerFire` |
| 17 | `Projectile_TowerIce` |
| 40 | `Aura_HeartPulse` |
| 41 | `Aura_EmpowerTower` |
| 46 | `Env_LanternGlow` |
| 47 | `Env_GroundFog` |
| 49 | `Env_TitleEmbers` |
| 51 | `Env_DestructionSparks` |
| 56 | `Juice_GroundDecal_Flame` |
| 57 | `Juice_GroundDecal_Ice` |
| 64 | `Pet_Aura_Fire` |
| 65 | `Pet_Aura_Ice` |
| 66 | `Pet_Attack` |
| 67 | `ShootingStar` |
| 73 | `Boss_AttackImpact` |
| 74 | `Boss_PhaseTransition` |
| 75 | `Boss_Telegraph` |
| 84 | `Enemy_Spawn` |
| 85 | `Despawn_Dissolve` |

---

## 7. FINDING 6 — orphan prefabs: VFX art on disk that no key names

`Assets/Resources/VFX/**` holds **67 prefabs**. Cross-referenced against the 152 baked string-key rows,
the 75 `VFXCatalog` rows, and every `"VFX/…"` `Resources.Load` literal in the tree, **12 are orphans** —
committed, shipped in the Resources payload, reachable by nothing.

| Orphan prefab | Why it is here | Assessment |
|---|---|---|
| `Aura/FireFlies.prefab` | mirrored by `StatusVfxMirrors.cs:52-53` for the owner tag *"ParticlePack FireFlies → Tree of Life Aura"* | **Mirror wired, catalog not re-pointed.** `TreeofLifeAura_Aura` still resolves to the **gitignored pack** copy (`ParticlePack/Misc Effects/Prefabs/FireFlies`), not this tracked mirror — so the key **breaks on a fresh clone** and the mirror that would fix it sits unused. The mirror file itself even warns about this class of gap (`StatusVfxMirrors.cs:49-51`). **Actionable: re-point the `TreeofLifeAura_Aura` pick at `Assets/Resources/VFX/Aura/FireFlies.prefab` and regen.** |
| `Buffs/Buff_Light.prefab` | mirrored by `StatusVfxMirrors.cs:56-57` for *"Buff_Light.prefab → Knight Shield Buff"* | Orphaned by the one-word path split in §3 — the `KnightShieldBuff_Aura` pick names `Res:VFX/Projectiles/`. Fixing §3 un-orphans this. |
| `Aura/top_down_starfall_line_blue.prefab` | mirrored by `StatusVfxMirrors.cs:59` for *"Lana starfall → Special Ability Mage cast"* | `SpecialAbilityMage_Cast` resolves to the **Lana pack** path instead. Lana is git-tracked, so this is not a clone hazard — but it is a duplicate. |
| `Projectiles/Casting_Fire_2.prefab` | the `_2` alternate of the casting telegraph | `CastingTelegraphVfx` only ever loads the BASE variant (`:62`). Reserved for a future owner retag. |
| `Projectiles/Explosion_Arcane.prefab` | | `VFXType.Impact_ExplosionAether` resolves to the **Spells Pack** original, not this mirror |
| `Projectiles/Explosion_Fire.prefab` | | `VFXType.Impact_ExplosionFire` resolves to the **Spells Pack** original, not this mirror |
| `Projectiles/Explosion_Storm.prefab` | | no key, no type |
| `Projectiles/Flash_generic.prefab` | | no key, no type |
| `Projectiles/Projectile_Fire.prefab` | | no key, no type (`Projectile_Fire_3` is the one wired, at ord 14) |
| `Projectiles/Projectile_Storm.prefab` | | no key, no type |
| `_Shared/Prefabs/ParticlesLight.prefab` | | shared-dependency staging for the mirror pass, not a playable effect |
| `_Shared/Prefabs/fireexplosioneffects__ParticlesLight.prefab` | | same |

**The pattern worth naming:** four of these (`FireFlies`, `Buff_Light`, `top_down_starfall_line_blue`,
`Explosion_Arcane/_Fire`) are mirrors that were *created correctly* and then not adopted, because
**mirroring a prefab does not re-point the catalog row that names it.** `StatusVfxMirrors.cs:49-51`
says exactly this in prose. Every mirror needs a paired store edit + regen, or it lands as an orphan
and the key keeps its pack dependency.

---

## 8. FINDING 7 — keys with no consumer

**76 of 154 keys are registered, resolved, pooled at startup, and played by nothing.**
(`VFXManager.InitialiseHovlPools`, `VFXManager.Hovl.cs:155-172`, pre-instantiates `PoolSize` copies of
**every** row with a prefab — so an unused key still costs its pool at boot.)

> **⚠ THE BOOT-COST HALF OF THIS FINDING IS CLOSED (WO-1113, 2026-08-16).** `InitialiseHovlPools` no
> longer instantiates anything: the warm is **demand-driven** — a key builds its authored `PoolSize`
> the first time it is actually played (`EnsureHovlKeyWarm`, called from `PlayKeyInternal`).
> Measured on the baked asset: **887 pooled GameObjects at boot → 0**; the keys with no consumer
> (85 by a strict `"key"`-literal scan, 510 of those instances) now cost nothing at all.
> `_eagerWarmAllVfxKeys` restores the old boot warm for A/B only. **No key, row or prefab was
> deleted** — the CONTENT question below (is the palette worth keeping?) is still the owner's, it
> just no longer costs boot time while it waits for an answer. Pinned by
> `SpawnBudgetAndVfxWarmRegression` [spawn-budget-vfx-warm] case 2.

The 76 split cleanly:

**(a) 45 `PP_*` Particle Pack rows** — a bulk-tagged library, not a wiring gap. Only 6 `PP_*` keys are
consumed (`PP_MuzzleFlash`, `PP_PlasmaExplosionEffect`, `PP_GroundFog`, `PP_FireBall`, `PP_TinyFlames`,
plus 5 named only by the null-slot regression baseline). The remaining 45 are a palette the owner tagged
for future picks. **Not a defect — but they are pre-warmed at pool size 6 each.**

**(b) 31 gameplay-named keys with no call site** — these read as *intended wiring that never landed*:

`Aegis_Cast` · `ArcaneTower-Baselevel_Projectile` · `AuraOverArcaneTower_Aura` · `DragonFire_Cast` ·
`DragonFire_Impact` · `ElectricitySpell_Cast` · `ElectricitySpell_Impact` · `Electricityimpact_Impact` ·
`Ember_Burn`¹ · `EndGameCAstingAnimation_Impact` · `EnemyCast_Cast` · `EnhamcingBuff_Cast` ·
`Explosion_Impact` · `FireFromTower-ArcaneTowerLevel3_Aura` · `Frost_Projectile` · `Haste_Cast` ·
`Holy_Aura` · `Holy_Impact` · `IceWeaponAura_Aura` · `Junk-DoNotuse_Cast`² · `KnightShieldBuff_Aura`¹ ·
`LongCastSpell_Cast` · `Node_Aura`³ · `RangedAttack-DaggerThrow_Projectile` ·
`RangedSpell-Powerful(Longcast)_Cast` · `RangerTowerlevel2Projectile_Projectile` · `ShieldBuff_Cast` ·
`Sleep_Impact` · `Spear_Projectile`⁴ · `Water_Projectile` · `lighteningOnSpellLand_Impact` ·
`onweaponskillmaybe_Impact` · `portal(rotate)_Aura`⁵ · `subtleHealinginarea(EnemySkill-Mage)_Cast` ·
`targetforSpell_Impact`

¹ also missing on disk — §3. ² the name says it: owner-tagged scratch, safe to delete.
³ **duplicate of `Poi_NodeAura`** — same prefab (`Res:VFX/Aura/Aura_PetLevel2`), one consumed
(`PoiCalloutSystem.cs:51`), one dead. ⁴ `Spear_Impact` is consumed; only the projectile half is dead.
⁵ **duplicate of `Dungeon_Portal_Gate`** — same prefab (`MagicCircles/Magic circle dark star`), one
consumed (`DungeonWorldPortalSpawner.cs:697`), one dead.

Several are visibly *owner-authored notes-to-self* rather than engineering keys
(`Junk-DoNotuse_Cast`, `onweaponskillmaybe_Impact`, `subtleHealinginarea(EnemySkill-Mage)_Cast`,
`targetforSpell_Impact`). That is the tagging workflow working as designed — the owner parks a pick
against a name before the system exists. **They should not be "cleaned up" by a CLI seat; they are the
owner's queue.**

---

## 9. FINDING 8 — null / disabled material slots

Two different shapes, one rule, opposite verdicts (`VfxParticleNullSlotRegression.cs:32-45`):

| Shape | Verdict | Handling |
|---|---|---|
| **DISABLED** `ParticleSystemRenderer`, all slots null | **never failed** — the vendor *container* pattern (a system that only parents/drives children; 339 such renderers across the packs) | `VFXManager.NormalizeVendorContainerRenderers` (`VFXManager.Hovl.cs:414-471`) fills slot 0 with a same-instance donor **while leaving the renderer disabled**, so nothing is drawn and `MagentaGuard`'s M2 probe stops firing. This is what closed the 12-per-session `Portal_Threshold_Aura` F8 spam (WO-1100). |
| **ENABLED** `ParticleSystemRenderer`, all slots null | **hard fail** — it draws engine-default magenta and the runtime deliberately refuses to repaint a particle slot (the 2026-08-05 white-blob lesson) | ratcheted baseline `KnownEnabledNullSlot` (`:87-95`) |

### The live gate state: `vfx-null-slot` is **FAIL, 2 findings**

Read verbatim from the most recent data-regression logs (`Builds/data-regression-1005.log:43128`, and
identically in `data-regression-punch/punch3/punch4/ranger.log`):

```
VFX_NULL_SLOT_FAIL - vfx-null-slot FAIL (2 finding(s); 178 prefab(s) checked, 0 skipped-unresolved)
```

| Key | Offending child renderer | On the ratchet? | What it would need |
|---|---|---|---|
| `PP_DissolveSolidHorizontal` → `ParticlePack/Misc Effects/Prefabs/DissolveSolidHorizontal` | `'Flakes'` — 1 ENABLED PSR, all slots null | **no** | An **owner ruling**: either (a) retag the key at a different prefab, or (b) mirror the pack prefab into `Resources/VFX/` and assign/repair the `Flakes` material there. Baselining it into `KnownEnabledNullSlot` is explicitly *not* a CLI call — *"do not baseline new debt without an owner ruling"* (the failure text itself). |
| `PP_HeatDistortion` → `ParticlePack/Smoke & Steam Effects/Prefabs/HeatDistortion` | `'HeatDistortion'` — 1 ENABLED PSR, all slots null | **no** | same two options |

**Blast radius is currently zero, and that is why it is easy to leave broken:** both keys are in the
§8 no-consumer set — nothing plays them, so no magenta reaches the screen and no F8 capture fires.
The gate is red on *latent* debt. If either key is ever wired, it ships magenta.

**Already-ratcheted debt (5 entries, `VfxParticleNullSlotRegression.cs:90-94`)** — same shape, accepted,
must not grow: `PP_EarthShatter`, `PP_GoopSpray`, `PP_GoopSprayEffect`, `PP_GoopStreamEffect`,
`PP_LightnigStormCloud` (1 renderer each). All five are likewise unconsumed. An independent YAML scan of
all 178 catalog-reachable prefabs this session reproduced exactly these 5 and no others — the two live
findings use a renderer with an **empty** material array, which a naive YAML scan misses and Unity's
`sharedMaterials` correctly reports as a null slot. *(Which is itself the §12 lesson: the gate log is
the data; a hand-rolled scan is a theory.)*

---

## 10. VFX that deliberately bypasses the catalog

Not everything visual goes through a key. Recorded so nobody hunts for a missing row:

| System | What it does | Citation |
|---|---|---|
| `PortalVFXController` | **builds** its glow quad, point light and vortex particle system in code when the serialized refs are null — the runtime-injector pattern, zero scene wiring | `Assets/_Modules/Village/Dungeon/PortalVFXController.cs:7-24` |
| `CastingTelegraphVfx` | `Resources.Load` by path + `Instantiate`, **not** `PlayKey` — so it is outside the pool and outside the loop budget | `Assets/_Modules/Village/Vfx/CastingTelegraphVfx.cs:149-161` |
| `AtbStatusVfx` | `Resources.Load` of the 7 `Res:VFX/Status/*` mirrors by path | `Assets/_Modules/BattleATB/AtbStatusVfx.cs` |
| `VfxAuraProximityCuller` | bounds the **population** of enemy/pet auras to nearest-N so they cannot eat the loop budget; towers / Heart / boss phases are exempt *structurally* — they simply never `Register` | `Assets/_Modules/Village/Vfx/VfxAuraProximityCuller.cs:1-37` |
| `PoiCalloutSystem` | owns the two POI keys and caps live node auras at `MaxNodeAuras = 6` for the same budget reason | `Assets/_Modules/Village/Vfx/PoiCalloutSystem.cs:9-64` |
| `StructureDamageVisuals` | the ONE structure-damage presentation observer; owns `Damage_*` + `Raid_Explosion` | `Assets/_Modules/Village/Vfx/StructureDamageVisuals.cs:54,256-261` |

### The loop-slot invariant every key author must know

A row flagged `IsLoop` **never returns its pool slot** unless the caller calls `VFXHandle.Stop()` —
fire-and-forget play of a loop row leaks one of the 20 global loop slots for the whole session
(`VFXManager.Hovl.cs:288-311`). Two guards exist because of it:

1. `IsLoop` is **derived from the prefab at bake time**, never taken from the Map/JSON literal
   (`HovlVfxCatalogGenerator.cs:457-474`) — 15 rows had declared `isLoop: true` against rate-0 burst
   prefabs, `Poi_NodeAura` and `Poi_Landmark` among them, and saturated the cap across six F8 sessions.
2. A loop row that declares a finite `DefaultLifetime` is routed through the **oneshot** path, which is
   leak-proof (`VFXManager.Hovl.cs:312-331`). At time of writing all loop rows declare no lifetime, so
   this guard is dormant by design.
   `VfxLoopFlagRegression` re-derives the flag and fails the gate if a stored value disagrees.

---

## 11. HOW TO ADD A VFX CORRECTLY

Five steps. Each is a rule with a citation, not a preference.

**1 — The OWNER tags the key.** In the VFX Caster window she picks a prefab and names a key; the window
writes it through `HovlVfxCatalogGenerator.WriteManualPick` into `Assets/Editor/VfxManualPicks.json`
with `manual: true` (`HovlVfxCatalogGenerator.cs:309-333`). A CLI seat never invents the pick — memory
`vfx-map-owner-tags-no-creative-pick`, restated in the ban prose at `:110-119` and enforced in the
failure text of `BannedVfxRegression.cs:184-187` (*"…or withhold the key if none is tagged (never
substitute)"*).

**2 — The CLI maps it VERBATIM to a NAMED HOOK.** Wire the key string at the consuming call site
(`VFXManager.PlayKey(...)`, or a `vfxCast`/`vfxImpact`/`vfxResidual` field in
`Assets/Resources/Data/Canonical/abilities.json`). Do not rename, re-scope or re-tint the owner's pick
on the way in. If a hook has no owner tag yet, **hold it** — an untagged hook waits; it does not get a
CLI guess.

**3 — It goes in the WINNING store.** That is `VfxManualPicks.json`. Adding the row to
`HovlVfxCatalogGenerator.Map` instead is how the §2 divergence happened: the Map loses to any JSON row
with the same key (`:406-408`), so a code-side edit to a key the JSON also carries **changes nothing at
runtime** and silently makes the comment above it a lie. Use the Map only for keys the owner has not
tagged (the `Damage_*` mirrors, `Poi_*`, `Aura_HeartPulse`).

**4 — It must not point at a banned effect.** Currently banned: `Spell_Fire_6` and
`Magic circle sun loop` (`BannedVfxRegression.cs:74-84`; scope notes at `:23-34` — colour variants and
the `sun` / `sun sparks` / `sunS loop` siblings are deliberately **not** banned). ⚠ Note the gap in §4:
the suite scans `.cs` and the two baked `.asset` files, **not** the JSON stores — so today a banned
pick in `VfxManualPicks.json` only goes red *after* a regen.

**5 — Regen, then PROVE it.**
```
Editor menu : Defenders/VFX/Generate Hovl VFX Catalog
Batchmode   : DeNelle.Editor.HovlVfxCatalogGenerator.Generate
```
Then check, in order:
- the run log for `HOVL_VFX_CATALOG_OK` **and** for a `prefab missing for '<key>'` warning — ⚠ **the
  marker prints even when rows were skipped** (`:344` runs regardless of `skippedMissing`), which is
  exactly how `Ember_Burn` stayed dead through every gate for months. **Read the warnings, not the
  marker.**
- the row actually landed in `Assets/Resources/VFX/HovlVfxCatalog.asset` (the file is text YAML — grep
  the key).
- `BANNED_VFX_OK`, `VFX_NULL_SLOT_OK`, and the loop-flag suite are green.
- **screenshot it.** Compile-green never proves an effect looks right — memory
  `headless-screenshot-verify-ui-before-build` and `screenshots-are-primary-evidence-for-visual-defects`.
  `Assets/Editor/VfxProofCapture.cs` exists for exactly this.

**Mirroring caveat (bites every time):** if the picked prefab lives in a **gitignored** pack
(`Assets/Hovl Studio/**`, `Assets/Spells Pack/**`, `Assets/UnityTechnologies/**`), the key renders on
your machine and **not on a fresh clone**. Mirroring the prefab into `Assets/Resources/VFX/` is the fix
— but it is a **dependency** mirror (materials/textures follow), not a file copy
(`StatusVfxMirrors.cs:8-10` records that trap), **and mirroring alone does not re-point the catalog
row**: you must edit the store to name the mirror and regen, or you have just created an orphan (§7).

---

## 12. Open items, ranked

| # | Item | Blocked on | §|
|---|---|---|---|
| 1 | `Ember_Burn` → `Debuff 1.prefab` does not exist; `knight.emberbrand-throw` residual has been dark since authoring | **owner tag** for the replacement | §3 |
| 2 | `KnightShieldBuff_Aura` names `Res:VFX/Projectiles/Buff_Light`; the file is at `Res:VFX/Buffs/` | **owner confirm**, then a 1-word path edit + regen | §3 |
| 3 | `vfx-null-slot` gate is FAIL on `PP_DissolveSolidHorizontal` + `PP_HeatDistortion` | **owner ruling**: retag or repair (baselining is not a CLI call) | §9 |
| 4 | 6 of 7 casting-telegraph schools unmirrored — including the DEFAULT (`arcane`) | CLI work: 6 dependency mirrors | §5 |
| 5 | `TreeofLifeAura_Aura` still points at the gitignored pack `FireFlies` though the tracked mirror exists — **breaks on a fresh clone** | CLI work: re-point + regen | §7 |
| 6 | `BannedVfxRegression` does not scan the two JSON stores — the winning store is unguarded | CLI work: widen `CatalogAssets` | §4 |
| 7 | Duplicate key pairs `Node_Aura`/`Poi_NodeAura` and `portal(rotate)_Aura`/`Dungeon_Portal_Gate` — same prefab, one live one dead each | owner: confirm the dead half can go | §8 |
| 8 | ~~45 unconsumed `PP_*` rows pre-warm ~6 pooled instances each at boot~~ **BOOT COST CLOSED (WO-1113): warm is demand-driven, 887 → 0 instances at boot** | owner: keep the palette? (now a content question only — it costs nothing to hold) | §8 |

---

## 13. How this document was verified (re-runnable)

- **Baked truth:** parsed `Assets/Resources/VFX/HovlVfxCatalog.asset` and `VFXCatalog.asset` as text
  YAML; resolved every `Prefab: {guid}` through an index built from all **61 918** `.meta` files under
  `Assets/`; existence-checked each resolved path on disk.
- **Conflicts:** parsed the `Map` initialiser out of `HovlVfxCatalogGenerator.cs` (with its five path
  constants) and set-diffed it against `VfxManualPicks.json` and the bake.
- **Caster index:** parsed all 2951 entries; 152 carry a key; compared each against picks and bake.
- **Consumers:** scanned 2189 `.cs`/`.json` files (vendor pack dirs excluded) for every baked key as a
  quoted string literal, plus a structural walk of `abilities.json` `vfx*` fields keyed by ability id.
- **Null slots:** the authoritative number is the gate's own output in `Builds/data-regression-*.log`;
  an independent YAML scan of all 178 catalog-reachable prefabs corroborated the 5 ratcheted entries.
- **Nothing here was inferred from a comment.** Where a comment and the data disagreed (§2), the data
  is recorded and the comment is flagged.
