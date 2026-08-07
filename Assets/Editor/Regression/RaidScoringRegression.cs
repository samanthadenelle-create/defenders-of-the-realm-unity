// =============================================================================
// RaidScoringRegression — headless gate for the LOCKED-V1 raid win/stars/loot/HUD
// slice (WO-771.6 + WO-771.11, teleport/deploy loop). Marker: RAID_SCORING_OK.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Village + DeNelle.Core).
// Wired into DeNelle.Editor.DataRegression.RunAll (one line — see the report).
//
// This closes the "win/stars are OUT" gap the raid spine flagged
// (RaidDeployController.cs:27, RaidVictoryController.cs:34). It proves, from data +
// source (NO PlayMode), that:
//   (A) RaidScoring exists and its PURE star math computes 0-3 from
//       cleared / boss-down / destruction% / the 180s clock (design B5), and its
//       loot math scales with stars + destruction.
//   (B) RaidVictoryController GRANTS loot on the OnCleared victory path (reusing the
//       village EconomyService / GameStateService — not a bespoke economy).
//   (C) a live raid HUD view exists, code-built (uGUI via ElarionUiKit) — NOT uxml.
//
// Mirrors the TowerPerkRegression contract: public static bool Run(out string
// reason); true = pass + a one-line summary, false = fail + the offending detail.
// Never throws (source-lint I/O is guarded).
// =============================================================================

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor
{
    /// <summary>
    /// Data + source regression for the V1 raid scoring/loot/HUD slice. Real static
    /// game code in (RaidScoring.ComputeStars/ComputeLoot), asserted out; plus a
    /// source-lint that the victory grant + the code-built HUD exist. Returns true
    /// (summary) / false (detail); never throws.
    /// </summary>
    public static class RaidScoringRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- RAID SCORING/LOOT/HUD (WO-771.6 + 771.11 V1) ---");

            // =================================================================
            //  (A) PURE star + loot math — the deterministic-enough V1 formulas.
            // =================================================================

            // Stars 0-3 across the design B5 thresholds (cleared / boss / % / clock).
            // OWNER LADDER 2026-07-30: 1 = just cleared, 2 = cleared with high survival OR under
            // the clock, 3 = cleared with BOTH. Sub-clear credit (>=50% razed = 1) is unchanged.
            // The two-axis cases below are the point: a clear no longer implies 3 stars.
            AssertStars(failures, "retreat, <50% razed",              false, false, 0.20f,  10f, 180f, 1.00f, 0);
            AssertStars(failures, "retreat, >=50% razed",             false, false, 0.50f,  10f, 180f, 1.00f, 1);
            AssertStars(failures, "boss down (partial), floor is 1",  false, true,  0.60f,  10f, 180f, 1.00f, 1);
            AssertStars(failures, "clear, slow AND costly -> 1",      true,  true,  1.00f, 240f, 180f, 0.20f, 1);
            AssertStars(failures, "clear, under clock but costly",    true,  true,  1.00f, 120f, 180f, 0.20f, 2);
            AssertStars(failures, "clear, slow but high survival",    true,  true,  1.00f, 240f, 180f, 1.00f, 2);
            AssertStars(failures, "clear, fast AND high survival",    true,  true,  1.00f, 120f, 180f, 1.00f, 3);
            // Threshold boundary is inclusive at HighSurvivalPct, exclusive just under it.
            AssertStars(failures, "clear, fast, survival exactly at threshold",
                        true, true, 1.00f, 120f, 180f, RaidScoring.HighSurvivalPct, 3);
            AssertStars(failures, "clear, fast, survival a hair under threshold",
                        true, true, 1.00f, 120f, 180f, RaidScoring.HighSurvivalPct - 0.01f, 2);
            // A scout clear (no troops deployed) reads survival 1f and must not be punished.
            AssertStars(failures, "clear, fast, no troops deployed",   true,  true,  1.00f, 120f, 180f, 1.00f, 3);

            // The result is ALWAYS in 0..3 across a wide sweep (no out-of-range star).
            for (int di = 0; di <= 10; di++)
            {
                float d = di / 10f;
                foreach (var cleared in new[] { false, true })
                foreach (var boss in new[] { false, true })
                foreach (var t in new[] { 30f, 180f, 400f })
                foreach (var sv in new[] { 0f, 0.5f, RaidScoring.HighSurvivalPct, 1f })
                {
                    int s = RaidScoring.ComputeStars(cleared, boss, d, t, 180f, sv);
                    if (s < 0 || s > 3)
                        failures.Add($"ComputeStars out of range: {s} (cleared={cleared}, boss={boss}, d={d:0.0}, t={t}, surv={sv:0.00})");
                    // A 3-star tier must NEVER be reachable without clearing the base.
                    if (s == 3 && !cleared)
                        failures.Add($"ComputeStars awarded 3 stars WITHOUT a clear (d={d:0.0}, t={t}, surv={sv:0.00})");
                }
            }

            // Loot scales with stars + destruction, and a nothing-raid pays nothing.
            var lootNone = RaidScoring.ComputeLoot(0, 0f, 40, 60, 15, 20);
            var lootHalf = RaidScoring.ComputeLoot(1, 0.5f, 40, 60, 15, 20);
            var lootFull = RaidScoring.ComputeLoot(3, 1f, 40, 60, 15, 20);
            log.AppendLine($"  loot none=({lootNone.Crystals}c/{lootNone.Food}f) " +
                           $"half=({lootHalf.Crystals}c/{lootHalf.Food}f) full=({lootFull.Crystals}c/{lootFull.Food}f)");
            if (!(lootNone.Crystals == 0 && lootNone.Food == 0))
                failures.Add($"ComputeLoot(0,0) should be empty, got {lootNone.Crystals}c/{lootNone.Food}f");
            if (!(lootFull.Crystals > lootHalf.Crystals && lootHalf.Crystals > lootNone.Crystals))
                failures.Add($"ComputeLoot crystals not monotonic: none {lootNone.Crystals} <= half {lootHalf.Crystals} <= full {lootFull.Crystals}");
            if (!(lootFull.Food > lootHalf.Food && lootHalf.Food > lootNone.Food))
                failures.Add($"ComputeLoot food not monotonic: none {lootNone.Food} <= half {lootHalf.Food} <= full {lootFull.Food}");

            // =================================================================
            //  (B)/(C) SOURCE-LINT — the victory grant + the code-built HUD.
            // =================================================================
            string modulesDir = null;
            try { modulesDir = Path.Combine(Application.dataPath, "_Modules"); } catch { }
            if (string.IsNullOrEmpty(modulesDir) || !Directory.Exists(modulesDir))
            {
                log.AppendLine("  (source-lint skipped — Assets/_Modules not found)");
            }
            else
            {
                // RaidScoring.cs carries the star + loot math + finalize + clock event.
                string scoringSrc = ReadFirst(modulesDir, "RaidScoring.cs");
                if (scoringSrc == null) failures.Add("RaidScoring.cs not found under Assets/_Modules");
                else
                {
                    RequireAll(failures, "RaidScoring.cs", scoringSrc,
                        "ComputeStars", "ComputeLoot", "Finalize", "OnTimeExpired");

                    // WO-853 sec.7 — the structures term must be REAL, not a constant that
                    // nothing reads. Checked against COMMENT-STRIPPED source: a token that only
                    // appears in a doc comment proves nothing, and the whole point of these four
                    // is that live code touches them.
                    string scoringCode = StripComments(scoringSrc);
                    RequireAll(failures, "RaidScoring.cs (code)", scoringCode,
                        "StructuresWeight",     // the 30% exists
                        "StructuresRazedPct",   // and something computes it
                        "CaptureStructureCensus",
                        "TowerAllegiance.PlayerOwned");   // and the census respects ownership

                    // The wall term must read the SHARED 0..1 abstraction. WallSegment stores an
                    // INVERTED 0-100 damage track, so hand-rolling the division is only correct
                    // while MaxHp happens to be 100 - it would silently skew every raid score the
                    // day that constant moves.
                    //
                    // STRIPPED source, and that is not incidental: on its first run this check
                    // failed against RaidScoring.cs's own doc comment, which quotes the forbidden
                    // expression while explaining why not to write it. An oracle that cannot tell
                    // code from prose punishes the author for documenting the trap.
                    if (scoringCode.Contains("Damage / 100") || scoringCode.Contains("Damage/100"))
                        failures.Add("RaidScoring.cs derives wall health from a hardcoded /100 instead of " +
                                     "WallSegment.HpFraction - that breaks silently if WallSegment.MaxHp changes");
                }

                // (B) RaidVictoryController grants loot on the victory path via the
                //     village economy (EconomyService.Grant / GameStateService mutators).
                string victorySrc = ReadFirst(modulesDir, "RaidVictoryController.cs");
                if (victorySrc == null) failures.Add("RaidVictoryController.cs not found under Assets/_Modules");
                else
                {
                    RequireAll(failures, "RaidVictoryController.cs", victorySrc,
                        "GrantLoot", "RaidScoring", "Finalize");
                    bool grantsToEconomy = victorySrc.Contains("EconomyService")
                                        || victorySrc.Contains("AddCrystals")
                                        || victorySrc.Contains("AddFood");
                    if (!grantsToEconomy)
                        failures.Add("RaidVictoryController.cs GrantLoot does not reach the village economy (EconomyService/AddCrystals/AddFood)");
                }

                // (C) a live raid HUD view exists, code-built (uGUI) — NOT uxml.
                string hudSrc = ReadFirst(modulesDir, "RaidHudController.cs");
                if (hudSrc == null) failures.Add("RaidHudController.cs not found under Assets/_Modules (no live raid HUD)");
                else
                {
                    // Shows the four readouts + is built through the kit (code-built).
                    RequireAll(failures, "RaidHudController.cs", hudSrc,
                        "ElarionUiKit", "RaidScoring", "RemainingSeconds", "DestructionPct");
                    // §8: code-built uGUI, never uxml.
                    if (hudSrc.Contains(".uxml") || hudSrc.Contains("UIDocument") || hudSrc.Contains("VisualElement"))
                        failures.Add("RaidHudController.cs references uxml/UIDocument/VisualElement — the raid HUD must be code-built uGUI (repo rule §8)");
                }

                // Belt-and-braces: no RaidHud.uxml smuggled into the project.
                try
                {
                    var uxml = Directory.GetFiles(Application.dataPath, "RaidHud*.uxml", SearchOption.AllDirectories);
                    if (uxml != null && uxml.Length > 0)
                        failures.Add($"a RaidHud*.uxml exists ({uxml.Length}) — the raid HUD must be code-built uGUI, not uxml");
                }
                catch { /* enumeration best-effort */ }
            }

            // =================================================================
            //  (D) THE OWNER'S DESTRUCTION SPLIT — WO-853 sec.7, ruled 2026-08-07.
            //
            //  EXECUTED, not source-linted: this suite already references DeNelle.Village,
            //  so it reads the real constants. Before today NOTHING pinned these numbers -
            //  the 0.60f values elsewhere in this file are destructionPct ARGUMENTS to
            //  ComputeStars, not the weight - so the split could have drifted silently.
            //  That is exactly the class of miss the 4-day audit kept turning up.
            // =================================================================
            log.AppendLine($"  destruction split: spire {RaidScoring.SpireWeight:P0} / " +
                           $"structures {RaidScoring.StructuresWeight:P0} / " +
                           $"garrison {RaidScoring.GarrisonWeight:P0}");

            AssertWeight(failures, "spire", RaidScoring.SpireWeight, 0.50f);
            AssertWeight(failures, "structures", RaidScoring.StructuresWeight, 0.30f);
            AssertWeight(failures, "garrison (derived)", RaidScoring.GarrisonWeight, 0.20f);

            float weightSum = RaidScoring.SpireWeight + RaidScoring.StructuresWeight + RaidScoring.GarrisonWeight;
            if (Mathf.Abs(weightSum - 1f) > 0.0001f)
                failures.Add($"[split] the destruction weights sum to {weightSum:0.###}, not 1.0 - " +
                             "the razed bar can no longer reach 100% (or overshoots it)");

            // The regression that matters most: structures must carry REAL weight. The whole
            // defect WO-853 opened with was "a raid is a fight, never a demolition" - which is
            // what a 0% structures term means, whatever the seam underneath can damage.
            if (RaidScoring.StructuresWeight <= 0f)
                failures.Add("[split] StructuresWeight is 0 - breaking walls and turrets scores NOTHING again, " +
                             "which is the exact WO-853 defect. The owner ruled 50/30/20 on 2026-08-07.");

            // The spire must stay the single largest term or it stops reading as the objective.
            if (RaidScoring.SpireWeight < RaidScoring.StructuresWeight ||
                RaidScoring.SpireWeight < RaidScoring.GarrisonWeight)
                failures.Add($"[split] the spire ({RaidScoring.SpireWeight:P0}) is no longer the largest term " +
                             $"(structures {RaidScoring.StructuresWeight:P0}, garrison {RaidScoring.GarrisonWeight:P0}) - " +
                             "the objective must remain the primary objective");

            if (failures.Count == 0)
            {
                reason = null;
                Debug.Log(log.ToString() + "RAID_SCORING_OK");
                return true;
            }

            reason = "raid-scoring: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "RAID_SCORING_FAIL: " + reason);
            return false;
        }

        /// <summary>
        /// Blank out // line comments and block comments so a source-lint tests CODE, not prose.
        /// Deliberately replaces each comment with a space rather than deleting it, so a token
        /// cannot be forged by two identifiers on either side of a stripped comment.
        /// Not a C# parser: a "//" inside a string literal is also stripped. That is acceptable
        /// here (this file's checks look for identifiers, not string content) and is the same
        /// trade RegressionMarkerRegression.StripLineComments already makes.
        /// </summary>
        private static string StripComments(string src)
        {
            if (string.IsNullOrEmpty(src)) return src ?? string.Empty;

            var sb = new StringBuilder(src.Length);
            for (int i = 0; i < src.Length; i++)
            {
                // Block comment
                if (i + 1 < src.Length && src[i] == '/' && src[i + 1] == '*')
                {
                    int end = src.IndexOf("*/", i + 2);
                    if (end < 0) { sb.Append(' '); break; }
                    sb.Append(' ');
                    i = end + 1;
                    continue;
                }
                // Line comment (covers /// doc comments too)
                if (i + 1 < src.Length && src[i] == '/' && src[i + 1] == '/')
                {
                    int nl = src.IndexOf('\n', i);
                    sb.Append(' ');
                    if (nl < 0) break;
                    sb.Append('\n');
                    i = nl;
                    continue;
                }
                sb.Append(src[i]);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Pin one destruction weight to the owner-ruled value. Tolerance is float-noise only -
        /// these are design numbers, so "close enough" is not a thing.
        /// </summary>
        private static void AssertWeight(List<string> failures, string label, float actual, float expected)
        {
            if (Mathf.Abs(actual - expected) > 0.0001f)
                failures.Add($"[split] {label} weight is {actual:0.###}, owner ruled {expected:0.###} " +
                             "(WO-853 sec.7, 2026-08-07). Change the ruling, not just the constant.");
        }

        private static void AssertStars(List<string> failures, string label,
            bool cleared, bool boss, float destruction, float elapsed, float clock,
            float survivalPct, int expected)
        {
            int got = RaidScoring.ComputeStars(cleared, boss, destruction, elapsed, clock, survivalPct);
            if (got != expected)
                failures.Add($"ComputeStars [{label}] expected {expected} star(s), got {got}");
        }

        private static void RequireAll(List<string> failures, string file, string src, params string[] tokens)
        {
            foreach (var tok in tokens)
                if (!src.Contains(tok))
                    failures.Add($"{file} is missing expected token '{tok}'");
        }

        /// <summary>First matching file's text under <paramref name="root"/>, or null.</summary>
        private static string ReadFirst(string root, string fileName)
        {
            try
            {
                var hits = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
                if (hits == null || hits.Length == 0) return null;
                return File.ReadAllText(hits[0]);
            }
            catch { return null; }
        }
    }
}
