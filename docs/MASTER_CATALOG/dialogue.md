# MASTER CATALOG — Dialogue

**Verified from code 2026-08-02** (branch `wip/village2-and-f8-tickets`). Supersedes the
2026-07-13 version, which documented the REMOVED Yarn/ClassicRPG stack. Scope: the custom
code-built dialogue spine (`Assets/_Modules/Core/Dialogue`), its Village command sink +
compat shim, the HUD view, the dialogue data catalog, the ambient/bubble line tables that
deliberately do NOT use the runner, and the regression oracles.

## Architecture in one breath

**Yarn is GONE — WO-557 full removal.** There are no YarnSpinner packages (`Packages/`
contains only `manifest.json` + `packages-lock.json`; no `yarn` entry in the manifest), no
`.yarn` files (`Assets/Dialogue/` deleted), no `DialogueSystem.prefab`
(`Assets/Resources/Dialogue/` exists but is EMPTY), no `DialogueRunner` from Yarn, no
command bridge, no source generator. Every conversation runs on **our own MVVM stack
(WO-455)**:

- **Data** — `Data/Canonical/dialogue/dialogues.json` (dual-copy: Resources + StreamingAssets,
  WebGL-safe via `CanonicalJson`) → `DialogueCatalog` (`DialogueModel.cs`).
  Current content: **version 2, 38 dialogues, 71 nodes, 14 speakers**.
