// =============================================================================
// OfflineHarvestBootstrap — self-installs the WO-115 OfflineHarvestService at
// runtime (no scene authoring, no VillageSceneBuilder / scene re-save), mirroring
// LevelUpSkillPopupBootstrap / MusicToggleBootstrap.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// One persistent service across scenes (DontDestroyOnLoad) — the offline clock is
// global, not per-scene, so a single instance owns it. The service's own Start /
// OnApplicationPause drive the claim; the welcome-back popup only renders if the
// current scene has a UIDocument to host it (else the haul is still banked silently).
// Installed AfterSceneLoad so GameStateService (which loads the save in its Awake)
// is already up by the time the service reads LastHarvestClaimMs.
// =============================================================================
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>Installs a single persistent <see cref="OfflineHarvestService"/> at runtime.</summary>
    public static class OfflineHarvestBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (OfflineHarvestService.Instance != null) return;
            var go = new GameObject("OfflineHarvestService");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<OfflineHarvestService>();
        }
    }
}
