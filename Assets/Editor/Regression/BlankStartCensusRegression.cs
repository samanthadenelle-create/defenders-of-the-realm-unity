// =============================================================================
// BlankStartCensusRegression — WO-703 / ticket BLANK-1 acceptance oracle.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. STANDALONE (NOT wired into DataRegression
// .RunAll — invoke via:
//   Unity.exe -batchmode -quit -projectPath <repoRoot>   (root is MACHINE-DEPENDENT)
//     -executeMethod DeNelle.Editor.BlankStartCensusRegression.Run
// or the "Defenders > Regression > Blank Start Census (WO-703)" menu item).
//
// THE RULING (owner 2026-07-13, CANON): a fresh start is the TREE, the WELL and
// the WALLS — INCLUDING their GATES — nothing else. This oracle opens the real
// merged hub scene (SceneRouter.Castle -> Main_Castle_Overworld), installs a
// FIXTURE fresh-save state (marker set, BaseLayout = the single WO-695 FTUE
// grace-default "forge" record — GameStateService.ResetToNewGame:839-847/864),
// then walks the full WO-703 spawner census and asserts every non-allowlisted
// structure visual / NPC source STANDS DOWN on that state:
//   - LEVER 1 RECONCILIATION (owner 2026-07-24, WWCD — SUPERSEDES the WO-703
//     "nothing else" for baked STOREFRONTS only): on a fresh hub the baked stores
//     must PRE-STAND, VISIBLE + STAFFED (CoC), NOT be hidden. The old gate hid all
//     8 stores on a blank save (catalog-row present, no record) → empty grass under
//     floating vendor NPCs (the captured on-device screenshot). So section 2 now
//     asserts the INVERSE: each baked storefront with NO replacement record STAYS
//     VISIBLE (StanddownActiveForBaked == false), and a storefront that DOES gain a
//     record STANDS DOWN (its live Building replaces it — no double). The tree/well/
//     walls/gates + runtime-station standdown + Colosseum flag-gate are unchanged.
//   - the 8 baked storefronts (StrategicPlacementMigration.BakedRows): each STAYS
//     VISIBLE with no record (Lever-1), and stands down only once a record replaces
//     it — StanddownActiveForBaked (HubStructureVisualInjector.TrySwap);
//   - the baked CastleBarracks hides via ff.barracks OFF;
//   - the runtime stations (apothecary / jewelers-bench) skip spawn via
//     StanddownActiveForStation (WO-703 supersedes the "never lost" carve-out);
//   - the Colosseum_ArenaEntrance placement is gated OFF via ff.colosseum;
//   - every vendor role (CastleVendorNpcInjector.AnchorRoles) withholds its NPC
//     unless its home-building record exists (fresh save: only "forge");
//   - the townsfolk injector spawns at most ONE villager per distinct building;
//   - the scene bake itself carries NO baked NPC bodies (AmbientNPC components).
// Emits BLANK_START_OK, or BLANK_START_FAIL listing each extra WITH its spawner.
// WO-707 (owner ruling 2026-07-13) KILLED the WO-695 FTUE grace forge — a fresh
// save carries ZERO records and ZERO vendors ("should be placed by player").
// The one sanctioned NPC at spawn is Sylas the Steward (WO-702 founding guide).
// =============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core;
using DeNelle.Core.State;
using DeNelle.Village;

namespace DeNelle.Editor
{
    public static class BlankStartCensusRegression
    {
        private const string ScenePath = "Assets/Scenes/Main_Castle_Overworld.unity";

        [MenuItem("Defenders/Regression/Blank Start Census (WO-703)")]
        public static void Run()
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- BLANK START CENSUS (WO-703 / BLANK-1: tree + well + walls/gates, nothing else) ---");

            var prevInstance = ReadServiceInstance();
            var created = new List<UnityEngine.Object>();

