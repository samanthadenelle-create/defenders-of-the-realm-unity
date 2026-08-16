// =============================================================================
// DualFamilyLevelResetRegression [dualfamily-level-reset] -- pins the one-shot
// migration that resets the legacy RESOURCE-ladder level of every DUAL-FAMILY
// building to 1 (owner ruling 2026-08-15: "reset to 1").
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Village + DeNelle.Core).
//
// THE DEFECT THIS CLEANS UP (fixed at source in adfbec3c):
//   The inverted upgrade-family precedence STARTED a farm/lumbermill/forge upgrade on
//   the CITY tier ladder and APPLIED it to the RESOURCE ladder, writing the purchased
//   city tier number into PlayerPrefs "dotr.resbuilding.level.<id>". Harvest yield and
//   tick speed silently rose while the paid-for tier ladder never moved. The precedence
//   fix stops new bogus writes but FREEZES the existing value -- nothing writes that key
//   for a dual-family id any more -- so a one-shot reset is required.
//
// WHAT IS PINNED (each leg is the invariant, not "the migration ran once"):
//   1. SET      -- the ids the migration acts on are exactly
//                  BuildingTierCatalog INTERSECT ResourceBuildingProgression, DERIVED
//                  through UpgradeFamilyResolver.IsDualFamily. The oracle computes that
//                  intersection independently and compares.
//   2. SCOPE    -- source-lint (comments AND string literals stripped): the migration
//                  asks UpgradeFamilyResolver.IsDualFamily and NEVER calls
//                  ResourceBuildingState.ResetAll (which would wipe every resource
//                  building AND revoke the Magic-gated TechTree unlocks). This is the
//                  leg that keeps leg 4 non-vacuous while EVERY resource building
//                  happens to be dual-family.
//   3. RESET    -- an inflated persisted level on a dual-family id goes back to 1.
//   4. COLLATERAL -- an unrelated "dotr.resbuilding.level.*" key that is NOT a
//                  dual-family resource building SURVIVES untouched.
//   5. ONE-SHOT -- a second call is AlreadyRun / resets nothing, AND a level the player
//                  legitimately earns AFTER the migration is NOT reset again.
//   6. CITY LADDER -- GameState.BuildingTiers is byte-for-byte unchanged across the
//                  migration. It holds the progress the player actually PAID for;
//                  clearing it would delete purchased progress.
//
// Marker: DUALFAMILY_LEVEL_RESET_OK / DUALFAMILY_LEVEL_RESET_FAIL. Expected: GREEN.
//
// Wire (DataRegression.RunAll):
//   if (!DualFamilyLevelResetRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dualfamily-level-reset] " + r);
// =============================================================================
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using DeNelle.Village.Buildings.Progression;
using DeNelle.Core.State;

namespace DeNelle.Editor
{
    public static class DualFamilyLevelResetRegression
    {
        private const string LevelPrefix = "dotr.resbuilding.level.";
        private const string MigrationSource =
            "_Modules/Village/Buildings/Progression/DualFamilyLevelResetMigration.cs";

        // A key under the SAME prefix that is NOT a resource building at all -- the
        // collateral canary. If the migration ever widens to "delete the prefix", this
        // vanishes and leg 4 goes red.
        private const string CanaryId = "zz-oracle-canary-not-a-resource-building";
        private const int CanaryLevel = 4;

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- DUAL-FAMILY RESOURCE-LEVEL RESET (one-shot, city ladder untouched) ---");

            var dual = CheckDerivedSet(failures, log);
            CheckSourceScope(failures, log);
            CheckBehaviour(dual, failures, log);

            reason = Finish(failures, log);
            return failures.Count == 0;
        }

