// =============================================================================
// RaidSelectionLayoutRegression - the RAIDS camp list MEASURES, at four camps and
// at eight: no card overlaps another, every card sits wholly inside the scrolling
// content, the capacity is DERIVED from the well, and the three WO-1442 defects
// cannot come back.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression   Namespace: DeNelle.Editor.Regression
//
// WO-1442. Owner felt-test 2026-09-06 on build 2026.09.06.358245, verbatim: "won and
// back to camp could attack but the screen and UI was very hard to use". The evidence
// is one adb screencap at 2670x1200 (scratchpad raid-ui.png); every number quoted
// below was measured off that frame, not inferred.
//
// WHY THIS SUITE IS SEPARATE FROM RaidSelectionSpoilsRegression: that one proves the
// WORDS (what a row says) and, in its case F, that each text band is TALL enough to
// render. This one proves the GEOMETRY the rows are laid out in - a different axis,
// and the one nothing measured. Case F stays where it is; nothing here duplicates it.
//
// =============================================================================
//  THE RED PROOF - what this suite says about the tree it was written against
// =============================================================================
// Against HEAD (the shipped 2026.09.06.358245 geometry) the SOURCE cases below are
// RED, and they are red for the three defects by name:
//
//   R1  D1 - THE STRAY BAR.  RaidSelectionScreen.CreateRaidCard called
//       `MedievalUiSkin.ApplyButton(cardBtn, primary: !dimmed)`. That helper sets
//       Selectable.Transition.SpriteSwap and puts
//       UI/ElarionMedieval/buttons/button-pressed-empty into the highlighted,
//       selected AND pressed slots (MedievalUiSkin.cs:74-80); the screen then
//       overwrote only image.sprite, never spriteState. So any non-Normal state
//       repainted the whole card as a 3:1 action plate under its labels.
//       IDENTIFIED BY PIXELS: the bar's gold rails down card one (rows 293-513,
//       height 221) sit at fractions 0.1810/0.2127/0.2398/0.7104/0.7421;
//       button-pressed-empty's own rails are 0.1823/0.2099-0.2141/0.2390-0.2445/
//       0.7030-0.7127/0.7403 - five of five inside 0.002. button-normal-empty
//       (0.1948/0.2058/0.2141/0.6478/0.6892) and frames/content-panel
//       (0.1148/0.1286/0.1403/0.8151) are both excluded, so it was never a
//       selection highlight and never a mis-anchored loot pill.
//       Case S1 FAILS while that call is in the file.
//
//   R2  D2 - THE TYPED WELL.  OpenInternal set the body zone to a hardcoded
//       0.20 .. 0.80 of the panel. Measured on her frame the well was 634 device px
//       = 510.0 reference px at a canvas scale of 1.2431 - exactly 0.60 of the
//       panel, i.e. those two literals. Worse, the kit's Close band tops out at
//       0.050 + CanonCtaHeight/(0.88 * 965.4) = 0.2054, so the floor of 0.20 put the
//       scroll well ~4.6 ref px INSIDE the shared Close.
//       Case S2 FAILS while those literals are the body anchors, and case S3 FAILS
//       while the file does not read chrome.layout.footer / chrome.layout.subHeader.
//
//   R3  D3 - NO OPAQUE LAYER.  The panel was built `withBackdrop: false`, its
//       chrome.content is alpha 0 by construction, and the shell MedievalUiSkin
//       .ApplyShell swaps in (frames/modal-frame-16x9) is alpha 0 at every interior
//       sample of its 1672x941 art. Three transparent layers, so the town's
//       "wood 113  iron 38" read straight through - and it is BELOW this canvas, as
//       the card plates clip its glyph tops flat in the capture.
//       Case S4 FAILS while `withBackdrop: false` is in the file.
//
//   R4  THE AFFORDANCE.  Nothing told the player a fourth camp existed. The list did
//       scroll - the kit rail is in her frame, 7 device px wide at x 2133-2139,
//       filling 0.66 of its track, which is four cards of content in a two-card well.
//       Case S5 FAILS while the screen paints no camp-count sentence.
//
// The MEASURED cases (A/B/C) are the INVARIANT GUARD, and they are honest about it:
// they were GREEN before this change too, because the VerticalLayoutGroup never did
// overlap rows. They exist so a future "make it fit" edit - a shorter card, a negative
// spacing, a fitter swapped out, a peek hacked in by overlapping rows - reds instead
// of shipping. Case C is the one that cannot pass by matching today's camp count: it
// sweeps the well height and pins capacity to a pure geometric floor.
//
// MUTATIONS THIS SUITE CATCHES (named, so the RED is reproducible):
//   M1. Put `MedievalUiSkin.ApplyButton(cardBtn, ...)` back on the card  -> S1.
//   M2. Retype the body anchors as 0.20f/0.80f                          -> S2 (+S3).
//   M3. Pass `withBackdrop: false` again                                -> S4.
//   M4. Delete BuildCampCountCaption / CampCountLine                    -> S5.
//   M5. Make CardGapPx negative to squeeze a third card into the fold   -> A/B (rows
//       overlap) and C (pitch no longer matches the floor).
//   M6. Return a fixed 4 from VisibleCardCapacity                       -> C.
//
// =============================================================================
//  RED PROOF, MEASURED 2026-09-06 (not asserted - run, and here is the output)
// =============================================================================
// The WO-1442 edits are uncommitted, so the baseline is HEAD (f986f3cff at the time
// of writing). The six source cases were evaluated against `git show HEAD:` copies,
// comments stripped exactly as Run() strips them:
//
//   RED    S1:no-action-skin      (MedievalUiSkin.ApplyButton was on the card)
//   RED    S2:no-typed-well       (anchorMin.x, 0.20f / anchorMax.x, 0.80f)
//   RED    S3:well-from-kit       (no chrome.layout.footer / subHeader read at all)
//   RED    S4:opaque-backdrop     (withBackdrop: false)
//   RED    S5:count-caption       (no CampCountLine anywhere)
//   GREEN  S6:lock-copy           <- GREEN ON PURPOSE. It is a PRESERVATION guard for
//                                   WO-1442 section 1, not a defect probe; it must be
//                                   green before and after, and red only if someone
//                                   later swaps the sentence for a padlock.
//
// 5 of 6 red. And the geometry those literals produced, cross-checked against the
// owner's own capture rather than against itself:
//   canvas scale       1.2430   (her frame: a 178 ref px card rendered 221 device px = 1.2416)
//   panel height       849.5 ref px
//   well at 0.20..0.80 509.7 ref px   (her frame: 634 device px / 1.2430 = 510.0)
//   whole cards seated 2              (her frame: 2 full cards and a 0.66 peek)
//   kit Close band top 0.2054 > 0.20  -> the well overlapped the shared Close by 4.6 ref px
//
// =============================================================================
//  ADDENDUM 2026-09-06 - WO-1462 / WO-1463: THE SUITE NOW COVERS THE FAMILY
// =============================================================================
// Nothing above is rewritten; the 09-06 measurement is frozen. What follows is new.
//
// The selection door was fixed and pinned (S4) while the DEPLOY door carried the
// identical defect one file away, uncovered by anything. An oracle scoped to one
// screen certified the family it did not measure - so two SIBLING cases are added:
//
//   S7  WO-1462 - RaidDeployScreen.cs took `withBackdrop: false`, the same three-
//       transparent-layers shape as R3/S4. RED PROOF, by inspection of the tree this
//       was written against (an edit-only lane; no Unity run is claimed):
//       `git show HEAD:Assets/_Modules/Village/Hero/RaidDeployScreen.cs` line 145 reads
//       `..., Close, withBackdrop: false,` - in CODE, not in a comment, so the case's
//       stripped-source Contains() fires. RED at HEAD.
//
//   S8  WO-1463 - RaidDeployController.cs built the rally flag from two bare
//       GameObject.CreatePrimitive calls (:639 pole, :648 banner) and "coloured" them
//       by writing renderer.material.color on Unity's built-in Default-Material, which
//       has no URP variant. RED PROOF, same inspection: at HEAD that file contains TWO
//       `CreatePrimitive(` and ZERO `sharedMaterial` / `.material =`, so both of S8's
//       positional windows are empty and both fail; `material.color =` is present, and
//       `ProtectPrimitiveArt` is absent. RED at HEAD on three counts.
//
// MUTATIONS ADDED TO THE LEDGER:
//   M7. Pass `withBackdrop: false` on the DEPLOY screen                 -> S7.
//   M8. Add a CreatePrimitive to RaidDeployController with no material  -> S8 (and
//       dropping ProtectPrimitiveArt reds S8 too - a correct material on a marker the
//       guard's sweep has disabled is still an invisible marker).
//
// S8 is deliberately POSITIONAL rather than file-wide: a "the helper is called
// somewhere" check goes green on a file holding one fixed primitive and one bare one,
// which is the exact shape the next edit adds.
//
// Contract mirrors the other suites - Run(out string reason): true = pass.
// Orchestrator registration (DataRegression.RunAll), covenant style:
//   if (!RaidSelectionLayoutRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[raid-selection-layout] " + r);
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Village.Hero;

