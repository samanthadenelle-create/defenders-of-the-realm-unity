# FULL-COVERAGE PLAN — step-in/step-out + DataRegression across every architecture area

**Status:** LIVE build sheet (2026-07-08). Owner directive: "team in each architecture area … make
sure we have full coverage." Produced by the `coverage-map-all-areas` workflow (16 area SMEs + synthesis,
verified from code). Executed wave-by-wave by the orchestrator: each team is edit-only on disjoint files;
the orchestrator batch-gates the combined tree once per wave (`COMPILE_GATE_OK`) and commits each team by
explicit path. All new oracle `.cs` land in `Assets/Editor/Regression/` (distinct filenames, never collide)
and register into `DataRegression.RunAll` (orchestrator owns that edit).

**Baseline coverage at start:** 38/103 critical flows instrumented, 21/112 data-invariants oracled.
**Un-oracled areas (0 coverage):** npcs, hud, battle-atb, scenes, devtools, misc, editor-tools.

**Pilot already landed (commit `c2aa8337`):** F8-41 enemy aggro+cast, F8-39 tower death/respawn, F8-15
death UI, F8-37 arena, overworld coverage — + 3 oracles (TowerRespawn, DefenseTargetable, ArenaPrefabAudit).
Those roots are PROVEN by `RunAll` (REGRESSION_FAIL=3 = open bugs correctly detected).

---

## 1. COVERAGE MATRIX (at baseline)

| # | Area | Assembly silo | Flows instr. | Invariants oracled | Worst gap |
|---|------|---------------|:---:|:---:|---|
| 1 | core | Core / AI | 2/7 | 5/13 | CanonicalJson.Read (data hub) silent + un-oracled |
| 2 | village-hero | Village (Hero/) | 4/7 | 4/7 | cast→projectile→damage-LAND silent, bare `onArrive()` |
| 3 | village-systems | Village (Buildings/Harvest/Arena/Tutorial) | 1/6 | 2/8 | TryUpgrade spend across Wood/Iron dual-wallet |
| 4 | village-npcs | Village (NPCs/) | 2/6 | 0/6 | party-join→HUD render, 3-assembly silent |
| 5 | village-enemies-world | Village (Enemies/Waves/World) + Core.World | 8/9 | 2/10 | EnemyBrain targeting precedence silent |
| 6 | hud | HUD / HUD.Kit | 4/6 | 0/3 | model→widget fill-commit value untraced |
| 7 | battle-atb | BattleATB | 2/7 | 0/6 | BuildSetup→roster→hero-class chain silent |
| 8 | dialogue | Core.Dialogue + Village | 1/5 | 2/5 | DialogueRunner silent early-termination |
| 9 | audio | Audio | 2/6 | 1/5 | PlayMusic→ClipFor→Crossfade silent (dead scenes) |
| 10 | economy-meta | Pets/Wallet/Cosmetics/Web3 | 1/7 | 1/9 | Glimmer debit-without-grant, no trace/oracle |
| 11 | data-catalogs | Core.Data + Village + Pets/Cosmetics | 2/4 | 1/7 | deserialize parse-to-empty mapping break |
| 12 | scenes | Core (SceneRouter) + Village.World | 2/7 | 0/5 | battle return-point restore silent |
| 13 | devtools-settings-onboarding | Settings/Onboarding/DevTools | 2/6 | 0/8 | whole Settings assembly zero trace/oracle |
| 14 | misc-modules | Dungeons + Environment/Data/UI | 1/6 | 0/6 | dungeon↔ATB encounter handoff silent |
| 15 | editor-tools | Editor | 1/6 | 0/5 | EditorBuildSettings scene integrity un-oracled |
| 16 | resources-art | Village/Core/DialogueUI art-load | 3/8 | 3/9 | HeroPortraits resolve broken + silent |

---

## 2. GAP LIST — TIER A (high risk: money / data-root / player-felt, silent AND un-oracled)

