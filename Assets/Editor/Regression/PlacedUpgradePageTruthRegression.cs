// =============================================================================
// PlacedUpgradePageTruthRegression [placed-upgrade-page] -- pins THE INVARIANT that
// EVERY structure family with an upgrade ladder reaching the upgrade panel gets a
// TRUTHFUL page.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Village + DeNelle.Core).
// Marker: PLACED_UPGRADE_PAGE_OK / PLACED_UPGRADE_PAGE_FAIL. Expected: GREEN.
//
// THE DEFECT THIS EXISTS TO PREVENT (owner ruling 2026-08-16, "upgrades should be
// accessable from manage tab"):
//   ManageScreenVM.BuildDefenseBrowse built rows for towers/walls/containers and then
//   opened the upgrade panel with a BARE catalog id. UpgradeFamilyResolver answers
//   None for a bare placed id, so BuildingUpgradeVM fell through to BuildUnknown,
//   which sets MaxTier = 0 -- and the View renders BuildMaxedCard: "<Building> has
//   reached tier 0 of 0 - there is nothing left to upgrade here." The Manage screen
//   told the player a LEVEL-1-OF-3 TOWER WAS MAXED.
//
// THE ASSERTION IS THE INVARIANT, NOT "towers work":
//   1. ROUTING   -- for EVERY catalog row with repo.maxLevel > 1, the job key
//                   PlacedUpgradeKey.Compose(id, x, z) resolves to
//                   UpgradeFamily.PlacedStructure (a bare id must NOT, or the '@'
//                   grammar has stopped carrying the family).
//   2. TRUTHFUL  -- a BuildingUpgradeVM opened on that key reports MaxTier == the
//      PAGE        catalog ceiling, CurrentTier < MaxTier at level 1, and
//                  HasNextUpgrade == true. HasNextUpgrade == false at level 1 is
//                  EXACTLY the state that renders the "nothing left to upgrade"
//                  maxed card, so this is the lie made undeniable.
//   3. ONE START -- the charge/gate/StartUpgrade sequence exists in exactly ONE file
//                   (PlacedStructureUpgradeService). Neither BuildModeController nor
//                   BuildingUpgradeVM may call BuildTimerService.StartUpgrade itself.
//                   Two copies of a start path is the dual-authority defect
//                   UpgradeFamilyResolver was created to retire. Matching runs on
//                   source with comments AND string literals STRIPPED, because several
//                   oracles have green/red-ed themselves on their own prose.
//   4. ONE KEY   -- the "itemId@cellX_cellZ" shape is composed through PlacedUpgradeKey,
//                   not hand-spelled, in the sites that produce it.
//
// Wire (DataRegression.RunAll):
//   if (!PlacedUpgradePageTruthRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[placed-upgrade-page] " + r);
// =============================================================================
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using DeNelle.Core.Catalog;
using DeNelle.Village.Buildings.Progression;

namespace DeNelle.Editor
{
    public static class PlacedUpgradePageTruthRegression
    {
        private const string CatalogRelPath = "Data/Canonical/structures-catalog.json";

        // The files that must NOT own a second copy of the start sequence. Relative to Assets/.
        private const string ServiceRel = "_Modules/Village/Buildings/Progression/PlacedStructureUpgradeService.cs";
        private static readonly string[] NonStartSites =
        {
            "_Modules/Village/BuildMode/BuildModeController.cs",              // the in-world doorway
            "_Modules/Village/Buildings/Progression/BuildingUpgradeVM.cs",    // the panel doorway
        };

        // The sites that PRODUCE a placed job key — they must compose it, not spell it.
        private static readonly string[] KeyComposingSites =
        {
            "_Modules/Village/BuildMode/BuildModeController.cs",
            "_Modules/Village/BuildMode/UnderConstructionVisual.cs",
            "_Modules/Village/UI/Manage/ManageScreenVM.cs",
        };

        private sealed class StructuresFile
        {
            [JsonProperty("entries")] public List<CatalogEntry> Entries = new List<CatalogEntry>();
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- PLACED UPGRADE PAGE TRUTH (no maxLevel>1 structure may render the maxed card at level 1) ---");

