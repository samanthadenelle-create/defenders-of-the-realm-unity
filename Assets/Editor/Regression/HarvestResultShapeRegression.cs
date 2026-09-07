// =============================================================================
// HarvestResultShapeRegression [harvest-result-shape]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core).
//
// WO-1525. The HARVEST RESULT screen was ELEVEN LINES OF PROSE, and the owner said
// so plainly (2026-09-06 20:29, build 358574, frame
// Logs/device/screens/owner-screen-20260906-202933.png):
//
//     "Harvest results feel off and way to much to read, needs organized and
//      visually pleasing"
//
// Four prose lines per resource, three resources over:
//
//     Stone storage: 3000 / 3000 (full)
//     Collected: 0 of 32307 from your collectors | Uncollected: 32307
//     Those 32307 stone are still waiting in your collectors - nothing was lost.
//     Stone storage 3000 is full. Spend stone, or upgrade a Stoneyard, then collect again.
//
// This suite drives HarvestResultVM.Build - the PURE seam - with the owner's ACTUAL
// numbers from that frame, so the assertions are about the SHAPE the player reads,
// never about source text. No canvas, no scene, no PlayMode.
//
// Cases:
//   1  [three-rows]        three resources in, three rows out, in order.
//   2  [banked-is-big]     the banked figure is the row's own field, grouped, and a
//                          zero bank prints "0" (never "+0").
//   3  [waiting-survives]  the WO-1434 reassurance FIGURE is still on every row - the
//                          ticket forbids shortening by deleting it.
//   4  [state-is-a-word]   a full store says the WORD "FULL" (the owner is red/green
//                          colourblind; hue may never carry the state).
//   5  [full-row-has-door] every blocked row carries ONE action to a DEFINED PanelId.
//   6  [build-vs-upgrade]  0 containers built -> BUILD; 1+ -> UPGRADE. Same fixture.
//   7  [said-once]         "nothing was lost" appears EXACTLY ONCE on the whole screen.
//   8  [burn-never-lies]   a genuinely-burning producer gets NO reassurance footer, and
//                          its second figure reads "lost" (WO-1434, inverted).
//   9  [overcap-spends]    OverCap is a different situation and gets the SPEND door.
//   10 [no-paragraphs]     no row field contains a newline - a row is fields, not prose.
//   11 [row-cap]           more resources than MaxRows collapse into "+N more".
//   12 [ascii-only]        every produced character is ASCII (non-ASCII = device tofu).
//
// Markers: HARVEST_RESULT_SHAPE_OK / HARVEST_RESULT_SHAPE_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.HarvestResultShapeRegression.RunAll
//
// OWED, and deliberately not claimed here: this suite proves the DATA shape. That the
// three plates FIT at 2670x1200 and 1920x1080 with no ellipsis is a CAPTURE claim
// (HarvestOverflow_*.png) and the felt-verify is the owner's, per WO-1525 section 4.
// PanelRouter.IsRegistered is FALSE in EditMode (nothing registers a panel outside a
// play session), so case 5 asserts the id is DEFINED, not that it is live.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Economy;
using DeNelle.Core.UI;

namespace DeNelle.Editor.Regression
{
    public static class HarvestResultShapeRegression
    {
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("HARVEST_RESULT_SHAPE_OK - " + reason);
            else Debug.LogError("HARVEST_RESULT_SHAPE_FAIL: " + reason);
        }

        // -- The owner's 2026-09-06 20:29 frame, verbatim -------------------------
        // Stone: 0 banked of 32,307, cap 3,000, NO Stoneyard built (WO-1525 section 2B).
        // Wood:  2,814 banked of 26,167, store lands on 26,000 / 26,000.
        // Iron:  792 banked of 13,083, store lands on 10,000 / 10,000.
        private static BankOverflowStatus Row(string name, string container, BankResource res,
                                              int granted, int requested, int current, int max,
                                              string source, bool overCap = false)
            => new BankOverflowStatus
            {
                Available = true,
                Resource = res,
                ResourceName = name,
                ContainerName = container,
                Requested = requested,
                Granted = granted,
                Lost = requested - granted,
                Current = current,
                Max = max,
                OverCap = overCap,
                Source = source,
            };

        private const string Collectors = "Collectors";

        private static List<BankOverflowStatus> OwnerFrame() => new List<BankOverflowStatus>
        {
            Row("Stone", "Stoneyard",  BankResource.Food, 0,    32307, 3000,  3000,  Collectors),
            Row("Wood",  "Lumberyard", BankResource.Wood, 2814, 26167, 23186, 26000, Collectors),
            Row("Iron",  "Foundry",    BankResource.Iron, 792,  13083, 9208,  10000, Collectors),
        };

