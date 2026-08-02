# MASTER CATALOG — Area: HUD (`DeNelle.HUD`)

**Verified from code 2026-08-02** (every file read at HEAD on `wip/village2-and-f8-tickets`,
cites are `file:line`; comments were checked against implementation, not trusted).
Supersedes the 2026-06-12 body (retired 3-canvas VillageHudController) and its 2026-07-22
STALE banner. **The live HUD is HudKit**: `HudKitController` + `PostureEvaluator` +
`HudAreasHost`/`HudAreasConfig` + `hud-areas.json`, factory-built (ElarionUiKit),
model-bound (Core.HudModel), posture-occupied. `VillageHudController` survives only as a
thin bootstrap target + command-event holder + push-seam adapter.

Scope: `Assets/_Modules/HUD/**` (asmdef `DeNelle.HUD`) + its hard seams:
`hud-areas.json` (dual copy), `PanelRouter`/`PanelManager` (Core), the Village
`*HudBridge` reflection push family, and the enforcement oracles
(`ObsidianQueueRegression`, `UiMvvmConformanceRegression`).

---

## 0. Architecture in one breath

- **One canvas, 11 named areas** (`HudAreasHost.cs:27-51` enum, `:95-111` geometry):
  Vitals, Status, System, TargetInfo, ActionRail, ActionBar, MoveCluster, Feedback,
  Dock, HeartStatus, QueueStatus. ScreenSpaceOverlay, sortingOrder **4000**
  (`HudAreasHost.cs:85`), CanvasScaler 1080x1920 match 0.5 (`:88-90`). Host is pure
  scaffolding — zero widgets, zero art.
- **Postures are data rows, not code branches** (`HudPosture.cs:17-33`): calm(town),
  calm(explore), build, hostile(prebattle), hostile(activebattle), hostile(postbattle),
  modal. Row keys via `HudPostureKeys.Key` (`HudPosture.cs:39-51`). Occupancy lives in
  `Data/Canonical/hud-areas.json` (Resources + StreamingAssets **byte-identical dual
  copy** — verified identical 2026-08-02; divergence FAILS `ObsidianQueueRegression`
  `:304-306`).
- **MVVM law (§5)**: widgets come only from the ElarionUiKit factory and bind Core
  `HudModel` `Changed` events — zero raw widget construction, zero state pulls in the
  kit (`HudKitController.cs:8-12`). Enforced by the **[ui-mvvm] ratchet**
  `Assets/Editor/Regression/UiMvvmConformanceRegression.cs` with
  **`HardFailOnNew = true`** (`:53`) — a new View that reads game state without a VM
  fails the gate. `HudKitController.cs` is allow-listed only for its compass-provider
  `FindAnyObjectByType` (Transform positions, not game state) (`:104`); the Village
  `*HudBridge.cs` suffix is the sanctioned push seam (`:113-114`).
- **Commands out**: kit taps fire either the owner `VillageHudController` UnityEvents
  (Village bridges subscribe by reflection) or Core statics/gates
  (`HudCommands`, `PanelRouter`, `ObsidianQueueGate`, `RaidEntryGate`, `PauseGate`).
- **Asmdef law**: `DeNelle.HUD` → DeNelle.Core, DeNelle.Data, Unity.Localization,
  UniTask, UnityEngine.UI, TMPro, LeanTouch/LeanCommon/CW.Common
  (`DeNelle.HUD.asmdef:4-14`). **Never Village/BattleATB.** Village types are reached
  only via loose reflection (`Type.GetType("DeNelle.Village.X, DeNelle.Village")`).

---

## 1. Kit/ — the live HUD

### Kit/HudKitController.cs (1,836 lines — THE HUD)
`DeNelle.HUD.Kit.HudKitController`, sealed MonoBehaviour. Built once per gameplay scene
by `VillageHudController.Start` via `HudKitController.Create(owner)` (`:132-148`):
creates `HudAreasHost`, adds a `PostureEvaluator`, loads `HudAreasConfig`, resolves
`CoreServices.HudModel`, then `BuildWidgets()` + `BindModels()` + first `ApplyPosture`.

**Widget registry**: id → wrapped root dict (`:58-59`); `Register` deactivates every
widget at build (`:621-625`) — **the occupancy rows are the only thing that turns a
widget on** (`ApplyPosture :1601-1616` reparents into the area mount + SetActive).
Registered ids (26): playerNameplate, playerBuffRow, wisdomChip, waveBlock, heartStatus,
targetCycle, fleeButton, targetFrame, enemyBuffRow, castBar, abilityRow,
assignableSkillRow, hpPotionSlot, manaPotionSlot, attackButton, resourceChips,
resourceChipsCollapsed, queueStatusChip, buildButton, talkButton, bagButton,
raidsButton, mapButton, questButton, moveCluster, chatDock, compass, feedbackLayer.

