# VFXType call-site inventory — 2026-08-25

This is the WO-935 Phase 0 freeze. It is a dated measurement, not a second catalog and not permission to renumber `VFXType`.

## Method and headline

- Authority: `Assets/_Modules/Village/Vfx/VFXType.cs` at `3a05432f3`.
- `VFXType` contains **94 values: `None` plus 93 effects**. The older WO-935 figures (about 79/95) have drifted.
- `Assets/Resources/VFX/VFXCatalog.asset` contains **75 distinct numeric rows**. Two rows use ordinals **94 and 95**, beyond the current enum's last ordinal (93); they are orphaned serialized rows and need a separate append-only catalog investigation.
- “Gameplay” below means a runtime reference outside `VFXManager`, `VFXCatalog`, `VFXHandle`, and `SpellVfxFactory`. “Router only” means the key is available to procedural/factory routing but has no external runtime owner. Comments and Editor proof/build code do not count.
- **29 effect keys have zero direct gameplay owner. Of those, 24 have a serialized catalog row.** Nine have no runtime reference at all: `Projectile_EnemyCasterBolt`, `Aura_EmpowerTower`, `Env_LanternGlow`, `Env_GroundFog`, `Env_TitleEmbers`, `Env_SteamVent`, `Env_SteamBurst`, `Despawn_Dissolve`, and `Harvest_Gold`.

`art` means a non-empty numeric row exists in the serialized `VFXCatalog`; it does not prove the referenced prefab is self-contained or visually correct. Line numbers are navigation aids for this snapshot.

## Complete enum-to-gameplay map

