# MASTER CATALOG — Area: HUD (`DeNelle.HUD`)

Scope: `Assets/_Modules/HUD/*` (asmdef `DeNelle.HUD`) plus the cross-asmdef HUD
seams: the `*HudBridge` family in `DeNelle.Village`, `FloatingHealthBar`
(Village), `PanelManager` (Core), `BreakCaptureHarness` (Core).

**Asmdef law:** `DeNelle.HUD` → `DeNelle.Core`, `DeNelle.Data`, Unity.Localization,
UniTask, UnityEngine.UI, TMPro, LeanTouch/LeanCommon/CW.Common. **Never references
`DeNelle.Village` / `DeNelle.BattleATB`.** Village→HUD pushes are either through
`IVillageHud` (interface in Core, via `CoreServices.Hud`) or via reflection-by-name
on the concrete `VillageHudController` for the "extra" setters not on the interface.

Verified against source 2026-06-12 (every file read, not summarized from comments).

---

## CODE — `Assets/_Modules/HUD/` (asmdef `DeNelle.HUD`, ns `DeNelle.HUD`)

### VillageHudController.cs  (2609 lines — the central HUD)
- **Responsibility:** the single code-built uGUI combat/town HUD. Implements
  `DeNelle.Core.HUD.IVillageHud`; registers via `CoreServices.RegisterHud(this)`
  in Awake, unregisters OnDestroy. Sleek/minimal, context-aware (village vs open
  world), three canvases.
- **MonoBehaviour lifecycle:** `Start()` calls `Build()` (try/catch wrapped — a
  build exception must never blank the HUD or halt the WebGL player) then
  `ApplyResponsiveLayout`/`ApplyContext`. `Update()` polls screen-size change,
  context (0.35s), `H` key toggles combat HUD, animates momentum/lookout-bell,
  `UpdateTownHud`, `UpdateHeroXpLine`.
- **Three nested canvases** (all under a `SafeArea` inset root):
  - base `VillageHUD` canvas sortingOrder 100 — always-on chrome (resource strip,
    castle/Heart banner, party frames, runic border, top chrome, town actions).
  - `BattleHUD` canvas sortingOrder 150 — combat clusters; faded via `BattleHudGroup`.
  - `TownHUD` canvas sortingOrder 140 — idle town clusters; faded via `TownHudGroup`.
- **Public read props:** `CanvasGroup BattleHudGroup`, `CanvasGroup TownHudGroup`,
  `bool InVillage` (=`_inVillage || _villageOnlyForced`). All three consumed by
  BattleHudVisibilityManager.
- **Public UnityEvents (read by Village bridges via reflection):** `BuildRequested`,
  `SkillsRequested`, `ShopRequested`, `TalkRequested`, `InventoryRequested`,
  `QuestsRequested`, `IntelRequested`, `AbilityRequested` (`UnityEvent<int>`),
  `RepairConfirmRequested`, `RepairCancelRequested`, `StartWaveRequested`.
- **IVillageHud setters (interface; pushed by bridges via `CoreServices.Hud`):**
  `SetWave(int)`, `SetCountdown(float)`, `SetHeartHp(float,float)`, `SetCrystals(int)`,
  `SetResources(int wood,int iron,int food,int gems)`, `SetAttackDirections(...)`
  *(empty body — compass is the separate CompassHud)*, `SetWaveImminent(bool)`,
  `ShowWaveClearBanner(int,int,string)`, `HideWaveClearBanner()`,
  `ShowRepairPrompt(string,float)`, `HideRepairPrompt()`, `SetForgettingLevel(float)`,
  `SetWardsReadout(...)` *(empty body — surfaced elsewhere)*, `SetComboCount(int)`,
  `SetKillStreak(int)`, `SetEnemyCount(int,int)`, `SetMana(float,float)`,
  `SetHeroHp(float,float)`, `SetAbilityCooldown(int,float,float)`,
  `SetAbilitySlot(...)` (4-arg + 6-arg w/ accentHex), `SetPartyMember(int,string,float,float)`,
  `SetPartyMemberVisible(int,bool)`.
- **Extra setters NOT on IVillageHud (resolved by reflection name from bridges):**
  `SetStartWaveAvailable(bool)`, `SetVillageContextForced(bool)`,
  `SetCombatHudVisible(bool)`, `SetHudVisible(bool)`, `SetWaveProgress(int,int)`,
  `SetLookoutStatus(int)`, `SetTownMetrics(float,int,int,int)`,
  `SetPassiveXp(int,int)`, `SetPassiveXpVisible(bool)`,
  `SetMinimapPoi(string,float,float)`, `ClearMinimapPois()`, `SetTalkAvailable(bool)`.
