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

        /// <summary>True while a pausePressure step runs — a hold OTHER systems may
        /// consult so mid-tutorial steps that FOLLOW the town wave don't re-open the
        /// loop (spec §2.1 pausePressure). The WaveManager first-run gate (!Onboarded)
        /// already covers the whole first run; this lock is the explicit seam.</summary>
        public static bool PressureHeld { get; private set; }

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

        // Contextual runtime state.
        private TutorialStepDef _activeCtx;
        private float _ctxEnteredAt;

        private static bool s_ranThisSession;

        // =====================================================================
        //  Bootstrap (no scene edit) — mirrors the legacy director's pattern
        // =====================================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!FeatureFlags.TutorialV2) return;                     // flag-gated, default OFF
            if (!HubScenes.IsHub(SceneManager.GetActiveScene().name)) return;
            if (FindAnyObjectByType<TutorialFlow>() != null) return;
            var go = new GameObject("TutorialFlow");
            go.AddComponent<TutorialFlow>();
            go.AddComponent<TutorialSignalAdapters>();   // Village-side real-event → bus adapters
            go.AddComponent<TutorialWorldAnchors>();     // world.sylas / world.gate_direction resolvers
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

            // Grants: the guided-build prepaid tower is the WO-T3 slice (it drives the
            // redone build flow). Note it visibly so a T1 run self-reports the gap.
            if (step.Grant != null && step.Grant.PrepaidTower)
                FlowTrace.Once("Tutorial", "grant-prepaid-" + step.Id,
                    $"step '{step.Id}' declares grant.prepaidTower — applied by the guided-build slice (WO-T3).");

            // CLEAR the completion latch BEFORE the intro plays: a stale earlier raise
            // must not complete the step, but a raise DURING the intro must (e.g. a
            // dialogue.ended completion that IS the intro's own end).
            if (!string.IsNullOrEmpty(_awaitSignal)) TutorialSignals.Clear(_awaitSignal);
            _completionArmed = true;
            _phase = Phase.AwaitCompletion;

            // Presentation (Core kit affordances — read the step model only).
            if (step.Objective != null && !string.IsNullOrEmpty(step.Objective.Text))
                ObjectiveBannerUi.Show(step.Objective.Text, step.Objective.Count,
                    step.Skippable ? (Action)SkipCurrentStep : null);
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

        private void CompleteCurrentStep(bool skipped)
        {
            _completionArmed = false;
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
            if (Time.unscaledTime - _watchdogAt < WatchdogSeconds) return;
            _watchdogAt = Time.unscaledTime;   // re-arm (throttled repeat, never a wedge)

            FlowTrace.Fail("Tutorial", $"STEP-STUCK :: {_step.Id} — no '{_awaitSignal}' after " +
                $"{Time.unscaledTime - _stepEnteredAt:0}s in-step (ff.tutorialv2 on).");
            DeNelle.Core.Analytics.EventTracker.Track("tutorial_step_drop", new
            {
                stepId = _step.Id,
                secondsIdle = Time.unscaledTime - _stepEnteredAt,
            });
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
