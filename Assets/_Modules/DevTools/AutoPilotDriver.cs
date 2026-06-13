// =============================================================================
// AutoPilotDriver — DEV-ONLY autonomous playtest bot ("AutoPilot").
// -----------------------------------------------------------------------------
// The bot DRIVES the game itself through its real PUBLIC seams (it never fakes
// input or sets transform.position): it walks the hero to each scene gate, opens
// each vendor + HUD panel, actuates the buttons on them, forces a wave, and
// finally walks into the south exit. The EXISTING capture layer does the
// recording — BreakCaptureHarness rides Application.logMessageReceived, so every
// FlowTrace.Warn / FlowTrace.Fail this bot emits lands in break-log.jsonl. The
// bot therefore writes NO break file of its own; it only writes a per-run
// summary (autopilot-summary.json) the headless AutoPilotTickets emitter reads.
//
// STATE MACHINE: one coroutine, phase by phase. Every phase logs FlowTrace.Step
// on enter + exit and is wrapped in a per-phase REALTIME watchdog
// (WaitForSecondsRealtime). Realtime is mandatory: the F8 flag flow sets
// Time.timeScale = 0, which would freeze a scaled WaitForSeconds and hang the
// bot forever. On timeout the phase emits FlowTrace.Fail("Auto","<phase>
// TIMEOUT") and advances — it NEVER hangs. A global ~4-minute cap aborts the
// whole run if something wedges between phases.
//
// PHASES (in order):
//   BootToGameplay    — load MainCastle_Hall (skips the Title->PetSelect UI flow
//                       a headless bot can't drive); the additive OuterWorld load
//                       fires via the existing WorldSceneLoader.
//   ResolveHero       — find the HeroLocomotion (abort gracefully if absent).
//   WalkToEachGate    — drive the hero to every SceneTransitionTrigger.
//   OpenEachVendor    — open each BuildingInteractable's surface (as it would),
//                       actuate its clickables, then close.
//   OpenEachHUDPanel  — open every registered PanelId, actuate, close.
//   TriggerWave       — WaveManager.ForceBeginNextWave, poll the phase advances.
//   AttemptExitCastle — LAST: walk into the south exit, record scene change.
//
// On completion it writes the summary and Application.Quit()s — UNLESS it was
// started from the dev-panel button (quitOnDone:false), so an in-editor manual
// run leaves the editor open.
//
// RELEASE-SAFE: the whole file is #if DEVELOPMENT_BUILD || UNITY_EDITOR.
// =============================================================================

#if DEVELOPMENT_BUILD || UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;
using DeNelle.Village;