        /// <summary>Containers built, as the owner's save had them: no Stoneyard, one
        /// Lumberyard, two Foundries. This is the ONE live signal the VM takes.</summary>
        private static int BuiltFor(BankResource r)
        {
            if (r == BankResource.Wood) return 1;
            if (r == BankResource.Iron) return 2;
            return 0;
        }

        private static int CountOf(string haystack, string needle)
        {
            int n = 0, i = 0;
            while (true)
            {
                i = haystack.IndexOf(needle, i, StringComparison.OrdinalIgnoreCase);
                if (i < 0) return n;
                n++;
                i += needle.Length;
            }
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            try
            {
                var vm = HarvestResultVM.Build(OwnerFrame(), BuiltFor);

                // -- 1 [three-rows] ----------------------------------------------
                if (vm.Rows.Count != 3)
                    failures.Add("[three-rows] the owner's three-resource frame produced " + vm.Rows.Count +
                                 " row(s) instead of 3");
                else if (vm.Rows[0].ResourceName != "Stone" || vm.Rows[1].ResourceName != "Wood" ||
                         vm.Rows[2].ResourceName != "Iron")
                    failures.Add("[three-rows] the rows are not in the order the producer handed them in " +
                                 "(got " + vm.Rows[0].ResourceName + "/" + vm.Rows[1].ResourceName + "/" +
                                 vm.Rows[2].ResourceName + ")");

                if (vm.Rows.Count == 3)
                {
                    var stone = vm.Rows[0];
                    var wood = vm.Rows[1];
                    var iron = vm.Rows[2];

                    // -- 2 [banked-is-big] ---------------------------------------
                    if (wood.BankedText != "+2,814")
                        failures.Add("[banked-is-big] wood banked reads '" + wood.BankedText + "', expected '+2,814' " +
                                     "- the BANKED figure is the row's headline and it is grouped");
                    if (iron.BankedText != "+792")
                        failures.Add("[banked-is-big] iron banked reads '" + iron.BankedText + "', expected '+792'");
                    // A plus sign in front of nothing reads as a gain that did not happen.
                    if (stone.BankedText != "0")
                        failures.Add("[banked-is-big] a zero bank reads '" + stone.BankedText + "', expected '0' - " +
                                     "'+0' claims a gain the player did not get");

                    // -- 3 [waiting-survives] ------------------------------------
                    // WO-1525 section 3: do NOT shorten by deleting the waiting figure. It is the
                    // WO-1434 reassurance and the reason the screen is trusted.
                    if (wood.WaitingText.IndexOf("23,353", StringComparison.Ordinal) < 0)
                        failures.Add("[waiting-survives] the wood row lost its waiting figure (got '" +
                                     wood.WaitingText + "', expected it to carry 23,353)");
                    if (stone.WaitingText.IndexOf("32,307", StringComparison.Ordinal) < 0)
                        failures.Add("[waiting-survives] the stone row lost its waiting figure (got '" +
                                     stone.WaitingText + "')");
                    if (wood.WaitingText.IndexOf("waiting", StringComparison.OrdinalIgnoreCase) < 0 ||
                        !wood.Waits)
                        failures.Add("[waiting-survives] a COLLECTOR row does not say WAITING - the law word " +
                                     "(WO-1392/WO-1434) is waiting, never lost");
                    if (wood.Burned || stone.Burned || iron.Burned)
                        failures.Add("[waiting-survives] a collector row is marked BURNED; those units stay in " +
                                     "the collector (ResourceCollector.Collect only drains what banked)");

                    // -- 4 [state-is-a-word] -------------------------------------
                    for (int i = 0; i < vm.Rows.Count; i++)
                    {
                        var r = vm.Rows[i];
                        if (r.StateWord != "FULL")
                            failures.Add("[state-is-a-word] the " + r.ResourceName + " row is at cap but its state " +
                                         "word is '" + r.StateWord + "' - the owner is red/green colourblind, so " +
                                         "FULL is a WORD and never a hue");
                        if (r.StorageText.IndexOf("FULL", StringComparison.Ordinal) < 0)
                            failures.Add("[state-is-a-word] the " + r.ResourceName + " bar label '" + r.StorageText +
                                         "' does not carry the state word");
                    }
                    if (wood.StorageText.IndexOf("26,000 / 26,000", StringComparison.Ordinal) < 0)
                        failures.Add("[state-is-a-word] the wood bar reads '" + wood.StorageText + "' - expected the " +
                                     "AFTER figure 26,000 / 26,000 (WO-1392: Current is the PRE-grant wallet)");
                    if (wood.After != 26000 || Mathf.Abs(wood.Fill01 - 1f) > 0.0001f)
                        failures.Add("[state-is-a-word] the wood bar fill/after disagree with the label (after=" +
                                     wood.After + " fill=" + wood.Fill01 + ")");

                    // -- 5 [full-row-has-door] -----------------------------------
                    for (int i = 0; i < vm.Rows.Count; i++)
                    {
                        var r = vm.Rows[i];
                        if (!r.HasAction)
                        {
                            failures.Add("[full-row-has-door] the " + r.ResourceName + " row is FULL and offers no " +
                                         "door - a wall with no door is the whole complaint");
                            continue;
                        }
                        if (!Enum.IsDefined(typeof(PanelId), r.ActionDoor))
                            failures.Add("[full-row-has-door] the " + r.ResourceName + " door points at an UNDEFINED " +
                                         "PanelId (" + (int)r.ActionDoor + ")");
                        if (r.ActionDoor != PanelId.Manage || r.ActionContext != HarvestResultVM.BuildingsTab)
                            failures.Add("[full-row-has-door] the " + r.ResourceName + " door is '" + r.ActionDoor +
                                         "'/'" + r.ActionContext + "' - ManageScreenPanel.Open(string) accepts only " +
                                         "Defense/Buildings/Research/Troops and IGNORES anything else, which is a " +
                                         "door that half-opens");
                    }

                    // -- 6 [build-vs-upgrade] ------------------------------------
                    if (stone.ActionText != "BUILD STONEYARD")
                        failures.Add("[build-vs-upgrade] with NO Stoneyard built the stone door reads '" +
                                     stone.ActionText + "', expected 'BUILD STONEYARD' - offering UPGRADE for a " +
                                     "structure that does not exist is the worse miss");
                    if (wood.ActionText != "UPGRADE LUMBERYARD")
                        failures.Add("[build-vs-upgrade] with a Lumberyard built the wood door reads '" +
                                     wood.ActionText + "', expected 'UPGRADE LUMBERYARD'");

                    // A null signal must degrade to BUILD, never throw.
                    var noSignal = HarvestResultVM.Build(OwnerFrame(), null);
                    if (noSignal.Rows.Count != 3 || noSignal.Rows[1].ActionText != "BUILD LUMBERYARD")
                        failures.Add("[build-vs-upgrade] a NULL container signal did not degrade to BUILD (wood door " +
                                     "read '" + (noSignal.Rows.Count > 1 ? noSignal.Rows[1].ActionText : "<none>") + "')");

                    // -- 10 [no-paragraphs] --------------------------------------
                    for (int i = 0; i < vm.Rows.Count; i++)
                    {
                        var r = vm.Rows[i];
                        if (r.ResourceName.IndexOf('\n') >= 0 || r.BankedText.IndexOf('\n') >= 0 ||
                            r.WaitingText.IndexOf('\n') >= 0 || r.StorageText.IndexOf('\n') >= 0 ||
                            r.ActionText.IndexOf('\n') >= 0)
                            failures.Add("[no-paragraphs] the " + r.ResourceName + " row carries a newline inside a " +
                                         "field - a row is FIELDS, and prose is what WO-1525 removed");
                        if (r.WaitingText.Length > 40)
                            failures.Add("[no-paragraphs] the " + r.ResourceName + " waiting field is " +
                                         r.WaitingText.Length + " chars ('" + r.WaitingText + "') - it has grown back " +
                                         "into a sentence");
                    }
                }

                // -- 7 [said-once] -----------------------------------------------
                string all = vm.AllText();
                int reassurances = CountOf(all, "nothing was lost");
                if (reassurances != 1)
                    failures.Add("[said-once] 'nothing was lost' appears " + reassurances + " time(s) on the whole " +
                                 "screen, expected exactly 1 - the old body said it once PER RESOURCE");
                if (!vm.FooterReassures || string.IsNullOrEmpty(vm.FooterLine))
                    failures.Add("[said-once] every row on this frame RETAINED its units, yet the footer does not " +
                                 "reassure (footer='" + vm.FooterLine + "')");

                // -- 8 [burn-never-lies] -----------------------------------------
                // OfflineHarvestService.Grant genuinely discards its pre-clamp accrual, so a row
                // from that path must NOT be covered by the reassurance. This is WO-1434's rule
                // read in the other direction, and it is the one the shorter screen could break.
                var burned = HarvestResultVM.Build(new List<BankOverflowStatus>
                {
                    Row("Wood", "Lumberyard", BankResource.Wood, 100, 500, 3900, 4000, "OfflineHarvest"),
                }, BuiltFor);
                if (burned.Rows.Count != 1)
                {
                    failures.Add("[burn-never-lies] the burn fixture produced " + burned.Rows.Count + " row(s)");
                }
                else
                {
                    var b = burned.Rows[0];
                    if (!b.Burned || b.Waits)
                        failures.Add("[burn-never-lies] an OfflineHarvest row is marked as waiting; that path drops " +
                                     "its pre-clamp accrual on the floor");
                    if (b.WaitingText.IndexOf("lost", StringComparison.OrdinalIgnoreCase) < 0)
                        failures.Add("[burn-never-lies] the burned row's second figure reads '" + b.WaitingText +
                                     "' and never says the units were lost");
                    if (burned.FooterReassures ||
                        CountOf(burned.AllText(), "nothing was lost") != 0)
                        failures.Add("[burn-never-lies] the reassurance footer is shown over a row that BURNED - " +
                                     "that is the WO-1434 lie inverted, and it is worse than the long copy");
                }

                // -- 9 [overcap-spends] ------------------------------------------
                var over = HarvestResultVM.Build(new List<BankOverflowStatus>
                {
                    Row("Wood", "Lumberyard", BankResource.Wood, 0, 300, 5000, 4000, Collectors, overCap: true),
                }, BuiltFor);
                if (over.Rows.Count != 1 || over.Rows[0].StateWord != "OVER")
                    failures.Add("[overcap-spends] an OVER-CAP row does not say OVER - it is a DIFFERENT situation " +
                                 "from a full bank (BankOverflowStatus.OverCap) and must not read the same");
                else if (over.Rows[0].ActionText != "SPEND WOOD")
                    failures.Add("[overcap-spends] the over-cap door reads '" + over.Rows[0].ActionText +
                                 "', expected 'SPEND WOOD' - more storage is not the fix above the cap");

                // -- 11 [row-cap] ------------------------------------------------
                var many = new List<BankOverflowStatus>();
                for (int i = 0; i < HarvestResultVM.MaxRows + 2; i++)
                    many.Add(Row("Res" + i, "Store", BankResource.Wood, 10, 100, 990, 1000, Collectors));
                var capped = HarvestResultVM.Build(many, BuiltFor);
                if (capped.Rows.Count != HarvestResultVM.MaxRows)
                    failures.Add("[row-cap] " + many.Count + " resources drew " + capped.Rows.Count + " rows; the cap " +
                                 "is " + HarvestResultVM.MaxRows + " because a fifth plate puts every door under " +
                                 "MinTouchPx");
                if (capped.OverflowLine != "+2 more")
                    failures.Add("[row-cap] the collapsed remainder reads '" + capped.OverflowLine +
                                 "', expected '+2 more'");
                if (capped.TotalRowCount != many.Count)
                    failures.Add("[row-cap] the VM forgot how many resources it was handed (TotalRowCount=" +
                                 capped.TotalRowCount + ")");

                // -- 12 [ascii-only] ---------------------------------------------
                string sweep = all + "\n" + burned.AllText() + "\n" + over.AllText() + "\n" + capped.AllText() +
                               "\n" + vm.TraceLine;
                for (int i = 0; i < sweep.Length; i++)
                {
                    char ch = sweep[i];
                    if (ch > 126 || (ch < 32 && ch != '\n'))
                    {
                        failures.Add("[ascii-only] the harvest result carries the non-ASCII character U+" +
                                     ((int)ch).ToString("X4") + " - it renders as tofu in the mobile atlas " +
                                     "(InvariantCulture grouping exists to prevent exactly this)");
                        break;
                    }
                }

                // An empty batch must produce an empty screen, never an exception or an empty state.
                var none = HarvestResultVM.Build(null, BuiltFor);
                if (none.Rows.Count != 0 || !string.IsNullOrEmpty(none.FooterLine))
                    failures.Add("[said-once] a null batch produced content (" + none.Rows.Count + " rows, footer='" +
                                 none.FooterLine + "')");
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            reason = failures.Count == 0
                ? "HARVEST RESULT SHAPE OK - three rows for three resources, the banked figure is the headline, the " +
                  "waiting figure survives, FULL is a word, every blocked row carries one door to a defined PanelId, " +
                  "'nothing was lost' is said exactly once and never over a burned row, and every string is ASCII"
                : string.Join("; ", failures);
            return failures.Count == 0;
        }
    }
}
