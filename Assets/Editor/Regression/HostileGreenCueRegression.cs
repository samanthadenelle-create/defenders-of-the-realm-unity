// =============================================================================
// HostileGreenCueRegression (WO-956) - proves the faction colour law: ENEMY-side
// presentation never sits on the green axis (owner is red/green colourblind;
// green is the SAFE/player hue).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
//
// WHAT IT PROVES, from real objects (not re-derivations):
//   1. The WO-956 hostile-palette PLACEHOLDERS are themselves off the green axis
//      (a green "hostile placeholder" would be the bug wearing a fix's name).
//   2. The green-dominance oracle trips on the KNOWN offenders (the retired
//      Warband-grunt orc green 0.30/0.42/0.22; the Lana Fog_poison greens) and
//      stays quiet on the deliberate near-neutrals (troll hide / ogre grey /
//      warlord slate) and on the enemy cast palette (arcane violet, fire orange).
//   3. EnforceOnTint substitutes green and PRESERVES alpha; leaves non-green alone.
//   4. END TO END on the REAL art: the committed Aura_Necromancer source prefab
//      (Lana Fog_poison - the VFX catalog points Aura_Necromancer at it) reads
//      green at baseline, reads NON-green after VfxLoopModulator.SetTintOverride,
//      and reads green again after Restore() - i.e. the pooled instance cannot
//      leak the override to its next (possibly player-side) user.
//
// No scene / no PlayMode. The one instantiated prefab is DestroyImmediate'd.
//
// Wire into the suite from DataRegression.RunAll (one line):
//   if (!HostileGreenCueRegression.Run(out var hostileGreenReason)) failures.Add(hostileGreenReason); else log.AppendLine("[hostile-green] " + hostileGreenReason);
// =============================================================================
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor
{
    public static class HostileGreenCueRegression
    {
        // The committed source art the VFX catalog binds to Aura_Necromancer
        // (VFXCatalogGenerator Map row; git-tracked Lana pack, never gitignored).
        private const string FogPoisonPath =
            "Assets/Lana Studio/Casual RPG VFX/Prefabs/Fog/Fog_poison.prefab";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- HOSTILE GREEN CUE (WO-956: enemy-side presentation never on the green axis) ---");

            // -- (1) the placeholders themselves are off the green axis ------------
            Expect(failures, log, !HostilePalette.IsGreenDominant(HostilePalette.PlaceholderEffectTint),
                $"PlaceholderEffectTint {HostilePalette.PlaceholderEffectTint} is not green-dominant");
            Expect(failures, log, !HostilePalette.IsGreenDominant(HostilePalette.PlaceholderBodyTint),
                $"PlaceholderBodyTint {HostilePalette.PlaceholderBodyTint} is not green-dominant");

            // -- (2) oracle truth table -------------------------------------------
            // Known offenders MUST trip:
            Expect(failures, log, HostilePalette.IsGreenDominant(new Color(0.30f, 0.42f, 0.22f)),
                "retired Warband-grunt orc green (0.30,0.42,0.22) trips the oracle");
            Expect(failures, log, HostilePalette.IsGreenDominant(new Color(0.18985686f, 0.5754717f, 0.11672304f)),
                "Fog_poison body green trips the oracle");
            // Deliberate non-greens MUST pass:
            Expect(failures, log, !HostilePalette.IsGreenDominant(new Color(0.38f, 0.40f, 0.34f)),
                "troll grey-green hide (near-neutral) does NOT trip");
            Expect(failures, log, !HostilePalette.IsGreenDominant(new Color(0.48f, 0.47f, 0.52f)),
                "ogre grey does NOT trip");
            Expect(failures, log, !HostilePalette.IsGreenDominant(new Color(0.22f, 0.20f, 0.26f)),
                "warlord undead slate does NOT trip");
            Expect(failures, log, !HostilePalette.IsGreenDominant(new Color(0.6f, 0.4f, 1f)),
                "EnemyTypeVfxSet default arcane violet does NOT trip");
            Expect(failures, log, !HostilePalette.IsGreenDominant(new Color(1f, 0.55f, 0.15f)),
                "Enemy default fire-orange cast tint does NOT trip");

            // -- (3) EnforceOnTint: substitute green (alpha preserved), pass non-green --
            var greenIn = new Color(0.2f, 0.8f, 0.2f, 0.35f);
            var enforced = HostilePalette.EnforceOnTint(greenIn, "regression");
            Expect(failures, log, !HostilePalette.IsGreenDominant(enforced),
                $"EnforceOnTint substituted the green tint (got {enforced})");
            Expect(failures, log, Mathf.Approximately(enforced.a, greenIn.a),
                "EnforceOnTint preserved the authored alpha");
            var orangeIn = new Color(1f, 0.55f, 0.15f, 1f);
            Expect(failures, log, HostilePalette.EnforceOnTint(orangeIn, "regression") == orangeIn,
                "EnforceOnTint left the non-green tint untouched");

            // -- (4) real art, real modulator: override + pool-safe restore --------
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FogPoisonPath);
            if (prefab == null)
            {
                failures.Add($"Aura_Necromancer source art missing at '{FogPoisonPath}' - the " +
                    "catalog row would fall to a procedural loop and this oracle cannot prove the re-tint.");
            }
            else
            {
                GameObject inst = null;
                try
                {
                    inst = Object.Instantiate(prefab);
                    var mod = inst.AddComponent<VfxLoopModulator>();

                    bool baselineGreen = mod.BaselineReadsGreen();
                    Expect(failures, log, baselineGreen,
                        "Fog_poison (Aura_Necromancer art) reads GREEN at baseline - the defect WO-956 closes");

                    mod.SetTintOverride(HostilePalette.PlaceholderEffectTint);
                    Expect(failures, log, !mod.CurrentReadsGreen(),
                        "after SetTintOverride the live instance no longer reads green");
                    Expect(failures, log, mod.BaselineReadsGreen() == baselineGreen,
                        "BaselineReadsGreen is unchanged by the override (baseline is authored truth)");

                    mod.Restore();
                    Expect(failures, log, mod.CurrentReadsGreen() == baselineGreen,
                        "after Restore the authored colours are back - the pool cannot receive a re-tinted instance");
                }
                finally
                {
                    if (inst != null) Object.DestroyImmediate(inst);
                }
            }

            if (failures.Count > 0)
            {
                reason = $"HOSTILE-GREEN-CUE: {failures.Count} failure(s): " + string.Join(" | ", failures);
                Debug.LogError(reason + "\n" + log);
                return false;
            }

            reason = "hostile-green-cue OK (placeholders off-axis; oracle truth table 9/9; " +
                     "EnforceOnTint substitutes+preserves alpha; Fog_poison override+restore round-trips)";
            Debug.Log(reason + "\n" + log);
            return true;
        }

        private static void Expect(List<string> failures, StringBuilder log, bool ok, string what)
        {
            log.AppendLine((ok ? "PASS " : "FAIL ") + what);
            if (!ok) failures.Add(what);
        }
    }
}
