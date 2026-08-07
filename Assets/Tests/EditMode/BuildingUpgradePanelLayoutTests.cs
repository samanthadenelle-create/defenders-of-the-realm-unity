// =============================================================================
// BuildingUpgradePanelLayoutTests (EditMode) - WO-895 + WO-841 + WO-832 S4.
// -----------------------------------------------------------------------------
// Pins three classes of regression on the Building Enhancements panel:
//   1. WO-895 ACTION-BUTTON LABELS - the one true button's four player states
//      must render DISTINCT, pure-ASCII text. That text is the primary
//      colour-free signal (the owner is red/green colourblind), so a collision
//      between two states' labels is a hard failure, not cosmetics. Build-time
//      and the per-second Update() tick both route through the ONE
//      FormatActionLabel composer, so the string must stay byte-stable.
//   2. WO-841 countdown FORMATTING - now M:SS (ASCII colon, zero-padded
//      seconds), clamped at zero so a completion race never shows "-1".
//   3. WO-832 S4 TRUNCATION-PROOF band constants - every fixed-pixel text band
//      must hold a whole number of kit FontFloor line boxes (the RumorBoard TMP
//      vertical-cull lesson 2026-08-02: a band shorter than its line count lets
//      TMP truncate mid-word - "...Structu", "Unlock 'Re"). Pure const math, no
//      scene needed.
// The 6-tier card rail this suite used to pin is GONE (WO-895) - the panel is a
// progress strip + ONE next-upgrade card, so the old Card*/Benefit* band
// constants were retired with it.
// =============================================================================

