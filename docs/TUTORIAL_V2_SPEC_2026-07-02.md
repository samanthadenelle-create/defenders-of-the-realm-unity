# Tutorial V2 — Audit + Spec (2026-07-02)

**Status: SPEC — READY FOR WO SLICING.** Decided design, not options. Supersedes the WO-277
TutorialDirector FTUE, the OnboardingFlow coach-marks, and the CompanionMeetingTrigger Yarn host
as the first-run teaching surface. Written from a code-level audit (citations inline).

Owner directives baked in (2026-07-02, binding):
- The tutorial **stops and actively helps the player build their FIRST TOWER** — a guided
  build-mode step, pressure paused, with its own funnel telemetry.
- The tutorial teaches **BOTH combat modes explicitly** — town wave-defense and the overworld
  BattleArena encounter are different games with different rules; the player must come out
  knowing that. Tower build precedes the town wave.
- Every NPC dialogue shows **name + guild + portrait** (styled-silhouette fallback — never a
  blank disc). Every popup closable. Everything through the master-frame kit
  (`docs/UI_BLINK_TEMPLATE_CANON.md`). Presentation never does service.

---

## PART 1 — AUDIT: what the tutorial is today

### 1.1 The flow, end to end

Entry: `TitleController` splash gate (**UIToolkit** UIDocument, code-built VisualElements —
`Assets/_Modules/Onboarding/TitleController.cs:62-74`) offers **Play Intro / Start New /
Continue** (`:258-260`). *Start New* → `OnboardingMode.ChooseFastPath()` (`:312`;
`Assets/_Modules/Core/OnboardingMode.cs:30-72`, PlayerPrefs `onboarding.fullTutorial`,
**default = FastPath**) → HeroSelect carousel (UIToolkit,
`Assets/_Modules/Onboarding/HeroSelectController.cs:26-52`) → `SceneRouter.GoCastle()` →
**MainCastle_Hall** (PetSelect usually bypassed via `FeatureFlags.BypassPetSelect`).

In the hub, **three overlapping first-run systems** race, all gated on `GameState.Onboarded`:

1. **`TutorialDirector`** (`Assets/_Modules/Village/Tutorial/TutorialDirector.cs`) — the WO-277
   "seven-scene FTUE", self-bootstrapped `[RuntimeInitializeOnLoadMethod]` (`:105-111`).
   The default **fast path** (`RunFastPath`, `:242-300`) plays **3 hardcoded companion-bubble
   lines** through `TutorialDialogue` (a bubble queue,
   `Assets/_Modules/Village/Tutorial/TutorialDialogue.cs:40-58`), arms two one-shot
   "learn-by-doing" barks (first tower / first breach, `:318-357`), and immediately
   `FinishTutorial()` → `GameStateService.FinishOnboarding()` + `WaveManager.BeginLoop()`
   (`:598-614`). The **full path** is dead code: Scenes 1–7 (`SceneArrivalAndMeeting` …
   `SceneFreedom`, `:422-574`) are "kept for reference but NO LONGER CALLED" (`:198-205`);
   the full path instead defers to…
2. **`CompanionMeetingTrigger`** (`Assets/_Modules/Village/CompanionMeetingTrigger.cs`) — hosts
   the "CompanionMeeting" node (`:53`, `:158`). Its header still says *Yarn* — Yarn is FULLY
   REMOVED (WO-557); it now rides the compat shim
   `DeNelle.Village.DialogueService` (`Assets/_Modules/Village/Tutorial/DialogueService.cs:1-15`)
   into the custom runner. The `CompanionMeeting` dialogue **does** exist in
   `dialogues.json` (line 199) — 2 nodes of Sylas flavour, no teaching steps.
3. **`OnboardingFlow`** (`Assets/_Modules/Onboarding/OnboardingFlow.cs:87`) — the legacy
   6-beat **UIToolkit** coach-mark overlay (`TutorialOverlay.uxml/.uss`), still baked into the
   Village scene by `VillageSceneBuilder.Characters.cs:437-467`, wired by reflection through
   `OnboardingIntegrator` (`Assets/_Modules/Village/OnboardingIntegrator.cs:46,96-145`), and
   suppressed at runtime by TutorialDirector **via reflection** (`TutorialDirector.cs:651-673`).

Plus the standalone **`SylasFirstMeeting`** beat
(`Assets/_Modules/Village/NPCs/SylasFirstMeeting.cs`) — a returning-player recruit beat, stood
down under `FeatureFlags.SingleHero` (`:141`), which duplicates the Sylas meeting lines a third
time as C# constants (`:382-430`) alongside the `SylasFirstMeeting` dialogue in dialogues.json
(line 118) and the `CompanionMeeting` dialogue (line 199).

