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

        // WO-111 Chunk 10: New combat / world SFX (procedural generated for no-asset safety;
        // drop-in Resources/Sfx/ overrides supported like existing). Routed exclusively
        // through CoreServices.Audio?.PlaySfx (mixer groups for mobile volume/perf).
        private static AudioClip s_swordClash;
        private static AudioClip s_spellCast;
        private static AudioClip s_towerArrowHit;
        private static AudioClip s_petHarvest;
        private static AudioClip s_buildingUpgrade;
        private static AudioClip s_levelUp;
        private static AudioClip s_enemyDeath;
        private static AudioClip s_heroHit;
        private static AudioClip s_buildDenied;

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

        // WO-111 new SFX (combat / world / progression). All via CoreServices.Audio
        // (mixer for mobile). Generated or Resources/Sfx/ override.
        public static void PlaySwordClash()
        {
            if (s_swordClash == null)
                s_swordClash = Resources.Load<AudioClip>("Sfx/SwordClash") ?? GenerateSwordClash();
            CoreServices.Audio?.PlaySfx(s_swordClash, 0.65f);
        }

        public static void PlaySpellCast()
        {
            if (s_spellCast == null)
                s_spellCast = Resources.Load<AudioClip>("Sfx/SpellCast") ?? GenerateSpellCast();
            CoreServices.Audio?.PlaySfx(s_spellCast, 0.55f);
        }

        public static void PlayTowerArrowHit()
        {
            if (s_towerArrowHit == null)
                s_towerArrowHit = Resources.Load<AudioClip>("Sfx/TowerArrowHit") ?? GenerateTowerArrowHit();
            CoreServices.Audio?.PlaySfx(s_towerArrowHit, 0.4f);
        }

        public static void PlayPetHarvest()
        {
            if (s_petHarvest == null)
                s_petHarvest = Resources.Load<AudioClip>("Sfx/PetHarvest") ?? GeneratePetHarvest();
            CoreServices.Audio?.PlaySfx(s_petHarvest, 0.5f);
        }

        public static void PlayBuildingUpgrade()
        {
            if (s_buildingUpgrade == null)
                s_buildingUpgrade = Resources.Load<AudioClip>("Sfx/BuildingUpgrade") ?? GenerateBuildingUpgrade();
            CoreServices.Audio?.PlaySfx(s_buildingUpgrade, 0.75f);
        }

        public static void PlayLevelUp()
        {
            if (s_levelUp == null)
                s_levelUp = Resources.Load<AudioClip>("Sfx/LevelUp") ?? GenerateLevelUp();
            CoreServices.Audio?.PlaySfx(s_levelUp, 0.9f);
        }

        public static void PlayEnemyDeath()
        {
            if (s_enemyDeath == null)
                s_enemyDeath = Resources.Load<AudioClip>("Sfx/EnemyDeath") ?? GenerateEnemyDeath();
            CoreServices.Audio?.PlaySfx(s_enemyDeath, 0.6f);
        }

        public static void PlayHeroHit()
        {
            if (s_heroHit == null)
                s_heroHit = Resources.Load<AudioClip>("Sfx/HeroHit") ?? GenerateHeroHit();
            CoreServices.Audio?.PlaySfx(s_heroHit, 0.5f);
        }

        /// <summary>
        /// WO-394 — the "build denied" buzz played when a placement is rejected (not
        /// enough resources / no space / blocks the gate / locked). A short, low,
        /// dissonant double-blip so the player feels the rejection even before reading
        /// the reason toast. No-op when the audio service is not yet registered.
        /// </summary>
        public static void PlayBuildDenied()
        {
            if (s_buildDenied == null)
                s_buildDenied = Resources.Load<AudioClip>("Sfx/BuildDenied") ?? GenerateBuildDenied();
            CoreServices.Audio?.PlaySfx(s_buildDenied, 0.55f);
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

        // ── WO-111 missing generators (called from the Play* methods) ────────

        private static AudioClip GenerateSwordClash()
        {
            return Synth("sfx_sword_clash", dur: 0.12f, f0: 900f, f1: 350f, noise: 0.55f, amp: 0.85f, seed: 0xA2C3, decay: 3.2f);
        }

        private static AudioClip GenerateSpellCast()
        {
            return Synth("sfx_spell_cast", dur: 0.28f, f0: 420f, f1: 780f, noise: 0.25f, amp: 0.6f, seed: 0x7E3B, decay: 2.8f);
        }

        private static AudioClip GenerateTowerArrowHit()
        {
            return Synth("sfx_arrow_hit", dur: 0.09f, f0: 1100f, f1: 520f, noise: 0.35f, amp: 0.7f, seed: 0x4D1F, decay: 4.5f);
        }

        private static AudioClip GeneratePetHarvest()
        {
            return Synth("sfx_pet_harvest", dur: 0.18f, f0: 310f, f1: 180f, noise: 0.45f, amp: 0.55f, seed: 0xB9E2, decay: 2.1f);
        }

        private static AudioClip GenerateBuildingUpgrade()
        {
            return Synth("sfx_build_upgrade", dur: 0.22f, f0: 260f, f1: 620f, noise: 0.18f, amp: 0.75f, seed: 0x3C8A, decay: 3.0f);
        }

        private static AudioClip GenerateLevelUp()
        {
            return Synth("sfx_level_up", dur: 0.35f, f0: 520f, f1: 980f, noise: 0.08f, amp: 0.65f, seed: 0xF1D4, decay: 1.8f);
        }

        private static AudioClip GenerateEnemyDeath()
        {
            return Synth("sfx_enemy_death", dur: 0.55f, f0: 180f, f1: 95f, noise: 0.30f, amp: 0.8f, seed: 0x2E6C, decay: 1.6f);
        }

        private static AudioClip GenerateHeroHit()
        {
            return Synth("sfx_hero_hit", dur: 0.11f, f0: 380f, f1: 210f, noise: 0.40f, amp: 0.9f, seed: 0x9A1B, decay: 3.8f);
        }

        // WO-394 — a low dissonant "denied" buzz: two short descending blips so it
        // reads as a UI rejection, not a combat hit. Built directly (not via Synth) so
        // the two-blip gate is explicit.
        private static AudioClip GenerateBuildDenied()
        {
            int n = Mathf.Max(16, (int)(0.20f * Rate));
            var data = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                // Two blips: ~210 Hz then a lower ~150 Hz, with a silent gap between.
                float hz = (t < 0.45f) ? 210f : 150f;
                bool gap = (t >= 0.42f && t < 0.52f);
                phase += 2.0 * System.Math.PI * hz / Rate;
                // Square-ish tone (sine + a dissonant 1.5 partial) for a harsher buzz.
                float s = (float)(System.Math.Sin(phase) * 0.7 + System.Math.Sin(phase * 1.5) * 0.3);
                float env = gap ? 0f : Mathf.Exp(-3.5f * (t % 0.5f));
                if (i > n - 64) env *= (n - i) / 64f;
                data[i] = s * env * 0.7f;
            }
            var clip = AudioClip.Create("sfx_build_denied", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
