# MASTER CATALOG — Core (`Assets/_Modules/Core`)

Foundation layer. Two asmdefs live here: **`DeNelle.Core`** (root namespace `DeNelle.Core`, refs UniTask/TextMeshPro/Addressables/ResourceManager only — first-party nothing) and **`DeNelle.AI`** (`DeNelle.AI`, refs DeNelle.Core). Plus **`DeNelle.Core.Tests`** (Editor-only, refs DeNelle.Core + DeNelle.Data). Every other module references Core; Core references nothing first-party. Verified by reading every `.cs` (~120 files).

**Cross-module pattern:** Core defines interfaces; implementing modules register concrete services via `CoreServices` / per-feature static hooks, so Village↔HUD↔Wallet etc. never reference each other (CLAUDE.md §5). Reflection bridge used only in `PersistenceBridge` (Core→Village WaveManager).

---

## ROOT FILES

| Class | Path | Responsibility / key API | Bootstrap | Status |
|---|---|---|---|---|
| `CoreServices` (static) | `CoreServices.cs` | Cross-asmdef service registry. Slots: `Hud` (IVillageHud), `Audio` (IAudioService), `Jupiter` (IJupiterService), `WalletSigner` (IWalletSigner). `RegisterX/UnregisterX`; callers null-check. | — | LIVE |
| `HubScenes` (static) | `HubScenes.cs` | Single source of "is this a hub/town scene". `Names = {Village2, MainCastle_Hall, CastleHub, CastleHub_MainKeep}`; `IsHub(name)` (exact-or-Contains). Resolves WO-411 HUD-drift. | — | LIVE |
| `SceneRouter` (static) | `SceneRouter.cs` | React-Router port. Scene-name consts; `LoadScene`, `LoadSceneWithFade` (UniTask), `GoTitle/GoHeroSelect/GoPetSelect/GoVillage/GoCastle/GoDungeon/GoBattle/GoPatriciaLight`. `LoadVillageWithLoader` (async + VillageLoadOverlay). `BattleParams`/`PatriciaLightParams` hand-off; `ISceneFader`. **Village const = "Village2"; Castle = "MainCastle_Hall".** | — | LIVE (PatriciaLight paths DEAD — DTT removed, see FLAGS) |
| `Constants` (static) | `Constants.cs` | Solana AdminAddress/ProjectVaultAddress/Sol/Usdc literals + `TowerSlots = 9`. | — | LIVE; FLAG: wallet literals self-flagged as should-flow-from-data |
| `DevBootScene` (static) | `DevBootScene.cs` | `-bootScene <Scene>` CLI arg → load that scene, skip onboarding. Arg-gated no-op in normal play. | `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` | LIVE (QA tool) |
| `IntroLauncher` (static) | `IntroLauncher.cs` | Decoupling hook: `Action Play` set by DialogueUI, invoked by Title button. Onboarding↔DialogueUI seam. | — | LIVE |
| `SeekerBootstrap` (static) | `SeekerBootstrap.cs` | Frame-pacing + Seeker device detect → quality tier (Seeker_Low/High/Desktop) + targetFrameRate (30/60), vSync off. `ApplyTier`, `LooksLikeSeeker`. | `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` | LIVE (tiers depend on editor MobileSettings.cs) |
| `OnboardingMode` (static) | `OnboardingMode.cs` | FAST-PATH (default) vs FULL-TUTORIAL switch. PlayerPrefs-backed `FullTutorial`/`FastPath`; `ChooseFastPath/ChooseFullTutorial`. | — | LIVE |
| `DialogueEventBus` (static) | `Events/DialogueEventBus.cs` | Gameplay→Yarn signal bus. `Raise(name)`/`HasFired`/`Clear`/`ClearAll`; latches until cleared. Case-insensitive. | — | LIVE |
| `DialoguePortrait` (static) | `DialoguePortrait.cs` | `Forced` (Resources path) to override Yarn speaker portrait. | — | LIVE |
| `AwarenessState` (enum) | `AwarenessState.cs` | Unaware/Alerted/Engaged perception. Orthogonal to EnemyTacticalState. | — | data |
| `FormationType`/`FormationContext` (enums) | `FormationType.cs` | 5 dynamic pack shapes (LooseRing/Wedge/Line/TightPack/Column) + Roam/Engage/Flee context. Monster-family arch. | — | data |
| `ResourceType` (enum) | `ResourceType.cs` | Iron/Wood/Food/AetherCrystal — cross-asmdef mirror of Village.MineResource. | — | data; FLAG stale comment (see FLAGS) |

**Material/visual fixers (root):**

| Class | Path | Responsibility | Bootstrap |
|---|---|---|---|
| `TripoMaterialFixer` (MonoBehaviour) | `TripoMaterialFixer.cs` | Awake: rebuild Phong FBX materials → URP/Lit, carry texture (`_MainTex`/`_BaseMap`), optional Resources fallback tex. | per-object component |
| `TreeOfLifeMaterialFixer` (MonoBehaviour) | `TreeOfLifeMaterialFixer.cs` | Runtime grey-tree safety net for Village2 centrepiece (polyperfect SM_Tree_Round). Also located by SceneRouter to seat tree at (0,-0.25,0). Old name-based search was a no-op (corrected 2026-06-04). | per-object component |
| `EnvironmentTreeMaterialFixer` (MonoBehaviour) | `EnvironmentTreeMaterialFixer.cs` | WO-332 WebGL white-tree fix for polyperfect+KayKit trees (everything except Tree of Life). | per-object/scene |
| `GroundZFightFixer` (MonoBehaviour) | `GroundZFightFixer.cs` | WO-333 runtime fix: lower baked Village2 "Ground" plane Y=0→-0.05 to stop coplanar z-fight with OuterWorld terrain (no rebake). | scene-load |

