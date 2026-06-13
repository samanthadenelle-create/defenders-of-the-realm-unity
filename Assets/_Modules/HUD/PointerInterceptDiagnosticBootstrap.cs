// =============================================================================
// PointerInterceptDiagnosticBootstrap — guarantees one PointerInterceptDiagnostic
// exists at all times (trace-only; self-gates to "overlay open"). Same
// RuntimeInitializeOnLoadMethod pattern as HelpMenuBootstrap.
// -----------------------------------------------------------------------------
// Part of the "Dev tools dead after a Yarn dialogue" probe (2026-06-13): keeps the
// pointer-hit dump alive across the additive OuterWorld load + scene swaps so the
// failing click after a Yarn dialogue is always captured.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.HUD
{
    public static class PointerInterceptDiagnosticBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void EnsureFirst()
        {
            EnsureExists();
            SceneManager.sceneLoaded -= OnSceneLoaded; // idempotent
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureExists();
        }

        private static void EnsureExists()
        {
            // GLOBAL dedupe (across ALL loaded scenes) — a second copy would just
            // double the dump lines, never change behaviour. One is enough.
            if (Object.FindFirstObjectByType<PointerInterceptDiagnostic>(FindObjectsInactive.Include) != null)
                return;

            var go = new GameObject("PointerInterceptDiagnostic");
            // Persist across scene swaps so the probe is never lost mid-investigation.
            Object.DontDestroyOnLoad(go);
            go.AddComponent<PointerInterceptDiagnostic>();
        }
    }
}
