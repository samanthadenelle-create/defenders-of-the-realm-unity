// =============================================================================
// WaveScalingRegression [wave-scaling] -- proves the most-played mode ESCALATES.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Village). Drives the REAL
// runtime fallback the WaveManager uses when no WaveScalingCurve asset is wired --
// WaveManager.EnsureScalingCurve() (private instance), reflected here so the test
// exercises the SAME lazy ScriptableObject.CreateInstance<WaveScalingCurve>() path
// a live wave scene takes (the wave scenes ship no curve asset). Then it drives the
// REAL Enemy.ApplyWaveScaling(hp,speed,dmg) and reads _maxHp by reflection to prove
// a wave-19 enemy is materially tougher than a wave-1 enemy.
//
// Marker: WAVE_SCALING_OK / WAVE_SCALING_FAIL. Expected: GREEN (default curve
// escalates; EnsureScalingCurve exists).
//
// Wire (DataRegression.RunAll):
//   if (!WaveScalingRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[wave-scaling] " + r);
// =============================================================================
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor
{
    public static class WaveScalingRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- WAVE SCALING (WaveManager.EnsureScalingCurve fallback + Enemy.ApplyWaveScaling) ---");

            var created = new List<GameObject>();
            try
            {
                // (1) Resolve the curve THROUGH the runtime fallback path (not a bare
                //     CreateInstance) -- drive WaveManager.EnsureScalingCurve() by reflection.
                WaveScalingCurve curve = null;
                var wmGo = new GameObject("WaveManager (wave-scaling oracle)");
                created.Add(wmGo);
                var wm = wmGo.AddComponent<WaveManager>();
                var ensure = typeof(WaveManager).GetMethod("EnsureScalingCurve",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (ensure == null)
                {
                    failures.Add("WaveManager.EnsureScalingCurve() not found by reflection (runtime-fallback seam renamed) -- re-point this oracle");
                }
                else
                {
                    curve = ensure.Invoke(wm, null) as WaveScalingCurve;
                    if (curve == null)
                        failures.Add("WaveManager.EnsureScalingCurve() returned null -- the no-asset fallback does NOT create a default curve (wave scaling would be DEAD)");
                }

                if (curve != null)
                {
                    float hp1 = curve.HpMultiplier(1);
                    float hp19 = curve.HpMultiplier(19);
                    float dmg1 = curve.DamageMultiplier(1);
                    float dmg19 = curve.DamageMultiplier(19);
                    log.AppendLine($"  curve HP x{hp1:0.00}@w1 -> x{hp19:0.00}@w19; DMG x{dmg1:0.00}@w1 -> x{dmg19:0.00}@w19");
                    if (!(hp19 > hp1))
                        failures.Add($"[wave-scaling] HpMultiplier(19)={hp19:0.00} is not > HpMultiplier(1)={hp1:0.00} (HP does not scale)");
                    if (!(dmg19 > dmg1))
                        failures.Add($"[wave-scaling] DamageMultiplier(19)={dmg19:0.00} is not > DamageMultiplier(1)={dmg1:0.00} (contact damage does not scale)");

                    // (2) Drive the REAL Enemy scaling and read _maxHp (reflection). Two enemies:
                    //     one scaled for wave 1 (mult 1.0 -> no change), one for wave 19.
                    var hpField = typeof(Enemy).GetField("_maxHp", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (hpField == null)
                    {
                        failures.Add("Enemy._maxHp field not found by reflection (max-HP seam renamed) -- re-point this oracle");
                    }
                    else
                    {
                        float maxHpWave1 = ApplyAndReadMaxHp(created, hpField, curve.HpMultiplier(1), curve.SpeedMultiplier(1), curve.DamageMultiplier(1));
                        float maxHpWave19 = ApplyAndReadMaxHp(created, hpField, curve.HpMultiplier(19), curve.SpeedMultiplier(19), curve.DamageMultiplier(19));
                        log.AppendLine($"  Enemy _maxHp: wave1={maxHpWave1:0.0} -> wave19={maxHpWave19:0.0}");
                        if (!(maxHpWave19 > maxHpWave1))
                            failures.Add($"[wave-scaling] Enemy.ApplyWaveScaling(wave19) _maxHp={maxHpWave19:0.0} is not > wave1 _maxHp={maxHpWave1:0.0} (a wave-19 enemy is no tougher than a wave-1 enemy)");
                    }
                }
            }
            catch (System.Exception ex)
            {
                failures.Add($"wave-scaling oracle threw: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                foreach (var go in created) if (go != null) Object.DestroyImmediate(go);
            }

            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "WAVE_SCALING_OK");
                reason = "WAVE SCALING OK -- EnsureScalingCurve fallback non-null, HP+DMG multipliers climb by wave 19, Enemy _maxHp scales up";
                return true;
            }
            reason = "wave-scaling: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "WAVE_SCALING_FAIL: " + reason);
            return false;
        }

        private static float ApplyAndReadMaxHp(List<GameObject> created, FieldInfo hpField,
                                               float hpMult, float speedMult, float dmgMult)
        {
            var go = new GameObject("Enemy (wave-scaling oracle)");
            created.Add(go);
            var enemy = go.AddComponent<Enemy>();   // auto-adds NavMeshAgent + EnemyDamageable
            enemy.ApplyWaveScaling(hpMult, speedMult, dmgMult);
            object v = hpField.GetValue(enemy);
            return v is float f ? f : 0f;
        }
    }
}
