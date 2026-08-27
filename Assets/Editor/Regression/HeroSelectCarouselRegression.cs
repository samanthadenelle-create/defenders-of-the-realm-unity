// =============================================================================
// HeroSelectCarouselRegression [hero-select-carousel] (WO-1248)
// Markers: HERO_SELECT_CAROUSEL_OK / HERO_SELECT_CAROUSEL_FAIL
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Wired into DataRegression.RunAll.
//
// WHAT WAS CAPTURED (owner, on device, 2026-08-27): the hero-select carousel
// rotate control rendered "Pr..." instead of "Previous", and the control itself
// was not a usable one-handed rotate affordance.
//
// CAUSE CLASS (local width + a shared recipe, not a copy typo):
//   * LOCAL: the rotate lanes were 0.068 of the stage well (Prev 0.148-0.216).
//     At portrait 1080x1920 that plate is ~63 ref px; BuildObsidianButton insets
//     its label to x 0.04..0.96, leaving ~58 px.
//   * SHARED: ElarionUiKit.BuildObsidianButton always arms FitSingleLine
//     (NoWrap + Ellipsis, autosize down to FontFloor). Any word that cannot fit
//     that floor becomes "Pr...". This is the same truncation class as WO-1245
//     (banner) and PROD-014 (repair toast). The recipe is correct for a CTA that
//     was authored wide enough; it is the wrong control for a narrow directional
//     plate. This suite does NOT change the kit. It asserts hero-select no
//     longer FEEDS that recipe a word the plate cannot hold.
//
// ==========================  HOW THIS SUITE IS HONEST  =======================
//   * THE WIDTH IS MEASURED. ElarionUiKit.MeasureLineWidthPx sums the real TMP
//     font's per-glyph advances at the size the player sees (FontMicro for the
//     word, FontHead for the chevron; FontFloor for the historical case).
//   * THE BOX IS PINNED BY SOURCE LINT. Every fraction below is a literal this
//     suite also asserts is still in HeroSelectController.cs. Narrow a lane and
//     the lint fails rather than the oracle silently following it down.
//   * FOUR SURFACES. 2670x1200 (Seeker landscape), 1920x1080, 1080x1920
//     (portrait), 1200x2670. A label that fits at one aspect can still cut at
//     another, which is the whole reason the WO named both.
//   * WO-1138 RATCHET. CaseHistoricalIsRed runs the SAME fit predicate over the
//     PRE-FIX 0.068 lane against the word "Previous" and FAILS THE SUITE IF THAT
//     GEOMETRY PASSES. So the oracle proves it would have gone red on today's
//     truncated layout, every run, in-process.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DeNelle.Core.UI;

namespace DeNelle.Editor.Regression
{
    public static class HeroSelectCarouselRegression
    {
        private const string Src = "Assets/_Modules/Onboarding/HeroSelectController.cs";
        private const string KitSrc = "Assets/_Modules/Core/UI/ElarionUiKitObsidian.cs";

        // ── the LIVE box, pinned by Case 0 ──────────────────────────────────
        private const float PanelX0 = 0.015f, PanelX1 = 0.985f;
        private const float PanelY0 = 0.02f,  PanelY1 = 0.98f;
        private const float WellXMin = 0.055f, WellXMax = 0.945f;
        private const float WellYMin = 0.075f, WellYMax = 0.835f;
        private const float CarouselYMin = 0.500f;
        private const float PrevXMin = 0.012f, PrevXMax = 0.148f;
        private const float NextXMin = 0.852f, NextXMax = 0.988f;
        private const float SideLXMin = 0.2591f, SideRXMax = 0.7409f;
        private const float CarArrowYMin = 0.42f, CarArrowYMax = 0.90f;
        private const float RotateWordX0 = 0.06f, RotateWordX1 = 0.94f;
        private const float RotateWordYMin = 0.04f, RotateWordYMax = 0.32f;
        private const float RotateChevronYMin = 0.34f, RotateChevronYMax = 0.96f;
        private const string PrevWord = "PREV";
        private const string NextWord = "NEXT";
        private const string PrevChevron = "<<";
        private const string NextChevron = ">>";

