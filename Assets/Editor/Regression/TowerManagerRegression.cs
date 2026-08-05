// =============================================================================
// TowerManagerRegression [tower-manager] (WO-880) - the Tower Manager can never
// again print a fabricated "rng 0, dmg 0", nor cut a row in half.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Namespace: DeNelle.Editor.Regression.
//
// WHAT BROKE (UI capture 2026-08-04, Builds/ui-capture/TowerManagerPanel_2340x1080.png
// = UI_REVIEW/22_Tower Manager/delivered.png). Two defects on OPPOSITE sides of the
// MVVM line, both reproducible on paper:
//
//   1 [data]  Every row read "(rng 0, dmg 0)". PlacedTowerListVM resolved ONLY
//             FindObjectsByType<Tower>() - the LEGACY Build-Menu lane whose stats come
//             from a TowerData ScriptableObject - and the View then read
//             Tower.CurrentRange / CurrentDamage straight off the scene object. Those
//             properties RETURN 0f whenever CurrentUpgrade() is null (Tower.cs:185-216),
//             which is every tower that has not finished being raised. Meanwhile the
//             towers the player actually builds go down the OTHER lane: a
//             PlacedStructure whose combat stats StructureFactory.AttachBehaviorImpl
//             copies off the CATALOG repo block onto a DefenseTower
//             (StructureFactory.cs:686-690  t.Range = r.range; t.Damage = r.damage;).
//             BuildModeController.LiveTowerCount (BuildModeController.cs:2891-2917) is
//             the canonical statement that BOTH lanes exist and neither sees the other.
//             So the panel was reading a source the game does not use, and printing a
//             manufactured zero for the one it did.
//   2 [clip]  The third row was cut mid-height. At 2340x1080 the CanvasScaler
//             (1080x1920, match 0.5) resolves the canvas to 2120x978 REFERENCE px; the
//             modal is anchored (0.18,0.12)-(0.82,0.88) so the panel is 743 ref px tall.
//             ElarionUiKit's close-band reservation (ElarionUiKit.cs:600-646) raises
//             FrameCore's body floor from 0.075 to footer.w + 0.015 = 0.300, leaving a
//             body of (0.835-0.300) * 743.28 = 397.6 ref px. The well was a FRACTION of
//             that body (0.16..0.97 = 0.81), i.e. 322 px against a row pitch of
//             112 + 8 = 120: 2.68 rows. Row 3 was cut at 73% of its height.
//             At 1920x1080 the same math gives a 439 px body and a 355 px well = 2.96
//             rows, which is why the 1920 shot looked almost clean and nobody caught it.
//
// This oracle is a CHEAP structural + data guard, not a pixel test:
//
//   1 [stat-source]  The catalog IS still the game's stat source (source law on
//                    StructureFactory), the VM reads THAT source (source law on the VM),
//                    and EVERY tower row in the live structures-catalog.json resolves
//                    through the VM's real PlacedTowerListVM.TryReadLiveStats to a
//                    NON-ZERO range/damage. That is the "a tower with real stats can
//                    never display 0" assertion, driven over real data.
//   2 [row-text]     The state-aware row formatter says "(building)" / "(no stats)" in
//                    TEXT when there is nothing to print, and NEVER emits "rng 0"; the
//                    legacy 5-arg string is byte-stable; everything is pure ASCII.
//   3 [well-snap]    TowerManagerPanel's public layout constants (read by REFLECTION so
//                    this file needs no UnityEngine.UI / TMP asmdef reference): the well
//                    height is an EXACT whole number of row pitches at BOTH capture
//                    aspects and across a swept body range, the fixed stack fits the
//                    body, and both the row height and the action band sit at/above the
//                    kit touch floor (a sub-floor band is what ClampMinTouch grows
//                    symmetrically into its neighbours - the WO-852 Echo-chip bug).
//   4 [view-law]     Source law on TowerManagerPanel.cs: the View no longer reads
//                    CurrentRange / CurrentDamage / CurrentLevel off a scene object, it
//                    renders the string the VM composes, and the two old fraction-of-
//                    parent bands are gone.
//
// Markers: TOWER_MANAGER_OK / TOWER_MANAGER_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.TowerManagerRegression.RunAll
// Registered in DataRegression.RunAll as the "tower-manager suite".
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using DeNelle.Core.Catalog;
using DeNelle.Village;
using DeNelle.Village.UI;