- **Reflection into Village (HUD→Core rule kept):** polls `DeNelle.Village.HeroProgression`
  (`Xp`/`XpToNext`) for the XP line; `DeNelle.Village.WaveManager` (`CountdownRemaining`,
  `Phase`, `CurrentWaveId`) as a wave-timer fallback; `DeNelle.Village.HeroLocomotion`
  for the hero transform (context radial test). All in try/catch, re-resolve on null.
- **Context model:** "in village" = active scene `Village2` AND hero within
  `TownRadius=60` of world origin (hysteresis 8). Past that = open world → village-only
  chrome hides. OuterWorld loads additively over Village2.
- **Art:** sprite-FIRST widget icons — `Resources/HudIcons/<name>` custom → `RpgUiCatalog`
  pack → `HudIcons/hud_widgets_sheet` sliced sheet → code-drawn glyph fallback. All
  Resources-only (WebGL-safe). Per-class ability icons via `AbilityIconForClassSlot`.
- **Light-parchment restyle:** local `L*` palette constants; text driven off dark
  ink (`LInk`/`LInkDim`) because the shared cream token is invisible on parchment.
- **Status:** WIRED + LIVE. The canonical HUD; everything routes here.

### VillageHudBootstrap.cs
- **Responsibility:** static guarantor that exactly one VillageHudController exists in
  every *gameplay* scene. `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` →
  subscribes `sceneLoaded`, calls `EnsureHudForScene`.
- Skips a `MenuScenes` allowlist (Title, HeroSelect, PetSelect, Intro, IntroFlow,
  Store, PackStore, Boot, Bootstrap, MainMenu, GameOver). Idempotent
  (`FindObjectOfType` guard). Spawns a host parented to the just-loaded scene (no DDOL).
- **Why:** village scene-builders author the HUD; full-load battle/dungeon scenes don't,
  so the bootstrap covers DTT/Arena/ATB/Dungeon automatically.
- **Status:** WIRED + LIVE.

### HelpMenu.cs  (+ HelpMenuBootstrap.cs)
- **Responsibility:** in-game settings/help overlay (the TOWN-HUD gear opens it via
  `HelpMenu.Instance.ToggleOverlay()`). Buttons: Report a bug, Controls, Reset Hero & Pet,
  Dev tools (dev builds only), Credits, Close. Singleton (`Instance`).
- **UI:** `UIDocument` but the tree is **code-built** off `rootVisualElement` (NO UXML
  asset) — the supported UIToolkit path. Borrows a PanelSettings from any existing
  scene UIDocument; **disables itself if none found** (logs warning). Explicit
  `LegacyRuntime.ttf` font (WO-417) so rows aren't blank. `sortingOrder=2700` (top-most).
- **PanelManager:** registers `PanelManager.Register("Help", Close, () => IsOpen)`;
  open/close call `NotifyOpened`/`NotifyClosed` — single-modal discipline.
- **Bug report:** POSTs to Vercel `…/api/bug-report` + opens a `mailto:` to
  `samanthadenelle@gmail.com` with auto-captured scene/build/device context + a saved
  screenshot path. Reset routes via reflection to `GameStateService.ResetToNewGame` +
  `SceneRouter.GoHeroSelect`. Dev tools opens `AdminOverlay`.
- **Bootstrap:** `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`, scene-scoped, idempotent.
- **Status:** WIRED + LIVE.

### AdminOverlay.cs  (+ AdminOverlayBootstrap.cs)
- **Responsibility:** owner-only debug overlay. Toggle via chord **Ctrl+Shift+A**, or
  from HelpMenu "Dev tools". Trimmed (2026-06-11) to two live buttons: "Load resources
  (full base)" + "Reset Yarn (replay tutorial)" + Close.
- **UI:** code-built `UIDocument` (no UXML); borrows PanelSettings; `sortingOrder=2710`
  (just above HelpMenu 2700). DANGER-red rimmed stone card (ElarionUi).
- **Reflection (HUD→Core asmdef kept):** all actions reflect into Village/Core — e.g.
  `EconomyService.GrantSpendable(int,int,int,int)` for the resource grant (mirrors
  Wood/Iron into GameState so the upgrade flow can spend them), `SceneRouter.GoHeroSelect`,
  PlayerPrefs FTUE-gate clears.
