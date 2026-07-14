# Master Catalog — DevTools / Settings / Onboarding

Reference catalog for the dev-tools, settings menus, and onboarding/dev-grant area.
Verified by reading source 2026-06-12. Terse; see code for full detail.

Scope: `Assets/_Modules/DevTools`, `Assets/_Modules/Settings`,
`Assets/_Modules/HUD/AdminOverlay*`, `Assets/_Modules/Core/OnboardingMode.cs`,
`Assets/_Modules/Onboarding/{OnboardingFlow,TitleController}`,
`Assets/_Modules/Village/OnboardingIntegrator.cs`,
`Assets/_Modules/Core/State/DifficultyTuning.cs`, + the two grant paths.

---

## 1. DevTools — asmdef `DeNelle.DevTools` (ns `DeNelle.DevTools`)

asmdef refs: Core, Village, Wallet, HUD, UniTask. **defineConstraints:
`UNITY_EDITOR || DEVELOPMENT_BUILD`** → whole assembly absent from release builds
(module-isolation EXCEPTION: tooling may reference gameplay modules).

### DevPanelController.cs
- File: `Assets/_Modules/DevTools/DevPanelController.cs`
- Responsibility: in-game QA/debug console (UI Toolkit overlay) — grants resources, jumps state, spawns boss, mock wallet, cheat toggles.
- **Whole file body is `#if DEVELOPMENT_BUILD || UNITY_EDITOR`** (belt + asmdef constraint).
- MonoBehaviour, `[RequireComponent(typeof(UIDocument))]`. Toggled by F1 hotkey (`_toggleKey`) + on-screen "DEV" corner chip. Spawned by DevBootstrap (no per-scene wiring).
- UI is **fully code-built with inline styles in `BindElements()`** — does NOT read DevPanel.uxml/.uss (UXML renders empty in builds). Self-comment confirms this; **NOT a stale comment** — see FLAGS.
- Static cheat flags read by gameplay: `GodMode` / `InstantWinWave` (get; private set), events `GodModeChanged` / `InstantWinWaveChanged`. (No integrator currently reads them in-tree — see FLAGS.)
- Live metrics panel refreshed ~5x/s while open (FPS, wave/phase, live enemies by EnemyDefId, boss, hero level/xp, heart hp/state, economy, wisdom, cheats).
- Public methods: `SetOpen(bool)`, `Close()`, `IsBound` (prop).
- Action groups: Resources (+crystals, +50k wood/stone/iron via `EconomyService.GrantSpendable`, wisdom, hero xp/level, trigger wave), Entitlements (grant by id / all packs via PackCatalog), Heart (hp/state), Waves (spawn enemy, Spawn Syndrath dragon, jump-to-wave, instant-win toggle), Scene jump (SceneRouter), Mock wallet (DevWalletProbe), Cheats (god-mode).
- Deps: GameStateService, EconomyService, PackCatalog, HeroProgression, WisdomCurrencyService, WaveManager, HeartController, DragonBoss, SceneRouter, VillageHudController, DamageNumberSpawner, DevWalletProbe.
- **Known dev-seam gaps (documented in code):** `JumpToWave`/`SpawnEnemy` fall back to `WaveManager.BeginLoop()` because `WaveManager.DevJumpToWave`/`DevSpawnOne` are NOT implemented — status line says so. `_dragonBossPrefab` must be inspector-assigned for Spawn Syndrath (auto-spawn via DevBootstrap leaves it null → reports cleanly).
- WIRED + LIVE (dev builds only).

### DevBootstrap.cs
- File: `Assets/_Modules/DevTools/DevBootstrap.cs`
- Responsibility: DEV-only auto-spawner — creates the DevPanel once, persistent, every scene.
- Whole body `#if DEVELOPMENT_BUILD || UNITY_EDITOR`. `static`.
- **Bootstrap: `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` → `Spawn()`** (guarded `_spawned`). Creates DontDestroyOnLoad GameObject "[DEV] QA Dev Console" + UIDocument (sortingOrder 9000) + DevPanelController.
- `ResolvePanelSettings()`: (1) Resources "DevPanelSettings", (2) adopt a themed sibling UIDocument's PanelSettings, (3) runtime-create one borrowing any theme. UXML at Resources "DevPanel" is OPTIONAL (controller code-builds).
- WIRED + LIVE (dev builds).

