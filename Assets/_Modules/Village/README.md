# Village — `DeNelle.Village`

The main gameplay module (~275 files): village defense loop, hero, enemies,
waves, buildings, outer world. References **Core only** — never HUD directly
(use `CoreServices.Hud?.`).

## Root files

`VillageController` (scene orchestrator), `EconomyService` (the Economy class — Grant is now the single faucet for pet harvest via MineNode and outpost trickle via Outpost; also exposes SecuredOutpostCount + TerritoryMultiplier for scaling), `CrystalEconomy`,
`VisualFactory`, `VillageStrings`, `OnboardingIntegrator`, `WaveSystemBridgeBootstrap`,
`EventSystemEnsurer`, `UIInputModuleFix`, `CompanionMeetingTrigger`.

## Subfolders

| Folder | Contents |
|---|---|
| `Audio/` | Wave music, tower audio/voice, heartwood ambience, GameSfx |
| `BuildMode/` | Player build mode: controller, palette UI, placement grid, ghost preview, desktop + LeanTouch input; **new modal preview window** (neutral plane + 3D object, 90°/free rotate buttons + drag, save yaw offset, place with it for wow UX). |
| `Buildings/` (47) | `Building`, `Tower`/`TowerCombat`/`DefenseTower`, `BuildingCatalog`, `BuildTimerService`, upgrade panel, CrystalMine, doors/drawbridge, DungeonPortal, TechTree, projectile pool |
| `Camera/` | `CinemachineCameraController` |
| `Campaign/` | `CampaignManager` |
| `Catalog/` | `StructureFactory`, `CatalogBootstrap` |
| `Cinematics/` | `DragonCinematicFlyby` |
| `Combat/` | `FloatingHealthBar`, `ThreatSkullPlate` |
| `Crafting/` | Village crafting panel, recipes, `VillageInventory` (WO-109: equipment recipes now consume via EconomyService, produce equippables for hero). Gear shop (ShopPanel) also feeds owned pieces into the same larder for equip. |
| `Dungeon/` + `Dungeons/` | Portal VFX, scene bootstrap; `DungeonDef`, `DungeonEntrance` |
| `Enemies/` (19) | `Enemy`, `EnemyBrain`, `EnemyBehaviorTree`, `EnemyFactory`, `DragonBoss`, `TargetManager`, `NavPathCoordinator`, hit reactions, damage numbers; **grouped by family (e.g. Orc Warband, Skeleton Legion, Troll) with Tank/DPS/Healer variety** + basic strategy (DPS focus-fire healers, Tank protect, etc.) via Brain + ActorAnimator for anims. |
| `Families/` | Monster family formations: `FamilyLeader`/`Member`, `FormationController` |
| `Gates/` | `Gate`, `GateProximityOpener` |
| `Harvest/` | Offline harvest accrual, `Worker`/`WorkerManager`, welcome-back popup; PetHarvestBootstrap (starter MineNodes + HarvestSites); integrated with EconomyService for pet + outpost yields. |
| `World/` | `MineNode` (+ visual + claim-as-HarvestSite), `HarvestSite` (claimed nodes with pet assignment, Economy.AddResource income, floating text, scaling, raid attraction), `ResourceGainPopup`, `NodeDiscoverySystem`, `Settlement`. |
| `World/Camps/` | `ClaimableCamp`, `Outpost`, `OutpostHub` (central hub + small defense grid + troop recruitment UI), `OutpostDefender` (Tank/DPS/Healer with Economy costs + upkeep), `CampSystem`, `CampGuards`, `CampDefenseWave` — clear→claim→build→recruit defenders. All income / costs go through EconomyService (the single source of truth). |
| `World/` | WO-108 Chunk 6: `AutoRampartDefense` (automated castle rampart turrets/wall emplacements for Last Bastion; scans + fires, Economy tie for future upkeep/empower). Full wiring in VillageSceneBuilder: ramparts/turrets/defenses after walls, Tree at (0,0,0), 4 districts with 2.8f scale + padding (no overlaps), stationed NPCs. Ramparts now 4m wide walkable platforms (NavMesh notes added). Epic cohesive Last Bastion geometry via builder only. |
| `Heart/` | `HeartController` (the Heart of Elarion), `GameOverScreen`, HUD bridge |
| `Hero/` (31) | `HeroLocomotion`, `HeroHealth`, `HeroAbilities` + ability catalog/input/VFX, gear (GearCatalog + GearLoadout + GearVisualApplier for defaults + shop equip visuals), cameras (`SmartMobileCamera`, `VillageCamera`), `VirtualJoystick`; **HeroBodySwapper + ActorAnimator drive** for correct Village/World anims (Idle/BattleReady, Move/Engage, class Attack/Cast, Hit, Death) using shared pipeline. ShopPanel + NPC "OpenShop" for basic buy/sell/equip (Economy + VillageInventory). |
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