- **PanelManager:** `Register("Admin", Close, () => IsOpen)` + NotifyOpened/Closed.
- **Public API:** `Toggle()`, `Open()`, `Close()`, `IsOpen`.
- **Dead/retained handlers (in-file, NOT on the panel):** `OnTriggerWave`,
  `OnGiveCrystals`, `OnSetOnboarded`, `OnSave`, `OnReset`, `BuildOrientRow`/`OnOrientAsset`,
  `IsAuthorised`/owner-wallet gate (`OwnerWalletAddress` is `""` → gate never passes;
  chord is the only entry). Kept "in case wanted back."
- **Bootstrap:** AfterSceneLoad, scene-scoped, idempotent.
- **Status:** WIRED + LIVE (chord entry). Wallet auth-gate inert by design.

### CompassHud.cs  (+ CompassHudBootstrap.cs)
- **Responsibility:** top-centre NSEW heading strip + red off-screen-enemy arrow pips
  around the screen edge. `Hero` transform + `Targets` list set by the bootstrap.
- **UI:** **pure code-built uGUI** (own ScreenSpaceOverlay Canvas, sortingOrder 96, no
  GraphicRaycaster). WO-322 re-fix replaced the old UIDocument/UXML version (which
  didn't render in builds and couldn't find a PanelSettings). LateUpdate updates heading
  + projects arrows; arrow math converts real px → CanvasScaler reference px.
- **Bootstrap:** AfterSceneLoad. Spawns ONLY when a hero exists (`FindHero` reflects
  `DeNelle.Village.HeroLocomotion`) — so no compass on Title/HeroSelect. Also adds
  `EnemyTargetTicker` (sibling class) that refreshes `Targets` at 2 Hz by reflecting
  `DeNelle.Village.Enemy`.
- **Status:** WIRED + LIVE.
- **FLAG (stale comment):** both files' headers still narrate the old UIDocument/
  PanelSettings design ("no longer needs a UIDocument") — comments reference a
  mechanism the code no longer uses; current code is uGUI-only. Not a bug, but the
  `UIDocument` token in those files is comment-only.

### BattleHudVisibilityManager.cs
- **Responsibility:** the single HUD-MODE manager (WO-337 + WO-339). Cross-fades (0.6s)
  three modes across the Town/Battle CanvasGroups: **BATTLE** (wave active OR ATB
  BattleController live), **TOWN** (village idle, no combat), **HIDDEN** (exploration /
  non-village — both groups out, base chrome+compass remain).
- **Singleton + bootstrap:** `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` spawns a
  **DontDestroyOnLoad** host. Awake dedup uses `Destroy(this)` (not `Destroy(gameObject)`)
  — correct per the singleton-dedup-destroys-host lesson.
- **Reflection (HUD→Core kept):** resolves `DeNelle.Village.WaveManager.Phase` + binds
  its `OnWaveStarted/OnWaveCleared/OnBreach/OnDefeat` UnityEvents; resolves
  `DeNelle.BattleATB.BattleController`. Re-resolves on scene load + 0.5s poll. All
  try/catch (WebGL-safe). Reads `VillageHudController.InVillage` (shared context, no dup test).
- **Status:** WIRED + LIVE.

### VirtualDPadLean.cs
- **Responsibility:** Lean.Touch virtual D-pad (radius-constrained knob) outputting a
  static `Vector2 Move` for HeroLocomotion to poll (reflection, no asm cycle). Code-built
  disc visuals. `OnFingerDown/Set/Up`, `PositionAt`.
- **Bootstrap:** has a `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)] Bootstrap()` that
  is intentionally a **NO-OP** ("HUDManager will own and attach this").
- **Status:** DEAD / ORPHANED. Its owner `HUDManager` **does not exist** (see FLAGS).
  Nothing in-scope constructs or attaches a VirtualDPadLean. The live D-pad/joystick used
  by HeroLocomotion is the Village `VirtualJoystick`, not this.

### HudTheme.cs  — `static class HudTheme`
- **Responsibility:** sleek/minimal Elarion HUD skin tokens + helpers (colours
  `Gold/HpRed/CastleGold/Lookout*`, `StylePanel`, `StyleWell`, `AddRim`, `Disc`, etc.)
  consumed throughout VillageHudController & FloatingHealthBar. Status: LIVE.

### XPBarController.cs
- Persistent bottom XP strip. Subscribes `HeroProgression` events via reflection.
  `[RuntimeInitializeOnLoadMethod]`. Status: LIVE (progression).