**The calm(town) action bar — SIX equal faces after the Queues-button retirement**
(`:412-517`): divisor math `btnW = (1 - gap*5)/6` (`:422-426`).
1. **Build** → `_owner.BuildRequested` (`:428-431`); tutorial spotlight target
   `TutorialHighlightRegistry.Register("hud.build_button")` (`:433`).
2. **Talk** → `HudCommands.Talk()` + legacy `TalkRequested` (`:437-444`); visibility =
   `PostureSignals.TalkAvailable` (dim to alpha 0.45, `OnTalkChanged :1431-1438`).
3. **Bag** → `PanelRouter.Open(PanelId.Inventory)` + legacy instance event + static
   `RaiseInventoryRequested` (`:448-461`) — the 07-06 "bag does nothing" RCA fix.
4. **Raids** → `RaidEntryGate.RequestOpen()` (`:474-480`). **Full-army dim poll**
   (WO-820, owner ruling): `Update()` polls `RaidEntryGate.ArmyStatus` by Version
   (`:1703-1725`); army not full ⇒ face+label tint to `ElarionUi.Disabled` but the
   button **stays interactable** — the tap must still fire so RaidSelectionScreen can
   redirect to the drillmaster (`:98-101`).
5. **Map** (WO-826) → `PanelRouter.Open(PanelId.RealmMap)` (`:498-505`), reflection-free.
   **Hidden until Onboarded** (WO-825 R4): `Update()` polls
   `GameStateService.Instance.State.Onboarded` and toggles the **inner button, not the
   widget root** — occupancy rows keep owning the root (the waveBlock self-gate
   precedent) (`:1727-1741`).
6. **Quests context button** → `OnContextAction` (`:511-517`, `:1469-1477`): focused
   upgradable building (`HudBuildingFocus.CurrentBuildingId` / `CurrentUpgradeAction`)
   ⇒ `PanelRouter.Open(BuildingUpgrade, id)`; else `PanelRouter.Open(RumorBoard)`.
   Face relabels **Quests ↔ Upgrade** via an `Update()` poll (`:1642-1656`).

**Queues entry retirement (owner 2026-08-01, commit eb5d0710)**: the bar's 7th
"Queues" button (renamed from "Work" 2026-08-01, commit 85ed4c98) was **retired** —
the right-column **Builders chip in the QueueStatus band is the ONE Queues entry**
(`:465-468`). Enforced by `ObsidianQueueRegression` check 7c: a re-registered
`workQueueButton` widget id in code (`:282-283`) or a `workQueueButton` row in either
hud-areas.json copy (`:301`) fails the gate.

**Builders/Training queue chip** (WO-778, `BuildQueueStatusChip :663-708`): always-on
CoC-style chip in the QueueStatus area; tap → `ObsidianQueueGate.RequestToggle`
(`:676-680`). Label "Builders 1/2 \n 9m 30s | Training N" (`FormatQueueChip :737-751`,
**all ASCII, no middot — glyph law** `:737`). Under it: WC3-style 5-deep queue rows
(owner 2026-07-30), text-encoded state `">"` working / `"-"` queued, `+N more` tail,
plate hidden when empty (`FormatQueueRows :712-733`; raycast off `:698,703`). Repaint
is Version-gated in `Update()` (`:1686-1701`) — `ObsidianQueueGate.Status.Version`
published by BuildTimerService on QueueChanged + a 1 s tick.

**Other widget clusters** (all factory-built in `BuildWidgets :212-551`):
- **Vitals**: WO-432 shared `BuildPartyNameplate` ("Hero" + HP/MP + in-plate gold XP
  strip, `withXpStrip:true` `:220-221`); WO-611 inset VitalsWell when
  `FeatureFlags.CombatHud611` (`:228-242`). Wisdom chip carries permanent "SKILL" text
  tag — colorblind law, icon + TEXT, never icon-or-nothing (`:252-256`).
- **Heart of Elarion status** (`BuildHeartStatus :757-792`): tree glyph + shared
  nameplate "Heart of Elarion" (**ASCII name — the "♥" glyph tofu'd on the build
  font**, `:781`), mana row hidden so it never reads as a second hero bar (`:784-789`).
- **Wave block** (`BuildWaveBlock :627-657`): labels + progress + Start Wave. Wave-chrome
  law: exists only in the calm(town) row AND self-gates to between-wave phases in
  `OnWave` (`:1224-1229`); countdown only when real (`:1236-1238`); Start Wave relabels
  "Start Now" during a countdown (`:1244-1253`); tutorial target `hud.wave_button`
  (`:654`).
- **Target frame + cast bar + status rows + target cycle** (`:287-378`, `:897-919`,
  `:946-977`): `_targetFrame.Bind(m.Target)` / `_castBar.Bind(m.Cast)` (`:1156-1157`);
  WO-611 flag adds gold "Lv N" title row with `FitSingleLine` ellipsize, re-anchored
  portrait, and the 3-state lock crosshair badge driven from `TargetModel` in
  `Update()` (`:1636-1640`). Target-cycle rows get a sanctioned interim
  `AddComponent<Button>` (kit `BuildNameplate` ships no tap helper — kit ask filed,
  `:963-968`).
