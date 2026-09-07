// =============================================================================
// HarvestResultVM - WO-1525. THE HARVEST RESULT, AS ROWS INSTEAD OF PROSE.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.UI
//
// THE MEASURED DEFECT (owner, 2026-09-06 20:29, device frame
// Logs/device/screens/owner-screen-20260906-202933.png, build 358574):
//
//     "Harvest results feel off and way to much to read, needs organized and
//      visually pleasing"
//
// The screen carried FOUR prose lines per resource, three resources over, plus a
// CLOSE - eleven lines to say three numbers three times:
//
//     Stone storage: 3000 / 3000 (full)
//     Collected: 0 of 32307 from your collectors | Uncollected: 32307
//     Those 32307 stone are still waiting in your collectors - nothing was lost.
//     Stone storage 3000 is full. Spend stone, or upgrade a Stoneyard, then collect again.
//
// =============================================================================
//  (!) THIS TYPE DECIDES; THE MODAL ONLY DRAWS.
// =============================================================================
// Every number, every word and every door lives here, as a PURE function of the
// clamp events plus ONE injected live signal (how many storage containers of that
// resource are already built). That is what makes the shape oracle-drivable with
// no canvas, no scene and no PlayMode - the same seam WelcomeBackDoorsVM (WO-1408)
// established, and for the same reason. HarvestOverflowModal must never grow a
// second opinion about what a row says.
//
// =============================================================================
//  (!) HIERARCHY BY SIZE, WEIGHT AND POSITION - NEVER BY HUE.
// =============================================================================
// The owner is red/green colourblind. FULL is the WORD "FULL"; a bar that is full
// still SAYS so in its value label. Nothing on this screen is legible only in
// colour, which is why every state this VM computes is emitted as text.
//
// =============================================================================
//  (!) WAITING, NEVER LOST - AND THE CONVERSE IS JUST AS BINDING.
// =============================================================================
// WO-1392 (collectors) and WO-1434 (Echo silo) both proved that the refused units
// STAY where they were, so their rows say WAITING and the footer reassures ONCE.
// But the burn branch is still a live code path (OfflineHarvestService.Grant
// discards its pre-clamp accrual), and printing "nothing was lost" over a row that
// genuinely burned would be the WO-1434 lie inverted. So the footer is
// SOURCE-AWARE: the reassurance appears only when EVERY row retained.
//
// ASCII ONLY - every string below reaches a mobile font atlas, and the figures are
// formatted with the invariant culture so a device locale cannot inject a
// non-ASCII group separator.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DeNelle.Core.Economy;

namespace DeNelle.Core.UI
{
    /// <summary>One resource's harvest outcome: what banked, what waits, the store, one door.</summary>
    public sealed class HarvestResultRow
    {
        /// <summary>Player word for the resource ("Wood"). Never empty.</summary>
        public string ResourceName = string.Empty;

        /// <summary>THE BIG NUMBER - what actually banked ("+2,814"). "0" when nothing fit
        /// (never "+0": a plus sign in front of nothing reads as a gain that did not happen).</summary>
        public string BankedText = string.Empty;

        /// <summary>The second number - what did not bank, and its fate. "23,353 waiting, safe"
        /// for a retaining producer; "12,291 lost" for the burn path. Empty when nothing was
        /// refused.</summary>
        public string WaitingText = string.Empty;

        /// <summary>The bar's inline value label: "26,000 / 26,000  FULL". The state WORD is part
        /// of this string on purpose - see the colourblind note in the file header.</summary>
        public string StorageText = string.Empty;

        /// <summary>"FULL", "OVER" or empty. Emitted separately so an oracle can assert the word
        /// exists without parsing the figures.</summary>
        public string StateWord = string.Empty;

        /// <summary>Store fill in 0..1 for the bar (AFTER this collect).</summary>
        public float Fill01;

