# VFX & AUDIO WIRING MAP — what exists, what is wired, what is orphaned

> **GENERATED FILE — DO NOT HAND-EDIT.** Regenerate in ~seconds:
> `python tools/vfx_audio_map.py` (report-only, exits 0) ·
> `python tools/vfx_audio_map.py --check` (exits 1 if any **BROKEN** row exists).
> Generated **2026-08-09 19:15**. The repo is the source of truth; this doc is a derived view, so it
> cannot drift the way a hand-maintained map does. Edit the catalogs/code, re-run, commit both.

## Summary

| bucket | count | meaning |
|---|---|---|
| **WIRED** | 166 | declared + an asset resolves + at least one non-test caller |
| **ORPHAN** | 118 | declared, nothing consumes it — **debt / work queue**, not a failure |
| **BROKEN** | 0 | a caller asks for a key/id that resolves to NOTHING at runtime — **defect** |
| FALLBACK | 15 | called, no prefab, but `VFXManager` renders a documented procedural stand-in |
| UNRESOLVED | 63 | call sites whose key is built at runtime — the link **cannot be proven statically** |

`VFX_AUDIO_MAP_OK 166/118/0` (wired/orphan/broken) · fallback=15 unresolved=63

Scanned: **1521** `.cs` files (`Assets/_Modules, Assets/Editor, Assets/Tests`), **210** data files (`.json`/`.asset`), **8869** prefab `.meta` GUIDs, **16** gitignored art roots read from `VfxResourceSelfContainmentRegression.GitignoredArtRoots`.

### How a row is bucketed

A **caller** is a reference from runtime code or from a data file. References that
live only in `/Editor/` or `/Tests/`, or only inside the key's own declaration and
plumbing (`VFXManager`, `VFXCatalog`, `AudioService`, …), do **not** make a key
consumed — they are noted on the row instead. That is the audit's CONSUMED
definition (`docs/reference/STACK_UTILIZATION_2026-08-09.md`), not a looser one.

For `Resources/Sfx` clips a caller must be a real `Resources.Load<AudioClip>("<name>")`.
A name that merely appears in a string list — `AudioService.CombatSfxResourceNames`
pre-warms clips it never plays — is shown as `(mention)` and does **not** count:
pre-warming a clip is not playing it.

### By domain

| domain | inventory | WIRED | ORPHAN | BROKEN | FALLBACK |
|---|---|---|---|---|---|
| Hovl VFX keys (`HovlVfxCatalog.asset`) | 140 | 66 | 74 | 0 | 0 |
| `VFXType` ordinals (`VFXCatalog.asset`) | 95 | 57 | 29 | 0 | 9 |
| `SfxId` values | 16 | 16 | 0 | 0 | 0 |
| `MusicTrack` values | 9 | 9 | 0 | 0 | 0 |
| `Resources/Sfx/*` clip files | 33 | 18 | 15 | 0 | 0 |

**Cross-check against the 2026-08-09 reverse audit** (did the number move?):

- Hovl VFX keys — audit measured **62/140 consumed (44%)**; this tool measures **66/140 (47%)**. Source: docs/reference/STACK_UTILIZATION_2026-08-09.md - 'Hovl VFX keys 62/140 (44%)'
- `VFXType` ordinals — audit measured **76/95 consumed (80%)**; this tool measures **66/95 (69%)**. Source: docs/reference/STACK_UTILIZATION_2026-08-09.md - 'VFXType ordinals 76/95 (80%)'