### FloatingXpText.cs
- Pops "+N XP" above the hero on XP gain. Self-bootstrap `[RuntimeInitializeOnLoadMethod]`,
  reflects `HeroProgression.OnXpChanged`. Status: LIVE.

### PlayerProgressPanel.cs
- Gear-screen progression-detail overlay (UIDocument, code-built). Reads HeroProgression
  by reflection on open. Status: LIVE (on-demand popup).

### QuestTrackerHud.cs (+ Bootstrap)
- Top-LEFT active story-quest list (objective text). Reads `QuestService`/`QuestCatalog`,
  repaints on `QuestChanged`. Code-built UIElements. Status: LIVE.

### DailyQuestHud.cs (+ Bootstrap)
- Top-RIGHT stack of 3 daily-quest chips. Reads `DailyQuestService.Today.Quests`. Singleton
  (`Instance.Toggle()`); spawned only once a scene has a hero. No claim/reward flow yet
  (follow-up). Status: LIVE (partial — display only).

### AttentionGlowUi.cs
- Reusable "chasing comet" attention cue around a RectTransform border (`Attach`). Used for
  the Talk button + tutorial focusing. Status: LIVE helper.

### PetUnlockTracker.cs
- Runtime state for unlocked pet skills + pet levels; PlayerPrefs `dotr-pet-unlocks-v1`.
  Singleton bootstrapped via `[RuntimeInitializeOnLoadMethod]`. Reaches PetSkillTreeCatalog.
  Status: LIVE.

### On-demand popup panels (each `XPanel.cs` + `XPanelBootstrap.cs`, hotkey-toggled)
- **ClanChatPanel** — team-chat stub, **Y** key. Reads `ClanService` directly (Core ref, no
  reflection). UIDocument code-built.
- **CosmeticShopPanel** — cosmetic shop, **C** key. Reflection bridge into `DeNelle.Cosmetics`.
  UIToolkit.
- **HeroTalentPanel** — 3 hero talent trees, **T** key. Reflects `DeNelle.Village.Talents`
  catalog + `WisdomCurrencyService`. UIDocument bottom-sheet.
- **PetSkillTreePanel** — 3-pet tabbed unlock surface, **P** key. UIToolkit.
- **LeaderboardPanel** — leaderboard + profile, **L** key (WO-129). Reads `LeaderboardService`
  (Core ref). Code-built UIToolkit (no UXML).
- All bootstraps are `[RuntimeInitializeOnLoadMethod]`. Status: LIVE (popups).

### DeNelle.HUD.asmdef
- Refs: DeNelle.Core, DeNelle.Data, Unity.Localization, UniTask, UnityEngine.UI,
  Unity.TextMeshPro, LeanTouch, LeanCommon, CW.Common. **No Village/BattleATB.** Correct.

---

## CROSS-ASMDEF SEAMS

### PanelManager.cs — `Assets/_Modules/Core/UI/` (asmdef `DeNelle.Core`, ns `DeNelle.Core.UI`)
- **Responsibility (DEF-212):** the single modal arbiter — at most one registered panel
  open at a time. Pure static state (no MonoBehaviour/scene object; survives additive
  loads, resets on domain reload).
- **API:** `PanelHandle Register(name, Action close, Func<bool> isOpen)`,
  `NotifyOpened(handle)` (closes the previously-open panel), `NotifyClosed(handle)`,
  `CloseOpen()`, `bool AnyOpen`, `string OpenPanelName`, `event OpenStateChanged`.
- **HUD consumers:** HelpMenu + AdminOverlay (and the Village/cosmetics panels). Used by
  e.g. MobileInteractButton to suppress world prompts while a modal owns the screen.
- **Status:** LIVE. This IS the "PanelManager modal discipline" the HUD panels obey.

### BreakCaptureHarness.cs — `Assets/_Modules/Core/Diagnostics/` (asmdef `DeNelle.Core`, ns `DeNelle.Core.Diagnostics`)
- **Responsibility:** always-on playtest "flight recorder." Captures errors/exceptions,
  possible softlocks (no hero movement+no progress event 75s), scene transitions; owner
  **F8** = screenshot + freeze + typed note. Writes `break-log.jsonl` + PNGs to
  persistentDataPath, a `[BREAK]` console line, and `EventTracker.Track("playtest_break")`.
- **Bootstrap:** `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`, DDOL host,
  `HideAndDontSave`. **Disabled on WebGLPlayer** (sandboxed FS). Every path try/caught;
  log handler reentrancy-guarded; screenshots throttled (25/session); errors deduped.
