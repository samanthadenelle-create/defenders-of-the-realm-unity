using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// Headless compile gate. Opening the project in batchmode forces a full
    /// script recompile; if everything compiles, <see cref="Run"/> executes and
    /// prints the marker below. If compilation fails, the marker never appears and
    /// the batch log carries the CS errors instead. CLI uses this as the
    /// authoritative "does the tree compile" check before committing.
    ///
    /// In addition to "did it compile", <see cref="Run"/> also runs a NUL-byte
    /// scan (WO-434): the Linux-mount ↔ Windows desync (CLAUDE.md §0) can leave a
    /// .cs file NUL-padded — Windows + HEAD look byte-clean but the mount-written
    /// copy carries embedded/trailing \x00 bytes that poison a commit and break
    /// compilation. Any such file is a FAILURE and the OK marker is suppressed, so
    /// run-unity-method's success-marker logic sees the gate as failed.
    /// </summary>
    public static class CompileGate
    {
        public static void Run()
        {
            // If scripts didn't compile, this method never executes (the marker
            // simply never appears). When it does run, also guard against the
            // mount-garble NUL-byte corruption before declaring the gate green.
            List<string> offenders = ScanForNulBytes();
            if (offenders.Count > 0)
            {
                foreach (string path in offenders)
                {
                    Debug.LogError(
                        "[CompileGate] NUL-BYTE CORRUPTION in " + path +
                        " — mount-garbled, do not commit (see CLAUDE.md §0)");
                }
                Debug.LogError(
                    "[CompileGate] gate FAILED — " + offenders.Count +
                    " .cs file(s) carry NUL bytes; OK marker withheld.");
                return; // suppress COMPILE_GATE_OK so the gate reports failure
            }

            List<string> braceOffenders = ScanBraceBalance();
            if (braceOffenders.Count > 0)
            {
                foreach (string path in braceOffenders)
                    Debug.LogError("[CompileGate] BRACE MISMATCH in " + path);
                Debug.LogError(
                    "[CompileGate] gate FAILED — " + braceOffenders.Count +
                    " .cs file(s) have mismatched braces; OK marker withheld.");
                return;
            }

            Debug.Log("COMPILE_GATE_OK :: scripts compiled clean");
        }

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
                    // unreadable — skip
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
