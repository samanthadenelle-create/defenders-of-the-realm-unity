// =============================================================================
// TutorialFlow — the Tutorial V2 thin interpreter (WO-T1, spec §2.1c).
// -----------------------------------------------------------------------------
// Walks the tutorial-steps.json registry (TutorialStepCatalog): for each
// mandatory step — wait trigger → track tutorial_step_enter → apply
// pausePressure/grant → play the intro dialogue (Sylas through the SAME custom
// dialogue system as every NPC) → arm spotlight + objective banner → await the
// ONE completion signal (TutorialSignals) with a generous STEP-STUCK watchdog →
// play outro → track complete → persist SeenTutorials → next. On the last step
// it is the ONLY V2-path caller of GameStateService.FinishOnboarding() and it
// kicks WaveManager.BeginLoop (the same handoff the legacy director does).
//
// WO-T3/T4 self-driving steps (2026-07-03): grant.prepaidTower credits one
// watchtower's crystals through GameStateService (idempotent per save); a step
// completing on wave.cleared spawns the scripted teaching wave via
// TutorialWaveSpawner (and polls its IsCleared, since it bypasses the wave
// loop); a step completing on arena.resolved:win stages ONE guaranteed rep via
// the OverworldEncounterSpawner factory chain once the hero crosses out.
//
// Contextual one-shot steps (flowId "contextual") ride the SAME registry: armed
// on their trigger signal, they play a short line + spotlight, never pause
// pressure, never gate, auto-complete, and persist "tutorial_ctx:<id>" so they
// fire once per save, ever.
//
// It contains NO content, NO UI construction (Core affordances only), NO
// economy — data in, signals out. Gated behind ff.tutorialv2 (default OFF);
// the legacy TutorialDirector stands down while the flag is ON (WO-T5 deletes it).
//
// Bot verifiability: FlowTrace.Step on step enter/complete, FlowTrace.Fail
// "STEP-STUCK :: <id>" after WatchdogSeconds without progress — headless
// oracles read the [Flow:Tutorial] lines.
// =============================================================================

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;
using DeNelle.Core.Tutorial;
using DeNelle.Core.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using CoreDialogue = DeNelle.Core.Dialogue;

namespace DeNelle.Village
{
    /// <summary>
    /// Data-driven tutorial interpreter (spec docs/TUTORIAL_V2_SPEC_2026-07-02.md).
    /// Self-bootstraps on hub scenes when <see cref="FeatureFlags.TutorialV2"/> is ON.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TutorialFlow : MonoBehaviour
    {
        // ── Tuning ────────────────────────────────────────────────────────────
        private const float SettleSeconds = 1.25f;      // scene settle (hero/NavMesh/HUD) — same as legacy
        private const float WatchdogSeconds = 120f;     // generous in-step bound → STEP-STUCK oracle
        // F8 seq 603 (STEP-STUCK :: founding_hollow, 2026-08-02): on a blank Build-Your-Own
        // town, PLACEMENT steps (completion "build.structure_placed:<id>") are legitimately
        // slow — the player browses the carousel, pans, and places into an empty field. Those
        // steps get this longer bound (per-kind const, matching the house style of the other
        // tuning consts above); the watchdog ALSO pauses outright while the builder is open
        // (TickWatchdog), so only truly-idle time counts against either bound.
        private const float PlacementWatchdogSeconds = 300f;
        private const float ReachedRadius = 6f;         // hero.reached:<anchor> proximity (m)
        private const float ContextualAutoCloseSeconds = 10f; // hint without dialogue: auto-dismiss

        // F8 seq 632 ROOT CAUSE 3 (2026-08-02): a step may author SEVERAL highlights
        // (founding_defend = ["hud.wave_button", "world.gate_direction"]) but UiSpotlight is a
        // singleton with ONE target, so only Highlight[0] was ever shown and the rest were
        // silently dropped. The flow now WALKS the whole list, rotating the single spotlight
        // across every authored id so each one is actually rendered.
        private const float HighlightCycleSeconds = 4f;

        // F8 seq 632 ROOT CAUSE 4 (2026-08-02): five silent minutes must never be possible. While a
        // step is stranded and the builder has NOT been opened, re-state the objective as a toast on
        // this cadence (escalating coach beat), capped so it can never become spam.
        private const float CoachNudgeSeconds = 45f;
        private const int CoachNudgeMaxBeats = 4;

        /// <summary>Persistence key prefixes (SeenTutorials — additive, no schema change:
        /// GameState.SeenTutorials is an existing SerializableDict, SaveSchema.cs:254).</summary>
        private const string SeenPrefix = "tutorial_v2:";
        private const string CtxSeenPrefix = "tutorial_ctx:";
        private const string GrantSeenPrefix = "tutorial_v2_grant:";

        // ── WO-T3: prepaid-tower grant ("this first one is on me") ─────────────
        // One watchtower's crystal cost, credited through the SAME store BuildMenu
        // charges (GameStateService.AddCrystals -> GameState.Resources.Crystals,
        // BuildMenu.OnConfirmBuild). 150 = the Flame/Ice Tower cost — Flame is the
        // menu's default-selected variant AND (with Ice) the only variant the stub
        // material counts can afford (BuildMenu.GetMaterialCount: wood 20 / stone 5
        // — Stone Tower needs stone 10, Aether needs 8, so both are un-buildable
        // today). Owner-tunable; move to catalog data with the WO-31 "Week 6" table.
        private const int PrepaidTowerCrystals = 150;

        // ── Scripted town wave (spec step 4: "HORN BLAST", no Start-Wave press) ─
        private const int TownWaveCount = 3;

        // ── Staged world encounter (spec step 5: a guaranteed rep, not a hunt) ──
        private const float StagedRepMinDistance = 10f;   // ahead of the hero — outside AggroRange(8)
        private const float StagedRepMaxDistance = 16f;

        /// <summary>True while a pausePressure step runs — a hold OTHER systems may
        /// consult so mid-tutorial steps that FOLLOW the town wave don't re-open the
        /// loop (spec §2.1 pausePressure). The WaveManager first-run gate (!Onboarded)
        /// already covers the whole first run; this lock is the explicit seam.</summary>
        public static bool PressureHeld { get; private set; }

        // ── Probe/observability surface (AutoPilot AssertTutorialArms, F8-29) ──
        // Read-only: lets the headless probe assert "fresh save => flow is LIVE, not
        // parked Finished" without reflection. No behaviour hangs off these.

        /// <summary>Current interpreter phase name (probe read — e.g. "Settling", "AwaitCompletion", "Finished").</summary>
        public string PhaseName => _phase.ToString();

        /// <summary>True when the mandatory chain is parked <c>Finished</c> (returning player,
        /// already ran this session, or — the F8-29 failure — declined a fresh run).</summary>
        public bool IsFinished => _phase == Phase.Finished;

        /// <summary>True once a mandatory chain has started this session (the
        /// <c>s_ranThisSession</c> resume block) — a probe uses this to tell a legitimate
        /// mid-session Finished from the fresh-boot decline.</summary>
        public static bool RanThisSession => s_ranThisSession;

        /// <summary>Current mandatory step id, or null when idle/finished (probe read).</summary>
        public string CurrentStepId => _step != null ? _step.Id : null;

        /// <summary>The live step's intro dialogue id (WO-702: SylasStewardInjector routes a
        /// manual Talk to this — replaying the current beat's line; null when none).</summary>
        public string CurrentIntroDialogueId =>
            _step != null && _step.Dialogue != null ? _step.Dialogue.Intro : null;

        /// <summary>
        /// F8 2026-07-08 ("died in tutorial") + the RECURRING F8 "enemies never spawn on the whole
        /// first run": TRUE only while the hero stands in the roster-less VILLAGE zone during the
        /// EARLY in-town FTUE. The ambient hostile spawners (WaveManager auto-loop, OverworldEncounter
        /// ring/scatter reps, RegionMobSpawner) consult this and stay OFF so the player cannot be
        /// killed while learning to build the town.
        /// <para>
        /// DECOUPLED from the whole first run (owner fix): the old definition suppressed for the
        /// entire run (ff.tutorialv2 and !Onboarded), and Onboarded only flips true when the tutorial
        /// COMPLETES/skips -- so leaving town never resumed spawns. The peace window still lifts the
        /// instant the hero ventures OUT of the village (the zone test below), resuming ambient spawns
        /// WITHOUT cancelling the tutorial.
        /// <para>
        /// END-AFTER-DEFEND (owner ruling 2026-07-24): the venture-out back half (world_encounter,
        /// return_home, freedom) was SCRAPPED, so the chain now ENDS at founding_defend. There is no
        /// longer an arena.resolved:win step, so VentureOutOrder returns int.MaxValue -- the ORDER
        /// boundary never trips and suppression holds for the whole (shorter) in-village tutorial,
        /// lifted normally by Onboarded when FinishFlow runs after the defend beat (the zone test still
        /// resumes spawns the moment the hero leaves the village even mid-tutorial).
        /// </para>
        /// Suppressed only when ALL hold: <c>ff.tutorialv2</c> on AND <see cref="GameState.Onboarded"/>
        /// false (outer guard -- a completed player is NEVER suppressed); a live TutorialFlow with the
        /// flow still running; the current step is BEFORE the venture-out beat (now moot -- no such
        /// step exists post-scrap, so this order test never trips); and the hero is in
        /// the roster-less Village zone (RegionSpawnTable.HasRoster false -- the SAME zone test the
        /// staged-rep probe uses). Any missing ref (no instance / no hero / thrown zone lookup) FAILS
        /// OPEN (returns false = spawns allowed) -- it never NREs and never wedges the world empty.
        /// <para>
        /// NOTE: the tutorial's OWN scripted encounters do NOT route through the gated ambient paths
        /// (the founding_defend teaching wave spawns via TutorialWaveSpawner ->
        /// WaveManager.SpawnEnemyForExternalMode; the staged world_encounter rep via EnemyFactory
        /// directly), so they still fire regardless of this flag -- only the AMBIENT sources suppress.
        /// </para>
        /// </summary>
        public static bool HostilesSuppressedForTutorial
        {
            get
            {
                // Outer guard: only the V2 FTUE, and only until onboarding completes. Once Onboarded
                // flips true (FinishFlow / SkipAll -> FinishOnboarding) this is NEVER suppressed
                // again -- a returning player always has live hostiles.
                if (!FeatureFlags.TutorialV2) return false;
                var svc = GameStateService.Instance;
                if (svc == null || svc.State == null || svc.State.Onboarded) return false;

                // Decoupled from !Onboarded ALONE (the recurring bug): the peace window now holds
                // ONLY while the hero is in-town and early in the FTUE. No instance / not started
                // yet -> fail open (spawns allowed), never suppress the whole first run.
                var flow = s_instance;
                if (flow == null) return false;
                return flow.IsInTownEarlyFtue();
            }
        }

        // -- FTUE peace window (decoupled from !Onboarded) ---------------------
        // The single live instance (set in Awake, cleared in OnDestroy) lets the static
        // HostilesSuppressedForTutorial getter the spawners already call consult the running
        // flow's current step + the hero's zone. One instance ever (Bootstrap dedupes).
        private static TutorialFlow s_instance;

        // Per-frame memo: HeroHealth consults the peace window on EVERY damage tick, so the zone
        // lookup runs at most once per frame no matter how many spawners ask.
        private int _suppressFrame = -1;
        private bool _suppressCached;

        // Cached order of the venture-out step (world_encounter -- the arena.resolved:win beat).
        // int.MinValue = uncomputed. END-AFTER-DEFEND (2026-07-24): that step was scrapped, so the
        // scan finds nothing and this resolves to int.MaxValue -- the order boundary never trips and
        // suppression is gated by the zone test alone (held in-village, lifted on leaving / Onboarded).
        private int _ventureOutOrder = int.MinValue;

        /// <summary>TRUE only while the hero stands in the roster-less Village zone during the early
        /// in-town FTUE (before the venture-out beat). Fails open on any missing ref.</summary>
        private bool IsInTownEarlyFtue()
        {
            if (_suppressFrame == Time.frameCount) return _suppressCached;
            _suppressFrame = Time.frameCount;
            _suppressCached = ComputeInTownEarlyFtue();
            return _suppressCached;
        }

        private bool ComputeInTownEarlyFtue()
        {
            // Flow must be live -- a fresh run in progress, never idle/returning/finished.
            if (_phase == Phase.Idle || _phase == Phase.Finished) return false;

            // Boundary: suppress only for steps BEFORE the venture-out beat. END-AFTER-DEFEND
            // (2026-07-24): the venture-out steps are scrapped, so VentureOutOrder is int.MaxValue and
            // this order test never trips -- founding_defend (order 45, the final step) stays suppressed
            // and its teaching wave still fires (TutorialWaveSpawner bypasses this gate). Suppression
            // then lifts via Onboarded when FinishFlow runs after defend, or the moment the hero leaves
            // the village (the zone test below). During the pre-first-step Settling window
            // (_step == null) treat as before-boundary.
            if (_step != null && _step.Order >= VentureOutOrder()) return false;

            // Zone: the hero must be in the roster-less Village zone. HasRoster is TRUE only in
            // OuterWorld roster regions (the SAME test as the staged-rep zone check below), so
            // in-village = NOT HasRoster. Resolve the hero lazily; no hero -> fail open.
            var hero = _hero != null ? _hero : (_hero = FindAnyObjectByType<HeroLocomotion>());
            if (hero == null) return false;

            Vector3 heroPos = hero.transform.position;
            bool hasRoster = true;   // fail-open: a thrown zone lookup => treat as not-in-village
            Guard.Try("Tutorial", "hostile-suppression zone check", () =>
                hasRoster = DeNelle.Core.World.RegionSpawnTable.HasRoster(
                    DeNelle.Core.World.ZoneManager.GetZone(heroPos)));
            return !hasRoster;
        }

        /// <summary>Order of the venture-out step (world_encounter -- the arena.resolved:win beat):
        /// the boundary below which the in-town peace window holds. int.MaxValue when no such step
        /// exists (data change) or the chain has not loaded yet -- the zone test still gates it.
        /// END-AFTER-DEFEND (2026-07-24): that step was scrapped from the chain, so this now ALWAYS
        /// returns int.MaxValue -- kept as a graceful no-op boundary; the zone test is the live gate.</summary>
        private int VentureOutOrder()
        {
            if (_ventureOutOrder != int.MinValue) return _ventureOutOrder;
            if (_steps == null) return int.MaxValue;   // chain not loaded yet -- do not cache
            int found = int.MaxValue;
            foreach (var s in _steps)
            {
                if (s == null || s.Completion == null) continue;
                if (string.Equals(s.Completion.Signal, TutorialSignals.ArenaWin, StringComparison.OrdinalIgnoreCase))
                { found = s.Order; break; }
            }
            _ventureOutOrder = found;
            return found;
        }

        private enum Phase { Idle, Settling, WaitTrigger, Running, AwaitCompletion, Finished }

        private List<TutorialStepDef> _steps;            // mandatory chain (ordered)
        private List<TutorialStepDef> _contextual;       // contextual one-shots
        private int _index = -1;
        private Phase _phase = Phase.Idle;
        private TutorialStepDef _step;
        private float _stepEnteredAt;
        private float _watchdogAt;
        private int _skips;
        private float _flowStartedAt;
        private bool _completionArmed;
        private string _awaitSignal;

        // Highlight walk (ROOT CAUSE 3) — every authored id for the live step, rotated through the
        // single UiSpotlight so none is silently dropped.
        private readonly List<string> _highlightIds = new List<string>();
        private int _highlightIndex;
        private float _nextHighlightAt;

        // Coach escalation (ROOT CAUSE 4) — per-step nudge bookkeeping.
        private int _coachBeats;
        private float _nextCoachAt;
        private bool _builderOpenedThisStep;

        private HeroLocomotion _hero;
        private WaveManager _wave;

        // Scripted town-wave runtime state (step 'town_wave', WO-T4).
        private TutorialWaveSpawner _tutorialWave;
        private bool _townWaveArmed;          // scripted wave requested for the current step
        private bool _townWaveSpawnSettled;   // SpawnAt finished — IsCleared is meaningful now

        // Staged world-encounter runtime state (step 'world_encounter', WO-T4).
        private bool _stagedRepPending;       // waiting for the hero to cross into OuterWorld
        private bool _stagedRepDone;          // one rep staged (or staging abandoned) this step
        private float _nextStageProbeAt;      // 1 Hz staging probe

        // Contextual runtime state.
        private TutorialStepDef _activeCtx;
        private float _ctxEnteredAt;

        private static bool s_ranThisSession;

        // =====================================================================
        //  Bootstrap (no scene edit) — mirrors the legacy director's pattern
        // =====================================================================

        // F8-29 (owner fresh-boot "i did not get a tutorial", RCA 2026-07-08): this was a ONE-SHOT
        // AfterSceneLoad that evaluated the hub gate against the TITLE scene (IsHub false) and —
        // unlike its sibling bootstraps (CompanionMeetingTrigger, CastleCompanionIntroducerInjector)
        // — never subscribed sceneLoaded, so the V2 interpreter could NEVER construct in the real
        // Title -> HeroSelect -> hub flow. Proof: zero [Flow:Tutorial] lines in the fresh session.
        // Now: evaluate at boot AND on every scene load; the decline path traces (never silent).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnAnySceneLoaded;
            SceneManager.sceneLoaded += OnAnySceneLoaded;
            TryArm("boot");
        }

