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

                // -- 7 [overflow-stays-pending] (WO-1392) --------------------------------
                // A COLLECTOR row (Source = HarvestOverflowModal.CollectorSource) reports units
                // that ResourceCollector.Collect left PENDING - nothing burned. The copy must say
                // WAITING, must not say lost, must show "of N" as the popup's number with its
                // source, and must name the cap by its player word ("Wood storage 4000").
                // RED on the pre-WO-1392 body: it printed "Those 414 wood ... they are lost."
                var waiting = new BankOverflowStatus
                {
                    Available = true, Resource = BankResource.Wood, ResourceName = "Wood",
                    ContainerName = "Lumberyard", Requested = 672, Granted = 258, Lost = 414,
                    Current = 3742, Max = 4000, OverCap = false, Source = HarvestOverflowModal.CollectorSource,
                };
                string wait = HarvestOverflowModal.BuildBody(new List<BankOverflowStatus> { waiting });
                if (wait.IndexOf("still waiting in your collectors", StringComparison.Ordinal) < 0)
                    failures.Add("[overflow-stays-pending] a collector row does not say the remainder is 'still waiting " +
                                 "in your collectors' - the player is told the units went somewhere they did not");
                if (wait.IndexOf("they are lost", StringComparison.Ordinal) >= 0 ||
                    wait.IndexOf("it is lost", StringComparison.Ordinal) >= 0 ||
                    wait.IndexOf("not added to storage", StringComparison.Ordinal) >= 0)
                    failures.Add("[overflow-stays-pending] a collector row still uses the LOST copy - nothing is burned " +
                                 "on a collect any more (ResourceCollector.Collect leaves the remainder pending)");
                if (wait.IndexOf("Collected: 258 of 672 from your collectors", StringComparison.Ordinal) < 0)
                    failures.Add("[overflow-stays-pending] the collector row does not read 'Collected: 258 of 672 from your " +
                                 "collectors' - the 'of N' must be the popup's number and say where it came from");
                if (wait.IndexOf("Wood storage 4000", StringComparison.Ordinal) < 0)
                    failures.Add("[cap-named] the collector row does not name the cap that bit by its player word " +
                                 "('Wood storage 4000')");
                string waitOne = HarvestOverflowModal.BuildBody(new List<BankOverflowStatus>
                {
                    new BankOverflowStatus { Available = true, Resource = BankResource.Wood, ResourceName = "Wood",
                        ContainerName = "Lumberyard", Requested = 5, Granted = 4, Lost = 1, Current = 3996, Max = 4000,
                        Source = HarvestOverflowModal.CollectorSource },
                });
                if (waitOne.IndexOf("That 1 wood is still waiting", StringComparison.Ordinal) < 0)
                    failures.Add("[overflow-stays-pending] the singular collector row does not read 'That 1 wood is still waiting'");

                // -- 8 [post-collect-figure] (WO-1392) -----------------------------------
                // The owner's 2026-09-04 23:41 screen: the Echo silo asked for 2393 wood against a
                // wallet of 2021/4000; 1979 fit, 414 did not. The body printed "Wood storage: 2021 /
                // 4000" - the PRE-grant figure - beside "414 lost", and 2021 + 414 < 4000 made the
                // cap look like a lie. The storage figure must be Current + Granted (4000, full),
                // the source must be named, and the cap named by its player word.
                // RED on the pre-WO-1392 body: it printed "2021 / 4000".
                var silo = new BankOverflowStatus
                {
                    Available = true, Resource = BankResource.Wood, ResourceName = "Wood",
                    ContainerName = "Lumberyard", Requested = 2393, Granted = 1979, Lost = 414,
                    Current = 2021, Max = 4000, OverCap = false, Source = "EchoService.DumpSilos",
                };
                string siloBody = HarvestOverflowModal.BuildBody(new List<BankOverflowStatus> { silo });
                bool postFigure = false, preFigure = false;
                foreach (var line in siloBody.Split('\n'))
                {
                    if (line.Contains("Wood") && line.Contains("4000 / 4000")) postFigure = true;
                    if (line.Contains("2021 / 4000")) preFigure = true;
                }
                if (!postFigure || preFigure)
                    failures.Add("[post-collect-figure] the storage line does not print the POST-collect figure (Current + " +
                                 "Granted = 4000 / 4000); the pre-grant 2021 is what made the owner's screen unreadable");
                if (siloBody.IndexOf("from the Echo silo", StringComparison.Ordinal) < 0)
                    failures.Add("[post-collect-figure] a silo-dump row does not say its 'of N' came from the Echo silo - " +
                                 "that is the number the popup never showed, and it must be told apart from the collectors'");
                if (siloBody.IndexOf("Wood storage 4000", StringComparison.Ordinal) < 0)
                    failures.Add("[cap-named] the silo row does not name the cap that bit by its player word ('Wood storage 4000')");
                // =============================================================
                //  WO-1434 -- THE SECOND MOVED PIN. THIS ASSERTION WAS FALSE.
                // -------------------------------------------------------------
                //  It used to read:
                //      if (siloBody.IndexOf("were not added to storage") < 0)
                //          failures.Add("... the silo dump STILL BURNS its overflow
                //                        today and must keep saying so");
                //  That belief was already stale when it was written. WO-1392 changed
                //  EchoService.DumpSilos to settle against the APPLIED basket
                //  (`s.SiloResources -= bankedFromSilo`, with an explicit STOP comment
                //  forbidding the old `-= pool`), so what the cap refuses STAYS IN THE
                //  SILO. The pin outlived the burn it was protecting and then REQUIRED
                //  the game to keep telling the player her resources were destroyed.
                //
                //  MEASURED, owner's Seeker 2026-09-06 (build 358161), one tap:
                //      [Flow:Harvest] silo dump: 28800 wood stayed in the silo - Wood storage full
                //      [Flow:Harvest] silo dump: 28800 iron stayed in the silo - Iron storage full
                //  and the pool was UNCHANGED at 57600 across three consecutive dumps
                //  (12:51:25, 12:56:03, 12:56:06). Nothing burned. Meanwhile the modal
                //  told her those 57,600 units "are lost".
                //
                //  The assertion is INVERTED, not deleted: a silo row must now say the
                //  units are WAITING, and must never say they were lost.
                // =============================================================
                if (siloBody.IndexOf("still waiting in your Echo silo", StringComparison.Ordinal) < 0)
                    failures.Add("[silo-never-burns] a silo-dump row does not say the refused units are STILL WAITING in " +
                                 "the silo - EchoService.DumpSilos retains them (WO-1392 applied-basket settle; proven on " +
                                 "the owner's device 2026-09-06, pool 57600 survived three dumps)");
                // The burn copy is exactly "... were not added to storage - they are lost." (and its
                // singular "it is lost"). Test for THOSE, not for the bare word: the correct
                // waiting copy legitimately ends "- nothing was lost."
                if (siloBody.IndexOf("not added to storage", StringComparison.Ordinal) >= 0 ||
                    siloBody.IndexOf("they are lost", StringComparison.Ordinal) >= 0 ||
                    siloBody.IndexOf("it is lost", StringComparison.Ordinal) >= 0)
                    failures.Add("[silo-never-burns] a silo-dump row still tells the player the units were LOST. They were " +
                                 "not: they stayed in the silo. The [Flow:Bank] 'LOST N' warn is the BANK saying it REFUSED " +
                                 "the units - never a statement about what the caller did with them");
                foreach (var ch in wait + siloBody)
                    if (ch > 126 || (ch < 32 && ch != '\n'))
                    {
                        failures.Add("[ascii-only] the WO-1392 copy carries the non-ASCII character U+" +
                                     ((int)ch).ToString("X4"));
                        break;
                    }
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
