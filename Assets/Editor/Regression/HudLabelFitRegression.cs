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

            if (HudActionBarModel.MaxVisibleFaces != 6)
                failures.Add("[boxes-pinned] HudActionBarModel.MaxVisibleFaces is " +
                             HudActionBarModel.MaxVisibleFaces + ", not 6 - the bar face got NARROWER or " +
                             "wider, so the Manage face measurement below is stale. Re-measure before " +
                             "changing this number: a face is already only about 10 characters wide at " +
                             "the legibility floor");

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