namespace DeNelle.Editor.Regression
{
    public static class RaidSelectionLayoutRegression
    {
        private const string ScreenRel = "_Modules/Village/Hero/RaidSelectionScreen.cs";
        private const string VmRel     = "_Modules/Village/Hero/RaidSelectionVM.cs";

        // WO-1462 / WO-1463 — THE SIBLING DOORS IN THE SAME RAID MODAL FAMILY.
        // WO-1442 fixed the transparent-panel defect on the SELECTION screen and this suite
        // pinned it there (S4). The DEPLOY screen carried the identical `withBackdrop: false`
        // call the whole time and no suite looked at it, which is exactly how the same defect
        // shipped twice one door apart. The oracle now covers the FAMILY, not one screen.
        private const string DeployRel     = "_Modules/Village/Hero/RaidDeployScreen.cs";
        private const string DeployCtrlRel = "_Modules/Village/Troops/RaidDeployController.cs";

        private const float Eps = 0.5f;

        /// <summary>THE OWNER'S LOCK COPY, VERBATIM (WO-1442 section 1). It names the gate AND
        /// the exact distance to clearing it, which is the shape WO-1427 asks for everywhere.
        /// A padlock glyph or a bare "Locked" is a REGRESSION, not a redesign.</summary>
        private const string LockCopyStem = "The Heart cannot reach this far yet - win ";
        private const string LockCopyTail = " to press on.";

        /// <summary>Device surfaces this screen actually meets. The first is the owner's own
        /// Seeker frame; the others bracket it so a fix cannot be tuned to one aspect.</summary>
        private struct Surface { public string Name; public float W, H; }

        private static readonly Surface[] Surfaces =
        {
            new Surface { Name = "2670x1200", W = 2670f, H = 1200f },   // the owner's capture
            new Surface { Name = "2340x1080", W = 2340f, H = 1080f },
            new Surface { Name = "1920x1080", W = 1920f, H = 1080f },
            new Surface { Name = "1080x1920", W = 1080f, H = 1920f },
        };

        /// <summary>Camp counts proven. FOUR is today's ladder; EIGHT is the ladder after she
        /// keeps winning - the WO's own guard against a fix that passes by coincidence.</summary>
        private static readonly int[] CampCounts = { 4, 8 };

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("=== RaidSelectionLayoutRegression: WO-1442 raid camp list geometry ===");

            // ── FIXTURE. The thing measured must be on disk; absence is RED with the path. ──
            string screenPath = Path.Combine(Application.dataPath, ScreenRel.Replace('/', Path.DirectorySeparatorChar));
            string vmPath     = Path.Combine(Application.dataPath, VmRel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(screenPath))
            {
                reason = "raid-selection-layout FAIL x1: [fixture] MISSING " + ScreenRel +
                         " - the screen this suite measures is not on disk.";
                return false;
            }
            if (!File.Exists(vmPath))
            {
                reason = "raid-selection-layout FAIL x1: [fixture] MISSING " + VmRel +
                         " - the view model that owns the camp-count words is not on disk.";
                return false;
            }

            // ── CAPABILITY. No canvas => a DECLARED stand-down, never a silent pass. ──
            GameObject probe = null;
            string canvasWhy = null;
            try { probe = NewCanvas("rsl-probe", 100f, 100f); }
            catch (Exception ex) { canvasWhy = ex.GetType().Name + ": " + ex.Message; }
            finally { if (probe != null) UnityEngine.Object.DestroyImmediate(probe); }
            if (canvasWhy != null)
            {
                return RegressionOutcome.Skip(out reason, "RAID SELECTION LAYOUT",
                    "no UI canvas can be instantiated in this environment (" + canvasWhy +
                    ") - no card rect can be measured");
            }

            // ⛔ LINT THE CODE, NOT THE PROSE. Both files now carry long comment blocks that
            // NAME the banned calls on purpose ("DO NOT PUT MedievalUiSkin.ApplyButton BACK ON
            // THIS ROW", "this call passed withBackdrop: false") - a raw Contains() would red on
            // the very comments that keep the defect from coming back, and the cure would be to
            // delete the explanation. Comments are stripped; string literals are preserved,
            // because the lock copy the suite pins IS a string literal.
            string screenSrc = StripComments(File.ReadAllText(screenPath));
            string vmSrc     = StripComments(File.ReadAllText(vmPath));