namespace DeNelle.Editor.Regression
{
    public static class TowerManagerRegression
    {
        private const string CatalogRelPath = "Data/Canonical/structures-catalog.json";
        private const string VmSrc      = "Assets/_Modules/Village/Buildings/UI/PlacedTowerListVM.cs";
        private const string ViewSrc    = "Assets/_Modules/Village/Buildings/UI/TowerManagerPanel.cs";
        private const string FactorySrc = "Assets/_Modules/Village/Catalog/StructureFactory.cs";

        private const string PanelTypeName = "DeNelle.Village.UI.TowerManagerPanel, DeNelle.Village";
        private const string KitTypeName   = "DeNelle.Core.UI.ElarionUiKit, DeNelle.Core";

        // The MEASURED FrameCore body heights at the two capture aspects (derivation in
        // the header). These are what the well has to snap inside.
        private const float BodyH_2340 = 397.6f;
        private const float BodyH_1920 = 439.1f;

        [Serializable]
        private sealed class StructuresFile
        {
            [JsonProperty("version")] public int Version;
            [JsonProperty("entries")] public List<CatalogEntry> Entries = new List<CatalogEntry>();
        }

        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("TOWER_MANAGER_OK - " + reason);
            else Debug.LogError("TOWER_MANAGER_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                Case(failures, "stat-source", () => Case1_StatSourceIsTheOneTheGameUses(failures, notes));
                Case(failures, "row-text",    () => Case2_RowTextNeverFabricatesAZero(failures, notes));
                Case(failures, "well-snap",   () => Case3_WellSnapsToWholeRows(failures, notes));
                Case(failures, "view-law",    () => Case4_ViewComputesNothing(failures, notes));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes.ToArray()) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "TOWER MANAGER OK - stat source = the catalog the game builds from; "
                       + "no row can print a fabricated 0; the list well is whole rows" + noteStr;
                return true;
            }
            reason = "TOWER MANAGER x" + failures.Count + " - " + string.Join(" | ", failures.ToArray()) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string tag, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + tag + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 1 [stat-source] — the VM reads the SAME source the game builds from.
        // ─────────────────────────────────────────────────────────────────────
        private static void Case1_StatSourceIsTheOneTheGameUses(List<string> failures, List<string> notes)
        {
            // (a) THE GAME's source: StructureFactory still copies the catalog repo block
            //     onto the live DefenseTower. If this ever moves, the VM must move with it.
            string factory = ReadSource(FactorySrc, failures);
            if (factory != null)
            {
                if (!Regex.IsMatch(factory, @"\.Range\s*=\s*r\.range"))
                    failures.Add("[stat-source] StructureFactory no longer assigns DefenseTower.Range from repo.range - "
                               + "the game's tower stat SOURCE moved; re-point PlacedTowerListVM.TryReadLiveStats");
                if (!Regex.IsMatch(factory, @"\.Damage\s*=\s*r\.damage"))
                    failures.Add("[stat-source] StructureFactory no longer assigns DefenseTower.Damage from repo.damage - "
                               + "the game's tower stat SOURCE moved; re-point PlacedTowerListVM.TryReadLiveStats");
            }

            // (b) THE VM reads that source, and enumerates the lane it lives on.
            string vm = ReadSource(VmSrc, failures);
            if (vm != null)
            {
                RequireSource(vm, "CatalogRegistry.Get(", "stat-source",
                    "the VM must read the CATALOG row, not only a TowerData ScriptableObject", failures);
                RequireSource(vm, "IsTowerEntry(", "stat-source",
                    "the VM must classify a tower with the SAME predicate the build economy uses", failures);
                RequireSource(vm, "GetComponentInChildren<DefenseTower>", "stat-source",
                    "the VM must prefer the LIVE component's stats (what the tower actually shoots with)", failures);
                RequireSource(vm, "FindObjectsByType<PlacedStructure>", "stat-source",
                    "the VM must enumerate the Build-Mode lane, or the panel cannot see a tower the player built", failures);
            }

            // (c) REAL DATA: every tower row in the shipped catalog resolves to non-zero stats
            //     through the VM's REAL resolver. This is the assertion that makes a 0 impossible.
            var entries = ParseCatalog(failures);
            if (entries == null) return;

            int registered = 0;
            foreach (var e in entries)
            {
                if (e == null || string.IsNullOrEmpty(e.id)) continue;
                if (CatalogRegistry.Get(e.id) == null) { CatalogRegistry.Register(e); registered++; }
            }

            int towers = 0, zeroes = 0;
            foreach (var e in entries)
            {
                if (!BuildModeController.IsTowerEntry(e)) continue;
                towers++;

                float range, damage;
                string source;
                bool known = PlacedTowerListVM.TryReadLiveStats(null, e, out range, out damage, out source);
                if (!known || (range <= 0f && damage <= 0f))
                {
                    zeroes++;
                    failures.Add("[stat-source] catalog tower '" + e.id + "' resolves to rng " + range.ToString("0.##")
                               + " / dmg " + damage.ToString("0.##") + " (source=" + source + ") - the manager row would "
                               + "read as having no stats; author repo.range/repo.damage or drop the tower type");
                    continue;
                }
                if (!source.StartsWith("catalog:", StringComparison.Ordinal))
                    failures.Add("[stat-source] catalog tower '" + e.id + "' answered from '" + source
                               + "' with no live object present - expected the catalog row");
            }

            if (towers == 0)
                failures.Add("[stat-source] structures-catalog.json contains NO tower row (IsTowerEntry) - "
                           + "the whole assertion is vacuous; the classifier or the catalog changed");
            else
                notes.Add("[stat-source] " + towers + " catalog tower row(s), " + zeroes
                        + " with no stats; " + registered + " row(s) registered for the probe");

            // (d) A row with NO source at all must report unknown, not zero.
            float r2, d2; string s2;
            if (PlacedTowerListVM.TryReadLiveStats(null, null, out r2, out d2, out s2))
                failures.Add("[stat-source] TryReadLiveStats reported KNOWN stats with no object and no catalog entry - "
                           + "that is exactly the fabricated 0/0 this WO removed");
            if (s2 != "none")
                failures.Add("[stat-source] TryReadLiveStats source for a sourceless tower = '" + s2 + "', expected 'none'");
        }