| Ord | VFXType | art | Gameplay call sites (runtime) |
|---:|---|:---:|---|
| 0 | `None` | no | Sentinel used throughout enemy, aura, gear and harvest controllers; not an effect. |
| 1 | `Impact_Physical` | yes | `DefenseTower:1106`; `TowerCombat:627`; `Enemy:1833,3163`; `AbilityVfxKit:265,268`; `HeroHealth:578,625` |
| 2 | `Impact_Aether` | yes | `DefenseTower:1107`; `PooledProjectile:144`; `SupportFieldStructure:535`; `TowerCombat:624`; `Enemy:3292`; `AbilityVfxKit:250,253`; `StoryCompanion:617` |
| 3 | `Impact_Flame` | yes | `DefenseTower:1104`; `TowerCombat:622,625`; `Enemy:3290` |
| 4 | `Impact_Ice` | yes | `DefenseTower:1105`; `SupportFieldStructure:534`; `TowerCombat:619,623,626`; `Enemy:3291` |
| 5 | `Impact_Heal` | yes | `CaravanHealField:188,203`; `SupportFieldStructure:537`; `AbilityVfxKit:251`; `AegisSetEffect:126`; `HeroAbilities:2119`; `HeroHealth:616,747,1041,1222,1291`; `CompanionGearSetup:161`; `StoryCompanion:452`; `TroopController:546` |
| 6 | `Impact_ExplosionFire` | yes | `TowerCombat:618`; `DragonBoss:261`; `AbilityVfxKit:252` |
| 7 | `Impact_ExplosionAether` | yes | `SupportFieldStructure:536`; `TowerCombat:620`; `EliteVFXController:306`; `AbilityVfxKit:249`; `StoryCompanion:620` |
| 8 | `Impact_ShockwaveRing` | yes | `Enemy:1762,2076`; `PlayerAttackController:380`; `AbilityVfxKit:266,267`; `HeroHealth:735`; `StoryCompanion:555` |
| 9 | `Impact_ShardsBurst` | yes | **Router only** (`VFXManager` procedural fallback) |
| 10 | `Impact_SmokeWisps` | yes | **Router only** (`VFXManager` procedural fallback) |
| 11 | `Projectile_ArcaneBolt` | yes | `AbilityVfxKit:248`; also `SpellVfxFactory` routing |
| 12 | `Projectile_FrostBolt` | yes | **Router only** (`SpellVfxFactory`) |
| 13 | `Projectile_Arrow` | yes | `AbilityVfxKit:258,260`; also `SpellVfxFactory` routing |
| 14 | `Projectile_FlameArrow` | yes | `AbilityVfxKit:259`; also `SpellVfxFactory` routing |
| 15 | `Projectile_TowerArcane` | no | `DefenseTower:1118`; `TowerCombat:361` |
| 16 | `Projectile_TowerFire` | no | `DefenseTower:1116` |
| 17 | `Projectile_TowerIce` | no | `DefenseTower:1117` |
| 18 | `Projectile_EnemyCasterBolt` | yes | **ZERO runtime references** |
| 19 | `Cast_MageCharge` | yes | `DefenseTower:1091`; `TowerCombat:334,380`; also `SpellVfxFactory` routing |
| 20 | `Cast_FireCharge` | yes | **Router only** (`SpellVfxFactory`) |
| 21 | `Cast_KnightSlam` | yes | **Router only** (`SpellVfxFactory` / manager fallback) |
| 22 | `Cast_RangerDraw` | yes | **Router only** (`VFXManager` procedural fallback) |
| 23 | `Cast_Heal` | yes | `CastleDoorController:78`; `HeroAbilities:2102`; `TroopController:545`; also `SpellVfxFactory` routing |
| 24 | `Cast_FrostNova` | yes | **Router only** (`SpellVfxFactory` / manager fallback) |
| 25 | `Cast_NecromancerSummon` | yes | **Router only** (`VFXManager` procedural fallback) |
| 26 | `Cast_EnemyCaster` | yes | **Router only** (`VFXManager` procedural fallback) |
| 27 | `Death_Skeleton` | yes | `Enemy:3216` |
| 28 | `Death_Boss` | yes | `Enemy:3209` |
| 29 | `Death_Brute` | yes | `Enemy:3213` |
| 30 | `Death_Wolf` | yes | **Router only** (`VFXManager` procedural fallback) |
| 31 | `Death_Tiefling` | yes | **Router only** (`VFXManager` procedural fallback) |
| 32 | `Death_Generic` | yes | `Enemy:358,2897`; `HeroHealth:674`; `VfxPool:137` |
| 33 | `Aura_EnemyCaster` | yes | `SupportFieldStructure:524`; `Enemy:3258` |
| 34 | `Aura_Necromancer` | yes | `SupportFieldStructure:525`; `Enemy:3254` |
| 35 | `Aura_Healer` | yes | `SupportFieldStructure:526` |
| 36 | `Aura_Flame` | yes | `GearAuraMap:113` |
| 37 | `Aura_Ice` | yes | `SupportFieldStructure:523`; `GearAuraMap:122` |
| 38 | `Aura_Dust` | yes | **Router only** (`VFXManager` procedural fallback) |
| 39 | `Aura_SmokeReaper` | yes | `Enemy:3255` |
| 40 | `Aura_HeartPulse` | no | `HeartAuraController:337` (bridged through Hovl key) |
| 41 | `Aura_EmpowerTower` | no | **ZERO runtime references** |
| 42 | `Aura_TalentNode` | yes | **Router only** (`VFXManager` procedural fallback) |
| 43 | `Env_TorchFlame` | yes | `EnvironmentVFX:38` |
| 44 | `Env_LanternGlow` | yes | **ZERO runtime references** |
| 45 | `Env_GroundFog` | yes | **ZERO runtime references** |
| 46 | `Env_DungeonPortal` | no | `PortalVFXController:620` |
| 47 | `Env_TitleEmbers` | no | **ZERO runtime references** |
| 48 | `Env_DestructionDust` | yes | `EnvironmentVFX:50`; `StructureHitReaction:180` |
| 49 | `Env_DestructionSparks` | no | `EnvironmentVFX:52` |
| 50 | `Juice_CriticalHit` | yes | `PlayerAttackController:898` |
| 51 | `Juice_KillStreak` | no | **Router only** (`VFXManager` procedural fallback) |
| 52 | `Juice_WaveClear` | yes | `BattleArena:2785` |
| 53 | `Juice_LevelUp` | yes | `BattleArena:2770`; `LevelUpVFXController:73`; `EchoAutoDeployTrigger:416` |
| 54 | `Juice_GroundDecal_Flame` | yes | **Router only** (`VFXManager` procedural fallback) |
| 55 | `Juice_GroundDecal_Ice` | yes | **Router only** (`VFXManager` procedural fallback) |
| 56 | `Portal_Enter` | no | `PortalVFXController:810` |
| 57 | `Portal_Exit` | no | `PortalVFXController:827` |
| 58 | `WaveClear_Celebration` | yes | `BattleArena:2760`; `WaveCelebrationManager:67` |
| 59 | `LevelUp_Celebration` | yes | `CollectorStackView:399` |
| 60 | `Combo_Tier1` | yes | `KillComboTracker:137` |
| 61 | `Combo_Tier2` | yes | `KillComboTracker:119,128` |
| 62 | `Pet_Aura_Fire` | yes | **Router only** (`VFXManager` procedural fallback) |
| 63 | `Pet_Aura_Ice` | yes | **Router only** (`VFXManager` procedural fallback) |
| 64 | `Pet_Attack` | no | **Router only** (`VFXManager` procedural fallback) |
| 65 | `ShootingStar` | no | `WeatherManager:24` (serialized field/default owner) |
| 66 | `Death_EnemyExplosion_Dungeon` | no | **Router only** (`VFXManager` procedural fallback) |
| 67 | `Elite_Spawn` | no | `EliteVFXController:258` |
| 68 | `Elite_Death` | yes | `EliteVFXController:234`; `Enemy:3188,3212` |
| 69 | `Boss_Spawn` | yes | `DragonBoss:299`; `EliteVFXController:257` |
| 70 | `Boss_Death` | yes | `DragonBoss:284`; `EliteVFXController:233` |
| 71 | `Boss_AttackImpact` | yes | `DragonBoss:281,338`; `EliteVFXController:305` |
| 72 | `Boss_PhaseTransition` | yes | `DragonBoss:275` |
| 73 | `Boss_Telegraph` | no | `DragonBoss:278` |
| 74 | `Boss_Aura_Phase1` | no | `DragonBoss:308` |
| 75 | `Boss_Aura_Phase2` | no | `DragonBoss:311` |
| 76 | `Boss_Aura_Phase3` | yes | `DragonBoss:314` |
| 77 | `Boss_FireBreath` | yes | `DragonBoss:333` |
| 78 | `Env_Candle` | yes | `DungeonCandleVfx:37` |
| 79 | `Env_SteamVent` | yes | **ZERO runtime references** |
| 80 | `Env_SteamBurst` | yes | **ZERO runtime references** |
| 81 | `Cast_MuzzleFlash` | yes | `TowerCombat:376`; `RangedAttackVFX:297` |
| 82 | `Enemy_Spawn` | yes | `EliteVFXController:259` |
| 83 | `Despawn_Dissolve` | yes | **ZERO runtime references** |
| 84 | `Aura_LowHealth` | no | `HeroHpStateAura:378` |
| 85 | `Aura_NearDeath` | no | `HeroHpStateAura:377` |
| 86 | `Aura_HealingInProgress` | yes | `HeroHpStateAura:379` |
| 87 | `Aura_ItemHeal` | yes | `GearAuraMap:181` |
| 88 | `Harvest_Iron` | yes | `HarvestAura:456,471` |
| 89 | `Harvest_Wood` | yes | `HarvestAura:457,473` |
| 90 | `Harvest_Food` | yes | `HarvestAura:458,475` (frozen enum name; player resource is Stone) |
| 91 | `Harvest_Crystal` | yes | `HarvestAura:459,477` |
| 92 | `Harvest_Gold` | yes | **ZERO runtime references** |
| 93 | `Collector_Ready` | yes | `HarvestAura:417` |

## Phase-routing consequences

1. Investigate serialized ordinals 94/95 before any enum/catalog regeneration. An append-only enum cannot legitimately resolve those rows on this source snapshot.
2. The highest-confidence paid-art waste is the 24 `art=yes` rows marked **Router only** or **ZERO**. Start with the nine truly unreferenced keys; they cannot be reached through a dynamic enum return.
3. Do not equate `art=no` with invisible. Several keys intentionally bridge to Hovl string keys or procedural fallbacks (`Aura_HeartPulse`, portal effects). Phase work must inspect the route before authoring another row.
4. `Harvest_Food` is an internal append-only enum compatibility name. Rename its player-facing presentation to Stone, but do not insert/delete/renumber this enum member as part of the resource retirement.
