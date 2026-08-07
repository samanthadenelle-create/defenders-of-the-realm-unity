// =============================================================================
// DungeonEncounterFamilyRegression (WO-1001 Phase 1, slice 2) - the per-encounter
// ENEMY FAMILY contract for composed dungeons.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (editor-only).  Namespace: DeNelle.Editor.Regression
//
// Markers: DUNGEON_ENCOUNTER_FAMILY_OK / DUNGEON_ENCOUNTER_FAMILY_FAIL (FAIL via
// Debug.LogError -> break-log.jsonl, per docs/INSTRUMENTATION_STANDARD.md 4/5).
//
// Runs standalone:
//   run-unity-method DeNelle.Editor.Regression.DungeonEncounterFamilyRegression.RunAll
// AND is wired into DataRegression.RunAll as [dungeon-encounter-family]
// (Run(out string reason), the same covenant-style contract as the sibling oracles).
//
// THE DEFECT THIS PINS
//   OutpostEnemyGroupSpawner is the ONLY spawner composed dungeons use. Before
//   WO-1001 its id picker was FOUR HARDCODED hollow-* string literals taking no
//   family argument, and its DefFor() hand-wrote four EnemyDefs in C# that ignored
//   enemies.json outright (and had drifted from it - code hollow-walker Hp 40 vs
//   json 52, hollow-rogue Hp 34 vs json 70). DungeonBaker compared EncounterSpec.kind
//   ONLY to the literal "none"; every other value fell through to the same hollow
//   spawn. Authoring "orc-group" SILENTLY SPAWNED HOLLOWS. Silent is the bug.
//
// The 7 cases:
//   1  catalog          enemies.json parses from BOTH dual copies, same id set
//   2  ids-exist        every id the family tables emit is a real, non-boss roster id
//   3  hollow-compat    hollow-group is byte-identical to the retired hardcoded picker
//   4  family-purity    orc-group is all orc, troll-group all troll, hollow all hollow
//   5  kind-fallback    unknown kind -> hollow-group AND is flagged, never silent
//   6  wiring           the serialized field exists and the baker/binder write it
//   7  authoring        every kind authored in a shipped layout/graph is a known kind
//
// Reads only JSON + source text + the DeNelle.Village family tables. It NEVER opens
// or saves a .unity scene and references NO art, so it passes with the packs ABSENT.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class DungeonEncounterFamilyRegression
    {
        private const string EnemiesStreaming = "Assets/StreamingAssets/Data/Canonical/enemies.json";
        private const string EnemiesResources = "Assets/Resources/Data/Canonical/enemies.json";

        private static readonly string[] EncounterAuthoringDirs =
        {
            "Assets/StreamingAssets/Data/Canonical/dungeon-layouts",
            "Assets/StreamingAssets/Data/Canonical/dungeon-graphs",
            "Assets/Resources/Data/Canonical/dungeon-layouts",
            "Assets/Resources/Data/Canonical/dungeon-graphs",
        };

        private const string SpawnerSrc = "Assets/_Modules/Village/Enemies/OutpostEnemyGroupSpawner.cs";
        private const string BakerSrc = "Assets/Editor/RoomForge/DungeonBaker.cs";
        private const string BinderSrc = "Assets/_Modules/Dungeons/DungeonRoomBinder.cs";

        /// <summary>The serialized field DungeonBaker writes BY NAME via SerializedObject.</summary>
        private const string KindFieldName = "encounterKind";

        // The pre-WO-1001 hardcoded picker, restated here as the COMPATIBILITY ORACLE.
        // hollow-group must reproduce this stream exactly - it is what every shipped
        // dungeon room already spawns.
        private static readonly string[] LegacyHollowIds =
            { "hollow-walker", "hollow-rogue", "hollow-warrior", "hollow-acolyte" };

        private static string LegacyPick(System.Random rng)
        {
            int roll = rng.Next(0, 10);
            if (roll < 5) return "hollow-walker";
            if (roll < 7) return "hollow-rogue";
            if (roll < 9) return "hollow-warrior";
            return "hollow-acolyte";
        }

        // Parsed once per run by case 1 and reused by the later cases.
        private static EnemyCatalog s_catalog;

        // ---- entry points -------------------------------------------------------

        /// <summary>Standalone batch entry - prints the DUNGEON_ENCOUNTER_FAMILY_OK/_FAIL marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("DUNGEON_ENCOUNTER_FAMILY_OK - " + reason);
            else Debug.LogError("DUNGEON_ENCOUNTER_FAMILY_FAIL: " + reason);
        }

        /// <summary>Covenant contract for DataRegression.RunAll ([dungeon-encounter-family]). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            s_catalog = null;
            try
            {
                Case(failures, "catalog", () => Case1_Catalog(failures));
                Case(failures, "ids-exist", () => Case2_IdsExist(failures));
                Case(failures, "hollow-compat", () => Case3_HollowCompat(failures));
                Case(failures, "family-purity", () => Case4_FamilyPurity(failures));
                Case(failures, "kind-fallback", () => Case5_KindFallback(failures));
                Case(failures, "wiring", () => Case6_Wiring(failures));
                Case(failures, "authoring", () => Case7_Authoring(failures, notes));
            }
            finally
            {
                s_catalog = null;
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes.ToArray()) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "DUNGEON ENCOUNTER FAMILY OK - 7/7 cases pass (" + OutpostEnemyGroupSpawner.KnownKinds.Length +
                         " kinds, every family id real+non-boss, hollow-group stream identical to the retired " +
                         "hardcoded picker, unknown kind flagged not silent, baker+binder write '" +
                         KindFieldName + "')" + noteStr;
                return true;
            }
            reason = "DUNGEON ENCOUNTER FAMILY FAIL x" + failures.Count + ": " +
                     string.Join(" | ", failures.ToArray()) + noteStr;
            return false;
        }

        // Guard each case so one throw becomes a labelled failure, not a dead suite.
        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  CASE 1 - enemies.json parses from BOTH dual copies with the same ids
        // =====================================================================
        private static void Case1_Catalog(List<string> failures)
        {
            var streaming = LoadCatalog(EnemiesStreaming, failures);
            var resources = LoadCatalog(EnemiesResources, failures);
            s_catalog = streaming ?? resources;

            if (streaming == null || resources == null) return;

            var a = IdSet(streaming);
            var b = IdSet(resources);
            foreach (var id in a)
                if (!b.Contains(id))
                    failures.Add("[catalog] id '" + id + "' is in the StreamingAssets copy but NOT in Resources - dual-copy drift");
            foreach (var id in b)
                if (!a.Contains(id))
                    failures.Add("[catalog] id '" + id + "' is in the Resources copy but NOT in StreamingAssets - dual-copy drift");
        }

        private static EnemyCatalog LoadCatalog(string path, List<string> failures)
        {
            if (!File.Exists(path))
            {
                failures.Add("[catalog] enemies.json missing at " + path + " - the dungeon stat source of record is gone");
                return null;
            }
            EnemyCatalog cat;
            try { cat = JsonConvert.DeserializeObject<EnemyCatalog>(StripBom(File.ReadAllText(path))); }
            catch (Exception ex)
            {
                failures.Add("[catalog] enemies.json parse error at " + path + ": " + ex.Message);
                return null;
            }
            if (cat == null || cat.Enemies == null || cat.Enemies.Count == 0)
            {
                failures.Add("[catalog] enemies.json at " + path + " deserialized to 0 defs (mapping break)");
                return null;
            }
            return cat;
        }

        // =====================================================================
        //  CASE 2 - every id the family tables emit is a REAL, non-boss roster id
        // =====================================================================
        private static void Case2_IdsExist(List<string> failures)
        {
            if (s_catalog == null) { failures.Add("[ids-exist] no enemies.json catalog to check ids against"); return; }

            foreach (string kind in OutpostEnemyGroupSpawner.KnownKinds)
            {
                OutpostEnemyGroupSpawner.FamilyTable(kind, out string[] ids, out int[] weights);

                if (ids == null || weights == null)
                { failures.Add("[ids-exist] kind '" + kind + "' returned a null table"); continue; }
                if (ids.Length != weights.Length)
                { failures.Add("[ids-exist] kind '" + kind + "' has " + ids.Length + " ids but " + weights.Length + " weights"); continue; }

                if (kind == OutpostEnemyGroupSpawner.KindNone)
                {
                    if (ids.Length != 0)
                        failures.Add("[ids-exist] kind 'none' must field NO enemies, table has " + ids.Length);
                    continue;
                }
                if (ids.Length == 0)
                { failures.Add("[ids-exist] kind '" + kind + "' has an EMPTY id table - it would spawn nothing"); continue; }

                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < ids.Length; i++)
                {
                    string id = ids[i];
                    if (string.IsNullOrEmpty(id)) { failures.Add("[ids-exist] kind '" + kind + "' has a null/empty id at slot " + i); continue; }
                    if (!seen.Add(id)) failures.Add("[ids-exist] kind '" + kind + "' lists id '" + id + "' twice - the weight is ambiguous");
                    if (weights[i] <= 0) failures.Add("[ids-exist] kind '" + kind + "' id '" + id + "' has weight " + weights[i] + " (must be > 0 or it can never roll)");

                    var def = s_catalog.Find(id);
                    if (def == null)
                    {
                        failures.Add("[ids-exist] kind '" + kind + "' emits id '" + id + "' which does NOT exist in enemies.json - " +
                                     "the family table invented an enemy (this is the class of bug WO-1001 removed)");
                        continue;
                    }
                    if (def.Boss)
                        failures.Add("[ids-exist] kind '" + kind + "' emits BOSS id '" + id + "' - a boss must never be rolled into a rank-and-file room group");
                }
            }
        }

        // =====================================================================
        //  CASE 3 - hollow-group is behaviour-identical to the retired picker
        // =====================================================================
        private static void Case3_HollowCompat(List<string> failures)
        {
            OutpostEnemyGroupSpawner.FamilyTable("hollow-group", out string[] ids, out int[] weights);

            if (ids.Length != LegacyHollowIds.Length)
                failures.Add("[hollow-compat] hollow-group fields " + ids.Length + " ids, the shipped group has " +
                             LegacyHollowIds.Length + " (" + string.Join(",", LegacyHollowIds) + ")");
            else
                for (int i = 0; i < ids.Length; i++)
                    if (!string.Equals(ids[i], LegacyHollowIds[i], StringComparison.Ordinal))
                        failures.Add("[hollow-compat] hollow-group slot " + i + " is '" + ids[i] + "', shipped is '" + LegacyHollowIds[i] + "'");

            int total = 0;
            for (int i = 0; i < weights.Length; i++) total += weights[i];
            if (total != 10)
                failures.Add("[hollow-compat] hollow-group weights total " + total + ", the shipped picker rolled over 10");

            // Drive BOTH pickers off identical seeded streams. Any divergence means a
            // shipped dungeon room would field a different mix than it does today.
            int diverged = 0;
            string firstDiff = null;
            for (int seed = 0; seed < 200; seed++)
            {
                var rngNew = new System.Random(seed);
                var rngOld = new System.Random(seed);
                for (int n = 0; n < 100; n++)
                {
                    string a = OutpostEnemyGroupSpawner.WeightedIdFor("hollow-group", rngNew);
                    string b = LegacyPick(rngOld);
                    if (string.Equals(a, b, StringComparison.Ordinal)) continue;
                    diverged++;
                    if (firstDiff == null) firstDiff = "seed " + seed + " roll " + n + ": new='" + a + "' shipped='" + b + "'";
                }
            }
            if (diverged > 0)
                failures.Add("[hollow-compat] hollow-group diverged from the shipped picker on " + diverged +
                             "/20000 rolls (first: " + firstDiff + ") - existing dungeon rooms would change composition");

            // Roles for the four shipped hollow ids must be EXACTLY what they were.
            AssertRole(failures, "hollow-walker", EnemyRole.DPS);
            AssertRole(failures, "hollow-rogue", EnemyRole.DPS);
            AssertRole(failures, "hollow-warrior", EnemyRole.DPS);
            AssertRole(failures, "hollow-acolyte", EnemyRole.Healer);
        }

        private static void AssertRole(List<string> failures, string id, EnemyRole want)
        {
            EnemyRole got = OutpostEnemyGroupSpawner.RoleForId(id);
            if (got != want)
                failures.Add("[hollow-compat] RoleForId('" + id + "') = " + got + ", the shipped mapping is " + want);
        }

        // =====================================================================
        //  CASE 4 - each family kind draws from ONE family (the whole point of the slice)
        // =====================================================================
        private static void Case4_FamilyPurity(List<string> failures)
        {
            if (s_catalog == null) { failures.Add("[family-purity] no enemies.json catalog to read families from"); return; }

            RequireFamily(failures, "hollow-group", "hollow");
            RequireFamily(failures, "orc-group", "orc");
            RequireFamily(failures, "troll-group", "troll");

            // mixed is the ONE kind allowed to cross families - but only across families
            // that actually exist in the roster, and it must genuinely mix.
            OutpostEnemyGroupSpawner.FamilyTable("mixed", out string[] ids, out int[] _);
            var families = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string id in ids)
            {
                var def = s_catalog.Find(id);
                if (def == null) continue;   // already reported by case 2
                families.Add(string.IsNullOrEmpty(def.Family) ? "hollow" : def.Family);
            }
            if (families.Count < 2)
                failures.Add("[family-purity] kind 'mixed' draws from only " + families.Count +
                             " family/families - it is not a mix");
        }

        private static void RequireFamily(List<string> failures, string kind, string family)
        {
            OutpostEnemyGroupSpawner.FamilyTable(kind, out string[] ids, out int[] _);
            foreach (string id in ids)
            {
                var def = s_catalog.Find(id);
                if (def == null) continue;   // already reported by case 2
                string fam = string.IsNullOrEmpty(def.Family) ? "hollow" : def.Family;
                if (!string.Equals(fam, family, StringComparison.OrdinalIgnoreCase))
                    failures.Add("[family-purity] kind '" + kind + "' emits '" + id + "' whose enemies.json family is '" +
                                 fam + "', expected '" + family + "' - the room would field the wrong faction");
            }
        }

        // =====================================================================
        //  CASE 5 - an unknown kind falls back to hollow AND is never silent
        // =====================================================================
        private static void Case5_KindFallback(List<string> failures)
        {
            // Every declared kind resolves to itself and is NOT a fallback.
            foreach (string kind in OutpostEnemyGroupSpawner.KnownKinds)
            {
                string got = OutpostEnemyGroupSpawner.ResolveKind(kind, out bool fellBack);
                if (fellBack) failures.Add("[kind-fallback] declared kind '" + kind + "' was treated as UNKNOWN");
                if (!string.Equals(got, kind, StringComparison.Ordinal))
                    failures.Add("[kind-fallback] declared kind '" + kind + "' resolved to '" + got + "'");
            }

            // Case + whitespace tolerance: an author's stray capital must not silently
            // become a hollow room.
            string tolerant = OutpostEnemyGroupSpawner.ResolveKind("  ORC-Group ", out bool tolerantFellBack);
            if (tolerantFellBack || !string.Equals(tolerant, "orc-group", StringComparison.Ordinal))
                failures.Add("[kind-fallback] '  ORC-Group ' resolved to '" + tolerant + "' (fellBack=" + tolerantFellBack +
                             "), expected 'orc-group' with no fallback");

            // Unknown / unauthored kinds: hollow-group, and FLAGGED.
            foreach (string bad in new[] { null, "", "   ", "skeleton-group", "orcgroup", "undead", "beast", "orc group" })
            {
                string got = OutpostEnemyGroupSpawner.ResolveKind(bad, out bool fellBack);
                string shown = bad == null ? "<null>" : "'" + bad + "'";
                if (!string.Equals(got, OutpostEnemyGroupSpawner.DefaultKind, StringComparison.Ordinal))
                    failures.Add("[kind-fallback] unknown kind " + shown + " resolved to '" + got + "', expected '" +
                                 OutpostEnemyGroupSpawner.DefaultKind + "'");
                if (!fellBack)
                    failures.Add("[kind-fallback] unknown kind " + shown + " was accepted SILENTLY (fellBack=false) - " +
                                 "silent acceptance of a bad kind IS the WO-1001 defect");
            }

            // kind 'none' fields nothing at all.
            string noneKind = OutpostEnemyGroupSpawner.ResolveKind(OutpostEnemyGroupSpawner.KindNone, out bool _);
            if (OutpostEnemyGroupSpawner.WeightedIdFor(noneKind, new System.Random(1)) != null)
                failures.Add("[kind-fallback] kind 'none' rolled an enemy id - it must field nothing");

            // The runtime must SAY SO. Prove the warn exists on the fallback path.
            string src = ReadOrEmpty(SpawnerSrc);
            if (src.Length == 0)
                failures.Add("[kind-fallback] could not read " + SpawnerSrc);
            else
            {
                if (src.IndexOf("unknown encounter kind", StringComparison.Ordinal) < 0)
                    failures.Add("[kind-fallback] " + SpawnerSrc + " no longer carries the 'unknown encounter kind' " +
                                 "warn text - a bad kind would fall back with no captured data line (CLAUDE.md sec.12)");
                if (src.IndexOf("FlowTrace.Warn", StringComparison.Ordinal) < 0)
                    failures.Add("[kind-fallback] " + SpawnerSrc + " has no FlowTrace.Warn at all");
            }
        }

        // =====================================================================
        //  CASE 6 - the field exists and the bake/bind paths actually write it
        // =====================================================================
        private static void Case6_Wiring(List<string> failures)
        {
            var field = typeof(OutpostEnemyGroupSpawner).GetField(
                KindFieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null)
                failures.Add("[wiring] serialized field '" + KindFieldName + "' missing on OutpostEnemyGroupSpawner " +
                             "(DungeonBaker.WriteEncounterFields writes it BY NAME via SerializedObject - a rename " +
                             "breaks the bake silently)");
            else if (field.FieldType != typeof(string))
                failures.Add("[wiring] field '" + KindFieldName + "' is " + field.FieldType.Name + ", expected string");

            // A fresh spawner must default to the shipped hollow group, so every scene
            // baked before this field existed keeps the composition it already had.
            var host = new GameObject("__wo1001_kind_default");
            try
            {
                var spawner = host.AddComponent<OutpostEnemyGroupSpawner>();
                if (field != null)
                {
                    string value = field.GetValue(spawner) as string;
                    string resolved = OutpostEnemyGroupSpawner.ResolveKind(value, out bool fellBack);
                    if (fellBack || !string.Equals(resolved, OutpostEnemyGroupSpawner.DefaultKind, StringComparison.Ordinal))
                        failures.Add("[wiring] a fresh spawner's '" + KindFieldName + "' is '" + (value ?? "<null>") +
                                     "' which resolves to '" + resolved + "' (fellBack=" + fellBack + ") - pre-WO-1001 " +
                                     "baked scenes must default to '" + OutpostEnemyGroupSpawner.DefaultKind + "'");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }

            string baker = ReadOrEmpty(BakerSrc);
            if (baker.Length == 0)
                failures.Add("[wiring] could not read " + BakerSrc);
            else if (baker.IndexOf("\"" + KindFieldName + "\"", StringComparison.Ordinal) < 0)
                failures.Add("[wiring] " + BakerSrc + " does not write '" + KindFieldName + "' via SerializedObject - " +
                             "EncounterSpec.kind would never reach the scene and every room would spawn hollows again");

            string binder = ReadOrEmpty(BinderSrc);
            if (binder.Length == 0)
                failures.Add("[wiring] could not read " + BinderSrc);
            else if (binder.IndexOf("enc.kind", StringComparison.Ordinal) < 0)
                failures.Add("[wiring] " + BinderSrc + " does not pass the layout's enc.kind to ConfigureRoomArea - " +
                             "an already-baked scene could never pick up its authored family");
        }

        // =====================================================================
        //  CASE 7 - every kind authored in a shipped layout/graph is a known kind
        // =====================================================================
        private static void Case7_Authoring(List<string> failures, List<string> notes)
        {
            int scanned = 0, blocks = 0;
            foreach (string dir in EncounterAuthoringDirs)
            {
                if (!Directory.Exists(dir)) { notes.Add("authoring dir absent: " + dir); continue; }
                foreach (string path in Directory.GetFiles(dir, "*.json", SearchOption.TopDirectoryOnly))
                {
                    scanned++;
                    JToken root;
                    try { root = JToken.Parse(StripBom(ReadOrEmpty(path))); }
                    catch (Exception ex) { failures.Add("[authoring] " + path + " parse error: " + ex.Message); continue; }

                    foreach (var enc in FindEncounterBlocks(root))
                    {
                        blocks++;
                        var kindTok = enc["kind"];
                        string kind = kindTok != null ? kindTok.ToString() : null;
                        string resolved = OutpostEnemyGroupSpawner.ResolveKind(kind, out bool fellBack);
                        if (!fellBack) continue;
                        failures.Add("[authoring] " + Path.GetFileName(path) + " authors encounter kind '" +
                                     (kind ?? "<absent>") + "' which the spawner does not know - it would spawn '" +
                                     resolved + "' instead. Known kinds: " +
                                     string.Join(", ", OutpostEnemyGroupSpawner.KnownKinds));
                    }
                }
            }
            if (scanned == 0)
                failures.Add("[authoring] no dungeon layout/graph JSON found in any of " +
                             EncounterAuthoringDirs.Length + " authoring dirs - the scan asserted nothing");
            else
                notes.Add("authoring scan: " + blocks + " encounter block(s) across " + scanned + " file(s)");
        }

        /// <summary>Every object value of a property literally named "encounter", at any depth.</summary>
        private static List<JObject> FindEncounterBlocks(JToken node)
        {
            var found = new List<JObject>();
            Walk(node, found);
            return found;
        }

        private static void Walk(JToken node, List<JObject> found)
        {
            if (node == null) return;
            var obj = node as JObject;
            if (obj != null)
            {
                foreach (var prop in obj.Properties())
                {
                    if (string.Equals(prop.Name, "encounter", StringComparison.Ordinal))
                    {
                        var encObj = prop.Value as JObject;
                        if (encObj != null) found.Add(encObj);
                    }
                    Walk(prop.Value, found);
                }
                return;
            }
            var arr = node as JArray;
            if (arr == null) return;
            foreach (var child in arr) Walk(child, found);
        }

        // ---- shared helpers -----------------------------------------------------

        private static HashSet<string> IdSet(EnemyCatalog cat)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in cat.Enemies)
                if (e != null && !string.IsNullOrEmpty(e.Id)) set.Add(e.Id);
            return set;
        }

        private static string StripBom(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s[0] == (char)0xFEFF ? s.Substring(1) : s;
        }

        private static string ReadOrEmpty(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path) : string.Empty; }
            catch (IOException) { return string.Empty; }
            catch (UnauthorizedAccessException) { return string.Empty; }
        }
    }
}
