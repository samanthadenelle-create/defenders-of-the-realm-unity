// =============================================================================
// SettingsBootstrap — reapplies persisted settings at launch (audit P0-8).
// -----------------------------------------------------------------------------
// The audit deliverable: "Settings PERSIST ... and reapply on launch." The
// persistence is in SettingsModel (PlayerPrefs + GameState); this is the piece
// that REAPPLIES them when the game starts, with no scene wiring.
//
// It runs via [RuntimeInitializeOnLoadMethod] — the same auto-run mechanism
// SeekerBootstrap uses — at AfterSceneLoad (NOT BeforeSceneLoad):
//   * SeekerBootstrap runs at BeforeSceneLoad and picks a quality tier from the
//     hardware. This must run AFTER it, so a player's explicit tier choice
//     overrides the auto-detected default.
//   * The audio apply needs GameStateService (for music/sfx/mute) to exist;
//     that singleton is created in its own Awake during the first scene load,
//     so AfterSceneLoad is the first safe point.
//
// If the player has never opened Settings, SettingsModel returns fresh
// defaults, so this still produces a correct, consistent initial state.
//
// Lives in DeNelle.Settings; references DeNelle.Core only.
// =============================================================================

using UnityEngine;

namespace DeNelle.Settings
{
    /// <summary>
    /// Auto-runs once at startup to re-apply persisted player settings (audio,
    /// quality tier, screen-shake). No scene presence — see <see cref="Init"/>.
    /// </summary>
    public static class SettingsBootstrap
    {
        /// <summary>True once <see cref="Init"/> has run for this session.</summary>
        public static bool HasRun { get; private set; }

        /// <summary>
        /// Auto-invoked by Unity after the first scene loads. Re-applies every
        /// persisted setting through <see cref="SettingsModel.ApplyAll"/> so the
        /// player's last choices are in force from the first frame of play.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Init()
        {
            if (HasRun) return;
            HasRun = true;

            SettingsModel.ApplyAll();

            Debug.Log(
                $"[SettingsBootstrap] Settings re-applied — master={SettingsModel.MasterVolume:0.00}, " +
                $"music={SettingsModel.MusicVolume:0.00}, sfx={SettingsModel.SfxVolume:0.00}, " +
                $"muted={SettingsModel.Muted}, quality={SettingsModel.Quality}, " +
                $"screenShake={SettingsModel.ScreenShake}.");
        }
    }
}