        // ─────────────────────────────────────────────────────────────────────
        // 2 [row-text] — the row says WHY it has no numbers; it never invents one.
        // ─────────────────────────────────────────────────────────────────────
        private static void Case2_RowTextNeverFabricatesAZero(List<string> failures, List<string> notes)
        {
            // Legacy 5-arg string stays byte-stable (the BuildMenu + the existing EditMode
            // test both depend on it).
            Expect(PlacedTowerListVM.FormatManagerRow(1, 2, 12f, 20f, false),
                   "Tower 1  -  Lv 2   (rng 12, dmg 20)", "row-text/legacy", failures);
            Expect(PlacedTowerListVM.FormatManagerRow(3, 1, 8.4f, 15.6f, true),
                   "> Tower 3  -  Lv 1   (rng 8, dmg 16)", "row-text/legacy-selected", failures);

            // A tower still being raised: no level, no stats, and it SAYS so.
            string building = PlacedTowerListVM.FormatManagerRow(3, 0, 0f, 0f, false, built: false, statsKnown: false);
            Expect(building, "Tower 3  -  (building)", "row-text/building", failures);

            // A raised tower whose stat source answered nothing: level, but no fake numbers.
            string noStats = PlacedTowerListVM.FormatManagerRow(2, 1, 0f, 0f, false, built: true, statsKnown: false);
            Expect(noStats, "Tower 2  -  Lv 1   (no stats)", "row-text/no-stats", failures);

            // Real stats route to the legacy string verbatim.
            Expect(PlacedTowerListVM.FormatManagerRow(1, 2, 12f, 20f, true, built: true, statsKnown: true),
                   "> Tower 1  -  Lv 2   (rng 12, dmg 20)", "row-text/known", failures);

            // THE PIN: no state-aware row may ever contain the fabricated zero readings.
            foreach (var s in new[] { building, noStats })
            {
                if (s.Contains("rng 0") || s.Contains("dmg 0"))
                    failures.Add("[row-text] a stat-less row printed '" + s + "' - the fabricated zero is back");
                if (s.Contains("Lv 0"))
                    failures.Add("[row-text] a stat-less row printed a level 0 ('" + s + "') instead of saying (building)");
            }

            // Selection is TEXT-encoded, never colour alone (the owner is red/green colourblind).
            if (!PlacedTowerListVM.FormatManagerRow(1, 1, 5f, 5f, true, true, true).StartsWith("> ", StringComparison.Ordinal))
                failures.Add("[row-text] the selected row lost its leading '> ' marker - selection would read as colour only");

            // Footers: the honest states, and no invented cost/level.
            string unbuilt = PlacedTowerListVM.FormatUnbuiltDetail("Archer");
            if (unbuilt.Contains("rng") || unbuilt.Contains("Lv "))
                failures.Add("[row-text] the unbuilt footer invented stats/level: '" + unbuilt + "'");
            string catalogDetail = PlacedTowerListVM.FormatCatalogDetail("Archer Tower", 2, 3, 14f, 8f, true);
            if (!catalogDetail.Contains("rng 14") || !catalogDetail.Contains("dmg 8"))
                failures.Add("[row-text] the catalog footer did not render its real stats: '" + catalogDetail + "'");
            if (!catalogDetail.Contains("Build Mode"))
                failures.Add("[row-text] the catalog footer must say where its upgrade verb lives: '" + catalogDetail + "'");

            // ASCII only (the TMP font ships no glyphs beyond it).
            foreach (var s in new[] { building, noStats, unbuilt, catalogDetail,
                                      PlacedTowerListVM.FormatDetail(2, 3, 12f, 20f, true, 50) })
                foreach (char c in s)
                    if (c > 126 || c < 32)
                    {
                        failures.Add("[row-text] non-ASCII char (U+" + ((int)c).ToString("X4") + ") in '" + s + "'");
                        break;
                    }

            notes.Add("[row-text] building/no-stats/known + both footers pinned, ASCII-clean");
        }