---

## Combat/ (`DeNelle.Core.Combat`)

| Type | Path | Responsibility / key API | Notes |
|---|---|---|---|
| `IDamageableStructure` | `Combat/IDamageableStructure.cs` | `IsAlive`; `ApplyContactDamage(float)`. Impl: HeartController, HeroHealth, Building, Tower, Gate (Village). | interface |
| `IDamageable` (+ `CombatFaction`, `CombatLayer`, `ICombatLayered`, `IDamageTintable`, `DamageElement`, `StatusEffect`) | `Combat/IDamageable.cs` | Cross-module attack target: `Faction/WorldPosition/Hp/IsAlive`; `TakeDamage(amount,element)`, `ApplyStatus(effect,sec)`. Air/ground via ICombatLayered. Impl: Village.Enemy; consumed by Pets+hero. | interface bundle |
| `IActorAnimator` | `Combat/IActorAnimator.cs` | Verb-level anim driver: SetLocomotion/SetCombatStance/PlayAttack/PlayCast/PlayWindUp/SetBlocking/PlayHit/Die/Revive/PlayVictory/PlayTurn/PlayEmote. | interface |
| `ActorAnimator` (MonoBehaviour, `[DisallowMultipleComponent]`) | `Combat/ActorAnimator.cs` | Concrete IActorAnimator. Resolves child Animator, caches declared params (guards absent-param spam), re-scans on runtime body/controller swap. WO-218 upper-body layer; WO-217 `ShapeAttackTempo`. `InvalidateAnimator`. | LIVE |
| `AnimParams` (static) + `HitDirection`/`DeathDirection`/`TurnDirection`/`EmoteType` enums | `Combat/AnimParams.cs` | Canonical anim param names+hashes (Speed/InCombat/Attack/Combo/Cast/WindUp/Block/Hit/HitDir/Dead/DeathDir/Victory/TurnDir/Emote). Speed = RAW world u/s. | data |
| `DamageAttribution` (static) | `Combat/DamageAttribution.cs` | Per-target damage ledger for shared kill-XP. `Record/Drain/Forget/Clear`. Keyed by target object ref. | LIVE |
| `EnemyState` (enum) | `Combat/EnemyState.cs` | Idle/Chase/Attack/Hit/Dead. Drives Animator "State" int. | data |
| `IRangedThreat` | `Combat/IRangedThreat.cs` | Optional `IsRangedAttacker` marker (WO-128 pet anti-ranged). Integrator seam noted; not yet implemented on enemies. | interface (additive, dormant) |

---

## State/ (`DeNelle.Core.State`) — the save/persistence spine

