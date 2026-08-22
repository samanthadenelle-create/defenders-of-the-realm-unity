// =============================================================================
// SiegeSpawnAuthorityRegression — [siege-spawn-authority] (WO-1026).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   Registered ONCE in DataRegression.RunAll.
//
// A SOURCE-SCANNING lint (precedent: HubSceneLiteralRegression, BannedVfxRegression).
// It exists because the failure it guards is INVISIBLE AT RUNTIME until it has already
// cost a week: two systems that both attack the player's town look fine in isolation and
// drift apart forever. CLAUDE.md's whole §5/§16 tone is about exactly this class of
// duplicated authority. So the rule is enforced by the gate, not by memory.
//
// FOUR RULES:
//
//  1. ⛔ THE SIEGE SPAWNS NOTHING. No Instantiate, no SpawnEnemyForExternalMode, in any
//     Siege*.cs or Core/Defense file. WaveManager already owns "hostiles attack the
//     player's town" and is the ONLY thing that does; the siege is a scheduler + a
//     recorder that asks WaveManager to begin a wave. One attacker, forever.
//
//  2. ⛔ NO SECOND WRITER ON THE OFFLINE CLOCK. SiegeScheduler.cs must not contain
//     LastHarvestClaimMs. The OfflineClaimCoordinator owns it; WO-1147 recorded that a
//     second writer made offline Echo repair never accrue once.
//
//  3. ⛔ THE PANEL NEVER RE-SCANS THE LIVE TOWN. DefenseReportPanel.cs must not mention
//     WaveDamageReport. It renders the PERSISTED record. A panel that re-scanned the
//     scene could not render last week's report (the town has changed) and could not
//     render a model-(c) ghost's report AT ALL — which quietly turns the (c) source swap
//     back into a rewrite. This is the least obvious of the four and the most load-bearing.
//
//  4. ⛔ EXACTLY ONE FILE WRITES AttackerSource.GeneratedPve — DefenseReportBuilder.cs.
//     That is the "do not hardcode that the attacker is generated" ruling made
//     enforceable: every reader branches on the record's Source field or on nothing, so
//     the day a ghost producer exists it writes GhostSnapshot in that one place and
//     NOTHING downstream changes.
//
// ZERO TARGETS IS A FAILURE, NOT A PASS. A lint that scanned nothing and reported OK is
// worse than no lint (HubSceneLiteralRegression makes the same call for the same reason):
// it converts a missing guard into a green marker.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>Duplicate-authority lint for the WO-1026 siege lane.</summary>
    public static class SiegeSpawnAuthorityRegression
    {
        private const string SelfFileName = "SiegeSpawnAuthorityRegression.cs";

        /// <summary>Files that must contain no spawn call. Relative to the project root.</summary>
        private static readonly string[] SpawnFreeFiles =
        {
            "Assets/_Modules/Village/Waves/SiegeSession.cs",
            "Assets/_Modules/Village/Waves/SiegeScheduler.cs",
            // NOT in Village/Waves on purpose: the siege's wall-clock reads live outside the
            // combat firewall's swept tree (DevTimeSkipRegression case6). It is still a siege
            // file and still spawns nothing, so the guard follows it here.
            "Assets/_Modules/Village/Siege/SiegeClock.cs",
            "Assets/_Modules/Village/Waves/SiegeSchedulerBootstrap.cs",
            "Assets/_Modules/Village/Waves/DefenseReportBuilder.cs",
            "Assets/_Modules/Village/Waves/StructureVitalsWatch.cs",
            "Assets/_Modules/Core/Defense/DefenseReport.cs",
            "Assets/_Modules/Core/Defense/DefenseReportLedger.cs",
            "Assets/_Modules/Core/UI/DefenseMapPlate.cs",
        };

        /// <summary>Every file allowed to write the siege's OWN cadence clock. None of them may
        /// touch the harvest clock. SiegeClock.cs is listed because the cadence writes MOVED there
        /// (combat firewall) — a guard that stayed pointed only at the scheduler would have been
        /// silently disarmed by that move.</summary>
        private static readonly string[] ClockWriterFiles =
        {
            "Assets/_Modules/Village/Waves/SiegeScheduler.cs",
            "Assets/_Modules/Village/Siege/SiegeClock.cs",
        };
        /// <summary>Presentation surfaces. NONE of them may re-scan the live town — they render
        /// the persisted record, which is what keeps the model-(c) swap a swap.</summary>
        private static readonly string[] RecordOnlyPresentationFiles =
        {
            "Assets/_Modules/Village/UI/Defense/DefenseReportPanel.cs",
            "Assets/_Modules/Core/UI/DefenseMapPlate.cs",
        };
        private const string BuilderFile = "Assets/_Modules/Village/Waves/DefenseReportBuilder.cs";

        /// <summary>Roots scanned for rule 4 (who writes GeneratedPve).</summary>
        private static readonly string[] ScanRoots = { "Assets/_Modules", "Assets/Editor" };

        public static bool Run(out string reason)
        {
            try { return RunCore(out reason); }
            catch (Exception ex)
            {
                reason = "siege-spawn-authority: oracle THREW " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static bool RunCore(out string reason)
        {
            var failures = new List<string>();

            string root = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(root))
            {
                reason = "siege-spawn-authority: could not resolve the project root from Application.dataPath -- " +
                         "the scan cannot run, so this is a FAILURE, not a skip";
                return false;
            }

            // ── Rule 1: the siege spawns nothing ─────────────────────────────────
            int checkedFiles = 0;
            for (int i = 0; i < SpawnFreeFiles.Length; i++)
            {
                string path = Abs(root, SpawnFreeFiles[i]);
                if (!File.Exists(path))
                {
                    failures.Add($"'{SpawnFreeFiles[i]}' is MISSING -- this lint is no longer looking at it. " +
                                 "A moved file silently disables the guard; re-point SpawnFreeFiles.");
                    continue;
                }
                checkedFiles++;
                string code = StripComments(ReadOrEmpty(path));

                if (code.Contains("Instantiate("))
                    failures.Add($"'{SpawnFreeFiles[i]}' contains Instantiate( -- THE SIEGE MUST NOT SPAWN. " +
                                 "WaveManager is the single town-attack authority; the siege schedules and " +
                                 "records, it never attacks. Two attackers drift apart (CLAUDE.md §5/§16).");
                if (code.Contains("SpawnEnemyForExternalMode"))
                    failures.Add($"'{SpawnFreeFiles[i]}' calls SpawnEnemyForExternalMode -- that is the FTUE's " +
                                 "per-enemy seam. The siege's ONLY sanctioned WaveManager call is " +
                                 "ForceBeginNextWave().");
            }
            if (checkedFiles == 0)
            {
                reason = "siege-spawn-authority: scanned 0 of " + SpawnFreeFiles.Length + " target files -- " +
                         "zero targets is a FAILURE, not a pass (a green marker over a missing guard).";
                return false;
            }

            // ── Rule 2: no second writer on the offline clock ────────────────────
            for (int i = 0; i < ClockWriterFiles.Length; i++)
            {
                string rel = ClockWriterFiles[i];
                string schedPath = Abs(root, rel);
                if (!File.Exists(schedPath))
                {
                    failures.Add($"'{rel}' is MISSING -- the WO-1147 clock guard cannot run.");
                    continue;
                }
                if (StripComments(ReadOrEmpty(schedPath)).Contains("LastHarvestClaimMs"))
                    failures.Add($"'{rel}' references LastHarvestClaimMs -- the OfflineClaimCoordinator " +
                                 "OWNS that clock (IOfflineClaimConsumer says consumers must not touch it). A second " +
                                 "writer is the WO-1147 bug: the frame-order coin-flip that made offline Echo repair " +
                                 "never accrue once. The siege has its OWN clock, GameState.LastSiegeUnixMs.");
            }

            // ── Rule 3: no presentation surface re-scans the live town ──────────
            for (int i = 0; i < RecordOnlyPresentationFiles.Length; i++)
            {
                string rel = RecordOnlyPresentationFiles[i];
                string path = Abs(root, rel);
                if (!File.Exists(path))
                {
                    failures.Add($"'{rel}' is MISSING -- the (c)-swap presentation guard cannot run.");
                    continue;
                }
                string code = StripComments(ReadOrEmpty(path));
                if (code.Contains("WaveDamageReport"))
                    failures.Add($"'{rel}' references WaveDamageReport -- a report surface must render the " +
                                 "PERSISTED record only. One that re-scans the live town cannot render an old " +
                                 "report (the town has changed) and cannot render a ghost's report at all, " +
                                 "which turns the model-(c) source swap back into a rewrite.");
                if (code.Contains("FindObjectsByType") || code.Contains("FindFirstObjectByType"))
                    failures.Add($"'{rel}' scans the scene (FindObjectsByType/FindFirstObjectByType) -- the same " +
                                 "rule, and the sharper version of it: a report surface must read NOTHING but " +
                                 "the record it was handed.");
            }

            // ── Rule 4: exactly ONE file writes GeneratedPve ─────────────────────
            var writers = new List<string>();
            int scanned = 0;
            for (int r = 0; r < ScanRoots.Length; r++)
            {
                string dir = Abs(root, ScanRoots[r]);
                if (!Directory.Exists(dir))
                {
                    failures.Add($"scan root '{ScanRoots[r]}' does not exist -- this lint is no longer looking at it.");
                    continue;
                }
                foreach (var path in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                {
                    string name = Path.GetFileName(path);
                    if (string.Equals(name, SelfFileName, StringComparison.OrdinalIgnoreCase)) continue;
                    // The contract oracle's FIXTURES legitimately name the value (it builds a
                    // GeneratedPve record and a GhostSnapshot one to prove they round-trip
                    // identically). Only a PRODUCER assignment is the thing being guarded.
                    // NOTE: DefenseReport.cs is deliberately NOT excluded. It used to set
                    // Source = GeneratedPve inside NewEmpty(); that write was REMOVED rather
                    // than whitelisted, because a whitelist here would have quietly permitted
                    // the exact second hardcoding this rule exists to prevent.
                    if (string.Equals(name, "DefenseReportContractRegression.cs", StringComparison.OrdinalIgnoreCase)) continue;
                    if (string.Equals(name, "DefenseReportPanel.cs", StringComparison.OrdinalIgnoreCase)) continue;   // reads the enum for a label chip only
                    scanned++;
                    string code = StripComments(ReadOrEmpty(path));
                    if (code.Contains("AttackerSource.GeneratedPve"))
                        writers.Add(Rel(root, path));
                }
            }
            if (scanned == 0)
            {
                reason = "siege-spawn-authority: rule 4 scanned 0 .cs files -- zero targets is a FAILURE.";
                return false;
            }
            if (writers.Count == 0)
                failures.Add("NOTHING writes AttackerSource.GeneratedPve -- the model-(a) producer is gone, so " +
                             "every report would carry the default source by accident rather than by decision.");
            else if (writers.Count > 1 || !writers[0].Replace('\\', '/').EndsWith("DefenseReportBuilder.cs"))
                failures.Add($"AttackerSource.GeneratedPve is written by {writers.Count} file(s) " +
                             $"[{string.Join(", ", writers)}] -- it must be written ONLY by DefenseReportBuilder.cs. " +
                             "A second writer is 'the attacker is generated' being hardcoded in a second place, " +
                             "which is exactly what makes the model-(c) source swap stop being a swap.");

            if (failures.Count == 0)
            {
                reason = $"SIEGE SPAWN AUTHORITY OK -- {checkedFiles} siege file(s) contain no spawn call " +
                         "(WaveManager stays the single town-attack authority); the scheduler never writes " +
                         "LastHarvestClaimMs; the panel never reads WaveDamageReport; exactly one file " +
                         "(DefenseReportBuilder.cs) writes AttackerSource.GeneratedPve";
                return true;
            }
            reason = $"SIEGE SPAWN AUTHORITY FAIL x{failures.Count}: " + string.Join(" | ", failures);
            return false;
        }

        private static string Abs(string root, string rel)
            => Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));

        private static string Rel(string root, string abs)
        {
            string r = abs.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                ? abs.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, '/')
                : abs;
            return r.Replace('\\', '/');
        }

        private static string ReadOrEmpty(string path)
        {
            try { return File.ReadAllText(path); } catch { return string.Empty; }
        }

        /// <summary>
        /// Strips // and /* */ so a HEADER that names a banned token (every one of these files
        /// explains the ban in its own comments) does not trip the lint it is documenting.
        /// Newlines inside block comments are preserved so line numbers do not drift.
        /// Mirrors HubSceneLiteralRegression.StripComments.
        /// </summary>
        private static string StripComments(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;
            var sb = new StringBuilder(src.Length);
            for (int i = 0; i < src.Length; i++)
            {
                if (i + 1 < src.Length && src[i] == '/' && src[i + 1] == '*')
                {
                    int end = src.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    if (end < 0) { sb.Append(' '); break; }
                    for (int k = i; k <= end + 1; k++) sb.Append(src[k] == '\n' ? '\n' : ' ');
                    i = end + 1;
                    continue;
                }
                if (i + 1 < src.Length && src[i] == '/' && src[i + 1] == '/')
                {
                    int nl = src.IndexOf('\n', i);
                    sb.Append(' ');
                    if (nl < 0) break;
                    sb.Append('\n');
                    i = nl;
                    continue;
                }
                sb.Append(src[i]);
            }
            return sb.ToString();
        }
    }
}
