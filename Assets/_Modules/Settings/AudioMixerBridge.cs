// =============================================================================
// AudioMixerBridge — the slider -> AudioMixer volume seam (audit P0-8 / P0-9).
// -----------------------------------------------------------------------------
// The settings audio sliders must drive the project AudioMixer's exposed
// volume parameters. The Audio-system agent is building that mixer IN PARALLEL
// (see docs/port-notes/settings-pause.md "Audio-mixer seam") — so at the time
// this code is written the mixer asset may not exist yet.
//
// This bridge is therefore written to be SEAM-SAFE:
//   - It resolves the AudioMixer by Resources path ("Audio/GameAudioMixer") at
//     first use; if the asset is absent every call is a quiet no-op (one
//     warning, then silence — never an exception, never a hard dependency).
//   - It also accepts a directly-assigned AudioMixer (SettingsController has a
//     serialized field) — that path takes priority over the Resources lookup.
//   - Slider value -> decibels: a 0..1.5 linear slider maps to a logarithmic
//     dB attenuation, the standard Unity pattern (Mathf.Log10(v) * 20).
//
// EXPECTED EXPOSED PARAMETERS (the contract with the Audio agent's mixer):
//     "MasterVol" / "MusicVol" / "SfxVol"
// If the Audio agent ships different names, update the constants below — they
// are the single point of coupling.
//
// Lives in DeNelle.Settings; references only UnityEngine.Audio + DeNelle.Core.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace DeNelle.Settings
{
    /// <summary>
    /// Pushes 0..1.5 linear volume values onto the project <see cref="AudioMixer"/>'s
    /// exposed dB parameters. Resolves the mixer lazily and no-ops safely when the
    /// mixer asset is not present (the parallel Audio-system seam).
    /// </summary>
    public static class AudioMixerBridge
    {
        // ── Exposed-parameter names — the contract with the Audio agent's mixer ──
        /// <summary>Exposed mixer parameter for the Master group volume (dB).</summary>
        public const string MasterParam = "MasterVol";
        /// <summary>Exposed mixer parameter for the Music group volume (dB).</summary>
        public const string MusicParam = "MusicVol";
        /// <summary>Exposed mixer parameter for the SFX group volume (dB).</summary>
        public const string SfxParam = "SfxVol";

        /// <summary>
        /// Resources-relative path the mixer is looked up at when no mixer is
        /// assigned directly. The Audio agent should place the mixer asset at
        /// <c>Assets/.../Resources/Audio/GameAudioMixer.mixer</c> for this to resolve.
        /// </summary>
        public const string MixerResourcePath = "Audio/GameAudioMixer";

        /// <summary>The slider's maximum — 1.5x lets a player push above unity gain (audio-mix-spec §2).</summary>
        public const float MaxLinear = 1.5f;

        // The lowest dB a slider can reach before we treat it as full mute. Unity
        // mixers clamp around -80 dB; -80 is effectively silent.
        private const float MinDb = -80f;

        // ── Lazy mixer resolution ─────────────────────────────────────────────
        private static AudioMixer _mixer;
        private static bool _resolveAttempted;
        private static bool _warnedMissing;
        // Tracks params already warned about so each missing param warns only once.
        private static readonly HashSet<string> _warnedParams = new HashSet<string>();

        /// <summary>
        /// True once a mixer has been resolved (assigned or found in Resources).
        /// Settings UI can read this to flag the seam in a tooltip if desired.
        /// </summary>
        public static bool HasMixer => ResolveMixer() != null;

        /// <summary>
        /// Directly assigns the project mixer — used by <see cref="SettingsController"/>
        /// when it has a serialized AudioMixer reference. Takes priority over the
        /// Resources lookup. Passing null re-enables the lazy Resources path.
        /// </summary>
        public static void SetMixer(AudioMixer mixer)
        {
            _mixer = mixer;
            _resolveAttempted = mixer != null;
            if (mixer != null) _warnedMissing = false;
        }

        // =====================================================================
        //  Public API — push a linear 0..MaxLinear volume onto a mixer group
        // =====================================================================

        /// <summary>Sets the Master group volume from a 0..1.5 linear slider value.</summary>
        public static void SetMaster(float linear) => SetGroup(MasterParam, linear);

        /// <summary>Sets the Music group volume from a 0..1.5 linear slider value.</summary>
        public static void SetMusic(float linear) => SetGroup(MusicParam, linear);

        /// <summary>Sets the SFX group volume from a 0..1.5 linear slider value.</summary>
        public static void SetSfx(float linear) => SetGroup(SfxParam, linear);

        /// <summary>
        /// Sets one exposed mixer parameter from a linear 0..<see cref="MaxLinear"/>
        /// value. Converts to decibels and calls <see cref="AudioMixer.SetFloat"/>.
        /// A no-op (no exception) when the mixer asset is absent — the parallel
        /// Audio-system seam — or when the parameter is not exposed on the mixer.
        /// </summary>
        public static void SetGroup(string exposedParam, float linear)
        {
            var mixer = ResolveMixer();
            if (mixer == null) return; // seam: mixer not built yet — quiet no-op.

            float db = LinearToDecibels(linear);
            if (!mixer.SetFloat(exposedParam, db))
            {
                // The mixer exists but does not expose this parameter — likely a
                // name drift between this bridge and the Audio agent's mixer.
                // Warn only once per parameter name to avoid log spam on every call.
                if (_warnedParams.Add(exposedParam))
                {
                    Debug.LogWarning(
                        $"[AudioMixerBridge] Mixer has no exposed parameter '{exposedParam}'. " +
                        "Check the parameter names against the Audio-system mixer " +
                        "(see docs/port-notes/settings-pause.md). Further calls silenced.");
                }
            }
        }

        // =====================================================================
        //  Linear <-> decibel conversion
        // =====================================================================

        /// <summary>
        /// Converts a 0..<see cref="MaxLinear"/> linear volume to decibels for an
        /// AudioMixer. 0 -> full mute (<see cref="MinDb"/>); 1.0 -> 0 dB (unity
        /// gain); 1.5 -> ~+3.5 dB. The logarithmic curve matches how loudness is
        /// perceived — a linear slider feels right driven through this.
        /// </summary>
        public static float LinearToDecibels(float linear)
        {
            float clamped = Mathf.Clamp(linear, 0f, MaxLinear);
            if (clamped <= 0.0001f) return MinDb;
            return Mathf.Log10(clamped) * 20f;
        }

        /// <summary>Inverse of <see cref="LinearToDecibels"/> — dB back to a 0..1.5 linear value.</summary>
        public static float DecibelsToLinear(float db)
        {
            if (db <= MinDb) return 0f;
            return Mathf.Clamp(Mathf.Pow(10f, db / 20f), 0f, MaxLinear);
        }

        // ── Internals ─────────────────────────────────────────────────────────

        /// <summary>
        /// Resolves the mixer once: returns an assigned mixer if present, else
        /// tries the Resources lookup. Caches the result (success or failure) and
        /// warns exactly once if the asset is missing.
        /// </summary>
        private static AudioMixer ResolveMixer()
        {
            if (_mixer != null) return _mixer;
            if (_resolveAttempted) return _mixer;

            _resolveAttempted = true;
            _mixer = Resources.Load<AudioMixer>(MixerResourcePath);

            if (_mixer == null && !_warnedMissing)
            {
                _warnedMissing = true;
                Debug.LogWarning(
                    $"[AudioMixerBridge] No AudioMixer at Resources/'{MixerResourcePath}' and " +
                    "none assigned. Volume sliders will persist but not affect audio until the " +
                    "Audio-system mixer is wired (parallel work — see " +
                    "docs/port-notes/settings-pause.md). This is expected pre-audio-integration.");
            }

            return _mixer;
        }

        /// <summary>
        /// Clears the cached mixer + resolution flags. Test/editor hook so a
        /// domain reload or a late-arriving mixer asset is picked up cleanly.
        /// </summary>
        public static void ResetCache()
        {
            _mixer = null;
            _resolveAttempted = false;
            _warnedMissing = false;
            _warnedParams.Clear();
        }
    }
}
