// =============================================================================
// AbilityAudioBridge — plays a per-ability SFX through DeNelle.Audio.AudioService
// without an asmdef reference (DeNelle.Village can't reference DeNelle.Audio), via
// the project's reflection-bridge pattern. No-ops safely if Audio is absent.
// -----------------------------------------------------------------------------
// Clips are GENERATED procedurally in code (no binary audio assets — fresh-clone
// safe), cached per effect kind. If an authored CC0 clip exists at
// Resources/Sfx/<Kind>.wav it's preferred (drop-in upgrade path). Owner WO-35.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DeNelle.Village
{
    public static class AbilityAudioBridge
    {
        private static bool s_resolved;
        private static PropertyInfo s_instanceProp;
        private static MethodInfo s_playSfx;

        public static void PlayForKind(AbilityEffect kind)
        {
            Resolve();
            if (s_instanceProp == null || s_playSfx == null) return;
            object inst = s_instanceProp.GetValue(null);
            if (inst == null) return;
            AudioClip clip = ProceduralSfx.ForKind(kind);
            if (clip == null) return;
            try { s_playSfx.Invoke(inst, new object[] { clip, VolumeFor(kind) }); }
            catch { /* audio is best-effort */ }
        }

        private static void Resolve()
        {
            if (s_resolved) return;
            s_resolved = true;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType("DeNelle.Audio.AudioService", false);
                if (t == null) continue;
                s_instanceProp = t.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                s_playSfx = t.GetMethod("PlaySfx", new[] { typeof(AudioClip), typeof(float) });
                break;
            }
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
    /// authored Resources/Sfx/&lt;Kind&gt; clip if present.</summary>
    internal static class ProceduralSfx
    {
        private const int Rate = 44100;
        private static readonly Dictionary<AbilityEffect, AudioClip> s_cache =
            new Dictionary<AbilityEffect, AudioClip>();

        public static AudioClip ForKind(AbilityEffect kind)
        {
            if (s_cache.TryGetValue(kind, out var cached) && cached != null) return cached;
            // TODO(sfx): drop a CC0 wav at Resources/Sfx/<Kind> to override the generated clip.
            AudioClip clip = Resources.Load<AudioClip>("Sfx/" + kind) ?? Generate(kind);
            s_cache[kind] = clip;
            return clip;
        }

        private static AudioClip Generate(AbilityEffect kind)
        {
            float dur, f0, f1, noise, amp;
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

            int n = Mathf.Max(16, (int)(dur * Rate));
            var data = new float[n];
            var rng = new System.Random(kind.GetHashCode());
            double phase = 0;
            float attack = 0.006f * Rate;             // 6 ms attack (no click)
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float hz = Mathf.Lerp(f0, f1, t);
                phase += 2.0 * Math.PI * hz / Rate;
                float s = (float)Math.Sin(phase);
                float ns = (float)(rng.NextDouble() * 2.0 - 1.0);
                float v = Mathf.Lerp(s, ns, noise);
                float env = i < attack ? (i / attack) : Mathf.Exp(-3.5f * t);  // attack then exp decay
                if (i > n - 64) env *= (n - i) / 64f;  // taper tail to silence (no click)
                data[i] = v * env * amp;
            }

            var clip = AudioClip.Create("sfx_" + kind, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