- **Status:** LIVE (editor + desktop only). Not strictly HUD but a diagnostic overlay
  (OnGUI note box). In-scope per request; no stale-comment issues found.

### FloatingHealthBar.cs — `Assets/_Modules/Village/Combat/` (asmdef `DeNelle.Village`, ns `DeNelle.Village`)
- **Responsibility (WO-178):** code-built **world-space** uGUI HP bar over a combat unit
  (Enemy/Hero). Type-agnostic via two delegates (`Func<float> fraction`, `Func<bool> isDead`).
  Static `Attach(...)`, `SetTargetedOn(host,bool)`; instance `SetTargeted`, `MarkEngaged`, `Init`.
- **DEF-206 "engage to reveal":** hidden until damaged / targeted / recently-engaged, then
  CanvasGroup-fades out. Green→amber→red states + critical pulse. Upright billboard (yaw-only).
- **WO-302 scale fix:** `ApplyScaleCompensation`/`InvAxis` cancel host lossyScale per-axis
  (no upper cap — the orc/troll People-family meshes import at >50× scale; capping the
  divisor caused the persistent "giant green pill"). Chip sprite uses `Image.Type.Simple`
  (Sliced had a min-size floor that fattened the 0.11m bar into an oval). Generated at
  runtime, WebGL-safe.
- **Wired by (Village side):** `Enemy.EnsureHealthBar`, `HeroHealthBootstrap`. Status: WIRED + LIVE.
- **NOTE:** lives in `DeNelle.Village`, NOT the HUD module — it is world-space combat art,
  not screen HUD. (The screen-space hero HP bar is `VillageHudController.SetHeroHp`.)

### HUD bridges (all in `DeNelle.Village`, ns `DeNelle.Village[.*]`) — Village→HUD push seam
The HUD never references Village; these bridges push data IN. Two reach styles:
`CoreServices.Hud` (IVillageHud interface) for interface methods, and reflection-by-name on
the concrete VillageHudController for the "extra" setters/events.

| Bridge | Path | Feeds HUD |
|---|---|---|
| WaveHudBridge | Village/Waves/ | WaveManager events → `IVillageHud` (WO-41: reflection removed, uses `CoreServices.Hud` directly) |
| HeartHudBridge | Village/Heart/ | Heart HP (event-driven, DEF-54) + crystals (0.5s poll) + full Wood/Iron/Food/Gems wallet → reflection |
| HeroAbilitiesHudBridge | Village/Hero/ | `AbilityRequested`→`HeroAbilities.TryCast`; pushes hero HP/mana/cooldowns → reflection |
| BuildMenuHudBridge | Village/Buildings/ | `BuildRequested`→`BuildMenu.Open` (reflection; RequireComponent BuildMenu) |
| StartWaveHudBridge | Village/Waves/ | `StartWaveRequested`→`WaveManager.ForceBeginNextWave`; `SetStartWaveAvailable` each frame |
| BuildModeHudBridge | Village/BuildMode/ | `BuildModeController.BuildModeChanged`→`SetCombatHudVisible(!building)`; static self-bootstrap |
| ComboHudBridge | Village/Vfx/ | `CombatFeedbackManager` combo/streak → `SetComboCount`/`SetKillStreak` (reflection) |
| ArenaHudBridge | Village/Arena/ | static `SetVisible(bool)`→`SetHudVisible` to hide HUD behind Arena modals (reflection) |
| PartyHudBridge | Village/NPCs/ | StoryCompanions → party slots 1..3 (`SetPartyMember`/`Visible`); companions have placeholder full HP |
| TalkHudBridge | Village/NPCs/ | gates `SetTalkAvailable` on TalkPromptRegistry; `TalkRequested`→nearest NPC dialogue (reflection; "Talk" off IVillageHud by design) |
| WallRepairHudBridge | Village/Walls/ | WallRepairController prompt events ↔ `ShowRepairPrompt`/`Hide` + `RepairConfirm/Cancel` (reflection) |
| TownHudBridge | Village/HUD/ | WO-339 town data every 0.5s: `SetWaveProgress`/`SetTownMetrics`/`SetMinimapPoi`/`ClearMinimapPois`/`SetLookoutStatus` (cached reflection; self-bootstrap DDOL; best-effort, placeholders) |

All bridges: WIRED + LIVE; all keep the HUD→Core asmdef rule.

---

## DATA / SCENES / PREFABS
- **No JSON** owned by the HUD module. (PetUnlockTracker persists a small JSON blob to
  PlayerPrefs `dotr-pet-unlocks-v1`; not an asset file.)
