# Master Catalog — Village Systems

Scope: `Assets/_Modules/Village/` subfolders **BuildMode, Harvest, Tutorial, Arena, Combat**, plus
root **EconomyService / ResourceCost / Buildings/Progression (building+upgrade)**. Hero / NPCs /
enemies-world are catalogued elsewhere.

- **Assembly:** all code below is in `DeNelle.Village.asmdef` (name `DeNelle.Village`).
  refs: Core, AI, Cosmetics, Data, Pets, Wallet, Audio, Localization, UniTask, Cinemachine,
  InputSystem, TextMeshPro, LeanTouch/LeanCommon/CW.Common, URP, **Unity.AI.Navigation**, YarnSpinner.
- **Namespaces:** `DeNelle.Village` (root + BuildMode + Harvest + Tutorial + Combat),
  `DeNelle.Village.Arena`, `DeNelle.Village.Buildings.Progression`, `DeNelle.Village.UI`.
- Verified by reading source (2026-06-12), not comments.

---

## 1. BuildMode — the CREATE verb (player base-building, WO-108)

Wired end-to-end **for towers**; CoC-style grid place/move/sell. Enter via HUD Build button →
top-down cam + frozen waves → palette card → ghost → place → charge → persist to
`GameState.BaseLayout` (save v14) → `BaseLayoutLoader` rebuilds on reload. Doc:
`docs/BUILD_MODE_ARCHITECTURE.md` (current, "~70% built, don't greenfield"); superseded spec
`docs/build-mode-architecture.md` (WO-108 original, kept for reference).