        // ─────────────────────────────────────────────────────────────────────
        // 3 [well-snap] — the list well is always a WHOLE number of rows.
        // ─────────────────────────────────────────────────────────────────────
        private static void Case3_WellSnapsToWholeRows(List<string> failures, List<string> notes)
        {
            Type panel = Type.GetType(PanelTypeName);
            if (panel == null)
            {
                failures.Add("[well-snap] type '" + PanelTypeName + "' not found - the panel moved or was renamed");
                return;
            }
            Type kit = Type.GetType(KitTypeName);
            float minTouch = kit != null ? ConstF(kit, "MinTouchPx", failures, "well-snap") : 112f;

            float rowH   = ConstF(panel, "RowPixelH", failures, "well-snap");
            float gap    = ConstF(panel, "RowGapPx", failures, "well-snap");
            float pitch  = ConstF(panel, "RowPitchPx", failures, "well-snap");
            float topPad = ConstF(panel, "ListTopPadPx", failures, "well-snap");
            float band   = ConstF(panel, "ActionBandPx", failures, "well-snap");
            float aGap   = ConstF(panel, "ActionGapPx", failures, "well-snap");
            float botPad = ConstF(panel, "BodyBottomPadPx", failures, "well-snap");

            if (rowH < minTouch)
                failures.Add("[well-snap] RowPixelH " + rowH + " is under the kit touch floor " + minTouch
                           + " - ClampMinTouch would grow each row button symmetrically into its neighbours");
            if (band < minTouch)
                failures.Add("[well-snap] ActionBandPx " + band + " is under the kit touch floor " + minTouch
                           + " - this is the exact sub-floor band that grew over the well and the footer");
            if (Mathf.Abs(pitch - (rowH + gap)) > 0.01f)
                failures.Add("[well-snap] RowPitchPx " + pitch + " != RowPixelH + RowGapPx (" + (rowH + gap) + ")");

            var snap = panel.GetMethod("SnappedWellHeightPx", BindingFlags.Public | BindingFlags.Static);
            var fit  = panel.GetMethod("WholeRowsThatFit", BindingFlags.Public | BindingFlags.Static);
            if (snap == null || fit == null)
            {
                failures.Add("[well-snap] SnappedWellHeightPx / WholeRowsThatFit are gone - the well is no longer snapped");
                return;
            }

            // The two capture aspects, plus a sweep, so no body height can produce a part row.
            var heights = new List<float> { BodyH_2340, BodyH_1920 };
            for (float h = 260f; h <= 1200f; h += 7f) heights.Add(h);

            int checkedHeights = 0;
            foreach (float bodyH in heights)
            {
                float well = (float)snap.Invoke(null, new object[] { bodyH });
                int rows   = (int)fit.Invoke(null, new object[] { bodyH });
                checkedHeights++;

                if (rows < 1) { failures.Add("[well-snap] body " + bodyH.ToString("0.#") + " -> " + rows + " rows"); continue; }

                // THE PIN: (well + gap) is an exact multiple of the pitch, so the mask edge
                // always lands on a row boundary and a row can never be cut mid-height.
                float mod = (well + gap) % pitch;
                if (mod > 0.01f && Mathf.Abs(mod - pitch) > 0.01f)
                    failures.Add("[well-snap] body " + bodyH.ToString("0.#") + " -> well " + well.ToString("0.#")
                               + " is NOT a whole number of " + pitch + "px row pitches (remainder " + mod.ToString("0.##") + ")");

                // The fixed stack fits the body (a body too small for even one row is clamped
                // to one on purpose, so only assert the fit above that floor).
                float stack = topPad + well + aGap + band + botPad;
                float oneRowStack = topPad + (pitch - gap) + aGap + band + botPad;
                if (bodyH >= oneRowStack && stack > bodyH + 0.01f)
                    failures.Add("[well-snap] body " + bodyH.ToString("0.#") + " -> stack " + stack.ToString("0.#")
                               + " overflows the body - the action band would sit outside it");
            }

            // The capture geometry itself: the 2340 shot must now show >= 2 WHOLE rows where it
            // used to show 2.68 (the clip). If this ever drops to 1 the panel became useless.
            int rows2340 = (int)fit.Invoke(null, new object[] { BodyH_2340 });
            if (rows2340 < 2)
                failures.Add("[well-snap] the 2340x1080 body (" + BodyH_2340 + "px) now shows only " + rows2340
                           + " whole row - the well lost too much to the fixed bands");

            notes.Add("[well-snap] " + checkedHeights + " body height(s) checked; 2340 body " + BodyH_2340
                    + "px -> " + rows2340 + " whole rows (was 2.68 = the clipped third row)");
        }

