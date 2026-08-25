# Master Catalog — DevTools / Settings / Onboarding

Verified **from the actual code** (not comments) **2026-08-02**. Supersedes the 2026-06-12/2026-07-13 body wholesale.
Scope: `Assets/_Modules/DevTools/**`, `Assets/_Modules/Settings/**`, `Assets/_Modules/Onboarding/**`,
plus the owner/dev grant surfaces in `Assets/_Modules/HUD` (AdminOverlay, OwnerDevToolsOverlay) and the
cross-module onboarding trio (`Core/OnboardingMode.cs`, `Village/OnboardingIntegrator.cs`).

---

## 1. DevTools — asmdef `DeNelle.DevTools` (ns `DeNelle.DevTools`), 10 files

asmdef defineConstraints `UNITY_EDITOR || DEVELOPMENT_BUILD` → the whole assembly is absent from release
builds; every file ALSO wraps its body in `#if DEVELOPMENT_BUILD || UNITY_EDITOR` (belt + braces).
Module-isolation EXCEPTION by design: tooling may reference Core, Village, HUD, Wallet.

### DevPanelController.cs — the F1/corner-tap QA console (DEPRECATED-but-still-growing)
- **Header says DEPRECATED** (owner 2026-06-24: "F10 dev menu retired — use Settings → DevTools (AdminOverlay)";
  TAGGED FOR REMOVAL 2026-06-28) — `DevPanelController.cs:2-7` — yet it keeps receiving new entries (WO-826 Realm
  Map button, line 799). It is the *headless/dev door*, not dead. Treat "remove after confirming no tool is lost"
  as still unconfirmed.
- MonoBehaviour `[RequireComponent(UIDocument)]` (`:73-74`), UI **code-built** at runtime (UXML files are
  editor-reference only). Spawned by DevBootstrap on a DDOL host — zero per-scene wiring.
- Hotkey: F1 toggle is gated by the **global `FeatureFlags.DevHotkeys` kill-switch, default OFF**
  (`DevPanelController.cs:238-248`; flag at `Core/FeatureFlags.cs:~275`) — a key press can never pop it unless
  PlayerPrefs `ff.devhotkeys=1`. The on-screen path is `DevCornerTapGesture` (below) + the "DEV" chip.
- Action groups (verified `AddGroup` calls, `:699-828`): Resources (699) · City upgrades free/dev (722) ·
  Grant pack/entitlement (737) · Heart (743) · Waves & enemies (753) · Scene jump (763) · **Raids (dev)** (774) ·
  Mock wallet balance (783) · UI Kit demo (796) · **Realm Map (WO-826)** (802) · Cheats (810) ·
  **AutoPilot (QA bot)** (820) · **Animation (feel)** (828).
- **Open Realm Map entry (WO-826)** `:799-807`: reflection-free — `DeNelle.Core.UI.PanelRouter.Open(PanelId.RealmMap)`,
  the same route the HUD Map button uses (RealmMapPanel registers the opener); warns if no opener registered
  (no hero scene). This is the dev/headless door to the parchment overworld panel.
- Static cheat flags `GodMode`/`InstantWinWave` + events remain exposed.

### DevBootstrap.cs — DEV auto-spawner
- `static`, `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` → one DDOL "[DEV] QA Dev Console" GameObject with
  UIDocument (sortingOrder 9000) + `DevPanelController` (`DevBootstrap.cs:111`) + **`DevCornerTapGesture`**
  (`:116`) so the touch entry exists in every scene of a dev build. PanelSettings resolved Resources-first,
  adopt-sibling, else runtime-created. WIRED + LIVE (dev builds).

### DevCornerTapGesture.cs — keyboard-less console opener
- `[RequireComponent(DevPanelController)]`; **FIVE taps in a bottom-LEFT hotspot within ~3 s** toggles the console
  (`DevCornerTapGesture.cs:42-45`). Exists because F-keys don't exist on web/mobile and UITK synthetic clicks are
  unreliable in built WebGL players (header `:9-16`) — it polls raw touch/mouse and calls `Toggle()`. LIVE (dev builds).

### AutoPilotDriver.cs — the autonomous playtest bot
- Coroutine state machine that drives the game **through real public seams** (never fakes input / sets transforms);
  the always-on `BreakCaptureHarness` does the recording (break-log.jsonl); the bot writes only
  `autopilot-summary.json` (`AutoPilotDriver.cs:2-11`).
