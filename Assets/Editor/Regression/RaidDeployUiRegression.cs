// =============================================================================
// RaidDeployUiRegression — WO-839 contract pins (Raid Deploy screen cleanup).
// -----------------------------------------------------------------------------
// Source-lint + pure-VM oracle (DataRegression family: headless, never throws).
// Pins the three contracts WO-839 established:
//   1. KIT ZONE CONTRACT — ElarionUiKit.ZonesFor's FrameCore case declares an
//      EXPLICIT footer band (z.footer) and a sub-header band (z.subHeader).
//      ROOT CAUSE pinned: FrameCore previously INHERITED the thin default footer;
//      the sweep-9413 relocation kept its ~0.065 height, too thin for the
//      MinTouchPx=112 floor, so ClampMinTouch grew footer CTAs past the band into
//      the shared Close (the owner's Raid Deploy bottom-row overlap).
//   2. DEV-GUARD CONTRACT — BreakCaptureHarness's IMGUI note-entry box ("What
//      looks wrong?") and its freeze entry (FlagHere note flow) sit INSIDE
//      #if UNITY_EDITOR || DEVELOPMENT_BUILD, so the capture field can never
//      render (nor timeScale-0 softlock) on a non-development player build.
//   3. VM CONTRACT — RaidDeployVM.ScoutReport is never null/empty, reports honest
//      config facts (walls/gates/garrison/boss), and NEVER surfaces
//      rewardMultiplier / shardDropChance (cosmetic-only fields the loot math
//      ignores — RAID_BATTLEFIELD_ANATOMY_2026-08-02: showing them would lie).
//
// REGISTRATION: not yet wired into DataRegression.RunAll (that file is the
// sole-committer's lane). Wire there as:
//   if (!RaidDeployUiRegression.Run(out var raidDeployUiReason))
//       failures.Add(raidDeployUiReason);
//   else log.AppendLine("[raid-deploy-ui] " + raidDeployUiReason);
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DeNelle.Village;
using DeNelle.Village.Hero;

namespace DeNelle.Editor
{
    public static class RaidDeployUiRegression
    {
        /// <summary>Runs all WO-839 contract pins. True when green; reason always says why.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                CheckFrameCoreZones(failures, notes);
                CheckCaptureFieldDevGuard(failures, notes);
                CheckScoutReportContract(failures, notes);
            }
            catch (Exception ex)
            {
                failures.Add("RAID-DEPLOY-UI oracle threw: " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "RAID-DEPLOY-UI OK — " + string.Join("; ", notes.ToArray());
                return true;
            }
            reason = "RAID-DEPLOY-UI VIOLATION x" + failures.Count + " — " + string.Join(" | ", failures.ToArray());
            return false;
        }

        // ── 1. KIT ZONE CONTRACT (source-lint of ElarionUiKit.ZonesFor) ─────────
        static void CheckFrameCoreZones(List<string> failures, List<string> notes)
        {
            int before = failures.Count;
            string path = Path.Combine(Application.dataPath, "_Modules/Core/UI/ElarionUiKit.cs");
            if (!File.Exists(path))
            {
                failures.Add("ElarionUiKit.cs not found at " + path);
                return;
            }
            string text;
            try { text = File.ReadAllText(path); }
            catch (Exception ex) { failures.Add("ElarionUiKit.cs unreadable (" + ex.Message + ")"); return; }

            int start = text.IndexOf("case RpgUiCatalog.FrameCore:", StringComparison.Ordinal);
            if (start < 0)
            {
                failures.Add("ElarionUiKit.ZonesFor has no FrameCore case (frame renamed? update this pin)");
                return;
            }
            int end = text.IndexOf("break;", start, StringComparison.Ordinal);
            if (end < 0) end = Math.Min(start + 4000, text.Length);
            string block = text.Substring(start, end - start);

            if (block.IndexOf("z.footer", StringComparison.Ordinal) < 0)
                failures.Add("FrameCore case no longer sets an EXPLICIT z.footer — WO-839 root cause regressed " +
                             "(inherited thin default footer + ClampMinTouch spills footer CTAs over the shared Close)");
            if (block.IndexOf("z.subHeader", StringComparison.Ordinal) < 0)
                failures.Add("FrameCore case no longer sets z.subHeader — WO-839 #1 regressed " +
                             "(badge/stars/target meta row stacks back into the body top)");

            if (failures.Count == before)
                notes.Add("FrameCore declares explicit footer + subHeader zones");
        }

        // ── 2. DEV-GUARD CONTRACT (source-lint of BreakCaptureHarness) ──────────
        const string DevGuard = "#if UNITY_EDITOR || DEVELOPMENT_BUILD";

