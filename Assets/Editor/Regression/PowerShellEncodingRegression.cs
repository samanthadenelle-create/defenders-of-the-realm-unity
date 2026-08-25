// =============================================================================
// PowerShellEncodingRegression -- WO-1187. Makes "this .ps1 will silently never
// run" mechanically detectable, instead of discoverable only by accident.
// -----------------------------------------------------------------------------
// WHY THIS SUITE EXISTS.
//
// tools/verify-dungeons.ps1 had NEVER ONCE RUN. It was recorded as "does not parse
// -- string is missing the terminator", pre-existing at HEAD, and the script was
// never wrong. The file was UTF-8 with NO BOM and contained multi-byte characters.
// Windows PowerShell 5.1 reads a BOM-less file as ANSI, so those bytes decode into
// stray characters and the tokenizer breaks -- reporting the fault at a line far
// from the real cause. The identical bytes decoded as UTF-8 parse clean.
//
// A sweep found FIFTEEN .ps1 in this repo with non-ASCII content and ZERO of them
// carrying a BOM. Fourteen parsed by luck. Five of those fourteen are the
// CLAUDE.md section 16 ship chain, where a step that never runs is indistinguishable
// from a step that passed -- and a missed R2 push produces capsule enemies with no
// error on screen.
//
// THE DAMAGE IS NOT ALWAYS A PARSE ERROR, WHICH IS WHY GROUP 2 COUNTS STATEMENTS.
// Captured on 2026-08-25: a BOM-less file whose only non-ASCII was a pushpin emoji
// (U+1F4CC = F0 9F 93 8C) parsed with 2 top-level statements where the UTF-8
// reading has 4. Byte 0x93 is U+201C in CP1252 -- a LEFT DOUBLE QUOTATION MARK,
// which PowerShell accepts as a string delimiter. The mangled file can therefore
// report zero errors while whole statements have been swallowed into a string.
// .claude/skills/f8-watcher-auto-alert.ps1 was in exactly that state at HEAD:
// 0 parse errors, 3 top-level statements before conversion, 13 after.
//
// Groups:
//   1 [encoding] every .ps1 in the repo is pure ASCII, or carries a UTF-8 BOM.
//                ASCII is preferred: a BOM is removable by the next tool that
//                writes the file, ASCII content cannot break.
//   2 [parse]    every .ps1 parses under Windows PowerShell 5.1 with 0 errors AND
//                a NON-ZERO top-level statement count. The statement count is
//                mandatory: an empty or truncated file parses with zero errors and
//                zero statements, so an error-count-only assertion cannot fail on
//                the broken state (docs/INSTRUMENTATION_STANDARD.md section 1.4b).
//                SKIPPED, loudly, on a non-Windows editor; a launch failure ON
//                Windows is a FAILURE, never a skip.
//   3 [self]     the group 1 classifier is proven to FAIL the known-bad byte
//                pattern and PASS the known-good ones, in memory. A gate that does
//                not fail the known-bad state is not a gate.
//
// Markers: POWERSHELL_ENCODING_OK / POWERSHELL_ENCODING_FAIL.
// Standalone: run-unity-method
//   -Method DeNelle.Editor.PowerShellEncodingRegression.RunAll
// Registered in DataRegression.RunAll as the "ps1-encoding suite".
// =============================================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DeNelle.Editor
{
    public static class PowerShellEncodingRegression
    {
        public const string MarkerOk   = "POWERSHELL_ENCODING_OK";
        public const string MarkerFail = "POWERSHELL_ENCODING_FAIL";

        /// <summary>Directories whose .ps1 files are generated, vendored or unreachable.</summary>
        private static readonly string[] ExcludedDirs = { "Library", "Builds", "node_modules", ".git", "Temp", "obj", "bin" };

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- POWERSHELL ENCODING (WO-1187) ---");

            string root = Path.GetFullPath(".");
            log.AppendLine("  repo root = " + root);

            List<string> scripts;
            try
            {
                scripts = EnumerateScripts(root);
            }
            catch (Exception e)
            {
                reason = MarkerFail + ": could not enumerate .ps1 files under " + root + " -- " +
                         e.GetType().Name + ": " + e.Message + ". The class this suite guards is " +
                         "invisible without the walk, so an enumeration failure is a FAILURE.";
                Debug.LogError(log + "\n" + reason);
                return false;
            }

            if (scripts.Count == 0)
            {
                reason = MarkerFail + ": found ZERO .ps1 files under " + root + ". This suite passing on " +
                         "an empty set would be decoration -- the repo has dozens of them, so a zero " +
                         "count means the walk is wrong, not that the repo is clean.";
                Debug.LogError(log + "\n" + reason);
                return false;
            }
            log.AppendLine("  scanned " + scripts.Count + " .ps1 file(s)");

            // -- 3 [self] prove the classifier fails the known-bad state ---------
            // Done FIRST so a broken classifier cannot certify the repo clean.
            byte[] badBytes  = { 0x23, 0x20, (byte)0xE2, (byte)0x80, (byte)0x94 }; // "# " + em dash, no BOM
            byte[] bomBytes  = { 0xEF, 0xBB, 0xBF, 0x23, 0x20, (byte)0xE2, (byte)0x80, (byte)0x94 };
            byte[] asciiBytes = Encoding.ASCII.GetBytes("# plain ascii");

            if (Classify(badBytes) != Verdict.NonAsciiNoBom)
                failures.Add("[self] the encoding classifier did NOT flag BOM-less bytes containing 0xE2 " +
                             "(a UTF-8 em dash). That is the exact byte pattern that kept " +
                             "tools/verify-dungeons.ps1 from ever running, so a classifier that passes it " +
                             "certifies the repo clean while the defect is present.");
            else
                log.AppendLine("  [self] known-bad (non-ASCII, no BOM) is correctly flagged");

            if (Classify(bomBytes) != Verdict.Ok)
                failures.Add("[self] the encoding classifier flagged non-ASCII content that DOES carry a " +
                             "UTF-8 BOM. PowerShell 5.1 honours the BOM, so that state is legitimate and " +
                             "flagging it would make the gate un-passable.");
            else
                log.AppendLine("  [self] non-ASCII WITH a BOM is correctly accepted");

            if (Classify(asciiBytes) != Verdict.Ok)
                failures.Add("[self] the encoding classifier flagged pure-ASCII content. The gate would " +
                             "then fail every file in the repo and be turned off within a day.");
            else
                log.AppendLine("  [self] pure ASCII is correctly accepted");

            // -- 1 [encoding] every .ps1 is ASCII, or carries a BOM --------------
            int asciiCount = 0, bomCount = 0;
            foreach (string path in scripts)
            {
                byte[] bytes;
                try
                {
                    bytes = File.ReadAllBytes(path);
                }
                catch (Exception e)
                {
                    failures.Add("[encoding] could not read " + Rel(root, path) + " -- " +
                                 e.GetType().Name + ": " + e.Message);
                    continue;
                }

                Verdict v = Classify(bytes);
                if (v == Verdict.Ok)
                {
                    if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) bomCount++;
                    else asciiCount++;
                    continue;
                }

                int maxByte = 0, firstOffset = -1;
                for (int i = 0; i < bytes.Length; i++)
                {
                    if (bytes[i] <= 127) continue;
                    if (firstOffset < 0) firstOffset = i;
                    if (bytes[i] > maxByte) maxByte = bytes[i];
                }

                failures.Add("[encoding] " + Rel(root, path) + " is UTF-8 with NO BOM and contains non-ASCII " +
                             "(max byte " + maxByte + ", first at offset " + firstOffset + "). Windows " +
                             "PowerShell 5.1 reads a BOM-less file as ANSI, so those bytes decode into stray " +
                             "characters -- CP1252 turns 0x91/0x92/0x93/0x94 into SMART QUOTES, which " +
                             "PowerShell accepts as string delimiters. The script can then be silently " +
                             "mis-parsed, or not run at all, while every gate around it stays green. FIX: " +
                             "make the file pure ASCII (substitute '-', '=', '*', '!!', 'STOP' for dashes, " +
                             "box-drawing and emoji in banners). Adding a BOM also works but is weaker -- " +
                             "the next tool that writes the file can drop it.");
            }
            log.AppendLine("  [encoding] " + asciiCount + " pure ASCII, " + bomCount + " non-ASCII with BOM, " +
                           (scripts.Count - asciiCount - bomCount) + " violating");

            // -- 2 [parse] every .ps1 parses, non-vacuously -----------------------
            RunParseGroup(root, scripts, failures, log);

            if (failures.Count > 0)
            {
                reason = MarkerFail + ": " + failures.Count + " failure(s) -- " + string.Join(" | ", failures);
                Debug.LogError(log + "\n" + reason);
                return false;
            }

            reason = MarkerOk + " -- all " + scripts.Count + " .ps1 file(s) are pure ASCII or BOM-tagged, " +
                     "and each parses with 0 errors and a non-zero statement count";
            Debug.Log(log + MarkerOk);
            return true;
        }

        private enum Verdict { Ok, NonAsciiNoBom }

        /// <summary>
        /// The whole defect in one predicate: non-ASCII content with no UTF-8 BOM. Kept as a
        /// separate method precisely so group 3 can call it with the known-bad bytes -- an
        /// assertion that cannot fail on the broken state is decoration.
        /// </summary>
        private static Verdict Classify(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return Verdict.Ok;

            bool hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            if (hasBom) return Verdict.Ok;

            for (int i = 0; i < bytes.Length; i++)
                if (bytes[i] > 127) return Verdict.NonAsciiNoBom;

            return Verdict.Ok;
        }

        private static List<string> EnumerateScripts(string root)
        {
            var found = new List<string>();
            var stack = new Stack<string>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                string dir = stack.Pop();
                foreach (string sub in Directory.GetDirectories(dir))
                {
                    string name = Path.GetFileName(sub);
                    if (ExcludedDirs.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase))) continue;
                    stack.Push(sub);
                }
                foreach (string f in Directory.GetFiles(dir, "*.ps1"))
                    found.Add(f);
            }

            found.Sort(StringComparer.OrdinalIgnoreCase);
            return found;
        }

        private static string Rel(string root, string full)
        {
            if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                full = full.Substring(root.Length).TrimStart('\\', '/');
            return full.Replace('\\', '/');
        }

        /// <summary>
        /// Group 2. Parses every script in ONE Windows PowerShell process -- 50-odd files is a
        /// single sub-second launch, where one process per file would not be. Asserts BOTH
        /// 0 errors AND a non-zero top-level statement count, because a truncated or
        /// quote-swallowed file reports zero errors.
        /// </summary>
        private static void RunParseGroup(string root, List<string> scripts, List<string> failures, StringBuilder log)
        {
#if UNITY_EDITOR_WIN
            string listFile = null;
            try
            {
                listFile = Path.Combine(Path.GetTempPath(), "wo1187-ps1-list-" + Guid.NewGuid().ToString("N") + ".txt");
                File.WriteAllLines(listFile, scripts, new UTF8Encoding(false));

                string inner =
                    "& { foreach($p in [System.IO.File]::ReadAllLines('" + listFile.Replace("'", "''") + "')) { " +
                    "$e = $null; " +
                    "$a = [System.Management.Automation.Language.Parser]::ParseFile($p, [ref]$null, [ref]$e); " +
                    "$n = 0; if ($a -ne $null -and $a.EndBlock -ne $null) { $n = $a.EndBlock.Statements.Count }; " +
                    "$c = 0; if ($e -ne $null) { $c = @($e).Count }; " +
                    "Write-Output ('PSPARSE|' + $p + '|' + $c + '|' + $n) } }";

                var psi = new ProcessStartInfo
                {
                    FileName               = "powershell.exe",
                    Arguments              = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" + inner + "\"",
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true
                };

                string stdout, stderr;
                using (var proc = Process.Start(psi))
                {
                    if (proc == null)
                    {
                        failures.Add("[parse] powershell.exe could not be started on a Windows editor. The parse " +
                                     "arm of this gate is the half that catches a file whose bytes are clean but " +
                                     "whose syntax is not, so a launch failure here is a FAILURE, not a skip.");
                        return;
                    }
                    stdout = proc.StandardOutput.ReadToEnd();
                    stderr = proc.StandardError.ReadToEnd();
                    proc.WaitForExit(120000);
                }

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string raw in stdout.Split('\n'))
                {
                    string line = raw.Trim();
                    if (!line.StartsWith("PSPARSE|")) continue;

                    string[] parts = line.Split('|');
                    if (parts.Length < 4) continue;

                    string path = parts[1];
                    seen.Add(path);

                    int errCount, stmtCount;
                    if (!int.TryParse(parts[2], out errCount) || !int.TryParse(parts[3], out stmtCount))
                    {
                        failures.Add("[parse] unreadable result line for " + Rel(root, path) + ": " + line);
                        continue;
                    }

                    if (errCount > 0)
                    {
                        failures.Add("[parse] " + Rel(root, path) + " does NOT parse under Windows PowerShell 5.1 (" +
                                     errCount + " error(s)). A script that does not parse never runs, and a step " +
                                     "that never runs is indistinguishable from a step that passed. Check the file " +
                                     "for non-ASCII bytes FIRST -- the reported line number is usually far from the " +
                                     "real cause.");
                        continue;
                    }

                    if (stmtCount <= 0)
                    {
                        failures.Add("[parse] " + Rel(root, path) + " parses with 0 errors but ZERO top-level " +
                                     "statements. That is what a truncated or emptied file looks like -- it is the " +
                                     "exact state that reported 'PARSES CLEAN' on an empty file during WO-1187. " +
                                     "Either the file was clobbered, or its whole body was swallowed into an " +
                                     "unterminated string by a stray high byte.");
                    }
                }

                int missing = scripts.Count(s => !seen.Contains(s));
                if (missing > 0)
                {
                    failures.Add("[parse] the parser reported on only " + seen.Count + " of " + scripts.Count +
                                 " script(s). Silence is not a pass. powershell.exe stderr: " +
                                 (string.IsNullOrEmpty(stderr) ? "(empty)" : stderr.Trim()));
                }
                else
                {
                    log.AppendLine("  [parse] " + seen.Count + " script(s) parsed with 0 errors and >0 statements");
                }
            }
            catch (Exception e)
            {
                failures.Add("[parse] the PowerShell parse pass threw " + e.GetType().Name + ": " + e.Message +
                             ". Reported rather than swallowed -- a catch that hides this would turn the gate off " +
                             "without saying so (CLAUDE.md section 12).");
            }
            finally
            {
                try { if (listFile != null && File.Exists(listFile)) File.Delete(listFile); } catch { /* temp file */ }
            }
#else
            log.AppendLine("  [parse] SKIPPED -- not a Windows editor, so Windows PowerShell 5.1 is not available. " +
                           "The [encoding] group above still ran and it is the arm that catches the WO-1187 class.");
#endif
        }

        /// <summary>Standalone entry point (run-unity-method).</summary>
        public static void RunAll()
        {
            bool ok = Run(out string reason);
            Debug.Log(reason);
            if (!ok) EditorApplication.Exit(1);
        }
    }
}
