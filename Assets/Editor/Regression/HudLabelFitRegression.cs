// =============================================================================
// HudLabelFitRegression [hud-label-fit] (WO-1144) - a town HUD label can never
// again be CUT, ellipsised, or painted through the widget next to it.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression   Markers: HUD_LABEL_FIT_OK / HUD_LABEL_FIT_FAIL
//
// WHY A DISTINCT MARKER (canon section 8): a shared REGRESSION_OK is how a 22-case
// suite's pass once read as the whole suite's pass. This one says its own name.
//
// WHAT WAS CAPTURED (2026-08-22 headed fleet, autopilot-runs/*/break_24_error.png at
// 2670x1200, IDENTICAL in all 8 runs - the screenshot IS the data):
//   1  "Tap to collec"      the Collectors rail chip, cut mid-word.
//   2  "Manag..."           the Manage bar face, ellipsised while every sibling fit.
//   3  "TIER UP! Initiate"  painted across the world tree at screen centre.
//   4  "Wave 1" / "Next wave in 45s" painted THROUGH the compass strip, with
//                           "Start Now" jammed against its bottom edge.
// Every layout NUMBER in that frame was legal. That is precisely why this oracle
// exists and why it MEASURES.
//
// ==========================  HOW THIS SUITE IS HONEST  =======================
// The banned shape is an oracle that recomputes a label's fit from the same
// constants the layout used - it cannot fail, and this repo found three of those
// in 24 hours. So:
//
//   * THE WIDTH IS MEASURED, NOT COUNTED. ElarionUiKit.MeasureLineWidthPx sums the
//     real TMP font asset's per-glyph HORIZONTAL ADVANCES - the same numbers TMP
//     steps the pen by - for the string as AUTHORED IN canon-strings.json. Add a
//     word to a canon string, or regenerate a role font with a wider face, and the
//     number moves and this suite fails. Nothing here counts characters.
//   * THE BOX IS PINNED BY SOURCE LINT, NOT ASSUMED. Every box width below is
//     derived from a literal this suite also asserts is still present in the file
//     that authors it (HudKitController is DeNelle.HUD, which this assembly does
//     NOT reference - the SessionShapeRegression assembly note). Narrow a chip or
//     a slot and the lint fails rather than the oracle silently following it down.
//   * TWO ASPECTS, BOTH LANDSCAPE. 2670x1200 (the capture) and 1920x1080. A label
//     that fits at one width can still cut at another, which is the whole reason
//     the WO demanded it.
//   * THE FLOOR IS THE FLOOR. Fit is asserted at ElarionUiKit.FontFloor (30 ref
//     px), the smallest LEGIBLE size auto-sizing may reach. "It would fit at 18px"
//     is not a pass - past the floor TMP ellipsises, which is defect 2 exactly.
//
// WHAT THIS SUITE CANNOT DO: prove two live rects do not overlap on a real canvas.
// That stays the job of RunCaptureHeadless plus eyes-on (canon: verify UI changes
// by SCREENSHOT). What it CAN do is make the four captured defects unreachable by
// construction, headlessly, on every gate run.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using DeNelle.Core.HudModel;
using DeNelle.Core.UI;

namespace DeNelle.Editor.Regression
{
    public static class HudLabelFitRegression
    {
        // ── the canonical artefacts ──────────────────────────────────────────
        private const string CanonRes = "Assets/Resources/Data/Canonical/canon-strings.json";
        private const string CanonStr = "Assets/StreamingAssets/Data/Canonical/canon-strings.json";
        private const string HudSrc   = "Assets/_Modules/HUD/Kit/HudKitController.cs";
        private const string TierSrc  = "Assets/_Modules/Village/Progression/TierSystem.cs";
        private const string AreasRes = "Assets/Resources/Data/Canonical/hud-areas.json";
        /// <summary>The Hero/Realm/Journey card deck - the file under test in Case 6 (WO-1341).</summary>
        private const string DeckSrc  = "Assets/_Modules/HUD/PlayerDeckWorkspace.cs";
        /// <summary>THE REFERENCE IMPLEMENTATION for a kit card face. Owner ruling 2026-09-03:
        /// "should match font and format of Manage screen". Case 6 reads its numbers OUT of this
        /// file rather than restating them, so Manage stays the standard by construction: restyle
        /// Manage and the deck must follow or the suite fails.</summary>
        private const string ManageSrc = "Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs";

        /// <summary>
        /// Card art that has a TITLE AND A TAGLINE PAINTED INTO THE PNG, verified by eye on
        /// 2026-09-03 under Assets/Resources/UI/ElarionMedieval/cards/. These four are the only
        /// ones in the kit that do: buildings.png (Manage), quests.png (Journey) and
        /// realm-store.png (Realm) are all illustration-left with an EMPTY text plate right,
        /// which is the standard ManageScreenPanel.cs:606 states outright ("the approved kit
        /// cards are text-safe layered faces: illustration and border are art, while title,
        /// purpose, count and interaction remain live").
        /// <para>Mounting one of these as a card face gives that card TWO producers for one
        /// string. On device build 2026.09.03.353742 that printed every Hero label twice, in two
        /// fonts, with two different wordings ("Manage your items" over "Browse every carried
        /// item by category", and "LOAD OUT" over "Loadout"). Re-authoring them text-free is the
        /// owner's call; until then no card may mount them.</para>
        /// </summary>
        private static readonly string[] BakedLabelCardArt = { "bag", "equipment", "skills", "loadout" };

        // ── the boxes, each pinned by a source lint in Case 0 ────────────────
        /// <summary>Collectors/Echoes/Resources rail chip, fixed reference px (HudKitController
        /// RailChipWidthPx == EchoUnlockFeedback.EchoChipWidthPx - three chips, one right edge).</summary>
        private const float RailChipWidthPx = 220f;
        /// <summary>Rail chip height - authored AT the tap floor.</summary>
        private const float RailChipHeightPx = 112f;
        /// <summary>ElarionUiKit.BuildObsidianButton insets its label to x 0.04..0.96.</summary>
        private const float ButtonLabelInset = 0.92f;
        /// <summary>HudAreasHost: ActionBar spans x 0.270..0.730 of the canvas.</summary>
        private const float ActionBarZoneFrac = 0.730f - 0.270f;
        /// <summary>HudKitController.BarGap.</summary>
        private const float BarGap = 0.01f;
        /// <summary>HudAreasHost: Status spans x 0.340..0.660 (the wave band inherits its width).</summary>
        private const float StatusZoneFrac = 0.660f - 0.340f;
        /// <summary>HudKitController.WaveBandHeightPx.</summary>
        private const float WaveBandHeightPx = 128f;
        /// <summary>The wave CTA's band inside it (y 0.03..0.93).</summary>
        private const float WaveCtaBandFrac = 0.93f - 0.03f;
        /// <summary>The wave labels' x band inside it (0.02..0.60).</summary>
        private const float WaveLabelBandFrac = 0.60f - 0.02f;
        /// <summary>TMP line advance as a multiple of font size (conservative; TMP's own line
        /// height for these faces is below this, so a pass here is a pass there).</summary>
        private const float LineHeightFactor = 1.2f;

