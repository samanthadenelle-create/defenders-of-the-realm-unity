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
//   4. WO-1385 (2026-09-04) BAND CONTRACT [deploy-bands-disjoint] - the right
//      column's three bands (SCOUT / ECHO GUIDE / ENEMY BASE) are literal
//      constants in RaidDeployScreen.cs and stack strictly, with a gap, never
//      intersecting. Owner's Seeker screenshot: the guide block was drawn over
//      the ENEMY BASE tail and CHANGE over the Echo name.
//   5. WO-1385 (2026-09-04) DEPLOY-BAR CONTRACT [deploy-bar-kit-button] - BEGIN
//      ASSAULT is the kit's primary button (BuildObsidianButton, Yellow) and the
//      raw flat "DeployGlow" AddImage slab is gone. Owner, verbatim: "yuck".
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
                CheckDeployBandsDisjoint(failures, notes);
                CheckDeployBarKitButton(failures, notes);
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

        // -- 4. WO-1385 BAND CONTRACT [deploy-bands-disjoint] (source-lint) ------
        // This suite has no headless screen build (source-lint + pure VM only), so the
        // pin reads the literal band constants the View lays the right column from and
        // asserts the geometry that the owner's 2026-09-04 screenshot violated:
        //   0 <= ScoutY0 < ScoutY1 < GuideY0 < GuideY1 < EnemyY0 < EnemyY1 <= 1
        // with a real gap (>= 0.005) between neighbouring bands. RED if any two bands
        // touch or cross (e.g. GuideBandY1 set back to 0.600 above a 0.44 scout top).
        const string DeployScreenRel = "_Modules/Village/Hero/RaidDeployScreen.cs";

        static void CheckDeployBandsDisjoint(List<string> failures, List<string> notes)
        {
            const string Tag = "[deploy-bands-disjoint]";
            int before = failures.Count;
            string path = Path.Combine(Application.dataPath, DeployScreenRel);
            if (!File.Exists(path)) { failures.Add(Tag + " RaidDeployScreen.cs not found at " + path); return; }
            string text;
            try { text = File.ReadAllText(path); }
            catch (Exception ex) { failures.Add(Tag + " RaidDeployScreen.cs unreadable (" + ex.Message + ")"); return; }

            string[] names = { "ScoutBandY0", "ScoutBandY1", "GuideBandY0", "GuideBandY1", "EnemyBandY0", "EnemyBandY1" };
            var vals = new float[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                if (!TryReadConstFloat(text, names[i], out vals[i]))
                {
                    failures.Add(Tag + " could not read 'private const float " + names[i] + " = <literal>f;' from " +
                                 "RaidDeployScreen.cs - the band constants must stay literal so this oracle can judge them");
                    return;
                }
            }

            if (vals[0] < 0f) failures.Add(Tag + " ScoutBandY0=" + vals[0] + " is below the body bottom");
            if (vals[5] > 1f) failures.Add(Tag + " EnemyBandY1=" + vals[5] + " is above the body top");
            for (int i = 0; i + 1 < names.Length; i++)
            {
                if (vals[i + 1] <= vals[i])
                    failures.Add(Tag + " " + names[i + 1] + "=" + vals[i + 1] + " is not above " + names[i] + "=" + vals[i] +
                                 " - the right-column bands intersect (WO-1385: ENEMY BASE / ECHO GUIDE / SCOUT REPORT " +
                                 "each own a vertical band; the owner's screenshot had three elements in one)");
            }
            // The gaps BETWEEN bands (scout->guide, guide->enemy) must be real, not zero.
            if (vals[2] - vals[1] < 0.005f)
                failures.Add(Tag + " SCOUT top " + vals[1] + " and GUIDE bottom " + vals[2] + " have no gap");
            if (vals[4] - vals[3] < 0.005f)
                failures.Add(Tag + " GUIDE top " + vals[3] + " and ENEMY bottom " + vals[4] + " have no gap");

            if (failures.Count == before)
                notes.Add("right-column bands disjoint (scout " + vals[0] + "-" + vals[1] + " / guide " + vals[2] + "-" +
                          vals[3] + " / enemy " + vals[4] + "-" + vals[5] + ")");
        }

        static bool TryReadConstFloat(string text, string name, out float value)
        {
            value = 0f;
            var m = System.Text.RegularExpressions.Regex.Match(text,
                @"private\s+const\s+float\s+" + name + @"\s*=\s*([0-9]*\.?[0-9]+)f?\s*;");
            if (!m.Success) return false;
            return float.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out value);
        }

        // -- 5. WO-1385 DEPLOY-BAR CONTRACT [deploy-bar-kit-button] (source-lint) -
        // Owner 2026-09-04 (Seeker build 355905, on the BEGIN ASSAULT row): "yuck". The
        // button sat on a flat yellow AddImage slab ("DeployGlow", the WO-839 halo) beside
        // ARMY READY?'s framed kit button - two visual languages on one row. Contract:
        // BuildDeployBar builds BEGIN ASSAULT through BuildObsidianButton with the Yellow
        // primary face, wires OnDeploy, and contains NO AddImage / "DeployGlow" at all.
        // RED if the slab literal returns or the CTA goes back to ElarionUiKit.Button(Confirm).
        static void CheckDeployBarKitButton(List<string> failures, List<string> notes)
        {
            const string Tag = "[deploy-bar-kit-button]";
            int before = failures.Count;
            string path = Path.Combine(Application.dataPath, DeployScreenRel);
            if (!File.Exists(path)) { failures.Add(Tag + " RaidDeployScreen.cs not found at " + path); return; }
            string text;
            try { text = File.ReadAllText(path); }
            catch (Exception ex) { failures.Add(Tag + " RaidDeployScreen.cs unreadable (" + ex.Message + ")"); return; }

            int start = text.IndexOf("private void BuildDeployBar(", StringComparison.Ordinal);
            if (start < 0) { failures.Add(Tag + " RaidDeployScreen.BuildDeployBar not found - the CTA builder moved"); return; }
            // The next method after the bar is the seating helper; bound the body there.
            int end = text.IndexOf("private static void SeatFooterCtaAtCanonicalHeight(", start, StringComparison.Ordinal);
            if (end < 0) end = Math.Min(start + 6000, text.Length);
            string bar = text.Substring(start, end - start);

            if (bar.IndexOf("\"DeployGlow\"", StringComparison.Ordinal) >= 0)
                failures.Add(Tag + " the \"DeployGlow\" slab literal is back in BuildDeployBar (owner: \"yuck\")");
            if (bar.IndexOf("AddImage(", StringComparison.Ordinal) >= 0)
                failures.Add(Tag + " BuildDeployBar paints an AddImage behind a CTA - the row must be kit buttons only");

            int obs = bar.IndexOf("BuildObsidianButton(", StringComparison.Ordinal);
            if (obs < 0)
                failures.Add(Tag + " BEGIN ASSAULT is no longer built by ElarionUiKit.BuildObsidianButton");
            else
            {
                int callEnd = bar.IndexOf("OnDeploy", obs, StringComparison.Ordinal);
                string call = callEnd > obs ? bar.Substring(obs, callEnd - obs) : bar.Substring(obs);
                if (call.IndexOf("ObsidianButtonColor.Yellow", StringComparison.Ordinal) < 0)
                    failures.Add(Tag + " the BEGIN ASSAULT BuildObsidianButton call is not the Yellow primary face");
                if (call.IndexOf("\"BEGIN ASSAULT\"", StringComparison.Ordinal) < 0)
                    failures.Add(Tag + " the BuildObsidianButton call in BuildDeployBar is not labelled \"BEGIN ASSAULT\"");
                if (callEnd < 0)
                    failures.Add(Tag + " the BEGIN ASSAULT button no longer wires OnDeploy");
            }
            // Both CTAs on the row share the seating call (same row geometry).
            int seats = 0, at = 0;
            while ((at = bar.IndexOf("SeatFooterCtaAtCanonicalHeight(", at, StringComparison.Ordinal)) >= 0) { seats++; at++; }
            if (seats < 2)
                failures.Add(Tag + " BuildDeployBar seats " + seats + " CTA(s) at the canonical height; ARMY READY? and " +
                             "BEGIN ASSAULT must share the row geometry (2 expected)");

            if (failures.Count == before)
                notes.Add("BEGIN ASSAULT = BuildObsidianButton Yellow, no DeployGlow slab, both CTAs seated on one row");
        }

        static string Join(IReadOnlyList<string> lines)
        {
            var arr = new string[lines.Count];
            for (int i = 0; i < lines.Count; i++) arr[i] = lines[i] ?? "<null>";
            return "[" + string.Join(" / ", arr) + "]";
        }
    }
}
