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
        private const float ReachedRadius = 6f;         // hero.reached:<anchor> proximity (m)
        private const float ContextualAutoCloseSeconds = 10f; // hint without dialogue: auto-dismiss

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

        /// <summary>
        /// F8 2026-07-08 ("during tutorial we need to not let anything spawn. died in tutorial"):
        /// TRUE while the first-time tutorial (FTUE) is active/incomplete — ALL ambient hostile
        /// spawners (WaveManager auto-loop, OverworldEncounter ring reps + scatter reps,
        /// RegionMobSpawner) consult this and stay OFF so the player cannot die mid-tutorial.
        /// <para>
        /// Robust + INSTANCE-INDEPENDENT (reads GameState, not this component, so it holds even
        /// before/without a TutorialFlow instance): the SAME <c>!Onboarded</c> gate
        /// <see cref="WaveManager"/>'s IsFirstRun uses, qualified by <c>ff.tutorialv2</c> so it
        /// never affects the legacy path. <see cref="GameState.Onboarded"/> flips true ONLY in
        /// <see cref="FinishFlow"/> -> GameStateService.FinishOnboarding (set synchronously BEFORE
        /// the FinishFlow BeginLoop kick), so this lifts EXACTLY when the tutorial completes and
        /// the intended post-tutorial wave loop is never blocked.
        /// </para>
        /// NOTE: the tutorial's OWN scripted encounters (TutorialWaveSpawner via
        /// WaveManager.SpawnEnemyForExternalMode; the staged world_encounter rep) do NOT route
        /// through the gated ambient paths, so they still fire — only the AMBIENT sources suppress.
        /// </summary>
        public static bool HostilesSuppressedForTutorial
        {
            get
            {
                if (!FeatureFlags.TutorialV2) return false;
                var svc = GameStateService.Instance;
                if (svc == null || svc.State == null) return false;
                return !svc.State.Onboarded;
            }
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
            if (FindAnyObjectByType<TutorialFlow>() != null) return;
            var go = new GameObject("TutorialFlow");
            go.AddComponent<TutorialFlow>();
            go.AddComponent<TutorialSignalAdapters>();   // Village-side real-event → bus adapters
            go.AddComponent<TutorialWorldAnchors>();     // world.sylas / world.gate_direction resolvers
            FlowTrace.Step("Tutorial", $"Bootstrap({reason}): TutorialFlow armed in hub '{scene}'.");
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
            if (step.Highlight != null && step.Highlight.Count > 0)
                UiSpotlight.Show(step.Highlight[0]);
            else
                UiSpotlight.Hide();

            // Intro dialogue — the standard NPC template; its end raises
            // dialogue.ended:<id> through the Core adapter.
            if (step.Dialogue != null && !string.IsNullOrEmpty(step.Dialogue.Intro))
            {
                if (!CoreDialogue.DialogueService.Play(step.Dialogue.Intro))
                    FlowTrace.Warn("Tutorial", $"step '{step.Id}' intro dialogue '{step.Dialogue.Intro}' unknown — continuing without it.");
            }
        }

        private void OnSignal(string id)
        {
            // Mandatory-step completion.
            if (_phase == Phase.AwaitCompletion && _completionArmed &&
                !string.IsNullOrEmpty(_awaitSignal) &&
                string.Equals(id, _awaitSignal, StringComparison.OrdinalIgnoreCase))
            {
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
        /// granted. It (a) applies every mandatory step's cumulative essential GRANTS (today
        /// the only <c>grant.*</c> authored is <c>first_tower.grant.prepaidTower</c> = +150
        /// crystals; ApplyPrepaidTowerGrant is idempotent per save), (b) marks every step
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

            UiSpotlight.Hide();
            ObjectiveBannerUi.Hide();
            PressureHeld = false;

            // Outro (Sylas reacts) — plays over the transition; never gates the chain.
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

        private void TickWatchdog()
        {
            if (_step == null) return;
            if (_phase != Phase.AwaitCompletion) return;   // only a live, awaiting step can strand
            if (Time.unscaledTime - _watchdogAt < WatchdogSeconds) return;

            // Owner ruling 2026-07-08 ("auto-advance the watchdog"): a step whose combat/
            // completion driver can never settle (e.g. town_wave's scripted spawner) must not
            // strand the banner forever. On trip we AUTO-COMPLETE (advance) the stuck step down
            // the SAME path a real completion signal takes — CompleteCurrentStep -> AdvanceToNextStep
            // — so state stays consistent (latch disarmed, drivers disarmed, seen-marked, UI hidden,
            // next step entered with a fresh watchdog window).
            //
            // Fires ONCE per stuck step: CompleteCurrentStep advances _step and EnterStep resets
            // _watchdogAt/_stepEnteredAt, so this cannot re-trip on the same step.
            string stuckId = _step.Id;
            string awaited  = _awaitSignal;
            float  idle     = Time.unscaledTime - _stepEnteredAt;
            _watchdogAt = Time.unscaledTime;   // re-arm guard (belt-and-suspenders alongside the advance)

            FlowTrace.Fail("Tutorial", $"STEP-STUCK :: {stuckId} — no '{awaited}' after " +
                $"{idle:0}s in-step (ff.tutorialv2 on); AUTO-ADVANCED via watchdog.");
            DeNelle.Core.Analytics.EventTracker.Track("tutorial_step_drop", new
            {
                stepId = stuckId,
                secondsIdle = idle,
                autoAdvanced = true,
            });

            // Reuse the normal completion/advance path (not skipped — the step is credited).
            CompleteCurrentStep(skipped: false);
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