### DevWalletProbe.cs
- File: `Assets/_Modules/DevTools/DevWalletProbe.cs`
- Responsibility: DEV-only `IWalletProvider` with QA-settable mock balances; wraps `StubWalletProvider`, overrides only `GetBalance`.
- Whole body `#if DEVELOPMENT_BUILD || UNITY_EDITOR`. `sealed class : IWalletProvider`.
- Static seed balance (5 SOL / 250 USDC / 2000 SKR). Public: `static SetMockBalance(CurrencyKind,double)`, `static MockBalance` (prop), + IWalletProvider members (Connect/Disconnect/GetBalance/SendPayment/SignMessageBase58/CanSignMessages all delegate to inner stub).
- LIVE only if a wallet screen builds its WalletService over DevWalletProbe (no in-tree consumer wires it — probe is set-only from DevPanel; see FLAGS).

### DevPanel.uxml (67 lines) / DevPanel.uss (210 lines)
- Editor-reference only. NOT loaded at runtime (controller code-builds). Effectively **dead at runtime** (kept as reference).

### README.md
- `DeNelle.DevTools` blurb. Current but terse (3 files listed).

---

## 2. AdminOverlay (the owner/dev grant path) — asmdef `DeNelle.HUD` (ns `DeNelle.HUD`)

### AdminOverlay.cs
- File: `Assets/_Modules/HUD/AdminOverlay.cs`
- Responsibility: owner-only debug overlay. **NOT `#if`-gated — ships in release builds** (unlike DevTools). Gated instead by owner-wallet match OR the debug chord.
- MonoBehaviour, `[DisallowMultipleComponent]`. Self-adds UIDocument in Awake, adopts a sibling PanelSettings, sortingOrder **2710** (just above HelpMenu 2700). Registers with `PanelManager` (DEF-212 single-modal arbiter, "Admin" slot).
- Owner gate: `const string OwnerWalletAddress = ""` (TODO(owner) — empty → `IsAuthorised()` always false → reachable ONLY via chord). Chord: **Ctrl+Shift+A** (`Update()`, legacy Input).
- **All gameplay calls go through `System.Reflection`** (HUD asmdef must not ref Village/Core.State): GameStateService, WaveManager, EconomyService, TowerPlacementRotateMenu.
- Public: `Toggle()`, `Open()` (Help menu "Dev tools" routes here), `Close()`, `IsOpen` (prop).
- **LIVE panel (owner-trimmed 2026-06-11) has only 3 buttons:** "Load resources (full base)" (`OnLoadResources` → reflection `EconomyService.GrantSpendable(50k wood,25k food,50k iron,25k crystals)`), "Reset Yarn (replay tutorial)" (`OnReplayTutorial` → clears PlayerPrefs `yarn.companionMeeting.seen`, `SceneRouter.GoHeroSelect()`), "Close".
- **DEAD-but-retained handlers (kept in file, NOT wired to any button):** `OnTriggerWave`, `OnGiveCrystals`, `OnSetOnboarded`, `OnSave`, `OnReset`, `BuildOrientRow`/`OnOrientAsset`/`OpenOrientMenu` (dev orient tool). See FLAGS.
- `OnReset` (dead) reflects `ResetToNewGame`, clears the FTUE PlayerPrefs gate, reloads "Village2".

### AdminOverlayBootstrap.cs
- File: `Assets/_Modules/HUD/AdminOverlayBootstrap.cs`
- Responsibility: autospawn AdminOverlay in every scene (overlay stays hidden until chord/wallet).
- `static`. **Bootstrap: `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` → `EnsureFirst()`** + `SceneManager.sceneLoaded` hook. Per-scene de-dup. WIRED + LIVE.

**TWO PARALLEL DEV PANELS** (per memory `playable-loop-exists-but-scene-gated`): corner "DEV" chip = DevPanelController (dev builds only, code-built); Settings→Dev tools / Ctrl+Shift+A = AdminOverlay (ships, reflection, 3 live buttons). Both grant the same EconomyService.GrantSpendable bundle.

---

## 3. Settings — asmdef `DeNelle.Settings` (ns `DeNelle.Settings`)

asmdef refs: **Core, UniTask only** (no Audio, no Village) → cross-module calls use reflection.

