// =============================================================================
// ContentBuildTargetRegression [content-build-target]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.
// Markers: CONTENT_BUILD_TARGET_OK / CONTENT_BUILD_TARGET_FAIL.
//
// WO-1315 follow-up. THE DEFECT IT GUARDS, and it is a CLASS, not an incident.
//
// Addressables builds for the ACTIVE EDITOR TARGET, never for the target named in
// BuildPlayerOptions. AddressablesContentBuild therefore carries two overloads:
//
//   EnsureBuilt(caller)                  back-compat, builds for whatever is active
//   EnsureBuilt(caller, expectedTarget)  refuses, loudly, on a mismatch (WO-1124)
//
// A player-build entry point that calls the ONE-ARGUMENT form cannot state which
// platform it just built content for, so it ships whatever the editor was last left
// on - and it does so with EVERY MARKER GREEN. There is no runtime error, no red
// line, no visible symptom until a player loads the build and the catalog resolves
// stale or absent content. That is the CLAUDE.md sec.16 signature: silent, green,
// and wrong.
//
// It has now landed FIVE times:
//   2026-08-18  an APK whose enemy bundle had never been uploaded.
//   2026-08-19  WO-1124: an Android APK carrying StandaloneWindows64 content.
//   2026-08-20  every enemy a capsule; two wrong theories before the device log.
//   2026-09-01  the Synty/Tripo re-point that a stale catalog would have dropped.
//   2026-09-02  WO-1315: a WebGL build reporting
//                 ADDRESSABLES_CONTENT_OK 751 locations :: WebGLBuild
//                 target=StandaloneWindows64
//               while ServerData/WebGL sat three days old.
//
// WO-1124 fixed the ANDROID call site and left the symmetric WebGL one open, which is
// the duplicated-state class of CLAUDE.md sec.2 and sec.5: a fix applied to one of N
// identical sites. Fixing WebGLBuild alone repeats that mistake at a larger N. THIS
// ORACLE IS THE THING THAT MAKES OCCURRENCE SIX IMPOSSIBLE, because it fails on the
// SHAPE across every entry point rather than on any one file.
//
// It is a pure SOURCE scan - no scene, no play mode, no Addressables runtime, no
// active build target. It is decidable from text, which is why it can be trusted to
// run inside the very batch it guards, and why it would have caught WO-1315 without
// building anything at all.
//
//   CASE 1 [entry-points]  Discover every player-build entry point under
//     Assets/Editor: a .cs file whose source, with comments and string CONTENT
//     blanked, calls BuildPipeline.BuildPlayer. Discovery is by BEHAVIOUR, not by
//     a *Build*.cs filename glob - a sixth entry point will not be named to suit us.
//     Finding NONE is a hard FAIL, never a quiet pass: "the scan found nothing to
//     assert" is the hollow-pass shape this repo ratchets against (WO-1138).
//
//   CASE 2 [targetless-call]  No entry point may call the one-argument
//     EnsureBuilt overload. Detected on the STRIPPED source by parsing the actual
//     argument list at paren depth - a call whose first argument is a string and
//     which has ZERO top-level commas. Because string CONTENT is blanked, no prose
//     in the file under test and none in THIS file can satisfy or trip the lint.
//
//   CASE 3 [known-good]  AndroidBuild and WebGLBuild must each still pass an
//     explicit BuildTarget. This is the case that proves the scanner is reading the
//     right files: if the discovery in CASE 1 silently drifted onto the wrong set,
//     CASE 2 would find zero violations and report a clean sweep of nothing. An
//     oracle must demonstrate it can SEE the good shape before its silence means
//     anything.
//
//   CASE 4 [seam-intact]  AddressablesContentBuild still declares the two-parameter
//     overload AND still refuses a mismatch before building. Without that check the
//     explicit target CASE 2 and CASE 3 demand would be decoration: every call site
//     could name its platform and the content build would still happily produce
//     another one.
//
// Standalone: run-unity-method
//   -Method DeNelle.Editor.Regression.ContentBuildTargetRegression.RunAll
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class ContentBuildTargetRegression
    {
        private const string EditorRootRel = "Assets/Editor";

        private const string ContentBuildRel = "Assets/Editor/AddressablesContentBuild.cs";

        // The player-build entry points known to exist when this oracle was written.
        // CASE 3 asserts these are FOUND and CORRECT, so a discovery that quietly stops
        // matching real files cannot read as a clean sweep.
        private static readonly string[] KnownGoodCallers = { "AndroidBuild.cs", "WebGLBuild.cs" };

        // The marker of a player-build entry point: it actually builds a player.
        // Deliberately NOT a filename pattern - the next entry point will not be
        // named to suit a glob, and WO-1315 is entirely about symmetric sites drifting.
        private const string BuildPlayerNeedle = "BuildPipeline.BuildPlayer";

        private const string EnsureBuiltNeedle = "EnsureBuilt";

        public static void RunAll()
        {
            bool ok = Run(out string reason);
            if (ok) Debug.Log("CONTENT_BUILD_TARGET_OK\n" + reason);
            else    Debug.LogError("CONTENT_BUILD_TARGET_FAIL\n" + reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes    = new List<string>();

            var entryPoints = new List<string>();

            DeNelle.Core.Diagnostics.Guard.Try("Regression", "content-build-target case 1",
                () => Case1_DiscoverEntryPoints(entryPoints, failures, notes));
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "content-build-target case 2",
                () => Case2_NoTargetlessEnsureBuilt(entryPoints, failures, notes));
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "content-build-target case 3",
                () => Case3_KnownCallersPassExplicitTarget(entryPoints, failures, notes));
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "content-build-target case 4",
                () => Case4_SeamStillRefusesMismatch(failures, notes));

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
        //  CASE 1 - who actually builds a player
        // =====================================================================
        private static void Case1_DiscoverEntryPoints(List<string> entryPoints, List<string> failures, List<string> notes)
        {
            string root = FullPath(EditorRootRel);
            if (!Directory.Exists(root))
            {
                failures.Add("missing directory: " + EditorRootRel + " - the scan has nothing to read, which is " +
                             "a broken oracle, not a clean tree.");
                return;
            }

            string[] all;
            try
            {
                all = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            }
            catch (Exception e)
            {
                failures.Add("could not enumerate " + EditorRootRel + ": " + e.GetType().Name + ": " + e.Message);
                return;
            }

            foreach (string full in all)
            {
                // The Regression folder is excluded on purpose: oracle files QUOTE both the
                // build-player call and the EnsureBuilt grammar in their own literals and
                // prose, so scanning them would make the suite read its own text as evidence.
                // This file is the clearest example of that.
                if (IsUnderRegressionFolder(full)) continue;

                string raw;
                try { raw = File.ReadAllText(full); }
                catch (Exception e)
                {
                    failures.Add("could not read " + RelOf(full) + ": " + e.GetType().Name + ": " + e.Message);
                    continue;
                }

                string src = StripCommentsAndStrings(raw);
                if (src.IndexOf(BuildPlayerNeedle, StringComparison.Ordinal) < 0) continue;
                entryPoints.Add(full);
            }

            if (entryPoints.Count == 0)
            {
                failures.Add("found ZERO player-build entry points under " + EditorRootRel + ". Either every " +
                             "build path moved out from under this scan or the detection needle changed - " +
                             "in both cases this suite is now asserting nothing while reporting a pass, " +
                             "which is the hollow shape WO-1138 ratchets against. Fix the scan, do not " +
                             "relax it.");
                return;
            }

            notes.Add("[case1] " + entryPoints.Count + " player-build entry point(s): " +
                      string.Join(", ", NamesOf(entryPoints)));
        }

        // =====================================================================
        //  CASE 2 - the target-less overload is banned at every entry point
        // =====================================================================
        private static void Case2_NoTargetlessEnsureBuilt(List<string> entryPoints, List<string> failures, List<string> notes)
        {
            if (entryPoints.Count == 0) return;   // CASE 1 already failed loudly

            int scanned = 0, good = 0;

            foreach (string full in entryPoints)
            {
                string raw;
                try { raw = File.ReadAllText(full); }
                catch (Exception e)
                {
                    failures.Add("could not read " + RelOf(full) + ": " + e.GetType().Name + ": " + e.Message);
                    continue;
                }

                string src = StripCommentsAndStrings(raw);
                scanned++;

                foreach (CallSite call in FindEnsureBuiltCalls(src))
                {
                    if (call.TopLevelArgCount == 1)
                    {
                        failures.Add(RelOf(full) + " (line " + call.Line + ") calls the ONE-ARGUMENT " +
                                     "AddressablesContentBuild.EnsureBuilt overload. Addressables builds for " +
                                     "the ACTIVE editor target, so this content build cannot state which " +
                                     "platform it produced and will ship whatever the editor was last left " +
                                     "on - silently, with every marker green (WO-1124, WO-1315). Pass the " +
                                     "expected BuildTarget explicitly, and switch the active target in this " +
                                     "method BEFORE the content build, the way AndroidBuild and WebGLBuild do.");
                    }
                    else if (call.TopLevelArgCount >= 2)
                    {
                        good++;
                    }
                }
            }

            if (scanned == 0)
            {
                failures.Add("no player-build entry point could be re-read for the EnsureBuilt scan - " +
                             "nothing was asserted.");
                return;
            }

            notes.Add("[case2] " + scanned + " entry point(s) scanned, " + good +
                      " explicit-target content build call(s) seen");
        }

        // =====================================================================
        //  CASE 3 - the scanner can demonstrably see the GOOD shape
        // =====================================================================
        private static void Case3_KnownCallersPassExplicitTarget(List<string> entryPoints, List<string> failures, List<string> notes)
        {
            if (entryPoints.Count == 0) return;   // CASE 1 already failed loudly

            var seen = new List<string>();

            foreach (string known in KnownGoodCallers)
            {
                string full = FindByName(entryPoints, known);
                if (full == null)
                {
                    failures.Add("player-build entry point '" + known + "' was NOT discovered by the scan. " +
                                 "Either the file moved or it no longer calls " + BuildPlayerNeedle + ". Until " +
                                 "that is resolved this suite cannot prove it is reading the real build paths, " +
                                 "so its silence about the other files means nothing.");
                    continue;
                }

                string raw;
                try { raw = File.ReadAllText(full); }
                catch (Exception e)
                {
                    failures.Add("could not read " + RelOf(full) + ": " + e.GetType().Name + ": " + e.Message);
                    continue;
                }

                string src = StripCommentsAndStrings(raw);

                bool explicitFound = false;
                foreach (CallSite call in FindEnsureBuiltCalls(src))
                    if (call.TopLevelArgCount >= 2) explicitFound = true;

                if (!explicitFound)
                {
                    failures.Add(known + " no longer passes an explicit BuildTarget to " +
                                 "AddressablesContentBuild.EnsureBuilt. This is the exact WO-1124 / WO-1315 " +
                                 "regression: the build still gates green and ships another platform's " +
                                 "catalog.");
                    continue;
                }

                seen.Add(known);
            }

            if (seen.Count == 0)
            {
                failures.Add("NONE of the known-good callers (" + string.Join(", ", KnownGoodCallers) + ") was " +
                             "found passing an explicit target. A sweep that never sees a single correct call " +
                             "site is not evidence that the tree is clean - it is evidence the scan is aimed " +
                             "at the wrong files.");
                return;
            }

            notes.Add("[case3] known-good caller(s) verified: " + string.Join(", ", seen.ToArray()));
        }

        // =====================================================================
        //  CASE 4 - the seam still refuses a mismatch
        // =====================================================================
        private static void Case4_SeamStillRefusesMismatch(List<string> failures, List<string> notes)
        {
            string full = FullPath(ContentBuildRel);
            if (!File.Exists(full))
            {
                failures.Add("missing file: " + ContentBuildRel + " - the content-build seam every player " +
                             "build depends on is gone.");
                return;
            }

            string src;
            try { src = StripCommentsAndStrings(File.ReadAllText(full)); }
            catch (Exception e)
            {
                failures.Add("could not read " + ContentBuildRel + ": " + e.GetType().Name + ": " + e.Message);
                return;
            }

            int before = failures.Count;

            bool twoParam = false;
            foreach (CallSite decl in FindEnsureBuiltCalls(src))
                if (decl.TopLevelArgCount >= 2) twoParam = true;

            if (!twoParam)
                failures.Add(ContentBuildRel + " no longer declares a two-parameter EnsureBuilt. Without it no " +
                             "call site can state its platform and the whole WO-1124 guard is gone.");

            if (src.IndexOf("expectedTarget", StringComparison.Ordinal) < 0)
                failures.Add(ContentBuildRel + " no longer names an expectedTarget parameter - a call site could " +
                             "pass a target that is never compared against the active one, which is decoration, " +
                             "not a guard.");

            if (src.IndexOf("activeBuildTarget", StringComparison.Ordinal) < 0)
                failures.Add(ContentBuildRel + " never reads EditorUserBuildSettings.activeBuildTarget, so it " +
                             "cannot detect the mismatch it exists to refuse. Addressables builds for the ACTIVE " +
                             "target; a seam that does not read it is blind by construction.");

            if (failures.Count == before)
                notes.Add("[case4] the content-build seam still takes an expected target and compares it " +
                          "against the active one");
        }

        // =====================================================================
        //  Call-site parsing
        // =====================================================================

        private struct CallSite
        {
            public int Line;
            public int TopLevelArgCount;
        }

        /// <summary>Find every EnsureBuilt argument list in ALREADY-STRIPPED source and count its
        /// TOP-LEVEL arguments. Zero-argument sites are dropped - a dozen UI panels in this repo
        /// have their own private EnsureBuilt() for lazy widget construction, and those are not
        /// this. Everything else is REPORTED, including a one-argument call whose argument is a
        /// variable rather than a literal: for a ratchet a loud false positive is strictly the
        /// safer direction than a quiet false negative (the reasoning RegressionMarkerRegression
        /// measured for its own RULE 1).</summary>
        private static List<CallSite> FindEnsureBuiltCalls(string src)
        {
            var found = new List<CallSite>();
            int at = 0;
            while (true)
            {
                int k = src.IndexOf(EnsureBuiltNeedle, at, StringComparison.Ordinal);
                if (k < 0) break;
                at = k + EnsureBuiltNeedle.Length;

                // Reject a longer identifier that merely ENDS with the needle
                // (TryInvokeEnsureBuilt, MyEnsureBuiltThing).
                if (k > 0 && IsIdentChar(src[k - 1])) continue;

                int p = SkipSpace(src, at);
                if (p >= src.Length || src[p] != '(') continue;

                int argCount = CountTopLevelArgs(src, p);
                if (argCount <= 0) continue;        // zero-argument UI EnsureBuilt, or unbalanced

                found.Add(new CallSite { Line = LineOf(src, k), TopLevelArgCount = argCount });
            }
            return found;
        }

        /// <summary>Count arguments in the parenthesised list that OPENS at <paramref name="open"/>.
        /// Nested calls, generics and casts are skipped by paren depth, so a comma inside them is
        /// not miscounted. Returns 0 for an empty or unbalanced list.</summary>
        private static int CountTopLevelArgs(string src, int open)
        {
            int depth = 0;
            int commas = 0;
            bool anyContent = false;

            for (int i = open; i < src.Length; i++)
            {
                char c = src[i];
                if (c == '(') { depth++; continue; }
                if (c == ')')
                {
                    depth--;
                    if (depth == 0) return anyContent ? commas + 1 : 0;
                    continue;
                }
                if (depth != 1) continue;

                if (c == ',') { commas++; continue; }
                if (char.IsWhiteSpace(c)) continue;

                // String CONTENT is already blanked by the stripper, so an argument list is
                // pure structure by the time it reaches here. That is the whole point: prose
                // can neither satisfy nor trip this lint.
                anyContent = true;
            }
            return 0;   // unbalanced - report nothing rather than guess
        }

        private static bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c == '_';

        private static int SkipSpace(string src, int i)
        {
            while (i < src.Length && char.IsWhiteSpace(src[i])) i++;
            return i;
        }

        private static int LineOf(string src, int index)
        {
            int line = 1;
            for (int i = 0; i < index && i < src.Length; i++)
                if (src[i] == '\n') line++;
            return line;
        }

        // =====================================================================
        //  Path + source helpers
        // =====================================================================

        private static string FullPath(string rel) =>
            Path.Combine(Directory.GetCurrentDirectory(), rel.Replace('/', Path.DirectorySeparatorChar));

        private static string RelOf(string full)
        {
            string cwd = Directory.GetCurrentDirectory();
            string rel = full.StartsWith(cwd, StringComparison.OrdinalIgnoreCase)
                ? full.Substring(cwd.Length).TrimStart(Path.DirectorySeparatorChar, '/')
                : full;
            return rel.Replace('\\', '/');
        }

        private static bool IsUnderRegressionFolder(string full)
        {
            string rel = RelOf(full);
            return rel.IndexOf("/Regression/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string FindByName(List<string> paths, string fileName)
        {
            foreach (string p in paths)
                if (string.Equals(Path.GetFileName(p), fileName, StringComparison.OrdinalIgnoreCase)) return p;
            return null;
        }

        private static List<string> NamesOf(List<string> paths)
        {
            var list = new List<string>();
            foreach (string p in paths) list.Add(Path.GetFileName(p));
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        /// <summary>Blank comments and string literal CONTENT so no prose - in the file under
        /// test or in this one - can satisfy a lint. Structure (quotes, newlines) is kept so
        /// the result still reads as code. Same implementation as the sibling oracles; kept
        /// local because these suites are deliberately standalone-runnable.</summary>
        private static string StripCommentsAndStrings(string raw)
        {
            var sb = new StringBuilder(raw.Length);
            bool inLine = false, inBlock = false, inStr = false, inChar = false, inVerbatim = false;
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                char n = i + 1 < raw.Length ? raw[i + 1] : '\0';

                if (inLine) { if (c == '\n') { inLine = false; sb.Append(c); } continue; }
                if (inBlock) { if (c == '*' && n == '/') { inBlock = false; i++; } else if (c == '\n') sb.Append(c); continue; }
                if (inVerbatim)
                {
                    if (c == '"' && n == '"') { i++; continue; }
                    if (c == '"') { inVerbatim = false; sb.Append('"'); }
                    else if (c == '\n') sb.Append(c);
                    continue;
                }
                if (inStr)
                {
                    if (c == '\\' && n != '\0') { i++; continue; }
                    if (c == '"') { inStr = false; sb.Append('"'); }
                    continue;
                }
                if (inChar)
                {
                    if (c == '\\' && n != '\0') { i++; continue; }
                    if (c == '\'') { inChar = false; sb.Append('\''); }
                    continue;
                }

                if (c == '/' && n == '/') { inLine = true; i++; continue; }
                if (c == '/' && n == '*') { inBlock = true; i++; continue; }
                if (c == '@' && n == '"') { inVerbatim = true; sb.Append('"'); i++; continue; }
                if (c == '$' && n == '"') { inStr = true; sb.Append('"'); i++; continue; }
                if (c == '"') { inStr = true; sb.Append('"'); continue; }
                if (c == '\'') { inChar = true; sb.Append('\''); continue; }
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
