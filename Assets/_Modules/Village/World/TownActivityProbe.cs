// =============================================================================
// TownActivityProbe - INSTRUMENTATION ONLY for the town-suspension ruling
// (owner 2026-08-07: "everything pauses except harvesting, while player is
// active").
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHY THIS EXISTS (CLAUDE.md section 12): the implementation rule for that ruling
// was "enumerate what actually ticks during a dungeon from FlowTrace before
// switching anything off - do not guess the list". A static read of the codebase
// produces a CANDIDATE list; it cannot produce the real one, because what is
// alive during a dungeon depends on scene lifetime, on which singletons are
// DontDestroyOnLoad, and on whether the town scene stayed additively loaded. All
// three vary at runtime and none of them are visible from source.
//
// So this probe reports, from a REAL dungeon session, three things per town
// system, on change only:
//   * ALIVE?    - does the object still exist once the player is elsewhere
//   * WHERE     - which scene owns it (or DontDestroyOnLoad), because that is
//                 what decides whether TownSuspension.SuspendedFor catches it
//   * GATED?    - would the suspension gate actually hold it right now
//
// THAT THIRD COLUMN IS THE POINT. A system that is alive and ticking but which
// SuspendedFor() returns false for is an UNGATED LEAK - the town is still being
// acted on while the player is away, and the suspension silently does not cover
// it. Those lines are logged as warnings so a capture surfaces them without
// anyone having to reason about assembly-level scene ownership by hand.
//
// It also names anything sitting in the ACTIVE scene, because those are the
// objects that MUST NOT be suspended (the player is standing among them). A
// suspension that started catching those would be the Time.timeScale mistake
// arriving by a different road, and this probe is how that gets caught.
//
// READ-ONLY. Never suspends, resumes, damages, spawns or mutates anything.
// Instrumented [Flow:TownProbe].
// =============================================================================

using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Read-only diagnostic that enumerates which TOWN systems are still alive and
    /// ticking while the player is elsewhere, and whether the town-suspension gate
    /// actually covers each one. Turns the suspension's coverage from an assumption
    /// into a captured line.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TownActivityProbe : MonoBehaviour
    {
        private const float PollInterval = 3f;

        private float _timer;
        private string _lastReport;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TrySpawn();
        }

        private static void OnSceneLoaded(Scene s, LoadSceneMode mode) => TrySpawn();

        private static void TrySpawn()
        {
            if (FindAnyObjectByType<TownActivityProbe>() != null) return;
            var go = new GameObject("TownActivityProbe");
            DontDestroyOnLoad(go);   // must outlive the town scene to observe its absence
            go.AddComponent<TownActivityProbe>();
        }

        private void Update()
        {
            // WO-1483: town frame path — Poll ENUMERATES town activity, so its cadence and
            // its per-poll cost both matter to the empty-town floor.
            using var _perf = DeNelle.Core.Diagnostics.FlowTrace.Measure(
                "Perf", "TownActivityProbe.Update", 4f, 1f);

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = PollInterval;
            Guard.Try("TownProbe", "enumerate town activity", Poll);
        }

        private void Poll()
        {
            // Only interesting while the player is actually elsewhere. In the hub this
            // would be a constant, meaningless heartbeat.
            var active = SceneManager.GetActiveScene();
            if (HubScenes.IsHub(active.name)) return;

            var findings = new List<string>();
            int ungated = 0;

            // WAVES - the headline system. Scene-scoped and baked into the hub scenes, so
            // whether it is even ALIVE here answers the first real question: does the town
            // keep running because its scene stayed loaded, or does it simply cease to
            // exist and the true defect is what happens on RETURN?
            foreach (var wm in FindObjectsByType<WaveManager>(FindObjectsSortMode.None))
                ungated += Note(findings, "WaveManager(phase=" + wm.Phase + ",cd=" +
                                          wm.CountdownRemaining.ToString("0.0") + ")", wm.gameObject, active);

            // STRUCTURE DAMAGE-OVER-TIME - a burning town structure keeps losing HP.
            foreach (var burn in FindObjectsByType<StructureBurn>(FindObjectsSortMode.None))
            {
                if (!burn.IsBurning) continue;
                ungated += Note(findings, "StructureBurn(BURNING '" + burn.name + "')", burn.gameObject, active);
            }

            // THE HEART - event-driven, so it never ticks on its own; what matters is
            // whether it still EXISTS to be contact-damaged by something that does.
            foreach (var heart in FindObjectsByType<HeartController>(FindObjectsSortMode.None))
                ungated += Note(findings, "HeartController", heart.gameObject, active);

            // LIVE ENEMIES - these Update independently of the wave loop, and their bodies
            // are pooled under DontDestroyOnLoad, so "the wave manager is gone" does NOT
            // imply "nothing is attacking the town". Split by scene so dungeon enemies
            // (which must keep running) are never confused with town enemies (which must not).
            int townEnemies = 0, activeSceneEnemies = 0;
            foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
            {
                if (e == null || !e.gameObject.activeInHierarchy) continue;
                if (e.gameObject.scene.handle == active.handle) activeSceneEnemies++;
                else townEnemies++;
            }
            if (townEnemies > 0)
            {
                findings.Add("Enemy x" + townEnemies + " OUTSIDE the active scene [gated=" +
                             TownSuspension.SuspendedFor((GameObject)null) + "]");
            }
            if (activeSceneEnemies > 0)
                findings.Add("Enemy x" + activeSceneEnemies + " in the ACTIVE scene (these MUST keep running)");

            string report =
                "scene='" + active.name + "' suspended=" + TownSuspension.IsSuspended +
                " grace=" + TownSuspension.ReturnGraceRemaining.ToString("0.0") + "s" +
                " policy=" + TownSuspension.WavePolicy +
                " reason='" + TownSuspension.Reason + "' :: " +
                (findings.Count == 0 ? "no town systems alive here" : Join(findings));

            if (report == _lastReport) return;
            _lastReport = report;

            if (!TownSuspension.IsSuspended && findings.Count > 0)
                FlowTrace.Fail("TownProbe",
                    report + " -> town systems are alive while the player is NOT in a hub scene, " +
                    "and the suspension is NOT engaged. The scene-driven gate did not fire for this scene.");
            else if (ungated > 0)
                FlowTrace.Warn("TownProbe",
                    report + " -> " + ungated + " town system(s) are alive but NOT covered by " +
                    "TownSuspension.SuspendedFor. These are ungated leaks: the town is still being " +
                    "acted on while the player is away.");
            else
                FlowTrace.Step("TownProbe", report);
        }

        /// <summary>
        /// Record one system with its scene and whether the suspension gate covers it.
        /// Returns 1 when it is an UNGATED leak (alive, town-side, but not held), else 0.
        /// </summary>
        private static int Note(List<string> into, string label, GameObject go, Scene active)
        {
            bool inActive = go != null && go.scene.handle == active.handle;
            bool gated = TownSuspension.SuspendedFor(go);
            string where = go == null ? "<null>"
                : (string.IsNullOrEmpty(go.scene.name) ? "DontDestroyOnLoad" : go.scene.name);

            into.Add(label + " scene=" + where + (inActive ? "(ACTIVE-must-not-suspend)" : "") +
                     " gated=" + gated);

            // Something town-side, alive, and not held is the leak this probe exists to name.
            return (!inActive && !gated) ? 1 : 0;
        }

        private static string Join(IEnumerable<string> parts)
        {
            var sb = new StringBuilder();
            foreach (var p in parts)
            {
                if (sb.Length > 0) sb.Append(" | ");
                sb.Append(p);
            }
            return sb.ToString();
        }
    }
}