using System.Collections.Generic;
using NUnit.Framework;
using DeNelle.Core.UI;
using DeNelle.Village.Buildings.Progression;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class BuildingUpgradePanelLayoutTests
    {
        // One floor-size TMP line box (the same derivation the panel's bands use).
        private const float FloorLine = ElarionUiKit.FontFloor * 1.25f;

        // -- WO-841 / WO-895 countdown formatting ------------------------------

        [Test]
        public void countdown_format_is_m_ss_and_byte_stable()
        {
            Assert.That(BuildingUpgradePanelMvvm.FormatMinutesSeconds(57), Is.EqualTo("0:57"));
            Assert.That(BuildingUpgradePanelMvvm.FormatMinutesSeconds(7), Is.EqualTo("0:07"));
            Assert.That(BuildingUpgradePanelMvvm.FormatMinutesSeconds(0), Is.EqualTo("0:00"));
            Assert.That(BuildingUpgradePanelMvvm.FormatMinutesSeconds(60), Is.EqualTo("1:00"));
            Assert.That(BuildingUpgradePanelMvvm.FormatMinutesSeconds(599), Is.EqualTo("9:59"));
            // Hours roll into the minutes field - the shape never changes on a long job.
            Assert.That(BuildingUpgradePanelMvvm.FormatMinutesSeconds(3723), Is.EqualTo("62:03"));
        }

        [Test]
        public void countdown_format_clamps_negative_to_zero()
        {
            // A completion race can momentarily read a negative remainder - the label
            // must never show "-1".
            Assert.That(BuildingUpgradePanelMvvm.FormatMinutesSeconds(-5), Is.EqualTo("0:00"));
        }

        [Test]
        public void countdown_ticks_produce_distinct_text_per_second()
        {
            // The Update() tick only assigns when the whole second changes - adjacent
            // seconds must therefore render distinct strings (else the tick is invisible).
            Assert.That(BuildingUpgradePanelMvvm.FormatMinutesSeconds(10),
                Is.Not.EqualTo(BuildingUpgradePanelMvvm.FormatMinutesSeconds(9)));
        }

        // -- WO-895 the one true button's state machine ------------------------

        [Test]
        public void every_action_label_is_pure_ascii()
        {
            // TMP renders non-ASCII as tofu boxes (CLAUDE.md S7). Every state, plus the
            // M:SS countdown and a real tier name, must stay inside ASCII.
            foreach (UpgradeActionState state in System.Enum.GetValues(typeof(UpgradeActionState)))
            {
                string s = BuildingUpgradePanelMvvm.FormatActionLabel(state, 125, "Drill Yard");
                foreach (char ch in s)
                    Assert.That((int)ch, Is.LessThan(128),
                        "button text must be ASCII-only (state " + state + ", glyph '" + ch + "')");
            }
        }

        [Test]
        public void the_four_player_states_read_differently_without_colour()
        {
            // THE colourblind-safety invariant: with hue removed, the ONLY thing telling
            // Ready / Missing resources / Queued / In progress apart is their TEXT. Two
            // states sharing a label would be indistinguishable to the owner.
            var labels = new List<string>
            {
                BuildingUpgradePanelMvvm.FormatActionLabel(UpgradeActionState.Ready, 0, "Drill Yard"),
                BuildingUpgradePanelMvvm.FormatActionLabel(UpgradeActionState.MissingResources, 0, "Drill Yard"),
                BuildingUpgradePanelMvvm.FormatActionLabel(UpgradeActionState.Queued, 0, "Drill Yard"),
                BuildingUpgradePanelMvvm.FormatActionLabel(UpgradeActionState.InProgress, 125, "Drill Yard"),
            };
            CollectionAssert.AllItemsAreUnique(labels);
            foreach (var l in labels)
                Assert.That(string.IsNullOrWhiteSpace(l), Is.False, "no state may render a blank button");
        }

        [Test]
        public void ready_label_names_the_next_tier()
        {
            // "what they can get to next" (owner ruling) - the Ready button says WHERE it goes.
            Assert.That(BuildingUpgradePanelMvvm.FormatActionLabel(UpgradeActionState.Ready, 0, "Drill Yard"),
                Is.EqualTo("Upgrade to Drill Yard"));
            // An unnamed tier degrades to a plain verb, never to "Upgrade to ".
            Assert.That(BuildingUpgradePanelMvvm.FormatActionLabel(UpgradeActionState.Ready, 0, null),
                Is.EqualTo("Upgrade"));
        }

        [Test]
        public void in_progress_label_carries_the_live_countdown()
        {
            Assert.That(BuildingUpgradePanelMvvm.FormatActionLabel(UpgradeActionState.InProgress, 125, "Drill Yard"),
                Is.EqualTo("In progress - 2:05"));
            // The tick must visibly change the label second to second.
            Assert.That(BuildingUpgradePanelMvvm.FormatActionLabel(UpgradeActionState.InProgress, 125, null),
                Is.Not.EqualTo(BuildingUpgradePanelMvvm.FormatActionLabel(UpgradeActionState.InProgress, 124, null)));
        }

        [Test]
        public void queued_and_missing_labels_say_what_is_wrong_in_words()
        {
            // Not a colour, not a bare disabled button - words.
            Assert.That(BuildingUpgradePanelMvvm.FormatActionLabel(UpgradeActionState.MissingResources, 0, "X"),
                Is.EqualTo("Missing resources"));
            Assert.That(BuildingUpgradePanelMvvm.FormatActionLabel(UpgradeActionState.Queued, 0, "X"),
                Does.StartWith("Queued"));
        }

        // -- WO-832 S4 truncation-proof band constants ------------------------

        [Test]
        public void floor_line_box_holds_the_kit_font_floor()
        {
            Assert.That(BuildingUpgradePanelMvvm.FloorLinePx, Is.GreaterThanOrEqualTo(FloorLine));
            // Never sub-floor: a line box below the hard floor's line would mean
            // sub-legible phone text got authored back in.
            Assert.That(BuildingUpgradePanelMvvm.FloorLinePx,
                Is.GreaterThan(ElarionUiKit.FontHardFloor));
        }

        [Test]
        public void next_card_bands_hold_their_line_counts()
        {
            // Progress strip: one label line (the segment bar rides inside it). Header:
            // kicker line + name line. Description: one composed sentence. Cost: caption +
            // chips share one band. Each must still seat its floor-size line box.
            Assert.That(BuildingUpgradePanelMvvm.ProgressStripPx, Is.GreaterThanOrEqualTo(FloorLine));
            Assert.That(BuildingUpgradePanelMvvm.NextHeaderBandPx, Is.GreaterThanOrEqualTo(FloorLine * 2f));
            Assert.That(BuildingUpgradePanelMvvm.NextDescBandPx, Is.GreaterThanOrEqualTo(FloorLine));
            Assert.That(BuildingUpgradePanelMvvm.CostBandPx, Is.GreaterThanOrEqualTo(FloorLine));
        }

        [Test]
        public void the_bonus_list_keeps_a_real_three_row_zone()
        {
            // THE budget invariant (WO-895): the bonuses ARE the answer to "what do I get
            // next", so the card's FIXED bands must never grow to the point where the flex
            // zone cannot seat three short rows. Landscape reference: the Upgrade body is
            // ~0.61 of a panel that is ~0.9 of the 1080-tall canvas.
            const float bodyPx = 0.61f * 0.9f * 1080f;
            float cardPx = bodyPx - BuildingUpgradePanelMvvm.ProgressStripPx
                                  - BuildingUpgradePanelMvvm.BandGapPx * 2f;
            float fixedPx = BuildingUpgradePanelMvvm.BandGapPx * 3f
                          + BuildingUpgradePanelMvvm.NextHeaderBandPx
                          + BuildingUpgradePanelMvvm.NextDescBandPx
                          + BuildingUpgradePanelMvvm.CostGapPx
                          + BuildingUpgradePanelMvvm.CostBandPx
                          + BuildingUpgradePanelMvvm.ActionBottomPx
                          + BuildingUpgradePanelMvvm.ActionBandPx;
            float bonusZonePx = cardPx - fixedPx;
            float threeRows = BuildingUpgradePanelMvvm.BonusRow1Px * 3f
                            + BuildingUpgradePanelMvvm.BandGapPx;   // 2 half-gaps
            Assert.That(bonusZonePx, Is.GreaterThanOrEqualTo(threeRows),
                "the next-upgrade card's fixed bands starved the bonus list (" + bonusZonePx
                + "px for " + threeRows + "px of rows) - a fatter header silently drops bonuses");
        }

        [Test]
        public void bonus_rows_hold_their_line_counts()
        {
            // 1-line rows seat one floor line; 2-line rows ("Unlocks the Spearman troop and
            // opens recruitment drills") seat two - the exact mid-word-cut regression.
            Assert.That(BuildingUpgradePanelMvvm.BonusRow1Px, Is.GreaterThanOrEqualTo(FloorLine));
            Assert.That(BuildingUpgradePanelMvvm.BonusRow2Px, Is.GreaterThanOrEqualTo(FloorLine * 2f));
            Assert.That(BuildingUpgradePanelMvvm.BonusSingleLineChars, Is.GreaterThan(0));
            Assert.That(BuildingUpgradePanelMvvm.BonusMaxRows, Is.GreaterThan(0));
        }

        [Test]
        public void action_band_meets_the_cta_canon_and_touch_floor()
        {
            // The one true button is the kit's canonical CTA height, which must clear the
            // touch floor AND seat the two floor lines a wrapped state label can need.
            Assert.That(BuildingUpgradePanelMvvm.ActionBandPx, Is.EqualTo(ElarionUiKit.CanonCtaHeight));
            Assert.That(BuildingUpgradePanelMvvm.ActionBandPx, Is.GreaterThanOrEqualTo(ElarionUiKit.MinTouchPx));
            Assert.That(BuildingUpgradePanelMvvm.ActionBandPx, Is.GreaterThanOrEqualTo(FloorLine * 2f));
            Assert.That(BuildingUpgradePanelMvvm.ActionBottomPx, Is.GreaterThanOrEqualTo(0f));
            // The in-progress fill bar must be visible but must never eat the label band.
            Assert.That(BuildingUpgradePanelMvvm.ActionFillPx, Is.GreaterThan(0f));
            Assert.That(BuildingUpgradePanelMvvm.ActionFillPx, Is.LessThan(FloorLine));
        }
    }
}