- **Runtime** — `DialogueRunner` (plain C# state machine, no MonoBehaviour/async):
  per node, show LINES → fire COMMANDS → present OPTIONS (filtered by `requires`) or
  follow `next` or END. `Stop()` synchronous + idempotent.
- **Seam** — static `DeNelle.Core.Dialogue.DialogueService`: `Play(id)` / `PlayDef(def)` /
  `Stop()`, events `Opened(vm)` / `Started` / `Ended` / `EndedWithId(id)` (WO-T1).
- **Verbs + conditions** — `DeNelle.Village.DialogueCommandSink`, ONE plain-C# object
  implementing `IDialogueCommandSink` + `IDialogueConditionSource`, registered at boot.
- **View** — `DeNelle.HUD.DialogueView`, code-built uGUI (FrameCore chrome, NOT UXML),
  bound to `DialogueViewModel` (MVVM, WO-744).
- **Compat shim** — `DeNelle.Village.DialogueService` forwards every legacy call site and
  implements the **"transactions = direct panels"** rule in `PlayStructure`.

Flag: `FeatureFlags.CustomDialogue` = `Get("customdialogue", defaultOn: true)`
(`Assets/_Modules/Core/FeatureFlags.cs:145`) — **default ON**. (Comments inside
`DialogueCommandSink.cs:23` still say "default off" — stale.)

## ★ THE COMMAND-REGISTRATION ANSWER (canon correction)

**There is no Yarn command registration host anymore, and `DialogueCommandBridge` does not
exist.** `grep "class DialogueCommandBridge"` finds ZERO declarations in the tree — the
prior audit could not find it because WO-557 deleted it along with `NPCCommandBridge` and
the whole Yarn stack. The name survives only in comments, WO files, and stale docs.

**The ~40 verbs actually live as `case` labels in the `switch` inside
`DialogueCommandSink.Run(verb, args)` — `Assets/_Modules/Village/Tutorial/DialogueCommandSink.cs:79-203`.**
Conditions live in `DialogueCommandSink.Check(condition)` — same file, lines 380-443.
Registration: `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)] Register()`
(`DialogueCommandSink.cs:46-55`) calls `DialogueService.RegisterSink/RegisterConditions`
behind `FeatureFlags.CustomDialogue`.

**The "single-register law" is OBSOLETE.** The Yarn source-generator's
register-exactly-once constraint died with Yarn. Its replacement is structural: duplicate
`case` labels in one switch are a **C# compile error**, so a "double registration" cannot
ship. The live break surface moved: an authored verb the sink does NOT route falls to the
`default:` branch — a `FlowTrace.Warn` no-op (`DialogueCommandSink.cs:198-202`), guarded
by `DialogueRegression` check 5 (every content verb must match a case label).

---

# CODE

## `DeNelle.Core.Dialogue` — `Assets/_Modules/Core/Dialogue/` (4 files)

### `DialogueService.cs` — the ONE launch seam. **WIRED/LIVE.**
Static (`DialogueService.cs:17`). Village registers the sink/conditions at boot; Core never
references gameplay.
- `RegisterSink` / `RegisterConditions` (:23-24).
- Events: `Opened(DialogueViewModel)` (:27 — View builds+binds BEFORE the first line),
  `Started` (:32 — engine-wide input-suppression signal, replaced the Yarn onDialogueStart
  hook), `Ended` (:34), `EndedWithId(string)` (:40 — WO-T1, feeds TutorialSignals).
- `ActiveVm` / `IsRunning` (:42-43).
- `Play(dialogueId)` (:47-69) — `DialogueCatalog.Find` → new VM → `Opened` → `Started` →
  `vm.Begin(def, sink, cond)` (synchronous entry; first line fires inside this call).
  Closed handler uses `ReferenceEquals(ActiveVm, vm)` so a stale close can't null a
  successor's ActiveVm (P0 re-entrancy fix, see DialogueView + AssertDialogueChain).
- `PlayDef(def)` (:80-102) — plays a CODE-BUILT `DialogueDef` with no catalog id (runtime
  pet-engagement prompt). Same flow, same View.
- `Stop()` (:105) — `ActiveVm?.Close()`, synchronous + race-free.

### `DialogueModel.cs` — data shapes + catalog loader. **WIRED/LIVE.**
- `DialogueLine` {speaker (empty = narration), text}, `DialogueOption` {text, requires,
  goto ("" / "end" ends)}, `DialogueCommand` {verb, args}, `DialogueNode` {id, condition
  (enter-gate), lines, commands, options, next}, `DialogueDef` {id, startNode, nodes} with
  `FindNode`/`EntryNode` (first match wins).
- `DialogueSpeaker` {name, affiliation, portrait} (:39-45) — the owner-ratified NPC-card
  standard (2026-07-02): every card shows NAME + AFFILIATION + PORTRAIT; empty portrait =
  styled silhouette. Declared once in the catalog's top-level `speakers` block; replaces
  the imperative per-node `portrait` command (kept as back-compat override).
- `DialogueCatalog` (:111-170) — static loader over
  `Data/Canonical/dialogue/dialogues.json` via `CanonicalJson.Read` (Resources dual-copy
  first, WebGL-safe, mirrors QuestCatalog). `Dialogues`, `Speakers`,
  `FindSpeaker(name)` (case-insensitive), `Find(id)`, `Reload()`. Parse failure →
  `Debug.LogError` + empty catalog (never throws to callers).

### `DialogueRunner.cs` — the state machine. **WIRED/LIVE.**
`sealed class` (:36), plus the two seam interfaces `IDialogueCommandSink` (:25-28) and
`IDialogueConditionSource` (:31-34). Phases Idle/Lines/Options; events `LineShown`,
`OptionsShown`, `Ended` (fires exactly once — `End()` idempotent, :167-176).
- `Begin` (:66-73) — Stops any prior run first; `_active` set BEFORE entry so a
  command-only node (no lines/options/next) fires its commands and ends synchronously —
  the pattern that needed the `<<stop>>` hack in Yarn, now trivial.
- `Advance` (:79-88) — tap-to-continue; no-op while options show.
- `Choose(i)` (:91-100) — goto target or end.
- `PostLines` (:130-165) — commands via `_sink?.Run`, options filtered by
  `_cond.Check(requires)`, then options / `next` / end. A condition-gated node entered
  false just ends (:111-112).

### `DialogueViewModel.cs` — the MVVM VM. **WIRED/LIVE.**
`sealed`, implements `IPanelViewModel` (:13). Owns a private runner; surface:
`IsOpen/Speaker/Text/ShowingOptions/OptionLabels/Title`, `Advance/Choose/Close`,
events `Changed`/`Closed`, `Dispose` (:120-127, unsubscribe discipline).
- **WO-744 projections** (moved OFF the View): `Affiliation` (:34-41, catalog read),
  `PortraitPath` (:47-57 — per-node `portrait` override wins, else speakers-block record),
  `PortraitForced` (:61).
- **WO-702 builder-truce owner** (:63-86): `HiddenForBuilder` + `SetBuilderActive(bool)` —
  publishes `BuildModeState.DialogueHiddenForBuilder = hidden && IsOpen`; cleared in
  `OnEnded` (:155-156) so it can never stick true after teardown (the founding_town
  softlock root).
- `Begin` (:102-112) clears `DialoguePortrait.Forced` before the runner fires this
  dialogue's commands (no portrait leak between conversations).

## Village side — `Assets/_Modules/Village/Tutorial/`

### `DialogueService.cs` (namespace `DeNelle.Village`) — the compat SHIM. **WIRED/LIVE.**
Static (:28). Thin Yarn-FREE forwarder for every legacy call site. **Naming hazard: this
shadows `DeNelle.Core.Dialogue.DialogueService` inside `DeNelle.Village` — Village code
must fully-qualify the Core one** (noted at `DialogueCommandSink.cs:51`).
- `IsRunning` (:31), `NodeExists` (:38-42 — gate OPTIONAL beats; unauthored id = clean
  no-op), `Play(node)` (:49-67 — empty/running/unauthored → Warn + false), `Stop()`
  (:124-131 — walk-away auto-close).
- `CurrentStructureId` / `CurrentStructureName` (:71-75) — set by `PlayStructure`; panels
  that need the focused building read these. (The old Yarn "bare `$var` arrives literal"
  memory is OBSOLETE — there are no Yarn variables; parameters flow through these
  properties.)
- **`PlayStructure(structureId, displayName)` (:83-120) — the vendor routing rule:**
  1. id authored in dialogues.json → CONVERSATION on the custom runner (:90-94);
  2. `BuildingCatalog.Find(id).IsShoppable` → TRANSACTION:
     `PanelRouter.Open(PanelId.PartyShop, structureId)` directly (:99-115) — guarded by
     ticket F8-14: `AmbientNPC.IsCombatActive` → Warn + `BuildFeedbackToast` "Shops closed
     during the assault!" + return true (:106-112);
  3. neither → false, caller's own `TryPanelFor` panel fallback runs (:118-119).
- New-game reset hook (:138-142): registers `Stop` on
  `DeNelle.Core.DialogueResetService.YarnVariableClear` — the hook NAME is stale (Yarn
  gone; the custom runner keeps no variable storage — state lives in
  QuestService/GameState).

### `DialogueCommandSink.cs` — **THE verb + condition registry.** **WIRED/LIVE.**
`sealed`, plain C# (no MonoBehaviour), implements both seam interfaces (:39). Registered
at boot (:46-55) behind `FeatureFlags.CustomDialogue`. Lazily hosts helper components
(`DialogueSinkHost` GameObject → `TutorialAutoWalk`, `TutorialHudOverlay`, :257-267).

**The verb switch (`Run`, :79-203) — ~40 case labels, routing directly to live services:**
- *Panels via `PanelRouter`*: `OpenRumorBoard`, `OpenUpgrade` (BuildingUpgrade+id),
  `OpenShop` (PartyShop; optional a1 = "buy"/"sell" locked-mode, owner F8 2026-07-10),
  `OpenCraft`, `OpenAlchemy` (ConsumableCrafting), `OpenJeweler` (JewelerCrafting),
  `OpenTalents` (→ HeroSkillTree — legacy HeroTalents route removed, EYES-SWEEP
  2026-07-06), `OpenCosmetics`, `OpenRealmStore` (monetization PackStore).
  (`OpenPetSkills` RETIRED 2026-07-08 — pet skill-tree deleted.)
- *Find-or-spawn panels*: `OpenEquip` (:211-217 EquipmentPanel), `OpenArena` (:219-225
  ArenaPanel) — both behind `PanelBlockedByBattle` (WO-437 `BattleLock`, :228-233).
- *Quests → QuestService*: `StartQuest`, `AdvanceQuest`, `CompleteQuest`, `GiveKeystone`,
  `SetFlag`/`SetQuestFlag` (alias pair), `RecruitCompanion` (→
  `GameStateService.AddToParty`).
- *Building upgrades*: `TryUpgradeBuilding` (city tier tree, WO-430); `structure_upgrade`
  (:141-155) — **BLIND-03-02 dual-authority guard**: `CatalogRegistry.ResolveUpgradeId` →
  `BuildingTierCatalog.IsUpgradable` → CITY tier tree WINS for overlapping ids
  (forge/lumbermill), else `ResourceBuildingState.TryUpgrade` (mirrors
  `BuildingUpgradeVM._isCity` precedence).
- *Audio*: `play_sfx` (:237-244 — only `horn_warning` mapped → `GameSfx.PlayLookoutHorn`,
  else UI click).
- *Economy/meta*: `save_game`, `grant_resources_for_towers` (×`TowerCrystalCost` 50, :207).
- *Barracks troops (WO-453)*: `ShowTrainingUI`, `StartTraining` →
  `TroopDialogueCommands` (`Assets/_Modules/Village/Troops/TroopDialogueCommands.cs`).
- *Pets*: `spawn_starting_pet`, `spawn_named_pet` (:291-303 — idempotent
  `PetAcquisitionService.Acquire` + `PetDeployer.DeployChosen`; deployer self-healed
  :306-332), `pet_task` (→ `PetTaskController.ApplyEngagementChoice`).
- *Camera → SmartMobileCamera*: `camera_focus`/`camera_glance`, `camera_shake`,
  `camera_show_all_gates` (small shake stub), `camera_return_to_hero`. Synchronous — the
  Yarn bridge's coroutine auto-restore holds are intentionally omitted (:16-20).
- *HUD → TutorialHudOverlay*: `set_hud_objective`, `set_hud_hint`, `highlight_ui`,
  `unhighlight_ui`.
- *Movement*: `start_autowalk` (:271-279 target resolution :336-360 — companion/pet/hero/
  village_tour/GameObject.Find), `stop_autowalk`, `enable_full_controls` (:281-287 —
  `FinishOnboarding` exactly once).
- *Portrait*: `portrait` → `DeNelle.Core.DialoguePortrait.Forced = a0`.
- `default:` → `FlowTrace.Warn` (:198-202) — stub/timed-wait verbs intentionally no-op,
  never faked.
- Combat shop-guard `ShopsClosedForCombat` (:61-68) applied to
  OpenShop/OpenCosmetics/OpenRealmStore (ticket F8-14).

**The condition grammar (`Check`, :380-443)** — single string key, prefix-parsed:
`!<key>` negation · `quest_<id>_active` / `quest_<id>_done` · `keystone_count_min_<n>`
(tested BEFORE the `keystone_` prefix, :396-398) · `keystone_<name>` ·
`pet_owned_<species>` · `pet_grantable_<species>` (composite: not-owned AND free slot —
the model's `requires` is a single key, so composite keys replace Yarn's two-gate ANDs,
:415-421) · `pet_select_closed` (owns-any AND slots-full, A7 gate, :427-433) ·
`onboarded` · unknown → Warn + false.

## Presentation — `Assets/_Modules/HUD/DialogueView.cs` (~980 lines). **WIRED/LIVE.**
Code-built uGUI, DDOL self-bootstrap behind the flag (:24-38 — a declined gate TRACES,
never silent). Subscribes `DialogueService.Opened` (:83).
- **Chrome**: `ElarionUiKit.BuildObsidianPanel` on **FrameCore** (window family — the
  double-frame F8-1/F8-5 fix, :204-227), canvas `sortingOrder 4800` (:191-197 — above the
  HUD kit 4000 and Echo HUD 4600, below battle 5000 and hard modals). Kit drop-zones
  header/body/footer/medallion; header band grown for 36px Speaker / 26px Affiliation
  (mobile ladder, F8 2026-07-08 :245-257); scrollable body well (§1.14 kit scroll zone).
- **Content-fit sizing** (`ResizeToContent`, :629-761 — owner F8 2026-07-16/17): first
  paint re-pins zones to fixed-pixel bands, then panel height = header + clamped measured
  body + close band (collapsed to 24px margin when Close hidden); body widened to the
  frame's inner border. Traced under `FlowTrace "Dialogue"` ("resize contentH=...").
- **Advance**: tap on body/viewport buttons + ANY keyboard key (mouse excluded, :439-454)
  with 0.25s min-hold; the "Tap to continue" chip and Continue button are REMOVED (owner
  F8 2026-07-10, `_tapHint = null` :355).
- **F8-22 one-action arbitration** (:587-607): exactly ONE of Continue-hint / options /
  shared Close visible; arbitration state traced once per change.
- **P0 re-entrancy guard** (:86-94, :149-159): Closed handler bound PER-VM
  (`OnClosedFor` identity check) — a stale close from a superseded dialogue is IGNORED so
  a Closed-chained successor's panel survives (was the frozen-build-mode /
  input-suppressed-forever root, owner "still cant do the tower" RCA 2026-07-08).
  Probe surface: `IsShowing` (:81).
