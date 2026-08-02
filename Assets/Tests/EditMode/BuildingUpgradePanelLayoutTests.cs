// =============================================================================
// BuildingUpgradePanelLayoutTests (EditMode) - WO-841 + WO-832 S4 invariants.
// -----------------------------------------------------------------------------
// Pins two classes of regression on the Building Enhancements panel:
//   1. WO-841 countdown tick FORMATTING - the build-time CTA text and the
//      per-second Update() tick both route through the ONE FormatCountdown
//      composer; the string must stay byte-stable (a drifting string would make
//      the first live tick visibly restyle the button) and pure ASCII.
//   2. WO-832 S4 TRUNCATION-PROOF band constants - every fixed-pixel text band
//      must hold a whole number of kit FontFloor line boxes (the RumorBoard TMP
//      vertical-cull lesson 2026-08-02: a band shorter than its line count lets
//      TMP truncate mid-word - "...Structu", "Unlock 'Re"). Pure const math, no
//      scene needed.
// =============================================================================

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

        // -- WO-841 countdown formatting --------------------------------------

        [Test]
        public void countdown_format_is_byte_stable()
        {
            Assert.That(BuildingUpgradePanelMvvm.FormatCountdown(57), Is.EqualTo("Under construction - 57s"));
            Assert.That(BuildingUpgradePanelMvvm.FormatCountdown(0), Is.EqualTo("Under construction - 0s"));
            Assert.That(BuildingUpgradePanelMvvm.FormatCountdown(599), Is.EqualTo("Under construction - 599s"));
        }

        [Test]
        public void countdown_format_clamps_negative_to_zero()
        {
            // A completion race can momentarily read a negative remainder - the label
            // must never show "-1s".
            Assert.That(BuildingUpgradePanelMvvm.FormatCountdown(-5), Is.EqualTo("Under construction - 0s"));
        }

        [Test]
        public void countdown_format_is_pure_ascii()
        {
            string s = BuildingUpgradePanelMvvm.FormatCountdown(123);
            foreach (char ch in s)
                Assert.That((int)ch, Is.LessThan(128),
                    "countdown text must be ASCII-only (non-ASCII glyph found: '" + ch + "')");
        }

        [Test]
        public void countdown_ticks_produce_distinct_text_per_second()
        {
            // The Update() tick only assigns when the whole second changes - adjacent
            // seconds must therefore render distinct strings (else the tick is invisible).
            Assert.That(BuildingUpgradePanelMvvm.FormatCountdown(10),
                Is.Not.EqualTo(BuildingUpgradePanelMvvm.FormatCountdown(9)));
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
        public void card_text_bands_hold_their_line_counts()
        {
            // Head: one fitted line. Name: two wrapped lines. Effect: three wrapped
            // lines ("Wood production +12%. Structural HP +25%" needs 3 at card width).
            // Footer: two wrapped lines ("Unlock 'Reinforced Blades' to open Tier 3").
            Assert.That(BuildingUpgradePanelMvvm.CardHeadBandPx, Is.GreaterThanOrEqualTo(FloorLine));
            Assert.That(BuildingUpgradePanelMvvm.CardNameBandPx, Is.GreaterThanOrEqualTo(FloorLine * 2f));
            Assert.That(BuildingUpgradePanelMvvm.CardEffectBandPx, Is.GreaterThanOrEqualTo(FloorLine * 3f));
            Assert.That(BuildingUpgradePanelMvvm.CardFooterBandPx, Is.GreaterThanOrEqualTo(FloorLine * 2f));
        }

        [Test]
        public void benefit_rows_hold_their_line_counts()
        {
            // 1-line rows seat one floor line; 2-line rows ("Opens Reinforced Blades
            // (Wood production +25%)") seat two - the exact mid-word-cut regression.
            Assert.That(BuildingUpgradePanelMvvm.BenefitRow1Px, Is.GreaterThanOrEqualTo(FloorLine));
            Assert.That(BuildingUpgradePanelMvvm.BenefitRow2Px, Is.GreaterThanOrEqualTo(FloorLine * 2f));
            Assert.That(BuildingUpgradePanelMvvm.BenefitSingleLineChars, Is.GreaterThan(0));
        }

        [Test]
        public void detail_cta_band_meets_touch_floor_and_two_lines()
        {
            // The CTA band height IS the kit touch floor - which must in turn hold the
            // two floor lines a wrapped busy/lock reason needs ("Under construction -
            // 599s" / "Not enough resources yet" at detail-pane width).
            Assert.That(ElarionUiKit.MinTouchPx, Is.GreaterThanOrEqualTo(FloorLine * 2f));
            Assert.That(BuildingUpgradePanelMvvm.CtaBottomPx, Is.GreaterThanOrEqualTo(0f));
        }
    }
}
