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
// DEF-22 FIX: AudioService (DeNelle.Audio) and SettingsModel (DeNelle.Settings)
// cannot reference each other — both only reference DeNelle.Core. The mute/
// volume toggle must reach AudioService so that the no-mixer fallback path
// (source.mute) and AudioService.IsMuted stay in sync with SettingsModel.
// Solution: this bootstrap subscribes to GameStateService.SettingsChanged and
// reflectively calls AudioService.Instance.ApplyPersistedSettings() whenever
// any setting changes — matching the project's reflection-bridge pattern for
// cross-module calls. PlayerPrefs persistence for the sound toggle is handled
// by SettingsModel.Muted → GameStateService.SetMuted (already persists to
// the canonical dotr-save blob via PlayerPrefs, surviving relaunches).
//
// Lives in DeNelle.Settings; references DeNelle.Core only.
// =============================================================================

using System.Reflection;
using UnityEngine;
using DeNelle.Core.State;

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

        // Reflection cache for the cross-module AudioService.ApplyPersistedSettings() call.
        // DEF-22: DeNelle.Settings cannot reference DeNelle.Audio directly; we
        // bridge via reflection — the same pattern HeroTalentPanel / AdminOverlay use.
        private static System.Type _audioServiceType;
        private static PropertyInfo _audioInstanceProp;
        private static MethodInfo _applyPersistedMethod;
        private static bool _audioReflectionResolved;

        /// <summary>
        /// Auto-invoked by Unity after the first scene loads. Re-applies every
        /// persisted setting through <see cref="SettingsModel.ApplyAll"/> so the
        /// player's last choices are in force from the first frame of play.
        /// Also wires <see cref="OnSettingsChanged"/> to
        /// <see cref="GameStateService.SettingsChanged"/> so live changes from
        /// the Settings screen are forwarded to AudioService (DEF-22).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Init()
        {
            if (HasRun) return;
            HasRun = true;

            SettingsModel.ApplyAll();
            NotifyAudioService(); // sync AudioService.IsMuted / per-source mute on boot.

            // Subscribe so future live changes (mute toggle, volume sliders) are
            // immediately forwarded to AudioService without any extra wiring.
            var gss = GameStateService.Instance;
            if (gss != null)
                gss.SettingsChanged.AddListener(OnSettingsChanged);

            Debug.Log(
                $"[SettingsBootstrap] Settings re-applied — master={SettingsModel.MasterVolume:0.00}, " +
                $"music={SettingsModel.MusicVolume:0.00}, sfx={SettingsModel.SfxVolume:0.00}, " +
                $"muted={SettingsModel.Muted}, quality={SettingsModel.Quality}, " +
                $"screenShake={SettingsModel.ScreenShake}.");
        }

        /// <summary>
        /// Called whenever <see cref="GameStateService.SettingsChanged"/> fires —
        /// forwards the new mute/volume state to AudioService (DEF-22 fix).
        /// </summary>
        private static void OnSettingsChanged()
        {
            NotifyAudioService();
        }

        /// <summary>
        /// Calls <c>AudioService.Instance.ApplyPersistedSettings()</c> via
        /// reflection so DeNelle.Settings does not need to reference DeNelle.Audio.
        /// Resolves the type lazily; silently skips if the service is not yet alive.
        /// </summary>
        private static void NotifyAudioService()
        {
            if (!_audioReflectionResolved)
            {
                _audioReflectionResolved = true;
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    _audioServiceType = asm.GetType("DeNelle.Audio.AudioService", false);
                    if (_audioServiceType != null) break;
                }
                if (_audioServiceType != null)
                {
                    _audioInstanceProp = _audioServiceType.GetProperty(
                        "Instance", BindingFlags.Public | BindingFlags.Static);
                    _applyPersistedMethod = _audioServiceType.GetMethod(
                        "ApplyPersistedSettings", BindingFlags.Public | BindingFlags.Instance);
                }
            }

            if (_audioInstanceProp == null || _applyPersistedMethod == null) return;
            try
            {
                var instance = _audioInstanceProp.GetValue(null);
                if (instance != null)
                    _applyPersistedMethod.Invoke(instance, null);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[SettingsBootstrap] AudioService.ApplyPersistedSettings() failed: " + ex.Message);
            }
        }
    }
}
