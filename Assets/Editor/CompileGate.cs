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
    }
}