        // -- 1. SET: the derived dual-family intersection ------------------------
        private static List<string> CheckDerivedSet(List<string> failures, StringBuilder log)
        {
            var dual = new List<string>();
            var nonDual = new List<string>();
            foreach (var def in ResourceBuildingProgression.All)
            {
                string id = def != null ? def.BuildingId : null;
                if (string.IsNullOrEmpty(id)) continue;

                bool inCity = BuildingTierCatalog.IsUpgradable(id);
                bool inResource = ResourceBuildingProgression.IsResourceBuilding(id);
                bool resolverSays = UpgradeFamilyResolver.IsDualFamily(id);

                if (resolverSays != (inCity && inResource))
                    failures.Add($"[dualfamily-level-reset] UpgradeFamilyResolver.IsDualFamily('{id}') = {resolverSays} " +
                                 $"but the raw catalogs say cityCatalog={inCity} resourceCatalog={inResource} -- the resolver " +
                                 "is no longer the true intersection, so the migration would act on the wrong set");

                if (resolverSays) dual.Add(id); else nonDual.Add(id);
            }
            dual.Sort(System.StringComparer.Ordinal);
            nonDual.Sort(System.StringComparer.Ordinal);

            log.AppendLine($"  [set] dual-family (corrupt ladder, reset to 1): [{string.Join(", ", dual.ToArray())}]");
            log.AppendLine($"  [set] resource-only (legitimate levels, left alone): [{string.Join(", ", nonDual.ToArray())}]");

            if (dual.Count == 0)
                failures.Add("[dualfamily-level-reset] NO id is dual-family any more (the city and resource ladders no " +
                             "longer overlap) -- the premise of this oracle changed; re-confirm both catalogs before editing it");

            return dual;
        }

        // -- 2. SCOPE: the migration derives, and never wholesale-resets ----------
        private static void CheckSourceScope(List<string> failures, StringBuilder log)
        {
            string path = Path.Combine(Application.dataPath,
                MigrationSource.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                failures.Add($"[dualfamily-level-reset] source 'Assets/{MigrationSource}' not found -- the one-shot " +
                             "migration is missing (file moved? update MigrationSource)");
                return;
            }

            string code = StripCommentsAndStrings(File.ReadAllText(path));
            bool derives = code.Contains("UpgradeFamilyResolver.IsDualFamily(");
            bool wholesale = code.Contains("ResourceBuildingState.ResetAll(") || code.Contains("ResetAll(");
            bool touchesCityTiers = code.Contains("BuildingTiers");
            log.AppendLine($"  [scope] Assets/{MigrationSource}: derivesViaResolver={derives} callsResetAll={wholesale} " +
                           $"mentionsBuildingTiers={touchesCityTiers}");

            if (!derives)
                failures.Add("[dualfamily-level-reset] the migration does not call UpgradeFamilyResolver.IsDualFamily( -- " +
                             "it hardcodes or hand-derives the id set, which goes stale the moment a catalog changes");
            if (wholesale)
                failures.Add("[dualfamily-level-reset] the migration calls ResetAll( -- that wipes EVERY resource building " +
                             "(including non-dual-family ids holding legitimate levels) AND revokes the Magic-gated TechTree " +
                             "unlocks. Only the dual-family ids are corrupt; use the per-id reset");
            if (touchesCityTiers)
                failures.Add("[dualfamily-level-reset] the migration references GameState.BuildingTiers -- the CITY ladder is " +
                             "the progress the player actually PAID for and must never be written by this cleanup");
        }