        private struct Aspect { public string Name; public float W, H; }
        private static readonly Aspect[] Aspects =
        {
            new Aspect { Name = "2670x1200 (the capture)", W = 2670f, H = 1200f },
            new Aspect { Name = "1920x1080",               W = 1920f, H = 1080f },
        };

        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("HUD_LABEL_FIT_OK - " + reason);
            else Debug.LogError("HUD_LABEL_FIT_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                Case(failures, "boxes-pinned",     () => Case0_BoxesStillAuthored(failures, notes));
                Case(failures, "canon-parity",     () => Case1_CanonParity(failures, notes));
                Case(failures, "collector-chip",   () => Case2_CollectorChip(failures, notes));
                Case(failures, "manage-face",      () => Case3_ManageFace(failures, notes));
                Case(failures, "wave-band",        () => Case4_WaveBand(failures, notes));
                Case(failures, "tier-stamp",       () => Case5_TierStamp(failures, notes));
                Case(failures, "deck-card-labels", () => Case6_DeckCardSingleProducer(failures, notes));
                Case(failures, "deck-card-packaging", () => Case7_DeckCardPackagingMargin(failures, notes));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes.ToArray()) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "HUD LABEL FIT OK - every authored chip/face line MEASURES inside its own box " +
                         "at the 30px legibility floor at both landscape aspects (real glyph advances, " +
                         "not a character count), the wave block owns a band the compass cannot enter " +
                         "and a CTA clear of the touch floor, and TIER UP is a capped screen stamp" + noteStr;
                return true;
            }
            reason = "hud-label-fit FAIL x" + failures.Count + ": " +
                     string.Join(" | ", failures.ToArray()) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        /// <summary>The parsed Resources copy, filled by Case 1 - the AUTHORED artefact, used as
        /// the fallback so a headless loader hiccup can never turn into a bogus width failure.</summary>
        private static Dictionary<string, string> _authored;

        /// <summary>
        /// The string this suite measures: the RUNTIME resolution (HudStrings, so the loader the
        /// game actually uses is exercised) with the AUTHORED file as the fallback. If the loader
        /// hands back the visible "[[missing:key]]" marker we measure the authored text instead
        /// and SAY SO - measuring the marker would fail on its length and blame the wrong thing.
        /// </summary>
        private static string Copy(List<string> failures, List<string> notes, string key, params object[] args)
        {
            string live = args == null || args.Length == 0 ? HudStrings.Get(key) : HudStrings.Format(key, args);
            if (live != null && live.IndexOf("[[missing:", StringComparison.Ordinal) < 0) return live;

            string raw;
            if (_authored != null && _authored.TryGetValue(key, out raw))
            {
                notes.Add("'" + key + "' did not resolve through the runtime loader headlessly - measured the " +
                          "authored canon-strings text instead");
                try { return args == null || args.Length == 0 ? raw : string.Format(raw, args); }
                catch (FormatException) { return raw; }
            }
            failures.Add("[copy] canon key '" + key + "' resolves to nothing at runtime AND is absent from " +
                         CanonRes + " - the HUD would paint a placeholder marker");
            return "";
        }

        // =====================================================================
        //  CASE 0 - the boxes this suite measures against are STILL the boxes
        // =====================================================================
        // Without this case the suite would be measuring against numbers it made up.
        // DeNelle.EditorRegression cannot reference DeNelle.HUD, so the pin is a source
        // lint: if someone narrows a chip, re-divides the bar, or drops the wave band,
        // this fails LOUDLY instead of the oracle quietly following the layout down.
        private static void Case0_BoxesStillAuthored(List<string> failures, List<string> notes)
        {
            string src = ReadSrc(HudSrc);
            if (src == null)
            {
                failures.Add("[boxes-pinned] cannot read " + HudSrc + " - every measurement below would be " +
                             "against invented numbers");
                return;
            }

            RequireLiteral(failures, src, "RailChipWidthPx = 220f",
                "the rail chip width this suite measures the Collectors lines against");
            RequireLiteral(failures, src, "RailChipHeightPx = ElarionUiKit.MinTouchPx",
                "the rail chip height (three text lines have to seat inside it)");
            RequireLiteral(failures, src, "BarGap = 0.01f",
                "the action-bar gap the per-face slot width is derived from");
            RequireLiteral(failures, src, "WaveBandHeightPx = 128f",
                "the wave block's own fixed-pixel band");

            // The kit's own inset + floors, read from Core (a real reference, not a lint).
            if (Mathf.Abs(ElarionUiKit.MinTouchPx - 112f) > 0.01f)
                failures.Add("[boxes-pinned] ElarionUiKit.MinTouchPx is " + ElarionUiKit.MinTouchPx +
                             ", not 112 - the wave CTA band (0.03..0.93 of " + WaveBandHeightPx +
                             "px) was sized to clear that floor and must be re-derived");
            if (Mathf.Abs(ElarionUiKit.FontFloor - 30f) > 0.01f)
                notes.Add("FontFloor is " + ElarionUiKit.FontFloor + " (was 30 when these boxes were fitted)");

            if (HudActionBarModel.MaxVisibleFaces != 4)
                failures.Add("[boxes-pinned] HudActionBarModel.MaxVisibleFaces is " +
                             HudActionBarModel.MaxVisibleFaces + ", not 4 - adaptive peaceful HUD is locked " +
                             "to Build/Hero/Journey/Manage");

            // The two widgets that collided are STILL both authored into one area, which is why the
            // wave block has to buy its own band rather than trust two fraction stacks to agree.
            string areas = ReadSrc(AreasRes);
            if (areas != null)
            {
                int status = areas.IndexOf("\"status\"", StringComparison.Ordinal);
                int bar = status >= 0 ? areas.IndexOf("\"actionBar\"", status, StringComparison.Ordinal) : -1;
                string window = (status >= 0 && bar > status) ? areas.Substring(status, bar - status) : "";
                bool shared = window.IndexOf("\"compass\"", StringComparison.Ordinal) >= 0 &&
                              window.IndexOf("\"waveBlock\"", StringComparison.Ordinal) >= 0;
                notes.Add(shared
                    ? "compass + waveBlock still share the calm(town) status area - the wave band's " +
                      "fixed-pixel hang below the mount is what keeps them disjoint"
                    : "compass + waveBlock no longer share the status area (the WO-1144 hang may now be " +
                      "belt-and-braces)");
            }
        }