- Phases (`:21-31`): BootToGameplay (MainCastle_Hall) → ResolveHero → WalkToEachGate → OpenEachVendor →
  OpenEachHUDPanel → TriggerWave → AttemptExitCastle; plus WO-449 outpost walk-to/engage and WO-602 home-return
  round-trip legs. Every phase has a **REALTIME watchdog** (F8 sets timeScale=0; scaled waits would hang) and a
  global cap of **420 s** (`:70` — raised 240→300→420 for the WO-597 popup oracle + 2026-07-07 verification probes).
- Anti-warp guard: single-frame hero displacement > 3 m = WARP fail (`:86-89`). Quits on completion unless
  launched from the dev-panel button (`quitOnDone:false`).

### AutoPilotInstaller.cs — opt-in spawner
- `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`; spawns the driver **only** on `--autopilot` CLI arg or
  `AUTOPILOT` env var (`AutoPilotInstaller.cs:41-48`). Fleet support: `--seed=`, `--run=` (namespaced output),
  `--scene=` / `AUTOPILOT_SCENE` boot-scene override (`:58-68`). Inert in normal play. LIVE (headless fleet).

### AutoPilotProbes.cs — passive world-state assertion probes
- Rides alongside the driver; reports via `FlowTrace.Fail/Warn("[Flow:AutoTest]")`. FIVE probes
  (`AutoPilotProbes.cs:11-32`): unexpected-cross (raid scene loaded during normal traversal), coplanar-floor
  (z-fight), wall-clip (hero inside wall collider), dual-navmesh/stranded/link faults, seam-reachable (every
  SceneTransitionTrigger on-mesh + walkable). No-ops unless `_armed` by the driver.

### AutoPilotLogGuards.cs — UI-layer health guard (WO-452 §A)
- Watches for the duplicate-UIDocument / dead-panel class: >1 enabled UIDocument sharing one PanelSettings, and
  any Onboarding-PanelSettings document alive in a gameplay scene (the "dev tools dead after Yarn" detector)
  (`AutoPilotLogGuards.cs:10-19`). Armed only by the driver; emits `FlowTrace.Fail("BotUI",…)`, deduped per run.

### ClickableActuator.cs — "press every button" helper
- Static; actuates uGUI `Button` + UITK `Button` on the open surface, DENYLIST for destructive controls
  (Quit/Logout/Reset/Delete/Disconnect/Wallet), every click try/caught + per-surface cap (`ClickableActuator.cs:7-18`).

### DevWalletProbe.cs — mock IWalletProvider
- Wraps `StubWalletProvider`, overrides `GetBalance` with QA-settable statics (5 SOL / 250 USDC / 2000 SKR).
  Set from the DevPanel "Mock wallet balance" group; still no in-tree consumer builds a WalletService over it
  (latent seam, unchanged).

### KayKitAnimProof.cs — animation A/B proof harness
- Dev-panel "Animation (feel)" tool: spawns the KayKit Adventurers 2.0 Knight beside the Tripo hero, driven by
  the proven `Resources/Enemies/HumanoidEnemy` controller, walking a scripted square (`KayKitAnimProof.cs:7-16`).
  **Editor-only spawn** (loads via AssetDatabase — the gitignored pack is outside Resources); dev player builds
  warn + no-op (`:18-21`). Despawn removes every trace.

### DevPanel.uxml / DevPanel.uss — editor reference only, dead at runtime (unchanged).

---

## 2. Owner/dev grant surfaces in DeNelle.HUD (ship in release)

### AdminOverlay.cs — Ctrl+Shift+A owner overlay (GREW since 2026-06-11 trim)
- Still not `#if`-gated (ships); owner-wallet gate still permanently false (`OwnerWalletAddress = ""`,
  `AdminOverlay.cs:32`). The chord is now **also gated by `FeatureFlags.DevHotkeys` (default OFF)**
  (`:148-152`) — in a shipped build the overlay is unreachable by keyboard unless `ff.devhotkeys=1`; the Help
  menu "Dev tools" button remains a caller.
