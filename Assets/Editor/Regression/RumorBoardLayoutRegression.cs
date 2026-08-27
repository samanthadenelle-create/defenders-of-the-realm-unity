// =============================================================================
// RumorBoardLayoutRegression [rumor-board-layout] - Brom's rumor board (WO-1192 v3)
// can never re-grow a second region, shrink a band under its line box, or place
// two authored rects in the same place.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Namespace: DeNelle.Editor.Regression.
//
// WHAT BROKE, TWICE, ON FRESH CAPTURES (2026-08-25 and 2026-08-26):
//   portrait  - the detail pane overlaid the whole list; the "* All" tab chip
//               floated over the "In Progress" heading; reward chips truncated to
//               "X... / Crys... / St... / Ma...".
//   landscape - the status line bisected the second In-Progress card; the
//               objective ended MID-WORD ("begun to sin"); two card titles
//               truncated to the IDENTICAL string; the lower third was dead black.
// The oracle reported the panel at TWO findings while it looked like that, which
// is the honest boundary of what a headless marker proves. So this file pins the
// BUDGET (decidable headlessly, at gate speed) and RunCaptureHeadless + eyes-on
// keep proving the pixels.
//
// WO-1192 v3 replaced five competing regions (tabs / list / detail / status /
// footer) with ONE: three self-contained rumor posters, paged three at a time.
// The cases below are the properties that make the old failures unreachable:
//
//   1 [poster-stack]  Every band inside a poster is a FIXED reference-pixel budget
//                     that is at least one TMP line box at the font it renders,
//                     every tap target is authored AT/above the kit touch floor,
//                     and the whole stack FITS the card the poster band really
//                     resolves to at all three LANDSCAPE capture aspects.
//   2 [zero-overlap]  The arithmetic assertion the two failing captures would have
//                     failed: NO TWO authored rects on this board share a pixel -
//                     not inside a poster, not between the three columns, not
//                     between the title / Next / Close of the head row, and not
//                     between the posters and the status band. Computed per aspect
//                     from the same constants the View lays out with.
//   3 [head-row]      Previous, Next and the shared Close are all at/above the touch
//                     floor and resolve INSIDE the panel, with the Close keeping the
//                     kit's canonical box (owner F8 x3).
//   4 [source-laws]   The retired surfaces stay retired (no tabs, no detail pane,
//                     no In-Progress, no Track, no selection step), the View routes
//                     through ElarionUiKit, strict MVVM holds, there is NO allow-list
//                     entry, the file is ASCII and NUL-free, and the reward row is
//                     still fed by QuestRewardMath with no fixed chip count.
//   5 [previous]      Owner felt-test 2026-08-27: "A previous button would be nice".
//                     Previous exists, is wired to PrevPage (wraps), is >= MinTouchPx
//                     on both axes, and its MEASURED label fits the host so the face
//                     cannot render as "Pr...". Portrait and landscape.
//
// HOW EACH CASE WAS PROVEN RED (WO-1138). The pre-rebuild tree at HEAD is the
// negative fixture, and every case fails against it by construction:
//   [poster-stack] reads PosterMinHeightPx / ReadBandPx / AcceptBandPx / HookBandPx -
//     none of those constants existed before this change, so ConstFloat records a
//     failure ("... does not exist") for each on the old file.
//   [zero-overlap] is computed from Poster1XMax / Poster2XMin / HeadTopY / CloseCentreX,
//     which likewise did not exist; and against the OLD geometry the same routine
//     reports a real collision - the old landscape status band spanned x 0.03-0.97 of
//     the body zone across the list column's floor, which is the "status line bisects
//     the second card" finding verbatim.
//   [head-row] fails on the old tree because the shared Close was seated in the
//     kit's DEFAULT bottom-centre band while the panel's own footer band was drawn
//     at the same place - two surfaces, one band.
//   [source-laws] fails on the old tree on EVERY forbidden token: RumorBoardVM.cs
//     shipped TabKeys / TabLabels / ActiveQuests / Track / DailyRow and
//     RumorBoardPanel.cs shipped BuildTabStrip / RenderDetail / _selectedId.
// Re-prove any case by restoring the token it forbids; each fails alone.
//
// NO HOLLOW PASSES: every early return in this file is preceded by a recorded
// FAILURE. A missing type, a missing constant or an unreadable source file is a
// FAILURE here, never a note - a guard that returns quietly lands green and hides
// the very drift the oracle exists to catch.
//
// Markers: RUMOR_BOARD_LAYOUT_OK / RUMOR_BOARD_LAYOUT_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.RumorBoardLayoutRegression.RunAll
// Registered in DataRegression.RunAll as the "rumor-board-layout suite".
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using DeNelle.Core.UI;

namespace DeNelle.Editor.Regression
{
    public static class RumorBoardLayoutRegression
    {
        private const string ViewSrc = "Assets/_Modules/Village/Hero/RumorBoardPanel.cs";
        private const string VmSrc = "Assets/_Modules/Village/Hero/RumorBoardVM.cs";

        private const string ViewType = "DeNelle.Village.Hero.RumorBoardPanel";
        private const string VmType = "DeNelle.Village.Hero.RumorBoardVM";
        private const string KitType = "DeNelle.Core.UI.ElarionUiKit";
        private const string UiType = "DeNelle.Core.UI.ElarionUi";

        /// <summary>The TMP line box multiplier the bands are budgeted from (~1.25em).</summary>
        private const float LineBoxMul = 1.25f;

        /// <summary>Pixels two authored rects must clear each other by. The layout oracle's own
        /// tolerance is 2 px; this budget asks for more so a rounding difference between the
        /// budget and the live layout pass can never turn into a finding.</summary>
        private const float ClearancePx = 6f;