These two numbers are **not** the same measurement and are not expected to match
exactly: the audit's "consumed" was a human judgement over call sites, this tool's is
a mechanical rule (runtime-or-data reference, excluding the key's own plumbing). Read
the delta as a sanity check on the order of magnitude, never as a pass/fail bar.

## 1. BROKEN — a call site asking for something that will not resolve

_None._ Every key/id with a caller resolves to an asset (or to a documented
procedural fallback — see the FALLBACK list below).

**FALLBACK (not broken, but no authored audio ships):** these `Resources/Sfx` names
have no file; every load site coalesces to a procedurally generated clip, so the
event is audible as *synth*, not as the sound someone authored. Dropping a real
file in at the named path upgrades it with no code change.

- `Sfx/BuildDenied` — requested from GameSfx.cs:238, HudUiRegression.cs:144
- `Sfx/LevelUp` — requested from GameSfx.cs:178, HudUiRegression.cs:146
- `Sfx/PetHarvest` — requested from GameSfx.cs:164, HudUiRegression.cs:147
- `Sfx/TowerFire` — requested from AudioService.cs:725, GameSfx.cs:69, HudUiRegression.cs:148
- `Sfx/TowerPlace` — requested from GameSfx.cs:81, HudUiRegression.cs:149
- `Sfx/WaveStart` — requested from AudioService.cs:725, GameSfx.cs:94, HudUiRegression.cs:150

## 2. ORPHAN — declared in a catalog, wired to nothing

This is a **work queue, not a statistic**. Every row below is a purchased/authored
asset the game never asks for. Grouped by domain so it is obvious which pack a block
of unused keys belongs to.

### Hovl VFX keys — 74 orphans

- **PP — Unity Particle Pack** (47): `PP_BigExplosion`, `PP_BigSplash`, `PP_Candles`, `PP_Dissolve`, `PP_DissolveSolidHorizontal`, `PP_DustExplosion`, `PP_DustMotesEffect`, `PP_DustStorm`, `PP_EarthShatter`, `PP_ElectricalSparks`, `PP_ElectricalSparksEffect`, `PP_EllenDissolve`, `PP_EllenRespawn`, `PP_EnergyExplosion`, `PP_FireFlies`, `PP_FlameStream`, `PP_FlameThrower`, `PP_FleshImpacts`, `PP_GoopSpray`, `PP_GoopSprayEffect`, `PP_GoopStreamEffect`, `PP_HeatDistortion`, `PP_IceLance`, `PP_LargeFlames`, `PP_LightnigStormCloud`, `PP_MediumFlames`, `PP_MetalImpacts`, `PP_PoisonGas`, `PP_PressurisedSteam`, `PP_RainEffect`, `PP_Respawn`, `PP_RisingSteam`, `PP_RocketTrail`, `PP_SandImpacts`, `PP_SandSwirlsEffect`, `PP_Shower`, `PP_SmallExplosion`, `PP_SmokeEffect`, `PP_SparksEffect`, `PP_Steam`, `PP_StoneImpacts`, `PP_TinyExplosion`, `PP_TinyFlames`, `PP_WaterFall`, `PP_WaterLeak`, `PP_WildFire`, `PP_WoodImpacts`
- **DragonFire** (2): `DragonFire_Cast`, `DragonFire_Impact`
- **ElectricitySpell** (2): `ElectricitySpell_Cast`, `ElectricitySpell_Impact`
- **Aegis** (1): `Aegis_Cast`
- **ArcaneTower-Baselevel** (1): `ArcaneTower-Baselevel_Projectile`
- **AuraOverArcaneTower** (1): `AuraOverArcaneTower_Aura`
- **Collector** (1): `Collector_Full`
- **Dungeon** (1): `Dungeon_Portal_Gate`
- **Electricityimpact** (1): `Electricityimpact_Impact`
- **EnemyCast** (1): `EnemyCast_Cast`
- **EnhamcingBuff** (1): `EnhamcingBuff_Cast`
- **FireFromTower-ArcaneTowerLevel3** (1): `FireFromTower-ArcaneTowerLevel3_Aura`
- **Fountain** (1): `Fountain_Heal_Aura`
- **Frost** (1): `Frost_Projectile`
- **IceWeaponAura** (1): `IceWeaponAura_Aura`
- **Junk-DoNotuse** (1): `Junk-DoNotuse_Cast`
- **LongCastSpell** (1): `LongCastSpell_Cast`
- **Raid** (1): `Raid_Explosion`
- **RangedAttack-DaggerThrow** (1): `RangedAttack-DaggerThrow_Projectile`
- **RangedSpell-Powerful(Longcast)** (1): `RangedSpell-Powerful(Longcast)_Cast`
- **RangerTowerlevel2Projectile** (1): `RangerTowerlevel2Projectile_Projectile`
- **Spear** (1): `Spear_Projectile`
- **Water** (1): `Water_Projectile`
- **lighteningOnSpellLand** (1): `lighteningOnSpellLand_Impact`
- **onweaponskillmaybe** (1): `onweaponskillmaybe_Impact`
- **subtleHealinginarea(EnemySkill-Mage)** (1): `subtleHealinginarea(EnemySkill-Mage)_Cast`

### `VFXType` ordinals — 29 orphans

- **Env** (6): `Env_LanternGlow`, `Env_GroundFog`, `Env_TitleEmbers`, `Env_Candle`, `Env_SteamVent`, `Env_SteamBurst`
- **Aura** (5): `Aura_Dust`, `Aura_EmpowerTower`, `Aura_PetLevel1`, `Aura_PetLevel2`, `Aura_PetLevel3`
- **Cast** (3): `Cast_RangerDraw`, `Cast_NecromancerSummon`, `Cast_EnemyCaster`
- **Death** (3): `Death_Wolf`, `Death_Tiefling`, `Death_EnemyExplosion_Dungeon`
- **Juice** (3): `Juice_KillStreak`, `Juice_GroundDecal_Flame`, `Juice_GroundDecal_Ice`
- **Pet** (3): `Pet_Aura_Fire`, `Pet_Aura_Ice`, `Pet_Attack`
- **Impact** (2): `Impact_ShardsBurst`, `Impact_SmokeWisps`
- **(no prefix)** (1): `ShootingStar`
- **Despawn** (1): `Despawn_Dissolve`
- **Harvest** (1): `Harvest_Gold`
- **Projectile** (1): `Projectile_EnemyCasterBolt`

### `Resources/Sfx/*` clips — 15 orphans

- **Sfx/Sfx** (12): `Sfx/Sfx_ArcaneExplosion`, `Sfx/Sfx_EnemyDeath`, `Sfx/Sfx_FireExplosion`, `Sfx/Sfx_FlameArrowLaunch`, `Sfx/Sfx_Heal`, `Sfx/Sfx_LevelUp`, `Sfx/Sfx_PetAttack`, `Sfx/Sfx_Shockwave`, `Sfx/Sfx_TowerShot`, `Sfx/Sfx_WardDim`, `Sfx/Sfx_WardLit`, `Sfx/Sfx_WizardCast`
- **(no prefix)** (3): `Sfx/SwordClash2`, `Sfx/SwordClash3`, `Sfx/SwordClash4`

## 3. UNRESOLVED — cannot be determined statically

These call sites pass a key that is **built or indirected at runtime** (a variable, a
field, an interpolated string). A static parse cannot prove which catalog row they
reach, so the keys they use may appear as ORPHAN above. **They are counted here and
never folded into the orphan list** — treat the orphan queue as "probably unused",
not "provably unused", until these are read by hand.

| call site | api | expression |
|---|---|---|
| `Assets/Editor/MotionCasterWindow.cs:1391` | `Resources.Load<AudioClip>` | `"" + sfxId` |
| `Assets/Editor/Regression/DataRegression.cs:2643` | `Resources.Load<AudioClip>` | `name` |
| `Assets/Editor/Regression/SfxResolveRegression.cs:41` | `Resources.Load<AudioClip>` | `key` |
| `Assets/Editor/RegressionSuite.cs:969` | `Resources.Load<AudioClip>` | `p` |
| `Assets/_Modules/Audio/AudioBootstrap.cs:186` | `Resources.Load<AudioClip>` | `resourceName` |
| `Assets/_Modules/Audio/AudioBootstrap.cs:206` | `Resources.Load<AudioClip>` | `resourceName` |
| `Assets/_Modules/Audio/AudioService.cs:513` | `PlayMusic` | `track` |
| `Assets/_Modules/Audio/AudioService.cs:767` | `Resources.Load<AudioClip>` | `path` |
| `Assets/_Modules/Audio/AudioService.cs:959` | `PlayMusic` | `track` |
| `Assets/_Modules/Audio/AudioService.cs:1052` | `PlayMusic` | `track` |
| `Assets/_Modules/Audio/ProceduralSfx.cs:62` | `Resources.Load<AudioClip>` | `"" + ResourceName(id` |
| `Assets/_Modules/Village/Audio/BattleMusicManager.cs:518` | `Resources.Load<AudioClip>` | `p` |
| `Assets/_Modules/Village/Audio/GameSfx.cs:138` | `Resources.Load<AudioClip>` | `"" + i` |
| `Assets/_Modules/Village/Audio/VillageAudioResources.cs:85` | `Resources.Load<AudioClip>` | `resourcePath` |
| `Assets/_Modules/Village/Buildings/ArcaneTower.cs:513` | `PlayKey` | `travelKey` |
| `Assets/_Modules/Village/Buildings/DefenseTower.cs:952` | `PlayKey` | `projKey` |
| `Assets/_Modules/Village/Buildings/DefenseTower.cs:963` | `PlayKey` | `impactKey` |
| `Assets/_Modules/Village/Buildings/DefenseTower.cs:1065` | `PlayKey` | `CastKeyFor(Element` |
| `Assets/_Modules/Village/Buildings/DefenseTower.cs:1069` | `PlayKey` | `CastKeyFor(Element` |
| `Assets/_Modules/Village/Buildings/SupportFieldStructure.cs:461` | `VFXManager.Play*` | `type` |
| `Assets/_Modules/Village/Buildings/TowerCombat.cs:394` | `PlayKey` | `CastKeyFor(element` |
| `Assets/_Modules/Village/Buildings/TowerCombat.cs:644` | `PlayKey` | `ImpactKeyFor(element` |
| `Assets/_Modules/Village/Enemies/DragonBoss.cs:1391` | `VFXManager.Play*` | `_breathStreamVfx` |
| `Assets/_Modules/Village/Enemies/DragonBoss.cs:1688` | `VFXManager.Play*` | `type` |
| `Assets/_Modules/Village/Enemies/Enemy.cs:1869` | `PlayKey` | `castKey` |
| `Assets/_Modules/Village/Enemies/Enemy.cs:1880` | `PlayKey` | `impactKey` |
| `Assets/_Modules/Village/Enemies/EnemyAuraVFX.cs:219` | `VFXManager.Play*` | `_type` |
| `Assets/_Modules/Village/Enemies/PlayerAttackController.cs:697` | `PlayKey` | `elementKey` |
| ~~`Assets/_Modules/Village/Harvest/EchoSpiritPresentation.cs:105`~~ RETIRED 2026-08-16 (WO-993) - the Echo aura consumer is gone; `Aura_HeartPulse` KEEPS its Heart-of-Elarion + ArcaneAura consumers and its Hovl bridge row | `VFXManager.Play*` | `_auraType` |
| `Assets/_Modules/Village/Harvest/HarvestAura.cs:361` | `VFXManager.Play*` | `type` |
| `Assets/_Modules/Village/Hero/AbilityAudioBridge.cs:89` | `Resources.Load<AudioClip>` | `"" + kind` |
| `Assets/_Modules/Village/Hero/GearAura.cs:380` | `VFXManager.Play*` | `type` |
| `Assets/_Modules/Village/Hero/GearAura.cs:784` | `VFXManager.Play*` | `type` |
| `Assets/_Modules/Village/Hero/HeroAbilities.cs:2020` | `PlayKey` | `def.VfxCast` |
| `Assets/_Modules/Village/Hero/HeroAbilities.cs:2030` | `PlayKey` | `row.vfxKey` |
| `Assets/_Modules/Village/Hero/HeroAbilities.cs:2049` | `Resources.Load<AudioClip>` | `"" + sfx` |
| `Assets/_Modules/Village/Hero/HeroAbilities.cs:2061` | `PlayKey` | `key` |
| `Assets/_Modules/Village/Hero/HeroAbilities.cs:2065` | `PlayKey` | `def.VfxImpact` |
| `Assets/_Modules/Village/Hero/HeroAbilities.cs:2092` | `PlayKey` | `def.VfxResidual` |
| `Assets/_Modules/Village/Hero/HeroAbilities.cs:2121` | `PlayKey` | `key` |
| `Assets/_Modules/Village/Hero/HeroHpStateAura.cs:285` | `VFXManager.Play*` | `type` |
| `Assets/_Modules/Village/Hero/RangedAttackVFX.cs:248` | `PlayKey` | `key` |
| `Assets/_Modules/Village/Vfx/ActionBundlePlayer.cs:304` | `PlayKey` | `vfxKey` |
| `Assets/_Modules/Village/Vfx/ActionBundlePlayer.cs:315` | `Resources.Load<AudioClip>` | `"" + sfxId` |
| `Assets/_Modules/Village/Vfx/ArcaneAura.cs:312` | `PlayKey` | `key` |
| `Assets/_Modules/Village/Vfx/EnvironmentVFX.cs:137` | `VFXManager.Play*` | `_vfxType` |
| `Assets/_Modules/Village/Vfx/PoiCalloutSystem.cs:218` | `PlayKey` | `NodeAuraKey` |
| `Assets/_Modules/Village/Vfx/PoiCalloutSystem.cs:229` | `PlayKey` | `LandmarkKey` |
| `Assets/_Modules/Village/Vfx/SpellVfxFactory.cs:133` | `VFXManager.Play*` | `type` |
| `Assets/_Modules/Village/Vfx/StructureDamageVisuals.cs:631` | `PlayKey` | `BreakKey` |
| `Assets/_Modules/Village/Vfx/StructureDamageVisuals.cs:704` | `PlayKey` | `wantKey` |
| `Assets/_Modules/Village/Vfx/StructureDamageVisuals.cs:761` | `PlayKey` | `BeaconKey` |
| `Assets/_Modules/Village/Vfx/VFXManager.cs:254` | `VFXManager.Play*` | `type` |
| `Assets/_Modules/Village/Vfx/VFXManager.cs:261` | `VFXManager.Play*` | `type` |
| `Assets/_Modules/Village/Vfx/VFXManager.cs:274` | `PlaySfxAtPosition` | `sfxId` |
| `Assets/_Modules/Village/Vfx/VFXManager.cs:331` | `VFXManager.Play*` | `type` |
| `Assets/_Modules/Village/Vfx/VFXManager.cs:338` | `VFXManager.Play*` | `type` |
| `Assets/_Modules/Village/Vfx/VFXManager.cs:345` | `VFXManager.Play*` | `type` |
| `Assets/_Modules/Village/Vfx/VFXManager.cs:352` | `VFXManager.Play*` | `type` |
| `Assets/_Modules/Village/Vfx/VFXManager.cs:359` | `VFXManager.Play*` | `type` |
| `Assets/_Modules/Village/Vfx/VFXManager.cs:366` | `VFXManager.Play*` | `type` |
| `Assets/_Modules/Village/Vfx/VFXManager.cs:383` | `VFXManager.Play*` | `type` |
| `Assets/_Modules/Village/World/WorldMusicDirector.cs:100` | `PlayMusic` | `track` |

## 4. Full inventory

### 4.1 Hovl VFX keys — `Assets/Resources/VFX/HovlVfxCatalog.asset`

Requested via `VFXManager.PlayKey("<key>", …)`. A key with no catalog row **no-ops**
(logged, throttled) — nothing spawns.

| key | bucket | prefab | called by | note |
|---|---|---|---|---|
| `Arcane_Aura` | **WIRED** | ...c circles/Prefabs/Loop version/Magic circle sun loop.prefab | ArcaneAura.cs, HovlVfxCatalogGenerator.cs (editor), VfxAuraDifferentiationRegression.cs (editor) +1 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `Dungeon_Portal_Gate` | **ORPHAN** | ...c circles/Prefabs/Loop version/Magic circle sun loop.prefab | HovlVfxCatalogGenerator.cs (editor), VfxCasterLibraryIndex.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `Aura_HeartPulse` | **WIRED** | ...PG VFX Bundle/Random effect prefabs/Buff white twist.prefab | ArcaneTower.cs, VFXManager.Hovl.cs, HovlVfxCatalogGenerator.cs (editor) +1 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `Collector_Full` | **ORPHAN** | ...Studio/RPG VFX Bundle/Random effect prefabs/Gold dot.prefab | HovlVfxCatalogGenerator.cs (editor), VfxCasterLibraryIndex.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `Raid_Explosion` | **ORPHAN** | ...ovl Studio/AOE Magic spells Vol.1/Prefabs/Meteor hit.prefab | HovlVfxCatalogGenerator.cs (editor), VfxCasterLibraryIndex.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `LevelUp_Burst` | **WIRED** | ...l Studio/RPG VFX Bundle/Random effect prefabs/Lvl up.prefab | ArcaneAura.cs, HovlVfxCatalogGenerator.cs (editor), VfxCasterLibraryIndex.json (editor) | GITIGNORED - prefab is in a GITIGNORED art root |
| `Spear_Impact` | **WIRED** | ...les Vol 1/Prefabs/Flash and hits/Hit 11 orange arrow.prefab | DefenseTower.cs, TowerCombat.cs, HovlVfxCatalogGenerator.cs (editor) +3 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `Taunt_Roar` | **WIRED** | ...udio/AOE Magic spells Vol.1/Prefabs/Energy explosion.prefab | HovlVfxCatalogGenerator.cs (editor), VfxCasterLibraryIndex.json (editor), abilities.json (data) +1 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `Taunt_Aura` | **WIRED** | ...circles/Prefabs/Loop version/Magic circle blood loop.prefab | HovlVfxCatalogGenerator.cs (editor), VfxCasterLibraryIndex.json (editor), abilities.json (data) +1 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `Aegis_Shield` | **WIRED** | ... circles/Prefabs/Loop version/Magic shield holy loop.prefab | HovlVfxCatalogGenerator.cs (editor), VfxCasterLibraryIndex.json (editor), abilities.json (data) +1 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `Damage_Smolder` | **WIRED** | Assets/Resources/VFX/Damage/Damage_Smolder.prefab | StructureDamageVisuals.cs, HovlVfxCatalogGenerator.cs (editor), ParticlePackVfxBatchBuilder.cs (editor) | RESOLVED |
| `Damage_Fire` | **WIRED** | Assets/Resources/VFX/Damage/Damage_Fire.prefab | StructureDamageVisuals.cs, HovlVfxCatalogGenerator.cs (editor), ParticlePackVfxBatchBuilder.cs (editor) | RESOLVED |
| `Damage_CriticalBeacon` | **WIRED** | Assets/Resources/VFX/Damage/Damage_CriticalBeacon.prefab | StructureDamageVisuals.cs, HovlVfxCatalogGenerator.cs (editor), ParticlePackVfxBatchBuilder.cs (editor) | RESOLVED |
| `Damage_BreakBurst` | **WIRED** | Assets/Resources/VFX/Damage/Damage_BreakBurst.prefab | StructureDamageVisuals.cs, HovlVfxCatalogGenerator.cs (editor), ParticlePackVfxBatchBuilder.cs (editor) | RESOLVED |
| `Damage_Ruin` | **WIRED** | Assets/Resources/VFX/Damage/Damage_Ruin.prefab | StructureDamageVisuals.cs, HovlVfxCatalogGenerator.cs (editor), ParticlePackVfxBatchBuilder.cs (editor) | RESOLVED |
| `Dash_Blink` | **WIRED** | ...PG VFX Bundle/Random effect prefabs/Buff white twist.prefab | HovlVfxCatalogGenerator.cs (editor), VfxCasterLibraryIndex.json (editor), abilities.json (data) +1 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `Poi_NodeAura` | **WIRED** | ...c circles/Prefabs/Loop version/Magic circle sun loop.prefab | PoiCalloutSystem.cs, HovlVfxCatalogGenerator.cs (editor), VfxAuraDifferentiationRegression.cs (editor) +1 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `Poi_Landmark` | **WIRED** | ...o/Map track markers VFX/Prefabs/Marker 4 Pillar Loop.prefab | PoiCalloutSystem.cs, HovlVfxCatalogGenerator.cs (editor), VfxCasterLibraryIndex.json (editor) | GITIGNORED - prefab is in a GITIGNORED art root |
| `ARcaneTower_Projectile` | **WIRED** | ...PG VFX Bundle/Random effect prefabs/Buff orange shot.prefab | DefenseTower.cs, VfxProofCapture.cs (editor), VfxCasterLibraryIndex.json (editor) +1 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `Aegis_Cast` | **ORPHAN** | .../Hovl Studio/Magic circles/Prefabs/Magic shield holy.prefab | HovlVfxCatalogGenerator.cs (editor), VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `ArcaneTower-Baselevel_Projectile` | **ORPHAN** | ...es Vol 1/Prefabs/Projectiles 2D/2D Projectile 7 pink.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `Arcane_Cast` | **WIRED** | ...ojectiles Vol 1/Prefabs/Flash and hits/Flash 16 fire.prefab | TowerCombat.cs, EnemyTypeVfxSet.cs, HovlVfxCatalogGenerator.cs (editor) +2 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `Arcane_Impact` | **WIRED** | ...mples/Legacy Particles/Prefabs/PlasmaExplosionEffect.prefab | EnemyTypeVfxSet.cs, WeaponVfxMap.cs, HovlVfxCatalogGenerator.cs (editor) +3 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `Arcane_Projectile` | **WIRED** | ...PG VFX Bundle/Random effect prefabs/Buff orange shot.prefab | EnemyTypeVfxSet.cs, HovlVfxCatalogGenerator.cs (editor), VfxCasterLibraryIndex.json (editor) +1 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `ArcherTower-Fire_Projectile` | **WIRED** | ...s/Projectiles(Particle collision)/Projectile 16 fire.prefab | DefenseTower.cs, VfxProofCapture.cs (editor), VfxCasterLibraryIndex.json (editor) +1 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `ArcherTower-Ice_Projectile` | **WIRED** | ...ectiles(Particle collision)/Projectile 14 blue rapid.prefab | DefenseTower.cs, VfxProofCapture.cs (editor), VfxCasterLibraryIndex.json (editor) +3 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `ArcherTowerLevel1_Projectile` | **WIRED** | ... 1/Prefabs/Projectiles 2D/2D Projectile 21 red arrow.prefab | DefenseTower.cs, VfxProofCapture.cs (editor), TowerProjectileMapRegression.cs (editor) +2 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `ArcherTowerLevel2_Projectile` | **WIRED** | ...1/Prefabs/Projectiles 2D/2D Projectile 20 pink arrow.prefab | DefenseTower.cs, VfxProofCapture.cs (editor), TowerProjectileMapRegression.cs (editor) +2 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `ArcherTower_Projectile` | **WIRED** | ...jectiles(Particle collision)/Projectile 13 red laser.prefab | DefenseTower.cs, VfxProofCapture.cs (editor), TowerProjectileMapRegression.cs (editor) +3 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `AuraOverArcaneTower_Aura` | **ORPHAN** | .../Prefabs/Projectiles 2D/2D Projectile 18 nova orange.prefab | VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `BurningStructure_Aura` | **WIRED** | ...mate VFX/Prefabs/Loop/pf_vfx-ult_demo_psys_loop_fire.prefab | StructureBurn.cs, VfxProofCapture.cs (editor), VfxCasterLibraryIndex.json (editor) +1 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `BurningStructure_Impact` | **WIRED** | ...mate VFX/Prefabs/Loop/pf_vfx-ult_demo_psys_loop_fire.prefab | StructureBurn.cs, VfxProofCapture.cs (editor), VfxCasterLibraryIndex.json (editor) +1 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `Cathedral_Aura` | **WIRED** | ...rcles/Prefabs/Loop version/Magic circle electro loop.prefab | HubStructureVisualInjector.cs, StructureFactory.cs, VfxManualPicks.json (editor) | GITIGNORED - prefab is in a GITIGNORED art root |
| `Cleave_Impact` | **WIRED** | ...udio/AOE Magic spells Vol.1/Prefabs/Energy explosion.prefab | ArcaneTower.cs, TowerCombat.cs, HovlVfxCatalogGenerator.cs (editor) +6 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `DefenseUp-Offhand(Shield)_Aura` | **WIRED** | .../Hovl Studio/Magic circles/Prefabs/Magic shield holy.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor), abilities.json (data) +1 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `DragonFire_Cast` | **ORPHAN** | ...Lana Studio/Casual RPG VFX/Prefabs/Fire/Flamethrower.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | RESOLVED - editor/test refs only |
| `DragonFire_Impact` | **ORPHAN** | ...tiles Vol 1/Prefabs/Flash and hits/Hit 20 pink arrow.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `ElectricitySpell_Cast` | **ORPHAN** | ...Projectiles(Particle collision)/Projectile 2 electro.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `ElectricitySpell_Impact` | **ORPHAN** | .../Prefabs/Projectiles with logic/Projectile 2 electro.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `Electricityimpact_Impact` | **ORPHAN** | .../Prefabs/Projectiles with logic/Projectile 2 electro.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `EnemyCast_Cast` | **ORPHAN** | ...Particles/Prefabs/Projectiles/Casting/Casting_Dark_3.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `EnhamcingBuff_Cast` | **ORPHAN** | .../Prefabs/Projectile VFX loop/Projectile dragon punch.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `FireFromTower-ArcaneTowerLevel3_Aura` | **ORPHAN** | ...PG VFX Bundle/Random effect prefabs/Buff orange shot.prefab | VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `FireImpact_Impact` | **WIRED** | ...Vol 1/Prefabs/Flash and hits/Hit 25 orange explosion.prefab | DefenseTower.cs, TowerCombat.cs, VfxCasterLibraryIndex.json (editor) +3 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `Fire_Cast` | **WIRED** | ...Projectiles Vol 1/Prefabs/Flash and hits/Hit 16 fire.prefab | DefenseTower.cs, TowerCombat.cs, Enemy.cs +5 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `FireballImpact_Impact` | **WIRED** | ...rojectiles with logic/Projectile 25 orange explosion.prefab | Enemy.cs, VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) +2 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `FireballTower_Projectile` | **WIRED** | ...s(Particle collision)/Projectile 25 orange explosion.prefab | DefenseTower.cs, VfxProofCapture.cs (editor), VfxCasterLibraryIndex.json (editor) +3 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `Fireball_Cast` | **WIRED** | ...Projectiles Vol 1/Prefabs/Flash and hits/Hit 16 fire.prefab | HovlVfxCatalogGenerator.cs (editor), VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) +2 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `Fireball_Impact` | **WIRED** | ...Vol 1/Prefabs/Flash and hits/Hit 25 orange explosion.prefab | WeaponVfxMap.cs, HovlVfxCatalogGenerator.cs (editor), WeaponElementalOnHitTests.cs (test) +4 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `Fireball_Projectile` | **WIRED** | ...s(Particle collision)/Projectile 25 orange explosion.prefab | HovlVfxCatalogGenerator.cs (editor), MotionCastingsTests.cs (test), VfxCasterLibraryIndex.json (editor) +3 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `Fountain_Heal_Aura` | **ORPHAN** | ...udio/RPG VFX Bundle/Random effect prefabs/Druid aura.prefab | HovlVfxCatalogGenerator.cs (editor), VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `Freezing_Impact` | **WIRED** | .../Prefabs/Projectile VFX loop/Projectile dragon punch.prefab | DefenseTower.cs, TowerCombat.cs, WeaponVfxMap.cs +5 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `Freezing_Projectile` | **WIRED** | ...les Vol 1/Prefabs/Flash and hits/Flash 10 blue laser.prefab | DefenseTower.cs, TowerCombat.cs, VfxProofCapture.cs (editor) +4 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `Frost_Impact` | **WIRED** | .../Prefabs/Projectile VFX loop/Projectile dragon punch.prefab | WeaponVfxMap.cs, HovlVfxCatalogGenerator.cs (editor), WeaponElementalOnHitTests.cs (test) +2 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `Frost_Projectile` | **ORPHAN** | ...ectiles(Particle collision)/Projectile 14 blue rapid.prefab | HovlVfxCatalogGenerator.cs (editor), VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `Heal_Aura` | **WIRED** | ...o/RPG VFX Bundle/Random effect prefabs/Buff palladin.prefab | HeroAbilities.cs, HovlVfxCatalogGenerator.cs (editor), VfxCasterLibraryIndex.json (editor) +1 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `Heal_Cast` | **WIRED** | ...PG VFX Bundle/Random effect prefabs/Buff white twist.prefab | HeroAbilities.cs, ConsumableUseService.cs, HovlVfxCatalogGenerator.cs (editor) +4 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `HealingFountain_Aura` | **WIRED** | ...udio/RPG VFX Bundle/Random effect prefabs/Druid aura.prefab | HealingFountain.cs, VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - prefab is in a GITIGNORED art root |
| `IceWeaponAura_Aura` | **ORPHAN** | Assets/Resources/VFX/Projectiles/Explosion_Ice.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | RESOLVED - editor/test refs only |
| `Junk-DoNotuse_Cast` | **ORPHAN** | ...Vol 1/Prefabs/Projectile VFX loop/Projectile 16 fire.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `KnightThrust_Impact` | **WIRED** | ...io/RPG VFX Bundle/Random effect prefabs/Dragon punch.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor), abilities.json (data) +3 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `KnightWeaponskill_Impact` | **WIRED** | ...udio/AOE Magic spells Vol.1/Prefabs/Energy explosion.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor), abilities.json (data) +1 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `LongCastSpell_Cast` | **ORPHAN** | ...udio/RPG VFX Bundle/Random effect prefabs/Buff chain.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `MageMeoteorAOE_Cast` | **WIRED** | ... RPG VFX/Prefabs/Top_down_attack/top_down_stone_line.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor), abilities.json (data) +1 more | RESOLVED |
| `Melee_Impact` | **WIRED** | ...fabs/Projectiles 2D/2D Projectile 24 green explosion.prefab | HovlVfxCatalogGenerator.cs (editor), VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) +2 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `Melee_Slash` | **WIRED** | ...io/RPG VFX Bundle/Random effect prefabs/Dragon punch.prefab | HovlVfxCatalogGenerator.cs (editor), VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) +2 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `NoneMageHealingCast_Cast` | **WIRED** | ...PG VFX Bundle/Random effect prefabs/Buff white twist.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor), abilities.json (data) +1 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `PP_BigExplosion` | **ORPHAN** | ...amples/Fire & Explosion Effects/Prefabs/BigExplosion.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_BigSplash` | **ORPHAN** | ...ePack/EffectExamples/Water Effects/Prefabs/BigSplash.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_Candles` | **ORPHAN** | ...iclePack/EffectExamples/Misc Effects/Prefabs/Candles.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_Dissolve` | **ORPHAN** | ...clePack/EffectExamples/Misc Effects/Prefabs/Dissolve.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_DissolveSolidHorizontal` | **ORPHAN** | ...xamples/Misc Effects/Prefabs/DissolveSolidHorizontal.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_DustExplosion` | **ORPHAN** | ...mples/Fire & Explosion Effects/Prefabs/DustExplosion.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_DustMotesEffect` | **ORPHAN** | .../EffectExamples/Misc Effects/Prefabs/DustMotesEffect.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_DustStorm` | **ORPHAN** | ...fectExamples/Smoke & Steam Effects/Prefabs/DustStorm.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_EarthShatter` | **ORPHAN** | ...ck/EffectExamples/Magic Effects/Prefabs/EarthShatter.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_ElectricalSparks` | **ORPHAN** | ...EffectExamples/Misc Effects/Prefabs/ElectricalSparks.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_ElectricalSparksEffect` | **ORPHAN** | ...ples/Legacy Particles/Prefabs/ElectricalSparksEffect.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_EllenDissolve` | **ORPHAN** | ...ck/EffectExamples/Misc Effects/Prefabs/EllenDissolve.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_EllenRespawn` | **ORPHAN** | ...ack/EffectExamples/Misc Effects/Prefabs/EllenRespawn.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_EnergyExplosion` | **ORPHAN** | ...les/Fire & Explosion Effects/Prefabs/EnergyExplosion.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_FireBall` | **WIRED** | ...ctExamples/Fire & Explosion Effects/Prefabs/FireBall.prefab | Enemy.cs, VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - prefab is in a GITIGNORED art root |
| `PP_FireFlies` | **ORPHAN** | ...lePack/EffectExamples/Misc Effects/Prefabs/FireFlies.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_FlameStream` | **ORPHAN** | ...xamples/Fire & Explosion Effects/Prefabs/FlameStream.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_FlameThrower` | **ORPHAN** | ...amples/Fire & Explosion Effects/Prefabs/FlameThrower.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_FleshImpacts` | **ORPHAN** | ...k/EffectExamples/Weapon Effects/Prefabs/FleshImpacts.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_GoopSpray` | **ORPHAN** | ...lePack/EffectExamples/Goop Effects/Prefabs/GoopSpray.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_GoopSprayEffect` | **ORPHAN** | .../EffectExamples/Goop Effects/Prefabs/GoopSprayEffect.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_GoopStreamEffect` | **ORPHAN** | ...EffectExamples/Goop Effects/Prefabs/GoopStreamEffect.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_GroundFog` | **WIRED** | ...fectExamples/Smoke & Steam Effects/Prefabs/GroundFog.prefab | DungeonWorldPortalSpawner.cs, VfxProofCapture.cs (editor), VfxCasterLibraryIndex.json (editor) +1 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `PP_HeatDistortion` | **ORPHAN** | ...xamples/Smoke & Steam Effects/Prefabs/HeatDistortion.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_IceLance` | **ORPHAN** | ...lePack/EffectExamples/Magic Effects/Prefabs/IceLance.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_LargeFlames` | **ORPHAN** | ...xamples/Fire & Explosion Effects/Prefabs/LargeFlames.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_LightnigStormCloud` | **ORPHAN** | ...Examples/Legacy Particles/Prefabs/LightnigStormCloud.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_MediumFlames` | **ORPHAN** | ...amples/Fire & Explosion Effects/Prefabs/MediumFlames.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_MetalImpacts` | **ORPHAN** | ...k/EffectExamples/Weapon Effects/Prefabs/MetalImpacts.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_MuzzleFlash` | **WIRED** | ...ck/EffectExamples/Weapon Effects/Prefabs/MuzzleFlash.prefab | DefenseTower.cs, TowerCombat.cs, VfxProofCapture.cs (editor) +4 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `PP_PlasmaExplosionEffect` | **WIRED** | ...mples/Legacy Particles/Prefabs/PlasmaExplosionEffect.prefab | ArcaneTower.cs, DefenseTower.cs, TowerCombat.cs +2 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `PP_PoisonGas` | **ORPHAN** | ...fectExamples/Smoke & Steam Effects/Prefabs/PoisonGas.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_PressurisedSteam` | **ORPHAN** | ...mples/Smoke & Steam Effects/Prefabs/PressurisedSteam.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_RainEffect` | **ORPHAN** | ...k/EffectExamples/Legacy Particles/Prefabs/RainEffect.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_Respawn` | **ORPHAN** | ...iclePack/EffectExamples/Misc Effects/Prefabs/Respawn.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_RisingSteam` | **ORPHAN** | ...ctExamples/Smoke & Steam Effects/Prefabs/RisingSteam.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_RocketTrail` | **ORPHAN** | ...ctExamples/Smoke & Steam Effects/Prefabs/RocketTrail.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_SandImpacts` | **ORPHAN** | ...ck/EffectExamples/Weapon Effects/Prefabs/SandImpacts.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_SandSwirlsEffect` | **ORPHAN** | ...EffectExamples/Misc Effects/Prefabs/SandSwirlsEffect.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_Shower` | **ORPHAN** | ...iclePack/EffectExamples/Water Effects/Prefabs/Shower.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_SmallExplosion` | **ORPHAN** | ...ples/Fire & Explosion Effects/Prefabs/SmallExplosion.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_SmokeEffect` | **ORPHAN** | ...ctExamples/Smoke & Steam Effects/Prefabs/SmokeEffect.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_SparksEffect` | **ORPHAN** | ...EffectExamples/Legacy Particles/Prefabs/SparksEffect.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_Steam` | **ORPHAN** | ...k/EffectExamples/Smoke & Steam Effects/Prefabs/Steam.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_StoneImpacts` | **ORPHAN** | ...k/EffectExamples/Weapon Effects/Prefabs/StoneImpacts.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_TinyExplosion` | **ORPHAN** | ...mples/Fire & Explosion Effects/Prefabs/TinyExplosion.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_TinyFlames` | **ORPHAN** | ...Examples/Fire & Explosion Effects/Prefabs/TinyFlames.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_WaterFall` | **ORPHAN** | ...ck/EffectExamples/Legacy Particles/Prefabs/WaterFall.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_WaterLeak` | **ORPHAN** | ...ePack/EffectExamples/Water Effects/Prefabs/WaterLeak.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_WildFire` | **ORPHAN** | ...ctExamples/Fire & Explosion Effects/Prefabs/WildFire.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PP_WoodImpacts` | **ORPHAN** | ...ck/EffectExamples/Weapon Effects/Prefabs/WoodImpacts.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `PosionCloud_Cast` | **WIRED** | ... Vol 1/Prefabs/Flash and hits/Hit 24 green explosion.prefab | WeaponVfxMap.cs, WeaponElementalOnHitTests.cs (test), VfxCasterLibraryIndex.json (editor) +1 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `RangedAttack-DaggerThrow_Projectile` | **ORPHAN** | ... Vol 1/Prefabs/Projectiles 2D/2D Projectile 8 dagger.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `RangedSpell-Powerful(Longcast)_Cast` | **ORPHAN** | .../Prefabs/Projectiles 2D/2D Projectile 17 nova violet.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `RangerTowerBaseProjectile_Projectile` | **WIRED** | .../Prefabs/Projectiles 2D/2D Projectile 1 nature arrow.prefab | DefenseTower.cs, VfxProofCapture.cs (editor), TowerProjectileTierTests.cs (test) +4 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `RangerTowerUpgraded_Projectile` | **WIRED** | ...l 1/Prefabs/Projectiles with logic/Projectile 7 pink.prefab | DefenseTower.cs, VfxProofCapture.cs (editor), VfxCasterLibraryIndex.json (editor) +3 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `RangerTowerlevel2Projectile_Projectile` | **ORPHAN** | ...1/Prefabs/Projectiles 2D/2D Projectile 14 blue rapid.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `SimpleCast_Cast` | **WIRED** | ...ojectiles Vol 1/Prefabs/Flash and hits/Flash 16 fire.prefab | ArcaneTower.cs, DefenseTower.cs, TowerCombat.cs +5 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `SimpleCast_Projectile` | **WIRED** | ...les Vol 1/Prefabs/Flash and hits/Flash 10 blue laser.prefab | DefenseTower.cs, VfxProofCapture.cs (editor), VfxCasterLibraryIndex.json (editor) +3 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `Spear_Projectile` | **ORPHAN** | .../Prefabs/Projectiles 2D/2D Projectile 1 nature arrow.prefab | HovlVfxCatalogGenerator.cs (editor), VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `Thunderbolt_Cast` | **WIRED** | ...Projectiles(Particle collision)/Projectile 2 electro.prefab | HovlVfxCatalogGenerator.cs (editor), VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) +2 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `Thunderbolt_Impact` | **WIRED** | .../Prefabs/Projectiles with logic/Projectile 2 electro.prefab | WeaponVfxMap.cs, HovlVfxCatalogGenerator.cs (editor), WeaponElementalOnHitTests.cs (test) +4 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `Thunderbolt_Projectile` | **WIRED** | ...Projectiles(Particle collision)/Projectile 2 electro.prefab | HovlVfxCatalogGenerator.cs (editor), VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) +2 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `TreeofLifeAura_Aura` | **WIRED** | ...lePack/EffectExamples/Misc Effects/Prefabs/FireFlies.prefab | HeartAuraController.cs, VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - prefab is in a GITIGNORED art root |
| `UpgradeStructureComplete_Aura` | **WIRED** | ...ticle Systems/Ultimate VFX/Demos/Fireworks/Fireworks.prefab | BuildModeController.cs, VfxAuraDifferentiationRegression.cs (editor), VfxLoopFlagRegression.cs (editor) +2 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `UpgradeVisual_Aura` | **WIRED** | ...ts/Lana Studio/Casual RPG VFX/Prefabs/Orbs/Orbs_fire.prefab | UnderConstructionVisual.cs, VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | RESOLVED |
| `Water_Projectile` | **ORPHAN** | ... 1/Prefabs/Projectiles with logic/Projectile 9 water.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `Weaponskillsword_Impact` | **WIRED** | ...A Projectiles Vol 1/Prefabs/Flash and hits/Hit 5 red.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor), abilities.json (data) +1 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `icebasedprojectile_Projectile` | **WIRED** | ...lePack/EffectExamples/Magic Effects/Prefabs/IceLance.prefab | DefenseTower.cs, VfxProofCapture.cs (editor), VfxCasterLibraryIndex.json (editor) +1 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `lighteningOnSpellLand_Impact` | **ORPHAN** | .../RPG VFX Bundle/Random effect prefabs/Electro splash.prefab | VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `onweaponskillmaybe_Impact` | **ORPHAN** | ...tiles Vol 1/Prefabs/Flash and hits/Hit 20 pink arrow.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |
| `softhealingaura_Aura` | **WIRED** | ...o/RPG VFX Bundle/Random effect prefabs/Buff palladin.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor), abilities.json (data) +1 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `subtleHealinginarea(EnemySkill-Mage)_Cast` | **ORPHAN** | ... VFX Bundle/Random effect prefabs/Buff orange circle.prefab | VfxCasterLibraryIndex.json (editor), VfxManualPicks.json (editor) | GITIGNORED - editor/test refs only; prefab is in a GITIGNORED art root |

