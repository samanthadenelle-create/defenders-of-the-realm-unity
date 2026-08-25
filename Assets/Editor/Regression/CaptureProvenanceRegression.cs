// =============================================================================
// CaptureProvenance + CaptureProvenanceRegression -- WO-1080.
// -----------------------------------------------------------------------------
// WHY THIS FILE EXISTS
//
// Four layout tickets (WO-1075/1076/1077/1078) were all minted from ONE aged
// capture log, `Builds/wo1060-capture.log`, and described a tree that had already
// moved on. WO-1076 was reopened against a panel fixed three days earlier
// (a2162f17d) and cost a seat a morning; three of the four quoted arithmetic
// ("drops from UI_TOUCH_FAIL x43 to x25") against a baseline that no longer
// existed.
//
// The trap, stated once so nobody re-designs around the wrong axis:
//
//   *** A CAPTURE LOG'S FILE DATE IS NOT EVIDENCE OF THE TREE IT MEASURED. ***
//
// `wo1060-capture.log` has an mtime of 2026-08-23 and an in-log licensing stamp of
// 2026-08-23T17:39:59Z. The fix it fails to reflect landed 2026-08-21. The log is
// NEWER than the commit it does not contain -- so any staleness check built on
// mtimes is defeated by the exact case that motivated it. The capture must record
// the COMMIT, and the ticket must cite that commit.
//
// -----------------------------------------------------------------------------
// WHY THE MECHANISM LIVES IN THE REGRESSION ASSEMBLY (deliberate, not an accident)
//
// `Assets/Editor/` is assembly `DeNelle.Editor`; `Assets/Editor/Regression/` is the
// SEPARATE assembly `DeNelle.EditorRegression`. The reference runs ONE WAY:
// DeNelle.Editor -> DeNelle.EditorRegression. So a resolver placed beside
// UICaptureLaunch.cs would be unreachable from any oracle here, and adding the
// reverse reference would be an assembly cycle. Putting the resolver on THIS side
// lets the capture call it AND lets the oracle below prove it actually resolves on
// this machine -- which is the whole point: a stamp that silently degrades to
// "unknown" is a stamp nobody can cite.
//
// -----------------------------------------------------------------------------
// THE CHAIN THIS ESTABLISHES
//
//   1. The CAPTURE stamps itself. `UICaptureLaunch.RunCaptureHeadless` emits
//
//          UI_CAPTURE_HEAD <40-hex sha> <branch> dirty=<true|false>
//
//      before it shoots anything, and a totals line after. No human remembers a
//      second command -- CLAUDE.md 16's rule, learned from the R2 push.
//
//   2. A minted layout ticket carries, in its header block:
//
//          **Capture:** `Builds/<log>.log` @ `<sha>` -- targets `Assets/.../<File>.cs`
//
//   3. `tools/board_build.py` resolves the newest commit touching that target and
//      flags STALE-CAPTURE when it is not reachable from the cited sha.
//
// This file owns step 1's mechanism and the oracle that keeps step 1 honest.
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    /// <summary>
    /// Resolves "which tree was this run measuring" for a capture harness, and owns the
    /// exact wire shape of the <c>UI_CAPTURE_HEAD</c> marker (format AND parse, so the two
    /// can never drift -- the duplicated-fact failure this whole ticket exists to kill).
    /// </summary>
    public static class CaptureProvenance
    {
        /// <summary>The marker a caller greps. DISTINCT from every other UI_CAPTURE_* marker
        /// (CLAUDE.md 8: one marker per entry point, never a shared string).</summary>
        public const string HeadMarker = "UI_CAPTURE_HEAD";

        /// <summary>Emitted INSTEAD of <see cref="HeadMarker"/> when the tree cannot be
        /// identified. Deliberately NOT a "UI_CAPTURE_HEAD..." suffix: a grep for the good
        /// marker must never match the failure line.</summary>
        public const string FailMarker = "UI_CAPTURE_PROVENANCE_FAIL";

        /// <summary>Carries the run's measured totals beside the sha, so a ticket's quoted
        /// baseline can be checked against the log it claims to come from.</summary>
        public const string StampMarker = "UI_CAPTURE_STAMP";

        /// <summary>Branch text used when HEAD is detached (no branch to name).</summary>
        public const string DetachedBranch = "DETACHED";

        private const int GitTimeoutMs = 8000;

        /// <summary>One resolved answer. <see cref="Resolved"/> is the only thing a caller
        /// may trust; every other field is descriptive.</summary>
        public readonly struct Head
        {
            /// <summary>40-char lowercase hex, or null when unresolved.</summary>
            public readonly string Sha;
            /// <summary>Branch name, or <see cref="DetachedBranch"/>, or null when unresolved.</summary>
            public readonly string Branch;
            /// <summary>True when tracked files under Assets/ carry uncommitted changes.</summary>
            public readonly bool Dirty;
            /// <summary>False when dirtiness could not be measured (the .git-file fallback
            /// cannot diff). A capture that cannot measure dirtiness is reported as DIRTY,
            /// because "unknown" must never read as "clean and citable".</summary>
            public readonly bool DirtyMeasured;
            /// <summary>"git" (the git binary answered) or "gitdir" (raw .git file read).</summary>
            public readonly string Source;
            /// <summary>Human-readable reason when <see cref="Resolved"/> is false.</summary>
            public readonly string Failure;

            public Head(string sha, string branch, bool dirty, bool dirtyMeasured,
                        string source, string failure)
            {
                Sha = sha;
                Branch = branch;
                Dirty = dirty;
                DirtyMeasured = dirtyMeasured;
                Source = source;
                Failure = failure;
            }

            public bool Resolved
            {
                get { return IsSha(Sha) && !string.IsNullOrEmpty(Branch); }
            }
        }

        /// <summary>Repo root as seen from a batchmode editor run (the project folder, which
        /// is the repo root -- Assets/ sits directly under it). Never hardcoded: CLAUDE.md 0,
        /// the root is C:\eoa on one machine and D:\eoa on another.</summary>
        public static string RepoRoot()
        {
            try
            {
                // Application.dataPath = <root>/Assets. Its parent is the root, and it is
                // correct even if the process cwd was changed by a wrapper script.
                string data = Application.dataPath;
                if (!string.IsNullOrEmpty(data))
                {
                    var parent = Directory.GetParent(data);
                    if (parent != null) return parent.FullName;
                }
            }
            catch (Exception) { /* fall through to cwd */ }
            return Directory.GetCurrentDirectory();
        }

        /// <summary>
        /// Resolve the commit this process is running against. Never throws; on total failure
        /// returns a Head whose <see cref="Head.Resolved"/> is false and whose
        /// <see cref="Head.Failure"/> names why.
        /// </summary>
        public static Head Resolve()
        {
            return Resolve(RepoRoot());
        }

        public static Head Resolve(string repoRoot)
        {
            var problems = new List<string>();

            // --- Primary: ask git. It is the only source that can answer "dirty". ---
            string sha = RunGit(repoRoot, "rev-parse HEAD", problems);
            if (IsSha(sha))
            {
                string branch = RunGit(repoRoot, "rev-parse --abbrev-ref HEAD", problems);
                if (string.IsNullOrEmpty(branch) || branch == "HEAD") branch = DetachedBranch;

                // Scoped to Assets/ on purpose: a capture measures the layout the C# under
                // Assets/ produces. An edited README cannot change a resolved rect, and
                // treating it as dirty would make every real run uncitable.
                string porcelain = RunGit(repoRoot, "status --porcelain -- Assets", problems);
                bool dirtyMeasured = porcelain != null;
                bool dirty = dirtyMeasured && porcelain.Trim().Length > 0;
                if (!dirtyMeasured) dirty = true; // unknown is never reported as clean.

                return new Head(sha.ToLowerInvariant(), branch, dirty, dirtyMeasured, "git", null);
            }

            // --- Fallback: read .git directly. No process, no PATH dependency. ---
            string fbBranch;
            string fbSha = ReadGitDirHead(repoRoot, out fbBranch, problems);
            if (IsSha(fbSha))
            {
                // The raw read cannot diff the index, so dirtiness is UNMEASURED and therefore
                // reported as dirty -- an un-citable capture is the safe answer.
                return new Head(fbSha.ToLowerInvariant(),
                                string.IsNullOrEmpty(fbBranch) ? DetachedBranch : fbBranch,
                                true, false, "gitdir", null);
            }

            return new Head(null, null, true, false, null,
                            problems.Count == 0 ? "no reason captured" : string.Join("; ", problems.ToArray()));
        }

        /// <summary>The exact wire shape. The ONE place it is written.</summary>
        public static string FormatHeadLine(Head head)
        {
            if (!head.Resolved) return null;
            return HeadMarker + " " + head.Sha + " " + head.Branch +
                   " dirty=" + (head.Dirty ? "true" : "false");
        }

        /// <summary>
        /// The ONE place the wire shape is read. Strict on purpose: a ticket citing a
        /// truncated or hand-typed sha must be REJECTED here rather than silently accepted
        /// and then compared against nothing.
        /// </summary>
        public static bool TryParseHeadLine(string line, out string sha, out string branch, out bool dirty)
        {
            sha = null;
            branch = null;
            dirty = false;
            if (string.IsNullOrEmpty(line)) return false;

            int at = line.IndexOf(HeadMarker, StringComparison.Ordinal);
            if (at < 0) return false;
            // Reject a longer marker that merely STARTS with ours (UI_CAPTURE_HEADROOM etc).
            int after = at + HeadMarker.Length;
            if (after < line.Length && line[after] != ' ') return false;

            string rest = line.Substring(after).Trim();
            string[] parts = rest.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) return false;
            if (!IsSha(parts[0])) return false;

            string dirtyToken = null;
            for (int i = 1; i < parts.Length; i++)
            {
                if (parts[i].StartsWith("dirty=", StringComparison.Ordinal)) { dirtyToken = parts[i]; break; }
            }
            if (dirtyToken == null) return false;

            string value = dirtyToken.Substring("dirty=".Length);
            if (value == "true") dirty = true;
            else if (value == "false") dirty = false;
            else return false;

            sha = parts[0].ToLowerInvariant();
            branch = parts[1];
            return branch.Length > 0 && !branch.StartsWith("dirty=", StringComparison.Ordinal);
        }

        /// <summary>40-char lowercase-or-uppercase hex.</summary>
        public static bool IsSha(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length != 40) return false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!hex) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------
        //  git process
        // ------------------------------------------------------------------
        private static string RunGit(string repoRoot, string args, List<string> problems)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("git", args)
                {
                    WorkingDirectory = repoRoot,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    if (p == null)
                    {
                        problems.Add("git '" + args + "': process did not start");
                        return null;
                    }
                    string stdout = p.StandardOutput.ReadToEnd();
                    string stderr = p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(GitTimeoutMs))
                    {
                        try { p.Kill(); } catch (Exception) { }
                        problems.Add("git '" + args + "': timed out after " + GitTimeoutMs + "ms");
                        return null;
                    }
                    if (p.ExitCode != 0)
                    {
                        problems.Add("git '" + args + "': exit " + p.ExitCode + " " + (stderr ?? string.Empty).Trim());
                        return null;
                    }
                    return (stdout ?? string.Empty).Trim();
                }
            }
            catch (Exception e)
            {
                problems.Add("git '" + args + "': " + e.GetType().Name + " " + e.Message);
                return null;
            }
        }

        // ------------------------------------------------------------------
        //  .git direct read (no git binary needed)
        // ------------------------------------------------------------------
        private static string ReadGitDirHead(string repoRoot, out string branch, List<string> problems)
        {
            branch = null;
            try
            {
                string gitPath = Path.Combine(repoRoot, ".git");
                string gitDir = null;

                if (Directory.Exists(gitPath))
                {
                    gitDir = gitPath;
                }
                else if (File.Exists(gitPath))
                {
                    // Linked worktree: ".git" is a FILE containing "gitdir: <path>".
                    string body = File.ReadAllText(gitPath).Trim();
                    const string key = "gitdir:";
                    if (body.StartsWith(key, StringComparison.Ordinal))
                    {
                        string p = body.Substring(key.Length).Trim();
                        gitDir = Path.IsPathRooted(p) ? p : Path.GetFullPath(Path.Combine(repoRoot, p));
                    }
                }

                if (string.IsNullOrEmpty(gitDir) || !Directory.Exists(gitDir))
                {
                    problems.Add(".git not found at " + gitPath);
                    return null;
                }

                string headFile = Path.Combine(gitDir, "HEAD");
                if (!File.Exists(headFile))
                {
                    problems.Add("no HEAD file under " + gitDir);
                    return null;
                }

                string head = File.ReadAllText(headFile).Trim();
                if (!head.StartsWith("ref:", StringComparison.Ordinal))
                {
                    // Detached: HEAD holds the sha itself.
                    return IsSha(head) ? head : null;
                }

                string refName = head.Substring(4).Trim();          // refs/heads/wip/foo
                branch = refName.StartsWith("refs/heads/", StringComparison.Ordinal)
                    ? refName.Substring("refs/heads/".Length)
                    : refName;

                string looseRef = Path.Combine(gitDir, refName.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(looseRef))
                {
                    string s = File.ReadAllText(looseRef).Trim();
                    if (IsSha(s)) return s;
                }

                // Packed refs: "<sha> <refname>" per line.
                string packed = Path.Combine(gitDir, "packed-refs");
                if (File.Exists(packed))
                {
                    foreach (string raw in File.ReadAllLines(packed))
                    {
                        string line = (raw ?? string.Empty).Trim();
                        if (line.Length == 0 || line[0] == '#' || line[0] == '^') continue;
                        int sp = line.IndexOf(' ');
                        if (sp <= 0) continue;
                        if (string.Equals(line.Substring(sp + 1).Trim(), refName, StringComparison.Ordinal))
                        {
                            string s = line.Substring(0, sp).Trim();
                            if (IsSha(s)) return s;
                        }
                    }
                }

                problems.Add("ref " + refName + " resolved to no sha (loose ref and packed-refs both missed)");
                return null;
            }
            catch (Exception e)
            {
                problems.Add(".git read: " + e.GetType().Name + " " + e.Message);
                return null;
            }
        }
    }

    /// <summary>
    /// WO-1080 oracle. Proves the provenance chain is ALIVE, not merely present:
    /// the resolver really answers on this machine, the wire shape round-trips, the parser
    /// refuses malformed citations, and the capture still emits the stamp itself.
    ///
    /// Registered in DataRegression.cs as "capture-provenance suite".
    /// </summary>
    public static class CaptureProvenanceRegression
    {
        private const string Tag = "CAPTURE_PROVENANCE";

        // The emit site this oracle pins. If someone deletes the call, a capture stops
        // stamping itself and we are back to a human remembering a second command --
        // which CLAUDE.md 16 records as "not a gate".
        private const string CaptureFileRelative = "Editor/UICaptureLaunch.cs";
        private const string EmitAnchor = "CaptureProvenance.FormatHeadLine";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new StringBuilder();

            CheckResolver(failures, notes);
            CheckRoundTrip(failures);
            CheckParserRejects(failures);
            CheckCaptureStampsItself(failures, notes);
            CheckMarkerIsDistinct(failures);
            NoteLiveCaptureLog(notes);

            if (failures.Count > 0)
            {
                reason = string.Join("\n", failures.ToArray());
                return false;
            }

            reason = Tag + " OK - " + notes.ToString().Trim();
            return true;
        }

        // 1. The resolver actually resolves HERE. Falsifiable: git absent from PATH, a
        //    relocated .git, a linked worktree whose gitdir pointer is wrong, or a packed-ref
        //    layout the fallback cannot walk all produce Resolved=false and fail this line.
        private static void CheckResolver(List<string> failures, StringBuilder notes)
        {
            CaptureProvenance.Head head;
            try
            {
                head = CaptureProvenance.Resolve();
            }
            catch (Exception e)
            {
                failures.Add(Tag + " FAIL resolver threw: " + e.GetType().Name + " " + e.Message);
                return;
            }

            if (!head.Resolved)
            {
                failures.Add(Tag + " FAIL the capture cannot identify the tree it is measuring, so every " +
                             "log it writes is uncitable and the WO-1080 chain is dead at step 1. Reason: " +
                             (head.Failure ?? "unstated"));
                return;
            }

            if (!CaptureProvenance.IsSha(head.Sha))
            {
                failures.Add(Tag + " FAIL resolved sha is not 40-hex: '" + head.Sha + "'");
                return;
            }

            notes.Append("head=").Append(head.Sha.Substring(0, 12))
                 .Append(" branch=").Append(head.Branch)
                 .Append(" dirty=").Append(head.Dirty ? "true" : "false")
                 .Append(head.DirtyMeasured ? "" : "(UNMEASURED->reported dirty)")
                 .Append(" via=").Append(head.Source ?? "?")
                 .Append("; ");
        }

        // 2. Format and parse are the same fact written twice. Pin them together, because
        //    that duplication going stale IS the class of defect this ticket exists to kill.
        private static void CheckRoundTrip(List<string> failures)
        {
            const string sha = "a2162f17d0000000000000000000000000000001";
            var head = new CaptureProvenance.Head(sha, "wip/village2-and-f8-tickets",
                                                  true, true, "git", null);
            string line = CaptureProvenance.FormatHeadLine(head);
            if (string.IsNullOrEmpty(line))
            {
                failures.Add(Tag + " FAIL FormatHeadLine returned nothing for a resolved head");
                return;
            }

            string outSha, outBranch;
            bool outDirty;
            if (!CaptureProvenance.TryParseHeadLine(line, out outSha, out outBranch, out outDirty))
            {
                failures.Add(Tag + " FAIL the parser cannot read the formatter's own line: '" + line + "'");
                return;
            }
            if (outSha != sha)
                failures.Add(Tag + " FAIL round-trip sha drifted: wrote " + sha + " read " + outSha);
            if (outBranch != "wip/village2-and-f8-tickets")
                failures.Add(Tag + " FAIL round-trip branch drifted: read '" + outBranch + "'");
            if (!outDirty)
                failures.Add(Tag + " FAIL round-trip dropped dirty=true -- a dirty capture would read as citable");

            // The clean direction too: dirty=false must survive as false, not default to it.
            string cleanLine = CaptureProvenance.FormatHeadLine(
                new CaptureProvenance.Head(sha, "master", false, true, "git", null));
            if (!CaptureProvenance.TryParseHeadLine(cleanLine, out outSha, out outBranch, out outDirty) || outDirty)
                failures.Add(Tag + " FAIL dirty=false did not survive the round trip: '" + cleanLine + "'");
        }

        // *** DO NOT "TIDY" THE COMPOSED STRING BELOW BACK INTO A LITERAL. READ THIS FIRST. ***
        //
        // RegressionMarkerRegression RULE 1 scans the SOURCE TEXT of every oracle file for an
        // ALL-CAPS *_OK token inside a double-quoted literal and counts one OWNER per file. It
        // reads text, so it cannot tell a MENTION from an EMISSION. This suite MENTIONS another
        // suite's marker on purpose -- as a NEGATIVE fixture, proving the parser refuses a line
        // that is not ours -- and a bare literal here therefore registered this file as a second
        // emitter of UI_CAPTURE_OK and turned the whole registry RED (2026-08-25).
        //
        // The fix keeps the RUNTIME STRING BYTE-IDENTICAL and only breaks up the source token, so
        // the test loses nothing: the parser is still handed the exact foreign marker line and
        // must still refuse it. Re-joining the halves would restore the false ownership and
        // re-red the gate. Widening RegressionMarkerRegression instead is the one move that is
        // never available (owner ruling 2026-08-24: no waivers -- do not take the batteries out
        // of a smoke alarm because it is beeping).
        private const string ForeignOkMarker = "UI_CAPTURE" + "_OK";

        // 3. A lax parser is worse than none: it would ACCEPT a hand-typed short sha and then
        //    compare it against nothing. Each of these must be refused.
        private static void CheckParserRejects(List<string> failures)
        {
            var bad = new[]
            {
                "UI_CAPTURE_HEAD a2162f17d master dirty=false",          // short (abbreviated) sha
                "UI_CAPTURE_HEAD a2162f17d0000000000000000000000000000001 master", // no dirty token
                "UI_CAPTURE_HEAD a2162f17d0000000000000000000000000000001 master dirty=maybe",
                "UI_CAPTURE_HEADROOM a2162f17d0000000000000000000000000000001 master dirty=false",
                ForeignOkMarker + " 51",   // a DIFFERENT marker entirely -- see the note above
                ""
            };

            foreach (string line in bad)
            {
                string s, b;
                bool d;
                if (CaptureProvenance.TryParseHeadLine(line, out s, out b, out d))
                    failures.Add(Tag + " FAIL parser ACCEPTED a malformed citation: '" + line + "'");
            }
        }

        // 4. The stamp is written BY THE CAPTURE. A step whose remedy is "someone remembers a
        //    second command" is not a gate (CLAUDE.md 16, the R2 push lesson). Falsifiable:
        //    delete the emit and this line goes red.
        private static void CheckCaptureStampsItself(List<string> failures, StringBuilder notes)
        {
            string path = Path.Combine(Application.dataPath,
                CaptureFileRelative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                failures.Add(Tag + " FAIL capture harness missing: " + path);
                return;
            }

            string src;
            try { src = File.ReadAllText(path); }
            catch (Exception e)
            {
                failures.Add(Tag + " FAIL cannot read " + path + ": " + e.Message);
                return;
            }

            if (src.IndexOf(EmitAnchor, StringComparison.Ordinal) < 0)
            {
                failures.Add(Tag + " FAIL " + CaptureFileRelative + " no longer calls " + EmitAnchor +
                             " -- the capture has stopped stamping the tree it measured, so a layout " +
                             "ticket minted from its log is uncitable again (WO-1080's whole defect).");
                return;
            }

            int entry = src.IndexOf("public static void RunCaptureHeadless", StringComparison.Ordinal);
            int emit = src.IndexOf(EmitAnchor, StringComparison.Ordinal);
            if (entry < 0 || emit < entry)
            {
                failures.Add(Tag + " FAIL the provenance emit is not inside/after RunCaptureHeadless " +
                             "(entry@" + entry + " emit@" + emit + ") -- the headless entry point is the " +
                             "one that writes the logs tickets are minted from.");
                return;
            }

            // Both halves of the stamp must still be CALLED, not merely defined. A private
            // method nobody invokes compiles green and prints nothing -- which is exactly how
            // an unregistered oracle sat dormant in this folder for two weeks.
            foreach (string call in new[] { "ReportCaptureProvenance()", "ReportCaptureStamp(" })
            {
                if (src.IndexOf(call, StringComparison.Ordinal) < 0)
                    failures.Add(Tag + " FAIL " + CaptureFileRelative + " defines the provenance " +
                                 "markers but never calls " + call + " -- a stamp that is not " +
                                 "invoked stamps nothing.");
            }

            notes.Append("emit pinned in ").Append(CaptureFileRelative).Append("; ");
        }

        // 5. CLAUDE.md 8: one marker per entry point, never a shared string. A 22-case suite's
        //    pass once read as the full suite's pass because two entry points printed the same
        //    marker. Guard the same failure here.
        private static void CheckMarkerIsDistinct(List<string> failures)
        {
            if (CaptureProvenance.FailMarker.StartsWith(CaptureProvenance.HeadMarker, StringComparison.Ordinal))
                failures.Add(Tag + " FAIL the failure marker '" + CaptureProvenance.FailMarker +
                             "' starts with the success marker '" + CaptureProvenance.HeadMarker +
                             "' -- a grep for the good marker would match the failure line.");

            if (CaptureProvenance.StampMarker.StartsWith(CaptureProvenance.HeadMarker, StringComparison.Ordinal))
                failures.Add(Tag + " FAIL the totals marker '" + CaptureProvenance.StampMarker +
                             "' starts with the head marker -- the two would grep as one.");
        }

        // 6. ADVISORY ONLY (never a failure): a fresh clone has no capture log, and a stale one
        //    predates this WO. Reported so a reader can see at a glance whether the log on disk
        //    is citable. Judge the gate by a FRESH log, never by this note.
        private static void NoteLiveCaptureLog(StringBuilder notes)
        {
            try
            {
                string log = Path.Combine(CaptureProvenance.RepoRoot(),
                    "Builds" + Path.DirectorySeparatorChar + "ui-capture.log");
                if (!File.Exists(log)) { notes.Append("no Builds/ui-capture.log on disk; "); return; }

                foreach (string line in File.ReadAllLines(log))
                {
                    string s, b;
                    bool d;
                    if (CaptureProvenance.TryParseHeadLine(line, out s, out b, out d))
                    {
                        notes.Append("Builds/ui-capture.log cites ").Append(s.Substring(0, 12))
                             .Append(" dirty=").Append(d ? "true" : "false").Append("; ");
                        return;
                    }
                }
                notes.Append("Builds/ui-capture.log carries NO parseable ")
                     .Append(CaptureProvenance.HeadMarker)
                     .Append(" -- it predates WO-1080 and must not be cited; ");
            }
            catch (Exception e)
            {
                notes.Append("capture-log note skipped (").Append(e.GetType().Name).Append("); ");
            }
        }
    }
}