- **LIVE panel is no longer 3 buttons** — the 2026-06-11 trim was reversed piecemeal. Wired buttons (verified
  `scroll.Add(Button(...))`, `:223-251` + `:316`): Load resources (full base) · Set Level 5 / Level 10
  (+skill pts) · +25 / +100 Wisdom · Trigger next wave · VFX Parade · Seating Editor (gear) · Lock-on toggle ·
  **FULL RESET (new player — wipes + quits)** · Orient Asset (`:316`). Handlers `OnGiveCrystals`, `OnSetOnboarded`,
  `OnSave`, `OnReset`, `OnReplayTutorial` (`:605-898`) exist but are not bound to buttons — dead-but-retained.
- All gameplay calls still via `System.Reflection` (HUD asmdef isolation). `AdminOverlayBootstrap` autospawn
  unchanged (`[RuntimeInitializeOnLoadMethod]` + sceneLoaded, per-scene dedupe).

### OwnerDevToolsOverlay.cs — RELEASE-safe owner-gated mobile dev tools (NEW since old catalog)
- Owner directive 2026-07-01: every other dev surface is keyboard-driven and/or compile-stripped, so nothing
  reached the mobile release player. This is a touch-driven, **release-shipping** overlay that only builds its
  UI when the signed-in Pi username equals the owner's ("samanthadenelle", case-insensitive)
  (`OwnerDevToolsOverlay.cs:2-32`). Self-bootstraps via `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`, DDOL,
  fully try/caught. uGUI canvas sortingOrder 5500 (above Pi sign-in 5000), bottom-left toggle.
- Reaches Core directly (SceneRouter/GameStateService/FeatureFlags/DebuggingController) and Village singletons by
  reflection (EconomyService/HeroProgression/WisdomCurrencyService/WaveManager — the AdminOverlay idiom).
  Includes **FeatureFlags toggles** incl. `devhotkeys` (`:247`).

**THREE parallel dev surfaces now:** DevPanel (dev builds; F1-behind-kill-switch + 5-tap corner) ·
AdminOverlay (ships; chord-behind-kill-switch + Help menu) · OwnerDevToolsOverlay (ships; Pi-owner-gated touch).

---

## 3. Settings — asmdef `DeNelle.Settings` (ns `DeNelle.Settings`), 7 files

asmdef refs: **Core, UniTask, UnityEngine.UI, Unity.TextMeshPro** (`DeNelle.Settings.asmdef:4-9`) — no
Village/HUD/Audio; cross-module reach is reflection bridges. Whole area is **code-built kit uGUI** (all UXML
retired 2026-07-03; still true).

### SettingsModel.cs — static store + apply layer. LIVE.
- Persistence split (`SettingsModel.cs:8-21`): Music/SFX/Mute/Difficulty → GameStateService canonical `dotr-save`
  (`:109-173`; GameState 0..100, UI 0..1.5, scale const `:82`); Master/Quality/ScreenShake → PlayerPrefs
  `dotr-settings-*` (`:64-66`). `ScreenShakeSetting.Enabled` static for gameplay reads (`:334-342`).
- `ApplyAudio()` → AudioMixerBridge (mute collapses groups w/o disturbing sliders `:245-251`); `ApplyQuality()`
  → `SeekerBootstrap.ApplyTier` (`:258-261`); `ResetToDefaults()` sets Muted **false** — distinct from the
  fresh-save default true (`:278-291`). `QualityTier` SeekerLow/SeekerHigh/Desktop (`:46-54`).

### AudioMixerBridge.cs — linear→dB pusher onto exposed mixer params. LIVE code, **seam UNFULFILLED**.
- Targets exposed params `MasterVol/MusicVol/SfxVol` (`AudioMixerBridge.cs:41-45`); lazy Resources lookup
  `"Audio/GameAudioMixer"` (`:52`, `:157-176`), `MaxLinear=1.5` (`:55`), −80 dB floor (`:136-141`).
- **DRIFT (P1):** a mixer asset now EXISTS at `Assets\Audio\Resources\Audio\GameAudioMixer.mixer` (nested
  Resources folder — resolves at "Audio/GameAudioMixer") **but has `m_ExposedParameters: []`** — no
  MasterVol/MusicVol/SfxVol. `SetFloat` fails into the warn-once branch (`:111-123`): sliders persist but still
  do not drive the mixer. Playback volume works only via AudioServiceBridge's per-source fallback. Worse,
  `HasMixer` is now TRUE, so SettingsController's "mixer not wired" notice (`SettingsController.cs:348-349`) is
  **wrongly hidden**.

