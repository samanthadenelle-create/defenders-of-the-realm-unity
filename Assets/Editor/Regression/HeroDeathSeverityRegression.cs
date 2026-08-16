// =============================================================================
// HeroDeathSeverityRegression (audit 2026-08-15) - a NORMAL hero death must not
// raise an F8 ERROR. Source-structural, headless, milliseconds, no play mode.
// -----------------------------------------------------------------------------
// THE DEFECT THIS PINS:
//   HeroHealth.EnterDeathFreeze dumped its freeze state with
//     FlowTrace.Fail("HeroDeath", "death freeze armed: ...")
//   and the comment above it admitted exactly why: "break-log is errors-only on
//   device - use Fail". That was TRUE. BreakCaptureHarness.OnLog records only
//   LogType.Error / Exception / Assert, so Fail was the ONLY severity that
//   survived to a tester's device - and the price was a PERMANENT, EXPECTED
//   error on the single most common event in the game.
//
//   Per CLAUDE.md sec.14 the owner's F8 captures are triaged LIVE by a seat. So
//   every time she died - twice in one dungeon run the night this was found -
//   the watch daemon woke a seat with an "error" that was just the game working.
//   RaidHeroCarryRegression's own header already cites the damage: "seats learned
//   to ignore Hero Fails". A trace that always fires is worse than no trace,
//   because it teaches everyone to stop reading the channel that the whole
//   instrument-first directive (sec.12) depends on.
//
// THE FIX THIS PINS - a MISSING SEVERITY was built, nothing was deleted:
//   FlowTrace.Capture(system, message) = capture-worthy but NOT a failure. It
//   logs at INFO (so no listener, gate or daemon reads it as an error) and calls
//   BreakCaptureHarness.RecordNote, which writes a kind:"note" row straight into
//   break-log.jsonl - bypassing the Unity log listener entirely. The F8 watch
//   daemon skips "note" alongside session_start / scene_loaded, so the state dump
//   still lands for post-hoc reading and NEVER wakes a triage seat.
//
//   sec.12 forbids stripping instrumentation, so this suite is deliberately
//   two-sided: it fails if the dump is gone (case 1) AND if it is an error again
//   (case 2). Downgrading the trace to nothing would break this suite just as
//   loudly as putting the Fail back.
//
// SCOPE NOTE - what is deliberately NOT banned:
//   HeroHealth's LateUpdate death-pin RESIDUAL watchdog keeps its FlowTrace.Fail.
//   That one fires only when a mover OTHER than the frozen agent writes a dead
//   hero's transform, which is a real defect being named. Case 2's banned-phrase
//   list targets NORMAL-LIFECYCLE prose only; it must never be widened into "no
//   Fail anywhere in HeroHealth", which would silence a working alarm.
//
// Contract mirrors the other covenant suites: Run(out string reason).
//   true  = pass (reason = one-line summary)
//   false = fail (reason = the exact invariant that broke)
// Registered in DataRegression.RunAll with the DISTINCT [hero-death-severity] tag.
// Standalone: run-unity-method DeNelle.Editor.Regression.HeroDeathSeverityRegression.RunAll
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class HeroDeathSeverityRegression
    {
        private const string HeroHealthPath = "Assets/_Modules/Village/Hero/HeroHealth.cs";
        private const string FlowTracePath  = "Assets/_Modules/Core/Diagnostics/FlowTrace.cs";
        private const string HarnessPath    = "Assets/_Modules/Core/Diagnostics/BreakCaptureHarness.cs";
        private const string DaemonPath     = ".claude/skills/run-defenders/f8-watch-daemon.ps1";

        /// <summary>The exact dump that used to be a Fail. Presence is asserted, not just severity.</summary>
        private const string DeathDumpText = "death freeze armed";

        /// <summary>
        /// Prose that means "this is the game working normally". A FlowTrace.Fail carrying any of
        /// these in HeroHealth is a permanent expected error in the owner's triage stream.
        /// Deliberately NARROW - see the scope note in the header before adding to it.
        /// </summary>
        private static readonly string[] NormalLifecyclePhrases =
        {
            "death freeze armed",
            "death pin rebased",
            "revive",
            "respawn",
        };

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("HERO_DEATH_SEVERITY_OK - " + reason);
            else Debug.LogError("HERO_DEATH_SEVERITY_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                Case(failures, "dump-is-capture",   () => Case1_DumpIsCapture(failures, notes));
                Case(failures, "no-lifecycle-fail", () => Case2_NoLifecycleFail(failures, notes));
                Case(failures, "capture-channel",   () => Case3_CaptureChannel(failures, notes));
                Case(failures, "daemon-skips-note", () => Case4_DaemonSkipsNote(failures, notes));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "HERO DEATH SEVERITY OK - the death-freeze state dump still exists and is a " +
                         "FlowTrace.Capture (kind note), no normal-lifecycle event in HeroHealth uses " +
                         "FlowTrace.Fail, and the F8 daemon skips note rows" + noteStr;
                return true;
            }
            reason = "hero-death-severity FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  CASE 1 - the dump EXISTS and is a Capture, not a Fail
        // =====================================================================
        private static void Case1_DumpIsCapture(List<string> failures, List<string> notes)
        {
            if (!TryReadCode(HeroHealthPath, out string code, failures, "dump-is-capture")) return;

            int dumpAt = IndexOfIgnoreCase(code, DeathDumpText);
            if (dumpAt < 0)
            {
                failures.Add("[dump-is-capture] the death-freeze state dump ('" + DeathDumpText + "') is GONE from " +
                             HeroHealthPath + " - CLAUDE.md sec.12 forbids stripping instrumentation; the fix was to " +
                             "change its SEVERITY (FlowTrace.Capture), never to delete it");
                return;
            }

            string owner = OwningCallName(code, dumpAt);
            if (owner != "Capture")
            {
                failures.Add("[dump-is-capture] the death-freeze dump is emitted by FlowTrace." + owner +
                             " - it must be FlowTrace.Capture. Fail makes every normal hero death an F8 error " +
                             "capture that wakes a live triage seat; Step/Warn/Once never reach break-log.jsonl at all");
                return;
            }
            notes.Add("death dump = FlowTrace.Capture");
        }

        // =====================================================================
        //  CASE 2 - no FlowTrace.Fail in HeroHealth carries normal-lifecycle prose
        // =====================================================================
        private static void Case2_NoLifecycleFail(List<string> failures, List<string> notes)
        {
            if (!TryReadCode(HeroHealthPath, out string code, failures, "no-lifecycle-fail")) return;

            const string token = "FlowTrace.Fail(";
            int failCount = 0;
            int at = 0;
            while ((at = code.IndexOf(token, at, StringComparison.Ordinal)) >= 0)
            {
                failCount++;
                string stmt = StatementAt(code, at);
                foreach (string phrase in NormalLifecyclePhrases)
                {
                    if (IndexOfIgnoreCase(stmt, phrase) < 0) continue;
                    failures.Add("[no-lifecycle-fail] a FlowTrace.Fail in " + HeroHealthPath + " carries the " +
                                 "normal-lifecycle text '" + phrase + "' (near offset " + at + ") - an EXPECTED " +
                                 "event must not raise an error capture. Use FlowTrace.Capture: it still reaches " +
                                 "break-log.jsonl (kind note) but does not wake the F8 triage seat");
                }
                at += token.Length;
            }
            notes.Add(failCount + " FlowTrace.Fail site(s) in HeroHealth, all anomaly-only");
        }

        // =====================================================================
        //  CASE 3 - the Capture channel is really non-error, and really durable
        // =====================================================================
        private static void Case3_CaptureChannel(List<string> failures, List<string> notes)
        {
            if (TryReadCode(FlowTracePath, out string flow, failures, "capture-channel"))
            {
                int at = flow.IndexOf("void Capture(", StringComparison.Ordinal);
                if (at < 0)
                {
                    failures.Add("[capture-channel] FlowTrace.Capture is missing from " + FlowTracePath +
                                 " - without it the only break-log-reaching severity is Fail, which is what " +
                                 "made every hero death an error in the first place");
                }
                else
                {
                    string body = Slice(flow, at, 1200);
                    if (body.IndexOf("Sink.Error", StringComparison.Ordinal) >= 0)
                        failures.Add("[capture-channel] FlowTrace.Capture routes through Sink.Error - that is a Fail " +
                                     "wearing a different name; it must emit at INFO severity");
                    if (body.IndexOf("RecordNote", StringComparison.Ordinal) < 0)
                        failures.Add("[capture-channel] FlowTrace.Capture does not call BreakCaptureHarness.RecordNote - " +
                                     "the dump would then never reach break-log.jsonl and the state is lost for post-hoc reading");
                }
            }

            if (!TryReadCode(HarnessPath, out string harness, failures, "capture-channel")) return;

            int rn = harness.IndexOf("void RecordNote(", StringComparison.Ordinal);
            if (rn < 0)
            {
                failures.Add("[capture-channel] BreakCaptureHarness.RecordNote is missing from " + HarnessPath);
                return;
            }
            string rnBody = Slice(harness, rn, 900);
            if (rnBody.IndexOf("\"note\"", StringComparison.Ordinal) < 0)
                failures.Add("[capture-channel] RecordNote does not record kind \"note\" - the F8 daemon skips rows by " +
                             "that exact kind, so any other kind wakes a triage seat on every hero death");
            if (rnBody.IndexOf("screenshot: false", StringComparison.Ordinal) < 0)
                failures.Add("[capture-channel] RecordNote takes a screenshot - an expected lifecycle note must not " +
                             "burn the harness's bounded screenshot budget that real breaks need");
            notes.Add("Capture -> RecordNote(kind note)");
        }

        // =====================================================================
        //  CASE 4 - the F8 watch daemon does NOT wake on a note row
        // ---------------------------------------------------------------------
        //  This is the half that the owner actually feels. The daemon emits a
        //  capture for EVERY new break-log line whose kind is not in its skip
        //  list, so a "note" row would still ping her triage seat if the two
        //  sides ever drift apart.
        // =====================================================================
        private static void Case4_DaemonSkipsNote(List<string> failures, List<string> notes)
        {
            if (!File.Exists(DaemonPath))
            {
                failures.Add("[daemon-skips-note] F8 watch daemon missing: " + DaemonPath +
                             " - the tree moved; re-point this case (do NOT delete it)");
                return;
            }

            string text;
            try { text = File.ReadAllText(DaemonPath); }
            catch (Exception ex)
            {
                failures.Add("[daemon-skips-note] could not read " + DaemonPath + ": " + ex.GetType().Name);
                return;
            }

            int at = text.IndexOf("$kindSkip", StringComparison.Ordinal);
            if (at < 0)
            {
                failures.Add("[daemon-skips-note] the daemon no longer defines $kindSkip - its capture filter was " +
                             "restructured; re-point this case so note rows are still proven silent");
                return;
            }
            string line = Slice(text, at, 200);
            int eol = line.IndexOf('\n');
            if (eol >= 0) line = line.Substring(0, eol);
            if (line.IndexOf("note", StringComparison.Ordinal) < 0)
            {
                failures.Add("[daemon-skips-note] $kindSkip does not include 'note' (" + line.Trim() + ") - every " +
                             "hero death would wake a live triage seat again, which is the whole defect");
                return;
            }
            notes.Add("daemon $kindSkip includes note");
        }

        // ---------------------------------------------------------------------
        //  helpers
        // ---------------------------------------------------------------------

        private static bool TryReadCode(string path, out string code, List<string> failures, string caseName)
        {
            code = string.Empty;
            if (!File.Exists(path))
            {
                failures.Add("[" + caseName + "] file missing: " + path + " - the tree moved; re-point this suite");
                return false;
            }
            try { code = StripComments(File.ReadAllText(path)); }
            catch (Exception ex)
            {
                failures.Add("[" + caseName + "] could not read " + path + ": " + ex.GetType().Name);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Removes // and /* */ comments while PRESERVING string literals - the literals are the
        /// evidence here (the trace message text), and the comments are noise that would otherwise
        /// match every phrase quoted in a header like this file's own.
        /// </summary>
        private static string StripComments(string src)
        {
            var sb = new StringBuilder(src.Length);
            bool inLine = false, inBlock = false, inStr = false, inChar = false, verbatim = false;
            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                char n = i + 1 < src.Length ? src[i + 1] : '\0';

                if (inLine) { if (c == '\n') { inLine = false; sb.Append(c); } continue; }
                if (inBlock) { if (c == '*' && n == '/') { inBlock = false; i++; } continue; }
                if (inChar)
                {
                    sb.Append(c);
                    if (c == '\\' && i + 1 < src.Length) { sb.Append(n); i++; }
                    else if (c == '\'') inChar = false;
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
                    else
                    {
                        if (c == '\\' && i + 1 < src.Length) { sb.Append(n); i++; }
                        else if (c == '"') inStr = false;
                    }
                    continue;
                }

                if (c == '@' && n == '"') { inStr = true; verbatim = true; sb.Append(c); sb.Append(n); i++; continue; }
                if (c == '"') { inStr = true; verbatim = false; sb.Append(c); continue; }
                if (c == '\'') { inChar = true; sb.Append(c); continue; }
                if (c == '/' && n == '/') { inLine = true; continue; }
                if (c == '/' && n == '*') { inBlock = true; i++; continue; }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// The FlowTrace method name whose call encloses <paramref name="offset"/> - i.e. which
        /// severity emitted this text. Walks BACK to the nearest "FlowTrace." before the offset.
        /// Returns "&lt;none&gt;" when the text is not inside a FlowTrace call at all.
        /// </summary>
        private static string OwningCallName(string code, int offset)
        {
            int at = code.LastIndexOf("FlowTrace.", offset, StringComparison.Ordinal);
            if (at < 0) return "<none>";
            int start = at + "FlowTrace.".Length;
            int end = start;
            while (end < code.Length && (char.IsLetterOrDigit(code[end]) || code[end] == '_')) end++;
            return end > start ? code.Substring(start, end - start) : "<none>";
        }

        /// <summary>The statement starting at <paramref name="at"/>, up to its terminating ';'.</summary>
        private static string StatementAt(string code, int at)
        {
            int end = code.IndexOf(';', at);
            if (end < 0) end = Math.Min(code.Length, at + 2000);
            return code.Substring(at, end - at);
        }

        private static string Slice(string s, int at, int len)
        {
            if (at < 0 || at >= s.Length) return string.Empty;
            return s.Substring(at, Math.Min(len, s.Length - at));
        }

        private static int IndexOfIgnoreCase(string haystack, string needle)
        {
            return haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
        }
    }
}
