// =============================================================================
// GearCurationExporter (WO-747, "Option A", ADDITIVE model) — makes the owner's
// Gear Caster curation reach the runtime catalog WITHOUT dropping live content.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor. Editor-only. The runtime-winning gear catalogs are the
// Resources copies (CanonicalJson.Read = Resources-first, StreamingAssets fallback).
// This tool MERGES the owner's curation INTO those copies additively:
//
//   Resources weapons.json = ALL current Resources weapons
//                            UNION (each included:true weapon pick, full row pulled
//                                   from the StreamingAssets library).
//   Resources armor.json   = ALL current Resources armor
//                            UNION (each included armor pick + the referenced
//                                   blink_armor default ids centurion/beasthunter/
//                                   dragonic/basic1, full rows from the library).
//
// ADDITIVE, NEVER DROPS: pure projection would ORPHAN live content —
//   • the StreamingAssets ARMOR library is MISSING the 15 class-tier progression
//     sets (armor_knight_common..legendary + ranger/mage) that live only in the
//     Resources copy and back the runtime loot/shop armor; and
//   • loot-tables / vendors / gear-recipes reference weapon ids not in the picks.
// So we keep every current Resources row and only ADD curated rows on top.
//
// DE-DUP: by id, case-insensitive. On a conflict the CURRENT Resources row WINS
// (authored fields preserved) — a library row is appended only when its id is not
// already in the Resources copy. FIELD FIDELITY: appended rows are Newtonsoft
// DeepClones of the exact library JObject (every field preserved). Current rows are
// preserved verbatim. Top-level shape + version are preserved (armor stays v2,
// weapons stays 1) and a "_generated" marker is (re)stamped.
//
// This ALSO fixes the HeroBodySwapper blink_armor default no-op: those ids live only
// in the library today, so merging their rows makes them resolve in Resources.
//
// RUN: menu "Defenders/Gear/Export Curated Catalog -> Resources" or headless
//   -executeMethod DeNelle.Editor.GearCurationExporter.Export
// It writes the Resources copies + AssetDatabase.Refresh; it does NOT commit and
// does NOT call EditorApplication.Exit (the batch wrapper owns the exit).
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Editor
{
    /// <summary>Additively merges the owner-curated gear picks (+ referenced blink_armor
    /// defaults) into the runtime-winning Resources canonical copies. Never drops. WO-747.</summary>
    public static class GearCurationExporter
    {
        private const string StreamingWeapons = "Assets/StreamingAssets/Data/Canonical/weapons.json";
        private const string StreamingArmor   = "Assets/StreamingAssets/Data/Canonical/armor.json";
        private const string ResourcesWeapons = "Assets/Resources/Data/Canonical/weapons.json";
        private const string ResourcesArmor   = "Assets/Resources/Data/Canonical/armor.json";
        private const string PicksPath        = "Assets/Editor/GearCurationPicks.json";

        private const string GeneratedMarker =
            "GearCurationExporter (additive merge) from StreamingAssets library + GearCurationPicks.json - DO NOT hand-edit";

        [MenuItem("Defenders/Gear/Export Curated Catalog -> Resources")]
        public static void ExportMenu() => Export();

        /// <summary>Headless/batchmode entry. Logs a machine-readable summary line.
        /// Never calls EditorApplication.Exit (the run wrapper owns exit).</summary>
        public static void Export()
        {
            FlowTrace.Step("GearExport", "begin ADDITIVE curated catalog merge (WO-747)");

            int weaponsTotal = -1, armorTotal = -1, weaponsAdded = 0, armorAdded = 0;

            Guard.Try("GearExport", "additive merge weapons + armor into Resources", () =>
            {
                // --- Load inputs (library = source of curated rows; Resources = base) ----
                JObject weaponsLib = ReadJsonObject(StreamingWeapons);
                JObject armorLib   = ReadJsonObject(StreamingArmor);
                JObject resWeapons = ReadJsonObject(ResourcesWeapons);
                JObject resArmor   = ReadJsonObject(ResourcesArmor);
                if (weaponsLib == null) { FlowTrace.Fail("GearExport", $"streaming weapons library missing/unreadable: {StreamingWeapons}"); return; }
                if (armorLib == null)   { FlowTrace.Fail("GearExport", $"streaming armor library missing/unreadable: {StreamingArmor}"); return; }
                if (resWeapons == null) { FlowTrace.Fail("GearExport", $"Resources weapons copy missing/unreadable: {ResourcesWeapons}"); return; }
                if (resArmor == null)   { FlowTrace.Fail("GearExport", $"Resources armor copy missing/unreadable: {ResourcesArmor}"); return; }

                var picks = ReadIncludedPickIds(PicksPath);
                if (picks == null)
                {
                    FlowTrace.Warn("GearExport", $"picks file missing/unreadable ({PicksPath}) - merging referenced defaults only.");
                    picks = new List<string>();
                }

                var weaponLibRows = RowMap(weaponsLib, "weapons");
                var armorLibRows  = RowMap(armorLib, "armor");

                // --- Classify picks by which library owns the id -------------------------
                var weaponAdds = new List<string>();
                var armorAdds  = new List<string>();
                foreach (var id in picks)
                {
                    if (weaponLibRows.ContainsKey(id)) weaponAdds.Add(id);
                    else if (armorLibRows.ContainsKey(id)) armorAdds.Add(id);
                    else FlowTrace.Fail("GearExport", $"picked id '{id}' is in NEITHER library - cannot pull a row, skipped.");
                }

                // Referenced blink_armor class defaults are always merged (from the library).
                foreach (var id in DataWebRegression.ReferencedDefaultArmorIds)
                {
                    if (armorLibRows.ContainsKey(id)) armorAdds.Add(id);
                    else FlowTrace.Warn("GearExport", $"referenced default armor id '{id}' absent from the library - cannot merge.");
                }

                // --- Additive merge (current Resources rows preserved, curated rows added)
                weaponsTotal = MergeAdditive(resWeapons, "weapons", weaponLibRows, weaponAdds, ResourcesWeapons, out weaponsAdded);
                armorTotal   = MergeAdditive(resArmor, "armor", armorLibRows, armorAdds, ResourcesArmor, out armorAdded);
            });

            if (weaponsTotal >= 0 && armorTotal >= 0)
            {
                AssetDatabase.Refresh();
                Debug.Log($"GEAR_CURATION_EXPORT weapons={weaponsTotal} (+{weaponsAdded} added) " +
                          $"armor={armorTotal} (+{armorAdded} added)");
                FlowTrace.Step("GearExport", $"done - Resources now holds {weaponsTotal} weapons (+{weaponsAdded}) + {armorTotal} armor (+{armorAdded}); nothing dropped.");
            }
            else
            {
                FlowTrace.Fail("GearExport", "merge aborted - an input was missing (see prior Fail lines). Resources left untouched.");
            }
        }

        // =====================================================================
        // Core merge
        // =====================================================================

        /// <summary>Writes <paramref name="resourcesAssetPath"/> = ALL current Resources rows
        /// (verbatim, authored fields win) UNION the library rows for <paramref name="addIds"/>
        /// whose id is not already present (deep-cloned, every field preserved). Top-level shape
        /// + version are preserved from the current Resources root; "_generated" is (re)stamped.
        /// Returns the total row count; <paramref name="added"/> = how many curated rows were appended.</summary>
        private static int MergeAdditive(JObject resRoot, string arrayKey,
                                         IReadOnlyDictionary<string, JObject> libRows,
                                         List<string> addIds, string resourcesAssetPath, out int added)
        {
            added = 0;
            var outArr = new JArray();
            var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1) Keep EVERY current Resources row, verbatim (never drop; authored wins).
            var srcArr = resRoot[arrayKey] as JArray;
            if (srcArr != null)
                foreach (var tok in srcArr)
                {
                    var row = tok as JObject;
                    if (row == null) continue;
                    outArr.Add(row.DeepClone());
                    string id = (string)row["id"];
                    if (!string.IsNullOrEmpty(id)) present.Add(id);
                }

            // 2) Append curated library rows for ids not already present (Resources wins on conflict).
            foreach (var id in addIds)
            {
                if (string.IsNullOrEmpty(id) || present.Contains(id)) continue;
                if (!libRows.TryGetValue(id, out var libRow))
                {
                    FlowTrace.Fail("GearExport", $"curated id '{id}' had no row in the '{arrayKey}' library - skipped.");
                    continue;
                }
                outArr.Add(libRow.DeepClone());
                present.Add(id);
                added++;
            }

            // Preserve the current Resources top-level shape + version + any _note; stamp _generated.
            var root = (JObject)resRoot.DeepClone();
            root["_generated"] = GeneratedMarker;
            root[arrayKey] = outArr;

            WriteUtf8NoBom(resourcesAssetPath, root.ToString(Formatting.Indented));
            FlowTrace.Step("GearExport", $"merged '{arrayKey}': {outArr.Count} row(s) total (+{added} curated) -> {resourcesAssetPath}");
            return outArr.Count;
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        /// <summary>Reads the included pick ids ({picks:[{id,included,...}]}). Returns null
        /// only when the file is missing/unreadable (caller treats that as empty picks).</summary>
        private static List<string> ReadIncludedPickIds(string assetPath)
        {
            JObject obj = ReadJsonObject(assetPath);
            if (obj == null) return null;
            var ids = new List<string>();
            var arr = obj["picks"] as JArray;
            if (arr == null) return ids;
            foreach (var tok in arr)
            {
                var p = tok as JObject;
                if (p == null) continue;
                bool included = p["included"] != null && p["included"].Type == JTokenType.Boolean && (bool)p["included"];
                string id = (string)p["id"];
                if (included && !string.IsNullOrEmpty(id)) ids.Add(id);
            }
            return ids;
        }

        /// <summary>id -> row map for a library array (first occurrence wins on a dup id).</summary>
        private static Dictionary<string, JObject> RowMap(JObject lib, string arrayKey)
        {
            var map = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);
            var arr = lib[arrayKey] as JArray;
            if (arr == null) return map;
            foreach (var tok in arr)
            {
                var row = tok as JObject;
                string id = row != null ? (string)row["id"] : null;
                if (!string.IsNullOrEmpty(id) && !map.ContainsKey(id)) map[id] = row;
            }
            return map;
        }

        private static JObject ReadJsonObject(string assetPath)
        {
            try
            {
                string full = Path.GetFullPath(assetPath);
                if (!File.Exists(full)) return null;
                string text = File.ReadAllText(full);
                if (string.IsNullOrWhiteSpace(text)) return null;
                return JObject.Parse(text);
            }
            catch (Exception ex)
            {
                FlowTrace.Fail("GearExport", $"could not read {assetPath}: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        private static void WriteUtf8NoBom(string assetPath, string contents)
        {
            string full = Path.GetFullPath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            // UTF-8 no BOM, LF - matches the canonical JSON convention + keeps the
            // CompileGate NUL/BOM guard happy.
            File.WriteAllText(full, contents.Replace("\r\n", "\n"), new UTF8Encoding(false));
        }
    }
}