| Class | File | Resp. / key public API | Bootstrap / wiring |
|---|---|---|---|
| **BuildModeController** | BuildModeController.cs (1682 ln) | MB. Build-Mode entry/exit + placement loop. `Instance`; `event Action<bool> BuildModeChanged`; `IsActive`; `EnsureExists()`; `Toggle/Enter/Exit()`; `SetInput(IBuildInput)`. | singleton+`EnsureExists`; toggled by BuildButtonBridge. LIVE. |
| **BuildButtonBridge** | BuildButtonBridge.cs (132) | static. Wires HUD `VillageHudController.BuildRequested` → `BuildModeController.Toggle` **by reflection** (Village can't ref HUD). | `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` + `sceneLoaded`, re-wires on any "Village" scene. LIVE. |
| **BuildModeHudBridge** | BuildModeHudBridge.cs (87) | static. Hides combat HUD cluster while Build Mode active, via `CoreServices.Hud` reflected `SetCombatHudVisible(bool)`. | `[RIOLM SubsystemRegistration]` resets statics + `[AfterSceneLoad]` hooks `BuildModeChanged`. LIVE. |
| **PlacementGrid** | PlacementGrid.cs (255) | MB. Shared cell-occupancy grid (3m cells, 28×22). `Instance`; `WorldToCell/CellToWorld/SnapToGrid`; `CanPlace/InBounds/Free/ClearAll`; `FootprintCells(metres)`; `SetGridVisible`. | singleton. LIVE. |
| **GhostPreview** | GhostPreview.cs (235) | MB. Translucent green/red placement ghost via VisualFactory.Skin. `SetEntry/SetPlaceholder/MoveTo/SetValid/Hide/Clear`. | created by controller. LIVE. |
| **PlacedStructure** | PlacedStructure.cs (92) | MB. Runtime marker on every placed structure. fields `itemId, gridCell, footprint, yawSteps, level, sellValue`; `TierVisual`; `ToSaveData()`; `SetHighlighted`. | added per spawn. LIVE. |
| **BaseLayoutLoader** | BaseLayoutLoader.cs (241) | MB. Runtime twin of VillageSceneBuilder — instantiates `GameState.BaseLayout` via StructureFactory + carving NavMeshObstacle (no rebake). `Instance`; `EnsureExists`; `LoadFromState`; `Forget/ClearLoaded`; `Rebuild(layout)`; `Spawn(data,grid)`. Empty layout = no-op (seed village stands). | singleton. LIVE. |
| **StructureTierVisual** | StructureTierVisual.cs (112) | MB. Per-tier visual progression on placed structure (DEF-208). `CurrentTier`; `Apply(level)`; `Refresh()`. | LIVE. |
| **RotationCorrectionRegistry** | RotationCorrectionRegistry.cs (149) | static. Per-prefab-type persistent yaw correction (WO-282, PlayerPrefs). `GetYawOffset(id)`; `SetAndSave(id,yaw)`; `GetAll()`; `ClearAllForTesting()`. | LIVE. |
| **BuildFeedbackToast** | BuildFeedbackToast.cs (200) | MB + `enum BuildRejectReason`. Surfaces WHY a placement was rejected (WO-394). `static Show(reason)`, `Show(string)`, `MessageFor(reason)`. | LIVE. |

**BuildMode UI (code-built, no UXML):**

| Class | File | Resp. / key public API |
|---|---|---|
| **BuildPaletteUI** | BuildPaletteUI.cs (368) | MB. Palette of buildable CatalogEntry cards (reads CatalogRegistry). events `OnEntrySelected/OnCardTapped/OnExitRequested/OnOrientRequested`; `Show/Hide/SetArmed(id)/Render`. |
| **BuildSelectionUI** | BuildSelectionUI.cs (196) | MB. Move/Sell/Upgrade action panel (WO-108 P2). events `OnMove/OnSell/OnUpgrade/OnCancelRequested`; `Hide`. |
| **BuildPreviewModal** | BuildPreviewModal.cs (524) | MB. Modal preview + rotation chooser. `Show(entry, Action<float> onConfirm, onCancel)`. |
| **BuildStructureInfoPanel** | BuildStructureInfoPanel.cs (462) | MB. Structure Info Preview panel (WO-352). events `OnPlaceRequested/OnCancelRequested`; `Show(entry)/Hide`. |

**BuildMode input seam (S6):**

| Class | File | Resp. |
|---|---|---|
| **IBuildInput** | IBuildInput.cs (73) | interface: `ScreenPoint`, `PlaceOrSelect`, `Cancel`, `Rotate`. |
| **DesktopBuildInput** | DesktopBuildInput.cs (47) | mouse/keyboard `IBuildInput`. |
| **LeanTouchBuildDriver** | LeanTouchBuildDriver.cs (270) | MB+`IBuildInput`. **THE ONLY Lean.Touch file in Build Mode.** `Install(cam)/Uninstall`; latched `PlaceOrSelect/Cancel/Rotate` (read-clears). |

---

## 2. Harvest — resource gathering + offline accrual (WO-115/117)

Two distinct sub-systems: **WO-115** offline accrual (DDOL service, claims on resume) and
**WO-117** active worker dispatch (lives in OuterWorld). `WorkerManager.UseOfflineCatchUp`
default **OFF** — WO-115 owns offline so they don't double-count. `MineNode`/`MineResource` are
in another module (referenced, not in scope).

| Class | File | Resp. / key public API | Bootstrap / state |
|---|---|---|---|
| **OfflineHarvestService** | OfflineHarvestService.cs (328) | MB. Accrue resources while away (WO-115). `Instance`; `ClaimOnResume=true`; `event Claimed`; `ClaimAccrual()`. Food→wallet, Crystals→GameState. | `OfflineHarvestBootstrap` DDOL. LIVE. |
| **OfflineHarvestBootstrap** | OfflineHarvestBootstrap.cs (31) | static. Self-installs OfflineHarvestService. | `[RIOLM AfterSceneLoad]`. LIVE. |
| **OfflineHarvestResult** | OfflineHarvestResult.cs (55) | data. Per-resource haul. `Iron/Wood/Food/AetherCrystals`, `AwaySeconds`, `WasCapped`, `Total`, `None`, `Add(MineResource,amt)`. |
| **TimeSource** | TimeSource.cs (42) | static. Single clock seam for accrual. `ServerOffsetMs`; `NowUnixMs()`. |
| **WorkerManager** | WorkerManager.cs (331) | MB. Harvest orchestrator (WO-117 Phase 1). `Instance`; `DropOff`; `UseOfflineCatchUp=false`; `ClickToDispatch=true`; `AttachFillIndicators=false`; `DispatchNearestFreeWorkerTo(node)`; `RegisterWorker(w)`; `ActiveAssignments()`. | installed by WorkerManagerBootstrap in OuterWorld. LIVE (OuterWorld-gated). |
| **WorkerManagerBootstrap** | WorkerManagerBootstrap.cs (120) | MB. Installs WorkerManager **only when OuterWorld scene loaded** (lives in that scene so workers unload on return). `Instance`; `EnsureFirst`-style. | `[RIOLM AfterSceneLoad]` DDOL + `sceneLoaded==OuterWorld`. **scene-gated to OuterWorld.** |
| **Worker** | Worker.cs (135) | MB + `enum WorkerState{Idle,Traveling,Collecting,Returning,Fleeing}`. Dispatched harvest unit. `State`; `TargetNode`; `IsCollectingAt/IsAvailable`; `DispatchTo(node)`; `Release()`. |
| **NodeFillIndicator** | NodeFillIndicator.cs (132) | MB. World-space harvest progress bar over a MineNode (WO-117). `Node`; `HeightOffset`; `BarSize`; `static Attach(node)`. Off by default (`AttachFillIndicators=false`). |
| **WelcomeBackPopup** | UI/WelcomeBackPopup.cs (188) | MB (ns `DeNelle.Village.UI`). "Gathered while you slept" summary (WO-115 §2). `static Show(OfflineHarvestResult)`. |

---

## 3. Tutorial — FTUE / first-time onboarding (WO-277 / DEF-222)

`TutorialDirector` is the 7-scene master sequencer; self-injects, gates on first-run save +
`HubScenes.IsHub` (runs in **MainCastle_Hall AND Village2** — widened from hardcoded "Village2",
which was the companion-never-meets-you bug). DialogueService + DialogueCommandBridge are the
shared Yarn interaction layer (used far beyond the tutorial — all vendors/buildings route here).

| Class | File | Resp. / key public API | Bootstrap / gating |
|---|---|---|---|
| **TutorialDirector** | TutorialDirector.cs (825) | MB. FTUE master sequencer (7 scenes). `static ForceRun/ForceSkip` (dev). Adds CompanionSpawner/TutorialDialogue/TutorialAutoWalk/TutorialWaveSpawner/PetIntroduction at runtime. Grants 3× tower cost post-battle. | `[RIOLM AfterSceneLoad]`; `Awake` destroys self if `!HubScenes.IsHub`; `Start` runs only if `IsFirstRun` (`!State.Onboarded`) or ForceRun. LIVE, save-gated. |
| **DialogueService** | DialogueService.cs (242) | static. **THE one launch path for every Yarn dialogue.** `Current` (finds DialogueRunner); `IsRunning`; `NodeExists(node)`; `Play(node)`; `Stop()`; `CurrentStructureId`; `PlayStructure(id, displayName)`. | LIVE, project-wide. |
| **DialogueCommandBridge** | DialogueCommandBridge.cs (860) | MB. Registers EVERY custom Yarn command/function onto the single DialogueRunner. `Install(runner)`. ~45 commands: camera_*, play_sfx/music, structure_* (status/upgrade/talk), autowalk, set_hud_*, spawn_wave_at_nearest, grant_resources_for_towers, pet verbs, Quest verbs (StartQuest/AdvanceQuest/CompleteQuest/SetQuestFlag/GiveKeystone), RecruitCompanion, Open* (Shop/Upgrade/Craft/Equip/Arena/RumorBoard), LearnRecipe, wait_for_event; functions HasKeystone/KeystoneCount/IsQuestActive/IsQuestComplete/pet_owned. **Consolidates the now-dead NPCCommandBridge** (single registration to avoid YarnSpinner dup-name throw). | LIVE. |
| **CompanionSpawner** | CompanionSpawner.cs (121) | MB. Picks+spawns FTUE companion (WO-277). `CompanionClass`; `CompanionTransform/Name/ShortName`; `Spawn()`; `ClearOverride()`; `static CompanionClassFor(player)`. |
| **TutorialAutoWalk** | TutorialAutoWalk.cs (127) | MB. Drives hero along scripted waypoints via `HeroLocomotion.SetAutoWalk(waypoint)` (NavMesh.SamplePosition snaps dest). `IsWalking/HasArrived`; `SetHero/WalkTo/Stop`. *(Uses NavMesh — see Flags note on HeroLocomotion.)* |
| **TutorialDialogue** | TutorialDialogue.cs (163) | MB. Scripted-line queue via TownsfolkBubble. `IsIdle/IsSpeaking`; `SetBubble/SetSpeaker/Enqueue/Say(speaker,lines)/Clear`. |
| **TutorialWaveSpawner** | TutorialWaveSpawner.cs (150) | MB. Scripted first-combat wave. `IsCleared`; `SetWaveManager(wave)`; `async SpawnAt(spawnPoint,count)`. |
| **TutorialHudOverlay** | TutorialHudOverlay.cs (278) | MB. FTUE objective banner + hint line + UI highlight. `SetObjective/HideObjective`; `SetHint/HideHint`; `Highlight(elementName,on)`. |
| **PetIntroduction** | PetIntroduction.cs (263) | MB + `enum PetRole{Defend,Gather}`. Scene-6 starter-pet name + role. `IsComplete`; `PetName`; `ChoseGather`; `Begin()`. |

---

## 4. Arena — async-PvP raid loop (ARENA MVP, WO-386/388/389/390)

~75% built (doc `docs/ARENA_SOLUTION.md`, current). Defender base = player's own `GameState.BaseLayout`;
SKR wager is a **client-side stub** (`ArenaWalletService`). Reachable via in-village herald. Three
seeded opponents. Reuses EnemyOutpost for the opponent garrison (no new combat).

| Class | File | Resp. / key public API | State |
|---|---|---|---|
| **ArenaMode** | ArenaMode.cs (476) | MB + `enum ArenaResult{None,Win,Loss}`. Async-PvP raid flow controller. `Instance`; `static List<string> AttackSquad`; `RaidInProgress`; `CurrentOpponent`; `UsePlayerCastle`; `event OnRaidEnded`; `TryStartRaid(opponent)` (debits SKR stub, spawns EnemyOutpost garrison, win=OnCleared/lose=hero-down|timeout). | LIVE; **SKR debit/escrow = stub**. |
| **ArenaHeraldSpawner** | ArenaHeraldSpawner.cs (445) | MB. In-village entry point (herald near Heart) that makes Arena reachable; proximity prompt. `Instance`; `HeraldOffset/InteractRadius/BannerHeight`. Also `AuraPulse` MB (intensity pulse). | `[RIOLM AfterSceneLoad]` DDOL singleton (`Destroy(this)` dedup). LIVE; suppressed while setup/recruit modes open. |
| **ArenaDefenseSetupController** | ArenaDefenseSetupController.cs (437) | MB. Defense SETUP/placement loop (WO-389 P2). `Instance`; `event SetupModeChanged`; `IsActive`; `EnsureExists/Toggle/Enter/Exit`; `SetInput(IBuildInput)`. | LIVE. |
| **ArenaAttackRecruitController** | ArenaAttackRecruitController.cs (244) | MB. ATTACK recruit loop (WO-389 #3 MVP). `Instance`; `event RecruitModeChanged`; `IsActive`; `EnsureExists`; `SetOpponent(op)`; `Toggle/Enter/Exit`. | LIVE. |
| **ArenaCatalog** | ArenaCatalog.cs (120) | `ArenaOpponentDef` (Id/DisplayName/Flavour/Tier/Threat/GuardCount/Wager/BaseRecipe; `WinPurse=Wager*2`) + static `ArenaCatalog.All/Get(id)`. **3 hardcoded seeded opponents** (no JSON). |
| **ArenaDefenseCatalog** | ArenaDefenseCatalog.cs (239) | `enum DefenderKind`, `ArenaDefenseDef` (Id/Cost/Kind/UnitClass/BehaviorId/Hp/Damage/Range/AttackInterval) + static catalog. `DefensePointPool=50` (TODO data-driven). **6 hardcoded defenders**; `SpentPoints(placed)`. |
| **DefensePatternLibrary** | DefensePatternLibrary.cs (193) | `DefensePattern` + static lib. **Placeholder MVP seed layouts** (TODO → arena-defense-patterns.json). `All/RandomPattern()/RandomLayout()`. |
| **ArenaDefensePaletteUI** | ArenaDefensePaletteUI.cs (241) | MB. Code-built defense setup palette. events `OnDefSelected/OnExitRequested`; `Show/Hide/Render(spent,remaining)`. |
| **ArenaAttackPaletteUI** | ArenaAttackPaletteUI.cs (380) | MB. Code-built attack recruit palette. events `OnRecruit/OnLaunchRequested/OnCancelRequested`; `Show/Hide/Render`. |
| **ArenaPanel** | ArenaPanel.cs (580) | MB. Code-built uGUI entry + result UI. `IsOpen`; `Open()/Close()`. |
| **ArenaProgressStore** | ArenaProgressStore.cs (117) | static. W/L ledger (GameState + PlayerPrefs). `Current`; `RecordWin(purse)/RecordLoss()`. |
| **ArenaWalletService** | ArenaWalletService.cs (123) | static. **CLIENT-SIDE SKR WAGER STUB.** Seed 500 SKR (PlayerPrefs `dotr-arena-skr-balance`). `Balance/CanAfford/Debit/Credit/DevReset`. STUB — replace w/ WalletService(Skr)+backend escrow. |
| **ArenaNavMeshBaker** | ArenaNavMeshBaker.cs (138) | MB. **Runtime** NavMesh bake for imported player-castle defender base. `BakeForCastle(root)`. Used ONLY on UsePlayerCastle path (seeded hub raids reuse pre-baked mesh — bake froze the game). |
| **ArenaHudBridge** | ArenaHudBridge.cs (55) | internal static. Suppresses whole gameplay HUD while an Arena setup screen is open. |
| **ArenaMode/AuraPulse helpers** | (above) | — |

---

## 5. Combat (folder) — world-space combat UI tells

Only two files in `Combat/` (ns `DeNelle.Village`); the heavier combat lives in BattleATB/other modules.

| Class | File | Resp. / key public API |
|---|---|---|
| **FloatingHealthBar** | Combat/FloatingHealthBar.cs | MB (WO-178). Code-built world-space HP bar over a combat unit. `static Attach(host, Func<float> fraction, ...)`; `static SetTargetedOn(host,bool)`; `SetTargeted(bool)`; `MarkEngaged()`; `Init(...)`. |
| **ThreatSkullPlate** | Combat/ThreatSkullPlate.cs | MB (WO-155 Ph3). Fallout-style red-skull readiness tell. `const RiskyDelta=3, LethalDelta=7`; `static Attach(host, Func<int> threatLevel, ...)`. |

---

## 6. Root Economy + Building/Upgrade Progression

### EconomyService.cs (405 ln, ns `DeNelle.Village`)
- `struct ResourceSnapshot` (Wood/Food/Iron/Crystals).
- **`struct ResourceCost`** (4-field: `Wood/Food/Iron/Crystals`; ctor named-args; `IsZero`; `WoodOnly/FoodOnly/IronOnly/CrystalsOnly` factories).
- **`EconomyService` : MonoBehaviour** — DEF-78 multi-resource wallet. `Instance`; `Wood/Iron` (in-session pool), `Food`/`Crystals` (read-through to `GameState.Resources`, single source of truth), `Snapshot`; `SecuredOutpostCount`; `TerritoryMultiplier=1+0.05*secured`; `event OnChanged`; `OnOutpostSecured()`; `CanAfford(ResourceCost)`; `TrySpend(cost)` (Wood/Iron from pool, Food/Crystals from GameState); `Grant(cost)` / `Grant(w,f,i,c)`; `GrantSpendable(...)` (writes BOTH the in-session pool AND `GameState.Wood/Iron` — see Flags: Wood/Iron dual-wallet).
  - Bootstrap: `[RIOLM AfterSceneLoad]` DDOL singleton. Bridges `GameStateService.ResourcesChanged` → re-emits `OnChanged` so HUD updates on GameState-backed gains. LIVE.

### CrystalEconomy.cs (150 ln, ns `DeNelle.Village`)
- `CrystalEconomy : MonoBehaviour` — lightweight Aether-crystal balance singleton. `Instance`; `CurrentCrystals` (get/set); `CanAfford(cost)`; `TrySpend(cost)`; `AddCrystals(amt)`. **Overlaps `EconomyService.Crystals` / `GameState.Resources.Crystals`** — see Flags.

### Buildings/Progression/ (ns `DeNelle.Village.Buildings.Progression`)
- **ResourceBuildingProgression.cs (507)** — data-driven leveling for the 3 resource buildings.
  - `enum HarvestResource`; **`readonly struct ResourceCost(HarvestResource, int)`** (resource+amount — DIFFERENT from EconomyService's 4-field ResourceCost; see Flags).
  - `ResourceLevelDef` (Level/Yields/YieldPerTick/UpgradeCost/MagicCost/UnlocksTechNode/IsMagicGated/IsMaxLevel).
  - `ResourceBuildingDef` (BuildingId/DisplayName/Yields/Levels[]/MaxLevel/ClampLevel/LevelDef).
  - static `ResourceBuildingProgression`: `FarmId/LumbermillId/ForgeId`; `All`; `OrderedIds`; `Find(id)`; `IsResourceBuilding(id)`. **Balance table HARDCODED in `Build()`** (NOT JSON): Farm→Food (5 lvl), Lumbermill→Wood (5 lvl), Forge→Iron (5 lvl + 6th **Magic-gated Arcane Forge** tier: 3 Magic + harvestables, unlocks `arcane_forge` tech node).
  - static `ResourceLedger` (read/spend Wood/Iron/etc).
- **ResourceBuildingState.cs (146)** — runtime level + upgrade behaviour. `enum UpgradeResult`; `event LevelChanged`; `GetLevel(id)`; `CurrentDef(id)`; `CurrentYield(id)`; `IsMaxLevel(id)`; `TryUpgrade(id)`; `ResetAll()`.
- **TechTree.cs (82)** — Magic-gated tech-node ledger. `const ArcaneForgeNodeId="arcane_forge"`; `event NodeUnlocked`; `IsUnlocked(id)`; `Unlock(id)`; `UnlockedNodes`; `ResetAll()`.
- **BuildingUpgradePanel.cs (594)** — MB, code-built UI-Toolkit upgrade panel for Farm/Lumbermill/Forge. `Instance`; `IsOpen`; `Toggle()`; `OpenFocused(id)`; `Open()/Close()`.
- **BuildingUpgradePanelBootstrap.cs (114)** — static. Auto-spawns BuildingUpgradePanel in any hero-present scene (mirrors VillageCraftingPanelBootstrap). `EnsureFirst()` via `[RIOLM AfterSceneLoad]`; + internal `BuildingUpgradePanelInput` MB. LIVE.

---

## 7. Related Docs

| Doc | Gist | State |
|---|---|---|
| `docs/BUILD_MODE_ARCHITECTURE.md` | Reconciliation: Build Mode ~70% built end-to-end for towers; gaps = palette content, 4-resource cost, upgrade verb, mobile touch, plot, arena tie-in. | **current** |
| `docs/build-mode-architecture.md` | Original WO-108 forward spec ("let the player do what VillageSceneBuilder does"). | **superseded** by the UPPERCASE doc (now largely implemented). |
| `docs/ARENA_SOLUTION.md` | 4-agent synthesis (WO-386/388/389/390): arena ~75% built; remaining = connective tissue + 2D presenter + defend/watch inversion. Defender base = player's own GameState.BaseLayout. | **current** |
| `docs/RESOURCE_ECONOMY_DESIGN.md` | "Never-designed" economy flow doc: hybrid fast-early/slow-late pacing, tunable numbers; sinks (build/wall-tiers/Forge/refine/crafting) vs faucets (nodes/mines/waves/offline). | **current (design-only)** |

---

## 8. FLAGS

### Duplicate / overlapping types
- **TWO different `ResourceCost` structs, same simple name, different namespaces:**
  - `DeNelle.Village.ResourceCost` (EconomyService.cs) — 4-field Wood/Food/Iron/Crystals wallet cost.
  - `DeNelle.Village.Buildings.Progression.ResourceCost` (ResourceBuildingProgression.cs) — single resource+amount.
  Easy to confuse / collide in `using`-heavy files; the upgrade tables use the Progression one.
- **THREE crystal stores in this tree, only ONE is canonical:** `GameState.Resources.Crystals` is
  the single source of truth (EconomyService.Crystals reads through it). `CrystalEconomy` (CrystalEconomy.cs)
  is a separate singleton with its own `CurrentCrystals` set/spend — verify it actually delegates to
  GameState or it is a stale parallel wallet.

### Wood/Iron dual-wallet hazard (documented in code)
- `EconomyService` Wood/Iron live in an **in-session pool** (shop + HUD bar read this) while the
  building-upgrade flow's `ResourceLedger` reads/spends **GameState.Wood/Iron**. They do NOT auto-sync;
  `GrantSpendable()` exists solely to write BOTH so dev grants are visible to both. Food/Crystals are
  single-sourced (GameState); Wood/Iron are the landmine.

### Stale-comment-vs-code class (the HeroLocomotion "pure transform" pattern)
- **TutorialAutoWalk** moves the hero by `HeroLocomotion.SetAutoWalk(waypoint)` and NavMesh-snaps the
  destination — i.e. the FTUE walk depends on the hero being a **NavMeshAgent**, consistent with the
  known HeroLocomotion comment-vs-code mismatch (a stale "pure transform" comment hides that
  HeroLocomotion drives a NavMeshAgent). No new stale comment authored inside these scoped files, but
  any reader trusting a "pure transform" HeroLocomotion comment will misread the auto-walk path.
- No other comment-vs-code contradictions found in the scoped files on read; headers (WO numbers,
  "the ONE path") match the code.

### Scene-gated / mode-gated (working but not always present)
- **Harvest WorkerManager** only installs when **OuterWorld** is loaded (WorkerManagerBootstrap); absent
  in the castle/village interior — looks "missing" there but is by design.
- **TutorialDirector** runs only on first-run save (`!Onboarded`) AND in a Hub scene (MainCastle_Hall /
  Village2); self-destructs elsewhere. `WorkerManager.UseOfflineCatchUp` ships **OFF** (WO-115 owns offline).
- **ArenaHeraldSpawner** prompt suppressed while Arena setup/recruit screens own the screen.

### Stub / not-yet-real
- **ArenaWalletService** entire SKR wager economy is a PlayerPrefs client stub (seed 500); `ArenaMode.TryStartRaid`
  debits/credits it. Marked STUB in-code — replace with WalletService(CurrencyKind.Skr) + backend escrow.
- **ArenaCatalog (3 opponents), ArenaDefenseCatalog (6 defenders, point pool 50), DefensePatternLibrary**
  are HARDCODED seed data with explicit `// TODO data-driven → *.json` notes (no JSON files exist in scope).
- **ResourceBuildingProgression** balance table is hardcoded in `Build()` (doc calls these "tunable
  placeholder" numbers), not data-driven.

### Dead / consolidated
- **NPCCommandBridge is dead** — all its vendor/station Yarn verbs (OpenShop/OpenUpgrade/OpenCraft/
  OpenEquip/OpenArena/OpenRumorBoard/LearnRecipe) were consolidated into DialogueCommandBridge so each
  Yarn action name registers exactly once (YarnSpinner throws on duplicate names). Do not re-add them.