        /// <summary>The store total after this collect, and its cap - the two figures inside
        /// <see cref="StorageText"/>, exposed for the oracle.</summary>
        public int After;
        public int Max;

        /// <summary>The one action chip's face ("UPGRADE LUMBERYARD" / "BUILD STONEYARD" /
        /// "SPEND WOOD"). Empty when this row needs no door.</summary>
        public string ActionText = string.Empty;

        /// <summary>Where the chip leads. Routed through <see cref="PanelRouter"/>.</summary>
        public PanelId ActionDoor = PanelId.Manage;

        /// <summary>The tab handed to <c>PanelRouter.Open(id, context)</c>.</summary>
        public string ActionContext = string.Empty;

        /// <summary>True when this row carries a door.</summary>
        public bool HasAction => !string.IsNullOrEmpty(ActionText);

        /// <summary>True when the refused units are STILL somewhere the player owns them.</summary>
        public bool Waits;

        /// <summary>True when the refused units were genuinely discarded by the producer.</summary>
        public bool Burned;

        /// <summary>"collectors" / "silo" / "burned" / "clean" - the trace word.</summary>
        public string TraceKind = "clean";
    }

    /// <summary>
    /// The whole HARVEST RESULT screen as data: the rows, the one footer line, and the
    /// "+N more" tail. Built by <see cref="Build"/>; drawn by HarvestOverflowModal.
    /// </summary>
    public sealed class HarvestResultVM
    {
        /// <summary>
        /// How many rows are drawn before the rest collapse into "+N more".
        ///
        /// <para>THREE, and the number is DERIVED from the modal's own constants, not chosen.
        /// HarvestOverflowModal draws the row band between <c>RowsTop</c> (0.86) and
        /// <c>RowsFloorWithOverflow</c> (0.36) with a <c>RowGap</c> of 0.02, so a plate is
        /// <c>(0.50 - 0.06) / 3 = 0.147</c> of the content rect - and the chip is drawn at the
        /// plate's FULL height (the WelcomeBackPopup DoorRowH trick, whose own comment records
        /// what a 0.88 inset gave back). At FOUR rows the same arithmetic yields 0.12, which is
        /// under the touch floor on a 1080-tall screen: a fourth row would put every door out of
        /// reach, which is worse than a collapsed line.</para>
        ///
        /// <para>It also costs nothing today: TownBankCapacity.UncappableResources (:265-269)
        /// exempts Crystals and Coins BY DESIGN, so only Wood / Iron / Food can produce an
        /// overflow row at all and a fourth aggregated row cannot occur. The "+N more" tail
        /// exists for the day that stops being true.</para>
        ///
        /// <para>(!) The exact pixel heights are OWED TO A CAPTURE, not asserted here - this lane
        /// ran no Unity, and BuildObsidianModal's content rect was not measured.</para>
        /// </summary>
        public const int MaxRows = 3;

        /// <summary>The Manage tab the doors land on. ManageScreenPanel.Open(string) accepts
        /// exactly "Defense", "Buildings", "Research" and "Troops[:id]" and IGNORES anything
        /// else, so this string is not free - it is the one that browses structures.</summary>
        public const string BuildingsTab = "Buildings";

        /// <summary>The rows, in the order they arrived. Never null.</summary>
        public readonly List<HarvestResultRow> Rows = new List<HarvestResultRow>();

        /// <summary>How many resources the caller handed in (may exceed <see cref="MaxRows"/>).</summary>
        public int TotalRowCount;

        /// <summary>"+2 more" when resources were collapsed, else empty.</summary>
        public string OverflowLine = string.Empty;

        /// <summary>The ONE footer sentence, said once for the whole screen instead of once per
        /// resource. Empty when nothing was refused at all.</summary>
        public string FooterLine = string.Empty;

        /// <summary>True when the footer is the WO-1434 reassurance (every row retained).</summary>
        public bool FooterReassures;

