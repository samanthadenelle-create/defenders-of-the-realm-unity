// =============================================================================
// UiCaptureFidelityRegression [ui-capture-fidelity]
//   ⛔ SOURCE LINT over UICaptureLaunch.cs + ElarionUiKit.cs. 100% of it. (WO-1494)
//   It reads those two files as TEXT and asserts that call sites, markers and tokens are
//   still PRESENT. It never runs the capture harness, never builds a canvas, never renders
//   a png and never reads a laid-out RectTransform. It proves the harness still CONTAINS
//   its fidelity machinery; it cannot prove that machinery BEHAVES. Both are worth having
//   and they are not the same claim - see "what this can and cannot see" below.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.  Namespace: DeNelle.Editor.Regression.
// Markers: UI_CAPTURE_FIDELITY_GUARD_OK / UI_CAPTURE_FIDELITY_GUARD_FAIL.
//   (deliberately NOT the runtime harness markers UI_CAPTURE_FIDELITY_OK /
//    UI_GEOMETRY_OK -- CLAUDE.md §8: one distinct marker per entry point, because
//    a shared string is exactly how one suite's pass read as another's.)
//
// WHAT BROKE (two independent RCAs, 2026-08-05): the headless UI capture harness
// was STRUCTURALLY BLIND to geometry defects and had been passing green while
// broken panels shipped to the owner.
//
// THE PROVING MECHANISM, not a theory:
//   * UICaptureLaunch.RenderCanvasToPng flipped the canvas to ScreenSpaceCamera and
//     called ApplyScreenSpaceScale, which rewrites ONLY canvas.scaleFactor. It never
//     resized the root canvas rect and never touched Screen.width/Screen.height.
//   * Panels are BUILT ONCE, before any render, and the kit computes zone geometry
//     AT BUILD TIME from ElarionUiKit.PostScaleCanvasHeight -- which reads Screen.*
//     and falls back to a hard-coded 1920 when Screen is unusable.
//   => Every png a run wrote therefore shared ONE geometry: the editor process's.
//      "1920x1080" and "2340x1080" in the filenames were LABELS, NOT LAYOUTS. Font
//      point size reproduced; zone geometry did not.
//   * That is not hypothetical: the founding Echo card WAS captured at two sizes and
//     passed green all night while, on the device, its caption rendered entirely
//     outside the black plate (layout.body's ZoneBacking). Against the batchmode
//     fallback geometry the defect is invisible; at the device's real geometry it is
//     obvious.
//   * And nothing in this repo had EVER rendered at 2670x1200 -- the Seeker's real
//     surface. 2340x1080 was only the harness size.
//
// This oracle pins the fix so it cannot be quietly undone. All of it is decidable
// from TEXT, so it runs in every batch with no scene, no play mode and no GPU.
//
// ⚠ WHAT THIS CAN AND CANNOT SEE (WO-1494, 2026-09-06). Every RULE below is asserted by
// TEXT MATCH against the harness source - IndexOf / Regex / brace-matched method bodies.
// The RULE prose is written in the harness's voice ("the run PROVES its own fix"): that
// describes what UICaptureLaunch.cs is supposed to DO, and this file only checks that the
// code saying so is still there. So a green run here means THE CALL SITES SURVIVE. It does
// NOT mean a capture ran, that Screen actually moved, that two aspects actually diverged,
// or that any png is geometrically correct. Those are the harness's own markers
// (UI_CAPTURE_FIDELITY_OK / UI_GEOMETRY_OK) and the owner's eyes - deliberately NOT this
// suite's markers, and deliberately not restated as this suite's findings.
//
//   RULE 1 [targets]      The capture matrix still contains 2670x1200, and the file
//          no longer claims 2340x1080 is the device/Seeker resolution.
//
//   RULE 2 [build-per-size]  Panels are built ONCE PER TARGET (ForEachTarget wraps a
//          full build->shoot->teardown), and no RenderCanvasToPng call hard-codes a
//          literal resolution pair. A hard-coded pair is the signature of the old
//          build-once/re-label shape -- the exact defect above.
//
//   RULE 3 [screen-first]  A CaptureSurfaceScope still drives the surface to the
//          target BEFORE the build and VERIFIES the move by reading it back. Setting
//          without verifying is how a harness convinces itself it is accurate.
//
//          UPDATED 2026-08-05 (evening), from captured data. The first cut of this
//          rule pinned the GAME VIEW as the lever. It is not one: in `-batchmode`
//          there is no editor window layout, hence no GameView GUIView for Screen.*
//          to mirror, so Screen stays on the 640x480 offscreen default. Proof, not
//          theory -- Builds/ui-capture-rail.log:475 shows a REAL D3D11 device with
//          screen=640x480 (so this is not the -nographics case) and :489 shows the
//          GameViewSizes/SizeSelectionCallback reflection SUCCEEDING while Screen
//          stayed 640x480; the run ended DEGRADED on every one of its 38 builds. The
//          lever is now ElarionUiKit's injectable surface (RULE 6).
//
//   RULE 6 [kit-surface]  ElarionUiKit exposes SurfaceWidth/SurfaceHeight, they fall
//          back to Screen.* when nothing overrides them (so device behaviour is
//          untouched), the override is EDITOR-ONLY, and PostScaleCanvasHeight reads
//          the surface rather than Screen directly. This is the only rule in the file
//          that reads the kit; without it the harness's lever can be removed from the
//          other end and every source-text check here would still pass.
//
//   RULE 7 [aspect-divergence]  The run PROVES its own fix: two targets with
//          different aspect ratios must resolve DIFFERENT zone rects, and identical
//          rects must fail the run. Without this, a future regression that quietly
//          re-pins the surface would restore the original defect -- one geometry
//          wearing several filenames -- with every other check in this file green.
//
//   RULE 4 [honest-degrade]  When the editor refuses to move Screen, the harness
//          says so LOUDLY (UI_CAPTURE_FIDELITY_DEGRADED, at error severity). A
//          scale-only run that reports itself as geometry-accurate is worse than no
//          run: a wrong shot gets reviewed, a missing one gets chased.
//
//   RULE 5 [geometry-gate]  The numeric layout assertions still exist and still
//          FAIL (not warn): text off its layout.body ZoneBacking, overlapping
//          sibling buttons, a button over foreign text, and an authored band under
//          ElarionUiKit.MinTouchPx measured BEFORE ClampMinTouch grows it. Eyes-only
//          review is not a gate; these numbers are.
//
// Deliberately NOT asserted: that a given run passed. Whether the pngs are clean is
// the run's job (its own markers) and the owner's eyes'. This oracle guards the
// HARNESS -- that it is still capable of seeing.
//
// Standalone: run-unity-method.ps1
//   -Method DeNelle.Editor.Regression.UiCaptureFidelityRegression.RunAll
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class UiCaptureFidelityRegression
    {
        private const string HarnessSrc = "Assets/Editor/UICaptureLaunch.cs";

        /// <summary>The OTHER end of the lever: the kit the harness now drives (RULE 6).</summary>
        private const string KitSrc = "Assets/_Modules/Core/UI/ElarionUiKit.cs";

        /// <summary>How much source after a method's signature counts as "inside" it.</summary>
        private const int MethodWindowChars = 400;

        /// <summary>A wider window for methods whose banner comment precedes the body.</summary>
        private const int WideWindowChars = 1600;

        /// <summary>
        /// The body of the method whose signature match starts at <paramref name="from"/>, found by
        /// BRACE MATCHING rather than a character window.
        ///
        /// <para>⛔ WHY THIS EXISTS (2026-08-22): the checks below used to slice a fixed
        /// <see cref="WideWindowChars"/> window after the signature. That makes an assertion's
        /// coverage a function of SOURCE FORMATTING - the least reliable signal available. Adding one
        /// capture entry to <c>RunCaptureHeadless</c> pushed <c>ProveGeometryMoves()</c> (still
        /// present, 34 lines below the signature) past the 1600-char edge, and the suite reported it
        /// as "gone ... the most convincing kind of dead code" while it was sitting right there.
        /// A false RED is cheaper than a false green but it still costs a debugging cycle, and the
        /// same window would eventually hide a real deletion behind a long comment.</para>
        ///
        /// <para>This is the identical defect the hollow-pass ratchet was widened to fix on the same
        /// day (its 4-line window missed 5 of 6 real sites). Match STRUCTURE, never proximity.</para>
        ///
        /// <para>Returns the whole remainder if the braces do not balance, so a malformed file
        /// degrades to the old over-broad behaviour instead of silently asserting nothing.</para>
        /// </summary>
        private static string MethodBody(string src, int from)
        {
            int open = src.IndexOf('{', from);
            if (open < 0) return src.Substring(from);

            int depth = 0;
            for (int i = open; i < src.Length; i++)
            {
                if (src[i] == '{') depth++;
                else if (src[i] == '}')
                {
                    depth--;
                    if (depth == 0) return src.Substring(open, i - open + 1);
                }
            }
            return src.Substring(open);
        }

        /// <summary>Standalone batch entry - prints the distinct marker a gate can grep.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("UI_CAPTURE_FIDELITY_GUARD_OK - " + reason);
            else Debug.LogError("UI_CAPTURE_FIDELITY_GUARD_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                string src = ReadSource(HarnessSrc, "input", failures);
                if (src != null)
                {
                    Case(failures, "targets", () => Case1_Targets(src, failures, notes));
                    Case(failures, "build-per-size", () => Case2_BuildPerSize(src, failures, notes));
                    Case(failures, "screen-first", () => Case3_ScreenFirst(src, failures, notes));
                    Case(failures, "honest-degrade", () => Case4_HonestDegrade(src, failures, notes));
                    Case(failures, "geometry-gate", () => Case5_GeometryGate(src, failures, notes));
                    Case(failures, "aspect-divergence", () => Case7_AspectDivergence(src, failures, notes));
                }

                string kit = ReadSource(KitSrc, "kit-surface", failures);
                if (kit != null)
                    Case(failures, "kit-surface", () => Case6_KitSurface(kit, failures, notes));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes.ToArray()) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "UI CAPTURE FIDELITY GUARD OK (SOURCE LINT, not a capture) - UICaptureLaunch.cs " +
                         "still CONTAINS the 2670x1200 target, the per-target build/shoot/teardown, the " +
                         "CaptureSurfaceScope read-back, the ProveGeometryMoves call site and its " +
                         "RunCaptureHeadless caller, the DEGRADED LogError, and the AuditGeometry gate " +
                         "tokens; ElarionUiKit.cs still routes PostScaleCanvasHeight through the " +
                         "injectable surface. This is PRESENCE OF THE CODE, proven by text match. It is " +
                         "NOT proof that a capture ran, that Screen moved, or that any png is " +
                         "geometrically correct - that is UI_CAPTURE_FIDELITY_OK / UI_GEOMETRY_OK on a " +
                         "harness run" + noteStr;
                return true;
            }
            reason = "ui-capture-fidelity FAIL x" + failures.Count + ": " +
                     string.Join(" | ", failures.ToArray()) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  RULE 1 - the Seeker's real surface is in the matrix
        // =====================================================================
        private static void Case1_Targets(string src, List<string> failures, List<string> notes)
        {
            if (!Regex.IsMatch(src, @"CaptureTarget\s*\(\s*2670\s*,\s*1200\s*\)"))
                failures.Add("[targets] the capture matrix no longer contains 2670x1200 - the Solana " +
                             "SEEKER'S REAL SURFACE. Before 2026-08-05 NOTHING in this repo had ever " +
                             "rendered at it, so every 'device' shot was a different device. Restore it.");
            else
                notes.Add("2670x1200 (Seeker) in matrix");

            if (!Regex.IsMatch(src, @"CaptureTarget\s*\(\s*1920\s*,\s*1080\s*\)"))
                failures.Add("[targets] the 1920x1080 reference landscape target is gone - it is the " +
                             "baseline every historical review shot was taken at");

            // The wrong claim that started this: 2340x1080 is a harness size, nothing more.
            foreach (Match m in Regex.Matches(src, @"2340x1080[^\r\n]*"))
            {
                string line = m.Value;
                if (Regex.IsMatch(line, @"(?i)the\s+(device|Seeker'?s?)\s+(resolution|screen|surface)")
                    || Regex.IsMatch(line, @"(?i)Seeker'?s?\s+EXACT\s+screen"))
                {
                    failures.Add("[targets] a comment still calls 2340x1080 the device/Seeker screen (\"" +
                                 Trim(line) + "\"). It is not - the Seeker's surface is 2670x1200. That " +
                                 "wrong label is why the real resolution went unshot for months.");
                }
            }
        }

        // =====================================================================
        //  RULE 2 - built per size, never built once and re-labelled
        // =====================================================================
        private static void Case2_BuildPerSize(string src, List<string> failures, List<string> notes)
        {
            if (src.IndexOf("ForEachTarget", StringComparison.Ordinal) < 0)
            {
                failures.Add("[build-per-size] ForEachTarget is gone. It is the ONLY thing making a panel " +
                             "be BUILT at the size it is shot at; without it the harness returns to " +
                             "building once and re-scaling, which is how filenames became labels rather " +
                             "than layouts and how the founding Echo card passed green while broken.");
                return;
            }

            // The old shape, verbatim: a literal WxH pair passed straight to the renderer.
            var hardCoded = new List<string>();
            foreach (Match m in Regex.Matches(src,
                @"RenderCanvasToPng\s*\([^;]*?,\s*(?<w>\d{3,5})\s*,\s*(?<h>\d{3,5})\s*\)"))
            {
                hardCoded.Add(m.Groups["w"].Value + "x" + m.Groups["h"].Value);
            }
            if (hardCoded.Count > 0)
                failures.Add("[build-per-size] " + hardCoded.Count + " RenderCanvasToPng call(s) still pass a " +
                             "HARD-CODED resolution (" + string.Join(", ", hardCoded.ToArray()) + ") instead of " +
                             "the target the panel was built under. That literal pair is the signature of the " +
                             "build-once/re-label harness: the png gets the filename of a resolution whose " +
                             "geometry it never had. Route it through ForEachTarget's CaptureTarget.");
            else
                notes.Add("no hard-coded render resolutions");

            // Every capture entry point the run calls must be a per-target one.
            var entries = new List<string>();
            foreach (Match m in Regex.Matches(src, @"count\s*\+=\s*(?<n>Capture[A-Za-z0-9_]+)\s*\(\s*\)"))
                entries.Add(m.Groups["n"].Value);
            if (entries.Count == 0)
            {
                // NB: the capture harness's own success marker is deliberately NOT spelled out as a
                // literal here. RegressionMarkerRegression scans oracle files for any *_OK token
                // inside a string and flags it as an EMISSION, so naming it would make this file
                // look like a second emitter of the harness's marker and fail the collision guard
                // (which exists because three entry points once shared one marker and a 22-case
                // pass read as the full suite's). Describe the marker; never spell it.
                failures.Add("[build-per-size] RunCaptureHeadless calls no Capture* entry points - the run " +
                             "would emit a green capture marker with a count of zero over nothing at all");
                return;
            }
            // Window scan, NOT a brace-matching regex: a literal brace inside a string trips
            // the CLAUDE.md rule-1 naive counter and the CompileGate scan (the lesson
            // RegressionMarkerRegression.cs records at its OpenBrace/CloseBrace pair).
            var notWrapped = new List<string>();
            foreach (string e in entries)
            {
                var sig = Regex.Match(src, @"private\s+static\s+int\s+" + Regex.Escape(e) + @"\s*\(\s*\)");
                if (!sig.Success) { notWrapped.Add(e); continue; }
                int start = sig.Index;
                int window = Math.Min(MethodWindowChars, src.Length - start);
                if (src.Substring(start, window).IndexOf("ForEachTarget", StringComparison.Ordinal) < 0)
                    notWrapped.Add(e);
            }
            if (notWrapped.Count > 0)
                failures.Add("[build-per-size] these capture entries do not run through ForEachTarget, so " +
                             "their panels are built once and shot at whatever geometry the editor happened " +
                             "to have: " + string.Join(", ", notWrapped.ToArray()));
            else
                notes.Add(entries.Count + " capture entries, all per-target");
        }

        // =====================================================================
        //  RULE 3 - move Screen BEFORE the build, and VERIFY the move
        // =====================================================================
        private static void Case3_ScreenFirst(string src, List<string> failures, List<string> notes)
        {
            if (src.IndexOf("CaptureSurfaceScope", StringComparison.Ordinal) < 0)
            {
                failures.Add("[screen-first] CaptureSurfaceScope is gone. Panels resolve their zones " +
                             "through ElarionUiKit.PostScaleCanvasHeight on the frame they are built, so " +
                             "moving the surface BEFORE the build is the only lever that makes a capture " +
                             "geometry-accurate. Nothing done to the canvas afterwards can substitute for it.");
                return;
            }

            // The lever itself: the kit's injectable surface, set AND released.
            if (src.IndexOf("SetSurfaceOverride", StringComparison.Ordinal) < 0)
                failures.Add("[screen-first] the scope no longer calls ElarionUiKit.SetSurfaceOverride. " +
                             "That is the load-bearing move -- the game-view path it replaced CANNOT move " +
                             "Screen.* in batchmode (captured proof: a D3D11 run whose reflection succeeded " +
                             "while Screen stayed 640x480 for all 38 builds).");
            else if (src.IndexOf("ClearSurfaceOverride", StringComparison.Ordinal) < 0)
                failures.Add("[screen-first] the scope sets the surface override but never clears it. A " +
                             "leaked override mis-sizes every panel built afterwards in that editor " +
                             "session, including the owner's own play-mode UI.");
            else
                notes.Add("surface override set + released");

            // VERIFY the effective surface (the value the build will actually read)...
            if (!Regex.IsMatch(src, @"ew\s*==\s*target\.W")
                && !Regex.IsMatch(src, @"ElarionUiKit\.SurfaceWidth\s*==\s*target\.W"))
                failures.Add("[screen-first] the scope no longer VERIFIES the move by reading the " +
                             "effective surface (ElarionUiKit.SurfaceWidth/SurfaceHeight) back. An " +
                             "unverified set is an assumption, and this whole defect class is assumptions " +
                             "the harness made about its own output.");
            else
                notes.Add("surface move is read back and verified");

            // ...and STILL read Screen back, because it is the residual the report must name.
            if (!Regex.IsMatch(src, @"Screen\.width\s*==\s*target\.W")
                && !Regex.IsMatch(src, @"sw\s*==\s*target\.W"))
                failures.Add("[screen-first] the scope no longer reads Screen.* back at all. It never " +
                             "moves in batchmode, and the run must SAY so: a panel that still reads " +
                             "Screen.* directly at build time is not covered by the kit surface, and " +
                             "silently dropping that residual is how the next blind spot opens.");

            if (src.IndexOf("PostScaleCanvasHeight", StringComparison.Ordinal) < 0)
                failures.Add("[screen-first] the file no longer records WHY the surface move matters " +
                             "(ElarionUiKit.PostScaleCanvasHeight is the build-time reader). Keep the " +
                             "causal note: the next reader will otherwise delete the scope as ceremony.");
        }

        // =====================================================================
        //  RULE 6 - the other end of the lever: the kit's injectable surface
        // =====================================================================
        private static void Case6_KitSurface(string kit, List<string> failures, List<string> notes)
        {
            if (kit.IndexOf("SetSurfaceOverride", StringComparison.Ordinal) < 0
                || kit.IndexOf("SurfaceWidth", StringComparison.Ordinal) < 0
                || kit.IndexOf("SurfaceHeight", StringComparison.Ordinal) < 0)
            {
                failures.Add("[kit-surface] ElarionUiKit no longer exposes an injectable surface " +
                             "(SurfaceWidth / SurfaceHeight / SetSurfaceOverride). It is the ONLY lever " +
                             "that moves build-time geometry in batchmode; without it every capture " +
                             "returns to one geometry wearing several filenames.");
                return;
            }

            // It must DEFAULT to Screen.* -- the injection may not change shipped behaviour.
            if (!Regex.IsMatch(kit, @"SurfaceWidth\s*=>[^;]*Screen\.width")
                || !Regex.IsMatch(kit, @"SurfaceHeight\s*=>[^;]*Screen\.height"))
                failures.Add("[kit-surface] SurfaceWidth/SurfaceHeight no longer fall back to " +
                             "Screen.width/Screen.height. The whole safety of this seam is that with no " +
                             "override it is byte-identical to the behaviour it replaced; a different " +
                             "default would be a silent layout change on the device.");
            else
                notes.Add("surface defaults to Screen.*");

            if (kit.IndexOf("Application.isEditor", StringComparison.Ordinal) < 0)
                failures.Add("[kit-surface] the surface override is no longer gated to the editor. A " +
                             "capture-only lever that a shipped build can pull is a device layout bug " +
                             "waiting for its first caller.");
            else
                notes.Add("override is editor-only");

            // And PostScaleCanvasHeight must actually READ it, or the seam is decorative.
            if (Regex.IsMatch(kit, @"float\s+screenH\s*=\s*Screen\.height"))
                failures.Add("[kit-surface] PostScaleCanvasHeight reads Screen.height directly again, so " +
                             "the injectable surface no longer reaches the one computation that resolves " +
                             "zone geometry at build time. That is the exact wiring the 2026-08-05 fix put in.");
            else if (!Regex.IsMatch(kit, @"float\s+screenH\s*=\s*SurfaceHeight"))
                failures.Add("[kit-surface] PostScaleCanvasHeight no longer resolves its height from " +
                             "SurfaceHeight -- the harness can set the surface all it likes and nothing " +
                             "will read it.");
            else
                notes.Add("PostScaleCanvasHeight reads the surface");
        }

        // =====================================================================
        //  RULE 7 - the run proves its own fix, or fails
        //  (SOURCE LINT: this case only checks the proof's call sites still exist)
        // =====================================================================
        private static void Case7_AspectDivergence(string src, List<string> failures, List<string> notes)
        {
            if (src.IndexOf("ProveGeometryMoves", StringComparison.Ordinal) < 0)
            {
                failures.Add("[aspect-divergence] ProveGeometryMoves is gone. It is the only check that " +
                             "two different ASPECTS resolve DIFFERENT zone rects -- i.e. the only thing " +
                             "standing between this harness and a silent return to one geometry wearing " +
                             "several filenames, which every other rule here would pass.");
                return;
            }

            var call = Regex.Match(src, @"public\s+static\s+void\s+RunCaptureHeadless\s*\(\s*\)");
            if (!call.Success ||
                MethodBody(src, call.Index).IndexOf("ProveGeometryMoves", StringComparison.Ordinal) < 0)
                failures.Add("[aspect-divergence] RunCaptureHeadless no longer runs ProveGeometryMoves, " +
                             "so the proof exists but never executes - the most convincing kind of dead code.");
            else
                notes.Add("divergence proof runs every capture");

            // The proof must derive its pair from the real matrix, not a hard-coded one, or it
            // stops tracking the matrix the moment a target is added or retired.
            var sig = Regex.Match(src, @"private\s+static\s+void\s+ProveGeometryMoves\s*\(\s*\)");
            if (sig.Success &&
                MethodBody(src, sig.Index)
                   .IndexOf("LandscapeTargets", StringComparison.Ordinal) < 0)
                failures.Add("[aspect-divergence] ProveGeometryMoves no longer derives its two aspects " +
                             "from LandscapeTargets, so the proof can drift away from the matrix the " +
                             "captures actually use (and away from the Seeker's 2670x1200).");

            if (src.IndexOf("RectDiffers", StringComparison.Ordinal) < 0)
                failures.Add("[aspect-divergence] the resolved-rect comparison (RectDiffers) is gone - " +
                             "the proof can no longer tell a moved layout from an identical one");

            // Failing the proof must sink the RUN, not print a note nobody reads.
            if (!Regex.IsMatch(src, @"_geoMoveProof\s*==\s*null"))
                failures.Add("[aspect-divergence] the fidelity report no longer branches on the proof, so " +
                             "a run whose geometry did NOT move can still report itself accurate. A " +
                             "warning here is indistinguishable from the silence that shipped the defect.");
            else
                notes.Add("failed proof degrades the run");
        }

        // =====================================================================
        //  RULE 4 - a scale-only run must announce itself
        // =====================================================================
        private static void Case4_HonestDegrade(string src, List<string> failures, List<string> notes)
        {
            if (src.IndexOf("UI_CAPTURE_FIDELITY_DEGRADED", StringComparison.Ordinal) < 0)
            {
                failures.Add("[honest-degrade] the UI_CAPTURE_FIDELITY_DEGRADED marker is gone. When the " +
                             "editor will not move Screen the harness is scale-only, and it MUST say so - " +
                             "silently reporting geometry accuracy it does not have is the original defect, " +
                             "not a lesser version of it.");
                return;
            }
            if (src.IndexOf("UI_CAPTURE_FIDELITY_OK", StringComparison.Ordinal) < 0)
                failures.Add("[honest-degrade] the UI_CAPTURE_FIDELITY_OK marker is gone, so a caller can no " +
                             "longer assert the positive case at all");

            if (!Regex.IsMatch(src, @"LogError\s*\(\s*""UI_CAPTURE_FIDELITY_DEGRADED"))
                failures.Add("[honest-degrade] UI_CAPTURE_FIDELITY_DEGRADED is no longer emitted at ERROR " +
                             "severity. A warning scrolls past; this one has to stop the gate.");
            else
                notes.Add("degrade is loud (LogError)");
        }

        // =====================================================================
        //  RULE 5 - the numeric layout gate still EXISTS and is still wired as a failure
        //  (SOURCE LINT: the numbers are AuditGeometry's, computed on a harness run, never here)
        // =====================================================================
        private static void Case5_GeometryGate(string src, List<string> failures, List<string> notes)
        {
            if (src.IndexOf("AuditGeometry", StringComparison.Ordinal) < 0)
            {
                failures.Add("[geometry-gate] AuditGeometry is gone. Without it the only thing standing " +
                             "between a layout defect and the owner is somebody opening a png and " +
                             "noticing - which is what failed for the founding Echo card.");
                return;
            }
            if (!Regex.IsMatch(src, @"AuditGeometry\s*\(\s*canvasGo"))
                failures.Add("[geometry-gate] AuditGeometry is no longer called from the render path, so it " +
                             "measures nothing however complete it looks");

            if (src.IndexOf("UI_GEOMETRY_OK", StringComparison.Ordinal) < 0
                || src.IndexOf("UI_GEOMETRY_FAIL", StringComparison.Ordinal) < 0)
                failures.Add("[geometry-gate] the UI_GEOMETRY_OK / UI_GEOMETRY_FAIL markers are gone - the " +
                             "orchestrator has nothing to assert on");
            else if (!Regex.IsMatch(src, @"LogError\s*\(\s*""UI_GEOMETRY_FAIL"))
                failures.Add("[geometry-gate] UI_GEOMETRY_FAIL is no longer an ERROR. These four rules were " +
                             "made failures on purpose: as warnings they are indistinguishable from the " +
                             "silence that shipped the defect.");

            // Each rule pinned by an IndexOf on its token - presence in the harness source,
            // never a measurement of what the rule does at runtime (WO-1494).
            RequireAll(src, failures, notes, "geometry-gate", new[]
            {
                Pin("ZoneBacking", "the layout.body black-plate containment rule - the founding Echo card's " +
                                   "caption rendered entirely outside this plate and every capture passed"),
                Pin("MinTouchPx", "the authored sub-touch-floor rule - the band is measured BEFORE " +
                                  "ClampMinTouch grows it, because the sub-floor band is the defect " +
                                  "signature and the symmetric growth is only its consequence"),
                Pin("RectMask2D", "the masked-content exemption - without it every scrolled row reads as " +
                                  "an overflow and the gate drowns in false failures nobody triages"),
            });

            if (src.IndexOf("BUTTONS OVERLAP", StringComparison.Ordinal) < 0)
                failures.Add("[geometry-gate] the sibling-button overlap rule is gone (no 'BUTTONS OVERLAP' " +
                             "finding) - that is the 'options stacked' / 'only the bottom chip is tappable' " +
                             "defect class");
            if (src.IndexOf("BUTTON OVER TEXT", StringComparison.Ordinal) < 0)
                failures.Add("[geometry-gate] the button-over-text rule is gone (no 'BUTTON OVER TEXT' finding)");
            if (src.IndexOf("TEXT OFF PLATE", StringComparison.Ordinal) < 0)
                failures.Add("[geometry-gate] the text-off-plate rule is gone (no 'TEXT OFF PLATE' finding) - " +
                             "it is the one that catches the Echo card at EVERY resolution");
        }

        // =====================================================================
        //  helpers
        // =====================================================================
        private sealed class PinSpec
        {
            public string Token;
            public string Why;
        }

        private static PinSpec Pin(string token, string why)
        {
            return new PinSpec { Token = token, Why = why };
        }

        private static void RequireAll(string src, List<string> failures, List<string> notes,
                                       string tag, PinSpec[] pins)
        {
            int ok = 0;
            foreach (var p in pins)
            {
                if (src.IndexOf(p.Token, StringComparison.Ordinal) >= 0) { ok++; continue; }
                failures.Add("[" + tag + "] '" + p.Token + "' no longer appears in " + HarnessSrc +
                             " - that removes " + p.Why);
            }
            if (ok == pins.Length) notes.Add("geometry rules pinned: " + ok + "/" + pins.Length);
        }

        private static string Trim(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Trim();
            return s.Length <= 90 ? s : s.Substring(0, 87) + "...";
        }

        private static string ReadSource(string path, string tag, List<string> failures)
        {
            try
            {
                if (!File.Exists(path))
                {
                    failures.Add("[" + tag + "] source not found: " + path +
                                 " - the UI capture harness is the subject of this oracle; if it moved, " +
                                 "re-point the path rather than dropping the guard");
                    return null;
                }
                return File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                failures.Add("[" + tag + "] could not read " + path + ": " + ex.Message);
                return null;
            }
        }
    }
}