- **Ability arc (Q/W/E/R)** (`BuildAbilityRow :794-860`): **WO-750 owner ruling —
  NO key-letter badges on the touch medallions** (icon = identity; keyboard/gamepad
  bindings stay live in code only) (`:820-825`, null keyBadge `:848`). WO-611:
  `CombatArcLayout611` (`:1795-1835`) positions medallions around the attack pill in
  pill-height units from the shared `Pill611*` constants (`:82-83`) — layout-time only,
  dirty-flag, never per-frame. Empty slots render as dimmed medallions
  (`SetEmptyMedallion :1326-1334`); `"text:"` IconKey prefix renders a word face
  (owner placeholder 2026-07-11, `:1298-1305`).
- **Assignable row + potions** (`:862-944`): hot-swap slots → `HudCommands.
  AssignableCast`; potions → `HudCommands.Potion/ManaPotion`, gated by count AND
  binding AND cooldown (`OnConsumables :1352-1375`).
- **Resource dock** (WO-431/440/697, `BuildResourceChips :979-1116`): right-edge
  "Resources" tab (icon + word, never icon-only — flag_03 `:1073-1075`) toggling an
  obsidian content-fit panel of 5 uniform chips (Gold/Wood/Iron/Food/Crystal; owner
  2026-07-15 colorblind ruling: **all five peers, no gilt/bold Gold** `:1047-1054`).
  Count-tween only, no flash (law lives in `CurrencyChip.SetAmount`, `:1210`).
  calm(explore) collapsed gold-only chip tap-expands the full row for 6 s
  (`:1101-1115`, `Update :1666-1681`).
- **Move cluster** (`:519-531`): CombatHud611 ⇒ virtual D-pad, else the four round
  buttons — both write `HudMoveInput.Set`.
- **Slide dock** (WO-439, `BuildSlideDock :1484-1523`): left gear tab, FIVE tabs —
  Chat / Leaderboard / Music / Settings / **Pause** (folded in, cosmetic flag A
  2026-07-24; the top-right Menu text button and standalone pause chip were culled —
  ONE settings door) (`:270-275`, `:1516-1520`). Chat/Leaderboard find the panels;
  Music reflects `DeNelle.Audio.MusicSelectionPanel.Toggle` (`:1568-1583`); Settings →
  `HelpMenu.Instance.ToggleOverlay()` else `PanelRouter.Open(GameGuide)` (`:1545-1551`);
  Pause → `PauseGate.RequestBack()`. Panel/tab pinned to fixed reference pixels
  (F8-12 tiny-mount fix, `:1490-1510`).
- **Compass** (`:537-544`): `HudCompassWidget.Create` + `WireCompassProviders`
  (`:558-607`) — hero/seam/enemy transforms resolved by reflection against
  `DeNelle.Village.HeroLocomotion` / `HeroLinkCrossing` / `Enemy`, polled ~4 Hz.
- **Feedback layer** (`:546-550`): marker only; ensures `CombatTextLayer.Instance`.
- **Toasts** (`ShowToast :193-206`): repair prompt (adds a green Repair button firing
  `RepairConfirmRequested`, `:169-182`) and wave-clear (`:185-191`) ride the Feedback
  area; self-expire (Cancel = expiry).

**Model binding** (`BindModels :1122-1161`): subscribes HeroVitals, Economy, Wave,
World, Abilities, Assignable, Consumables, PlayerStatus, TargetStatus, TargetCycle
models + `PostureSignals.TalkChanged`; late-binds via `InvokeRepeating` if
`CoreServices.HudModel` is not yet registered (`:1126-1131`). All unsubscribed in
`OnDestroy` (`:1763-1771`, also zeroes `HudMoveInput`).

**ApplyPosture** (`:1589-1629`): CombatHud611 + hostile posture ⇒
`PanelManager.CloseAll()` (combat HUD is the active screen, owner rules 1+2,
`:1593-1599`); then pure data-driven occupancy; then dynamic availability gates
(flee/talk/consumables/status/wave) that never do layout (`:1618-1625`).

**Update() polls** (no model event exists for these Core statics): lock badge
(`:1636-1640`), quest-context face (`:1647-1656`), flee availability (`:1659-1664`),
collapsed-chips expand window (`:1666-1681`), queue chip Version (`:1686-1701`),
raids army Version (`:1707-1725`), map Onboarded (`:1731-1741`).