        private static void OnAnySceneLoaded(Scene scene, LoadSceneMode mode) => TryArm("sceneLoaded");

        private static void TryArm(string reason)
        {
            string scene = SceneManager.GetActiveScene().name;
            if (!FeatureFlags.TutorialV2)
            {
                FlowTrace.Step("Tutorial", $"Bootstrap({reason}): ff.tutorialv2 OFF — dormant.");
                return;
            }
            if (!HubScenes.IsHub(scene))
            {
                FlowTrace.Step("Tutorial", $"Bootstrap({reason}): scene '{scene}' is not a hub — waiting.");
                return;
            }
            // F8 seq 632 ROOT CAUSE 1 (2026-08-02) - the flow could arm where building is IMPOSSIBLE.
            // Village2 is BOTH a HubScenes.Names entry AND ownership:"Enemy" in scene-configs.json.
            // BuildModeController.Enter() refuses outright on an enemy-owned scene, so EVERY placement
            // step ("build.structure_placed:<id>") is a GENUINE can-never-complete state there: the
            // player taps the spotlit BUILD button and nothing happens, forever. The owner sat 300s on
            // founding_hollow and was auto-advanced by the watchdog.
            //
            // WHY THIS GATE AND NOT step.Scene: TutorialStepDef.Scene is DEAD DATA (Phase.WaitTrigger is
            // declared and never assigned; the only .Trigger reads are the contextual path), and the
            // authored value is "MainCastle_Hall" - a LEGACY scene, NOT the live hub
            // (Main_Castle_Overworld, CLAUDE.md sec.7). Honouring it would stop the FTUE from EVER
            // running. The correct, smaller fix is the semantic one: the FTUE is a TOWN flow, so refuse
            // to arm anywhere its steps cannot complete. sceneLoaded re-evaluates, so walking back into
            // the home hub still arms it.
            if (HubScenes.IsEnemyOwnedScene(scene))
            {
                FlowTrace.Warn("Tutorial", $"Bootstrap({reason}): hub scene '{scene}' is ENEMY-OWNED " +
                    "(scene-configs.json ownership=Enemy) - build mode is refused there, so the founding " +
                    "placement steps could never complete. NOT arming; re-evaluated on the next scene load.");
                return;
            }
            if (FindAnyObjectByType<TutorialFlow>() != null) return;
            var go = new GameObject("TutorialFlow");
            go.AddComponent<TutorialFlow>();
            // WO-854 Silo E (2026-08-04): the signal adapters are NOT added here any more.
            // TutorialSignalAdapters now runs its own RuntimeInitializeOnLoadMethod bootstrap,
            // because this arm path returns early on ff.tutorialv2 OFF and on an enemy-owned hub -
            // which meant every wave/build/arena signal existed only while the FTUE was armed, and
            // story-quest stages that complete off those signals silently inherited the flag.
            // Adding the component here again would stand up a SECOND emitter host on a latching bus.
            go.AddComponent<TutorialWorldAnchors>();     // world.sylas / world.gate_direction resolvers
            FlowTrace.Step("Tutorial", $"Bootstrap({reason}): TutorialFlow armed in hub '{scene}'.");
        }

