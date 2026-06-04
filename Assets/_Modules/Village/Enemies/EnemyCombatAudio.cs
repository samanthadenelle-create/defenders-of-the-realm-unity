// =============================================================================
// EnemyCombatAudio (WO-220) — fallback combat SFX for enemy hit / death.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// RECONCILE PASS, not a new audio system. Enemy.cs already plays per-type hit /
// death clips from its EnemyTypeVfxSet through the enemy's own AudioSource. But
// when no type-set is assigned (or its clip array is empty) the enemy is SILENT
// on hit and on death. This bridge fills only that gap: a procedurally-generated,
// fresh-clone-safe fallback clip played through the EXISTING audio surface
// (CoreServices.Audio.PlaySfx) — exactly the pattern AbilityAudioBridge /
// ProceduralSfx already use for hero ability casts. No SfxId is reachable from
// DeNelle.Village (it lives in DeNelle.Audio, which Village does not reference),
// so the AudioClip overload of IAudioService is the correct seam.
//
// OWNERSHIP OF THE IMPACT SOUND: the ENEMY owns the hit-impact sound. The hero's
// PlayerAttackController plays only the swing WHOOSH (pre-contact) + the perfect-
// hit sting; it deliberately does NOT play an on-contact impact clip, so the
// impact is never double-played. Enemy.TakeDamageFrom is the single owner.
//
// All clips are generated once and cached (static) — no per-hit allocation, no
// binary audio assets, no scene wiring.
// =============================================================================

using DeNelle.Core;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Generated fallback hit / death SFX for enemies that have no
    /// <see cref="EnemyTypeVfxSet"/> clip assigned. Routed through
    /// <see cref="CoreServices.Audio"/> (null-guarded). WO-220.
    /// </summary>
    internal static class EnemyCombatAudio
    {
        private const int Rate = 44100;

        private static AudioClip s_hitClip;
        private static AudioClip s_deathClip;

        /// <summary>
        /// Plays the generic enemy hit "thwack" through CoreServices.Audio.
        /// No-op when the audio service is not yet registered. Call this only as a
        /// FALLBACK — when the enemy's type-set provided no hit clip.
        /// </summary>
        public static void PlayHit()
        {
            // Prefer an authored CC0 clip if one is dropped in (drop-in upgrade
            // path, same convention as ProceduralSfx); otherwise generate.
            if (s_hitClip == null)
                s_hitClip = Resources.Load<AudioClip>("Sfx/EnemyHit") ?? GenerateHit();
            CoreServices.Audio?.PlaySfx(s_hitClip, 0.5f);
        }

        /// <summary>
        /// Plays the generic enemy death "pop / collapse" through CoreServices.Audio.
        /// No-op when the audio service is not yet registered. Call this only as a
        /// FALLBACK — when the enemy's type-set provided no death clip.
        /// </summary>
        public static void PlayDeath()
        {
            if (s_deathClip == null)
                s_deathClip = Resources.Load<AudioClip>("Sfx/EnemyDeath") ?? GenerateDeath();
            CoreServices.Audio?.PlaySfx(s_deathClip, 0.6f);
        }

        // ── Procedural generation (fresh-clone-safe) ─────────────────────────

        // A short noisy mid "thwack" — fast attack, quick exponential decay.
        private static AudioClip GenerateHit()
        {
            return Synth("sfx_enemy_hit", dur: 0.12f, f0: 520f, f1: 180f,
                         noise: 0.55f, amp: 0.6f, seed: 0x51735);
        }

        // A lower, longer "pop / collapse" — descending pitch, more body.
        private static AudioClip GenerateDeath()
        {
            return Synth("sfx_enemy_death", dur: 0.32f, f0: 300f, f1: 70f,
                         noise: 0.45f, amp: 0.7f, seed: 0xDEA7);
        }

        // Mirrors ProceduralSfx.Synth (kept local so this file is self-contained
        // and does not depend on AbilityAudioBridge's internal synth): a sine
        // sweep blended with noise, 6 ms attack, exponential decay, tail taper to
        // avoid clicks.
        private static AudioClip Synth(string name, float dur, float f0, float f1,
                                       float noise, float amp, int seed)
        {
            int n = Mathf.Max(16, (int)(dur * Rate));
            var data = new float[n];
            var rng = new System.Random(seed);
            double phase = 0;
            float attack = 0.006f * Rate;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float hz = Mathf.Lerp(f0, f1, t);
                phase += 2.0 * System.Math.PI * hz / Rate;
                float s = (float)System.Math.Sin(phase);
                float ns = (float)(rng.NextDouble() * 2.0 - 1.0);
                float v = Mathf.Lerp(s, ns, noise);
                float env = i < attack ? (i / attack) : Mathf.Exp(-3.5f * t);
                if (i > n - 64) env *= (n - i) / 64f;
                data[i] = v * env * amp;
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