        // ── the PRE-FIX box (WO-1138 ratchet). Quoted from HeroSelectController
        //    before WO-1248: PrevXMin = 0.148f, PrevXMax = 0.216f, kit inset 0.04-0.96.
        private const float OldPrevXMin = 0.148f, OldPrevXMax = 0.216f;
        private const float KitButtonLabelInset = 0.92f;
        private const string OwnerWord = "Previous";
        private const string OldKitLabel = "< PREV";

        private const float LineHeightFactor = 1.2f;

        private struct Aspect { public string Name; public float W, H; }
        private static readonly Aspect[] Aspects =
        {
            new Aspect { Name = "2670x1200 (Seeker landscape)", W = 2670f, H = 1200f },
            new Aspect { Name = "1920x1080",                    W = 1920f, H = 1080f },
            new Aspect { Name = "1080x1920 (portrait)",         W = 1080f, H = 1920f },
            new Aspect { Name = "1200x2670 (Seeker portrait)",  W = 1200f, H = 2670f },
        };

        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("HERO_SELECT_CAROUSEL_OK - " + reason);
            else Debug.LogError("HERO_SELECT_CAROUSEL_FAIL: " + reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                Case(failures, "boxes-pinned",      () => Case0_BoxesStillAuthored(failures, notes));
                Case(failures, "recipe-class",      () => Case1_NoLongerFeedsKitEllipsis(failures, notes));
                Case(failures, "label-fits",        () => Case2_DesignedCopyFits(failures, notes));
                Case(failures, "touch-floor",       () => Case3_TouchFloor(failures, notes));
                Case(failures, "historical-is-red", () => Case4_HistoricalIsRed(failures, notes));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes.ToArray()) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "HERO SELECT CAROUSEL OK - designed PREV/NEXT + chevron MEASURES inside its " +
                         "plate at the authored size on four surfaces, both rotate plates clear " +
                         "MinTouchPx(" + ElarionUiKit.MinTouchPx.ToString("0") + ") as authored, and " +
                         "the pre-fix 0.068-lane + 'Previous' geometry still measures RED" + noteStr;
                return true;
            }
            reason = "hero-select-carousel FAIL x" + failures.Count + ": " +
                     string.Join(" | ", failures.ToArray()) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  CASE 0 - the numbers this suite measures against are still in the file
        // =====================================================================
        private static void Case0_BoxesStillAuthored(List<string> failures, List<string> notes)
        {
            string src = ReadSrc(Src);
            if (src == null) { failures.Add("[boxes-pinned] cannot read " + Src); return; }

            RequireLiteral(failures, src, "private const float PrevXMin = 0.012f, PrevXMax = 0.148f;",
                "the rotate-prev lane this suite measures");
            RequireLiteral(failures, src, "private const float NextXMin = 0.852f, NextXMax = 0.988f;",
                "the rotate-next lane this suite measures");
            RequireLiteral(failures, src, "private const float CarArrowYMin = 0.42f, CarArrowYMax = 0.90f;",
                "the rotate plate height");
            RequireLiteral(failures, src, "private const string RotatePrevWord = \"PREV\";",
                "the designed prev word");
            RequireLiteral(failures, src, "private const string RotateNextWord = \"NEXT\";",
                "the designed next word");
            RequireLiteral(failures, src, "private const string RotatePrevChevron = \"<<\";",
                "the designed prev chevron");
            RequireLiteral(failures, src, "private const string RotateNextChevron = \">>\";",
                "the designed next chevron");
            RequireLiteral(failures, src, "private const float RotateWordX0 = 0.06f, RotateWordX1 = 0.94f;",
                "the word/chevron x inset");
            RequireLiteral(failures, src, "new Vector2(0.015f, 0.02f), new Vector2(0.985f, 0.98f)",
                "the panel anchors the well is a fraction of");
            RequireLiteral(failures, src, "private const float WellXMin = 0.055f, WellXMax = 0.945f;",
                "the well x the rotate lanes are a fraction of");
            RequireLiteral(failures, src, "private const float WellYMin = 0.075f, WellYMax = 0.835f;",
                "the well y the carousel band is a fraction of");
            RequireLiteral(failures, src, "private const float CarouselYMin   = 0.500f;",
                "the carousel band floor");

            if (PrevXMax >= SideLXMin)
                failures.Add("[boxes-pinned] PrevXMax (" + PrevXMax + ") reaches SideLXMin (" +
                             SideLXMin + ") - the rotate plate would collide with the left side card");
            if (NextXMin <= SideRXMax)
                failures.Add("[boxes-pinned] NextXMin (" + NextXMin + ") reaches SideRXMax (" +
                             SideRXMax + ") - the rotate plate would collide with the right side card");

            notes.Add("rotate lanes x[" + PrevXMin.ToString("0.000") + "," + PrevXMax.ToString("0.000") +
                      "] / [" + NextXMin.ToString("0.000") + "," + NextXMax.ToString("0.000") +
                      "], word inset " + ((RotateWordX1 - RotateWordX0) * 100f).ToString("0") + "%");
        }