        // =====================================================================
        //  CASE 1 - the words live in canon, in BOTH copies, in ASCII
        // =====================================================================
        private static void Case1_CanonParity(List<string> failures, List<string> notes)
        {
            var res = ReadCanon(CanonRes, failures);
            var str = ReadCanon(CanonStr, failures);
            _authored = res;
            if (res == null || str == null) return;

            foreach (string key in HudStrings.AllKeys)
            {
                string a, b;
                if (!res.TryGetValue(key, out a))
                {
                    failures.Add("[canon-parity] " + CanonRes + " has no '" + key + "' - the HUD would " +
                                 "render the [[missing:key]] marker where a word belongs");
                    continue;
                }
                if (!str.TryGetValue(key, out b))
                {
                    failures.Add("[canon-parity] " + CanonStr + " has no '" + key + "' - a device build " +
                                 "reading StreamingAssets would lose this line");
                    continue;
                }
                if (!string.Equals(a, b, StringComparison.Ordinal))
                    failures.Add("[canon-parity] '" + key + "' DIFFERS between the copies: Resources '" + a +
                                 "' vs StreamingAssets '" + b + "'");
                foreach (char c in a)
                    if (c > 127)
                    {
                        failures.Add("[canon-parity] '" + key + "' carries the non-ASCII character U+" +
                                     ((int)c).ToString("X4") + " - TMP renders it as tofu");
                        break;
                    }
            }
            notes.Add(HudStrings.AllKeys.Length + " HUD copy keys present in both canonical copies");
        }

        // =====================================================================
        //  CASE 2 - the Collectors chip: "Tap to collec" can never come back
        // =====================================================================
        // The chip is 220 x 112 ref px with a 0.92 label inset, so the label rect is
        // ~202 x 112. Line 1 ("Collectors N/M full") WRAPS - which is fine, the chip is
        // two lines tall by design - so the real assertions are:
        //   (a) every ACTION line fits on ONE line of ~202 px at the floor  <- defect 1
        //   (b) the whole block still seats in 112 px of height once line 1 has wrapped
        private static void Case2_CollectorChip(List<string> failures, List<string> notes)
        {
            float boxW = RailChipWidthPx * ButtonLabelInset;
            float boxH = RailChipHeightPx;
            float floor = ElarionUiKit.FontFloor;

            // Worst realistic counts: two digits each side, and a two-digit percentage.
            string count = Copy(failures, notes, HudStrings.KeyCollectorsCount, 12, 12);
            string[] actionLines =
            {
                Copy(failures, notes, HudStrings.KeyCollectorsFullLine),
                Copy(failures, notes, HudStrings.KeyCollectorsNearlyLine, 99),
                Copy(failures, notes, HudStrings.KeyCollectorsWaitingLine, 99),
            };

            foreach (string action in actionLines)
            {
                string detail;
                float w = ElarionUiKit.MeasureLineWidthPx(ElarionUiKit.FontRole.Body, action, floor, out detail);
                if (w < 0f)
                {
                    failures.Add("[collector-chip] cannot measure '" + action + "': " + detail);
                    continue;
                }
                if (w > boxW)
                    failures.Add("[collector-chip] the action line '" + action + "' MEASURES " +
                                 w.ToString("0.0") + " ref px at the " + floor + "px legibility floor but the " +
                                 "chip label rect is only " + boxW.ToString("0.0") + " px (" + detail +
                                 "). There is no legible size at which it fits, so TMP cuts it - that is the " +
                                 "captured 'Tap to collec' exactly. Shorten the WORDS in canon-strings.json; " +
                                 "do NOT drop the font and do NOT widen the chip (three rail chips share one edge)");

                // (b) height: line 1 wraps, then the action line. Measured wrap, not assumed.
                int lines = WrappedLineCount(count, boxW, floor) + 1;
                float needed = lines * floor * LineHeightFactor;
                if (needed > boxH)
                    failures.Add("[collector-chip] '" + count + "' + '" + action + "' needs " + lines +
                                 " wrapped lines = " + needed.ToString("0.0") + " ref px of height at the floor, " +
                                 "but the chip is only " + boxH.ToString("0.0") + " px tall - the bottom line " +
                                 "would be truncated away");
            }

            string d2;
            string title = Copy(failures, notes, HudStrings.KeyCollectorsTitle);
            float cw = ElarionUiKit.MeasureLineWidthPx(ElarionUiKit.FontRole.Body, title, floor, out d2);
            if (cw > boxW)
                failures.Add("[collector-chip] even the bare title '" + title + "' MEASURES " + cw.ToString("0.0") +
                             " px against a " + boxW.ToString("0.0") + " px rect (" + d2 + ")");
            notes.Add("collector chip rect " + boxW.ToString("0") + "x" + boxH.ToString("0") +
                      " ref px; longest action line " + LongestOf(actionLines, floor).ToString("0.0") + " px");
        }

