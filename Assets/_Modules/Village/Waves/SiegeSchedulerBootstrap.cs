// =============================================================================
// SiegeSchedulerBootstrap — installs the SiegeScheduler in the hub, scene-free (WO-1026).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// SCENE-INDEPENDENT ON PURPOSE. CLAUDE.md §3 forbids hand-editing a .unity scene, and
// baking a scheduler component into Main_Castle_Overworld would mean a rebake every
// time it changes AND a silent dead loop in whichever hub variant someone forgot.
// Mirrors ManageScreenBootstrap / RealmMapPanelBootstrap.
//
// It installs on EVERY hub scene load and tears down when the player leaves, because
// the scheduler is a hub-only concept: SiegeScheduler.Defer() also checks the active
// scene, so this is belt-and-braces, not the only gate.
//
// It installs even when FeatureFlags.Siege is OFF. That is deliberate: a scheduler
// that is present-and-saying-"deferred: ff.siege OFF" is diagnosable, while an absent
// one is indistinguishable from a broken install. The flag gates the ARMING, not the
// existence. Nothing spawns and no WaveManager entry point is called while it is off.
// =============================================================================

using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>Installs the single <see cref="SiegeScheduler"/> whenever a hub scene loads.</summary>
    public static class SiegeSchedulerBootstrap
    {
        private static bool _hooked;

        /// <summary>Arms the scene hook once per process.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Install()
        {
            if (_hooked) return;
            _hooked = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            InstallForActiveScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            InstallForActiveScene();
        }

        private static void InstallForActiveScene()
        {
            Guard.Try("Siege", "install siege scheduler", () =>
            {
                string scene = SceneManager.GetActiveScene().name;
                bool hub = HubScenes.IsHub(scene);

                if (!hub)
                {
                    if (SiegeScheduler.Instance != null)
                    {
                        Object.Destroy(SiegeScheduler.Instance.gameObject);
                        FlowTrace.Step("Siege", $"scheduler torn down -- left the hub for '{scene}'.");
                    }
                    return;
                }

                if (SiegeScheduler.Instance != null) return;   // idempotent

                var go = new GameObject("SiegeScheduler");
                go.AddComponent<SiegeScheduler>();
                FlowTrace.Step("Siege", $"scheduler installed in hub '{scene}' (flag={FeatureFlags.Siege}).");
            });
        }
    }
}