            // WO-1462 / WO-1463 — the two SIBLING files. Absence is a NAMED failure, never a
            // silent skip: an oracle that quietly stops measuring a file that moved is how the
            // deploy door went uncovered in the first place.
            string deployPath     = Path.Combine(Application.dataPath, DeployRel.Replace('/', Path.DirectorySeparatorChar));
            string deployCtrlPath = Path.Combine(Application.dataPath, DeployCtrlRel.Replace('/', Path.DirectorySeparatorChar));
            string deploySrc     = File.Exists(deployPath)     ? StripComments(File.ReadAllText(deployPath))     : null;
            string deployCtrlSrc = File.Exists(deployCtrlPath) ? StripComments(File.ReadAllText(deployCtrlPath)) : null;
            if (deploySrc == null)
                failures.Add("[fixture] MISSING " + DeployRel + " - the sibling raid modal this suite now covers " +
                             "is not on disk; S7 cannot be evaluated.");
            if (deployCtrlSrc == null)
                failures.Add("[fixture] MISSING " + DeployCtrlRel + " - the in-raid deploy controller this suite " +
                             "now lints for bare primitives is not on disk; S8 cannot be evaluated.");

            try
            {
                foreach (var s in Surfaces)
                {
                    var surface = s;
                    foreach (var n in CampCounts)
                    {
                        int camps = n;
                        Case(failures, "cards:" + surface.Name + ":" + camps,
                             () => CaseCards(surface, camps, failures, notes, log));
                    }
                    Case(failures, "caption-band:" + surface.Name,
                         () => CaseCaptionBand(surface, failures, log));
                }
                Case(failures, "capacity-is-derived", () => CaseCapacityIsDerived(failures, log));
                Case(failures, "bands-disjoint",      () => CaseCardBandsDisjoint(failures, log));
                Case(failures, "S1:no-action-skin",   () => CaseNoActionButtonSkin(screenSrc, failures, log));
                Case(failures, "S2:no-typed-well",    () => CaseNoTypedWell(screenSrc, failures, log));
                Case(failures, "S3:well-from-kit",    () => CaseWellFromKit(screenSrc, failures, log));
                Case(failures, "S4:opaque-backdrop",  () => CaseOpaqueBackdrop(screenSrc, failures, log));
                Case(failures, "S5:count-caption",    () => CaseCountCaption(screenSrc, vmSrc, failures, log));
                Case(failures, "S6:lock-copy",        () => CaseLockCopyVerbatim(vmSrc, failures, log));
                if (deploySrc != null)
                    Case(failures, "S7:deploy-opaque-backdrop",
                         () => CaseDeployOpaqueBackdrop(deploySrc, failures, log));
                if (deployCtrlSrc != null)
                    Case(failures, "S8:no-bare-primitive",
                         () => CaseNoBarePrimitive(deployCtrlSrc, failures, log));
                Case(failures, "words",               () => CaseCaptionWords(failures, log));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : string.Empty;
            if (failures.Count == 0)
            {
                reason = "RAID SELECTION LAYOUT OK - " + Surfaces.Length + " surfaces x {4,8} camps " +
                         "MEASURED on a live canvas: no card overlaps another, every card sits wholly " +
                         "inside the scrolling content at the one pitch (" + RaidSelectionScreen.RowPitchPx.ToString("0") +
                         " ref px), the capacity is a pure floor of the well (never the camp count), " +
                         "the caption band clears the blank-text law, and the three WO-1442 defects " +
                         "(action-button skin on a row, a typed well, a transparent panel) are all absent, " +
                         "and the SIBLING raid doors hold too: RaidDeployScreen takes the kit backdrop and " +
                         "RaidDeployController creates no bare primitive without an explicit URP material" + noteStr;
                Debug.Log("RAID_SELECTION_LAYOUT_OK\n" + log);
                return true;
            }

            Debug.LogError("RAID_SELECTION_LAYOUT_FAIL: " + failures.Count + " failure(s)\n" + log);
            reason = "raid-selection-layout FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  CASE A/B - the card rects, MEASURED, at four camps AND at eight.
        // =====================================================================
        private static void CaseCards(Surface s, int camps, List<string> failures,
                                      List<string> notes, StringBuilder log)
        {
            string tag = "[cards:" + s.Name + ":" + camps + "]";
            GameObject root = null;
            try
            {
                float refW, refH;
                ReferenceBox(s, out refW, out refH);
                root = NewCanvas("rsl-" + s.Name + "-" + camps, refW, refH);
                var rootRt = (RectTransform)root.transform;

                var panel = Region(rootRt, "Panel",
                    RaidSelectionScreen.PanelAnchorMin, RaidSelectionScreen.PanelAnchorMax);

                // ⛔ THE SCREEN'S OWN BAND FUNCTION, not a copy of its numbers. Feeding it the
                // mirrored FrameCore zones is the only part this suite supplies; the arithmetic
                // that turns them into a well is the shipping one, so a change there moves both.
                float wellY0, wellY1;
                RaidSelectionScreen.ComputeWellBand(
                    RaidSelectionScreen.FallbackFooterY0,
                    RaidSelectionScreen.FallbackSubHeaderY0, out wellY0, out wellY1);
                // The x fractions are FrameCore's body inset, typed. They are stated rather than
                // derived on purpose: nothing this suite asserts is horizontal (overlap between
                // full-width stacked rows, containment, pitch and capacity are all vertical), so a
                // drift in the inset changes no verdict here. The VERTICAL band is the one that
                // decides everything, and that one comes from the screen's own function above.
                var well = Region(panel, "Well", new Vector2(0.055f, wellY0), new Vector2(0.945f, wellY1));

                Settle(rootRt);
                float wellPx = well.rect.height;
                int capacity = RaidSelectionScreen.VisibleCardCapacity(wellPx);

                // THE REAL KIT SCROLL ZONE - the same call BuildCards makes, with the same
                // spacing and padding, so what is measured is the layout that ships.
                var handle = ElarionUiKit.MakeScrollZone(well, spacing: RaidSelectionScreen.CardGapPx,
                                                         padding: ScrollPadPxOf());
                if (handle == null || handle.content == null)
                {
                    failures.Add(tag + " MakeScrollZone returned no content column - nothing to measure.");
                    return;
                }

                var rows = new List<RectTransform>();
                for (int i = 0; i < camps; i++)
                {
                    // Sized EXACTLY as CreateRaidCard sizes a card: the kit's scroll column runs
                    // childControlHeight:false, so a row carries its own height via sizeDelta.
                    var go = new GameObject("RaidCard_" + i, typeof(RectTransform));
                    go.transform.SetParent(handle.content, false);
                    var rt = (RectTransform)go.transform;
                    rt.sizeDelta = new Vector2(0f, RaidSelectionScreen.CardHeightPx);
                    rows.Add(rt);
                }
                Settle(rootRt);

                var contentRect = WorldRect(handle.content);
                var viewportRect = handle.viewport != null ? WorldRect(handle.viewport) : new Rect();

                log.AppendLine(tag + " well " + wellPx.ToString("0") + " ref px (band " +
                               wellY0.ToString("0.###") + ".." + wellY1.ToString("0.###") +
                               ") capacity " + capacity + " content h=" +
                               contentRect.height.ToString("0") + " viewport h=" +
                               viewportRect.height.ToString("0"));

                // 1. ZERO OVERLAP - the thing the WO asks for by name.
                for (int i = 0; i < rows.Count; i++)
                {
                    for (int j = i + 1; j < rows.Count; j++)
                    {
                        var a = WorldRect(rows[i]);
                        var b = WorldRect(rows[j]);
                        if (Overlaps(a, b))
                            failures.Add(tag + " card " + i + " " + Fmt(a) + " OVERLAPS card " + j +
                                         " " + Fmt(b) + " - two camps are painted on the same pixels.");
                    }
                }

                // 2. FULL CONTAINMENT - every card wholly inside the scrolling content, so no
                //    camp is unreachable however far the player drags.
                for (int i = 0; i < rows.Count; i++)
                {
                    var r = WorldRect(rows[i]);
                    if (r.height <= Eps)
                        failures.Add(tag + " card " + i + " measured height " + r.height.ToString("0.##") +
                                     " - the row COLLAPSED (a height-controlling layout group would do this).");
                    if (!Contains(contentRect, r))
                        failures.Add(tag + " card " + i + " " + Fmt(r) + " is NOT inside the content column " +
                                     Fmt(contentRect) + " - that camp cannot be scrolled to.");
                }

                // 3. THE ONE PITCH - cards stack at CardHeightPx + CardGapPx, no more and no less.
                for (int i = 1; i < rows.Count; i++)
                {
                    float pitch = WorldRect(rows[i - 1]).yMin - WorldRect(rows[i]).yMin;
                    if (Mathf.Abs(pitch - RaidSelectionScreen.RowPitchPx) > 1.5f)
                        failures.Add(tag + " pitch between card " + (i - 1) + " and " + i + " measured " +
                                     pitch.ToString("0.##") + " ref px, expected " +
                                     RaidSelectionScreen.RowPitchPx.ToString("0.##") +
                                     " (CardHeightPx + CardGapPx). A squeezed pitch is how a peek gets faked.");
                }

                // 4. EVERY CAMP REACHABLE - either they all fit, or the well scrolls far enough
                //    that the LAST card's bottom can reach the viewport's bottom.
                bool allFit = camps <= capacity;
                bool scrollable = contentRect.height > viewportRect.height + Eps;
                if (!allFit && !scrollable)
                    failures.Add(tag + " " + camps + " camps with a capacity of " + capacity +
                                 " but content (" + contentRect.height.ToString("0") +
                                 ") does not exceed the viewport (" + viewportRect.height.ToString("0") +
                                 ") - the overflow is unreachable.");

                // 5. A CARD MUST FIT THE WELL AT ALL. If one card is taller than the viewport the
                //    player can never see a whole camp on any surface - that is a design red, not
                //    a scroll problem, and it must name itself rather than hide behind scrolling.
                if (viewportRect.height > Eps && RaidSelectionScreen.CardHeightPx > viewportRect.height + Eps)
                    failures.Add(tag + " one card (" + RaidSelectionScreen.CardHeightPx.ToString("0") +
                                 " ref px) is TALLER than the whole viewport (" +
                                 viewportRect.height.ToString("0") + ") - no camp can ever be read in full here.");

                if (capacity < 1)
                    notes.Add(s.Name + " seats 0 whole cards (well " + wellPx.ToString("0") + " ref px)");
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // =====================================================================
        //  CASE - the caption's band clears the blank-text law.
        // =====================================================================
        // A text band under roughly NeedPx(fontPt) does not render SMALL in this project, it
        // renders NOTHING (RaidSelectionSpoilsRegression case F, forged when four of the
        // card's five rows shipped invisible). The camp-count caption seats in FrameCore's
        // sub-header band, so that band has to clear the law on every surface - including
        // portrait, where the panel is tallest, and 2670x1200, where it is shortest.
        private static void CaseCaptionBand(Surface s, List<string> failures, StringBuilder log)
        {
            string tag = "[caption-band:" + s.Name + "]";
            float refW, refH;
            ReferenceBox(s, out refW, out refH);
            float panelH = (RaidSelectionScreen.PanelAnchorMax.y - RaidSelectionScreen.PanelAnchorMin.y) * refH;
            float bandPx = (RaidSelectionScreen.FallbackSubHeaderY1 - RaidSelectionScreen.FallbackSubHeaderY0) * panelH;
            float needPx = RaidSelectionScreen.NeedPx(RaidSelectionScreen.RowFontPt);
            log.AppendLine(tag + " panel h=" + panelH.ToString("0") + " caption band " +
                           bandPx.ToString("0.#") + " px, needs " + needPx.ToString("0.#"));
            if (bandPx + 0.05f < needPx)
                failures.Add(tag + " the camp-count caption's band is " + bandPx.ToString("0.#") +
                             " px but a " + RaidSelectionScreen.RowFontPt + " pt line needs " +
                             needPx.ToString("0.#") + " - it would render BLANK, not small.");
        }

        // =====================================================================
        //  CASE C - the capacity is a FLOOR OF THE WELL, never a camp count.
        // =====================================================================
        // This is the case the WO's "a fix that seats four is the same bug at five" is aimed
        // at. It never mentions a camp count: it sweeps the well and pins the answer to pure
        // geometry, so a hardcoded return value or an off-by-one cannot survive it.
        private static void CaseCapacityIsDerived(List<string> failures, StringBuilder log)
        {
            const string tag = "[capacity-is-derived]";
            float pitch = RaidSelectionScreen.RowPitchPx;
            float card  = RaidSelectionScreen.CardHeightPx;
            float gap   = RaidSelectionScreen.CardGapPx;
            int   pad   = ScrollPadPxOf();
            if (pitch <= 0f || card <= 0f)
            {
                failures.Add(tag + " pitch " + pitch + " / card " + card + " is not positive - the layout has no scale.");
                return;
            }
            if (gap < 0f)
                failures.Add(tag + " CardGapPx is NEGATIVE (" + gap + ") - rows would be squeezed into each other " +
                             "to fake a denser fold. Density is a preference; an overlapped row is a defect.");

            int prev = -1;
            for (float wellPx = 100f; wellPx <= 1400f; wellPx += 5f)
            {
                int cap = RaidSelectionScreen.VisibleCardCapacity(wellPx);
                if (cap < 0) { failures.Add(tag + " negative capacity " + cap + " at well " + wellPx); return; }
                if (cap < prev)
                {
                    failures.Add(tag + " capacity DROPPED from " + prev + " to " + cap + " as the well grew to " +
                                 wellPx.ToString("0") + " - capacity must be monotone in the well height.");
                    return;
                }
                prev = cap;
                if (cap >= 1)
                {
                    // n cards need n*pitch - gap + 2*pad; n+1 must NOT fit.
                    float needs     = cap * pitch - gap + 2f * pad;
                    float needsMore = (cap + 1) * pitch - gap + 2f * pad;
                    if (wellPx + 0.001f < needs)
                    {
                        failures.Add(tag + " claims " + cap + " whole cards at a well of " + wellPx.ToString("0") +
                                     " ref px, but " + cap + " cards need " + needs.ToString("0.#") +
                                     " - a card would be cut mid-row.");
                        return;
                    }
                    if (wellPx >= needsMore - 0.001f)
                    {
                        failures.Add(tag + " claims only " + cap + " whole cards at a well of " + wellPx.ToString("0") +
                                     " ref px, but " + (cap + 1) + " fit in " + needsMore.ToString("0.#") +
                                     " - the well is being under-used.");
                        return;
                    }
                }
            }
            log.AppendLine(tag + " swept 100..1400 ref px: capacity is monotone and equals the exact " +
                           "whole-card floor at every step (pitch " + pitch.ToString("0") + ", pad " + pad + ").");
        }

        // =====================================================================
        //  CASE - the card's five text bands are disjoint and inside the card.
        // =====================================================================
        // Case F of RaidSelectionSpoilsRegression proves each band is TALL enough. This proves
        // they do not sit on top of each other and none hangs off the plaque - the "no element
        // overlaps another on any card" half of the WO's acceptance, on the same live table.
        private static void CaseCardBandsDisjoint(List<string> failures, StringBuilder log)
        {
            const string tag = "[bands-disjoint]";
            var bands = RaidSelectionScreen.CardBands;
            if (bands == null || bands.Length == 0)
            {
                failures.Add(tag + " RaidSelectionScreen.CardBands is empty - the card has no authored bands to check.");
                return;
            }
            for (int i = 0; i < bands.Length; i++)
            {
                var b = bands[i];
                if (b.Y0 < -0.0001f || b.Y1 > 1.0001f || b.Y1 <= b.Y0)
                    failures.Add(tag + " band '" + b.Name + "' is " + b.Y0.ToString("0.###") + ".." +
                                 b.Y1.ToString("0.###") + " - not a positive band inside the card.");
                for (int j = i + 1; j < bands.Length; j++)
                {
                    var c = bands[j];
                    if (b.Y0 < c.Y1 - 0.0001f && c.Y0 < b.Y1 - 0.0001f)
                        failures.Add(tag + " bands '" + b.Name + "' (" + b.Y0.ToString("0.###") + ".." +
                                     b.Y1.ToString("0.###") + ") and '" + c.Name + "' (" +
                                     c.Y0.ToString("0.###") + ".." + c.Y1.ToString("0.###") +
                                     ") OVERLAP - two rows of the card would print on each other.");
                }
            }
            log.AppendLine(tag + " " + bands.Length + " authored bands, all disjoint and inside the card.");
        }

        // =====================================================================
        //  S1 - D1: no ACTION-BUTTON skin on a LIST ROW.  (RED before WO-1442)
        // =====================================================================
        private static void CaseNoActionButtonSkin(string src, List<string> failures, StringBuilder log)
        {
            const string tag = "[S1:no-action-skin]";
            if (src.Contains("MedievalUiSkin.ApplyButton"))
                failures.Add(tag + " RaidSelectionScreen calls MedievalUiSkin.ApplyButton. That helper installs " +
                             "Selectable.Transition.SpriteSwap with button-pressed-empty in the highlighted / " +
                             "selected / pressed slots, so the row repaints as a 3:1 action plate the moment it " +
                             "is touched - the gold bar across The Forsaken Camp in the 2026-09-06 capture. " +
                             "A list row is not a CTA; leave it on StyleButtonColors' ColorTint.");
            if (!src.Contains("StyleButtonColors"))
                failures.Add(tag + " the card button no longer routes through ElarionUiKit.StyleButtonColors - " +
                             "it has no press feedback at all, which reads as a dead tap (WO-1110 section 2).");
            else
                log.AppendLine(tag + " card rows are ColorTint only; no button sprite can replace the card art.");
        }

        // =====================================================================
        //  S2 - D2: the well is not two typed fractions.  (RED before WO-1442)
        // =====================================================================
        private static void CaseNoTypedWell(string src, List<string> failures, StringBuilder log)
        {
            const string tag = "[S2:no-typed-well]";
            // The exact shipped literals, as an anchor assignment. Matching the pair rather than
            // the bare numbers keeps an unrelated 0.20f elsewhere in the file from crying wolf.
            bool typedFloor = src.Contains("anchorMin.x, 0.20f") || src.Contains("anchorMin.x, 0.2f");
            bool typedCeil  = src.Contains("anchorMax.x, 0.80f") || src.Contains("anchorMax.x, 0.8f");
            if (typedFloor || typedCeil)
                failures.Add(tag + " the body zone is anchored to typed fractions again. Measured on the owner's " +
                             "frame that pair resolved to a 510 ref px well whose FLOOR (0.20) sat ~4.6 ref px " +
                             "inside the kit's Close band (top 0.2054). Derive the band from chrome.layout.");
            else
                log.AppendLine(tag + " no typed body anchors.");
        }

        // =====================================================================
        //  S3 - D2: the well INHERITS the kit's reservation.  (RED before WO-1442)
        // =====================================================================
        private static void CaseWellFromKit(string src, List<string> failures, StringBuilder log)
        {
            const string tag = "[S3:well-from-kit]";
            bool readsFooter    = src.Contains("chrome.layout.footer");
            bool readsSubHeader = src.Contains("chrome.layout.subHeader");
            bool usesBandFn     = src.Contains("ComputeWellBand(");
            if (!readsFooter || !readsSubHeader)
                failures.Add(tag + " the screen does not read chrome.layout.footer" +
                             (readsSubHeader ? "" : " / chrome.layout.subHeader") +
                             " - it is no longer inheriting the factory's Close-band reservation, so the mirrored " +
                             "FrameCore constants have quietly become the source of truth and will drift.");
            if (!usesBandFn)
                failures.Add(tag + " the screen does not call ComputeWellBand - the oracle would then be measuring " +
                             "a band the screen does not build.");
            if (readsFooter && readsSubHeader && usesBandFn)
                log.AppendLine(tag + " the well band is read off the live chrome and computed by the shared function.");
        }

        // =====================================================================
        //  S4 - D3: the panel has an opaque layer.  (RED before WO-1442)
        // =====================================================================
        private static void CaseOpaqueBackdrop(string src, List<string> failures, StringBuilder log)
        {
            const string tag = "[S4:opaque-backdrop]";
            if (src.Contains("withBackdrop: false") || src.Contains("withBackdrop:false"))
                failures.Add(tag + " RaidSelectionScreen passes withBackdrop: false. This panel has no other opaque " +
                             "layer: chrome.content is built at alpha 0, and the shell ApplyShell swaps in " +
                             "(frames/modal-frame-16x9) is alpha 0 at every interior sample of its art. The town's " +
                             "\"wood 113  iron 38\" then reads straight through, as it did on 2026-09-06.");
            else
                log.AppendLine(tag + " the kit's Backdrop layer is built (the default the sibling panels take).");
        }

        // =====================================================================
        //  S5 - the affordance is a sentence that COUNTS.  (RED before WO-1442)
        // =====================================================================
        private static void CaseCountCaption(string screenSrc, string vmSrc, List<string> failures, StringBuilder log)
        {
            const string tag = "[S5:count-caption]";
            int before = failures.Count;
            if (!vmSrc.Contains("CampCountLine"))
                failures.Add(tag + " RaidSelectionVM has no CampCountLine - nothing tells the player how many camps " +
                             "exist, which is the whole reason a fourth camp went unfound on 2026-09-06.");
            if (!screenSrc.Contains("CampCountLine"))
                failures.Add(tag + " RaidSelectionScreen never paints the camp-count sentence.");
            if (!screenSrc.Contains("VisibleCardCapacity"))
                failures.Add(tag + " the screen does not derive its capacity - the caption cannot know whether to " +
                             "say 'drag the list' without it.");
            // THE VM OWNS THE WORDS (the MVVM seam this screen's suites already pin). A count
            // noun typed in the View is two copies of the same sentence, one screenshot apart.
            if (screenSrc.Contains("\" camps\"") || screenSrc.Contains("\" camp\""))
                failures.Add(tag + " the View types the camp noun itself - the VM owns the words.");
            if (failures.Count == before)
                log.AppendLine(tag + " the caption is VM-owned and its hint is gated on the derived capacity.");
        }

        // =====================================================================
        //  S6 - the lock copy survives, VERBATIM.
        // =====================================================================
        // WO-1442 section 1 puts this first "so it is not lost in a rewrite": the sentence names
        // the gate AND the exact distance to clearing it, in player words. A padlock glyph or a
        // bare "Locked" would be a regression - and the owner is red/green colourblind, so the
        // words are not decoration on a colour, they ARE the signal.
        private static void CaseLockCopyVerbatim(string vmSrc, List<string> failures, StringBuilder log)
        {
            const string tag = "[S6:lock-copy]";
            int before = failures.Count;
            if (!vmSrc.Contains(LockCopyStem))
                failures.Add(tag + " the lock sentence stem \"" + LockCopyStem + "\" is gone from RaidSelectionVM. " +
                             "WO-1442 section 1 preserves it verbatim; it names the gate and the distance to it.");
            if (!vmSrc.Contains(LockCopyTail))
                failures.Add(tag + " the lock sentence tail \"" + LockCopyTail + "\" is gone from RaidSelectionVM.");
            // A bare state word with no remedy, ASSIGNED or RETURNED (not merely discussed -
            // comments are already stripped, but the doc prose that forbids it is worth keeping
            // legal in either form).
            if (vmSrc.Contains("return \"Locked\"") || vmSrc.Contains("= \"Locked\"") ||
                vmSrc.Contains("return \"LOCKED\"") || vmSrc.Contains("= \"LOCKED\""))
                failures.Add(tag + " RaidSelectionVM hands back a bare \"Locked\" - a state word with no remedy. " +
                             "Every refusal on this screen names what unlocks it, because the owner is " +
                             "red/green colourblind and the words ARE the signal.");
            if (failures.Count == before) log.AppendLine(tag + " the lock copy is intact, verbatim.");
        }

        // =====================================================================
        //  S7 - WO-1462: the SIBLING deploy door has an opaque layer too.
        // =====================================================================
        // RED PROOF, by inspection of the tree this was written against (edit-only lane - no
        // Unity run is claimed): `git show HEAD:Assets/_Modules/Village/Hero/RaidDeployScreen.cs`
        // line 145 reads `..., Close, withBackdrop: false,` - a live call argument, not a comment,
        // so it survives StripComments and this case is RED at HEAD, for the same
        // three-transparent-layers reason S4 documents. It is the FAMILY guard: S4 covers the
        // selection door, S7 the deploy door, so a fix on one can no longer leave the other.
        private static void CaseDeployOpaqueBackdrop(string src, List<string> failures, StringBuilder log)
        {
            const string tag = "[S7:deploy-opaque-backdrop]";
            int before = failures.Count;

            if (src.Contains("withBackdrop: false") || src.Contains("withBackdrop:false"))
                failures.Add(tag + " RaidDeployScreen passes withBackdrop: false. This panel has no other opaque " +
                             "layer either - chrome.content is built at alpha 0, ApplyShell re-asserts that, and " +
                             "modal-frame-16x9 is alpha 0 at every interior sample - so the town reads straight " +
                             "through the deploy screen, as it did on 2026-09-06.");

            // A BESPOKE OPAQUE QUAD IS NOT THE CURE (WO-1462 section 3). The kit's named
            // Backdrop is the ONE authority for this layer; a hand-painted plate on this screen
            // would pass a naive "is it opaque" check while re-splitting ownership in two.
            if (src.Contains("\"DeployBackdrop\"") || src.Contains("\"Backdrop\""))
                failures.Add(tag + " RaidDeployScreen paints its own backdrop plate. FrameCore already owns the " +
                             "backdrop (ElarionUiKit.cs:568,573-579); a second one is a second authority.");

            // And it must still be going through the shared factory at all - if the frame call
            // itself were swapped out, the default this case relies on would not exist.
            if (!src.Contains("BuildObsidianPanel"))
                failures.Add(tag + " RaidDeployScreen no longer builds through ElarionUiKit.BuildObsidianPanel, so " +
                             "there is no kit default backdrop for it to inherit.");

            if (failures.Count == before)
                log.AppendLine(tag + " the deploy door takes the kit's 0.94-alpha Backdrop by default, like its sibling.");
        }

        // =====================================================================
        //  S8 - WO-1463: no BARE primitive in the in-raid deploy controller.
        // =====================================================================
        // GameObject.CreatePrimitive assigns Unity's built-in Default-Material, which has no
        // URP variant and renders MAGENTA in a player build; setting .color on it does nothing.
        //
        // RED PROOF (measured by inspection of the tree this suite was written against, not
        // asserted): `git show HEAD:Assets/_Modules/Village/Troops/RaidDeployController.cs`
        // contains TWO CreatePrimitive calls, at :639 (the pole) and :648 (the banner), and
        // ZERO occurrences of `sharedMaterial` or `.material =` anywhere in the file - the only
        // thing following each primitive was TintRenderer, i.e. a tint ON the built-in default.
        // So both windows below are empty of an assignment and this case is RED at HEAD.
        //
        // THE LINT IS POSITIONAL ON PURPOSE. A file-wide "the helper is called at least once"
        // check passes on a file with one fixed primitive and one bare one - which is precisely
        // the shape a later edit adds. Each CreatePrimitive must be followed by a material
        // assignment BEFORE the next CreatePrimitive or the enclosing return.
        //
        // Scope note carried from WO-1463 section 2: the ticket asks for this lint "anywhere
        // under _Modules". This suite is scoped to the file the WO's evidence names; the
        // repo-wide sweep is a separate, larger ticket and is recorded as such in the RESULT.
        private static void CaseNoBarePrimitive(string src, List<string> failures, StringBuilder log)
        {
            const string tag = "[S8:no-bare-primitive]";
            const string Prim = "CreatePrimitive(";

            int found = 0, bare = 0;
            int i = src.IndexOf(Prim, StringComparison.Ordinal);
            while (i >= 0)
            {
                found++;
                int next = src.IndexOf(Prim, i + Prim.Length, StringComparison.Ordinal);
                int ret  = src.IndexOf("return ", i + Prim.Length, StringComparison.Ordinal);

                // The window ends at whichever boundary comes first; if neither exists, the
                // rest of the file. A primitive built at the very end with no return still gets
                // a window, so it cannot escape by being last.
                int end = src.Length;
                if (next >= 0) end = Math.Min(end, next);
                if (ret  >= 0) end = Math.Min(end, ret);

                string window = src.Substring(i, end - i);
                bool assigned = window.Contains("sharedMaterial") ||
                                window.Contains(".material =") ||
                                window.Contains(".material=") ||
                                window.Contains("ApplyUrpMaterial") ||
                                window.Contains("BuildUrpLitMaterial") ||
                                window.Contains("ResolveUrpLitShader");
                if (!assigned)
                {
                    bare++;
                    failures.Add(tag + " a GameObject.CreatePrimitive in RaidDeployController is not given an " +
                                 "explicit URP material before the next primitive or the return. CreatePrimitive " +
                                 "assigns the built-in Default-Material, which has no URP variant and renders " +
                                 "MAGENTA in a player build - and tinting it via renderer.material.color does " +
                                 "nothing, which is exactly how the rally flag shipped as a magenta block on " +
                                 "2026-09-06. Route it through MagentaGuard.BuildUrpLitMaterial and assign " +
                                 "sharedMaterial (see RaidBaseGenerator.ApplyUrpMaterial).");
                }
                i = next;
            }

            // Deliberate primitive art must ALSO register with the guard, or the sweep classes
            // it as a stray placeholder and DISABLES it - a fixed material on a hidden object.
            if (found > 0 && !src.Contains("ProtectPrimitiveArt"))
                failures.Add(tag + " RaidDeployController builds primitive art but never calls " +
                             "MagentaGuard.ProtectPrimitiveArt, so MagentaGuard's sweep will treat the marker as " +
                             "a stray placeholder and disable it (MagentaGuard.IsPrimitivePlaceholder).");

            // And the defect's own mechanism, named: a tint on whatever material is already
            // there is not a fix, it is the bug.
            if (src.Contains("material.color ="))
                failures.Add(tag + " RaidDeployController still writes renderer.material.color. On the built-in " +
                             "Default-Material that is a no-op under URP; the colour must ride a material the " +
                             "code created from a URP shader.");

            if (bare == 0 && found > 0)
                log.AppendLine(tag + " " + found + " primitive(s), each given an explicit URP material and the " +
                               "subtree registered with MagentaGuard.");
            else if (found == 0)
                log.AppendLine(tag + " no CreatePrimitive in RaidDeployController - nothing to lint.");
        }

        // =====================================================================
        //  CASE - the caption's own words, at four camps and at eight.
        // =====================================================================
        private static void CaseCaptionWords(List<string> failures, StringBuilder log)
        {
            const string tag = "[words]";
            // Built through the VM's own formatter, with no catalog: the suite proves the
            // sentence's SHAPE and its count arithmetic, not the ladder (that is pinned in
            // RaidEscalationRegression A/B).
            CheckCaption(tag, 4, 2, true,  failures, log);   // her frame: 4 camps, 2 seated -> hint
            CheckCaption(tag, 4, 4, false, failures, log);   // all fit -> no hint
            CheckCaption(tag, 8, 3, true,  failures, log);   // the ladder she is climbing toward
            CheckCaption(tag, 1, 1, false, failures, log);   // singular, never "camp(s)"
        }

        private static void CheckCaption(string tag, int camps, int visible, bool wantHint,
                                         List<string> failures, StringBuilder log)
        {
            // ⛔ THE VM'S OWN FORMATTER, CALLED. Building the expected string out of the same
            // three constants the VM uses would be a tautology that cannot fail - it would
            // "prove" the sentence by restating it. The expectation below is written out,
            // literally, and compared to what the shipping method returns.
            string actual = DeNelle.Village.Hero.RaidSelectionVM.CampCountLine(camps, visible);
            string noun = camps == 1 ? " camp" : " camps";
            string expect = wantHint
                ? camps + noun + " - drag the list to see them all."
                : camps + noun + ".";
            if (!string.Equals(actual, expect, StringComparison.Ordinal))
            {
                failures.Add(tag + " CampCountLine(" + camps + ", " + visible + ") returned \"" +
                             (actual ?? "(null)") + "\" but the caption must read \"" + expect + "\".");
                return;
            }
            // ASCII only - a device without the glyph prints tofu, and this line's whole job is
            // to be readable. (The project has shipped that bug; see the diamond/star notes.)
            for (int i = 0; i < actual.Length; i++)
            {
                if (actual[i] > (char)126)
                {
                    failures.Add(tag + " the caption \"" + actual + "\" carries a non-ASCII character at " + i +
                                 " - device tofu risk.");
                    return;
                }
            }
            if (!actual.StartsWith(camps.ToString(), StringComparison.Ordinal))
                failures.Add(tag + " the caption for " + camps + " camps does not lead with the count: \"" + actual + "\"");
            if (!actual.EndsWith(".", StringComparison.Ordinal))
                failures.Add(tag + " the caption is not a sentence: \"" + actual + "\"");
            // The hint half must appear when and ONLY when the well cannot seat every camp -
            // the whole point of feeding it a DERIVED capacity.
            bool hasHint = actual.Contains("drag the list");
            if (hasHint != wantHint)
                failures.Add(tag + " with " + camps + " camps and " + visible + " seated the caption " +
                             (hasHint ? "TELLS the player to drag when everything already fits"
                                      : "does NOT tell the player to drag, so the overflow stays invisible") +
                             ": \"" + actual + "\"");
            log.AppendLine(tag + " " + camps + " camps / " + visible + " seated -> \"" + actual + "\"");
        }

        // =====================================================================
        //  Measurement plumbing (mirrors ArmyMusterLayoutRegression's proven rig)
        // =====================================================================

        /// <summary>
        /// Drop C# comments, keep string literals. String-aware (escapes, verbatim strings and
        /// char literals all survive) so a "//" inside a literal is never mistaken for a comment.
        /// </summary>
        private static string StripComments(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;
            var sb = new StringBuilder(src.Length);
            bool inStr = false, inChar = false, inLine = false, inBlock = false, verbatim = false;
            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                char n = i + 1 < src.Length ? src[i + 1] : '\0';
                if (inLine)
                {
                    if (c == '\n') { inLine = false; sb.Append(c); }
                    continue;
                }
                if (inBlock)
                {
                    if (c == '*' && n == '/') { inBlock = false; i++; }
                    else if (c == '\n') sb.Append(c);
                    continue;
                }
                if (inStr)
                {
                    sb.Append(c);
                    if (verbatim)
                    {
                        if (c == '"' && n == '"') { sb.Append(n); i++; }
                        else if (c == '"') { inStr = false; verbatim = false; }
                    }
                    else if (c == '\\' && n != '\0') { sb.Append(n); i++; }
                    else if (c == '"') inStr = false;
                    continue;
                }
                if (inChar)
                {
                    sb.Append(c);
                    if (c == '\\' && n != '\0') { sb.Append(n); i++; }
                    else if (c == '\'') inChar = false;
                    continue;
                }
                if (c == '/' && n == '/') { inLine = true; continue; }
                if (c == '/' && n == '*') { inBlock = true; i++; continue; }
                if (c == '@' && n == '"') { sb.Append(c); sb.Append(n); i++; inStr = true; verbatim = true; continue; }
                if (c == '"') { sb.Append(c); inStr = true; continue; }
                if (c == '\'') { sb.Append(c); inChar = true; continue; }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>The scroll column's padding, read off the screen rather than typed. It is
        /// private there on purpose (nothing else may set it), so this reads the ONE public
        /// consequence of it: the difference the capacity function makes for a single card.</summary>
        private static int ScrollPadPxOf()
        {
            // capacity(w) flips to 1 at w = CardHeightPx + 2*pad. Solve for pad by bisection on
            // the shipping function, so a change to the padding moves this with it and no copy
            // of the number exists in this file.
            float lo = RaidSelectionScreen.CardHeightPx;
            float hi = RaidSelectionScreen.CardHeightPx + 400f;
            for (int i = 0; i < 60; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (RaidSelectionScreen.VisibleCardCapacity(mid) >= 1) hi = mid; else lo = mid;
            }
            return Mathf.RoundToInt((hi - RaidSelectionScreen.CardHeightPx) * 0.5f);
        }

        /// <summary>The reference box a surface resolves to under the kit's CanvasScaler
        /// (1080x1920, MatchWidthOrHeight 0.5) - derived, never tabulated. On 2670x1200 this
        /// yields scale 1.2431, which is exactly what the owner's capture measures (a 178 ref
        /// px card rendered 221 device px).</summary>
        private static void ReferenceBox(Surface s, out float refW, out float refH)
        {
            float scale = Mathf.Pow(2f, Mathf.Lerp(
                Mathf.Log(s.W / 1080f, 2f), Mathf.Log(s.H / 1920f, 2f), 0.5f));
            refW = s.W / scale;
            refH = s.H / scale;
        }

        private static GameObject NewCanvas(string name, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            go.hideFlags = HideFlags.HideAndDontSave;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.position = Vector3.zero;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            return go;
        }

        private static RectTransform Region(Transform parent, string name, Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }

        private static void Settle(RectTransform root)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        }

        private static readonly Vector3[] _corners = new Vector3[4];

        private static Rect WorldRect(RectTransform rt)
        {
            if (rt == null) return new Rect();
            rt.GetWorldCorners(_corners);
            float x0 = Mathf.Min(_corners[0].x, _corners[2].x);
            float x1 = Mathf.Max(_corners[0].x, _corners[2].x);
            float y0 = Mathf.Min(_corners[0].y, _corners[2].y);
            float y1 = Mathf.Max(_corners[0].y, _corners[2].y);
            return new Rect(x0, y0, x1 - x0, y1 - y0);
        }

        private static bool Overlaps(Rect a, Rect b)
        {
            if (a.width <= Eps || a.height <= Eps || b.width <= Eps || b.height <= Eps) return false;
            return a.xMin + Eps < b.xMax && b.xMin + Eps < a.xMax &&
                   a.yMin + Eps < b.yMax && b.yMin + Eps < a.yMax;
        }

        private static bool Contains(Rect outer, Rect inner)
        {
            return inner.xMin >= outer.xMin - Eps && inner.xMax <= outer.xMax + Eps &&
                   inner.yMin >= outer.yMin - Eps && inner.yMax <= outer.yMax + Eps;
        }

        private static string Fmt(Rect r)
        {
            return "[x " + r.xMin.ToString("0") + ".." + r.xMax.ToString("0") +
                   " y " + r.yMin.ToString("0") + ".." + r.yMax.ToString("0") + "]";
        }
    }
}
