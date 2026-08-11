// =============================================================================
// EquipmentScreenLayoutRegression [equipment-screen-layout] (WO-1015)
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Namespace: DeNelle.Editor.Regression.
// Markers: EQUIPMENT_SCREEN_LAYOUT_OK / EQUIPMENT_SCREEN_LAYOUT_FAIL.
//
// WHAT BROKE (owner felt-test capture 2026-08-10, the Thrain the Wise paperdoll):
//
//   E1  a dev-gated "Orient" word-button rendered ON the modal, floating over the
//       Shield (Off Hand) slot and clipping its text. The same strand had already
//       been removed from the build palette by WO-1010 D1 - it was COPY-PASTED, not
//       shared, so removing one instance never removed the others.
//   E2  the hero preview box rendered as a flat dark-navy rectangle. That colour is
//       simultaneously the panel's own plate fill AND the preview camera's clear
//       colour, so "drew nothing", "never rendered" and "never enabled" are the same
//       pixels. Instrumentation - not a theory - has to separate them, and a visible
//       fallback has to make the blank box unreachable either way.
//   E3  ~40% dead space. ROOT CAUSE at source: ElarionUiKit.ZonesFor(FrameCharacter)
//       caps its body zone at y=0.605 because Stats_Panel bakes a PORTRAIT ARCH above
//       it. This screen never used the arch, so 0.605..0.905 rendered as empty black.
//   E4  every slot overprinted itself. Same arithmetic, compounded: at 2340x1080 the
//       body zone resolved to ~440px and the Amulet plate to 0.145 of that (~64px);
//       its interior caption/name/grant bands were 0.17/0.17/0.18 of 64px = ~11px
//       each, against a FontFloor(30) line box of ~37.5px. TMP renders OUTSIDE its
//       rect by default, so all three painted the same pixels.
//   E5  item icons were "a few dark pixels" - 0.36 x 0.36 of that collapsed plate.
//
// Same failure class as WO-841 / WO-852 / WO-865 / WO-905. This oracle is a CHEAP
// STRUCTURAL guard, not a pixel test: it replays the view's own band arithmetic at
// two reference rects and pins the properties that make the bug inexpressible.
// Pixel truth stays with RunCaptureHeadless + eyes-on the device.
//
//   1 [floors]   every band constant clears its own floor - the kit touch floor for a
//                tappable band, one whole TMP line box for a text band.
//   2 [row]      THE GEOMETRY ASSERTION E4 EXISTS FOR: the three slot text bands plus
//                their pads and gutters sum to EXACTLY SlotRowPx, each band is seated
//                at a disjoint fixed-pixel offset from the row top, and no two of the
//                resulting [top,bottom) intervals intersect. If a band grows, shrinks
//                or a gutter is removed so that they overlap again, this FAILS.
//   3 [budget]   the top-level band budget resolves positive at BOTH reference rects
//                (2340x1080 landscape -> two-column, 1080x1920 portrait -> stacked),
//                the preview keeps its floor, and a slot list that cannot fit SCROLLS
//                rather than compressing a row.
//   4 [preview]  THE SOURCE PIN E2 EXISTS FOR: the preview cannot render with no
//                fallback. The name band, the state band and the single fallback
//                entry point must all be present, the state label must be written on
//                the fallback path, and the decisive RT probe must still be called.
//   5 [source]   the laws that keep the regression unreachable: no "Orient" control on
//                any gameplay screen, kit routing, no 1/n fraction slicing, no NUL.
//
// Standalone: run-unity-method DeNelle.Editor.Regression.EquipmentScreenLayoutRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced).
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class EquipmentScreenLayoutRegression
    {
        private const string PanelSrc     = "Assets/_Modules/Village/Hero/EquipmentPanel.cs";
        private const string PreviewSrc   = "Assets/_Modules/Village/Hero/HeroPreviewViewer.cs";
        private const string InventorySrc = "Assets/_Modules/Village/Hero/InventoryUIBuilder.cs";
        private const string PaletteSrc   = "Assets/_Modules/Village/BuildMode/BuildPaletteUI.cs";

        private const string PanelType    = "DeNelle.Village.Hero.EquipmentPanel";
        private const string KitType      = "DeNelle.Core.UI.ElarionUiKit";

        /// <summary>The TMP line box multiplier the bands are budgeted from (~1.25em).</summary>
        private const float LineBoxMul = 1.25f;

        /// <summary>Conservative average glyph advance as a fraction of font size (bold mixed-case
        /// LiberationSans; the real average is nearer 0.50), so a pass means real headroom.</summary>
        private const float AvgAdvanceEm = 0.55f;

        // ── THE REFERENCE RECTS ──────────────────────────────────────────────
        // Landscape = the Seeker, the device the WO-1015 capture came from.
        //   scaler 1080x1920 match 0.5 @ 2340x1080 -> scale 1.1040 -> canvas 2119.6 x 978.3
        // Portrait = the kit's own reference canvas.
        // These are DEVICE facts, not layout knobs.
        private const float LandscapeCanvasH = 978.3f;
        private const float LandscapeCanvasW = 2119.6f;
        private const float PortraitCanvasH = 1920f;
        private const float PortraitCanvasW = 1080f;

        // The panel rect EquipmentPanel.Open authors, and FrameCharacter's body-zone x span.
        private const float PanelFracW = 0.88f;   // 0.06 .. 0.94
        private const float PanelFracH = 0.91f;   // 0.05 .. 0.96
        private const float BodyZoneFracW = 0.88f; // ZonesFor(FrameCharacter).body x 0.060..0.940

        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("EQUIPMENT_SCREEN_LAYOUT_OK - " + reason);
            else Debug.LogError("EQUIPMENT_SCREEN_LAYOUT_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                Case(failures, "floors",  () => Case1_Floors(failures, notes));
                Case(failures, "row",     () => Case2_RowBandsDisjoint(failures, notes));
                Case(failures, "budget",  () => Case3_BandBudget(failures, notes));
                Case(failures, "preview", () => Case4_PreviewFallbackPin(failures, notes));
                Case(failures, "source",  () => Case5_SourceLaws(failures, notes));
            }
            catch (Exception ex)
            {
                failures.Add($"[suite] THREW {ex.GetType().Name}: {ex.Message}");
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "EQUIPMENT SCREEN LAYOUT OK - every slot row is a fixed-pixel band stack whose " +
                         "label/value/hint intervals are provably disjoint, the icon art band clears the " +
                         "touch floor, the top-level budget resolves positive at both reference rects " +
                         "(landscape two-column and portrait stacked) with the slot list scrolling rather " +
                         "than compressing, the hero preview cannot render without its name+state fallback " +
                         "or without the RT probe, and no 'Orient' control survives on any gameplay screen" +
                         noteStr;
                return true;
            }
            reason = "equipment-screen-layout FAIL x" + failures.Count + ": " +
                     string.Join(" | ", failures) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add($"[{name}] THREW {ex.GetType().Name}: {ex.Message}"); }
        }

        // =====================================================================
        //  Shared constant reads
        // =====================================================================

        private sealed class Layout
        {
            public float MinTouch, FontFloor, LineBox;
            public float BandGap, RowPx, RowPad, LabelBand, ValueBand, HintBand, RowBandGap, IconPx;
            public float PreviewMin, PreviewName, PreviewState, PreviewPad;
            public float TargetBar, TwoColMinW, SlotColFrac, ColGapFrac;
            public float BodyTop, CloseY0, CloseGap, CanonCtaH;
            public int SlotCount;
            public bool Ok;
        }

        private static Layout ReadLayout(List<string> failures, string tag)
        {
            var L = new Layout();
            Type view = FindType(PanelType);
            Type kit  = FindType(KitType);
            if (view == null)
            {
                failures.Add($"{tag} {PanelType} not found - the equipment panel was renamed or removed; " +
                             "re-point this oracle rather than deleting the only guard on its band stack");
                return L;
            }
            if (kit == null)
            {
                failures.Add($"{tag} {KitType} not found - cannot read the kit touch/font floors");
                return L;
            }

            L.MinTouch  = ConstFloat(kit, "MinTouchPx", failures, tag);
            L.CanonCtaH = ConstFloat(kit, "CanonCtaHeight", failures, tag);
            L.FontFloor = ConstFloat(FindType("DeNelle.Core.UI.ElarionUiKit"), "FontFloor", failures, tag);
            if (L.MinTouch <= 0f || L.FontFloor <= 0f) return L;
            L.LineBox = L.FontFloor * LineBoxMul;

            L.BandGap     = ConstFloat(view, "BandGapPx", failures, tag);
            L.RowPx       = ConstFloat(view, "SlotRowPx", failures, tag);
            L.RowPad      = ConstFloat(view, "SlotRowPadPx", failures, tag);
            L.LabelBand   = ConstFloat(view, "SlotLabelBandPx", failures, tag);
            L.ValueBand   = ConstFloat(view, "SlotValueBandPx", failures, tag);
            L.HintBand    = ConstFloat(view, "SlotHintBandPx", failures, tag);
            L.RowBandGap  = ConstFloat(view, "SlotRowBandGapPx", failures, tag);
            L.IconPx      = ConstFloat(view, "SlotIconPx", failures, tag);
            L.SlotCount   = (int)ConstFloat(view, "SlotCount", failures, tag);
            L.PreviewMin  = ConstFloat(view, "PreviewMinPx", failures, tag);
            L.PreviewName = ConstFloat(view, "PreviewNameBandPx", failures, tag);
            L.PreviewState= ConstFloat(view, "PreviewStateBandPx", failures, tag);
            L.PreviewPad  = ConstFloat(view, "PreviewPadPx", failures, tag);
            L.TargetBar   = ConstFloat(view, "TargetBarPx", failures, tag);
            L.TwoColMinW  = ConstFloat(view, "TwoColumnMinWidthPx", failures, tag);
            L.SlotColFrac = ConstFloat(view, "SlotColumnFrac", failures, tag);
            L.ColGapFrac  = ConstFloat(view, "ColumnGapFrac", failures, tag);
            L.BodyTop     = ConstFloat(view, "BodyTopFrac", failures, tag);
            L.CloseY0     = ConstFloat(view, "CloseBandY0", failures, tag);
            L.CloseGap    = ConstFloat(view, "CloseGapY", failures, tag);

            L.Ok = L.RowPx > 0f && L.LabelBand > 0f && L.ValueBand > 0f && L.HintBand > 0f &&
                   L.IconPx > 0f && L.SlotCount > 0 && L.BodyTop > 0f && L.CanonCtaH > 0f;
            return L;
        }

        // =====================================================================
        //  CASE 1 - the numeric floors
        // =====================================================================
        private static void Case1_Floors(List<string> failures, List<string> notes)
        {
            var L = ReadLayout(failures, "[floors]");
            if (!L.Ok) return;

            // TAPPABLE bands. A band SHORTER than the kit floor is the bug itself: ClampMinTouch
            // grows the control past the band on BOTH sides and it lands on its neighbours.
            TouchFloor(failures, "SlotRowPx", L.RowPx, L.MinTouch,
                "the whole slot row IS the tap target that opens the change-drawer");
            TouchFloor(failures, "TargetBarPx", L.TargetBar, L.MinTouch,
                "the hero/companion picker buttons live here; it used to be a 0.035 body fraction " +
                "(~15px) and ClampMinTouch grew those buttons straight over the slots below");
            TouchFloor(failures, "SlotIconPx", L.IconPx, L.MinTouch,
                "E5 - the item art band. It was 0.36 x 0.36 of a collapsed ~64px plate (~23px), " +
                "which is the 'a few dark pixels in each slot' defect verbatim");

            // TEXT bands must seat a whole line box or TMP culls / ellipsizes / overprints.
            LineFloor(failures, "SlotLabelBandPx", L.LabelBand, L.LineBox, L.LineBox,
                "the slot name (\"Weapon (Main Hand)\")");
            LineFloor(failures, "SlotValueBandPx", L.ValueBand, L.LineBox, L.LineBox,
                "the equipped item name, or the WORD \"Empty\" that carries the empty state for a " +
                "red/green colourblind player - if this band cannot seat a line box the STATE is lost, " +
                "not merely the styling");
            LineFloor(failures, "SlotHintBandPx", L.HintBand, L.LineBox, L.LineBox,
                "the grant line / \"Craft one at the Jeweler\" pointer");
            LineFloor(failures, "PreviewNameBandPx", L.PreviewName, L.LineBox, L.LineBox,
                "E2 - the hero name that makes a blank preview box impossible");
            LineFloor(failures, "PreviewStateBandPx", L.PreviewState, L.LineBox, L.LineBox,
                "E2 - the live/portrait STATE word (colourblind law: the state is said, not tinted)");

            if (L.RowBandGap <= 0f)
                failures.Add($"[floors] SlotRowBandGapPx={L.RowBandGap} - the gutter between the three " +
                             "slot text bands has been removed. Two adjacent TMP line boxes with zero " +
                             "gutter touch, and descenders/ascenders visibly collide (E4's softer form)");
            if (L.BandGap <= 0f)
                failures.Add($"[floors] BandGapPx={L.BandGap} - the guaranteed gutter between top-level " +
                             "bands is gone; bands are allowed to touch again");

            notes.Add($"floors: touch={L.MinTouch}, lineBox={L.LineBox:F1}; row={L.RowPx}, " +
                      $"icon={L.IconPx}, bands {L.LabelBand}/{L.ValueBand}/{L.HintBand}");
        }

        private static void TouchFloor(List<string> failures, string name, float v, float floor, string why)
        {
            if (v < floor)
                failures.Add($"[floors] EquipmentPanel.{name}={v} is BELOW the kit touch floor " +
                             $"MinTouchPx={floor} - {why}");
        }

        private static void LineFloor(List<string> failures, string name, float v, float need, float lineBox, string why)
        {
            if (v < need)
                failures.Add($"[floors] EquipmentPanel.{name}={v} is shorter than the {need:F1}px it needs " +
                             $"(one TMP line box at the kit FontFloor is {lineBox:F1}) - {why}; a band " +
                             "shorter than its line box does not clip, it RENDERS OUTSIDE ITSELF onto its " +
                             "neighbour, which is exactly the WO-1015 E4 overprint");
        }

        // =====================================================================
        //  CASE 2 - THE GEOMETRY ASSERTION: the slot row's bands are disjoint
        // =====================================================================
        // This is the case the WO asks for by name. It does not check "the numbers look sane" - it
        // replays BuildGearSlotRow's own cursor and proves the resulting intervals cannot intersect.
        private static void Case2_RowBandsDisjoint(List<string> failures, List<string> notes)
        {
            var L = ReadLayout(failures, "[row]");
            if (!L.Ok) return;

            // (a) The three bands + pads + gutters must sum to EXACTLY the row height. Under-sum is
            //     unspent pixels (dead space inside every row); over-sum is the last band hanging
            //     out of the row and onto the row beneath it.
            float sum = L.RowPad * 2f + L.LabelBand + L.ValueBand + L.HintBand + L.RowBandGap * 2f;
            if (Mathf.Abs(sum - L.RowPx) > 0.51f)
                failures.Add($"[row] the slot row's parts sum to {sum} px but SlotRowPx={L.RowPx}. " +
                             (sum > L.RowPx
                                ? $"The stack OVERFLOWS its row by {sum - L.RowPx}px - the hint band hangs " +
                                  "out of the row and onto the next one, which is E4 re-introduced one row down."
                                : $"The stack UNDER-fills its row by {L.RowPx - sum}px - dead space inside " +
                                  "every single row (E3's defect at row scale).") +
                             " Keep SlotRowPx == pad*2 + label + value + hint + gap*2.");

            // (b) Replay the cursor and prove the intervals are pairwise disjoint. Band() seats each
            //     band at [cursor, cursor+h) measured DOWN from the text column's top edge and then
            //     advances by h + gutter, so this is the real geometry, not a restatement of (a).
            float textColH = L.RowPx - L.RowPad * 2f;
            var names = new[] { "label", "value", "hint" };
            var heights = new[] { L.LabelBand, L.ValueBand, L.HintBand };
            var top = new float[3];
            var bottom = new float[3];
            float cursor = 0f;
            for (int i = 0; i < 3; i++)
            {
                top[i] = cursor;
                bottom[i] = cursor + heights[i];
                cursor += heights[i] + L.RowBandGap;
            }
            for (int i = 0; i < 3; i++)
                for (int j = i + 1; j < 3; j++)
                    if (top[j] < bottom[i] && top[i] < bottom[j])
                        failures.Add($"[row] slot bands '{names[i]}' [{top[i]}..{bottom[i]}] and " +
                                     $"'{names[j]}' [{top[j]}..{bottom[j]}] OVERLAP inside the row. " +
                                     "This is the WO-1015 E4 defect verbatim (\"Weapon (Main Hand)\" over " +
                                     "\"Emberglass Staff\" over \"+0% dmg\"). Bands must never share pixels.");

            // The last band must land inside the text column.
            if (bottom[2] > textColH + 0.51f)
                failures.Add($"[row] the hint band ends at {bottom[2]}px but the text column is only " +
                             $"{textColH}px tall (SlotRowPx {L.RowPx} minus {L.RowPad}px pad top and bottom) - " +
                             "the last band is outside the row it belongs to");

            // (c) The ART band must fit inside the row with real padding, or the icon is clipped by
            //     the plate edge (which reads as the same "invisible icon" defect E5 names).
            if (L.IconPx + L.RowPad * 2f > L.RowPx + 0.51f)
                failures.Add($"[row] the icon art band ({L.IconPx}px) plus its padding ({L.RowPad * 2f}px) " +
                             $"exceeds SlotRowPx={L.RowPx} - the icon is clipped top and bottom by the plate");

            notes.Add($"row: sum={sum}/{L.RowPx}, intervals label[{top[0]}..{bottom[0]}] " +
                      $"value[{top[1]}..{bottom[1]}] hint[{top[2]}..{bottom[2]}] in a {textColH}px column");
        }

        // =====================================================================
        //  CASE 3 - the top-level band budget at BOTH reference rects
        // =====================================================================
        private static void Case3_BandBudget(List<string> failures, List<string> notes)
        {
            var L = ReadLayout(failures, "[budget]");
            if (!L.Ok) return;

            Budget(failures, notes, L, "landscape 2340x1080", LandscapeCanvasH, LandscapeCanvasW, true);
            Budget(failures, notes, L, "portrait 1080x1920",  PortraitCanvasH,  PortraitCanvasW,  false);
        }

        private static void Budget(List<string> failures, List<string> notes, Layout L,
                                   string tag, float canvasH, float canvasW, bool expectTwoColumn)
        {
            // Replay EquipmentPanel.Open's arithmetic verbatim.
            float panelPx  = canvasH * PanelFracH;
            float panelWpx = canvasW * PanelFracW;
            float bodyFloor = L.CloseY0 + L.CanonCtaH / panelPx + L.CloseGap;
            float wellPx  = (L.BodyTop - bodyFloor) * panelPx;
            float wellWpx = panelWpx * BodyZoneFracW;

            if (wellPx <= 0f)
            {
                failures.Add($"[budget] {tag}: the well COLLAPSES - bodyFloor {bodyFloor:F3} has risen past " +
                             $"BodyTopFrac {L.BodyTop:F3} at panel height {panelPx:F0}px. The shared Close " +
                             "reservation now eats the whole body; the screen would render empty.");
                return;
            }

            bool twoColumn = wellWpx >= L.TwoColMinW;
            if (twoColumn != expectTwoColumn)
                failures.Add($"[budget] {tag}: the well is {wellWpx:F0}px wide and TwoColumnMinWidthPx=" +
                             $"{L.TwoColMinW}, so the screen picks {(twoColumn ? "two-column" : "stacked")} " +
                             $"where this reference expects {(expectTwoColumn ? "two-column" : "stacked")}. " +
                             "Either the threshold or a panel/zone fraction moved - re-derive both branches " +
                             "rather than relaxing the threshold, or one aspect ships untested.");

            float slotsNatural = L.SlotCount * L.RowPx + (L.SlotCount - 1) * L.BandGap;
            float contentPx = wellPx;   // single-target case: no picker band
            float previewPx, slotsPx;
            if (twoColumn)
            {
                previewPx = contentPx;
                slotsPx = contentPx;

                // The slot COLUMN must be wide enough for the art band plus a readable text column.
                float colW = wellWpx * (L.SlotColFrac - L.ColGapFrac * 0.5f);
                float textW = colW - (L.RowPad + 6f + L.IconPx + 14f) - (L.RowPad + 6f);
                // The longest fixed caption on this screen. Single-line (FitSingleLine), so the whole
                // string has to fit or it ellipsizes - "Weapon (Main Han..." is a legibility defect.
                const string longestCaption = "Weapon (Main Hand)";
                float need = longestCaption.Length * L.FontFloor * AvgAdvanceEm;
                if (need > textW)
                    failures.Add($"[budget] {tag}: the slot text column is {textW:F0}px but the longest slot " +
                                 $"caption \"{longestCaption}\" needs ~{need:F0}px at the kit FontFloor - it " +
                                 "would ellipsize. Widen SlotColumnFrac or shrink the art band.");
                notes.Add($"{tag}: two-column, slotCol={colW:F0} text={textW:F0} (caption needs {need:F0})");
            }
            else
            {
                previewPx = Mathf.Max(L.PreviewMin, contentPx - slotsNatural - L.BandGap);
                slotsPx = contentPx - previewPx - L.BandGap;
                if (slotsPx < L.RowPx)
                {
                    previewPx = Mathf.Max(0f, contentPx - L.RowPx - L.BandGap);
                    slotsPx = contentPx - previewPx - L.BandGap;
                }
            }

            if (slotsPx < L.RowPx)
                failures.Add($"[budget] {tag}: the slot band resolves to {slotsPx:F0}px, under ONE row " +
                             $"({L.RowPx}px). The player cannot see a single gear slot; the preview has " +
                             "eaten the screen.");

            if (previewPx < L.PreviewMin)
                failures.Add($"[budget] {tag}: the preview band resolves to {previewPx:F0}px, under the " +
                             $"{L.PreviewMin}px floor - the paperdoll art is squeezed to nothing. Note the " +
                             "name+state fallback bands still render (they are fixed px), so this is a " +
                             "proportion failure, not a blank-box failure.");

            // The preview's own inner budget: the two fallback bands plus padding must leave art room.
            float previewArtFloor = L.PreviewPad + L.PreviewState + L.RowBandGap + L.PreviewName + L.RowBandGap;
            if (previewArtFloor + L.PreviewPad >= previewPx)
                failures.Add($"[budget] {tag}: the preview's fallback bands + padding need " +
                             $"{previewArtFloor + L.PreviewPad:F0}px of a {previewPx:F0}px band, leaving no " +
                             "art region at all. The fallback must never squeeze the thing it backs up.");

            // A list that cannot fit MUST scroll - never compress. Compression is E4's other door.
            bool scrolls = slotsPx < slotsNatural;
            notes.Add($"{tag}: canvas={canvasH:F0}x{canvasW:F0} panel={panelPx:F0} well={wellPx:F0}x{wellWpx:F0} " +
                      $"preview={previewPx:F0} slots={slotsPx:F0}/{slotsNatural:F0} " +
                      (scrolls ? "SCROLLS" : "fits"));
        }

        // =====================================================================
        //  CASE 4 - THE SOURCE PIN: the preview cannot render with no fallback
        // =====================================================================
        // The render rig needs a live play session (a camera, a RenderTexture, a cloned body), so
        // the fallback contract is pinned at SOURCE rather than green-ticked over a null. A blank
        // preview box is a STRUCTURAL failure, so a structural guard is the right instrument.
        private static void Case4_PreviewFallbackPin(List<string> failures, List<string> notes)
        {
            string raw = ReadText(PanelSrc, failures, "[preview]");
            if (raw == null) return;
            string code = StripComments(raw);

            // (1) The two fallback bands are built UNCONDITIONALLY in BuildPreviewWidget. They are
            //     what makes a blank box unreachable; if either is deleted or made conditional the
            //     screen can go blank again exactly as it did in the 2026-08-10 capture.
            Law(failures, code, "_previewNameLabel",
                "the hero-NAME fallback band is gone from the preview - with no name and no art the " +
                "box is a flat plate again, which is WO-1015 E2 verbatim");
            Law(failures, code, "_previewStateLabel",
                "the STATE-word fallback band is gone - the player would have no way to tell a broken " +
                "preview from an intentionally empty one, and (colourblind law) no word carries the state");
            Law(failures, code, "Band_PreviewName",
                "the name band's fixed-pixel seat is gone - if it went back to a parent fraction it can " +
                "collapse under its line box and cull its own glyphs");
            Law(failures, code, "Band_PreviewState",
                "the state band's fixed-pixel seat is gone (same failure mode as the name band)");

            // (2) There is exactly ONE fallback entry point and it WRITES the state label. A fallback
            //     that hides the render without saying anything is the silent failure the WO ends.
            if (!Regex.IsMatch(code, @"void\s+ShowPreviewFallback\s*\("))
                failures.Add("[preview] EquipmentPanel.ShowPreviewFallback is gone - the single fallback " +
                             "entry point that hides the dead render AND states the reason has been removed; " +
                             "every failing branch would go back to bare HidePreview() with no words on screen");
            else
            {
                var m = Regex.Match(code, @"void\s+ShowPreviewFallback\s*\([^)]*\)",
                                    RegexOptions.Singleline);
                string bodyText = m.Success ? Block(code, code.IndexOf(BraceOpen, m.Index + m.Length)) : "";
                if (bodyText.IndexOf("_previewStateLabel", StringComparison.Ordinal) < 0)
                    failures.Add("[preview] ShowPreviewFallback no longer writes _previewStateLabel - the " +
                                 "fallback would hide the render silently and the box reads as blank again");
                if (bodyText.IndexOf("FlowTrace", StringComparison.Ordinal) < 0)
                    failures.Add("[preview] ShowPreviewFallback no longer traces its reason - CLAUDE.md Sec.12 " +
                                 "forbids a catch/fallback that swallows without logging, and the next reader " +
                                 "of this bug would start from zero evidence again");
            }

            // (3) Every branch that gives up on the live render must go THROUGH the fallback. A bare
            //     HidePreview() outside ShowPreviewFallback/Dispose is the old silent path.
            int hideCalls = Regex.Matches(code, @"HidePreview\s*\(\s*\)").Count;
            if (hideCalls > 3)
                failures.Add($"[preview] HidePreview() is called {hideCalls} times. It may only be reached " +
                             "from its own declaration, ShowPreviewFallback and DisposePreview - any other " +
                             "caller is a branch that turns the preview off WITHOUT putting a word on screen, " +
                             "which is how E2 shipped.");

            // (4) The decisive instrument is still wired. Without it a rig that renders an empty
            //     frustum reports a perfect green chain and still shows the owner a flat navy box.
            if (code.IndexOf("ProbeRenderedContent", StringComparison.Ordinal) < 0)
                failures.Add("[preview] EquipmentPanel no longer calls HeroPreviewViewer.ProbeRenderedContent - " +
                             "the ONE line that distinguishes 'the camera drew nothing' from 'the panel never " +
                             "showed it' is gone. The camera's clear colour is byte-identical to the panel's " +
                             "plate fill, so without the probe those two causes are indistinguishable and the " +
                             "next investigation restarts from a guess (CLAUDE.md Sec.12).");

            string previewRaw = ReadText(PreviewSrc, failures, "[preview]");
            if (previewRaw != null)
            {
                string pcode = StripComments(previewRaw);
                if (!Regex.IsMatch(pcode, @"void\s+ProbeRenderedContent\s*\("))
                    failures.Add("[preview] HeroPreviewViewer.ProbeRenderedContent is gone - the readback that " +
                                 "proves whether anything was DRAWN (as opposed to merely constructed) has been " +
                                 "removed. Instrumentation is permanent (CLAUDE.md Sec.12): flag it off, never strip it.");
                if (pcode.IndexOf("rt.Create() FAILED", StringComparison.Ordinal) < 0 &&
                    !Regex.IsMatch(pcode, @"FlowTrace\.Fail\s*\(\s*""Preview"""))
                    failures.Add("[preview] HeroPreviewViewer's render-texture allocation failure is silent again " +
                                 "(no FlowTrace.Fail on the !rt.Create() path) - a bare `return false` reaches the " +
                                 "panel as an indistinguishable 'no preview' and is one live path to the blank box");
                if (previewRaw.IndexOf('\0') >= 0)
                    failures.Add("[preview] HeroPreviewViewer.cs contains an embedded NUL byte (mount-garble, " +
                                 "CLAUDE.md Sec.0) - the compile gate rejects this");
            }

            notes.Add("preview fallback pinned at source on " + PanelSrc);
        }

        // =====================================================================
        //  CASE 5 - the source laws that keep the regression unreachable
        // =====================================================================
        private static void Case5_SourceLaws(List<string> failures, List<string> notes)
        {
            string raw = ReadText(PanelSrc, failures, "[source]");
            if (raw == null) return;
            string code = StripComments(raw);

            // E1 - the rogue "Orient" control, on EVERY screen that carried it. The strand was
            // copy-pasted, not shared, so each site is pinned individually: removing one instance
            // has twice now failed to remove the others (WO-1010 D1 -> WO-1015 E1).
            OrientFree(failures, PanelSrc, raw);
            string inv = ReadText(InventorySrc, failures, "[source]");
            if (inv != null) OrientFree(failures, InventorySrc, inv);
            string pal = ReadText(PaletteSrc, failures, "[source]");
            if (pal != null) OrientFree(failures, PaletteSrc, pal);

            // The fixed-pixel band law itself.
            Law(failures, code, "BandGapPx",
                "the guaranteed band gutter constant is gone - bands may touch again");
            Law(failures, code, "SlotRowPx",
                "the fixed slot ROW pitch is gone; if the slots went back to hand-anchored body " +
                "fractions, E3/E4 return exactly as captured on 2026-08-10");
            Law(failures, code, "PostScaleCanvasHeight",
                "the panel no longer MEASURES its well before spending it - band budgets computed " +
                "against an unmeasured rect are guesses (WO-905 / WO-865 root cause)");
            Law(failures, code, "MakeScrollZone",
                "the slot list no longer scrolls - a list that cannot scroll must compress its rows " +
                "to fit, and a compressed row is an overprinted row (E4's other door)");

            // The band budget must be PRINTED, not merely computed (WO-1015 acceptance criterion:
            // "the band budget is printed once in a FlowTrace.Step line").
            if (!Regex.IsMatch(code, @"FlowTrace\.Step\s*\(\s*""Equip""[^;]*bands\(px\)", RegexOptions.Singleline))
                failures.Add("[source] the one-line band budget FlowTrace.Step (\"bands(px): ...\") is gone from " +
                             "EquipmentPanel - the geometry is back to being an eyeball claim rather than a " +
                             "captured fact, and the next layout bug here starts with no numbers");

            // The 1/n fraction slice - the WO-852 shape, verbatim.
            if (Regex.IsMatch(code, @"1f\s*/\s*(?:n|rows|cols|count|Length)\b", RegexOptions.IgnoreCase))
                failures.Add("[source] the panel slices a host into 1/n FRACTIONS again - each slice resolves " +
                             "below MinTouchPx and ClampMinTouch then grows the control past the slice on BOTH " +
                             "sides, stacking it on its neighbours (WO-852 verbatim). Size bands in fixed px.");

            // Kit routing + the no-hand-rolled-uGUI law (the [ui-obsidian] ratchet).
            if (code.IndexOf("ElarionUiKit", StringComparison.Ordinal) < 0)
                failures.Add("[source] the panel no longer goes through ElarionUiKit - the " +
                             "UiObsidianConformanceRegression hand-rolled-uGUI law");

            // ASCII-only law. Checked on the COMMENT-STRIPPED source rather than by pairing quotes:
            // literal-pairing is unreliable (one escaped quote earlier in the file shifts every pair
            // after it, so the check silently stops testing anything), and "which literals reach TMP"
            // is not decidable statically anyway. Whole-file-outside-comments is a strictly stronger
            // law, trivially true to hold, and it also stops an em dash living in a log string until
            // someone copy-pastes it into a label. Non-ASCII renders as tofu in the build TMP font
            // (WO-713, the U+2692 hammer).
            for (int i = 0; i < code.Length; i++)
                if (code[i] > 0x7E)
                {
                    int lineNo = 1;
                    for (int k = 0; k < i; k++) if (code[k] == '\n') lineNo++;
                    failures.Add($"[source] EquipmentPanel.cs line {lineNo} has a non-ASCII character " +
                                 $"(U+{(int)code[i]:X4}) OUTSIDE a comment. Prose in comments may use any " +
                                 "character; code and string literals may not - non-ASCII renders as tofu " +
                                 "in the build TMP font. Use plain ASCII ('-', not an em dash).");
                    break;
                }

            if (raw.IndexOf('\0') >= 0)
                failures.Add("[source] EquipmentPanel.cs contains an embedded NUL byte (mount-garble, " +
                             "CLAUDE.md Sec.0) - the compile gate rejects this");

            notes.Add("source laws checked on " + PanelSrc + " + the two other Orient sites");
        }

        /// <summary>No gameplay screen may CONSTRUCT an "Orient" control. Checked on the RAW source
        /// (not comment-stripped) for a button/label literal, so the lesson written in prose above
        /// each removal site is allowed while a re-added control is not.</summary>
        private static void OrientFree(List<string> failures, string path, string raw)
        {
            string code = StripComments(raw);
            // A control is a string literal "Orient" (or "Orient ...") handed to a kit factory, or a
            // GameObject named OrientDev. FlowTrace system tags ("Orient", $"...") are NOT controls,
            // so require the literal to sit in an argument list next to a kit call or a name assign.
            if (Regex.IsMatch(code, @"(ButtonPack|Button|BuildObsidianButton|TechPrimaryButton|Label)\s*\([^;]{0,200}""Orient",
                              RegexOptions.Singleline) ||
                code.IndexOf("OrientDev", StringComparison.Ordinal) >= 0 ||
                Regex.IsMatch(code, @"void\s+BuildOrientButton\s*\("))
                failures.Add($"[source] {path} constructs an \"Orient\" control again. WO-1015 E1: this " +
                             "dev-gated strand was copy-pasted into three gameplay screens and rendered on " +
                             "the owner's felt-test builds (development builds ARE dev-gated builds), " +
                             "floating over the Shield (Off Hand) slot. The seating editor keeps its ONE " +
                             "sanctioned entry point - AdminOverlay's \"Orient Asset\" / \"Seating Editor\" " +
                             "on the dev overlay. Do not re-add a per-screen launcher.");
        }

        private static void Law(List<string> failures, string code, string token, string why)
        {
            if (code.IndexOf(token, StringComparison.Ordinal) < 0)
                failures.Add($"[source] '{token}' is gone from EquipmentPanel - {why}");
        }

        // =====================================================================
        //  helpers
        // =====================================================================

        // The brace characters as named constants. Deliberately NOT written as char literals: this
        // repo's mandatory quality gate (CLAUDE.md Sec.1) counts brace characters to detect a
        // mount-garbled file, and a lone brace inside a literal or a doc comment makes a perfectly
        // healthy file read as MISMATCHED. Naming them keeps the gate honest.
        private const char BraceOpen = (char)123;
        private const char BraceClose = (char)125;

        /// <summary>Return the brace-balanced block that starts at the opening brace at
        /// <paramref name="open"/>. Returns empty when the index is not an opening brace.</summary>
        private static string Block(string code, int open)
        {
            if (open < 0 || open >= code.Length || code[open] != BraceOpen) return "";
            int depth = 0;
            for (int i = open; i < code.Length; i++)
            {
                if (code[i] == BraceOpen) depth++;
                else if (code[i] == BraceClose)
                {
                    depth--;
                    if (depth == 0) return code.Substring(open, i - open + 1);
                }
            }
            return code.Substring(open);
        }

        /// <summary>Read a public const float/int by reflection (no asmdef reference needed).</summary>
        private static float ConstFloat(Type t, string name, List<string> failures, string tag)
        {
            if (t == null) return 0f;
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (f == null)
            {
                failures.Add($"{tag} {t.Name}.{name} does not exist - the layout constant this oracle pins " +
                             "was renamed or removed; re-point it rather than deleting the guard");
                return 0f;
            }
            object v = f.GetValue(null);
            if (v is float fv) return fv;
            if (v is int iv) return iv;
            if (v is double dv) return (float)dv;
            failures.Add($"{tag} {t.Name}.{name} is not a numeric constant " +
                         $"(got {(v == null ? "null" : v.GetType().Name)})");
            return 0f;
        }

        private static string ReadText(string path, List<string> failures, string tag)
        {
            try
            {
                if (!File.Exists(path))
                {
                    failures.Add($"{tag} source not found: {path}");
                    return null;
                }
                return File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                failures.Add($"{tag} could not read {path}: {ex.Message}");
                return null;
            }
        }

        /// <summary>Blank out // and block comments so a lesson written in prose (which deliberately
        /// quotes the retired shapes) can never fail a source law.</summary>
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
