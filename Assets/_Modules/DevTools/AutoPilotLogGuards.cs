// =============================================================================
// AutoPilotLogGuards — DEV-ONLY runtime panel-health guard for the AutoPilot bot.
// -----------------------------------------------------------------------------
// WO-452 §A (tranche 3). Complements AutoPilotProbes: where the probes watch the
// world (navmesh, walls, magenta materials), THIS component watches the UI layer
// for the duplicate-UIDocument / dead-panel class that bit us (the dev tools went
// "dead after Yarn" because a second enabled OnboardingPanelSettings document sat
// over the scene eating input).
//
// THREE CHECKS, all log-scan / structural (headless-safe — no pixel assertion):
//   1. DUPLICATE UIDocument — >1 ENABLED UIDocument sharing the same PanelSettings
//      in one scene. Two live panels over one PanelSettings raycast-fight + eat
//      input. (PanelSettings names that legitimately drive multiple docs go in
//      ExpectedMultiple.)
//   2. ONBOARDING PANEL IN A GAMEPLAY SCENE — any enabled UIDocument bound to an
//      "Onboarding" PanelSettings while the active scene is NOT an onboarding/title
//      scene. This is the headless detector for the dev-tools-dead-after-Yarn class
//      (the runtime OnboardingPanelGuard fix PREVENTS it; this CATCHES a regression).
//   3. (covered by 1+2) multiple PanelSettings → reported as the duplicate group.
//
// On a hit it emits FlowTrace.Fail("BotUI", ...) so the always-on BreakCaptureHarness
// records it in break-log.jsonl and the headless AutoPilotTickets emitter ranks it.
// Every finding is de-duped per run so a sustained condition logs once.
//
// GATING: spawned + armed ONLY by AutoPilotDriver (autopilot-only). Until Arm() is
// called every check is a no-op, so a stray AddComponent never asserts against a
// normal play session. Scans on Arm, on every sceneLoaded, and on a throttled tick
// (to catch runtime-injected documents).
//
// RELEASE-SAFE: the whole file is #if DEVELOPMENT_BUILD || UNITY_EDITOR.
// =============================================================================

#if DEVELOPMENT_BUILD || UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using DeNelle.Core.Diagnostics;

namespace DeNelle.DevTools
{
    /// <summary>
    /// DEV-ONLY runtime UI-health guard that rides alongside <see cref="AutoPilotDriver"/>.
    /// Detects duplicate UIDocument / multiple-PanelSettings / onboarding-panel-in-
    /// gameplay-scene defects and reports them via <c>FlowTrace.Fail</c> ([Flow:BotUI])
    /// so they land in break-log.jsonl. Spawned + armed by the driver (autopilot-only).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AutoPilotLogGuards : MonoBehaviour
    {
        private const string Tag = "BotUI";

        // Until armed (by the driver), every check is a no-op.
        private bool _armed;

        // Throttled re-scan to catch documents injected at runtime (after sceneLoaded).
        private const float ScanInterval = 5f;
        private float _nextScan;

        // De-dupe: a given defect (stable key) is reported at most once per run.
        private readonly HashSet<string> _reported = new HashSet<string>();

        // PanelSettings names that legitimately drive MORE THAN ONE UIDocument at once
        // (none today — every canonical panel owns its own PanelSettings). Add a name
        // here if a deliberate multi-document panel is introduced.
        private static readonly string[] ExpectedMultiple = Array.Empty<string>();

        /// <summary>
        /// Arm the guard. Called by <see cref="AutoPilotDriver"/> on an autopilot run.
        /// Idempotent; until armed every check is a no-op.
        /// </summary>
        public void Arm()
        {
            if (_armed) return;
            _armed = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            FlowTrace.Step(Tag, "AutoPilotLogGuards ARMED — duplicate-UIDocument / multi-PanelSettings / onboarding-panel-in-gameplay-scene guard active.");
            try { ScanPanels("arm"); }
            catch (Exception ex) { FlowTrace.Warn(Tag, "panel scan (arm) threw: " + ex.Message); }
        }

        private void OnDestroy()
        {
            if (_armed) SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!_armed) return;
            try { ScanPanels("sceneLoaded:" + (scene.name ?? "?")); }
            catch (Exception ex) { FlowTrace.Warn(Tag, "panel scan (sceneLoaded) threw: " + ex.Message); }
        }

