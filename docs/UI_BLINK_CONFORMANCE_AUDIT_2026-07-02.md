# UI Blink/Obsidian Conformance Audit — 2026-07-02

**Scope:** every UI surface in `Assets/_Modules` (HUD, Village, Core, BattleATB, Onboarding, Wallet, Settings, Audio, BuildMode) audited against the master-frame canon. **Verified from CODE, not comments** (several header comments are provably stale — see §6). Read-only audit; no code was changed.

**Lens per surface:** (a) builds via `BuildObsidianPanel`/master factory? (b) canvas discipline, (c) font, (d) chrome canon (black+gold, shared Close, drop-zones), (e) MVVM binding vs state-pull, **(f) reuse-from-common** (per-screen copies of kit components = violation), **(g) presentation-does-no-service** (any service/`.Instance`/scene-graph/state read or mutation in a View = violation, cited even when the screen renders correctly).

---

## 1. The canonical standard (5 lines)

1. **Factory:** every panel = `ElarionUiKit.BuildObsidianPanel(...)` / `BuildObsidianModal(...)` (`Assets/_Modules/Core/UI/ElarionUiKit.cs:307` / `:407`) — Blink frame sprite-first (all 15 `frame_*.png` are committed under `Assets/Resources/RpgUi/frame/`, so the frame path always renders) with drop-zones from `ZonesFor` (`:246`); procedural black+gold fallback. Screens DROP chrome-less content into `chrome.layout.{header,body,medallion,footer}` and never restyle.
2. **Canvas:** `ElarionUiKit.BuildModalCanvas` (`:93`) — ScreenSpaceOverlay, CanvasScaler 1080×1920 match 0.5, overrideSorting; modals in the 31000 band. Hand-rolled canvases, landscape 1920×1080 reference resolutions, and runtime `UIDocument`/UIToolkit (canon §1: uGUI only) are all violations.
3. **Font:** TMP via `ElarionUiKit.EnsureFont` (`:996`) → `TMP_Settings.defaultFontAsset` ?? LiberationSans SDF. `ElarionUi.Font*` are SIZES, not fonts. Legacy `UnityEngine.UI.Text`/`LegacyRuntime.ttf`, IMGUI, and UIToolkit theme fonts are deviations (sole sanctioned exception: `ElarionUiKit.ToastCard` legacy Text, `:504`).
4. **Chrome:** near-black `ObsidianFill` + gold `ObsidianTrim` (`:181-183`); ONE shared `ObsidianCloseButton` (`:426`) — no per-panel X/Close; kit primitives (`Button`, `Label`, `Header`, `Slot`, `Card`, `Bar`, `Portrait`, `ToastCard`, `BuildConfirmModal`) reused, never re-authored.
5. **MVVM:** View binds a pure-C# VM and raises commands; the View never reads game state, never calls a service, never mutates gameplay. Reference implementation: `Assets/_Modules/HUD/DialogueView.cs:79` (frame + zones + `DialogueViewModel`, zero service reads).

**Headline counts (strict): 3 CONFORMANT · 26 PARTIAL · 34 LEGACY · 3 MISSING** (+5 no-chrome-conformant world gizmos/bridges). Only ONE full screen in the game meets the bar its own canon sets: `HeroSkillTreePanelMvvm`. The two kit toasts also pass. Everything else deviates on at least one axis.

---

## 2. Conformance matrix

Verdicts are strict and uniform (dev/settings surfaces judged by the same bar). "kit 31000" = `BuildModalCanvas` at the standard modal band. SVC = presentation-does-service violations (details cited in-row or in §5).

### 2a. Village panels (Hero / Items / Talents / Quest)