        // =====================================================================
        //  CASE 1 - the CLASS of truncation is no longer fed this control
        // =====================================================================
        private static void Case1_NoLongerFeedsKitEllipsis(List<string> failures, List<string> notes)
        {
            string src = ReadSrc(Src);
            if (src == null) { failures.Add("[recipe-class] cannot read " + Src); return; }

            if (src.IndexOf("BuildObsidianButton(_classColumn, \"< PREV\"", StringComparison.Ordinal) >= 0)
                failures.Add("[recipe-class] HeroSelectController still passes \"< PREV\" as a kit " +
                             "button label. That string is the workaround the WO forbade AND a TMP " +
                             "rich-text landmine ('<' starts a tag). The rotate control is ICON+word, " +
                             "not a truncated Previous");
            if (src.IndexOf("BuildObsidianButton(_classColumn, \"NEXT >\"", StringComparison.Ordinal) >= 0)
                failures.Add("[recipe-class] HeroSelectController still passes \"NEXT >\" as a kit " +
                             "button label - the other half of the truncated word-button pair");
            if (src.IndexOf("BuildRotateControl", StringComparison.Ordinal) < 0)
                failures.Add("[recipe-class] BuildRotateControl is gone - the designed ICON+word " +
                             "control this suite measures against is no longer the thing that builds");
            if (src.IndexOf("ArmRotateGlyph", StringComparison.Ordinal) < 0)
                failures.Add("[recipe-class] ArmRotateGlyph is gone - without it the rotate labels " +
                             "fall back to FitLine / FitSingleLine and 'PREV' becomes 'Pr...' again");
            if (src.IndexOf("t.richText = false", StringComparison.Ordinal) < 0)
                failures.Add("[recipe-class] rotate glyphs no longer disable TMP richText - a " +
                             "chevron starting with '<' is parsed as a tag");
            if (src.IndexOf("t.overflowMode = TextOverflowModes.Overflow", StringComparison.Ordinal) < 0)
                failures.Add("[recipe-class] rotate glyphs no longer use Overflow - Ellipsis is the " +
                             "silent 'Pr...' path. A miss must be visible, not truncated");
            if (src.IndexOf("t.enableAutoSizing = false", StringComparison.Ordinal) < 0)
                failures.Add("[recipe-class] rotate glyphs autosize again - the geometry oracle " +
                             "would be measuring a different size than the player sees");

            string kit = ReadSrc(KitSrc);
            if (kit != null && kit.IndexOf("FitSingleLine(tt)", StringComparison.Ordinal) < 0)
                notes.Add("kit BuildObsidianButton no longer arms FitSingleLine on the constructed " +
                          "path - the shared-recipe half of the class may have moved; re-read");
        }

        // =====================================================================
        //  CASE 2 - the designed copy MEASURES inside its plate
        // =====================================================================
        private static void Case2_DesignedCopyFits(List<string> failures, List<string> notes)
        {
            float wordSize = ElarionUi.FontMicro;
            float chevronSize = ElarionUi.FontHead;
            string[] words = { PrevWord, NextWord };
            string[] chevrons = { PrevChevron, NextChevron };

            foreach (var a in Aspects)
            {
                float btnW, btnH, labelW, wordH, chevronH;
                RotateBox(a, out btnW, out btnH, out labelW, out wordH, out chevronH);

                foreach (string word in words)
                {
                    AssertFits(failures, a.Name, "word", word, wordSize, labelW);
                    float needed = wordSize * LineHeightFactor;
                    if (needed > wordH + 0.5f)
                        failures.Add("[label-fits] at " + a.Name + " the word '" + word + "' needs " +
                                     needed.ToString("0.0") + " ref px of height at FontMicro(" +
                                     wordSize.ToString("0") + ") but the word band is only " +
                                     wordH.ToString("0.0") + " px - the line would clip");
                }
                foreach (string ch in chevrons)
                {
                    AssertFits(failures, a.Name, "chevron", ch, chevronSize, labelW);
                    float needed = chevronSize * LineHeightFactor;
                    if (needed > chevronH + 0.5f)
                        failures.Add("[label-fits] at " + a.Name + " the chevron '" + ch + "' needs " +
                                     needed.ToString("0.0") + " ref px of height at FontHead(" +
                                     chevronSize.ToString("0") + ") but the chevron band is only " +
                                     chevronH.ToString("0.0") + " px");
                }
                notes.Add("rotate plate at " + a.Name + ": " + btnW.ToString("0") + "x" +
                          btnH.ToString("0") + " ref px, label " + labelW.ToString("0") + " px");
            }
        }

