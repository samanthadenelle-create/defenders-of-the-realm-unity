// =============================================================================
// EchoHollowVisualInjector — runtime visual swap of the castle-hub Echo Hollow from
// the baked polyperfect STABLES to the lightweight PetHouse2 model (owner 2026-06-17),
// WITHOUT a scene rebuild.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHY runtime (not a bake): MainCastle_Hall bakes Echo Hollow as a polyperfect
// Stables via CastleHubBuilder. The catalog repoint to Structures/PetHouse2 only
// affects build-mode / runtime catalog placement, NOT the baked instance — so the
// town kept showing the stable. A full CastleHubBuilder rebuild of the PRIMARY
// scene can't be verified headless, and PetHouse2 imports with EMBEDDED Tripo
// materials (materialImportMode 2, no extraction) → it would render MAGENTA in URP
// through the builder path (which never URP-fixes materials).
//
// This injector follows the project's established no-scene-edit pattern (CampSystem,
// StoryCompanionInjector, VillageNpcInjector): on every hub load it finds the baked
// Echo Hollow, HIDES its stables renderers (keeping the NPC interact point + roaming
// colliders), and skins PetHouse2 in via VisualFactory.Skin — which BOUNDS-FITS the
// raw FBX to building scale AND URP-fixes the embedded Tripo materials (no magenta).
// Idempotent (a marker child guards re-swaps); graceful (PetHouse2 missing → the
// stables visual is restored, nothing breaks).
// =============================================================================

using DeNelle.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>Re-skins the baked Echo Hollow stables to PetHouse2 at runtime (no scene edit).</summary>
    public static class EchoHollowVisualInjector
    {
        // The name CastleHubBuilder gives the baked Echo Hollow structure instance.
        private const string EchoHollowName = "EchoHollow_Pets_RoamingArea";
        // The skinned PetHouse2 child is renamed to this — also the idempotency guard.
        private const string SwappedMarker  = "EchoHollow_PetHouse2";
        // PetHouse2 is a raw Tripo FBX; fit its largest dimension to building scale.
        private const float  TargetSizeM    = 7f;
        private const string PetHousePath   = "Structures/PetHouse2";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            // Handle the scene already active at boot (no-op unless it's the hub).
            if (HubScenes.IsHub(SceneManager.GetActiveScene().name)) TrySwap();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (HubScenes.IsHub(scene.name)) TrySwap();
        }

        private static void TrySwap()
        {
            Transform echo = FindByName(EchoHollowName);
            if (echo == null) return;                       // not in this scene
            if (echo.Find(SwappedMarker) != null) return;   // already swapped (idempotent)

            // Hide the baked STABLES visual (renderers only — the NPC interact point +
            // roaming-area colliders/logic stay live).
            var stablesRenderers = echo.GetComponentsInChildren<Renderer>(true);
            foreach (var r in stablesRenderers)
                if (r != null) r.enabled = false;

            // Skin PetHouse2 in: SkinOptions.Structure = bounds-fit to TargetSizeM +
            // seat-on-ground + URP-fix the embedded Tripo materials (so it never renders magenta).
            var vis = VisualFactory.Skin(echo, PetHousePath, SkinOptions.Structure(TargetSizeM));
            if (vis == null)
            {
                // PetHouse2 absent on this machine — restore the stables; nothing lost.
                foreach (var r in stablesRenderers)
                    if (r != null) r.enabled = true;
                Debug.LogWarning("[EchoHollowVisualInjector] " + PetHousePath +
                                 " not found — kept the baked stables visual.");
                return;
            }

            vis.name = SwappedMarker;
            Debug.Log("[EchoHollowVisualInjector] Echo Hollow re-skinned to PetHouse2 (stables hidden).");
        }

        // Name match across the loaded scene(s). Runs once per hub load (not per frame).
        private static Transform FindByName(string name)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t != null && t.name == name) return t;
            return null;
        }
    }
}
