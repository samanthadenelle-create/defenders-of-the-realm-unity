// =============================================================================
// StackTraceLogTypeRegression [stacktrace-logtype]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.
// Markers: STACKTRACE_LOGTYPE_OK / STACKTRACE_LOGTYPE_FAIL.
//
// PERF D3 (docs/reference/READY_SILOS_2026-09-04.md line 106; measured in
// docs/reference/SEEKER_PERFORMANCE_AUDIT_2026-09-04.md rows A16/A17 and the
// configuration-audit row ":59 m_StackTraceTypes: 0100...").
//
// THE MEASURED DEFECT. On the Seeker, steady play logged 257 logcat lines/s with
// ~35 stack-traced Debug.Log calls/s, and the town-load second held 13,956 lines.
// Every one of those calls walked a MANAGED STACK and crossed JNI, because
// ProjectSettings.asset:59 set ScriptOnly for all six log types - Log and Warning
// included. Log and Warning are the two HIGH-VOLUME types in this project (FlowTrace
// alone routes its Info and Warn through Debug.Log / Debug.LogWarning), and their
// stack traces are never the thing anyone reads: the [Flow:<system>] tag and the
// FlowTrace call ordering already say where the line came from.
//
// The fix is a data change, not a code change: Log and Warning go to None (0), while
// Error, Assert and Exception KEEP ScriptOnly (1) - those are the ones where a stack
// is the whole value of the line, and their volume is a rounding error.
//
// ⛔ WHAT THIS IS NOT. This is NOT stripping instrumentation (CLAUDE.md sec.12, BINDING).
// Not one FlowTrace call is removed or silenced. Every [Flow:*] line still prints, in
// full, at the same place; only the appended stack WALK is dropped for the two chatty
// types. Fail and the exception path are untouched, so a logged failure still carries
// its stack. If a future seat needs Log stacks back for a session, that is an EDITOR
// choice at the console, not a shipped setting.
//
// WHY AN ORACLE AT ALL, for a single hex string:
//   * ProjectSettings.asset is REWRITTEN WHOLESALE by the editor. Anyone who opens
//     Player Settings and ticks a stack-trace box in the console, or any tooling that
//     round-trips PlayerSettings, silently restores ScriptOnly - and the regression is
//     INVISIBLE. Nothing crashes, nothing looks different on screen, no marker goes
//     red; the build just pays for a stack walk on every log line again. That is the
//     CLAUDE.md sec.16 signature: silent, green and wrong, and it is exactly the shape
//     that put this row on the perf audit in the first place.
//   * It is decidable from TEXT - no play mode, no device, no build - so it can be
//     trusted to run in the same batch it guards.
//
//   CASE 1 [field-present]  The m_StackTraceTypes line EXISTS in ProjectSettings.asset
//     and parses as SIX 8-hex-digit little-endian int32 values. Finding no line, or a
//     line of an unexpected width, is a hard FAIL and never a quiet pass: a scan that
//     found nothing to assert must not read as an assertion that passed (WO-1138).
//
//   CASE 2 [chatty-types-none]  Index 2 (Warning) and index 3 (Log) are None (0).
//     This is the case the ticket exists for.
//
//   CASE 3 [diagnostic-types-kept]  Index 0 (Error), index 1 (Assert) and index 4
//     (Exception) are still ScriptOnly (1). Without this, "turn the stacks off" could
//     be satisfied by turning ALL of them off - which would delete the stack from the
//     three lines where it is the entire diagnostic value, and would do it while this
//     suite reported a pass. An oracle that only checks the direction of a change
//     cannot see the over-correction.
//
// The index order is Unity's LogType order: Error, Assert, Warning, Log, Exception,
// plus a sixth trailing slot the serializer carries; the sixth is asserted only to be
// a well-formed value, never to a particular meaning, because nothing here reads it.
//
// Standalone: run-unity-method
//   -Method DeNelle.Editor.Regression.StackTraceLogTypeRegression.RunAll
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class StackTraceLogTypeRegression
    {
        private const string SettingsRel = "ProjectSettings/ProjectSettings.asset";

        // The YAML key, split so that this file's own text can never be mistaken for
        // the asset under test by any other source-scanning oracle.
        private const string FieldKey = "m_Stack" + "TraceTypes";

        private const int ExpectedValueCount = 6;
        private const int HexDigitsPerValue  = 8;

        private const int IdxError     = 0;
        private const int IdxAssert    = 1;
        private const int IdxWarning   = 2;
        private const int IdxLog       = 3;
        private const int IdxException = 4;

        private const int None       = 0;   // StackTraceLogType.None
        private const int ScriptOnly = 1;   // StackTraceLogType.ScriptOnly

        public static void RunAll()
        {
            bool ok = Run(out string reason);
            if (ok) Debug.Log("STACKTRACE_LOGTYPE_OK\n" + reason);
            else    Debug.LogError("STACKTRACE_LOGTYPE_FAIL\n" + reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes    = new List<string>();
            var values   = new List<int>();

            DeNelle.Core.Diagnostics.Guard.Try("Regression", "stacktrace-logtype case 1",
                () => Case1_FieldPresentAndWellFormed(values, failures, notes));
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "stacktrace-logtype case 2",
                () => Case2_ChattyTypesAreNone(values, failures, notes));
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "stacktrace-logtype case 3",
                () => Case3_DiagnosticTypesKeepScriptOnly(values, failures, notes));

            if (failures.Count == 0)
            {
                reason = string.Join("; ", notes);
                return true;
            }

            var sb = new StringBuilder();
            sb.Append(failures.Count).Append(" failure(s):");
            foreach (string f in failures) sb.Append("\n  - ").Append(f);
            if (notes.Count > 0) sb.Append("\n  (context: ").Append(string.Join("; ", notes)).Append(')');
            reason = sb.ToString();
            return false;
        }

        // =====================================================================
        //  CASE 1 - the field exists and parses as six int32 values
        // =====================================================================
        private static void Case1_FieldPresentAndWellFormed(List<int> values, List<string> failures, List<string> notes)
        {
            string full = FullPath(SettingsRel);
            if (!File.Exists(full))
            {
                failures.Add("missing file: " + SettingsRel + " - the scan has nothing to read, which is a " +
                             "broken oracle, not a clean tree.");
                return;
            }

            string[] lines;
            try { lines = File.ReadAllLines(full); }
            catch (Exception e)
            {
                failures.Add("could not read " + SettingsRel + ": " + e.GetType().Name + ": " + e.Message);
                return;
            }

            string hex = null;
            int lineNo = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();
                if (!trimmed.StartsWith(FieldKey + ":", StringComparison.Ordinal)) continue;
                hex    = trimmed.Substring(FieldKey.Length + 1).Trim();
                lineNo = i + 1;
                break;
            }

            if (hex == null)
            {
                failures.Add("no '" + FieldKey + ":' line in " + SettingsRel + ". Either the key was renamed by a " +
                             "Unity upgrade or the file moved; in both cases this suite is now asserting nothing " +
                             "while reporting a pass. Fix the scan, do not relax it.");
                return;
            }

            int expectedLength = ExpectedValueCount * HexDigitsPerValue;
            if (hex.Length != expectedLength)
            {
                failures.Add(SettingsRel + ":" + lineNo + " - " + FieldKey + " is " + hex.Length + " hex digits, " +
                             "expected " + expectedLength + " (" + ExpectedValueCount + " little-endian int32 " +
                             "values). Value read: '" + hex + "'. The packed layout changed; re-derive the " +
                             "indices before trusting cases 2 and 3.");
                return;
            }

            for (int v = 0; v < ExpectedValueCount; v++)
            {
                string word = hex.Substring(v * HexDigitsPerValue, HexDigitsPerValue);
                if (!TryParseLittleEndianInt32(word, out int parsed))
                {
                    failures.Add(SettingsRel + ":" + lineNo + " - value " + v + " ('" + word + "') is not " +
                                 "8 hex digits of a little-endian int32.");
                    return;
                }
                values.Add(parsed);
            }

            notes.Add("[case1] " + SettingsRel + ":" + lineNo + " " + FieldKey + " = [" +
                      string.Join(",", values.ConvertAll(x => x.ToString(CultureInfo.InvariantCulture)).ToArray()) + "]");
        }

        // =====================================================================
        //  CASE 2 - Warning and Log must not walk a stack
        // =====================================================================
        private static void Case2_ChattyTypesAreNone(List<int> values, List<string> failures, List<string> notes)
        {
            if (values.Count != ExpectedValueCount) return;   // CASE 1 already failed loudly

            CheckIs(values, IdxWarning, "Warning", None, "None",
                    "a stack walk on every Debug.LogWarning - the audit measured ~35 stack-traced calls/s in " +
                    "steady play and 13,956 log lines in the town-load second",
                    failures);
            CheckIs(values, IdxLog, "Log", None, "None",
                    "a stack walk on every Debug.Log, which is the single highest-volume log type in this " +
                    "project (every FlowTrace.Step/Info routes through it)",
                    failures);

            if (failures.Count == 0)
                notes.Add("[case2] Warning=None Log=None - no managed stack walk on the two chatty types");
        }

        // =====================================================================
        //  CASE 3 - Error, Assert and Exception must KEEP their stacks
        // =====================================================================
        private static void Case3_DiagnosticTypesKeepScriptOnly(List<int> values, List<string> failures, List<string> notes)
        {
            if (values.Count != ExpectedValueCount) return;   // CASE 1 already failed loudly

            int before = failures.Count;

            CheckIs(values, IdxError, "Error", ScriptOnly, "ScriptOnly",
                    "an error line with no stack - the diagnostic value of Debug.LogError is almost entirely " +
                    "the stack, and FlowTrace.Fail is emitted on this channel",
                    failures);
            CheckIs(values, IdxAssert, "Assert", ScriptOnly, "ScriptOnly",
                    "an assertion with no stack, which cannot be located",
                    failures);
            CheckIs(values, IdxException, "Exception", ScriptOnly, "ScriptOnly",
                    "an unhandled exception with no stack - the one line where the stack IS the report",
                    failures);

            if (failures.Count == before)
                notes.Add("[case3] Error/Assert/Exception=ScriptOnly - the diagnostic channels keep their stacks");
        }

        // =====================================================================
        //  helpers
        // =====================================================================
        private static void CheckIs(List<int> values, int index, string typeName, int expected, string expectedName,
                                    string consequence, List<string> failures)
        {
            int actual = values[index];
            if (actual == expected) return;
            failures.Add(FieldKey + " index " + index + " (" + typeName + ") is " + actual + ", expected " +
                         expected + " (" + expectedName + "). Consequence: " + consequence + ". Fix it in " +
                         SettingsRel + " - and if the editor rewrote this file, that is the regression this " +
                         "suite exists to catch, not a reason to change the expectation.");
        }

        private static bool TryParseLittleEndianInt32(string word, out int value)
        {
            value = 0;
            if (word == null || word.Length != HexDigitsPerValue) return false;

            int result = 0;
            for (int b = 3; b >= 0; b--)
            {
                string pair = word.Substring(b * 2, 2);
                if (!int.TryParse(pair, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int octet))
                    return false;
                result = (result << 8) | octet;
            }
            value = result;
            return true;
        }

        private static string FullPath(string rel) =>
            Path.Combine(Directory.GetCurrentDirectory(), rel.Replace('/', Path.DirectorySeparatorChar));
    }
}