- **No prefabs / no scene files** — the entire HUD is code-built at runtime and
  bootstrapped per-scene. This is deliberate (UXML/UIDocument-sourced HUDs do NOT render
  in player builds — PIPELINE_STATE §8). Scene-builders (VillageSceneBuilder /
  WallRepairSceneSetup) author a VillageHudController in village scenes; the bootstrap
  covers all other gameplay scenes.

## DOCS / LEGACY ASSETS
- **`README.md`** — HUD module map. Title "HUD — DeNelle.HUD". **PARTLY STALE** (see FLAGS:
  references a non-existent `HUDManager`).
- **`README_HUD.md`** — "Dark Fantasy Mobile HUD (HUD-001)" setup/integration spec for the
  rich `HUDManager` + Lean D-pad. **STALE / ASPIRATIONAL** — `HUDManager.cs` does not exist
  in the repo; this entire doc describes an unshipped (or removed) component.
- **`VillageHud.uxml` / `VillageHud.uss`** — legacy Week-4 UIToolkit village HUD. **DEAD** —
  retained for reference only; not loaded at runtime (the HUD is the code-built
  VillageHudController). UXML does not render in builds.

---

## FLAGS

1. **DEAD DOC + ORPHANED CODE — `HUDManager` does not exist.** Both `README.md` and the
   entire `README_HUD.md` describe a rich `HUDManager` battle HUD as a shipped deliverable
   ("Deliverables: HUDManager.cs"). There is **no `HUDManager.cs`** anywhere in the repo.
   Consequently `VirtualDPadLean.cs` is orphaned: its bootstrap is a deliberate no-op
   ("HUDManager will own and attach this"), and nothing else constructs it → the Lean D-pad
   is never instantiated. The live movement input is the Village `VirtualJoystick`. Action:
   either restore HUDManager or delete README_HUD.md + VirtualDPadLean.cs and correct README.md.

2. **DEAD LEGACY ASSETS — `VillageHud.uxml` / `.uss`.** Superseded by code-built
   VillageHudController; UXML doesn't render in builds. Safe to delete; currently "retained
   for reference."

3. **STALE COMMENTS (comment-vs-code, the requested class of issue) — CompassHud /
   CompassHudBootstrap.** Both headers still narrate the OLD UIDocument/UXML/PanelSettings
   design ("no longer needs a UIDocument", "BAILED forever when none existed"). The current
   code is pure uGUI (own Canvas, no UIDocument). The `UIDocument` token in these two files
   is **comment-only** — a grep for UIDocument falsely flags them. Not a bug; the narrative
   could mislead a reader into thinking a PanelSettings dependency still exists.

4. **DEAD-BUT-RETAINED handlers in AdminOverlay.cs.** `OnTriggerWave`, `OnGiveCrystals`,
   `OnSetOnboarded`, `OnSave`, `OnReset`, `BuildOrientRow`/`OnOrientAsset`, and the
   owner-wallet auth path (`IsAuthorised`, `OwnerWalletAddress = ""`) are NOT wired to any
   panel button (panel trimmed 2026-06-11 to 2 buttons). Intentionally kept "in case wanted
   back" — but they are currently unreachable code. `OwnerWalletAddress=""` means the wallet
   auth-gate can never pass; the Ctrl+Shift+A chord is the only way in (by design).

5. **CONTRADICTION between code intent and a comment in AdminOverlay.** The `SetOpen` method
   computes `IsAuthorised()` then ignores the result (empty `if` body with a comment
   explaining it always allows via chord). Functionally fine but the auth check is vestigial.

6. **DailyQuestHud is display-only** — no claim/reward dispense flow yet (header notes it as
   a follow-up "once economy reward dispense is wired"). Not broken; incomplete feature.

7. **No comment-vs-code defects found in the core HUD setters / FloatingHealthBar /
   BattleHudVisibilityManager / PanelManager / BreakCaptureHarness** — their lengthy headers
   match the implementation (FloatingHealthBar's WO-302/DEF-206 narrative and the per-axis
   scale-comp code agree; BattleHudVisibilityManager's reflection narrative matches).

---

### Items cataloged
30 HUD-module `.cs` files (incl. 14 bootstraps), 1 HudTheme, 2 legacy UXML/USS assets,
2 module READMEs, 12 Village-side HUD bridges, FloatingHealthBar, PanelManager,
BreakCaptureHarness. **Total ≈ 50 catalog items.**
