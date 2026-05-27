// =============================================================================
// WaveScalingCurve (DEF-59) — ScriptableObject that defines how enemy stats
// scale as the wave number increases.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT IT DOES:
//   Three AnimationCurves (HP, speed, contact-damage) keyed on wave number
//   (x-axis). WaveManager samples them in SpawnOne() after Configure() and
//   calls Enemy.ApplyWaveScaling() to boost the fresh instance. Because the
//   scaling is applied AFTER Configure the enemy's base values always come from
//   enemies.json, and the curve is a pure multiplier on top — easy to tune in
//   the SO inspector without touching data files.
//
// DEFAULT CURVES (set in Reset so a freshly-created SO is immediately usable):
//   HP     : 1.0× at wave 1 → 2.5× at wave 20 (linear, clamped to 20)
//   Speed  : 1.0× at wave 1 → 1.4× at wave 20
//   Damage : 1.0× at wave 1 → 2.0× at wave 20
//
//   Beyond wave 20 all three curves clamp at their final value (WrapMode.Clamp).
//   Tune freely in the Inspector — the curves are read at runtime with
//   AnimationCurve.Evaluate(waveNumber), so any shape works.
//
// USAGE:
//   1. Create an asset: Assets → Create → Defenders / Waves / Wave Scaling Curve
//   2. Assign it to WaveManager._scalingCurve in the Inspector.
//   3. Done — WaveManager will sample it on every enemy spawn.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Defines how enemy stats (HP, speed, contact damage) scale with the wave
    /// number. Assign to <see cref="WaveManager"/> and tune the curves freely.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WaveScalingCurve",
        menuName  = "Defenders/Waves/Wave Scaling Curve")]
    public sealed class WaveScalingCurve : ScriptableObject
    {
        [Tooltip("HP multiplier as a function of wave number (x = wave, y = ×). " +
                 "Default: 1.0 at wave 1 → 2.5 at wave 20.")]
        public AnimationCurve HpCurve = DefaultHp();

        [Tooltip("Move-speed multiplier as a function of wave number. " +
                 "Default: 1.0 at wave 1 → 1.4 at wave 20.")]
        public AnimationCurve SpeedCurve = DefaultSpeed();

        [Tooltip("Contact-damage multiplier as a function of wave number. " +
                 "Default: 1.0 at wave 1 → 2.0 at wave 20.")]
        public AnimationCurve DamageCurve = DefaultDamage();

        // ── Evaluate helpers — called by WaveManager ──────────────────────────

        /// <summary>HP multiplier for <paramref name="wave"/> (1-based wave number).</summary>
        public float HpMultiplier(int wave)    => Mathf.Max(1f, HpCurve.Evaluate(wave));

        /// <summary>Speed multiplier for <paramref name="wave"/> (1-based wave number).</summary>
        public float SpeedMultiplier(int wave) => Mathf.Max(0.5f, SpeedCurve.Evaluate(wave));

        /// <summary>Contact-damage multiplier for <paramref name="wave"/>.</summary>
        public float DamageMultiplier(int wave) => Mathf.Max(1f, DamageCurve.Evaluate(wave));

        // ── Default curve factories ───────────────────────────────────────────

        private static AnimationCurve DefaultHp()
        {
            var c = new AnimationCurve(
                new Keyframe(0f,  1.0f),
                new Keyframe(20f, 2.5f));
            c.preWrapMode  = WrapMode.Clamp;
            c.postWrapMode = WrapMode.Clamp;
            return c;
        }

        private static AnimationCurve DefaultSpeed()
        {
            var c = new AnimationCurve(
                new Keyframe(0f,  1.0f),
                new Keyframe(20f, 1.4f));
            c.preWrapMode  = WrapMode.Clamp;
            c.postWrapMode = WrapMode.Clamp;
            return c;
        }

        private static AnimationCurve DefaultDamage()
        {
            var c = new AnimationCurve(
                new Keyframe(0f,  1.0f),
                new Keyframe(20f, 2.0f));
            c.preWrapMode  = WrapMode.Clamp;
            c.postWrapMode = WrapMode.Clamp;
            return c;
        }

        private void Reset()
        {
            HpCurve     = DefaultHp();
            SpeedCurve  = DefaultSpeed();
            DamageCurve = DefaultDamage();
        }
    }
}
