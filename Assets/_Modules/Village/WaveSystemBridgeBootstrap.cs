// =============================================================================
// WaveSystemBridgeBootstrap — Sprint C wiring. Attaches the wave-reactive GROUP 3
// bridges (CampaignManager + WaveMusicController + TowerVoiceController) onto the
// WaveManager GameObject at runtime, so they're live without a VillageSceneBuilder
// re-run (which would re-save Village.unity — a known corruption risk).
// -----------------------------------------------------------------------------
// All three are [RequireComponent(typeof(WaveManager))] bridges that self-wire via
// GetComponent (the DailyQuestCombatBridge pattern), so attaching them to the GO
// that already has WaveManager is all that's needed. Idempotent + per-scene. Each
// no-ops safely until its assets are assigned (CampaignManager with no CampaignData,
// WaveMusicController with no AudioClips, TowerVoiceController with no voice lines),
// so this is safe to run in any scene that has a WaveManager.
//
// This lives in DeNelle.Village (same assembly as WaveManager and all three
// bridges), so it attaches them with direct AddComponent<T>() — no reflection,
// unlike VillageSceneBuilder which is in the Editor assembly.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>Runtime-attaches the wave-reactive GROUP 3 bridges to the WaveManager GO.</summary>
    public static class WaveSystemBridgeBootstrap
    {
        private static bool s_hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (s_hooked) return;
            s_hooked = true;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Attach();   // the first scene is already loaded when this runs
        }

        private static void OnSceneLoaded(Scene s, LoadSceneMode mode) => Attach();

        private static void Attach()
        {
            var managers = Object.FindObjectsByType<WaveManager>(FindObjectsSortMode.None);
            if (managers.Length == 0) return;   // not a wave scene — nothing to wire

            var go = managers[0].gameObject;
            Ensure<CampaignManager>(go);
            Ensure<WaveMusicController>(go);
            Ensure<TowerVoiceController>(go);
        }

        private static void Ensure<T>(GameObject go) where T : Component
        {
            if (go.GetComponent<T>() == null) go.AddComponent<T>();
        }
    }
}