### SettingsModel.cs
- File: `Assets/_Modules/Settings/SettingsModel.cs`
- Responsibility: static store + apply layer for all options. `static class`.
- Also defines `enum QualityTier { SeekerLow=0, SeekerHigh=1, Desktop=2 }` and `static class ScreenShakeSetting { bool Enabled }` (gameplay reads this flag without depending on the persistence layer).
- Persistence split: Music/SFX/Mute/**Difficulty** → GameStateService (canonical `dotr-save`); Master/QualityTier/ScreenShake → PlayerPrefs keys (`dotr-settings-*`).
- Props: `MasterVolume`, `MusicVolume`, `SfxVolume` (UI scale 0..1.5; GameState stores 0..100), `Muted` (fresh default **true**, a11y), `Difficulty` (GameState #23, default Normal), `Quality` (default = SeekerBootstrap auto-pick), `HasExplicitQuality`, `ScreenShake` (default true).
- Apply: `ApplyAll()` / `ApplyAudio()` (→ AudioMixerBridge) / `ApplyQuality()` (→ SeekerBootstrap.ApplyTier) / `ApplyScreenShake()`. `ResetToDefaults()`. Tier name mapping helpers `TierName`/`TierFromName`/`TierLabel`.
- Deps: GameStateService, AudioMixerBridge, SeekerBootstrap (Core), DifficultyTuning. WIRED + LIVE.

### SettingsController.cs (re-verified from code 2026-07-13, WO-714 W8)
- File: `Assets/_Modules/Settings/SettingsController.cs`
- Responsibility: the options menu — Master/Music/SFX sliders + Music On/Off + global mute, 3-way Difficulty selector + blurb, 3-tier Quality selector, screen-shake toggle, Game Guide (WO-588) + Reset Defaults, Back = chrome Close. Modal.
- **CODE-BUILT since 2026-07-03 (WO-F, coverage row #47): NO UIDocument/UXML.** Lazy `ElarionUiKit.BuildObsidianModal` (FrameSettings, sortingOrder 32000) on first `Open()`; all controls are composed uGUI/TMP through the kit. WO-714 W8 (2026-07-13): modal widened to x 0.08–0.92 and every label routed through `FitSingleLine`/`FitBlock` at the WO-693 mobile font floor.
- Public: `Open()`, `Close()`, `IsOpen`, event `SettingsClosed` (UnityEvent). Every control persists+applies through SettingsModel (no save step). Music On/Off also drives live audio via AudioServiceBridge.
- Opened by PauseController's Settings button. LIVE via `PauseHudBootstrap` (below) — no scene placement needed.

### SettingsBootstrap.cs
- File: `Assets/_Modules/Settings/SettingsBootstrap.cs`
- Responsibility: re-apply persisted settings at launch + forward changes to AudioService.
- `static`. **Bootstrap: `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` → `Init()`** (after SeekerBootstrap's BeforeSceneLoad). Calls `SettingsModel.ApplyAll()`, subscribes `GameStateService.SettingsChanged`, reflectively calls `DeNelle.Audio.AudioService.ApplyPersistedSettings()` (DEF-22, asmdef isolation). `HasRun` prop. WIRED + LIVE.

### AudioMixerBridge.cs
- File: `Assets/_Modules/Settings/AudioMixerBridge.cs`
- Responsibility: 0..1.5 linear → dB onto AudioMixer exposed params (`MasterVol`/`MusicVol`/`SfxVol`). `static`.
- **Seam-safe: no-ops quietly when no mixer asset present** (resolves Resources `Audio/GameAudioMixer` lazily, or `SetMixer()` direct). `HasMixer`, `SetMaster/Music/Sfx/Group`, `LinearToDecibels`/`DecibelsToLinear`, `MaxLinear=1.5`, `ResetCache()`. The mixer asset is the parallel-Audio seam — may be absent → sliders persist but per-mixer audio unaffected (AudioService per-source fallback covers playback). WIRED, partially-live (mixer-dependent).

### PauseController.cs (re-verified from code 2026-07-13, WO-714 W8)
- File: `Assets/_Modules/Settings/PauseController.cs`
- Responsibility: pause overlay + `Time.timeScale` freeze (audit P0-10). Resume/Settings/Quit-to-Title; chrome Close = Resume.
- **CODE-BUILT since 2026-07-03 (WO-F, coverage row #47b): NO UIDocument/UXML, NO Esc handling.** Lazy kit modal (FrameOptions, sortingOrder 31500 — below Settings 32000) on first `Pause()`. Toggle arrives via `PauseGate.PauseToggleRequested` (the Core back/pause seam); `OnApplicationPause(true)` auto-pauses (mobile compliance), never auto-resumes.
- Public: `TogglePause()`, `Pause()`, `Resume()`, `IsPaused`, `AttachSettings(SettingsController)` (WO-714 W8 runtime wiring — the serialized `_settings` ref is scene-only), event `PauseStateChanged(bool)`. Settings button builds only if a settings screen is attached. Quit → restores timeScale FIRST then `SceneRouter.GoTitle()`. LIVE via `PauseHudBootstrap`.

### PauseHudBootstrap.cs (NEW 2026-07-13, WO-714 W8 — the routing that made Pause/Settings reachable)
- File: `Assets/_Modules/Settings/PauseHudBootstrap.cs`
- Responsibility: closes the "panels exist but nothing routes to them" gap (proved by grep: no scene carried either controller's GUID; `PauseGate.RequestBack()` had zero call sites). Contains 2 types:
  - `static PauseHudBootstrap` — **Bootstrap: `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` + sceneLoaded hook** (HelpMenuBootstrap pattern, global dedupe). Per gameplay scene spawns `PauseSettingsHost` = SettingsController + PauseController (wired via `AttachSettings`) + PauseHudButton. Skips front-end scenes (Title/HeroSelect/PetSelect/Intro/Splash/Loading).
  - `PauseHudButton : MonoBehaviour` — the on-screen pause chip (own uGUI canvas sort 90, 52px kit slot plate + gear icon, top-right edge at the retired MusicToggleHud's vacated spot right 14 / top 200; null-art fallback = two gold pause bars, glyph-proof). Tap → `PauseGate.RequestBack()`. Hides while `PanelManager.AnyOpen` (QuestTrackerHud pattern).
- WIRED + LIVE (no scene wiring needed).

### MusicToggleBootstrap.cs
- File: `Assets/_Modules/Settings/MusicToggleBootstrap.cs`
- Responsibility: always-visible ♪ on/off button installed into every scene (owner: "music toggle everywhere"). Contains 3 types:
  - `static MusicToggleBootstrap` — **Bootstrap: `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` → `Init()`** + sceneLoaded hook; borrows top-sorted UIDocument's PanelSettings, spawns MusicToggleHud (+50 sort).
  - `MusicToggleHud : MonoBehaviour [RequireComponent(UIDocument)]` — code-built button top-right (top 200, right 14), loads sprite `HudIcons/hud_music`. `Toggle()` drives SettingsModel + AudioServiceBridge.
  - `internal static AudioServiceBridge` — reflection bridge to `DeNelle.Audio.AudioService` (SetMuted / SetVolume(MixerGroup.Music)). No-ops if Audio absent.
- DORMANT since 2026-07-12: `ForceHudButton = false` keeps the HUD overlay retired (owner bug — it overlapped mobile controls); the affordance moved into Settings. `AudioServiceBridge` remains the live audio seam SettingsController uses.

### UXML/USS
- **NONE remain in this module (verified 2026-07-13).** SettingsScreen.uxml/.uss + PauseOverlay.uxml/.uss were retired with the 2026-07-03 code-built conversion and are deleted from the tree — the whole area now code-builds; the UXML-in-builds risk class is closed here (MASTER_CATALOG P1 #8 marked RESOLVED).

### README.md — current.

---

## 4. Onboarding mode + flow

### OnboardingMode.cs — ns `DeNelle.Core` (asmdef `DeNelle.Core`)
- File: `Assets/_Modules/Core/OnboardingMode.cs`
- Responsibility: fast-path vs full-tutorial switch for a new game (owner 2026-06-06: default = fast into battle). `static class`.
- `FullTutorial` (get/set, mirrors PlayerPrefs `onboarding.fullTutorial`), `FastPath` (= !FullTutorial), `ChooseFastPath()`, `ChooseFullTutorial()`. Lives in Core (only assembly both Onboarding + Village ref). WIRED + LIVE.
- Set by TitleController splash buttons; read by Village TutorialDirector/CompanionMeetingTrigger.

### OnboardingFlow.cs — ns `DeNelle.Onboarding` (asmdef `DeNelle.Onboarding`; refs Core, Data, Localization, UniTask, Timeline)
- File: `Assets/_Modules/Onboarding/OnboardingFlow.cs`
- Responsibility: first-run guided 6-beat coach-mark tutorial (audit P0-11). Flips `GameState.Onboarded` on finish/skip → stops cold-open replay.
- MonoBehaviour, `[RequireComponent(typeof(UIDocument))]`. Defines `enum TutorialGate { None, BuildTower, PlacePet }`.
- **Code-built fallback overlay** (`BuildOverlayInCode`) when UXML elements absent (UXML-renders-empty trap) — binds from TutorialOverlay.uxml first, falls back. sortingOrder 250 (DEF-153, above HUD).
- Beats use canon strings from en.json (`tutorial.steps.*`) via `CanonStrings.Locale`. 6 beats: Welcome / Heart / Force-field / Raise tower (BuildTower) / Wardens (PlacePet) / Hold the line.
- Public: `TryRun()` (gated by `ShouldRun` = `!Onboarded`), `Run()` (ungated, for Replay), `NotifyTowerBuilt()`, `NotifyPetPlaced()`, `IsRunning`, `HasFinished`, `BeatCount`. UnityEvents: `OpenBuildMenuRequested`, `BeginWaveRequested`, `TutorialClosed`. `Finish()` → `GameStateService.FinishOnboarding()`.
- Module-isolated: only Core + UnityEvents; gameplay wiring lives in OnboardingIntegrator. WIRED + LIVE.

### OnboardingIntegrator.cs — ns `DeNelle.Village` (asmdef `DeNelle.Village`)
- File: `Assets/_Modules/Village/OnboardingIntegrator.cs`
- Responsibility: WO-133 bridge wiring OnboardingFlow (resolved by **full-name reflection**, held as UnityEngine.Object) into BuildMenu/WaveManager/VillageController/PetDeployer.
- MonoBehaviour, `[DisallowMultipleComponent]`. `Start()`→`Wire()`. Attached at runtime by `VillageController.EnsureOnboardingIntegrator`.
- Seams: OpenBuildMenuRequested→BuildMenu.Open; BeginWaveRequested→WaveManager.BeginLoop().Forget(); TutorialClosed→VillageController.OnOnboardingClosed; BuildMenu.BuildingPlaced→NotifyTowerBuilt; PetDeployer pets-deployed (polled in Update, no per-pet event)→NotifyPetPlaced. All null-guarded; degrades silently if OnboardingFlow type absent. WIRED + LIVE.

### TitleController.cs — ns `DeNelle.Onboarding`
- File: `Assets/_Modules/Onboarding/TitleController.cs`
- Responsibility: Title scene orchestrator + the splash gate where OnboardingMode is set; the title landing IS the hero-select (code-built, no UXML dependency, WebGL-safe).
- MonoBehaviour, `[RequireComponent(typeof(UIDocument))]`. Splash buttons: **"Start New"→`OnboardingMode.ChooseFastPath()`** + build hero-select; **"Play Intro"→`OnboardingMode.ChooseFullTutorial()`** + `IntroLauncher.Play`; **"Continue"→`SceneRouter.GoCastle()`**.
- Heavy regression scar-tissue: DEF-253 watchdog, WebGL orphan re-assert, WO-335 NeutralizeOverlayPanels (shared OnboardingPanelSettings pick-stealing), VerifyFourCardsEven self-heal. Hero pick routes to PetSelect. Serialized `_splash` assigned but **never played** (bumper cut 2026-06-04 — see FLAGS). WIRED + LIVE.
- (Sibling onboarding files not deep-cataloged here: SplashLoading, StoryIntroController [`ShouldAutoPlay = !Onboarded`], PetSelectController, HeroSelectController, HeroCatalog, IntroPetCatalog, CanonStrings, TitleStarfield.)

### DifficultyTuning.cs — ns `DeNelle.Core.State`
- File: `Assets/_Modules/Core/State/DifficultyTuning.cs`
- Responsibility: single source of truth for what Difficulty changes — the between-wave countdown multiplier. `static class`.
- `CountdownMultiplier(Difficulty)`: Easy 2.0 / Normal 1.0 / Hard 0.6 (derived from 600/300/180 s targets). `Label()`, `Blurb()`, consts `NormalBuildWindowSeconds=300`/`Easy=600`/`Hard=180`. Read by SettingsController (labels) + WaveManager (multiplier). WIRED + LIVE.
- `enum Difficulty { Easy, Normal, Hard }` lives in `Assets/_Modules/Core/State/Enums.cs` (EnumMember "easy"/"normal"/"hard").

### OnboardingSceneBuilder.cs — ns `DeNelle.Editor` (editor-only)
- File: `Assets/Editor/OnboardingSceneBuilder.cs`
- Responsibility: Week-1 scene generator — `BuildAll()` creates Title (fully built) + 3 near-empty scenes, wires TitleController/SplashLoading/StoryIntroController **by reflection** (Editor asmdef can't ref Onboarding). Idempotent. Triggered manually / `-executeMethod`. Editor tool, not runtime.

---

## 5. The two grant paths (summary)

1. **DevPanel** (dev builds): `GiveCrystals`, `GiveBuildMaterials`→`EconomyService.GrantSpendable(50k wood,25k food,50k iron,25k crystals)` + Stone 50k + Magic 100, `GrantEntitlement`/`GrantAllPacks` (PackCatalog), `GiveWisdom`, `GiveHeroXp`/`LevelHero`/`SetHeroLevel`.
2. **AdminOverlay** (ships): single live "Load resources (full base)" button → reflection `EconomyService.GrantSpendable(50k,25k,50k,25k)`. Note: Wood/Iron must route through GrantSpendable to land in BOTH the in-session pool (shop/HUD) AND GameState ledger (upgrades) — both grant paths document this.

---

## FLAGS

### Stale-comment-vs-code
- ~~PauseController Esc header/body contradiction~~ **RESOLVED (verified 2026-07-13):** the current PauseController has NO Update()/Esc handling at all — the toggle rides `PauseGate.PauseToggleRequested` (keyboard-removal sweep), and the HUD-button caller landed as `PauseHudButton` (WO-714 W8).
- DevPanelController / DevBootstrap headers reference DevPanel.uxml as the UI source, but the code **explicitly does NOT use it** (code-built). The code comments self-correct this clearly, so it is documented-not-stale — but the class-doc summary line "drives DevPanel.uxml" wording could mislead a skim.
- AdminOverlay class header says actions "call through reflection so the HUD asmdef stays decoupled" — accurate. But the header's list of actions (waves, give crystals, reset) describes the **pre-2026-06-11 trim**; only Load-resources + Reset-Yarn are now wired (inline comment at ~143 documents the trim). Header is partially stale.
- AdminOverlay sortingOrder inline comment references an old "170" value that was raised to 2710 — documented, not a live bug.

### Dead / duplicate code
- **AdminOverlay dead handlers** (compiled, unreachable — no button binds them): `OnTriggerWave`, `OnGiveCrystals`, `OnSetOnboarded`, `OnSave`, `OnReset`, `BuildOrientRow` + `OnOrientAsset` + `OpenOrientMenu` (whole dev-orient-tool subsystem). Retained by owner decision "in case wanted back."
- **DevWalletProbe** has no in-tree consumer that constructs a WalletService over it; DevPanel only *sets* mock balances. The mock numbers are inert unless a wallet screen is manually built over the probe (documented seam, currently latent).
- **DevPanel.uxml / DevPanel.uss** — not loaded at runtime; dead-at-runtime editor reference.
- **TitleController `_splash`** SerializeField — assigned by builder but the studio bumper was cut (2026-06-04); `RunArrival` never plays it. Dead-but-wired field.
- DevPanelController static cheat flags `GodMode`/`InstantWinWave` + their Changed events — exposed for an integrator that does not appear to consume them in-tree (the integrator-note shows the intended `#if`-gated read pattern but no gameplay file reads them). Latent feature.

### Scene-gated / disabled
- DevTools whole assembly compiled out of release (`#if` + asmdef defineConstraints) — by design.
- AdminOverlay owner-wallet gate is permanently false (`OwnerWalletAddress = ""`) → only the Ctrl+Shift+A chord opens it.
- ~~SettingsController / PauseController only render if a scene/integrator places their UIDocument~~ **RESOLVED 2026-07-13 (WO-714 W8):** no UIDocument/PanelSettings involved anymore (code-built kit modals) and `PauseHudBootstrap` auto-installs both + the on-screen pause chip per gameplay scene — no scene wiring.
- MusicToggleBootstrap HUD overlay DORMANT since 2026-07-12 (owner bug: button overlapped mobile controls) behind `ForceHudButton = false`; the Music On/Off affordance lives in Settings (same SettingsModel + AudioServiceBridge seam). AudioServiceBridge itself stays live.
- AudioMixerBridge no-ops until the parallel Audio mixer asset exists at `Resources/Audio/GameAudioMixer` — sliders persist but don't drive the mixer (per-source AudioService fallback covers actual playback).

### Broken / contradictory
- ~~UXML-bound risk: SettingsScreen.uxml + PauseOverlay.uxml~~ **RESOLVED (verified from code 2026-07-13):** both UXML surfaces were deleted with the 2026-07-03 code-built conversion; the whole area now code-builds. See MASTER_CATALOG P1 #8 (marked RESOLVED).