| Surface | Builds-via | Canvas | Font | Chrome | Binding | Verdict |
|---|---|---|---|---|---|---|
| **HeroSkillTreePanelMvvm** `Village/Talents/HeroSkillTreePanelMvvm.cs` | `BuildObsidianPanel(FrameTalent)` :434, body zone :441 | kit 31000 :428 | kit TMP | black+gold, shared Close :612, Blink talent sprites :305-322 | `HeroSkillTreeVM` :98,:112 — VM-only, zero service reads | **CONFORMANT** |
| ShopPanel `Village/Hero/ShopPanel.cs` | `BuildObsidianPanel(FrameMerchant)` :274, body zone :285 | kit 31000 :266 | kit TMP | black+gold, shared Close :345 | `ShopVM` :96-132; SVC: `EconomyService.Instance` :83, `FindWithTag("Player")` :737, `FindFirstObjectByType<HeroLocomotion>` :739, `AddComponent<GearLoadout>` :755, `GearCatalog.Find*` :239,:244 | PARTIAL |
| PartyShopPanelMvvm `Village/Hero/PartyShopPanelMvvm.cs` | `BuildObsidianPanel(FrameMerchant)` :284, body zone :295 | kit 31000 :278 (+private preview cam/RT rig :96-108) | kit TMP | black+gold, shared Close :405 | `PartyShopVM` :239-246; SVC heavy: scene resolution :173-191, `EconomyService.Instance` :207, `VillageInventory.Instance` :205, `GearCatalog` :994,:999, `Addressables.LoadAssetAsync` :1070, `VisualFactory.Skin` :1025; REUSE: replicates `EquipmentController.LoadsViaAddressable` :1035-1062 | PARTIAL |
| InventoryUIBuilder `Village/Hero/InventoryUIBuilder.cs` | `BuildObsidianPanel(FrameInventory)` :47 | **hand-rolled canvas** :28-40 (duplicates kit) | kit TMP | black+gold, shared Close | NO VM; SVC: `GameStateService.Instance.State.Resources` :111-116, `FindWithTag` :422; **hardcoded "SKR" wallet** :132 | PARTIAL |
| EquipmentPanel (Gear Preview) `Village/Hero/EquipmentPanel.cs` | `BuildObsidianPanel(FrameCharacter)` :100, body zone :113 | kit but **sortingOrder 2500** (off-band) :87 | kit TMP | black+gold, shared Close; second panel idiom `PanelFramed(PanelWindowDark)` drawer :403 | `EquipVM` :245-252; SVC: scene resolution :156-190, `HeroAbilities` :881; **direct gameplay mutation** `_equip.Equip(id)` :671-672; hardcoded hero names :852-876 | PARTIAL |
| TroopTrainingPanel `Village/Hero/TroopTrainingPanel.cs` | `BuildObsidianPanel` procedural :60 | kit 31000 :53 | kit TMP + raw TMP w/o `EnsureFont` :80-136 | procedural black+gold, shared Close | **NO VM**; SVC: `EconomyService.Instance` :115,:142-144,:211,:240, `GameStateService...Army` :157-160, `TroopCatalog` :180,:234, `Save()` :243 | PARTIAL |
| RaidSelectionScreen `Village/Hero/RaidSelectionScreen.cs` | `BuildObsidianPanel` procedural :89 | kit 31000 :82 | kit TMP | procedural black+gold, shared Close | **NO VM**; SVC: `SceneConfigCatalog` :145,:153 | PARTIAL |
| RaidDeployScreen `Village/Hero/RaidDeployScreen.cs` | `BuildObsidianPanel` procedural, empty title :89 + **redundant 2nd Header** :110 | kit 31050 :82 | kit TMP | procedural black+gold, shared Close | **NO VM**; SVC: `GameStateService` :338-363, `TroopCatalog` :239,:407, `SceneRouter.GoRaid` :332; placeholders §5 | PARTIAL |
| RumorBoardPanel (quest board) `Village/Hero/RumorBoardPanel.cs` | `BuildObsidianModal(FrameQuest)` :73, body zone :82 | via modal, **sortingOrder 1000** (off-band) | kit TMP (`EnsureFont` throughout) | obsidian + shared Close BUT **stone/wood-tinted rows/tabs** (`ElarionUi.PanelStone*`) :279,:328,:371,:390,:412 | **NO VM**; SVC: `QuestService.Instance` :143,:192,:501,:519, `QuestCatalog` :193,:505, `DailyQuestService.Instance` :311; REUSE: hand-rolled tab/Track/Accept buttons :269-281,:383-394,:424-433; dead `CreateHeader`/`CreateBigButton` :525-566 | PARTIAL |
| CraftingPanelMvvm (consumables) `Village/Items/CraftingPanelMvvm.cs` | `BuildObsidianPanel(FrameCrafting)` :231-233, body zone :241 | kit 31000 :225 | kit TMP | black+gold, shared Close | `CraftingVM` :66,:80 — clean; REUSE: hand-rolled recipe plate :142-162 vs kit `Slot`/`Card` | PARTIAL |
| JewelerPanelMvvm `Village/Items/JewelerPanelMvvm.cs` | `BuildObsidianPanel(FrameCrafting)` :236-238, body zone :246 | kit 31000 :230 | kit TMP | black+gold, shared Close | `JewelerVM` :65,:79 — clean; REUSE: bespoke plate :141-159 | PARTIAL |
| **VillageCraftingPanel (Workshop — still LIVE, sole `PanelId.Crafting` registrant :66)** `Village/Crafting/VillageCraftingPanel.cs` | **runtime UIDocument/UIToolkit** :42,:46,:134-204 | none (UITK; own scrim :107) | UITK theme font | stone, **per-panel X** :228-234 | **NO VM**; SVC: `VillageInventory.Instance` :72-79,:277,:433-435 incl. `TryCraft` :435, `CraftingRecipeCatalog` :113,:244,:473 | **LEGACY** |
| HeroLoadoutPanelMvvm `Village/Talents/HeroLoadoutPanelMvvm.cs` | `BuildObsidianPanel` procedural (no frame/zones) :251-253 | kit 31050 :244 | kit TMP | black+gold, shared Close | `HeroLoadoutVM` :69,:83 — clean; REUSE: bespoke tiles :123-173,:206-238 | PARTIAL |
| GameGuidePanel `Village/UI/Guide/GameGuidePanel.cs` | `BuildObsidianPanel` procedural :91-93 | kit 31000 :84 | kit TMP | black+gold, shared Close | `GuideVM` :60 — clean; REUSE: bespoke tab buttons :212-249 | PARTIAL |

### 2b. Upgrade screens (owner redo target)

| Surface | Builds-via | Canvas | Font | Chrome | Binding | Verdict |
|---|---|---|---|---|---|---|
| **BuildingUpgradePanelMvvm — THE live upgrade screen** (`FeatureFlags.BuildingUpgradePanel` default ON, `FeatureFlags.cs:106`; bootstrap :41; `PanelId.BuildingUpgrade` :58-59) `Village/Buildings/Progression/BuildingUpgradePanelMvvm.cs` | `BuildObsidianPanel` **procedural — no frame, no drop-zones** :199-201 | kit 31000 :190 | kit TMP | black+gold, shared Close :237; Blink slot plates :390-406 | `BuildingUpgradeVM` :94,:115; SVC in View: `EconomyService.Instance` :94, `BuildingTierCatalog.All` :79, `ResourceBuildingProgression.FarmId` :81; REUSE: hand-rolled tier rows `CreateRow` :320-388, `ApplyMainButtonState` :168 | **PARTIAL — redo spec §3.1** |
| BuildingUpgradePanel (UIDocument twin) `Village/Buildings/Progression/BuildingUpgradePanel.cs` | runtime UIDocument :42,:170-264 | none | UITK theme font | **brown `PanelVendor` frame** :211-219 + **X close** :288-294 | NO VM; `GameStateService` :100-108, `ResourceLedger` :336,:640-644, `TryUpgrade` :649; hardcoded "Unlocks tech: Arcane Forge" :478 | **LEGACY + DEAD** (flag-suppressed :47) — delete candidate |
| Village tier / research / perk screen | — | — | — | — | `VillageTierService.cs:23` + `BuildingPerkService.cs:20` are pure static logic; perks surface only as rows in BuildingUpgradePanelMvvm (:349-369). No dedicated surface exists. | **MISSING** |

### 2c. HUD core & in-world

