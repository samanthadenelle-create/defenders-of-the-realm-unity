# Village — `DeNelle.Village`

The main gameplay module (~275 files): village defense loop, hero, enemies,
waves, buildings, outer world. References **Core only** — never HUD directly
(use `CoreServices.Hud?.`).

## Root files

`VillageController` (scene orchestrator), `EconomyService`, `CrystalEconomy`,
`VisualFactory`, `VillageStrings`, `OnboardingIntegrator`, `WaveSystemBridgeBootstrap`,
`EventSystemEnsurer`, `UIInputModuleFix`, `CompanionMeetingTrigger`.

## Subfolders

| Folder | Contents |
|---|---|
| `Audio/` | Wave music, tower audio/voice, heartwood ambience, GameSfx |
| `BuildMode/` | Player build mode: controller, palette UI, placement grid, ghost preview, desktop + LeanTouch input |
| `Buildings/` (47) | `Building`, `Tower`/`TowerCombat`/`DefenseTower`, `BuildingCatalog`, `BuildTimerService`, upgrade panel, CrystalMine, doors/drawbridge, DungeonPortal, TechTree, projectile pool |
| `Camera/` | `CinemachineCameraController` |
| `Campaign/` | `CampaignManager` |
| `Catalog/` | `StructureFactory`, `CatalogBootstrap` |
| `Cinematics/` | `DragonCinematicFlyby` |
| `Combat/` | `FloatingHealthBar`, `ThreatSkullPlate` |
| `Crafting/` | Village crafting panel, recipes, `VillageInventory` |
| `Dungeon/` + `Dungeons/` | Portal VFX, scene bootstrap; `DungeonDef`, `DungeonEntrance` |
| `Enemies/` (19) | `Enemy`, `EnemyBrain`, `EnemyBehaviorTree`, `EnemyFactory`, `DragonBoss`, `TargetManager`, `NavPathCoordinator`, hit reactions, damage numbers |
| `Families/` | Monster family formations: `FamilyLeader`/`Member`, `FormationController` |
| `Gates/` | `Gate`, `GateProximityOpener` |
| `Harvest/` | Offline harvest accrual, `Worker`/`WorkerManager`, welcome-back popup |
| `Heart/` | `HeartController` (the Heart of Elarion), `GameOverScreen`, HUD bridge |
| `Hero/` (31) | `HeroLocomotion`, `HeroHealth`, `HeroAbilities` + ability catalog/input/VFX, gear, cameras (`SmartMobileCamera`, `VillageCamera`), `VirtualJoystick` |
| `Items/` | Consumables, loot tables, item drops, `ItemInventory`, `ItemHud` |
| `Monetization/` | `RewardedAdManager` |
| `NPCs/` | Townsfolk, story companions, `SylasFirstMeeting`, dialogue bubbles |
| `PatriciaLight/` (12) | Defend-the-Tower mode: `PatriciaLightController`, tower aim, FP/OTS cameras, breach choice |
| `Pets/` | Village-side pet hooks: `AuraController`, contextual behaviour, tower repair visuals |
| `Progression/` | `ProgressionManager`, `TierSystem`, level-up VFX, wave XP bridge |
| `Quests/` | `DailyQuestGateBridge` |
| `Talents/` | Hero talent catalog/modifiers, talent tree panel, Wisdom currency |
| `Tutorial/` | `TutorialDirector`, dialogue service, auto-walk, tutorial wave spawner |
| `Vfx/` (12) | `VFXManager`, `VFXCatalog`, pooling, `WeatherManager`, hit-stop, decals |
| `Walls/` | `WallSegment`, wall repair (controller, highlight, HUD bridge), `WallLayout` |
| `Waves/` (16) | `WaveManager`, `WaveData`, group spawner/coordinator, countdown UI, kill combo, celebrations, scaling curve, `AlertIntelSystem` |
| `World/` (29) | Outer world: camps (`CampSystem`, `ClaimableCamp`, `Outpost`), mine/crystal nodes, settlements, tribes, ward stones/tether, region mob spawner, navmesh installers, `WorldSceneLoader` |
| `Dev/` `Scripts/` | Empty |

> Maintenance: update this README when files are added/removed.
