// =============================================================================
// HarvestResultCopyRegression [harvest-result-copy]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core).
//
// WO-1370. The HARVEST RESULT modal was unreadable, and the owner said so plainly:
// she could not tell what the "3000" referred to.
//
// The body was built by a loop written for a LIST, so ONE resource came out as four
// disconnected fragments separated by blank lines:
//
//     Stone
//     Collected: 0 of 90   |   Uncollected: 90
//
//     Storage: 3000 / 3000. Upgrade a Silo, or spend stone, before collecting again.
//
//     Each uncollected amount was not added to storage.
//
// The resource WORD and its storage FIGURE were in different blocks, so 3000 / 3000
// had no visible owner; the loss was only implied by a subtraction; and the trailing
// sentence said "Each" about a list of one.
//
// This suite calls BuildBody directly - it is a pure static string function, so the
// copy is testable headlessly with no canvas, no play session and nothing to restore.
//
// Cases:
//   1 [name-with-figure] the resource name and ITS storage figure are on the SAME line.
//   2 [loss-is-named]    the loss is stated with the word "lost", not left to arithmetic.
//   3 [no-list-tail]     no trailing summary sentence, and never the word "Each" on a
//                        single-resource body.
//   4 [number-agreement] 1 reads "was", many read "were" - both from the same call.
//   5 [ascii-only]       every character is ASCII (non-ASCII renders as tofu on device).
//   6 [multi-resource]   a two-resource body still names each resource beside its own
//                        figure - the fix must not trade the list case for the single one.
//
// Markers: HARVEST_RESULT_COPY_OK / HARVEST_RESULT_COPY_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.HarvestResultCopyRegression.RunAll
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Economy;
using DeNelle.Core.UI;

namespace DeNelle.Editor.Regression
{
    public static class HarvestResultCopyRegression
    {
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("HARVEST_RESULT_COPY_OK - " + reason);
            else Debug.LogError("HARVEST_RESULT_COPY_FAIL: " + reason);
        }

        /// <summary>The owner's ACTUAL 2026-09-04 overflow, not a generic sentinel.</summary>
        private static BankOverflowStatus Stone(int lost) => new BankOverflowStatus
        {
            Available = true,
            ResourceName = "Stone",
            ContainerName = "Silo",
            Requested = lost,
            Granted = 0,
            Lost = lost,
            Current = 3000,
            Max = 3000,
            OverCap = false,
            Source = "OfflineHarvest",
        };

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            try
            {
                string body = HarvestOverflowModal.BuildBody(new List<BankOverflowStatus> { Stone(90) });
                var lines = (body ?? string.Empty).Split('\n');

                // ── 1 [name-with-figure] ────────────────────────────────────────────────
                // THE defect. Somewhere in this body there must be ONE line carrying both the
                // resource word and its storage figure; "3000 / 3000" alone on a line is what
                // the owner could not read.
                bool paired = false;
                foreach (var line in lines)
                    if (line.IndexOf("Stone", StringComparison.Ordinal) >= 0 &&
                        line.IndexOf("3000 / 3000", StringComparison.Ordinal) >= 0) { paired = true; break; }
                if (!paired)
                    failures.Add("[name-with-figure] no single line carries BOTH the resource name and its storage " +
                                 "figure - '3000 / 3000' floats free of the word it describes, which is exactly what " +
                                 "the owner could not interpret (WO-1370)");

                // ── 2 [loss-is-named] ───────────────────────────────────────────────────
                if (body.IndexOf("lost", StringComparison.OrdinalIgnoreCase) < 0)
                    failures.Add("[loss-is-named] the body never says the amount was LOST - the player is left to " +
                                 "infer it from 'Collected 0 of 90'");
                if (body.IndexOf("was not added to storage", StringComparison.Ordinal) < 0 &&
                    body.IndexOf("were not added to storage", StringComparison.Ordinal) < 0)
                    failures.Add("[loss-is-named] the authoritative 'not added to storage' truth is gone");

                // ── 3 [no-list-tail] ────────────────────────────────────────────────────
                if (body.IndexOf("Each", StringComparison.Ordinal) >= 0)
                    failures.Add("[no-list-tail] a single-resource body still says 'Each' - a list sentence about a " +
                                 "list of one, which is how the old copy ended");

                // ── 4 [number-agreement] ────────────────────────────────────────────────
                string one = HarvestOverflowModal.BuildBody(new List<BankOverflowStatus> { Stone(1) });
                if (one.IndexOf("was not added to storage", StringComparison.Ordinal) < 0)
                    failures.Add("[number-agreement] the SINGULAR body does not read 'was not added to storage'");
                if (one.IndexOf("were not added", StringComparison.Ordinal) >= 0)
                    failures.Add("[number-agreement] the singular body uses a plural verb");
                if (body.IndexOf("were not added to storage", StringComparison.Ordinal) < 0)
                    failures.Add("[number-agreement] the PLURAL body does not read 'were not added to storage'");

                // ── 5 [ascii-only] ──────────────────────────────────────────────────────
                foreach (var ch in body)
                    if (ch > 126 || (ch < 32 && ch != '\n'))
                    {
                        failures.Add("[ascii-only] the body carries the non-ASCII character U+" +
                                     ((int)ch).ToString("X4") + " - it renders as tofu on the device");
                        break;
                    }

                // ── 6 [multi-resource] ──────────────────────────────────────────────────
                var wood = Stone(60);
                wood.ResourceName = "Wood";
                wood.ContainerName = "Lumberyard";
                wood.Current = 2000; wood.Max = 2000;
                string two = HarvestOverflowModal.BuildBody(new List<BankOverflowStatus> { Stone(90), wood });
                bool stonePaired = false, woodPaired = false;
                foreach (var line in two.Split('\n'))
                {
                    if (line.Contains("Stone") && line.Contains("3000 / 3000")) stonePaired = true;
                    if (line.Contains("Wood") && line.Contains("2000 / 2000")) woodPaired = true;
                }
                if (!stonePaired || !woodPaired)
                    failures.Add("[multi-resource] a two-resource body does not put each resource beside its OWN " +
                                 "figure - the single-resource fix must not break the list case it came from");
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            reason = failures.Count == 0
                ? "HARVEST RESULT COPY OK - the resource name and its storage figure share a line, the loss is named " +
                  "as lost, singular and plural both read correctly, there is no list-tail sentence, and the body is " +
                  "pure ASCII"
                : string.Join("; ", failures);
            return failures.Count == 0;
        }
    }
}