        /// <summary>The one trace line the modal emits per open.</summary>
        public string TraceLine
        {
            get
            {
                int waiting = 0, burned = 0, doors = 0;
                for (int i = 0; i < Rows.Count; i++)
                {
                    var r = Rows[i];
                    if (r == null) continue;
                    if (r.Waits) waiting++;
                    if (r.Burned) burned++;
                    if (r.HasAction) doors++;
                }
                return "rows=" + TotalRowCount + " shown=" + Rows.Count + " waiting=" + waiting +
                       " burned=" + burned + " doors=" + doors +
                       " footer='" + (FooterReassures ? "reassure" : (string.IsNullOrEmpty(FooterLine) ? "none" : "loss")) + "'";
            }
        }

        /// <summary>
        /// THE PURE SEAM. Clamp events in, rows out - no service lookup, no clock, no scene.
        /// </summary>
        /// <param name="results">The aggregated overflow rows. Null/empty yields an empty VM.</param>
        /// <param name="builtContainersFor">How many storage containers of that resource the
        /// player has ALREADY BUILT - the one live signal, and the only thing that decides
        /// BUILD versus UPGRADE. Null (or a negative answer) is read as ZERO, which produces
        /// BUILD: offering "upgrade" for a structure that does not exist is the worse miss.</param>
        public static HarvestResultVM Build(IReadOnlyList<BankOverflowStatus> results,
                                            Func<BankResource, int> builtContainersFor)
        {
            var vm = new HarvestResultVM();
            if (results == null || results.Count == 0) return vm;
            vm.TotalRowCount = results.Count;

            bool anyRefused = false;
            bool anyBurned = false;

            for (int i = 0; i < results.Count; i++)
            {
                var s = results[i];
                bool retains = Retains(s.Source);
                int refused = s.Lost > 0 ? s.Lost : 0;
                if (refused > 0)
                {
                    anyRefused = true;
                    if (!retains) anyBurned = true;
                }
                if (vm.Rows.Count >= MaxRows) continue;

                string name = string.IsNullOrEmpty(s.ResourceName) ? "Resource" : s.ResourceName;
                string container = string.IsNullOrEmpty(s.ContainerName) ? "Storehouse" : s.ContainerName;

                // WO-1392's figure, unchanged: BankOverflowStatus.Current is the wallet BEFORE the
                // grant was applied (that is what the clamp weighed), so the number the player can
                // check against the HUD rail is Current + Granted.
                int granted = s.Granted > 0 ? s.Granted : 0;
                long after64 = (long)Math.Max(0, s.Current) + granted;
                int after = after64 > int.MaxValue ? int.MaxValue : (int)after64;
                int max = s.Max;

                var row = new HarvestResultRow
                {
                    ResourceName = name,
                    BankedText = granted > 0 ? "+" + N(granted) : "0",
                    After = after,
                    Max = max,
                    Fill01 = max > 0 ? Clamp01((float)after / max) : 1f,
                    Waits = refused > 0 && retains,
                    Burned = refused > 0 && !retains,
                    TraceKind = refused <= 0 ? "clean" : SourceWord(s.Source),
                };

                // THE SECOND NUMBER. The law word rides WITH the figure, so a player who reads
                // nothing else still learns the fate of what did not fit.
                if (refused > 0)
                    row.WaitingText = retains ? N(refused) + " waiting, safe" : N(refused) + " lost";

                // THE STATE, AS A WORD. OverCap is a DIFFERENT situation from a full bank
                // (BankOverflowStatus.OverCap spends a paragraph on why) and keeps its own word.
                if (s.OverCap) row.StateWord = "OVER";
                else if (max > 0 && after >= max) row.StateWord = "FULL";
                row.StorageText = max > 0 ? N(after) + " / " + N(max) : N(after);
                if (!string.IsNullOrEmpty(row.StateWord))
                    row.StorageText += "  " + row.StateWord;

                // THE ONE DOOR. A row without a state word is not blocked by anything, so it gets
                // no chip - a door onto "nothing to do here" teaches the player the screen lies
                // (the WelcomeBackDoorsVM rule, same words).
                if (!string.IsNullOrEmpty(row.StateWord))
                {
                    int built = 0;
                    if (builtContainersFor != null)
                    {
                        int n = builtContainersFor(s.Resource);
                        built = n > 0 ? n : 0;
                    }
                    row.ActionText = s.OverCap
                        ? "SPEND " + name.ToUpperInvariant()
                        : (built > 0 ? "UPGRADE " + container.ToUpperInvariant()
                                     : "BUILD " + container.ToUpperInvariant());
                    row.ActionDoor = PanelId.Manage;
                    row.ActionContext = BuildingsTab;
                }

                vm.Rows.Add(row);
            }

            int hidden = vm.TotalRowCount - vm.Rows.Count;
            if (hidden > 0) vm.OverflowLine = "+" + hidden + " more";

            // THE FOOTER - ONCE, for the whole screen, and SOURCE-AWARE. The reassurance is the
            // WO-1434 law sentence and it is only true when every producer on this screen retained.
            if (anyRefused)
            {
                if (!anyBurned)
                {
                    vm.FooterReassures = true;
                    vm.FooterLine = "Nothing was lost - every waiting unit banks as soon as there is room.";
                }
                else
                {
                    vm.FooterReassures = false;
                    vm.FooterLine = "Units shown as lost were not added to storage.";
                }
            }

            return vm;
        }

