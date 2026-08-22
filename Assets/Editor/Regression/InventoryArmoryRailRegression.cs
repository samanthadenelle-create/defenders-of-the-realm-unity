// =============================================================================
// InventoryArmoryRailRegression — WO-1133 "The Armory Rail" is what it says it is.
// -----------------------------------------------------------------------------
// A SOURCE-LINT + MEASUREMENT oracle (the HudLabelFitRegression family): it reads the
// .cs that authors the Bag and the two canonical string copies, and it MEASURES. It
// does not run PlayMode, so it slots straight into the headless DataRegression batch.
// Never throws - an unreadable file becomes a failure line, never a crash.
//
// ⚠ THE RULE THIS SUITE WAS WRITTEN AGAINST: DO NOT ASSERT GEOMETRY BY RECOMPUTING IT
// FROM THE SAME CONSTANTS THE LAYOUT USES. That yields a suite structurally incapable
// of failing, and this repo found three of them in twenty-four hours. So every case
// below has an INDEPENDENT authority:
//
//   Case 1  the canonical JSON files          vs  the key list the View can paint
//   Case 2  the DESIGN's own zone ratios      vs  the anchor literals parsed out of source
//   Case 3  the real font's glyph advances    vs  the box each string renders in
//   Case 4  ElarionUiKit.MinTouchPx           vs  the authored entry/cell sizes
//   Case 5  the captured defects              vs  their construction still being absent
//   Case 6  the blank-preview evidence gate   vs  the mount path still asking for it
//
// Case 2's numbers come from the ratified design table (374 / 1496 / 800 of 2670), which
// lives in the work order, NOT in the code being checked - nudge an anchor and the two
// disagree. Case 3 sums the same per-glyph advances TMP steps the pen by, so lengthening
// a sentence moves the number. Case 5 is the half of this ticket that was REMOVAL: the
// empty preview box, the VIEW GEAR ribbon, the 78x72 cell and the tab row are asserted
// GONE, because a redesign whose deletions creep back is a redesign that did not happen.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using DeNelle.Core.UI;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class InventoryArmoryRailRegression
    {
        // ── the artefacts ────────────────────────────────────────────────────
        private const string CanonRes  = "Assets/Resources/Data/Canonical/canon-strings.json";
        private const string CanonStr  = "Assets/StreamingAssets/Data/Canonical/canon-strings.json";
        private const string BuilderSrc = "Assets/_Modules/Village/Hero/InventoryUIBuilder.cs";
        private const string GridSrc    = "Assets/_Modules/Village/Hero/InventoryGrid.cs";
        private const string PaneSrc    = "Assets/_Modules/Village/Hero/InventorySidebar.cs";
        private const string HeaderSrc  = "Assets/_Modules/Village/Hero/InventoryPaperDoll.cs";
        private const string PreviewSrc = "Assets/_Modules/Village/Hero/HeroPreviewViewer.cs";

        // ── THE DESIGN's zone table (WO-1133 D3), the INDEPENDENT authority for Case 2.
        //    These are the ratified device-px widths on the design's own 2670-wide canvas.
        //    They are deliberately NOT read from the code the case is checking.
        private const float DesignCanvasW = 2670f;
        private const float DesignRailW   = 374f;
        private const float DesignStageW  = 1496f;
        private const float DesignPaneW   = 800f;
        /// <summary>Ratio tolerance. Tight enough that a real nudge fails, loose enough for f-suffix rounding.</summary>
        private const float RatioTolerance = 0.004f;

        // ── boxes, each pinned by a source lint in Case 0 ────────────────────
        /// <summary>The framed panel spans x 0.04..0.96 of the modal canvas.</summary>
        private const float PanelWidthFrac = 0.96f - 0.04f;
        /// <summary>A rail entry's label rect inside the entry (BuildRailEntry re-anchors x 0.16..0.98).</summary>
        private const float RailLabelInset = 0.98f - 0.16f;
        /// <summary>ElarionUiKit.BuildObsidianButton insets its label to x 0.04..0.96.</summary>
        private const float ButtonLabelInset = 0.92f;
        /// <summary>The pane CTA spans x 0.06..0.94 of the pane.</summary>
        private const float PaneCtaInset = 0.94f - 0.06f;
        /// <summary>TMP line advance as a multiple of font size (conservative).</summary>
        private const float LineHeightFactor = 1.2f;

        private struct Aspect { public string Name; public float W, H; }
        private static readonly Aspect[] Aspects =
        {
            new Aspect { Name = "2670x1200 (the design + the capture)", W = 2670f, H = 1200f },
            new Aspect { Name = "1920x1080",                            W = 1920f, H = 1080f },
        };

        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("INVENTORY_ARMORY_RAIL_OK - " + reason);
            else Debug.LogError("INVENTORY_ARMORY_RAIL_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                Case(failures, "zones-pinned",   () => Case0_BoxesStillAuthored(failures, notes));
                Case(failures, "canon-parity",   () => Case1_CanonParity(failures, notes));
                Case(failures, "zone-ratios",    () => Case2_ZoneRatios(failures, notes));
                Case(failures, "label-fit",      () => Case3_LabelFit(failures, notes));
                Case(failures, "touch-floor",    () => Case4_TouchFloor(failures, notes));
                Case(failures, "removals",       () => Case5_RemovalsStayRemoved(failures, notes));
                Case(failures, "preview-gate",   () => Case6_PreviewEvidenceGate(failures, notes));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count > 0)
            {
                reason = failures.Count + " failure(s): " + string.Join(" | ", failures);
                return false;
            }
            reason = "Bag/Armory Rail verified - " + string.Join("; ", notes);
            return true;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  CASE 0 - the insets this suite measures against are STILL the insets
        // =====================================================================
        // Without this case the suite would be measuring against numbers it made up.
        // DeNelle.EditorRegression does not reference the View's private layout, so each
        // inset above is pinned to the literal in the file that authors it. Move an inset
        // and this lint fails rather than the measurement silently following it.
        private static void Case0_BoxesStillAuthored(List<string> failures, List<string> notes)
        {
            string src = ReadSrc(BuilderSrc);
            if (src == null) { failures.Add("[zones-pinned] cannot read " + BuilderSrc); return; }

            if (!Regex.IsMatch(src, @"new Vector2\(0\.04f, 0\.03f\), new Vector2\(0\.96f, 0\.97f\)"))
                failures.Add("[zones-pinned] the framed panel is no longer anchored 0.04..0.96 x 0.03..0.97 - " +
                             "PanelWidthFrac (" + PanelWidthFrac.ToString("0.00") + ") is now measuring a box " +
                             "that does not exist, so every width below is wrong");

            if (!Regex.IsMatch(src, @"lrt\.anchorMin = new Vector2\(0\.16f, 0\.42f\)") ||
                !Regex.IsMatch(src, @"lrt\.anchorMax = new Vector2\(0\.98f, 0\.92f\)"))
                failures.Add("[zones-pinned] a rail entry's label is no longer re-anchored to x 0.16..0.98 - " +
                             "RailLabelInset no longer describes the label rect the fit case measures against");

            notes.Add("layout insets pinned to source");
        }

        // =====================================================================
        //  CASE 1 - every sentence exists, in BOTH copies, byte-identical, ASCII
        // =====================================================================
        private static Dictionary<string, string> _authored;

        private static void Case1_CanonParity(List<string> failures, List<string> notes)
        {
            var a = ReadCanon(CanonRes, failures);
            var b = ReadCanon(CanonStr, failures);
            if (a == null || b == null) return;
            _authored = a;

            foreach (string key in InventoryStrings.AllKeys)
            {
                string va, vb;
                bool inA = a.TryGetValue(key, out va);
                bool inB = b.TryGetValue(key, out vb);
                if (!inA || !inB)
                {
                    failures.Add("[canon-parity] '" + key + "' is missing from " +
                                 (!inA && !inB ? "BOTH canonical copies" : (!inA ? CanonRes : CanonStr)) +
                                 " - the Bag would paint the visible [[missing:" + key + "]] marker");
                    continue;
                }
                if (!string.Equals(va, vb, StringComparison.Ordinal))
                    failures.Add("[canon-parity] '" + key + "' DIFFERS between the copies: Resources '" + va +
                                 "' vs StreamingAssets '" + vb + "'. The two must be byte-identical - the " +
                                 "player gets whichever one their platform loads");
                foreach (char c in va)
                    if (c > 127)
                    {
                        failures.Add("[canon-parity] '" + key + "' carries the non-ASCII character U+" +
                                     ((int)c).ToString("X4") + " - TMP renders it as tofu");
                        break;
                    }
            }
            notes.Add(InventoryStrings.AllKeys.Length + " Bag copy keys present in both canonical copies");
        }

        // =====================================================================
        //  CASE 2 - the built zones match the RATIFIED design, not themselves
        // =====================================================================
        // The design's authority is 374 / 1496 / 800 across 2670. The code's claim is four
        // anchor literals. This case parses the literals OUT OF SOURCE and checks the three
        // resulting bands against the design's ratios - two independent statements of the
        // same fact, which is what makes disagreement possible at all.
        private static void Case2_ZoneRatios(List<string> failures, List<string> notes)
        {
            string src = ReadSrc(BuilderSrc);
            if (src == null) { failures.Add("[zone-ratios] cannot read " + BuilderSrc); return; }

            float x0, x1, railX1, stageX1;
            if (!TryConst(src, "ZoneX0", out x0) || !TryConst(src, "ZoneX1", out x1) ||
                !TryConst(src, "RailX1", out railX1) || !TryConst(src, "StageX1", out stageX1))
            {
                failures.Add("[zone-ratios] could not parse ZoneX0/ZoneX1/RailX1/StageX1 out of " + BuilderSrc +
                             " - the three-zone layout is no longer stated as constants, so nothing can " +
                             "check it against the design");
                return;
            }

            float interior = x1 - x0;
            if (interior <= 0f) { failures.Add("[zone-ratios] the panel interior is zero or inverted"); return; }

            // Ordering first: a seam that has crossed its neighbour is a collapsed or overlapping
            // zone, which no ratio check would describe usefully.
            if (!(x0 < railX1 && railX1 < stageX1 && stageX1 <= x1))
            {
                failures.Add(string.Format("[zone-ratios] the seams are out of order: {0:F3} < {1:F3} < {2:F3} <= {3:F3} " +
                                           "is false. The rail, stage and pane overlap or invert.", x0, railX1, stageX1, x1));
                return;
            }

            var got = new[] { (railX1 - x0) / interior, (stageX1 - railX1) / interior, (x1 - stageX1) / interior };
            var want = new[] { DesignRailW / DesignCanvasW, DesignStageW / DesignCanvasW, DesignPaneW / DesignCanvasW };
            var names = new[] { "rail", "stage", "pane" };
            // The design's three widths are quoted against the full 2670 canvas, so renormalise
            // them onto the interior the code actually divides.
            float wantSum = want[0] + want[1] + want[2];
            for (int i = 0; i < 3; i++) want[i] /= wantSum;

            for (int i = 0; i < 3; i++)
            {
                float delta = Mathf.Abs(got[i] - want[i]);
                if (delta > RatioTolerance)
                    failures.Add(string.Format(
                        "[zone-ratios] the {0} band is {1:P2} of the interior but WO-1133 D3 ratifies {2:P2} " +
                        "(delta {3:P2}, tolerance {4:P2}). Either the anchors drifted or the design changed - " +
                        "if the design changed, change the numbers in THIS suite too, deliberately.",
                        names[i], got[i], want[i], delta, RatioTolerance));
            }

            float sum = got[0] + got[1] + got[2];
            if (Mathf.Abs(sum - 1f) > 0.001f)
                failures.Add(string.Format("[zone-ratios] the three bands sum to {0:P2} of the interior, not 100%. " +
                                           "They must ABUT exactly - a gap is the dead black band the redesign " +
                                           "exists to remove, and an overlap paints one zone over another.", sum));

            notes.Add(string.Format("zones rail/stage/pane = {0:P1}/{1:P1}/{2:P1} of the interior, matching D3",
                                    got[0], got[1], got[2]));
        }

        // =====================================================================
        //  CASE 3 - every label FITS its real box, measured, at the legibility floor
        // =====================================================================
        // The captured defect on the sibling HUD was "Tap to collec" and "Manag..." - words
        // cut mid-glyph because the sentence could not fit at ANY legible size. The Bag's
        // rail is the same hazard: seven labels in a 14% column. This measures each one with
        // the real font's advances against the real rect, at BOTH landscape aspects.
        private static void Case3_LabelFit(List<string> failures, List<string> notes)
        {
            float floor = ElarionUiKit.FontFloor;

            string[] railKeys =
            {
                InventoryStrings.KeyRailGear, InventoryStrings.KeyRailWeapons, InventoryStrings.KeyRailArmor,
                InventoryStrings.KeyRailTrinkets, InventoryStrings.KeyRailPotions, InventoryStrings.KeyRailSkills,
                InventoryStrings.KeyRailMap, InventoryStrings.KeyRailHeader,
            };
            string[] wornKeys =
            {
                InventoryStrings.KeySlotMainHand, InventoryStrings.KeySlotOffHand, InventoryStrings.KeySlotArmor,
                InventoryStrings.KeySlotAmulet, InventoryStrings.KeySlotRing, InventoryStrings.KeySlotEmpty,
            };
            string[] actionKeys =
            {
                InventoryStrings.KeyActionEquip, InventoryStrings.KeyActionUse, InventoryStrings.KeyActionWorn,
                InventoryStrings.KeyPaneColumnWorn, InventoryStrings.KeyPaneColumnThis,
                InventoryStrings.KeyPaneWornBadge, InventoryStrings.KeyPaneNoSelection,
            };

            string src = ReadSrc(BuilderSrc);
            float zx0, zx1, railX1, stageX1;
            if (src == null ||
                !TryConst(src, "ZoneX0", out zx0) || !TryConst(src, "ZoneX1", out zx1) ||
                !TryConst(src, "RailX1", out railX1) || !TryConst(src, "StageX1", out stageX1))
            {
                failures.Add("[label-fit] cannot parse the zone constants, so there is no box to measure against");
                return;
            }

            foreach (var a in Aspects)
            {
                float canvasW = a.W / ScaleFactor(a.W, a.H);
                float panelW  = canvasW * PanelWidthFrac;
                float railBox = panelW * (railX1 - zx0) * RailLabelInset;
                float paneBox = panelW * (zx1 - stageX1) * PaneCtaInset * ButtonLabelInset;

                MeasureAll(failures, railKeys, railBox, floor, a.Name, "rail entry",
                    "The rail is 14% of the panel by design; the fix is FEWER LETTERS in " +
                    "canon-strings.json, not a smaller font (FontFloor is a floor, not a budget) " +
                    "and not a wider rail (the stage and pane share its edges)");

                MeasureAll(failures, actionKeys, paneBox, floor, a.Name, "pane action/header",
                    "The pane CTA spans x 0.06..0.94 of a 30% column");

                notes.Add(string.Format("{0}: rail label rect {1:F0} px, pane CTA rect {2:F0} px",
                                        a.Name, railBox, paneBox));
            }

            // The worn-slot keys share the Gear section's slot plates, whose value band is the
            // narrower of the two things the plate carries; measured at the tighter aspect only,
            // because a pass at the narrower box is a pass at the wider one.
            {
                var a = Aspects[1];
                float canvasW = a.W / ScaleFactor(a.W, a.H);
                float stageW  = canvasW * PanelWidthFrac * (stageX1 - railX1);
                // The worn slots occupy x 0.47..1.00 of the stage; the kit Slot insets its cell to
                // 0.06..0.94, and the label band inside that is 0.05..0.95.
                float wornBox = stageW * (1.00f - 0.47f) * (0.94f - 0.06f) * (0.95f - 0.05f);
                MeasureAll(failures, wornKeys, wornBox, ElarionUiKit.FontFloor, a.Name, "worn slot",
                    "A worn slot's key and value share one plate");
                notes.Add(string.Format("worn slot label rect {0:F0} px at {1}", wornBox, a.Name));
            }

            // The long empty-section sentences WRAP by design (they sit in a centred note box
            // that is 92% of the stage and half its height), so the assertion for them is that
            // the wrapped block SEATS - not that one line fits.
            {
                var a = Aspects[0];
                float canvasW = a.W / ScaleFactor(a.W, a.H);
                float canvasH = a.H / ScaleFactor(a.W, a.H);
                float stageW = canvasW * PanelWidthFrac * (stageX1 - railX1) * (0.96f - 0.04f);
                float noteH  = canvasH * (0.97f - 0.03f) * (0.875f - 0.300f) * (0.66f - 0.34f) * (0.94f - 0.06f);

                string[] emptyKeys =
                {
                    InventoryStrings.KeyEmptyWeapons, InventoryStrings.KeyEmptyArmor,
                    InventoryStrings.KeyEmptyTrinkets, InventoryStrings.KeyEmptyPotions,
                    InventoryStrings.KeyEmptySkills, InventoryStrings.KeyEmptyMapLocked,
                    InventoryStrings.KeyPaneGearGaps,
                };
                foreach (string key in emptyKeys)
                {
                    string text = Copy(failures, key);
                    if (string.IsNullOrEmpty(text)) continue;
                    int lines = WrappedLineCount(text, stageW, ElarionUiKit.FontFloor);
                    float needed = lines * ElarionUiKit.FontFloor * LineHeightFactor;
                    if (needed > noteH)
                        failures.Add(string.Format(
                            "[label-fit] the empty-section line '{0}' wraps to {1} lines = {2:F0} ref px at the " +
                            "{3:F0} px floor, but its note box is only {4:F0} px tall - the tail would be culled, " +
                            "and a half-shown sentence telling the player where to GET the item is worse than no " +
                            "sentence. Shorten the words in canon-strings.json.",
                            key, lines, needed, ElarionUiKit.FontFloor, noteH));
                }
                notes.Add(string.Format("empty-section note box {0:F0}x{1:F0} ref px", stageW, noteH));
            }
        }

        private static void MeasureAll(List<string> failures, string[] keys, float boxW, float floor,
                                       string aspect, string what, string advice)
        {
            foreach (string key in keys)
            {
                string text = Copy(failures, key);
                if (string.IsNullOrEmpty(text)) continue;
                string detail;
                float w = ElarionUiKit.MeasureLineWidthPx(ElarionUiKit.FontRole.Body, text, floor, out detail);
                if (w < 0f) { failures.Add("[label-fit] cannot measure '" + key + "': " + detail); continue; }
                if (w > boxW)
                    failures.Add(string.Format(
                        "[label-fit] at {0} the {1} string '{2}' (\"{3}\") MEASURES {4:F0} ref px at the {5:F0} px " +
                        "legibility floor, but its rect is only {6:F0} px ({7}). TMP ellipsises past the floor, so " +
                        "it would be cut on the device. {8}",
                        aspect, what, key, text, w, floor, boxW, detail, advice));
            }
        }

        // =====================================================================
        //  CASE 4 - nothing interactive is below the touch floor
        // =====================================================================
        // MinTouchPx is a FLOOR, and D3 is explicit that relying on ClampMinTouch to rescue a
        // sub-floor element is the 2026-07-16 grey-plate defect class: it inflates and stacks
        // into its neighbour. So the sizes are authored at or above the floor, and this case
        // proves the authored numbers - AND proves the arithmetic that forced the rail to
        // scroll, so a later "simplification" back to a fixed column fails here instead of
        // shipping seven squashed entries.
        private static void Case4_TouchFloor(List<string> failures, List<string> notes)
        {
            string src = ReadSrc(BuilderSrc);
            if (src == null) { failures.Add("[touch-floor] cannot read " + BuilderSrc); return; }

            float entryCount, gapPx;
            if (!TryConst(src, "RailEntryCount", out entryCount)) entryCount = 7f;
            if (!TryConst(src, "RailEntryGapPx", out gapPx)) gapPx = 8f;

            if (!Regex.IsMatch(src, @"RailEntryHeightPx\s*=\s*ElarionUiKit\.MinTouchPx"))
                failures.Add("[touch-floor] RailEntryHeightPx is no longer defined AS ElarionUiKit.MinTouchPx. " +
                             "A rail entry authored at a literal can silently fall under the floor when the " +
                             "floor moves; deriving it from the constant makes that impossible");

            float zy0, zy1;
            if (!TryConst(src, "RailY0", out zy0) || !TryConst(src, "RailY1", out zy1))
            { failures.Add("[touch-floor] cannot parse RailY0/RailY1"); return; }

            foreach (var a in Aspects)
            {
                float canvasH = a.H / ScaleFactor(a.W, a.H);
                float panelH  = canvasH * (0.97f - 0.03f);
                float railH   = panelH * (zy1 - zy0);
                float needed  = entryCount * ElarionUiKit.MinTouchPx + (entryCount - 1f) * gapPx;

                int visible = Mathf.FloorToInt((railH + gapPx) / (ElarionUiKit.MinTouchPx + gapPx));
                if (visible < 1)
                    failures.Add(string.Format("[touch-floor] at {0} the rail band is {1:F0} ref px - not even ONE " +
                                               "entry fits at the {2:F0} px floor", a.Name, railH, ElarionUiKit.MinTouchPx));

                // If the entries DO all fit, the scroll is unnecessary but harmless; if they do not,
                // a scroll is mandatory. Assert the file still has one, because that is the whole
                // reason nothing here is sub-floor.
                if (needed > railH && !Regex.IsMatch(src, @"RailViewport"))
                    failures.Add(string.Format(
                        "[touch-floor] at {0} seven rail entries at the {1:F0} px floor need {2:F0} ref px but the " +
                        "rail band is {3:F0} px, and the scrolling viewport is gone. The entries would have to be " +
                        "{4:F0} px each - under the floor - which is exactly the sub-floor tap target D3 forbids.",
                        a.Name, ElarionUiKit.MinTouchPx, needed, railH, (railH - (entryCount - 1f) * gapPx) / entryCount));

                notes.Add(string.Format("{0}: rail band {1:F0} px, ~{2} of {3:F0} entries visible at the floor",
                                        a.Name, railH, visible, entryCount));
            }

            // The GRID cell is derived at runtime from the measured stage, so what is asserted here
            // is that the derivation still refuses to go under the floor.
            string grid = ReadSrc(GridSrc);
            if (grid == null) { failures.Add("[touch-floor] cannot read " + GridSrc); return; }
            if (!Regex.IsMatch(grid, @"cell\s*<\s*ElarionUiKit\.MinTouchPx"))
                failures.Add("[touch-floor] the grid no longer compares its derived cell against " +
                             "ElarionUiKit.MinTouchPx. The captured defect 4 was tiles far below the floor; " +
                             "without this comparison a narrow stage silently reproduces it");
            if (!Regex.IsMatch(grid, @"Mathf\.Max\(cell,\s*ElarionUiKit\.MinTouchPx\)"))
                failures.Add("[touch-floor] the grid no longer clamps its cell UP to the floor as a last resort");
        }

        // =====================================================================
        //  CASE 5 - the deletions stay deleted (half this ticket was removal)
        // =====================================================================
        private static void Case5_RemovalsStayRemoved(List<string> failures, List<string> notes)
        {
            var banned = new (string File, string Pattern, string Why)[]
            {
                (HeaderSrc,  @"""VIEW GEAR""",
                 "the gold VIEW GEAR ribbon is back. It was painted across a preview box that renders a " +
                 "uniform clear colour, i.e. a broken box sitting on top of a button that opened the working " +
                 "gear screen. The ROUTE survives as the rail's Gear section; the ribbon must not"),
                (HeaderSrc,  @"ViewGearRibbon|ViewGearTap",
                 "the VIEW GEAR ribbon's construction is back in the header partial"),
                (PaneSrc,    @"Tap an item to inspect it",
                 "the pane is narrating the interface again. That sentence was the full-width gold hint bar - " +
                 "visually louder than the two items it described (captured defect 5). The pane says what is " +
                 "worn and what a swap replaces instead"),
                (GridSrc,    @"new Vector2\(78f,\s*72f\)",
                 "the 78x72 grid cell literal is back. It is far below the 112 ref px touch floor and it is " +
                 "captured defect 4. The cell is DERIVED from the measured stage width now"),
                (GridSrc,    @"constraintCount\s*=\s*isLandscape",
                 "the orientation-branching 5/4 column count is back. The build is landscape-locked and the " +
                 "column count is a design constant"),
                (BuilderSrc, @"ElarionUiKit\.BuildTabRow",
                 "the top tab row is back. It is what clipped its own selected label (captured defect 2) and " +
                 "what could not carry per-section counts. The rail replaced it"),
            };

            foreach (var b in banned)
            {
                string raw = ReadSrc(b.File);
                if (raw == null) { failures.Add("[removals] cannot read " + b.File); continue; }
                string code = StripComments(raw);
                if (Regex.IsMatch(code, b.Pattern))
                    failures.Add("[removals] " + Path.GetFileName(b.File) + ": " + b.Why);
            }

            // And the positive half: the route the ribbon used must STILL exist, just relocated.
            string builder = ReadSrc(BuilderSrc);
            if (builder != null && !Regex.IsMatch(StripComments(builder),
                    @"PanelRouter\.Open\(\s*DeNelle\.Core\.UI\.PanelId\.EquipmentPanel\s*\)"))
                failures.Add("[removals] the Bag no longer routes to PanelId.EquipmentPanel at all. The design " +
                             "PROMOTES the gear view rather than cutting it - deleting the door was the point, " +
                             "deleting the room was not");

            notes.Add(banned.Length + " removed constructions still absent; the EquipmentPanel route still present");
        }

        // =====================================================================
        //  CASE 6 - a blank preview can never be mounted again
        // =====================================================================
        // THE DEFECT THIS PINS, from captured data: F8 seq 2833 and seq 3585 both recorded
        // "RT PROBE: the preview render texture is a UNIFORM clear colour". The camera clears
        // to a colour byte-identical to the plate behind it, so a rig that drew NOTHING and a
        // rig that drew a hero are the same pixels - to a screenshot, to a gate, and to the
        // player. The only defence is to ASK before mounting.
        private static void Case6_PreviewEvidenceGate(List<string> failures, List<string> notes)
        {
            string preview = ReadSrc(PreviewSrc);
            if (preview == null) { failures.Add("[preview-gate] cannot read " + PreviewSrc); return; }
            string pcode = StripComments(preview);

            if (!Regex.IsMatch(pcode, @"void\s+ProbeRenderedContent\s*\("))
                failures.Add("[preview-gate] HeroPreviewViewer.ProbeRenderedContent is gone - the readback that " +
                             "produced the only decisive line we have about the blank preview");
            if (!Regex.IsMatch(pcode, @"bool\s+DrewContent\s*\("))
                failures.Add("[preview-gate] HeroPreviewViewer.DrewContent is gone - callers can no longer ask " +
                             "whether the rig drew, so the only thing left to do is mount and hope");
            if (!Regex.IsMatch(pcode, @"bool\s+TryMeasureDrawn\s*\("))
                failures.Add("[preview-gate] the shared TryMeasureDrawn readback is gone - if the probe and the " +
                             "gate measure separately they can disagree, and then the trace describes a " +
                             "different texture than the one the decision was made on");

            string builder = ReadSrc(BuilderSrc);
            if (builder == null) { failures.Add("[preview-gate] cannot read " + BuilderSrc); return; }
            string bcode = StripComments(builder);

            int gate = bcode.IndexOf("DrewContent", StringComparison.Ordinal);
            int mount = bcode.IndexOf("_heroPreviewImage.texture", StringComparison.Ordinal);
            if (gate < 0)
                failures.Add("[preview-gate] the Bag mounts the hero preview WITHOUT asking DrewContent. That is " +
                             "exactly how the owner's empty navy rectangle shipped: the mount cannot tell a hero " +
                             "from an empty frustum, because they are the same pixels");
            else if (mount >= 0 && gate > mount)
                failures.Add("[preview-gate] DrewContent is called AFTER the texture is assigned - the gate has to " +
                             "come first or it is not a gate, just a log line next to a mounted blank box");

            if (!Regex.IsMatch(bcode, @"DrewContent\([^)]*""Inventory"""))
                failures.Add("[preview-gate] the Bag's probe no longer tags its lines \"Inventory\". WO-1133 D1 " +
                             "required BOTH preview call sites to be probed precisely because EquipmentPanel " +
                             "tags its lines \"Equip\" - one tag for two surfaces means a future capture cannot " +
                             "say which one was blank, which is the ambiguity that cost this ticket a day");

            notes.Add("preview mount is evidence-gated and tagged per call site");
        }

        // =====================================================================
        //  helpers
        // =====================================================================

        /// <summary>The authored canon text for a key (Case 1 fills the map).</summary>
        private static string Copy(List<string> failures, string key)
        {
            string raw;
            if (_authored != null && _authored.TryGetValue(key, out raw)) return raw;
            failures.Add("[label-fit] canon key '" + key + "' is absent from " + CanonRes +
                         " - nothing to measure (see the canon-parity failures)");
            return null;
        }

        /// <summary>Parse a `private const [float|int] NAME = 1.23f;` value out of source.</summary>
        private static bool TryConst(string src, string name, out float value)
        {
            value = 0f;
            var m = Regex.Match(src, @"\b" + Regex.Escape(name) + @"\s*=\s*(-?\d+(?:\.\d+)?)f?\s*[;,]");
            if (!m.Success) return false;
            return float.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float,
                                  System.Globalization.CultureInfo.InvariantCulture, out value);
        }

        /// <summary>The CanvasScaler's own math (1080x1920, MatchWidthOrHeight 0.5).</summary>
        private static float ScaleFactor(float screenW, float screenH)
        {
            return Mathf.Pow(screenW / 1080f, 0.5f) * Mathf.Pow(screenH / 1920f, 0.5f);
        }

        /// <summary>Greedy word wrap using the SAME measured advances, so the line count asserted
        /// against is the line count TMP would produce - not a guess at one.</summary>
        private static int WrappedLineCount(string text, float boxW, float fontSize)
        {
            if (string.IsNullOrEmpty(text) || boxW <= 0f) return 0;
            string[] words = text.Split(' ');
            int lines = 1;
            string current = "";
            foreach (string word in words)
            {
                string candidate = current.Length == 0 ? word : current + " " + word;
                string detail;
                float w = ElarionUiKit.MeasureLineWidthPx(ElarionUiKit.FontRole.Body, candidate, fontSize, out detail);
                if (w > boxW && current.Length > 0) { lines++; current = word; }
                else current = candidate;
            }
            return lines;
        }

        private static string ReadSrc(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path) : null; }
            catch { return null; }
        }

        /// <summary>Strip // and /* */ so a rule quoted in a comment is never mistaken for code.</summary>
        private static string StripComments(string src)
        {
            if (string.IsNullOrEmpty(src)) return "";
            src = Regex.Replace(src, @"/\*.*?\*/", "", RegexOptions.Singleline);
            src = Regex.Replace(src, @"//[^\n]*", "");
            return src;
        }

        /// <summary>Flat "key": "value" reader — the canonical copies are one pair per line.</summary>
        private static Dictionary<string, string> ReadCanon(string path, List<string> failures)
        {
            string raw = ReadSrc(path);
            if (raw == null) { failures.Add("[canon-parity] cannot read " + path); return null; }
            var map = new Dictionary<string, string>();
            foreach (string line in raw.Replace("\r\n", "\n").Split('\n'))
            {
                string t = line.Trim();
                if (t.Length < 5 || t[0] != '"') continue;
                int keyEnd = t.IndexOf('"', 1);
                if (keyEnd <= 1) continue;
                int colon = t.IndexOf(':', keyEnd);
                if (colon < 0) continue;
                string rest = t.Substring(colon + 1).Trim();
                if (rest.Length < 2 || rest[0] != '"') continue;
                int valEnd = rest.LastIndexOf('"');
                if (valEnd <= 0) continue;
                map[t.Substring(1, keyEnd - 1)] = Unescape(rest.Substring(1, valEnd - 1));
            }
            return map;
        }

        private static string Unescape(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    i++;
                    if (s[i] == 'n') sb.Append('\n');
                    else if (s[i] == 't') sb.Append('\t');
                    else sb.Append(s[i]);
                }
                else sb.Append(s[i]);
            }
            return sb.ToString();
        }
    }
}