        private void Awake()
        {
            // Publish the single live instance so the static HostilesSuppressedForTutorial getter
            // the spawners call can reach this flow's current step + hero zone. Set as early as
            // possible (before Start) so the peace window can hold from the first settle frame.
            s_instance = this;
        }

        private void Start()
        {
            var svc = GameStateService.Instance;
            var state = svc != null ? svc.State : null;

            _steps = TutorialStepCatalog.MandatorySteps();
            _contextual = TutorialStepCatalog.ContextualSteps();
            TutorialSignals.Raised += OnSignal;

            bool firstRun = state != null && !state.Onboarded && !s_ranThisSession;
            if (firstRun && _steps.Count > 0)
            {
                s_ranThisSession = true;
                _flowStartedAt = Time.unscaledTime;
                DeNelle.Core.Analytics.EventTracker.Track("tutorial_started", new
                {
                    flowId = TutorialStepCatalog.FlowId,
                    mode = OnboardingMode.FullTutorial ? "intro" : "new",
                });
                FlowTrace.Step("Tutorial", $"flow '{TutorialStepCatalog.FlowId}' started ({_steps.Count} steps).");
                _phase = Phase.Settling;
                _stepEnteredAt = Time.unscaledTime;
            }
            else
            {
                _phase = Phase.Finished;   // returning player — contextual watchers only
            }
        }