        /// <summary>
        /// True when the producer named by <paramref name="source"/> KEEPS what the cap refused.
        /// <para>The two live producers both do: ResourceCollector.Collect only drains what banked
        /// (WO-1392) and EchoService.DumpSilos settles against the APPLIED basket (WO-1434, proven
        /// on the owner's Seeker - pool 57600 survived three dumps). Everything else is read as a
        /// burn, which is the safe direction: it never promises safety that was not proven.</para>
        /// </summary>
        public static bool Retains(string source)
        {
            if (string.IsNullOrEmpty(source)) return false;
            if (string.Equals(source, HarvestOverflowModal.CollectorSource, StringComparison.Ordinal)) return true;
            return source.IndexOf("DumpSilos", StringComparison.Ordinal) >= 0;
        }

        /// <summary>"collectors" / "silo" / "burned" - the trace word for one row's producer.</summary>
        public static string SourceWord(string source)
        {
            if (string.Equals(source, HarvestOverflowModal.CollectorSource, StringComparison.Ordinal)) return "collectors";
            if (!string.IsNullOrEmpty(source) && source.IndexOf("DumpSilos", StringComparison.Ordinal) >= 0) return "silo";
            return "burned";
        }

        /// <summary>Grouped figure, ASCII-safe. InvariantCulture on purpose: a device locale can
        /// otherwise group with U+00A0 (narrow no-break space), which renders as tofu.</summary>
        public static string N(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        /// <summary>Every player-facing string this VM produced, in reading order - the one seam
        /// an oracle uses to sweep for ASCII, for a repeated sentence, or for line count.</summary>
        public string AllText()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Rows.Count; i++)
            {
                var r = Rows[i];
                if (r == null) continue;
                sb.Append(r.ResourceName).Append('\n')
                  .Append(r.BankedText).Append('\n')
                  .Append(r.StorageText).Append('\n');
                if (!string.IsNullOrEmpty(r.WaitingText)) sb.Append(r.WaitingText).Append('\n');
                if (r.HasAction) sb.Append(r.ActionText).Append('\n');
            }
            if (!string.IsNullOrEmpty(OverflowLine)) sb.Append(OverflowLine).Append('\n');
            if (!string.IsNullOrEmpty(FooterLine)) sb.Append(FooterLine);
            return sb.ToString();
        }
    }
}