| Class | Path | Responsibility / key API | Bootstrap | Status |
|---|---|---|---|---|
| `GameState` (ScriptableObject, sealed) | `State/GameState.cs` | Pure-data persisted store. 41 React `partialize` fields + many append-only (Zones/Tribes/Settlements/Wards/BaseLayout/PartyMemberIds/Arena/ArenaDefense/BuildJobs/Magic/GearInventory/PetName). `SchemaVersion = SaveSchema.CurrentVersion`. `[CreateAssetMenu]`. Asset at `State/GameState.asset`. `AetherCrystals` field DEPRECATED (folded into Resources.Crystals v18). `PartySize` derived. | — | LIVE; some fields in-memory-only (not yet in SaveSchema: Tribes/Wards/Arena). Settlements (v21) + PetName ARE round-tripped. |
| `GameStateService` (MonoBehaviour, sealed, singleton) | `State/GameStateService.cs` | Behaviour layer (Zustand analog). `Load()`/`Save()` (PlayerPrefs `dotr-save` → migrate → validate → apply). 11 per-domain UnityEvents. Typed mutators (AddCrystals/AddFood/RecordRun/BindWallet/ChooseHero/Set*/AdvanceTutorial/AddToParty/RemoveFromParty/FinishOnboarding). `ResetToNewGame` carve-out. Backend delta-sync (SyncToBackend/LoadFromBackend/SyncAfterWave/SaveBeforeSceneChange, offline queue, WO-121 wallet-signed auth). `EnsureZoneGraph`. Vercel URLs hard-coded (backend never deployed). | `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)] EnsureInstance` + GameStateBootstrap (BeforeSceneLoad) — DUAL bootstrap, both guarded | LIVE |
| `GameStateBootstrap` (static) | `State/GameStateBootstrap.cs` | Spawns GameStateService at startup if absent. | `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` | LIVE (note: overlaps GameStateService.EnsureInstance AfterSceneLoad — redundant but both guard on Instance) |
| `SaveSchema` (static) + `SaveFile`/`PersistedState`/`SaveValidationException`/`SaveValidationResult` | `State/SaveSchema.cs` | Save shape + validator (Zod port). `CurrentVersion = 33`, `FileFormat = 1`, key `dotr-save`. `PersistedState` = all-nullable ~60 fields ([JsonProperty] camelCase). `Validate` (NonNegInt/FiniteInt clamps, NaN/Inf reject). `JsonSettings` (StringEnumConverter + TutorialStepConverter). | — | LIVE; FLAG file-banner header still says v10 (see FLAGS) |
| `SaveMigrator` (static) + `SaveMigrationResult` | `State/SaveMigrator.cs` | Registry-based v1→v33 migration. Steps dict (2-10,14,17,18,21-33; 11-13,15,16,19,20 are additive-default-on-read, no step). `Migrate`/`MigrateForImport` (rejects newer-than-build). | — | LIVE; FLAG file-banner header still says "v1→v10 / nine-step" (stale) |
| `PersistenceBridge` (MonoBehaviour, sealed, `[DisallowMultipleComponent]`) | `State/PersistenceBridge.cs` | DDOL bridge: wave-clear→SyncAfterWave (reflection to Village.WaveManager.OnWaveCleared), scene-enter→LoadFromBackend, app-quit→Save. `_loadOnEnterScenes = {Village2, ATBBattle, PatriciaLight_TD}`. | `EnsureExists()` called by GameStateService.Awake | LIVE; FLAG: PatriciaLight_TD is a dead scene name (DTT removed) |
| `Enums` | `State/Enums.cs` | Difficulty/MovementStyle/BreachStyle/HeroClass/PetSpecies/TutorialStep — all `[EnumMember]` wire strings. TutorialStep `1..7|done`. | — | data |
| `NestedTypes` | `State/NestedTypes.cs` | PetData, ResourceBalance (struct, Starter{250,80,15}), PendingTowerBuild, AtbInventory, ChatContact, **ChatMessage**, LootStash, ActiveDungeonRun, SeedTree, DungeonProgress, QuestState, QuestProgress, RegionProgress. | — | data |
| `DifficultyTuning` (static) | `State/DifficultyTuning.cs` | Difficulty→between-wave countdown multiplier (Easy 2.0/Normal 1.0/Hard 0.6 off 300s base). `CountdownMultiplier/Label/Blurb`. | — | LIVE |
| `ServerConfig` (sealed) | `State/ServerConfig.cs` | Backend remote-config (boss drops, pack sales, events, empowerment cost, maintenance). All nullable + `Default` + accessor helpers + `IsEventActive`. | — | LIVE (never null; backend undeployed) |
| `BackendAuthConfig` (static) | `State/BackendAuthConfig.cs` | WO-121 master flag for wallet-signed save auth. `Enforced` = runtime `Override` ?? `BACKEND_AUTH_ENFORCED` define. Defaults OFF. | — | LIVE (off) |
| `HeroClassOpt` (enum) + `HeroClassOptExtensions` | `State/HeroClassOpt.cs` | Inspector-serializable `HeroClass?` wrapper (None sentinel). `ToNullable`/`ToOpt`. SO only — save uses real `HeroClass?`. | — | data |
| `SerializableDict<K,V>` | `State/SerializableDict.cs` | Unity-serializable Dictionary subclass via ISerializationCallbackReceiver parallel lists. | — | util |
| `TutorialStepConverter` | `State/TutorialStepConverter.cs` | Newtonsoft converter: Step1-7→ints, Done→"done"; reads either, >7→Done. | — | util |
| `ArenaProgress` (struct) | `State/ArenaProgress.cs` | Arena async-PvP W/L ledger (Wins/Losses/Streak/TotalPurse/`Empty`/`TotalRaids`). In-memory + PlayerPrefs (not in SaveSchema yet). | — | data |
| `BuildJobData` (struct) + `BuildJobType` enum | `State/BuildJobData.cs` | WO-172 CoC build timer (StructureId/JobType/StartMs/DurationMs/`FinishMs`/`Type`). unix-ms, offline-counting. | — | data |
| `PlacedStructureData` (struct) | `State/PlacedStructureData.cs` | WO-108 base-layout record (itemId/cellX/cellZ/yawSteps/level/yawOffset). Grid-relative, server-replayable. | — | data |
| `PlacedDefenderData` (struct) | `State/PlacedDefenderData.cs` | WO-389 Arena-defense twin of above (itemId/cellX/cellZ/yawSteps). | — | data |

---

## World/ (`DeNelle.Core.World`)

| Type | Path | Responsibility / key API | Notes |
|---|---|---|---|
| `ZoneManager` (static) | `World/ZoneManager.cs` | THE region classifier. `GetZone(pos)`/`ZoneAt`/`DangerTierAt`/`Depth`/`ThreatLevel` (depth×danger). Village half-extents 42/33. `DefaultZoneGraph` (5 ZoneStates), `DefaultDestination`. | LIVE |
| `RegionId`/`RegionZone`/`NodeType`/`ZoneState` | `World/RegionZone.cs` | Region enum (Village/Goldfields/Stoneback/Mirewood/Ashwood, append-never-renumber), static facts, City/Horde tag, persisted ZoneState (string RegionKey, Neighbors, Destination). | data |
| `RegionSpawnTable` (static) + `DepthBand`/`RegionEnemyEntry` | `World/RegionSpawnTable.cs` | WO-155 region→enemy roster, depth-banded weighted pick. `PickEnemyId/RosterFor/HasRoster/BandFor`. Mid 0.34/Core 0.67. Forward-design enemy ids. | data/logic |
| `GameClock` (static) | `World/GameClock.cs` | `CurrentDay()` from PlayerPrefs epoch (`dotr-gameclock-epoch`), 1 game-day = 1 real day. WO-159 razed lockout. Self-flagged as stopgap (no real day-system on branch). | LIVE (stopgap) |
| `CrystalGrade` (enum) + `CrystalRegion` (static) | `World/CrystalGrade.cs` | Aether/Verdant/Mire/Wraith grade; `TopGradeFor(dangerTier)`. Lean WO-144 slice (no ledger yet). | data |
| `WardStoneDef`/`WardStoneState`/`WardReach` | `World/WardContent.cs` | WO-112 ward-tether exploration. Reach math (`ReachForRegion`/`DistancePastReach`, BaseReach 12). In-memory only (not in SaveSchema). | data/logic |
| `TribeDef`/`TribeState`/`SettlementPhase`/`SettlementState`/`WorldPoint` | `World/WorldContent.cs` | WO-159/160 settlements + roaming tribes. JsonUtility-safe. In-memory only (GameState.Tribes/Settlements not in SaveSchema yet). | data |
| `GarrisonRecipe`/`GarrisonRecipeFile` | `World/GarrisonRecipe.cs` | Recipe-first garrison/dungeon data (id/kind/size/theme/enemies/levelRange/threat/props). Convenience accessors. | data |
| `GarrisonRecipeCatalog` (static) | `World/GarrisonRecipeCatalog.cs` | WebGL-safe loader for `garrison-recipes.json` via CanonicalJson. `All/Find/Reload`. | LIVE |

