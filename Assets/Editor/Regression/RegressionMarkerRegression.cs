// =============================================================================
// RegressionMarkerRegression [regression-marker]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.   Markers: REGRESSION_MARKER_OK / _FAIL.
//
// THE ORACLE THAT KILLS A WHOLE CLASS OF INVISIBLE GATE FAILURE.
//
// Project law is "judge by the MARKER, never the exit code" (memory
// `gates-report-success-without-proving-it`). That law only holds while a marker
// identifies WHICH suite produced it and while the thing the gate runs is the
// thing the gate claims to run. On 2026-08-02 both halves were broken at once:
//
//   * THREE classes emitted the identical bare literal `REGRESSION_OK` -
//     DataRegression.RunAll (the real gate, ~90 registered oracle suites),
//     SessionRegression.RunAll (6 checks), and Assets/Editor/RegressionSuite.cs
//     (22 cases). A log containing REGRESSION_OK did not say which one ran.
//   * tools/regression/checkin_gate.ps1 invoked the 22-case LEGACY suite and
//     judged it by that shared marker, so every "REGRESSION_OK" a RESULT file
//     cited from the check-in path was the SMALL suite's pass. Roughly 64 oracle
//     suites had never run in the automated check-in path at all.
//   * Several suite files existed on disk with a full Run(out reason) contract
//     and were never registered anywhere - a file that never runs.
//
// This suite asserts the invariants that make those three states impossible,
// by SCANNING SOURCE under Assets/Editor (no scene, no play mode, no runtime
// singletons - it is decidable from text, which is why it can be trusted to run
// in the same batch it is guarding):
//
//   RULE 1  [marker-uniqueness]  No two distinct ORACLE files emit the same
//           `*_OK` marker literal in live code. Scoped to oracle files
//           (*Regression.cs / *Oracle.cs / *Audit.cs + RegressionSuite.cs) so
//           scene builders that happen to share a NAV_OK token are not dragged in.
//           KnownDuplicateMarkers is a NAMED, SHRINKING allowlist for pre-existing
//           debt - never a place to park a new collision.
//
//   RULE 2  [registration]  Every file under Assets/Editor/Regression that
//           exposes `public static bool Run(out string <name>)` is referenced in
//           DataRegression.RunAll. An unregistered oracle is a file that never
//           runs. A suite may opt out ONLY by saying so in its own header (see
//           StandaloneOptOutTokens) - the way RepairProbeRegression does.
//
//   RULE 3  [gate-grep]  Every marker literal a gate .ps1 actually greps for
//           (Select-String / -Pattern lines under tools/ and .claude/skills/)
//           is emitted by exactly ONE class under Assets/Editor. Zero owners =
//           the gate can never pass. Two owners = the gate cannot tell which
//           suite passed, which is the 2026-08-02 bug itself.
//
//   RULE 4  [hollow-pass ratchet]  A registered suite must be ABLE to go red,
//           and must not answer OK out of a null/missing guard without having
//           asserted anything ("no-op and report OK"). Many of the new suites
//           need runtime state (GameStateService.Instance, GearLoadout.Current,
//           the economy ledger) that is NULL in editor batchmode; the tempting
//           shape is `if (x == null) { reason = "skipped"; return true; }`, which
//           green-passes forever and defeats the oracle. Pre-existing named skips
//           are baselined in KnownHollowPassFiles; this rule is a RATCHET - it
//           fails on a NEW one only. Legit cases opt out with `hollow-pass-ok`.
//
// Self-reference: this file is EXCLUDED from the RULE 1 emitter scan (it names
// other suites' markers in its own allowlists, which would read as emitting them)
// and is subject to RULE 2 like every other suite - its own registration line in
// DataRegression.RunAll is what satisfies it.
//
// Standalone: run-unity-method.ps1
//   -Method DeNelle.Editor.Regression.RegressionMarkerRegression.RunStandalone
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class RegressionMarkerRegression
    {
        // ---------------------------------------------------------------------
        //  Scan scope
        // ---------------------------------------------------------------------
        private const string SelfFileName = "RegressionMarkerRegression.cs";
        private const string RegistryFileName = "DataRegression.cs";

        // Declared as a balanced PAIR on one line on purpose: a lone opening-brace char
        // literal trips the CLAUDE.md rule-1 naive brace counter + the CompileGate scan.
        private const char OpenBrace = '{', CloseBrace = '}';

        // Gate-script roots (relative to the project root). node_modules is skipped.
        private static readonly string[] GateScriptRoots = { "tools", ".claude/skills" };

        // A file counts as an ORACLE (RULE 1 scope) by name.
        private static bool IsOracleFile(string fileName)
        {
            if (string.Equals(fileName, SelfFileName, StringComparison.OrdinalIgnoreCase)) return false;
            if (string.Equals(fileName, "RegressionSuite.cs", StringComparison.OrdinalIgnoreCase)) return true;
            return fileName.EndsWith("Regression.cs", StringComparison.Ordinal)
                || fileName.EndsWith("Oracle.cs", StringComparison.Ordinal)
                || fileName.EndsWith("Audit.cs", StringComparison.Ordinal);
        }

        // ---------------------------------------------------------------------
        //  RULE 1 allowlist - PRE-EXISTING duplicate marker literals.
        //  SHRINK THIS LIST. Do not grow it. Each entry names live debt.
        // ---------------------------------------------------------------------
        //  DUNGEON_EXIT_OK - DungeonExitRegression.cs and
        //  DungeonExitReachableRegression.cs both emit it. They are two different
        //  suites (one asserts the exit exists, one asserts it is reachable) and
        //  both are registered in DataRegression.RunAll. Their DataRegression tags
        //  collided too ("[dungeon-exit]" twice) - that half was fixed 2026-08-02
        //  (the second is now "[dungeon-exit-reachable]"); renaming the marker
        //  literal itself means editing those suite bodies and is owed.
        private static readonly HashSet<string> KnownDuplicateMarkers = new HashSet<string>(StringComparer.Ordinal)
        {
            "DUNGEON_EXIT_OK",
        };

        // ---------------------------------------------------------------------
        //  RULE 2 opt-out - a suite that DECLARES itself standalone in its header.
        // ---------------------------------------------------------------------
        private static readonly string[] StandaloneOptOutTokens =
        {
            "NOT wired into DataRegression",     // RepairProbeRegression's declaration
            "regression-registry: standalone",   // explicit opt-out token for new files
        };

        // ---------------------------------------------------------------------
        //  RULE 4 baseline - files that ALREADY answer OK out of a guard.
        //  RATCHET: a NEW file doing this fails. These are owed cleanups.
        // ---------------------------------------------------------------------
        //  HeroLocomotionClipRegression       - "motion-castings.json missing - skip"
        //  OfflineHarvestRegression           - "skipped: needs fleet" (documented NAMED SKIP)
        //  VillageEconomyRegression           - "skipped: needs fleet"
        //  ModalArbiterRegistrationRegression - "SKIPPED -- Assets/_Modules not found"
        //  UiMvvmConformanceRegression        - "SKIPPED -- Assets/_Modules not found"
        //  UiObsidianConformanceRegression    - "SKIPPED -- Assets/_Modules not found"
        // The last three are source-lints over Assets/_Modules; that directory cannot be
        // absent in this project, so the skip is unreachable rather than dangerous - but it
        // is the same SHAPE, so it is baselined rather than excused.
        private static readonly HashSet<string> KnownHollowPassFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "HeroLocomotionClipRegression.cs",
            "OfflineHarvestRegression.cs",
            "VillageEconomyRegression.cs",
            "ModalArbiterRegistrationRegression.cs",
            "UiMvvmConformanceRegression.cs",
            "UiObsidianConformanceRegression.cs",
        };

        private const string HollowPassOptOut = "hollow-pass-ok";

        // ---------------------------------------------------------------------
        //  Regexes
        // ---------------------------------------------------------------------
        // An _OK marker literal appearing inside a string literal in live code.
        private static readonly Regex MarkerInLiteral = new Regex(
            "\"[^\"\\n]*?\\b([A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*_OK)\\b", RegexOptions.Compiled);

        // A marker token anywhere in a PowerShell grep line.
        private static readonly Regex MarkerToken = new Regex(
            "\\b([A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*_(?:OK|FAIL))\\b", RegexOptions.Compiled);

        private static readonly Regex RunSignature = new Regex(
            "public\\s+static\\s+bool\\s+Run\\s*\\(\\s*out\\s+string\\s+\\w+\\s*\\)", RegexOptions.Compiled);

        private static readonly Regex ClassDecl = new Regex(
            "\\b(?:public|internal)\\s+(?:static\\s+|sealed\\s+|partial\\s+)*class\\s+(\\w+)", RegexOptions.Compiled);

        // =====================================================================
        //  Entry points
        // =====================================================================

        /// <summary>Standalone batch entry - prints REGRESSION_MARKER_OK / _FAIL.</summary>
        public static void RunStandalone()
        {
            if (Run(out string reason)) Debug.Log("REGRESSION_MARKER_OK - " + reason);
            else Debug.LogError("REGRESSION_MARKER_FAIL - " + reason);
        }

        /// <summary>DataRegression-shaped contract. NEVER throws.</summary>
        public static bool Run(out string reason)
        {
            try
            {
                return RunCore(out reason);
            }
            catch (Exception ex)
            {
                reason = "REGRESSION MARKER: oracle threw " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        // =====================================================================
        //  Body
        // =====================================================================
        private static bool RunCore(out string reason)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(projectRoot))
            {
                reason = "REGRESSION MARKER: could not resolve the project root from Application.dataPath";
                return false;
            }

            string editorDir = Path.Combine(projectRoot, "Assets", "Editor");
            string regressionDir = Path.Combine(editorDir, "Regression");
            if (!Directory.Exists(editorDir) || !Directory.Exists(regressionDir))
            {
                reason = "REGRESSION MARKER: Assets/Editor or Assets/Editor/Regression is missing - cannot verify";
                return false;
            }

            string registryPath = Path.Combine(regressionDir, RegistryFileName);
            if (!File.Exists(registryPath))
            {
                reason = "REGRESSION MARKER: " + RegistryFileName + " not found - the suite registry is gone";
                return false;
            }
            string registryBody = ExtractRunAllBody(ReadOrEmpty(registryPath));
            if (string.IsNullOrEmpty(registryBody))
            {
                reason = "REGRESSION MARKER: could not locate DataRegression.RunAll's body - registration cannot be verified";
                return false;
            }

            var failures = new List<string>();

            // Every .cs under Assets/Editor, with comments stripped once.
            var allEditorFiles = Directory.GetFiles(editorDir, "*.cs", SearchOption.AllDirectories);
            var codeByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var rawByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in allEditorFiles)
            {
                string raw = ReadOrEmpty(p);
                rawByPath[p] = raw;
                codeByPath[p] = StripLineComments(raw);
            }

            // -----------------------------------------------------------------
            //  RULE 1 - marker uniqueness across oracle files
            // -----------------------------------------------------------------
            var markerOwners = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            var allMarkerOwners = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            int oracleFileCount = 0;
            foreach (var kv in codeByPath)
            {
                string name = Path.GetFileName(kv.Key);
                bool isSelf = string.Equals(name, SelfFileName, StringComparison.OrdinalIgnoreCase);
                bool isOracle = IsOracleFile(name);
                if (isOracle) oracleFileCount++;

                foreach (Match m in MarkerInLiteral.Matches(kv.Value))
                {
                    string marker = m.Groups[1].Value;
                    if (!isSelf) Add(allMarkerOwners, marker, name);
                    if (isOracle) Add(markerOwners, marker, name);
                }
            }

            foreach (var kv in markerOwners)
            {
                if (kv.Value.Count < 2) continue;
                if (KnownDuplicateMarkers.Contains(kv.Key)) continue;
                failures.Add("marker '" + kv.Key + "' is emitted by " + kv.Value.Count +
                             " distinct oracle files (" + string.Join(", ", kv.Value.ToArray()) +
                             ") - a log carrying it cannot say WHICH suite passed. Give each a distinct marker.");
            }

            // -----------------------------------------------------------------
            //  RULE 2 - every Run(out string) oracle is registered
            //  RULE 4 - and can actually go red / does not answer OK from a guard
            // -----------------------------------------------------------------
            int registered = 0, optedOut = 0, hollowBaseline = 0;
            var newHollow = new List<string>();
            foreach (var p in Directory.GetFiles(regressionDir, "*.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(p);
                string code = codeByPath.ContainsKey(p) ? codeByPath[p] : StripLineComments(ReadOrEmpty(p));
                string raw = rawByPath.ContainsKey(p) ? rawByPath[p] : ReadOrEmpty(p);
                if (!RunSignature.IsMatch(code)) continue;

                // Which class in this file owns the Run(out string) entry point?
                foreach (string cls in ClassesWithRunEntryPoint(code))
                {
                    bool inRegistry = Regex.IsMatch(registryBody, "\\b" + Regex.Escape(cls) + "\\.Run\\s*\\(\\s*out\\b");
                    if (inRegistry) { registered++; continue; }
                    if (StandaloneOptOutTokens.Any(t => raw.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0))
                    { optedOut++; continue; }
                    failures.Add("oracle '" + cls + "' (" + name + ") exposes Run(out string) but is NOT referenced in " +
                                 "DataRegression.RunAll - an unregistered oracle is a file that never runs. " +
                                 "Register it, or declare 'regression-registry: standalone' in its header.");
                }

                // RULE 4a - can it go red at all?
                bool canFail = code.Contains("return false")
                            || Regex.IsMatch(code, "return\\s+\\w+\\.Count\\s*==\\s*0")
                            || Regex.IsMatch(code, "return\\s+\\w+\\s*\\.\\s*Count\\s*==\\s*0");
                bool optedOutHollow = raw.IndexOf(HollowPassOptOut, StringComparison.OrdinalIgnoreCase) >= 0;
                if (!canFail && !optedOutHollow && !KnownHollowPassFiles.Contains(name))
                    newHollow.Add(name + " has no failing path at all (no 'return false', no 'return <list>.Count == 0') - it can only ever report OK");

                // RULE 4b - does it answer OK straight out of a null/missing guard?
                var hollowLines = FindHollowPassLines(code);
                if (hollowLines.Count > 0)
                {
                    if (KnownHollowPassFiles.Contains(name)) hollowBaseline++;
                    else if (!optedOutHollow)
                        newHollow.Add(name + " returns TRUE out of a null/missing guard (line " + hollowLines[0] +
                                      ") - a suite that green-passes on a null singleton asserts nothing. " +
                                      "Fail (or make the state install), or mark the line '" + HollowPassOptOut + "'.");
                }
            }
            foreach (var h in newHollow)
                failures.Add("hollow pass: " + h);

            // -----------------------------------------------------------------
            //  RULE 3 - gate scripts grep a marker somebody emits, unambiguously
            // -----------------------------------------------------------------
            int gateGreps = 0;
            foreach (var root in GateScriptRoots)
            {
                string dir = Path.Combine(projectRoot, root.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(dir)) continue;
                foreach (var ps1 in Directory.GetFiles(dir, "*.ps1", SearchOption.AllDirectories))
                {
                    if (ps1.Replace('\\', '/').IndexOf("/node_modules/", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    string scriptName = Path.GetFileName(ps1);
                    foreach (var line in ReadOrEmpty(ps1).Split('\n'))
                    {
                        string l = line.Trim();
                        if (l.StartsWith("#")) continue;
                        if (l.IndexOf("Select-String", StringComparison.OrdinalIgnoreCase) < 0 &&
                            l.IndexOf("-Pattern", StringComparison.OrdinalIgnoreCase) < 0) continue;

                        foreach (Match m in MarkerToken.Matches(l))
                        {
                            string token = m.Groups[1].Value;
                            if (token.EndsWith("_FAIL", StringComparison.Ordinal)) continue;  // FAIL greps are diagnostics
                            gateGreps++;
                            if (!allMarkerOwners.ContainsKey(token))
                            {
                                failures.Add(scriptName + " greps for marker '" + token +
                                             "' but NO class under Assets/Editor emits it - that gate stage can never pass.");
                                continue;
                            }
                            var owners = allMarkerOwners[token];
                            if (owners.Count > 1 && !KnownDuplicateMarkers.Contains(token))
                                failures.Add(scriptName + " greps for marker '" + token + "' which " + owners.Count +
                                             " different files emit (" + string.Join(", ", owners.ToArray()) +
                                             ") - the gate cannot tell which suite it just judged.");
                        }
                    }
                }
            }

            if (failures.Count > 0)
            {
                reason = "REGRESSION MARKER FAIL (" + failures.Count + "): " + string.Join(" | ", failures.ToArray());
                return false;
            }

            reason = "REGRESSION MARKER OK -- " + oracleFileCount + " oracle files, " + markerOwners.Count +
                     " distinct _OK markers (0 undeclared collisions), " + registered +
                     " Run(out) oracles registered in DataRegression.RunAll (" + optedOut +
                     " declared standalone), " + gateGreps +
                     " gate-script marker grep(s) all resolve to exactly one emitter; hollow-pass ratchet " +
                     hollowBaseline + " baselined / 0 new";
            return true;
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        private static void Add(Dictionary<string, SortedSet<string>> map, string key, string value)
        {
            SortedSet<string> set;
            if (!map.TryGetValue(key, out set)) { set = new SortedSet<string>(StringComparer.Ordinal); map[key] = set; }
            set.Add(value);
        }

        private static string ReadOrEmpty(string path)
        {
            try { return File.ReadAllText(path); }
            catch (IOException) { return string.Empty; }
            catch (UnauthorizedAccessException) { return string.Empty; }
        }

        /// <summary>Strips // line comments (string-literal aware enough for this scan).</summary>
        private static string StripLineComments(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;
            var sb = new System.Text.StringBuilder(src.Length);
            bool inStr = false, esc = false;
            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                char n = i + 1 < src.Length ? src[i + 1] : '\0';
                if (inStr)
                {
                    sb.Append(c);
                    if (esc) esc = false;
                    else if (c == '\\') esc = true;
                    else if (c == '"') inStr = false;
                    continue;
                }
                if (c == '/' && n == '/')
                {
                    while (i < src.Length && src[i] != '\n') i++;
                    if (i < src.Length) sb.Append('\n');
                    continue;
                }
                if (c == '"') { inStr = true; sb.Append(c); continue; }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>The brace-matched body of DataRegression.RunAll (the suite registry).</summary>
        // =====================================================================
        //  EXPECTED REGISTERED-SUITE COUNT  (audit finding G1)
        // =====================================================================
        // THE HOLE THIS CLOSES. DataRegression computes its headline number as
        //   suitesTotal = suitesGreen + suitesRed
        // where green counts "[tag]" lines in the log and red counts entries in
        // `failures`. 78 of the ~130 suites are registered inside Guard.Try(...)
        // with the return value DISCARDED. A suite that THROWS therefore appends
        // no [tag] line and adds no failure - it silently LEAVES THE DENOMINATOR,
        // and the marker still reads green at a smaller number
        // ("REGRESSION_OK 125/125 suites"). Nothing anywhere pinned the count, so
        // a vanished suite was indistinguishable from a suite that never existed.
        //
        // WHY THIS IS DERIVED AND NOT A LITERAL. Writing `const int Expected = 130`
        // would BE the defect it is meant to catch - the same shape as
        // SessionRegression's hardcoded "SESSION_GUARDS_OK 6/6 checks" (audit G8),
        // a count that is a LABEL rather than a measurement. So both sides are
        // measured: the expected count is counted from the SOURCE registration
        // call-sites between DataRegression's own START/END fences, and compared
        // against the count the RUN actually produced. Adding a suite moves both
        // numbers together and needs no edit here; a suite disappearing at runtime
        // moves only one, which is exactly the event we want to be loud.
        //
        // Counting rule: occurrences of ".Run(out ..." inside the fenced region of
        // RunAll's body, with line comments stripped first so a commented-out or
        // documented registration cannot inflate the expectation.
        public static bool TryGetExpectedSuiteCount(out int expected, out string detail)
        {
            expected = -1;
            detail = string.Empty;
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string registryPath = Path.Combine(projectRoot, "Assets", "Editor", "Regression", RegistryFileName);
                if (!File.Exists(registryPath))
                {
                    detail = RegistryFileName + " not found at " + registryPath;
                    return false;
                }

                string body = ExtractRunAllBody(ReadOrEmpty(registryPath));
                if (string.IsNullOrEmpty(body))
                {
                    detail = "could not locate DataRegression.RunAll's body";
                    return false;
                }

                // ORDER MATTERS, and getting it wrong is a self-inflicted blind spot:
                // the fence markers THEMSELVES live inside `//` comments, so stripping
                // comments first deletes the very landmarks used to find the region.
                // Locate the fences in the RAW body, slice, and only THEN strip comments
                // from inside the slice (so a commented-out registration cannot inflate
                // the expectation).
                // Anchor on the "<<<" suffix, NOT the bare words. The fence block's own
                // instructional comment says "ADD NEW SUITE REGISTRATIONS ABOVE THE END
                // FENCE, NOT BELOW IT", so a bare IndexOf("END FENCE") matches that prose
                // ~12 lines in and slices a window containing no registrations at all -
                // which then reads as "0 call-sites" and looks like a broken regex rather
                // than a mis-anchored search. Only the real markers carry "<<<".
                // The markers also contain a non-ASCII em dash, so neither anchor spans it.
                int start = body.IndexOf("START FENCE <<<", StringComparison.Ordinal);
                int end = body.IndexOf("END FENCE <<<", StringComparison.Ordinal);
                if (start < 0 || end < 0 || end <= start)
                {
                    detail = "START/END FENCE markers not found in RunAll's body (or out of order) - " +
                             "the fenced registry region is what makes the count derivable";
                    return false;
                }

                string fenced = StripLineComments(body.Substring(start, end - start));
                expected = RunSiteInFence.Matches(fenced).Count;
                detail = "counted " + expected + " registration call-site(s) between the fences";
                return expected > 0;
            }
            catch (Exception ex)
            {
                detail = "threw " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        /// <summary>A registration call-site: `Something.Run(out var r)` / `Run(out r)`.</summary>
        private static readonly Regex RunSiteInFence = new Regex(
            @"\.Run\s*\(\s*out\s+(?:var\s+)?\w+\s*\)", RegexOptions.Compiled);

        private static string ExtractRunAllBody(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;
            int sig = src.IndexOf("public static void RunAll()", StringComparison.Ordinal);
            if (sig < 0) return string.Empty;
            int open = src.IndexOf(OpenBrace, sig);
            if (open < 0) return string.Empty;
            int depth = 0;
            for (int i = open; i < src.Length; i++)
            {
                if (src[i] == OpenBrace) depth++;
                else if (src[i] == CloseBrace)
                {
                    depth--;
                    if (depth == 0) return src.Substring(open, i - open + 1);
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// Names of the classes in one file that declare a Run(out string) entry point.
        /// A file may hold a helper class alongside the suite (TalentStrategyRegression.cs
        /// does), so the signature is attributed to the class whose declaration precedes it.
        /// </summary>
        private static List<string> ClassesWithRunEntryPoint(string code)
        {
            var result = new List<string>();
            var decls = ClassDecl.Matches(code).Cast<Match>().ToList();
            foreach (Match sig in RunSignature.Matches(code))
            {
                string owner = null;
                foreach (var d in decls)
                {
                    if (d.Index < sig.Index) owner = d.Groups[1].Value;
                    else break;
                }
                if (!string.IsNullOrEmpty(owner) && !result.Contains(owner)) result.Add(owner);
            }
            return result;
        }

        /// <summary>
        /// Lines where the suite answers TRUE straight out of a null / missing-file /
        /// empty guard - the "no-op and report OK" shape. Narrow window (the guard and
        /// the return within 4 lines, with a reason assignment) to keep false positives
        /// near zero; anything legitimate opts out with the hollow-pass-ok token.
        /// </summary>
        private static List<int> FindHollowPassLines(string code)
        {
            var hits = new List<int>();
            if (string.IsNullOrEmpty(code)) return hits;
            string[] lines = code.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].IndexOf("return true", StringComparison.Ordinal) < 0) continue;
                int from = Math.Max(0, i - 3);
                var window = new System.Text.StringBuilder();
                for (int j = from; j <= i; j++) window.Append(lines[j]).Append('\n');
                string w = window.ToString();
                bool guarded = w.Contains("== null")
                            || w.Contains("IsNullOrEmpty")
                            || w.Contains("!File.Exists")
                            || w.Contains("!Directory.Exists");
                bool namesReason = w.Contains("reason");
                bool alsoFails = w.Contains("return false");
                if (guarded && namesReason && !alsoFails) hits.Add(i + 1);
            }
            return hits;
        }
    }
}