        // =====================================================================
        //  CASE 3 - the Manage bar face: "Manag..." can never come back
        // =====================================================================
        // A face is one MaxVisibleFaces-th of the ActionBar zone, which is a fraction of
        // the CANVAS - so it is aspect-dependent and has to be measured at both. The
        // captured sentence "Manage - 2 of 3 idle" was roughly four times its box; the
        // face now paints ManageBaseLabel plus a canon BADGE on a second line.
        private static void Case3_ManageFace(List<string> failures, List<string> notes)
        {
            float floor = ElarionUiKit.FontFloor;
            float slotFrac = (1f - BarGap * (HudActionBarModel.MaxVisibleFaces - 1)) /
                             HudActionBarModel.MaxVisibleFaces;

            string[] faceLines =
            {
                HudActionBarModel.ManageBaseLabel,
                Copy(failures, notes, HudStrings.KeyManageIdleAll, 3),
                Copy(failures, notes, HudStrings.KeyManageIdleSome, 2, 3),
            };

            foreach (var a in Aspects)
            {
                float canvasW = a.W / ScaleFactor(a.W, a.H);
                float faceW = canvasW * ActionBarZoneFrac * slotFrac;
                float boxW = faceW * ButtonLabelInset;

                foreach (string line in faceLines)
                {
                    string detail;
                    float w = ElarionUiKit.MeasureLineWidthPx(ElarionUiKit.FontRole.Body, line, floor, out detail);
                    if (w < 0f) { failures.Add("[manage-face] cannot measure '" + line + "': " + detail); continue; }
                    if (w > boxW)
                        failures.Add("[manage-face] at " + a.Name + " the face line '" + line + "' MEASURES " +
                                     w.ToString("0.0") + " ref px at the " + floor + "px floor but a bar face's " +
                                     "label rect is only " + boxW.ToString("0.0") + " px (" + detail +
                                     "). TMP ellipsises past the floor - that is the captured 'Manag...'. Put " +
                                     "FEWER WORDS on the face (HudStrings/ManageFaceBadge); the one-line " +
                                     "sentence belongs in HudActionBarModel.ManageFaceLabel, which nothing paints");
                }

                // Two lines have to seat in the face's height as well.
                float barZoneH = (a.H / ScaleFactor(a.W, a.H)) * (0.150f - 0.015f);
                float faceH = barZoneH * (0.95f - 0.10f);
                float needed = 2f * floor * LineHeightFactor;
                if (needed > faceH)
                    failures.Add("[manage-face] at " + a.Name + " a two-line face needs " +
                                 needed.ToString("0.0") + " ref px but the face is only " + faceH.ToString("0.0") +
                                 " px tall - the badge line would be culled, which is worse than the ellipsis " +
                                 "(a culled line says nothing at all)");
                notes.Add("manage face at " + a.Name + ": " + boxW.ToString("0") + "x" + faceH.ToString("0") +
                          " ref px of label rect");
            }

            // The View must still paint the BADGE, not the sentence, or the box math above is moot.
            string src = ReadSrc(HudSrc);
            if (src != null)
            {
                if (src.IndexOf("ManageFaceBadge", StringComparison.Ordinal) < 0)
                    failures.Add("[manage-face] HudKitController no longer reads HudActionBarModel." +
                                 "ManageFaceBadge - if it went back to painting ManageFaceLabel, the face is " +
                                 "carrying a sentence again and every measurement above is measuring the wrong " +
                                 "string");
                if (src.IndexOf("ElarionUiKit.FitBlock(_manageButtonLabel)", StringComparison.Ordinal) < 0)
                    failures.Add("[manage-face] the Manage face label is no longer re-armed with FitBlock - " +
                                 "BuildObsidianButton arms FitSingleLine (no-wrap + ellipsis), so a second line " +
                                 "cannot render and the badge is silently ellipsised away");
            }
        }

        // =====================================================================
        //  CASE 4 - the wave block owns a band the compass cannot enter
        // =====================================================================
        private static void Case4_WaveBand(List<string> failures, List<string> notes)
        {
            string src = ReadSrc(HudSrc);
            if (src == null) { failures.Add("[wave-band] cannot read " + HudSrc); return; }

            // (a) It must NOT stretch the shared Status mount any more. That single line is what
            //     put the wave labels inside the compass strip (which owns y 0.34..1.00 of it).
            int bwb = src.IndexOf("private void BuildWaveBlock", StringComparison.Ordinal);
            int end = bwb >= 0 ? src.IndexOf("Register(\"waveBlock\"", bwb, StringComparison.Ordinal) : -1;
            string body = (bwb >= 0 && end > bwb) ? src.Substring(bwb, end - bwb) : "";
            if (body.Length == 0)
            {
                failures.Add("[wave-band] BuildWaveBlock not found in " + HudSrc);
            }
            else
            {
                if (body.IndexOf("wbrt.anchorMax = Vector2.one", StringComparison.Ordinal) >= 0)
                    failures.Add("[wave-band] the wave block stretches the whole Status mount again " +
                                 "(anchorMax = Vector2.one). hud-areas.json puts the compass in that SAME " +
                                 "mount and HudCompassWidget's strip owns y 0.34-1.00 of it, so the wave " +
                                 "labels are being painted through the compass - the captured defect 4");
                if (body.IndexOf("WaveBandHeightPx", StringComparison.Ordinal) < 0)
                    failures.Add("[wave-band] the wave block no longer sizes itself in FIXED reference px. " +
                                 "It must not go back to a height FRACTION: HudArea.Status is 0.845-0.990, " +
                                 "which is 278 ref px in portrait but collapses to ~140 in landscape, and a " +
                                 "compass + two labels + a bar + a 112px CTA has never fitted in 140");
                if (body.IndexOf("pivot = new Vector2(0.5f, 1f)", StringComparison.Ordinal) < 0)
                    failures.Add("[wave-band] the wave band no longer hangs from its mount's BOTTOM edge " +
                                 "(pivot 0.5,1) - it is back inside the crown with the compass");
            }

            // (b) The CTA must be authored AT the touch floor. ClampMinTouch cannot rescue it: the
            //     rect is still 0 when the button is built, which is how it shipped at ~46 ref px.
            float ctaH = WaveBandHeightPx * WaveCtaBandFrac;
            if (ctaH < ElarionUiKit.MinTouchPx - 0.5f)
                failures.Add("[wave-band] the Start Wave CTA resolves to " + ctaH.ToString("0.0") +
                             " ref px tall, under the " + ElarionUiKit.MinTouchPx + "px touch floor");

            // (c) The countdown - the longest wave string - must fit the label column it now has.
            //     MEASURED at the floor, at both aspects.
            string countdown = "Next wave in 45s";
            foreach (var a in Aspects)
            {
                float canvasW = a.W / ScaleFactor(a.W, a.H);
                float boxW = canvasW * StatusZoneFrac * WaveLabelBandFrac;
                string detail;
                float w = ElarionUiKit.MeasureLineWidthPx(ElarionUiKit.FontRole.Body, countdown,
                                                          ElarionUiKit.FontFloor, out detail);
                if (w < 0f) { failures.Add("[wave-band] cannot measure the countdown: " + detail); break; }
                if (w > boxW)
                    failures.Add("[wave-band] at " + a.Name + " the countdown '" + countdown + "' MEASURES " +
                                 w.ToString("0.0") + " ref px but its label column is only " +
                                 boxW.ToString("0.0") + " px - it would ellipsize the seconds away, which is " +
                                 "the only number on that line that changes");
                else
                    notes.Add("wave countdown at " + a.Name + ": " + w.ToString("0") + " px in a " +
                              boxW.ToString("0") + " px column");
            }
        }

