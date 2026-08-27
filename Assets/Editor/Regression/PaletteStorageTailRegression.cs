// =============================================================================
// PaletteStorageTailRegression [palette-storage-tail]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
//
// WO-1082 SECTION 4a, OWNER RULING 2026-08-26 (4b REJECTED).
// Owner, 2026-08-24, verbatim: "can we order the collectors as last item in array as
// they are only items they dont get free and build time is 7 minutes". Her two stated
// reasons are exactly true of the three STORAGE CONTAINERS and of nothing else in the
// Town palette: lumberyard / foundry / silo are the only rows carved out of the free
// 15-second first-build grace (BuildModeController "other than the pallets", 2026-08-06)
// and the only rows that take ~7.68 minutes. So the rows she wanted last are the
// containers, and this suite is what keeps them last.
//
// ⭐ THE LOAD-BEARING FACT THIS SUITE EXISTS TO PIN, and WO-1082 section 2 got it wrong:
//   THE FLAT STRIP ORDER IS **TYPE-MAJOR**, NOT ARRAY ORDER.
//   BuildPaletteVM.AggregateOfType walks the verb's catalogTypes IN ORDER and concatenates
//   CatalogRegistry.OfType(type) per type. So while the Town verb listed "Resource" before
//   "Collector", EVERY Collector row (collector_farm, collector_forge) trailed EVERY
//   Resource row no matter where it sat in structures-catalog entries[] -- and the three
//   containers are Resource rows. Moving rows inside the catalog alone therefore COULD NOT
//   put the containers last, which is why the fix is two data edits that only work
//   together:
//     (a) structures-catalog.json  - barracks moved ABOVE lumberyard, so the containers are
//                                    the last Resource rows;
//     (b) build-categories.json    - the Town row's catalogTypes lead with "Collector".
//   Either edit alone leaves a collector on the tail. A future seat "tidying" catalogTypes
//   back to Resource-first would silently undo the owner's ruling with no other symptom,
//   which is precisely the drift this file turns into a red gate.
//
// ⛔ AND THE TRAP ON THE OTHER SIDE: authoring a HIGH displayOrder on a container to push
//   it last does the OPPOSITE. BuildPaletteVM.OrderKey maps unauthored (0/absent) to
//   int.MaxValue, so ANY authored order sorts BEFORE every unauthored row -- displayOrder
//   900 on the Lumberyard would move it to the FRONT. Case [containers-unauthored] pins
//   that the three containers stay unauthored.
//
// WHAT IT PROVES (every case asserts the GOOD path, not only the failure):
//   (1) [town-tail]              - the REAL shipped projection (BuildPaletteVM over the real
//                                  BuildPaletteVM.SortForDisplay + the real lockedIds filter)
//                                  ends with lumberyard -> foundry -> silo, in that order.
//   (2) [town-head]              - and it still OPENS on collector_lumbermill. This is the
//                                  WO-963 tutorial ruling that 4b would have reversed; it is
//                                  asserted HERE too so "containers last" can never be bought
//                                  by moving the Lumber Mill off the first card.
//   (3) [containers-unauthored]  - none of the three containers authors a displayOrder, and
//                                  each is still type Resource (the type is what puts them
//                                  ahead of the Collector rows once Collector leads).
//   (4) [type-major]             - source lint: AggregateOfType still concatenates PER TYPE
//                                  IN catalogTypes ORDER. If that loop is ever replaced by a
//                                  single flat query, edit (b) stops meaning anything and this
//                                  suite would otherwise keep passing on a coincidence.
//   (5) [dual-copy]              - both canonical copies of both files are byte-identical.
//
// NOT provable here: how the shelf FEELS to scroll, and whether the containers read as
// "the expensive ones" at the end. That is the owner's felt-verify (PO closes).
//
// Marker: PALETTE_STORAGE_TAIL_OK / PALETTE_STORAGE_TAIL_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.PaletteStorageTailRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into DataRegression.RunAll
// is left to the committer (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DeNelle.Core.Catalog;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class PaletteStorageTailRegression
    {
        private const string ResCatalog    = "Assets/Resources/Data/Canonical/structures-catalog.json";
        private const string SaCatalog     = "Assets/StreamingAssets/Data/Canonical/structures-catalog.json";
        private const string ResCategories = "Assets/Resources/Data/Canonical/build-categories.json";
        private const string SaCategories  = "Assets/StreamingAssets/Data/Canonical/build-categories.json";
        private const string PaletteSrc    = "Assets/_Modules/Village/BuildMode/BuildPaletteVM.cs";

        /// <summary>The owner's three rows, in the order they must close the strip.</summary>
        private static readonly string[] StorageTail = { "lumberyard", "foundry", "silo" };

        /// <summary>The tutorial's first placement; it must stay the first card (WO-963).</summary>
        private const string LumbermillId = "collector_lumbermill";

        [Serializable]
        private sealed class CatalogFile
        {
            [JsonProperty("version")] public int Version;
            [JsonProperty("entries")] public List<CatalogEntry> Entries = new List<CatalogEntry>();
        }

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("PALETTE_STORAGE_TAIL_OK - " + reason);
            else Debug.LogError("PALETTE_STORAGE_TAIL_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            string strip = "<not projected>";
            try
            {
                CatalogFile catalog = ParseCatalog(failures);
                JObject townRow = ParseTownRow(failures);

                Case(failures, "dual-copy",             () => CaseDualCopy(failures));
                Case(failures, "containers-unauthored", () => CaseContainersUnauthored(catalog, failures));
                Case(failures, "type-major",            () => CaseTypeMajor(failures));
                Case(failures, "town-order",            () => strip = CaseTownOrder(catalog, townRow, failures));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "PALETTE STORAGE TAIL OK - the Town strip projects as [" + strip + "]: it opens on '" +
                         LumbermillId + "' and closes on " + string.Join(" -> ", StorageTail) +
                         ", none of the three containers authors a displayOrder, AggregateOfType is still " +
                         "type-major over catalogTypes, and both canonical copies of both files are " +
                         "byte-identical.";
                return true;
            }
            reason = "palette-storage-tail FAIL x" + failures.Count + ": " + string.Join(" | ", failures);
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  Parse - through the PRODUCTION serializer settings
        // =====================================================================

        // CatalogBootstrap.LoadFromJson's settings VERBATIM: parsing any other way would
        // green-tick a field the shipping loader silently drops.
        private static CatalogFile ParseCatalog(List<string> failures)
        {
            if (!File.Exists(ResCatalog))
            {
                failures.Add("[parse] " + ResCatalog + " is MISSING - there is no catalog to order.");
                return null;
            }
            var settings = new JsonSerializerSettings
            {
                Converters = { new StringEnumConverter() },
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore,
            };
            var file = JsonConvert.DeserializeObject<CatalogFile>(File.ReadAllText(ResCatalog), settings);
            if (file == null || file.Entries == null || file.Entries.Count == 0)
            {
                failures.Add("[parse] " + ResCatalog + " parsed to ZERO entries through the production settings.");
                return null;
            }
            return file;
        }

        private static JObject ParseTownRow(List<string> failures)
        {
            if (!File.Exists(ResCategories))
            {
                failures.Add("[parse] " + ResCategories + " is MISSING - the Town verb has no catalogTypes/lockedIds.");
                return null;
            }
            var root = JObject.Parse(File.ReadAllText(ResCategories));
            var cats = root["categories"] as JArray;
            if (cats == null || cats.Count == 0)
            {
                failures.Add("[parse] build-categories.json has no categories[] array.");
                return null;
            }
            foreach (var c in cats)
                if (string.Equals((string)c["buildType"], "Town", StringComparison.OrdinalIgnoreCase))
                    return c as JObject;
            failures.Add("[parse] build-categories.json has NO 'Town' category row - the palette this ticket " +
                         "orders does not exist.");
            return null;
        }

        // =====================================================================
        //  dual-copy - the standing law for both canonical files
        // =====================================================================

        private static void CaseDualCopy(List<string> failures)
        {
            AssertByteEqual(ResCatalog, SaCatalog, failures);
            AssertByteEqual(ResCategories, SaCategories, failures);
        }

        private static void AssertByteEqual(string a, string b, List<string> failures)
        {
            if (!File.Exists(a)) { failures.Add("[dual-copy] " + a + " is MISSING."); return; }
            if (!File.Exists(b)) { failures.Add("[dual-copy] " + b + " is MISSING."); return; }
            byte[] x = File.ReadAllBytes(a);
            byte[] y = File.ReadAllBytes(b);
            bool same = x.Length == y.Length;
            if (same)
                for (int i = 0; i < x.Length; i++)
                    if (x[i] != y[i]) { same = false; break; }
            if (!same)
                failures.Add("[dual-copy] " + a + " and " + b + " DIFFER (" + x.Length + " vs " + y.Length +
                             " bytes). Resources WINS at runtime, so the shipped order and the editor-read " +
                             "order would disagree.");
        }

        // =====================================================================
        //  containers-unauthored - the displayOrder trap, stated as an assertion
        // =====================================================================

        private static void CaseContainersUnauthored(CatalogFile file, List<string> failures)
        {
            if (file == null)
            {
                failures.Add("[containers-unauthored] the catalog did not parse - the three storage rows could " +
                             "not be checked at all (this is a FAILURE, not a skip).");
                return;
            }
            int found = 0;
            foreach (string id in StorageTail)
            {
                CatalogEntry e = Find(file, id);
                if (e == null)
                {
                    failures.Add("[containers-unauthored] storage row '" + id + "' is MISSING from the catalog - " +
                                 "ids are frozen save keys and the owner's WO-1082 ruling names this row.");
                    continue;
                }
                found++;
                if (e.displayOrder > 0)
                    failures.Add("[containers-unauthored] '" + id + "' authors displayOrder " + e.displayOrder +
                                 " - an AUTHORED order sorts BEFORE every unauthored row (BuildPaletteVM.OrderKey " +
                                 "maps unauthored to int.MaxValue), so this moves the container to the FRONT, the " +
                                 "exact opposite of WO-1082. Leave it unauthored and move the ROW instead.");
                if (e.type != CatalogType.Resource)
                    failures.Add("[containers-unauthored] '" + id + "' is type " + e.type + ", expected Resource - " +
                                 "the containers sit last BECAUSE Resource trails Collector in the Town verb's " +
                                 "catalogTypes. Change the type and the tail silently reshuffles.");
            }
            if (found != StorageTail.Length)
                failures.Add("[containers-unauthored] only " + found + " of " + StorageTail.Length +
                             " storage rows were found - the check did not cover what it claims to cover.");
        }

        // =====================================================================
        //  type-major - the source seam edit (b) depends on
        // =====================================================================

        private static void CaseTypeMajor(List<string> failures)
        {
            if (!File.Exists(PaletteSrc))
            {
                failures.Add("[type-major] " + PaletteSrc + " is MISSING - the aggregation seam cannot be linted.");
                return;
            }
            string src = File.ReadAllText(PaletteSrc);
            if (src.IndexOf("AggregateOfType", StringComparison.Ordinal) < 0)
            {
                failures.Add("[type-major] BuildPaletteVM no longer declares AggregateOfType - the Town strip's " +
                             "order no longer comes from catalogTypes at all, so build-categories.json's " +
                             "'Collector before Resource' has stopped meaning anything and the containers may " +
                             "no longer be last.");
                return;
            }
            if (src.IndexOf("foreach (var type in types)", StringComparison.Ordinal) < 0)
                failures.Add("[type-major] AggregateOfType no longer walks 'foreach (var type in types)' - the " +
                             "concatenation is what makes the Town verb's catalogTypes ORDER (Collector, then " +
                             "Resource) decide which family trails the strip. WO-1082's fix is half data, half " +
                             "this loop.");
            if (src.IndexOf("SortForDisplay", StringComparison.Ordinal) < 0)
                failures.Add("[type-major] BuildPaletteVM no longer calls SortForDisplay - WO-963's authored " +
                             "head would be gone and this suite's head assertion would be meaningless.");
        }

        // =====================================================================
        //  town-order - the REAL shipped projection, head AND tail
        // =====================================================================

        private static string CaseTownOrder(CatalogFile file, JObject town, List<string> failures)
        {
            if (file == null || town == null)
            {
                failures.Add("[town-order] the catalog and/or the Town verb row did not parse, so the shipped " +
                             "strip could not be projected at all (this is a FAILURE, not a skip).");
                return "<not projected>";
            }

            // The verb's declared types, IN AUTHORED ORDER - that order is half the fix.
            var types = new List<CatalogType>();
            var typeArr = town["catalogTypes"] as JArray;
            if (typeArr == null || typeArr.Count == 0)
            {
                failures.Add("[town-order] the Town row authors no catalogTypes.");
                return "<not projected>";
            }
            foreach (var t in typeArr)
            {
                string s = (string)t;
                if (Enum.TryParse(s, true, out CatalogType ct)) types.Add(ct);
                else failures.Add("[town-order] catalogTypes names '" + s + "', which is not a CatalogType.");
            }
            if (types.Count == 0) return "<not projected>";

            var lockedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (town["lockedIds"] is JArray la)
                foreach (var x in la) { string s = (string)x; if (!string.IsNullOrEmpty(s)) lockedIds.Add(s); }

            var visibleLocked = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (town["visibleLockedIds"] is JObject vo)
                foreach (var kv in vo) visibleLocked[kv.Key] = (string)kv.Value ?? "Locked";

            // AggregateOfType, replicated in the ONE place a test may replicate it - and the
            // [type-major] case above is what keeps this replica honest.
            var aggregated = new List<CatalogEntry>();
            foreach (var t in types)
                foreach (var e in file.Entries)
                    if (e != null && e.type == t) aggregated.Add(e);

            if (aggregated.Count == 0)
            {
                failures.Add("[town-order] the Town verb aggregated ZERO catalog rows - the palette would be " +
                             "empty, so the tail assertion below would pass vacuously.");
                return "<not projected>";
            }

            // The REAL VM: real SortForDisplay, real lockedIds filter, real visible-lock pass.
            // unlockedProvider is pinned to "nothing unlocked yet" so the projection is the
            // FIRST-SESSION shelf and never depends on this machine's PlayerPrefs.
            var category = new BuildCategory
            {
                Types = types.ToArray(),
                Label = "Build Town",
                LockedIds = lockedIds,
                VisibleLockedReasons = visibleLocked,
            };
            var vm = new BuildPaletteVM(
                null,
                _ => category,
                _ => aggregated,
                _ => false,
                () => aggregated.Count,
                BuildType.Town,
                null,
                _ => false);

            var ids = new List<string>();
            foreach (var c in vm.Cards) ids.Add(c != null ? c.Id : "<null>");
            string strip = string.Join(",", ids.ToArray());

            if (ids.Count < StorageTail.Length + 1)
            {
                failures.Add("[town-order] the Town strip projected only " + ids.Count + " card(s) [" + strip +
                             "] - too few to assert a head AND a three-row tail.");
                return strip;
            }

            // HEAD - the good path, stated positively (WO-963 / the ruling 4b would have reversed).
            if (!string.Equals(ids[0], LumbermillId, StringComparison.OrdinalIgnoreCase))
                failures.Add("[town-order] the strip OPENS on '" + ids[0] + "', expected '" + LumbermillId +
                             "'. WO-963 (owner 2026-08-10) seeds the carousel to the tutorial's teaching order " +
                             "and the founding beat highlights build.card." + LumbermillId +
                             ". Putting the containers last must never cost the Lumber Mill its first card - " +
                             "that is exactly why WO-1082 section 4b was REJECTED on 2026-08-26.");

            // TAIL - the owner's ask.
            for (int i = 0; i < StorageTail.Length; i++)
            {
                int at = ids.Count - StorageTail.Length + i;
                if (!string.Equals(ids[at], StorageTail[i], StringComparison.OrdinalIgnoreCase))
                    failures.Add("[town-order] tail slot " + (i + 1) + " of " + StorageTail.Length + " is '" +
                                 ids[at] + "', expected '" + StorageTail[i] + "'. The strip is [" + strip +
                                 "]. WO-1082 section 4a (owner ruling 2026-08-26): Lumberyard -> Foundry -> Silo " +
                                 "close the Town palette because they are the only rows carved out of the free " +
                                 "15-second first build and the only rows that take ~7.68 minutes. The two levers " +
                                 "are the barracks row position in structures-catalog.json AND 'Collector' " +
                                 "leading the Town catalogTypes in build-categories.json - check BOTH.");
            }

            return strip;
        }

        private static CatalogEntry Find(CatalogFile file, string id)
        {
            if (file == null || file.Entries == null) return null;
            foreach (var e in file.Entries)
                if (e != null && string.Equals(e.id, id, StringComparison.OrdinalIgnoreCase)) return e;
            return null;
        }
    }
}
