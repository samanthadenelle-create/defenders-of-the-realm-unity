// =============================================================================
// SpawnBudgetAndVfxWarmRegression [spawn-budget-vfx-warm]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.
// Markers: SPAWN_BUDGET_VFX_WARM_OK / SPAWN_BUDGET_VFX_WARM_FAIL.
//
// Pins the three WO-1113 MOBILE-PERFORMANCE fixes (target: Solana Seeker phone).
// Each one closes a defect whose whole character was that it looked fine in code
// and cost nothing but frames, memory and evidence on the device:
//
//   CASE 1 - THE CONCURRENCY CAP IS ENFORCED ON THE LIVE SPAWN PATH.
//     WaveManager._maxSimultaneousEnemies (8) was read in exactly ONE place:
//     SpawnBatch, the LEGACY flat path. _smartComposition ships ON, so every wave
//     the player meets came through SpawnSmartComposedWave -> SmartEnemySpawner,
//     which released the WHOLE composition with no cap check at all - up to
//     WaveCompositionBuilder.MaxCount = 22 bodies at once, more in endless. The
//     serialized field promised a ceiling the live path did not have. This case
//     drives the REAL budget arithmetic over the REAL compositions the builder
//     generates and asserts the cap both binds and never drops an enemy, then
//     lints (comment- AND string-stripped) that the live call site still passes a
//     budget + a held-slot sink, and that the wave-clear gate still respects the
//     held count. Deleting any of those restores the defect while compiling.
//
//   CASE 2 - NO PRE-WARM FOR A KEY WITH NO CONSUMER.
//     VFXManager.InitialiseHovlPools instantiated PoolSize bodies for EVERY row
//     in the baked catalog at Awake. The 2026-08-16 VFX catalog audit found 76 of
//     the 152 keys have no consumer anywhere in the tree (45 of them the PP_*
//     palette), so roughly a third of that boot bill bought effects nothing can
//     play. The warm is now DEMAND-DRIVEN: a key builds its authored pool on its
//     first actual play. This case measures the real bill from the real baked
//     asset, proves unconsumed keys genuinely exist (so it can never pass hollow
//     on an empty catalog), and lints that boot warming stays behind the
//     rollback-only eager flag while the play path still warms on demand.
//     NOTE: no key, row or prefab is deleted - an untagged key today may be
//     owner-tagged tomorrow, and deleting owner-tagged art is not a CLI call.
//
//   CASE 3 - AN OFF-NAVMESH SPAWN ON THE LIVE PATH IS SNAPPED AND TRACED.
//     SmartEnemySpawner left `pos` at the raw marker on a NavMesh.SamplePosition
//     miss and said nothing, while the legacy WaveManager.SpawnOne had been fixed
//     for that exact miss (WO-430: warn + ground-snap) and VerifySpawnedEnemy
//     warned on !agent.isOnNavMesh. The path the player actually meets had
//     NEITHER - which is how a wave stalls on stranded enemies with no evidence
//     in the break-log. This case lints that the live path now ground-snaps on a
//     miss and reports it through FlowTrace (a bare Debug.LogWarning is invisible
//     to the F8 harness, so it does not count).
//
// SOURCE-LINT DISCIPLINE: every lint runs on source with comments AND string
// literals removed, so no prose above (or in the files under test) can satisfy a
// check. The ONE place raw source is read is the VFX consumer scan in case 2,
// where the key literal in a PlayKey("...") call IS the evidence being counted.
//
// Standalone: run-unity-method
//   -Method DeNelle.Editor.Regression.SpawnBudgetAndVfxWarmRegression.RunAll
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class SpawnBudgetAndVfxWarmRegression
    {
        private const string WaveManagerSourcePath =
            "Assets/_Modules/Village/Waves/WaveManager.cs";
        private const string SmartSpawnerSourcePath =
            "Assets/_Modules/Village/Waves/SmartEnemySpawner.cs";
        private const string HovlSourcePath =
            "Assets/_Modules/Village/Vfx/VFXManager.Hovl.cs";

        // The cap the field ships with. The oracle asserts the ARITHMETIC, not this
        // number - retuning the cap is an owner decision and must not fail the gate.
        private const int SampleCap = 8;

        public static void RunAll()
        {
            bool ok = Run(out string reason);
            if (ok) Debug.Log("SPAWN_BUDGET_VFX_WARM_OK\n" + reason);
            else    Debug.LogError("SPAWN_BUDGET_VFX_WARM_FAIL\n" + reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes    = new List<string>();

            DeNelle.Core.Diagnostics.Guard.Try("Regression", "spawn-budget case 1",
                () => Case1_CapEnforcedOnLivePath(failures, notes));
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "spawn-budget case 2",
                () => Case2_NoPrewarmForUnconsumedKeys(failures, notes));
            DeNelle.Core.Diagnostics.Guard.Try("Regression", "spawn-budget case 3",
                () => Case3_OffNavMeshSnappedAndTraced(failures, notes));

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
        //  CASE 1 - the cap is real on the path the player actually plays
        // =====================================================================
        private static void Case1_CapEnforcedOnLivePath(List<string> failures, List<string> notes)
        {
            // --- 1a: the PURE budget arithmetic, on the REAL compositions --------
            // Drives BudgetFor / ReleaseCountFor exactly as SpawnWave does: release
            // into the budget, hold the rest. Two invariants, on every authored wave:
            //   * the first release NEVER exceeds the cap  (the cap is real), and
            //   * released + held == the composition total (the cap never THINS).
            int wavesExaminedBinding = 0, maxTotal = 0;
            for (int waveId = 1; waveId <= 30; waveId++)
            {
                EnemyWaveComposition comp =
                    WaveCompositionBuilder.Build(waveId, false, null);
                if (comp == null || comp.Entries.Count == 0)
                {
                    failures.Add("[case1] WaveCompositionBuilder.Build(" + waveId +
                                 ") produced an EMPTY composition - this case cannot assert " +
                                 "a cap over nothing.");
                    return;
                }

                int total = comp.TotalCount;
                if (total > maxTotal) maxTotal = total;

                int budget   = SmartEnemySpawner.BudgetFor(SampleCap, 0);
                int released = 0, held = 0;
                for (int i = 0; i < comp.Entries.Count; i++)
                {
                    int slot = comp.Entries[i].Count;
                    int rel  = SmartEnemySpawner.ReleaseCountFor(slot, budget);
                    released += rel;
                    held     += slot - rel;
                    budget   -= rel;
                }

                if (released > SampleCap)
                    failures.Add("[case1] wave " + waveId + ": first release was " + released +
                                 " with a cap of " + SampleCap + " - the cap is NOT enforced.");
                if (released + held != total)
                    failures.Add("[case1] wave " + waveId + ": released " + released + " + held " +
                                 held + " != roster " + total + " - the cap DROPPED enemies " +
                                 "(it must hold them, never thin the wave).");
                if (total > SampleCap)
                {
                    wavesExaminedBinding++;
                    if (held <= 0)
                        failures.Add("[case1] wave " + waveId + ": roster " + total +
                                     " exceeds the cap " + SampleCap + " yet nothing was held.");
                }
            }

            if (wavesExaminedBinding == 0)
                failures.Add("[case1] NO generated wave in 1..30 exceeded the sample cap " +
                             SampleCap + " (max roster seen " + maxTotal + ") - the cap would " +
                             "never bind, so this case would be asserting nothing.");
            else
                notes.Add("[case1] budget arithmetic held on 30 real compositions (" +
                          wavesExaminedBinding + " exceed cap " + SampleCap +
                          ", max roster " + maxTotal + ")");

            // Uncapped sentinel: 0 means "no cap", not "no room".
            if (SmartEnemySpawner.BudgetFor(0, 5) != 0)
                failures.Add("[case1] BudgetFor(0, live) must return 0 (the no-cap sentinel).");
            if (SmartEnemySpawner.BudgetFor(SampleCap, SampleCap + 4) != 0)
                failures.Add("[case1] BudgetFor over capacity must clamp to 0, never negative.");
            if (SmartEnemySpawner.ReleaseCountFor(6, int.MaxValue) != 6)
                failures.Add("[case1] ReleaseCountFor with an unlimited budget must release the " +
                             "whole slot (the uncapped path must be unchanged).");

            // --- 1b: the LIVE call site still carries the budget ----------------
            string wm = ReadStripped(WaveManagerSourcePath, out string wmErr);
            if (wm == null)
            {
                failures.Add("[case1] cannot read " + WaveManagerSourcePath + " (" + wmErr + ")");
            }
            else
            {
                RequireAll(failures, "case1", wm, WaveManagerSourcePath, new[]
                {
                    "SmartSpawnBudget",              // the budget exists
                    "_maxSimultaneousEnemies",       // and is derived from the serialized cap
                    "DrainSmartReinforcements",      // held enemies are released later
                    "_heldSmartReinforcements",      // and the clear gate knows about them
                });

                // The cap must reach the SMART call, not only SpawnBatch.
                int smartCall = wm.IndexOf("_smartSpawner.SpawnWave", StringComparison.Ordinal);
                if (smartCall < 0)
                {
                    failures.Add("[case1] WaveManager no longer calls _smartSpawner.SpawnWave - " +
                                 "the live path moved; re-point this oracle before trusting it.");
                }
                else
                {
                    int close = wm.IndexOf(';', smartCall);
                    string call = close > smartCall ? wm.Substring(smartCall, close - smartCall) : "";
                    if (call.IndexOf("budget", StringComparison.Ordinal) < 0 ||
                        call.IndexOf("deferred", StringComparison.Ordinal) < 0)
                        failures.Add("[case1] the live SmartEnemySpawner.SpawnWave call passes no " +
                                     "budget and/or no held-slot sink - the cap is dead on the " +
                                     "path the player plays (the exact WO-1113 defect).");
                }
            }

            // --- 1c: the spawner honours the budget instead of ignoring it -------
            string ss = ReadStripped(SmartSpawnerSourcePath, out string ssErr);
            if (ss == null)
            {
                failures.Add("[case1] cannot read " + SmartSpawnerSourcePath + " (" + ssErr + ")");
            }
            else
            {
                RequireAll(failures, "case1", ss, SmartSpawnerSourcePath, new[]
                {
                    "maxToSpawn",
                    "ReleaseCountFor",
                    "deferred.Add",
                });
            }
        }

        // =====================================================================
        //  CASE 2 - unconsumed keys cost nothing at boot
        // =====================================================================
        private static void Case2_NoPrewarmForUnconsumedKeys(List<string> failures, List<string> notes)
        {
            // --- 2a: measure the REAL bill from the REAL baked catalog ----------
            var catalog = Resources.Load<HovlVfxCatalog>("VFX/HovlVfxCatalog");
            if (catalog == null || catalog.Rows == null || catalog.Rows.Length == 0)
            {
                failures.Add("[case2] Resources/VFX/HovlVfxCatalog did not load (or has zero rows) - " +
                             "the pre-warm bill cannot be measured, and a pass here would be hollow.");
            }
            else
            {
                string[] sources = SafeEnumerateCs("Assets");
                if (sources.Length == 0)
                {
                    failures.Add("[case2] found ZERO .cs files under Assets - the consumer scan " +
                                 "cannot run, so every key would read as unconsumed.");
                }
                else
                {
                    // A key is CONSUMED when its literal text appears in any .cs other than the
                    // catalog generator/manager plumbing. Raw text on purpose: the evidence of a
                    // consumer is the key string inside a PlayKey("...") call.
                    var haystack = new StringBuilder();
                    foreach (string f in sources)
                    {
                        // Skip the files whose job is to ENUMERATE keys - they name every key
                        // and would make the whole catalog read as consumed.
                        string lower = f.Replace('\\', '/').ToLowerInvariant();
                        if (lower.EndsWith("/hovlvfxcatalog.cs")) continue;
                        if (lower.Contains("hovlvfxcataloggenerator")) continue;
                        if (lower.Contains("vfxcasterwindow")) continue;
                        try { haystack.Append(File.ReadAllText(f)).Append('\n'); }
                        catch (Exception) { /* unreadable file: counted as no consumer */ }
                    }
                    string all = haystack.ToString();

                    int rows = 0, eagerInstances = 0, unconsumed = 0, unconsumedInstances = 0;
                    foreach (var row in catalog.Rows)
                    {
                        if (string.IsNullOrEmpty(row.Key) || row.Prefab == null || row.PoolSize <= 0)
                            continue;
                        rows++;
                        eagerInstances += row.PoolSize;
                        if (all.IndexOf("\"" + row.Key + "\"", StringComparison.Ordinal) < 0)
                        {
                            unconsumed++;
                            unconsumedInstances += row.PoolSize;
                        }
                    }

                    if (rows == 0)
                        failures.Add("[case2] no catalog row has both a prefab and a PoolSize - " +
                                     "nothing to measure.");
                    else if (unconsumed == 0)
                        failures.Add("[case2] every one of the " + rows + " catalog keys resolved a " +
                                     "consumer. That contradicts the 2026-08-16 VFX audit; either the " +
                                     "scan is broken or the catalog changed - do not trust a green " +
                                     "here until it is re-verified.");
                    else
                        notes.Add("[case2] eager warm would build " + eagerInstances +
                                  " pooled instances across " + rows + " keys; " + unconsumed +
                                  " keys (" + unconsumedInstances + " instances) have NO consumer " +
                                  "and now cost 0 at boot");
                }
            }

            // --- 2b: boot warming stays behind the rollback-only flag ----------
            string hovl = ReadStripped(HovlSourcePath, out string hovlErr);
            if (hovl == null)
            {
                failures.Add("[case2] cannot read " + HovlSourcePath + " (" + hovlErr + ")");
                return;
            }

            if (hovl.IndexOf("EnsureHovlKeyWarm", StringComparison.Ordinal) < 0)
            {
                failures.Add("[case2] EnsureHovlKeyWarm is gone - a key's pool is no longer warmed " +
                             "on demand.");
            }
            else
            {
                // The demand warm must be reached from the PLAY path, or nothing warms at all.
                int play = hovl.IndexOf("PlayKeyInternal", StringComparison.Ordinal);
                int callInPlay = play < 0 ? -1
                    : hovl.IndexOf("EnsureHovlKeyWarm(", play, StringComparison.Ordinal);
                if (callInPlay < 0)
                    failures.Add("[case2] PlayKeyInternal does not call EnsureHovlKeyWarm - consumed " +
                                 "keys would never build their pool depth.");
            }

            // The boot method must consult the eager flag BEFORE it can instantiate anything.
            int init = hovl.IndexOf("void InitialiseHovlPools", StringComparison.Ordinal);
            if (init < 0)
            {
                failures.Add("[case2] InitialiseHovlPools is gone - re-point this oracle.");
            }
            else
            {
                int flag   = hovl.IndexOf("_eagerWarmAllVfxKeys", init, StringComparison.Ordinal);
                int create = hovl.IndexOf("CreateHovlInstance", init, StringComparison.Ordinal);
                if (flag < 0)
                    failures.Add("[case2] InitialiseHovlPools no longer checks _eagerWarmAllVfxKeys - " +
                                 "boot is pre-warming every key again (WO-1113 defect restored).");
                else if (create >= 0 && create < flag)
                    failures.Add("[case2] InitialiseHovlPools instantiates BEFORE it checks the eager " +
                                 "flag - the boot warm is unconditional again.");
            }

            // The flag itself must DEFAULT to off, or the demand warm ships disabled.
            if (hovl.IndexOf("_eagerWarmAllVfxKeys = false", StringComparison.Ordinal) < 0)
                failures.Add("[case2] _eagerWarmAllVfxKeys does not default to false - the shipping " +
                             "default must be demand-warm; eager is the rollback path.");
        }

        // =====================================================================
        //  CASE 3 - the live path snaps AND reports an off-mesh spawn
        // =====================================================================
        private static void Case3_OffNavMeshSnappedAndTraced(List<string> failures, List<string> notes)
        {
            int failedBefore = failures.Count;

            string ss = ReadStripped(SmartSpawnerSourcePath, out string err);
            if (ss == null)
            {
                failures.Add("[case3] cannot read " + SmartSpawnerSourcePath + " (" + err + ")");
                return;
            }

            int sample = ss.IndexOf("NavMesh.SamplePosition", StringComparison.Ordinal);
            if (sample < 0)
            {
                failures.Add("[case3] the live spawner no longer samples the NavMesh at all - every " +
                             "spawn would sit at its raw marker position.");
                return;
            }

            // The MISS branch: ground-snap raycast + a FlowTrace warning, within the spawn loop
            // that follows the sample. Debug.LogWarning alone does not count - the F8 break-log
            // only captures FlowTrace, which is how this miss stayed invisible for so long.
            string tail = ss.Substring(sample);
            RequireAll(failures, "case3", tail, SmartSpawnerSourcePath, new[]
            {
                "Physics.Raycast",     // ground-snap on the miss
                "FlowTrace.Warn",      // and it is REPORTED where F8 can see it
                "VerifyOnNavMesh",     // post-spawn stranded-agent check
            });

            int verify = ss.IndexOf("VerifyOnNavMesh(Enemy", StringComparison.Ordinal);
            if (verify < 0)
            {
                failures.Add("[case3] VerifyOnNavMesh is not defined in the live spawner - a stranded " +
                             "agent would go unreported (WaveManager.VerifySpawnedEnemy only ever " +
                             "guarded the LEGACY path).");
            }
            else
            {
                string body = ss.Substring(verify);
                if (body.IndexOf("isOnNavMesh", StringComparison.Ordinal) < 0)
                    failures.Add("[case3] VerifyOnNavMesh does not test agent.isOnNavMesh - it cannot " +
                                 "detect the stranded spawn it exists for.");
                if (body.IndexOf("FlowTrace.Warn", StringComparison.Ordinal) < 0)
                    failures.Add("[case3] VerifyOnNavMesh does not report through FlowTrace - an F8 " +
                                 "capture would show a stalled wave with no cause.");
            }

            if (failures.Count == failedBefore)
                notes.Add("[case3] live spawner ground-snaps a NavMesh miss and traces both the miss " +
                          "and any stranded agent through FlowTrace");
        }

        // =====================================================================
        //  Helpers
        // =====================================================================
        private static void RequireAll(
            List<string> failures, string caseTag, string haystack, string path, string[] needles)
        {
            foreach (string n in needles)
                if (haystack.IndexOf(n, StringComparison.Ordinal) < 0)
                    failures.Add("[" + caseTag + "] " + path + " no longer contains '" + n +
                                 "' in live code (comments and string literals stripped).");
        }

        private static string[] SafeEnumerateCs(string root)
        {
            try { return Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories); }
            catch (Exception) { return Array.Empty<string>(); }
        }

        /// <summary>
        /// Reads a .cs file with // and block comments AND the contents of string / char
        /// literals removed, so a lint can only ever match real CODE. Prose that describes a
        /// defect must never be able to satisfy the check that guards it.
        /// Returns null (with a reason) when the file cannot be read.
        /// </summary>
        private static string ReadStripped(string path, out string error)
        {
            error = null;
            string raw;
            try
            {
                if (!File.Exists(path)) { error = "file not found"; return null; }
                raw = File.ReadAllText(path);
            }
            catch (Exception e) { error = e.GetType().Name + ": " + e.Message; return null; }

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
                if (c == '@' && n == '"') { inVerbatim = true; sb.Append("\""); i++; continue; }
                if (c == '$' && n == '"') { inStr = true; sb.Append('"'); i++; continue; }
                if (c == '"') { inStr = true; sb.Append('"'); continue; }
                if (c == '\'') { inChar = true; sb.Append('\''); continue; }
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