namespace DeNelle.DevTools
{
    /// <summary>
    /// DEV-ONLY autonomous playtest bot. A coroutine state machine that drives the
    /// hero + UI through their public seams while the always-on BreakCaptureHarness
    /// records any break. Spawned by <see cref="AutoPilotInstaller"/> (on
    /// <c>--autopilot</c> / the AUTOPILOT env var) or by the dev-panel "Run
    /// AutoPilot" button.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AutoPilotDriver : MonoBehaviour
    {
        // ── tunables (realtime seconds) ──────────────────────────────────────
        private const float GlobalCapSeconds   = 240f;  // ~4 min hard cap on the whole run
        private const float WalkToGateTimeout   = 25f;   // per-gate approach
        private const float VendorTimeout       = 20f;   // per-vendor open+actuate+close
        private const float HudPanelTimeout     = 15f;   // per-panel open+actuate+close
        private const float WaveTimeout         = 20f;   // wait for the wave phase to advance
        private const float ExitTimeout         = 30f;   // walk into the south exit
        private const float SettleSeconds        = 0.4f;  // brief pause after an open/close
        private const float BootTimeout          = 30f;   // load MainCastle_Hall + settle
        private const float ResolveHeroTimeout   = 15f;   // hero may spawn after scene load

        // The gameplay scene the bot must be in before it can drive. Loading it
        // single-mode triggers WorldSceneLoader's additive OuterWorld load.
        private const string GameplayScene = "MainCastle_Hall";

        // Default seed when no --seed arg is supplied — a fixed value keeps a lone
        // run fully deterministic. Fleet runs pass distinct seeds to diverge paths.
        public const int DefaultSeed = 12345;

        // Set true when started from the dev-panel button — then we DON'T quit on done.
        private bool _quitOnDone = true;

        // Fleet variation: a seeded RNG shuffles the work order within phases (gates,
        // vendors, panels, click order) so different seeds explore different paths.
        private System.Random _rng = new System.Random(DefaultSeed);
        private int _seed = DefaultSeed;
        // When set (fleet mode --run=<id>), summary is written under
        // persistentDataPath/autopilot-runs/<id>/ alongside this run's break-log.
        private string _runId;

        private HeroLocomotion _hero;
        private float _runStartRealtime;

        // Per-phase result rows for the summary file.
        private readonly List<PhaseResult> _phases = new List<PhaseResult>();

        /// <summary>
        /// Configure + start the bot. <paramref name="quitOnDone"/> false (the
        /// dev-panel manual run) leaves the editor open on completion; true (the
        /// headless --autopilot run) quits so a CI invocation terminates.
        /// </summary>
        public void Begin(bool quitOnDone)
        {
            Begin(quitOnDone, DefaultSeed, null);
        }

        /// <summary>
        /// Fleet-aware start. <paramref name="seed"/> drives the per-run path variation
        /// (work-order shuffles + click order); <paramref name="runId"/>, when non-null,
        /// namespaces the summary into persistentDataPath/autopilot-runs/&lt;id&gt;/ to
        /// match the BreakCaptureHarness output for the same run.
        /// </summary>
        public void Begin(bool quitOnDone, int seed, string runId)
        {
            _quitOnDone = quitOnDone;
            _seed = seed;
            _runId = string.IsNullOrEmpty(runId) ? null : runId;
            _rng = new System.Random(seed);
            StartCoroutine(RunAll());
        }

        private void Start()
        {
            // If something added this component directly (no Begin call), still run.
            if (_runStartRealtime == 0f && !_started)
                Begin(_quitOnDone);
        }

        private bool _started;

        private IEnumerator RunAll()
        {
            if (_started) yield break;
            _started = true;
            _runStartRealtime = Time.realtimeSinceStartup;
            FlowTrace.Step("Auto", $"AutoPilot START (quitOnDone={_quitOnDone}, seed={_seed}, run='{_runId ?? "<none>"}', scene='{ActiveScene()}').");

            // Skip Yarn: a background suppressor dismisses any dialogue that auto-starts
            // (e.g. the companion intro SylasFirstMeeting on entering MainCastle_Hall) so a
            // headless bot never stalls inside a conversation it can't read. This is the
            // concrete first case of the owner's "bot decides from a tag what to skip" idea —
            // dialogue is the prime skip; a future BotHints/tag pass generalizes per-scene
            // (ATB / Arena coverage) what each bot exercises vs skips.
            StartCoroutine(SuppressDialogue());

            // FIRST: get into the gameplay scene. Headless can't drive the
            // Title->PetSelect->MainCastle_Hall UI flow, so jump straight there.
            yield return RunPhase("BootToGameplay", BootToGameplay(), abortIfFailed: true);

            yield return RunPhase("ResolveHero", ResolveHero(), abortIfFailed: true);
            // If the hero never resolved, RunAll short-circuits to the summary.
            if (_hero != null)
            {
                yield return RunPhase("WalkToEachGate", WalkToEachGate());
                yield return RunPhase("OpenEachVendor", OpenEachVendor());
                yield return RunPhase("OpenEachHUDPanel", OpenEachHUDPanel());
                yield return RunPhase("TriggerWave", TriggerWave());
                yield return RunPhase("AttemptExitCastle", AttemptExitCastle());
            }

            FlowTrace.Step("Auto", "AutoPilot complete");
            WriteSummary();

            if (_quitOnDone)
            {
                FlowTrace.Step("Auto", "AutoPilot quitting (quitOnDone=true).");
                Application.Quit();
            }
            else
            {
                FlowTrace.Step("Auto", "AutoPilot done — editor left open (quitOnDone=false).");
            }
        }

        // Background guard: dismiss any Yarn dialogue that auto-starts, so the bot never
        // stalls inside a conversation it cannot read headless. Runs ~1/sec for the bot's
        // lifetime (the host GameObject is destroyed on quit, ending this loop).
        private IEnumerator SuppressDialogue()
        {
            while (true)
            {
                try
                {
                    if (DialogueService.IsRunning)
                    {
                        DialogueService.Stop();
                        FlowTrace.Step("Auto", "SuppressDialogue: dismissed an auto-started dialogue (skip-Yarn).");
                    }
                }
                catch (System.Exception ex) { FlowTrace.Warn("Auto", "SuppressDialogue: " + ex.Message); }
                yield return Wait(1f);
            }
        }

        // =====================================================================
        //  Phase runner — wraps each phase in a realtime watchdog + step logs
        // =====================================================================

        private bool _abortRun;

        /// <summary>
        /// Runs one phase coroutine against a REALTIME watchdog. If the phase does
        /// not finish within its declared timeout, the phase is failed
        /// (FlowTrace.Fail) and the run advances — never hangs. The global cap is
        /// also enforced here.
        /// </summary>
        private IEnumerator RunPhase(string name, IEnumerator phase, bool abortIfFailed = false)
        {
            if (_abortRun) yield break;

            float start = Time.realtimeSinceStartup;
            FlowTrace.Step("Auto", $"PHASE ENTER: {name}");

            float timeout = TimeoutFor(name);
            bool timedOut = false;
            bool threw = false;
            string error = null;

            while (true)
            {
                // global cap
                if (Time.realtimeSinceStartup - _runStartRealtime > GlobalCapSeconds)
                {
                    FlowTrace.Fail("Auto", $"GLOBAL CAP ({GlobalCapSeconds:0}s) exceeded during '{name}' — aborting run.");
                    _abortRun = true;
                    timedOut = true;
                    break;
                }
                // per-phase realtime watchdog
                if (Time.realtimeSinceStartup - start > timeout)
                {
                    FlowTrace.Fail("Auto", $"{name} TIMEOUT (>{timeout:0}s) — advancing.");
                    timedOut = true;
                    break;
                }

                bool moveNext;
                try
                {
                    moveNext = phase.MoveNext();
                }
                catch (Exception ex)
                {
                    threw = true;
                    error = ex.Message;
                    FlowTrace.Fail("Auto", $"{name} THREW: {ex.Message}");
                    break;
                }
                if (!moveNext) break;          // phase finished normally
                yield return phase.Current;    // honour the phase's own yields
            }

            float dur = Time.realtimeSinceStartup - start;
            string status = threw ? "threw" : (timedOut ? "timeout" : "ok");
            _phases.Add(new PhaseResult { phase = name, status = status, seconds = dur, detail = error ?? _lastDetail });
            _lastDetail = null;
            FlowTrace.Step("Auto", $"PHASE EXIT: {name} ({status}, {dur:0.0}s)");

            if (abortIfFailed && status != "ok")
            {
                FlowTrace.Warn("Auto", $"Critical phase '{name}' did not pass — ending run early.");
                _abortRun = true;
            }
        }

        // A phase can stash a one-line detail (e.g. counts) for the summary row.
        private string _lastDetail;

        private static float TimeoutFor(string phase)
        {
            switch (phase)
            {
                case "WalkToEachGate":    return WalkToGateTimeout * 6f; // covers multiple gates
                case "OpenEachVendor":    return VendorTimeout * 12f;
                case "OpenEachHUDPanel":  return HudPanelTimeout * 8f;
                case "TriggerWave":       return WaveTimeout;
                case "AttemptExitCastle": return ExitTimeout;
                case "BootToGameplay":    return BootTimeout;
                case "ResolveHero":       return ResolveHeroTimeout;
                default:                  return 30f;
            }
        }

        // =====================================================================
        //  PHASE: BootToGameplay (FIRST)
        //  A headless bot can't drive the Title->PetSelect->MainCastle_Hall UI
        //  flow, so if we're not already in the gameplay scene, load it directly.
        //  MainCastle_Hall is in Build Settings, so LoadScene-by-name works
        //  headless; its single load triggers the additive OuterWorld load via the
        //  existing WorldSceneLoader. We then wait (realtime) for the active scene
        //  to BE MainCastle_Hall plus a few frames so its Awake/Start has run.
        // =====================================================================
        private IEnumerator BootToGameplay()
        {
            if (ActiveScene() == GameplayScene)
            {
                FlowTrace.Step("Auto", $"BootToGameplay -> already in '{GameplayScene}', nothing to load.");
                _lastDetail = "already in gameplay scene";
                yield break;
            }

            FlowTrace.Step("Auto", $"BootToGameplay -> loading {GameplayScene} (from '{ActiveScene()}').");
            try
            {
                SceneManager.LoadScene(GameplayScene);
            }
            catch (Exception ex)
            {
                FlowTrace.Fail("Auto", $"BootToGameplay: LoadScene('{GameplayScene}') threw — {ex.Message}");
                _lastDetail = "LoadScene threw";
                yield break;
            }

            float t0 = Time.realtimeSinceStartup;
            bool arrived = false;
            while (Time.realtimeSinceStartup - t0 < BootTimeout)
            {
                if (ActiveScene() == GameplayScene) { arrived = true; break; }
                yield return null;
            }

            if (!arrived)
            {
                FlowTrace.Fail("Auto", $"BootToGameplay: '{GameplayScene}' never became active within {BootTimeout:0}s — aborting.");
                _lastDetail = "scene never active";
                yield break;
            }

            // Give the scene a couple of frames so Awake/Start (and the additive
            // OuterWorld load it kicks off) get a chance to run before ResolveHero.
            for (int i = 0; i < 3; i++) yield return null;

            FlowTrace.Step("Auto", $"BootToGameplay -> arrived in '{GameplayScene}'.");
            _lastDetail = $"loaded {GameplayScene}";
        }

        // =====================================================================
        //  PHASE: ResolveHero
        // =====================================================================
        private IEnumerator ResolveHero()
        {
            // Poll — the hero may spawn a moment after scene load (the BootToGameplay
            // additive OuterWorld load + hero spawn can lag the active-scene swap).
            float t0 = Time.realtimeSinceStartup;
            while (_hero == null && Time.realtimeSinceStartup - t0 < ResolveHeroTimeout)
            {
                _hero = UnityEngine.Object.FindAnyObjectByType<HeroLocomotion>();
                if (_hero != null) break;
                yield return null;
            }

            if (_hero == null)
            {
                FlowTrace.Fail("Auto", "ResolveHero: no HeroLocomotion in scene — cannot drive. Aborting gracefully.");
                _lastDetail = "hero not found";
                yield break;
            }
            FlowTrace.Step("Auto", $"ResolveHero: hero '{_hero.name}' at {_hero.transform.position}.");
        }

        // =====================================================================
        //  PHASE: WalkToEachGate
        //  Drive the hero (via SetAutoWalk) to within ProximityRadius of every
        //  SceneTransitionTrigger. We do NOT want to actually cross here (that's
        //  the LAST phase), so we record the scene name before/after and clear
        //  the auto-walk the instant we're in range.
        // =====================================================================
        private IEnumerator WalkToEachGate()
        {
            var gates = UnityEngine.Object.FindObjectsByType<SceneTransitionTrigger>(
                FindObjectsSortMode.None);
            if (gates == null || gates.Length == 0)
            {
                FlowTrace.Warn("Auto", "WalkToEachGate: no SceneTransitionTrigger in scene.");
                _lastDetail = "0 gates";
                yield break;
            }
            Shuffle(gates);   // seeded order so different bots probe gates differently
            FlowTrace.Step("Auto", $"WalkToEachGate: {gates.Length} gate(s) found.");

            int reached = 0;
            foreach (var gate in gates)
            {
                if (gate == null) continue;
                string sceneBefore = ActiveScene();
                float radius = Mathf.Max(1f, gate.ProximityRadius);
                FlowTrace.Step("Auto", $"WalkToEachGate: heading to '{gate.name}' (radius={radius:0.0}m, scene='{sceneBefore}').");

                _hero.SetAutoWalk(gate.transform);
                float t0 = Time.realtimeSinceStartup;
                bool inRange = false;
                while (Time.realtimeSinceStartup - t0 < WalkToGateTimeout)
                {
                    if (_hero == null) break;
                    float d = HorizontalDistance(_hero.transform.position, gate.transform.position);
                    // Stop just SHORT of the gate's fire radius so we don't cross now.
                    if (d <= radius + 0.5f) { inRange = true; break; }
                    yield return null;
                }
                _hero.ClearAutoWalk();

                string sceneAfter = ActiveScene();
                if (inRange)
                {
                    reached++;
                    FlowTrace.Step("Auto", $"WalkToEachGate: reached '{gate.name}' (scene before='{sceneBefore}', after='{sceneAfter}').");
                }
                else
                {
                    FlowTrace.Warn("Auto", $"WalkToEachGate: did NOT reach '{gate.name}' within {WalkToGateTimeout:0}s (closest path blocked / navmesh edge).");
                }

                // If walking to a gate accidentally crossed (proximity fired), bail
                // out of the rest — the scene changed under us.
                if (sceneAfter != sceneBefore)
                {
                    FlowTrace.Warn("Auto", $"WalkToEachGate: scene changed '{sceneBefore}'->'{sceneAfter}' while approaching '{gate.name}' — stopping gate sweep.");
                    break;
                }
                yield return Wait(SettleSeconds);
            }
            _lastDetail = $"{reached}/{gates.Length} gates reached";
        }

        // =====================================================================
        //  PHASE: OpenEachVendor
        //  Enumerate BuildingInteractable. Its Interact() is private, but routes
        //  through the SAME public seams it would: DialogueService.PlayStructure
        //  for the economy vendors and PanelRouter.Open for the legacy panels.
        //  We replicate that routing here, verify the surface opened, actuate the
        //  clickables, then close.
        // =====================================================================
        private IEnumerator OpenEachVendor()
        {
            var buildings = UnityEngine.Object.FindObjectsByType<BuildingInteractable>(
                FindObjectsSortMode.None);
            if (buildings == null || buildings.Length == 0)
            {
                FlowTrace.Warn("Auto", "OpenEachVendor: no BuildingInteractable in scene.");
                _lastDetail = "0 vendors";
                yield break;
            }
            Shuffle(buildings);   // seeded vendor order
            FlowTrace.Step("Auto", $"OpenEachVendor: {buildings.Length} building(s) found.");

            int opened = 0;
            foreach (var bi in buildings)
            {
                if (bi == null) continue;
                string name = bi.name;

                // Walk near it first (so a real run mirrors the player approach).
                _hero.SetAutoWalk(bi.transform);
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 8f)
                {
                    if (_hero == null) break;
                    if (HorizontalDistance(_hero.transform.position, bi.transform.position) <= 6.5f) break;
                    yield return null;
                }
                _hero.ClearAutoWalk();

                // Open the surface the way the building would. The vendor routing is
                // building-specific (see BuildingInteractable.Interact): the economy
                // buildings open the shared Yarn structure dialogue; the rest open a
                // PanelRouter panel. We try the dialogue first, then panels.
                bool surfaceOpened = false;
                FlowTrace.Step("Auto", $"OpenEachVendor: interacting with '{name}'.");

                // The bot can't read the building's private hookId, so it tries the
                // structure dialogue for the well-known economy ids and the panel ids
                // for the rest — exactly the two public routes Interact() takes.
                // We probe by attempting a structure dialogue with the building name
                // as a label; PlayStructure returns false if the id isn't routable.
                // Practically, the HUD-panel route below is the reliable verifiable
                // path, so we exercise it for every registered panel in the next phase
                // and here we focus on the dialogue surface.
                if (DialogueService.PlayStructure("market", name)
                    || DialogueService.PlayStructure("farm", name))
                {
                    yield return Wait(SettleSeconds);
                    surfaceOpened = DialogueService.IsRunning;
                    if (surfaceOpened)
                    {
                        FlowTrace.Step("Auto", $"OpenEachVendor: '{name}' opened a structure dialogue.");
                        ClickableActuator.ActuateAll(null, _rng);
                        yield return Wait(SettleSeconds);
                        DialogueService.Stop();
                        opened++;
                    }
                }

                if (!surfaceOpened)
                {
                    // Fall through to a panel route if one is registered for a
                    // common building panel (Building Upgrade is the broadest).
                    if (PanelRouter.IsRegistered(PanelId.BuildingUpgrade)
                        && PanelRouter.Open(PanelId.BuildingUpgrade))
                    {
                        yield return Wait(SettleSeconds);
                        if (PanelManager.AnyOpen)
                        {
                            FlowTrace.Step("Auto", $"OpenEachVendor: '{name}' opened BuildingUpgrade panel.");
                            ClickableActuator.ActuateAll(null, _rng);
                            yield return Wait(SettleSeconds);
                            PanelManager.CloseOpen();
                            opened++;
                            surfaceOpened = true;
                        }
                    }
                }

                if (!surfaceOpened)
                    FlowTrace.Warn("Auto", $"OpenEachVendor: '{name}' opened no verifiable surface (no routable dialogue / panel).");

                yield return Wait(SettleSeconds);
            }
            _lastDetail = $"{opened}/{buildings.Length} vendor surfaces opened";
        }

