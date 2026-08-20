// =============================================================================
// OfflineContentBootstrap — PROD-010. Runs OfflineContentService.ResolveContentSource
// once per launch, before anything asks Addressables for content.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core. Self-installing, no scene authoring — the same
// [RuntimeInitializeOnLoadMethod] convention CatalogBootstrap and PursuitBattleProbe use.
//
// ⛔ WHY THIS EXISTS AS ITS OWN OBJECT rather than a line in some existing boot path:
// ResolveContentSource is a COROUTINE (it awaits a catalog check that can take seconds
// or hang), and a RuntimeInitialize hook is a plain static method with nowhere to run
// one. So this spawns one DontDestroyOnLoad host, runs it, and gets out of the way.
//
// AfterSceneLoad, not BeforeSceneLoad: coroutines need a live scene. The first content
// request in this project comes from the hub/dungeon load, well after the Title scene
// settles, so resolving here is comfortably ahead of it — but it is NOT a hard barrier,
// and that is deliberate. Blocking the boot on a network check is exactly the stall this
// ticket exists to prevent. A load that races us simply behaves as it does today.
//
// The result lands in OfflineContentService.Source, which any UI can read to say
// something true ("Offline - using downloaded content") instead of guessing.
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core
{
    /// <summary>Boots the PROD-010 content-source resolve exactly once per launch.</summary>
    public sealed class OfflineContentBootstrap : MonoBehaviour
    {
        private static OfflineContentBootstrap s_instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (s_instance != null) return;

            var go = new GameObject("OfflineContentBootstrap");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<OfflineContentBootstrap>();
        }

        private void Start()
        {
            // Guarded: a throw here must never take the boot down. The whole point of
            // PROD-010 is that a bad network degrades instead of failing, and that promise
            // would be hollow if the resolver itself could break the launch.
            Guard.Try("OfflineContent", "resolve content source at boot",
                () => StartCoroutine(OfflineContentService.ResolveContentSource(src =>
                    FlowTrace.Step("OfflineContent", $"boot resolve -> {src}"))));
        }

        private void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }
    }
}