        /// <summary>The LANDSCAPE capture aspects. Portrait is deliberately absent: the game is
        /// landscape-only (owner ruling 2026-08-26) and the v3 board has exactly ONE layout, so
        /// a portrait budget here would assert a composition nobody designed.</summary>
        private static readonly int[,] Aspects = { { 1920, 1080 }, { 2340, 1080 }, { 2670, 1200 } };

        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("RUMOR_BOARD_LAYOUT_OK - " + reason);
            else Debug.LogError("RUMOR_BOARD_LAYOUT_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                Case(failures, "poster-stack", () => Case1_PosterStack(failures, notes));
                Case(failures, "zero-overlap", () => Case2_ZeroOverlap(failures, notes));
                Case(failures, "head-row", () => Case3_HeadRow(failures, notes));
                Case(failures, "source-laws", () => Case4_SourceLaws(failures, notes));
                Case(failures, "previous", () => Case5_Previous(failures, notes));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "RUMOR BOARD LAYOUT OK - the v3 poster stack is a whole TMP line box in FIXED " +
                         "reference pixels at every band, every tap target is authored at/above the kit " +
                         "touch floor, NO TWO authored rects share a pixel at any of the " +
                         Aspects.GetLength(0) + " landscape capture aspects, Previous is a real paging " +
                         "control whose MEASURED label fits (>= MinTouchPx, never 'Pr...'), the shared " +
                         "Close keeps its canonical box inside the panel, and the retired surfaces " +
                         "(tabs / detail pane / In-Progress / Track / selection) stay retired" + noteStr;
                return true;
            }
            reason = "rumor-board-layout FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  The measured board, derived ONCE from the same constants the View uses.
        // =====================================================================

        private sealed class Board
        {
            public int W, H;
            public float PanelW, PanelH;
            public float CardW, CardH;
            public float CardBottomPx;     // poster floor above the panel floor
            public float HeadTopPx;        // head row top, above the panel floor
        }

        private static Board Measure(Type view, int w, int h, float panelMin, float panelMax)
        {
            // CanvasScaler ScaleWithScreenSize, reference 1080x1920, match 0.5 (BuildModalCanvas).
            float scale = Mathf.Sqrt(w / 1080f) * Mathf.Sqrt(h / 1920f);
            float span = panelMax - panelMin;
            var b = new Board { W = w, H = h };
            b.PanelW = span * (w / scale);
            b.PanelH = span * (h / scale);
            float yMin = Frac(view, "PosterYMin", panelMin, span);
            float yMax = Frac(view, "PosterYMax", panelMin, span);
            b.CardH = (yMax - yMin) * b.PanelH;
            b.CardBottomPx = yMin * b.PanelH;
            b.CardW = (Frac(view, "Poster1XMax", panelMin, span) - Frac(view, "Poster1XMin", panelMin, span)) * b.PanelW;
            b.HeadTopPx = Frac(view, "HeadTopY", panelMin, span) * b.PanelH;
            return b;
        }