### 4.2 `VFXType` ordinals — `Assets/Resources/VFX/VFXCatalog.asset`

A `VFXType` with no wired prefab is **not** broken: `VFXManager.ProceduralFallback` /
`ProceduralLoopFallback` render a stand-in, so something is drawn. Those rows are
bucketed `FALLBACK`, which reads as "ships, but with placeholder art".

| key | bucket | prefab | called by | note |
|---|---|---|---|---|
| `Impact_Physical` | **WIRED** | ...Studio/Casual RPG VFX/Prefabs/Slash/Slash_stone_once.prefab | DefenseTower.cs, TowerCombat.cs, Enemy.cs +4 more | RESOLVED |
| `Impact_Aether` | **WIRED** | ...Studio/Casual RPG VFX/Prefabs/Range_attack/Hit_magic.prefab | DefenseTower.cs, PooledProjectile.cs, SupportFieldStructure.cs +6 more | RESOLVED |
| `Impact_Flame` | **WIRED** | Assets/Resources/VFX/Projectiles/Spell_Fire_6.prefab | DefenseTower.cs, TowerCombat.cs, Enemy.cs +2 more | RESOLVED |
| `Impact_Ice` | **WIRED** | ...Studio/Casual RPG VFX/Prefabs/Range_attack/Hit_frost.prefab | DefenseTower.cs, SupportFieldStructure.cs, TowerCombat.cs +3 more | RESOLVED |
| `Impact_Heal` | **WIRED** | ...Studio/Casual RPG VFX/Prefabs/Range_attack/Hit_heart.prefab | SupportFieldStructure.cs, AbilityVfxKit.cs, AegisSetEffect.cs +6 more | RESOLVED |
| `Impact_ExplosionFire` | **WIRED** | ...rticles/Prefabs/Projectiles/Explosion/Explosion_Fire.prefab | TowerCombat.cs, DragonBoss.cs, AbilityVfxKit.cs +2 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `Impact_ExplosionAether` | **WIRED** | ...icles/Prefabs/Projectiles/Explosion/Explosion_Arcane.prefab | SupportFieldStructure.cs, TowerCombat.cs, EliteVFXController.cs +4 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `Impact_ShockwaveRing` | **WIRED** | ...Lana Studio/Casual RPG VFX/Prefabs/Burst/Burst_rings.prefab | Enemy.cs, PlayerAttackController.cs, AbilityVfxKit.cs +4 more | RESOLVED |
| `Impact_ShardsBurst` | **ORPHAN** | ...Lana Studio/Casual RPG VFX/Prefabs/Burst/Burst_sharp.prefab | VFXManager.cs | RESOLVED - only its own declaration/plumbing references it |
| `Impact_SmokeWisps` | **ORPHAN** | ...ana Studio/Casual RPG VFX/Prefabs/Burst/Poof_generic.prefab | VFXManager.cs | RESOLVED - only its own declaration/plumbing references it |
| `Projectile_ArcaneBolt` | **WIRED** | Assets/Resources/VFX/Projectiles/Projectile_Arcane.prefab | AbilityVfxKit.cs, SpellVfxFactory.cs | RESOLVED |
| `Projectile_FrostBolt` | **WIRED** | Assets/Resources/VFX/Projectiles/Projectile_Ice.prefab | SpellVfxFactory.cs | RESOLVED |
| `Projectile_Arrow` | **WIRED** | ... VFX/Prefabs/Range_attack/Projectiles_green_shuriken.prefab | AbilityVfxKit.cs, SpellVfxFactory.cs, VFXManager.cs | RESOLVED |
| `Projectile_FlameArrow` | **WIRED** | Assets/Resources/VFX/Projectiles/Projectile_Fire_3.prefab | AbilityVfxKit.cs, SpellVfxFactory.cs, VFXManager.cs | RESOLVED |
| `Projectile_TowerArcane` | **FALLBACK** | - | DefenseTower.cs, VFXManager.cs, VfxProofCapture.cs (editor) | NOROW - no VFXCatalog row |
| `Projectile_TowerFire` | **FALLBACK** | - | DefenseTower.cs, VFXManager.cs | NOROW - no VFXCatalog row |
| `Projectile_TowerIce` | **FALLBACK** | - | DefenseTower.cs | NOROW - no VFXCatalog row |
| `Projectile_EnemyCasterBolt` | **ORPHAN** | ... RPG VFX/Prefabs/Range_attack/Projectiles_dark_magic.prefab | - | RESOLVED - no reference anywhere in the scanned corpus |
| `Cast_MageCharge` | **WIRED** | ...ana Studio/Casual RPG VFX/Prefabs/Orbs/Orbs_electric.prefab | DefenseTower.cs, TowerCombat.cs, SpellVfxFactory.cs +2 more | RESOLVED |
| `Cast_FireCharge` | **WIRED** | Assets/Resources/VFX/Projectiles/Casting_Fire.prefab | SpellVfxFactory.cs | RESOLVED |
| `Cast_KnightSlam` | **WIRED** | ...dio/Casual RPG VFX/Prefabs/Burst/Flash_dubble_circle.prefab | SpellVfxFactory.cs, VFXManager.cs | RESOLVED |
| `Cast_RangerDraw` | **ORPHAN** | .../Lana Studio/Casual RPG VFX/Prefabs/Orbs/Orbs_leaves.prefab | VFXManager.cs | RESOLVED - only its own declaration/plumbing references it |
| `Cast_Heal` | **WIRED** | Assets/Spells Pack/Particles/Prefabs/Buffs/Buff_Nature.prefab | CastleDoorController.cs, HeroAbilities.cs, SpellVfxFactory.cs +2 more | GITIGNORED - prefab is in a GITIGNORED art root |
| `Cast_FrostNova` | **WIRED** | ... VFX/Prefabs/Area_generic/Area_generic_blue_outbreak.prefab | SpellVfxFactory.cs, VFXManager.cs | RESOLVED |
| `Cast_NecromancerSummon` | **ORPHAN** | ...VFX/Prefabs/Area_generic/Area_generic_green_outbreak.prefab | VFXManager.cs | RESOLVED - only its own declaration/plumbing references it |
| `Cast_EnemyCaster` | **ORPHAN** | ...ana Studio/Casual RPG VFX/Prefabs/Orbs/Orbs_electric.prefab | VFXManager.cs | RESOLVED - only its own declaration/plumbing references it |
| `Death_Skeleton` | **WIRED** | ...ana Studio/Casual RPG VFX/Prefabs/Burst/Poof_generic.prefab | Enemy.cs, VFXManager.cs, VfxProofCapture.cs (editor) | RESOLVED |
| `Death_Boss` | **WIRED** | Assets/Resources/VFX/Death/Boss_Death.prefab | Enemy.cs, VFXManager.cs | RESOLVED |
| `Death_Brute` | **WIRED** | Assets/Resources/VFX/Death/Death_Brute.prefab | Enemy.cs, VFXManager.cs | RESOLVED |
| `Death_Wolf` | **ORPHAN** | .../Lana Studio/Casual RPG VFX/Prefabs/Burst/Poof_water.prefab | VFXManager.cs | RESOLVED - only its own declaration/plumbing references it |
| `Death_Tiefling` | **ORPHAN** | Assets/Resources/VFX/Death/Death_Tiefling.prefab | VFXManager.cs | RESOLVED - only its own declaration/plumbing references it |
| `Death_Generic` | **WIRED** | Assets/Resources/VFX/Death/Death_Generic.prefab | Enemy.cs, HeroHealth.cs, VFXManager.cs +1 more | RESOLVED |
| `Aura_EnemyCaster` | **WIRED** | Assets/Resources/VFX/Aura/Aura_EnemyCaster.prefab | SupportFieldStructure.cs, Enemy.cs, VFXManager.cs | RESOLVED |
| `Aura_Necromancer` | **WIRED** | ...ts/Lana Studio/Casual RPG VFX/Prefabs/Fog/Fog_poison.prefab | SupportFieldStructure.cs, Enemy.cs, VFXManager.cs | RESOLVED |
| `Aura_Healer` | **WIRED** | ...PG VFX/Prefabs/Regeneration/Regeneration_health_loop.prefab | SupportFieldStructure.cs, VFXManager.cs | RESOLVED |
| `Aura_Flame` | **WIRED** | .../Lana Studio/Casual RPG VFX/Prefabs/Fire/Fire_medium.prefab | GearAuraMap.cs, VFXManager.cs | RESOLVED |
| `Aura_Ice` | **WIRED** | Assets/Lana Studio/Casual RPG VFX/Prefabs/Fog/Fog_frost.prefab | SupportFieldStructure.cs, GearAuraMap.cs, VFXManager.cs | RESOLVED |
| `Aura_Dust` | **ORPHAN** | Assets/Resources/VFX/Aura/Aura_Dust.prefab | VFXManager.cs | RESOLVED - only its own declaration/plumbing references it |
| `Aura_SmokeReaper` | **WIRED** | ...Lana Studio/Casual RPG VFX/Prefabs/Fog/Fog_speedSlow.prefab | Enemy.cs, VFXManager.cs | RESOLVED |
| `Aura_HeartPulse` | **FALLBACK** | - | EchoSpiritPresentation.cs, HeartAuraController.cs, VFXManager.Hovl.cs +1 more | NOROW - no VFXCatalog row |
| `Aura_EmpowerTower` | **ORPHAN** | - | - | NOROW - no reference anywhere in the scanned corpus; no VFXCatalog row |
| `Aura_PetLevel1` | **ORPHAN** | Assets/Resources/VFX/Aura/Aura_PetLevel1.prefab | VFXManager.cs | RESOLVED - only its own declaration/plumbing references it |
| `Aura_PetLevel2` | **ORPHAN** | Assets/Resources/VFX/Aura/Aura_PetLevel2.prefab | VFXManager.cs | RESOLVED - only its own declaration/plumbing references it |
| `Aura_PetLevel3` | **ORPHAN** | Assets/Resources/VFX/Aura/Aura_PetLevel3.prefab | VFXManager.cs | RESOLVED - only its own declaration/plumbing references it |
| `Env_TorchFlame` | **WIRED** | ...s/Lana Studio/Casual RPG VFX/Prefabs/Fire/Fire_small.prefab | EnvironmentVFX.cs, VFXManager.cs | RESOLVED |
| `Env_LanternGlow` | **ORPHAN** | - | - | NOROW - no reference anywhere in the scanned corpus; no VFXCatalog row |
| `Env_GroundFog` | **ORPHAN** | - | - | NOROW - no reference anywhere in the scanned corpus; no VFXCatalog row |
| `Env_DungeonPortal` | **WIRED** | Assets/Resources/VFX/Portal/Env_DungeonPortal.prefab | PortalVFXController.cs | RESOLVED |
| `Env_TitleEmbers` | **ORPHAN** | - | - | NOROW - no reference anywhere in the scanned corpus; no VFXCatalog row |
| `Env_DestructionDust` | **WIRED** | ...ana Studio/Casual RPG VFX/Prefabs/Burst/Poof_generic.prefab | EnvironmentVFX.cs, StructureHitReaction.cs | RESOLVED |
| `Env_DestructionSparks` | **FALLBACK** | - | EnvironmentVFX.cs | NOROW - no VFXCatalog row |
| `Juice_CriticalHit` | **WIRED** | .../Lana Studio/Casual RPG VFX/Prefabs/Burst/Flash_star.prefab | PlayerAttackController.cs, VFXManager.cs | RESOLVED |
| `Juice_KillStreak` | **ORPHAN** | ...udio/Casual RPG VFX/Prefabs/Burst/Burst_rainbow_mist.prefab | VFXManager.cs | RESOLVED - only its own declaration/plumbing references it |
| `Juice_WaveClear` | **WIRED** | ...s/Lana Studio/Casual RPG VFX/Prefabs/States/Level_up.prefab | BattleArena.cs, VFXManager.cs | RESOLVED |
| `Juice_LevelUp` | **WIRED** | ...s/Lana Studio/Casual RPG VFX/Prefabs/States/Level_up.prefab | BattleArena.cs, LevelUpVFXController.cs, VFXManager.cs +1 more | RESOLVED |
| `Juice_GroundDecal_Flame` | **ORPHAN** | - | VFXManager.cs | NOROW - only its own declaration/plumbing references it; no VFXCatalog row |
| `Juice_GroundDecal_Ice` | **ORPHAN** | - | VFXManager.cs | NOROW - only its own declaration/plumbing references it; no VFXCatalog row |
| `Portal_Enter` | **WIRED** | Assets/Resources/VFX/Portal/Portal_Enter.prefab | PortalVFXController.cs, VFXManager.cs | RESOLVED |
| `Portal_Exit` | **WIRED** | Assets/Resources/VFX/Portal/Portal_Exit.prefab | PortalVFXController.cs, VFXManager.cs | RESOLVED |
| `WaveClear_Celebration` | **WIRED** | ...s/Lana Studio/Casual RPG VFX/Prefabs/States/Level_up.prefab | BattleArena.cs, VFXManager.cs, WaveCelebrationManager.cs | RESOLVED |
| `LevelUp_Celebration` | **WIRED** | ...s/Lana Studio/Casual RPG VFX/Prefabs/States/Level_up.prefab | CollectorStackView.cs, ~~AuraController.cs~~ (DELETED 2026-08-16, WO-993), VFXManager.cs | RESOLVED |
| `Combo_Tier1` | **WIRED** | ...ana Studio/Casual RPG VFX/Prefabs/Burst/Flash_circle.prefab | VFXManager.cs, KillComboTracker.cs | RESOLVED |
| `Combo_Tier2` | **WIRED** | ...dio/Casual RPG VFX/Prefabs/Burst/Flash_dubble_circle.prefab | VFXManager.cs, KillComboTracker.cs | RESOLVED |
| `Pet_Aura_Fire` | **ORPHAN** | - | VFXManager.cs | NOROW - only its own declaration/plumbing references it; no VFXCatalog row |
| `Pet_Aura_Ice` | **ORPHAN** | - | VFXManager.cs | NOROW - only its own declaration/plumbing references it; no VFXCatalog row |
| `Pet_Attack` | **ORPHAN** | - | VFXManager.cs | NOROW - only its own declaration/plumbing references it; no VFXCatalog row |
| `ShootingStar` | **ORPHAN** | - | VFXManager.cs | NOROW - only its own declaration/plumbing references it; no VFXCatalog row |
| `Death_EnemyExplosion_Dungeon` | **ORPHAN** | Assets/Resources/VFX/Death/Death_EnemyExplosion_Dungeon.prefab | VFXManager.cs | RESOLVED - only its own declaration/plumbing references it |
| `Elite_Spawn` | **WIRED** | Assets/Resources/VFX/Portal/Elite_Spawn.prefab | EliteVFXController.cs, VFXManager.cs | RESOLVED |
| `Elite_Death` | **WIRED** | Assets/Resources/VFX/Death/Elite_Death.prefab | EliteVFXController.cs, Enemy.cs, VFXManager.cs | RESOLVED |
| `Boss_Spawn` | **WIRED** | Assets/Resources/VFX/Portal/Boss_Spawn.prefab | EliteVFXController.cs, VFXManager.cs | RESOLVED |
| `Boss_Death` | **WIRED** | Assets/Resources/VFX/Death/Boss_Death.prefab | DragonBoss.cs, EliteVFXController.cs, VFXManager.cs | RESOLVED |
| `Boss_AttackImpact` | **FALLBACK** | - | DragonBoss.cs, EliteVFXController.cs, VFXManager.cs | NOROW - no VFXCatalog row |
| `Boss_PhaseTransition` | **FALLBACK** | - | DragonBoss.cs, VFXManager.cs | NOROW - no VFXCatalog row |
| `Boss_Telegraph` | **FALLBACK** | - | DragonBoss.cs, VFXManager.cs | NOROW - no VFXCatalog row |
| `Boss_Aura_Phase1` | **WIRED** | Assets/Resources/VFX/Aura/Boss_Aura_Phase1.prefab | DragonBoss.cs, VFXManager.cs | RESOLVED |
| `Boss_Aura_Phase2` | **WIRED** | Assets/Resources/VFX/Aura/Boss_Aura_Phase2.prefab | DragonBoss.cs, VFXManager.cs | RESOLVED |
| `Boss_Aura_Phase3` | **WIRED** | Assets/Resources/VFX/Aura/Boss_Aura_Phase3.prefab | DragonBoss.cs, VFXManager.cs | RESOLVED |
| `Boss_FireBreath` | **WIRED** | Assets/Resources/VFX/Boss/Boss_FireBreath.prefab | DragonBoss.cs | RESOLVED |
| `Env_Candle` | **ORPHAN** | Assets/Resources/VFX/Env/Env_Candle.prefab | - | RESOLVED - no reference anywhere in the scanned corpus |
| `Env_SteamVent` | **ORPHAN** | Assets/Resources/VFX/Env/Env_SteamVent.prefab | - | RESOLVED - no reference anywhere in the scanned corpus |
| `Env_SteamBurst` | **ORPHAN** | Assets/Resources/VFX/Env/Env_SteamBurst.prefab | - | RESOLVED - no reference anywhere in the scanned corpus |
| `Cast_MuzzleFlash` | **WIRED** | Assets/Resources/VFX/Weapon/Cast_MuzzleFlash.prefab | TowerCombat.cs, RangedAttackVFX.cs | RESOLVED |
| `Enemy_Spawn` | **FALLBACK** | - | EliteVFXController.cs, ParticlePackVfxBatchBuilder.cs (editor) | NOROW - no VFXCatalog row |
| `Despawn_Dissolve` | **ORPHAN** | - | - | NOROW - no reference anywhere in the scanned corpus; no VFXCatalog row |
| `Aura_LowHealth` | **WIRED** | Assets/Resources/VFX/Aura/Aura_LowHealth.prefab | HeroHpStateAura.cs | RESOLVED |
| `Aura_NearDeath` | **WIRED** | Assets/Resources/VFX/Aura/Aura_NearDeath.prefab | HeroHpStateAura.cs | RESOLVED |
| `Aura_HealingInProgress` | **WIRED** | Assets/Resources/VFX/Aura/Aura_HealingInProgress.prefab | HeroHpStateAura.cs | RESOLVED |
| `Aura_ItemHeal` | **WIRED** | Assets/Resources/VFX/Aura/Aura_ItemHeal.prefab | GearAuraMap.cs | RESOLVED |
| `Harvest_Iron` | **WIRED** | Assets/Resources/VFX/Harvest/Harvest_Iron.prefab | HarvestAura.cs | RESOLVED |
| `Harvest_Wood` | **WIRED** | Assets/Resources/VFX/Harvest/Harvest_Wood.prefab | HarvestAura.cs | RESOLVED |
| `Harvest_Food` | **WIRED** | Assets/Resources/VFX/Harvest/Harvest_Food.prefab | HarvestAura.cs | RESOLVED |
| `Harvest_Crystal` | **WIRED** | Assets/Resources/VFX/Harvest/Harvest_Crystal.prefab | HarvestAura.cs | RESOLVED |
| `Harvest_Gold` | **ORPHAN** | Assets/Resources/VFX/Harvest/Harvest_Gold.prefab | - | RESOLVED - no reference anywhere in the scanned corpus |
| `Collector_Ready` | **WIRED** | Assets/Resources/VFX/Harvest/Collector_Ready.prefab | HarvestAura.cs | RESOLVED |

