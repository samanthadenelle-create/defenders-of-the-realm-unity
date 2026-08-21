// =============================================================================
// SpawnAreaEnemyIdRegression [spawn-area-enemy-ids] — the gate for the defect class
// that shipped on 2026-08-20: A CANONICAL DATA FILE NAMES AN ENEMY ID THAT NOTHING
// CAN SPAWN, and nothing anywhere fails.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.  Markers: SPAWN_AREA_ENEMY_IDS_OK / _FAIL.
//
// WHAT SHIPPED. Assets/Resources/Data/Canonical/spawn-areas.json authored its
// overworld bands with tank/dps/healer enemy ids. Seven of the nine ids it named
// resolved to nothing real — skeleton-tank, skeleton-warrior, skeleton-mage,
// orc-tank, orc-warrior, orc-mage, troll-berserker.
//
// WHY IT WAS INVISIBLE. A dangling id does not throw and does not blank anything.
// SpawnAreaTable.BuildDraw hands the string on; EnemyFactory.ModelForEnemy misses
// every case, misses the family fallback (the roamer defs set no Family), and lands
// on the SIZE DEFAULT at the bottom of the switch — "Skeleton_Minion" (or
// "Skeleton_Golem" above 2.3m). The device trace read:
//
//     [Flow:Enemy] EnemyFactory.Build: id='skeleton-warrior' -> model 'Skeleton_Minion'
//                  loading 'Enemies/Skeleton_Minion'
//
// so EVERY band — orc bands, troll bands, all of them — spawned the same legacy
// KayKit skeleton regardless of what the band asked for. A wrong-but-VALID enemy is
// the worst failure mode there is: nothing is red, the game runs, and the whole
// overworld quietly reads as one undead family.
//
// -----------------------------------------------------------------------------
// SCOPE: EVERY CANONICAL FILE THAT KEYS BY ENEMY ID, NOT JUST SPAWN-AREAS
// -----------------------------------------------------------------------------
// The rule is one rule, so it is enforced in one place. EnemyIdSources below is the
// EXPLICIT, EXTENDABLE registry of files this suite reads; every failure names the
// FILE and the KEY it came from. To cover a new file, add one row there.
//
//   spawn-areas.json              families[].tank / .dps / .healer     (via SpawnAreaTable)
//   loot-tables.json              tables[].id, minus the declared non-enemy tables
//   dungeons/*.json               scriptedEncounters[].enemyTypes[],
//                                 miniBoss.enemyType, encounterPool.<tier>[]
//
// DELIBERATELY EXCLUDED, EACH FOR A VERIFIED REASON (do not "helpfully" add these —
// each would be a FALSE POSITIVE, which is worse than no check at all):
//
//   * motion-castings.json — its targets are ANIMATION-RIG / FAMILY keys, not enemy
//     def ids. The live target set is { humanoid, orc, hollow, troll, orc-mage,
//     orc-warrior, orc-tank, <hero classes> }, wired by an "inherits" chain
//     (orc-tank inherits orc). An id-based check would flag "humanoid", "orc",
//     "hollow" and "troll" — all correct authoring. Verified at source 2026-08-20:
//     the file's own header says "Targets (enemy family | hero class) x keywords".
//
//   * *-group / *-band tokens (hollow-group, orc-warband, skeleton-band, troll-band,
//     dungeon-graphs encounter tokens) are BAND/POOL names, never enemy ids. This
//     suite only ever reads the keys listed above, so it cannot trip on them.
//
//   * dungeons chests[].id and scriptedEncounters[].id (cellar-chest,
//     cellar-hollow-one, garden-hollow-one) are INSTANCE ids — the id OF the
//     encounter/chest, whose enemyTypes[] hold the actual enemy ids. Verified at
//     source 2026-08-20. Only the enemy-bearing keys are read.
//
// -----------------------------------------------------------------------------
// THE PREDICATE: "KNOWN ENEMY ID" IS THREE-TIERED, AND IT IS MEASURED, NOT LISTED
// -----------------------------------------------------------------------------
// "Is it a row in enemies.json?" is TOO NARROW to be the whole gate, and using it
// alone would red correct data. An id is spawnable, with a real body, if ANY of:
//
//   (a) it is a row in enemies.json (the catalog), OR
//   (b) EnemyResolver knows it (the Hollow table — this is how the dungeon layouts'
//       underscore ids hollow_villager_a / hollow_apprentice_minor resolve; they are
//       deliberately NOT catalog rows), OR
//   (c) EnemyFactory.ModelForEnemy gives it an explicit model — i.e. it does NOT
//       fall through to the size default. This is the CODE-SYNTHESISED tier: ids
//       that no catalog row backs but that live spawners emit with a hand-built
//       EnemyDef and a committed mesh. Verified at source 2026-08-20:
//         orc-warrior / orc-tank / orc-mage — OverworldEncounterSpawner's OrcPool +
//           every Scatter*Pool + TutorialFlow's warband (ff.overworldencounter
//           defaults ON), each with its own Orc_Warrior/_Tank/_Mage mesh;
//         orc-warlord — BattleArena.BossEnemyId, ~5% of arena fights.
//
// Tier (c) is MEASURED by asking ModelForEnemy, never by a hand-maintained list, so
// it cannot rot the way a copied table does. An id that satisfies none of the three
// is the real defect: it spawns the generic size-default skeleton.
//
// -----------------------------------------------------------------------------
// CASE LIST — each guards a failure that is SILENT
// -----------------------------------------------------------------------------
//   1. [parse]       spawn-areas.json loads through the GAME'S loader
//                    (DeNelle.Core.World.SpawnAreaTable, reading via CanonicalJson
//                    exactly as the runtime does — not a bespoke regex) and yields
//                    areas with families. Zero areas or zero references is a HARD
//                    failure: it would make every case below vacuously green.
//
//   2. [keys]        The RAW json is walked and every key inside families[]
//                    enumerated. SpawnFamilyEntry maps exactly id/weight/tank/dps/
//                    healer; an author adding a fourth role slot ("support": "…")
//                    gets a field Newtonsoft silently drops AND an enemy id nothing
//                    verifies. Fails on any unknown key, so this suite's coverage can
//                    never fall behind the data.
//
//   3. [catalog]     THE HEADLINE, across ALL registered files. Every referenced id
//                    must satisfy the three-tier predicate. The failure names the ID,
//                    the FILE, and the exact KEY/location — "an id is missing" would
//                    cost the next reader a search through four bands and three files.
//
//   4. [copies]      spawn-areas.json's Resources copy and StreamingAssets copy must
//                    be BYTE identical. CanonicalJson reads Resources first and
//                    StreamingAssets second, so a drift means the editor/desktop build
//                    and the WebGL/streaming build spawn DIFFERENT enemies from the
//                    "same" file — its own silent bug class in this repo.
//
//   5. [model]       The player-visible harm itself, for spawn-area ids. Each is
//                    pushed through the REAL path (EnemyFactory.ModelForEnemy on its
//                    actual catalog row) and must not come back as a size-default
//                    skeleton. Catches the NEXT id that IS in the catalog but still
//                    degrades — case 3 alone would pass it. Deliberately narrow so it
//                    cannot cry wolf: it fires only when the model is one of the two
//                    size defaults AND the catalog row did not itself ask for that
//                    model (hollow-villager-a legitimately wears Skeleton_Minion).
//
//   6. [approved]    EnemyFactory.Build asks EnemyResolver.IsCombatApproved and
//                    REDIRECTS a deferred Wildlands id to a Hollow substitute. That
//                    redirect has the SAME visible symptom as the bug above — the band
//                    asks for an orc and a skeleton walks out — so a deferred id in a
//                    band fails unless listed in KnownDeferredReferences with a note.
//
//   7. [loot-tables] loot-tables.json is keyed by the SLAIN ENEMY'S EnemyDefId
//                    (ItemDropWatcher.ResolveEnemyTable), and on a miss it silently
//                    falls back to defaults.enemy / defaults.boss. So a table keyed to
//                    an id nothing can ever spawn is DEAD AUTHORED CONTENT that no
//                    playtest can distinguish from working content — the identical
//                    silent-fallback shape as the spawn-areas defect. Every
//                    enemy-intent table id must satisfy the predicate; the container /
//                    tier / default tables are excluded BY NAME in NonEnemyLootTables.
//
//   8. [dungeons]    Same rule for the dungeon layouts' enemy-bearing keys.
//
// Pure data/asset logic (json parse + catalog lookup + model resolve); no scene, no
// play mode. Runs inside DataRegression.RunAll and standalone via
//   run-unity-method.ps1 -Method DeNelle.Editor.Regression.SpawnAreaEnemyIdRegression.RunAll
// Mirrors the TownsfolkBodyPoolRegression contract: public static bool Run(out string
// reason), every case wrapped so one throw is a labelled failure, never a dead suite.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DeNelle.Core.Enemies;
using DeNelle.Core.World;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class SpawnAreaEnemyIdRegression
    {
        // ── THE FILE REGISTRY (extend HERE) ──────────────────────────────────────
        // Assets-relative paths. Absolute paths are resolved off Application.dataPath
        // at runtime: the repo root is machine-dependent (C:\eoa on one seat, D:\eoa on
        // another) and must never be hardcoded (CLAUDE.md §0).

        private const string SpawnAreasResources  = "Resources/Data/Canonical/spawn-areas.json";
        private const string SpawnAreasStreaming  = "StreamingAssets/Data/Canonical/spawn-areas.json";
        private const string LootTablesResources  = "Resources/Data/Canonical/loot-tables.json";
        private const string DungeonDirResources  = "Resources/Data/Canonical/dungeons";

        /// <summary>
        /// Every key SpawnFamilyEntry maps. Anything else inside a families[] entry is a
        /// field Newtonsoft drops on the floor — and if it holds an enemy id, an id this
        /// suite would never have checked. See case 2.
        /// </summary>
        private static readonly HashSet<string> KnownFamilyKeys =
            new HashSet<string>(StringComparer.Ordinal) { "id", "weight", "tank", "dps", "healer", "note" };

        /// <summary>The keys that name an ENEMY ID (the subset of the above case 3 walks).</summary>
        private static readonly string[] EnemyIdKeys = { "tank", "dps", "healer" };

        /// <summary>
        /// The two ids EnemyFactory.ModelForEnemy falls back to when an id matches no case
        /// and no family — the exact silent degradation this suite exists to stop.
        /// </summary>
        private static readonly HashSet<string> SizeDefaultModels =
            new HashSet<string>(StringComparer.Ordinal) { "Skeleton_Minion", "Skeleton_Golem" };

        /// <summary>
        /// loot-tables.json table ids that are deliberately NOT enemy ids, each verified at
        /// source 2026-08-20. Listed BY NAME so a typo'd enemy table can never hide among
        /// them, and so adding a new non-enemy table requires touching this list:
        ///   common-grunt / boss-hoard — the declared defaults.enemy / defaults.boss, reached
        ///     by ItemDropWatcher on a miss, never by an id match.
        ///   crate-common / barrel-common / chest-rare / dungeon-chest — CONTAINER tables,
        ///     addressed by a literal lootTableId on a breakable/chest (DungeonChainBuilder,
        ///     KayKitChallengeOutpostBuilder, DungeonBaker, the dungeon-graphs nodes).
        ///   dungeon-hollow / dungeon-miniboss / dungeon-deepboss — DEPTH-TIER tables named
        ///     by lootTableId in the dungeon-graphs (dg_sunken_vault, dg_bonecrypt,
        ///     dg_ember_deep), not by any enemy's def id.
        /// </summary>
        private static readonly HashSet<string> NonEnemyLootTables =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "common-grunt", "boss-hoard",
                "crate-common", "barrel-common", "chest-rare", "dungeon-chest",
                "dungeon-hollow", "dungeon-miniboss", "dungeon-deepboss",
            };

        /// <summary>
        /// Ids a band references that are KNOWN-deferred by the Wildlands gate
        /// (EnemyResolver's deferred set, PAIN_POINTS_2026-07-26 §1.1: the living Wildlands
        /// art retargets to exploded geometry, so EnemyFactory.Build redirects them to a
        /// Hollow substitute). Listed by name and dated so the standing substitution is
        /// DOCUMENTED rather than discovered on a device, and so adding a new deferred id to
        /// a band requires touching this list.
        ///
        ///   orc-raider (2026-08-20) — goldfields + stoneback "orc-warband" dps slot.
        ///   Redirected at Build to hollow-warrior/hollow-walker, i.e. the orc warband's DPS
        ///   currently walks out as a Hollow. Clears the moment the Orc_Berserker rig is
        ///   fixed (Phase-2 art task) or the band is repointed at an approved orc id.
        /// </summary>
        private static readonly HashSet<string> KnownDeferredReferences =
            new HashSet<string>(StringComparer.Ordinal) { "orc-raider" };

        /// <summary>One enemy-id reference, carrying enough provenance to fix it without a search.</summary>
        private struct Reference
        {
            /// <summary>Assets-relative file the reference came from.</summary>
            public string File;
            /// <summary>The exact key/location inside that file.</summary>
            public string Where;
            public string EnemyId;
            /// <summary>True for spawn-areas.json refs (cases 5 + 6 apply only to those).</summary>
            public bool IsSpawnArea;
            public override string ToString() => $"{File} :: {Where}";
        }

        /// <summary>Standalone batch entry — prints the SPAWN_AREA_ENEMY_IDS_OK/_FAIL marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("SPAWN_AREA_ENEMY_IDS_OK - " + reason);
            else Debug.LogError("SPAWN_AREA_ENEMY_IDS_FAIL: " + reason);
        }

        /// <summary>Covenant contract for DataRegression.RunAll ([spawn-area-enemy-ids]). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var refs = new List<Reference>();
            EnemyCatalog catalog = LoadEnemyCatalog(failures);
            int areaCount = 0, familyCount = 0, lootTables = 0, dungeonFiles = 0;

            // --- gather (each source is its own case so one bad file cannot blind the rest) ---
            Case(failures, "parse",       () => Source1_SpawnAreas(failures, refs, out areaCount, out familyCount));
            Case(failures, "loot-tables", () => lootTables   = Source2_LootTables(failures, refs));
            Case(failures, "dungeons",    () => dungeonFiles = Source3_Dungeons(failures, refs));

            // --- assert ---
            Case(failures, "keys",     () => Case2_EveryFamilyKeyIsMapped(failures));
            Case(failures, "catalog",  () => Case3_EveryIdIsKnown(failures, refs, catalog));
            Case(failures, "copies",   () => Case4_BothCopiesAreByteIdentical(failures));
            Case(failures, "model",    () => Case5_NoIdDegradesToTheSizeDefault(failures, refs, catalog));
            Case(failures, "approved", () => Case6_NoUndocumentedDeferredId(failures, refs));

            if (failures.Count == 0)
            {
                var ids = refs.Select(r => r.EnemyId).Distinct().OrderBy(s => s, StringComparer.Ordinal).ToList();
                reason = $"SPAWN-AREA-ENEMY-IDS OK - 8/8 cases pass ({refs.Count} enemy-id reference(s) across " +
                         $"3 registered source(s): spawn-areas.json {areaCount} area(s)/{familyCount} " +
                         $"family entr(ies), loot-tables.json {lootTables} enemy-intent table(s), " +
                         $"{dungeonFiles} dungeon layout(s); {ids.Count} distinct id(s): " +
                         $"{string.Join(", ", ids)}. Every id is spawnable (enemies.json row, EnemyResolver " +
                         "id, or an explicit ModelForEnemy case), no spawn-area id degrades to the " +
                         "size-default skeleton, and both spawn-areas.json copies are byte-identical.)";
                return true;
            }
            reason = "SPAWN-AREA-ENEMY-IDS FAIL x" + failures.Count + ": " + string.Join(" | ", failures);
            return false;
        }

        // =====================================================================
        //  SOURCE 1 — spawn-areas.json, through the GAME's loader
        // =====================================================================
        private static void Source1_SpawnAreas(List<string> failures, List<Reference> refs,
                                               out int areaCount, out int familyCount)
        {
            areaCount = 0; familyCount = 0;

            // Reload so an edit made in this same session (or by another seat) is read,
            // never a cached table from an earlier call in the same batch run.
            SpawnAreaTable.Reload();
            var areas = SpawnAreaTable.All;

            if (areas == null || areas.Count == 0)
            {
                failures.Add("[parse] SpawnAreaTable loaded ZERO areas from " +
                             SpawnAreaTable.StreamingRelativePath + ". Either the file is missing/malformed " +
                             "(CanonicalJson swallows the read and only warns) or every area lacked an id/center. " +
                             "Zero areas means the overworld spawns nothing AND every other case in this suite " +
                             "would verify nothing, which must never read as a pass.");
                return;
            }
            areaCount = areas.Count;

            foreach (var area in areas)
            {
                if (area == null) continue;
                if (area.Families == null || area.Families.Count == 0)
                {
                    failures.Add($"[parse] area '{area.Id}' has NO families[] — SpawnAreaTable.BuildDrawFor " +
                                 "returns Valid==false for it, so this authored ground silently spawns nothing.");
                    continue;
                }

                foreach (var fam in area.Families)
                {
                    if (fam == null) continue;
                    familyCount++;

                    bool any = false;
                    foreach (var role in EnemyIdKeys)
                    {
                        string id = IdForRole(fam, role);
                        if (string.IsNullOrEmpty(id)) continue;
                        any = true;
                        refs.Add(new Reference
                        {
                            File = SpawnAreasResources,
                            Where = $"area '{area.Id}' / family '{fam.Id}' / '{role}' key",
                            EnemyId = id,
                            IsSpawnArea = true,
                        });
                    }

                    if (!any)
                        failures.Add($"[parse] area '{area.Id}' family '{fam.Id}' names NO enemy id in any of " +
                                     "tank/dps/healer — BuildDrawFor yields an empty encounter for it.");
                }
            }

            if (refs.Count == 0 && failures.Count == 0)
                failures.Add("[parse] the spawn areas produced ZERO enemy-id references. Nothing would be " +
                             "verified below; treating that as green is how the 2026-08-20 defect shipped.");
        }

        private static string IdForRole(SpawnFamilyEntry fam, string role)
        {
            switch (role)
            {
                case "tank":   return fam.Tank;
                case "dps":    return fam.Dps;
                case "healer": return fam.Healer;
                default:       return null;
            }
        }

        // =====================================================================
        //  SOURCE 2 — loot-tables.json, keyed by the slain enemy's EnemyDefId
        // =====================================================================
        private static int Source2_LootTables(List<string> failures, List<Reference> refs)
        {
            var root = ReadJson(LootTablesResources, "loot-tables", failures);
            if (root == null) return 0;

            var tables = root["tables"] as JArray;
            if (tables == null)
            {
                failures.Add($"[loot-tables] '{LootTablesResources}' has no 'tables' array — the schema changed " +
                             "under LootTableCatalog, which would load zero tables and send EVERY kill to " +
                             "defaults.enemy.");
                return 0;
            }

            int enemyIntent = 0;
            foreach (var tok in tables)
            {
                string id = (string)tok["id"];
                if (string.IsNullOrEmpty(id)) continue;
                if (NonEnemyLootTables.Contains(id)) continue;   // container / tier / default table

                enemyIntent++;
                refs.Add(new Reference
                {
                    File = LootTablesResources,
                    Where = $"tables[] entry id '{id}' (source '{(string)tok["source"]}')",
                    EnemyId = id,
                });
            }
            return enemyIntent;
        }

        // =====================================================================
        //  SOURCE 3 — dungeon layouts (globbed, so a NEW dungeon is covered free)
        // =====================================================================
        private static int Source3_Dungeons(List<string> failures, List<Reference> refs)
        {
            string dir = AbsolutePath(DungeonDirResources);
            if (!Directory.Exists(dir)) return 0;   // no dungeon layouts authored — not a defect

            int files = 0;
            foreach (var abs in Directory.GetFiles(dir, "*.json", SearchOption.TopDirectoryOnly).OrderBy(p => p))
            {
                string rel = DungeonDirResources + "/" + Path.GetFileName(abs);
                var root = ReadJson(rel, "dungeons", failures);
                if (root == null) continue;
                files++;

                // scriptedEncounters[].enemyTypes[] — NOTE: the sibling 'id' on each entry is
                // the ENCOUNTER's own id (cellar-hollow-one), never an enemy id. Not read.
                var scripted = root["scriptedEncounters"] as JArray;
                if (scripted != null)
                    foreach (var enc in scripted)
                    {
                        string encId = (string)enc["id"];
                        if (enc["enemyTypes"] is JArray types)
                            foreach (var t in types)
                                AddDungeonRef(refs, rel, $"scriptedEncounters['{encId}'].enemyTypes[]", (string)t);
                    }

                // miniBoss.enemyType
                if (root["miniBoss"] is JObject mb)
                    AddDungeonRef(refs, rel, "miniBoss.enemyType", (string)mb["enemyType"]);

                // encounterPool.<tier>[]
                if (root["encounterPool"] is JObject pool)
                    foreach (var tier in pool.Properties())
                        if (tier.Value is JArray arr)
                            foreach (var t in arr)
                                AddDungeonRef(refs, rel, $"encounterPool.{tier.Name}[]", (string)t);
            }
            return files;
        }

        private static void AddDungeonRef(List<Reference> refs, string file, string where, string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            refs.Add(new Reference { File = file, Where = where, EnemyId = id });
        }

        // =====================================================================
        //  CASE 2 — the RAW json holds no family key the DTO (and case 3) misses
        // =====================================================================
        private static void Case2_EveryFamilyKeyIsMapped(List<string> failures)
        {
            var root = ReadJson(SpawnAreasResources, "keys", failures);
            if (root == null) return;

            var areas = root["areas"] as JArray;
            if (areas == null)
            {
                failures.Add("[keys] the raw file has no 'areas' array — the schema changed under the DTO.");
                return;
            }

            foreach (var areaTok in areas)
            {
                string areaId = (string)areaTok["id"];
                var families = areaTok["families"] as JArray;
                if (families == null) continue;

                foreach (var famTok in families)
                {
                    if (!(famTok is JObject fam)) continue;
                    string famId = (string)fam["id"];

                    foreach (var prop in fam.Properties())
                    {
                        if (KnownFamilyKeys.Contains(prop.Name)) continue;
                        failures.Add($"[keys] area '{areaId}' family '{famId}' carries an UNMAPPED key " +
                                     $"'{prop.Name}' (value '{prop.Value}'). SpawnFamilyEntry maps only " +
                                     $"{string.Join("/", KnownFamilyKeys.OrderBy(k => k, StringComparer.Ordinal))}, " +
                                     "so Newtonsoft drops it silently — and if it names an enemy id, the " +
                                     "[catalog] case below never checks it. Add the field to SpawnFamilyEntry, " +
                                     "add the key to EnemyIdKeys, and add it to KnownFamilyKeys.");
                    }
                }
            }
        }

        // =====================================================================
        //  CASE 3 — every referenced id, in every registered file, is SPAWNABLE
        // =====================================================================
        private static void Case3_EveryIdIsKnown(List<string> failures, List<Reference> refs,
                                                 EnemyCatalog catalog)
        {
            if (catalog == null) return;   // already reported by LoadEnemyCatalog

            foreach (var r in refs)
            {
                if (IsKnownEnemyId(catalog, r.EnemyId)) continue;
                failures.Add($"[catalog] DANGLING ENEMY ID '{r.EnemyId}' — referenced by {r.Where} in " +
                             $"'{r.File}', but it is NOT a row in enemies.json, NOT an id EnemyResolver " +
                             "knows, and EnemyFactory.ModelForEnemy gives it no explicit model (it falls " +
                             "through to the size default). Nothing fails at runtime — the id degrades into " +
                             "a generic KayKit skeleton, or the authored content keyed to it never fires at " +
                             "all (the 2026-08-20 device defect). Fix the id, add the enemy row, or give it a " +
                             "ModelForEnemy case.");
            }
        }

        /// <summary>
        /// The three-tier spawnability predicate — see the file header. Deliberately MEASURED
        /// (catalog lookup + resolver + the real ModelForEnemy call), never a hand-maintained
        /// list, so it cannot rot the way a copied table does.
        /// </summary>
        private static bool IsKnownEnemyId(EnemyCatalog catalog, string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (catalog != null && catalog.Find(id) != null) return true;      // (a) catalog row
            if (EnemyResolver.IsHollowId(id)) return true;                     // (b) resolver-owned
            string model = EnemyFactory.ModelForEnemy(new EnemyDef { Id = id });
            return !string.IsNullOrEmpty(model) && !SizeDefaultModels.Contains(model);   // (c) explicit case
        }

        // =====================================================================
        //  CASE 4 — the Resources copy and the StreamingAssets copy are identical
        // =====================================================================
        private static void Case4_BothCopiesAreByteIdentical(List<string> failures)
        {
            string a = AbsolutePath(SpawnAreasResources), b = AbsolutePath(SpawnAreasStreaming);
            bool haveA = File.Exists(a), haveB = File.Exists(b);

            if (!haveA) failures.Add($"[copies] '{SpawnAreasResources}' is MISSING — CanonicalJson reads the " +
                                     "Resources copy first, so the editor and desktop builds lose the file.");
            if (!haveB) failures.Add($"[copies] '{SpawnAreasStreaming}' is MISSING — the WebGL/streaming build " +
                                     "reads this copy, so it would load zero areas and spawn nothing.");
            if (!haveA || !haveB) return;

            byte[] ba = File.ReadAllBytes(a), bb = File.ReadAllBytes(b);
            if (ba.Length == bb.Length && ba.SequenceEqual(bb)) return;

            failures.Add($"[copies] the two spawn-areas.json copies DRIFTED ('{SpawnAreasResources}' is " +
                         $"{ba.Length} bytes, '{SpawnAreasStreaming}' is {bb.Length}). CanonicalJson prefers " +
                         "Resources, so the editor/desktop build and the streaming build would spawn " +
                         "DIFFERENT enemies from the 'same' data file — a drift no runtime check can see. " +
                         "Re-copy one over the other.");
        }

        // =====================================================================
        //  CASE 5 — no spawn-area id degrades to the size-default skeleton
        // =====================================================================
        private static void Case5_NoIdDegradesToTheSizeDefault(List<string> failures, List<Reference> refs,
                                                               EnemyCatalog catalog)
        {
            if (catalog == null) return;

            foreach (var r in refs.Where(x => x.IsSpawnArea).GroupBy(x => x.EnemyId).Select(g => g.First()))
            {
                var def = catalog.Find(r.EnemyId);
                if (def == null) continue;   // handled by [catalog]

                string model = EnemyFactory.ModelForEnemy(def);
                if (string.IsNullOrEmpty(model))
                {
                    failures.Add($"[model] id '{r.EnemyId}' ({r.Where}) resolved to an EMPTY model — it would " +
                                 "spawn a tinted capsule.");
                    continue;
                }
                if (!SizeDefaultModels.Contains(model)) continue;

                // NARROW ON PURPOSE (no false positives): a row is allowed to ask for a
                // size-default mesh by name — hollow-villager-a genuinely wears
                // Skeleton_Minion. Only an id that lands there WITHOUT the data asking is
                // the silent degradation.
                if (string.Equals(def.ModelKey, model, StringComparison.Ordinal)) continue;

                failures.Add($"[model] SILENT DEGRADATION: id '{r.EnemyId}' ({r.Where}) resolves through " +
                             $"EnemyFactory.ModelForEnemy to '{model}' — the SIZE DEFAULT at the bottom of the " +
                             $"switch, not anything the band asked for (its enemies.json modelKey is " +
                             $"'{(string.IsNullOrEmpty(def.ModelKey) ? "<none>" : def.ModelKey)}'). The row " +
                             "exists, so the [catalog] case passes it, and the player still sees a generic " +
                             "KayKit skeleton. Give the id a case/family in ModelForEnemy, or a committed " +
                             "modelKey in enemies.json.");
            }
        }

        // =====================================================================
        //  CASE 6 — no band references a deferred id that is not documented here
        // =====================================================================
        private static void Case6_NoUndocumentedDeferredId(List<string> failures, List<Reference> refs)
        {
            foreach (var r in refs.Where(x => x.IsSpawnArea).GroupBy(x => x.EnemyId).Select(g => g.First()))
            {
                if (EnemyResolver.IsCombatApproved(r.EnemyId)) continue;
                if (KnownDeferredReferences.Contains(r.EnemyId)) continue;

                string sub = EnemyResolver.SubstituteHollowId(r.EnemyId, null, 2.0f);
                failures.Add($"[approved] id '{r.EnemyId}' ({r.Where}) is NOT combat-approved " +
                             "(EnemyResolver's Wildlands deferral gate, PAIN_POINTS §1.1). EnemyFactory.Build " +
                             $"REDIRECTS it to the Hollow substitute '{sub}', so this band asks for one " +
                             "creature and a skeleton walks out — the same player-visible symptom as a " +
                             "dangling id. Repoint the band at an approved id, or add this id to " +
                             "KnownDeferredReferences with a dated note saying why the substitution is " +
                             "acceptable for now.");
            }
        }

        // -------- helpers --------

        /// <summary>Parse one Assets-relative json file, reporting a miss/malformation under <paramref name="tag"/>.</summary>
        private static JObject ReadJson(string assetsRelative, string tag, List<string> failures)
        {
            string path = AbsolutePath(assetsRelative);
            if (!File.Exists(path))
            {
                failures.Add($"[{tag}] '{assetsRelative}' does not exist — the canonical copy the editor and " +
                             "desktop builds read is gone.");
                return null;
            }
            try { return JObject.Parse(File.ReadAllText(path)); }
            catch (Exception ex)
            {
                failures.Add($"[{tag}] '{assetsRelative}' is not parseable JSON ({ex.GetType().Name}: " +
                             $"{ex.Message}). CanonicalJson would swallow this as a warning and load nothing.");
                return null;
            }
        }

        /// <summary>
        /// The REAL catalog the game reads (CanonicalJson bytes -> EnemyCatalog), by the same
        /// path EnemyResolverRegression uses. A failed load is a hard failure: an oracle that
        /// verifies ids against an empty catalog would fail everything, and one that skips is
        /// worse — it would go green on the exact defect it exists to catch.
        /// </summary>
        private static EnemyCatalog LoadEnemyCatalog(List<string> failures)
        {
            try
            {
                string json = DeNelle.Core.CanonicalJson.Read(WaveDataLoader.EnemiesRelativePath);
                if (string.IsNullOrEmpty(json))
                {
                    failures.Add($"[catalog] {WaveDataLoader.EnemiesRelativePath} could not be read — the enemy " +
                                 "catalog is unavailable, so no data-file id can be verified at all.");
                    return null;
                }
                var catalog = JsonConvert.DeserializeObject<EnemyCatalog>(json);
                if (catalog == null || catalog.Enemies == null || catalog.Enemies.Count == 0)
                {
                    failures.Add("[catalog] enemies.json produced 0 EnemyDef rows (mapping break/empty file) — " +
                                 "every referenced id would read as dangling, so the catalog itself is the fault.");
                    return null;
                }
                return catalog;
            }
            catch (Exception ex)
            {
                failures.Add($"[catalog] enemies.json parse THREW {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Absolute path for an Assets-relative file. Resolved off Application.dataPath at
        /// runtime — the repo root is machine-dependent (C:\eoa on one seat, D:\eoa on
        /// another) and must never be hardcoded (CLAUDE.md §0).
        /// </summary>
        private static string AbsolutePath(string assetsRelative)
            => Path.Combine(Application.dataPath, assetsRelative).Replace('\\', '/');

        // Guard each case so one throw becomes a labelled failure, not a dead suite.
        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add($"[{name}] THREW {ex.GetType().Name}: {ex.Message}"); }
        }
    }
}