        // =====================================================================
        //  PHASE: OpenEachHUDPanel
        //  For every PanelId registered with PanelRouter: open, assert AnyOpen,
        //  actuate the clickables on the open surface, then CloseOpen.
        // =====================================================================
        private IEnumerator OpenEachHUDPanel()
        {
            int opened = 0, registered = 0;
            // Seeded panel order so different bots open panels in different sequences.
            var panelIds = new List<PanelId>();
            foreach (PanelId pid in Enum.GetValues(typeof(PanelId))) panelIds.Add(pid);
            Shuffle(panelIds);
            foreach (PanelId id in panelIds)
            {
                if (!PanelRouter.IsRegistered(id)) continue;
                registered++;

                FlowTrace.Step("Auto", $"OpenEachHUDPanel: opening '{id}'.");
                bool ok = PanelRouter.Open(id);
                yield return Wait(SettleSeconds);

                if (!ok)
                {
                    FlowTrace.Warn("Auto", $"OpenEachHUDPanel: PanelRouter.Open({id}) returned false.");
                    continue;
                }
                if (!PanelManager.AnyOpen)
                {
                    FlowTrace.Warn("Auto", $"OpenEachHUDPanel: '{id}' opened but PanelManager.AnyOpen is false (panel may not register a handle).");
                }
                else
                {
                    FlowTrace.Step("Auto", $"OpenEachHUDPanel: '{id}' open (PanelManager='{PanelManager.OpenPanelName}').");
                    opened++;
                }

                ClickableActuator.ActuateAll(null, _rng);
                yield return Wait(SettleSeconds);

                PanelManager.CloseOpen();
                yield return Wait(SettleSeconds);
            }
            FlowTrace.Step("Auto", $"OpenEachHUDPanel: {opened}/{registered} registered panels verified open.");
            _lastDetail = $"{opened}/{registered} panels";
        }

