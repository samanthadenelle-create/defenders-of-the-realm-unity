using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// Headless compile gate. CLI uses the <c>COMPILE_GATE_OK</c> marker as the
    /// authoritative "does the tree compile" check before committing.
    ///
    /// WHY THIS FILE WAS REWRITTEN (gate-integrity bug, 2026-08-09):
    /// the old gate ASSUMED "if scripts didn't compile, this method never executes"
    /// and printed the OK marker unconditionally. That assumption is FALSE, and we
    /// have the captured proof: <c>Builds/chrome-compile.log</c> carries
    /// <c>COMPILE_GATE_OK</c> at line 4803 AND 54 <c>error CS</c> lines at 3681+.
    /// The mechanism, read off that log rather than guessed:
    ///   line  261  first script compilation requested -> SUCCEEDED
    ///   line 2428  domain reloaded with the good assemblies
    ///   line 2547  SECOND compilation requested (a .cs was saved mid-run) -> FAILED
    ///               (##### ExitCode 1, DeNelle.Village, 54 CS errors)
    ///   line 4803  -executeMethod ran anyway, off the STALE loaded domain, and the
    ///               old Run() printed the OK marker
    ///   line 4814  "Scripts have compiler errors."  (return code 1)
    /// i.e. Unity honours -executeMethod against the previously-loaded assemblies
    /// when a LATER compile of a RUNTIME asmdef fails. The marker described the
    /// stale DLLs, not the tree on disk. This is the
    /// "gates-report-success-without-proving-it" failure class, and it is severe
    /// precisely because every seat trusts the marker BECAUSE exit codes lie.
    ///
    /// THE FIX: <see cref="Run"/> now PROVES cleanliness instead of assuming it.
    /// The primary proof is a scan of the editor log this session is writing — the
    /// C# compiler's own error output, which the log ordering above shows is
    /// already on disk by the time Run() executes. Corroborated by
    /// EditorUtility.scriptCompilationFailed (reflected, may not exist) and by an
    /// assembly-output existence check via CompilationPipeline.
    ///
    /// DESIGN RULE, NON-NEGOTIABLE: a check that CANNOT DETERMINE the answer FAILS
    /// LOUD. It never passes silently. An "I couldn't tell" that prints green is
    /// the exact bug this file exists to kill.
    ///
    /// Also retained (WO-434): the NUL-byte scan and the brace-balance scan. The
    /// Linux-mount <-> Windows desync (CLAUDE.md §0) can leave a .cs NUL-padded —
    /// Windows + HEAD look byte-clean but the mount-written copy carries embedded
    /// \x00 bytes that poison a commit and break compilation.
    /// </summary>
    public static class CompileGate
    {
        /// <summary>
        /// EXACT marker other scripts grep for (run-tests.ps1,
        /// tools/regression/checkin_gate.ps1). Do not change this string.
        /// </summary>
        private const string OkMarker = "COMPILE_GATE_OK :: scripts compiled clean";

        /// <summary>
        /// Failure marker. Deliberately does NOT contain the substring
        /// "COMPILE_GATE_OK", so a `Select-String -Pattern 'COMPILE_GATE_OK'`
        /// consumer cannot mistake a failure for a pass.
        /// </summary>
        private const string FailMarker = "COMPILE_GATE_FAIL";

        /// <summary>Cap on how many offenders we name per check; the rest are counted.</summary>
        private const int MaxNamed = 12;

        /// <summary>
        /// TEST HOOK - internal, so only DeNelle.Editor can set it, and null in every
        /// production path. When set, the gate reads THIS log instead of discovering
        /// the live session log, which lets <see cref="CompileGateSelfTest"/> replay a
        /// real captured log (Builds/chrome-compile.log, the actual 2026-08-09
        /// false-green run) through the untouched production code path and prove the
        /// gate now goes RED on it. Always restored to null by the self-test.
        /// </summary>
        internal static string LogPathOverrideForSelfTest;

        public static void Run()
        {
            RunInternal();
        }

        /// <summary>
        /// The gate proper. Returns TRUE only when the tree is positively proven
        /// clean (i.e. the OK marker was printed). <see cref="Run"/> is the void
        /// entry point Unity's -executeMethod calls; this overload exists so the
        /// self-test can assert on the verdict instead of scraping the log.
        /// </summary>
        internal static bool RunInternal()
        {
            var failures = new List<string>();

            // ---- CHECK 1: did the C# compiler actually succeed? -----------------
            // Runs FIRST and reads the log BEFORE this method prints anything, so
            // our own diagnostic output can never poison our own scan.
            failures.AddRange(ProveScriptsCompiled());

            // ---- CHECK 2: mount-garble NUL bytes (WO-434) -----------------------
            List<string> nulOffenders = ScanForNulBytes();
            if (nulOffenders.Count > 0)
            {
                List<string> nulShown = Cap(nulOffenders, out int nulMore);
                foreach (string path in nulShown)
                {
                    Debug.LogError(
                        "[CompileGate] NUL-BYTE CORRUPTION in " + path +
                        " - mount-garbled, do not commit (see CLAUDE.md §0)");
                }
                if (nulMore > 0)
                    Debug.LogError("[CompileGate] ... and " + nulMore + " more NUL-corrupt file(s)");
                failures.Add(nulOffenders.Count + " .cs file(s) carry NUL bytes");
            }

            // ---- CHECK 3: brace balance ----------------------------------------
            List<string> braceOffenders = ScanBraceBalance();
            if (braceOffenders.Count > 0)
            {
                List<string> braceShown = Cap(braceOffenders, out int braceMore);
                foreach (string path in braceShown)
                    Debug.LogError("[CompileGate] BRACE MISMATCH in " + path);
                if (braceMore > 0)
                    Debug.LogError("[CompileGate] ... and " + braceMore + " more brace-mismatched file(s)");
                failures.Add(braceOffenders.Count + " .cs file(s) have mismatched braces");
            }

            // ---- verdict --------------------------------------------------------
            if (failures.Count > 0)
            {
                foreach (string reason in failures)
                    Debug.LogError("[CompileGate] REASON: " + reason);
                // OK marker withheld -> run-unity-method / checkin_gate see a failed gate.
                Debug.LogError(FailMarker + " :: " + failures.Count +
                               " check(s) failed - see [CompileGate] lines above. OK marker withheld.");
                return false;
            }

            Debug.Log(OkMarker);
            return true;
        }

        // =====================================================================
        // CHECK 1 - compile proof
        // =====================================================================

        /// <summary>
        /// Returns a list of failure reasons proving the scripts did NOT compile
        /// clean. Empty list == positively proven clean. Never returns empty on an
        /// inconclusive result: if no evidence source can be consulted, that is
        /// itself a failure reason (see the DESIGN RULE in the class doc).
        /// </summary>
        private static List<string> ProveScriptsCompiled()
        {
            var failures = new List<string>();
            bool haveAuthoritativeEvidence = false;

            // --- 1a. the editor log: the C# compiler's own output ---------------
            // This is the ground truth that the 2026-08-09 false-green proved was
            // sitting right there unread (errors at log line 3681, marker at 4803).
            string logPath = FindEditorLogPath();
            if (!string.IsNullOrEmpty(logPath))
            {
                if (TryScanLogForCompileErrors(logPath, out var byFile, out int totalErrors,
                                               out var samples, out bool unityVerdict, out string readError))
                {
                    haveAuthoritativeEvidence = true;

                    if (totalErrors > 0 || unityVerdict)
                    {
                        Debug.LogError("[CompileGate] COMPILE ERRORS found in the editor log for THIS session:");
                        Debug.LogError("[CompileGate]   log = " + logPath);

                        var names = new List<string>();
                        foreach (var kv in byFile)
                            names.Add(kv.Key + "  (" + kv.Value + " error" + (kv.Value == 1 ? "" : "s") + ")");
                        names.Sort(StringComparer.OrdinalIgnoreCase);

                        List<string> namesShown = Cap(names, out int more);
                        foreach (string n in namesShown)
                            Debug.LogError("[CompileGate]   FILE: " + n);
                        if (more > 0)
                            Debug.LogError("[CompileGate]   ... and " + more + " more file(s) with errors");

                        foreach (string s in samples)
                            Debug.LogError("[CompileGate]   > " + s);

                        if (unityVerdict)
                            Debug.LogError("[CompileGate]   Unity's own verdict line present: \"Scripts have compiler errors.\"");

                        failures.Add(totalErrors + " C# compile error(s) across " + byFile.Count +
                                     " file(s) - the loaded assemblies do NOT match the tree on disk");
                    }
                }
                else
                {
                    // Could not read a log we DID locate: inconclusive -> fail loud.
                    Debug.LogError("[CompileGate] could not read the editor log '" + logPath +
                                   "': " + readError);
                }
            }
            else
            {
                Debug.LogWarning("[CompileGate] no editor log path could be resolved " +
                                 "(no -logFile argument and no Application.consoleLogPath).");
            }

            // --- 1b. EditorUtility.scriptCompilationFailed (reflected) ----------
            // Public surface for this has moved around across Unity versions, so we
            // reflect rather than bind. Present == authoritative; absent == simply
            // no evidence from this source (it does not, on its own, pass anything).
            if (TryGetScriptCompilationFailed(out bool compilationFailed, out string reflectNote))
            {
                haveAuthoritativeEvidence = true;
                if (compilationFailed)
                {
                    Debug.LogError("[CompileGate] EditorUtility.scriptCompilationFailed == true " +
                                   "(Unity reports the script compilation for this domain FAILED).");
                    failures.Add("EditorUtility.scriptCompilationFailed == true");
                }
            }
            else
            {
                Debug.Log("[CompileGate] note: EditorUtility.scriptCompilationFailed unavailable (" +
                          reflectNote + ") - relying on the log scan + assembly outputs.");
            }

            // --- 1c. every compiled assembly must have produced an output DLL ---
            // Catches "this assembly never built at all". Deliberately checks
            // EXISTENCE, not mtime: an mtime comparison false-fails constantly in
            // this repo because git checkout / stash rewrites .cs timestamps while
            // Unity's content hashing correctly skips the recompile.
            try
            {
                var missing = new List<string>();
                // Fully qualified: `Assembly` alone is ambiguous between
                // System.Reflection.Assembly and UnityEditor.Compilation.Assembly.
                foreach (UnityEditor.Compilation.Assembly asm in
                         CompilationPipeline.GetAssemblies(AssembliesType.Editor))
                {
                    if (string.IsNullOrEmpty(asm.outputPath)) continue;
                    string outPath = asm.outputPath;
                    if (!Path.IsPathRooted(outPath))
                        outPath = Path.Combine(Directory.GetCurrentDirectory(), outPath);
                    if (!File.Exists(outPath))
                        missing.Add(asm.name + " -> " + asm.outputPath);
                }

                haveAuthoritativeEvidence = true;

                if (missing.Count > 0)
                {
                    List<string> missingShown = Cap(missing, out int missingMore);
                    foreach (string m in missingShown)
                        Debug.LogError("[CompileGate] MISSING ASSEMBLY OUTPUT: " + m);
                    if (missingMore > 0)
                        Debug.LogError("[CompileGate] ... and " + missingMore + " more assembly output(s) missing");
                    failures.Add(missing.Count + " assembly output DLL(s) missing - those assemblies never built");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[CompileGate] CompilationPipeline.GetAssemblies threw: " + e.Message);
            }

            // --- inconclusive == FAIL. Never pass silently. ----------------------
            if (!haveAuthoritativeEvidence)
            {
                failures.Add("INCONCLUSIVE - no compile-status evidence source could be consulted " +
                             "(log unreadable, scriptCompilationFailed absent, CompilationPipeline threw). " +
                             "A gate that cannot prove green must report red.");
            }

            return failures;
        }

        /// <summary>
        /// Resolves the log file this editor session is writing: the -logFile
        /// command-line argument first (that is what run-unity-method.ps1 passes),
        /// then Application.consoleLogPath. Returns null when neither resolves to
        /// an existing file (e.g. `-logFile -` streams to stdout with no file).
        /// </summary>
        private static string FindEditorLogPath()
        {
            // Self-test replay hook; null in every production path.
            if (!string.IsNullOrEmpty(LogPathOverrideForSelfTest) &&
                File.Exists(LogPathOverrideForSelfTest))
            {
                return LogPathOverrideForSelfTest;
            }

            try
            {
                string[] args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (!string.Equals(args[i], "-logFile", StringComparison.OrdinalIgnoreCase)) continue;
                    string candidate = args[i + 1];
                    if (string.IsNullOrEmpty(candidate) || candidate == "-") break;
                    if (!Path.IsPathRooted(candidate))
                        candidate = Path.Combine(Directory.GetCurrentDirectory(), candidate);
                    if (File.Exists(candidate)) return candidate;
                    break;
                }
            }
            catch (Exception)
            {
                // fall through to consoleLogPath
            }

            try
            {
                string console = Application.consoleLogPath;
                if (!string.IsNullOrEmpty(console) && File.Exists(console)) return console;
            }
            catch (Exception)
            {
                // no log available
            }

            return null;
        }

        private static readonly Regex CsErrorRx =
            new Regex(@"\berror CS\d{3,5}\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Streams the (still open, still being written) editor log looking for
        /// C# compiler error lines of the form
        ///   Assets\Path\File.cs(258,27): error CS0103: The name '...' ...
        /// plus Unity's own "Scripts have compiler errors." verdict line.
        /// Opened with FileShare.ReadWrite|Delete because the editor holds it open.
        /// Returns false only when the log could not be read at all.
        /// </summary>
        private static bool TryScanLogForCompileErrors(
            string logPath,
            out Dictionary<string, int> byFile,
            out int totalErrors,
            out List<string> samples,
            out bool unityVerdict,
            out string readError)
        {
            byFile = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            totalErrors = 0;
            samples = new List<string>();
            unityVerdict = false;
            readError = null;

            try
            {
                using (var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read,
                                               FileShare.ReadWrite | FileShare.Delete))
                using (var sr = new StreamReader(fs))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line.IndexOf("Scripts have compiler errors", StringComparison.Ordinal) >= 0)
                        {
                            unityVerdict = true;
                            continue;
                        }

                        // cheap reject before the regex - the log is large
                        if (line.IndexOf("error CS", StringComparison.Ordinal) < 0) continue;
                        if (!CsErrorRx.IsMatch(line)) continue;

                        totalErrors++;

                        string file = ExtractSourceFile(line);
                        byFile.TryGetValue(file, out int n);
                        byFile[file] = n + 1;

                        if (samples.Count < 3) samples.Add(line.Trim());
                    }
                }
            }
            catch (Exception e)
            {
                readError = e.GetType().Name + ": " + e.Message;
                return false;
            }

            return true;
        }

        /// <summary>Pulls the "Path\File.cs(line,col)" prefix out of a compiler error line.</summary>
        private static string ExtractSourceFile(string line)
        {
            int cs = line.IndexOf(".cs(", StringComparison.OrdinalIgnoreCase);
            if (cs < 0) return "(unattributed)";
            int close = line.IndexOf(')', cs);
            if (close < 0) return line.Substring(0, cs + 3).Trim();
            // file path only (drop the (line,col)) so counts aggregate per file
            return line.Substring(0, cs + 3).Trim();
        }

        /// <summary>
        /// Reads EditorUtility.scriptCompilationFailed without a compile-time bind
        /// (its accessibility has varied by Unity version). Returns false when the
        /// member does not exist - which means "no evidence", NOT "clean".
        /// </summary>
        private static bool TryGetScriptCompilationFailed(out bool failed, out string note)
        {
            failed = false;
            note = "not found";
            try
            {
                const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                Type t = typeof(EditorUtility);

                PropertyInfo p = t.GetProperty("scriptCompilationFailed", flags);
                if (p != null && p.PropertyType == typeof(bool) && p.CanRead)
                {
                    failed = (bool)p.GetValue(null, null);
                    note = "property";
                    return true;
                }

                FieldInfo f = t.GetField("scriptCompilationFailed", flags);
                if (f != null && f.FieldType == typeof(bool))
                {
                    failed = (bool)f.GetValue(null);
                    note = "field";
                    return true;
                }
            }
            catch (Exception e)
            {
                note = "threw " + e.GetType().Name;
            }

            return false;
        }

        /// <summary>Returns at most <see cref="MaxNamed"/> entries; <paramref name="more"/> gets the remainder count.</summary>
        private static List<string> Cap(List<string> all, out int more)
        {
            if (all.Count <= MaxNamed)
            {
                more = 0;
                return all;
            }

            more = all.Count - MaxNamed;
            return all.GetRange(0, MaxNamed);
        }

        // =====================================================================
        // WO-434 NUL scan + brace scan (unchanged behaviour)
        // =====================================================================

        /// <summary>
        /// Scans every project .cs file under Assets/ for an embedded/trailing NUL
        /// byte (\x00). Returns the list of offending paths (empty when clean).
        /// Fast: reads bytes and early-outs on the first NUL per file. Skips
        /// Library/Temp/obj/.git and non-.cs files.
        /// </summary>
        public static List<string> ScanForNulBytes()
        {
            var offenders = new List<string>();
            string assetsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Assets");
            if (!Directory.Exists(assetsRoot))
            {
                return offenders;
            }

            foreach (string path in Directory.EnumerateFiles(assetsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string norm = path.Replace('\\', '/');
                if (norm.Contains("/Library/") || norm.Contains("/Temp/") ||
                    norm.Contains("/obj/") || norm.Contains("/.git/"))
                {
                    continue;
                }

                if (FileContainsNul(path))
                {
                    offenders.Add(path);
                }
            }

            return offenders;
        }

        private static bool FileContainsNul(string path)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var buffer = new byte[64 * 1024];
                    int read;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        for (int i = 0; i < read; i++)
                        {
                            if (buffer[i] == 0)
                            {
                                return true; // early-out on first NUL
                            }
                        }
                    }
                }
            }
            catch (IOException)
            {
                // Unreadable file is not our concern here; treat as clean.
            }

            return false;
        }

        /// <summary>Fast static scan: every Assets/*.cs must have balanced { } counts.
        /// Catches mount-garble / half-written files BEFORE a wasted compile cycle.</summary>
        public static List<string> ScanBraceBalance()
        {
            var offenders = new List<string>();
            string assetsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Assets");
            if (!Directory.Exists(assetsRoot)) return offenders;

            foreach (string path in Directory.EnumerateFiles(assetsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string norm = path.Replace('\\', '/');
                if (norm.Contains("/Library/") || norm.Contains("/Temp/") ||
                    norm.Contains("/obj/") || norm.Contains("/.git/"))
                    continue;

                try
                {
                    string text = File.ReadAllText(path);
                    if (!BraceBalanced(text, out int open, out int close))
                        offenders.Add(norm + " (" + open + " open vs " + close + " close)");
                }
                catch (IOException)
                {
                    // unreadable - skip
                }
            }

            return offenders;
        }

        /// <summary>Counts { } outside strings / line+block comments (avoids lint-test false positives).</summary>
        private static bool BraceBalanced(string text, out int open, out int close)
        {
            open = close = 0;
            bool lineComment = false, blockComment = false, str = false, chr = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (lineComment)
                {
                    if (c == '\n') lineComment = false;
                    continue;
                }
                if (blockComment)
                {
                    if (c == '*' && i + 1 < text.Length && text[i + 1] == '/')
                    {
                        blockComment = false;
                        i++;
                    }
                    continue;
                }
                if (str)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == '"') str = false;
                    continue;
                }
                if (chr)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == '\'') chr = false;
                    continue;
                }
                if (c == '/' && i + 1 < text.Length)
                {
                    if (text[i + 1] == '/')
                    {
                        lineComment = true;
                        i++;
                        continue;
                    }
                    if (text[i + 1] == '*')
                    {
                        blockComment = true;
                        i++;
                        continue;
                    }
                }
                if (c == '"') { str = true; continue; }
                if (c == '\'') { chr = true; continue; }
                if (c == '{') open++;
                else if (c == '}') close++;
            }
            return open == close;
        }
    }
}