### SettingsBootstrap.cs — launch re-apply. LIVE.
- `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` (`SettingsBootstrap.cs:65-66`) → `SettingsModel.ApplyAll()`;
  subscribes `GameStateService.SettingsChanged`; DEF-22 reflection call
  `DeNelle.Audio.AudioService.Instance.ApplyPersistedSettings()` on boot + each change (`:101-131`). Deliberately
  AfterSceneLoad so the player's explicit tier overrides SeekerBootstrap's BeforeSceneLoad auto-pick (`:8-16`).

### SettingsController.cs — the options modal. LIVE via PauseHudBootstrap.
- Lazy code-built ObsidianModal `FrameSettings`, sortingOrder 32000 (`SettingsController.cs:150-153`). Sections
  (`:204-250`): Audio (Master/Music/SFX + Music On/Off + Mute-all) · Gameplay (Easy/Normal/Hard →
  `GameState.Difficulty`) · Graphics (3 tiers) · Comfort (screen shake) · Help (Game Guide WO-588 + Reset).
- 2026-07-30 audit rebuild: fixed px-ladder rows (`RequiredLadderPx = 1238f`, `:76`) inside a vertical
  ScrollRect (`BuildScrollHost` `:515-542`) so ClampMinTouch can't inflate rows over captions. Music On/Off
  drives live audio via AudioServiceBridge (`:412-432`). PanelManager battle-allowed (`:157-158`). Serialized
  `_audioMixer` never set (component is AddComponent-installed, `:95-101`) — the Resources path always applies.

### PauseController.cs — pause overlay + timeScale freeze. LIVE via PauseHudBootstrap.
- Kit modal `FrameOptions`, sortingOrder 31500 (`PauseController.cs:155-158`); Resume/Settings/Quit column
  (`:170-177`). Pause captures + zeroes `Time.timeScale`, Resume restores the CAPTURED scale (`:62`, `:200-221`).
  `OnApplicationPause(true)` auto-pauses only when `PauseGate.ExternalPresentationActive` is false;
  native rewarded-ad presentation preserves its existing caller instead of opening Pause over it. Toggle rides Core `PauseGate.PauseToggleRequested`
  (`:76-77`); PanelManager battle-allowed "Pause" (`:181-182`). `AttachSettings()` runtime wiring (`:102-112`);
  Settings opens over pause (pause yields its arbiter slot first, `:239-248`). Quit restores timeScale then
  `SceneRouter.GoTitle()` (`:259-271`).