        private void Update()
        {
            if (!_armed) return;
            float now = Time.realtimeSinceStartup;
            if (now < _nextScan) return;
            _nextScan = now + ScanInterval;
            try { ScanPanels("tick"); }
            catch (Exception ex) { FlowTrace.Warn(Tag, "panel scan (tick) threw: " + ex.Message); }
        }

        // =====================================================================
        //  The scan — group enabled UIDocuments by PanelSettings, flag dupes +
        //  onboarding panels live in a gameplay scene.
        // =====================================================================
        private void ScanPanels(string cause)
        {
            var docs = UnityEngine.Object.FindObjectsByType<UIDocument>(
                FindObjectsInactive.Include);
            if (docs == null || docs.Length == 0) return;

            string activeScene = SceneManager.GetActiveScene().name ?? "";
            bool inOnboardingScene =
                activeScene.IndexOf("Onboard", StringComparison.OrdinalIgnoreCase) >= 0
                || activeScene.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0
                || activeScene.IndexOf("PetSelect", StringComparison.OrdinalIgnoreCase) >= 0;

            // Group ENABLED documents by their PanelSettings (by asset name).
            var byPanel = new Dictionary<string, List<UIDocument>>();
            foreach (var d in docs)
            {
                if (d == null || !d.isActiveAndEnabled) continue;
                var ps = d.panelSettings;
                string psName = ps != null ? ps.name : "<no-PanelSettings>";

                // CHECK 2: an onboarding panel enabled in a non-onboarding scene = the
                // input-eating dead-UI class (dev tools dead after Yarn).
                if (ps != null
                    && psName.IndexOf("Onboard", StringComparison.OrdinalIgnoreCase) >= 0
                    && !inOnboardingScene)
                {
                    string okKey = "onboarding-in-gameplay:" + d.gameObject.scene.name + ":" + d.name;
                    if (_reported.Add(okKey))
                        FlowTrace.Fail(Tag, $"dead/duplicate UI: UIDocument '{d.name}' is bound to onboarding PanelSettings " +
                            $"'{psName}' and ENABLED in non-onboarding scene '{activeScene}' (cause={cause}) — it eats input / blanks " +
                            "the dev UI (the post-Yarn dead-UI regression the runtime OnboardingPanelGuard prevents).");
                }

                if (!byPanel.TryGetValue(psName, out var list))
                {
                    list = new List<UIDocument>();
                    byPanel[psName] = list;
                }
                list.Add(d);
            }

            // CHECK 1: >1 enabled document sharing a single PanelSettings (not expected-multiple).
            foreach (var kv in byPanel)
            {
                if (kv.Value.Count < 2) continue;
                if (Array.IndexOf(ExpectedMultiple, kv.Key) >= 0) continue;

                string key = "dupe-panel:" + activeScene + ":" + kv.Key;
                if (!_reported.Add(key)) continue;

                var names = new List<string>(kv.Value.Count);
                foreach (var d in kv.Value) names.Add(d != null ? d.name : "<null>");
                FlowTrace.Fail(Tag, $"duplicate UIDocument: {kv.Value.Count} ENABLED documents share PanelSettings '{kv.Key}' " +
                    $"in scene '{activeScene}' (cause={cause}) — docs=[{string.Join(",", names)}]. Two live panels over one " +
                    "PanelSettings raycast-fight / eat input.");
            }
        }
    }
}

#endif // DEVELOPMENT_BUILD || UNITY_EDITOR