### 4.3 `SfxId` — `Assets/_Modules/Audio/SfxId.cs`

> **There is no `SfxClipLibrary` asset anywhere in `Assets/`.** `AudioService`
> Resources-loads `"Audio/SfxClipLibrary"`, gets null, and every `SfxId` therefore resolves to the
> **procedurally synthesised** clip from `ProceduralSfx.For(id)` — audible, but not
> authored audio. Wiring an authored library is a drop-in upgrade with no code change.

| key | bucket | clip source | called by | note |
|---|---|---|---|---|
| `FireExplosion` | **WIRED** | ProceduralSfx.For synthesised clip | ProceduralSfx.cs, BuildModeController.cs, VFXManager.cs +1 more | PROCEDURAL |
| `ArcaneExplosion` | **WIRED** | ProceduralSfx.For synthesised clip | ProceduralSfx.cs, VFXManager.cs, SfxClipLibraryBuilder.cs (editor) | PROCEDURAL - fires ONLY via VFXManager's VFXType->SfxId pairing — silent whenever the paired VFXType is not played |
| `Shockwave` | **WIRED** | ProceduralSfx.For synthesised clip | ProceduralSfx.cs, VFXManager.cs, SfxClipLibraryBuilder.cs (editor) | PROCEDURAL - fires ONLY via VFXManager's VFXType->SfxId pairing — silent whenever the paired VFXType is not played |
| `Heal` | **WIRED** | ProceduralSfx.For synthesised clip | ProceduralSfx.cs, VFXManager.cs, SfxClipLibraryBuilder.cs (editor) | PROCEDURAL - fires ONLY via VFXManager's VFXType->SfxId pairing — silent whenever the paired VFXType is not played |
| `WizardCast` | **WIRED** | ProceduralSfx.For synthesised clip | ProceduralSfx.cs, VFXManager.cs, SfxClipLibraryBuilder.cs (editor) | PROCEDURAL - fires ONLY via VFXManager's VFXType->SfxId pairing — silent whenever the paired VFXType is not played |
| `FlameArrowLaunch` | **WIRED** | ProceduralSfx.For synthesised clip | ProceduralSfx.cs, VFXManager.cs, SfxClipLibraryBuilder.cs (editor) | PROCEDURAL - fires ONLY via VFXManager's VFXType->SfxId pairing — silent whenever the paired VFXType is not played |
| `TowerShot` | **WIRED** | ProceduralSfx.For synthesised clip | ProceduralSfx.cs, VFXManager.cs, SfxClipLibraryBuilder.cs (editor) | PROCEDURAL - fires ONLY via VFXManager's VFXType->SfxId pairing — silent whenever the paired VFXType is not played |
| `EnemyDeath` | **WIRED** | ProceduralSfx.For synthesised clip | ProceduralSfx.cs, VFXManager.cs, SfxClipLibraryBuilder.cs (editor) | PROCEDURAL - fires ONLY via VFXManager's VFXType->SfxId pairing — silent whenever the paired VFXType is not played |
| `WaveClear` | **WIRED** | ProceduralSfx.For synthesised clip | ProceduralSfx.cs, VFXManager.cs, SfxClipLibraryBuilder.cs (editor) | PROCEDURAL - fires ONLY via VFXManager's VFXType->SfxId pairing — silent whenever the paired VFXType is not played |
| `LevelUp` | **WIRED** | ProceduralSfx.For synthesised clip | ProceduralSfx.cs, BuildModeController.cs, VFXManager.cs +1 more | PROCEDURAL |
| `ComboSmall` | **WIRED** | ProceduralSfx.For synthesised clip | ProceduralSfx.cs, VFXManager.cs, SfxClipLibraryBuilder.cs (editor) | PROCEDURAL - fires ONLY via VFXManager's VFXType->SfxId pairing — silent whenever the paired VFXType is not played |
| `ComboBig` | **WIRED** | ProceduralSfx.For synthesised clip | ProceduralSfx.cs, VFXManager.cs, SfxClipLibraryBuilder.cs (editor) | PROCEDURAL - fires ONLY via VFXManager's VFXType->SfxId pairing — silent whenever the paired VFXType is not played |
| `PetFireAura` | **WIRED** | ProceduralSfx.For synthesised clip | ProceduralSfx.cs, VFXManager.cs, SfxClipLibraryBuilder.cs (editor) | PROCEDURAL - fires ONLY via VFXManager's VFXType->SfxId pairing — silent whenever the paired VFXType is not played |
| `PetAttack` | **WIRED** | ProceduralSfx.For synthesised clip | ProceduralSfx.cs, VFXManager.cs, SfxClipLibraryBuilder.cs (editor) | PROCEDURAL - fires ONLY via VFXManager's VFXType->SfxId pairing — silent whenever the paired VFXType is not played |
| `WardLit` | **WIRED** | ProceduralSfx.For synthesised clip | ProceduralSfx.cs, WardStone.cs, SfxClipLibraryBuilder.cs (editor) | PROCEDURAL |
| `WardDim` | **WIRED** | ProceduralSfx.For synthesised clip | ProceduralSfx.cs, WardStone.cs, SfxClipLibraryBuilder.cs (editor) | PROCEDURAL |