### PauseHudBootstrap.cs — the per-scene installer. LIVE.
- `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` + sceneLoaded (`PauseHudBootstrap.cs:47-55`); per gameplay
  scene spawns `PauseSettingsHost` = SettingsController + PauseController wired via AttachSettings (`:89-93`);
  global dedupe for additive streaming (`:77-87`); skips Title/HeroSelect/PetSelect/*Intro*/*Splash*/*Loading*
  (`:58-66`).
- **DRIFT: the on-screen `PauseHudButton` chip is CULLED** (owner cosmetic flag A, 2026-07-24) — SpawnInScene no
  longer adds it (`:94-99`); the pause door is the "Pause" tab in HudKitController's left gold gear dock (same
  `PauseGate.RequestBack()` caller). The `PauseHudButton` class remains in-file, unreferenced — DORMANT (`:112-225`).

### MusicToggleBootstrap.cs — DORMANT bootstrap, LIVE bridge.
- `ForceHudButton = false` (`MusicToggleBootstrap.cs:47`) → `Init()` returns immediately (`:52`); the HUD ♪
  button stays retired (2026-07-12; the affordance is Settings' Music On/Off row). `MusicToggleHud` retained
  (`:92-170`). `AudioServiceBridge` (internal static, `:186-251`) is the **live** reflection seam to
  `DeNelle.Audio.AudioService` (`Instance`, `SetMuted`, `SetVolume(MixerGroup.Music)`) — what makes Settings'
  music toggle audible despite the param-less mixer.

### UXML/USS — none in this module (unchanged since the 2026-07-03 conversion).

---

## 4. Onboarding — asmdef `DeNelle.Onboarding` (ns `DeNelle.Onboarding`), 14 files

asmdef refs (`DeNelle.Onboarding.asmdef:4-13`): Core, Data, Unity.Localization, UniTask, Unity.Timeline,
UnityEngine.UI, Unity.TextMeshPro, **GoogleSignIn (NEW, WO-769)**.

### FoundingChoiceController.cs — Build-Your-Own vs Default Town (WO-748). LIVE.
- Fires ONCE at founding, before first hub entry. Gate `ShouldOffer` (`FoundingChoiceController.cs:80-92`):
  `!Onboarded` AND empty `BaseLayout` + session latch.
- **Entry chain (WO-769):** `PresentOrContinue(onContinue)` routes through
  `LoginPanelController.PresentOrContinue` (login-or-guest) FIRST, then the founding choice (`:102-110`).
  Callers: `HeroSelectController.OnDiveVillageClicked` (the DEFAULT `BypassPetSelect` path — FTUE-01 fix,
  `HeroSelectController.cs:644-657`) and `PetSelectController.OnEnable` belt-and-braces
  (`PetSelectController.cs:125-131`); both continue into `SceneRouter.GoCastle`.
- **"Default Town"** (`:214-251`): Core-only write — `State.StrategicPlacementMigrated = false` + Save inside
  `Guard.Try` (`:234-238`); the Castle-load one-shot `StrategicPlacementMigration.RunIfNeeded` then converts the
  live baked ring into movable BaseLayout records. Free — does not touch FreeBuildsUsed (`:208-213`).
- **"Build Your Own"** (`:253-259`): a literal no-op (leaves `StrategicPlacementMigrated = true`), just
  `Continue()` → `LoadingOverlay.Show("Founding your town…")` → GoCastle (`:261-279`). Forced choice: Close
  hidden (`:160-163`); PanelManager close-delegate = Continue (`:199-201`).
- **WO-834 interaction (verified):** NO WO-834 code in this module — grep clean. WO-834
  (`WorkOrders/WORK_ORDER_834_blank_town_baked_standdown.md`, IMPLEMENTED 2026-08-02) fixed the downstream bug
  (Build-Your-Own still showed the furnished baked town) in the BuildMode/save silo: persisted
  `everBuiltStructureIds` (save v35→v36) + `StructureSingleton.MayBakedTwinSurface(id, everBuilt, migrated)` —
  `StrategicPlacementMigrated == false` still surfaces everything (covers Default Town); otherwise only
  ever-built ids surface. FoundingChoiceController deliberately unchanged (no new founding flag).

### HeroSelectController.cs — the hero pick. LIVE.
- Code-built kit uGUI on `FrameCore`, forced flow (`HeroSelectController.cs:194-198`): left class column /
  center portrait stage / right specs (lore, pips, signature, Q-F-E-R) / green "Enter Elarion" CTA (`:227-247`).
- V1: only Knight (Grom) playable (`:107`); other classes are locked previews ("Coming soon" scrim + disabled
  CTA, `:317-341`, `:414-432`, `:603-609`). Returning player with a persisted HeroClass self-skips → GoCastle
  (`:113-124`). Confirm (`:637-661`): persist hero → `FeatureFlags.BypassPetSelect` (default ON) →
  **`FoundingChoiceController.PresentOrContinue(SceneRouter.GoCastle)`**; flag OFF → GoPetSelect. Portraits from
  `Resources/HeroPortraits/<slug>` (Thrain/Grom/Sylas/Elara, `:687-745`).

### HeroCatalog.cs — static presentation catalog. **FOUR heroes** now — Mage, Knight, Ranger, **Cleric (Elara)**
  (`HeroCatalog.cs:122-171`); the file header still says "three heroes" — comment lies.

### TitleController.cs — title screen. LIVE.
- Fully code-built uGUI (WO-C); legacy UIDocuments hard-disabled in Awake (`TitleController.cs:100-103`,
  `:143-165`). Title art `Resources/Title/Title_L|Title_H` with text fallback (`:186-245`).
- Bottom row (`:270-311`): **Continue** (only when a save exists; persists Knight if class missing → GoCastle,
  `:283-287`, `:412-429`) · **Start New** → `ResetToNewGame()` + `DialogueResetService.ResetForNewGame()` +
  `OnboardingMode.ChooseFastPath()` → GoHeroSelect (`:350-371`) · **Play Intro** →
  `OnboardingMode.ChooseFullTutorial()` + clears HeroClass → `IntroLauncher.Play` (9-screen cinematic) /
  StoryIntro cold-open fallback (`:376-409`). DEF-253 8 s unscaled watchdog (`:96-98`, `:499-509`); "Powered
  with SKR" badge behind `ff.skrpreview` (`:318-334`); spawns TitleStarfield (`:105-112`).

### Tutorial V2 hooks — NONE in this module (verified by grep).
Tutorial V2 lives in Core + Village: `FeatureFlags.TutorialV2` **default ON** (`Core/FeatureFlags.cs:462`),
signals/model in `Core/Tutorial/` (TutorialSignals.cs, TutorialStepModel.cs — WO-T1), interpreter
`Village/Tutorial/V2/TutorialFlow.cs` (self-bootstraps on hub scenes when ON, `:50`), legacy
`Village/Tutorial/TutorialDirector.cs` stands down while ON (`:128-134`),
`Village/NPCs/SylasStewardInjector.cs` gated `TutorialV2 && !Onboarded` (`:82`, `:141`). Onboarding's only
touchpoints: TitleController sets OnboardingMode; TutorialDirector suppresses legacy OnboardingFlow by reflection
(`TutorialDirector.cs:689`).

### Cross-module trio — all intact (re-verified)
- `Core/OnboardingMode.cs` — static `FullTutorial` ↔ PlayerPrefs `onboarding.fullTutorial` (`:32`, `:44-62`),
  `ChooseFastPath()/ChooseFullTutorial()` (`:68-71`); default fast path.
- `Onboarding/OnboardingFlow.cs` — 6-beat coach-mark (Welcome/Heart/Force-field/BuildTower/PlacePet/Wave1,
  `:199-210`) gated `!Onboarded` (`ShouldRun`, `:383-394`); code-built kit card, legacy TutorialOverlay.uxml
  UIDocument disabled in Awake (`:219-232`); finish/skip → `GameStateService.FinishOnboarding()`.
  **Now the TutorialV2-OFF fallback path**, not the live FTUE.
- `Village/OnboardingIntegrator.cs` — full-name-reflection bridge, five seams (OpenBuildMenu→BuildMenu.Open,
  BeginWave→WaveManager.BeginLoop, TutorialClosed→VillageController, BuildingPlaced→NotifyTowerBuilt,
  PetDeployer poll→NotifyPetPlaced), attached by `VillageController.EnsureOnboardingIntegrator`
  (`:24-26`, `:46-50`, `:72-83`). Unchanged.

### Remaining files (terse)
- **LoginPanelController.cs (NEW, WO-769)** — Obsidian email/password sign-in + "Play as Guest"; presentation over
  `Core.Auth.FirebaseAuthService`; `PresentOrContinue` self-skips when signed in (`:72-89`); guest = existing
  device-hash id. Sits in front of the founding choice at the new-game chokepoint.
- **LoginViewModel.cs (NEW, WO-769)** — pure-C# VM over FirebaseAuthService; success binds Firebase UID as save
  player-id via `GameStateService.BindWallet` (`:8-11`); GoogleSignIn `#if UNITY_ANDROID || UNITY_EDITOR` (`:18-23`).
- **PetSelectController.cs** — 3-starter-Warden pick; still UIDocument/UITK-hosted (`:51`); **effectively DORMANT**:
  `BypassPetSelect` ON routes OnEnable straight to founding-choice→GoCastle (`:125-131`); self-skips when a pet
  is already chosen (`:136-140`). Writes `GameState.StarterPetId` + Save when it does run.
- **StoryIntroController.cs** — Stone Choir cold-open, code-built; `ShouldAutoPlay = !Onboarded` (`:121-130`);
  tap-advance with 1.25 s grace + Skip (CTS cancel); `ForceHide()` used by the title watchdog.
- **SplashLoading.cs** — bumper **video permanently OFF** (`_playBumperVideo = false`, `:49` — the clip crashes the
  Windows player decoder); static "DeNelle Studios presents" card from canon key `publisher`.
- **CanonStrings.cs** — lazy loader of `StreamingAssets/Data/Canonical/canon-strings.json` + `en.json`; unknown
  key renders `[[missing:key]]` (`:16-17`); gameTitle "Echoes of Elarion" (`:46-49`).
- **IntroPetCatalog.cs** — display-subset pets.json reader so PetSelect needs no DeNelle.Pets ref (`:4-20`).
- **TitleStarfield.cs** — runtime ~240-star ParticleSystem; comets removed WO-451 (`:30-31`).
- **OnboardingPanelGuard.cs** — `[RuntimeInitializeOnLoadMethod]` + sceneLoaded: disables any
  OnboardingPanelSettings-bound UIDocument in non-onboarding scenes (the dev-tools-dead-after-Yarn *preventer*,
  `:21-32`). LIVE. (DevTools' AutoPilotLogGuards is the matching *detector*.)
- **OnboardingSceneBuilder.cs** (`Assets/Editor/`) — editor-only Week-1 scene generator, reflection-wired. Unchanged.

### DifficultyTuning.cs — `Core/State/DifficultyTuning.cs`, unchanged: countdown multiplier Easy 2.0 / Normal 1.0 /
Hard 0.6; read by SettingsController (labels/blurbs) + WaveManager.

---

## 5. The grant paths (summary, 2026-08-02)

1. **DevPanel** (dev builds): resources/upgrades/entitlements/waves/scene-jump/raids/mock-wallet/cheats + AutoPilot
   launcher + Realm Map opener + KayKit anim proof.
2. **AdminOverlay** (ships, chord+Help-menu, DevHotkeys-gated): load-resources, level/wisdom sets, trigger wave,
   VFX parade, seating editor, lock-on, FULL RESET, orient-asset.
3. **OwnerDevToolsOverlay** (ships, Pi-owner-gated, touch): Core-direct + Village-reflection grants and flag toggles —
   the only dev surface reachable on a keyboard-less release build.

---

## FLAGS (risk ledger)

### Broken / misleading
- **P1 — Mixer seam unfulfilled + notice wrongly hidden:** `GameAudioMixer.mixer` exists (nested Resources at
  `Assets\Audio\Resources\Audio\`) with **zero exposed parameters** (`m_ExposedParameters: []`); AudioMixerBridge's
  warn-once branches fire and the Settings "mixer not wired" notice is hidden because `HasMixer` is now true.
  Expose MasterVol/MusicVol/SfxVol or key the notice on a param-verify.
- HeroCatalog header says three heroes; code ships four (Cleric added). Comment lies — trust `:122-171`.
- DevPanelController header block says DEPRECATED/TAGGED FOR REMOVAL, yet WO-826 added the Realm Map entry to it —
  it is the live dev/headless door; the removal note is stale intent.
- AdminOverlay's "trimmed to 3 buttons" history (old catalog) is obsolete — ~11 buttons are live again incl.
  FULL RESET; the un-bound handlers list also changed. Trust `:223-251`.

### Dead / dormant
- `PauseHudButton` (culled 2026-07-24, class retained) · `MusicToggleHud` (retired 2026-07-12,
  `ForceHudButton=false`) · AdminOverlay unbound handlers (`OnGiveCrystals/OnSetOnboarded/OnSave/OnReset/
  OnReplayTutorial`) · DevPanel.uxml/.uss (runtime-dead) · DevWalletProbe (no consumer builds a WalletService
  over it) · `PetSelectController` (BypassPetSelect default ON) · `OnboardingFlow` 6-beat (TutorialV2 default ON
  supersedes it) · SplashLoading bumper video (`_playBumperVideo=false`).

### Gating quick-reference
- `ff.devhotkeys` default OFF kills F1 (DevPanel), F12 (DebugCanvas), Ctrl+Shift+A (AdminOverlay), test spawners,
  jukebox J — everywhere incl. editor. On-screen doors remain: 5-tap corner (dev builds), Help-menu Dev tools,
  OwnerDevToolsOverlay (owner Pi account).
- `FeatureFlags.BypassPetSelect` default ON — PetSelect skipped; founding choice fires on the HeroSelect path,
  behind the WO-769 login-or-guest gate.
- `FeatureFlags.TutorialV2` default ON — Village TutorialFlow owns FTUE; legacy OnboardingFlow/TutorialDirector
  stand down.
- DevTools assembly compiled out of release (asmdef constraint + `#if`) — by design.
- AdminOverlay owner-wallet gate permanently false (`OwnerWalletAddress = ""`).