---

## Catalog/ (`DeNelle.Core.Catalog`) — build-system data model

| Type | Path | Responsibility | Notes |
|---|---|---|---|
| `CatalogEntry` + `CellPlacement` + `OrientationFix` | `Catalog/CatalogEntry.cs` | One catalog def: id/displayName/type/kind/visualPrefabPath (LOOK)/repo (BEHAVIOR)/composite/orientation. OrientationFix: per-axis scale, only manual=true applied. | data |
| `CatalogRegistry` (static) | `Catalog/CatalogRegistry.cs` | id+type registry. `Register/Get/OfType/Count/Clear`. Content modules register at startup. | LIVE |
| `CatalogType`/`EntryKind`/`NavSurfaceKind`/`PlacementSurface` | `Catalog/CatalogType.cs` | Taxonomy enums. | data |
| `PlacementRules` | `Catalog/PlacementRules.cs` | Declarative placement conditions (surface/overlap/footprint/gate-clearance/support/affordable/ownedGate). | data |
| `RepoProps` + `ResourceCost` | `Catalog/RepoProps.cs` | BEHAVIOR half: navSurface/buildCost/cost (multi-resource)/maxLevel/upgradeCost/upgradeVisualPath/behaviorId/singleton/placement/visualHeight + combat stats (range/damage/fireRate/canHitAir/element) + AoE (aoeRadius/slowSeconds/splashFraction). | data |
| `BuildTimerConfig` (ScriptableObject) + `BuildJobKind` | `Catalog/BuildTimerConfig.cs` | WO-172 timer tuning SO (hybrid duration curve, ad-skip, instant-finish, build slots). `DurationSecondsForTier/InstantFinishPrice/CreateDefault`. Resources path `Economy/BuildTimerConfig`. | data; code-default fallback |

---

## Data/ (`DeNelle.Core.Data`)

| Type | Path | Responsibility | Notes |
|---|---|---|---|
| `CanonicalJson` (static) | `Data/CanonicalJson.cs` | **WebGL-safe JSON reader** — Resources.Load<TextAsset> first, StreamingAssets File fallback. `Read(relativePath)`. The dual-copy contract hub. (namespace `DeNelle.Core`, not `.Data`) | LIVE core util |
| `DataInjector` (static) | `Data/DataInjector.cs` | Generic `Inject<T>`/`TryInject<T>` over CanonicalJson. (namespace `DeNelle.Core`) | LIVE |
| `BattlePassData` (SO) + `BattlePassReward`/`BattlePassRewardKind` | `Data/BattlePassData.cs`, `Data/BattlePassReward.cs` | DEF-69 season tracks (free+premium, cosmetics/currency only). Pure authoring. | data; runtime manager is follow-up |
| `CampaignData` (SO)/`CampaignProgressRecord`/`MissionData` (SO) | `Data/CampaignData.cs`, `Data/CampaignProgressRecord.cs`, `Data/MissionData.cs` | DEF-68 Spire Chronicles. Lean field set; progress in-memory only. | data |
| `PetType` (SO) | `Data/PetType.cs` | DEF-57 pet-species stub (name/moveSpeed/range/damage/cooldown). | data stub |
| `SkillType` (enum) + `SkillRequirement` | `Data/SkillTypes.cs` | LOCKED craft-skill enum (None/Blacksmith/Woodworking/Arcane/GatheringSpeed) + placement gate. | data |
| `SpecialAbility`/`EmpowermentAbility` (enums) | `Data/SpecialAbility.cs` | Per-upgrade passive + max-level empowerment (ManaSurge/GlacialCore/EternalEmber/TrueAim). | data |
| `TacticalData` (SO) + `EnemyArchetype` | `Data/TacticalData.cs` | DEF-72/WO-145 per-archetype tactical AI (flank/retreat/suppress/target-scoring/kiting/reposition). Archetypes Standard/Flanker/Siege/Flyer/Support/Boss/Kiter. | data |
| `TowerData` (SO) + `TowerTargets`/`TowerUpgrade`/`TowerEmpowermentData` | `Data/TowerData.cs` | DEF-73/74 tower type + 3-level upgrade chain + targeting matrix `CanTarget(targets,layer)`. No prefab field (visuals from upgrades). | data |
| `GarrisonRecipe`/`GarrisonRecipeCatalog` | (in World/, namespace `DeNelle.Core.World`) | see World section. | — |

