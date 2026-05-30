// =============================================================================
// WorldSceneLoader — additively loads the OuterWorld scene over the Village so
// the town + the surrounding regions form one continuous world at runtime
// (the owner's "two scenes existing together" model).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHY additive: Village.unity (castle/town, VillageSceneBuilder) and
// OuterWorld.unity (regions + mine nodes, OuterWorldBuilder) are SEPARATE scene
// files so the two builders never collide. At play, this loader brings OuterWorld
// in on top of Village — both run in one shared space, one physics/render world,
// player can't tell it's two scenes.
//
// Self-bootstrapping (AfterSceneLoad, like AudioBootstrap / WaveSystemBridge):
// when Village is the active scene and OuterWorld isn't already loaded, load it
// additively. No scene wiring, no manual call. Safe no-op in any other scene.
// Domain-reload-off safe via the s_done reset.
// =============================================================================
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    public static class WorldSceneLoader
    {
        private const string VillageSceneName    = "Village";
        private const string OuterWorldSceneName  = "OuterWorld";

        private static bool s_done;

        // Reset the guard each play session (domain reload may be disabled).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetGuard() => s_done = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (s_done) return;
            s_done = true;

            // Only pull the outer world in when we're actually in the Village.
            var active = SceneManager.GetActiveScene();
            if (active.name != VillageSceneName) return;

            // Already loaded? (re-entry / editor play-twice) — don't double-load.
            if (SceneManager.GetSceneByName(OuterWorldSceneName).isLoaded) return;

            // Guard: the scene must be in Build Settings to load by name.
            if (!Application.CanStreamedLevelBeLoaded(OuterWorldSceneName))
            {
                Debug.LogWarning("[WorldSceneLoader] '" + OuterWorldSceneName +
                    "' is not in Build Settings — outer world not loaded. " +
                    "Add it (EnsureBuildSettings) so the regions/mine nodes appear.");
                return;
            }

            SceneManager.LoadScene(OuterWorldSceneName, LoadSceneMode.Additive);
            Debug.Log("[WorldSceneLoader] OuterWorld loaded additively over Village.");
        }
    }
}