- **WO-702 builder truce** (:96-106, `TickBuilderTruce` :460-484): while
  `BuildModeState.IsActive`, a live dialogue is HIDDEN, never Closed — closing fires
  `Ended` and would falsely complete a dialogue-gated tutorial step. VM owns the state.
- **WO-795 modal truce** (commit `8ba7154a`, 2026-08-01; :108-117, `TickModalTruce`
  :491-513, `OnArbiterClose` :531-544): while a DIFFERENT arbiter-tracked modal owns the
  screen (`PanelManager.AnyOpen && !_arbiterNotified`) the dialogue hides by the same
  law; an arbiter swap-close HIDES instead of destroying mid-conversation; genuine
  dismissals (ESC/CloseAll with record cleared, or the panel's own X) truly Close. The
  same commit gave `ObjectiveBannerUi` (the coach banner,
  `Assets/_Modules/Core/UI/ObjectiveBannerUi.cs:250-282`) a matching modal fade-out with
  state intact — the "coach/banner truce".
- **Modal arbiter**: registered BATTLE-ALLOWED on first VISIBLE paint (:560-572 —
  registering inside `OnOpened` would trip the isOpen-verify; a command-only dialogue that
  closes before painting never registers, correctly).
- **Speaker card** (:820-884): portrait priority = per-node `portrait` command → speakers
  block (both via VM projections) → class portrait (Knight/Ranger/Wizard/Healer) →
  procedurally-drawn hooded SILHOUETTE (:901-949 — never a raw tinted disc, the "Sylas
  yellow blank" fix). Card composition traced once per speaker.

## Core hooks (`DeNelle.Core`, no gameplay dep)
- `DialoguePortrait.Forced` — `Assets/_Modules/Core/DialoguePortrait.cs` (sink writes via
  `portrait` verb; VM projects; VM.Begin clears).
- `DialogueResetService` — `Assets/_Modules/Core/DialogueResetService.cs` — new-game reset
  seam; hook field still named `YarnVariableClear` (:46, stale name; now wired to
  `Stop` only).
- `BuildModeState.DialogueHiddenForBuilder` — `Assets/_Modules/Core/BuildModeState.cs`
  (WO-702 truce publish; build loop reads it to keep input usable under a hidden dialogue).
- `TutorialSignals` adapter — `Assets/_Modules/Core/Tutorial/TutorialSignals.cs:107-109`:
  `DialogueService.EndedWithId` → raises `dialogue.ended:<id>` (Tutorial V2 step gates).

## Launch seams — who starts dialogue (callers, all through the two services)
- `BuildingInteractable.cs:303` — building tap → `PlayStructure(hookId, label)`, `:314`
  panel fallback `TryPanelFor`.
- `CastleVendorNpcInjector.cs:1186` — vendor NPC Talk → `PlayStructure(_structureId,
  _label)`; walk-away `Stop()` :1119.
- `CastleCompanionIntroducerInjector.cs:459` — `Play(_node)` ("SylasFirstMeeting"),
  once-then-retire.
- `SylasFirstMeeting.cs:196-199` — `NodeExists` gate + `Play`.
- `CompanionMeetingTrigger.cs:158` — `Play("CompanionMeeting")` (FTUE).
- `SylasStewardInjector.cs:326` — `Play(dialogueId)`.
- `TutorialFlow.cs` (V2) — step intro `:525`, mid-step `:682`, outro `:841`, contextual
  `:1257`–1260; intro DEFERRED while the builder is open (`_deferredIntroId`, :521 —
  WO-702) and completion keyed on `dialogue.ended:<id>` (:698-700).
- `PetTaskController.cs:160` — **the only `PlayDef` caller** (runtime code-built
  pet-engagement prompt; busy-check :109 spans both services + BattleLock).
- `TorchWardenDress.cs:290` — dungeon Torch Warden `Play("dun_torch_warden")`; listens
  `EndedWithId` :261. **This is the one dungeon NPC on the main runner.**
- `EchoCardVM.cs:212` — `"echo_first_meeting"` (first Echo card meeting beat).
- `AutoPilotDriver.cs` — probes: `AssertDialogueChain` (:2677, phase :335), popup-close
  oracle via `PlayStructure("market", ...)` (:4623-4760), panel capture via
  `Play("brom_intro")` (:5675), dialogue suppression `Stop()` (:1134-1136).

## NOT on the runner — the bubble/table split (unchanged systems)
These are static line tables rendered by world-space UGUI bubbles/toasts, deliberately
outside `DialogueService`:
- `TownsfolkDialogue.cs` (`Assets/_Modules/Village/NPCs/`) — ambient villager pools: 9
  archetypes (5 original + 4 WO-116 wardens; enum values STABLE, :54-76) + the
  dragon-foreshadow rumor tiers (owner 2026-07-08, `DragonHintTier` Far/Mid/Near/Imminent,
  `DragonWaveId = 4` mirrored from waves.json :106, `TierForWave` :272). Rendered by
  `AmbientNPC` / `TownsfolkBubble`.
- `CompanionDialogue.cs` — per-hero story-companion pools (Grom/Sylas/Thrain/Elara by
  `HeroClass`, :45-55); rendered by `StoryCompanion`.
- **Bryn dungeon path** — `Assets/_Modules/Dungeons/Wanderer/Bryn.cs` +
  `WandererDialogue.cs`: proximity speech bubble via the `IWandererBubble` seam (:38-45;
  MUTE self-report if unwired :139-146), hysteresis radius, line choice (:290-311) =
  fresh-visit canon line from `lore-fragments.json#bryn-cottage-entry` → layout
  `firstEncounterLine` → inlined `HealersCottageLine`; else tier/deaths/clears pick.
  WO-770.7: first-meet/idle lines ALSO surface through a toast sink (:264-271 —
  `DungeonToastView.Show`), which made `WandererDialogue.FirstMeet[]/Idle[]` live.
- `EchoUnlockDialogue` (`Assets/_Modules/Village/Harvest/`) — the Echo awaken/level-up
  portrait CARD; its own canvas, not the runner (oracle: `EchoCardCopyRegression`).
- `LoreFragments` / `DungeonToastView` — dungeon lore surfaces.

## `DeNelle.DialogueUI` — `Assets/_Modules/DialogueUI/` (post-Yarn residue, 2 classes)
asmdef refs `DeNelle.Core`, `Unity.TextMeshPro`, `Unity.InputSystem` only (no Yarn, no
Village). Contains:
- `IntroSequencePlayer.cs` — the ~30s skippable boot VIDEO (WO-569;
  `StreamingAssets/Video/Defenders.mp4` via VideoPlayer URL → RawImage; slate-sequence
  fallback; registers on `Core.IntroLauncher.Play`). Yarn-free (header :36). Not really
  "dialogue" anymore — a relocation candidate.
- `PortraitCache.cs` — static Sprite cache wrapping `HeroPortraits/<Name>` Textures
  (portraits import as Texture2D, so `Resources.Load<Sprite>` is null; caches misses too).
  Used by battle/HUD portrait paths; `DialogueView` does its own `Resources.Load<Sprite>`
  + silhouette instead.

---

# DATA — `dialogues.json`

`Assets/Resources/Data/Canonical/dialogue/dialogues.json` +
`Assets/StreamingAssets/Data/Canonical/dialogue/dialogues.json` — **must stay
byte-identical** (DialogueRegression check 2). **version 2 · 14 speakers · 38 dialogues ·
71 nodes.**

- **Speakers block** (card standard): Miller, Woodcutter, Echo Warden, Arcanist,
  Drillmaster, Brom, Sylas, Coppin, Blacksmith, Armorer, Sable, Herbalist, Echo, Bryn.
- **Structure/vendor cards** (opened by `PlayStructure` when authored): `farm`,
  `lumbermill`, `arcane-tower`, `barracks` (2 nodes each), `market` (4), `forge`,
  `armorer`, `jeweler` (3 each), `apothecary`, `jewelers-bench` (2 each).
- **Story/FTUE**: `SylasFirstMeeting` (8 nodes), `CompanionMeeting` (2), `brom_intro` (3),
  `echo_first_meeting` (1), `pet-house` (9 — the Echo Hollow attunement flow; the ONE
  conversational structure).
- **Tutorial V2 beats** (single-node, played by TutorialFlow) — **ONE arc, ONE speaker: every
  `tut_*` record speaks as the `{guide}` token and nothing else** (WO-1014, dialogues.json v8,
  pinned by `TutorialGuideIdentityRegression` `[tutorial-guide-identity]`).
  Mandatory chain: `tut_founding_greet`, `tut_founding_walk`, `tut_founding_stores`,
  `tut_founding_ack`, `tut_founding_defense`, `tut_founding_timers`, `tut_town_wave`,
  `tut_town_wave_done`, `tut_founding_win`. Unreferenced but kept:
  `tut_founding_hollow`, `tut_founding_hollow_done`, `tut_founding_town`, `tut_founding_echo`.
  Contextuals: `tut_ctx_build_weapons`, `tut_ctx_build_armor`, `tut_ctx_echo_assign`,
  `tut_ctx_first_spend`, `tut_ctx_gear_equip`, `tut_ctx_talents`, and `tut_ctx_two_fights`
  (WO-1014 salvage — authored, NOT yet wired; its trigger is an owner creative pin).
  **RETIRED 2026-08-10 (WO-1014) — deleted, and a re-add now FAILS the regression:**
  `tut_move_to_sylas`, `tut_meet_sylas`, `tut_first_tower`, `tut_first_tower_done`,
  `tut_world_encounter`, `tut_world_encounter_win`, `tut_world_encounter_retry`,
  `tut_return_home`, `tut_freedom`. That legacy arc was spoken by a hard-coded human
  ("Sylas, Scout of the Reach") and ran CONCURRENTLY with the `{guide}` founding arc —
  two narrators, no identity (owner felt-test 2026-08-10). Sylas remains a hero name and
  keeps the separate non-tutorial `SylasFirstMeeting` beat.
- **Dungeon**: `dun_torch_warden` (1).

---

# TESTS / ORACLES

- `Assets/Editor/Regression/DialogueRegression.cs` — **the dialogue SME oracle**
  (markers `DIALOGUE_OK`/`DIALOGUE_FAIL`), 9 checks: catalog parse via the REAL loader ·
  dual-copy byte-equal · id/node/goto/next/reachability integrity · speaker card data ·
  **every content verb matches a `case` label in the sink switch** (scans
  `DialogueCommandSink.cs`, mandates the 6 vendor verbs, :50-54) · every condition key
  parses through the REAL `Check` · runner state machine · VM wiring · **the P0
  re-entrancy guard through the REAL `DialogueService.Play`**.
- `DataRegression.CheckDialogueSpeakers` (`Assets/Editor/Regression/DataRegression.cs:1533-1585`)
  — card standard: every spoken speaker resolves to a record with name + affiliation;
  every DECLARED portrait path loads a sprite (empty = legal silhouette).
- `AutoPilotDriver.AssertDialogueChain` (:2677) — play-mode P0 probe: Play A → Closed-chain
  Play B → assert the successor survives (`DialogueView.IsShowing`), `EndedWithId` fires,
  stale-Closed Warn fires exactly once, input released after close.
- Unit: `Assets/Tests/EditMode/DialogueRunnerTests.cs`,
  `Assets/_Modules/Core/Tests/DialogueViewModelTests.cs`,
  `Assets/Tests/EditMode/CastleCompanionIntroducerTest.cs` (asserts the intro routes via
  `DialogueService.Play`), `Assets/Editor/Regression/TownsfolkDialogueRegression.cs`,
  `EchoCardCopyRegression.cs`, `DungeonToastRegression.cs`.

**FlowTrace tags**: `Dialogue` (service/sink/view/truces/card/resize), `DlgLayout`
(post-layout geometry dump, `DialogueView.cs:398-430`), `UI` (Village shim + WO-795
modal-truce + coach banner), `Tutorial` (flow intro/outro), `Portrait` (PortraitCache),
`Dungeon` (Bryn), `Auto` (probes).

---

# FLAGS / RISK LEDGER

## ⛔ NAME PIN — `Alduin` and `Aldwin` are TWO DIFFERENT CHARACTERS (recorded 2026-09-02)

They are one letter apart, they both appear in authored copy, and **the mistake has now been minted
TWICE, in opposite directions** — once by "correcting" Aldwin into Alduin, once the other way. Write
the pin down so it is not minted a third time.

| Name | Who | Where the canon string lives |
|---|---|---|
| **Alduin the Mournful** | the **NECROMANCER boss** — dungeon lore, `Alduin's journal` | `canon-strings.json`; enemy id `alduin` is registered in `EnemyResolver.HollowTable` with **`CombatSpawnable = false`** (a dialogue NPC, never a boss fight) and resolves to the Boss faction (`Enemies/EnemyResolver.cs:180,333`) |
| **Aldwin, the Ice Echo** | **Echo #1, the founding wolf** — the player's first companion Echo | `EchoRosterCatalog`; harvest affinity **Food** (`economy-meta.md`, `village-systems.md`) |

**TWO regression suites forbid conflating them, and they assert in both directions:**
- `Assets/Editor/Regression/DungeonLoreReadableRegression.cs` (WO-881) — fails if the lore copy loses
  `"Alduin's journal"` **or gains "Aldwin"**; fails if `canon-strings.json` loses
  `"Alduin the Mournful"`; fails if `EchoRosterCatalog` loses `"Aldwin, the Ice Echo"`
  (header `:12-22`, assertions `:100-110`).
- `Assets/Editor/Regression/EchoEngageDialogueRegression.cs` (WO-1031) — `:27`
  *"Aldwin/Alduin are DIFFERENT characters"*, and `:168` carries the instruction verbatim:
  **"Note Aldwin != Alduin the Mournful - do not correct one into the other"**.

⚠ So a spellcheck-style "fix" to either name **fails the gate by design**. If one of these suites goes
red on a name, the bug is almost always the edit, not the assertion.

## Canon corrections (this rewrite)
- **`DialogueCommandBridge`, `NPCCommandBridge`, YarnSpinner packages, ClassicRPG UI,
  `Assets/Dialogue/*.yarn` (64 nodes), `DialogueSystem.prefab`, `IntroCommandBridge`,
  `CompanionDialoguePresenter`, `TalkPromptRegistry`+`TalkHudBridge` reflection bridge,
  `DialogueEventBus` `wait_for_event` blocking** — ALL REMOVED (WO-557). Any doc/WO/memory
  citing them describes the dead stack. The Yarn hazards (source-generator single-register
  law, No-node race, `<<stop>>` hack, bare-`$var`-literal command args, OptionItem
  recursion guard) died with it.
- Verb host = `DialogueCommandSink.Run` switch (see the ★ section).

## Stale comment vs. code
- `DialogueCommandSink.cs:23` "Flag-gated on CustomDialogue (default off)" — flag is
  **default ON** (`FeatureFlags.cs:145`). Yarn coexistence is over; the flag is now a
  kill-switch (OFF renders NO dialogue at all — `DialogueView.Bootstrap` traces this).
- `DialogueResetService.YarnVariableClear` (`:46`) — stale NAME for a live hook (now just
  Stop-on-new-game; no variable storage exists).
- Sink/Shim headers still route-explain "mirrors DialogueCommandBridge" — a deleted class;
  routing parity claims can't be diff-checked against it anymore.
- Several caller comments (`CastleCompanionIntroducerInjector.cs:28`,
  `CastleVendorNpcInjector.cs`) still say "authored Yarn node" — the ids now live in
  dialogues.json.

## Fragile / watch
- **Namespace shadowing**: `DeNelle.Village.DialogueService` shadows the Core one inside
  Village — an unqualified call inside Village silently hits the shim (usually fine, but
  `RegisterSink` etc. exist only on Core; see `DialogueCommandSink.cs:51`).
- **Warn-default verbs**: an authored verb missing from the switch is a logged no-op at
  runtime — only `DialogueRegression` check 5 makes it a gate failure. Keep it green.
- **Single-key conditions**: the model's `requires` is ONE key; ANDs need composite keys
  in the sink grammar (`pet_grantable_` precedent). An author writing `a && b` gets
  "unknown condition → false".
- **Synchronous re-entrancy** is the system's one deep hazard class: `Play` fires the
  first line inside the call, and a `Closed`-handler chaining into the next `Play`
  re-enters. Guarded three ways (per-VM Closed in the View, `ReferenceEquals` in the
  service, `_active`-before-enter in the runner) and locked by AssertDialogueChain +
  DialogueRegression check 9 — do not remove any leg.
- **Truce law (WO-702 / WO-795)**: a live dialogue under a builder or another modal is
  HIDDEN, never Closed — `Ended` falsely completes `dialogue.ended:<id>` tutorial gates.
  Any new overlay must follow the same idiom (commit `8ba7154a` is the template; the
  coach banner `ObjectiveBannerUi` fades by the same rule).
- **Dual-copy divergence**: dialogues.json exists in Resources AND StreamingAssets (plus
  baked copies in Builds/ and Android intermediates). Edit BOTH sources;
  DialogueRegression check 2 byte-compares them.
- **Leftovers**: empty `Assets/Resources/Dialogue/` folder; `DeNelle.DialogueUI` now hosts
  only the video intro + PortraitCache (module name is a misnomer; README status
  unverified this pass).