        // =====================================================================
        //  CASE 5 - TIER UP is a CAPPED SCREEN STAMP, never a world-space label
        // =====================================================================
        private static void Case5_TierStamp(List<string> failures, List<string> notes)
        {
            string src = ReadSrc(TierSrc);
            if (src == null) { failures.Add("[tier-stamp] cannot read " + TierSrc); return; }

            if (src.IndexOf("DamageNumberSpawner.SpawnLabel(", StringComparison.Ordinal) >= 0)
                failures.Add("[tier-stamp] TierSystem is back on DamageNumberSpawner.SpawnLabel. That is a " +
                             "WORLD-SPACE TextMesh whose on-screen size is a function of camera distance, and " +
                             "it spawns at the HERO - who in town stands metres from the camera. At the 1.6 " +
                             "scale it shipped with, the 2026-08-22 fleet captured 'TIER UP!  Initiate' " +
                             "painted across the entire world tree. Use CombatText (screen space, font capped " +
                             "at 44 ref px, dark outline, pooled and deduped)");
            if (src.IndexOf("CombatText.Show(", StringComparison.Ordinal) < 0)
                failures.Add("[tier-stamp] TierSystem no longer announces the milestone through CombatText - " +
                             "a tier-up that says nothing is worse than one that says it too loudly");
            notes.Add("TIER UP routed through the CombatText stamp layer");
        }

        // =====================================================================
        //  CASE 6 - ONE STRING, ONE PRODUCER, IN MANAGE'S FORMAT  (WO-1341)
        // =====================================================================
        // WHAT WAS CAPTURED (owner device screenshot, build 2026.09.03.353742, the HERO deck):
        // every label on the screen drawn TWICE, overlapping, in two fonts, disagreeing on the
        // words - "BAG / Manage your items" (serif gold, baked into cards/bag.png) under
        // "BAG / Browse every carried item by category" (the live TMP text), and "LOAD OUT"
        // against "Loadout". The live purpose was additionally ellipsised mid-word.
        //
        // NOT A DOUBLED MOUNT. ObsidianNavigationWorkspace.RenderCurrent destroys every content
        // child before it renders and BuildShell is idempotent, so nothing builds twice. The
        // second producer was the TEXTURE. That is why this case lints an ART REFERENCE and a
        // FORMAT, and does not go looking for a duplicate Build call.
        //
        // HOW THIS CASE STAYS HONEST: the format numbers are not restated here, they are READ
        // OUT OF ManageScreenPanel.cs and the deck is required to equal them. Manage is the
        // standard per the owner's ruling, so if Manage is restyled this fails until the deck
        // follows - it cannot pass by recomputing the deck's own constants back at itself.
        private static void Case6_DeckCardSingleProducer(List<string> failures, List<string> notes)
        {
            string deck = ReadSrc(DeckSrc);
            if (deck == null) { failures.Add("[deck-card-labels] cannot read " + DeckSrc); return; }
            string manage = ReadSrc(ManageSrc);
            if (manage == null) { failures.Add("[deck-card-labels] cannot read " + ManageSrc); return; }

            // ---- 6a  exactly ONE producer per Hero card label ------------------------------
            // Anchor inside CardsFor FIRST. PlayerDeckWorkspace.SubtitleFor switches on the same
            // enum earlier in the file, so slicing from the first 'case PlayerDeckKind.Hero:'
            // lands on a block with no Route( in it and this case would pass on nothing. That is
            // the silent-pass shape this suite's header calls banned - it was caught by running
            // the oracle RED against HEAD before shipping it.
            int cardsFor = deck.IndexOf("List<Card> CardsFor(", StringComparison.Ordinal);
            if (cardsFor < 0)
            {
                failures.Add("[deck-card-labels] no 'List<Card> CardsFor(' in " + DeckSrc +
                             " - the deck card table was renamed and 6a is measuring nothing");
                return;
            }
            string hero = SliceCase(deck.Substring(cardsFor), "case PlayerDeckKind.Hero:");
            if (hero == null)
            {
                failures.Add("[deck-card-labels] no 'case PlayerDeckKind.Hero:' block in " + DeckSrc +
                             " - the Hero deck is the screen the FTUE teaches (Hero -> Skills); this " +
                             "case cannot silently pass because the block was renamed");
                return;
            }

            int routes = 0;
            foreach (string raw in hero.Split('\n'))
            {
                string line = raw.Trim();
                if (line.StartsWith("//", StringComparison.Ordinal)) continue;
                if (line.IndexOf("Route(", StringComparison.Ordinal) < 0) continue;
                routes++;
                // Route(title, purpose, concept, panelId[, artKey]) - three quoted literals is a
                // text-free card whose ONLY label producer is the live TMP text. A fourth is an
                // art key, i.e. a second producer painted into the same plate.
                int quotes = 0;
                for (int i = 0; i < line.Length; i++) if (line[i] == '"') quotes++;
                if (quotes != 6)
                    failures.Add("[deck-card-labels] Hero route has " + (quotes / 2) + " string literals, " +
                                 "expected 3 (title, purpose, concept): '" + line + "'. A 4th is an ART KEY, " +
                                 "and a Hero card that mounts illustrated art gets its title and tagline a " +
                                 "SECOND time from the PNG - that is the WO-1341 defect exactly");
            }
            if (routes != 4)
                failures.Add("[deck-card-labels] found " + routes + " Hero deck routes, expected 4 " +
                             "(Bag, Equipment, Skills, Loadout)");

            // ---- 6b  the label-baked PNGs are mounted by NOTHING in the deck ---------------
            foreach (string key in BakedLabelCardArt)
            {
                string art = CardArtDir + key + ".png";
                if (!File.Exists(art))
                    notes.Add("card art '" + key + "' is gone from disk - if it was re-authored " +
                              "text-free, drop it from BakedLabelCardArt and the art key may return");
                if (deck.IndexOf("\"" + key + "\"", StringComparison.Ordinal) >= 0)
                    failures.Add("[deck-card-labels] " + DeckSrc + " references card art key \"" + key +
                                 "\" as a string literal. " + art + " has a TITLE AND TAGLINE BAKED INTO " +
                                 "THE PNG, so mounting it puts a second producer under the live text. " +
                                 "Re-author the art text-free (owner's call) before restoring this key");
            }

            // ---- 6c  FORMAT PARITY, with the expectation read out of Manage ----------------
            Parity(failures, "card title size",  deck, manage, "face.fontSize", ";");
            Parity(failures, "card title align", deck, manage, "face.alignment", ";");
            Parity(failures, "card title fit",   deck, manage, "FitSingleLine(face,", ")");
            string manageFit = ArgsOf(manage, "FitSingleLine(description,", ")");
            string deckFit   = ArgsOf(deck,   "FitSingleLine(purpose,", ")");
            if (manageFit == null || deckFit == null)
                failures.Add("[deck-card-labels] cannot read the card purpose fit call from " +
                             (manageFit == null ? ManageSrc : DeckSrc));
            else if (!string.Equals(manageFit, deckFit, StringComparison.Ordinal))
                failures.Add("[deck-card-labels] card purpose fit floors drifted from the Manage " +
                             "reference: Manage fits '" + manageFit + "', the deck fits '" + deckFit + "'");
            if (deck.IndexOf("(int)ElarionUi.FontMicro, TextAlignmentOptions.Center", StringComparison.Ordinal) < 0)
                failures.Add("[deck-card-labels] the deck card purpose is no longer FontMicro/Centred - " +
                             "Manage authors its card description at (int)ElarionUi.FontMicro centred " +
                             "(ManageScreenPanel.cs:632-634) and the owner ruled Manage is the standard");

            // ---- 6d  the truncation may not come back --------------------------------------
            // "Choose the abilities equipped for bat..." in the capture was TMP inserting U+2026
            // at RENDER time because the purpose label hard-set TextOverflowModes.Ellipsis with
            // wrapping off. Manage sets neither. The one legitimate ellipsis on a card is the
            // fixed-width "[ LOCKED ]" badge, which cannot truncate.
            string purposeBlock = Between(deck, "available ? spec.Purpose", "FitSingleLine(purpose,");
            if (purposeBlock == null)
                failures.Add("[deck-card-labels] cannot find the deck card purpose label block in " + DeckSrc);
            else
            {
                if (purposeBlock.IndexOf("TextOverflowModes.Ellipsis", StringComparison.Ordinal) >= 0)
                    failures.Add("[deck-card-labels] the deck card purpose hard-sets " +
                                 "TextOverflowModes.Ellipsis again. That is what printed 'Choose the " +
                                 "abilities equipped for bat...' on device - and the inserted glyph is " +
                                 "U+2026, which the tofu oracle will not forgive. Manage lets it wrap");
                if (purposeBlock.IndexOf("enableWordWrapping = false", StringComparison.Ordinal) >= 0)
                    failures.Add("[deck-card-labels] the deck card purpose disables word wrapping again; " +
                                 "Manage does not, and a card plate is too narrow to guarantee one line");
            }

            // ---- 6e  ASCII ONLY in the authored Hero copy -----------------------------------
            for (int i = 0; i < hero.Length; i++)
            {
                if (hero[i] <= 126) continue;
                failures.Add("[deck-card-labels] non-ASCII U+" + ((int)hero[i]).ToString("X4") +
                             " in the Hero deck block - player-facing copy is ASCII-only (no em dash, " +
                             "ellipsis glyph or smart quotes) or the tofu oracle fails on it");
                break;
            }

            notes.Add("Hero deck: 4 text-free cards, one live producer per label, format read from Manage");
        }