---

## Progression/ (`DeNelle.Core.Progression`)

| Type | Path | Responsibility | Bootstrap |
|---|---|---|---|
| `SkillSystem` (MonoBehaviour, singleton) | `Progression/SkillSystem.cs` | Craft-skill levels (Blacksmith/Woodworking/Arcane/GatheringSpeed, start 0, 2 free points). `HasRequiredSkill/GrantPoints/SetLevel/GrantSkillPoint/SpendPoint/GetSkillLevel`. `OnSkillsChanged`. | `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)] Bootstrap` |
| `IXpEarner` | `Progression/IXpEarner.cs` | `EarnerId/WorldPosition/Level/AddXp`. Hero+pet shared kill-XP. | — |
| `XpEarnerRegistry` (static) | `Progression/XpEarnerRegistry.cs` | id→IXpEarner map. `Register/Unregister/TryGet`. | — |

---

## Quests/ (`DeNelle.Core.Quests`)

| Type | Path | Responsibility | Bootstrap |
|---|---|---|---|
| `DailyQuestService` (MonoBehaviour, singleton) + DTOs (`DailyQuestTemplate`/`DailyQuestSlotReward`/`DailyQuestCatalogData`/`DailyQuestInstance`/`DailyQuestSet`) + `DailyQuestCatalog` (static) | `Quests/DailyQuests.cs` | 3-slot daily quests (combat/exploration/wildcard). PlayerPrefs per-day. `Report/Reroll/ForceRollToday`, `QuestCompleted` event (Village reward bridge). Day-1 guaranteed build-towers quest. `FeatureShipped` gates several as NOT-shipped (harvesting/tower-build/cosmetic-shop/hero-talents). | `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` |
| `QuestService` (MonoBehaviour, singleton) | `Quests/QuestService.cs` | Story quests → GameState.Quests (syncs w/ save). `StartQuest/AdvanceQuest/CompleteQuest/GetStage/SetFlag/HasFlag/GiveKeystone`, `RewardEarned` event (Village grants). | `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` |
| `QuestCatalog` (static) + DTOs (`QuestReward`/`QuestStage`/`QuestDef`/`QuestCatalogData`) | `Quests/QuestCatalog.cs` | Story-quest loader (`quests.json` via CanonicalJson). `Quests/FindQuest/Stages/Reload`. | — |

---

## Services/ (`DeNelle.Core.Services`)

| Type | Path | Responsibility | Bootstrap | Status |
|---|---|---|---|---|
| `ClanService` (MonoBehaviour, singleton) + `ClanRole`/`ClanMember`/`ChatMessage`/`ClanState` | `Services/ClanService.cs` | Local-only clan + team-chat stub (PlayerPrefs). `CreateClan/LeaveClan/AddTemplatedMessage/AddCustomMessage`. Ring buffer 100, 140-char custom cap. | `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` | LIVE stub (no backend) |
| `ChatPhraseCatalog` (static) + DTOs | `Services/ChatPhraseCatalog.cs` | `chat-phrases.json` loader via CanonicalJson. `FindPhrase/TextFor/PhrasesByCategory`. | — | LIVE |
| `LeaderboardService` (MonoBehaviour, singleton) + `LeaderboardMetric`/`LeaderboardEntry`/`PlayerProfile`/`ILeaderboardSource`/`LocalStubLeaderboardSource` | `Services/LeaderboardService.cs` | Pluggable leaderboard; default `LocalStubLeaderboardSource` (real local profile from GameState + sample rivals, honest IsLocalStub). `FetchTopAsync/GetLocalProfile/SubmitLocalAsync/SetSource`. | `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` | LIVE stub (drop-in remote later) |

---

## Analytics / Promo / Referral

| Type | Path | Responsibility | Bootstrap | Status |
|---|---|---|---|---|
| `EventTracker` (MonoBehaviour, singleton, `[DisallowMultipleComponent]`) | `Analytics/EventTracker.cs` | Batched analytics → `/api/events/track`. Offline queue (PlayerPrefs, cap 200), retry w/ backoff, circuit breaker. `Track(name,props)` static. Fires session_start. | `EnsureExists()` from GameStateService.Awake | LIVE (backend undeployed → circuit opens harmlessly) |
| `PromoCodeService` (MonoBehaviour, singleton) + `PromoReward` | `Promo/PromoCodeService.cs` | Promo redemption → `/api/promo/redeem`. Local dedup (PlayerPrefs). Awards via GameState.Resources. `RedeemAsync`, `OnRedeemed/OnRedeemFailed`. | `EnsureExists()` | LIVE (needs live backend) |
| `PromoCodeUI` (MonoBehaviour, singleton) | `Promo/PromoCodeUI.cs` | **UI-Toolkit (UIDocument)** promo entry panel. `Open()`. | scene `_document` | FLAG: UXML/UIDocument doesn't render in player builds (PIPELINE §8) |
| `ReferralService` (MonoBehaviour, singleton) | `Referral/ReferralService.cs` | Referral generate/share/claim → `/api/referral/*`. X-share. Awards via `AddCrystals`. `EnsureCodeAsync/ShareOnX/ClaimAsync`. | `EnsureExists()` | LIVE (needs live backend) |
| `InviteFriendsUI` (MonoBehaviour, singleton) | `Referral/InviteFriendsUI.cs` | **UI-Toolkit (UIDocument)** referral panel. `Open()`. | scene `_document` | FLAG: same UXML-in-build risk |