### Kit/HudAreasHost.cs (132)
Enum `HudArea` (**11 areas** — QueueStatus added WO-778, `:49-50`) + the one-canvas
scaffolding host. Geometry (`:95-111`): Vitals 0.01-0.33 x 0.80-0.985; Status
0.34-0.66 x 0.845-0.99; System 0.845-0.995 x 0.88-0.985; TargetInfo 0.28-0.72 x
0.66-0.84; ActionRail 0.78-0.995 x 0.04-0.42; ActionBar 0.28-0.72 x 0.015-0.15;
MoveCluster 0.01-0.27 x 0.03-0.33; Dock 0-0.23 x 0.33-0.43; HeartStatus 0.01-0.33 x
0.70-0.792; QueueStatus 0.78-0.995 x 0.53-0.865 (taller since 2026-07-30 for the WC3
rows); Feedback full-screen, last sibling, never raycast (`:113-115`). NOTE: the
class doc says "nine mounts" (`:53`) and the build log line says "9 area mounts"
(`:117`) — the code adds **11**; comment drift only.

### Kit/HudAreasConfig.cs (139)
`hud-areas.json` loader. `Resources.Load<TextAsset>("Data/Canonical/hud-areas")`
(`:54-58`), JsonUtility parse (WebGL-safe, no Newtonsoft, `:13`). Unknown area string
⇒ row **warn-skipped** (`:77-81`) — *exactly how the Work button went dark once*; the
MANDATORY rule: never add a JSON area without its `TryParseArea` case (`:116-119`,
oracle-checked `ObsidianQueueRegression:307-309`). Absent/unparseable file ⇒
`FlowTrace.Fail` + minimal authored fallback (vitals + move + settings in the 4 live
postures) so bad data can never blank the HUD (`:97-99`, `:125-137`).
`Occupancy(posture)` returns an empty map for row-less postures (hostile(postbattle),
modal) (`:41-46`).

### Kit/PostureEvaluator.cs (110)
The single writer of the live posture. 0.15 s unscaled poll (`:49`, `:60-64`).
Derivation precedence (`Evaluate :78-108`): Modal ← Context==Modal; HostilePostbattle
← `PostureSignals.EndStateVisible`; HostileActiveBattle ← Context==Battle (a live
town wave lands here — the wave IS the threat); Build ← Context==BuildMode;
HostilePrebattle ← `PostureSignals.PursuitActive` OR **manual** lock
(`Target.HasTarget && Locked` — auto-nearest tracking must NOT keep battle chrome up,
owner 2026-07-05, `:98-102`); CalmTown ← Context==Town; else CalmExplore. Every
transition emits the fleet-assertable `[Flow:HudKit] posture a->b` line (`:71-73`).

### Kit/HudPosture.cs (53)
The posture enum + `HudPostureKeys.Key` mapping to the owner's JSON spellings.

### Kit/HudMoveInput.cs (30)
Static `Vector2 Move` (clamped, `:25-28`) written by the kit's move cluster/D-pad;
**read by `HeroLocomotion` via loose reflection** on the type string
`"DeNelle.HUD.Kit.HudMoveInput, DeNelle.HUD"` (`:8-11`) — replaced VirtualDPadLean.

