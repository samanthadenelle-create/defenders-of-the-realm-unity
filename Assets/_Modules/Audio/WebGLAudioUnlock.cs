// =============================================================================
// WebGLAudioUnlock — mobile-browser audio gesture unlock.
// -----------------------------------------------------------------------------
// iOS Safari and mobile Chrome start the Web Audio AudioContext SUSPENDED until
// the first user gesture (autoplay policy), so any music started at scene-load is
// silent on mobile (owner 2026-06-02: "dont hear any audio on mobile"). This
// polls for the first tap / click / key, then asks the AudioService to un-pause
// the listener and re-assert the current track so it actually sounds — then it
// removes itself.
//
// Self-bootstrapping DDOL (same pattern as AudioBootstrap). Harmless off-WebGL:
// the first input just re-affirms whatever is already playing.
// =============================================================================

using UnityEngine;

namespace DeNelle.Audio
{
    /// <summary>Re-asserts music on the first user gesture so mobile WebGL audio plays.</summary>
    public sealed class WebGLAudioUnlock : MonoBehaviour
    {
        private static bool s_spawned;
        private bool _unlocked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (s_spawned) return;
            s_spawned = true;
            var go = new GameObject("WebGLAudioUnlock");
            DontDestroyOnLoad(go);
            go.AddComponent<WebGLAudioUnlock>();
        }

        private void Update()
        {
            if (_unlocked) return;
            if (!FirstGestureThisFrame()) return;
            _unlocked = true;
            AudioService.Instance?.ResumeAfterUnlock();
            Destroy(gameObject);
        }

        private static bool FirstGestureThisFrame()
        {
            if (Input.touchCount > 0) return true;
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) return true;
            if (Input.anyKeyDown) return true;
            return false;
        }
    }
}
