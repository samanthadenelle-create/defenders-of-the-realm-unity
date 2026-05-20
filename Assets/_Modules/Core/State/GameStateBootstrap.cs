// =============================================================================
// GameStateBootstrap — auto-spawns the GameStateService singleton at startup.
// -----------------------------------------------------------------------------
// Matches the AudioBootstrap / SeekerBootstrap pattern. Without this, the
// onboarding controllers (HeroSelectController / PetSelectController) hit
// GameStateService.Instance == null and log "hero choice was NOT persisted",
// the Onboarded flag never reads back, and the cold open plays every launch.
// =============================================================================

using UnityEngine;

namespace DeNelle.Core.State
{
    public static class GameStateBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void EnsureGameStateService()
        {
            if (GameStateService.Instance != null) return;
            var go = new GameObject("GameStateService");
            go.AddComponent<GameStateService>();
            // GameStateService.Awake claims the singleton, calls Load() from
            // PlayerPrefs, and DontDestroyOnLoad's the GameObject.
        }
    }
}