**Uninstrumented critical flows (26):**
1. core — `CanonicalJson.Read`/`DataInjector.Inject` — dual-copy JSON hub; Resources-miss→StreamingAssets-miss returns null, zero log (pink-floor at the data root).
2. economy — `GlimmerCurrencyService.TryPurchase/SpendGlimmer/TryAddGlimmer/Equip` — silent debit-without-grant = player pays, gets nothing.
3. economy — `CryptoPaymentManager`→reflection→`TryAddGlimmer` grant landing (money taken, reflected method null).
4. economy — `BattlePassManager.AddXP/PurchasePremiumPass` (2400-Glimmer + back-date, partial-grant on mid-fail).
5. economy — `PetAcquisitionService.Acquire` (species miss, slot not persisted).
6. village-systems — `ResourceBuildingState.TryUpgrade` spend across Wood/Iron dual-wallet.
7. village-systems — `OfflineHarvestService.ClaimAccrual` (clock/cap/double-grant).
8. village-systems — `ArenaMode.TryStartRaid` wallet debit/credit/refund.
9. village-hero — cast→`RangedAttackVFX.FireArrow/Orb`→`ProjectileMover.onArrive` damage-LAND (bare invoke, runtime-verified).
10. village-hero — `HeroProgression.AddXp/ApplyLevelRewards` (swallowing empty catch on Wisdom grant — banned).
11. village-npcs — party-join→`AddToParty`→injector→`PartyHudBridge` (unguarded reflection, 3-assembly silent).
12. village-enemies-world — `EnemyBrain.Update` precedence + `Enemy.DriveNav` authority (two aggro authorities).
13. hud — `HudKitController.OnVitals/OnEconomy/...` fill-commit VALUE (the HP 9/145→6% felt bug).
14. battle-atb — `BuildSetup→BuildEnemyRoster→MapToEngineDef→ResolveHeroClass` (silent Cleric→Mage alias, unknown-id→skeleton).
15. dialogue — `DialogueRunner.EnterNode/PostLines/Choose/End` (condition-gated node ends convo, zero trace).
16. audio — `AudioService.PlayMusic→ClipFor→CrossfadeTo` (missing MP3 → silent scene).
17. data-catalogs — per-catalog `LoadJson`/`DataInjector.TryInject` deserialize (wrong-key → empty, success, zero log).
18. scenes — `SceneRouter.GoBattle→StashReturnPoint→OnReturnSceneLoaded→WarpTo` (double-subscribe strands hero).
19. scenes — `LoadSceneWithFade` onboarding→home (unregistered-scene silent abort).
20. devtools — Settings apply/persist chain (whole `DeNelle.Settings` zero trace; UXML-only, feeds DifficultyTuning).
21. devtools — `OnboardingIntegrator.Wire()` reflection bridge (dropped seam = dead tutorial, silent).
22. misc — `DungeonRuntimeState.BeginEncounterHandoff/ResumeAfterEncounter` (BUG-008 softlock).
23. editor — Player build (`WebGLBuild/DesktopBuild/AndroidBuild`) — build death is raw `Exit(1)` no breadcrumb.
24. editor — Scene builder realize + Animator factory (primitive-fallback / renamed anim param, silent).
25. resources-art — `PortraitCache.Get("HeroPortraits/…")` — folder ABSENT, bare `return null` (blank hero portraits).
26. resources-art — `RpgUiCatalog.Get` code-built-UI atlas (missing role folder blanks every uGUI panel).

**Un-oracled decidable invariants (high):** CanonicalJson dual-copy sync + version-triple coherence
(SaveSchema==GameState==migrator); Resources↔StreamingAssets byte-parity (26 shared) + 6 StreamingAssets-only
WebGL-broken files; ATB engine determinism + MapToEngineDef roster key; Glimmer purchase round-trip;
HUD hud-areas.json never-blank + PostureEvaluator precedence; **Aegis-set reachability** (all 4 aegis weapons
lack setId → ward unreachable — known bug); crystal single-source-of-truth (3-store); every route const in
Build Settings + Castle flag-resolution both MergedWorld states; EditorBuildSettings scene-list integrity;
**HeroPortraits hard-fail**.

