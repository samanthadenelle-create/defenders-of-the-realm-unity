// =============================================================================
// AbilityAudioBridge — plays per-ability SFX and music through CoreServices.Audio
// (WO-41 refactor: reflection removed, uses IAudioService / IAudioService).
// Clips are GENERATED procedurally in code (no binary audio assets —
// fresh-clone safe), cached per effect kind. If an authored CC0 clip exists at
// Resources/Sfx/<Kind>.wav it's preferred (drop-in upgrade path). Owner WO-35.
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core;
using UnityEngine;

namespace DeNelle.Village
{
    public static class AbilityAudioBridge
    {
        public static void PlayForKind(AbilityEffect kind)
        {
            AudioClip clip = ProceduralSfx.ForKind(kind);
            if (clip == null) return;
            CoreServices.Audio?.PlaySfx(clip, VolumeFor(kind));
        }

        /// <summary>
        /// WO-37: plays a class-flavoured variant of the effect SFX (Knight heavier/
        /// grittier, Ranger brighter/tighter, Mage = the base clip). Falls back to
        /// the per-kind clip for unknown classes.
        /// </summary>
        public static void PlayForClassAndKind(string heroClass, AbilityEffect kind)
        {
            AudioClip clip = ProceduralSfx.ForClassAndKind(heroClass, kind);
            if (clip == null) return;
            CoreServices.Audio?.PlaySfx(clip, VolumeFor(kind));
        }

        /// <summary>
        /// Plays a music track by CoreServices.Audio.PlayMusic. Maps the track name
        /// string to the DeNelle.Core.Audio.MusicTrack enum. No-ops if Audio is
        /// absent or the track name is not recognised. WO-38.
        /// </summary>
        public static void PlayMusic(string trackName)
        {
            if (CoreServices.Audio == null) return;
            switch (trackName?.ToLowerInvariant())
            {
                case "village": CoreServices.Audio.PlayMusic(DeNelle.Core.Audio.MusicTrack.Village); break;
                case "battle":  CoreServices.Audio.PlayMusic(DeNelle.Core.Audio.MusicTrack.Battle);  break;
                case "victory": CoreServices.Audio.PlayMusic(DeNelle.Core.Audio.MusicTrack.Victory); break;
                case "dungeon": CoreServices.Audio.PlayMusic(DeNelle.Core.Audio.MusicTrack.Dungeon); break;
                // unrecognised track names are silent no-ops
            }
        }

        /// <summary>Plays a short tense alarm sting (wave imminent). WO-40.</summary>
        public static void PlayDangerSting()
        {
            AudioClip clip = ProceduralSfx.DangerSting();
            if (clip == null) return;
            CoreServices.Audio?.PlaySfx(clip, 0.6f);
        }

        private static float VolumeFor(AbilityEffect k)
        {
            switch (k)
            {
                case AbilityEffect.Strike: return 0.4f;
                case AbilityEffect.Snare:  return 0.5f;
                case AbilityEffect.Aoe:    return 0.7f;
                case AbilityEffect.Cleave: return 0.7f;
                case AbilityEffect.Heal:   return 0.45f;
                case AbilityEffect.Meteor: return 0.9f;
                default:                   return 0.5f;
            }
        }
    }

    /// <summary>Generates short, click-free ability SFX in code (cached). Prefers an
    /// authored clip at the audio key "Sfx/&lt;Kind&gt;" if present, resolved through
    /// DeNelle.Core.AudioAssetLoader (Addressables-first, Resources-fallback).</summary>
    internal static class ProceduralSfx
    {
        private const int Rate = 44100;
        private static readonly Dictionary<AbilityEffect, AudioClip> s_cache =
            new Dictionary<AbilityEffect, AudioClip>();

        public static AudioClip ForKind(AbilityEffect kind)
        {
            if (s_cache.TryGetValue(kind, out var cached) && cached != null) return cached;
            // TODO(sfx): drop a CC0 wav at the audio key "Sfx/<Kind>" to override the generated clip
            // (AudioAssetLoader resolves it from Addressables first, then Resources).
            AudioClip clip = DeNelle.Core.AudioAssetLoader.LoadClip("Sfx/" + kind, optional: true) ?? Generate(kind);
            s_cache[kind] = clip;
            return clip;
        }

        private static AudioClip s_dangerSting;