        // =====================================================================
        // CASE 7  [deck-card-packaging]  A DECK CARD MAY NEVER DRAW ITS OWN
        //         PACKAGING MARGIN (owner F8 2026-09-03, "the journey raids
        //         button is wrong").
        // ---------------------------------------------------------------------
        // WHAT WAS CAPTURED: the owner's phone photo of the JOURNEY panel. The
        // RAIDS card carried a pale near-white FILLED band right around its ornate
        // frame, spilling to the cell edge, while the QUESTS card beside it - the
        // same art family, the same 1774x887 - was clean. That band is the
        // authoring tool's CHECKERBOARD: cards/raids.png was flattened onto it
        // instead of being exported with alpha, so its border pixels are OPAQUE.
        //
        // WHY THE EXISTING FIX DID NOT COVER IT: WO-1311 derives each card's
        // packaging margin from the sprite's alpha-built TIGHT MESH. That can only
        // see a margin that is TRANSPARENT. For raids.png the mesh honestly reports
        // "no margin at all", the card renders 1:1, and the checkerboard is drawn.
        // Every one of that ticket's own pins stayed green through the defect.
        //
        // HOW THIS CASE IS HONEST - IT OPENS THE PNG. It does not read a constant
        // back at itself and it does not trust the fit code's arithmetic. For every
        // card art key the deck actually mounts it decodes the file, finds the
        // alpha bounds and the INK bounds, and asks the one question the source
        // cannot answer: does this image have a pale border that alpha cannot see?
        //   * a transparent margin  -> the alpha route owns it, no table row wanted
        //   * an opaque pale margin -> PlayerDeckWorkspace.OpaqueMargins MUST carry
        //                              a row for it, with the measured numbers
        //   * a row for a card that no longer has one -> the art was re-exported
        //                              and the row is now a stale crop; delete it
        // Re-export raids.png properly and this case turns RED until the row goes,
        // which is the only way a hardcoded margin is made self-retiring.
        //
        // PROVEN RED: with the OpaqueMargins row for "raids" removed, this case
        // fails with "card art 'raids' has an OPAQUE packaging margin L49 T63 R48
        // B78 ... and NO row" - it names the exact card in the photo, from pixels.
        private static void Case7_DeckCardPackagingMargin(List<string> failures, List<string> notes)
        {
            string deck = ReadSrc(DeckSrc);
            if (deck == null) { failures.Add("[deck-card-packaging] cannot read " + DeckSrc); return; }

            // The plumbing must still be wired, or every measurement below is moot.
            if (deck.IndexOf("TryOpaqueMargin(key, rect", StringComparison.Ordinal) < 0)
                failures.Add("[deck-card-packaging] " + DeckSrc + " no longer calls TryOpaqueMargin " +
                             "from the fit measurement - an opaque packaging margin is invisible to " +
                             "the alpha route, so with that call gone the RAIDS defect is back");
            if (deck.IndexOf("alphaSawNoMargin && !opaqueMargin", StringComparison.Ordinal) < 0)
                failures.Add("[deck-card-packaging] " + DeckSrc + " no longer guards its 'render 1:1' " +
                             "early-return on !opaqueMargin, so an authored margin is measured and " +
                             "then thrown away");

            if (!Directory.Exists(CardArtDir))
            { failures.Add("[deck-card-packaging] card art directory is gone: " + CardArtDir); return; }

            int checkedCards = 0, corrected = 0;
            foreach (string file in Directory.GetFiles(CardArtDir, "*.png"))
            {
                string key = Path.GetFileNameWithoutExtension(file);
                // Only cards the deck actually mounts. An unused PNG's packaging is nobody's bug.
                if (deck.IndexOf("\"" + key + "\"", StringComparison.Ordinal) < 0) continue;
                checkedCards++;

                int w, h, aL, aT, aR, aB, iL, iT, iR, iB;
                if (!MeasureCardPng(file, out w, out h, out aL, out aT, out aR, out aB,
                                    out iL, out iT, out iR, out iB))
                { failures.Add("[deck-card-packaging] cannot decode " + file); continue; }

                bool transparentMargin = aL > 0 || aT > 0 || aR > 0 || aB > 0;
                // A pale border that alpha cannot see. 8px sits well under the ~50px these
                // deliveries carry and well over any single-pixel authoring slop.
                bool opaqueMargin = !transparentMargin && (iL >= 8 || iT >= 8 || iR >= 8 || iB >= 8);
                bool hasRow = HasOpaqueMarginRow(deck, key);

                if (opaqueMargin && !hasRow)
                {
                    failures.Add("[deck-card-packaging] card art '" + key + "' has an OPAQUE packaging " +
                        "margin L" + iL + " T" + iT + " R" + iR + " B" + iB + " (" + w + "x" + h + ") and " +
                        "NO row in PlayerDeckWorkspace.OpaqueMargins. Its border pixels are not " +
                        "transparent, so the WO-1311 alpha route cannot see them and the card draws that " +
                        "pale band around its own frame - the Journey RAIDS defect exactly. Add the row " +
                        "with these measured numbers, or have the PNG re-exported with alpha");
                }
                else if (!opaqueMargin && hasRow)
                {
                    failures.Add("[deck-card-packaging] PlayerDeckWorkspace.OpaqueMargins still carries a " +
                        "row for '" + key + "', but that PNG no longer has an opaque packaging margin " +
                        "(alpha margin L" + aL + " T" + aT + " R" + aR + " B" + aB + "). The art was " +
                        "re-exported; the row is now a hardcoded crop of real artwork. DELETE the row");
                }
                else if (opaqueMargin)
                {
                    corrected++;
                    RequireOpaqueMarginNumbers(failures, deck, key, w, h, iL, iT, iR, iB);
                }
            }

            if (checkedCards == 0)
                failures.Add("[deck-card-packaging] matched NO card art to " + DeckSrc + " - the deck's " +
                             "art keys were renamed and this case is measuring nothing");
            else
                notes.Add("deck card packaging: " + checkedCards + " mounted card PNGs opened, " +
                          corrected + " carry an opaque margin corrected by an authored row");
        }

