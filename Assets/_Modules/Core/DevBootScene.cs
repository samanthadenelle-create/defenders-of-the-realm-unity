// =============================================================================
// DevBootScene — QA/dev hook: launch the player straight into a scene, skipping
// the Title -> HeroSelect -> PetSelect onboarding flow.
//
//   DefendersOfTheRealm.exe -bootScene Village
//
// Enables autonomous + manual build-side verification of in-world scenes (the
// HUD, village art, gates, dungeon entrances, ATB) without having to click
// through the intro every time. ARG-GATED: with no -bootScene argument this is a
// no-op, so it has zero effect on a normal player session. Fires once at startup
// via RuntimeInitializeOnLoadMethod(AfterSceneLoad) — no scene wiring needed, so
// it works in a built player.
//
// NOTE: booting a gameplay scene directly skips the onboarding state setup
// (hero/pet selection). GameStateService falls back to its defaults (Mage +
// starter pets), which is fine for visual/QA smoke tests. If you want this
// stripped from public release builds, gate the body on Debug.isDebugBuild.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Core
{
    public static class DevBootScene
    {
        private const string Flag = "-bootScene";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string target = ParseTarget();
            if (string.IsNullOrEmpty(target)) return; // normal launch — no-op

            if (SceneManager.GetActiveScene().name == target) return; // already there
            if (!Application.CanStreamedLevelBeLoaded(target))
            {
                Debug.LogWarning($"[DevBootScene] -bootScene '{target}' is not in Build Settings — ignoring.");
                return;
            }

            Debug.Log($"[DevBootScene] -bootScene '{target}' — loading directly, skipping the onboarding flow.");
            SceneManager.LoadScene(target);
        }

        private static string ParseTarget()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], Flag, System.StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }
    }
}
