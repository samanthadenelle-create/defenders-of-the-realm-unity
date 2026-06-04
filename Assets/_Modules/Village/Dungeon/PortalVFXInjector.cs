// =============================================================================
// PortalVFXInjector — DEF-100 safety net.
// -----------------------------------------------------------------------------
// The portal VFX controller is normally attached by its host (DungeonPortal /
// DungeonEntranceBootstrap). This injector is a belt-and-braces guarantee: after
// every scene load it finds every portal (by DungeonPortal or DungeonEntrance
// component) and ensures a self-sufficient PortalVFXController is present, so the
// interior glow renders even on portals created by a code path that forgot to
// add it. Code-only — no scene/prefab edit. Idempotent (GetComponent guard).
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    internal static class PortalVFXInjector
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            EnsureAll();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureAll();

        private static void EnsureAll()
        {
            // DungeonEntrance is the runtime-built portal; DungeonPortal is the
            // alternate (Buildings) variant. Cover both.
            foreach (var e in Object.FindObjectsOfType<DungeonEntrance>(true))
            {
                if (e == null) continue;
                if (e.GetComponent<PortalVFXController>() == null)
                    e.gameObject.AddComponent<PortalVFXController>();
            }

            foreach (var p in Object.FindObjectsOfType<DungeonPortal>(true))
            {
                if (p == null) continue;
                if (p.GetComponent<PortalVFXController>() == null)
                    p.gameObject.AddComponent<PortalVFXController>();
            }
        }
    }
}