| Surface | Builds-via | Canvas | Font | Chrome | Binding | Verdict |
|---|---|---|---|---|---|---|
| **VillageHudController** (main HUD: battle/town clusters, resources, hero HP/MP, party, abilities, wave) `HUD/VillageHudController.cs` | hand-rolled; **parallel `HudTheme` kit** (`StylePanel` :396, `RoundedFrame` :1282,:1817,:1982, `Disc` :1368,:1709) duplicating ElarionUiKit; own `AddText` :3027 w/o `EnsureFont` | own nested canvases (100/140/150) | TMP implicit | HudTheme language, not kit tokens | `IVillageHud` push seam = sanctioned; BUT reflection pulls: `WaveManager` :812-866, `HeroLocomotion` :2452-2456, `WisdomCurrencyService` :1486-1497, `GameStateService.Instance` :1894, `HelpMenu.Instance` :1354. Broken bindings §5. | **LEGACY** |
| CompassHud `HUD/CompassHud.cs` | code uGUI, kit tokens :145-146 | own canvas :116-129 (kit-matching values, not kit call) | TMP, own `AddText` :402 w/o `EnsureFont` | n/a (strip) | clean (bootstrap-fed) | PARTIAL |
| XPBarController `HUD/XPBarController.cs` | **UIToolkit** :19-21,:115-126 + IMGUI fallback :290-331 | none | UITK | n/a | reflection `HeroProgression` :150-227 | **LEGACY** |
| FloatingXpText `HUD/FloatingXpText.cs` | **IMGUI OnGUI** :273-313 | none | GUI.skin | n/a | reflection `HeroProgression` :117-179 | **LEGACY** |
| QuestTrackerHud `HUD/QuestTrackerHud.cs` | code uGUI, stone card :79-80 | own canvas :58-67 | TMP + `EnsureFont` :157 | stone (not obsidian) | SVC: `QuestService.Instance` :34,:98, `QuestCatalog` :117-131 | PARTIAL |
| DailyQuestHud `HUD/DailyQuestHud.cs` | **UIToolkit** (`RequireComponent(UIDocument)` :19); hand-rolled UITK toast :129-154 (not `ToastCard`) | none | UITK | n/a | SVC: `DailyQuestService.Instance` :56,:93; stray `Debug.Log` :95 | **LEGACY** |
| SocialAccessCluster `HUD/SocialAccessCluster.cs` | kit Buttons :118-123 | own canvas **ref-res 1920×1080 (wrong orientation)** :106 | kit | kit buttons | SVC: reflection `HeroLocomotion` :87-90, `FindObjectOfType` Clan/Leaderboard :143,:150, MusicSelectionPanel :157-169 | PARTIAL |
| EchoWorkforceHud (echo/silo display) `Village/Harvest/EchoWorkforceHud.cs` | `BuildObsidianModal` :106 (sort 4600) | via modal | kit | black+gold, shared Close | `HarvestPanelGate` seam :61 BUT `EchoService.Instance` direct :56,:138-146,:151 | PARTIAL |
| BattleArenaHud (arena HUD + victory/defeat) `Village/Arena/BattleArenaHud.cs` | mixed: Victory :157 / Defeat :202 via `BuildObsidianPanel` + kit; but Flee/intro strip = own `AddPanel/AddText/AddButton` :437-481 with **legacy Text LegacyRuntime** :458-468 | own canvas **ref-res 1920×1080** :375, sort 5000 :366-376 | split TMP/legacy | split | dumb view — data pushed via `ShowResult` :131 / `BattleRewardSummary` :181-188 | PARTIAL |
| FloatingHealthBar (world-space) `Village/Combat/FloatingHealthBar.cs` | code-built world-space canvas :192 (legit) | world-space | n/a | REUSE: own palette :50-55 + own chip sprite :358-387 duplicate kit tokens | delegate-bound (`Func<float>`) :62-63 — clean | PARTIAL |
| GearGrantToast `Village/NPCs/GearGrantToast.cs` | `ElarionUiKit.ToastCard` :73 | own canvas ref-res 1920×1080 :62 | kit (sanctioned legacy Text) | kit toast | args-bound | **CONFORMANT** (minor ref-res) |
| BuildFeedbackToast `Village/BuildMode/BuildFeedbackToast.cs` | `ElarionUiKit.ToastCard` :137 | ref-res 1920×1080 :126 | kit | kit toast | reason-map bound :104-116 | **CONFORMANT** (minor ref-res) |
| Item pickup notification ("ItemHud") | — | — | — | — | `ItemDropWatcher` is logic-only; no on-screen pickup feedback exists | **MISSING** |
| Population display | part of VillageHudController town metrics (`SetTownMetrics` :2824, live-bound) — no standalone widget | | | | | (covered above) |

### 2d. Popup panels & dev overlays (UIDocument family — all fail the uGUI canon)

