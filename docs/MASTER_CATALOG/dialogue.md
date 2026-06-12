# MASTER CATALOG — Dialogue

Reference catalog for the dialogue area. Scope: `Assets/_Modules/DialogueUI`, the
vendored `Packages/dev.yarnspinner.unity.addons.*` addons, `Assets/Dialogue` (.yarn nodes),
plus the live dialogue spine that physically lives in `DeNelle.Village`
(`DialogueService`, `DialogueCommandBridge`, `TalkPromptRegistry`, `TalkHudBridge`) and the
Core hooks it needs. Verified by reading the actual files (not comments).

## Architecture in one breath

Every Yarn conversation in the game plays through **ONE** shared runner. `DialogueService`
(static, in `DeNelle.Village`) hosts-or-reuses `Resources/Dialogue/DialogueSystem.prefab`
(a copy of the paid ClassicRPG Canvas UI — code/Canvas, **not** UXML, so it renders in
WebGL builds), installs **`DialogueCommandBridge`** (the single live command bridge with
~40 verbs/functions) before the runner starts, then plays a node by name. All content
compiles into one `DefendersDialogue.yarnproject` (glob `**/*.yarn`). The presenter on the
prefab is the portrait-aware `CompanionDialoguePresenter` (subclass of ClassicRPG
`RPGDialoguePresenter`).

Three host paths exist, all reusing the same prefab:
- `DialogueService.Play / PlayStructure` → installs `DialogueCommandBridge` (village/vendor/FTUE/structure).
- `IntroSequencePlayer.Play` → installs `IntroCommandBridge` (cinematic fades/audio/transition).
- `CompanionMeetingTrigger` (Village) → hosts prefab, lets it autostart `CompanionMeeting` (FTUE).

---

# CODE

## Live dialogue spine (in `DeNelle.Village` assembly, namespace `DeNelle.Village`)

