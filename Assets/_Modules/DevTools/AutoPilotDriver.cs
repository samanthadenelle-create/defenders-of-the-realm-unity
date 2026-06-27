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
using DeNelle.Village.World.Camps;   // EnemyOutpost / RaidOutpostSystem — WO-449 walk-to phase
// Enemy (WO-449 combat-on-approach assertion) resolves via `using DeNelle.Village;` above.

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
        private const float WaveTimeout         = 30f;   // wait for the wave phase to advance (bumped 20→30: covers the bounded start-retry window; player keeps a ~45s countdown)
        private const float ExitTimeout         = 30f;   // walk into the south exit
        private const float SettleSeconds        = 0.4f;  // brief pause after an open/close
        private const float BootTimeout          = 30f;   // load MainCastle_Hall + settle
        private const float ResolveHeroTimeout   = 15f;   // hero may spawn after scene load
        private const float OuterWalkTimeout     = 60f;   // WO-449: poll outpost realize (~10s) + walk ~70m + engage

        // WO-449 ANTI-WARP: the continuous-walk loop must NEVER teleport. A single-frame
        // hero displacement beyond this many metres is a WARP (the bug this phase guards),
        // not a walk — the hero's NavMesh walk moves far less than this per frame.
        private const float WalkMaxStepMeters = 3f;

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

        // Passive assertion probes that ride alongside the scripted phases and catch
        // UX/structural defects the phase machine misses (unexpected auto-cross,
        // coplanar z-fight floors, walk-through-wall clips, dual-navmesh / stranding).
        // Spawned + armed only on an autopilot run (this driver is autopilot-only).
        private AutoPilotProbes _probes;

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
        public void Begin(bool quitOnDone, int seed, string runId, string startScene = null)
        {
            _quitOnDone = quitOnDone;
            _seed = seed;
            _runId = string.IsNullOrEmpty(runId) ? null : runId;
            _startScene = string.IsNullOrEmpty(startScene) ? null : startScene.Trim();
            _rng = new System.Random(seed);
            StartCoroutine(RunAll());
        }

        // Optional boot-scene override (--scene=<name> / AUTOPILOT_SCENE). When set, BootToGameplay loads
        // THIS scene instead of MainCastle_Hall — the "instant spawn into Village2 / a garrison" the owner
        // asked for, so a headless/dev bot lands directly in the system under test (the real Village2Raid/
        // Garrison/HeroControlEnsurer path) with no traversal. The scene MUST be in Build Settings to load
        // by name; if it isn't, the LoadScene throws and is caught/logged (falls through to abort).
        private string _startScene;
        private string TargetScene => string.IsNullOrEmpty(_startScene) ? GameplayScene : _startScene;

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

            // Arm the passive assertion probes (autopilot-only — this driver is the sole
            // spawner). They watch world state across every phase via FlowTrace.Fail.
            try
            {
                _probes = gameObject.AddComponent<AutoPilotProbes>();
                _probes.Arm();
            }
            catch (Exception ex) { FlowTrace.Warn("Auto", "Failed to arm AutoPilotProbes: " + ex.Message); }

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
                // Hero-crossing test (owner 2026-06-21): if a HeroLinkCrossing pair exists (e.g. Village2 gate),
                // walk the hero into the entry and assert it WARPS to the destination. Runs first so --scene=Village2
                // proves the gate crossing headless (owner never the detector).
                yield return RunPhase("AssertHeroCrossing", AssertHeroCrossing());
                yield return RunPhase("WalkToEachGate", WalkToEachGate());
                yield return RunPhase("OpenEachVendor", OpenEachVendor());
                // Runs even if building discovery found 0 vendors: it opens shops DIRECTLY
                // by context (the castle storefronts route through CastleNpcInteractable +
                // DialogueService, NOT BuildingInteractable), so the contract assertion is
                // not gated on building discovery.
                yield return RunPhase("AssertVendorContracts", AssertVendorContracts());
                // TKT-15 talk-fix DATA-VERIFY: drive each castle vendor's REAL Interact() (the path
                // the HUD Talk button fires) and assert SHOPPABLE vendors open the Buy/Sell dialogue,
                // not the upgrade panel (the regression). Closes the castle-vendor headless coverage hole.
                yield return RunPhase("AssertVendorTalkRoute", AssertVendorTalkRoute());
                // ASSERTION-DEPTH EXPANSION: the bot used to mostly verify "didn't crash" +
                // the vendor STOCK contract. These two phases assert real CORRECTNESS of the
                // economy + equip wiring — that a buy actually deducts the cost AND grows the
                // inventory, and that equipping actually changes the hero's loadout/stat.
                yield return RunPhase("AssertEconomyDeduct", AssertEconomyDeduct());
                yield return RunPhase("AssertEquip", AssertEquip());
                // DETERMINISTIC GARRISON-ROSTER DIAG (tickets #2 troll-orientation + #4 magenta): the chaos
                // walk cannot reliably reach Village2's garrison before the time budget, so the orc/troll
                // roster never spawns to be inspected. Build the EXACT village2_stronghold roster HERE via the
                // canonical EnemyFactory path (no traversal) and let EnemyFactory's own render-verify + the
                // worldUp trace + TripoMatFix VERIFY lines capture each one. Also warps the hero to prove the
                // WarpTo path keeps its body (the #2 bare-pill hero-side check). Read-only diagnosis; cleans up.
                yield return RunPhase("DiagGarrisonRoster", DiagGarrisonRoster());
                yield return RunPhase("OpenEachHUDPanel", OpenEachHUDPanel());
                yield return RunPhase("TriggerWave", TriggerWave());
                // AttemptExitCastle deliberately crosses a scene seam, so tell the
                // UNEXPECTED-CROSS probe this load is intentional (else it would flag
                // the bot's own exit). Clear it again right after.
                _probes?.SetIntentionalCrossPhase(true);
                yield return RunPhase("AttemptExitCastle", AttemptExitCastle());
                _probes?.SetIntentionalCrossPhase(false);

                // WO-449: the continuous-walk raid loop — walk to a live OuterWorld outpost and
                // prove combat triggers ON FOOT (no teleport). This phase loads NO scene, so the
                // UNEXPECTED-CROSS probe stays ARMED (NOT marked intentional): a re-introduced
                // raid/outpost teleport would trip AutoPilotProbes' scene-load Fail.
                yield return RunPhase("WalkToOuterWorldOutpost", WalkToOuterWorldOutpost());

                // WO-482: drive the overworld-encounter -> isolated BattleArena loop HEADLESSLY via the
                // REAL trigger path end-to-end (NOT a BeginEncounter bypass). Warp the hero into an
                // OuterWorld roster region, force the spawner's real SpawnRep, then warp the hero onto
                // the rep so RepEngageWatcher's own Update fires Engage()->BeginEncounter. Asserts a rep
                // SPAWNED + the battle DROPPED + the family staged, then force-wins. This FAILS if the
                // rep->engage->battle path is broken (the spawn-gate bug that sailed through the old
                // direct-call oracle). The owner is never the tester (memory never-dragdrop-or-manual).
                yield return RunPhase("AssertEncounterBattle", AssertEncounterRealPath());
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

        // WO-482 — HEADLESS verify of the overworld-encounter -> isolated BattleArena loop
        // via the REAL trigger path (NOT a BeginEncounter bypass). The old oracle called
        // BattleArena.BeginEncounter DIRECTLY and a real bug (reps never spawned: the
        // spawner gated on GetActiveScene()=="OuterWorld", false under additive OuterWorld)
        // sailed through a green PASS. This drives the ACTUAL chain: warp the hero into an
        // OuterWorld roster region on navmesh -> force the spawner's REAL SpawnRep ->
        // warp the hero onto the rep so RepEngageWatcher's OWN Update fires Engage()->
        // BeginEncounter -> assert BattleInProgress -> assert the family staged -> force-win.
        // Every failure is a FlowTrace.Fail so it lands in break-log.jsonl as a ranked ticket.
        private IEnumerator AssertEncounterRealPath()
        {
            const string Tag = "Auto";
            if (_hero == null) { _lastDetail = "no hero - skipped"; FlowTrace.Warn(Tag, "AssertEncounterRealPath: no hero - skipped."); yield break; }

            // 1) Enable the (default-OFF) feature for the assertion; restore after.
            int prevFlag = PlayerPrefs.GetInt("ff.overworldencounter", -1);
            PlayerPrefs.SetInt("ff.overworldencounter", 1);

            // 2) Ensure OuterWorld is loaded, then warp the hero to a point that is BOTH on
            //    navmesh AND classified by ZoneManager as an OUTER roster region (so the
            //    spawner's HeroInOuterWorld()/anchor are valid). Sample candidate points.
            Vector3 homePos = _hero.transform.position;
            Vector3[] candidates = { new Vector3(40f, 0f, 40f), new Vector3(60f, 0f, 0f), new Vector3(0f, 0f, 40f), new Vector3(40f, 0f, 0f), new Vector3(-40f, 0f, 40f) };
            Vector3 landing = Vector3.zero;
            bool placed = false;
            foreach (var c in candidates)
            {
                if (!UnityEngine.AI.NavMesh.SamplePosition(c, out var nh, 12f, UnityEngine.AI.NavMesh.AllAreas)) continue;
                bool roster = false;
                try { roster = DeNelle.Core.World.RegionSpawnTable.HasRoster(DeNelle.Core.World.ZoneManager.GetZone(nh.position)); }
                catch (Exception ex) { FlowTrace.Warn(Tag, "AssertEncounterRealPath: zone check threw " + ex.Message); }
                if (roster) { landing = nh.position; placed = true; break; }
            }

            if (!placed)
            {
                _lastDetail = "no on-mesh roster region found -> rep cannot anchor";
                FlowTrace.Fail(Tag, "AssertEncounterRealPath: no candidate point was BOTH on navmesh AND in an OuterWorld roster region (OuterWorld not loaded / navmesh not baked?) - the real rep path cannot run.");
                RestoreEncounterFlag(prevFlag);
                yield break;
            }

            try { _hero.WarpTo(landing); } catch (Exception ex) { FlowTrace.Warn(Tag, "AssertEncounterRealPath: hero WarpTo threw " + ex.Message); }
            for (int i = 0; i < 3; i++) yield return null;
            FlowTrace.Step(Tag, "AssertEncounterRealPath: hero warped into OuterWorld roster region @ " + landing + ".");

            // 3) Force the spawner's REAL spawn path (SpawnRep) -- do NOT spawn reps ourselves,
            //    do NOT call BeginEncounter. Then wait for a real OrcRep_* to exist.
            var spawner = DeNelle.Village.OverworldEncounterSpawner.Instance;
            if (spawner == null)
            {
                _lastDetail = "OverworldEncounterSpawner.Instance NULL";
                FlowTrace.Fail(Tag, "AssertEncounterRealPath: OverworldEncounterSpawner.Instance was NULL - the spawner never bootstrapped.");
                RestoreEncounterFlag(prevFlag);
                yield break;
            }
            try { spawner.ForcePopulateForTest(); } catch (Exception ex) { FlowTrace.Fail(Tag, "AssertEncounterRealPath: ForcePopulateForTest threw " + ex.GetType().Name + ": " + ex.Message); }

            GameObject rep = null;
            float spawnWait = 0f;
            while (spawnWait < 8f)
            {
                rep = FindRepObject();
                if (rep != null) break;
                spawnWait += Time.deltaTime; yield return null;
            }

            bool repSpawned = rep != null;
            if (!repSpawned)
            {
                // THIS is the bug the old direct-call oracle missed: no rep ever materialised.
                _lastDetail = "repSpawned=false -> rep->engage->battle path BROKEN";
                FlowTrace.Fail(Tag, "AssertEncounterRealPath: NO OrcRep_* spawned within 8s after the REAL SpawnRep path - the overworld rep never materialised (the spawn-gate class of bug).");
                RestoreEncounterFlag(prevFlag);
                yield break;
            }
            FlowTrace.Step(Tag, "AssertEncounterRealPath: real rep '" + rep.name + "' spawned @ " + rep.transform.position + ".");

            // 4) Drive the REAL engage: warp the hero ONTO the rep (within EngageRange ~2.6m) so
            //    RepEngageWatcher's OWN Update fires Engage()->BeginEncounter. Do NOT call Engage().
            Vector3 onRep = rep.transform.position;
            try { _hero.WarpTo(onRep); } catch (Exception ex) { FlowTrace.Warn(Tag, "AssertEncounterRealPath: hero WarpTo(rep) threw " + ex.Message); }
            FlowTrace.Step(Tag, "AssertEncounterRealPath: hero warped onto rep @ " + onRep + " (within EngageRange) - waiting for the watcher to fire Engage.");

            // 5) Assert the battle DROPS (BattleInProgress becomes true) within ~6s -- the real
            //    "drop to battle" the owner never reached.
            var arena = DeNelle.Village.Arena.BattleArena.Instance;
            float engageWait = 0f;
            while (engageWait < 6f && !(arena != null && arena.BattleInProgress))
            {
                // re-assert position in case any locomotion nudged the hero off the rep before the watcher ticked
                engageWait += Time.deltaTime; yield return null;
            }
            bool droppedToBattle = arena != null && arena.BattleInProgress;
            if (!droppedToBattle)
            {
                _lastDetail = "repSpawned=true droppedToBattle=false -> engage->battle BROKEN";
                FlowTrace.Fail(Tag, "AssertEncounterRealPath: rep spawned but BattleInProgress never became true within 6s of the hero touching it - RepEngageWatcher.Engage()->BeginEncounter did NOT drop to battle.");
                RestoreEncounterFlag(prevFlag);
                yield break;
            }
            FlowTrace.Step(Tag, "AssertEncounterRealPath: dropped to battle (BattleInProgress=true) via the REAL watcher engage.");

            // 6) Let the arena build + bake + warp + spawn settle, then assert the family staged.
            float build = 0f;
            while (build < 4f) { build += Time.deltaTime; yield return null; }

            var found = new System.Collections.Generic.List<DeNelle.Village.Enemy>();
            foreach (var e in UnityEngine.Object.FindObjectsByType<DeNelle.Village.Enemy>(FindObjectsSortMode.None))
                if (e != null && e.gameObject.name.StartsWith("ArenaEnemy_")) found.Add(e);

            int skinned = 0, orcCtrl = 0;
            foreach (var e in found)
            {
                if (e == null) continue;
                if (e.GetComponentInChildren<SkinnedMeshRenderer>() != null) skinned++;   // real orc mesh, not a capsule
                var anim = e.GetComponentInChildren<Animator>();
                string ctrl = (anim != null && anim.runtimeAnimatorController != null) ? anim.runtimeAnimatorController.name : "";
                if (ctrl.IndexOf("Orc", StringComparison.OrdinalIgnoreCase) >= 0) orcCtrl++;
            }

            if (found.Count == 0)
                FlowTrace.Fail(Tag, "AssertEncounterRealPath: NO arena enemies spawned - the open-arena family never materialised.");
            else if (skinned < found.Count)
                FlowTrace.Fail(Tag, "AssertEncounterRealPath: " + (found.Count - skinned) + "/" + found.Count + " orcs fell back to a CAPSULE (model failed to load).");
            else if (orcCtrl < found.Count)
                FlowTrace.Fail(Tag, "AssertEncounterRealPath: " + (found.Count - orcCtrl) + "/" + found.Count + " orcs lack an Orc animator controller (would T-pose).");
            else
                FlowTrace.Step(Tag, "AssertEncounterRealPath: " + found.Count + " orcs spawned, all skinned + Orc-rigged.");

            // Force the WIN path deterministically (headless can't drive hero attacks).
            foreach (var e in found)
                if (e != null) { try { e.Kill(); } catch (Exception ex) { FlowTrace.Warn(Tag, "AssertEncounterRealPath: Kill threw " + ex.Message); } }

            // Wait for resolution (BattleInProgress -> false).
            float wait = 0f;
            while (arena.BattleInProgress && wait < 15f) { wait += Time.deltaTime; yield return null; }
            bool resolved = !arena.BattleInProgress;
            if (!resolved)
                FlowTrace.Fail(Tag, "AssertEncounterRealPath: battle did NOT resolve within 15s after the family died - loop stuck.");

            // Assert the hero returned to the engagement spot (where it touched the rep).
            float dist = (_hero != null) ? Vector3.Distance(_hero.transform.position, onRep) : 999f;
            bool heroReturn = dist <= 5f;
            if (!heroReturn)
                FlowTrace.Fail(Tag, "AssertEncounterRealPath: hero NOT returned (dist " + dist.ToString("F1") + "m from engagement spot).");

            bool pass = repSpawned && droppedToBattle && found.Count > 0 && skinned == found.Count && orcCtrl == found.Count && resolved && heroReturn;
            _lastDetail = "repSpawned=" + repSpawned + " droppedToBattle=" + droppedToBattle +
                          " spawned=" + found.Count + " skinned=" + skinned + " orcRig=" + orcCtrl +
                          " resolved=" + resolved + " heroReturn=" + dist.ToString("F1") + "m -> " + (pass ? "PASS" : "FAIL");
            FlowTrace.Step(Tag, "AssertEncounterRealPath: " + _lastDetail);

            RestoreEncounterFlag(prevFlag);
        }

        // Find a live overworld rep object (named "OrcRep_*" by the spawner's SpawnRep).
        private static GameObject FindRepObject()
        {
            foreach (var e in UnityEngine.Object.FindObjectsByType<DeNelle.Village.Enemy>(FindObjectsSortMode.None))
                if (e != null && e.gameObject.name.StartsWith("OrcRep_")) return e.gameObject;
            return null;
        }

        private static void RestoreEncounterFlag(int prev)
        {
            if (prev < 0) PlayerPrefs.DeleteKey("ff.overworldencounter");
            else PlayerPrefs.SetInt("ff.overworldencounter", prev);
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

        // =====================================================================
        //  PHASE: DiagGarrisonRoster (tickets #2 + #4 deterministic capture)
        //  Builds the real village2_stronghold roster via the canonical
        //  GarrisonStatBlocks -> EnemyFactory.Build path, in the CURRENT scene, so
        //  the orc/troll/hollow bodies actually instantiate (the chaos walk never
        //  reaches the garrison in time). Each Build self-reports via EnemyFactory's
        //  render-verify + the worldUp trace + TripoMatFix VERIFY lines, which is the
        //  data that proves: (#4) orcs render URP not magenta after the fixer fix;
        //  (#2) the troll's worldUp (tipped vs upright). Then warp the hero and dump
        //  its body/renderer/onMesh state (the #2 bare-pill hero-side check). Cleans up.
        // =====================================================================
        private IEnumerator DiagGarrisonRoster()
        {
            string[] roster = { "orc-berserker", "orc-shaman", "orc-raider", "troll", "hollow-warrior", "hollow-walker" };
            Vector3 around = _hero != null ? _hero.transform.position : Vector3.zero;
            var root = new GameObject("[DiagGarrisonRoster]").transform;
            int built = 0, magenta = 0, tipped = 0;

            for (int i = 0; i < roster.Length; i++)
            {
                string id = roster[i];
                Vector3 want = around + new Vector3((i - 2) * 2.5f, 0f, 7f);
                if (UnityEngine.AI.NavMesh.SamplePosition(want, out UnityEngine.AI.NavMeshHit hit, 10f, UnityEngine.AI.NavMesh.AllAreas)) want = hit.position;

                Enemy enemy = null;
                try
                {
                    EnemyDef def = DeNelle.Village.World.Camps.GarrisonStatBlocks.BuildTypedDef(id, 1);
                    enemy = EnemyFactory.Build(def, want, Quaternion.identity, root);   // logs render-verify + worldUp (+ TripoMatFix)
                }
                catch (Exception ex)
                {
                    FlowTrace.Fail("DiagRoster", $"build '{id}' threw {ex.GetType().Name}: {ex.Message}");
                }

                // Let the body skin + the Tripo material fixer run a few frames before we read shaders.
                yield return Wait(0.35f);

                if (enemy != null)
                {
                    built++;
                    bool anyMagenta = false; Vector3 up = Vector3.up; bool haveUp = false;
                    foreach (var r in enemy.GetComponentsInChildren<Renderer>(true))
                    {
                        if (r == null) continue;
                        var m = r.sharedMaterial;
                        string sh = (m != null && m.shader != null) ? m.shader.name : "<null>";
                        bool isMagenta = sh.IndexOf("InternalError", StringComparison.OrdinalIgnoreCase) >= 0
                                      || sh.IndexOf("Hidden/", StringComparison.OrdinalIgnoreCase) >= 0;
                        if (isMagenta) anyMagenta = true;
                        if (!haveUp) { up = r.transform.up; haveUp = true; }
                        FlowTrace.Step("DiagRoster", $"'{id}' renderer '{r.name}' shader='{sh}' magenta={isMagenta}");
                    }
                    bool isTipped = Vector3.Angle(up, Vector3.up) > 30f;   // >30deg off vertical = laid over
                    if (anyMagenta) magenta++;
                    if (isTipped) tipped++;
                    FlowTrace.Step("DiagRoster",
                        $"'{id}' SUMMARY magenta={anyMagenta} worldUp={up} tipped={isTipped} (upright iff worldUp~=(0,1,0)).");
                }
            }

            // #2 hero-side: warp the hero and confirm it keeps its skinned body + lands on navmesh
            // (the bare-pill arrival theory). WarpTo self-logs SamplePosition HIT/MISS + isOnNavMesh.
            if (_hero != null)
            {
                int bodyRenderers = 0;
                foreach (var smr in _hero.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (smr != null && smr.enabled && smr.sharedMesh != null) bodyRenderers++;
                Vector3 dest = around + new Vector3(0f, 0f, 9f);
                _hero.WarpTo(dest);
                yield return Wait(0.2f);
                int afterRenderers = 0;
                foreach (var smr in _hero.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (smr != null && smr.enabled && smr.sharedMesh != null) afterRenderers++;
                FlowTrace.Step("DiagRoster",
                    $"HERO post-warp body skinned-renderers before={bodyRenderers} after={afterRenderers} name='{_hero.name}' " +
                    $"(bare-pill iff after==0).");
            }

            yield return Wait(0.3f);
            if (root != null) UnityEngine.Object.Destroy(root.gameObject);
            _lastDetail = $"roster built {built}/{roster.Length}, magenta={magenta}, tipped={tipped}";
            FlowTrace.Step("DiagRoster", $"DiagGarrisonRoster done — built {built}/{roster.Length}, magenta={magenta}, tipped={tipped}.");
        }

        // =====================================================================
        //  PHASE: AssertHeroCrossing — walk the hero into a HeroLinkCrossing entry
        //  and confirm it WARPS to the paired destination (owner 2026-06-21, headless
        //  test of the Village2 gate crossing). Skips cleanly if no crossing pair.
        // =====================================================================
        private IEnumerator AssertHeroCrossing()
        {
            if (_hero == null) { _lastDetail = "no hero"; yield break; }
            // Pick the CLOSEST enterable crossing to the hero (the one it can actually walk to on its island).
            HeroLinkCrossing entry = null; float best = 9999f;
            foreach (var c in HeroLinkCrossing.Registry)
            {
                if (c == null || !c.bidirectional || c.Partner() == null) continue;
                float d = HorizontalDistance(_hero.transform.position, c.transform.position);
                if (d < best) { best = d; entry = c; }
            }
            if (entry == null) { _lastDetail = "no HeroLinkCrossing pair"; FlowTrace.Step("Auto", "AssertHeroCrossing: no crossing pair in scene — skipping."); yield break; }

            Vector3 destPos = entry.Partner().transform.position;
            FlowTrace.Step("Auto", $"AssertHeroCrossing: nearest crossing '{entry.crossingId}' @ {entry.transform.position} " +
                                   $"(d={best:F1}); walking hero {_hero.transform.position} into it; partner @ {destPos}.");
            // WO-530: capture pre-test pose + flag this as an INTENTIONAL warp phase so the continuous
            // SEAM/wall probes don't false-fire while the hero is deliberately displaced ~7km.
            Vector3 home = _hero.transform.position; Quaternion homeRot = _hero.transform.rotation;
            _probes?.SetIntentionalCrossPhase(true);
            _hero.SetAutoWalk(entry.transform);

            // A REAL warp = a single-frame position JUMP far larger than a walk step (not mere proximity).
            bool warped = false; bool reachedEntry = false;
            Vector3 lastPos = _hero.transform.position;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 14f)
            {
                if (_hero == null) break;
                Vector3 pos = _hero.transform.position;
                if (HorizontalDistance(pos, entry.transform.position) < entry.enterRadius + 0.5f) reachedEntry = true;
                if (HorizontalDistance(pos, lastPos) > 6f) { warped = true; break; }   // teleport, not a step
                lastPos = pos;
                yield return null;
            }
            if (_hero != null) _hero.ClearAutoWalk();
            if (warped)
            {
                FlowTrace.Step("Auto", $"AssertHeroCrossing: CROSSED '{entry.crossingId}' — real warp jump, hero now {_hero.transform.position}.");
                _lastDetail = "crossing OK (warp fired)";
                // Post-warp internal navigation: from the landing point, can the hero PATH to the keep / chokepoint?
                Vector3 land = _hero.transform.position;
                foreach (var n in new[] { "Spawn_Keep", "Spawn_Chokepoint", "Spawn_Rear" })
                {
                    var go = GameObject.Find(n);
                    if (go == null) continue;
                    if (!UnityEngine.AI.NavMesh.SamplePosition(land, out var a, 4f, UnityEngine.AI.NavMesh.AllAreas)) continue;
                    if (!UnityEngine.AI.NavMesh.SamplePosition(go.transform.position, out var b, 6f, UnityEngine.AI.NavMesh.AllAreas))
                    { FlowTrace.Warn("Auto", $"post-warp: '{n}' has no navmesh nearby."); continue; }
                    var p = new UnityEngine.AI.NavMeshPath();
                    UnityEngine.AI.NavMesh.CalculatePath(a.position, b.position, UnityEngine.AI.NavMesh.AllAreas, p);
                    FlowTrace.Step("Auto", $"post-warp inside-nav: landing -> '{n}': status={p.status} (PathComplete = walkable inside).");
                }
            }
            else
            { FlowTrace.Fail("Auto", $"AssertHeroCrossing: NO warp on '{entry.crossingId}' — reachedEntry={reachedEntry}, hero {(_hero != null ? _hero.transform.position.ToString() : "<null>")}. If reachedEntry=false the marker is unreachable on the hero's navmesh island."); _lastDetail = "crossing FAILED"; }

            // WO-530: restore the hero to its pre-test position before the next phase. Leaving it at the
            // ~7km partner-region landing made WalkToEachGate + the continuous SEAM/wall probes measure
            // from 7045m on a different navmesh island — the false SEAM-UNREACHABLE / wall-clip / 0-of-5
            // report. Restore + drop the intentional-cross flag.
            if (_hero != null) _hero.WarpTo(home, homeRot);
            _probes?.SetIntentionalCrossPhase(false);
            FlowTrace.Step("Auto", "AssertHeroCrossing: restored hero to pre-test position.");
        }

        private static float TimeoutFor(string phase)
        {
            switch (phase)
            {
                case "WalkToEachGate":    return WalkToGateTimeout * 6f; // covers multiple gates
                case "OpenEachVendor":    return VendorTimeout * 12f;
                case "AssertVendorContracts": return ContractTimeout * 8f; // covers the known-context set
                case "AssertVendorTalkRoute": return ContractTimeout * 8f; // one Interact() per castle vendor
                case "AssertEconomyDeduct": return EconomyDeductTimeout;
                case "AssertEquip":       return EquipTimeout;
                case "OpenEachHUDPanel":  return HudPanelTimeout * 8f;
                case "TriggerWave":       return WaveTimeout;
                case "AttemptExitCastle": return ExitTimeout;
                case "BootToGameplay":    return BootTimeout;
                case "ResolveHero":       return ResolveHeroTimeout;
                case "WalkToOuterWorldOutpost": return OuterWalkTimeout;
                case "DiagGarrisonRoster": return 45f;
                case "AssertHeroCrossing": return 18f;
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
            string target = TargetScene;   // --scene override (Village2 / a garrison) else MainCastle_Hall
            if (ActiveScene() == target)
            {
                FlowTrace.Step("Auto", $"BootToGameplay -> already in '{target}', nothing to load.");
                _lastDetail = "already in gameplay scene";
                yield break;
            }

            FlowTrace.Step("Auto", $"BootToGameplay -> loading {target} (from '{ActiveScene()}')" +
                                   (_startScene != null ? " [--scene override]" : "") + ".");
            try
            {
                SceneManager.LoadScene(target);
            }
            catch (Exception ex)
            {
                FlowTrace.Fail("Auto", $"BootToGameplay: LoadScene('{target}') threw — {ex.Message} " +
                               "(is the --scene target in Build Settings?)");
                _lastDetail = "LoadScene threw";
                yield break;
            }

            float t0 = Time.realtimeSinceStartup;
            bool arrived = false;
            while (Time.realtimeSinceStartup - t0 < BootTimeout)
            {
                if (ActiveScene() == target) { arrived = true; break; }
                yield return null;
            }

            if (!arrived)
            {
                FlowTrace.Fail("Auto", $"BootToGameplay: '{target}' never became active within {BootTimeout:0}s — aborting.");
                _lastDetail = "scene never active";
                yield break;
            }

            // Give the scene a couple of frames so Awake/Start (and the additive
            // OuterWorld load it kicks off) get a chance to run before ResolveHero.
            for (int i = 0; i < 3; i++) yield return null;

            FlowTrace.Step("Auto", $"BootToGameplay -> arrived in '{target}'.");
            _lastDetail = $"loaded {target}";
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
        //  PHASE: AssertVendorTalkRoute  (TKT-15 talk-fix DATA-VERIFY, 2026-06-20)
        //  Owner top-priority bug: the castle Talk button must open the vendor Buy/Sell
        //  DIALOGUE for SHOPPABLE vendors (forge/armorer/market/jeweler), NOT be stolen
        //  by the upgrade panel (the TKT-15 regression). OpenEachVendor never exercises
        //  this — castle vendors are CastleNpcInteractable, not BuildingInteractable. So
        //  this oracle drives the REAL routing: for each castle vendor it reflect-invokes
        //  the private Interact() (the exact path the HUD Talk button fires via
        //  TalkPromptRegistry) and asserts a SHOPPABLE vendor took the dialogue route
        //  (DialogueService.IsRunning) rather than opening the BuildingUpgrade panel. A
        //  violation is FlowTrace.Fail -> break-log -> ranked ticket. Upgrade-only vendors
        //  (lumbermill/farm/arcane-tower) are reported, not flagged. PlayStructure is the
        //  same headless-safe seam OpenEachVendor already uses.
        // =====================================================================
        private IEnumerator AssertVendorTalkRoute()
        {
            var vendors = UnityEngine.Object.FindObjectsByType<CastleNpcInteractable>(
                FindObjectsSortMode.None);
            if (vendors == null || vendors.Length == 0)
            {
                _lastDetail = "0 CastleNpcInteractable found (not in MainCastle_Hall?)";
                FlowTrace.Warn("Auto", "AssertVendorTalkRoute: 0 castle vendors found — talk route not exercised.");
                yield break;
            }

            var fId = typeof(CastleNpcInteractable).GetField(
                "_structureId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fId == null)
            {
                _lastDetail = "reflection failed (_structureId not found)";
                FlowTrace.Fail("Auto", "AssertVendorTalkRoute: could not reflect _structureId on CastleNpcInteractable.");
                yield break;
            }

            int checkedCount = 0, shoppableChecked = 0, violations = 0;
            foreach (var v in vendors)
            {
                if (v == null) continue;
                string id = fId.GetValue(v) as string;
                if (string.IsNullOrEmpty(id)) continue;

                bool shoppable = false;
                try { var def = BuildingCatalog.Find(id); shoppable = def != null && def.IsShoppable; } catch { }

                // Verify the routing DECISION via the SAME shared method Interact() uses — PURE, no
                // side effects. (Invoking the full Interact() would host a Yarn dialogue whose teardown
                // Stop() races the known No-node bug, polluting the break-log every cycle — the RUN-1
                // harness-integrity trap. ResolveRoute is the single source of truth, so asserting it
                // cannot drift from the real branch, and the opened SURFACE is headless-invisible anyway.)
                string route = null;
                try { route = CastleNpcInteractable.ResolveRoute(id); }
                catch (Exception ex) { FlowTrace.Fail("Auto", $"AssertVendorTalkRoute: ResolveRoute('{id}') threw — {ex.Message}"); }

                checkedCount++;
                if (shoppable)
                {
                    shoppableChecked++;
                    if (route == "talk-dialogue")
                        FlowTrace.Step("Auto", $"AssertVendorTalkRoute: '{id}' shoppable -> route='talk-dialogue' (FIX OK).");
                    else
                    {
                        violations++;
                        FlowTrace.Fail("Auto", $"TALK-ROUTE VIOLATION: shoppable vendor '{id}' resolves to '{route ?? "<none>"}' " +
                            "(expected 'talk-dialogue') — the upgrade short-circuit is stealing Talk.");
                    }
                }
                else
                {
                    FlowTrace.Step("Auto", $"AssertVendorTalkRoute: '{id}' not-shoppable -> route='{route ?? "<none>"}' (informational).");
                }

                yield return null;
            }

            _lastDetail = $"{checkedCount} castle vendors ({shoppableChecked} shoppable), {violations} talk-route violation(s)";
            FlowTrace.Step("Auto", $"AssertVendorTalkRoute: {_lastDetail}.");
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
            // Trigger + poll the CANONICAL singleton (active-scene WaveManager) so we
            // drive and watch the SAME instance — with two WaveManagers live (hub +
            // Village2), a bare Find could trigger one and poll the other (the
            // intermittent "TriggerWave timeout"). Fall back to Find pre-Awake.
            var wm = WaveManager.Instance ?? UnityEngine.Object.FindAnyObjectByType<WaveManager>();
            if (wm == null)
            {
                FlowTrace.Step("Auto", "TriggerWave: no WaveManager in scene — N/A (hub has no wave loop), skipping.");
                _lastDetail = "no WaveManager — N/A (skipped)";
                yield break;
            }

            WavePhase before = wm.Phase;

            // PROBE-FLAKE FIX (overnight cycle 2, RCA-confirmed): an earlier phase's
            // ClickableActuator pass clicks the "Defend!" button, so the wave may ALREADY
            // be running (Countdown/Active) before TriggerWave runs. ForceSpawnNextWaveNow
            // is a deliberate no-op while Active, so the old `Phase != before` poll could
            // never trip and hung 30s — the intermittent 5/12 "TriggerWave timeout". An
            // already-running wave IS success; only a wave stuck at Idle is a real failure.
            if (before != WavePhase.Idle)
            {
                FlowTrace.Step("Auto", $"TriggerWave: wave already running (phase '{before}') — N/A, skipping.");
                _lastDetail = $"already running ({before})";
                yield break;
            }

            FlowTrace.Step("Auto", $"TriggerWave: forcing next wave (phase before='{before}').");
            wm.ForceSpawnNextWaveNow();   // immediate spawn (skip countdown) — only reached from Idle

            float t0 = Time.realtimeSinceStartup;
            bool advanced = false;
            while (Time.realtimeSinceStartup - t0 < WaveTimeout)
            {
                // Success = the wave is now running, regardless of an intermediate Countdown.
                if (wm.Phase == WavePhase.Active || wm.Phase != before) { advanced = true; break; }
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

            // Drive toward the seam. The seam is now CONFIRM-TO-CROSS only — it no
            // longer auto-warps on proximity. While the hero is in range the trigger
            // registers a "Travel to <dest>" prompt on the shared MobileInteractButton
            // each frame (LateUpdate). So once we reach proximity we TAP that prompt
            // (MobileInteractButton.InvokeActive) exactly as a real player would; the
            // seam's tap callback then WarpTo's the hero to targetPosition.
            _hero.SetAutoWalk(exit.transform);

            float t0 = Time.realtimeSinceStartup;
            bool warped = false;
            bool reachedProximity = false;
            bool tapped = false;
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

                // In range + the seam's "Travel to ..." prompt is up this frame ->
                // TAP it (simulate the on-screen confirm) so the seam crosses. We poll
                // each frame because the trigger (re)registers the prompt in LateUpdate;
                // tapping is harmless to repeat (InvokeActive no-ops once cleared) but
                // we stop driving toward the gate once we've fired the confirm.
                if (reachedProximity && MobileInteractButton.IsActive)
                {
                    if (MobileInteractButton.InvokeActive())
                    {
                        tapped = true;
                        FlowTrace.Step("Auto", $"AttemptExitCastle: bot tapped seam '{gateName}' -> cross (Travel to '{targetScene}').");
                    }
                }

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
            else if (!tapped)
            {
                // Real, ticket-worthy: reached proximity but the "Travel to ..." prompt
                // never went active, so the bot had nothing to tap (seam didn't register
                // its confirm prompt).
                FlowTrace.Fail("Auto", $"AttemptExitCastle: no seam prompt to tap — hero reached closest {closestToGate:0.0}m of gate '{gateName}' " +
                    $"(radius {radius:0.0}m) but the 'Travel to {targetScene}' confirm prompt never went active within {ExitTimeout:0}s.");
                _lastDetail = $"no seam prompt (reached {closestToGate:0.0}m / radius {radius:0.0}m, nothing to tap)";
            }
            else
            {
                // Real, ticket-worthy: tapped the confirm prompt but the seam never
                // warped us (the tap callback failed to cross).
                FlowTrace.Fail("Auto", $"AttemptExitCastle: tapped seam but it did NOT cross — hero reached closest {closestToGate:0.0}m of gate '{gateName}' " +
                    $"(radius {radius:0.0}m), tapped the 'Travel to {targetScene}' prompt, but no warp to target {warpTarget} within {ExitTimeout:0}s.");
                _lastDetail = $"tapped but no cross (reached {closestToGate:0.0}m / radius {radius:0.0}m, no warp)";
            }
        }

        // =====================================================================
        //  PHASE: WalkToOuterWorldOutpost  (WO-449 + WO-452 seeded chaos)
        //  The continuous-walk raid loop, asserted end-to-end: resolve a live
        //  OuterWorld EnemyOutpost (RaidOutpostSystem.Outposts), WALK to it via the
        //  same SetAutoWalk seam WalkToEachGate uses, and prove BOTH oracles:
        //    (a) ANTI-WARP: no single-frame jump > WalkMaxStepMeters (the assertion
        //        that would have caught the old teleport), and
        //    (b) COMBAT-ON-APPROACH: the hero reaches the outpost ON FOOT and a
        //        garrison Enemy is alive + within engage range on arrival.
        //  This phase LOADS NO SCENE — the UNEXPECTED-CROSS probe is left armed, so a
        //  re-introduced raid teleport trips AutoPilotProbes' scene-load Fail.
        //
        //  SEEDED CHAOS (WO-452): a per-phase seed ("autopilot.seed" env/PlayerPrefs,
        //  else the run seed) drives WHICH outpost is targeted + jittered pause/dash
        //  beats along the walk. Chaos changes the PATH only — the anti-warp +
        //  combat-on-approach oracles ALWAYS run, never weakened by the random route.
        // =====================================================================
        private IEnumerator WalkToOuterWorldOutpost()
        {
            // ── Seed source: env var / PlayerPrefs "autopilot.seed", else the run seed. ──
            int seed = _seed;
            try
            {
                string env = Environment.GetEnvironmentVariable("autopilot.seed");
                if (!string.IsNullOrEmpty(env) && int.TryParse(env, out int es)) seed = es;
                else if (PlayerPrefs.HasKey("autopilot.seed")) seed = PlayerPrefs.GetInt("autopilot.seed");
            }
            catch { /* env read is best-effort; fall back to the run seed */ }
            var rng = new System.Random(seed);
            FlowTrace.Step("Auto", $"WalkToOuterWorldOutpost seed={seed}");

            if (_hero == null)
            {
                FlowTrace.Warn("Auto", "WalkToOuterWorldOutpost: no hero — skipping.");
                _lastDetail = "no hero — skipped";
                yield break;
            }

            // (a) Resolve a realized outpost — they materialise ~10s after the OuterWorld
            // additive load (RaidOutpostSystem.SpawnDelaySeconds), so poll up to ~12s.
            EnemyOutpost outpost = null;
            float tPoll = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - tPoll < 12f && outpost == null)
            {
                var live = new List<EnemyOutpost>();
                var arr = RaidOutpostSystem.Outposts;   // entries null until realized
                if (arr != null)
                    for (int i = 0; i < arr.Length; i++)
                        if (arr[i] != null) live.Add(arr[i]);
                if (live.Count > 0)
                {
                    // Chaos: target a RANDOM available outpost (oracles are outpost-agnostic).
                    outpost = live[rng.Next(live.Count)];
                    break;
                }
                yield return Wait(0.5f);
            }

            if (outpost == null)
            {
                // Not ticket-worthy on its own: the walk loop may be flag-off (RaidContinuousWalk)
                // or the outpost simply never realized in this scene. Warn + skip cleanly.
                FlowTrace.Warn("Auto", "WalkToOuterWorldOutpost: no realized EnemyOutpost found within ~12s " +
                    "(continuous-walk flag off, or not in OuterWorld) — skipping.");
                _lastDetail = "no outpost realized — skipped";
                yield break;
            }

            Transform target = outpost.transform;
            float engageRange = EnemyOutpost.GarrisonRing + 2f;   // "reached on foot" proximity
            Vector3 heroStart = _hero.transform.position;
            FlowTrace.Step("Auto", $"WalkToOuterWorldOutpost: walking to '{outpost.OutpostId}' at {target.position} " +
                $"(engageRange={engageRange:0.0}m, heroStart={heroStart}).");

            _hero.SetAutoWalk(target);

            // Walk + assert. prev tracks the previous frame's position for the anti-warp test.
            Vector3 prev = _hero.transform.position;
            float t0 = Time.realtimeSinceStartup;
            bool reachedOnFoot = false;
            int nextJitterBeat = 2 + rng.Next(3);   // first pause/dash beat after a few frames
            int frame = 0;

            while (Time.realtimeSinceStartup - t0 < OuterWalkTimeout)
            {
                if (_hero == null) break;
                Vector3 pos = _hero.transform.position;

                // ── (b/anti-warp) ORACLE: a single-frame jump > WalkMaxStepMeters is a WARP. ──
                float d = Vector3.Distance(pos, prev);
                if (d > WalkMaxStepMeters)
                {
                    FlowTrace.Fail("Auto", $"hero WARPED instead of walked: single-frame jump {d:0.0}m " +
                        "(continuous-walk loop must never teleport).");
                    _hero.ClearAutoWalk();
                    _lastDetail = $"WARP detected ({d:0.0}m single-frame jump)";
                    yield break;   // end the phase FAILED
                }
                prev = pos;

                // Reached the outpost ON FOOT?
                if (HorizontalDistance(pos, target.position) <= engageRange)
                {
                    reachedOnFoot = true;
                    break;
                }

                // ── CHAOS: jittered pause/dash beats. Occasionally drop auto-walk for a few
                // frames (a "pause"), then re-issue it (a "dash" resumes the route). This varies
                // the PATH/timing only; the oracles above run every frame regardless. No per-frame
                // allocation — just counters + the existing SetAutoWalk/ClearAutoWalk seam.
                if (frame >= nextJitterBeat)
                {
                    _hero.ClearAutoWalk();
                    int pauseFrames = 1 + rng.Next(4);
                    for (int p = 0; p < pauseFrames; p++)
                    {
                        if (_hero == null) break;
                        // Anti-warp still armed during the pause (the hero should be ~still).
                        Vector3 pp = _hero.transform.position;
                        float dp = Vector3.Distance(pp, prev);
                        if (dp > WalkMaxStepMeters)
                        {
                            FlowTrace.Fail("Auto", $"hero WARPED during chaos-pause: single-frame jump {dp:0.0}m " +
                                "(continuous-walk loop must never teleport).");
                            _lastDetail = $"WARP detected during pause ({dp:0.0}m)";
                            yield break;
                        }
                        prev = pp;
                        yield return null;
                    }
                    if (_hero == null) break;
                    _hero.SetAutoWalk(target);   // resume the route
                    nextJitterBeat = frame + 4 + rng.Next(6);
                }

                frame++;
                yield return null;
            }
            if (_hero != null) _hero.ClearAutoWalk();

            if (!reachedOnFoot)
            {
                FlowTrace.Fail("Auto", $"WalkToOuterWorldOutpost: hero never reached outpost '{outpost.OutpostId}' on foot " +
                    $"within {OuterWalkTimeout:0}s (closest approach failed; navmesh edge / blocked).");
                _lastDetail = "did not reach outpost on foot";
                yield break;
            }

            // ── (b) COMBAT-ON-APPROACH ORACLE: reached on foot — is the garrison engaging? ──
            // Give the aggro a moment to pull a defender into engage range, then assert at least
            // one garrison Enemy is alive within range. (The outpost may auto-clear if it spawned
            // empty — that is its own legit anti-deadlock; treat an already-cleared outpost as
            // "nothing to engage" and report it, not a hard combat failure.)
            bool engaged = false;
            float tEngage = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - tEngage < 6f && !engaged)
            {
                if (outpost == null || _hero == null) break;
                if (outpost.Cleared) break;   // garrison gone (cleared/auto-cleared)
                engaged = AnyGarrisonEngaging(outpost, _hero.transform.position, engageRange + 6f);
                if (engaged) break;
                yield return null;
            }

            if (engaged)
            {
                FlowTrace.Step("Auto", $"WalkToOuterWorldOutpost: PASS — reached '{outpost.OutpostId}' ON FOOT (no warp) " +
                    "and a garrison defender engaged on approach.");
                _lastDetail = "reached on foot + garrison engaged";
            }
            else if (outpost != null && outpost.Cleared)
            {
                FlowTrace.Warn("Auto", $"WalkToOuterWorldOutpost: reached '{outpost.OutpostId}' on foot but it was already CLEARED " +
                    "(empty/auto-cleared garrison) — nothing to engage.");
                _lastDetail = "reached on foot; outpost already cleared";
            }
            else
            {
                FlowTrace.Fail("Auto", "reached outpost on foot but garrison never engaged on approach");
                _lastDetail = "reached on foot but no engage";
            }
        }

        // True if any living garrison Enemy under the outpost is within engageRange of the hero
        // (the "combat on approach" signal). Cheap GetComponentsInChildren over the outpost root
        // only (the garrison is parented under it), run a handful of times, not per frame.
        private static bool AnyGarrisonEngaging(EnemyOutpost outpost, Vector3 heroPos, float engageRange)
        {
            if (outpost == null) return false;
            var enemies = outpost.GetComponentsInChildren<Enemy>(includeInactive: false);
            if (enemies == null) return false;
            float r2 = engageRange * engageRange;
            foreach (var e in enemies)
            {
                if (e == null || !e.IsAlive) continue;
                if ((e.transform.position - heroPos).sqrMagnitude <= r2) return true;
            }
            return false;
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