---

## UI/ (`DeNelle.Core.UI`)

| Type | Path | Responsibility | Notes |
|---|---|---|---|
| `ElarionUi` (static) | `UI/ElarionUi.cs` | THE in-game UI palette + UI-Toolkit inline-style helpers + swappable panel/menu bg hook (Resources/UI/panel_bg, menu_bg). Shared by HUD(uGUI)+Village(UIElements). | LIVE theme |
| `ElarionUiKit` (static) | `UI/ElarionUiKit.cs` | Code-built uGUI coherence kit (modal/panel/frame builders, WebGL-safe rounded sprite). Additive — older surfaces unchanged. | LIVE |
| `PanelManager` (static) + `PanelHandle` | `UI/PanelManager.cs` | DEF-212 one-modal-at-a-time arbiter. `NotifyOpened/NotifyClosed/AnyOpen`. Pure static, no scene object. | LIVE |
| `PanelRouter` (static) + `PanelId` | `UI/PanelRouter.cs` | DEF-213 reflection-free named-panel open registry (Village opens HUD panels). `Open(PanelId)`. Replaces fragile reflection. | LIVE |
| `AddressableUIManager` (MonoBehaviour, singleton) | `UI/AddressableUIManager.cs` | Async UI prefab load/unload via Addressables (UI-Core/Debug/Menus/Tower labels). `ShowAsync/Hide`. | LIVE (needs Addressables groups authored) |
| `PortraitLockOverlay` (MonoBehaviour) | `UI/PortraitLockOverlay.cs` | "Rotate to portrait" landscape gate. Code-built uGUI, max sortingOrder. | `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` self-bootstrap |
| `RpgUiCatalog` (static) | `UI/RpgUiCatalog.cs` | WebGL-safe RPG-UI sprite-pack accessor (Resources/RpgUi/<role>). Sprite-or-null contract. Role/name consts (RolePanel/RoleSlot/RoleIcons…, PanelWindowDark/SlotItem/ButtonGold…). | LIVE (sprites mirrored in by `BlinkUiImporter`; null-safe until present) |
| `ConceptIconResolver` (static) | `UI/ConceptIconResolver.cs` | Data-driven icon resolver: concept id → sprite via `Resources/Data/Canonical/concept-icons.json`. `Resolve`/`ResolveAny`/`ResolveOverride`. Sprite-or-null (caller keeps glyph fallback). | LIVE |
| `ShopTheme` (static) | `UI/ShopTheme.cs` | WO-175 shared shop palette + UI-Toolkit helpers (CosmeticShop + PackStore). NOTE: a DUPLICATE of `ElarionUi`'s palette (aliases the same colours) — slated to fold into the `UiStyle` authority (Obsidian spec §6.1). | LIVE theme |
| `IPanelView` / `IPanelViewModel` (interfaces) | `UI/Mvvm/IPanelView.cs`, `UI/Mvvm/IPanelViewModel.cs` | The strict-MVVM panel seam. View is a dumb skin (Bind/Unbind, renders from VM, routes input as commands); VM is View-agnostic (no UnityEngine UI types, `Title`/`Changed`/`Close`/`Dispose`, unit-testable). SAME VM binds our ElarionUiKit panel OR a Blink prefab. Implemented by `HeroSkillTreePanelMvvm`+`HeroSkillTreeVM`, `InventoryVM`, etc. (in `DeNelle.Village`). | LIVE (MVVM seam — supersedes the older HUD event-bridge+reflection pattern for new panels) |
| `VillageLoadOverlay` (MonoBehaviour, sealed) | `UI/VillageLoadOverlay.cs` | Code-built uGUI village loading screen (spinner/progress/lore). `Show/HideAndDestroy/SetProgress`. Driven by SceneRouter. | LIVE |

---

## VFX / Debug / Diagnostics / Validation / Addressables / Scripts/AI

| Type | Path | Responsibility | Bootstrap | Notes |
|---|---|---|---|---|
| `Hud` (static) | `VFX/Hud.cs` | Reusable "draw attention" API. `Focus(target)/Unfocus`. (namespace `DeNelle.Core`) | — | LIVE |
| `AttentionGlow` (MonoBehaviour, sealed, `[RequireComponent(LineRenderer)]`) | `VFX/AttentionGlow.cs` | Scrolling-glow square frame around a target. `Attach`. Driven by Hud only. | — | LIVE |
| `DebugCanvasUI` (MonoBehaviour, sealed) | `Debug/DebugCanvasUI.cs` | F12 playtest overlay (wallet bind/sync/state). **namespace `DeNelle.Core.DevOverlay`** (NOT `.Debug`). | scene-wired | Editor/dev only |
| `BreakCaptureHarness` (MonoBehaviour, sealed) | `Diagnostics/BreakCaptureHarness.cs` | Always-on flight recorder: errors/softlocks/scene-transitions → break-log.jsonl + screenshots + EventTracker. Reentrancy-guarded. `using Debug = UnityEngine.Debug`. | `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)] Install` | LIVE (zero-setup) |
| `OrientationValidator` (static) | `Validation/OrientationValidator.cs` | WO-363 pure facing-matches-input rule (XZ, 30° tolerance). | — | logic |
| `OrientationGuard` (MonoBehaviour, sealed, `[DisallowMultipleComponent]`) | `Validation/OrientationGuard.cs` | Opt-in per-character runtime guard (logs GATE-FAILED). Inert unless `_enabled` + `ORIENTATION_GATE` define. | per-object | dormant by default |
| `AddressablesGroupConfigBase` (+concrete) (SO) | `Addressables/AddressablesGroupConfig.cs` | Typed AssetReference registry SOs. **namespace `DeNelle.Core.AssetDelivery`** (NOT `.Addressables`). | — | data |
| `AddressablesMemoryProfiler` (MonoBehaviour, sealed) | `Addressables/AddressablesMemoryProfiler.cs` | Handle-leak tracker (TrackHandle/UntrackHandle/HandleCount/GetReport). namespace `DeNelle.Core.AssetDelivery`. | scene | dev tool |
| `SkinController` (MonoBehaviour) | `Addressables/SkinController.cs` | Async Addressables skin loader per slot (Material/Texture/GameObject/Mesh). `ApplySkinAsync/RemoveSkin`. namespace `DeNelle.Core.AssetDelivery`. | per-object | LIVE |
| `BTNode`/`Selector`/`Sequence`/`ActionNode`/`Condition` | `Scripts/AI/*.cs` | DEF-43 lightweight behaviour-tree primitives. **Assembly `DeNelle.AI`, namespace `DeNelle.AI`** (separate asmdef). | — | logic |