**TIER B / C** (medium/low): see the workflow output — CatalogRegistry, Quest, World-zone, combo-rhythm,
worker-dispatch, tutorial-FTUE, talk-prompt, compass provider, turn-resolution, jukebox, pet-harvest,
dungeon-entry, difficulty/pause, crafting chain, navmesh-bake, etc.

---

## 3. WAVE PLAN (collision-free)

**Shared-file single-owner reconciliation (removes ALL inter-team collisions):**
- `SceneRouter.cs` → **Scenes team** (core's Steps folded in).
- `CanonicalJson, DataInjector, GarrisonRecipeCatalog, QuestCatalog, DailyQuests, QuestService` → **Core-Hub**; Catalogs excludes them.
- `TalkPromptRegistry, TalkHudBridge` → **NPCs**; Dialogue defers.

After this, all 16 teams are mutually file-disjoint (verified).

### WAVE 1 — highest-risk, closes every Tier-A gap (8 teams, disjoint)

| Team | Instrument (files) | New oracles |
|---|---|---|
| **Core-Hub** | CanonicalJson, DataInjector, CatalogRegistry, CoreServices, GarrisonRecipeCatalog, QuestCatalog, DailyQuests, QuestService, ZoneManager, RegionSpawnTable | CoreDataHubRegression, CoreCatalogRegression, CoreWorldLogicRegression, CoreSaveContractRegression |
| **Village-Hero** | HeroProgression, RangedAttackVFX, ProjectileMover, HeroHitReaction, AttackTimingBonus, AegisSetEffect, HeroLinkCrossing, HeroControlEnsurer | HeroProgressionRegression, AegisSetReachabilityRegression |
| **Village-Systems** | ResourceBuildingState/Progression, TechTree, OfflineHarvestService, WorkerManager, Worker, ArenaMode, ArenaWalletService, ArenaProgressStore, ArenaHeraldSpawner, ArenaDefenseSetupController, ArenaAttackRecruitController, TutorialDirector, CompanionSpawner, TutorialWaveSpawner, CrystalEconomy, GhostPreview, PlacementGrid, BuildFeedbackToast | BuildingUpgradeRegression, OfflineHarvestRegression, VillageEconomyRegression, ArenaCatalogRegression |
| **Village-NPCs** | PartyHudBridge, TalkHudBridge, TalkPromptRegistry, ElaraWaveThreeJoin, SylasFirstMeeting, StoryCompanion, GearOfferChoiceUI, GearGrantToast, CastleCompanionIntroducerInjector, CastleVendorNpcInjector | CompanionRosterRegression, TownsfolkDialogueRegression |
| **Battle-ATB** | BattleController, ATBRuntimeState, ATBCombatManager, BattleHudUgui | AtbEngineRegression |
| **Economy-Meta** | GlimmerCurrencyService, BattlePassManager, PetAcquisitionService, PetHarvester, PetProgression, JupiterSwapService, WalletBridgeStub | EconomyMetaCatalogRegression, GlimmerEconomyRegression |
| **Scenes** | SceneRouter, DungeonEntrance, WorldSceneLoader | SceneRoutingRegression |
| **Resources-Art** | PortraitCache, RpgUiCatalog, ItemIconCatalog, ProjectileVFXCatalog, ProjectileArtCatalog | ArtResourceRegression |

### WAVE 2 — medium/low + un-oracled backfill (8 teams, disjoint)

| Team | Instrument (files) | New oracles |
|---|---|---|
| **Village-Enemies-World** | EnemyBrain, EnemyBehaviorTree | EnemyRosterRegression, WorldSystemsRegression |
| **HUD** | HudKitController, HudMoveInput | HudAreasOracle, HudPostureOracle |
| **Dialogue** | DialogueRunner, DialogueViewModel, DialogueCommandSink | DialogueGraphRegression |
| **Audio** | AudioService, WebGLAudioUnlock, MusicSelectionPanel, AudioBootstrap | AudioRegression |
| **Catalogs** | GearCatalog, BuildingCatalog, AbilityCatalog, WaveData, PetCatalog, PetSkillTreeCatalog, CosmeticCatalog, HeroTalentCatalog, ConsumableCatalog, ConsumableCraftingCatalog, MaterialCatalog, GearCraftingRecipeCatalog, CraftingRecipeCatalog, JewelerRecipeCatalog, Theme, ChatPhraseCatalog, CanonStrings, IntroPetCatalog, LoreFragments, DungeonLayout, VillageStrings, CatalogBootstrap, WalletRegistry | CatalogSyncRegression, CatalogMappingRegression, CatalogIntegrityResourcesRegression |
| **DevTools-Settings-Onboarding** | SettingsController/Model/Bootstrap, AudioMixerBridge, PauseController, MusicToggleBootstrap, OnboardingIntegrator, OnboardingMode, DifficultyTuning, DevBootstrap, DevWalletProbe | SettingsRegression |
| **Misc-Modules** | DungeonRuntimeState, DungeonInventory, CraftingPedestal, IngredientPickup, CraftableShopProvider, CraftingPanelController, DungeonHudController, DungeonHero, Lantern, NightTorchLightSystem, GameOverUI | DungeonDataRegression |
| **Editor-Tools** | GarrisonSceneBuilder, CastleHubBuilder, Village2Generator, WebGLBuild, DesktopBuild, AndroidBuild, AnimatorSetup, HeroAnimatorFactory, DragonAnimatorSetup, EnemyAnimatorSetup, OuterWorldNavBake, CastleOffsetCapture, CastleWallsFromRecipe, HeroPortraitRenderer, ItemIconSlicer | BuildSettingsIntegrityRegression, RecipeResolveRegression, AnimatorContractRegression, BuildMenuUniquenessRegression |

---

## 4. COMPLETENESS CHECK

**Zero-oracle areas (prioritize authoring, first headless coverage ever):** npcs, hud, battle-atb, scenes,
devtools, misc, editor-tools.

**Runtime-only invariants — route to the AutoPilot FLEET, NOT an oracle:** CoreServices.Hud/Audio non-null
post-load; damage-LAND on `onArrive()` (physics event); Settings/Pause UXML actually binds in a player build
(UXML-empty trap); scenes return-point full restore (needs GoBattle→ATB→return drive).

**Decidable-but-fails-by-design (pair the oracle with a code/save fix, not just a passing test):**
- economy — pet active-slot save round-trip (no persisted field, flag_17).
- village-enemies-world — TribeState/SettlementState/WardStoneState NOT in SaveSchema/SaveMigrator (dropped on reload).
- resources-art — HeroPortraits/<slug> folder ABSENT (oracle hard-fails until art added).
- village-hero — Aegis weapons lack setId (ward unreachable until catalog fixed).
- version-triple / dual-copy sync oracles will fire on real current drift.

**Quarantine (do NOT instrument into service):** editor `OuterWorldBuilder.BakeWorldNavMesh` re-saves the
corruption-cursed `Village.unity` — flag/quarantine, don't wire live.

**Oracle dedup:** ZoneManager+WardReach appear in both Core-Hub and Enemies-World (split them); quests/garrison
map-non-empty in both Core-Hub and Catalogs (Core-Hub owns); pets/cosmetics/packs mapping in both Economy-Meta
and Catalogs (Economy-Meta owns object-mapping+bounds, Catalogs asserts generic deserialize-nonzero).
</content>
