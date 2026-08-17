// =============================================================================
// VillageAudioResources (WO-571) — Resources-by-id clip + mixer-group resolution
// for the DeNelle.Village audio controllers.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHY THIS EXISTS (WO-571 "audio content pass"):
//   Several Village audio controllers (TowerVoiceController, HeartwoodAmbient
//   Controller) carried [SerializeField] AudioClip fields that were NEVER assigned
//   — canon BANS inspector drag-drop authoring (memory never-dragdrop) and no
//   prefab carries them, so every one shipped NULL → silent. This helper is the
//   data-driven, NO-drag-drop seam: a controller resolves its clips by a CONVENTION
//   Resources PATH (so dropping a correctly-named clip "just works", the same
//   pattern AudioBootstrap / GameSfx / BattleMusicManager already use) and routes
//   its AudioSources through the SHARED AudioMixer groups so the player's
//   master/music/sfx/voice volume + mute actually apply.
//
// CONVENTION PATHS (drop a clip at any of these and it plays — no code change):
//   • Heartwood ambient beds : Resources/Audio/Ambient/Heartwood_Healthy
//                              Resources/Audio/Ambient/Heartwood_Strained
//                              Resources/Audio/Ambient/Heartwood_Critical
//   • Heartwood stingers      : Resources/Audio/Sfx/Heart_Hit
//                              Resources/Audio/Sfx/Heart_Fall
//   • Tower/Heart voice lines : Resources/Audio/Voice/HeartFailing(_1/_2/_3)
//   (full list lives in docs/AUDIO/AUDIO_CLIP_MANIFEST.md)
//
// WebGL-safe: no File I/O. Clip resolution goes through DeNelle.Core.AudioAssetLoader
// (Addressables-first, Resources-fallback) so the convention keys above keep working
// unchanged once AudioAddressablesGrouper moves the audio out of Resources; every call
// is try/catch-guarded so resolution never throws out into a controller's lifecycle.
// The MIXER lookup below stays on Resources.Load deliberately — GameAudioMixer.mixer is
// a documented KEEP-BEHIND (1.8 KB, three seam-less call sites).
// =============================================================================

using UnityEngine;
using UnityEngine.Audio;
using DeNelle.Audio;   // AudioBootstrap.MixerResourcePath (Village references DeNelle.Audio)

namespace DeNelle.Village
{
    /// <summary>
    /// Loads audio clips by a convention Resources path and resolves the shared
    /// AudioMixer groups for the Village audio controllers (WO-571). No drag-drop,
    /// no File I/O — drop a correctly-named clip under a Resources/ folder and it
    /// "just works".
    /// </summary>
    internal static class VillageAudioResources
    {
        // The one shared mixer (the SAME asset AudioBootstrap / BattleMusicManager
        // / AudioService use), resolved once.
        private static AudioMixer s_mixer;
        private static bool s_mixerResolved;

        private static AudioMixer Mixer()
        {
            if (!s_mixerResolved)
            {
                s_mixerResolved = true;
                try { s_mixer = Resources.Load<AudioMixer>(AudioBootstrap.MixerResourcePath); }
                catch { s_mixer = null; }
            }
            return s_mixer;
        }

        /// <summary>
        /// The shared mixer group by name (e.g. "Music", "SFX", "Voice"), or null
        /// when the mixer is absent / the group is not found (caller routes to the
        /// default output — still audible).
        /// </summary>
        public static AudioMixerGroup Group(string groupName)
        {
            var mixer = Mixer();
            if (mixer == null || string.IsNullOrEmpty(groupName)) return null;
            try
            {
                var groups = mixer.FindMatchingGroups(groupName);
                return (groups != null && groups.Length > 0) ? groups[0] : null;
            }
            catch { return null; }
        }

        /// <summary>
        /// Load an AudioClip by its audio key (extension-less, no "Resources/" prefix)
        /// through <see cref="DeNelle.Core.AudioAssetLoader"/> — Addressables-first,
        /// Resources-fallback, so the same key keeps working after the audio migrates
        /// out of Resources. Null when absent. WebGL-safe (never throws).
        /// </summary>
        public static AudioClip Load(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath)) return null;
            // optional:true — THIS METHOD'S CONTRACT IS "Null when absent" (see summary), and every
            // caller is written to that contract: HeartwoodAmbientController guards each bed with
            // `if (_x == null) _x = Load(...)`, TowerVoiceController null-checks before queueing,
            // and LoadFirst below EXPECTS misses by design — it walks candidates until one resolves,
            // so on a 4-path list three misses are the normal case, not three errors.
            // Reporting these at Fail put NINE error-level lines in the F8 queue on 2026-08-17
            // (Heartwood_Healthy/_Strained/_Critical, Heart_Hit, Heart_Fall, HeartFailing x4) for
            // ambient and voice content that was simply never authored. Still reported once per
            // key, just at Warn — the level now matches the contract.
            try { return DeNelle.Core.AudioAssetLoader.LoadClip(resourcePath, optional: true); }
            catch { return null; }
        }

        /// <summary>The first clip that resolves from a list of candidate paths, or null.</summary>
        public static AudioClip LoadFirst(params string[] paths)
        {
            if (paths == null) return null;
            foreach (string p in paths)
            {
                AudioClip c = Load(p);
                if (c != null) return c;
            }
            return null;
        }
    }
}