### 1.2 The tech driving it

- **Narrative**: a mix of (a) hardcoded C# string arrays through a world-space bubble
  (`TutorialDialogue` → `TownsfolkBubble`), and (b) `dialogues.json` nodes through the custom
  `DialogueRunner` (`Assets/_Modules/Core/Dialogue/DialogueRunner.cs:36` — plain C# state
  machine, `ff.customdialogue` **default ON**, Yarn gone). The command vocabulary already
  includes tutorial verbs: `set_hud_objective` / `set_hud_hint` / `highlight_ui` /
  `start_autowalk` / `enable_full_controls` / `grant_resources_for_towers` / `portrait`
  (`Assets/_Modules/Village/Tutorial/DialogueCommandSink.cs:104-135`).
- **Tutorial chrome**: `TutorialHudOverlay` — objective banner + HUD highlight — is **UIToolkit
  on a borrowed-PanelSettings UIDocument** (`Assets/_Modules/Village/Tutorial/TutorialHudOverlay.cs:10-22,44`),
  and its `Highlight()` reaches into the HUD's UIDocument by element name. `PetIntroduction`
  is also UIToolkit (`Assets/_Modules/Village/Tutorial/PetIntroduction.cs:11-16`). All of this
  violates the settled kit canon (`docs/UI_BLINK_TEMPLATE_CANON.md` §1: code-built uGUI only).
- **Persistence**: `GameState.Onboarded` (`GameState.cs:35`) + `GameState.SeenTutorials`
  (`GameState.cs:113`), saved to PlayerPrefs `dotr-save` (`SaveSchema.cs:38`).
  `FinishOnboarding()` (`GameStateService.cs:528-544`) is callable from **three** places —
  `TutorialDirector.SkipToGameplay` (`TutorialDirector.cs:600`), the dialogue verb
  `enable_full_controls` (`DialogueCommandSink.cs:220-226`), and `OnboardingFlow` — so any path
  can mark the player "taught" without teaching.
- **Wave gate**: `WaveManager` auto-arm is blocked while `!Onboarded`
  (`Assets/_Modules/Village/Waves/WaveManager.cs:481-534`) — the "pause the pressure" primitive
  already exists for the first run.

### 1.3 Sylas's role

Sylas ("Scout of the Reach", Ranger) is the tutorial/companion voice: the walk-up recruit beat
(`SylasFirstMeeting.cs`), the `SylasFirstMeeting` + `CompanionMeeting` dialogues, and the
fast-path hook speaker. Under V1 single-hero he never recruits (`SylasFirstMeeting.cs:141`);
he is effectively the **tutorial NPC**.

### 1.4 What breaks or embarrasses today (the 5 worst)

1. **The yellow blank portrait (the reported bug).** `DialogueView.RefreshPortrait`
   (`Assets/_Modules/HUD/DialogueView.cs:204-218`) resolves a speaker portrait via
   `ElarionUiKit.PortraitForClass`, which maps **class words only** — Knight/Ranger/Wizard/Healer
   (`Assets/_Modules/Core/UI/ElarionUiKit.cs:1289-1295`). "Sylas" → null; the
   `SylasFirstMeeting` dialogue authors no `portrait` command; fallback = `CircleSprite` tinted
   `PortraitPlaceholder` — warm tan `(0.74,0.66,0.50)` (`Assets/_Modules/Core/UI/UiStyle.cs:117`)
   = the yellow disc. And **no NPC identity model exists at all**: "guild" appears nowhere in
   `Assets/_Modules` — name+guild+portrait cannot be rendered from data today.