        /// <summary>Screen fraction -> panel fraction, the same conversion RumorBoardPanel.PanelFrac
        /// performs. Read from the View's own constants so the two cannot drift.</summary>
        private static float Frac(Type view, string constName, float panelMin, float span)
        {
            var f = view.GetField(constName, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (f == null) return float.NaN;
            object v = f.GetValue(null);
            float sf = v is float fv ? fv : (v is int iv ? iv : float.NaN);
            return (sf - panelMin) / span;
        }

        // =====================================================================
        //  CASE 1 - the poster's fixed-pixel stack is honest and it FITS
        // =====================================================================
        private static void Case1_PosterStack(List<string> failures, List<string> notes)
        {
            Type view = FindType(ViewType);
            Type kit = FindType(KitType);
            Type ui = FindType(UiType);
            if (view == null) { failures.Add("[poster-stack] " + ViewType + " not found - the rumor board view was renamed or removed; re-point this oracle rather than deleting the guard"); return; }
            if (kit == null) { failures.Add("[poster-stack] " + KitType + " not found - cannot read the kit touch floor"); return; }
            if (ui == null) { failures.Add("[poster-stack] " + UiType + " not found - cannot read the font ladder the bands are budgeted from"); return; }

            float minTouch = ConstFloat(kit, "MinTouchPx", failures, "[poster-stack]");
            float fontBody = ConstFloat(ui, "FontBody", failures, "[poster-stack]");
            float fontMicro = ConstFloat(ui, "FontMicro", failures, "[poster-stack]");
            float fontFloor = ConstFloat(ui, "FontFloorMobile", failures, "[poster-stack]");
            if (minTouch <= 0f || fontBody <= 0f || fontMicro <= 0f || fontFloor <= 0f) return;

            float bodyLine = fontBody * LineBoxMul;
            float microLine = fontMicro * LineBoxMul;

            float titleBand = ConstFloat(view, "TitleBandPx", failures, "[poster-stack]");
            float hookBand = ConstFloat(view, "HookBandPx", failures, "[poster-stack]");
            float readBand = ConstFloat(view, "ReadBandPx", failures, "[poster-stack]");
            float acceptBand = ConstFloat(view, "AcceptBandPx", failures, "[poster-stack]");
            float rewardBand = ConstFloat(view, "RewardBandPx", failures, "[poster-stack]");
            float headBand = ConstFloat(view, "HeadBandPx", failures, "[poster-stack]");
            float statusBand = ConstFloat(view, "StatusBandPx", failures, "[poster-stack]");
            float posterMin = ConstFloat(view, "PosterMinHeightPx", failures, "[poster-stack]");
            float panelMin = ConstFloat(view, "PanelAnchorMin", failures, "[poster-stack]");
            float panelMax = ConstFloat(view, "PanelAnchorMax", failures, "[poster-stack]");
            if (titleBand <= 0f || hookBand <= 0f || readBand <= 0f || acceptBand <= 0f ||
                rewardBand <= 0f || headBand <= 0f || statusBand <= 0f || posterMin <= 0f ||
                panelMax <= panelMin) return;

            // Every band is a whole TMP line box at the font it renders.
            if (titleBand < 2f * bodyLine)
                failures.Add("[poster-stack] TitleBandPx=" + titleBand + " cannot seat TWO FontBody line boxes (" +
                             (2f * bodyLine) + ") - the v3 title is a TWO-LINE block and the second line is culled whole");
            if (hookBand < microLine)
                failures.Add("[poster-stack] HookBandPx=" + hookBand + " is under one FontMicro line box (" +
                             microLine + ") - TMP culls the hook whole, which is the -11px body class of bug (WO-866)");
            if (rewardBand < microLine)
                failures.Add("[poster-stack] RewardBandPx=" + rewardBand + " is under one FontMicro line box (" +
                             microLine + ") - the reward chips render as empty outlines (the 2026-08-02 symptom)");

            // Every tap target is AUTHORED at the floor - never grown into it by ClampMinTouch,
            // which would spill it symmetrically into both neighbours.
            if (readBand < minTouch)
                failures.Add("[poster-stack] ReadBandPx=" + readBand + " is below the kit touch floor " + minTouch +
                             " - 'Read the letter >' is a real tap target, not a decorative line");
            if (acceptBand < minTouch)
                failures.Add("[poster-stack] AcceptBandPx=" + acceptBand + " is below the kit touch floor " + minTouch +
                             " - Accept is the hero action of this board");
            if (headBand < minTouch)
                failures.Add("[poster-stack] HeadBandPx=" + headBand + " is below the kit touch floor " + minTouch +
                             " - the mockup's 0.823-0.917 fraction resolves to 91 ref px at 2670x1200, which is " +
                             "exactly why this band is authored in PIXELS and not as that fraction");

            // ...and the whole stack FITS the card at every landscape capture aspect.
            for (int i = 0; i < Aspects.GetLength(0); i++)
            {
                var b = Measure(view, Aspects[i, 0], Aspects[i, 1], panelMin, panelMax);
                if (float.IsNaN(b.CardH) || b.CardH <= 0f)
                {
                    failures.Add("[poster-stack] the poster band constants did not resolve at " +
                                 Aspects[i, 0] + "x" + Aspects[i, 1] + " - a missing PosterY/PosterX constant " +
                                 "cannot be treated as 'nothing to check'");
                    continue;
                }
                if (b.CardH < posterMin)
                    failures.Add("[poster-stack] at " + b.W + "x" + b.H + " a poster resolves " +
                                 b.CardH.ToString("F0") + " ref px tall, under PosterMinHeightPx(" + posterMin +
                                 ") - the top-hung and bottom-hung halves of the stack meet and something is culled");
                if (b.CardBottomPx < statusBand + ClearancePx)
                    failures.Add("[poster-stack] at " + b.W + "x" + b.H + " the poster floor is only " +
                                 b.CardBottomPx.ToString("F0") + " ref px above the panel floor, which does not " +
                                 "clear the status band (" + statusBand + ") - the status line lands on Accept, " +
                                 "which is the landscape finding from the 2026-08-26 capture verbatim");
                notes.Add(b.W + "x" + b.H + ": poster " + b.CardW.ToString("F0") + "x" + b.CardH.ToString("F0") +
                          " ref px (min " + posterMin + ")");
            }
        }

        // =====================================================================
        //  CASE 2 - NO TWO AUTHORED RECTS SHARE A PIXEL
        // =====================================================================
        private struct Band
        {
            public string Name;
            public float X0, X1, Y0, Y1;   // reference px, panel-local, y up
            public Band(string n, float x0, float x1, float y0, float y1)
            { Name = n; X0 = x0; X1 = x1; Y0 = y0; Y1 = y1; }
        }

        private static void Case2_ZeroOverlap(List<string> failures, List<string> notes)
        {
            Type view = FindType(ViewType);
            Type kit = FindType(KitType);
            if (view == null || kit == null)
            { failures.Add("[zero-overlap] view/kit type not found - the geometry budget cannot be evaluated"); return; }

            float panelMin = ConstFloat(view, "PanelAnchorMin", failures, "[zero-overlap]");
            float panelMax = ConstFloat(view, "PanelAnchorMax", failures, "[zero-overlap]");
            float ctaW = ConstFloat(kit, "CanonCtaWidth", failures, "[zero-overlap]");
            float ctaH = ConstFloat(kit, "CanonCtaHeight", failures, "[zero-overlap]");
            if (panelMax <= panelMin || ctaW <= 0f || ctaH <= 0f) return;
            float span = panelMax - panelMin;

            string[] needed =
            {
                "TitleTopPx", "TitleBandPx", "HookTopPx", "HookBandPx", "ReadTopPx", "ReadBandPx",
                "AcceptBottomPx", "AcceptBandPx", "RewardBottomPx", "RewardBandPx",
                "RuleBottomPx", "RulePx", "HeadBandPx", "StatusBandPx", "StatusBottomPx",
                "HeadGapPx",
            };
            var px = new Dictionary<string, float>();
            foreach (var n in needed)
            {
                float v = ConstFloat(view, n, failures, "[zero-overlap]");
                if (v <= 0f && n != "StatusBottomPx") return;   // ConstFloat already recorded WHY
                px[n] = v;
            }

            string prevLabel = ConstString(view, "PreviousLabel", failures, "[zero-overlap]");
            if (string.IsNullOrEmpty(prevLabel)) return;
            float prevW = InvokePageButtonWidth(view, prevLabel, failures, "[zero-overlap]");
            if (prevW <= 0f) return;
            float headGap = px["HeadGapPx"];

            for (int a = 0; a < Aspects.GetLength(0); a++)
            {
                var b = Measure(view, Aspects[a, 0], Aspects[a, 1], panelMin, panelMax);
                if (float.IsNaN(b.CardH) || b.CardH <= 0f)
                { failures.Add("[zero-overlap] poster band constants missing at " + b.W + "x" + b.H); continue; }

                // ---- inside ONE poster (card-local, y measured up from the card floor) ----
                var poster = new List<Band>
                {
                    new Band("Title",   0f, b.CardW, b.CardH - px["TitleTopPx"] - px["TitleBandPx"], b.CardH - px["TitleTopPx"]),
                    new Band("Hook",    0f, b.CardW, b.CardH - px["HookTopPx"] - px["HookBandPx"],   b.CardH - px["HookTopPx"]),
                    new Band("Read",    0f, b.CardW, b.CardH - px["ReadTopPx"] - px["ReadBandPx"],   b.CardH - px["ReadTopPx"]),
                    new Band("Rule",    0f, b.CardW, px["RuleBottomPx"],   px["RuleBottomPx"] + px["RulePx"]),
                    new Band("Rewards", 0f, b.CardW, px["RewardBottomPx"], px["RewardBottomPx"] + px["RewardBandPx"]),
                    new Band("Accept",  0f, b.CardW, px["AcceptBottomPx"], px["AcceptBottomPx"] + px["AcceptBandPx"]),
                };
                AssertDisjoint(failures, poster, "poster @" + b.W + "x" + b.H,
                    "two bands inside one poster occupy the same pixels - one of them is drawn over the other, " +
                    "which is the 'button over text' finding the fresh captures reported");

                foreach (var band in poster)
                {
                    if (band.Y0 < -0.01f || band.Y1 > b.CardH + 0.01f)
                        failures.Add("[zero-overlap] at " + b.W + "x" + b.H + " the poster band '" + band.Name +
                                     "' resolves outside its card (" + band.Y0.ToString("F0") + ".." +
                                     band.Y1.ToString("F0") + " in a " + b.CardH.ToString("F0") + " px card)");
                }

                // ---- the three columns, the head row and the status band (panel-local) ----
                float x1Min = Frac(view, "Poster1XMin", panelMin, span) * b.PanelW;
                float x1Max = Frac(view, "Poster1XMax", panelMin, span) * b.PanelW;
                float x2Min = Frac(view, "Poster2XMin", panelMin, span) * b.PanelW;
                float x2Max = Frac(view, "Poster2XMax", panelMin, span) * b.PanelW;
                float x3Min = Frac(view, "Poster3XMin", panelMin, span) * b.PanelW;
                float x3Max = Frac(view, "Poster3XMax", panelMin, span) * b.PanelW;
                float posterTop = b.CardBottomPx + b.CardH;

                float closeCx = Frac(view, "CloseCentreX", panelMin, span) * b.PanelW;
                float nextX0 = Frac(view, "NextXMin", panelMin, span) * b.PanelW;
                float nextX1 = Frac(view, "NextXMax", panelMin, span) * b.PanelW;
                float titleX0 = Frac(view, "TitleXMin", panelMin, span) * b.PanelW;
                float titleY0 = Frac(view, "TitleYMin", panelMin, span) * b.PanelH;
                float titleY1 = Frac(view, "TitleYMax", panelMin, span) * b.PanelH;
                if (float.IsNaN(closeCx) || float.IsNaN(nextX0) || float.IsNaN(titleX0))
                { failures.Add("[zero-overlap] head-row constants missing at " + b.W + "x" + b.H); continue; }

                float prevX1 = nextX0 - headGap;
                float prevX0 = prevX1 - prevW;
                // Live title ends at Previous's left minus the head gap (BuildTitle's inset).
                float titleX1 = prevX0 - headGap;

                var panel = new List<Band>
                {
                    new Band("Poster1", x1Min, x1Max, b.CardBottomPx, posterTop),
                    new Band("Poster2", x2Min, x2Max, b.CardBottomPx, posterTop),
                    new Band("Poster3", x3Min, x3Max, b.CardBottomPx, posterTop),
                    new Band("Title",   titleX0, titleX1, titleY0, titleY1),
                    new Band("Previous", prevX0, prevX1, b.HeadTopPx - px["HeadBandPx"], b.HeadTopPx),
                    new Band("Next",    nextX0, nextX1, b.HeadTopPx - px["HeadBandPx"], b.HeadTopPx),
                    new Band("Close",   closeCx - ctaW * 0.5f, closeCx + ctaW * 0.5f, b.HeadTopPx - ctaH, b.HeadTopPx),
                    new Band("Status",  x1Min, x3Max, px["StatusBottomPx"], px["StatusBottomPx"] + px["StatusBandPx"]),
                };
                AssertDisjoint(failures, panel, "board @" + b.W + "x" + b.H,
                    "two authored surfaces on the board occupy the same pixels - this is the class of defect " +
                    "the whole v3 rebuild exists to make unreachable (five regions fighting for one screen)");

                notes.Add(b.W + "x" + b.H + ": columns " + x1Min.ToString("F0") + "-" + x1Max.ToString("F0") +
                          " / " + x2Min.ToString("F0") + "-" + x2Max.ToString("F0") +
                          " / " + x3Min.ToString("F0") + "-" + x3Max.ToString("F0") +
                          ", head " + (b.HeadTopPx - ctaH).ToString("F0") + "-" + b.HeadTopPx.ToString("F0"));
            }
        }

        private static void AssertDisjoint(List<string> failures, List<Band> bands, string where, string why)
        {
            for (int i = 0; i < bands.Count; i++)
                for (int j = i + 1; j < bands.Count; j++)
                {
                    float ow = Mathf.Min(bands[i].X1, bands[j].X1) - Mathf.Max(bands[i].X0, bands[j].X0);
                    float oh = Mathf.Min(bands[i].Y1, bands[j].Y1) - Mathf.Max(bands[i].Y0, bands[j].Y0);
                    if (ow <= ClearancePx || oh <= ClearancePx) continue;
                    failures.Add("[zero-overlap] " + where + " '" + bands[i].Name + "' and '" + bands[j].Name +
                                 "' share " + ow.ToString("F0") + "x" + oh.ToString("F0") + " ref px - " + why);
                }
        }

        // =====================================================================
        //  CASE 3 - the head row: Previous, Next and the ONE shared Close
        // =====================================================================
        private static void Case3_HeadRow(List<string> failures, List<string> notes)
        {
            Type view = FindType(ViewType);
            Type kit = FindType(KitType);
            if (view == null || kit == null)
            { failures.Add("[head-row] view/kit type not found - the head row budget cannot be evaluated"); return; }

            float minTouch = ConstFloat(kit, "MinTouchPx", failures, "[head-row]");
            float ctaW = ConstFloat(kit, "CanonCtaWidth", failures, "[head-row]");
            float ctaH = ConstFloat(kit, "CanonCtaHeight", failures, "[head-row]");
            float panelMin = ConstFloat(view, "PanelAnchorMin", failures, "[head-row]");
            float panelMax = ConstFloat(view, "PanelAnchorMax", failures, "[head-row]");
            float headBand = ConstFloat(view, "HeadBandPx", failures, "[head-row]");
            if (minTouch <= 0f || ctaW <= 0f || ctaH <= 0f || panelMax <= panelMin || headBand <= 0f) return;
            float span = panelMax - panelMin;

            string prevLabel = ConstString(view, "PreviousLabel", failures, "[head-row]");
            float prevW = string.IsNullOrEmpty(prevLabel) ? 0f
                : InvokePageButtonWidth(view, prevLabel, failures, "[head-row]");
            if (prevW > 0f && prevW < minTouch)
                failures.Add("[head-row] Previous host is " + prevW.ToString("F0") +
                             " ref px wide, under the kit touch floor " + minTouch +
                             " - owner bounce 2026-08-27; a paging face is a real tap target");

            for (int a = 0; a < Aspects.GetLength(0); a++)
            {
                var b = Measure(view, Aspects[a, 0], Aspects[a, 1], panelMin, panelMax);
                float nextW = (Frac(view, "NextXMax", panelMin, span) - Frac(view, "NextXMin", panelMin, span)) * b.PanelW;
                float closeCx = Frac(view, "CloseCentreX", panelMin, span) * b.PanelW;
                if (float.IsNaN(nextW) || float.IsNaN(closeCx))
                { failures.Add("[head-row] Next/Close constants missing at " + b.W + "x" + b.H); continue; }

                if (nextW < minTouch)
                    failures.Add("[head-row] at " + b.W + "x" + b.H + " Next resolves " + nextW.ToString("F0") +
                                 " ref px wide, under the kit touch floor " + minTouch);
                if (closeCx + ctaW * 0.5f > b.PanelW)
                    failures.Add("[head-row] at " + b.W + "x" + b.H + " the shared Close's canonical " + ctaW +
                                 " px box runs " + (closeCx + ctaW * 0.5f - b.PanelW).ToString("F0") +
                                 " px past the panel's right edge - owner F8 2026-07-04: everything must be " +
                                 "INSIDE the panel, and the Close's SIZE is canonical, so only CloseCentreX may move");
                if (closeCx - ctaW * 0.5f < 0f)
                    failures.Add("[head-row] at " + b.W + "x" + b.H + " the shared Close runs past the panel's LEFT edge");
                if (b.HeadTopPx - ctaH < 0f || b.HeadTopPx > b.PanelH)
                    failures.Add("[head-row] at " + b.W + "x" + b.H + " the head row falls outside the panel");

                // THE GUTTER. The type tag and the NEW chip poke ABOVE their card on purpose, and
                // the head row's LOWEST edge is the shared Close's bottom. At 2670x1200 that
                // gutter is only ~30 ref px, so a tag that straddled its card edge at half its
                // own height (38) would put the right-hand poster's NEW chip INSIDE the Close's
                // box. The overhang is a declared number for exactly this reason; this is the
                // assertion that keeps it declared.
                float overhang = ConstFloat(view, "TypeTagOverhangPx", failures, "[head-row]");
                if (overhang > 0f)
                {
                    float gutter = (b.HeadTopPx - ctaH) - (b.CardBottomPx + b.CardH);
                    if (overhang + ClearancePx > gutter)
                        failures.Add("[head-row] at " + b.W + "x" + b.H + " the poster's " + overhang +
                                     " px tag/NEW overhang does not clear the " + gutter.ToString("F0") +
                                     " ref px gutter under the head row - the right-hand poster's NEW chip " +
                                     "lands inside the shared Close's canonical box");
                }

                notes.Add(b.W + "x" + b.H + ": Previous " + prevW.ToString("F0") + " px, Next " +
                          nextW.ToString("F0") + " px wide, Close " +
                          (closeCx - ctaW * 0.5f).ToString("F0") + "-" + (closeCx + ctaW * 0.5f).ToString("F0") +
                          " in a " + b.PanelW.ToString("F0") + " px panel");
            }

            // The Close must still be the KIT's shared Close, re-seated by the KIT's own seater -
            // never a second Close hand-rolled into the head row.
            string raw = ReadSource(ViewSrc, failures, "[head-row]");
            if (raw == null) return;
            string src = StripComments(raw);
            if (src.IndexOf("SeatSharedCloseInside", StringComparison.Ordinal) < 0)
                failures.Add("[head-row] RumorBoardPanel no longer seats the shared Close through the kit's " +
                             "SeatSharedCloseInside - a hand-placed Close is a SECOND close convention, and the " +
                             "owner ruled there is exactly one (a labeled button, never an X)");
            if (Regex.IsMatch(src, "BuildObsidianButton\\s*\\([^;]*\"Close\""))
                failures.Add("[head-row] the board builds its OWN 'Close' button - it must re-seat the kit's " +
                             "shared Close instead, or the game has two Closes that look and size differently");
        }

        // =====================================================================
        //  CASE 4 - the source laws: what stays retired, and what stays routed
        // =====================================================================
        private static void Case4_SourceLaws(List<string> failures, List<string> notes)
        {
            string viewRaw = ReadSource(ViewSrc, failures, "[source-laws]");
            string vmRaw = ReadSource(VmSrc, failures, "[source-laws]");
            if (viewRaw == null || vmRaw == null) return;
            string view = StripComments(viewRaw);
            string vm = StripComments(vmRaw);

            // -- the retired surfaces stay retired -------------------------------
            // Each of these was a REGION of the old board. Every one of the six WO-1060
            // findings against this panel was a card overlapping the tab band's chip label or
            // the detail pane; with neither region in existence, none of them is reachable.
            var retired = new Dictionary<string, string>
            {
                { "TabKeys",        "the filter tab strip (its chip label is what the cards overlapped)" },
                { "TabLabels",      "the filter tab strip" },
                { "BuildTabStrip",  "the filter tab band" },
                { "RenderDetail",   "the master-detail pane (it overlaid the whole list in portrait)" },
                { "_selectedId",    "the selection step (v3 has none - the card you read is the card you accept)" },
                { "ActiveQuests",   "the In-Progress section (tracking is the HUD tracker's job)" },
                { "DailyRow",       "the Daily tab" },
            };
            foreach (var kv in retired)
            {
                if (Regex.IsMatch(view, "\\b" + kv.Key + "\\b"))
                    failures.Add("[source-laws] RumorBoardPanel has re-grown " + kv.Key + " - " + kv.Value +
                                 ". The v3 board is THREE POSTERS and nothing else (owner-approved concept); " +
                                 "a second region is how five surfaces ended up fighting for one screen");
                if (Regex.IsMatch(vm, "\\b" + kv.Key + "\\b"))
                    failures.Add("[source-laws] RumorBoardVM has re-grown " + kv.Key + " - " + kv.Value);
            }
            if (Regex.IsMatch(vm, "\\bpublic\\s+void\\s+Track\\s*\\("))
                failures.Add("[source-laws] RumorBoardVM.Track is back - the rumor board only OFFERS work; " +
                             "pinning a quest belongs to the HUD tracker (owner ruling 2026-08-26)");

            // -- the v3 shape is actually built ----------------------------------
            if (view.IndexOf("PageQuests", StringComparison.Ordinal) < 0)
                failures.Add("[source-laws] the View no longer renders the VM's paged window (PageQuests) - " +
                             "an unpaged board grows past three posters and the fixed columns collide");
            if (view.IndexOf("NextPage", StringComparison.Ordinal) < 0)
                failures.Add("[source-laws] the Next > paging command is gone from the View");
            if (view.IndexOf("PrevPage", StringComparison.Ordinal) < 0)
                failures.Add("[source-laws] the Previous paging command is gone from the View - owner " +
                             "felt-test 2026-08-27 asked for a previous button, and a dead face is not one");
            if (vm.IndexOf("PrevPage", StringComparison.Ordinal) < 0)
                failures.Add("[source-laws] RumorBoardVM.PrevPage is gone - Previous must actually navigate");
            if (vm.IndexOf("% pages", StringComparison.Ordinal) < 0 &&
                vm.IndexOf("% pages;", StringComparison.Ordinal) < 0)
                failures.Add("[source-laws] RumorBoardVM.NextPage no longer WRAPS (the modulo is gone) - the " +
                             "owner chose the keep-going form, so the last page must roll back to the first");
            if (view.IndexOf("PreviousHost", StringComparison.Ordinal) < 0)
                failures.Add("[source-laws] PreviousHost is gone from the View - the Previous control was removed");
            if (view.IndexOf("MeasureLineWidthPx", StringComparison.Ordinal) < 0)
                failures.Add("[source-laws] the View no longer MEASURES the Previous label " +
                             "(MeasureLineWidthPx) - a character-count host is how 'Previous' becomes 'Pr...'");
            if (!Regex.IsMatch(view, "BuildObsidianButton\\s*\\([^;]*\"Accept\""))
                failures.Add("[source-laws] a poster no longer carries its OWN Accept - the whole point of v3 " +
                             "is that there is no selection step between reading a rumor and taking it");
            if (Regex.IsMatch(view, "BuildObsidianButton\\s*\\([^;]*\"Prev\""))
                failures.Add("[source-laws] the Previous face is labelled 'Prev' - that is the truncated form " +
                             "the owner bounce forbids. The word is Previous");

            // -- fixed pixels, never a fraction of the card -----------------------
            if (view.IndexOf("HangTop", StringComparison.Ordinal) < 0)
                failures.Add("[source-laws] the poster's fixed-pixel band helper (HangTop) is gone - a band " +
                             "expressed as a FRACTION of the card scales with the aspect and TMP culls its " +
                             "line box whole the moment it dips under one line (the WO-866 -11px body)");

            // -- the reward row still goes through the ONE reward authority --------
            if (vm.IndexOf("QuestRewardMath.Sum", StringComparison.Ordinal) < 0)
                failures.Add("[source-laws] the reward row no longer sums through QuestRewardMath over " +
                             "QuestRewardLine - WO-1201/1202 are the reward authority and a second reward " +
                             "schema is exactly what this board must not author");
            // NOTE the [2-9]: `chips.Count == 0` is the legitimate "no rewards, draw no row"
            // guard. What is forbidden is a layout that ASSUMES a count (the old row read
            // "Crystals 220 / Food 90 / Magic 45 / Relic ..." and a four-chip assumption breaks
            // the moment WO-1163's Stone lands).
            if (Regex.IsMatch(view, "chips\\.Count\\s*[=!<>]=\\s*[2-9]") ||
                Regex.IsMatch(view, "for\\s*\\([^;]*;\\s*[a-zA-Z_]+\\s*<\\s*4\\s*;[^;]*Chip"))
                failures.Add("[source-laws] the reward row assumes a FIXED chip count - WO-1163 retires Food " +
                             "for Stone and a layout hardcoding four chips breaks on a data change");
            if (view.IndexOf("ConceptIdFor", StringComparison.Ordinal) >= 0)
                failures.Add("[source-laws] the View resolves a concept id itself - ElarionUiKit.ConceptIdFor is " +
                             "the ONE translator and the kit's CurrencyChip already calls it; a second copy is a " +
                             "second registry (canon sec.7: that is how the Stone row wore the Food art)");

            // -- kit routing + strict MVVM ----------------------------------------
            if (view.IndexOf("ElarionUiKit", StringComparison.Ordinal) < 0)
                failures.Add("[source-laws] RumorBoardPanel does not go through ElarionUiKit - the " +
                             "UiObsidianConformanceRegression hand-rolled-uGUI law");
            foreach (string forbidden in new[] { "QuestService", "QuestCatalog", "DailyQuestService" })
                if (Regex.IsMatch(view, "\\b" + forbidden + "\\s*\\."))
                    failures.Add("[source-laws] RumorBoardPanel touches " + forbidden + " directly - the View is a " +
                                 "read-only consumer of RumorBoardVM (strict MVVM, [ui-mvvm] ratchet armed)");

            // -- NO ALLOW-LIST ENTRY (owner ruling 2026-08-24: no waivers) ---------
            AssertNotAllowListed(failures);

            // -- ASCII + NUL, both files ------------------------------------------
            AssertAsciiAndNulFree(failures, ViewSrc, viewRaw);
            AssertAsciiAndNulFree(failures, VmSrc, vmRaw);

            notes.Add("source laws checked on " + ViewSrc + " + " + VmSrc);
        }

        /// <summary>The panel must CONFORM, not be waived. The owner ruled 2026-08-24 that the
        /// LayoutOracle touch/geometry allow-list may only ever SHRINK, so an entry naming this
        /// board is a failure of THIS oracle regardless of what the capture run reports.</summary>
        private static void AssertNotAllowListed(List<string> failures)
        {
            const string oracleSrc = "Assets/_Modules/Core/UI/LayoutOracle.cs";
            string raw = ReadSource(oracleSrc, failures, "[source-laws]");
            if (raw == null) return;
            if (raw.IndexOf("RumorBoard", StringComparison.OrdinalIgnoreCase) >= 0)
                failures.Add("[source-laws] LayoutOracle names RumorBoard - the rumor board must CONFORM, never " +
                             "be waived (owner ruling 2026-08-24: no waivers, and the allow-list may only SHRINK)");
        }

        private static void AssertAsciiAndNulFree(List<string> failures, string path, string raw)
        {
            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] <= 127) continue;
                int line = 1;
                for (int j = 0; j < i; j++) if (raw[j] == '\n') line++;
                failures.Add("[source-laws] " + path + " carries a NON-ASCII character (U+" +
                             ((int)raw[i]).ToString("X4") + ") at line " + line +
                             " - it renders as tofu on the shipped TMP font, and a tofu oracle scans this file " +
                             "including its COMMENTS");
                break;
            }
            if (raw.IndexOf('\0') >= 0)
                failures.Add("[source-laws] " + path + " contains an embedded NUL byte (mount-garble, " +
                             "CLAUDE.md sec.0) - the compile gate rejects this");
        }

        // =====================================================================
        //  CASE 5 - Previous exists, is tappable, and its MEASURED label fits
        // =====================================================================
        // Owner felt-test 2026-08-27: "A previous button would be nice". The v3 mockup
        // only drew Next >; this case is the bounce. A face labelled "Prev" or sized
        // so FitSingleLine ellipsises to "Pr..." is a fail, even if a button exists.
        private static readonly int[,] PreviousAspects =
        {
            { 1920, 1080 }, { 2340, 1080 }, { 2670, 1200 },
            { 1080, 2340 }, { 1200, 2670 },
        };

        private static void Case5_Previous(List<string> failures, List<string> notes)
        {
            Type view = FindType(ViewType);
            Type vm = FindType(VmType);
            Type kit = FindType(KitType);
            if (view == null || vm == null || kit == null)
            { failures.Add("[previous] view/vm/kit type not found - Previous cannot be evaluated"); return; }

            string label = ConstString(view, "PreviousLabel", failures, "[previous]");
            if (label == null) return;
            if (label != "Previous")
                failures.Add("[previous] PreviousLabel is '" + label + "', not 'Previous' - a shortened " +
                             "face ('Prev', 'Pr...') is the truncation the bounce forbids");

            float minTouch = ConstFloat(kit, "MinTouchPx", failures, "[previous]");
            float inset = ConstFloat(view, "PageButtonLabelInset", failures, "[previous]");
            float headBand = ConstFloat(view, "HeadBandPx", failures, "[previous]");
            float headGap = ConstFloat(view, "HeadGapPx", failures, "[previous]");
            float panelMin = ConstFloat(view, "PanelAnchorMin", failures, "[previous]");
            float panelMax = ConstFloat(view, "PanelAnchorMax", failures, "[previous]");
            if (minTouch <= 0f || inset <= 0f || headBand <= 0f || headGap <= 0f || panelMax <= panelMin) return;

            float host = InvokePageButtonWidth(view, label, failures, "[previous]");
            if (host <= 0f) return;
            if (host < minTouch)
                failures.Add("[previous] PageButtonWidthPx('" + label + "')=" + host.ToString("F0") +
                             " is below MinTouchPx " + minTouch);
            if (headBand < minTouch)
                failures.Add("[previous] HeadBandPx=" + headBand + " is below MinTouchPx " + minTouch +
                             " - Previous shares this band with Next");

            string detail;
            float measured = ElarionUiKit.MeasureLineWidthPx(
                ElarionUiKit.FontRole.Body, "Previous", ElarionUi.FontBody, out detail);
            if (measured < 0f)
            {
                failures.Add("[previous] cannot MEASURE 'Previous': " + detail +
                             " - a host that is not measured is how the face becomes 'Pr...'");
            }
            else
            {
                float inner = host * inset;
                if (measured > inner)
                    failures.Add("[previous] 'Previous' MEASURES " + measured.ToString("0.0") +
                                 " ref px at FontBody but the host inner width is only " +
                                 inner.ToString("0.0") + " px (" + detail +
                                 "). FitSingleLine will ellipsis it to 'Pr...'. Grow the host; do not " +
                                 "shorten the word");
                notes.Add("Previous label " + measured.ToString("0.0") + " px in a " +
                          host.ToString("0.0") + " px host (inner " + inner.ToString("0.0") + ")");
            }

            string viewRaw = ReadSource(ViewSrc, failures, "[previous]");
            string vmRaw = ReadSource(VmSrc, failures, "[previous]");
            if (viewRaw == null || vmRaw == null) return;
            string viewSrc = StripComments(viewRaw);
            string vmSrc = StripComments(vmRaw);

            if (viewSrc.IndexOf("BuildPreviousButton", StringComparison.Ordinal) < 0)
                failures.Add("[previous] BuildPreviousButton is gone - the control was not built");
            if (!Regex.IsMatch(viewSrc, "BuildObsidianButton\\s*\\([^;]*PreviousLabel"))
                failures.Add("[previous] the View no longer builds an ElarionUiKit button with PreviousLabel");
            if (viewSrc.IndexOf("PrevPage", StringComparison.Ordinal) < 0)
                failures.Add("[previous] the View does not route Previous to PrevPage - a dead button");
            if (!Regex.IsMatch(vmSrc, @"public\s+void\s+PrevPage\s*\("))
                failures.Add("[previous] RumorBoardVM.PrevPage is missing - Previous has nowhere to navigate");
            if (vmSrc.IndexOf("_pageIndex - 1", StringComparison.Ordinal) < 0 &&
                vmSrc.IndexOf("_pageIndex-1", StringComparison.Ordinal) < 0)
                failures.Add("[previous] PrevPage no longer steps the page index backward");
            if (vmSrc.IndexOf("% pages", StringComparison.Ordinal) < 0)
                failures.Add("[previous] PrevPage no longer WRAPS (the modulo is gone) - Previous on page 0 " +
                             "must roll to the last page, the keep-going pair of Next");

            float span = panelMax - panelMin;
            for (int a = 0; a < PreviousAspects.GetLength(0); a++)
            {
                var b = Measure(view, PreviousAspects[a, 0], PreviousAspects[a, 1], panelMin, panelMax);
                float nextX0 = Frac(view, "NextXMin", panelMin, span) * b.PanelW;
                if (float.IsNaN(nextX0) || b.PanelW <= 0f)
                {
                    failures.Add("[previous] NextXMin did not resolve at " + b.W + "x" + b.H);
                    continue;
                }
                float prevX1 = nextX0 - headGap;
                float prevX0 = prevX1 - host;
                if (prevX0 < -0.01f)
                    failures.Add("[previous] at " + b.W + "x" + b.H + " Previous's left edge is " +
                                 prevX0.ToString("F0") + " px - it runs past the panel's LEFT edge");
                if (prevX1 > b.PanelW)
                    failures.Add("[previous] at " + b.W + "x" + b.H + " Previous's right edge is past the panel");
                notes.Add(b.W + "x" + b.H + ": Previous " + prevX0.ToString("F0") + "-" +
                          prevX1.ToString("F0") + " in a " + b.PanelW.ToString("F0") + " px panel");
            }
        }

        // =====================================================================
        //  helpers
        // =====================================================================

        /// <summary>Read a public const float by reflection (no asmdef reference needed). A
        /// MISSING constant is a FAILURE, never a silent zero: this oracle pins a budget, and a
        /// budget that quietly evaluates to nothing is a hollow pass.</summary>
        /// <summary>Public const string, same failure mode as ConstFloat: missing is a FAIL.</summary>
        private static string ConstString(Type t, string name, List<string> failures, string tag)
        {
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (f == null)
            {
                failures.Add(tag + " " + t.Name + "." + name + " does not exist - the Previous label this " +
                             "oracle pins was renamed or removed; re-point it rather than deleting the guard");
                return null;
            }
            return f.GetValue(null) as string;
        }

        /// <summary>Call RumorBoardPanel.PageButtonWidthPx so the oracle and the View cannot
        /// drift onto two different host-width formulae.</summary>
        private static float InvokePageButtonWidth(Type view, string label, List<string> failures, string tag)
        {
            var m = view.GetMethod("PageButtonWidthPx", BindingFlags.Public | BindingFlags.Static);
            if (m == null)
            {
                failures.Add(tag + " RumorBoardPanel.PageButtonWidthPx does not exist - the Previous host " +
                             "is no longer MEASURED from its label");
                return 0f;
            }
            try
            {
                object v = m.Invoke(null, new object[] { label });
                if (v is float fv) return fv;
                failures.Add(tag + " PageButtonWidthPx did not return a float");
                return 0f;
            }
            catch (Exception ex)
            {
                failures.Add(tag + " PageButtonWidthPx threw " + ex.GetType().Name + ": " + ex.Message);
                return 0f;
            }
        }

        private static float ConstFloat(Type t, string name, List<string> failures, string tag)
        {
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (f == null)
            {
                failures.Add(tag + " " + t.Name + "." + name + " does not exist - the layout constant this oracle " +
                             "pins was renamed or removed; re-point it rather than deleting the guard");
                return 0f;
            }
            object v = f.GetValue(null);
            if (v is float fv) return fv;
            if (v is int iv) return iv;
            if (v is double dv) return (float)dv;
            failures.Add(tag + " " + t.Name + "." + name + " is not a numeric constant");
            return 0f;
        }

        private static string ReadSource(string path, List<string> failures, string tag)
        {
            try
            {
                if (!File.Exists(path))
                {
                    failures.Add(tag + " source not found: " + path + " - the file this oracle lints was moved " +
                                 "or renamed; a missing source is a FAILURE, not a skip");
                    return null;
                }
                return File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                failures.Add(tag + " could not read " + path + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>Blank out // and block comments so a lesson written in prose (which names the
        /// retired tokens on purpose) can never fail a source law.</summary>
        private static string StripComments(string src)
        {
            string noBlock = Regex.Replace(src, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(noBlock, @"//[^\n]*", " ");
        }

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try { t = asm.GetType(fullName, false); }
                catch { }
                if (t != null) return t;
            }
            return null;
        }
    }
}