        // =====================================================================
        //  CASE 3 - authored size >= MinTouchPx, so ClampMinTouch is a no-op
        // =====================================================================
        private static void Case3_TouchFloor(List<string> failures, List<string> notes)
        {
            float floor = ElarionUiKit.MinTouchPx;
            foreach (var a in Aspects)
            {
                float btnW, btnH, labelW, wordH, chevronH;
                RotateBox(a, out btnW, out btnH, out labelW, out wordH, out chevronH);
                float shortest = Mathf.Min(btnW, btnH);
                if (shortest + 0.5f < floor)
                    failures.Add("[touch-floor] at " + a.Name + " the rotate plate is " +
                                 btnW.ToString("0.0") + "x" + btnH.ToString("0.0") +
                                 " ref px; shortest side " + shortest.ToString("0.0") +
                                 " is under MinTouchPx(" + floor.ToString("0") +
                                 "). ClampMinTouch would grow it into a neighbour - that is how " +
                                 "the old arrows ate the side cards");
                else
                    notes.Add("touch at " + a.Name + ": shortest " + shortest.ToString("0") +
                              " >= " + floor.ToString("0"));
            }
        }

        // =====================================================================
        //  CASE 4 - the PRE-FIX geometry is still RED (WO-1138)
        // =====================================================================
        private static void Case4_HistoricalIsRed(List<string> failures, List<string> notes)
        {
            // Portrait is the smoking gun: 0.068 of the well * 0.92 kit inset leaves a
            // label rect "Previous" cannot occupy at FontFloor, which is exactly
            // FitSingleLine's last step before Ellipsis paints "Pr...".
            var portrait = Aspects[2];
            float oldLabelW = OldLabelBoxW(portrait);
            string detail;
            float prevW = ElarionUiKit.MeasureLineWidthPx(ElarionUiKit.FontRole.Body, OwnerWord,
                                                          ElarionUiKit.FontFloor, out detail);
            if (prevW < 0f)
            {
                failures.Add("[historical-is-red] cannot measure '" + OwnerWord + "': " + detail +
                             " - the ratchet cannot prove it would have failed");
                return;
            }

            if (prevW <= oldLabelW)
                failures.Add("[historical-is-red] '" + OwnerWord + "' MEASURES " + prevW.ToString("0.0") +
                             " ref px at FontFloor(" + ElarionUiKit.FontFloor.ToString("0") +
                             ") and FITS the pre-fix portrait label box of " + oldLabelW.ToString("0.0") +
                             " px (" + detail + "). The truncation predicate cannot fail, so a green " +
                             "run of this suite would prove nothing. The captured defect was '" +
                             OwnerWord + "' -> 'Pr...' in that box");
            else
                notes.Add("historical RED: '" + OwnerWord + "' is " + prevW.ToString("0.0") +
                          " px vs old portrait box " + oldLabelW.ToString("0.0") + " px - " +
                          (prevW - oldLabelW).ToString("0.0") + " px over, which is the captured 'Pr...'");

            // Same predicate on the kit label that actually shipped ("< PREV") - if THAT
            // also overflows the old box, say so; if it fits, the truncation the owner
            // saw was of "Previous", not of the workaround abbreviation.
            float oldKitW = ElarionUiKit.MeasureLineWidthPx(ElarionUiKit.FontRole.Body, OldKitLabel,
                                                            ElarionUiKit.FontFloor, out string kitDetail);
            if (oldKitW >= 0f)
            {
                notes.Add("historical '" + OldKitLabel + "' is " + oldKitW.ToString("0.0") +
                          " px vs old portrait box " + oldLabelW.ToString("0.0") + " px (" +
                          (oldKitW > oldLabelW ? "also overflows" : "would have fit at the floor") +
                          "; " + kitDetail + ")");
            }

            // Landscape Seeker at FontBody (the size BuildObsidianButton STARTS at, before
            // autosize). If "Previous" fits even there, the Seeker report would have been
            // a different defect; we record the number either way.
            var seeker = Aspects[0];
            float seekerBox = OldLabelBoxW(seeker);
            float bodyW = ElarionUiKit.MeasureLineWidthPx(ElarionUiKit.FontRole.Body, OwnerWord,
                                                          ElarionUi.FontBody, out string bodyDetail);
            if (bodyW >= 0f)
                notes.Add("Seeker landscape at FontBody(" + ElarionUi.FontBody + "): '" + OwnerWord +
                          "' is " + bodyW.ToString("0.0") + " px vs old box " + seekerBox.ToString("0.0") +
                          " px (" + (bodyW > seekerBox ? "overflows before autosize" : "fits at Body") +
                          "; " + bodyDetail + ")");
        }