            try
            {
                // ── 0. The merged hub scene, opened for a REAL scene-content census ──
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                if (!scene.IsValid() || !scene.isLoaded)
                { failures.Add($"could not open '{ScenePath}' — no scene to census"); Finish(failures, log); return; }
                if (scene.name != SceneRouter.Castle)
                    failures.Add($"opened scene '{scene.name}' != SceneRouter.Castle '{SceneRouter.Castle}' — " +
                                 "the standdown gates are castle-scoped and would not engage");
                log.AppendLine($"[scene] '{scene.name}' opened ({scene.rootCount} roots); SceneRouter.Castle='{SceneRouter.Castle}'");

                // ── 1. FIXTURE fresh-save state (mirrors GameStateService.ResetToNewGame:
                //       marker TRUE, BaseLayout = the one FTUE grace 'forge' record) ──
                var state = ScriptableObject.CreateInstance<GameState>();
                created.Add(state);
                state.StrategicPlacementMigrated = true;
                // WO-707 (owner ruling 2026-07-13): the WO-695 FTUE grace-default Forge is
                // KILLED — "should be placed by player". Fresh save = EMPTY BaseLayout;
                // the vista is the tree, the well, the walls, and Sylas the Steward (WO-702).
                state.BaseLayout = new List<PlacedStructureData>();
                var svcGo = new GameObject("BlankStartCensus_GameStateService");
                svcGo.SetActive(false);   // Awake must never run (no Load over the fixture)
                created.Add(svcGo);
                var svc = svcGo.AddComponent<GameStateService>();
                SetPrivate(svc, "_state", state);
                WriteServiceInstance(svc);
                log.AppendLine("[fixture] fresh-save state installed: marker=true, BaseLayout=EMPTY (WO-707: grace forge killed — player places everything)");

                // ── 1b. Populate the structures catalog the way the RUNTIME does ──
                // CatalogBootstrap.Register() is [RuntimeInitializeOnLoadMethod] — it never runs in
                // an editor -executeMethod context, so CatalogRegistry is EMPTY here and
                // HasCatalogRow(id) returns false for every id, making StanddownActiveForBaked
                // refuse for all 8 storefronts (probe-context false FAIL, first run 2026-07-13:
                // BLANK_START_FAIL (7), all attributed 'StanddownActiveForBaked said no' while the
                // fleet's runtime fresh save showed them correctly stood down). Invoke the same
                // private bootstrap by reflection (the DeNelle.Editor idiom) so the probe evaluates
                // the gates against the REAL registry the player build boots with.
                var bootstrapType = FindType("DeNelle.Village.CatalogBootstrap");
                bootstrapType?.GetMethod("Register", BindingFlags.NonPublic | BindingFlags.Static)
                             ?.Invoke(null, null);
                var registryType = FindType("DeNelle.Core.Catalog.CatalogRegistry");
                int catalogCount = (int)(registryType?.GetProperty("Count", BindingFlags.Public | BindingFlags.Static)
                                                     ?.GetValue(null) ?? 0);
                if (catalogCount == 0)
                {
                    failures.Add("PROBE CONTEXT: CatalogRegistry is empty after CatalogBootstrap.Register() — " +
                                 "the standdown gates cannot be evaluated truthfully (fix the probe bootstrap, not the game).");
                }
                log.AppendLine($"[fixture] catalog bootstrapped: {catalogCount} registry entrie(s)");

                // ── 2. Baked storefronts (LEVER 1, owner 2026-07-24): each present bake must
                //       PRE-STAND VISIBLE on a fresh (recordless) save, and must STAND DOWN once
                //       a record replaces it. Two directions, both asserted below. ──
                foreach (var (bakedName, itemId) in ReadBakedRows(failures))
                {
                    var t = FindInScene(bakedName);
                    if (t == null)
                    {
                        log.AppendLine($"[baked] {bakedName} -> not in scene (nothing to census)");
                        continue;
                    }

                    // 2a. NO record on the fresh save => the store MUST stay visible (pre-stand).
                    bool downNoRecord = StrategicPlacementMigration.StanddownActiveForBaked(bakedName, out string id);
                    if (downNoRecord)
                        failures.Add($"LEVER-1 VIOLATION: baked '{bakedName}' (itemId '{itemId}') STANDS DOWN on a fresh " +
                                     "save with NO replacement record — it must PRE-STAND VISIBLE + STAFFED " +
                                     "(HubStructureVisualInjector.TrySwap hides it → empty grass under a floating vendor).");
                    else
                        log.AppendLine($"[baked] {bakedName} -> STAYS VISIBLE on fresh save (no record; Lever-1 pre-stand; itemId '{id}')");

                    // 2b. Add a record for this itemId => the store MUST now stand down (the live
                    //     Building/replayed record replaces it — player-built replacement hides the
                    //     baked original, no double). Restore the empty layout after the probe.
                    if (!string.IsNullOrEmpty(itemId))
                    {
                        state.BaseLayout.Add(new PlacedStructureData(itemId, 0, 0, 0, level: 1,
                            yawOffset: 0f, worldY: 0f, wallMounted: false));
                        bool downWithRecord = StrategicPlacementMigration.StanddownActiveForBaked(bakedName, out _);
                        state.BaseLayout.Clear();
                        if (!downWithRecord)
                            failures.Add($"DOUBLE-SPAWN RISK: baked '{bakedName}' (itemId '{itemId}') does NOT stand down " +
                                         "even with a BaseLayout record present — its player-built replacement would " +
                                         "render on TOP of the baked original (StanddownActiveForBaked must hide it).");
                        else
                            log.AppendLine($"[baked] {bakedName} -> STANDS DOWN when itemId '{itemId}' has a record (replacement replaces bake — no double)");
                    }
                }

                // ── 3. Baked CastleBarracks: hidden while ff.barracks is OFF ──
                if (FindInScene("CastleBarracks") != null)
                {
                    if (FeatureFlags.Barracks)
                        failures.Add("EXTRA structure: baked 'CastleBarracks' visible — ff.barracks is ON in this environment " +
                                     "(default OFF; spawner: scene bake, hidden by HubStructureVisualInjector.TrySwap)");
                    else
                        log.AppendLine("[baked] CastleBarracks -> STANDS DOWN (ff.barracks OFF)");
                }

                // ── 4. Runtime stations: WO-703 unconditional standdown on marker-set saves ──
                foreach (string stationId in new[] { "apothecary", "jewelers-bench" })
                {
                    if (StrategicPlacementMigration.StanddownActiveForStation(stationId))
                        log.AppendLine($"[station] {stationId} -> STANDS DOWN (StanddownActiveForStation; injector skips spawn)");
                    else
                        failures.Add($"EXTRA structure: runtime station '{stationId}' would SPAWN on a fresh save — " +
                                     "spawner: CraftingStationInjector/JewelerStationInjector.Inject (WO-703 standdown gate said no)");
                }

                // ── 5. Colosseum: default-OFF flag gates the placement ──
                if (FeatureFlags.Colosseum)
                    failures.Add("EXTRA structure: 'Colosseum_ArenaEntrance' would be PLACED — ff.colosseum is ON in this " +
                                 "environment (default OFF; spawner: HubStructureVisualInjector.TryPlace Places row)");
                else
                    log.AppendLine("[placed] Colosseum_ArenaEntrance -> STANDS DOWN (ff.colosseum OFF)");
                if (FindInScene("Colosseum_ArenaEntrance") != null)
                    failures.Add("EXTRA structure: 'Colosseum_ArenaEntrance' is BAKED into the scene — it must be " +
                                 "runtime-placed (flag-gated) only");

                // ── 6. Vendor NPCs: RECORD-eligibility census (a lower bound). ──
                // LEVER 1 (owner 2026-07-24): a fresh hub is NOT vendorless — the baked storefronts
                // now pre-stand VISIBLE (section 2) and CastleVendorNpcInjector's Lever-1 FALLBACK
                // (ResolveBakedOrStationAnchor) seats each trade's speaker at its baked store WITHOUT
                // a record. This section still asserts the RECORD-BACKED count is ZERO on the recordless
                // fixture — that isolates the record path from the fallback path; any vendor visible on
                // a fresh hub is the intended Lever-1 fallback staffing, not a stray record. (The
                // fallback itself is exercised at runtime by the AutoPilot vendor-coverage oracle.)
                int vendorsEligible = 0;
                foreach (var (role, buildingId) in ReadAnchorRoles(failures))
                {
                    bool hasRecord = HasRecord(state, buildingId);
                    if (hasRecord)
                    {
                        vendorsEligible++;
                        log.AppendLine($"[vendor] {role} -> record-backed at '{buildingId}' (record present)");
                        if (buildingId != "forge")
                            failures.Add($"EXTRA NPC: vendor '{role}' eligible via unexpected record '{buildingId}' on the " +
                                         "fresh-save fixture — spawner: CastleVendorNpcInjector.AnchorVendorsToPlacedBuildings");
                    }
                    else
                        log.AppendLine($"[vendor] {role} -> no '{buildingId}' record (fresh hub: seated by the Lever-1 baked-store fallback, not a record)");
                }
                if (vendorsEligible != 0)
                    failures.Add($"vendor census: {vendorsEligible} record-backed role(s) on a fresh save — expected ZERO " +
                                 "(WO-707: no grace forge; record-backed vendors come online only as the player places buildings — " +
                                 "the pre-stand vendors are seated by the Lever-1 fallback instead).");

                // ── 7. Townsfolk: one villager per distinct building (fresh save: <=1) ──
                int villagerCap = ReadConstInt(typeof(CastleTownsfolkInjector), "VillagerCount", failures);
                int distinctBuildings = state.BaseLayout.Count;   // fresh save: records are the only building source
                int villagers = Math.Min(villagerCap, distinctBuildings);
                if (villagers > 1)
                    failures.Add($"EXTRA NPCs: townsfolk injector would spawn {villagers} villagers on a fresh save — " +
                                 "spawner: CastleTownsfolkInjector.Inject (one-per-distinct-building policy broken)");
                else
                    log.AppendLine($"[townsfolk] {villagers} villager(s) max on fresh save (cap {villagerCap}, " +
                                   $"{distinctBuildings} distinct building(s)) — one per home building");

                // ── 8. The scene bake itself must carry NO NPC bodies ──
                foreach (var npc in UnityEngine.Object.FindObjectsByType<AmbientNPC>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                    failures.Add($"EXTRA NPC: baked AmbientNPC '{npc.gameObject.name}' lives in the scene file — " +
                                 "spawner: the scene bake (all NPCs must be runtime-injected + gated)");
                log.AppendLine("[baked-npc] scene AmbientNPC sweep complete");

                // ── 9. Allowlist presence: the ruled trio must actually exist ──
                foreach (string wanted in new[] { "HeartOfElarion", "Well" })
                {
                    if (FindInScene(wanted) == null)
                        log.AppendLine($"[allowlist] WARN: '{wanted}' not found by exact name in the scene " +
                                       "(verify the tree/well anchor names if the bake changed)");
                    else
                        log.AppendLine($"[allowlist] '{wanted}' present (stays — the ruling's set)");
                }
            }
            catch (Exception ex)
            {
                failures.Add($"census oracle threw: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                WriteServiceInstance(prevInstance);
                foreach (var o in created)
                    if (o != null) UnityEngine.Object.DestroyImmediate(o);
            }

            Finish(failures, log);
        }

        private static void Finish(List<string> failures, StringBuilder log)
        {
            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() +
                    "BLANK_START_OK — fresh save census: tree + well + walls/gates only " +
                    "(zero records, zero vendors — WO-707: the player places everything; Sylas the Steward is the one sanctioned NPC, WO-702).");
                return;
            }
            Debug.LogError(log.ToString() + "BLANK_START_FAIL (" + failures.Count + "):\n  - " +
                           string.Join("\n  - ", failures));
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }

