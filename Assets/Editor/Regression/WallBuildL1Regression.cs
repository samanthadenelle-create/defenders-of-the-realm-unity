// =============================================================================
// WallBuildL1Regression [wall-build-l1] -- WO-948: walls BUILD at level 1 ONLY.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (editor-only; references DeNelle.Core +
// DeNelle.Village). Contract mirrors the other Run(out reason) oracles:
//   public static bool Run(out string reason)   -- NEVER throws
//   markers: WALL_BUILD_L1_OK (Debug.Log) / WALL_BUILD_L1_FAIL (LogError)
//
// THE RULING (owner 2026-08-10, WO-948, CoC model): a wall can only ever be
// PLACED at its level-1 wood tier; higher tiers exist only by UPGRADING the
// placed piece. Steel/Spiked (walls.json levels 2..3) stay WO-904's behind
// raid-steal. Enforcement is DATA: build-categories.json Walls lockedIds carries
// wall_stone (the palette filter, BuildPaletteVM.Rebuild), and the catalog caps
// wall_wood at maxLevel 2 / wall_stone at maxLevel 1 (the existing upgrade verb's
// MaxLevelFor gate, BuildModeController:2375, refuses past the cap).
//
// Cases:
//   1 [palette-set]   the placeable wall set is EXACTLY {wall_wood}: every build
//                     verb that feeds CatalogType.Wall locks wall_stone, and the
//                     Walls verb's rendered set is one card. (BuildPaletteVM's
//                     ConfigureGroup unions every verb's lockedIds, so this also
//                     covers the D15 grouped view.)
//   2 [replay-safe]   wall_stone + gate_stone catalog rows SURVIVE (existing
//                     saves replay/sell placed stone walls), and the replay path
//                     (BaseLayoutLoader.cs) references neither LockedIds nor
//                     BuildCategoryRegistry -- the palette gate CANNOT reach it.
//   3 [l1-cap]        the rung is authored: wall_wood maxLevel == 2 with a
//                     non-zero L1->L2 price and the stone tier model at
//                     upgradeVisualPath[0] (and the prefab actually loads);
//                     wall_stone maxLevel == 1 with wallTierBase 1; NO Wall row
//                     reaches past level 2 (steel/spiked stay unreachable).
//   4 [derive]        WallDefense's WO-948 derive: min rule over placed walls,
//                     cap at MaxReachableWallLevel (1), and walls.json's stone
//                     rung actually pays (heartMult L1 < L0, targetHeight L1 > L0).
//
// Wire (DataRegression.RunAll):
//   DeNelle.Core.Diagnostics.Guard.Try("Regression", "wall-build-l1 suite", () => { if (!DeNelle.Editor.Regression.WallBuildL1Regression.Run(out var r)) failures.Add(r); else log.AppendLine("[wall-build-l1] " + r); });
//
// Standalone: run-unity-method DeNelle.Editor.Regression.WallBuildL1Regression.RunAll
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using DeNelle.Core.Catalog;
using DeNelle.Village;
using DeNelle.Village.Walls;

