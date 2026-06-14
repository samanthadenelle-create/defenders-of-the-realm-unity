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
using DeNelle.Village.Hero;
using DeNelle.Village.Crafting;

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
        private const float ContractTimeout     = 12f;   // per-vendor-context contract assertion
        private const float EconomyDeductTimeout = 15f;  // open shop + read-before + buy + read-after assert
        private const float EquipTimeout         = 15f;   // add gear + equip + assert loadout changed
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
                // Runs even if building discovery found 0 vendors: it opens shops DIRECTLY
                // by context (the castle storefronts route through CastleNpcInteractable +
                // DialogueService, NOT BuildingInteractable), so the contract assertion is
                // not gated on building discovery.
                yield return RunPhase("AssertVendorContracts", AssertVendorContracts());
                // ASSERTION-DEPTH EXPANSION: the bot used to mostly verify "didn't crash" +
                // the vendor STOCK contract. These two phases assert real CORRECTNESS of the
                // economy + equip wiring — that a buy actually deducts the cost AND grows the
                // inventory, and that equipping actually changes the hero's loadout/stat.
                yield return RunPhase("AssertEconomyDeduct", AssertEconomyDeduct());
                yield return RunPhase("AssertEquip", AssertEquip());
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
                case "AssertVendorContracts": return ContractTimeout * 8f; // covers the known-context set
                case "AssertEconomyDeduct": return EconomyDeductTimeout;
                case "AssertEquip":       return EquipTimeout;
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
            // ROBUST DISCOVERY: FindObjectsByType spans ALL loaded scenes, but the castle's
            // additive OuterWorld (and any runtime-injected buildings) can load a beat after
            // MainCastle_Hall becomes active. So RETRY for up to ~5s before concluding 0 —
            // a building can spawn shortly after scene load. (FindObjectsSortMode.None.)
            BuildingInteractable[] buildings = null;
            float t0Discover = Time.realtimeSinceStartup;
            int attempts = 0;
            while (Time.realtimeSinceStartup - t0Discover < 5f)
            {
                attempts++;
                buildings = UnityEngine.Object.FindObjectsByType<BuildingInteractable>(
                    FindObjectsSortMode.None);
                if (buildings != null && buildings.Length > 0) break;
                yield return Wait(0.5f);
            }

            if (buildings == null || buildings.Length == 0)
            {
                // NOTE: in MainCastle_Hall this is EXPECTED — the castle storefronts route
                // through CastleNpcInteractable (spawned by CastleVendorNpcInjector) +
                // DialogueService, NOT BuildingInteractable. The AssertVendorContracts phase
                // below covers vendors directly by context, so 0 here is not a dead end.
                FlowTrace.Step("Auto", $"OpenEachVendor: 0 BuildingInteractable after {attempts} attempt(s) over ~5s. " +
                    "MainCastle_Hall vendors use CastleNpcInteractable, not BuildingInteractable — covered by AssertVendorContracts.");
                _lastDetail = "0 BuildingInteractable (castle uses CastleNpcInteractable)";
                yield break;
            }
            Shuffle(buildings);   // seeded vendor order
            FlowTrace.Step("Auto", $"OpenEachVendor: {buildings.Length} building(s) found (after {attempts} discovery attempt(s)).");
            foreach (var b in buildings)
                if (b != null) FlowTrace.Step("Auto", $"OpenEachVendor: discovered BuildingInteractable '{b.name}'.");

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
        //  PHASE: AssertVendorContracts
        //  The point of the chain: bots judge whether a vendor sells the RIGHT
        //  category. For each KNOWN vendor context the game uses, open the shop for
        //  that context and assert its ACTUAL built stock (ShopPanel.CurrentStock)
        //  stays within VendorStockContract.AllowedFor(context). A violation is a
        //  LogError (FlowTrace.Fail) -> break-log -> ticket. Runs even if building
        //  discovery found 0 vendors, because it opens shops DIRECTLY by context via
        //  ShopPanel.Open — the same seam DialogueCommandBridge.OpenShop uses.
        //
        //  Known contexts: the spec set {forge, market, jeweler, armorer} PLUS the
        //  contexts the castle actually wires (CastleVendorNpcInjector.VendorFor):
        //  lumbermill, farm, pet-house, arcane-tower. The jeweler is modeled as Armor
        //  in the contract today (the crystal/jewelry adornment arc). The non-shop
        //  ids (lumbermill/farm/pet-house/arcane-tower) still resolve a contract
        //  (Potion / general default) and a stock, so asserting them is valid and
        //  cheap — and catches a future regression if one of them starts stocking gear.
        // =====================================================================
        private IEnumerator AssertVendorContracts()
        {
            // The known vendor-context set: the spec minimum plus the contexts the
            // castle storefronts actually open (verified against CastleVendorNpcInjector).
            var contexts = new List<string>
            {
                "forge", "market", "jeweler", "armorer",
                "lumbermill", "farm", "pet-house", "arcane-tower",
            };
            Shuffle(contexts); // seeded order so different bots probe contexts differently

            // One reusable ShopPanel host: find an existing one, else create our own.
            // Re-Open(ctx) closes the prior surface, so vendors never stack. We destroy
            // the host we created at the end.
            ShopPanel panel = UnityEngine.Object.FindObjectOfType<ShopPanel>();
            bool createdHost = false;
            GameObject host = null;
            if (panel == null)
            {
                host = new GameObject("AutoPilotShopPanelHost");
                panel = host.AddComponent<ShopPanel>();
                createdHost = true;
            }

            int checkedCount = 0, violations = 0, emptyWarns = 0;
            foreach (var ctx in contexts)
            {
                try
                {
                    FlowTrace.Step("Auto", $"AssertVendorContracts: opening shop for context '{ctx}'.");
                    panel.Open(ctx);
                }
                catch (Exception ex)
                {
                    FlowTrace.Fail("Auto", $"AssertVendorContracts: Open('{ctx}') threw — {ex.Message}");
                    continue;
                }

                // Wait a frame so ShowBuy (called inside Open) has built the rows + populated
                // CurrentStock. (Open->ShowBuy is synchronous, but a frame is cheap insurance.)
                yield return null;

                try
                {
                    string vc = panel.VendorContext;
                    var allowed = VendorStockContract.AllowedFor(vc);
                    var stock = panel.CurrentStock;
                    int n = stock != null ? stock.Count : 0;

                    bool compliant = true;
                    if (stock != null)
                    {
                        foreach (var item in stock)
                        {
                            if ((allowed & item.kind) == 0)
                            {
                                compliant = false;
                                violations++;
                                FlowTrace.Fail("Auto", $"VENDOR CONTRACT VIOLATION: '{vc}' allows {allowed} but stocked {item.id} ({item.kind})");
                            }
                        }
                    }

                    if (n == 0)
                    {
                        // A vendor should not be empty when its contract allows a category the
                        // catalog actually has stock for. Weapon/Armor come from GearCatalog;
                        // Potion is the panel's built-in potion list (always available), so an
                        // allowed-Potion vendor that built nothing is the meaningful empty case.
                        bool catalogHasWeapons = SafeAny(GearCatalog.AllWeapons());
                        bool catalogHasArmors  = SafeAny(GearCatalog.AllArmors());
                        bool shouldHaveStock =
                            ((allowed & GearKind.Weapon) != 0 && catalogHasWeapons) ||
                            ((allowed & GearKind.Armor)  != 0 && catalogHasArmors)  ||
                            ((allowed & GearKind.Potion) != 0);
                        if (shouldHaveStock)
                        {
                            emptyWarns++;
                            FlowTrace.Warn("Auto", $"AssertVendorContracts: vendor '{vc}' (allows {allowed}) built EMPTY stock though the catalog has matching wares.");
                        }
                        else
                        {
                            FlowTrace.Step("Auto", $"AssertVendorContracts: '{vc}' empty (allows {allowed}; catalog has no matching gear) — not flagged.");
                        }
                    }
                    else if (compliant)
                    {
                        FlowTrace.Step("Auto", $"AssertVendorContracts: '{vc}' COMPLIES — {n} item(s), all within {allowed}.");
                    }

                    checkedCount++;
                }
                catch (Exception ex)
                {
                    FlowTrace.Fail("Auto", $"AssertVendorContracts: assertion for '{ctx}' threw — {ex.Message}");
                }

                yield return Wait(SettleSeconds);
            }

            // Close/dispose: re-Open's Close ran per ctx; destroy our host so nothing lingers.
            // (ShopPanel.Close is private + the panel doesn't register with PanelManager, so
            // destroying the host is the bot's clean teardown.) Belt-and-braces CloseOpen too.
            try { PanelManager.CloseOpen(); } catch { }
            if (createdHost && host != null) UnityEngine.Object.Destroy(host);

            _lastDetail = $"{checkedCount} contexts checked, {violations} violation(s), {emptyWarns} empty-warn(s)";
            FlowTrace.Step("Auto", $"AssertVendorContracts: {_lastDetail}.");
        }

        // Null-safe "any element" over an IEnumerable (GearCatalog returns lists that are
        // never null per its contract, but guard anyway).
        private static bool SafeAny<T>(System.Collections.Generic.IEnumerable<T> seq)
        {
            if (seq == null) return false;
            foreach (var _ in seq) return true;
            return false;
        }

        // =====================================================================
        //  PHASE: AssertEconomyDeduct  (ASSERTION-DEPTH EXPANSION)
        //  Correctness, not just "didn't crash": a buy must DEDUCT exactly the item
        //  cost from EconomyService AND grow VillageInventory by one. Open a vendor's
        //  shop (the same ShopPanel.Open(ctx) seam AssertVendorContracts uses), read the
        //  resource snapshot BEFORE, pick the first AFFORDABLE item in CurrentStock,
        //  perform the buy, read AFTER, and assert the delta is EXACTLY the cost and the
        //  inventory gained the item. A mismatch is FlowTrace.Fail -> break-log -> ticket.
        //
        //  FALLBACK (documented): ShopPanel's real buy handlers (TryBuyWeapon/Armor/Potion)
        //  are PRIVATE — the bot cannot cleanly trigger the panel's own buy. So per the
        //  spec it asserts the LOWER-LEVEL invariant the handlers are built on: for an
        //  affordable item, EconomyService.TrySpend(cost) deducts exactly that cost and
        //  VillageInventory.Add(id,1) grows the count — exactly the two lines each Try*
        //  handler runs on success. Potions are SKIPPED for cost-resolution (their cost is
        //  private to ShopPanel); we resolve weapon/armor cost via GearCatalog.GetBuyCost,
        //  which is the authoritative cost the handlers spend.
        // =====================================================================
        private IEnumerator AssertEconomyDeduct()
        {
            var eco = EconomyService.Instance;
            var inv = VillageInventory.Instance;
            if (eco == null || inv == null)
            {
                FlowTrace.Warn("Auto", $"AssertEconomyDeduct: missing service (eco={(eco != null)}, inv={(inv != null)}) — skipping.");
                _lastDetail = "no economy/inventory service — skipped";
                yield break;
            }

            // One reusable ShopPanel host (mirror of AssertVendorContracts' teardown).
            ShopPanel panel = UnityEngine.Object.FindObjectOfType<ShopPanel>();
            bool createdHost = false;
            GameObject host = null;
            if (panel == null)
            {
                host = new GameObject("AutoPilotEconomyPanelHost");
                panel = host.AddComponent<ShopPanel>();
                createdHost = true;
            }

            // Open a general vendor (empty context -> "all" contract, the widest stock) so
            // we maximize the chance of finding an affordable, cost-resolvable gear item.
            try { panel.Open(""); }
            catch (Exception ex)
            {
                FlowTrace.Fail("Auto", $"AssertEconomyDeduct: ShopPanel.Open('') threw — {ex.Message}");
                if (createdHost && host != null) UnityEngine.Object.Destroy(host);
                _lastDetail = "Open threw";
                yield break;
            }
            yield return null; // let ShowBuy populate CurrentStock

            // Pick the first AFFORDABLE weapon/armor in the actual built stock whose cost is
            // resolvable + non-zero (a free item can't prove a deduction). Potions skipped.
            string buyId = null; GearKind buyKind = GearKind.None; ResourceCost cost = default; bool found = false;
            var stock = panel.CurrentStock;
            if (stock != null)
            {
                foreach (var item in stock)
                {
                    if (item.kind == GearKind.Weapon)
                    {
                        var w = GearCatalog.FindWeapon(item.id);
                        if (w == null) continue;
                        cost = GearCatalog.GetBuyCost(w);
                    }
                    else if (item.kind == GearKind.Armor)
                    {
                        var a = GearCatalog.FindArmor(item.id);
                        if (a == null) continue;
                        cost = GearCatalog.GetBuyCost(a);
                    }
                    else continue; // potion cost is private to ShopPanel — not assertable here

                    if (cost.IsZero) continue;        // need a real cost to prove a deduction
                    if (!eco.CanAfford(cost)) continue;
                    buyId = item.id; buyKind = item.kind; found = true;
                    break;
                }
            }

            // Teardown the panel surface now — we assert against the services directly.
            try { PanelManager.CloseOpen(); } catch { }
            if (createdHost && host != null) UnityEngine.Object.Destroy(host);

            if (!found)
            {
                // Not a ticket-worthy failure: the vendor simply had no affordable, cost-bearing
                // gear (empty catalog / can't afford anything). Grant a known item + cost so the
                // INVARIANT is still exercised — this is the documented lower-level fallback.
                FlowTrace.Step("Auto", "AssertEconomyDeduct: no affordable cost-bearing gear in stock; " +
                    "exercising the TrySpend/Add invariant with a synthetic cost the wallet can afford.");
                cost = new ResourceCost(wood: 1);
                if (!eco.CanAfford(cost))
                {
                    FlowTrace.Warn("Auto", "AssertEconomyDeduct: wallet cannot afford even 1 wood — cannot assert deduction. Skipping.");
                    _lastDetail = "no affordable item + empty wallet — skipped";
                    yield break;
                }
                buyId = "autopilot-deduct-probe"; buyKind = GearKind.None;
            }

            // Snapshot BEFORE.
            int wBefore = eco.Wood, iBefore = eco.Iron, fBefore = eco.Food, cBefore = eco.Crystals;
            int invBefore = inv.Get(buyId);
            FlowTrace.Step("Auto", $"AssertEconomyDeduct: buying '{buyId}' ({buyKind}) cost W{cost.Wood} F{cost.Food} I{cost.Iron} C{cost.Crystals} " +
                $"(wallet before W{wBefore} F{fBefore} I{iBefore} C{cBefore}, inv {invBefore}).");

            // Perform the buy via the lower-level invariant the private handlers run.
            bool spent = eco.TrySpend(cost);
            if (spent && inv != null) inv.Add(buyId, 1);

            int wAfter = eco.Wood, iAfter = eco.Iron, fAfter = eco.Food, cAfter = eco.Crystals;
            int invAfter = inv.Get(buyId);

            // ASSERT: spend succeeded, resources dropped by EXACTLY the cost, inventory +1.
            bool ok = spent;
            if (!spent)
                FlowTrace.Fail("Auto", $"AssertEconomyDeduct: TrySpend returned FALSE for an affordable cost (CanAfford was true) — economy did not deduct for '{buyId}'.");

            if (spent)
            {
                bool deductExact = (wBefore - wAfter) == cost.Wood
                                && (iBefore - iAfter) == cost.Iron
                                && (fBefore - fAfter) == cost.Food
                                && (cBefore - cAfter) == cost.Crystals;
                if (!deductExact)
                {
                    ok = false;
                    FlowTrace.Fail("Auto", $"AssertEconomyDeduct: economy did not deduct by the exact cost — " +
                        $"deltas W{wBefore - wAfter}/F{fBefore - fAfter}/I{iBefore - iAfter}/C{cBefore - cAfter} " +
                        $"vs cost W{cost.Wood}/F{cost.Food}/I{cost.Iron}/C{cost.Crystals}.");
                }
                if (invAfter != invBefore + 1)
                {
                    ok = false;
                    FlowTrace.Fail("Auto", $"AssertEconomyDeduct: inventory not updated — '{buyId}' was {invBefore}, now {invAfter} (expected {invBefore + 1}).");
                }
            }

            if (ok)
                FlowTrace.Step("Auto", $"AssertEconomyDeduct: PASS — '{buyId}' deducted exactly + inventory {invBefore}->{invAfter}.");
            _lastDetail = ok
                ? $"deduct OK for '{buyId}' (inv {invBefore}->{invAfter})"
                : $"deduct/inventory MISMATCH for '{buyId}'";
            yield return Wait(SettleSeconds);
        }

        // =====================================================================
        //  PHASE: AssertEquip  (ASSERTION-DEPTH EXPANSION)
        //  Correctness: equipping a weapon must CHANGE the hero's loadout/stat. Resolve
        //  (or attach) the hero's GearLoadout, pick a catalog weapon, add it to the
        //  inventory, equip it via GearLoadout.EquipWeaponById (the same public seam the
        //  ShopPanel EQUIP tab uses), and assert EquippedWeapon is non-null afterward AND
        //  the loadout actually reflects what we equipped (or WeaponMult moved). A no-op
        //  equip (loadout unchanged) is FlowTrace.Fail -> break-log -> ticket.
        // =====================================================================
        private IEnumerator AssertEquip()
        {
            // Resolve the hero's GearLoadout (lazily attach if absent — the ShopPanel EQUIP
            // path does the same: AddComponent<GearLoadout>() on the hero when none exists).
            GameObject heroGo = _hero != null ? _hero.gameObject : null;
            if (heroGo == null)
            {
                var tagged = GameObject.FindWithTag("Player");
                if (tagged != null) heroGo = tagged;
            }
            if (heroGo == null)
            {
                FlowTrace.Warn("Auto", "AssertEquip: no hero GameObject to equip on — skipping.");
                _lastDetail = "no hero — skipped";
                yield break;
            }

            GearLoadout loadout = heroGo.GetComponent<GearLoadout>();
            if (loadout == null) loadout = heroGo.AddComponent<GearLoadout>();
            yield return null; // let Awake/OnEnable run (Refresh may auto-equip a best item)

            // Pick a catalog weapon to force-equip. Prefer one DIFFERENT from whatever is
            // auto-equipped so the change is observable even if a best-weapon was auto-set.
            WeaponDef target = null;
            var beforeWeapon = loadout.EquippedWeapon;
            string beforeId = beforeWeapon != null ? beforeWeapon.id : null;
            float multBefore = loadout.WeaponMult;
            foreach (var w in GearCatalog.AllWeapons())
            {
                if (w == null) continue;
                if (target == null) target = w;                 // first valid as a fallback
                if (beforeId == null || w.id != beforeId) { target = w; break; } // prefer a different one
            }

            if (target == null)
            {
                FlowTrace.Warn("Auto", "AssertEquip: gear catalog has no weapons to equip — skipping (cannot assert equip wiring).");
                _lastDetail = "no catalog weapons — skipped";
                yield break;
            }

            // Own it first (mirrors the player flow: buy/own -> equip), then equip via the
            // public seam. EquipWeaponById no-ops if the id isn't in the catalog — we chose
            // target FROM the catalog, so it must resolve.
            if (VillageInventory.Instance != null) VillageInventory.Instance.Add(target.id, 1);

            FlowTrace.Step("Auto", $"AssertEquip: equipping '{target.id}' (before weapon='{beforeId ?? "<null>"}', mult={multBefore:0.00}).");
            loadout.EquipWeaponById(target.id);
            yield return null; // let ApplyStats run

            var afterWeapon = loadout.EquippedWeapon;
            float multAfter = loadout.WeaponMult;

            // ASSERT: a weapon is now equipped AND the loadout reflects the equip — either it
            // is the exact piece we forced, or (defensive) the damage multiplier moved. A
            // loadout that did NOT change at all means the equip path is a no-op.
            bool nowEquipped = afterWeapon != null;
            bool isTarget    = afterWeapon != null && afterWeapon.id == target.id;
            bool multMoved   = !Mathf.Approximately(multAfter, multBefore);
            bool changed     = isTarget || multMoved;

            if (!nowEquipped)
            {
                FlowTrace.Fail("Auto", $"AssertEquip: EquippedWeapon is NULL after EquipWeaponById('{target.id}') — equip path did not set the loadout.");
                _lastDetail = "equip left EquippedWeapon null";
            }
            else if (!changed)
            {
                FlowTrace.Fail("Auto", $"AssertEquip: loadout did NOT change — equipped '{(afterWeapon != null ? afterWeapon.id : "<null>")}' " +
                    $"but expected '{target.id}' and WeaponMult stayed {multAfter:0.00}. Equip is a no-op.");
                _lastDetail = "equip did not change loadout";
            }
            else
            {
                FlowTrace.Step("Auto", $"AssertEquip: PASS — EquippedWeapon='{afterWeapon.id}' (target '{target.id}', " +
                    $"mult {multBefore:0.00}->{multAfter:0.00}).");
                _lastDetail = $"equipped '{afterWeapon.id}' (mult {multBefore:0.00}->{multAfter:0.00})";
            }
            yield return Wait(SettleSeconds);
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
        //
        //  CALIBRATION (false-positive fix): the hub (MainCastle_Hall) has NO wave
        //  system, so when there's no WaveManager there is nothing to advance — the
        //  old code warned but the phase still burned its full timeout and the
        //  emitter logged a timeout/hang ticket (false positive, 8/8). We now treat
        //  "no WaveManager in scene" as N/A and return SUCCESS (skipped) immediately.
        //  It is ONLY a real Fail when a WaveManager EXISTS and its Phase refuses to
        //  advance after ForceBeginNextWave within the timeout.
        // =====================================================================
        private IEnumerator TriggerWave()
        {
            var wm = UnityEngine.Object.FindAnyObjectByType<WaveManager>();
            if (wm == null)
            {
                FlowTrace.Step("Auto", "TriggerWave: no WaveManager in scene — N/A (hub has no wave loop), skipping.");
                _lastDetail = "no WaveManager — N/A (skipped)";
                yield break;
            }

            WavePhase before = wm.Phase;
            FlowTrace.Step("Auto", $"TriggerWave: forcing next wave (phase before='{before}').");
            wm.ForceSpawnNextWaveNow();   // immediate spawn (skip countdown) — fixes the TriggerWave timeout from Idle

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
        //  Walk into the south exit trigger and detect the REAL crossing.
        //
        //  CALIBRATION (false-positive fix): the castle->OuterWorld seam loads
        //  OuterWorld ADDITIVELY (SceneTransitionTrigger.loadAdditive=true), so on a
        //  SUCCESSFUL crossing the ACTIVE scene STAYS MainCastle_Hall — the old
        //  "did the active scene change?" test therefore ALWAYS timed out (false
        //  positive). The reliable signal is that the seam WARPS the hero to
        //  trigger.targetPosition on Cross(). We poll the hero's distance to that
        //  warp landing instead. (OuterWorld may also already be loaded additively
        //  at startup, so "OuterWorld isLoaded" is NOT a usable crossing signal.)
        //
        //  SUCCESS = hero actually warped to within 8m of the seam's targetPosition.
        //  FAILURE (real, ticket-worthy) = within the timeout the hero never reached
        //  the gate's ProximityRadius (couldn't path), OR reached it but never warped
        //  (seam didn't fire). The Fail names WHICH, with the closest distance reached.
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

            // Pick the south-most gate (smallest world Z) as the "exit" — same
            // selection the gate sweep uses; it's the wired castle->OuterWorld seam.
            SceneTransitionTrigger exit = null;
            float minZ = float.MaxValue;
            foreach (var g in gates)
            {
                if (g == null) continue;
                if (g.transform.position.z < minZ) { minZ = g.transform.position.z; exit = g; }
            }
            // No exit trigger OR no hero is NOT a ticket-worthy failure — there is
            // simply nothing to attempt (e.g. a scene with no wired seam, or the hero
            // was destroyed/unloaded). End the phase cleanly (ok/skipped), never throw
            // or burn the timeout. Only a hero that REACHED the gate but never warped
            // is a real Fail (handled below).
            if (exit == null || _hero == null)
            {
                FlowTrace.Warn("Auto", "AttemptExitCastle: no exit trigger / hero — skipping");
                _lastDetail = exit == null ? "no exit gate" : "no hero — skipped";
                yield break;
            }

            // SceneTransitionTrigger (DeNelle.Village) exposes these as PUBLIC fields —
            // read them by typed access (no reflection => no FieldInfo NRE). Cache the
            // gate's transform position too: a one-way crossing can unload/destroy the
            // SOURCE seam after the warp, which would Unity-fake-null `exit` and NRE on
            // `exit.transform.position` in the poll loop below.
            string targetScene = exit.targetSceneName;
            Vector3 warpTarget = exit.targetPosition;
            float radius = Mathf.Max(1f, exit.ProximityRadius);
            Vector3 gatePos = exit.transform.position;
            string gateName = exit.name;
            Vector3 heroStart = _hero.transform.position;

            FlowTrace.Step("Auto", $"AttemptExitCastle: walking into '{exit.name}' " +
                $"(target='{targetScene}'@{warpTarget}, radius={radius:0.0}m, heroStart={heroStart}).");

            // Drive toward the seam. The crossing fires from PROXIMITY (the trigger's
            // own Update distance-check), so we just need to get within radius; the
            // seam then WarpTo's the hero to targetPosition.
            _hero.SetAutoWalk(exit.transform);

            float t0 = Time.realtimeSinceStartup;
            bool warped = false;
            bool reachedProximity = false;
            float closestToGate = float.MaxValue;   // closest the hero ever got to the gate
            while (Time.realtimeSinceStartup - t0 < ExitTimeout)
            {
                // Hero (or its GameObject) could be destroyed/unloaded mid-walk — bail
                // cleanly rather than NRE on `_hero.transform`.
                if (_hero == null) break;

                Vector3 pos = _hero.transform.position;

                // Did the seam warp us to the landing? (full 3D distance — the warp
                // sets Y too). This is the authoritative crossing signal.
                if (Vector3.Distance(pos, warpTarget) < 8f) { warped = true; break; }

                // Track how close we get to the gate (horizontal — navmesh is planar).
                // Use the CACHED gate position: the source seam may be gone after a
                // crossing, so we must never dereference `exit.transform` here.
                float dGate = HorizontalDistance(pos, gatePos);
                if (dGate < closestToGate) closestToGate = dGate;
                if (dGate <= radius + 0.5f) reachedProximity = true;

                yield return null;
            }
            if (_hero != null) _hero.ClearAutoWalk();

            if (warped)
            {
                float finalDist = _hero != null ? Vector3.Distance(_hero.transform.position, warpTarget) : 0f;
                FlowTrace.Step("Auto", $"AttemptExitCastle: CROSSED — hero warped to '{targetScene}' target {warpTarget} (now {finalDist:0.0}m from landing).");
                _lastDetail = $"crossed to {targetScene} (warped to target)";
            }
            else if (!reachedProximity)
            {
                // Real, ticket-worthy: the hero could not path to the seam at all.
                FlowTrace.Fail("Auto", $"AttemptExitCastle: hero could not path to the gate '{gateName}' — " +
                    $"closest {closestToGate:0.0}m of radius {radius:0.0}m within {ExitTimeout:0}s (navmesh edge / blocked).");
                _lastDetail = $"could not reach gate (closest {closestToGate:0.0}m / radius {radius:0.0}m)";
            }
            else
            {
                // Real, ticket-worthy: reached proximity but the seam never warped us.
                FlowTrace.Fail("Auto", $"AttemptExitCastle: seam did NOT fire — hero reached closest {closestToGate:0.0}m of gate '{gateName}' " +
                    $"(radius {radius:0.0}m) but no warp to target {warpTarget} within {ExitTimeout:0}s.");
                _lastDetail = $"seam did not fire (reached {closestToGate:0.0}m / radius {radius:0.0}m, no warp)";
            }
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