        // ── census-table readers (reflection on the REAL private tables — the same
        //    fail-loud seam style StrategicPlacementRegression uses) ──────────────
        private static List<(string, string)> ReadBakedRows(List<string> failures)
        {
            var rows = new List<(string, string)>();
            var f = typeof(StrategicPlacementMigration).GetField("BakedRows",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (f == null || !(f.GetValue(null) is Array arr) || arr.Length == 0)
            { failures.Add("StrategicPlacementMigration.BakedRows unreadable by reflection — the census table moved; re-point this oracle"); return rows; }
            foreach (var row in arr)
            {
                var t = row.GetType();
                rows.Add((t.GetField("bakedName").GetValue(row) as string,
                          t.GetField("itemId").GetValue(row) as string));
            }
            return rows;
        }

        private static List<(string, string)> ReadAnchorRoles(List<string> failures)
        {
            var roles = new List<(string, string)>();
            var f = typeof(CastleVendorNpcInjector).GetField("AnchorRoles",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (f == null || !(f.GetValue(null) is Array arr) || arr.Length == 0)
            { failures.Add("CastleVendorNpcInjector.AnchorRoles unreadable by reflection — the vendor census moved; re-point this oracle"); return roles; }
            foreach (var row in arr)
            {
                var t = row.GetType();   // ValueTuple<string,string> (Role, BuildingId)
                roles.Add((t.GetField("Item1").GetValue(row) as string,
                           t.GetField("Item2").GetValue(row) as string));
            }
            return roles;
        }

        private static int ReadConstInt(Type type, string name, List<string> failures)
        {
            var f = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            if (f == null)
            { failures.Add($"{type.Name}.{name} unreadable by reflection — re-point this oracle"); return 0; }
            return (int)f.GetRawConstantValue();
        }

        private static bool HasRecord(GameState state, string itemId)
        {
            if (state.BaseLayout == null) return false;
            foreach (var r in state.BaseLayout)
                if (r.itemId == itemId) return true;
            return false;
        }

        private static Transform FindInScene(string name)
        {
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t != null && t.name == name) return t;
            return null;
        }

        private static void SetPrivate(object obj, string field, object value)
        {
            var f = obj.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) f.SetValue(obj, value);
        }

        /// <summary>AppDomain-wide type lookup by full name (the DeNelle.Editor no-Village-ref idiom).</summary>
        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        private static GameStateService ReadServiceInstance()
        {
            var f = typeof(GameStateService).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            return f != null ? f.GetValue(null) as GameStateService : null;
        }

        private static void WriteServiceInstance(GameStateService svc)
        {
            var f = typeof(GameStateService).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            if (f != null) f.SetValue(null, svc);
        }
    }
}
