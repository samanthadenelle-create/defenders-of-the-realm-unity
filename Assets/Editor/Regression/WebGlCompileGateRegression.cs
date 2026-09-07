// =============================================================================
// WebGlCompileGateRegression [webgl-compile-gate] - WO-1575
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Shape: public static bool Run(out string reason)
// - registered into DataRegression.RunAll between the oracle-suite fences.
//
// WHAT WENT WRONG, MEASURED (WO-1575 sec.1): Builds/webgl-build.log failed the
// Addressables WebGL content build with exactly one error -
//   Assets\_Modules\Core\Diagnostics\WebTrace.cs(325,35): error CS1501:
//   No overload for method 'Warn' takes 3 arguments
// - while DeNelle.Editor.CompileGate.Run printed its clean-tree marker on this
// machine the entire time. The gate only ever saw the ACTIVE build target's define
// set, so every `#if UNITY_WEBGL` block in the tree was compiled by NOTHING a seat
// runs before committing. FlowTrace is a moving overload set (a 4-arg Measure
// landed 2026-09-06, WO-1483), so this class of rot recurs by construction.
//
// ⛔ THIS FILE MUST NEVER CARRY THE WHOLE-GATE MARKER LITERALS (fixed 2026-09-07,
// Builds/reg-wave10c.log 453/454). RegressionMarkerRegression's gate-script rule
// treats ANY file containing a marker literal as an emitter of it, so the first
// draft - which spelled the whole-gate OK marker out in a const and in four
// comment lines - made checkin_gate.ps1's grep ambiguous between CompileGate.cs
// and this suite, and turned the whole regression red. Every marker this suite
// reasons about is therefore COMPOSED from MarkerPrefix below, which is a
// compile-time constant expression: the VALUES are exact, and the source text
// never contains a whole marker token. Do not "tidy" these back into one literal,
// and do not write a whole marker into a comment here either - the scan reads raw
// text. Naming the marker in PROSE (the whole-gate OK marker) is the way to refer
// to one in this file.
//
// ⚠ HONEST SCOPE - THIS SUITE IS A SOURCE LINT AND SAYS SO IN EVERY REASON STRING.
// It cannot run a WebGL player compile: Assets/Editor/Regression's asmdef
// (DeNelle.EditorRegression) does not reference DeNelle.Editor, where CompileGate
// lives, and a second real player compile inside the regression run would double
// the gate's wall clock for no new information. The compile itself is PROVEN BY
// THE MARKERS on a fresh CompileGate log, which is the project's own rule - judge
// by the marker, never the exit code. What a lint CAN do is stop the pass being
// deleted, reordered or defanged by a later edit, which is the failure mode that
// put WO-1575 on the board.
//
// WHAT THIS PINS, AND WHY EACH ONE IS HERE
//   1. [wired]    RunInternal actually CALLS the WebGL pass. A private method that
//                 nothing invokes is the same hole with more source in it.
//   2. [order]    that call sits AFTER ProveScriptsCompiled(). Check 1's contract is
//                 "read the editor log before this method prints anything"; the
//                 WebGL pass writes its own `error CS` lines into that same live log,
//                 so running it first would let the gate's own diagnostics poison the
//                 editor-compile scan and manufacture a false red.
//   3. [api]      the pass uses PlayerBuildInterface.CompilePlayerScripts with
//                 BuildTarget.WebGL / BuildTargetGroup.WebGL - the EXACT call the
//                 failing content build makes (Scriptable Build Pipeline's
//                 BuildPlayerScripts task, Editor/Tasks/BuildPlayerScripts.cs:41).
//                 Same call, same input, same verdict.
//   4. [no-switch] the file contains NO SwitchActiveBuildTarget. WO-1575's scope
//                 guard, and memory `desktop-build-after-android-target` records the
//                 cost: an unexpected target switch is a full asset reimport and it
//                 breaks the Android/Windows ship chain.
//   5. [target-pin] the pass reads EditorUserBuildSettings.activeBuildTarget on both
//                 sides of the compile, so a switch smuggled in by a future Unity
//                 version is CAUGHT rather than assumed absent (WO-1575 AC4).
//   6. [guard]    BuildPipeline.IsBuildTargetSupported gates the pass, so a machine
//                 with no WebGL module prints a NAMED refusal instead of passing
//                 silently on code it never compiled.
//   7. [markers]  the three WebGL markers exist AND keep their substring discipline:
//                 no WebGL marker contains the whole-gate OK marker, and neither the
//                 WebGL FAIL nor the WebGL SKIPPED literal contains the WebGL OK
//                 literal. An existing grep for the whole-gate pass must not be able
//                 to match this stage, and a grep for the stage's pass must not be
//                 able to match its failure.
//   8. [named-skip] EVERY skip site carries a non-empty `reason=<token>`. Asserted
//                 SHAPEWISE, never against a fixed list of reasons: the gate grew a
//                 third reason (`package-reference-gap`, cg-wave10 2026-09-07) days
//                 after this suite was written, and a hardcoded roster would have
//                 gone red on a legitimate new stand-down. A skip is not a pass, so
//                 what has to be true is that the log SAYS WHY - which is decidable
//                 without knowing the reasons in advance. The reasons found are
//                 listed in the PASS string so the log still names them.
//
// Markers: WEBGL_LINT_LANE_OK / WEBGL_LINT_LANE_FAIL.
// No allowlist, no exemption collection - nothing here to expire (WO-1495).
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DeNelle.Editor.Regression
{
    public static class WebGlCompileGateRegression
    {
        private const string Tag = "[webgl-compile-gate]";
        private const string GatePath = "Assets/Editor/CompileGate.cs";

        // ⛔ COMPOSED, NOT SPELT OUT - see the file header. These are compile-time
        // constant expressions, so the VALUES are exact while the source text of this
        // file never carries a whole marker token for the uniqueness scan to attribute
        // to this suite.
        private const string MarkerPrefix = "COMPILE_GATE_";

        /// <summary>The whole gate's pass marker - the one this stage must never impersonate.</summary>
        private const string GateOk = MarkerPrefix + "OK";

        /// <summary>The three markers the WebGL pass is required to keep emitting.</summary>
        private const string WebGlOk = MarkerPrefix + "WEBGL_OK";
        private const string WebGlFail = MarkerPrefix + "WEBGL_FAIL";
        private const string WebGlSkipped = MarkerPrefix + "WEBGL_SKIPPED";

        public static bool Run(out string reason)
        {
            string src;
            if (!TryReadRepoFile(GatePath, out src, out reason)) return false;

            // --- 1. [wired] ----------------------------------------------------
            int callIdx = src.IndexOf("CompileForWebGl(", StringComparison.Ordinal);
            int declIdx = src.IndexOf("private static List<string> CompileForWebGl(", StringComparison.Ordinal);
            if (declIdx < 0)
            {
                reason = Tag + " SOURCE LINT FAIL [wired]: " + GatePath + " no longer declares " +
                         "`private static List<string> CompileForWebGl(` - the WebGL player-script " +
                         "pass has been removed, so #if UNITY_WEBGL code is compiled by nothing " +
                         "before a commit (WO-1575).";
                return false;
            }
            if (callIdx < 0 || callIdx == declIdx)
            {
                reason = Tag + " SOURCE LINT FAIL [wired]: CompileForWebGl is declared but never " +
                         "CALLED from RunInternal. A pass nothing invokes is the WO-1575 hole with " +
                         "more source in it.";
                return false;
            }

            // --- 2. [order] ----------------------------------------------------
            int proveIdx = src.IndexOf("failures.AddRange(ProveScriptsCompiled());", StringComparison.Ordinal);
            if (proveIdx < 0)
            {
                reason = Tag + " SOURCE LINT FAIL [order]: could not find the check-1 call site " +
                         "`failures.AddRange(ProveScriptsCompiled());` in " + GatePath + ", so the " +
                         "ordering between the editor-log scan and the WebGL pass cannot be pinned. " +
                         "Re-point this lint at the renamed call site.";
                return false;
            }
            if (callIdx < proveIdx)
            {
                reason = Tag + " SOURCE LINT FAIL [order]: the WebGL pass is invoked BEFORE " +
                         "ProveScriptsCompiled(). The WebGL compile appends its own `error CS` lines " +
                         "to the live editor log that check 1 scans, so this ordering manufactures a " +
                         "false red on the editor-compile check.";
                return false;
            }

            // --- 3. [api] ------------------------------------------------------
            string[] required =
            {
                "PlayerBuildInterface.CompilePlayerScripts",
                "ScriptCompilationSettings",
                "BuildTarget.WebGL",
                "BuildTargetGroup.WebGL",
            };
            foreach (string token in required)
            {
                if (src.IndexOf(token, StringComparison.Ordinal) >= 0) continue;
                reason = Tag + " SOURCE LINT FAIL [api]: " + GatePath + " no longer contains `" +
                         token + "`. The pass must use the SAME call the failing Addressables WebGL " +
                         "content build makes (SBP BuildPlayerScripts.cs:41), or it is not testing " +
                         "the thing that broke.";
                return false;
            }

            // --- 4. [no-switch] ------------------------------------------------
            if (src.IndexOf("SwitchActiveBuildTarget", StringComparison.Ordinal) >= 0)
            {
                reason = Tag + " SOURCE LINT FAIL [no-switch]: " + GatePath + " contains " +
                         "SwitchActiveBuildTarget. WO-1575's scope guard forbids it: a target switch " +
                         "is a full asset reimport and it breaks the Android/Windows ship chain " +
                         "(memory `desktop-build-after-android-target`).";
                return false;
            }

            // --- 5. [target-pin] -----------------------------------------------
            int targetReads = CountOccurrences(src, "EditorUserBuildSettings.activeBuildTarget");
            if (targetReads < 2)
            {
                reason = Tag + " SOURCE LINT FAIL [target-pin]: " + GatePath + " reads " +
                         "EditorUserBuildSettings.activeBuildTarget " + targetReads + " time(s); the " +
                         "pass must read it on BOTH sides of the compile and FAIL on a mismatch " +
                         "(WO-1575 AC4). One read cannot prove the target did not move.";
                return false;
            }

            // --- 6. [guard] ----------------------------------------------------
            if (src.IndexOf("IsBuildTargetSupported", StringComparison.Ordinal) < 0)
            {
                reason = Tag + " SOURCE LINT FAIL [guard]: " + GatePath + " no longer calls " +
                         "BuildPipeline.IsBuildTargetSupported. Without it a machine with no WebGL " +
                         "module installed passes SILENTLY on code it never compiled - which is " +
                         "exactly the WO-1575 defect wearing a new hat.";
                return false;
            }

            // --- 7. [markers] --------------------------------------------------
            string[] markers = { WebGlOk, WebGlFail, WebGlSkipped };
            foreach (string marker in markers)
            {
                if (src.IndexOf(marker, StringComparison.Ordinal) >= 0) continue;
                reason = Tag + " SOURCE LINT FAIL [markers]: " + GatePath + " no longer emits `" +
                         marker + "`. Marker absence on a fresh log is a FAILURE (CLAUDE.md sec.16), " +
                         "which only works while the marker exists to be absent.";
                return false;
            }

            // Substring discipline, asserted on the composed values rather than
            // trusted: this is decidable arithmetic, so there is no reason to assume it.
            foreach (string marker in markers)
            {
                if (marker.IndexOf(GateOk, StringComparison.Ordinal) >= 0)
                {
                    reason = Tag + " SOURCE LINT FAIL [markers]: the WebGL marker `" + marker +
                             "` CONTAINS the whole-gate pass marker as a substring, so a grep for " +
                             "the whole gate's verdict would match this one stage instead.";
                    return false;
                }
            }
            if (WebGlFail.IndexOf(WebGlOk, StringComparison.Ordinal) >= 0 ||
                WebGlSkipped.IndexOf(WebGlOk, StringComparison.Ordinal) >= 0)
            {
                reason = Tag + " SOURCE LINT FAIL [markers]: a WebGL failure/skip marker contains the " +
                         "WebGL OK marker as a substring, so a grep for the pass would match a " +
                         "failure. Rename it.";
                return false;
            }

            // --- 8. [named-skip] -----------------------------------------------
            // Shapewise, deliberately: reasons are allowed to grow (the gate added
            // `package-reference-gap` after this suite was written). What must hold is
            // that no stand-down is anonymous.
            List<string> skipReasons;
            if (!TryCollectSkipReasons(src, out skipReasons, out string skipProblem))
            {
                reason = Tag + " SOURCE LINT FAIL [named-skip]: " + skipProblem +
                         " A skip is not a pass, so a skip that does not say WHY is an " +
                         "unexplained hole in the gate's coverage.";
                return false;
            }

            reason = Tag + " SOURCE LINT PASS: CompileGate.cs runs a WebGL player-script pass after " +
                     "check 1 (call at char " + callIdx + " > ProveScriptsCompiled at " + proveIdx +
                     "), via PlayerBuildInterface.CompilePlayerScripts for BuildTarget.WebGL, with no " +
                     "SwitchActiveBuildTarget, " + targetReads + " activeBuildTarget reads, an " +
                     "IsBuildTargetSupported module guard, 3 substring-disjoint markers, and " +
                     skipReasons.Count + " NAMED skip reason(s) [" + string.Join(", ", skipReasons.ToArray()) +
                     "]. WEBGL_LINT_LANE_OK - this is a LINT; the COMPILE itself is a separate " +
                     "artifact, proven by the WebGL markers on a fresh CompileGate log.";
            return true;
        }

        /// <summary>
        /// Every emission of the SKIPPED marker must be immediately followed by a
        /// non-empty <c>reason=&lt;token&gt;</c>. Returns the distinct reasons found.
        /// </summary>
        private static bool TryCollectSkipReasons(string src, out List<string> reasons, out string problem)
        {
            reasons = new List<string>();
            problem = null;

            // Sites are the const IDENTIFIER followed by the reason text, e.g.
            //   Debug.LogWarning(WebGlSkippedMarker + " reason=webgl-module-not-installed :: " ...
            var rx = new Regex(@"WebGlSkippedMarker\s*\+\s*""\s*reason=([A-Za-z0-9._-]*)",
                               RegexOptions.CultureInvariant);
            MatchCollection matches = rx.Matches(src);

            if (matches.Count == 0)
            {
                problem = "no `WebGlSkippedMarker + \" reason=<token>\"` emission site was found in " +
                          GatePath + ", so either the skip path was deleted or its reason is no longer " +
                          "attached to the marker (re-point this lint if the shape changed on purpose).";
                return false;
            }

            foreach (Match m in matches)
            {
                string token = m.Groups[1].Value;
                if (string.IsNullOrEmpty(token))
                {
                    problem = "a SKIPPED emission in " + GatePath + " carries an EMPTY reason token.";
                    return false;
                }
                if (!reasons.Contains(token)) reasons.Add(token);
            }

            return true;
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            int n = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0)
            {
                n++;
                i += needle.Length;
            }
            return n;
        }

        private static bool TryReadRepoFile(string relPath, out string text, out string reason)
        {
            text = null;
            reason = null;
            string full = Path.Combine(Directory.GetCurrentDirectory(),
                                       relPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full))
            {
                reason = Tag + " SOURCE LINT FAIL: " + relPath + " not found at " + full +
                         " - WEBGL_LINT_LANE_FAIL. The gate file this suite exists to pin " +
                         "is gone or moved; re-point the lint rather than deleting it.";
                return false;
            }
            try
            {
                text = File.ReadAllText(full);
            }
            catch (Exception e)
            {
                reason = Tag + " SOURCE LINT FAIL: could not read " + relPath + " (" +
                         e.GetType().Name + ": " + e.Message + ") - WEBGL_LINT_LANE_FAIL. " +
                         "A lint that cannot read its subject reports red, never green.";
                return false;
            }
            return true;
        }
    }
}