        private void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
            TutorialSignals.Raised -= OnSignal;
            PressureHeld = false;
        }

        // =====================================================================
        //  Update-driven state machine (headless-friendly; no UI dependency)
        // =====================================================================

        private void Update()
        {
            switch (_phase)
            {
                case Phase.Settling:
                    if (Time.unscaledTime - _stepEnteredAt >= SettleSeconds)
                    {
                        ResolveSceneRefs();
                        AdvanceToNextStep();
                    }
                    break;

                case Phase.AwaitCompletion:
                    TickDeferredIntro();   // WO-702 truce: release a builder-held intro
                    TickHighlightCycle();  // ROOT CAUSE 3: walk EVERY authored highlight
                    TickCoachNudge();      // ROOT CAUSE 4: never five silent minutes again
                    TickProximityProbe();
                    TickScriptedWave();
                    TickStagedEncounter();
                    TickWatchdog();
                    break;
            }

            TickContextual();
        }

        private void ResolveSceneRefs()
        {
            _hero = FindAnyObjectByType<HeroLocomotion>();
            _wave = FindAnyObjectByType<WaveManager>();
        }

        // =====================================================================
        //  Mandatory chain
        // =====================================================================

        private void AdvanceToNextStep()
        {
            var svc = GameStateService.Instance;
            var state = svc != null ? svc.State : null;

            while (true)
            {
                _index++;
                if (_index >= _steps.Count) { FinishFlow(); return; }
                _step = _steps[_index];
                if (_step == null || string.IsNullOrEmpty(_step.Id)) continue;

                // Resume support: a step already completed on this save never replays.
                if (state != null && state.SeenTutorials != null &&
                    state.SeenTutorials.TryGetValue(SeenPrefix + _step.Id, out bool seen) && seen)
                {
                    FlowTrace.Step("Tutorial", $"step '{_step.Id}' already seen — resuming past it.");
                    continue;
                }

                // Prebuilt / Default-Town skip (owner ruling 2026-07-24): a build-teaching step marked
                // skipIfPrebuilt is SKIPPED when the town is already laid out (BaseLayout carries the
                // Default-Town seed signature). CRITICAL: apply its GRANTS first (founding_hollow grants
                // the starterPet — a Default-Town player must NOT be left pet-less), reusing the exact
                // idempotent grant path SkipAll uses (ApplyPrepaidTowerGrant / ApplyStarterPetGrant),
                // then mark it seen so a resume never replays it — but do NOT play its intro dialogue.
                // A Build-Your-Own (blank template) town has no such signature (IsTownPrebuilt false),
                // so the kept build-teaching steps still run in full.
                if (_step.SkipIfPrebuilt && IsTownPrebuilt())
                {
                    FlowTrace.Step("Tutorial", $"step '{_step.Id}' skipIfPrebuilt — town is prebuilt (Default Town); " +
                        "applying its grants + marking seen, intro suppressed (owner ruling 2026-07-24).");
                    if (_step.Grant != null && _step.Grant.PrepaidTower) ApplyPrepaidTowerGrant(_step);
                    if (_step.Grant != null && _step.Grant.StarterPet) ApplyStarterPetGrant(_step);
                    GameStateService.Instance?.MarkTutorialSeen(SeenPrefix + _step.Id);
                    continue;
                }
                break;
            }

            EnterStep(_step);
        }

        private void EnterStep(TutorialStepDef step)
        {
            _stepEnteredAt = Time.unscaledTime;
            _watchdogAt = Time.unscaledTime;
            _completionArmed = false;
            _awaitSignal = step.Completion != null ? step.Completion.Signal : null;
            _coachBeats = 0;
            _nextCoachAt = Time.unscaledTime + CoachNudgeSeconds;
            _builderOpenedThisStep = false;

            FlowTrace.Step("Tutorial", $"STEP-ENTER :: {step.Id} (order={step.Order}, completes on '{_awaitSignal}').");
            DeNelle.Core.Analytics.EventTracker.Track("tutorial_step_enter", new
            {
                stepId = step.Id,
                order = step.Order,
                flowId = TutorialStepCatalog.FlowId,
            });

            // Pressure hold — the WaveManager first-run gate (!Onboarded,
            // WaveManager auto-arm block) covers the whole first run; this exposes
            // the explicit per-step lock for anything else that pushes pressure.
            PressureHeld = step.PausePressure;

            // Grant (WO-T3): the guided-build prepaid tower — credit one watchtower's
            // cost through the SAME economy seam BuildMenu charges. Idempotent per save.
            if (step.Grant != null && step.Grant.PrepaidTower)
                ApplyPrepaidTowerGrant(step);

            // PART 2 (guided-build tolerance): a build step whose demanded structure is
            // ALREADY placed (free-carousel player, or a tower dropped before the step
            // armed) must not burn the 120s watchdog. If BaseLayout already satisfies the
            // completion signal, complete NOW down the normal path (grants + outro + advance)
            // instead of clearing the latch and waiting on an event that already fired.
            if (TryAutoCompleteAlreadyBuilt(step)) return;

            // CLEAR the completion latch BEFORE the intro plays: a stale earlier raise
            // must not complete the step, but a raise DURING the intro must (e.g. a
            // dialogue.ended completion that IS the intro's own end).
            if (!string.IsNullOrEmpty(_awaitSignal)) TutorialSignals.Clear(_awaitSignal);
            _completionArmed = true;
            _phase = Phase.AwaitCompletion;

            // WO-T4 self-driving combat steps — keyed on the COMPLETION SIGNAL (data-
            // driven: no step-id branching), so the json stays the only content source.
            //  * wave.cleared      -> spawn the scripted teaching wave (spec step 4:
            //    "HORN BLAST" — the step must complete WITHOUT the player pressing
            //    Start Wave; the loop is held closed by the !Onboarded gate anyway).
            //  * arena.resolved:win -> stage ONE guaranteed rep once the hero crosses
            //    into OuterWorld (spec step 5 — no hunting for a random encounter).
            if (string.Equals(_awaitSignal, TutorialSignals.WaveCleared, StringComparison.OrdinalIgnoreCase))
                StartScriptedTownWave(step);
            if (string.Equals(_awaitSignal, TutorialSignals.ArenaWin, StringComparison.OrdinalIgnoreCase))
            {
                _stagedRepPending = true;
                _stagedRepDone = false;
                _nextStageProbeAt = 0f;
                FlowTrace.Step("Tutorial", $"step '{step.Id}' will stage one guaranteed rep once the hero enters an OuterWorld roster region.");
            }

            // Presentation (Core kit affordances — read the step model only).
            if (step.Objective != null && !string.IsNullOrEmpty(step.Objective.Text))
                ObjectiveBannerUi.Show(step.Objective.Text, step.Objective.Count,
                    step.Skippable ? (Action)SkipCurrentStep : null,
                    (Action)SkipAll);   // persistent whole-FTUE skip (confirmed in the banner)
            ArmHighlights(step);

            // Intro dialogue — the standard NPC template; its end raises
            // dialogue.ended:<id> through the Core adapter.
            // WO-702 truce (owner F8 2026-07-13 "pause the sylas dialogue till either
            // action asked is completed or closed builder"; captured collision:
            // STEP-STUCK :: founding_town — the intro opened BEHIND the build palette
            // and sat unread 120s): while the builder is open, HOLD the intro and play
            // it when build mode exits (TickDeferredIntro). The completion gate stays
            // dialogue.ended — the dialogue simply presents when it can be read.
            if (step.Dialogue != null && !string.IsNullOrEmpty(step.Dialogue.Intro))
            {
                if (DeNelle.Core.BuildModeState.IsActive)
                {
                    _deferredIntroStepId = step.Id;
                    _deferredIntroId = step.Dialogue.Intro;
                    FlowTrace.Step("Tutorial",
                        $"deferred-intro :: step '{step.Id}' intro '{step.Dialogue.Intro}' held while the builder is open (WO-702 truce) — plays on builder exit.");
                }
                else if (!CoreDialogue.DialogueService.Play(step.Dialogue.Intro))
                    FlowTrace.Warn("Tutorial", $"step '{step.Id}' intro dialogue '{step.Dialogue.Intro}' unknown — continuing without it.");
            }
        }

        // ── PART 2: guided-build tolerance — auto-complete an already-built step ──
        // A founding-arc build step whose completion is a BUILD signal must not strand for
        // the full watchdog when the player ALREADY placed the demanded structure. On
        // EnterStep, if GameState.BaseLayout already satisfies the await signal, complete
        // the step immediately down the SAME completion path a real signal takes (grants,
        // outro, seen-mark, advance) — never clear-the-latch-and-wait on an event that fired
        // before the step armed. Chains cleanly: each auto-complete advances to the next
        // step, which re-checks (a carousel player who placed pet-house + lumberyard + a
        // tower up front walks through all three build steps at once).
        //
        // Signal -> BaseLayout presence:
        //   build.tower_placed          -> ANY Tower/Gate (defense) structure present
        //   build.structure_placed:<id> -> that itemId present (collector ids: lumberyard, pet-house)

        /// <summary>Auto-complete the entering step if BaseLayout already satisfies its build
        /// completion signal. Returns true when it consumed the step (caller returns without
        /// arming AwaitCompletion). No-op for any non-build step.</summary>
        private bool TryAutoCompleteAlreadyBuilt(TutorialStepDef step)
        {
            if (string.IsNullOrEmpty(_awaitSignal)) return false;
            if (!BaseLayoutSatisfiesBuildSignal(_awaitSignal)) return false;

            FlowTrace.Step("Tutorial", $"STEP-AUTOCOMPLETE :: {step.Id} — completion signal '{_awaitSignal}' " +
                "already satisfied by an existing structure in BaseLayout; completing without waiting (guided-build tolerance).");
            CompleteCurrentStep(skipped: false);   // normal path: grants + outro + seen-mark + advance
            return true;
        }

        /// <summary>Maps a build completion signal to a GameState.BaseLayout presence check.
        /// False for any non-build signal (proximity / dialogue / wave / arena steps are never
        /// auto-completed this way) and when nothing is placed yet.</summary>
        private static bool BaseLayoutSatisfiesBuildSignal(string signal)
        {
            var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            if (state == null || state.BaseLayout == null || state.BaseLayout.Count == 0) return false;

            // build.structure_placed:<id> — a specific catalog itemId must already be placed.
            if (signal.StartsWith(TutorialSignals.StructurePlacedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string wantId = signal.Substring(TutorialSignals.StructurePlacedPrefix.Length);
                if (string.IsNullOrEmpty(wantId)) return false;
                foreach (var rec in state.BaseLayout)
                    if (BuildIdMatches(rec.itemId, wantId))
                        return true;
                return false;
            }

            // build.tower_placed — ANY tower/defense structure already present.
            if (string.Equals(signal, TutorialSignals.TowerPlaced, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var rec in state.BaseLayout)
                    if (IsDefenseStructure(rec.itemId)) return true;
                return false;
            }

            return false;   // not a build signal
        }

        /// <summary>
        /// True when a placed record's itemId satisfies a wanted build id. Normally an
        /// ordinal-ignore-case equality, plus WO-748 id-drift reconciliation: the Default
        /// Town seed writes the wood storefront as <c>collector_lumbermill</c> (the seed's
        /// collector id; earlier migrations used <c>lumbermill</c>), but the founding_stores
        /// FTUE step keys on <c>lumberyard</c>. They are the same wood-resource building under
        /// different catalog ids, so accept any — the guided-build step auto-satisfies for a
        /// Default Town founding (and for a self-built lumberyard).
        /// </summary>
        private static bool BuildIdMatches(string recordId, string wantId)
        {
            if (string.Equals(recordId, wantId, StringComparison.OrdinalIgnoreCase)) return true;
            return IsWoodResourceId(recordId) && IsWoodResourceId(wantId);
        }

        /// <summary>The interchangeable wood-resource building ids (WO-748 id drift): the FTUE
        /// step's <c>lumberyard</c>, the older migration's <c>lumbermill</c>, and the current
        /// Default-Town seed's <c>collector_lumbermill</c> — all the same wood collector.</summary>
        private static bool IsWoodResourceId(string id)
        {
            return string.Equals(id, "lumberyard", StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, "lumbermill", StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, "collector_lumbermill", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Prebuilt/Default-Town detection (owner ruling 2026-07-24): true when
        /// GameState.BaseLayout carries the Default-Town seed signature — a seeded Echo Hollow
        /// (<c>pet-house</c>) or wood collector (<c>collector_lumbermill</c> / lumbermill / lumberyard,
        /// via <see cref="IsWoodResourceId"/>). This is the SAME BaseLayout presence the guided-build
        /// auto-completer reads, NOT StrategicPlacementMigrated (already flipped back true by tutorial
        /// time). A Build-Your-Own (blank template) town leaves BaseLayout without the signature, so this
        /// returns false and the build-teaching steps (founding_hollow/stores/town) run in full.</summary>
        private static bool IsTownPrebuilt()
        {
            var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            if (state == null || state.BaseLayout == null || state.BaseLayout.Count == 0) return false;
            foreach (var rec in state.BaseLayout)
            {
                if (string.IsNullOrEmpty(rec.itemId)) continue;
                if (string.Equals(rec.itemId, "pet-house", StringComparison.OrdinalIgnoreCase)) return true;
                if (IsWoodResourceId(rec.itemId)) return true;
            }
            return false;
        }

        /// <summary>True when a placed itemId resolves to a Tower/Gate (Defense) in the catalog
        /// — the "any tower/defense structure present" rule for build.tower_placed.</summary>
        private static bool IsDefenseStructure(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            var entry = DeNelle.Core.Catalog.CatalogRegistry.Get(itemId);
            if (entry == null) return false;
            return entry.type == DeNelle.Core.Catalog.CatalogType.Tower
                || entry.type == DeNelle.Core.Catalog.CatalogType.Gate;
        }

        // ── WO-702: deferred step-intro (dialogue/builder truce) ──────────────
        // A step whose intro would have opened UNDER the build palette holds it here;
        // the Update loop releases it the first frame the builder is closed. If the
        // step already completed/changed while deferred (e.g. the player finished the
        // asked build action), the stale intro is dropped with a trace, never played
        // over the NEXT step.
        private string _deferredIntroStepId;
        private string _deferredIntroId;

        private void TickDeferredIntro()
        {
            if (string.IsNullOrEmpty(_deferredIntroId)) return;
            if (DeNelle.Core.BuildModeState.IsActive)
            {
                // Builder still open — keep holding, and keep the STEP-STUCK watchdog
                // from firing on a legitimate long builder session (the intro the step
                // waits on is deliberately not on screen yet).
                _watchdogAt = Time.unscaledTime;
                return;
            }

            string id = _deferredIntroId, stepId = _deferredIntroStepId;
            _deferredIntroId = null;
            _deferredIntroStepId = null;

            if (_step == null || !string.Equals(_step.Id, stepId, StringComparison.OrdinalIgnoreCase))
            {
                FlowTrace.Step("Tutorial",
                    $"deferred-intro :: held intro '{id}' for step '{stepId}' DROPPED — the step is no longer live (completed/changed while the builder was open).");
                return;
            }

            FlowTrace.Step("Tutorial",
                $"deferred-intro :: builder closed — playing held intro '{id}' for step '{stepId}' (WO-702 truce).");
            // The step effectively BEGINS for the player when the intro finally shows —
            // restart the STEP-STUCK clock so a long, legitimate builder session doesn't
            // count against the watchdog.
            _watchdogAt = Time.unscaledTime;
            if (!CoreDialogue.DialogueService.Play(id))
                FlowTrace.Warn("Tutorial", $"step '{stepId}' deferred intro dialogue '{id}' unknown — continuing without it.");
        }

        // =====================================================================
        //  ROOT CAUSE 3 (F8 seq 632) — walk EVERY authored highlight
        // ---------------------------------------------------------------------
        //  EnterStep used to do `UiSpotlight.Show(step.Highlight[0])` and drop the rest
        //  on the floor with no trace. founding_defend authors
        //  ["hud.wave_button", "world.gate_direction"], so the GATE callout — the half
        //  that tells the player WHERE the enemies come from — never rendered, ever.
        //  UiSpotlight is a singleton with ONE target, so the fix is to ROTATE it across
        //  the whole authored list on a slow, readable cadence rather than to teach only
        //  the first item. A single-id step never cycles (no churn, no log spam).
        // =====================================================================

        /// <summary>Builds the live step's highlight walk and shows the first id. A PLACEMENT
        /// step with no authored highlight still falls back to the Build-button coach mark
        /// (the F8 seq 603 rule); a step with nothing at all clears the spotlight.</summary>
        private void ArmHighlights(TutorialStepDef step)
        {
            _highlightIds.Clear();
            _highlightIndex = 0;

            if (step != null && step.Highlight != null)
                foreach (var h in step.Highlight)
                    if (!string.IsNullOrEmpty(h) && !_highlightIds.Contains(h))
                        _highlightIds.Add(h);

            if (_highlightIds.Count == 0 && IsPlacementStep())
            {
                // F8 seq 603 (2026-08-02): a PLACEMENT step always lights the Build-button
                // coach highlight so the player is pointed at the door into the builder even
                // when the step data authors no highlight. Same registry path every authored
                // highlight takes (UiSpotlight -> TutorialHighlightRegistry; "hud.build_button"
                // is registered by HudKitController). This is the code-level guarantee
                // against data drift.
                _highlightIds.Add("hud.build_button");
                FlowTrace.Step("Tutorial", $"step '{step.Id}' placement step with no authored highlight - " +
                    "defaulting the coach highlight to 'hud.build_button' (F8 seq 603 rule).");
            }

            if (_highlightIds.Count == 0)
            {
                UiSpotlight.Hide();
                return;
            }

            _nextHighlightAt = Time.unscaledTime + HighlightCycleSeconds;
            UiSpotlight.Show(_highlightIds[0]);
            if (_highlightIds.Count > 1)
                FlowTrace.Step("Tutorial", $"step '{step.Id}' authors {_highlightIds.Count} highlights " +
                    $"[{string.Join(", ", _highlightIds)}] - the ONE spotlight rotates across all of them every " +
                    $"{HighlightCycleSeconds:0}s (F8 seq 632: only Highlight[0] used to render).");
        }

        /// <summary>Rotates the single spotlight across every authored highlight id so none is
        /// silently dropped. No-op for a 0/1-id step.</summary>
        private void TickHighlightCycle()
        {
            if (_highlightIds.Count < 2) return;
            if (Time.unscaledTime < _nextHighlightAt) return;
            _nextHighlightAt = Time.unscaledTime + HighlightCycleSeconds;
            _highlightIndex = (_highlightIndex + 1) % _highlightIds.Count;
            UiSpotlight.Show(_highlightIds[_highlightIndex]);
        }

        // =====================================================================
        //  ROOT CAUSE 4 (F8 seq 632) — the escalating coach beat
        // ---------------------------------------------------------------------
        //  The owner sat FIVE SILENT MINUTES on founding_hollow. The objective banner was
        //  up, but nothing ever re-stated the ask and nothing noticed the builder had never
        //  been opened. A stranded player must be coached, not timed out: while the step is
        //  awaiting and the builder has NOT been opened even once, re-toast the objective
        //  every CoachNudgeSeconds, escalating from the objective line to an explicit
        //  "tap BUILD" instruction, capped at CoachNudgeMaxBeats so it can never be spam.
        //  Opening the builder retires the nudge — at that point the player has found the
        //  door and the watchdog is paused anyway.
        // =====================================================================

        private void TickCoachNudge()
        {
            if (_step == null) return;

            // The builder being open at ANY point this step means the player found the door.
            if (DeNelle.Core.BuildModeState.IsActive)
            {
                if (!_builderOpenedThisStep)
                {
                    _builderOpenedThisStep = true;
                    FlowTrace.Step("Tutorial", $"coach :: step '{_step.Id}' - builder opened; " +
                        "the escalating nudge stands down (the player has found the door).");
                }
                return;
            }
            if (_builderOpenedThisStep) return;
            if (_coachBeats >= CoachNudgeMaxBeats) return;
            if (Time.unscaledTime < _nextCoachAt) return;

            _coachBeats++;
            _nextCoachAt = Time.unscaledTime + CoachNudgeSeconds;

            string objective = _step.Objective != null && !string.IsNullOrEmpty(_step.Objective.Text)
                ? _step.Objective.Text : null;
            string msg;
            if (IsPlacementStep())
                msg = _coachBeats <= 1 && objective != null
                    ? objective
                    : (objective != null ? objective + " - tap BUILD to open the builder." : "Tap BUILD to open the builder.");
            else
                msg = objective;

            if (string.IsNullOrEmpty(msg))
            {
                // No authored objective = nothing honest to say. Report it rather than
                // toasting a placeholder (CLAUDE.md sec.12: no silent failure, no fiction).
                FlowTrace.Warn("Tutorial", $"coach :: step '{_step.Id}' has been idle " +
                    $"{Time.unscaledTime - _stepEnteredAt:0}s with NO authored objective text - " +
                    "cannot re-state the ask; the step teaches nothing while stranded.");
                _coachBeats = CoachNudgeMaxBeats;   // do not re-check every frame
                return;
            }

            ElarionUiKit.ShowToast(msg, ElarionUiKit.ToastTone.Gold, 3.4f);
            FlowTrace.Warn("Tutorial", $"coach :: step '{_step.Id}' idle " +
                $"{Time.unscaledTime - _stepEnteredAt:0}s awaiting '{_awaitSignal}' with the builder never opened - " +
                $"re-stated the objective (beat {_coachBeats}/{CoachNudgeMaxBeats}).");

            // Re-assert the spotlight from the top of the walk: a glow the player scrolled
            // past is worth re-showing with the toast.
            if (_highlightIds.Count > 0)
            {
                _highlightIndex = 0;
                _nextHighlightAt = Time.unscaledTime + HighlightCycleSeconds;
                UiSpotlight.Show(_highlightIds[0]);
            }
        }

        // =====================================================================
        //  ROOT CAUSE 2 (F8 seq 632) — emitter/listener id equivalence
        // ---------------------------------------------------------------------
        //  founding_stores awaited "build.structure_placed:lumberyard" and OnSignal matched
        //  it with a strict string.Equals, while the PRE-placement auto-completer
        //  (BaseLayoutSatisfiesBuildSignal) had used the wood-id equivalence helper
        //  (BuildIdMatches) since WO-748. That asymmetry is the defect: the Town palette
        //  ships BOTH "lumberyard" (the Lumberyard storage container) and
        //  "collector_lumbermill" (the card literally labelled Lumbermill, the one that
        //  actually harvests timber). A player who placed the harvester raised
        //  "build.structure_placed:collector_lumbermill", the strict compare said no, and
        //  the step stalled for the full 300s bound with the player having DONE THE THING.
        //  The live signal path now applies the SAME equivalence the auto-completer does.
        // =====================================================================

        /// <summary>True when a RAISED signal satisfies the step's AWAITED signal. Exact
        /// (ordinal-ignore-case) for every signal, plus the wood-id equivalence for
        /// <c>build.structure_placed:&lt;id&gt;</c> — the same <see cref="BuildIdMatches"/>
        /// rule <see cref="BaseLayoutSatisfiesBuildSignal"/> has always used.</summary>
        private static bool SignalSatisfies(string awaited, string raised)
        {
            if (string.IsNullOrEmpty(awaited) || string.IsNullOrEmpty(raised)) return false;
            if (string.Equals(raised, awaited, StringComparison.OrdinalIgnoreCase)) return true;

            if (!awaited.StartsWith(TutorialSignals.StructurePlacedPrefix, StringComparison.OrdinalIgnoreCase) ||
                !raised.StartsWith(TutorialSignals.StructurePlacedPrefix, StringComparison.OrdinalIgnoreCase))
                return false;

            string wantId = awaited.Substring(TutorialSignals.StructurePlacedPrefix.Length);
            string gotId = raised.Substring(TutorialSignals.StructurePlacedPrefix.Length);
            return BuildIdMatches(gotId, wantId);
        }

        private void OnSignal(string id)
        {
            // Mandatory-step completion.
            if (_phase == Phase.AwaitCompletion && _completionArmed &&
                !string.IsNullOrEmpty(_awaitSignal) &&
                SignalSatisfies(_awaitSignal, id))
            {
                if (!string.Equals(id, _awaitSignal, StringComparison.OrdinalIgnoreCase))
                    FlowTrace.Step("Tutorial", $"step '{_step.Id}' completed by EQUIVALENT signal '{id}' " +
                        $"(awaited '{_awaitSignal}') - same building under a different catalog id " +
                        "(F8 seq 632 root cause 2: the player did the thing, the game now notices).");
                CompleteCurrentStep(skipped: false);
                return;
            }

            // Contextual completion: the hint's own dialogue ended.
            if (_activeCtx != null && _activeCtx.Dialogue != null &&
                !string.IsNullOrEmpty(_activeCtx.Dialogue.Intro) &&
                string.Equals(id, TutorialSignals.DialogueEndedPrefix + _activeCtx.Dialogue.Intro,
                              StringComparison.OrdinalIgnoreCase))
            {
                CompleteContextual("complete");
                return;
            }

            // Contextual triggers (armed any time after Start, incl. post-tutorial).
            TryTriggerContextual(id);
        }

        /// <summary>Skip affordance intent (banner Skip / dialogue footer). Only
        /// honours steps authored skippable (spec: steps 3–5 are not).</summary>
        public void SkipCurrentStep()
        {
            if (_phase != Phase.AwaitCompletion || _step == null || !_step.Skippable) return;
            _skips++;
            DeNelle.Core.Analytics.EventTracker.Track("tutorial_step_skip", new
            {
                stepId = _step.Id,
                order = _step.Order,
                seconds = Time.unscaledTime - _stepEnteredAt,
            });
            FlowTrace.Step("Tutorial", $"STEP-SKIP :: {_step.Id}.");
            CompleteCurrentStep(skipped: true);
        }

        /// <summary>
        /// Player-facing SKIP TUTORIAL (owner directive 2026-07-08): completes the ENTIRE
        /// FTUE in one call so a skipper ends in the SAME state as a completer — NOT half-
        /// granted. It (a) applies every mandatory step's cumulative essential GRANTS
        /// (grant.prepaidTower = +150 crystals; WO-702 grant.starterPet = the founding
        /// Hollow's pet — both idempotent per save), (b) marks every step
        /// seen so a resume never replays one, then (c) reuses the SINGLE completion path
        /// <see cref="FinishFlow"/> — which sets Onboarded (GameStateService.FinishOnboarding),
        /// tears down the spotlight/banner/pressure-hold, and kicks the normal town wave loop.
        /// Step-scoped combat drivers (scripted town wave, staged rep) + any live contextual
        /// hint are disarmed first so nothing fires after skip. Idempotent — a second call
        /// once <see cref="Phase.Finished"/> is a no-op.
        /// </summary>
        public void SkipAll()
        {
            if (_phase == Phase.Finished) return;

            FlowTrace.Step("Tutorial", $"SKIP-ALL :: requested at step '{(_step != null ? _step.Id : "<none>")}' " +
                $"(index {_index}/{(_steps != null ? _steps.Count : 0)}) — completing the whole FTUE.");

            // Disarm the current step's completion latch + every step-scoped combat driver so a
            // late scripted-wave clear / still-pending rep stage can never fire after the skip.
            _completionArmed = false;
            _townWaveArmed = false;
            _stagedRepPending = false;

            // Dismiss any live contextual hint (clears its spotlight) so nothing lingers.
            if (_activeCtx != null) CompleteContextual("skip");

            // Apply EVERY mandatory step's cumulative essential grants + mark each step seen, so
            // the skipper ends in the SAME state as a completer. ApplyPrepaidTowerGrant persists a
            // grant flag BEFORE crediting (idempotent per save — no double-grant); already-seen
            // steps are skipped for the seen-mark but their grant is still reconciled idempotently.
            var svc = GameStateService.Instance;
            var state = svc != null ? svc.State : null;
            if (_steps != null)
            {
                foreach (var step in _steps)
                {
                    if (step == null || string.IsNullOrEmpty(step.Id)) continue;
                    if (step.Grant != null && step.Grant.PrepaidTower)
                        ApplyPrepaidTowerGrant(step);   // idempotent: no-op if already granted this save
                    // WO-702: the founding_hollow starter-pet grant is essential too — a
                    // skipper ends with the pet like a completer. Same idempotent key.
                    if (step.Grant != null && step.Grant.StarterPet)
                        ApplyStarterPetGrant(step);
                    bool alreadySeen = state != null && state.SeenTutorials != null &&
                        state.SeenTutorials.TryGetValue(SeenPrefix + step.Id, out bool seen) && seen;
                    if (!alreadySeen) svc?.MarkTutorialSeen(SeenPrefix + step.Id);
                }
            }

            // Owner 2026-07-10 felt-bug ("cancelled tutorial but it restarts on the Build button"):
            // an explicit Skip must ALSO silence the CONTEXTUAL one-shot hints. ctx_first_spend fires
            // on economy.can_afford_upgrade — which THIS skip's own crystal grant guarantees — and
            // spotlights hud.build_button, so opening Build after a cancel resurfaced a Sylas hint that
            // reads as "the tutorial restarted". Mark every one-shot ctx seen (same persistence CtxSeen
            // checks) so a skipper gets NO further tutorial content; completers still receive the hints.
            if (_contextual != null)
                foreach (var ctx in _contextual)
                    if (ctx != null && !string.IsNullOrEmpty(ctx.Id) && ctx.OneShot)
                        svc?.MarkTutorialSeen(CtxSeenPrefix + ctx.Id);

            DeNelle.Core.Analytics.EventTracker.Track("tutorial_skipped_all", new
            {
                fromStep = _step != null ? _step.Id : null,
                index = _index,
                seconds = Time.unscaledTime - _flowStartedAt,
            });
            FlowTrace.Step("Tutorial", "SKIP-ALL :: grants applied + all steps marked seen — finishing flow " +
                "(Onboarded set, spotlight/banner/pressure-hold torn down, town loop kicked).");

            // Reuse the SINGLE completion path — do NOT invent a divergent finisher.
            _index = _steps != null ? _steps.Count : 0;
            _step = null;
            FinishFlow();
        }

        private void CompleteCurrentStep(bool skipped)
        {
            _completionArmed = false;
            // Disarm the step-scoped combat drivers so a late scripted-wave clear or a
            // still-pending rep stage can never fire into a LATER step.
            _townWaveArmed = false;
            _stagedRepPending = false;
            var step = _step;

            if (!skipped)
            {
                FlowTrace.Step("Tutorial", $"STEP-COMPLETE :: {step.Id} " +
                    $"({Time.unscaledTime - _stepEnteredAt:0.0}s).");
                DeNelle.Core.Analytics.EventTracker.Track("tutorial_step_complete", new
                {
                    stepId = step.Id,
                    order = step.Order,
                    seconds = Time.unscaledTime - _stepEnteredAt,
                });
            }

            GameStateService.Instance?.MarkTutorialSeen(SeenPrefix + step.Id);

            // WO-702: COMPLETION-side grant — the starter pet FOLLOWS the Hollow placement
            // (reward-after-action; ENTER-side grants like prepaidTower stay in EnterStep).
            // Applied on skip too: a skipped step still credits its essential grant.
            if (step.Grant != null && step.Grant.StarterPet)
                ApplyStarterPetGrant(step);

            _highlightIds.Clear();   // ROOT CAUSE 3: never rotate a dead step's walk
            _highlightIndex = 0;
            UiSpotlight.Hide();
            ObjectiveBannerUi.Hide();
            PressureHeld = false;

            // Outro (Sylas reacts) — plays over the transition; never gates the chain.
            // A SKIPPED step (player skip OR a watchdog rescue) never plays one: the outro
            // is Sylas reacting to a thing the player did, so playing it for a step that did
            // not happen narrates a fiction (F8 seq 632 root cause 4).
            if (!skipped && step.Dialogue != null && !string.IsNullOrEmpty(step.Dialogue.Outro))
            {
                if (!CoreDialogue.DialogueService.Play(step.Dialogue.Outro))
                    FlowTrace.Warn("Tutorial", $"step '{step.Id}' outro dialogue '{step.Dialogue.Outro}' unknown — skipped.");
            }

            AdvanceToNextStep();
        }

        private void FinishFlow()
        {
            _phase = Phase.Finished;
            _step = null;
            UiSpotlight.Hide();
            ObjectiveBannerUi.Hide();
            PressureHeld = false;

            DeNelle.Core.Analytics.EventTracker.Track("tutorial_completed", new
            {
                totalSeconds = Time.unscaledTime - _flowStartedAt,
                skips = _skips,
            });
            FlowTrace.Step("Tutorial", $"flow COMPLETE ({Time.unscaledTime - _flowStartedAt:0.0}s, {_skips} skips).");

            // The SINGLE V2-path finisher (spec §2.1c): mark onboarded + kick the loop —
            // the same handoff the legacy director performs (TutorialDirector.SkipToGameplay).
            GameStateService.Instance?.FinishOnboarding();
            if (_wave == null) _wave = FindAnyObjectByType<WaveManager>();
            _wave?.BeginLoop().Forget();
        }

        // =====================================================================
        //  WO-T3 — grant.prepaidTower ("this first one is on me")
        // =====================================================================

        /// <summary>
        /// Credits one watchtower's crystal cost through the SAME store BuildMenu
        /// charges (GameStateService.AddCrystals -> GameState.Resources.Crystals,
        /// BuildMenu.OnConfirmBuild BuildMenu.cs:403). Idempotent PER SAVE: the
        /// grant persists a SeenTutorials flag (the same mechanism step completion
        /// uses) BEFORE crediting, so a re-entered/replayed step never re-grants.
        /// </summary>
        private void ApplyPrepaidTowerGrant(TutorialStepDef step)
        {
            var svc = GameStateService.Instance;
            var state = svc != null ? svc.State : null;
            if (svc == null || state == null)
            {
                FlowTrace.Warn("Tutorial", $"step '{step.Id}' grant.prepaidTower — GameStateService unavailable, grant skipped (player pays from the starter wallet).");
                return;
            }

            string key = GrantSeenPrefix + step.Id;
            if (state.SeenTutorials != null &&
                state.SeenTutorials.TryGetValue(key, out bool granted) && granted)
            {
                FlowTrace.Step("Tutorial", $"step '{step.Id}' grant.prepaidTower already granted on this save — not re-granted.");
                return;
            }

            svc.MarkTutorialSeen(key);               // persist FIRST — a re-entry can never double-pay
            svc.AddCrystals(PrepaidTowerCrystals);   // clamps, persists, raises ResourcesChanged (HUD + BuildMenu re-render)
            FlowTrace.Step("Tutorial", $"step '{step.Id}' grant.prepaidTower APPLIED: +{PrepaidTowerCrystals} crystals " +
                $"(one watchtower — BuildMenu default variant cost) via GameStateService.AddCrystals; balance now {state.Resources.Crystals}.");
        }

        // =====================================================================
        //  WO-702 — grant.starterPet (the Echo Hollow's reward: the pet emerges)
        // =====================================================================

        /// <summary>Starter pet species the founding-arc Hollow placement grants --
        /// "aether-sprite", the ETHEREAL SPIRIT (owner call 2026-07-16: the founding
        /// Echo must read as an ethereal spirit, NOT the quadruped ice-wolf that
        /// T-posed). Of the three starter models this is the only ethereal/spirit one
        /// (element "aether", archetype "Heart-Ward", fairy/sprite body) and the only
        /// HUMANOID rig (AccuRig CC_Base_* skeleton) -- so a humanoid idle controller
        /// dropped at Resources/Pets/aether-sprite.controller (or the shared
        /// Resources/Pets/PetIdle.controller) binds via PetDeployer.WirePetAnimator
        /// and settles it out of the bind pose. The floating-spirit hover/drift/aura
        /// (EchoSpiritPresentation) reads ethereal even before that idle exists.
        /// PetSelect is bypassed under ff.bypasspetselect, so this default is what the
        /// founding Echo becomes.</summary>
        private const string StarterPetSpecies = "aether-sprite";

        /// <summary>
        /// WO-702: grants the starter pet on COMPLETION of the founding_hollow step —
        /// the reward follows the placement. Three moves, each self-reporting:
        ///   1. record <c>GameState.StarterPetId</c> (the SAME field the old PetSelect
        ///      confirm wrote — SyncSlotsFromState restores slot 0 from it on reload),
        ///   2. VISIBLE BIRTH (owner refinement 2026-07-13): the pet's body emerges AT
        ///      the placed Echo Hollow via PetDeployer.SummonAt (the WO-360 one-summon
        ///      path; PetHeroLeash then walks it to the hero) — Guard-wrapped so a failed
        ///      visual spawn NEVER blocks the grant,
        ///   3. roster grant through the ONE funnel PetAcquisitionService.Acquire
        ///      (GameState.Pets + OwnedPets + slot + Save; idempotent via its Owns check).
        /// Idempotent per save like prepaidTower: the tutorial_v2_grant key persists FIRST.
        /// Order matters: StarterPetId before SummonAt (its owned-gate), SummonAt before
        /// Acquire (so the slot redeploy sees the Hollow-born body and never double-spawns).
        /// </summary>
        private void ApplyStarterPetGrant(TutorialStepDef step)
        {
            var svc = GameStateService.Instance;
            var state = svc != null ? svc.State : null;
            if (svc == null || state == null)
            {
                FlowTrace.Warn("Tutorial", $"step '{step.Id}' grant.starterPet — GameStateService unavailable, grant skipped.");
                return;
            }

            string key = GrantSeenPrefix + step.Id;
            if (state.SeenTutorials != null &&
                state.SeenTutorials.TryGetValue(key, out bool granted) && granted)
            {
                FlowTrace.Step("Tutorial", $"step '{step.Id}' grant.starterPet already granted on this save — not re-granted.");
                return;
            }
            svc.MarkTutorialSeen(key);   // persist FIRST — a replay can never double-grant (prepaidTower order)

            // 1) StarterPetId — the canonical "pet-<species>" catalog id.
            if (string.IsNullOrEmpty(state.StarterPetId))
            {
                var def = DeNelle.Pets.PetCatalog.FindBySpecies(StarterPetSpecies);
                state.StarterPetId = def != null && !string.IsNullOrEmpty(def.Id)
                    ? def.Id : "pet-" + StarterPetSpecies;
            }

            // 2) SCRAPPED (owner felt-test 2026-07-17): "Echoes are portrait-card spirits, NOT
            // 3D models -- scrap giving them a model." The founding-echo VISIBLE BIRTH (the
            // PetDeployer.SummonAt aether-sprite body + the EchoSpiritPresentation floating-spirit
            // layer, PO's superseded 2026-07-16 call) is retired -- the founding Echo now awakens
            // as a PORTRAIT CARD (EchoUnlockDialogue) and lives in the pet roster (EchoRosterView),
            // no 3D echo body in the world. The DATA grant below (StarterPetId + roster Acquire) is
            // UNCHANGED, and the abstract EchoService silo/workforce is untouched.
            FlowTrace.Step("Tutorial", $"step '{step.Id}' grant.starterPet — visible echo MODEL birth SCRAPPED (echoes are portrait cards now); roster grant + StarterPetId still applied.");

            // 3) Roster grant (the single funnel — Acquire saves, covering StarterPetId too).
            var petSvc = DeNelle.Pets.PetAcquisitionService.Instance;
            if (petSvc == null)
            {
                FlowTrace.Warn("Tutorial", $"step '{step.Id}' grant.starterPet — PetAcquisitionService.Instance is null; StarterPetId recorded, roster entry deferred.");
                svc.Save();   // still persist StarterPetId + the grant key
                return;
            }
            bool acquiredNew = false;
            Guard.Try("Tutorial", "starter-pet Acquire", () =>
                acquiredNew = petSvc.Acquire(StarterPetSpecies, DeNelle.Pets.PetAcquisitionSource.Starter));
            FlowTrace.Step("Tutorial", $"step '{step.Id}' grant.starterPet APPLIED: '{StarterPetSpecies}' " +
                $"(acquiredNew={acquiredNew}, StarterPetId='{state.StarterPetId}', roster={(state.Pets != null ? state.Pets.Count : 0)} pet(s)).");
        }

        // ResolveHollowPosition() REMOVED (F8 seq 632 sweep, 2026-08-02): it existed only to
        // anchor the PetDeployer.SummonAt "visible birth" of the founding Echo at the placed
        // Hollow. That birth was SCRAPPED on 2026-07-17 (echoes are portrait cards, not 3D
        // models — see ApplyStarterPetGrant step 2), leaving this method with ZERO callers.
        // Dead code in a flow this load-bearing reads as a live path and mis-teaches the next
        // reader; it is gone. The Hollow BUILDING itself is untouched and still real.

        // =====================================================================
        //  WO-T4 — scripted town wave (spec step 4: horn blast, no Start-Wave press)
        // =====================================================================

        private void StartScriptedTownWave(TutorialStepDef step)
        {
            _townWaveArmed = false;
            _townWaveSpawnSettled = false;

            if (_wave == null) _wave = FindAnyObjectByType<WaveManager>();
            var gate = NearestGateToHero();
            if (_tutorialWave == null)
            {
                _tutorialWave = FindAnyObjectByType<TutorialWaveSpawner>();
                if (_tutorialWave == null) _tutorialWave = gameObject.AddComponent<TutorialWaveSpawner>();
            }
            _tutorialWave.SetWaveManager(_wave);

            _townWaveArmed = true;
            FlowTrace.Step("Tutorial", $"step '{step.Id}' scripted town wave: SpawnAt({(gate != null ? gate.SpawnId : "NULL-gate")}, {TownWaveCount}) " +
                "via TutorialWaveSpawner (wave loop stays closed — the !Onboarded gate holds).");
            RunScriptedTownWave(gate).Forget();
        }

        private async UniTaskVoid RunScriptedTownWave(WaveSpawnPoint gate)
        {
            // SpawnAt awaits the enemy catalog before any enemy exists; IsCleared would
            // read true (spawn-requested, none live) during that await — so the clear
            // poll (TickScriptedWave) only arms once the spawn has actually settled.
            await _tutorialWave.SpawnAt(gate, TownWaveCount);
            _townWaveSpawnSettled = true;
        }

        /// <summary>
        /// The scripted wave bypasses WaveManager's wave loop (TutorialWaveSpawner owns
        /// the spawned enemies' lifecycle; WaveManager.OnWaveCleared never fires for it),
        /// so the flow polls the spawner's own all-dead check and raises the bus signal
        /// itself. A skipped spawn (no WaveManager / no gate / empty catalog) reads
        /// IsCleared=true by the spawner's proceed-don't-wedge contract — the step then
        /// completes rather than stranding (the spawner already logged the warning).
        /// </summary>
        private void TickScriptedWave()
        {
            if (!_townWaveArmed || !_townWaveSpawnSettled) return;
            if (_tutorialWave == null || !_tutorialWave.IsCleared) return;
            _townWaveArmed = false;
            FlowTrace.Step("Tutorial", "scripted town wave CLEARED (all tutorial enemies dead) — raising 'wave.cleared'.");
            TutorialSignals.Raise(TutorialSignals.WaveCleared);
        }

        /// <summary>Nearest wave gate to the hero — the same nearest-gate rule the legacy
        /// director and TutorialWorldAnchors.ResolveNearestGate use ("the gate the tower covers").</summary>
        private WaveSpawnPoint NearestGateToHero()
        {
            if (_hero == null) _hero = FindAnyObjectByType<HeroLocomotion>();
            Vector3 from = _hero != null ? _hero.transform.position : Vector3.zero;

            WaveSpawnPoint best = null;
            float bestSqr = float.MaxValue;
            foreach (var p in FindObjectsByType<WaveSpawnPoint>(FindObjectsSortMode.None))
            {
                if (p == null) continue;
                float sqr = (p.transform.position - from).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = p; }
            }
            return best;
        }

        // =====================================================================
        //  WO-T4 — staged world encounter (spec step 5: one guaranteed rep)
        // =====================================================================

        /// <summary>
        /// Once the hero has crossed into an OuterWorld roster region, stages ONE rep at
        /// a guaranteed near anchor (10-16m ahead, navmesh-snapped, path-complete) through
        /// the SAME factory chain OverworldEncounterSpawner.SpawnRep drives — EnemyFactory
        /// .Build -> Enemy.Configure -> RepEngageWatcher.Init (def values mirror SpawnRep,
        /// OverworldEncounterSpawner.cs:313-343; migrate both onto the spec's fixed-anchor
        /// SpawnRep overload when that lands, spec §2.6). Retries at 1 Hz until an anchor
        /// resolves; never wedges — the STEP-STUCK watchdog still covers abandonment.
        /// </summary>
        private void TickStagedEncounter()
        {
            if (!_stagedRepPending || _stagedRepDone) return;
            if (Time.unscaledTime < _nextStageProbeAt) return;
            _nextStageProbeAt = Time.unscaledTime + 1f;

            if (!FeatureFlags.OverworldEncounter)
            {
                // RepEngageWatcher is inert while the flag is off — a staged rep could
                // never engage. Stand down loudly; arena.resolved:win must then come
                // from the player finding a fight by other means (or the watchdog reports).
                _stagedRepDone = true;
                FlowTrace.Warn("Tutorial", "staged encounter SKIPPED: ff.overworldencounter is OFF — no rep can engage; step relies on the watchdog.");
                return;
            }
            if (!OverworldEncounterSpawner.OuterWorldLoaded()) return;   // world not streamed in yet
            if (_hero == null) { _hero = FindAnyObjectByType<HeroLocomotion>(); if (_hero == null) return; }

            Vector3 heroPos = _hero.transform.position;
            bool inOuter = false;
            Guard.Try("Tutorial", "staged-rep zone check", () => inOuter =
                DeNelle.Core.World.RegionSpawnTable.HasRoster(DeNelle.Core.World.ZoneManager.GetZone(heroPos)));
            if (!inOuter) return;   // hero still castle-side — wait for the crossing

            // Anchor: ahead of the hero, just outside the rep's 8m aggro ring, on the
            // navmesh, in an OuterWorld roster zone, with a COMPLETE path from the hero
            // (the same candidate gates SpawnRep applies — guaranteed reachable).
            Vector3 anchor = Vector3.zero;
            bool anchorFound = false;
            var path = new UnityEngine.AI.NavMeshPath();
            Vector3 fwd = _hero.transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            fwd.Normalize();
            for (int attempt = 0; attempt < 8 && !anchorFound; attempt++)
            {
                float yaw = UnityEngine.Random.Range(-40f, 40f);
                float dist = UnityEngine.Random.Range(StagedRepMinDistance, StagedRepMaxDistance);
                Vector3 cand = heroPos + Quaternion.Euler(0f, yaw, 0f) * fwd * dist;
                if (!UnityEngine.AI.NavMesh.SamplePosition(cand, out var hit, 8f, UnityEngine.AI.NavMesh.AllAreas)) continue;
                bool candInOuter = false;
                Guard.Try("Tutorial", "staged-rep anchor zone gate", () => candInOuter =
                    DeNelle.Core.World.RegionSpawnTable.HasRoster(DeNelle.Core.World.ZoneManager.GetZone(hit.position)));
                if (!candInOuter) continue;
                if (UnityEngine.AI.NavMesh.CalculatePath(heroPos, hit.position, UnityEngine.AI.NavMesh.AllAreas, path)
                    && path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete)
                { anchor = hit.position; anchorFound = true; }
            }
            if (!anchorFound) return;   // retry next tick — the hero keeps walking, candidates change

            // Def mirrors OverworldEncounterSpawner.SpawnRep's rep exactly (owner-tuned
            // 2026-07-01 values): field-killable hook, zero contact damage, +5% chase.
            var def = new EnemyDef
            {
                Id = "orc-warrior", Name = "Orc Warleader", DisplayName = "Orc Warband", Ai = "walker",
                Hp = 98f, MoveSpeed = 6.3f, ContactDamage = 0f,
                AttackInterval = 1.5f, Height = 2.0f, AggroRadius = 8f,
                XpReward = 42, GlimmerReward = 9,
            };

            Enemy enemy = null;
            Guard.Try("Tutorial", "stage tutorial rep", () =>
            {
                enemy = EnemyFactory.Build(def, anchor, Quaternion.identity, transform);
                if (enemy == null) return;
                enemy.gameObject.name = "OrcRep_Tutorial";
                enemy.Configure("orc-rep-tutorial", def, null);    // no Heart — tethered hook, not a marcher
                enemy.SetBrainTargetPosition(anchor);              // idle at its anchor until it sees you
                int threat = 1;
                Guard.Try("Tutorial", "staged-rep zone threat", () =>
                    threat = Mathf.Max(1, DeNelle.Core.World.ZoneManager.ThreatLevel(anchor)));
                enemy.gameObject.AddComponent<RepEngageWatcher>()
                     .Init(new[] { "orc-warrior", "orc-tank", "orc-mage" }, threat);   // the SpawnRep OrcFamily
            });

            _stagedRepDone = true;
            if (enemy != null)
                FlowTrace.Step("Tutorial", $"staged tutorial rep 'OrcRep_Tutorial' at {anchor} ({Vector3.Distance(heroPos, anchor):0.0}m from hero) — engage pops the BattleArena; win raises 'arena.resolved:win'.");
            else
                FlowTrace.Fail("Tutorial", "staged tutorial rep FAILED to build (EnemyFactory returned null) — step relies on natural reps / the watchdog.");
        }

        // =====================================================================
        //  hero.reached:<anchor> — the proximity probe (spec §2.1b)
        // =====================================================================

        private void TickProximityProbe()
        {
            if (string.IsNullOrEmpty(_awaitSignal) ||
                !_awaitSignal.StartsWith(TutorialSignals.HeroReachedPrefix, StringComparison.OrdinalIgnoreCase))
                return;

            if (_hero == null) { _hero = FindAnyObjectByType<HeroLocomotion>(); if (_hero == null) return; }

            string anchorId = _awaitSignal.Substring(TutorialSignals.HeroReachedPrefix.Length);
            if (!TutorialWorldAnchors.TryResolveAnchor(anchorId, out Vector3 pos)) return;

            Vector3 d = _hero.transform.position - pos;
            d.y = 0f;
            if (d.sqrMagnitude <= ReachedRadius * ReachedRadius)
                TutorialSignals.Raise(_awaitSignal);
        }

        // =====================================================================
        //  Watchdog — STEP-STUCK oracle (bot verifiability)
        // =====================================================================

        /// <summary>True when the CURRENT awaited completion is a specific-structure
        /// placement ("build.structure_placed:&lt;id&gt;" — founding_hollow / founding_stores).
        /// Drives the longer placement watchdog bound + the Build-button default highlight
        /// (F8 seq 603, 2026-08-02).</summary>
        private bool IsPlacementStep() =>
            !string.IsNullOrEmpty(_awaitSignal) &&
            _awaitSignal.StartsWith(TutorialSignals.StructurePlacedPrefix, StringComparison.OrdinalIgnoreCase);

        /// <summary>The STEP-STUCK bound for the current step: placement-completion steps get
        /// <see cref="PlacementWatchdogSeconds"/> (300s), everything else the default 120s.</summary>
        private float WatchdogSecondsForCurrentStep() =>
            IsPlacementStep() ? PlacementWatchdogSeconds : WatchdogSeconds;

        private void TickWatchdog()
        {
            if (_step == null) return;
            if (_phase != Phase.AwaitCompletion) return;   // only a live, awaiting step can strand

            // F8 seq 603 (2026-08-02): the watchdog PAUSES while build mode is active — the
            // player is DOING the asked thing (browsing/placing in the builder), so builder
            // time never counts against the bound. Same build-mode seam the deferred-intro
            // truce already reads (BuildModeState.IsActive, fed by the build.mode_entered/
            // exited flow); a TRUE pause (deadline shifts by the paused frame), not a reset,
            // so pre-builder idle time is kept.
            if (DeNelle.Core.BuildModeState.IsActive)
            {
                _watchdogAt += Time.unscaledDeltaTime;
                FlowTrace.Once("Tutorial", "watchdog-builder-pause",
                    "STEP-STUCK watchdog PAUSED while the builder is open (build-mode time never counts — F8 seq 603 rule, 2026-08-02).");
                return;
            }

            float bound = WatchdogSecondsForCurrentStep();
            if (Time.unscaledTime - _watchdogAt < bound) return;

            // Owner ruling 2026-07-08 ("auto-advance the watchdog"): a step whose combat/
            // completion driver can never settle (e.g. town_wave's scripted spawner) must not
            // strand the banner forever. On trip we RESCUE (advance) the stuck step down the
            // SAME path a real completion takes — CompleteCurrentStep -> AdvanceToNextStep — so
            // state stays consistent (latch disarmed, drivers disarmed, seen-marked, UI hidden,
            // next step entered with a fresh watchdog window).
            //
            // F8 seq 632 ROOT CAUSE 4 (2026-08-02): the rescue is recorded as SKIPPED, never as a
            // genuine completion. It used to pass skipped:false, which made a step the player never
            // did indistinguishable from one they did: the STEP-COMPLETE trace and the
            // tutorial_step_complete analytic both fired, and — worse — the OUTRO played, so Sylas
            // narrated "There - your Hollow stands, and your first Echo has answered" for a Hollow
            // that was never built and is absent from BaseLayout. The tutorial must never tell the
            // player a thing happened that did not. skipped:true suppresses the outro + the
            // completion trace/analytic while KEEPING the two things a rescue must still do:
            //   * MarkTutorialSeen  — otherwise the stuck step replays forever on resume, and
            //   * the essential GRANT (grant.starterPet / grant.prepaidTower) — the SkipAll rule:
            //     a player who did not finish a beat must never end up half-granted and blocked
            //     out of the systems the later beats depend on.
            //
            // Fires ONCE per stuck step: CompleteCurrentStep advances _step and EnterStep resets
            // _watchdogAt/_stepEnteredAt, so this cannot re-trip on the same step.
            string stuckId = _step.Id;
            string awaited  = _awaitSignal;
            float  idle     = Time.unscaledTime - _stepEnteredAt;
            _watchdogAt = Time.unscaledTime;   // re-arm guard (belt-and-suspenders alongside the advance)

            FlowTrace.Fail("Tutorial", $"STEP-STUCK :: {stuckId} — no '{awaited}' after " +
                $"{idle:0}s in-step (bound {bound:0}s" +
                (IsPlacementStep() ? ", placement 300s rule, builder time excluded" : ", builder time excluded") +
                $"; ff.tutorialv2 on; builderOpenedThisStep={_builderOpenedThisStep}, coachBeats={_coachBeats}); " +
                "RESCUED via watchdog and recorded as SKIPPED - the step was NOT completed, its outro is " +
                "suppressed (no fiction narrated), grants still applied so the player is never half-granted.");
            DeNelle.Core.Analytics.EventTracker.Track("tutorial_step_drop", new
            {
                stepId = stuckId,
                secondsIdle = idle,
                autoAdvanced = true,
                recordedAs = "skipped",
                builderOpened = _builderOpenedThisStep,
                coachBeats = _coachBeats,
            });

            // Reuse the normal completion/advance path, recorded HONESTLY as a skip.
            _skips++;
            CompleteCurrentStep(skipped: true);
        }

        // =====================================================================
        //  Contextual one-shots (flowId "contextual") — spec CREATIVE SCOPE
        // =====================================================================

        private void TryTriggerContextual(string signalId)
        {
            if (_activeCtx != null || _contextual == null) return;

            foreach (var ctx in _contextual)
            {
                if (ctx == null || ctx.Trigger == null) continue;
                if (!string.Equals(ctx.Trigger.Type, "signal", StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.Equals(ctx.Trigger.Signal, signalId, StringComparison.OrdinalIgnoreCase)) continue;
                if (CtxSeen(ctx)) continue;

                _activeCtx = ctx;
                _ctxEnteredAt = Time.unscaledTime;
                FlowTrace.Step("Tutorial", $"CTX-ENTER :: {ctx.Id} (trigger '{signalId}').");
                DeNelle.Core.Analytics.EventTracker.Track("contextual_step_enter", new
                {
                    stepId = ctx.Id,
                    triggerSignal = signalId,
                });

                // Never pausePressure, never gate — a short line + a spotlight only.
                if (ctx.Highlight != null && ctx.Highlight.Count > 0)
                    UiSpotlight.Show(ctx.Highlight[0]);
                if (ctx.Dialogue != null && !string.IsNullOrEmpty(ctx.Dialogue.Intro))
                {
                    if (!CoreDialogue.DialogueService.Play(ctx.Dialogue.Intro))
                        FlowTrace.Warn("Tutorial", $"contextual '{ctx.Id}' dialogue '{ctx.Dialogue.Intro}' unknown.");
                }
                return;
            }
        }

        private void TickContextual()
        {
            if (_activeCtx == null) return;
            // No dialogue (or it never opened) — auto-dismiss after a short beat so a
            // contextual hint can never linger like a gate.
            if (Time.unscaledTime - _ctxEnteredAt >= ContextualAutoCloseSeconds &&
                !CoreDialogue.DialogueService.IsRunning)
                CompleteContextual("dismiss");
        }

        private void CompleteContextual(string outcome)
        {
            var ctx = _activeCtx;
            _activeCtx = null;
            if (ctx == null) return;

            FlowTrace.Step("Tutorial", $"CTX-{outcome.ToUpperInvariant()} :: {ctx.Id}.");
            DeNelle.Core.Analytics.EventTracker.Track("contextual_step_" + outcome, new
            {
                stepId = ctx.Id,
                seconds = Time.unscaledTime - _ctxEnteredAt,
            });
            if (ctx.OneShot)
                GameStateService.Instance?.MarkTutorialSeen(CtxSeenPrefix + ctx.Id);

            // Only clear the spotlight if the mandatory chain isn't using it.
            if (_phase != Phase.AwaitCompletion) UiSpotlight.Hide();
        }

        private static bool CtxSeen(TutorialStepDef ctx)
        {
            var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            return ctx.OneShot && state != null && state.SeenTutorials != null &&
                   state.SeenTutorials.TryGetValue(CtxSeenPrefix + ctx.Id, out bool seen) && seen;
        }
    }
}