### 4.4 `MusicTrack` — `Assets/_Modules/Core/Audio/MusicTrack.cs`

Clips are bound in `AudioBootstrap` by `Resources.Load<AudioClip>(<name>)`; the map
resolves each name against every `Assets/**/Resources/` root.

> **Assignment names that resolve to no file:** `Village` → `"village"`, `Battle` → `"battle_theme_NEW"`, `Battle` → `"battle_theme2_NEW"`, `Battle` → `"battle_theme3_NEW"`, `Overworld` → `"world_theme_NEW"`

| key | bucket | clip | called by | note |
|---|---|---|---|---|
| `Village` | **WIRED** | Assets/Audio/Resources/whispering_pines.mp3 | AudioBootstrap.cs, AudioService.cs, MusicDirector.cs +4 more | RESOURCES - Resources.Load name(s) with no file: village |
| `Battle` | **WIRED** | Assets/Audio/Resources/siege_iron_bastion.mp3 | AudioBootstrap.cs, AudioService.cs, MusicDirector.cs +3 more | RESOURCES - Resources.Load name(s) with no file: battle_theme_NEW, battle_theme2_NEW, battle_theme3_NEW |
| `Victory` | **WIRED** | Assets/Audio/Resources/victory.mp3 | AudioBootstrap.cs, AudioService.cs, MusicDirector.cs +8 more | RESOURCES |
| `Dungeon` | **WIRED** | Assets/Audio/Resources/Music/echoes_beneath_elarion.mp3 | AudioBootstrap.cs, AudioService.cs, MusicDirector.cs +2 more | RESOURCES |
| `Overworld` | **WIRED** | Assets/Audio/Resources/mainworld1_NEW.mp3 | AudioBootstrap.cs, AudioService.cs, MusicDirector.cs +5 more | RESOURCES - Resources.Load name(s) with no file: world_theme_NEW |
| `Defeat` | **WIRED** | ...at.mp3, Assets/Audio/Resources/Music/heartwood_collapse.wav | AudioBootstrap.cs, AudioService.cs, MusicDirector.cs +4 more | RESOURCES |
| `Title` | **WIRED** | Assets/Audio/Resources/title.mp3 | AudioBootstrap.cs, AudioService.cs, MusicDirector.cs +2 more | RESOURCES |
| `Arena` | **WIRED** | Assets/Audio/Resources/Music/echo_theme.mp3 | AudioBootstrap.cs, AudioService.cs, MusicDirector.cs +3 more | RESOURCES |
| `Raid` | **WIRED** | Assets/Audio/Resources/Music/Raid/brass-rampart.mp3 | AudioBootstrap.cs, AudioService.cs, MusicDirector.cs +2 more | RESOURCES |