        // -- 3/4/5/6. BEHAVIOUR --------------------------------------------------
        private static void CheckBehaviour(List<string> dual, List<string> failures, StringBuilder log)
        {
            if (dual == null || dual.Count == 0) return;   // premise already failed

            // Save every pref this oracle disturbs, restore in finally.
            bool hadMarker = PlayerPrefs.HasKey(DualFamilyLevelResetMigration.MarkerKey);
            int markerBefore = PlayerPrefs.GetInt(DualFamilyLevelResetMigration.MarkerKey, 0);
            bool hadCanary = PlayerPrefs.HasKey(LevelPrefix + CanaryId);
            int canaryBefore = PlayerPrefs.GetInt(LevelPrefix + CanaryId, int.MinValue);

            var hadLevel = new Dictionary<string, bool>();
            var levelBefore = new Dictionary<string, int>();
            foreach (var id in dual)
            {
                hadLevel[id] = PlayerPrefs.HasKey(LevelPrefix + id);
                levelBefore[id] = PlayerPrefs.GetInt(LevelPrefix + id, int.MinValue);
            }

            GameStateService priorGss = GameStateService.Instance;
            GameObject gssGo = null;
            GameState throwaway = null;
            try
            {
                // ---- fixture: an inflated resource level on every dual-family id, a
                //      purchased CITY tier alongside it, and the collateral canary.
                const int Bogus = 3;
                foreach (var id in dual) PlayerPrefs.SetInt(LevelPrefix + id, Bogus);
                PlayerPrefs.SetInt(LevelPrefix + CanaryId, CanaryLevel);
                PlayerPrefs.DeleteKey(DualFamilyLevelResetMigration.MarkerKey);   // arm the one-shot
                PlayerPrefs.Save();

                throwaway = ScriptableObject.CreateInstance<GameState>();
                gssGo = new GameObject("GSS (dualfamily-level-reset oracle)");
                var gss = gssGo.AddComponent<GameStateService>();
                bool gssReady = InstallState(gss, throwaway);
                if (gssReady)
                {
                    throwaway.BuildingTiers = new Dictionary<string, int>();
                    for (int i = 0; i < dual.Count; i++) throwaway.BuildingTiers[dual[i]] = 2;
                }
                else
                {
                    log.AppendLine("    [city-ladder] SKIPPED: GameStateService state seam not reflectable (needs fleet)");
                }

                // ---- RUN 1
                var outcome1 = DualFamilyLevelResetMigration.RunIfNeeded(out int reset1);
                log.AppendLine($"  [run1] outcome={outcome1} resetCount={reset1} (fixture level {Bogus} on {dual.Count} id(s))");

                if (outcome1 != DualFamilyLevelResetMigration.Outcome.Reset)
                    failures.Add($"[dualfamily-level-reset] run 1 returned {outcome1}, expected Reset -- {dual.Count} " +
                                 $"dual-family id(s) were seeded at the inflated level {Bogus} and the migration did not clean them");
                if (reset1 != dual.Count)
                    failures.Add($"[dualfamily-level-reset] run 1 reset {reset1} id(s), expected {dual.Count} -- an inflated " +
                                 "resource level survived, so the harvest yield and tick speed stay silently boosted forever");

                foreach (var id in dual)
                {
                    int after = ResourceBuildingState.GetLevel(id);
                    log.AppendLine($"    '{id}': level {Bogus} -> {after}");
                    if (after != 1)
                        failures.Add($"[dualfamily-level-reset] '{id}' is at level {after} after the migration, expected 1 " +
                                     "(owner ruling: reset to 1)");
                }

                // ---- 4. COLLATERAL
                int canaryAfter = PlayerPrefs.GetInt(LevelPrefix + CanaryId, int.MinValue);
                log.AppendLine($"  [collateral] non-dual-family key '{LevelPrefix}{CanaryId}': {CanaryLevel} -> {canaryAfter}");
                if (canaryAfter != CanaryLevel)
                    failures.Add($"[dualfamily-level-reset] the migration changed '{LevelPrefix}{CanaryId}' " +
                                 $"({CanaryLevel} -> {canaryAfter}) -- it must reset ONLY dual-family ids; a resource " +
                                 "building outside the overlap holds a level the player legitimately earned");

                // ---- 6. CITY LADDER untouched
                if (gssReady)
                {
                    var tiers = throwaway.BuildingTiers;
                    var bad = new List<string>();
                    foreach (var id in dual)
                    {
                        int t = tiers != null && tiers.TryGetValue(id, out var v) ? v : -1;
                        if (t != 2) bad.Add($"{id}={t}");
                    }
                    log.AppendLine($"  [city-ladder] GameState.BuildingTiers entries after migration: " +
                                   $"{(bad.Count == 0 ? "all still 2 (unchanged)" : string.Join(", ", bad.ToArray()))}");
                    if (bad.Count > 0)
                        failures.Add("[dualfamily-level-reset] the migration modified GameState.BuildingTiers (" +
                                     string.Join(", ", bad.ToArray()) + ") -- the CITY ladder is the progress the player " +
                                     "actually PAID for; resetting it deletes purchased progress");
                }

                // ---- 5. ONE-SHOT + idempotent: a level EARNED AFTER the migration survives.
                string probe = dual[0];
                PlayerPrefs.SetInt(LevelPrefix + probe, 4);
                PlayerPrefs.Save();

                var outcome2 = DualFamilyLevelResetMigration.RunIfNeeded(out int reset2);
                int probeAfter = ResourceBuildingState.GetLevel(probe);
                log.AppendLine($"  [run2] outcome={outcome2} resetCount={reset2}; '{probe}' re-leveled to 4 after the " +
                               $"migration reads back {probeAfter}");

                if (outcome2 != DualFamilyLevelResetMigration.Outcome.AlreadyRun)
                    failures.Add($"[dualfamily-level-reset] run 2 returned {outcome2}, expected AlreadyRun -- the one-shot " +
                                 "latch did not burn, so the cleanup would re-run on every launch");
                if (reset2 != 0)
                    failures.Add($"[dualfamily-level-reset] run 2 reset {reset2} id(s), expected 0 -- the migration is not idempotent");
                if (probeAfter != 4)
                    failures.Add($"[dualfamily-level-reset] '{probe}' was re-leveled to 4 AFTER the migration and reads back " +
                                 $"{probeAfter} -- a player who legitimately levels a resource building later must never be reset again");
                if (!DualFamilyLevelResetMigration.HasRun)
                    failures.Add("[dualfamily-level-reset] DualFamilyLevelResetMigration.HasRun is false after a completed run -- " +
                                 "the PlayerPrefs latch was not persisted");
            }
            catch (System.Exception ex)
            {
                failures.Add($"dualfamily-level-reset oracle threw: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                if (gssGo != null) Object.DestroyImmediate(gssGo);
                if (throwaway != null) Object.DestroyImmediate(throwaway);
                SetGssInstance(priorGss);

                foreach (var id in dual)
                {
                    if (hadLevel[id]) PlayerPrefs.SetInt(LevelPrefix + id, levelBefore[id]);
                    else PlayerPrefs.DeleteKey(LevelPrefix + id);
                }
                if (hadCanary) PlayerPrefs.SetInt(LevelPrefix + CanaryId, canaryBefore);
                else PlayerPrefs.DeleteKey(LevelPrefix + CanaryId);
                if (hadMarker) PlayerPrefs.SetInt(DualFamilyLevelResetMigration.MarkerKey, markerBefore);
                else PlayerPrefs.DeleteKey(DualFamilyLevelResetMigration.MarkerKey);
                PlayerPrefs.Save();
            }
        }

        // -- Source hygiene: strip comments AND string/char literals --------------
        // Nothing this suite greps may come from a comment, a doc-comment or a literal --
        // this file's own prose names ResetAll and BuildingTiers on purpose.
        private static string StripCommentsAndStrings(string src)
        {
            if (string.IsNullOrEmpty(src)) return string.Empty;
            var sb = new StringBuilder(src.Length);
            int i = 0, n = src.Length;
            while (i < n)
            {
                char c = src[i];

                if (c == '/' && i + 1 < n && src[i + 1] == '/')
                {
                    while (i < n && src[i] != '\n') i++;
                    continue;
                }
                if (c == '/' && i + 1 < n && src[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < n && !(src[i] == '*' && src[i + 1] == '/')) i++;
                    i = i + 2 <= n ? i + 2 : n;
                    sb.Append(' ');
                    continue;
                }
                if (c == '@' && i + 1 < n && src[i + 1] == '"')
                {
                    i += 2;
                    while (i < n)
                    {
                        if (src[i] == '"')
                        {
                            if (i + 1 < n && src[i + 1] == '"') { i += 2; continue; }
                            i++; break;
                        }
                        i++;
                    }
                    sb.Append(" \"\" ");
                    continue;
                }
                if (c == '"')
                {
                    i++;
                    while (i < n)
                    {
                        if (src[i] == '\\') { i += 2; continue; }
                        if (src[i] == '"') { i++; break; }
                        i++;
                    }
                    sb.Append(" \"\" ");
                    continue;
                }
                if (c == '\'')
                {
                    i++;
                    while (i < n)
                    {
                        if (src[i] == '\\') { i += 2; continue; }
                        if (src[i] == '\'') { i++; break; }
                        i++;
                    }
                    sb.Append(" '' ");
                    continue;
                }

                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        private static bool InstallState(GameStateService svc, GameState state)
        {
            var f = typeof(GameStateService).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
            if (f == null) return false;
            f.SetValue(svc, state);
            return SetGssInstance(svc);
        }

        private static bool SetGssInstance(GameStateService svc)
        {
            var f = typeof(GameStateService).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            if (f == null) return false;
            f.SetValue(null, svc);
            return true;
        }

        private static string Finish(List<string> failures, StringBuilder log)
        {
            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "DUALFAMILY_LEVEL_RESET_OK");
                return "DUAL-FAMILY LEVEL RESET OK -- dual-family resource levels reset to 1 once; " +
                       "non-dual keys, the city tier ladder and the tech tree untouched";
            }
            string reason = "dualfamily-level-reset: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "DUALFAMILY_LEVEL_RESET_FAIL: " + reason);
            return reason;
        }
    }
}