        /// <summary>A short two-tone descending alarm sting for the wave-imminent alert (WO-40).</summary>
        public static AudioClip DangerSting()
        {
            if (s_dangerSting != null) return s_dangerSting;
            int n = Mathf.Max(16, (int)(0.45f * Rate));
            var data = new float[n];
            float attack = 0.006f * Rate;
            double phase = 0;
            int half = Rate / 12;   // ~12 Hz on/off pulse rate
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float baseHz = (i % (half * 2) < half) ? 330f : 247f;   // alternating alarm tones
                float hz = baseHz * Mathf.Lerp(1f, 0.85f, t);            // slight descent
                phase += 2.0 * System.Math.PI * hz / Rate;
                float s = (float)System.Math.Sin(phase);
                float env = i < attack ? (i / attack) : Mathf.Exp(-2.2f * t);
                if (i > n - 64) env *= (n - i) / 64f;
                data[i] = s * env * 0.7f;
            }
            var clip = AudioClip.Create("sfx_danger", n, 1, Rate, false);
            clip.SetData(data, 0);
            s_dangerSting = clip;
            return clip;
        }

        private static readonly Dictionary<string, AudioClip> s_classCache =
            new Dictionary<string, AudioClip>();

        /// <summary>
        /// WO-37: class-flavoured SFX. Knight = lower + grittier (heavier, metallic),
        /// Ranger = brighter + cleaner (tighter snap); Mage / unknown classes get the
        /// base per-kind clip unchanged. Cached per class+kind.
        /// </summary>
        public static AudioClip ForClassAndKind(string heroClass, AbilityEffect kind)
        {
            string c = (heroClass ?? string.Empty).ToLowerInvariant();
            if (c != "knight" && c != "ranger") return ForKind(kind);   // mage / default = base clip
            string key = c + ":" + kind;
            if (s_classCache.TryGetValue(key, out var cached) && cached != null) return cached;
            Params(kind, out float dur, out float f0, out float f1, out float noise, out float amp);
            if (c == "knight") { f0 *= 0.7f; f1 *= 0.7f; noise = Mathf.Clamp01(noise + 0.18f); dur *= 1.1f; }
            else               { f0 *= 1.25f; f1 *= 1.25f; noise = Mathf.Clamp01(noise * 0.6f); }   // ranger
            var clip = Synth("sfx_" + c + "_" + kind, dur, f0, f1, noise, amp, key.GetHashCode());
            s_classCache[key] = clip;
            return clip;
        }

        private static void Params(AbilityEffect kind, out float dur, out float f0, out float f1,
                                   out float noise, out float amp)
        {
            switch (kind)
            {
                case AbilityEffect.Strike: dur = 0.12f; f0 = 1200; f1 = 500;  noise = 0.10f; amp = 0.5f; break;  // zippy pew
                case AbilityEffect.Snare:  dur = 0.20f; f0 = 1500; f1 = 400;  noise = 0.15f; amp = 0.5f; break;  // cold clink
                case AbilityEffect.Aoe:    dur = 0.40f; f0 = 220;  f1 = 1800; noise = 0.30f; amp = 0.6f; break;  // whoomph + shimmer
                case AbilityEffect.Cleave: dur = 0.25f; f0 = 170;  f1 = 90;   noise = 0.40f; amp = 0.7f; break;  // heavy thunk
                case AbilityEffect.Heal:   dur = 0.80f; f0 = 523;  f1 = 784;  noise = 0.00f; amp = 0.45f; break; // warm rising chime
                case AbilityEffect.Meteor: dur = 0.55f; f0 = 420;  f1 = 70;   noise = 0.50f; amp = 0.8f; break;  // descending roar/boom
                default:                   dur = 0.20f; f0 = 600;  f1 = 400;  noise = 0.20f; amp = 0.5f; break;
            }
        }

        private static AudioClip Synth(string name, float dur, float f0, float f1, float noise, float amp, int seed)
        {
            int n = Mathf.Max(16, (int)(dur * Rate));
            var data = new float[n];
            var rng = new System.Random(seed);
            double phase = 0;
            float attack = 0.006f * Rate;             // 6 ms attack (no click)
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float hz = Mathf.Lerp(f0, f1, t);
                phase += 2.0 * System.Math.PI * hz / Rate;
                float s = (float)System.Math.Sin(phase);
                float ns = (float)(rng.NextDouble() * 2.0 - 1.0);
                float v = Mathf.Lerp(s, ns, noise);
                float env = i < attack ? (i / attack) : Mathf.Exp(-3.5f * t);  // attack then exp decay
                if (i > n - 64) env *= (n - i) / 64f;  // taper tail to silence (no click)
                data[i] = v * env * amp;
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip Generate(AbilityEffect kind)
        {
            Params(kind, out float dur, out float f0, out float f1, out float noise, out float amp);
            return Synth("sfx_" + kind, dur, f0, f1, noise, amp, kind.GetHashCode());
        }
    }
}