### 4.5 `Resources/Sfx/*` — the string-keyed audio inventory

A **second** audio path parallel to `SfxId`: `GameSfx`, `EnemyCombatAudio` and the
`sfxId` / `sfxImpact` columns of `motion-castings.json` load clips **by name** out of
`Resources/Sfx/`. An orphan here is a shipped audio file nothing plays.

| key | bucket | file | called by | note |
|---|---|---|---|---|
| `Sfx/BuildingUpgrade` | **WIRED** | Assets/_Modules/Audio/Resources/Sfx/BuildingUpgrade.wav | GameSfx.cs, quests.json (data) +1 more | RESOURCES |
| `Sfx/DragonRoar` | **WIRED** | Assets/_Modules/Audio/Resources/Sfx/DragonRoar.mp3 | GameSfx.cs, AudioService.cs (mention), SfxResolveRegression.cs (mention) | RESOURCES |
| `Sfx/EnemyCastCharge` | **WIRED** | Assets/_Modules/Audio/Resources/Sfx/EnemyCastCharge.wav | EnemyCombatAudio.cs, AudioService.cs (mention) | RESOURCES |
| `Sfx/EnemyDeath` | **WIRED** | Assets/_Modules/Audio/Resources/Sfx/EnemyDeath.wav | GameSfx.cs, EnemyCombatAudio.cs, AudioService.cs (mention) | RESOURCES |
| `Sfx/EnemyDeath2` | **WIRED** | Assets/_Modules/Audio/Resources/Sfx/EnemyDeath2.wav | EnemyCombatAudio.cs, AudioService.cs (mention) | RESOURCES |
| `Sfx/EnemyHit` | **WIRED** | Assets/_Modules/Audio/Resources/Sfx/EnemyHit.wav | EnemyCombatAudio.cs, AudioService.cs (mention), HudUiRegression.cs (mention) | RESOURCES |
| `Sfx/FootstepsWalk` | **WIRED** | Assets/_Modules/Audio/Resources/Sfx/FootstepsWalk.mp3 | HeroLocomotion.cs, SfxResolveRegression.cs (mention) | RESOURCES |
| `Sfx/Heal` | **WIRED** | Assets/Resources/Sfx/Heal.mp3 | AudioService.cs (mention), motion-castings.json (data), weaponskill-animations.json (data) +2 more | RESOURCES - 2 files share this Resources name |
| `Sfx/HeroHit` | **WIRED** | Assets/_Modules/Audio/Resources/Sfx/HeroHit.wav | GameSfx.cs, AudioService.cs (mention) | RESOURCES |
| `Sfx/LookoutHorn` | **WIRED** | Assets/Resources/Sfx/LookoutHorn.wav | GameSfx.cs, AudioService.cs (mention) | RESOURCES |
| `Sfx/Sfx_ArcaneExplosion` | **ORPHAN** | Assets/_Modules/Audio/Resources/Sfx/Sfx_ArcaneExplosion.wav | - | RESOURCES - no reference anywhere in the scanned corpus |
| `Sfx/Sfx_EnemyDeath` | **ORPHAN** | Assets/_Modules/Audio/Resources/Sfx/Sfx_EnemyDeath.wav | - | RESOURCES - no reference anywhere in the scanned corpus |
| `Sfx/Sfx_FireExplosion` | **ORPHAN** | Assets/_Modules/Audio/Resources/Sfx/Sfx_FireExplosion.wav | - | RESOURCES - no reference anywhere in the scanned corpus |
| `Sfx/Sfx_FlameArrowLaunch` | **ORPHAN** | Assets/_Modules/Audio/Resources/Sfx/Sfx_FlameArrowLaunch.wav | - | RESOURCES - no reference anywhere in the scanned corpus |
| `Sfx/Sfx_Heal` | **ORPHAN** | Assets/_Modules/Audio/Resources/Sfx/Sfx_Heal.wav | - | RESOURCES - no reference anywhere in the scanned corpus |
| `Sfx/Sfx_LevelUp` | **ORPHAN** | Assets/_Modules/Audio/Resources/Sfx/Sfx_LevelUp.wav | - | RESOURCES - no reference anywhere in the scanned corpus |
| `Sfx/Sfx_PetAttack` | **ORPHAN** | Assets/_Modules/Audio/Resources/Sfx/Sfx_PetAttack.wav | - | RESOURCES - no reference anywhere in the scanned corpus |
| `Sfx/Sfx_Shockwave` | **ORPHAN** | Assets/_Modules/Audio/Resources/Sfx/Sfx_Shockwave.wav | - | RESOURCES - no reference anywhere in the scanned corpus |
| `Sfx/Sfx_TowerShot` | **ORPHAN** | Assets/_Modules/Audio/Resources/Sfx/Sfx_TowerShot.wav | - | RESOURCES - no reference anywhere in the scanned corpus |
| `Sfx/Sfx_WardDim` | **ORPHAN** | Assets/_Modules/Audio/Resources/Sfx/Sfx_WardDim.wav | - | RESOURCES - no reference anywhere in the scanned corpus |
| `Sfx/Sfx_WardLit` | **ORPHAN** | Assets/_Modules/Audio/Resources/Sfx/Sfx_WardLit.wav | - | RESOURCES - no reference anywhere in the scanned corpus |
| `Sfx/Sfx_WizardCast` | **ORPHAN** | Assets/_Modules/Audio/Resources/Sfx/Sfx_WizardCast.wav | - | RESOURCES - no reference anywhere in the scanned corpus |
| `Sfx/Spell_Impact` | **WIRED** | Assets/Resources/Sfx/Spell_Impact.mp3 | AudioService.cs (mention), motion-castings.json (data) +1 more | RESOURCES |
| `Sfx/SpellCast` | **WIRED** | Assets/_Modules/Audio/Resources/Sfx/SpellCast.wav | GameSfx.cs, AudioService.cs (mention) | RESOURCES |
| `Sfx/SwordClash` | **WIRED** | Assets/_Modules/Audio/Resources/Sfx/SwordClash.wav | GameSfx.cs, AudioService.cs (mention), GameSfx.cs (mention) | RESOURCES |
| `Sfx/SwordClash2` | **ORPHAN** | Assets/_Modules/Audio/Resources/Sfx/SwordClash2.wav | AudioService.cs (mention) | RESOURCES - string mentions only, no load call; named in a string list (pre-warm) but never loaded — may still be reached by one of the dynamic loads in section 3 |
| `Sfx/SwordClash3` | **ORPHAN** | Assets/_Modules/Audio/Resources/Sfx/SwordClash3.wav | AudioService.cs (mention) | RESOURCES - string mentions only, no load call; named in a string list (pre-warm) but never loaded — may still be reached by one of the dynamic loads in section 3 |
| `Sfx/SwordClash4` | **ORPHAN** | Assets/_Modules/Audio/Resources/Sfx/SwordClash4.wav | AudioService.cs (mention) | RESOURCES - string mentions only, no load call; named in a string list (pre-warm) but never loaded — may still be reached by one of the dynamic loads in section 3 |
| `Sfx/Swords_Clash` | **WIRED** | Assets/Resources/Sfx/Swords_Clash.mp3 | AudioService.cs (mention), motion-castings.json (data) +1 more | RESOURCES |
| `Sfx/SwordSwing` | **WIRED** | Assets/_Modules/Audio/Resources/Sfx/SwordSwing.wav | GameSfx.cs, AudioService.cs (mention), SfxResolveRegression.cs (mention) | RESOURCES |
| `Sfx/TowerArrowHit` | **WIRED** | Assets/_Modules/Audio/Resources/Sfx/TowerArrowHit.wav | GameSfx.cs, AudioService.cs (mention) | RESOURCES |
| `Sfx/UiClick` | **WIRED** | Assets/_Modules/Audio/Resources/Sfx/UiClick.wav | AudioService.cs, SfxResolveRegression.cs (mention) | RESOURCES |
| `Sfx/WeaponDraw` | **WIRED** | Assets/_Modules/Audio/Resources/Sfx/WeaponDraw.wav | GameSfx.cs, AudioService.cs (mention), SfxResolveRegression.cs (mention) | RESOURCES |

