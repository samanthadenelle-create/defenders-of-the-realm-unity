// =============================================================================
// GameSfx (DEF-183) — combat + world one-shot SFX for events that had none.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// DEF-183 "Sound Everything" pass — combat + UI polish. This is a RECONCILE,
// not a new audio system: every clip is played through the EXISTING audio
// surface (CoreServices.Audio?.PlaySfx) — the same pattern EnemyCombatAudio and
// AbilityAudioBridge/ProceduralSfx already use. Clips are GENERATED procedurally
// in code (fresh-clone-safe — no binary audio assets, no scene wiring) and
// cached statically, with an authored CC0 Resources/Sfx/<name> drop-in override
// path (matching the EnemyCombatAudio convention).
//
// Covers the gaps DEF-183 calls out that DeNelle.Village owns:
//   • Tower FIRE   — TowerCombat.FireAt (a short punchy "pew")
//   • Tower PLACE  — TowerPlacementSystem.PlaceTower (a wooden "thunk")
//   • Wave START   — WaveFeedbackDirector.OnWaveStarted (a low battle horn)
//
// Enemy hit/death (EnemyCombatAudio), hero ability cast (AbilityAudioBridge),
// the wave-imminent danger sting and wave-clear victory music already exist and
// are NOT duplicated here. UI button clicks are handled in DeNelle.HUD via the
// new IAudioService.PlayUiClick() seam (HUD references Core only).
//
// SfxId / SfxClipLibrary are NOT reachable from DeNelle.Village (they live in
// DeNelle.Audio, which Village does not reference), so the AudioClip overload of
// IAudioService is the correct seam — the same call EnemyCombatAudio makes.
// =============================================================================

using DeNelle.Core;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Generated one-shot combat/world SFX for events that previously played
    /// nothing (tower fire, tower place, wave start). Routed through
    /// <see cref="CoreServices.Audio"/> (null-guarded). DEF-183.
    /// </summary>
    internal static class GameSfx
    {
        private const int Rate = 44100;

        private static AudioClip s_towerFire;
        private static AudioClip s_towerPlace;
        private static AudioClip s_waveStart;
        private static AudioClip s_lookoutHorn;

        /// <summary>
        /// Plays the tower-fire "pew" through CoreServices.Audio. Quiet by design —
        /// many towers can fire at once, so this is mixed low to avoid a wall of
        /// sound. No-op when the audio service is not yet registered.
        /// </summary>
        public static void PlayTowerFire()
        {
            if (s_towerFire == null)
                s_towerFire = Resources.Load<AudioClip>("Sfx/TowerFire") ?? GenerateTowerFire();
            CoreServices.Audio?.PlaySfx(s_towerFire, 0.28f);
        }

        /// <summary>
        /// Plays the tower-place "thunk" through CoreServices.Audio — the satisfying
        /// confirm when a tower is committed to the build queue. No-op when the
        /// audio service is not yet registered.
        /// </summary>
        public static void PlayTowerPlace()
        {
            if (s_towerPlace == null)
                s_towerPlace = Resources.Load<AudioClip>("Sfx/TowerPlace") ?? GenerateTowerPlace();
            CoreServices.Audio?.PlaySfx(s_towerPlace, 0.7f);
        }

        /// <summary>
        /// Plays the wave-start battle horn through CoreServices.Audio — the "here
        /// they come" cue when a wave actually begins. Distinct from the
        /// wave-imminent danger sting (AbilityAudioBridge.PlayDangerSting), which
        /// fires during the countdown. No-op when the audio service is absent.
        /// </summary>
        public static void PlayWaveStart()
        {
            if (s_waveStart == null)
                s_waveStart = Resources.Load<AudioClip>("Sfx/WaveStart") ?? GenerateWaveStart();
            CoreServices.Audio?.PlaySfx(s_waveStart, 0.8f);
        }

        /// <summary>
        /// The lookout's horn — the "a raid is incoming" warning blown when a wave
        /// enters its countdown (and previewed by the FTUE's horn line). Real
        /// recorded clip at Resources/Sfx/LookoutHorn; no-op if absent or the audio
        /// service isn't up yet.
        /// </summary>
        public static void PlayLookoutHorn()
        {
            if (s_lookoutHorn == null)
                s_lookoutHorn = Resources.Load<AudioClip>("Sfx/LookoutHorn");
            if (s_lookoutHorn != null)
                CoreServices.Audio?.PlaySfx(s_lookoutHorn, 0.85f);
        }

        // ── Procedural generation (fresh-clone-safe) ─────────────────────────

        // A short, bright "pew" — quick descending tone with a touch of noise.
        private static AudioClip GenerateTowerFire()
        {
            return Synth("sfx_tower_fire", dur: 0.10f, f0: 1400f, f1: 600f,
                         noise: 0.12f, amp: 0.5f, seed: 0x70F1, decay: 4.0f);
        }

        // A low wooden "thunk" — short, body-heavy, a little grit.
        private static AudioClip GenerateTowerPlace()
        {
            return Synth("sfx_tower_place", dur: 0.22f, f0: 240f, f1: 90f,
                         noise: 0.30f, amp: 0.7f, seed: 0x701A, decay: 3.0f);
        }

        // A two-note rising battle horn — longer, brassy, no noise.
        private static AudioClip GenerateWaveStart()
        {
            int n = Mathf.Max(16, (int)(0.70f * Rate));
            var data = new float[n];
            float attack = 0.012f * Rate;
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                // Two-step rising horn: low note then a higher note partway through.
                float hz = (t < 0.45f) ? 165f : 220f;
                phase += 2.0 * System.Math.PI * hz / Rate;
                // Add a 5th harmonic for a brassier, hornier timbre.
                float s = (float)(System.Math.Sin(phase) * 0.75
                                  + System.Math.Sin(phase * 1.5) * 0.25);
                float env = i < attack ? (i / attack) : Mathf.Exp(-1.4f * t);
                if (i > n - 96) env *= (n - i) / 96f;
                data[i] = s * env * 0.7f;
            }
            var clip = AudioClip.Create("sfx_wave_start", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Mirrors EnemyCombatAudio.Synth / ProceduralSfx.Synth (kept local so this
        // file is self-contained): a sine sweep blended with noise, 6 ms attack,
        // exponential decay, tail taper to avoid clicks. `decay` tunes the falloff.
        private static AudioClip Synth(string name, float dur, float f0, float f1,
                                       float noise, float amp, int seed, float decay)
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
                float env = i < attack ? (i / attack) : Mathf.Exp(-decay * t);
                if (i > n - 64) env *= (n - i) / 64f;
                data[i] = v * env * amp;
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