        // ─────────────────────────────────────────────────────────────────────
        // 4 [view-law] — the View renders; it does not compute or read state.
        // ─────────────────────────────────────────────────────────────────────
        private static void Case4_ViewComputesNothing(List<string> failures, List<string> notes)
        {
            string view = ReadSource(ViewSrc, failures);
            if (view == null) return;

            // The View must not read a tower's live stats off the scene object - that read is
            // what printed the 0s, and it is a VM responsibility.
            foreach (var banned in new[] { ".CurrentRange", ".CurrentDamage", ".CurrentLevel" })
                if (NonCommentContains(view, banned))
                    failures.Add("[view-law] TowerManagerPanel reads '" + banned + "' off a scene object - "
                               + "the VM owns tower stats (that read is the WO-880 defect)");

            RequireSource(view, "_vm.ManagerRowFor(", "view-law",
                "the row label must be COMPOSED BY THE VM, not assembled in the View", failures);
            RequireSource(view, "SnapListWellToWholeRows", "view-law",
                "the well must be snapped to whole rows", failures);

            // The two fraction-of-parent bands that caused the clip + the ClampMinTouch overlap.
            foreach (var frac in new[] { "new Vector2(0.06f, 0.16f)", "new Vector2(0.10f, 0.03f)", "new Vector2(0.52f, 0.03f)" })
                if (NonCommentContains(view, frac))
                    failures.Add("[view-law] the fraction-of-parent band '" + frac + "' is back - "
                               + "WO-841/852 bands are FIXED PIXELS (a sub-floor fraction is grown symmetrically by ClampMinTouch)");

            notes.Add("[view-law] View reads no tower stats; bands are fixed pixels");
        }

