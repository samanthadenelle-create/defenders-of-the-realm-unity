// =============================================================================
// CameraModeControllerBootstrap (WO-338) — auto-attaches CameraModeController to
// the game camera at runtime, so the context-aware TOWN/BATTLE camera mode arms
// with NO scene edit / rebake.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village (same assembly as CameraModeController + SmartMobileCamera)
//
// CameraModeController only activates when it sits on the Main Camera next to
// SmartMobileCamera (it post-processes SMC's transform — see its file header).
// This bootstrap finds that camera and AddComponent<CameraModeController>() if
// absent — mirroring CompassHudBootstrap / WaveSystemBridgeBootstrap:
//   • [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] for the first scene
//   • SceneManager.sceneLoaded re-arm for additive / swapped scenes
//   • Village*-scene-gated (the town bird's-eye is a village concept; we don't
//     want it hijacking the Title / HeroSelect / battle-only scenes)
//   • try/catch around the work (WebGL halts the whole player on an uncaught throw)
//   • Idempotent (DisallowMultipleComponent + an explicit GetComponent guard)
//
// We attach to the camera that already has SmartMobileCamera (the validated 3rd-
// person rig); if none exists in the scene we fall back to Camera.main so the mode
// controller still arms (it stands down gracefully when SMC is absent).
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>
    /// Runtime-attaches <see cref="CameraModeController"/> to the game camera on
    /// village scene load, so WO-338 auto-wires without a scene edit.
    /// </summary>
    public static class CameraModeControllerBootstrap
    {
        // Domain-reload-off safety: statics persist between Play sessions, so a
        // hooked flag would skip re-arming on the 2nd Play. Reset before Init.
        private static bool s_hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_hooked = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (s_hooked) return;
            s_hooked = true;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryAttach(SceneManager.GetActiveScene()); // first scene is already loaded
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryAttach(scene);

        private static void TryAttach(Scene scene)
        {
            try
            {
                if (!scene.IsValid()) return;
                // Town bird's-eye is a village concept — only arm in village scenes.
                if (scene.name == null ||
                    scene.name.IndexOf("Village", System.StringComparison.OrdinalIgnoreCase) < 0)
                    return;

                // Prefer the camera that owns the validated SmartMobileCamera rig.
                SmartMobileCamera smc = null;
                var rigs = Object.FindObjectsByType<SmartMobileCamera>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (rigs != null && rigs.Length > 0) smc = rigs[0];

                Camera cam = smc != null ? smc.GetComponent<Camera>() : Camera.main;
                if (cam == null) return;

                // Idempotent — never a second controller on the same camera.
                if (cam.GetComponent<CameraModeController>() == null)
                    cam.gameObject.AddComponent<CameraModeController>();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[CameraModeControllerBootstrap] attach failed: " + e.Message);
            }
        }
    }
}