        // =====================================================================
        //  PHASE: TriggerWave
        //  WaveManager.ForceBeginNextWave(), then poll until the phase advances
        //  off its current value (or timeout -> Fail).
        // =====================================================================
        private IEnumerator TriggerWave()
        {
            var wm = UnityEngine.Object.FindAnyObjectByType<WaveManager>();
            if (wm == null)
            {
                FlowTrace.Warn("Auto", "TriggerWave: no WaveManager in scene.");
                _lastDetail = "no WaveManager";
                yield break;
            }

            WavePhase before = wm.Phase;
            FlowTrace.Step("Auto", $"TriggerWave: forcing next wave (phase before='{before}').");
            wm.ForceBeginNextWave();

            float t0 = Time.realtimeSinceStartup;
            bool advanced = false;
            while (Time.realtimeSinceStartup - t0 < WaveTimeout)
            {
                if (wm.Phase != before) { advanced = true; break; }
                yield return null;
            }

            if (advanced)
                FlowTrace.Step("Auto", $"TriggerWave: phase advanced '{before}'->'{wm.Phase}'.");
            else
                FlowTrace.Fail("Auto", $"TriggerWave: phase did NOT advance from '{before}' within {WaveTimeout:0}s.");
            _lastDetail = $"phase {before}->{wm.Phase}";
        }