### Kit/HudCompassWidget.cs (450)
The reusable kit compass (replaced the standalone-canvas `CompassHud`, which is
**deleted from the tree** — see risk #4). Presentation-only: three provider delegates
(`:50-54`), polled at 0.25 s (`:63`, `:194-203`). Blink-Obsidian octagon + cardinal
label + rotating gold objective **needle** (WO-438, `:249-270`) pointing at the
nearest region-gate seam; heading = camera flattened forward, hero-forward fallback
(`:213-222`). Enemy threat = pooled **red apex-up triangle pips** (procedural sprite,
static-cached, `:393-421`) — a distinct SHAPE vs the gold needle because the **owner
is red/green colorblind: meaning never by color alone** (`:59-62`, `:379-382`).
Off-fan enemies pin to the band edge rotated ±90° as direction arrows (`:298-310`);
16 px min pip height (0-rect hardening, `:371-376`). F8-16 probe seams for the
AutoPilot `AssertCompassMarks` fleet: `EnemyProviderWired` / `EnemyMarkCount` /
`ActiveTickCount` / `TryGetFirstActiveTickSize` / `ForceProviderPoll` (`:83-130`).
Editor builds append the yaw in degrees to the cardinal (`:240-244`).
**No minimap exists**: the kit registers no minimap widget; `SetMinimapPoi` /
`ClearMinimapPois` are no-op adapters marked "minimap deferred (P23 report)"
(`VillageHudController.cs:191-192`). The compass is the whole navigation surface;
the WO-825/826 Realm Map panel is the map surface.

---

## 2. VillageHudController.cs (206) — command/host shell

"THIN BOOTSTRAP + PUSH-SEAM ADAPTER (P23 total demolition)" — the 3,000-line widget
body is gone (`:1-41`). Three jobs:
1. **Bootstrap target**: `Start()` registers `CoreServices.RegisterHud(this)` and
   builds the kit inside `Guard.Try` (`:104-114`); `OnDestroy` unregisters (`:116-119`).
2. **Command-event holder** (`:61-82`): BuildRequested, SkillsRequested, ShopRequested,
   TalkRequested, InventoryRequested, QuestsRequested, IntelRequested, RaidRequested,
   RallyRequested, RetreatRequested, `AbilityRequested` (UnityEvent&lt;int&gt;),
   RepairConfirm/CancelRequested, StartWaveRequested — byte-for-byte the Village
   bridges' reflection contract. Plus the static `InventoryRequestedStatic` /
   `RaiseInventoryRequested` seam for bridges that outlive a scene HUD (`:77-79`).
3. **IVillageHud adapter**: live forwards = `SetTalkAvailable` →
   `PostureSignals.SetTalkAvailable` (the root-caused stale-one-shot-reflection fix,
   `:130`), `SetStartWaveAvailable`, `ShowRepairPrompt`, `ShowWaveClearBanner`,
   `SetHudVisible`, `SetVillageContextForced` (`:133-166`). **Every data setter is a
   deliberate no-op** (`:175-204`) — the P4 producers (HudModelProducers) already push
   the same facts into Core.HudModel; making the setters no-ops closes the
   dual-fill-source risk. `InVillage` reads the ONE context model (`:93-101`);
   `TownHudGroup` exposes the kit host's CanvasGroup (`:90`).

### VillageHudBootstrap.cs (124)
WO-334 static guarantor: `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` + sceneLoaded
(`:74-81`) ensures exactly one VillageHudController per gameplay scene; skips the
menu-scene allowlist (Title, HeroSelect, PetSelect, Intro, IntroFlow, Store, PackStore,
Boot, Bootstrap, MainMenu, GameOver — `:59-72`); idempotent; host parented to the
just-loaded scene, no DDOL (`:99-104`).

---

## 3. In-module VMs + Views (strict MVVM, Silo E)

All VMs: pure C# (no UnityEngine UI), implement `IPanelViewModel`
(Title/Changed/Close/Dispose), an `ISource` seam interface with a `ServiceSource`
inner class as the SOLE live singleton-resolution site, EditMode-tested
(`Assets/Tests/EditMode/DailyQuestVMTests.cs`, `ClanChatVMTests.cs`,
`LeaderboardVMTests.cs`, `QuestTrackerVMTests.cs`).

- **DailyQuestVM.cs (256)** — projects `DailyQuestService.Today` into ItemVM tiles
  (Equipped = Completed) + ProgressText/RewardFor/FlavorFor/CelebrationKeyFor; owns
  row selection (`Select :141-149`); dormant `Reroll` command (no View wires it,
  `:17-19`). Rewards auto-dispense (DEF-223) — **no claim command exists** (`:16-17`).
  `ResolveLabel` (`:213-217`) substitutes `{target}` into the quest label — currently
  a **private static local to this VM**; a change to share it (with the quest-label
  consumers outside this module) is **in flight 2026-08-02, not landed** — the tree
  has exactly two `ResolveLabel` hits, both in this file.
- **DailyQuestHud.cs (367) + Bootstrap (70)** — WO-714 QUESTS master-detail view
  (rows well + shared parchment detail card), WO-795 ScrollRect well, binds
  DailyQuestVM, view-local `_celebrated` toast memory only. Bootstrap spawns it
  HIDDEN per scene with a hero (reflection hero-find `:62-68`), suppressed in
  enemy-owned raid scenes (WO-550, `:34-38`). **Opener risk**: its header still says
  the TOWN ACTIONS "Quests" button toggles it (WO-411), but the kit's Quests button
  now routes to the Rumor Board (`HudKitController.cs:1475`) — see risk #6.
- **ClanChatVM.cs (255)** — projects ClanService + ChatPhraseCatalog into
  MessageRow/ChipRow lists (dividers, never-blank fallbacks); commands OnHeaderButton
  (Leave), CreateClan, SendPhrase, SendCustom. **ClanChatPanel.cs (417) +
  Bootstrap (70)** — Obsidian master-frame modal, `Toggle()` from the kit dock,
  PanelManager-registered.
- **LeaderboardVM.cs (191) / LeaderboardPanel.cs (274) + Bootstrap (69)** — WO-129
  modal; VM owns the async FetchTopAsync + metric tabs + the honest
  "Local (offline)" source badge; opened from the kit dock.
- **QuestTrackerVM.cs (177) / QuestTrackerHud.cs (209) + Bootstrap (84)** — the
  minimized far-right quest ICON (owner 2026-07-06 ruling: tracker card is gone; the
  board is the reading surface). VM resolves the tracked quest (WO-454 type-aware
  fallback), exposes HasTrackedQuest + UpdateSnapshot (gold update dot = luminance
  cue, owner colorblind). Click → `PanelRouter.Open(RumorBoard)`. Code-built uGUI on
  its own canvas — NOT the kit occupancy table (predates it; second HUD canvas).
- **BugReportVM.cs (242)** — WO-596 player bug report. Submit button IS the consent
  (`:9-11`); POSTs to the live -v2 Vercel `api/bug-report` (`:36`); auto-attaches
  FlowTrace tail, scene, session id, version, platform, and the Pi uid only as a
  **salted SHA-256 hash** (`:40`, `PiUidHash :180-194`); note cap 1000 chars; local
  persistentDataPath fallback on failed POST, non-WebGL only (`:198-209`).
  **BugReportView.cs (387)** — Obsidian master-frame form; clean-frame screenshot
  captured BEFORE the form builds (`BreakCaptureHarness.CaptureForReport`, `:16-19`);
  timeScale freeze while typing (`:79-80`); eased open/close; PanelManager
  "BugReport" (`:77`). VM-bound; its residual `FindAnyObjectByType` is EventSystem
  infra (ratchet allow-list `UiMvvmConformanceRegression.cs:99`).
- **DialogueView.cs (978)** — the dumb uGUI dialogue skin (WO-455), binds
  `DialogueViewModel` via `DialogueService.Opened`; DDOL self-bootstrap behind
  `FeatureFlags.CustomDialogue` with a traced decline (`:24-38`). Carries three
  hard-won laws: the P0 **per-VM Closed handler** re-entrancy fix (a stale close must
  never tear down a chained successor dialogue, `:86-94`); the **WO-702 builder
  truce** (Build Mode open ⇒ dialogue HIDDEN, never Closed — closing fires Ended and
  falsely completes dialogue-gated tutorial steps; truce state lives in the VM per
  WO-744, `:96-106`); the **WO-795 modal truce** (another arbiter-tracked modal open
  ⇒ hidden, re-shows + re-notifies on modal close, `:108-117`). Registered
  **battle-allowed** with PanelManager (scripted narrative must survive battle lock,
  `:70-73`). Probe seam `IsShowing` (`:76-81`).

---

## 4. Overlays, dev tools, misc

- **HelpMenu.cs (399) + Bootstrap (50)** — the Settings/Help modal (kit gear →
  `Instance.ToggleOverlay()`): Report Bug (BugReportView), Controls, Reset Hero & Pet,
  Dev tools (dev builds), Credits. WO-F reference conversion to code-built uGUI
  Obsidian master frame; still lends AdminOverlay a synthesized runtime PanelSettings
  (AdminOverlay stays UITK, `:14-16`). Ratchet allow-listed as dev surface.
- **AdminOverlay.cs (918) + Bootstrap (47)** — owner debug overlay, chord
  Ctrl+Shift+A; `OwnerWalletAddress = ""` so the wallet gate never passes (chord is
  the only door, by design, `:29-32`). UITK code-built; reflection into
  Village/Core.State. Allow-listed.
- **OwnerDevToolsOverlay.cs (487)** — RELEASE-SAFE touch dev-tools button for mobile,
  gated to the signed-in owner Pi account ("samanthadenelle"); no
  DEVELOPMENT_BUILD/#if gates; reflection to real shipped gameplay methods
  (`:15-27`). Allow-listed.
- **DebuggingController.cs (372)** — F9 flag-gated dual-stack (uGUI + UITK) UI
  debugger; dormant when off; static `Capture()` seam. Allow-listed.
- **PointerInterceptDiagnostic.cs (204) + Bootstrap (44)** — trace-only pointer-hit
  dump when a dev/settings overlay is open (the "4th interceptor" investigation);
  changes no behaviour.
- **CosmeticShopPanel.cs (607) + Bootstrap (70)** — Glimmer cosmetic shop modal
  (Obsidian FrameMerchant); reflection bridge to `DeNelle.Cosmetics` (asmdef
  isolation; misses degrade to "shop unavailable", `:15-18`).
- **AttentionGlowUi.cs (81)** — reusable chasing-comet border cue
  (`Attach(target,tint,dot)`); used by talk/tutorial focus.
- **README.md / README_HUD.md** — **BADLY STALE** (risk #1).

---

## 5. hud-areas.json — the occupancy contract

`Assets/Resources/Data/Canonical/hud-areas.json` + byte-identical mirror
`Assets/StreamingAssets/Data/Canonical/hud-areas.json` (CanonicalJson dual-copy law;
**verified byte-identical 2026-08-02**; divergence, a missing `queueStatusChip` row,
or a resurrected `workQueueButton` row FAILS `ObsidianQueueRegression :295-306`).
Version 1, 6 posture rows:
- **calm(town)**: vitals(playerNameplate, xpBar, wisdomChip); heartStatus; status
  (compass, waveBlock); system(settingsButton); queueStatus(queueStatusChip);
  actionRail(resourceChips); actionBar(buildButton, talkButton, bagButton,
  raidsButton, mapButton, questButton) — the six faces; moveCluster; dock(chatDock);
  feedback.
- **build**: system(settingsButton) only — near-empty edit-session HUD.
- **calm(explore)**: nameplate+xpBar, compass, settingsButton,
  actionRail(resourceChipsCollapsed, attackButton), actionBar(talkButton, bagButton),
  moveCluster, chatDock, feedback. No wave chrome by construction.
- **hostile(prebattle)** / **hostile(activebattle)**: buff rows, heartStatus, compass,
  targetFrame(+castBar in active), abilityRow(+attackButton in active),
  actionBar(**buildButton**, assignableSkillRow, potions), moveCluster, feedback;
  active adds system(fleeButton, settingsButton).
- **hostile(postbattle)** and **modal**: `"areas": []` — total stand-down.

**Deliberately-inert rows** (posture only iterates REGISTERED widgets):
`xpBar` (standalone bar removed 07-06 — the in-plate strip is THE XP display,
`HudKitController.cs:246-250`) and `settingsButton` (top-right Menu door removed
2026-07-24 — the left gear dock is the one settings entry, `:270-275`). These rows are
harmless but are a trap for a data-reader assuming row ⇒ rendered (risk #3).

---

## 6. Cross-assembly seams

- **Village → HUD push**: bridges in `DeNelle.Village` reach the HUD via
  `CoreServices.Hud` (IVillageHud) or reflection-by-name on `VillageHudController`'s
  UnityEvents/extra setters. Since P23 most data pushes are dead ends by design
  (no-op setters §2); the live seams are the command events, StartWave availability,
  repair/wave toasts, HUD visibility, and the Core statics (`PostureSignals`,
  `HudBuildingFocus`, `HudCommands`, `ObsidianQueueGate.Status`,
  `RaidEntryGate.ArmyStatus`, `BuildModeState`, `TutorialHighlightRegistry`). The
  `*HudBridge.cs` suffix is the ratchet-sanctioned push idiom
  (`UiMvvmConformanceRegression.cs:113-114`).
- **HUD → Village reads**: loose reflection ONLY — compass providers
  (`HudKitController.cs:558-607`), jukebox toggle (`:1568-1583`), hero-find in
  bootstraps (`DailyQuestHudBootstrap.cs:62-68`). Never an asmdef edge.
- **PanelRouter (Core, `Assets/_Modules/Core/UI/PanelRouter.cs`)**: reflection-free
  id → opener registry (DEF-213). HUD-relevant ids: `Inventory = 14` (registered
  scene-independently by HeroInventoryController's boot hook, `:81-86`),
  `RealmMap = 15` (WO-826, registered by RealmMapPanel via RealmMapPanelBootstrap,
  `:87-92`), `RumorBoard`, `BuildingUpgrade`, `GameGuide`. Every Open runs a
  **post-open visibility verify** (WO-465: "didn't throw ≠ rendered") with the WO-437
  battle-lock refusal carved out as a contract, not a failure (`:248-274`); raises
  `PanelOpened` for tutorial signals (`:197-211`).
- **PanelManager (Core) — the modal law**: one registered panel open at a time;
  `NotifyOpened` closes the previous; `Register` vs `RegisterBattleAllowed`
  (`PanelManager.cs:85-96`) — non-battle-allowed panels are refused while
  `BattleLock.IsInBattle()` (`:122`); `CloseAll` (`:212`) is what the kit fires on
  the hostile-posture flip. HUD registrants: HelpMenu, AdminOverlay, BugReportView,
  ClanChatPanel, LeaderboardPanel, CosmeticShopPanel, DialogueView (battle-allowed).

---

## 7. Owner rulings on record (HUD-binding)

1. **No key-letter badges on mobile** (WO-750, 2026-07-19): touch medallions render
   with no Q/W/E/R chip — the icon carries identity; keyboard/gamepad bindings stay
   code-only (`HudKitController.cs:820-825`, `:848`).
2. **Never color-only** (owner is red/green colorblind, §7 canon): state is always
   carried by text/shape/count — "SKILL" tag (`:252-256`), `">"`/`"-"` queue rows
   (`:690`), triangle-vs-needle compass shapes (`HudCompassWidget.cs:59-62`), uniform
   resource chips (`:1047-1054`), DailyQuestHud "+ Done"/"2 / 5" counts.
3. **ASCII-only in TMP** (build-font glyph law): no "♥" (`:781`), no middot (`:737`),
   ASCII `"^"` chevron (`HudCompassWidget.cs:184`) — the build font tofus non-ASCII.
4. **Queues entry = the Builders chip, nothing else** (2026-08-01): bar button
   retired; oracle-enforced (`ObsidianQueueRegression :279-303`).
5. **Raids dims, never disables** (WO-820): the tap must always reach
   RaidSelectionScreen's drillmaster redirect (`HudKitController.cs:98-101`).
6. **Map hidden pre-Onboarded** (WO-825 R4 / WO-826) (`:1727-1741`).
7. **WO-835 "action-bar applicability repack" — IMPLEMENTED 2026-08-02 (pending
  gates + PO felt-verify).** The bar renders ONLY applicable faces, packed +
  centered at constant width, from the ordered array computed in
  **`Assets/_Modules/Core/HudModel/HudActionBarModel.cs`** (`ActionBarButtonId`
  enum order = bar order; `Shared` instance; edge-triggered
  `ActiveButtonsChanged`/`RaidsDimmedChanged`; `ISource` seam +
  `HudActionBarModelTests` / `HudActionBarRegression`). The View's fixed `/6`
  math, Talk dim, Raids army-dim poll, Map Onboarded poll and the
  Quests<->Upgrade relabel are all RETIRED from `HudKitController` —
  it binds the model and runs a render-from-array pass (`ApplyActionBar`) +
  `ApplyRaidsDim`. New pieces: `PostureSignals.RaidCapable` (mirror seam,
  default TRUE never-false-block), Village
  `Troops/RaidCapabilityHudBridge.cs` (FeatureFlags.Raid + `StructureSingleton.
  IsBuilt("barracks")` + `ArmyReadiness.Compute` deployable>=1, WO-823
  single-source), the split-out **Upgrade** face (`upgradeButton` widget +
  calm(town) occupancy row in BOTH json copies), ActionBar zone widened to
  x 0.270-0.730. SEMANTICS PRESERVED: Map still Onboarded-gated (WO-825 R4,
  now repacked — no hole); a VISIBLE Raids face still dims-not-disables on a
  not-full army (WO-820); rulings #4/#5/#6 below read through this model now.

---

## 8. RISK LEDGER (2026-08-02, priority order)

1. **README.md + README_HUD.md are dangerously stale.** `README.md` still documents
   `HUDManager`, `VirtualDPadLean`, `XPBarController`, `FloatingXpText`, `CompassHud`
   and legacy UXML — none exist in the module anymore — and never mentions Kit/ or
   the VMs. `README_HUD.md` describes the never-shipped HUD-001 `HUDManager`. A
   fresh agent following the README system lands on fiction. Action: rewrite both.
2. **RESOLVED 2026-08-02 (WO-835 implemented, pending gates).** The map-hole,
   talk-dim and raids-dim Update() polls moved into `HudActionBarModel` (Core);
   the bar is render-from-array, so holes are impossible by construction. Residual
   watch item: line-number cites in §1 of this catalog for `HudKitController.cs`
   (bar build ~:412-517, Update polls ~:1631-1742) predate the WO-835 edit and
   have shifted; re-verify cites on the next catalog pass.
3. **Inert hud-areas.json rows** (`xpBar`, `settingsButton`) look load-bearing but
   render nothing (unregistered ids). Safe today, but a future widget accidentally
   registered under one of these ids would silently appear in postures nobody
   re-reviewed. Consider pruning the rows in the same breath as the next json edit
   (dual-copy + oracle rules apply).
4. **Stale comment: `HudCompassWidget.cs:154-157` TODO(dedup)** claims a standalone
   `DeNelle.HUD.CompassHud` still coexists — it was deleted; the kit widget is the
   only compass. Also `HudAreasHost.cs:53/:117` says "nine mounts" but builds 11.
   Comment-only; misleads a reader, not the runtime.
5. **`ObsidianQueueRegression` 7c is a source-string oracle** — it greps
   `HudKitController.cs` and both json copies for literal tokens
   (`:269-309`). Renaming the file, the widget id, or the gate call site breaks the
   gate loudly (good) but also means refactors must touch the oracle in the same
   commit (know before you rename).
6. **DailyQuestHud opener is unverified-possibly-orphaned.** It spawns hidden per
   WO-411 expecting the TOWN ACTIONS "Quests" button to toggle it, but the kit's
   Quests button routes to the Rumor Board (`HudKitController.cs:1475`) and no
   HudKit/dock code path calls `DailyQuestHud.Toggle`. If nothing else opens it, the
   whole VM+View surface is dark. Needs a reachability check (and either a route or
   a retirement).
7. **Bag tap triple-fires** (`:456-460`): PanelRouter.Open(Inventory) + the instance
   `InventoryRequested` UnityEvent + the static `RaiseInventoryRequested`. Deliberate
   (legacy listeners in hub scenes), but a listener subscribed to both event paths
   would double-handle; keep new listeners on exactly one seam.
8. **QuestTrackerHud is a second HUD canvas** outside the kit occupancy system (own
   ScreenSpaceOverlay canvas). It self-hides on modals, but it does not obey posture
   rows — combat/build postures rely on its own gating, not hud-areas.json.
9. **DailyQuestVM.ResolveLabel shared-ification in flight** (2026-08-02): when it
   lands, the cite in §3 (private static `:213-217`) goes stale — update this
   catalog in the same commit (§15 canon law).
10. **`buildButton` occupies the actionBar in BOTH hostile postures** (json rows
   `:183`, `:250`) — the Build door is live mid-combat by data. If that is not
   intended (Build Mode flips the posture to `build` anyway), it is a one-row json
   fix; recorded here because it reads like an oversight rather than a ruling.

---

### Items cataloged
35 module files (7 Kit + 28 root: 6 VM/View pairs + views, 8 bootstraps, 5 dev/debug
surfaces, 2 stale READMEs, asmdef), hud-areas.json dual copy, 2 enforcement oracles,
PanelRouter/PanelManager seams, 7 owner rulings, 10-item risk ledger.