        // ── helpers ───────────────────────────────────────────────────────────

        private static List<CatalogEntry> ParseCatalog(List<string> failures)
        {
            string json = DeNelle.Core.CanonicalJson.Read(CatalogRelPath);
            if (string.IsNullOrEmpty(json))
            {
                failures.Add("[stat-source] " + CatalogRelPath + " unreadable (CanonicalJson.Read returned empty)");
                return null;
            }
            try
            {
                var settings = new JsonSerializerSettings
                {
                    Converters = { new StringEnumConverter() },
                    NullValueHandling = NullValueHandling.Ignore,
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                };
                var file = JsonConvert.DeserializeObject<StructuresFile>(json, settings);
                if (file == null || file.Entries == null || file.Entries.Count == 0)
                {
                    failures.Add("[stat-source] structures-catalog.json deserialized to 0 entries");
                    return null;
                }
                return file.Entries;
            }
            catch (Exception ex)
            {
                failures.Add("[stat-source] structures-catalog.json failed to parse: " + ex.Message);
                return null;
            }
        }

        private static string ReadSource(string relPath, List<string> failures)
        {
            try
            {
                string full = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", relPath);
                if (!File.Exists(full)) { failures.Add("[suite] source not found: " + relPath); return null; }
                return File.ReadAllText(full);
            }
            catch (Exception ex) { failures.Add("[suite] could not read " + relPath + ": " + ex.Message); return null; }
        }

        private static void RequireSource(string text, string needle, string tag, string why, List<string> failures)
        {
            if (text.IndexOf(needle, StringComparison.Ordinal) < 0)
                failures.Add("[" + tag + "] '" + needle + "' is gone - " + why);
        }

        /// <summary>True when <paramref name="needle"/> appears on a line that is not a comment
        /// (the file documents the removed reads in its banner, which must not trip the lint).</summary>
        private static bool NonCommentContains(string text, string needle)
        {
            foreach (var raw in text.Split('\n'))
            {
                string line = raw.TrimStart();
                if (line.StartsWith("//") || line.StartsWith("*") || line.StartsWith("/*")) continue;
                if (line.IndexOf(needle, StringComparison.Ordinal) >= 0) return true;
            }
            return false;
        }

        private static void Expect(string actual, string expected, string tag, List<string> failures)
        {
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
                failures.Add("[" + tag + "] got '" + actual + "', expected '" + expected + "'");
        }

        private static float ConstF(Type t, string name, List<string> failures, string tag)
        {
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (f == null) { failures.Add("[" + tag + "] const " + t.Name + "." + name + " not found"); return 0f; }
            try { return Convert.ToSingle(f.GetValue(null)); }
            catch (Exception ex) { failures.Add("[" + tag + "] const " + name + " unreadable: " + ex.Message); return 0f; }
        }
    }
}
