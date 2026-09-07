// =============================================================================
// SheatheTraceLatchRegression - WO-1582 (the sheathed-weapon equip trace fills
// the logcat ring).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Shape: public static bool Run(out string reason)
// - registered into DataRegression.RunAll by the implementing lane, per the
// dispatch instruction and this file's fencing rule.
//
// THE DEFECT THIS PINS. EquipmentController.ComputeSheathRotation runs on the
// per-frame ApplyHoldPose path and used to log through FlowTrace.Throttle(..., 5f,
// ...). A TIME throttle on a per-frame site logs forever at its cadence whether or
// not anything moved: the owner's device logcat on 2026-09-07 08:28-08:29 carried
// twelve identical "sheathed long axis on 'Hero (Blaise)': tiltFromVertical=0deg
// longAxisDotUp=1" lines in one minute with no value change. The Android main ring
// is 256 KiB, so that steady drip evicts the boot window and the instrument
// destroys the evidence it exists to preserve (memory:
// logcat-ring-buffer-destroys-evidence).
//
// HONEST SCOPE - AND THIS SUITE IS NOT A LINT ALONE (WO-1494: six suites claimed to
// MEASURE and were source text). Cases 1-5 are a REAL FIXTURE: they swap
// FlowTrace.Sink for a counting sink and drive the SHIPPING emit path
// (EquipmentController.EmitSheatheTraceIfChanged) with their own latch dictionary,
// then COUNT the lines that actually reached the sink. Case 6 is the only source
// lint here, and it says so in its reason string; it exists so a later edit cannot
// quietly put the 5s Throttle back or delete the trace outright (CLAUDE.md sec.12:
// NEVER STRIP FLOWTRACE - the fix was a latch, not a deletion).
//
// WHAT EACH CASE PINS
//   1. [steady]    100 re-equips of the SAME prop at the SAME result emit exactly
//                  ONE line. This is the ticket's acceptance number.
//   2. [changed]   a changed result emits a second line - the trace still reports
//                  every transition, which is MORE evidence than the 5s throttle
//                  gave (a change between two ticks used to be invisible).
//   3. [returned]  a change BACK to the first result emits a third line. This is
//                  why the latch is a last-value dictionary and not FlowTrace.Once:
//                  Once is a HashSet, so A -> B -> A would go unrecorded and a pose
//                  that went wrong and recovered would leave no trace of either move.
//   4. [identity]  a different (hero, prop, socket) identity keeps its OWN row - one
//                  hero's steady pose cannot silence another's first line.
//   5. [loud]      a missing latch emits rather than swallows. A diagnostic whose
//                  de-dupe state is absent must fail LOUD: a silenced trace reads
//                  exactly like a code path that never ran.
//   6. [source]    ComputeSheathRotation's body no longer calls FlowTrace.Throttle
//                  for the sheathe-rot key, still calls EmitSheatheTraceIfChanged,
//                  and still carries the tiltFromVertical measurement text.
//
// WHAT THIS CANNOT PROVE, AND DOES NOT CLAIM (CLAUDE.md sec.11B). It cannot prove the
// device ring now holds the boot window - that is a logcat capture off the owner's
// Seeker. It cannot prove the pose is correct; it never looks at a quaternion.
// =============================================================================
using System.Collections.Generic;
using System.IO;
using System.Text;
using DeNelle.Core.Diagnostics;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class SheatheTraceLatchRegression
    {
        private const string ControllerPath = "Assets/_Modules/Village/Hero/EquipmentController.cs";
        private const string Tag = "[sheathe-trace-latch] ";

        // Declared as a PAIR so this file's own brace tally stays balanced - CLAUDE.md sec.1 runs a
        // naive open-vs-close count over every .cs, and a lone open-brace char literal in the
        // brace-matcher below reads to that gate as a missing close.
        private const char OpenBrace = '{', CloseBrace = '}';

        /// <summary>Counts every line FlowTrace hands to a sink, so the fixture measures the REAL
        /// emit path instead of trusting the helper's return value.</summary>
        private sealed class CountingSink : ITraceSink
        {
            public readonly List<string> Lines = new List<string>();
            public void Info(string line)  { Lines.Add(line); }
            public void Warn(string line)  { Lines.Add(line); }
            public void Error(string line) { Lines.Add(line); }
        }

        public static bool Run(out string reason)
        {
            var notes = new StringBuilder();
            var failures = new List<string>();

            // --- cases 1-5: the behavioural fixture ---------------------------------------
            var sink = new CountingSink();
            ITraceSink previousSink = FlowTrace.Sink;
            bool previousEnabled = FlowTrace.Enabled;
            try
            {
                FlowTrace.Sink = sink;
                FlowTrace.Enabled = true;
                // Category filters cannot be READ back off FlowTrace (Only/Mute are write-only), so
                // this restores the DEFAULT all-on state rather than the caller's. DataRegression
                // runs with the default, and a muted "Equip" here would make every case below count
                // zero and fail for the wrong reason - a fixture must not be able to pass or fail on
                // a filter it cannot see.
                FlowTrace.AllOn();

                var latch = new Dictionary<string, string>();
                const string heroA = "sheathe-rot-main-Hero (Blaise)-sword_A-CC_Base_Spine01";
                const string heroB = "sheathe-rot-off-Hero (Blaise)-knight_shield_starter-CC_Base_Spine01";
                const string sigVertical = "tilt=0|dot=1|sign=-1|src=PER-MESH derived|why=measured";
                const string sigAcross   = "tilt=90|dot=0|sign=-1|src=PER-MESH derived|why=measured";

                // 1. [steady] 100 re-equips of the same prop at the same result.
                for (int i = 0; i < 100; i++)
                    EquipmentController.EmitSheatheTraceIfChanged(latch, heroA, sigVertical,
                        "sheathed long axis on 'Hero (Blaise)': tiltFromVertical=0deg");
                if (sink.Lines.Count != 1)
                    failures.Add(Tag + "FIXTURE FAIL [steady]: 100 identical re-equips emitted " +
                                 sink.Lines.Count + " line(s), expected exactly 1. The per-frame " +
                                 "ApplyHoldPose path is back to flooding the 256 KiB Android ring, " +
                                 "which is the whole of WO-1582.");
                else
                    notes.Append("[steady] 100 identical re-equips -> 1 line. ");

                // 2. [changed] the result moves.
                EquipmentController.EmitSheatheTraceIfChanged(latch, heroA, sigAcross,
                    "sheathed long axis on 'Hero (Blaise)': tiltFromVertical=90deg");
                if (sink.Lines.Count != 2)
                    failures.Add(Tag + "FIXTURE FAIL [changed]: a CHANGED result left the total at " +
                                 sink.Lines.Count + ", expected 2. The latch has silenced the trace " +
                                 "instead of de-duplicating it - a trace that cannot report a change " +
                                 "is stripped instrumentation by another name (CLAUDE.md sec.12).");
                else
                    notes.Append("[changed] a changed result -> a 2nd line. ");

                // 3. [returned] and moves BACK. FlowTrace.Once would stay silent here.
                EquipmentController.EmitSheatheTraceIfChanged(latch, heroA, sigVertical,
                    "sheathed long axis on 'Hero (Blaise)': tiltFromVertical=0deg");
                if (sink.Lines.Count != 3)
                    failures.Add(Tag + "FIXTURE FAIL [returned]: a result that changed BACK to a " +
                                 "previously seen value left the total at " + sink.Lines.Count +
                                 ", expected 3. That is HashSet (FlowTrace.Once) behaviour, not a " +
                                 "last-value latch: a pose that went wrong and recovered would leave " +
                                 "no record of either move.");
                else
                    notes.Append("[returned] a change back -> a 3rd line. ");

                // 4. [identity] a second identity carries its own row.
                EquipmentController.EmitSheatheTraceIfChanged(latch, heroB, sigVertical,
                    "sheathed long axis on 'Hero (Blaise)' off-hand");
                if (sink.Lines.Count != 4)
                    failures.Add(Tag + "FIXTURE FAIL [identity]: a DIFFERENT (hero, prop, socket) " +
                                 "identity left the total at " + sink.Lines.Count + ", expected 4. " +
                                 "The latch is keyed too coarsely - one prop's steady pose is " +
                                 "silencing another prop's first line.");
                else
                    notes.Append("[identity] a 2nd identity -> its own line. ");

                // 5. [loud] no latch means emit, never swallow.
                EquipmentController.EmitSheatheTraceIfChanged(null, heroA, sigVertical, "no-latch probe");
                EquipmentController.EmitSheatheTraceIfChanged(latch, null, sigVertical, "no-identity probe");
                if (sink.Lines.Count != 6)
                    failures.Add(Tag + "FIXTURE FAIL [loud]: a missing latch/identity left the total " +
                                 "at " + sink.Lines.Count + ", expected 6. Missing de-dupe state must " +
                                 "fall back to LOUD - a silenced trace is indistinguishable from a " +
                                 "code path that never ran, which is the ambiguity sec.12 exists to remove.");
                else
                    notes.Append("[loud] a missing latch still emits. ");

                // Every line must still be the tagged FlowTrace shape a capture greps for.
                foreach (string line in sink.Lines)
                {
                    if (line != null && line.Contains("[Flow:Equip]")) continue;
                    failures.Add(Tag + "FIXTURE FAIL [loud]: an emitted line is not [Flow:Equip]-" +
                                 "tagged ('" + (line ?? "<null>") + "'). Every capture, daemon and " +
                                 "grep in this repo keys off that prefix.");
                    break;
                }
            }
            finally
            {
                FlowTrace.Sink = previousSink;
                FlowTrace.Enabled = previousEnabled;
            }

            // --- case 6: the only source lint here, and it says so ------------------------
            string full = Path.Combine(Directory.GetCurrentDirectory(), ControllerPath);
            if (!File.Exists(full))
            {
                reason = Tag + "SOURCE LINT FAIL [source]: " + ControllerPath + " not found - the " +
                         "lint cannot prove the throttle stayed retired, so it fails rather than " +
                         "pass blind.";
                return false;
            }
            string body = ExtractBody(File.ReadAllText(full),
                "private Quaternion ComputeSheathRotation(Transform socket, float sideSign)");
            if (body == null)
            {
                failures.Add(Tag + "SOURCE LINT FAIL [source]: could not brace-match the body of " +
                             "ComputeSheathRotation(Transform, float) in " + ControllerPath +
                             ". The method was renamed or its braces do not close.");
            }
            else
            {
                // STRIP COMMENTS AND STRING LITERALS BEFORE JUDGING, and the reason is this repo's
                // own canon style: a retired call is QUOTED in the comment that retires it (sec.15),
                // so ComputeSheathRotation's body legitimately contains the words
                // "FlowTrace.Throttle" inside the paragraph explaining why it is gone. A raw
                // Contains() would fire on the tombstone and force a seat to delete the very
                // explanation that stops the next one re-adding it.
                string code = StripLiteralsAndComments(body);
                if (code.Contains("FlowTrace.Throttle"))
                    failures.Add(Tag + "SOURCE LINT FAIL [source]: ComputeSheathRotation calls " +
                                 "FlowTrace.Throttle again. A TIME throttle on this per-frame body " +
                                 "logs forever at its cadence whether or not the pose moved - that is " +
                                 "the WO-1582 defect verbatim (12 identical lines/minute on device). " +
                                 "Use the keyed latch (EmitSheatheTraceIfChanged).");
                if (!code.Contains("EmitSheatheTraceIfChanged"))
                    failures.Add(Tag + "SOURCE LINT FAIL [source]: ComputeSheathRotation no longer " +
                                 "calls EmitSheatheTraceIfChanged. The sheathed-pose measurement has " +
                                 "been stripped, which CLAUDE.md sec.12 forbids outright - the WO-1582 " +
                                 "cure was to LATCH the line, never to delete it.");
                if (!code.Contains("tiltFromVertical"))
                    failures.Add(Tag + "SOURCE LINT FAIL [source]: the tiltFromVertical measurement " +
                                 "is gone from ComputeSheathRotation. That number is the one that " +
                                 "answers 'is the sword lying across the body' without a rebuild.");
                if (body != null && !failures.Exists(f => f.Contains("[source]")))
                    notes.Append("[source] the throttle stayed retired and the latched trace is intact. ");
            }

            if (failures.Count > 0)
            {
                reason = string.Join(" | ", failures.ToArray());
                return false;
            }
            // No "<n>/<n>" label here on purpose (WO-1493 / audit G8): a count written as a literal
            // is a claim, not a measurement. The notes below are appended by the cases that actually
            // ran, so what this line reports is what happened.
            reason = Tag + "PASS - " + notes.ToString().Trim() +
                     " (cases 1-5 COUNT lines through the real FlowTrace sink; case 6 is a source " +
                     "lint. Neither proves the device ring holds the boot window - that is a logcat " +
                     "capture off the Seeker.)";
            return true;
        }

        /// <summary>Blank out // and /* */ comments and the CONTENTS of "..." / '...' literals, so a
        /// Contains() judges executable code and never a tombstone comment or a message string.
        /// Single pass, escape-aware, verbatim-string aware. Length is not preserved and does not
        /// need to be - every caller here asks only whether a token is present.</summary>
        private static string StripLiteralsAndComments(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;
            var outp = new StringBuilder(src.Length);
            int i = 0;
            while (i < src.Length)
            {
                char c = src[i];
                char next = i + 1 < src.Length ? src[i + 1] : '\0';

                if (c == '/' && next == '/')
                {
                    while (i < src.Length && src[i] != '\n') i++;
                    continue;
                }
                if (c == '/' && next == '*')
                {
                    i += 2;
                    while (i + 1 < src.Length && !(src[i] == '*' && src[i + 1] == '/')) i++;
                    i = i + 2 <= src.Length ? i + 2 : src.Length;
                    continue;
                }
                if (c == '@' && next == '"')
                {
                    i += 2;                                     // verbatim: "" is an escaped quote
                    while (i < src.Length)
                    {
                        if (src[i] == '"')
                        {
                            if (i + 1 < src.Length && src[i + 1] == '"') { i += 2; continue; }
                            i++; break;
                        }
                        i++;
                    }
                    continue;
                }
                if (c == '"' || c == '\'')
                {
                    char quote = c;
                    i++;
                    while (i < src.Length)
                    {
                        if (src[i] == '\\') { i += 2; continue; }
                        if (src[i] == quote) { i++; break; }
                        i++;
                    }
                    continue;
                }
                outp.Append(c);
                i++;
            }
            return outp.ToString();
        }

        /// <summary>Brace-match the body that follows <paramref name="signature"/>. Returns null when
        /// the signature is absent or the braces never close.</summary>
        private static string ExtractBody(string src, string signature)
        {
            if (string.IsNullOrEmpty(src)) return null;
            int at = src.IndexOf(signature, System.StringComparison.Ordinal);
            if (at < 0) return null;
            int open = src.IndexOf(OpenBrace, at + signature.Length);
            if (open < 0) return null;
            int depth = 0;
            for (int i = open; i < src.Length; i++)
            {
                char c = src[i];
                if (c == OpenBrace) depth++;
                else if (c == CloseBrace)
                {
                    depth--;
                    if (depth == 0) return src.Substring(open, i - open + 1);
                }
            }
            return null;
        }
    }
}
