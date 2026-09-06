// =============================================================================
// UiCaptureCoverageRegression [ui-capture-coverage]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.  Namespace: DeNelle.Editor.Regression.
// Markers: UI_CAPTURE_COVERAGE_OK / UI_CAPTURE_COVERAGE_FAIL.
//
// WHAT BROKE (owner, 2026-08-04): she opened UI_REVIEW/INDEX.html and it was
// "mostly just the blank templates and nothing else".
//
// THE PROVING DATA (not inference): Builds\Windows\DefendersOfTheRealm.exe was
// built at 21:18:09; at 21:21:06 -- three minutes later -- THIRTY-FIVE
// panel_*.png files under LocalLow ui-shots were rewritten at exactly 33150
// bytes each, the byte-signature of a flat black frame. An AutoPilot fleet had
// run in its DEFAULT mode (run-autopilot-fleet.ps1 passes -batchmode
// -nographics unless -Graphics is given), and AutoPilotDriver.CaptureRawShot
// fired ScreenCapture anyway -- its own comment recorded the outcome as
// acceptable: "a -nographics fleet writes a blank frame, never an error".
// Every panel the fleet walked overwrote a REAL review shot with black, and
// build-ui-review.ps1 then paired those blanks with the Blink templates and
// badged them "PAIR COMPLETE".
//
// So the review was never un-fed. It was fed poison by a logic run that had no
// business writing review artefacts at all.
//
// This oracle pins the invariants that make that state unreachable. All of it is
// decidable from TEXT (source + _mapping.json + the PanelId registry), so it runs
// in every batch with no scene, no play mode and no GPU:
//
//   RULE 1 [blank-guard]  AutoPilotDriver.CaptureRawShot still refuses to write
//          without a graphics device, and UICaptureLaunch still measures a frame
//          before counting it toward UI_CAPTURE_OK. These two guards are the only
//          things standing between a headless run and a review full of convincing
//          black rectangles. Deleting either re-opens the exact 2026-08-04 hole.
//
//   RULE 2 [coverage]  Every row's deliveredShot FILENAME is one AutoPilot actually
//          writes -- either from an explicit CaptureUiPanel/CaptureComponentPanel/
//          CaptureThrowawayPanel call, or from the OpenEachHUDPanel sweep over
//          PanelIds something really registers with PanelRouter -- or the row is
//          named in KnownUncapturable below WITH a reason. Matching on the FILENAME
//          and not on the panelId is deliberate: the filename is the whole contract
//          with build-ui-review.ps1, and one row legitimately diverges (the Pack
//          Store screen is PanelId.RealmStore, so its shot is panel_RealmStore.png).
//          A panelId-keyed check would have reported that healthy row as a gap.
//
//   RULE 3 [shot-name]  Every non-exempt row names a deliveredShot, and it is
//          shaped panel_*.png. A row with no filename can never be paired however
//          well it renders. Rows whose filename diverges from panel_<panelId>.png
//          are surfaced as a note so the divergence stays deliberate, not forgotten.
//
//   RULE 4 [exemptions]  Every KnownUncapturable entry still matches a real
//          mapping row and carries a real reason. A screen we cannot shoot must be
//          NAMED and ARGUED; an unexplained exemption is indistinguishable from an
//          oversight, and a stale one hides a screen that came back.
//
// Deliberately NOT asserted: that the PNGs exist or look right. Whether a given
// run produced pixels is the run's job (RULE 1's guards make a blank impossible to
// pass off as one) and the owner's eyes'. This oracle guards the WIRING.
//
// Standalone: run-unity-method.ps1
//   -Method DeNelle.Editor.Regression.UiCaptureCoverageRegression.RunAll
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class UiCaptureCoverageRegression
    {
        private const string MappingPath = "UI_REVIEW/_mapping.json";
        private const string DriverSrc = "Assets/_Modules/DevTools/AutoPilotDriver.cs";
        private const string HarnessSrc = "Assets/Editor/UICaptureLaunch.cs";
        private const string ModulesRoot = "Assets/_Modules";

        /// <summary>How much source after a method's signature counts as "inside" it.</summary>
        private const int MethodWindowChars = 1400;

        // ---------------------------------------------------------------------
        //  RULE 4 - mapping rows no capture path can reach, each with its reason.
        //  SHRINK THIS. Every entry is a screen the owner cannot review.
        // ---------------------------------------------------------------------
        private static readonly Dictionary<string, string> KnownUncapturable =
            new Dictionary<string, string>(StringComparer.Ordinal)
        {
            {
                "PetSkillTree",
                "RETIRED 2026-07-08: the pet skill-tree stack was DELETED. No PetSkillTreePanel " +
                "class exists anywhere under Assets/ and PanelId value 4 was removed, so there is " +
                "nothing to open in any mode. This mapping row outlived its screen and should be " +
                "retired by the owner rather than routed."
            },
            {
                "HeroTalents",
                "PanelId.HeroTalents is RETIRED as a route (kept at value 0 only so default(PanelId) " +
                "is defined) - nothing registers it, so the AutoPilot sweep cannot open it. The SCREEN " +
                "is not missing: HeroSkillTreePanelMvvm serves both rows and is captured as " +
                "panel_HeroSkillTree.png. This row is a duplicate view of one screen, not a gap."
            },
            {
                "ShopPanel",
                "RETIRED 2026-09-06 (WO-1430): the legacy ShopPanel was DELETED as a doorless panel - " +
                "no production file opened it, and only AutoPilotDriver + UICaptureLaunch constructed " +
                "it so it could be photographed, which PanelDoorRegression reports as " +
                "[panel-door-is-harness-only]. The MERCHANT SCREEN is not missing: PartyShopPanelMvvm " +
                "registers PanelId.PartyShop and the OpenEachHUDPanel sweep shoots panel_PartyShop.png. " +
                "This row is a duplicate view of one screen, not a gap - same shape as HeroTalents " +
                "above. The owner should retire the row rather than have it routed."
            },
        };

        /// <summary>Standalone batch entry - prints the distinct marker a gate can grep.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("UI_CAPTURE_COVERAGE_OK - " + reason);
            else Debug.LogError("UI_CAPTURE_COVERAGE_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                Case(failures, "blank-guard", () => Case1_BlankGuards(failures, notes));

                var rows = ReadMappingRows(failures);
                if (rows != null)
                {
                    var covered = CollectCapturedShotNames(failures, notes);
                    if (covered != null)
                    {
                        Case(failures, "coverage", () => Case2_Coverage(rows, covered, failures, notes));
                        Case(failures, "shot-name", () => Case3_ShotNames(rows, covered, failures, notes));
                    }
                    Case(failures, "exemptions", () => Case4_Exemptions(rows, failures, notes));
                }
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "UI CAPTURE COVERAGE OK - no capture path can write a blank frame, and every " +
                         "panelId in " + MappingPath + " is either shot by AutoPilot under the exact " +
                         "deliveredShot filename build-ui-review.ps1 reads, or exempt with a stated " +
                         "reason" + noteStr;
                return true;
            }
            reason = "ui-capture-coverage FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  RULE 1 - neither capture path may ship a blank
        // =====================================================================
        private static void Case1_BlankGuards(List<string> failures, List<string> notes)
        {
            string driver = ReadSource(DriverSrc, "blank-guard", failures);
            if (driver != null)
            {
                int shotIdx = driver.IndexOf("private static void CaptureRawShot", StringComparison.Ordinal);
                if (shotIdx < 0)
                {
                    failures.Add("[blank-guard] AutoPilotDriver.CaptureRawShot no longer exists - it is the " +
                                 "single writer every UI_REVIEW shot goes through; re-point this guard rather " +
                                 "than dropping it");
                }
                else
                {
                    // Scope the check to a window around the method so an unrelated graphics
                    // test elsewhere in this 5000-line file cannot satisfy it by accident.
                    // A fixed window rather than a brace scan: a lone brace CHAR LITERAL trips
                    // the CLAUDE.md rule-1 naive counter and the CompileGate scan (the lesson
                    // RegressionMarkerRegression.cs records at its OpenBrace/CloseBrace pair).
                    int window = Math.Min(MethodWindowChars, driver.Length - shotIdx);
                    string body = driver.Substring(shotIdx, window);

                    if (body.IndexOf("graphicsDeviceType", StringComparison.Ordinal) < 0)
                        failures.Add("[blank-guard] AutoPilotDriver.CaptureRawShot writes screenshots WITHOUT " +
                                     "checking for a graphics device. Under -nographics ScreenCapture produces " +
                                     "flat black frames, and this method overwrites the real UI_REVIEW shots " +
                                     "with them - 35 of them on 2026-08-04, which is what made INDEX.html read " +
                                     "as 'blank templates and nothing else'. Restore the guard.");
                    else
                        notes.Add("AutoPilot capture writer is graphics-gated");
                }
            }

            string harness = ReadSource(HarnessSrc, "blank-guard", failures);
            if (harness != null)
            {
                if (harness.IndexOf("IsBlank", StringComparison.Ordinal) < 0)
                    failures.Add("[blank-guard] UICaptureLaunch lost its IsBlank measurement - UI_CAPTURE_OK <n> " +
                                 "is a PRE-SHIP GATE, so it must count real pixels only. Without this, a run with " +
                                 "no graphics device reports a full green count over black frames.");
                else if (harness.IndexOf("BLANK RENDER", StringComparison.Ordinal) < 0)
                    failures.Add("[blank-guard] UICaptureLaunch still measures blankness but no longer REFUSES " +
                                 "the write - a measured-and-shipped blank is the same lie as an unmeasured one");
                else
                    notes.Add("UICaptureLaunch refuses blank renders");
            }
        }

        // =====================================================================
        //  Inputs
        // =====================================================================

        private sealed class Row
        {
            public string PanelId;
            public string DeliveredShot;
        }

        /// <summary>
        /// Pull (panelId, deliveredShot) out of the hand-authored mapping. Regex rather than
        /// a JSON dependency: this assembly stays reference-light and the row shape
        /// (panelId ... deliveredShot) is stable and hand-kept. Zero rows parsed is a
        /// FAILURE, never a quiet pass.
        /// </summary>
        private static List<Row> ReadMappingRows(List<string> failures)
        {
            string src = ReadSource(MappingPath, "input", failures);
            if (src == null) return null;

            var rx = new Regex(
                "\"panelId\"\\s*:\\s*\"(?<pid>[^\"]+)\".*?\"deliveredShot\"\\s*:\\s*(?:\"(?<shot>[^\"]*)\"|null)",
                RegexOptions.Singleline);

            var rows = new List<Row>();
            foreach (Match m in rx.Matches(src))
                rows.Add(new Row { PanelId = m.Groups["pid"].Value, DeliveredShot = m.Groups["shot"].Value });

            if (rows.Count == 0)
            {
                failures.Add("[input] " + MappingPath + " parsed ZERO rows - either it is empty or its row " +
                             "shape changed (panelId ... deliveredShot). It is the hand-authored INPUT to " +
                             "build-ui-review.ps1 and must never be deleted; fix the parse rather than the check");
                return null;
            }
            return rows;
        }

        /// <summary>
        /// The set of shot BASENAMES AutoPilot can write, derived from source:
        ///   (a) every literal name passed to a CaptureUiPanel / CaptureUiPanelSettled /
        ///       CaptureComponentPanel / CaptureThrowawayPanel call, and
        ///   (b) the OpenEachHUDPanel sweep, which shoots id.ToString() for every PanelId
        ///       that something actually registers with PanelRouter.
        /// (b) is deliberately narrowed to REGISTERED ids: the sweep skips unregistered
        /// ones, so counting the bare enum would falsely certify a retired route.
        /// </summary>
        private static HashSet<string> CollectCapturedShotNames(List<string> failures, List<string> notes)
        {
            string driver = ReadSource(DriverSrc, "coverage", failures);
            if (driver == null) return null;

            var names = new HashSet<string>(StringComparer.Ordinal);

            foreach (Match m in Regex.Matches(driver,
                @"Capture(?:UiPanel|UiPanelSettled|ComponentPanel|ThrowawayPanel)\s*(?:<[^>]+>)?\s*\(\s*""(?<n>[A-Za-z0-9_]+)"""))
                names.Add(m.Groups["n"].Value);

            int explicitCount = names.Count;

            bool hasSweep = driver.IndexOf("CaptureUiPanelSettled(id.ToString()", StringComparison.Ordinal) >= 0;
            if (!hasSweep)
            {
                failures.Add("[coverage] AutoPilotDriver.OpenEachHUDPanel no longer shoots each registered " +
                             "PanelId (CaptureUiPanelSettled(id.ToString(), ...)). That sweep is what covers " +
                             "most of the review set; without it the mapping rows below lose their only " +
                             "capture route.");
            }
            else
            {
                foreach (string id in RegisteredPanelIds(failures)) names.Add(id);
            }

            notes.Add("AutoPilot shots: " + explicitCount + " explicit + " + (names.Count - explicitCount) +
                      " registered-PanelId sweep");
            return names;
        }

        /// <summary>PanelId values something actually registers with PanelRouter (source-scanned).</summary>
        private static HashSet<string> RegisteredPanelIds(List<string> failures)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                foreach (string file in Directory.GetFiles(ModulesRoot, "*.cs", SearchOption.AllDirectories))
                {
                    string src;
                    try { src = File.ReadAllText(file); }
                    catch { continue; }
                    if (src.IndexOf("PanelRouter.Register", StringComparison.Ordinal) < 0) continue;
                    foreach (Match m in Regex.Matches(src, @"PanelRouter\.Register\s*\(\s*PanelId\.(?<id>[A-Za-z0-9_]+)"))
                        ids.Add(m.Groups["id"].Value);
                }
            }
            catch (Exception ex)
            {
                failures.Add("[coverage] could not scan " + ModulesRoot + " for PanelRouter registrations: " + ex.Message);
            }

            if (ids.Count == 0)
                failures.Add("[coverage] found ZERO PanelRouter.Register(PanelId.X) calls - the scan is broken " +
                             "(or routing was rewritten); this oracle would otherwise report a false gap for " +
                             "every router-driven screen");
            return ids;
        }

        // =====================================================================
        //  RULE 2 - every mapping row has a capture route
        // =====================================================================
        private static void Case2_Coverage(List<Row> rows, HashSet<string> covered,
                                           List<string> failures, List<string> notes)
        {
            // The set of FILENAMES AutoPilot can produce. CaptureUiPanel(name) writes
            // panel_<name>.png, so this is the exact vocabulary build-ui-review.ps1 reads.
            var writable = new HashSet<string>(StringComparer.Ordinal);
            foreach (string n in covered) writable.Add("panel_" + n + ".png");

            int ok = 0;
            foreach (var row in rows)
            {
                if (KnownUncapturable.ContainsKey(row.PanelId)) continue;
                if (!string.IsNullOrEmpty(row.DeliveredShot) && writable.Contains(row.DeliveredShot))
                {
                    ok++;
                    continue;
                }

                failures.Add("[coverage] _mapping.json row panelId='" + row.PanelId + "' expects '" +
                             row.DeliveredShot + "', which NO AutoPilot capture writes: nothing calls " +
                             "CaptureUiPanel(\"" + row.PanelId + "\") and no PanelRouter.Register(PanelId." +
                             row.PanelId + ") exists for the OpenEachHUDPanel sweep to find. Add a capture " +
                             "(AutoPilotDriver.CaptureExtraPanels is where the non-router screens live), or " +
                             "declare the row in KnownUncapturable WITH a reason. A row with neither renders " +
                             "as a permanent 'AWAITING SHOT' card nobody ever chases.");
            }
            notes.Add("rows covered: " + ok + "/" + rows.Count + " (" + KnownUncapturable.Count + " exempt)");
        }

        // =====================================================================
        //  RULE 3 - every row names a pairable file, and divergences stay visible
        // =====================================================================
        private static void Case3_ShotNames(List<Row> rows, HashSet<string> covered,
                                            List<string> failures, List<string> notes)
        {
            var diverged = new List<string>();
            foreach (var row in rows)
            {
                if (KnownUncapturable.ContainsKey(row.PanelId)) continue;

                if (string.IsNullOrEmpty(row.DeliveredShot))
                {
                    failures.Add("[shot-name] _mapping.json row '" + row.PanelId + "' has no deliveredShot " +
                                 "filename, so build-ui-review.ps1 can never pair it however well it renders");
                    continue;
                }

                if (!Regex.IsMatch(row.DeliveredShot, @"^panel_[A-Za-z0-9_]+\.png$"))
                {
                    failures.Add("[shot-name] row '" + row.PanelId + "' names deliveredShot '" +
                                 row.DeliveredShot + "', which is not the panel_<name>.png shape every " +
                                 "capture path writes - it could never be produced by any run");
                    continue;
                }

                string conventional = "panel_" + row.PanelId + ".png";
                if (!string.Equals(conventional, row.DeliveredShot, StringComparison.Ordinal))
                    diverged.Add(row.PanelId + "->" + row.DeliveredShot);
            }

            // Not a failure: a screen's PanelId can legitimately differ from its mapping
            // label (Pack Store is PanelId.RealmStore). Surfaced so it stays a decision.
            if (diverged.Count > 0)
                notes.Add("rows whose shot name diverges from panel_<panelId>.png: " +
                          string.Join(", ", diverged));
        }

        // =====================================================================
        //  RULE 4 - exemptions stay real and stay argued
        // =====================================================================
        private static void Case4_Exemptions(List<Row> rows, List<string> failures, List<string> notes)
        {
            var mapped = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows) mapped.Add(row.PanelId);

            foreach (var kv in KnownUncapturable)
            {
                if (!mapped.Contains(kv.Key))
                    failures.Add("[exemptions] '" + kv.Key + "' is exempted from UI capture but has no row in " +
                                 MappingPath + " - a stale excuse for a screen that no longer exists (or came " +
                                 "back under a new id). Prune it.");

                if (string.IsNullOrEmpty(kv.Value) || kv.Value.Trim().Length < 40)
                    failures.Add("[exemptions] '" + kv.Key + "' carries no real reason. A screen the owner " +
                                 "cannot review must say WHY, in words the next reader can act on.");
            }
            notes.Add("exemptions: " + KnownUncapturable.Count);
        }

        // =====================================================================
        //  helpers
        // =====================================================================
        private static string ReadSource(string path, string tag, List<string> failures)
        {
            try
            {
                if (!File.Exists(path))
                {
                    failures.Add("[" + tag + "] source not found: " + path);
                    return null;
                }
                return File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                failures.Add("[" + tag + "] could not read " + path + ": " + ex.Message);
                return null;
            }
        }
    }
}