            var ladderRows = LoadLadderRows(failures, log);
            CheckRouting(ladderRows, failures, log);
            CheckTruthfulPage(ladderRows, failures, log);
            CheckOneStartPath(failures, log);
            CheckOneKeyComposer(failures, log);

            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "PLACED_UPGRADE_PAGE_OK");
                reason = "PLACED UPGRADE PAGE OK -- " + ladderRows.Count +
                         " ladder row(s) route to PlacedStructure and report a real ceiling; ONE start path";
                return true;
            }
            string r = "placed-upgrade-page: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "PLACED_UPGRADE_PAGE_FAIL: " + r);
            reason = r;
            return false;
        }

        // ── Catalog: every row that carries a per-instance level ladder ──────────
        private static List<CatalogEntry> LoadLadderRows(List<string> failures, StringBuilder log)
        {
            var rows = new List<CatalogEntry>();
            string json = DeNelle.Core.CanonicalJson.Read(CatalogRelPath);
            if (string.IsNullOrEmpty(json))
            {
                failures.Add("[premise] " + CatalogRelPath + " unreadable -- the ladder set is unknowable, so this oracle cannot assert anything");
                return rows;
            }

            StructuresFile file = null;
            try
            {
                file = JsonConvert.DeserializeObject<StructuresFile>(json, new JsonSerializerSettings
                {
                    Converters = { new StringEnumConverter() },
                    NullValueHandling = NullValueHandling.Ignore,
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                });
            }
            catch (System.Exception ex)
            {
                failures.Add("[premise] structures-catalog.json failed to parse: " + ex.Message);
                return rows;
            }

            if (file == null || file.Entries == null || file.Entries.Count == 0)
            {
                failures.Add("[premise] structures-catalog.json deserialized to 0 entries");
                return rows;
            }

            foreach (var e in file.Entries)
            {
                if (e == null || e.repo == null || string.IsNullOrEmpty(e.id)) continue;
                // Hydrate the registry so the REAL resolvers (CatalogRegistry.Get inside the VM
                // and the cost path) answer exactly as they do in the game.
                if (CatalogRegistry.Get(e.id) == null) CatalogRegistry.Register(e);
                if (e.repo.maxLevel > 1) rows.Add(e);
            }

            // NOT a skip: a catalog with no level ladders at all means the whole placed-upgrade
            // page has no subject, which is a real regression in the data, not a reason to pass.
            if (rows.Count == 0)
                failures.Add("[premise] NO catalog row carries repo.maxLevel > 1 -- the placed-structure upgrade page has no subject at all (data regression, not a vacuous pass)");

            log.AppendLine("  [premise] " + rows.Count + " catalog row(s) carry a per-instance level ladder (repo.maxLevel > 1)");
            return rows;
        }

        // ── 1. ROUTING: the job key resolves to the placed family, the bare id must not ──
        private static void CheckRouting(List<CatalogEntry> rows, List<string> failures, StringBuilder log)
        {
            log.AppendLine("  [routing] PlacedUpgradeKey.Compose(id,x,z) -> UpgradeFamily.PlacedStructure");
            foreach (var e in rows)
            {
                string key = PlacedUpgradeKey.Compose(e.id, 3, 7);
                var family = UpgradeFamilyResolver.Resolve(key);
                if (family != UpgradeFamily.PlacedStructure)
                    failures.Add("[routing] UpgradeFamilyResolver.Resolve('" + key + "') = " + family +
                                 ", expected PlacedStructure -- the panel would fall through to BuildUnknown (MaxTier 0) and render the 'nothing left to upgrade' maxed card for a level-1 structure");

                if (!PlacedUpgradeKey.TryParse(key, out string backId, out int bx, out int bz)
                    || backId != e.id || bx != 3 || bz != 7)
                    failures.Add("[routing] PlacedUpgradeKey round-trip broke for '" + e.id +
                                 "' (composed '" + key + "') -- the timer key and the panel id would name different structures");
            }
        }

        // ── 2. TRUTHFUL PAGE: the VM reports a real ceiling and a next step ──────
        private static void CheckTruthfulPage(List<CatalogEntry> rows, List<string> failures, StringBuilder log)
        {
            log.AppendLine("  [truthful-page] BuildingUpgradeVM(jobKey) -> MaxTier == ceiling, HasNextUpgrade at level 1");
            foreach (var e in rows)
            {
                int ceiling = PlacedStructureUpgradeService.MaxLevelFor(e);
                if (ceiling <= 1)
                {
                    failures.Add("[truthful-page] '" + e.id + "' authors maxLevel " + e.repo.maxLevel +
                                 " but the shared ceiling resolver returns " + ceiling +
                                 " -- the ladder is unreachable and the page would be honest-but-empty for authored data");
                    continue;
                }

                // No GameStateService in batchmode: LevelOf then reports the level-1 baseline,
                // which IS the case under test (a freshly placed structure must not read maxed).
                BuildingUpgradeVM vm = null;
                try
                {
                    vm = new BuildingUpgradeVM(PlacedUpgradeKey.Compose(e.id, 3, 7), null, null);
                    log.AppendLine("    '" + e.id + "': " + vm.TierWord + " " + vm.CurrentTier + " of " + vm.MaxTier +
                                   " hasNext=" + vm.HasNextUpgrade + " state=" + vm.ActionState);

                    if (vm.MaxTier != ceiling)
                        failures.Add("[truthful-page] '" + e.id + "' opens with MaxTier " + vm.MaxTier +
                                     ", expected " + ceiling + " -- the page states a ceiling the ladder does not have");
                    if (vm.CurrentTier < 1)
                        failures.Add("[truthful-page] '" + e.id + "' opens at level " + vm.CurrentTier +
                                     " -- a standing structure is never below level 1");
                    if (!vm.HasNextUpgrade)
                        failures.Add("[truthful-page] '" + e.id + "' opens with HasNextUpgrade FALSE at level " +
                                     vm.CurrentTier + " of " + vm.MaxTier +
                                     " -- that is the exact state that renders 'has reached tier 0 of 0 - there is nothing left to upgrade here'. THE PAGE IS LYING ABOUT A STRUCTURE THE PLAYER CAN STILL UPGRADE.");
                    if (vm.ActionState == UpgradeActionState.Maxed || vm.ActionState == UpgradeActionState.Unavailable)
                        failures.Add("[truthful-page] '" + e.id + "' opens with ActionState " + vm.ActionState +
                                     " at level " + vm.CurrentTier + " of " + vm.MaxTier +
                                     " -- the one true button would offer no way to upgrade an upgradable structure");
                }
                catch (System.Exception ex)
                {
                    failures.Add("[truthful-page] opening the upgrade page for '" + e.id + "' THREW " +
                                 ex.GetType().Name + ": " + ex.Message);
                }
                finally
                {
                    vm?.Dispose();
                }
            }
        }

        // ── 3. ONE START PATH ────────────────────────────────────────────────────
        private static void CheckOneStartPath(List<string> failures, StringBuilder log)
        {
            log.AppendLine("  [one-start] StartUpgrade( lives ONLY in PlacedStructureUpgradeService (comments + string literals stripped)");

            string serviceSrc = ReadStripped(ServiceRel, failures);
            if (serviceSrc != null && !serviceSrc.Contains("StartUpgrade("))
                failures.Add("[one-start] Assets/" + ServiceRel + " no longer calls StartUpgrade( -- the extracted start path has been hollowed out, so whatever calls it now owns a second copy");

            foreach (var rel in NonStartSites)
            {
                string src = ReadStripped(rel, failures);
                if (src == null) continue;
                bool startsTimer = src.Contains(".StartUpgrade(");
                bool callsService = src.Contains("PlacedStructureUpgradeService.");
                log.AppendLine("    Assets/" + rel + ": callsService=" + callsService + " ownsStartUpgrade=" + startsTimer);

                if (startsTimer)
                    failures.Add("[one-start] Assets/" + rel + " calls .StartUpgrade( itself -- the charge/gate/start sequence now exists in TWO places, which is the dual-authority defect UpgradeFamilyResolver was created to retire; route it through PlacedStructureUpgradeService");
                if (!callsService)
                    failures.Add("[one-start] Assets/" + rel + " no longer routes through PlacedStructureUpgradeService -- one of the two doorways has stopped sharing the one start path");
            }
        }

        // ── 4. ONE KEY COMPOSER ──────────────────────────────────────────────────
        private static void CheckOneKeyComposer(List<string> failures, StringBuilder log)
        {
            log.AppendLine("  [one-key] the itemId@cellX_cellZ shape is composed through PlacedUpgradeKey");
            foreach (var rel in KeyComposingSites)
            {
                string src = ReadStripped(rel, failures);
                if (src == null) continue;
                bool composes = src.Contains("PlacedUpgradeKey.Compose(");
                log.AppendLine("    Assets/" + rel + ": composes=" + composes);
                if (!composes)
                    failures.Add("[one-key] Assets/" + rel + " no longer composes the placed job key through PlacedUpgradeKey.Compose -- a hand-spelled key that drifts by one character silently never matches its timer job, and the '@' is the grammar UpgradeFamilyResolver reads to pick the family");
            }
        }

        private static string ReadStripped(string rel, List<string> failures)
        {
            string path = Path.Combine(Application.dataPath, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                failures.Add("[source] Assets/" + rel + " not found -- the oracle cannot pin its invariant (file moved? update this suite)");
                return null;
            }
            return StripCommentsAndStrings(File.ReadAllText(path));
        }

        // Strip comments AND string/char literals: several oracles have matched their own
        // prose and reported a false result, so nothing grepped here may come from a comment.
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
    }
}
