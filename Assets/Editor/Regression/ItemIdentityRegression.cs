// =============================================================================
// ItemIdentityRegression [item-identity]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
//
// Pins owner F8 seq 641 (live felt-test 2026-08-02): "shows as poition but says
// iron scrap" - a bag row rendered a health-potion sprite under the name
// "Iron Scrap".
//
// THE PROVEN DEFECT (both halves):
//   DATA SHAPE - "IronScrap" is a materials.json row (displayName "Iron Scrap",
//                category "metal", no authored iconPath). It is NOT a consumable.
//   CODE       - a bag row resolved its two halves from DIFFERENT sources:
//                  NAME  InventoryVM.BuildConsumables set `name = id`, which the
//                        sidebar spaced into "Iron Scrap".
//                  ICON  InventoryGrid -> ConsumableIcon -> a keyword guess over
//                        the id, ending in an UNCONDITIONAL health-bottle return
//                        for anything that matched nothing.
//                So the id resolved a MATERIAL name and a POTION sprite.
//
// Cases:
//   1 [id-collision]   No id appears in more than ONE catalog (consumables /
//                      materials / weapons / armor / accessories). A shared id is
//                      the OTHER way name and art can disagree, and it must stay
//                      impossible - ItemIdentity resolves consumables-then-
//                      materials, which is only unambiguous while this holds.
//   2 [row-identity]   Every consumables.json / materials.json row carries BOTH
//                      halves of its own identity: a non-empty displayName AND a
//                      non-empty glyph. The glyph is the terminal art fallback; a
//                      row without one is a row that must borrow somebody's art.
//   3 [icon-path]      Every authored iconPath actually exists under
//                      Assets/Resources - an iconPath that resolves null silently
//                      demotes the row back to the keyword/glyph path.
//   4 [same-row-code]  Source lint on the three files that build a bag row: the
//                      VM must resolve name + icon role from ItemIdentity (not
//                      `name = id` + a hardcoded potion role), and the potion
//                      fallbacks must be gated on ItemIdentity.IsConsumable. This
//                      is the code half of the defect, and what regresses is a
//                      DELETED term - exactly what a lint catches.
//   5 [dual-copy]      consumables.json + materials.json are byte-identical across
//                      Resources and StreamingAssets. (weapons.json is deliberately
//                      NOT such a pair - see ShieldDefenseRegression - so it is
//                      only read here for the collision check, never compared.)
//
// NOTE, not a failure: loot-tables.json can drop materialIds that no catalog owns.
// Those reach the bag with no authored name or glyph. They are reported in the
// reason string for the PO to author; failing on them would be failing on missing
// CONTENT, which is the owner's call, not this suite's.
//
// Markers: ITEM_IDENTITY_OK / ITEM_IDENTITY_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.ItemIdentityRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class ItemIdentityRegression
    {
        private const string ConsumablesRes = "Assets/Resources/Data/Canonical/consumables.json";
        private const string ConsumablesSA  = "Assets/StreamingAssets/Data/Canonical/consumables.json";
        private const string MaterialsRes   = "Assets/Resources/Data/Canonical/materials.json";
        private const string MaterialsSA    = "Assets/StreamingAssets/Data/Canonical/materials.json";
        private const string WeaponsRes     = "Assets/Resources/Data/Canonical/weapons.json";
        private const string ArmorRes       = "Assets/Resources/Data/Canonical/armor.json";
        private const string AccessoriesRes = "Assets/Resources/Data/Canonical/accessories.json";
        private const string LootTablesRes  = "Assets/Resources/Data/Canonical/loot-tables.json";

        private const string ResourcesRoot  = "Assets/Resources/";

        // Held as consts (never as inline literals) so this file's own brace count stays
        // balanced for the CLAUDE.md section-1 gate - a lint suite must not trip the lint.
        private const char OpenBrace  = '{';
        private const char CloseBrace = '}';

        private const string VmSrc      = "Assets/_Modules/Village/Hero/InventoryVM.cs";
        private const string GridSrc    = "Assets/_Modules/Village/Hero/InventoryGrid.cs";
        private const string ControlSrc = "Assets/_Modules/Village/Hero/HeroInventoryController.cs";

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("ITEM_IDENTITY_OK - " + reason);
            else Debug.LogError("ITEM_IDENTITY_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                Case(failures, "id-collision",  () => Case1_NoCrossCatalogIdCollision(failures, notes));
                Case(failures, "row-identity",  () => Case2_EveryRowCarriesItsOwnIdentity(failures, notes));
                Case(failures, "icon-path",     () => Case3_AuthoredIconPathsExist(failures, notes));
                Case(failures, "same-row-code", () => Case4_RowHalvesShareOneSource(failures));
                Case(failures, "dual-copy",     () => Case5_DualCopiesIdentical(failures));
                Case(failures, "drop-coverage", () => Note_UnknownDroppedMaterials(notes));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "ITEM IDENTITY OK - no id is owned by two catalogs, every consumable/material row " +
                         "carries its own displayName + glyph, every authored iconPath exists in Resources, " +
                         "the bag row resolves NAME and ICON from the SAME ItemIdentity row (no potion " +
                         "fallback for a non-consumable), and both catalogs are byte-identical dual copies" +
                         noteStr;
                return true;
            }
            reason = "item-identity FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  CASE 1 - an id belongs to exactly ONE catalog
        // =====================================================================
        private static void Case1_NoCrossCatalogIdCollision(List<string> failures, List<string> notes)
        {
            var catalogs = new (string label, string path, string arrayKey)[]
            {
                ("consumables", ConsumablesRes, "consumables"),
                ("materials",   MaterialsRes,   "materials"),
                ("weapons",     WeaponsRes,     "weapons"),
                ("armor",       ArmorRes,       "armor"),
                ("accessories", AccessoriesRes, "accessories"),
            };

            // id (lowercased) -> the catalogs that claim it.
            var owners = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var c in catalogs)
            {
                var ids = ReadIds(c.path, c.arrayKey, failures, "id-collision");
                notes.Add(c.label + "=" + ids.Count);
                foreach (var id in ids)
                {
                    string key = id.ToLowerInvariant();
                    List<string> list;
                    if (!owners.TryGetValue(key, out list)) { list = new List<string>(); owners[key] = list; }
                    if (!list.Contains(c.label)) list.Add(c.label);
                }
            }

            foreach (var kv in owners)
            {
                if (kv.Value.Count < 2) continue;
                failures.Add("[id-collision] id '" + kv.Key + "' is claimed by " + string.Join(" + ", kv.Value) +
                             " - one owned id would then resolve its NAME from whichever catalog is consulted " +
                             "first and its ART from whichever the icon path consults, which is precisely the " +
                             "F8-641 symptom (a potion sprite under a material's name) with a different cause");
            }
        }

        // =====================================================================
        //  CASE 2 - a row can always identify itself without borrowing
        // =====================================================================
        private static void Case2_EveryRowCarriesItsOwnIdentity(List<string> failures, List<string> notes)
        {
            int checkedRows = 0;
            foreach (var (path, key) in new[] { (ConsumablesRes, "consumables"), (MaterialsRes, "materials") })
            {
                foreach (var row in ReadRows(path, key, failures, "row-identity"))
                {
                    checkedRows++;
                    string id = (string)row["id"];
                    if (string.IsNullOrEmpty(id))
                    {
                        failures.Add("[row-identity] " + key + " has a row with no 'id' - it can never be resolved " +
                                     "by id, so anything owning it shows a raw/blank identity");
                        continue;
                    }

                    string display = (string)row["displayName"];
                    if (string.IsNullOrEmpty(display))
                        failures.Add("[row-identity] '" + id + "' (" + key + ") has no displayName - the bag falls " +
                                     "back to spacing the raw id, which is how a materials row printed 'Iron Scrap' " +
                                     "while its art came from somewhere else entirely");

                    string glyph = (string)row["glyph"];
                    if (string.IsNullOrEmpty(glyph))
                        failures.Add("[row-identity] '" + id + "' (" + key + ") has no glyph - the glyph is the " +
                                     "TERMINAL art fallback once the potion fallback is (correctly) refused, so a " +
                                     "row without one renders an empty cell or borrows a neighbour's sprite");
                }
            }
            notes.Add("identity rows checked=" + checkedRows);
        }

        // =====================================================================
        //  CASE 3 - an authored iconPath must actually exist
        // =====================================================================
        private static void Case3_AuthoredIconPathsExist(List<string> failures, List<string> notes)
        {
            string[] extensions = { ".png", ".jpg", ".jpeg", ".asset", ".psd", ".tga" };
            int authored = 0, missing = 0;

            foreach (var (path, key) in new[] { (ConsumablesRes, "consumables"), (MaterialsRes, "materials") })
            {
                foreach (var row in ReadRows(path, key, failures, "icon-path"))
                {
                    string iconPath = (string)row["iconPath"];
                    if (string.IsNullOrEmpty(iconPath)) continue;
                    authored++;

                    bool found = false;
                    foreach (var ext in extensions)
                    {
                        if (File.Exists(ResourcesRoot + iconPath + ext)) { found = true; break; }
                    }
                    if (!found)
                    {
                        missing++;
                        failures.Add("[icon-path] '" + (string)row["id"] + "' (" + key + ") authors iconPath '" +
                                     iconPath + "' but no asset exists at " + ResourcesRoot + iconPath +
                                     ".<png|jpg|asset> - Resources.Load returns null and the row silently drops " +
                                     "back to the generic fallback path the F8-641 fix exists to constrain");
                    }
                }
            }
            notes.Add("authored iconPaths=" + authored + " missing=" + missing);
        }

        // =====================================================================
        //  CASE 4 - name and icon are read off the SAME row, in code
        // =====================================================================
        private static void Case4_RowHalvesShareOneSource(List<string> failures)
        {
            // --- the VM half: the row's NAME + icon ROLE come from ItemIdentity ---
            string vm = ReadStrippedSource(VmSrc, failures, "same-row-code");
            if (vm != null)
            {
                var body = ExtractMethod(vm, "BuildConsumables");
                if (body == null)
                {
                    failures.Add("[same-row-code] InventoryVM.BuildConsumables not found - the bag's owned-row " +
                                 "projection moved without re-pointing this oracle; re-point it deliberately");
                }
                else
                {
                    if (body.IndexOf("ItemIdentity", StringComparison.Ordinal) < 0)
                        failures.Add("[same-row-code] InventoryVM.BuildConsumables no longer consults ItemIdentity - " +
                                     "the row's NAME and its ICON ROLE are back to being derived independently, which " +
                                     "is the F8-641 defect verbatim");

                    if (Regex.IsMatch(body, @"\bstring\s+name\s*=\s*id\s*;"))
                        failures.Add("[same-row-code] InventoryVM.BuildConsumables sets `name = id` again - the raw id " +
                                     "then gets spaced into a plausible-looking name ('IronScrap' -> 'Iron Scrap') that " +
                                     "was never checked against the row supplying the art");

                    if (body.IndexOf("IconRoleMaterial", StringComparison.Ordinal) < 0)
                        failures.Add("[same-row-code] InventoryVM.BuildConsumables never assigns IconRoleMaterial - " +
                                     "every catch-all row is typed as a potion again, so a crafting material is once " +
                                     "more routed down the potion art path");
                }
            }

            // --- the View half: the potion fallback is gated on the row being a consumable ---
            string ctl = ReadStrippedSource(ControlSrc, failures, "same-row-code");
            if (ctl != null)
            {
                var body = ExtractMethod(ctl, "ConsumableIcon");
                if (body == null)
                {
                    failures.Add("[same-row-code] HeroInventoryController.ConsumableIcon not found - the potion " +
                                 "fallback moved; re-point this oracle at its new home deliberately");
                }
                else if (!Regex.IsMatch(body, @"ItemIdentity\s*\.\s*Is(Consumable|Material)|ItemIdentity\s*\.\s*KindOf"))
                {
                    failures.Add("[same-row-code] HeroInventoryController.ConsumableIcon does not gate its pack-potion " +
                                 "fallbacks on ItemIdentity - the method ends in an unconditional health bottle, so " +
                                 "ANY id that matches no keyword (every crafting material) renders as a potion under " +
                                 "its own material name. This exact line is F8-641");
                }
            }

            // --- the grid half: the material role has its own, non-potion art path ---
            string grid = ReadStrippedSource(GridSrc, failures, "same-row-code");
            if (grid != null)
            {
                if (grid.IndexOf("IconRoleMaterial", StringComparison.Ordinal) < 0)
                    failures.Add("[same-row-code] InventoryGrid does not handle InventoryVM.IconRoleMaterial - a " +
                                 "material-role slot falls through ResolveItemIcon to null with no material art path, " +
                                 "or worse is folded back into the potion case");

                if (grid.IndexOf("ForMaterial", StringComparison.Ordinal) < 0)
                    failures.Add("[same-row-code] InventoryGrid never calls ItemIconCatalog.ForMaterial - material art " +
                                 "is being resolved through the potion-biased keyword mapper again ('HealthHerb' and " +
                                 "'Oil Flask' both keyword-match potion rows)");
            }
        }

        // =====================================================================
        //  CASE 5 - the two identity catalogs are byte-identical dual copies
        // =====================================================================
        private static void Case5_DualCopiesIdentical(List<string> failures)
        {
            foreach (var (a, b, label) in new[]
            {
                (ConsumablesRes, ConsumablesSA, "consumables.json"),
                (MaterialsRes,   MaterialsSA,   "materials.json"),
            })
            {
                if (!File.Exists(a)) { failures.Add("[dual-copy] missing " + a); continue; }
                if (!File.Exists(b)) { failures.Add("[dual-copy] missing " + b); continue; }

                byte[] ba = File.ReadAllBytes(a);
                byte[] bb = File.ReadAllBytes(b);
                if (ba.Length != bb.Length || !ByteEquals(ba, bb))
                    failures.Add("[dual-copy] " + label + " differs between Resources (" + ba.Length + " bytes) and " +
                                 "StreamingAssets (" + bb.Length + " bytes) - the editor and the shipped player would " +
                                 "then disagree about an item's NAME or ICON, which reproduces F8-641 on device only");
            }
        }

        // =====================================================================
        //  NOTE - dropped material ids no catalog owns (content gap, PO's call)
        // =====================================================================
        private static void Note_UnknownDroppedMaterials(List<string> notes)
        {
            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sink = new List<string>();
            foreach (var id in ReadIds(MaterialsRes, "materials", sink, "drop-coverage")) known.Add(id);
            foreach (var id in ReadIds(ConsumablesRes, "consumables", sink, "drop-coverage")) known.Add(id);

            if (!File.Exists(LootTablesRes)) { notes.Add("loot-tables.json missing - drop coverage unknown"); return; }

            var unknown = new List<string>();
            try
            {
                string json = File.ReadAllText(LootTablesRes);
                foreach (Match m in Regex.Matches(json, "\"materialId\"\\s*:\\s*\"([^\"]+)\""))
                {
                    string id = m.Groups[1].Value;
                    if (!known.Contains(id) && !unknown.Contains(id)) unknown.Add(id);
                }
            }
            catch (Exception ex)
            {
                notes.Add("loot-tables.json unreadable (" + ex.GetType().Name + ")");
                return;
            }

            notes.Add(unknown.Count == 0
                ? "every dropped materialId has an identity row"
                : "PO: " + unknown.Count + " dropped materialId(s) have NO identity row (no displayName, no glyph) - " +
                  string.Join(",", unknown));
        }

        // =====================================================================
        //  HELPERS
        // =====================================================================
        private static bool ByteEquals(byte[] a, byte[] b)
        {
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static List<string> ReadIds(string path, string arrayKey, List<string> failures, string caseName)
        {
            var ids = new List<string>();
            foreach (var row in ReadRows(path, arrayKey, failures, caseName))
            {
                string id = (string)row["id"];
                if (!string.IsNullOrEmpty(id)) ids.Add(id);
            }
            return ids;
        }

        private static List<JObject> ReadRows(string path, string arrayKey, List<string> failures, string caseName)
        {
            var result = new List<JObject>();
            if (!File.Exists(path))
            {
                failures.Add("[" + caseName + "] " + path + " not found");
                return result;
            }
            try
            {
                var root = JObject.Parse(File.ReadAllText(path));
                var arr = root[arrayKey] as JArray;
                if (arr == null)
                {
                    failures.Add("[" + caseName + "] " + path + " has no '" + arrayKey + "' array");
                    return result;
                }
                foreach (var r in arr) { var o = r as JObject; if (o != null) result.Add(o); }
            }
            catch (Exception ex)
            {
                failures.Add("[" + caseName + "] " + path + " failed to parse (" + ex.GetType().Name + ": " + ex.Message + ")");
            }
            return result;
        }

        private static string ReadStrippedSource(string path, List<string> failures, string caseName)
        {
            if (!File.Exists(path))
            {
                failures.Add("[" + caseName + "] " + path + " not found - the file moved without updating this oracle");
                return null;
            }
            try { return StripComments(File.ReadAllText(path)); }
            catch (Exception ex)
            {
                failures.Add("[" + caseName + "] could not read " + path + ": " + ex.GetType().Name + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>Strips // and comment blocks so a lint can never be satisfied by prose.</summary>
        private static string StripComments(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;
            string noBlock = Regex.Replace(src, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(noBlock, @"//[^\r\n]*", " ");
        }

        /// <summary>
        /// The brace-balanced body of a named method, or null. Comment-stripped source only,
        /// so a method name mentioned in prose can never satisfy the lint.
        /// </summary>
        private static string ExtractMethod(string strippedSource, string methodName)
        {
            if (string.IsNullOrEmpty(strippedSource)) return null;
            var m = Regex.Match(strippedSource,
                @"\b" + Regex.Escape(methodName) + @"\s*\([^)]*\)\s*" + Regex.Escape(OpenBrace.ToString()));
            if (!m.Success) return null;

            int start = strippedSource.IndexOf(OpenBrace, m.Index);
            if (start < 0) return null;

            int depth = 0;
            for (int i = start; i < strippedSource.Length; i++)
            {
                char c = strippedSource[i];
                if (c == OpenBrace) depth++;
                else if (c == CloseBrace)
                {
                    depth--;
                    if (depth == 0) return strippedSource.Substring(start, i - start + 1);
                }
            }
            return null;
        }
    }
}