---

## Web3/ (`DeNelle.Core.Web3`)

| Type | Path | Responsibility | Notes |
|---|---|---|---|
| `IJupiterService` + `SwapQuote`/`SwapInputToken` | `Web3/IJupiterService.cs` | Jupiter swap-panel contract. `OpenSwapPanel/CloseSwapPanel/GetQuoteAsync`. Impl: Web3.JupiterSwapService. Devnet-gated, no real swap signed. | interface |
| `IWalletSigner` | `Web3/IWalletSigner.cs` | WO-121 ed25519 message signer. `CanSign/WalletAddress/SignMessageBase58`. Impl: Wallet.WalletService. Stub can't sign. | interface |

---

## Audio / HUD interfaces

| Type | Path | Responsibility | Notes |
|---|---|---|---|
| `IAudioService` | `Audio/IAudioService.cs` | `PlaySfx/PlayMusic/PlayUiClick`. Impl: Audio.AudioService via CoreServices.Audio. | interface |
| `MusicTrack` (enum) | `Audio/MusicTrack.cs` | Village/Battle/Victory/Dungeon/Overworld/Defeat/Title/Arena (append-only indices). | data |
| `IVillageHud` | `HUD/IVillageHud.cs` | Passive HUD contract: SetWave/SetCountdown/SetHeartHp/SetCrystals/SetResources/SetAttackDirections/SetWaveImminent/ShowWaveClearBanner/ShowRepairPrompt/SetForgettingLevel/SetWardsReadout. Impl: VillageHudController via CoreServices.Hud. | interface |

---

## Tests/ (`DeNelle.Core.Tests`, Editor-only)

`SaveLoadRoundTripTest` (round-trip + simulated restart), `ResetCarveOutTest` (ResetToNewGame preserves wallet/breachStyle/social), `SaveMigratorTest` (migration step chain + version gate — note: header says v1→v10, schema is now v33), `SaveSchemaValidateTest` (NaN/Inf reject + clamps), `TestSupport` (SpawnService/ClearSave/MakeRichState/WriteSaveFile via reflection). asmdef refs DeNelle.Core + **DeNelle.Data** (note: not DeNelle.Core.Data namespace — a separate `DeNelle.Data` asmdef).

---

## DATA / JSON (loaded by Core, dual-copy contract)