| Surface | Builds-via | Font | Close | Binding | Live? | Verdict |
|---|---|---|---|---|---|---|
| HelpMenu (settings/help) `HUD/HelpMenu.cs` | UIDocument code-built :125, own PanelSettings :66-78, sort 2700 | LegacyRuntime.ttf :218-222 | per-panel Close :201 | NO VM; reflection `GameStateService.ResetToNewGame`/`SceneRouter` :378-394; hardcoded email :31 / endpoint :32 | yes | **LEGACY** |
| AdminOverlay `HUD/AdminOverlay.cs` | UIDocument :160, sort 32000 :96 | LegacyRuntime.ttf :262 | per-panel Close :237 | reflection Economy/Wave/Progression :423-431,:438,:650-670,:774-828; grant literals 50000/25000 :662,:670 | dev only (release-stripped :211-236) | **LEGACY** |
| OwnerDevToolsOverlay `HUD/OwnerDevToolsOverlay.cs` | bespoke uGUI canvas :140-144, sort 5500, **no CanvasScaler config** | legacy Text :456 | none | `PiSignInController` :94, `SceneRouter` :239-244, `GameStateService` :351, reflection :264-346 | **ships** (Pi-user gate :61) | **LEGACY** |
| LeaderboardPanel (ranks) `HUD/LeaderboardPanel.cs` | UIDocument :96 | UITK | **per-panel X** :160 | `LeaderboardService.Instance` :67,:73; placeholder rivals :237-247 | yes | **LEGACY** |
| ClanChatPanel (chat) `HUD/ClanChatPanel.cs` | UIDocument :88 | UITK | **no close at all** | `ClanService.Instance` :59,:65,:246,:410-447; hardcoded clan "Ember Wardens"/"EMBR" :176,:180,:420-421 | yes | **LEGACY** |
| PlayerProgressPanel `HUD/PlayerProgressPanel.cs` | authored UXML `Q<>` :55-67 (**UXML doesn't render in builds**) + IMGUI fallback :174-235 | GUI.skin | UXML :67 / IMGUI :233 | reflection `HeroProgression` :95-143; `_imguiXpNeeded=200f` :44,:104 | likely dead/orphan | **LEGACY** |
| HeroTalentPanel (old, T-key) `HUD/HeroTalentPanel.cs` | UIDocument :112 | UITK | per-panel "Close (T)" :196 | reflection Talents/Wisdom :494-545 | **DEAD** — bootstrap retired :36-42; superseded by HeroSkillTreePanelMvvm (`PanelId.HeroTalents` :78) | **LEGACY + DEAD** — delete candidate |
| PetSkillTreePanel `HUD/PetSkillTreePanel.cs` | UIDocument :140, borrowed PanelSettings :61-83, sort 105 | UITK | per-panel "Close (P)" :193 | `PetUnlockTracker.Instance` :96,:287-293,:382-390; reflection catalog :587-661 | yes (PanelRouter :89) | **LEGACY** |
| CosmeticShopPanel `HUD/CosmeticShopPanel.cs` | UIDocument :279, borrowed PanelSettings :74-95, sort 95 | UITK | per-panel close (`ShopTheme.StyleCloseButton`) :341-342 | reflection Cosmetics/Glimmer :140-275,:401-448; hardcoded footer :374 | yes (NOT superseded — PackStore is separate) | **LEGACY** |
| DebuggingController (F9 inspector) `HUD/DebuggingController.cs` | bespoke uGUI :186-190, sort int.MaxValue, no scaler | legacy Text :209,:222 | none | diagnostic-only | dormant :72 | **LEGACY** (same bar applied) |
| SettingsController + SettingsModel `Settings/SettingsController.cs` | UIDocument + authored `SettingsScreen.uxml` `Q<>` :165-187 + runtime buttons :211-296 | LegacyRuntime.ttf :204-208 | "Back" :187 | binds static `SettingsModel` but Apply* in view :445-484; `AudioMixerBridge`/`DifficultyTuning` direct :146,:245,:434 | scene-wired via `PauseController._settings` only (`PauseController.cs:59`) | **LEGACY** |
| MusicSelectionPanel `Audio/MusicSelectionPanel.cs` | UIDocument code-built :112, borrowed PanelSettings :67-83, sort 96 | UITK | per-panel Close :150 | `AudioService.Instance` :164,:173-179,:207-211 | yes (SocialAccessCluster toggle :49; J-key dev-gated :100) | **LEGACY** |

### 2e. End-state & interstitial screens (first-class citizens)

**There is no GameOver scene; all end-states are runtime overlays.**

| Surface | Exists? | Builds-via | Binding | Verdict |
|---|---|---|---|---|
| **Hero death screen** | **HUB SCENES ONLY** — `GameOverScreen` gates on `HubScenes.IsHub` (`Village/Hero/GameOverScreen.cs:41`). In arena/dungeon/outpost the hero silently respawns or evacuates (`HeroHealth.cs:524-556`, :538-543) with **no death screen and no defeat sting** :202-205 | `GameOverScreen.BuildOverlay` :228: own canvas :233-238 **ref-res 1280×720** :238, sort 32760; kit `Scrim/Panel/Header/Label/Button` but **NOT `BuildObsidianPanel`**; **manual Input hit-test buttons (no EventSystem)** :267-270 | NO VM; `GameStateService`/`SceneRouter`/services direct :96-107,:142-148,:217,:223 | **PARTIAL** (screen) + **MISSING** (non-hub death) |
| **Heart / Tree of Life death (game over)** | YES — `GameOverScreen.ShowHeartFell` :167 ("THE ROOT WENT SILENT") + Defeat music :222-223, triggered by `HeartController.cs:231-235` | same overlay as above | same | **PARTIAL** |
| GameOverUI (orphan) `Modules/UI/GameOverUI.cs` | not referenced by any scene (`HeroHealth.cs:520-521` comment); hardcoded "Elarion has fallen..." :21 | serialized uGUI | — | **LEGACY + DEAD** — delete candidate |
| Arena victory / defeat | YES — the ONLY end-state on the master factory: `BattleArenaHud.cs` `BuildObsidianPanel` :157 (victory) / :202 (defeat) | see §2c row | pushed `BattleRewardSummary` | PARTIAL |
| Raid victory `Village/World/Camps/RaidVictoryController.cs` | YES | `BuildModalCanvas`+`Scrim`+`Panel`+`Header` :266-280 — kit pieces, **not `BuildObsidianPanel`** | NO VM; `RaidClaimService`/`GameStateService`/`SceneRouter`/Audio direct :168 | PARTIAL |
| Outpost victory `Village/World/Camps/OutpostVictoryController.cs` | YES | `ShowVictoryToast` :243, same pattern :247-256, auto-dismiss | direct services | PARTIAL |
| Wave celebration `Village/Waves/WaveCelebrationManager.cs` | YES | prefab text else **IMGUI OnGUI toast** :280-299 (hardcoded style :285-292); reflection TMP :254-260 | direct | **LEGACY** |
| Wave-clear banner (HUD) | **`ShowWaveClearBanner` is a NO-OP** — body empty at `VillageHudController.cs:2662` (banner removed WO-563); interface still advertises it | — | — | (see §5) |
| ATB battle end | ATB is flag-retired (`WaveBreachToAtb` OFF, `FeatureFlags.cs:215`; DungeonRealtime replaces :225-231). **No result screen ever existed** — `HandleOutcome` :576 lingers then `LoadSceneWithFade` :672 (`BattleATB/BattleController.cs`); `BattleHudUgui.cs` uses kit pieces on own 1920×1080 canvas :203 | | fallbacks "Blaise" :79, "skeleton" :76, seed 42 :73, hardcoded dmg text :855-859 | PARTIAL (retired) |

### 2f. Title / onboarding / store / sign-in / overlays

| Surface | Builds-via | Binding | Verdict |
|---|---|---|---|
| TitleController `Onboarding/TitleController.cs` | **UIDocument** :62, `BuildTitleScreen` :557; re-implemented `MakeMenuButton` :276 | services direct :306-385; hardcoded copy :724,:1094 | **LEGACY** |
| HeroSelectController `Onboarding/HeroSelectController.cs` | **UIDocument** :75, `BuildScreen` :194 | `GameStateService.ChooseHero` :222,:962, `SceneRouter` :949-953; hardcoded :311,:525,:684 | **LEGACY** (also not yet the Blink creation-carousel canon) |
| PetSelectController `Onboarding/PetSelectController.cs` | UIDocument :51 | — | **LEGACY + retired** (flag-bypassed :125-130) |
| StoryIntroController `Onboarding/StoryIntroController.cs` | UIDocument :43 | — | **LEGACY** |
| OnboardingFlow (tutorial overlay) `Onboarding/OnboardingFlow.cs` | UIDocument :86 + `TutorialOverlay.uxml` + code fallback :318 (hardcoded colors :360-373) | `GameStateService.FinishOnboarding` :631 | **LEGACY** |
| IntroSequencePlayer (video intro) `Village/DialogueUI/IntroSequencePlayer.cs` | `BuildModalCanvas` :251, uGUI | — | PARTIAL |
| PackStore `Wallet/PackStore.cs` | **UIDocument** :37 (authored UXML renders empty in builds → code-built UITK scaffold :112-120) | `WalletService` :87 | **LEGACY** |
| Pi sign-in `Core/Platform/PiSignInController.cs` | `BuildButton` :192 on raw canvas :194 (sort 5000 :198), **legacy Text LegacyRuntime** :212-221 | `PiPlatform.Current` :49, `UnityWebRequest` in view :153; hardcoded Pi-violet :207, VerifyUrl :22, copy :79,:91,:222 | **LEGACY** |
| VillageLoadOverlay `Core/UI/VillageLoadOverlay.cs` | code uGUI 1080×1920 :81 ✓ but **legacy Text LegacyRuntime** :214-215 | hardcoded lore :48-56, title :112, colors :103,:135 | **LEGACY** |
| PortraitLockOverlay `Core/UI/PortraitLockOverlay.cs` | Bootstrap deliberately no-op :66-79 (would be legacy Text :183,:225) | — | DEAD by design |
| RegionGate / seam transition | no dedicated mask UI — `SceneRouter.LoadSceneWithFade` only | — | (note) |

### 2g. Build-mode flow (owner addendum — full flow audited)

**Headline: NOT ONE surface in the entire build-mode flow routes through `ElarionUiKit`.** Live panels are runtime UIDocuments themed with the OLDER `ElarionUi` parchment/stone kit or raw inline colors; two placement systems coexist.

| Surface | Builds-via | Chrome | Binding | Live? | Verdict |
|---|---|---|---|---|---|
| BuildMenu `Village/Buildings/UI/BuildMenu.cs` | **UXML UIDocument** :71-118 (UXML doesn't clone in builds :262-266 → code-built UITK fallback :338-419) | stone + per-panel Close :69,:231,:413 | NO VM; **economy spend in view** `AddCrystals(-cost)` :285-289,:621-627; `TowerPlacementSystem.Instance` :369-372; `FindObjectsByType<Tower>` + `t.Upgrade()` :379-383,:656,:732-757; reflection `WallRepairController` :818-842; **hardcoded `Variants[]` tower costs/DPS/HP** :133-139, fixed 20 wood/5 stone :780-788, `_localCrystalBalance=500` :62 | yes | **LEGACY** |
| BuildPaletteUI (create strip) `Village/BuildMode/BuildPaletteUI.cs` | UIDocument code-built :178,:301 | `ElarionUi` parchment :206-208,:329-334; per-panel "Done"/"Orient" :221,:229 | NO VM; `GameStateService.Instance` :85,:96,:409, `EconomyService...CanAfford` :373, `CatalogRegistry` :272,:281; seed "❖ 0" :210 | yes (`BuildModeController.cs:206`) | PARTIAL |
| BuildSelectionUI (Move/Upgrade/Sell/Cancel) `Village/BuildMode/BuildSelectionUI.cs` | UIDocument :137 | **raw inline colors** :158,:169-184, local `StyleButton` :188-194 | clean (controller callbacks) | yes (:996) | **LEGACY** |
| TowerManagerPanel `Village/Buildings/UI/TowerManagerPanel.cs` | UIDocument :110, borrowed PanelSettings :53-58 | `ElarionUi` stone :123-126; per-panel Close :144, local `Btn` :254 | NO VM; `FindObjectsByType<Tower>` :154; **mutates directly** `TryUpgrade()` :209, `Destroy(...)` raze :216; **stale on-screen "Towers (press M)"** :125 (M-key removed :85-88) | yes | **LEGACY** |
| BuildStructureInfoPanel `Village/BuildMode/BuildStructureInfoPanel.cs` | UIDocument :132, `ElarionUi` parchment :141-239, own Place/Cancel :255,:266 | parchment | `PlacementGrid.Instance` :416, `StructureFactory` :419; synthesized descriptions :436-447 | **DEAD** — `OnCardTapped` subscription disabled (`BuildModeController.cs:1767-1775`) | PARTIAL + DEAD |
| BuildPreviewModal `Village/BuildMode/BuildPreviewModal.cs` | hand-rolled uGUI canvas :86-90 (sort 100), **legacy Text** :104-161, hand-rolled obsidian-imitation :101,:116,:175, manual Input hit-test :404-440 | imitation, 5 per-modal buttons :164-189 | `RotationCorrectionRegistry.SetAndSave` :459,:477 | **DEAD** — no `AddComponent<BuildPreviewModal>` anywhere; rotate path itself dormant (`BuildModeController.cs:478,:515`) | **LEGACY + DEAD** — delete candidate |
| LeanTouchBuildDriver button bar `Village/BuildMode/LeanTouchBuildDriver.cs` | UIDocument :239 | **raw colors** :254,:258, local `StyleBigButton` :263 | input seam only — clean | touch only (:1891) | **LEGACY** |
| GhostPreview `Village/BuildMode/GhostPreview.cs` | world-space `VisualFactory.Skin` :85, tint-only validity :167-179 — **no cost readout on ghost** | n/a | `CatalogEntry` only — clean | yes | CONFORMANT (world gizmo; cost-label gap noted) |
| PlacementGrid overlay `Village/BuildMode/PlacementGrid.cs` | world `LineRenderer` :229 | hardcoded blue :258 | clean | yes | CONFORMANT (gizmo) |
| TowerPlacementSystem `Village/Buildings/TowerPlacementSystem.cs` | world `LineRenderer` ring :367; legacy `Input.*` :180-217; **parallel placement system** duplicating GhostPreview (entered via BuildMenu :371,:632,:916, not BuildModeController) | n/a | `GameStateService.Instance` :156,:281,:318, `SkillSystem.Instance` :287 | yes | CONFORMANT as gizmo; **duplication flag** |
| BuildModeHudBridge / BuildButtonBridge `Village/BuildMode/` | bridges only (no UI): HUD hide on enter :76-94; HUD `BuildRequested` → `Toggle()` :90-120 | — | sanctioned reflection seam | yes | CONFORMANT (bridges) |

---

## 3. The redo list (work-order-ready)

Ordered as a work sequence; ordering rationale = player exposure (end-state + every-session surfaces earliest), not a tiering of importance — every item below is a full redo to the same bar. Each item: target frame, drop-zone contents, VM contract.

### 3.1 WO-A — BuildingUpgradePanelMvvm conformance finish (owner-named; smallest gap)
The live upgrade screen is already obsidian+VM; three fixes make it the second reference implementation:
- **Frame:** pass `frameName: RpgUiCatalog.FrameCrafting` (or mint `FrameCore` zones) at `BuildingUpgradePanelMvvm.cs:199`; move header→`layout.header`, tier list→`layout.body`, wallet strip→`layout.footer` (footer zone already exists in the default `ZonesFor`).
- **VM injection:** remove the two View-side reads — `EconomyService.Instance` (:94) and `BuildingTierCatalog.All`/`FarmId` (:79-81) — by moving default-building resolution + economy handle into `BuildingUpgradeVM`'s construction (a static `BuildingUpgradeVM.CreateDefault()` on the VM side).
- **Rows:** replace `CreateRow` (:320-388) with kit `Slot` + a bound row (name/cost/state chip = `ItemVM`-shaped), keeping the perk icons.
- Delete the dead UIDocument twin `BuildingUpgradePanel.cs` + its bootstrap (flag-suppressed since default flipped ON), and fix both files' stale "default OFF" headers.
- **MISSING sibling:** if the village-tier/research ladder should be player-visible beyond building rows, that is a NEW surface (spec via PO — not built today): `FrameTalent` frame, body = tier ladder bound to a `VillageTierVM` over `VillageTierService`/`BuildingPerkService`.

### 3.2 WO-B — ONE Obsidian end-state template (player died / tree died / victory / results)
Today: four divergent implementations (GameOverScreen non-factory overlay + manual hit-testing, RaidVictoryController kit-pieces, OutpostVictory toast, WaveCelebration IMGUI) and one no-op (wave banner). Build ONE `EndStateScreen` in presentation:
- `BuildObsidianModal("EndState", title, …, frameName: FrameCore)`; header = outcome title ("THE ROOT WENT SILENT" / "YOU HAVE FALLEN" / "VICTORY"); medallion/body top = crown/star rating socket (reuse BattleArenaHud's crown sprites :227); body = rewards list via kit `Slot` rows; footer = kit `Button`s (Continue / Retry / Title) — real EventSystem buttons, killing GameOverScreen's manual `Input` hit-test (:267-270).
- Bound to an `EndStateVM { Kind, Title, Sub, Rewards[ItemVM], Stars, Commands }`; today's callers (GameOverScreen triggers `HeartController.cs:231-235` + `HeroHealth`, RaidVictoryController, OutpostVictoryController, BattleArenaHud ShowResult, WaveCelebrationManager) each construct the VM and stop drawing.
- **Closes the MISSING:** wire hero death in arena/dungeon/outpost (`HeroHealth.cs:524-556`) to the same screen (Kind=HeroDeath, non-hub variant with Respawn/Evacuate commands) + play the defeat sting (:202-205 gap). Delete orphan `Modules/UI/GameOverUI.cs`. Either implement or remove `IVillageHud.ShowWaveClearBanner` (no-op at `VillageHudController.cs:2662`) — route wave-clear through the template's toast-sized variant.

### 3.3 WO-C — Title + HeroSelect + onboarding to the template (first-impression set, UIDocument exit)
- TitleController → `BuildObsidianPanel(FrameCore)` full-screen: header = game title art, body = menu column of kit `Button`s bound to `TitleVM { Start, Continue, Settings, Credits }`; kill `MakeMenuButton` (:276) and direct service calls (:306-385).
- HeroSelectController → the canon Blink creation-carousel (memory `hero-select-blink-creation-carousel`): `FrameCharacter`; medallion = class portrait; body = central hero + class column left/specs right; `HeroSelectVM { Classes[], Selected, Choose }` absorbs `GameStateService.ChooseHero` (:222,:962). V1 Knight-only selectable.
- OnboardingFlow tutorial overlay → kit toast/`BuildConfirmModal` primitives; delete `TutorialOverlay.uxml` path (:86) — UXML cannot ship. StoryIntro → `FrameDialogue` reusing the dialogue template. Delete retired PetSelectController.

### 3.4 WO-D — Build-mode flow onto the kit (zero-kit flow today)
- BuildMenu: LEGACY on every axis — rebuild as `BuildObsidianModal(FrameCrafting)`; body = structure grid of kit `Slot`s bound to `BuildMenuVM { Entries[ItemVM], Wallet, Select }`; **all hardcoded `Variants[]` numbers (:133-139) move to catalog data**; placement/upgrade/economy commands move to the VM/services (View spend at :285-289 is the worst single MVVM violation found).
- BuildPaletteUI → same VM, kit strip (uGUI) with footer wallet; BuildSelectionUI → kit `Button` row; TowerManagerPanel → `FrameCore` panel + `TowerListVM` (kills direct `TryUpgrade`/`Destroy` :209,:216 and the stale "(press M)" :125); LeanTouch bar → kit Buttons.
- Consolidate the TWO placement systems (GhostPreview vs TowerPlacementSystem ring) into one, and add the missing on-ghost cost readout. Delete dead BuildPreviewModal + BuildStructureInfoPanel (or resubscribe the info panel if the tap-preview is wanted — PO call).

### 3.5 WO-E — Main HUD convergence (largest surface; every session, every minute)
VillageHudController is LEGACY by duplication, not by look: `HudTheme` is a full parallel kit (`StylePanel`, `RoundedFrame`, `Disc`, `GoldButton`) predating ElarionUiKit.
- Phase 1 (mechanical): retire `HudTheme` token-by-token onto `ElarionUiKit`/`UiStyle.Theme`; route `AddText` (:3027) through `EnsureFont`; adopt kit `Bar`/`Portrait`/`PartyFrameRow` (which were EXTRACTED from this HUD — the HUD never adopted its own extraction).
- Phase 2 (bindings): fix the dead bindings in §5 (party MP, SetMana, hero name); replace reflection pulls (:812-866, :1486-1497, :1894) with bridge pushes (the sanctioned direction — bridges already exist for most of these).

### 3.6 WO-F — UIDocument popup family → kit panels (one per lane, shared recipe)
Same recipe each: `BuildObsidianModal(frame)` + zones + a thin VM; each is an independent file-disjoint lane.
| Panel | Frame | VM |
|---|---|---|
| VillageCraftingPanel (Workshop) | `FrameCrafting` | `WorkshopVM` over `VillageInventory`/`CraftingRecipeCatalog` (mirror CraftingPanelMvvm — it's the proven shape) |
| SettingsController | `FrameSettings` | `SettingsVM` over SettingsModel (+ owner's HUD-by-space-type toggles later) |
| MusicSelectionPanel | `FrameOptions` | `MusicVM { Tracks[], Current, Select }` over AudioService |
| PetSkillTreePanel | `FramePet` | `PetTreeVM` over PetUnlockTracker (copy HeroSkillTreePanelMvvm graph) |
| CosmeticShopPanel | `FrameMerchant` | `StoreVM` (binding-map row exists) |
| LeaderboardPanel | `FrameQuest`/`FrameCore` | `RanksVM` (kill the X :160) |
| ClanChatPanel | `FrameCore` | `ChatVM` (add the missing Close) |
| HelpMenu | `FrameSettings` | `HelpVM` (keep bug-report POST in the VM/service) |
| PackStore | `FrameMerchant` | `PackStoreVM` over WalletService (store scene PanelSettings issue disappears with UIDocument) |
| Pi sign-in | `BuildConfirmModal` | `SignInVM` (move UnityWebRequest out of the view :153) |
| XPBarController / FloatingXpText / DailyQuestHud / QuestTrackerHud / WaveCelebration | kit `Bar` + `ToastCard` + `FrameQuest` chips | existing services behind thin VMs; IMGUI/UITK paths deleted |
| VillageLoadOverlay / AdminOverlay / OwnerDevToolsOverlay / DebuggingController | kit primitives (TMP via EnsureFont) | dev surfaces — same bar, lowest sequence position |

### 3.7 Small-diff conformance fixes (batchable, no redesign)
- InventoryUIBuilder: call `BuildModalCanvas` instead of :28-40; bind or remove the "SKR" placeholder :132.
- EquipmentPanel: sortingOrder 2500→31000 band (:87); remove direct `_equip.Equip` (:671); (full Obsidian paper-doll restyle = WO-582 canon, separate).
- RumorBoardPanel: stone rows→kit tokens; hand-rolled buttons→kit `Button`; delete dead `CreateHeader`/`CreateBigButton`; sort 1000→band.
- RaidDeployScreen: remove redundant second header (:110); RaidSelection/RaidDeploy/TroopTraining get thin VMs; TroopTraining raw TMPs→`EnsureFont`.
- HeroLoadoutPanelMvvm / GameGuidePanel / CraftingPanelMvvm / JewelerPanelMvvm: pass a frameName + move content into zones; recipe plates→kit `Slot`.
- Ref-res 1920×1080 canvases (SocialAccessCluster :106, BattleArenaHud :375, toasts :62,:126, BattleHudUgui :203, GameOverScreen 1280×720 :238) → 1080×1920 kit standard.
- MVVM-panel open-site cleanups: ShopPanel :83/:737-755, PartyShopPanelMvvm :173-207 (+ move the addressable-preview logic behind a shared service; it forks `EquipmentController.LoadsViaAddressable`).

---

## 4. Canvas & font census (the "same canvas discipline" ask)

- **Kit-canvas conformant (31000 band):** ShopPanel, PartyShop, TroopTraining, RaidSelection (31000), RaidDeploy/HeroLoadout (31050), Crafting/Jeweler Mvvm, HeroSkillTree, BuildingUpgradeMvvm, GameGuide, EchoWorkforce (4600 via modal), RumorBoard (1000 — off-band), EquipmentPanel (2500 — off-band), IntroSequencePlayer.
- **Hand-rolled uGUI canvases:** InventoryUIBuilder (:28), VillageHudController (nested 100/140/150), CompassHud (:116), QuestTrackerHud (:58), SocialAccessCluster (:101, landscape), BattleArenaHud (:366, landscape), GameOverScreen (:233, 1280×720), toasts (landscape), OwnerDevToolsOverlay (:140, no scaler), DebuggingController (:186, no scaler), PiSignIn (:194), VillageLoadOverlay (:81 ✓ portrait), BattleHudUgui (:203, landscape), DialogueView (:67 — kit values but built inline; the reference impl itself hand-assembles its canvas).
- **UIDocument/UIToolkit (canon violation as a class):** VillageCraftingPanel, BuildingUpgradePanel(dead), BuildMenu, BuildPaletteUI, BuildSelectionUI, TowerManagerPanel, BuildStructureInfoPanel(dead), LeanTouchBuildDriver, HelpMenu, AdminOverlay, LeaderboardPanel, ClanChatPanel, PlayerProgressPanel, HeroTalentPanel(dead), PetSkillTreePanel, CosmeticShopPanel, XPBarController, DailyQuestHud, Settings, MusicSelection, Title, HeroSelect, PetSelect(dead), StoryIntro, OnboardingFlow, PackStore. **26 surfaces.**
- **Font deviations:** LegacyRuntime/legacy Text — HelpMenu :218, AdminOverlay :262, OwnerDevTools :456, Settings :204, PiSignIn :212, VillageLoadOverlay :214, BattleArenaHud intro strip :458, BuildPreviewModal :104, DebuggingController :209. IMGUI — FloatingXpText :273, PlayerProgressPanel :174, WaveCelebration :280. UITK theme font — all UIDocument surfaces. Raw TMP without `EnsureFont` — TroopTraining :80-136, VillageHudController :3027, CompassHud :402.

---

## 5. "Pulls incorrectly" — displayed values not bound to live data

**Main HUD (every session):**
- Party member MP: `fillAmount = 1f` set once, never updated — `VillageHudController.cs:1873`.
- `SetMana(float,float)` — interface setter is a **no-op** (hero MP pushed by bridge is dropped) — `VillageHudController.cs:2898`.
- `ShowWaveClearBanner` — **no-op body** :2662 while `IVillageHud` still advertises it; wave-clear shows nothing from this path.
- Hero name falls back to a class label, not the player/hero name — :1908-1917.
- PartyHudBridge pushes companions at placeholder full HP (catalog-known).
**Wallets / economy:**
- InventoryUIBuilder wallet "SKR" literal `:132` (Gold/Crystals above it are live).
- BuildMenu: entire tower cost/DPS/HP economy is a hardcoded `Variants[]` stub :133-139; material have-counts fixed 20 wood/5 stone :780-788; crystal balance seeds 500 :62; upgrade result line computed from the stub :727.
- BuildPaletteUI seeds "❖ 0" :210 (corrected live — cosmetic first-frame lie).
**Placeholders shown as content:**
- RaidDeployScreen: "Battle Preview (enemy base)" niche :275; static "Est. Clear Time" :280 (TODO live); "Auto Recommend" logs only :297,:314-319; party slot defaults "Knight" :366.
- LeaderboardPanel: placeholder rival rows :237-247. ClanChatPanel: hardcoded clan "Ember Wardens"/"EMBR" :176,:180,:420-421. PlayerProgressPanel: `_imguiXpNeeded=200f` :44,:104.
- TowerManagerPanel title "Towers (press M)" — trigger removed, hint stale :125 (removal noted :85-88).
- BuildingUpgradePanel(dead): "Unlocks tech: Arcane Forge" literal :478.
- ATB (retired): enemy "skeleton" :76, hero "Blaise" :79, seed 42 :73, damage text :855-859.
- PiSignIn copy/color/URL inline :22,:79,:91,:207,:222; VillageLoadOverlay lore/title :48-56,:112; hardcoded hero display names in PartyShop :229-234 and EquipmentPanel :852-876.
- Benign-but-noted: HeroSkillTree respec fallback 300 :617 (pre-bind only; live value binds at :601).

---

## 6. Canon/docs corrections surfaced by this audit (same-breath rule)

1. `BuildingUpgradePanel.cs` + `BuildingUpgradePanelMvvm.cs` headers claim the flag ships OFF; `FeatureFlags.cs:106` defaults **ON** → MVVM panel is live, UIDocument twin is dead. Headers STALE.
2. `docs/MASTER_CATALOG/hud.md` (verified 2026-06-12) predates DialogueView/ElarionUiKit/Obsidian and still lists ClanChat "Y", Cosmetic "C", Talent "T" hotkey popups as LIVE — HeroTalentPanel is retired; catalog needs a refresh pass for the HUD/UI area.
3. `docs/UI_MVVM_BINDING_MAP.md` lists `VillageCraftingPanel` and `BuildingUpgradePanel` as the mapped views — both mappings now point at superseded/legacy implementations.
4. Two theme kits coexist in `DeNelle.Core.UI` (`ElarionUi` parchment vs `ElarionUiKit` obsidian) with no doc stating which is canon for which layer — the build-mode flow themed itself on the wrong one. One line in `UI_BLINK_TEMPLATE_CANON.md` should declare `ElarionUi` = tokens only (colors/sizes), never a panel language.
5. Dead-code delete candidates found: `BuildingUpgradePanel(+Bootstrap)`, `HeroTalentPanel`, `Modules/UI/GameOverUI.cs`, `BuildPreviewModal`, `BuildStructureInfoPanel` (or resubscribe), `PetSelectController`, `PlayerProgressPanel`, `VillageHud.uxml/.uss`, `VirtualDPadLean` (catalog-flagged), RumorBoard `CreateHeader`/`CreateBigButton`.

---

*Audit executed 2026-07-02 by read-only fleet (5 silo sweeps + build-mode sweep), every row verified from source. No code edited.*

---

## OWNER ADDENDA (2026-07-02, post-audit — BINDING on every redo)
- **Reuse from common wherever possible; continuity means everything; presentation does NOT do service.**
- **No low-priority surfaces** — any screen can be a player's first touch; every path is a shining example of our quality. Build mode included.
- **Shop/store rule:** every button must serve a REAL purpose — no generic X-to-close (the one shared Close is the only close affordance), no decorative/dead buttons. A button that does nothing observable is a defect.
- **NPC dialogue card standard:** every NPC dialogue shows name + guild/shop affiliation + portrait; a missing portrait must render a styled silhouette placeholder, never a raw color quad (the Sylas yellow-blank case). Mechanically enforceable — oracle to catch violations.
- **One action = one button:** never two different Equip buttons (or any duplicated action affordance) on the same surface — a single canonical control per action, reused from the kit.
- **The earns-its-place test (owner 2026-07-02):** on every screen, every element must answer "is this here for a reason, RIGHT NOW?" Examples the owner named: the "Sign in with Pi" button riding the HUD all game (nobody plays half the game then decides to log in — belongs on title/menu context, not mid-combat); the compass markers you can't even see (make it a real, visible, useful compass or remove it); anything visible while exploring must offer the player a benefit in that moment.
- **Wave chrome is contextual (owner 2026-07-02):** the Next Wave timer shows a REAL countdown or it goes ("the timer would have value if it was real countdown — that is missing"); the Start Wave button + wave medallion appear only where starting a wave is a sensible act (in town, between waves) — never "in the field." Part of the HUD-by-space-type direction.
- **Bottom status bar (owner 2026-07-02):** the tree health readout earns its place; the tower count (0/0) has NEVER been used and the population readout runs logic never tested — each either becomes real, used, verified information or comes off the bar. No untested numbers shown to players.
- **Mobile feel (owner 2026-07-02):** even on mobile the UI felt ROUGH — smoothness/transition standards apply to touch targets too (press feedback, eased state changes, no snap).
- **Walk feel:** locomotion animation "didnt feel like walking" — walk cycle vs movement speed mismatch (foot-slide) and clip quality are a felt-priority for the hero polish lane.
- **Hot-swap bars (feature ask):** there are NO hot swap bars — the HUD convergence redo must include quick-swap action/consumable bars (the arena potion row generalized: assignable slots, usable in town/world per space-type rules).
- **Touch controller (owner 2026-07-02):** the on-screen movement controller = FOUR ROUNDED BUTTONS per the owner mockup — not the current square D-pad tiles. Part of the HUD/controls redo.
- **Currency display (owner 2026-07-02):** GOLD is the currency that matters most yet renders tiny/inconsistent (shop corner "Gold: 15", tiny far-right chips). Build ONE dedicated currency component in the common kit (icon + amount, gold given visual primacy, consistent size/placement rules) and every surface — HUD bar, shops, upgrade panels, end-state spoils — reuses it. No per-screen currency rendering ever again.
- **Maxed buildings (owner 2026-07-02):** a fully-upgraded building must NOT keep showing the upgrade affordance/prompt — it reverts to its normal interaction role (talk/quests). The world affordance consults BuildingUpgradeVM maxed state. (Upgrade-lane line item.)
- **Crafting panel redesign (owner 2026-07-02):** filters + SCROLLABLE recipe list on the left; selecting a recipe shows its detail on the right (ingredients with have/need counts, result preview) + a Craft button. Standard master-detail shape on the Obsidian frame. (Crafting-lane redo item.)
- **THE HUD MANDATE (owner 2026-07-03, BINDING — "no one stops till every single element of the HUD outside our icons is this"):** every HUD element converts to the real Blink Obsidian art — Health_Bar/Energy_Bar + fills, Cast_Bar 1-3 + fill, the 5x4 button families with hover states, the 3-state Close, Action_Bar_Slot, Notification toasts, chat set, HUD cores, decorations — via the canonical pipeline (BlinkUiImporter mirror → Resources/RpgUi → RpgUiCatalog → kit). Our own game icons stay. Iterate until the owner felt-pass.