namespace DeNelle.Editor.Regression
{
    public static class WallBuildL1Regression
    {
        private const string CatalogRelPath = "Data/Canonical/structures-catalog.json";
        private const string LoaderSrc      = "Assets/_Modules/Village/BuildMode/BaseLayoutLoader.cs";
        private const string WoodWallId     = "wall_wood";
        private const string StoneWallId    = "wall_stone";
        private const string StoneGateId    = "gate_stone";
        private const string StoneVisual    = "Structures/Wall_Medieval_Stone";

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("WALL_BUILD_L1_OK - " + reason);
            else Debug.LogError("WALL_BUILD_L1_FAIL: " + reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("=== WallBuildL1Regression [wall-build-l1] (WO-948: walls build at L1 only) ===");

            try
            {
                HydrateCatalog(failures, log);
                CasePaletteSet(failures, log);
                CaseReplaySafe(failures, log);
                CaseL1Cap(failures, log);
                CaseDerive(failures, log);
            }
            catch (Exception ex)
            {
                failures.Add("[wall-build-l1] WallBuildL1Regression THREW: " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "WALL BUILD L1 OK - the placeable wall set is exactly [wall_wood], wall_stone/gate_stone " +
                         "rows survive for save replay (and the replay path cannot see the palette gate), the " +
                         "wood->stone rung is authored (maxLevel 2, priced, stone tier model resolves) with " +
                         "steel/spiked unreachable, and the WallDefense derive follows the min rule capped at stone.";
                Debug.Log("WALL_BUILD_L1_OK\n" + log);
                return true;
            }
            reason = "wall-build-l1: " + failures.Count + " failure(s): " + string.Join(" | ", failures);
            Debug.LogError("WALL_BUILD_L1_FAIL: " + failures.Count + " failure(s)\n" + log +
                           "\n - " + string.Join("\n - ", failures));
            return false;
        }

        // =====================================================================
        //  Catalog hydration -- the SAME parse CatalogBootstrap performs (mirrors
        //  BuildMenuRealEconomyRegression.HydrateCatalog). A parse break FAILS.
        // =====================================================================
        [Serializable]
        private sealed class StructuresFile
        {
            [JsonProperty("version")] public int Version;
            [JsonProperty("entries")] public List<CatalogEntry> Entries = new List<CatalogEntry>();
        }

        private static void HydrateCatalog(List<string> failures, StringBuilder log)
        {
            if (CatalogRegistry.OfType(CatalogType.Wall).Count > 0)
            {
                log.AppendLine("  catalog already hydrated (" + CatalogRegistry.OfType(CatalogType.Wall).Count + " Wall row(s))");
                return;
            }

            string json = DeNelle.Core.CanonicalJson.Read(CatalogRelPath);
            if (string.IsNullOrEmpty(json))
            {
                failures.Add("[palette-set] " + CatalogRelPath + " unreadable - no wall rows to verify at all");
                return;
            }
            StructuresFile file = null;
            try
            {
                var settings = new JsonSerializerSettings
                {
                    Converters = { new StringEnumConverter() },
                    NullValueHandling = NullValueHandling.Ignore,
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                };
                file = JsonConvert.DeserializeObject<StructuresFile>(json, settings);
            }
            catch (Exception ex)
            {
                failures.Add("[palette-set] structures-catalog.json failed to parse: " + ex.Message);
                return;
            }
            if (file == null || file.Entries == null || file.Entries.Count == 0)
            {
                failures.Add("[palette-set] structures-catalog.json deserialized to 0 entries");
                return;
            }
            int n = 0;
            foreach (var e in file.Entries)
            {
                if (e == null || string.IsNullOrEmpty(e.id)) continue;
                if (CatalogRegistry.Get(e.id) == null) { CatalogRegistry.Register(e); n++; }
            }
            log.AppendLine("  hydrated CatalogRegistry with " + n + " entry(ies) from " + CatalogRelPath);
        }

        // =====================================================================
        //  CASE 1 [palette-set] -- exactly ONE placeable wall card: wall_wood.
        // =====================================================================
        private static void CasePaletteSet(List<string> failures, StringBuilder log)
        {
            var wallRows = CatalogRegistry.OfType(CatalogType.Wall);
            if (wallRows == null || wallRows.Count == 0)
            {
                failures.Add("[palette-set] CatalogRegistry serves ZERO Wall rows - hydration failed upstream");
                return;
            }

            // Every verb that feeds CatalogType.Wall must lock wall_stone. This is what makes
            // the ConfigureGroup union (BuildPaletteVM:215) hold too: a row locked under every
            // Wall-feeding verb can never surface through the grouped view either.
            foreach (BuildType verb in Enum.GetValues(typeof(BuildType)))
            {
                var cat = BuildCategoryRegistry.Get(verb);
                if (cat == null || cat.Types == null) continue;
                bool feedsWalls = false;
                foreach (var t in cat.Types) if (t == CatalogType.Wall) { feedsWalls = true; break; }
                if (!feedsWalls) continue;

                if (cat.LockedIds == null || !cat.LockedIds.Contains(StoneWallId))
                {
                    failures.Add("[palette-set] build verb '" + verb + "' feeds CatalogType.Wall but does NOT lock '" +
                                 StoneWallId + "' - the player can place a stone wall directly, against the WO-948 " +
                                 "ruling (walls build at L1 only; stone is reached by upgrade)");
                    continue;
                }

                // The verb's rendered wall set (the exact BuildPaletteVM.Rebuild filter:
                // OfType(types) minus lockedIds) must be exactly [wall_wood].
                var rendered = new List<string>();
                foreach (var t in cat.Types)
                {
                    var entries = CatalogRegistry.OfType(t);
                    if (entries == null) continue;
                    foreach (var e in entries)
                    {
                        if (e == null || string.IsNullOrEmpty(e.id)) continue;
                        if (cat.LockedIds.Contains(e.id)) continue;
                        if (e.type == CatalogType.Wall) rendered.Add(e.id);
                    }
                }
                if (rendered.Count != 1 || !string.Equals(rendered[0], WoodWallId, StringComparison.OrdinalIgnoreCase))
                    failures.Add("[palette-set] verb '" + verb + "' renders wall card(s) [" + string.Join(",", rendered) +
                                 "] - WO-948 requires EXACTLY [" + WoodWallId + "]");
                else
                    log.AppendLine("  [palette-set] verb '" + verb + "' renders exactly [" + WoodWallId + "] (wall_stone locked) OK");
            }
        }

        // =====================================================================
        //  CASE 2 [replay-safe] -- the catalog rows survive; the replay path
        //  cannot consult the palette gate.
        // =====================================================================
        private static void CaseReplaySafe(List<string> failures, StringBuilder log)
        {
            if (CatalogRegistry.Get(StoneWallId) == null)
                failures.Add("[replay-safe] catalog row '" + StoneWallId + "' is GONE - existing saves with placed " +
                             "stone walls can no longer replay/sell them. WO-948 closes only PLACEMENT; the row must survive.");
            if (CatalogRegistry.Get(StoneGateId) == null)
                failures.Add("[replay-safe] catalog row '" + StoneGateId + "' is GONE - a gate is not a wall tier and " +
                             "its row must survive untouched (its ladder is WO-904's).");

            // The replay path resolves via CatalogRegistry.Get + StructureFactory.Create
            // (BaseLayoutLoader.Spawn :287) and must never grow a dependency on the palette
            // gate. Source-lint: no LockedIds / BuildCategoryRegistry token in the loader.
            string src = ReadSource(LoaderSrc);
            if (src == null)
            {
                failures.Add("[replay-safe] cannot read " + LoaderSrc + " - the replay-isolation lint could not run");
            }
            else
            {
                if (src.Contains("LockedIds") || src.Contains("lockedIds") || src.Contains("BuildCategoryRegistry"))
                    failures.Add("[replay-safe] " + LoaderSrc + " references the palette gate (LockedIds/" +
                                 "BuildCategoryRegistry) - replaying a saved stone wall would now consult the palette, " +
                                 "which WO-948 explicitly must not affect");
                else
                    log.AppendLine("  [replay-safe] rows survive; BaseLayoutLoader has no palette-gate reference OK");
            }
        }

        // =====================================================================
        //  CASE 3 [l1-cap] -- the wood->stone rung is authored; deeper is not.
        // =====================================================================
        private static void CaseL1Cap(List<string> failures, StringBuilder log)
        {
            int already = failures.Count;
            var wood = CatalogRegistry.Get(WoodWallId);
            if (wood == null || wood.repo == null)
            {
                failures.Add("[l1-cap] '" + WoodWallId + "' missing (or has no repo) - the one placeable wall is gone");
            }
            else
            {
                if (wood.repo.maxLevel != 2)
                    failures.Add("[l1-cap] " + WoodWallId + ".repo.maxLevel = " + wood.repo.maxLevel +
                                 ", expected 2 - the build-reachable ladder is wood(L1)->stone(L2) ONLY; " +
                                 "steel/spiked are WO-904's behind raid-steal");
                bool priced = wood.repo.upgradeCost != null && wood.repo.upgradeCost.Length >= 1 &&
                              !wood.repo.upgradeCost[0].IsZero;
                if (!priced)
                    failures.Add("[l1-cap] " + WoodWallId + " has NO authored non-zero L1->L2 upgradeCost - the " +
                                 "wood->stone rung would fall back to the generic scaler or be free");
                bool visual = wood.repo.upgradeVisualPath != null && wood.repo.upgradeVisualPath.Length >= 1 &&
                              string.Equals(wood.repo.upgradeVisualPath[0], StoneVisual, StringComparison.Ordinal);
                if (!visual)
                    failures.Add("[l1-cap] " + WoodWallId + ".repo.upgradeVisualPath[0] != '" + StoneVisual +
                                 "' - upgrading to stone would not swap the model (the tier reskin reads this array)");
                else if (Resources.Load<GameObject>(StoneVisual) == null)
                    failures.Add("[l1-cap] the stone tier model '" + StoneVisual + "' does not LOAD from Resources - " +
                                 "the authored upgradeVisualPath points at nothing");
                if (wood.repo.wallTierBase != 0)
                    failures.Add("[l1-cap] " + WoodWallId + ".repo.wallTierBase = " + wood.repo.wallTierBase +
                                 ", expected 0 (wood is the walls.json base rung)");
                if (failures.Count == already)
                    log.AppendLine("  [l1-cap] " + WoodWallId + ": maxLevel 2, rung priced, stone model resolves OK");
            }

            var stone = CatalogRegistry.Get(StoneWallId);
            if (stone != null && stone.repo != null)
            {
                if (stone.repo.maxLevel != 1)
                    failures.Add("[l1-cap] " + StoneWallId + ".repo.maxLevel = " + stone.repo.maxLevel +
                                 ", expected 1 - a legacy placed stone wall is already AT the stone rung; " +
                                 "climbing further from it would reach steel outside WO-904's gate");
                if (stone.repo.wallTierBase != 1)
                    failures.Add("[l1-cap] " + StoneWallId + ".repo.wallTierBase = " + stone.repo.wallTierBase +
                                 ", expected 1 (its walls.json ladder level: Stone) - the WallDefense derive " +
                                 "would misread a legacy stone wall as wood");
            }

            // NO Wall row may reach past walls.json level 1 through the upgrade verb:
            // wallTierBase + (maxLevel - 1) <= MaxReachableWallLevel for every Wall row.
            foreach (var e in CatalogRegistry.OfType(CatalogType.Wall))
            {
                if (e == null || e.repo == null) continue;
                int top = e.repo.wallTierBase + Mathf.Max(1, e.repo.maxLevel) - 1;
                if (top > WallDefense.MaxReachableWallLevel)
                    failures.Add("[l1-cap] Wall row '" + e.id + "' can climb to walls.json level " + top +
                                 " (wallTierBase " + e.repo.wallTierBase + " + maxLevel " + e.repo.maxLevel +
                                 ") - past the WO-948 stone cap (" + WallDefense.MaxReachableWallLevel +
                                 "); steel/spiked are WO-904's");
            }
        }

        // =====================================================================
        //  CASE 4 [derive] -- the WallDefense min-rule derive + the rung's payoff.
        // =====================================================================
        private static void CaseDerive(List<string> failures, StringBuilder log)
        {
            int already = failures.Count;
            if (WallDefense.MaxReachableWallLevel != 1)
                failures.Add("[derive] WallDefense.MaxReachableWallLevel = " + WallDefense.MaxReachableWallLevel +
                             ", expected 1 - raising it is WO-904's move (raid-steal), nothing else's");

            // Pure derive cases: (wallTierBase, placedLevel) -> expected walls.json level.
            AssertDerive(failures, "no walls placed", new (int, int)[0], 0);
            AssertDerive(failures, "one wood wall at L1", new[] { (0, 1) }, 0);
            AssertDerive(failures, "one wood wall upgraded to stone (L2)", new[] { (0, 2) }, 1);
            AssertDerive(failures, "legacy stone wall (base 1, L1)", new[] { (1, 1) }, 1);
            AssertDerive(failures, "mixed: wood L1 + legacy stone (min rule)", new[] { (0, 1), (1, 1) }, 0);
            AssertDerive(failures, "all upgraded: wood L2 + legacy stone", new[] { (0, 2), (1, 1) }, 1);
            AssertDerive(failures, "legacy over-levelled wood L3 (capped at stone)", new[] { (0, 3) }, 1);
            AssertDerive(failures, "legacy over-levelled stone L3 (capped at stone)", new[] { (1, 3) }, 1);

            // The rung PAYS, from the same data the wave loop consumes.
            float m0 = WallDefense.HeartDamageMultiplier(0);
            float m1 = WallDefense.HeartDamageMultiplier(1);
            if (!(m1 < m0))
                failures.Add("[derive] heartDamageMultiplier L1 (" + m1 + ") is not < L0 (" + m0 +
                             ") - the wood->stone rung grants the Heart no protection (walls.json broken?)");
            float h0 = WallDefense.TargetHeight(0);
            float h1 = WallDefense.TargetHeight(1);
            if (!(h1 > h0) || h0 <= 0f)
                failures.Add("[derive] targetHeight L1 (" + h1 + ") is not > L0 (" + h0 +
                             ") > 0 - the stone tier's data height is not authored/loaded");

            if (failures.Count == already)
                log.AppendLine("  [derive] min rule + stone cap hold; stone rung pays (heartMult " +
                               m0.ToString("0.00") + "->" + m1.ToString("0.00") + ", height " +
                               h0.ToString("0.0") + "->" + h1.ToString("0.0") + ") OK");
        }

        private static void AssertDerive(List<string> failures, string what, (int, int)[] walls, int expected)
        {
            int got = WallDefense.DeriveWallLevel(walls);
            if (got != expected)
                failures.Add("[derive] " + what + ": DeriveWallLevel = " + got + ", expected " + expected);
        }

        // =====================================================================
        //  Helpers
        // =====================================================================
        private static string ReadSource(string rel)
        {
            try
            {
                var parent = Directory.GetParent(Application.dataPath);
                string root = parent != null ? parent.FullName : Directory.GetCurrentDirectory();
                string p = Path.Combine(root, rel);
                return File.Exists(p) ? File.ReadAllText(p) : null;
            }
            catch (Exception) { return null; }
        }
    }
}