Core loaders read canonical JSON via `CanonicalJson.Read` (Resources/Data/Canonical/*.json wins, StreamingAssets/Data/Canonical/*.json fallback — **keep both in sync**):
- `quests.json` (QuestCatalog) — story quest defs {version, quests[]: id/title/stages[]}
- `daily-quests.json` (DailyQuestCatalog) — {version, slotCount, reroll knobs, slots[], templates[]}
- `chat-phrases.json` (ChatPhraseCatalog) — {version, categories[], phrases[]}
- `garrison-recipes.json` (GarrisonRecipeCatalog) — {recipes[]: id/kind/size/theme/enemies/levelRange/...}
- `themes.json` (Theme) — {default:"midnight-luxe", themes:{key→ {name/radius/font/colors HSL}}}
- generic tables via `DataInjector.Inject<T>(path)` (weapons/waves/etc. — owned by other modules)

PlayerPrefs keys owned/read by Core: `dotr-save` (GameState), `dotr-sync-queue` (backend delta), `dotr-event-queue` (analytics), `dotr-daily-quests-v1` + `dotr-daily-quests-day1-done-v1`, `dotr-clans-v1` + `dotr-account-id-v1`, `dotr-redeemed-promos`, `dotr-referral-*`, `dotr-gameclock-epoch`, `realm-defenders-settings` (legacy, read+deleted by migrator v9), `onboarding.fullTutorial`.

---

## FLAGS

### Stale comment vs. code
1. **`SaveSchema.cs` header** (lines 11-13) says "CurrentVersion = 10, FileFormat = 1" — actual `CurrentVersion = 33` (FileFormat = 1 is still correct). The inline doc on the const IS up to date; the file banner is stale. (Also `SaveSchema.cs:209` calls PersistedState "the 41-field persisted payload" — now ~60 fields.)
2. **`SaveMigrator.cs` header** describes "the migration chain v1 → v10 (nine stacked-if)" and "nine-step" — the chain now runs to **v33** (Steps 2-10,14,17,18,21-33 + several additive-default-on-read versions). Banner stale; the Steps dict + step XML are current.
3. **`Theme.cs` header** (lines 13-19) says it loads via `Application.streamingAssetsPath` and that "spec Part 3 forbids Resources.Load" — the actual `EnsureLoaded()` (line 187) loads via `CanonicalJson.Read` (Resources-first, WebGL-safe). The header documents the OLD, abandoned loading approach.
4. **`ResourceType.cs` header** maps `ResourceType.AetherCrystal → GameState.AetherCrystals` and `Food → GameState.Resources.Food` — but `GameState.AetherCrystals` is **DEPRECATED** (folded into `Resources.Crystals` as of save v18). The mapping comment points at a retired field.
5. **`SaveMigratorTest` header** ("v1→v2 ... v9->v10", "nine-step") — stale like #2; the real chain now runs to v33 (test still validates the real chain but the doc count is wrong).

### Folder ≠ namespace (intentional, per memory `core-namespace-shadows-unityengine-statics` — flagged so a reader isn't surprised, NOT a bug)
- `Debug/DebugCanvasUI.cs` → namespace `DeNelle.Core.DevOverlay` (avoids a `DeNelle.Core.Debug` namespace shadowing `UnityEngine.Debug`).
- `Addressables/*` (AddressablesGroupConfig/MemoryProfiler/SkinController) → namespace `DeNelle.Core.AssetDelivery` (avoids shadowing `UnityEngine.Addressables`).
- `Data/CanonicalJson.cs` + `Data/DataInjector.cs` → namespace `DeNelle.Core` (not `.Data`).
- `World/GarrisonRecipe*.cs` + `Data/...` split: garrison types live in `DeNelle.Core.World`, not `.Data`.

### Dead / stale scene references (DTT/PatriciaLight removed 2026-06-09 per PIPELINE_STATE)
- `SceneRouter.PatriciaLight` const + `GoPatriciaLight` + `PendingPatriciaLight` + `PatriciaLightParams` — the "Defend the Tower" scene is **REMOVED**; these routing paths are DEAD (no live caller / scene not in build).
- `PersistenceBridge._loadOnEnterScenes` includes `"PatriciaLight_TD"` — dead scene name (also note the name disagrees with SceneRouter's `"PatriciaLightMode"` const — they never matched).
- `SceneRouter` dungeon consts (DungeonHealersCottage..DungeonApothecarysVault) — only HealersCottage ships; others are stubs (dungeon-stub-builder pattern).

### Redundant / duplicated
- **Dual GameStateService bootstrap:** `GameStateBootstrap` (BeforeSceneLoad) AND `GameStateService.EnsureInstance` (AfterSceneLoad) both auto-spawn the singleton. Both guard on `Instance`, so harmless, but two mechanisms do one job.
- **Two `ChatMessage` types:** `DeNelle.Core.State.ChatMessage` (NestedTypes, 1:1 mailbox) and `DeNelle.Core.Services.ChatMessage` (ClanService team-chat) — different namespaces, both valid, easy to confuse.
- `AetherCrystals` exists in 3 places (GameState field, PersistedState `aetherCrystals`, SyncDelta) all DEPRECATED/zeroed post-v18 but retained for save back-compat (deliberate, documented).

### Scene-gated / feature-flagged OFF
- `DailyQuestService.FeatureShipped` returns **false** for harvesting / tower-build / cosmetic-shop / hero-talents — daily-quest templates requiring those features are filtered OUT (the features exist elsewhere but the quest gate treats them as un-shipped; stale gate vs. actual feature state worth a designer check).
- `BackendAuthConfig.Enforced` defaults OFF (wallet-signed save auth dormant until backend deploys + real signer lands).
- `OrientationGuard` inert unless `_enabled` + `ORIENTATION_GATE` define.

### Backend-dependent (never deployed — see memory `backend-never-connected`)
- `GameStateService` delta-sync/LoadFromBackend, `EventTracker`, `PromoCodeService`, `ReferralService`, `LeaderboardService` all target `https://defenders-of-the-realm-v2.vercel.app` — the backend was **never deployed**. These run resilient (local-save-only, circuit-breaker, honest stub sources); they are NOT live bugs, they are pre-deploy stubs. `ClanService` is a pure local PlayerPrefs stub by design.

### UXML-in-build risk (per memory `uxml-uidocuments-dont-render-in-builds`)
- `PromoCodeUI` and `InviteFriendsUI` are **UI-Toolkit (UIDocument)** — these come up empty in player builds. Core's newer UI (ElarionUiKit / VillageLoadOverlay / PortraitLockOverlay) deliberately uses code-built uGUI to avoid this; the two promo/referral panels predate that discipline.

### Append-only fields not yet round-tripped through SaveSchema (documented, in-memory + PlayerPrefs-snapshot only)
`GameState.Tribes`, `Wards`, `Arena` — live correctly in-memory within a session but the schema round-trip + version bump is an explicitly-deferred save-owner follow-up. `Settlements` (v21) and `PetName` ARE now wired (SaveSchema PersistedState field + Snapshot + ApplyPersisted), as are `Zones`/`BaseLayout`/`PartyMemberIds`/`ArenaDefense` (v17/v14/v16/v19). Only Tribes/Wards/Arena remain genuinely unpersisted.
