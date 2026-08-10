// =============================================================================
// HarvestDripRegression — WO-953 oracle for the harvest drip-feedback lane.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Shape: public static bool Run(out string reason)
// — registered into DataRegression.RunAll by the orchestrator (registration line
// lives with the orchestrator; this file only provides Run).
//
// FOUR ASSERTION GROUPS (headless, data-decidable, no play-mode):
//   1. Tuning data      — harvest-tuning.json loads through HarvestTuning and the
//      DEFAULT VALUES ARE THE OLD HARDCODES UNCHANGED (owner ruling: promotion,
//      not retune — 5 / 6s / 5). Version present.
//   2. Dual-copy law    — the Resources and StreamingAssets copies are BYTE-
//      IDENTICAL (the canonical-data law; a drifted pair is how a retune ships
//      to the editor but not the device).
//   3. Gain parsing     — ResourceGainPopup.TryParseGain accepts exactly the
//      "+N Name" income shape (multi-word labels included) and rejects
//      status text / zero / malformed messages, so the one-pool merge path
//      only ever engages on real gains.
//   4. Merge constants  — the burst-throttle window is sane: positive and under
//      the label lifetime (a window longer than the label would try to merge
//      into a recycled body).
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DeNelle.Village;
using DeNelle.Village.World;

namespace DeNelle.Editor
{
    public static class HarvestDripRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            void Fail(string s) => failures.Add("HARVEST_DRIP FAIL: " + s);

            try
            {
                CheckTuningData(Fail);
                CheckDualCopy(Fail);
                CheckGainParsing(Fail);
                CheckMergeConstants(Fail);
            }
            catch (Exception ex)
            {
                Fail($"oracle threw: {ex.GetType().Name}: {ex.Message}");
            }

            if (failures.Count == 0)
            {
                reason = "HARVEST DRIP OK — harvest-tuning.json loads (5/6s/5 defaults unchanged) + dual-copy "
                       + "byte-identical + '+N Name' gain parsing (accept/reject) + merge window sane";
                return true;
            }
            reason = $"harvest-drip: {failures.Count} failure(s): " + string.Join(" | ", failures);
            return false;
        }

        // =====================================================================
        //  Group 1 — the promoted pet-node rates load, values unchanged
        // =====================================================================
        private static void CheckTuningData(Action<string> Fail)
        {
            HarvestTuning.Reload();
            if (HarvestTuning.PetNodeYieldPerExtract != 5)
                Fail($"petNode.yieldPerExtract={HarvestTuning.PetNodeYieldPerExtract} (expected 5 — WO-953 promotes, never retunes; the tuning pass is the owner's)");
            if (Mathf.Abs(HarvestTuning.PetNodeExtractCooldownSeconds - 6f) > 0.001f)
                Fail($"petNode.extractCooldownSeconds={HarvestTuning.PetNodeExtractCooldownSeconds} (expected 6)");
            if (HarvestTuning.PetNodeSiteBaseYield != 5)
                Fail($"petNode.siteBaseYield={HarvestTuning.PetNodeSiteBaseYield} (expected 5)");
            if (HarvestTuning.Version < 1)
                Fail($"harvest-tuning version {HarvestTuning.Version} (expected >= 1)");
        }

        // =====================================================================
        //  Group 2 — dual-copy law (Resources copy == StreamingAssets copy)
        // =====================================================================
        private static void CheckDualCopy(Action<string> Fail)
        {
            string resPath = Path.Combine(Application.dataPath, "Resources/Data/Canonical/harvest-tuning.json");
            string saPath  = Path.Combine(Application.dataPath, "StreamingAssets/Data/Canonical/harvest-tuning.json");
            if (!File.Exists(resPath)) { Fail($"missing Resources copy: {resPath}"); return; }
            if (!File.Exists(saPath))  { Fail($"missing StreamingAssets copy: {saPath}"); return; }
            byte[] a = File.ReadAllBytes(resPath);
            byte[] b = File.ReadAllBytes(saPath);
            if (a.Length != b.Length)
            {
                Fail($"dual copies differ in size ({a.Length} vs {b.Length} bytes) — the dual-copy law requires byte-identical files");
                return;
            }
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) { Fail($"dual copies differ at byte {i} — keep Resources + StreamingAssets byte-identical"); return; }
        }

        // =====================================================================
        //  Group 3 — the "+N Name" gain parser (the one-pool routing decision)
        // =====================================================================
        private static void CheckGainParsing(Action<string> Fail)
        {
            void ExpectGain(string msg, int wantAmount, string wantLabel)
            {
                if (!ResourceGainPopup.TryParseGain(msg, out int amt, out string label))
                { Fail($"TryParseGain rejected valid gain '{msg}'"); return; }
                if (amt != wantAmount || label != wantLabel)
                    Fail($"TryParseGain('{msg}') = ({amt}, '{label}') (expected ({wantAmount}, '{wantLabel}'))");
            }
            void ExpectReject(string msg)
            {
                if (ResourceGainPopup.TryParseGain(msg, out int amt, out string label))
                    Fail($"TryParseGain accepted non-gain '{msg}' as ({amt}, '{label}') — a status phrase must never enter the merge path");
            }

            ExpectGain("+5 Wood", 5, "Wood");
            ExpectGain("+12 Aether Crystals", 12, "Aether Crystals");
            ExpectGain("+1 Iron", 1, "Iron");
            ExpectReject(null);
            ExpectReject("");
            ExpectReject("LEVEL UP! Lv.3");
            ExpectReject("+0 Wood");     // zero gain is not a gain
            ExpectReject("+5");          // no label
            ExpectReject("+5Wood");      // no separator
            ExpectReject("+ Wood");      // no digits
            ExpectReject("5 Wood");      // no plus
        }

        // =====================================================================
        //  Group 4 — merge window sanity (burst throttle vs label lifetime)
        // =====================================================================
        private static void CheckMergeConstants(Action<string> Fail)
        {
            float w = DamageNumberSpawner.GainMergeWindowSeconds;
            if (w <= 0f)
                Fail($"GainMergeWindowSeconds={w} (must be positive — no window means every tick stacks a popup)");
            // Labels linger 1.6s (DamageNumberSpawner.BuildLabel). A merge window at or
            // beyond that would target bodies the pool may already have recycled.
            if (w >= 1.6f)
                Fail($"GainMergeWindowSeconds={w} (must stay under the 1.6s label lifetime — merging into a recycled body is the cross-lease bug)");
        }
    }
}