## 5. Blind spots — what this map CANNOT see

Named on purpose. A map that quietly omits what it could not resolve is worse than
one that states its limits.

1. **Serialized references in prefabs and scenes.** A VFX prefab dragged into a
   MonoBehaviour field, or an `AudioClip` on an `AudioSource` in a `.unity` scene, is a
   real consumer this map does not count — it only follows *keys and enum ids*. A row
   marked ORPHAN may still be referenced by GUID from a prefab or scene.
2. **Runtime-built keys** — see §3 UNRESOLVED.
3. **Reachability.** "Has a caller" is not "a player reaches it". A caller behind a
   dead feature flag or an unreachable branch still counts as WIRED here.
4. **Gitignored art.** Prefabs under the 16 gitignored art roots resolve on this
   machine and on **no fresh clone**; those rows are marked but still counted as having
   an asset. `VfxResourceSelfContainmentRegression` is the authority on that exposure.
5. **`SetMusicClip` / `AddMusicClip` from other code or the Inspector.** Only
   `AudioBootstrap`'s literal assignments are parsed.
6. **Addressables / asset bundles.** Not consulted; everything here is `Resources` +
   direct GUID references.

---

Source of truth: `tools/vfx_audio_map.py`. Related canon:
`docs/reference/AUDIT_2026-08-09.md` ("every gate asserts a thing EXISTS, almost none
assert it is CONSUMED"), `docs/reference/STACK_UTILIZATION_2026-08-09.md`,
`Assets/Editor/Regression/VfxResourceSelfContainmentRegression.cs`.