        // =====================================================================
        //  geometry
        // =====================================================================

        /// <summary>CanvasScaler ScaleWithScreenSize, reference 1080x1920, match 0.5 -
        /// ElarionUiKit.BuildModalCanvas verbatim. Turns a device frame into the
        /// REFERENCE canvas every fraction above is a fraction OF.</summary>
        private static float ScaleFactor(float screenW, float screenH)
        {
            return Mathf.Pow(screenW / 1080f, 0.5f) * Mathf.Pow(screenH / 1920f, 0.5f);
        }

        private static void RotateBox(Aspect a,
            out float btnW, out float btnH, out float labelW, out float wordH, out float chevronH)
        {
            float scale = ScaleFactor(a.W, a.H);
            float canvasW = a.W / scale;
            float canvasH = a.H / scale;
            float wellW = canvasW * (PanelX1 - PanelX0) * (WellXMax - WellXMin);
            float wellH = canvasH * (PanelY1 - PanelY0) * (WellYMax - WellYMin);
            float carH = wellH * (1f - CarouselYMin);
            btnW = wellW * (PrevXMax - PrevXMin);
            btnH = carH * (CarArrowYMax - CarArrowYMin);
            labelW = btnW * (RotateWordX1 - RotateWordX0);
            wordH = btnH * (RotateWordYMax - RotateWordYMin);
            chevronH = btnH * (RotateChevronYMax - RotateChevronYMin);
        }

        private static float OldLabelBoxW(Aspect a)
        {
            float scale = ScaleFactor(a.W, a.H);
            float canvasW = a.W / scale;
            float wellW = canvasW * (PanelX1 - PanelX0) * (WellXMax - WellXMin);
            return wellW * (OldPrevXMax - OldPrevXMin) * KitButtonLabelInset;
        }

        private static void AssertFits(List<string> failures,
            string aspect, string kind, string text, float fontSize, float boxW)
        {
            string detail;
            float w = ElarionUiKit.MeasureLineWidthPx(ElarionUiKit.FontRole.Body, text, fontSize, out detail);
            if (w < 0f)
            {
                failures.Add("[label-fits] cannot measure " + kind + " '" + text + "': " + detail);
                return;
            }
            if (w > boxW)
                failures.Add("[label-fits] at " + aspect + " the " + kind + " '" + text +
                             "' MEASURES " + w.ToString("0.0") + " ref px at " + fontSize.ToString("0") +
                             " px but its plate label rect is only " + boxW.ToString("0.0") +
                             " px (" + detail + "). TMP would clip or ellipsize it - that is the " +
                             "captured 'Pr...'. Widen the plate; do NOT shorten the word");
        }

        private static void RequireLiteral(List<string> failures, string src, string literal, string why)
        {
            if (src.IndexOf(literal, StringComparison.Ordinal) < 0)
                failures.Add("[boxes-pinned] " + Src + " no longer contains '" + literal + "' (" + why +
                             "). This suite measures against that number - re-measure the labels and " +
                             "update this pin together, or the oracle is asserting against a box that is gone");
        }

        private static string ReadSrc(string relPath)
        {
            try { return File.Exists(relPath) ? File.ReadAllText(relPath) : null; }
            catch { return null; }
        }
    }
}