2. **Three overlapping, mutually-suppressing first-run systems** (§1.1): TutorialDirector's dead
   full path (`TutorialDirector.cs:198-227`), CompanionMeetingTrigger still documented as a Yarn
   host (`CompanionMeetingTrigger.cs:2-40`), OnboardingFlow coach-marks still baked into
   Village and suppressed by reflection (`TutorialDirector.cs:651-673`), and Sylas's meeting
   authored in **three places** (C# constants, `SylasFirstMeeting` json, `CompanionMeeting` json).
   Nobody can say what a new player actually sees without running it.
3. **The default path teaches nothing.** FastPath = 3 bubble lines + 2 one-shot barks
   (`TutorialDirector.cs:242-300, 318-357`). No movement teach, no guided build, no combat-mode
   teach, no spend teach — and it's hardcoded strings, invisible to the dialogue system,
   localization, and data tooling.
4. **Tutorial + whole onboarding UI is on the banned stack.** `TutorialHudOverlay`
   (`TutorialHudOverlay.cs:44` — UIDocument), `PetIntroduction` (`PetIntroduction.cs:11-16`),
   `OnboardingFlow`/`TutorialOverlay.uxml`, and the entire title flow (`TitleController.cs:62`,
   `HeroSelectController.cs`, `StoryIntroController.cs:43`) are UIToolkit — the conformance
   audit's finding stands. None of it goes through `BuildObsidianPanel`.
5. **Zero telemetry, zero bot coverage.** `EventTracker`
   (`Assets/_Modules/Core/Analytics/EventTracker.cs:109`) emits `session_start`,
   `wave_completed`, `purchase_completed`, `playtest_break`… — **no `tutorial_*` event
   anywhere**. AutoPilot (`Assets/_Modules/DevTools/AutoPilotDriver.cs`) has phases for seams,
   vendors, economy, encounters — **no tutorial phase**. The single most valuable focus-group
   funnel is unmeasured and unregressed, and `FinishOnboarding` is reachable from three call
   sites so "completed tutorial" doesn't even mean anything.

---

## PART 2 — THE SPEC

### 2.0 Shape (one paragraph)

The tutorial becomes a **data-driven step registry** (`tutorial-steps.json`) walked by a **thin
runtime interpreter** (`TutorialFlow`). Each step declares a trigger, a dialogue id (Sylas
speaks through the SAME custom dialogue system + master-frame template as every NPC), a UI
highlight target, a completion condition bound to an event the game already emits, and a
skippable flag. Presentation is entirely the kit (one spotlight affordance, the standard NPC
card). Every step enter/complete/skip/drop goes through EventTracker. Every step is
bot-completable headless, with `FlowTrace.Fail` as the oracle. Adding or reordering steps =
editing JSON. Gated behind **`ff.tutorialv2`** during migration.

### 2.1 Data — `tutorial-steps.json`

Canonical location (same convention as dialogues.json):
`Assets/StreamingAssets/Data/Canonical/tutorial/tutorial-steps.json` (+ Resources mirror).

```json
{
  "version": 1,
  "flowId": "ftue_v2",
  "steps": [
    {
      "id": "first_tower",
      "order": 30,
      "scene": "MainCastle_Hall",
      "trigger":    { "type": "prev_complete" },
      "pausePressure": true,
      "dialogue":   { "intro": "tut_first_tower", "outro": "tut_first_tower_done" },
      "highlight":  ["hud.build_button", "build.palette.watchtower", "build.valid_cell"],
      "grant":      { "prepaidTower": true },
      "completion": { "signal": "build.tower_placed" },
      "skippable":  true,
      "objective":  { "text": "Raise your first watchtower", "count": 1 }
    }
  ]
}
```

Field semantics (all declarative; the interpreter owns zero content):

| field | meaning |
|---|---|
| `trigger` | `prev_complete` (default chain), `scene_enter:<name>`, or `signal:<busId>` (event-triggered steps, e.g. a contextual step armed until an event fires) |
| `pausePressure` | while the step runs, the wave loop stays frozen (`WaveManager` first-run gate — already blocks auto-arm while `!Onboarded`, `WaveManager.cs:481-534`; the interpreter additionally holds a `TutorialPressureLock` so mid-tutorial steps that FOLLOW the town wave don't re-open it) |
| `dialogue` | ids into `dialogues.json`; Sylas's lines live there and render through `DialogueView` + `BuildObsidianPanel(FrameDialogue)` like every NPC — **no bubble-queue fork, no C# string arrays** |
| `highlight` | ids into the **highlight registry** (§2.2) — HUD element / panel element / world anchor to spotlight, in order |
| `completion` | ONE signal id from the **signal bus** (§2.1b); the step completes when it fires |
| `skippable` | shows the shared Skip affordance; skip fires `tutorial_step_skip` and advances |
| `objective` | the kit objective banner text + progress |

**2.1b The signal bus — reuse what the game already emits.** `TutorialSignals` (Core) is a thin
adapter that maps existing events to stable string ids — no new gameplay events:

| signal id | existing source |
|---|---|
| `hero.reached:<anchor>` | `TutorialAutoWalk.HasArrived` / a proximity probe on `HeroLocomotion` (same pattern as `AutoPilotProbes.CheckSeamReachable`, `AutoPilotProbes.cs:701`) |
| `dialogue.ended:<id>` | `DialogueRunner.Ended` (`DialogueRunner.cs:55`) |
| `build.mode_entered` | `BuildModeController.BuildModeChanged` (`BuildModeController.cs:50`) |
| `build.tower_placed` | `TowerPlacementSystem.OnTowerPlaced` (`TowerPlacementSystem.cs:39`) + the `BuildModeController.Place` commit (`BuildModeController.cs:864-920`) — the redone build flow (§2.3) unifies these behind one placement-committed event |
| `wave.cleared` | `WaveManager.OnWaveCleared` (`WaveManager.cs:260`) / `TutorialWaveSpawner.IsCleared` |
| `arena.resolved:win` | `BattleArena.OnBattleEnded` (`BattleArena.cs:191`, raised at `:1490`) |
| `panel.opened:<PanelId>` | `PanelRouter.Open` |

Every signal both drives the interpreter AND writes `FlowTrace.Step("Tutorial", …)` — one
instrumentation seam for humans, bots, and telemetry.

**2.1c The interpreter — `TutorialFlow` (DeNelle.Village).** A single small MonoBehaviour:
load steps → for each: wait trigger → `EventTracker.Track("tutorial_step_enter")` → apply
`pausePressure`/`grant` → play intro dialogue → arm highlights → await completion signal
(with a FlowTrace-`Warn`ed generous failsafe, never a wedge) → play outro → track complete →
next. On the last step it is the **ONLY** caller of `GameStateService.FinishOnboarding()`
in the V2 path (the `enable_full_controls` verb and OnboardingFlow's calls are removed, §2.6).
It contains **no content, no UI construction, no economy** — data in, signals out.

### 2.2 Presentation — everything through the master-frame kit

- **Dialogue** = the existing reference implementation: `DialogueView` +
  `BuildObsidianPanel(frameName: RpgUiCatalog.FrameDialogue)` (`DialogueView.cs:79-81`). The
  tutorial adds nothing bespoke.
- **NPC identity card (fixes the yellow portrait, for EVERY NPC).** New canonical data
  `npcs.json` (`Assets/StreamingAssets/Data/Canonical/npcs/npcs.json`):
  `{ "id": "sylas", "name": "Sylas", "guild": "Scouts of the Reach", "portrait": "Portraits/sylas" }`.
  `DialogueView` resolves the speaker through this registry: portrait → `PortraitCache`
  (`PortraitCache.cs:39` — already handles Texture→Sprite); guild renders as a small
  `ParchmentDim` sub-line under the speaker name in the header zone. Fallback chain:
  authored `portrait` command → npcs.json portrait → class portrait → **styled silhouette**
  (the kit's crest silhouette glyph on the obsidian disc with the gold ring — a deliberate
  design, never the flat tan disc). `PortraitForClass` stays for party frames; dialogue no
  longer depends on it for named NPCs.
- **Spotlight/arrow affordance — built ONCE in the kit.** `ElarionUiKit.Spotlight(RectTransform
  target)` / `SpotlightWorld(Transform anchor)`: a dimmed full-screen cutout + gold pulse ring +
  animated chevron, uGUI, closable/ignorable (never input-locks the screen — it guides, the
  completion signal advances). Targets resolve through a **highlight registry**
  (`TutorialHighlightRegistry.Register("hud.build_button", rt)`) that HUD/panels populate as
  they build — replaces `TutorialHudOverlay.Highlight`'s UIDocument name-reach
  (`TutorialHudOverlay.cs:16-18`).
- **Objective banner** — rebuilt as a kit strip (top-centre, obsidian+gold, uGUI), replacing the
  UIToolkit `TutorialHudOverlay` banner. Exposed as `set_hud_objective`'s new backing so the
  dialogue verb keeps working.
- **Every popup closable**: all tutorial surfaces use the kit's ONE shared Close; Skip is the
  dialogue-panel footer affordance on skippable steps.
- **Presentation never does service**: the spotlight/banner/card read the step model only; all
  grants, pressure locks, and completion logic live in `TutorialFlow`/services.

### 2.3 V1 flow — the decided step order

Nine steps, each short and verifiable. Town combat ≠ world combat is a **first-class teaching
arc** (steps 4–7): the player builds a tower, defends the town WITH it, then is explicitly told
"out there is different" and fights the arena. `pausePressure` holds the wave loop everywhere
except step 5.

| # | id | teaches | completion signal | skippable |
|---|---|---|---|---|
| 1 | `move_to_sylas` | movement (stick/WASD) — "walk to the scout by the gate" + world spotlight on Sylas | `hero.reached:sylas_anchor` | yes |
| 2 | `meet_sylas` | talk/interact; the NPC card (name + guild + portrait); tap-to-advance | `dialogue.ended:tut_meet_sylas` | yes |
| 3 | `first_tower` **(owner directive — guided build)** | build mode, end to end, pressure paused: Sylas stops the world ("before the horns sound, we build") → spotlight `hud.build_button` → **the REDONE build flow** (the build-mode UI is on the conformance redo list — kit-less today with economy reads inside `BuildPaletteUI.cs:362-377`; the tutorial drives the redone, kit-conformant flow, NOT the current View) → spotlight the watchtower card → spotlight a valid placement cell (from `IsValidPlacement`, `BuildModeController.cs:633-729`) → prepaid placement → confirm → **Sylas reacts** (`tut_first_tower_done`) | `build.tower_placed` | no (core teach) — but time-boxed with a Sylas re-prompt, never a wedge |
| 4 | `town_wave` **(combat mode 1 — town)** | wave defense: Sylas names the mode ("in town, they come in WAVES at the gates — your towers hold the line and you fight beside them") → horn → `TutorialWaveSpawner.SpawnAt(gate, 3)` scripted gentle wave at the gate the tower covers → fight → clear → Sylas debrief | `wave.cleared` | no |
| 5 | `world_encounter` **(combat mode 2 — world)** | the overworld arena: Sylas walks the player toward the world exit ("out there is DIFFERENT — no walls, no towers; when something finds you, it's you alone in the fight, start to finish") → a **staged rep** (one `OverworldEncounterSpawner.SpawnRep` at a fixed near anchor, guaranteed reachable) → engage → `BattleArena.BeginEncounter` → lock-on/abilities called out by the arena HUD → win → auto-return | `arena.resolved:win` (a loss re-stages the rep + a Sylas encourage line; `tutorial_step_drop` if abandoned) | no |
| 6 | `return_home` | the return/seam + "two worlds" recap: Sylas contrasts the two fights in one line each | `hero.reached:hub_anchor` | yes |
| 7 | `spend_reward` | economy loop: grant (`grant_resources_for_towers` verb, `DialogueCommandSink.cs:104-106`) → spotlight the shop/upgrade surface → open it | `panel.opened:PartyShop` | yes |
| 8 | `wave_basics` | the real loop ahead: the DEFEND button, countdown, "fortify every gate" objective | `dialogue.ended:tut_wave_basics` | yes |
| 9 | `freedom` | close: Sylas's send-off; `FinishOnboarding()`; wave loop arms (`WaveManager.BeginLoop`) | `dialogue.ended:tut_freedom` | — |

All Sylas content = new `tut_*` dialogues in `dialogues.json` (canon tone), consolidating the
three duplicated Sylas sources; the C# `MeetingLines` constants and fast-path strings are deleted.

**"Start New" vs "Play Intro"**: Tutorial V2 IS the default new-game path (it replaces FastPath's
3 lines — it is short enough). "Play Intro" additionally plays the 9-screen cinematic first.
`OnboardingMode` stops branching the *teaching* (it only gates the cinematic). Returning players
(`Onboarded == true`) never see it; steps are also individually replayable later via
`SeenTutorials` keys (`tutorial_v2:<stepId>`).

### 2.4 Telemetry — the funnel (the most valuable focus-group metric)

Through the existing rail (`EventTracker.Track`, `EventTracker.cs:109` →
`api/events/track.js` → Neon `analytics_events`; works on WebGL/Pi, Windows, editor):

- `tutorial_started` `{flowId, mode: "new"|"intro"}`
- `tutorial_step_enter` / `tutorial_step_complete` / `tutorial_step_skip`
  `{stepId, order, seconds, attempt}`
- `tutorial_step_drop` `{stepId, secondsIdle}` — the failsafe timer fired or the session ended
  mid-step (queue persistence on quit already exists, `EventTracker.cs:346-393`, so drops
  from closed tabs still land next session)
- **Guided-build sub-funnel** (owner directive): `tutorial_build_enter` →
  `tutorial_build_armed` (card picked) → `tutorial_build_placed` → `tutorial_build_completed`,
  each `{seconds}` — placement friction is the #1 thing we want the focus group to measure
- **Combat-mode sub-events**: `tutorial_wave_cleared` `{seconds, heroDeaths}` and
  `tutorial_arena_result` `{won, seconds, attempt}`
- `tutorial_completed` `{totalSeconds, skips}`

The funnel query is then one GROUP BY over `event_name, properties->>'stepId'`.

### 2.5 Bot verifiability — tutorial regression forever

New AutoPilot phase **`RunTutorial`** in `AutoPilotDriver` (follows the existing
`AssertEncounterBattle` template: real seams, `RunPhase` watchdog, `FlowTrace.Fail` oracle):

1. Fresh state: `GameStateService.ResetToNewGame()` (resets `Onboarded` + `SeenTutorials`,
   `GameStateService.cs:770-807`); boot `MainCastle_Hall` with `ff.tutorialv2` on.
2. For each step, the bot performs the **player action through the real seam** — never a
   bypass: `HeroLocomotion.SetAutoWalk` to the anchor (1, 6), `DialogueService` advance
   (2, 8, 9 — the existing dialogue-suppressor `SuppressDialogue`, `AutoPilotDriver.cs:454-469`,
   is DISABLED for this phase), `BuildModeController` enter → arm watchtower → place at a
   `IsValidPlacement`-verified cell (3), fight the scripted wave with the existing combat
   drive (4), walk into the staged rep and win the arena (5) — the arena already has headless
   oracles (`ArenaCombatOracle` reads the `FlowTrace("BattleArena")` lines), `PanelRouter`
   open (7).
3. Oracle: every step must emit its `tutorial_step_complete` FlowTrace line within
   `TimeoutFor(step)`; a miss = `FlowTrace.Fail("Tutorial", "STEP-STUCK:<id> …")` → error →
   `BreakCaptureHarness` → `break-log.jsonl` → ticket (`AutoPilotTickets`). Final oracle:
   `Onboarded == true` AND `WaveManager` phase left Idle.
4. **DataRegression invariants** (editor gate, runs with the compile gate): every step's
   `dialogue` ids exist in `dialogues.json`; every `highlight` id is a registered registry key;
   every `completion.signal` is a known bus id; `order` strictly increasing; every NPC speaker
   used by `tut_*` dialogues exists in `npcs.json` **with a resolvable portrait or an explicit
   silhouette** (the yellow-disc class of bug becomes a build failure).

### 2.6 Migration

**Reused (no rewrite):** `DialogueRunner`/`DialogueService`/`DialogueCommandSink` + verbs,
`DialogueView` (+ NPC card extension), `TutorialWaveSpawner`, `TutorialAutoWalk`,
`OverworldEncounterSpawner.SpawnRep` (+ a fixed-anchor staging overload), `BattleArena` +
`OnBattleEnded`, `WaveManager` first-run gate + events, `TowerPlacementSystem.OnTowerPlaced` /
`BuildModeController` events, `GameState.Onboarded`/`SeenTutorials`, `EventTracker`,
`ElarionUiKit`, `PortraitCache`, AutoPilot/`BreakCaptureHarness`/`DataRegression`.

**Deleted (after `ff.tutorialv2` flips default-ON and the owner felt-verifies):**
- `TutorialDirector` (both paths — fast-path strings and the dead Scenes 1–7), `CompanionSpawner`
  use inside it, the reflection suppressor.
- `CompanionMeetingTrigger` (stale Yarn host) + the `CompanionMeeting` dialogue (folded into
  `tut_*`).
- `OnboardingFlow` + `TutorialOverlay.uxml/.uss` + `OnboardingIntegrator` +
  `VillageSceneBuilder.BuildOnboardingFlow` (legacy UIToolkit coach-marks).
- `TutorialHudOverlay` (UIToolkit) — replaced by the kit banner/spotlight; the
  `set_hud_objective`/`highlight_ui` verbs re-point to the new backing.
- `SylasFirstMeeting.MeetingLines` C# constants (json wins); the component itself stays parked
  behind `SingleHero` until companions return.
- `enable_full_controls`' `FinishOnboarding` side-effect (`DialogueCommandSink.cs:220-226`) —
  `TutorialFlow` becomes the single finisher.
- `TutorialDialogue`/`TownsfolkBubble` stays for ambient NPC barks only — no longer a tutorial
  delivery channel.

**Slices (WO-sized, in order):**

| slice | scope | gate |
|---|---|---|
| **WO-T1 — Registry + interpreter + telemetry** | `tutorial-steps.json` schema + loader, `TutorialSignals` bus, `TutorialFlow` interpreter, `tutorial_*` events, `ff.tutorialv2` (default OFF), DataRegression invariants | headless: steps chain on synthetic signals; events land in Neon |
| **WO-T2 — Kit affordances + NPC card** | `ElarionUiKit.Spotlight`/`SpotlightWorld` + highlight registry, kit objective banner (retire `TutorialHudOverlay`), `npcs.json` + DialogueView name/guild/portrait card + styled-silhouette fallback (**fixes the Sylas yellow portrait for every NPC**) | screenshot-compare vs the Blink dialogue template; Sylas card renders name+guild+art |
| **WO-T3 — Guided build step** | step 3 against the **redone build flow** (depends on the build-mode conformance redo WO — kit chrome, economy out of `BuildPaletteUI`); placement-committed signal; pressure lock; build sub-funnel | bot completes a tower placement end-to-end or `FlowTrace.Fail` |
| **WO-T4 — Combat-mode steps + content** | steps 4–6: scripted town wave (reuse `TutorialWaveSpawner`), staged-rep world encounter (fixed-anchor `SpawnRep`), loss-retry; all `tut_*` Sylas dialogues authored in `dialogues.json` | bot clears the wave AND wins the arena headless; funnel events distinct per mode |
| **WO-T5 — Bot phase + flip + delete** | AutoPilot `RunTutorial` phase; flip `ff.tutorialv2` default ON; delete the legacy list above; canon update (MASTER_CATALOG, PIPELINE_STATE, this doc → SHIPPED) | full-tutorial bot run green in the fleet; owner felt-verifies; funnel visible in Neon |

---

## CREATIVE SCOPE DECISION (2026-07-02)

**Status: DECIDED (creative director, owner-delegated).** This section scopes the mandatory
guided flow vs. contextual just-in-time teaching. It trims the §2.3 nine-step list to
**seven mandatory steps** and defers harvest, store, gear, and talents to a contextual
one-shot hint system built on the SAME registry.

### The ruling principle

The mandatory tutorial teaches exactly one thing: **the core loop as a felt experience** —
*defend home → venture out → grow stronger → defend better*. Every step that is the loop stays
mandatory. Everything that is a *system the loop feeds* (economy, gear, talents, harvest) is
taught the moment the game hands the player a reason to care — a first-touch contextual hint —
because teaching a menu before the player holds the thing it spends is teaching by reading, not
doing. For a Pi-browser focus group with short sessions, every mandatory minute must buy loop
comprehension; menus bought contextually cost zero funnel drop.

### The four calls

**1. HARVEST (echo workforce) — OUT of the mandatory flow. Contextual, in two beats.**
The system is passive-by-design ("passive to play, engaging to watch" — COMBAT_PIVOT_NORTHSTAR)
and per ECHO_WORKFORCE_SPEC the first echo is **auto-born and auto-assigned** — there is no
player action to guide, so a mandatory step would be a lecture, not a teach. But the world-tree
fantasy DOES belong in the tutorial emotionally: it lands as a **shown moment inside the final
`freedom` step** — after the arena win, Sylas turns the player to the Heart, the first echo
visibly drifts out ("you drove the dark back — and the Tree *answered*"), no gate, no
interaction, pure payoff. The **interactive** teach (drag-drop assignment / silo dump) fires
contextually at its natural earned moment: **when Echo 2 is born** (wave 5) — "New Echo joined!"
is already a designed event; that celebration IS the tutorial for assignment. Splitting it this
way gets the emotion at minute six and the mechanic at the hour it matters, instead of neither
landing at minute four.

**2. STORE / SPENDING — OUT as a gated step. Contextual on first affordable moment.**
The proposed `spend_reward` step (open a panel to proceed) is the definition of front-loading a
menu before the player cares. Cut it. Instead: the town-wave clear **grants the reward
on-screen** (the loot toast is kept — earning stays in the mandatory flow, it's the "grow
stronger" beat), and a one-shot contextual hint (spotlight on the shop/upgrade surface + one
Sylas line) fires the **first time the player can afford something after the tutorial ends**.
The funnel keeps measuring it — `contextual_step_*` events — but a browser player is never held
hostage by a shop screen.

**3. GEAR EQUIP — contextual.** Trigger: **first gear item enters the inventory** (first drop
or first purchase). One-shot spotlight on the equipment surface + "iron serves no one in a
chest." Equipping before owning anything worth equipping teaches nothing; equipping the sword
you just won teaches itself.

**4. TALENTS — contextual, confirmed.** Trigger: **first skill point earned**. Same one-shot
pattern. Talents are the deepest menu in the game and the least urgent at minute one; the
skill-point toast is the invitation.

### The mandatory seven (with each step's emotional job)

| # | id | emotional beat — what the player FEELS |
|---|---|---|
| 1 | `move_to_sylas` | **"I'm here, and it responds to me."** Agency in ten seconds; the world has a person waiting for you. |
| 2 | `meet_sylas` | **"Someone knows my name — and this place has stakes."** Warmth + the first hint of threat (the horns). The NPC card makes the world feel authored, not placeholder. |
| 3 | `first_tower` | **"I built that. It's MINE."** Ownership. The pause-the-world framing ("before the horns sound, we build") makes building feel like preparation, not homework. *(owner directive — unchanged)* |
| 4 | `town_wave` | **"My tower and I held the line — together."** Vindication: the thing you just built visibly earns its keep in the same minute. This is *defend home*. |
| 5 | `world_encounter` | **"Out here I'm alone — and I'm enough."** Danger + competence. The contrast with step 4 is the whole point: no walls, no towers, just you. This is *venture out*. *(owner directive — both modes explicit, unchanged)* |
| 6 | `return_home` | **"Home means something now."** Relief + the two-worlds recap in one line each. Walking back INTO the walls you defended closes the loop spatially. |
| 7 | `freedom` | **"The world itself is responding to me — and it's all mine now."** The wave reward lands, the Tree stirs, the first echo drifts out, Sylas steps back. Send-off = loop restated as a promise: *defend, venture, grow, defend better.* |

Changes vs. §2.3: former step 7 (`spend_reward`) and step 8 (`wave_basics`) are cut from the
mandatory chain. `wave_basics`'s one load-bearing line (the DEFEND button / "fortify every
gate") folds into the `freedom` send-off dialogue — it's one sentence, not a step. The reward
grant moves to the `town_wave` clear where it's earned. The echo/tree reveal is a scripted
non-gated moment inside `freedom`.

**Estimated completion: 5–7 minutes** (steps 1–2 ≈ 60s, tower ≈ 90s, wave ≈ 90s, encounter
≈ 120s incl. travel, return + freedom ≈ 60s). Under the "minutes, not tens of minutes" target
with margin for a first-time fumble; the 9-step version risked 10+ with two menu steps at the
lowest-energy point of the session. Skip remains available on steps 1, 2, 6 per §2.3; 3–5 stay
unskippable (the loop IS the tutorial).

### The contextual just-in-time system (what must exist)

Reuse the §2.1 registry — **no second system.** A contextual step is a `tutorial-steps.json`
entry with `"flowId": "contextual"` and `trigger: { "type": "signal:<busId>" }` instead of
`prev_complete`, plus one new field `"oneShot": true` (persist via the existing
`SeenTutorials` key `tutorial_ctx:<stepId>` — fires once per save, ever). Behavior: on trigger,
play the (short!) Sylas line + spotlight; **never `pausePressure`, never gate** — a contextual
hint is ignorable by definition and auto-dismisses on first interaction with the target.
Telemetry rides the same rail: `contextual_step_enter/complete/dismiss` with `{stepId,
triggerSignal}` — so the focus-group data shows whether just-in-time teaching converts (did
they equip within 60s of the hint?). Bot coverage: the `RunTutorial` AutoPilot phase extends
with post-tutorial probes that synthesize each trigger signal and assert the hint fires once
and only once. Initial contextual registry: `ctx_echo_assign` (signal `echo.born:2`),
`ctx_first_spend` (signal `economy.can_afford_upgrade`, first true after `Onboarded`),
`ctx_gear_equip` (signal `inventory.gear_added:first`), `ctx_talents` (signal
`skillpoint.earned:first`). New bus ids are additions to the §2.1b `TutorialSignals` adapter —
same pattern, existing events.

### Reasoning, compressed

The nine-step draft was right about the spine and wrong about the tail: it ended the tutorial
in menus (`spend_reward`, `wave_basics` — two consecutive steps of being told things) exactly
when a short-attention browser player has spent their patience and earned their freedom. The
seven-step cut ends instead on the game's two best feelings back to back — *I survived alone
out there* and *the world tree answered* — and hands every menu to the moment the player is
already reaching for it. One concept per step holds; time-to-first-delight is ~90 seconds
(first tower); and the funnel gets sharper, not weaker: mandatory events measure loop
comprehension, contextual events measure system adoption, and the two are no longer
confounded in a single chain.

---
*Sources: code audit 2026-07-02 (TutorialDirector, SylasFirstMeeting, OnboardingFlow,
TitleController, DialogueRunner/View/CommandSink, WaveManager, BattleArena,
OverworldEncounterSpawner, BuildModeController, BuildPaletteUI, TowerPlacementSystem,
EventTracker, AutoPilotDriver/Probes, GameStateService). All file:line cites verified against
the working tree on branch `wip/village2-and-f8-tickets`.*