        /// <summary>Decode a PNG off disk and return its size plus TWO margins, both in pixels off
        /// each edge in IMAGE space (top-left origin): the ALPHA margin (fully transparent border)
        /// and the INK margin (border that is opaque but pale - what the alpha route cannot see).
        /// Reads the file bytes, so the importer's isReadable setting is irrelevant.</summary>
        private static bool MeasureCardPng(string path, out int w, out int h,
                                           out int aL, out int aT, out int aR, out int aB,
                                           out int iL, out int iT, out int iR, out int iB)
        {
            w = h = 0; aL = aT = aR = aB = 0; iL = iT = iR = iB = 0;
            Texture2D tex = null;
            try
            {
                tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(File.ReadAllBytes(path))) return false;
                w = tex.width; h = tex.height;
                if (w < 8 || h < 8) return false;
                var px = tex.GetPixels32();

                int axMin = w, axMax = -1, ayMin = h, ayMax = -1;   // any non-transparent pixel
                int ixMin = w, ixMax = -1, iyMin = h, iyMax = -1;   // non-transparent AND not pale
                for (int y = 0; y < h; y++)
                {
                    int row = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        var c = px[row + x];
                        if (c.a <= 8) continue;
                        if (x < axMin) axMin = x;
                        if (x > axMax) axMax = x;
                        if (y < ayMin) ayMin = y;
                        if (y > ayMax) ayMax = y;
                        // 170/255 sits above every authored parchment tone in these cards and
                        // below the ~200+ checkerboard - that is what separates ink from packaging.
                        if ((c.r + c.g + c.b) / 3 >= 170) continue;
                        if (x < ixMin) ixMin = x;
                        if (x > ixMax) ixMax = x;
                        if (y < iyMin) iyMin = y;
                        if (y > iyMax) iyMax = y;
                    }
                }
                if (axMax < 0 || ixMax < 0) return false;