        // =====================================================================
        //  PHASE: AttemptExitCastle (LAST)
        //  Walk into the south exit trigger and record whether the active scene
        //  changed. The south gate is the SceneTransitionTrigger whose target is
        //  OuterWorld and that sits at the most -Z position; we just walk into
        //  every gate that hasn't fired and let the proximity logic cross.
        // =====================================================================
        private IEnumerator AttemptExitCastle()
        {
            var gates = UnityEngine.Object.FindObjectsByType<SceneTransitionTrigger>(
                FindObjectsSortMode.None);
            if (gates == null || gates.Length == 0)
            {
                FlowTrace.Warn("Auto", "AttemptExitCastle: no SceneTransitionTrigger to exit through.");
                _lastDetail = "no exit gate";
                yield break;
            }

            // Pick the south-most gate (smallest world Z) as the "exit".
            SceneTransitionTrigger exit = null;
            float minZ = float.MaxValue;
            foreach (var g in gates)
            {
                if (g == null) continue;
                if (g.transform.position.z < minZ) { minZ = g.transform.position.z; exit = g; }
            }
            if (exit == null) { _lastDetail = "no exit gate"; yield break; }

            string sceneBefore = ActiveScene();
            FlowTrace.Step("Auto", $"AttemptExitCastle: walking into '{exit.name}' (target='{exit.targetSceneName}', scene='{sceneBefore}').");

            // Walk a bit PAST the gate so the proximity radius certainly fires.
            _hero.SetAutoWalk(exit.transform);
            float t0 = Time.realtimeSinceStartup;
            bool changed = false;
            while (Time.realtimeSinceStartup - t0 < ExitTimeout)
            {
                if (ActiveScene() != sceneBefore) { changed = true; break; }
                yield return null;
            }
            if (_hero != null) _hero.ClearAutoWalk();

            string sceneAfter = ActiveScene();
            if (changed || sceneAfter != sceneBefore)
                FlowTrace.Step("Auto", $"AttemptExitCastle: active scene changed '{sceneBefore}'->'{sceneAfter}'.");
            else
                FlowTrace.Warn("Auto", $"AttemptExitCastle: active scene UNCHANGED ('{sceneBefore}') after {ExitTimeout:0}s — exit never fired (additive load or blocked navmesh).");
            _lastDetail = $"scene {sceneBefore}->{sceneAfter}";
        }