### `DialogueService` — `Assets/_Modules/Village/Tutorial/DialogueService.cs`
Static game-wide entry point to start Yarn dialogue on the shared runner. **WIRED/LIVE** — the one launch seam.
- `DialogueRunner Current` — `FindObjectOfType<DialogueRunner>()` or null.
- `bool IsRunning` — true while any dialogue plays.
- `bool NodeExists(string node)` — node compiled into shared program? (hosts if needed; never throws).
- `bool Play(string node)` — start a node; false+log on empty/missing/already-running. WebGL crash-guard try/catch around `StartDialogue`.
- `bool PlayStructure(string structureId, string displayName=null)` — opens parameterized `StructureMenu` node (or `PetHouse` for `pet-house`); seeds `$structureId`/`$structureName`; stores `CurrentStructureId`.
- `string CurrentStructureId { get; }` — last PlayStructure id; **read by `CmdStructureStatus` instead of the Yarn arg** (bare command-arg doesn't interpolate → arrives literal `"$structureId"`). Documented memory: yarn-bare-command-arg-literal.
- `void Stop()` — stop current dialogue (walk-away auto-close).
- `private Host()` — Instantiate prefab, `autoStart=false` (caller picks node), install `DialogueCommandBridge` before Start (with WebGL crash-guards).
- Deps: `Yarn.Unity` (DialogueRunner/YarnProject), `DialogueCommandBridge`. Const prefab path `"Dialogue/DialogueSystem"`.

### `DialogueCommandBridge` — `Assets/_Modules/Village/Tutorial/DialogueCommandBridge.cs`
`MonoBehaviour [DisallowMultipleComponent]`. **THE single live command bridge.** Registers EVERY custom Yarn command/function project-wide and delegates to real systems. One per hosted runner; installed by `DialogueService.Host` (and `CompanionMeetingTrigger`).
- `void Install(DialogueRunner runner)` — RegisterCommands, seed `$companionName` now + on `onDialogueStart`.
- ~40 registrations (`Reg` = `AddCommandHandler`; functions via `AddFunction`):
  - **Camera**: `camera_focus`, `camera_glance`(→focus), `camera_shake`, `camera_show_all_gates`, `camera_return_to_hero` → `SmartMobileCamera`.
  - **Audio**: `play_sfx` (only `horn_warning` mapped → `GameSfx.PlayLookoutHorn`, else UI click), `play_music` (inert/log).
  - **Structure (parameterized)**: `portrait`, `structure_status`, `structure_upgrade`, `structure_talk` (stub log).
  - **Movement**: `start_autowalk`, `stop_autowalk`, `enable_player_input`, `enable_full_controls` (→ `GameStateService.FinishOnboarding`).
  - **HUD**: `set_hud_objective`, `set_hud_hint`, `highlight_ui`, `unhighlight_ui` → `TutorialHudOverlay`.
  - **Combat/economy**: `spawn_wave_at_nearest` → `TutorialWaveSpawner`; `grant_resources_for_towers` (×50 crystals).
  - **Pets**: `spawn_starting_pet`, `spawn_named_pet`, `show_pet_name_prompt`, `show_pet_role_choice`, `send_pet_to_harvest`; self-heals a `PetDeployer`.
  - **Quests (WO-290/291)**: `StartQuest`, `AdvanceQuest`, `CompleteQuest`, `SetQuestFlag`, `GiveKeystone`, `RecruitCompanion` (WO-238 → `AddToParty`) → `QuestService`/`GameStateService`. Functions: `HasKeystone`, `KeystoneCount`, `IsQuestActive`, `IsQuestComplete`, `pet_owned`.
  - **Vendor/station verbs (consolidated from dead NPCCommandBridge — WO-106/109/291/304)**: `OpenShop`, `OpenUpgrade`, `OpenCraft`, `OpenEquip`, `OpenArena`, `OpenRumorBoard`, `LearnRecipe` (stub log). Each self-heals its code-built panel host.
  - **Misc/world**: `save_game`, `spawn_npc`/`move_npc`/`grant_pet`/`grant_elder_blessing`/`transition_to` (mostly safe log stubs for world-NPC nodes).
  - **Blocking**: `wait_for_event` (`Func<string,IEnumerator>`) — polls `DialogueEventBus` / spawner / pet-intro; 120s safety timeout.
- `Update()`: one-time subscribe to `TowerPlacementSystem.OnTowerPlaced` → raises `DialogueEventBus "tower_placed"`.
- Lazy sub-systems added as components: `TutorialAutoWalk`, `TutorialWaveSpawner`, `PetIntroduction`, `TutorialHudOverlay`.
- Deps: `DeNelle.Core`, `.Core.Quests`, `.Core.State`, `DeNelle.Pets`, `Yarn.Unity`, `Prog = Village.Buildings.Progression`.

### `TalkPromptRegistry` — `Assets/_Modules/Village/NPCs/TalkPromptRegistry.cs`
Static O(1) self-registering registry of talkable NPCs in range (no per-frame scan — honors OuterWorld-leak hardening). **WIRED/LIVE.**
- `int Count` — talkable NPCs in range now.
- `void Register(Transform node, Action talk)` — idempotent per node.
- `void Deregister(Transform node)` — drop (safe if absent; prunes nulls).
- `Action NearestTalk(Vector3 from)` — talk action of nearest entry, or null.

### `TalkHudBridge` — `Assets/_Modules/Village/NPCs/TalkHudBridge.cs`
`MonoBehaviour`. Gates the HUD Talk button on `TalkPromptRegistry.Count>0` and routes a press to `NearestTalk(hero)`. **WIRED/LIVE.**
- `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)] Bootstrap()` — spawns a `DontDestroyOnLoad` host.
- Reaches HUD `SetTalkAvailable(bool)` + `TalkRequested` UnityEvent by **reflection** (HUD stays out of Core; mirrors `HeroAbilitiesHudBridge`). Throttled 0.25s poll, edge-triggered push, capped ~240 hook attempts.

## `DeNelle.DialogueUI` assembly — `Assets/_Modules/DialogueUI/`
asmdef refs: `YarnSpinner.Unity`, `YarnSpinner.Addons.ClassicRPG`, `UniTask`, `DeNelle.Core`, `DeNelle.Village`. Namespace `DeNelle.DialogueUI`. Isolates the ClassicRPG UI dependency to this one assembly.

### `CompanionDialoguePresenter` — `CompanionDialoguePresenter.cs`
`sealed : RPGDialoguePresenter`. **WIRED/LIVE** — the presenter on the shared prefab (swapped in by `DialogueAdvanceSetup`). Adds dynamic speaker portraits + a light-parchment reskin + speaker name-banner without forking the package.
- `override RunLineAsync(line, token)` — inject `icon:` portrait tag, update name banner, re-assert dark ink, call base.
- `override RunOptionsAsync(opts, token)` — base builds options, then retints option items dark-ink for 4 frames (fixes green-on-parchment unreadability).
- `override OnDialogueStartedAsync()` — one-time: `RepairOptionsLayoutOnce` (VerticalLayoutGroup+ContentSizeFitter so line text & option list stack instead of overlap — WO-337 follow-up), `ReskinToLightParchmentOnce`, `BuildNameBannerOnce`.
- `override OnDialogueCompleteAsync()` — clears `DialoguePortrait.Forced` so it never leaks to the next convo.
- Portrait priority: `DialoguePortrait.Forced` (set by `<<portrait>>`/`structure_status`) → else `HeroPortraits/<CharacterName>`. Pulls sprites from `PortraitCache`. All TMP access via `Graphic` base + reflection (asmdef has **no** Unity.TextMeshPro ref by design).

### `IntroCommandBridge` — `IntroCommandBridge.cs`
`MonoBehaviour [DisallowMultipleComponent]`. **WIRED/LIVE** (installed by `IntroSequencePlayer`). Services the cinematic intro's Yarn commands.
- `void Install(DialogueRunner runner)` — registers `fade_from_black`, `fade_to_black`, `fade_to_white`, `fade_from_white` (UI-Toolkit alpha overlay, no UGUI), `play_sfx` (`Resources/Sfx/<id>` best-effort), `play_music` (`title_theme`→`MusicTrack.Title`), `transition_to` (stop runner → `SceneRouter.GoHeroSelect`).

### `IntroSequencePlayer` — `IntroSequencePlayer.cs`
Static. **WIRED/LIVE.** Hosts the shared prefab for the 9-screen intro and starts node `Intro_Screen1`.
- `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)] Register()` — sets `DeNelle.Core.IntroLauncher.Play = Play` (decoupled trigger; Title's "Play Intro" fires it without referencing the dialogue stack).
- `void Play()` — host-or-reuse runner, `autoStart=false`, install `IntroCommandBridge`, start `Intro_Screen1`.

### `PortraitCache` — `PortraitCache.cs`
Static process-lifetime registry of speaker portrait `Sprite`s. **WIRED/LIVE.**
- `bool Has(string resourcePath)` / `Sprite Get(string resourcePath)` — lazily build (wrapping `Texture2D` since portraits import as Default textures, so `Resources.Load<Sprite>` returns null), cache hits AND misses (one lookup per missing speaker).

### `NPCCommandBridge` — `NPCCommandBridge.cs` — **DEAD / NEUTRALIZED (2026-06-08)**
`MonoBehaviour [DisallowMultipleComponent]`. `Install()` registers **nothing** (logs a warning). Kept only so stale references compile. All its former verbs/functions moved verbatim to `DialogueCommandBridge`. Reason: YarnSpinner source generator statically scans every `Install(IActionRegistration)` and throws "An item with the same key has already been added" on a name registered in two scanned methods → broke ALL dialogue. **No live caller** (`PlaceNpcStation` builder it referenced does not exist).

## Core hooks (`DeNelle.Core`, no Yarn/Village dep)

### `DialoguePortrait` — `Assets/_Modules/Core/DialoguePortrait.cs`
Static cross-assembly hook. `public static string Forced;` — Resources path of a portrait to force (Village bridge writes via `<<portrait>>`/`structure_status`; presenter reads). Cleared on dialogue complete.

### `DialogueEventBus` — `Assets/_Modules/Core/Events/DialogueEventBus.cs`
Static pure-C# signal bus so gameplay raises named events that Yarn `<<wait_for_event NAME>>` blocks on. Case-insensitive, latching.
- `event Action<string> Fired`; `void Raise(name)`; `bool HasFired(name)`; `void Clear(name)`; `void ClearAll()`.
- Live producers: `DialogueCommandBridge.Update` raises `tower_placed` from `TowerPlacementSystem.OnTowerPlaced`. `wait_for_event` also special-cases `wave_cleared` (spawner.IsCleared) and `pet_named`/`pet_role_chosen` (petIntro.IsComplete).

## Editor builder (not in scope dirs but the prefab's origin)

### `DialogueSystemBuilder` — `Assets/Editor/DialogueSystemBuilder.cs` (`DeNelle.Editor`)
One-shot batchmode builder (WO-358) guaranteeing `Resources/Dialogue/DialogueSystem.prefab` exists + wired the way `DialogueService.Host` expects. Delegates to `YarnDialogueSetup.Setup()` (reimport+compile yarnproject, copy ClassicRPG Canvas prefab, wire `yarnProject`/`autoStart`/`startNode`) + `DialogueAdvanceSetup.Wire()` (tap/click advance, swap in `CompanionDialoguePresenter`, hide blue Next indicator). All Yarn access reflection-only. Menu `Defenders/Dialogue/Build DialogueSystem Prefab`; batch `DeNelle.Editor.DialogueSystemBuilder.Build`.

---

# PREFAB

### `Assets/Resources/Dialogue/DialogueSystem.prefab` — **the shared dialogue system (LIVE)**
Copy of the paid ClassicRPG "Classic RPG Dialogue System" prefab: full Canvas UI (WebGL-safe, no UXML). Hosts a `DialogueRunner` (YarnProject = `DefendersDialogue`, `autoStart` true with `startNode` CompanionMeeting — overridden to false by every host path except `CompanionMeetingTrigger`), the `CompanionDialoguePresenter`, typewriter, line/options panels (`Container`→`Line`/`Options`, with `Icon Holder`, `Text`, `Items`). Loaded by path `"Dialogue/DialogueSystem"`.

---

# PACKAGES (vendored Yarn addons)

### `dev.yarnspinner.unity.addons.classicrpg` v3.2.0 — **USED (only addon referenced)**
asmdef `YarnSpinner.Addons.ClassicRPG`, namespace `Yarn.Unity.Addons.ClassicRPG`. Provides the dialogue presenter + Canvas UI prefab that the whole game's dialogue renders through.
- `RPGDialoguePresenter.cs` (858 lines) — `DialoguePresenterBase` subclass; base of `CompanionDialoguePresenter`. Public: `ShowFeatureFlags`, `Typewriter`, `static SetDialogueBoxStyle(string)`, `static HideDialogueBoxes()`, `override OnDialogueStartedAsync/OnDialogueCompleteAsync/RunLineAsync/RunOptionsAsync`. Per-line `#icon:<sprite>` metadata → `Resources.Load<Sprite>` portrait. **Calls `OptionItem.BeginOptionsView()` at line 675** (clears the global submit guard for a fresh options view).
- `OptionItem.cs` — **`Selectable` with a STATIC re-entrancy guard (`s_submitInFlight` + `s_lastSubmitFrame`).** Project-modified (not vanilla): adds `IPointerClickHandler` (mouse click submits), `IPointerEnterHandler` (hover sets EventSystem selection — unifies mouse+keyboard), `Update()` Space/Return submit. The static guard fixes a runaway-recursion crash: `onSubmit` resumes the presenter SYNCHRONOUSLY inline → re-presents options on the same call stack → held Space re-submits the new auto-selected item unbounded. Per-instance `_submitted` can't stop it (each cascade level is a fresh instance) → guard must be global; reset only by `BeginOptionsView()`.
- Other scripts: `ActionButton.cs`, `Letterbox.cs`, `SpeedControllableLetterTypewriter.cs`, `Tweening/Tweening.cs`. Assets: prefabs (`Classic RPG Dialogue System`, `Option Item`, `Palette`), Libertinus/SofiaSans fonts, sprites, shadergraphs, audio. `Samples~/ClassicRPG` (NPC.yarn demo, Fairy, demo scene) — sample, not shipped.

### `dev.yarnspinner.unity.addons.snaaake` v3.2.0 — **VENDORED-BUT-UNUSED (dead in this project)**
asmdef `YarnSpinner.Addons.Snaaake`, namespace `Yarn.Unity.Addons.Snaaake`. Talking-head retro presenter. **No project asmdef references it; no `.cs` uses its types.** Scripts: `RandomImageFill`, `SubtitlePresenter`, `TalkingHeadCharacter(View)`, `TalkingHeadDialoguePresenter`. Full `Samples~/Snaaake` (Snaaake.unity scene, Dwight/MaxForce characters, ~40 dialogue audio wavs). Carrying weight only.

### `dev.yarnspinner.unity.addons.textanimator` v3.1.0 — **VENDORED-BUT-UNUSED (dead in this project)**
asmdef `YarnSpinner.Addons.TextAnimatorIntegration` (+ `.Editor`). Integrates the paid "Text Animator for TMP" asset (NOT present). **No project reference; no project `.cs` uses its types.** Ships THREE parallel versions (`TextAnimatorMarkupManager`/`_v2`/`_v3`, `TextAnimatorYarnTypewriter`/`_v2`/`_v3`) for different TextAnimator releases, plus an editor config window + two `.unitypackage` support bundles. Inert without the Text Animator asset installed.

---

# DATA — Yarn nodes (`Assets/Dialogue/`)

Project: `DefendersDialogue.yarnproject` (projectFileVersion 3, sourceFiles `**/*.yarn`, baseLanguage en, excludes `**/*~/*`). All `.yarn` below compile into the one program. **64 nodes across 21 files** (counts below).

### `_Declarations.yarn` (1 node `_Declarations`)
All `<<declare>>`s — the Yarn variable schema. Notable: `$companionName`, `$petName/$petRole/$petMode`, tutorial `$tutorialPhase/$tutorialStep/$tutorialComplete`, structure menu (`$structureId/$structureName/$structureLevel/$structureYield/$upgradeCostText/$upgradeResult/$structureMaxed`; plus runtime-set caps `$structureCanShop/$structureCanUpgrade/$upgradeType` — set by `CmdStructureStatus`, **not declared here**), soul flags (`$aldricFreed`…`$varenFreed`, `$soulsFreed`), per-vendor quest stages `$q_<vendor>_stage` (forge/armorer/lumber/granary/jeweler/market/inn/stable/steward), `$sylasMet`, and the Forgemasters' saga block (`$saga_act`, `$saga_<x>_seeded`, `$saga_peace_*`, `$saga_comp_*`, `$saga_reforge_choice`, `$saga_reforged`, `$saga_ending_seen`).

### Intro — `Intro/IntroSequence.yarn` (9 nodes `Intro_Screen1..9`)
Cinematic. Uses `<<fade_*>>`, `<<play_sfx>>`, `<<play_music>>`, `<<wait>>`, `<<jump>>`. Many SFX (`heartbeat_slow`, `deep_rumble`, `souls_ascending`, `whispers_faint`, `war_drums_distant`…) have **no `Resources/Sfx/<id>` asset** → `IntroCommandBridge.CmdPlaySfx` no-ops cleanly. Final screen ends with `transition_to` → hero select.

### Tutorial/FTUE — `Tutorial/CompanionMeeting.yarn` (8 nodes: `CompanionMeeting`, `VillageTour`, `FirstTower`, `SecondGateAmbush`, `ResourceGrant`, `QuestCallout`, `PetIntroduction`, `TutorialComplete`)
The prefab's autostart node. Drives the guided onboarding via `camera_focus/glance`, `start/stop_autowalk` (village_tour, gate_1, gate_2), `set_hud_objective/hint`, `spawn_wave_at_nearest`, `grant_resources_for_towers`, pet prompts, and blocking `<<wait_for_event tower_placed / wave_cleared / pet_named>>`. `enable_full_controls` at the end → `FinishOnboarding`.

### Companion — `Companion/SylasFirstMeeting.yarn` (4: `SylasFirstMeeting`, `SylasAccept`, `SylasDecline`, `SylasGreetAgain`)
First-meet branch (guards on `$sylasMet`). `SylasAccept` fires `StartQuest companion.sylas` + `RecruitCompanion Ranger` (→ AddToParty) + `SetQuestFlag …met` + `CompleteQuest`.

### Companion — `Companion/PostTutorialGuidance.yarn` (14: `PostTutorial_WhatsNext`, `PostTutorial_PathOverview`, `Path_Defense/Exploration/Grinding/Discovery`, `Companion_Reminder_Defend/Explore/Upgrade/Pet`, `Companion_FirstNight/FirstKill/FirstExplore/FirstDungeon`)
Post-FTUE guidance + companion barks, gated on `$tutorialComplete`/`$postTutorialGuidanceGiven`; interpolates `$petName`.

### Lore — `Lore/WorldLore.yarn` (9: `Lore_HeartWood`, `Lore_TheOrcs`, `Lore_TheGoblins`, `Lore_TheDragons`, `Lore_TheElementals`, `Lore_Echoes`, `Lore_Elarion`, `Companion_Idle_Combat`, `Companion_Idle_Peace`, `Companion_LevelUp`) — note: 10 titles (world lore + idle/levelup barks).

### Souls — `NPCs/SoulAwakening.yarn` (6: `Soul_FirstAwakening` … `Soul_FifthAwakening`, `Soul_Progress_Check`)
Heartwood soul-freeing beats. Uses world-NPC verbs `spawn_npc`/`move_npc`/`grant_pet` (currently **log-stub** handlers in DialogueCommandBridge) + `camera_focus`, `play_sfx`, set soul flags. Grants like `grant_pet grimhound "Aldric's Grimhound"` are narrative-only stubs today.

### Structures — `Structures/StructureMenu.yarn` (2: `StructureMenu`, `PetHouse`)
The **ONE parameterized building-interaction node** ("rinse and repeat, just the parameter"). `<<structure_status $structureId>>` seeds caps/cost; options gate on `$structureCanUpgrade`/`$structureCanShop` (Upgrade/Buy/Sell/Talk/Leave). `PetHouse` = pet attunement flow: each echo species option gated `<<if not pet_owned("…")>>` → `<<spawn_named_pet "…">>` (ice-wolf/flame-pup/aether-sprite). Comment in-file documents the Yarn-v3 two-command re-entrancy bug that forced folding `<<portrait>>` into `structure_status`.

### Vendors — `NPCs/NPC_*.yarn` — 11 files, stage-aware "stemming" Talk nodes:
- `NPC_Forge.yarn` `TalkToForge` — Borin, quest `vendor.forge`, keystone Emberbrand, stages 0→1→2→3→9; bonded branch seeds saga + opens Craft/Shop/Upgrade/Equip/`SagaBorin`.
- `NPC_Armorer.yarn` `TalkToArmorer`, `NPC_Lumbermill.yarn` `TalkToLumbermill`, `NPC_Granary.yarn` `TalkToGranary`, `NPC_Jeweler.yarn` `TalkToJeweler`, `NPC_Market.yarn` `TalkToMarket`, `NPC_Stable.yarn` `TalkToStable`, `NPC_Steward.yarn` (`TalkToSteward` + `StewardFinale` — keystone-gated meta-quest needing 6 keystones → `SpireAwakened`), `NPC_Arena.yarn` `TalkToArena`, `NPC_Inn.yarn` (`TalkToInn` + `RumorBoard`).
- All use the consolidated verbs `OpenShop/OpenUpgrade/OpenCraft/OpenEquip/OpenArena/OpenRumorBoard` + quest verbs + `HasKeystone`/`IsQuest*` branching.
- `NPC_StableBonds.yarn` (9: `WildHearts`, `BondSproutling/Craghound/Frostkit/Emberpup/Mirewing/Glimmermoth/Stoneback/AetherFox`) — creature-bonding sub-flow.

### Saga — `NPCs/ForgemastersSaga.yarn` (12: `SagaBorin/Halvard/Pell/Wren`, `SagaCheckAct3`, `SagaScene_SteelTruth/BoughForTree/SharedHearth/TheReforging/ReforgeComplete`, `SagaScene_Ending_Heart/Ending_Regions`)
The Forgemasters' saga multi-act questline (reads the `$saga_*` declares; opened from the bonded vendor branches).

### Other input asset
`Assets/Dialogue/DialogueAdvance.inputactions` — input action map for tap/click/press advance (bound by `DialogueAdvanceSetup`).

---

# DOCS

### `Assets/_Modules/DialogueUI/README.md` — **STALE**
Title "DialogueUI — `DeNelle.DialogueUI`". Gist: intro cinematic + companion dialogue presentation layer; lists 4 files (`IntroSequencePlayer`, `IntroCommandBridge`, `CompanionDialoguePresenter`, `PortraitCache`). **Stale: omits `NPCCommandBridge.cs`** (the 5th file in the module). Its self-maintenance note ("update when files are added/removed") was not honored.

### Package READMEs/CHANGELOGs/LICENSEs — vendor docs (classicrpg/snaaake/textanimator), current to their upstream versions; describe the upstream addon, not this project's modifications (e.g. they do NOT mention the OptionItem static-guard or CompanionDialoguePresenter reskin).

---

# FLAGS

## Stale comment vs. code
- **`NPC_Forge.yarn` header (and the same pattern across vendor `.yarn` files)** says quest verbs "come from **NPCCommandBridge** (WO-290)". FALSE — `NPCCommandBridge` is dead/neutralized; ALL verbs are registered on `DialogueCommandBridge`. Comment-vs-code mismatch (the exact class flagged in the prompt).
- **`DialogueUI/README.md`** lists 4 module files but the module has 5 (`NPCCommandBridge.cs` missing) — stale doc vs. directory.
- **`DialogueCommandBridge` header** lists "~30 custom commands" — actual count is ~40 after the WO-290/291/304 + vendor-verb consolidation. Stale count, not load-bearing.
- **`PetHouse`/`spawn_named_pet` comments** still carry a "TODO: free-text pet name UI" and call the flow "name + select your pet", but the live flow only lets the player pick a species (Yarn can't capture free text) — the name is the catalog name, not player-entered. Comment over-claims the feature.

## Dead / duplicate code
- **`NPCCommandBridge`** (`DialogueUI/NPCCommandBridge.cs`) — DEAD: empty `Install` that registers nothing, no live caller, kept only for compile. Its verbs were duplicates that broke the Yarn source generator; behaviour moved to `DialogueCommandBridge`.
- **`dev.yarnspinner.unity.addons.snaaake`** — vendored, ZERO references in project asmdefs or `.cs`. Dead weight (full sample scene + ~40 audio clips).
- **`dev.yarnspinner.unity.addons.textanimator`** — vendored, ZERO references; depends on a paid "Text Animator for TMP" asset that is NOT in the project. Ships THREE redundant versioned copies (`*_v2`/`*_v3`) of each manager/typewriter. Inert.
- ClassicRPG `Samples~/ClassicRPG` (NPC.yarn, Fairy, demo scene) — sample content, not part of the shipping `DefendersDialogue.yarnproject` (it's outside `Assets/`).

## Scene-gated / disabled / stub
- World-NPC verbs `spawn_npc`, `move_npc`, `grant_pet`, `grant_elder_blessing`, `transition_to`, `structure_talk`, `LearnRecipe`, `play_music` are registered but are **log-only stubs / intentionally inert** in `DialogueCommandBridge` (so `SoulAwakening.yarn` etc. never error, but their narrative grants don't actually happen yet).
- Intro SFX (`heartbeat_slow`, `souls_ascending`, etc.) have no `Resources/Sfx/<id>` assets → silently no-op.
- `DialogueSystem.prefab` ships `autoStart=CompanionMeeting`; every host path except `CompanionMeetingTrigger` sets `autoStart=false` immediately after Instantiate (before `Start()`), so the FTUE only auto-runs in the village-entry path.

## Broken / contradictory / fragile
- **Yarn-v3 re-entrancy hazard (handled, but fragile):** synchronous `StartDialogue` prologue + inline `onSubmit` continuations cause two distinct crash classes that are guarded, not eliminated — (1) `OptionItem` runaway-recursion (static `s_submitInFlight`/`s_lastSubmitFrame` guard) and (2) the StructureMenu two-back-to-back-commands `SignalContentComplete` throw (folded `<<portrait>>` into `structure_status`). New nodes that fire two synchronous commands at node entry, or new presenters, can re-trip these.
- **Bare command-arg does not interpolate** (`<<structure_status $structureId>>` arrives as literal `"$structureId"`). Worked around via `DialogueService.CurrentStructureId`; any new command relying on a bare `$var` arg will silently get the literal string (cost hours historically — see memory yarn-bare-command-arg-literal).
- **Single-runner / single-registration constraint:** every Yarn action name must be registered exactly ONCE project-wide or the YarnSpinner source generator throws and breaks ALL dialogue (the reason NPCCommandBridge was neutralized). Adding a second `Install(IActionRegistration)` method that repeats any name will re-break the importer.