        static void CheckCaptureFieldDevGuard(List<string> failures, List<string> notes)
        {
            int before = failures.Count;
            string path = Path.Combine(Application.dataPath, "_Modules/Core/Diagnostics/BreakCaptureHarness.cs");
            if (!File.Exists(path))
            {
                failures.Add("BreakCaptureHarness.cs not found at " + path);
                return;
            }
            string text;
            try { text = File.ReadAllText(path); }
            catch (Exception ex) { failures.Add("BreakCaptureHarness.cs unreadable (" + ex.Message + ")"); return; }

            // The IMGUI note box must sit inside the dev guard within OnGUI.
            // Search the phrase FROM OnGUI onward — the harness quotes "What looks
            // wrong?" in explanatory comments near the file top, and a plain first-
            // IndexOf matched those, flagging a guarded box as unguarded (the false
            // positive this suite fired on 2026-08-02).
            int onGui = text.IndexOf("void OnGUI()", StringComparison.Ordinal);
            int noteBox = onGui < 0 ? -1 : text.IndexOf("What looks wrong?", onGui, StringComparison.Ordinal);
            if (onGui < 0)
            {
                failures.Add("BreakCaptureHarness has no OnGUI() (harness rewritten? update this pin)");
            }
            else if (noteBox < 0)
            {
                // Note flow removed entirely = also safe; record it.
                notes.Add("note-entry box absent from harness (removed — trivially guarded)");
            }
            else
            {
                int guard = text.IndexOf(DevGuard, onGui, StringComparison.Ordinal);
                if (guard < 0 || guard > noteBox)
                    failures.Add("the 'What looks wrong?' note-entry box is OUTSIDE " + DevGuard +
                                 " in OnGUI — the dev capture field would render on a RELEASE player build (WO-839 §3)");
            }

            // The freeze ENTRY must be guarded too, or a release F8 would softlock at
            // timeScale 0 with the note box compiled out.
            int flagHere = text.IndexOf("void FlagHere()", StringComparison.Ordinal);
            int noteModeOn = flagHere < 0 ? -1 : text.IndexOf("_noteMode = true", flagHere, StringComparison.Ordinal);
            if (flagHere >= 0 && noteModeOn >= 0)
            {
                int guard = text.IndexOf(DevGuard, flagHere, StringComparison.Ordinal);
                if (guard < 0 || guard > noteModeOn)
                    failures.Add("FlagHere's note-mode/freeze entry is not dev-guarded — a release F8 would " +
                                 "freeze at timeScale 0 with no note box to commit out (WO-839 §3)");
            }

            if (failures.Count == before && noteBox >= 0)
                notes.Add("capture note field + freeze entry are dev-guarded");
        }

        // ── 3. VM CONTRACT (pure C# — RaidDeployVM.ScoutReport) ─────────────────
        static void CheckScoutReportContract(List<string> failures, List<string> notes)
        {
            int before = failures.Count;

            var def = new SceneConfigDef
            {
                id = "regression_raid",
                displayName = "Regression Raid",
                sceneName = "RaidBase_regression",
                wallTier = "ReinforcedSteel",
                entranceCount = 2,
                garrison = new GarrisonDef
                {
                    composition = new List<GarrisonUnitDef>
                    {
                        new GarrisonUnitDef { enemyId = "orc-berserker", count = 4 },
                        new GarrisonUnitDef { enemyId = "shaman", count = 2 },
                    },
                    boss = "necromancer",
                },
                rewardMultiplier = 2.2f,
                shardDropChance = 0.2f,
            };

            var vm = new RaidDeployVM(def, null, null, null, null);
            try
            {
                var report = vm.ScoutReport;
                if (report == null || report.Count == 0)
                {
                    failures.Add("ScoutReport is null/empty for a fully-specified def");
                }
                else
                {
                    bool walls = false, garrison = false, boss = false;
                    foreach (var line in report)
                    {
                        if (line == null) { failures.Add("ScoutReport contains a null line"); continue; }
                        if (line.Contains("Reinforced Steel") && line.Contains("2 gates")) walls = true;
                        if (line == "Garrison: 6 defenders") garrison = true;
                        if (line == "Boss: Necromancer") boss = true;
                        string lower = line.ToLowerInvariant();
                        // The anatomy-doc lie guard: these config fields are COSMETIC (the
                        // loot math never applies them) — surfacing them on screen is a lie.
                        if (lower.Contains("reward") || lower.Contains("shard"))
                            failures.Add("ScoutReport surfaces a cosmetic-only reward field (lie on screen): '" + line + "'");
                    }
                    if (!walls) failures.Add("ScoutReport missing the walls line ('Reinforced Steel', '2 gates'); got: " + Join(report));
                    if (!garrison) failures.Add("ScoutReport missing 'Garrison: 6 defenders'; got: " + Join(report));
                    if (!boss) failures.Add("ScoutReport missing 'Boss: Necromancer'; got: " + Join(report));
                }
            }
            finally { vm.Dispose(); }

            var emptyVm = new RaidDeployVM(null, null, null, null, null);
            try
            {
                var report = emptyVm.ScoutReport;
                if (report == null || report.Count != 1 || report[0] != "No scout intel available.")
                    failures.Add("null-def ScoutReport must be exactly ['No scout intel available.']; got: " +
                                 (report == null ? "<null>" : Join(report)));
                if (emptyVm.CanDeploy)
                    failures.Add("null-def VM reports CanDeploy=true (deploy contract regressed)");
            }
            finally { emptyVm.Dispose(); }

            if (failures.Count == before)
                notes.Add("ScoutReport contract holds (walls/garrison/boss honest, no cosmetic reward fields, safe null-def fallback)");
        }

        static string Join(IReadOnlyList<string> lines)
        {
            var arr = new string[lines.Count];
            for (int i = 0; i < lines.Count; i++) arr[i] = lines[i] ?? "<null>";
            return "[" + string.Join(" / ", arr) + "]";
        }
    }
}