        // =====================================================================
        //  Summary file
        // =====================================================================
        private void WriteSummary()
        {
            try
            {
                var summary = new RunSummary
                {
                    utc = DateTime.UtcNow.ToString("o"),
                    totalSeconds = Time.realtimeSinceStartup - _runStartRealtime,
                    aborted = _abortRun,
                    phases = _phases.ToArray(),
                };
                // Fleet mode: write into persistentDataPath/autopilot-runs/<id>/ so it
                // sits beside this run's break-log.jsonl and the aggregator can count
                // it as one run's coverage. Default (no --run) -> root, unchanged.
                string baseDir = Application.persistentDataPath;
                string outDir = baseDir;
                if (!string.IsNullOrEmpty(_runId))
                {
                    outDir = Path.Combine(Path.Combine(baseDir, "autopilot-runs"), _runId);
                    Directory.CreateDirectory(outDir);
                }
                string path = Path.Combine(outDir, "autopilot-summary.json");
                File.WriteAllText(path, JsonUtility.ToJson(summary, true));
                FlowTrace.Step("Auto", $"AutoPilot summary written -> {path}");
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("Auto", "AutoPilot: failed to write summary — " + ex.Message);
            }
        }

        // =====================================================================
        //  Helpers
        // =====================================================================
        private static string ActiveScene()
        {
            try { return SceneManager.GetActiveScene().name; } catch { return "?"; }
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }

        // Seeded Fisher-Yates in-place shuffle of the per-phase work order so distinct
        // seeds explore distinct paths. Uses _rng (seeded in Begin). Null-safe.
        private void Shuffle<T>(IList<T> list)
        {
            if (list == null || _rng == null) return;
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
            }
        }

        // Realtime wait — never freezes under timeScale=0 (F8 flag flow).
        private static WaitForSecondsRealtime Wait(float seconds)
            => new WaitForSecondsRealtime(seconds);

        [Serializable]
        private struct PhaseResult
        {
            public string phase;
            public string status;   // ok / timeout / threw
            public float seconds;
            public string detail;
        }

        [Serializable]
        private struct RunSummary
        {
            public string utc;
            public float totalSeconds;
            public bool aborted;
            public PhaseResult[] phases;
        }
    }
}

#endif // DEVELOPMENT_BUILD || UNITY_EDITOR