                // GetPixels32 is bottom-left origin; report TOP-LEFT origin margins.
                aL = axMin; aR = w - 1 - axMax; aT = h - 1 - ayMax; aB = ayMin;
                iL = ixMin; iR = w - 1 - ixMax; iT = h - 1 - iyMax; iB = iyMin;
                return true;
            }
            catch { return false; }
            finally { if (tex != null) UnityEngine.Object.DestroyImmediate(tex); }
        }

        /// <summary>True when PlayerDeckWorkspace.OpaqueMargins declares a row for this art key.
        /// The anchor carries the row's NEXT field on purpose: the card table one screen away
        /// writes 'ArtKey = "raids"', which contains 'Key = "raids"' as a substring and made the
        /// first draft of this case report a row that was not there.</summary>
        private static bool HasOpaqueMarginRow(string deck, string key)
        {
            return deck.IndexOf(RowAnchor(key), StringComparison.Ordinal) >= 0;
        }

        private static string RowAnchor(string key)
        {
            return "Key = \"" + key + "\", Width";
        }

        /// <summary>The authored row must equal what the PNG actually measures - within 2px, which
        /// is authoring slop, not a licence to drift.</summary>
        private static void RequireOpaqueMarginNumbers(List<string> failures, string deck, string key,
                                                       int w, int h, int l, int t, int r, int b)
        {
            int at = deck.IndexOf(RowAnchor(key), StringComparison.Ordinal);
            if (at < 0) return;
            // Bound the row at the NEXT row's constructor rather than at a closing brace: a bare
            // brace literal in this file would unbalance the repo's C# brace-count gate.
            int next = deck.IndexOf("new OpaqueMargin", at, StringComparison.Ordinal);
            int end = next > at ? next : Math.Min(deck.Length, at + 400);
            string row = deck.Substring(at, end - at);
            MarginField(failures, key, row, "Width", w);
            MarginField(failures, key, row, "Height", h);
            MarginField(failures, key, row, "Left", l);
            MarginField(failures, key, row, "Top", t);
            MarginField(failures, key, row, "Right", r);
            MarginField(failures, key, row, "Bottom", b);
        }

        private static void MarginField(List<string> failures, string key, string row,
                                        string name, int measured)
        {
            int at = row.IndexOf(name + " = ", StringComparison.Ordinal);
            if (at < 0)
            { failures.Add("[deck-card-packaging] OpaqueMargins row '" + key + "' has no " + name); return; }
            int i = at + name.Length + 3;
            var digits = new StringBuilder();
            while (i < row.Length && char.IsDigit(row[i])) digits.Append(row[i++]);
            int authored;
            if (digits.Length == 0 || !int.TryParse(digits.ToString(), out authored))
            { failures.Add("[deck-card-packaging] OpaqueMargins row '" + key + "' has an unreadable " + name); return; }
            if (Math.Abs(authored - measured) > 2)
                failures.Add("[deck-card-packaging] OpaqueMargins row '" + key + "' declares " + name + " = " +
                             authored + " but the PNG measures " + measured + ". The authored margin is a " +
                             "claim about that file's pixels - re-measure it, or the card is cropped wrong");
        }

        private const string CardArtDir = "Assets/Resources/UI/ElarionMedieval/cards/";

        /// <summary>Assert the deck's literal for <paramref name="anchor"/> equals the MANAGE
        /// reference's - so the reference file, not this suite, owns the number.</summary>
        private static void Parity(List<string> failures, string what, string deck, string manage,
                                   string anchor, string terminator)
        {
            string want = ArgsOf(manage, anchor, terminator);
            string got  = ArgsOf(deck, anchor, terminator);
            if (want == null) { failures.Add("[deck-card-labels] '" + anchor + "' is gone from the Manage reference " + ManageSrc); return; }
            if (got == null)  { failures.Add("[deck-card-labels] '" + anchor + "' is gone from " + DeckSrc); return; }
            if (!string.Equals(want, got, StringComparison.Ordinal))
                failures.Add("[deck-card-labels] " + what + " drifted from the Manage reference: " +
                             "Manage has '" + anchor + " " + want + "', the deck has '" + got + "'");
        }

        /// <summary>Text between the first <paramref name="anchor"/> and the next
        /// <paramref name="terminator"/>, trimmed. Null when either is absent.</summary>
        private static string ArgsOf(string src, string anchor, string terminator)
        {
            int a = src.IndexOf(anchor, StringComparison.Ordinal);
            if (a < 0) return null;
            a += anchor.Length;
            int b = src.IndexOf(terminator, a, StringComparison.Ordinal);
            if (b < 0) return null;
            return src.Substring(a, b - a).Replace("=", "").Trim();
        }

        private static string Between(string src, string from, string to)
        {
            int a = src.IndexOf(from, StringComparison.Ordinal);
            if (a < 0) return null;
            int b = src.IndexOf(to, a, StringComparison.Ordinal);
            return b < 0 ? null : src.Substring(a, b - a);
        }

        /// <summary>The body of one switch case, up to the next 'case ' / 'default:' label.</summary>
        private static string SliceCase(string src, string label)
        {
            int a = src.IndexOf(label, StringComparison.Ordinal);
            if (a < 0) return null;
            a += label.Length;
            int next = src.IndexOf("case PlayerDeckKind.", a, StringComparison.Ordinal);
            int def  = src.IndexOf("default:", a, StringComparison.Ordinal);
            int end  = next < 0 ? def : def < 0 ? next : Math.Min(next, def);
            return end < 0 ? src.Substring(a) : src.Substring(a, end - a);
        }

        // =====================================================================
        //  helpers
        // =====================================================================

        /// <summary>CanvasScaler ScaleWithScreenSize, reference 1080x1920, MatchWidthOrHeight 0.5 -
        /// the HudAreasHost canvas verbatim. This is what turns a 2670x1200 device frame into the
        /// 2148x965 REFERENCE canvas every fraction above is a fraction OF.</summary>
        private static float ScaleFactor(float screenW, float screenH)
        {
            return Mathf.Pow(screenW / 1080f, 0.5f) * Mathf.Pow(screenH / 1920f, 0.5f);
        }

        /// <summary>Greedy word wrap using the SAME measured advances, so the line count this
        /// suite asserts against is the line count TMP would produce - not a guess at one.</summary>
        private static int WrappedLineCount(string text, float boxW, float fontSize)
        {
            if (string.IsNullOrEmpty(text)) return 0;
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

        private static float LongestOf(string[] lines, float fontSize)
        {
            float max = 0f;
            foreach (string s in lines)
            {
                string detail;
                float w = ElarionUiKit.MeasureLineWidthPx(ElarionUiKit.FontRole.Body, s, fontSize, out detail);
                if (w > max) max = w;
            }
            return max;
        }

        private static void RequireLiteral(List<string> failures, string src, string literal, string why)
        {
            if (src.IndexOf(literal, StringComparison.Ordinal) < 0)
                failures.Add("[boxes-pinned] " + HudSrc + " no longer contains '" + literal + "' (" + why +
                             "). This suite measures against that number - re-measure the labels and update " +
                             "this pin together, or the oracle is asserting against a box that is gone");
        }

        private static string ReadSrc(string relPath)
        {
            try { return File.Exists(relPath) ? File.ReadAllText(relPath) : null; }
            catch { return null; }
        }

        /// <summary>Flat string->string read of a canonical file (the CanonStrings convention),
        /// without pulling a JSON dependency into this suite: the canonical copies are one
        /// "key": "value" pair per line.</summary>
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
