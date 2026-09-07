// =============================================================================
// DefenseReportChipModel - WO-1515 sec.2B/2D: the town HUD's ATTACK REPORT chip,
// decided in Core so the View can only paint what this returns.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.HudModel
//
// THE OWNER RULING (2026-09-06 20:05, verbatim): "the only way to get to the
// defense report is buried under settings then realm. should be on screen as a
// button if there is a report that is incoming".
//
// So the chip is CONDITIONAL, never furniture: it exists ONLY while
// DefenseReportLedger.UnreadCount() > 0 and it must not render at all - not
// greyed, not empty-stated - when that count is zero (WO-1515 sec.3: a permanent
// chip is a fifth status glance competing with the four that earn their place).
//
// WHY THE COMPOSE/CURRENT SPLIT: Compose is PURE - every branch is drivable from a
// fixture, so DefenseReportLayoutRegression [chip-gate] can assert the visibility
// rule and the caption words without a GameState, a save file or a running HUD.
// Current is the one place that reads the ledger. The View (HudKitController)
// calls Current on a throttled tick and relays the strings; it decides nothing.
// This is the BuildersChipCopy / HeartObjectiveCopy shape, for the same reason.
//
// COLOURBLIND LAW (the owner is red/green colourblind): the outcome is a WORD -
// HELD / BREACHED / OVERRUN - never a tint. A greyscale capture of this chip loses
// nothing.
//
// ASCII ONLY - the mobile font atlas has no glyphs past U+007E.
// =============================================================================

using DeNelle.Core.Defense;

namespace DeNelle.Core.HudModel
{
    /// <summary>What the ATTACK REPORT chip shows this tick. A value, not a handle.</summary>
    public struct DefenseReportChipSnapshot
    {
        /// <summary>True only while at least one retained report is unread.</summary>
        public bool Visible;

        /// <summary>The chip face. Two lines: the title, then the outcome WORD.
        /// Empty whenever <see cref="Visible"/> is false - there is no "off" caption,
        /// because there is no off chip.</summary>
        public string Caption;

        /// <summary>How many retained reports the player has not opened.</summary>
        public int UnreadCount;

        /// <summary>Cheap repaint key. Moves whenever the chip's face or presence changes,
        /// so a View can compare an int instead of re-deriving a string every frame.</summary>
        public int Key;
    }

    /// <summary>The chip's ONE authority. Pure composition plus one ledger read.</summary>
    public static class DefenseReportChipModel
    {
        /// <summary>Line 1 of the chip face. The player's word for the screen it opens.</summary>
        public const string TitleLine = "ATTACK REPORT";

        /// <summary>
        /// THE outcome word. Moved here from DefenseReportPanel's private copy so the chip
        /// and the panel can never disagree about what a record is called - a fact written
        /// twice is this repo's dominant failure mode (CLAUDE.md sec.2/sec.5/sec.16).
        /// </summary>
        public static string OutcomeWord(DefenseOutcome o)
        {
            switch (o)
            {
                case DefenseOutcome.Overrun: return "OVERRUN";
                case DefenseOutcome.Breached: return "BREACHED";
                default: return "HELD";
            }
        }

        /// <summary>
        /// PURE. Given how many reports are unread and the outcome of the NEWEST unread one,
        /// returns exactly what the chip shows. No statics are read, so every branch is a
        /// fixture in the suite.
        /// </summary>
        /// <param name="unreadCount">DefenseReportLedger.UnreadCount().</param>
        /// <param name="newestUnread">Outcome of the newest unread record. Ignored when the
        /// count is zero.</param>
        public static DefenseReportChipSnapshot Compose(int unreadCount, DefenseOutcome newestUnread)
        {
            var snap = new DefenseReportChipSnapshot();
            snap.UnreadCount = unreadCount < 0 ? 0 : unreadCount;
            snap.Visible = snap.UnreadCount > 0;
            // No caption when there is no chip. An "off" string is how a conditional chip
            // quietly becomes a permanent one.
            snap.Caption = snap.Visible
                ? TitleLine + "\n" + OutcomeWord(newestUnread)
                : string.Empty;
            // The key must move on BOTH axes the face depends on: presence and outcome.
            // (count * 8) leaves room for every DefenseOutcome value; +1 keeps the visible
            // key clear of the invisible 0.
            snap.Key = snap.Visible ? (snap.UnreadCount * 8) + ((int)newestUnread) + 1 : 0;
            return snap;
        }

        /// <summary>
        /// The live snapshot. The ONE ledger read - and it walks the retained ring newest
        /// first for the first unread record, so the word on the chip is the word on the
        /// report the player is about to land on.
        /// </summary>
        public static DefenseReportChipSnapshot Current
        {
            get
            {
                int unread = DefenseReportLedger.UnreadCount();
                if (unread <= 0) return Compose(0, DefenseOutcome.Held);

                var newestFirst = DefenseReportLedger.NewestFirst();
                for (int i = 0; i < newestFirst.Count; i++)
                {
                    var r = newestFirst[i];
                    if (r != null && !r.Read) return Compose(unread, r.Outcome);
                }
                // UnreadCount said there was one and the walk found none. That is a real
                // disagreement inside one authority, not a rounding error - fall back to the
                // safe visible face rather than dropping the door the owner asked for.
                return Compose(unread, DefenseOutcome.Held);
            }
        }
    }
}
