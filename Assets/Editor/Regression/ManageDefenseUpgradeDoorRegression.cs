// =============================================================================
// ManageDefenseUpgradeDoorRegression — headless oracle: once a defensive
// structure is standing in this town, Manage's DEFENSE tab is the door to
// upgrading it, and it actually opens.
// Marker: MANAGE_DEFENSE_DOOR_OK / MANAGE_DEFENSE_DOOR_FAIL.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (editor-only). Wired into DataRegression.RunAll.
// Style/contract mirrors ManageTroopsTrainDoorRegression, which this is modelled on.
//
// WHY THIS SUITE EXISTS:
//   Owner felt-test 2026-08-30, build 2026.08.30.348233: "I do not see a way from
//   manage or anywhere else intuitively to get to the upgrade defensive screen."
//
//   The investigation found the DOOR intact and the screen correctly GATED: the
//   owner had run ResetToNewGame that morning (proven in the device boot log), so
//   GameState.BaseLayout was empty, ManageScreenVM.BuildVisibleTabs derived zero
//   categories, and the progressive-disclosure contract sent her to the Build-new
//   route instead. Working as designed on a fresh save.
//
//   That is precisely why this suite is worth having. "Correctly gated" and
//   "silently orphaned" look IDENTICAL from the player's chair, and the existing
//   suites cannot tell them apart either — UpgradeQueueFullSurfaceRegression,
//   BuildingUpgradeAuthorityRegression, UpgradeFamilyPrecedenceRegression and
//   PlacedUpgradePageTruthRegression all test the upgrade LAYERS (the queue-full
//   surface, the authority, the family resolver, the page's truthfulness) and
//   none tests whether a player who OWNS a tower is offered a way to upgrade it.
//   ManageProgressiveDisclosureRegression pins the source SHAPE of the gate but
//   never drives it, so it would pass with the gate stuck permanently closed.
//
//   So this suite asserts the OTHER half of the contract that the gate implies:
//   absent before placement is fine, but PRESENT AFTER PLACEMENT is mandatory.
//
// Proves, with REAL types, the REAL catalog and the REAL VM (no play mode):
//   0. premise — the catalog still carries at least one per-instance level ladder;
//   1. an EMPTY BaseLayout yields NO tabs (the gate is real, and this is the state
//      the owner reported from);
//   2. ONE placed ladder structure makes ManageTab.Defense appear — the case that
//      catches a genuinely orphaned door;
//   3. the Defense tab emits a real Upgrade row for it, with a live Activate; a
//      drawn tab with no rows is not a door;
//   4. the row's CTA is keyed through PlacedUpgradeKey, not the bare id (the
//      2026-08-16 defect where Manage told the player a level-1 tower was maxed).
// =============================================================================

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using DeNelle.Core.Catalog;
using DeNelle.Core.State;
using DeNelle.Village.Buildings.Progression;
using DeNelle.Village.UI;

namespace DeNelle.Editor
{
    public static class ManageDefenseUpgradeDoorRegression
    {
        private const string CatalogRelPath = "Data/Canonical/structures-catalog.json";

        private sealed class StructuresFile
        {
            [JsonProperty("entries")] public List<CatalogEntry> Entries = new List<CatalogEntry>();
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("=== ManageDefenseUpgradeDoorRegression: owning a tower must offer a way to upgrade it ===");

            try
            {
                var ladder = LoadOneLadderEntry(failures, log);
                if (ladder != null) RunLiveDoorChecks(ladder, failures, log);
            }
            catch (Exception ex)
            {
                failures.Add($"ManageDefenseUpgradeDoorRegression threw: {ex.GetType().Name}: {ex.Message}");
            }

            return Verdict(failures, log, out reason);
        }

        // ── 0. premise: hydrate the real catalog, take one real ladder id ─────
        private static CatalogEntry LoadOneLadderEntry(List<string> failures, StringBuilder log)
        {
            string json = DeNelle.Core.CanonicalJson.Read(CatalogRelPath);
            if (string.IsNullOrEmpty(json))
            {
                failures.Add("[premise] " + CatalogRelPath + " unreadable — the subject of this oracle is unknowable, " +
                             "so it asserts nothing. FAIL, not a skip.");
                return null;
            }

            StructuresFile file;
            try
            {
                file = JsonConvert.DeserializeObject<StructuresFile>(json, new JsonSerializerSettings
                {
                    Converters = { new StringEnumConverter() },
                    NullValueHandling = NullValueHandling.Ignore,
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                });
            }
            catch (Exception ex)
            {
                failures.Add("[premise] structures-catalog.json failed to parse: " + ex.Message);
                return null;
            }

            if (file == null || file.Entries == null || file.Entries.Count == 0)
            {
                failures.Add("[premise] structures-catalog.json deserialized to 0 entries");
                return null;
            }

            CatalogEntry chosen = null;
            int ladders = 0;
            foreach (var e in file.Entries)
            {
                if (e == null || e.repo == null || string.IsNullOrEmpty(e.id)) continue;
                // Hydrate so the REAL resolvers inside the VM answer exactly as they do in game.
                if (CatalogRegistry.Get(e.id) == null) CatalogRegistry.Register(e);
                if (e.repo.maxLevel <= 1) continue;
                ladders++;
                // Prefer the first entry whose SHARED ceiling is genuinely > 1: reading raw
                // maxLevel would pick a row the upgrade service then refuses, and the tab would
                // correctly stay empty for a reason that has nothing to do with the door.
                if (chosen == null && PlacedStructureUpgradeService.MaxLevelFor(e) > 1) chosen = e;
            }

            if (chosen == null)
            {
                failures.Add("[premise] NO catalog row carries a usable per-instance level ladder (repo.maxLevel > 1 " +
                             "AND PlacedStructureUpgradeService.MaxLevelFor > 1). The Defense tab has no subject at " +
                             "all — a data regression, not a vacuous pass.");
                return null;
            }

            log.AppendLine($"  [premise] {ladders} ladder row(s) in the catalog; subject = '{chosen.id}' " +
                           $"(ceiling {PlacedStructureUpgradeService.MaxLevelFor(chosen)})");
            return chosen;
        }

        // ── 1-4. the LIVE door: drive the real VM against real state ──────────
        private static void RunLiveDoorChecks(CatalogEntry subject, List<string> failures, StringBuilder log)
        {
            var priorInstance = GameStateService.Instance;
            // The VM's CTAs can reach a spend path that calls Save(), which writes the editor
            // PlayerPrefs slot. Back it up and restore it so a regression run can never eat a
            // developer's editor save.
            string priorSave = PlayerPrefs.GetString(SaveSchema.PlayerPrefsKey, null);

            GameObject gssGo = null;
            GameState throwaway = null;
            try
            {
                throwaway = ScriptableObject.CreateInstance<GameState>();
                gssGo = new GameObject("GSS (manage-defense-door oracle)");
                var gss = gssGo.AddComponent<GameStateService>();
                if (!InstallState(gss, throwaway))
                {
                    // NOT A SKIP (the UpgradeQueueFullSurfaceRegression ruling): a suite that
                    // green-passes on an unreachable seam asserts nothing, most eagerly on the
                    // day the seam breaks.
                    failures.Add("[manage-defense-door] GameStateService state seam is not reflectable, so the LIVE " +
                                 "checks (gate closed when empty, Defense tab after placement, real Upgrade row) " +
                                 "could not run. This is a FAIL, not a skip.");
                    return;
                }

                throwaway.Onboarded = true;
                throwaway.BaseLayout = new List<PlacedStructureData>();

                // ── CASE 1: empty town -> no categories. The gate is real. ────
                // This is the exact state the owner reported from (ResetToNewGame, blank town).
                // It must stay TRUE: if this ever fails, the disclosure contract has inverted and
                // a fresh player is being shown categories for things they do not own.
                var empty = new ManageScreenVM();
                empty.Rebuild();
                if (empty.VisibleTabs.Count != 0)
                    failures.Add("[case 1] an EMPTY BaseLayout produced " + empty.VisibleTabs.Count + " visible tab(s) " +
                                 "(" + Describe(empty.VisibleTabs) + "). Progressive disclosure says every category is " +
                                 "absent until something is placed; the Build-new route is the fresh-save answer.");
                else
                    log.AppendLine("  case 1 OK - empty BaseLayout -> 0 visible tabs (correctly gated fresh save)");

                // ── CASE 2: place ONE. The Defense tab must appear. THE case. ──
                throwaway.BaseLayout.Add(new PlacedStructureData(subject.id, 3, 7, 0, 1));

                var vm = new ManageScreenVM();
                vm.Rebuild();
                log.AppendLine("  after placing 1x '" + subject.id + "': visible tabs = " + Describe(vm.VisibleTabs));

                if (!vm.VisibleTabs.Contains(ManageTab.Defense))
                {
                    failures.Add("[case 2] a structure with a level ladder ('" + subject.id + "') is standing in this " +
                                 "town and ManageScreenVM STILL does not offer the Defense tab (tabs: " +
                                 Describe(vm.VisibleTabs) + "). A player who owns a tower has no way to upgrade it — " +
                                 "this is the orphaned-door case the owner reported. See " +
                                 "ManageScreenVM.BuildVisibleTabs / HasLevelLadder.");
                    return;
                }
                log.AppendLine("  case 2 OK - Defense tab appears once a ladder structure is placed");

                // ── CASE 3: the tab actually emits a row a player can tap ─────
                vm.SelectTab(ManageTab.Defense);
                vm.Rebuild();

                var rows = new List<BrowseRowVM>(vm.BrowseRows);
                log.AppendLine($"  Defense tab produced {rows.Count} browse row(s):");
                for (int i = 0; i < rows.Count; i++)
                    log.AppendLine($"    [{rows[i].ActionText}] \"{rows[i].Label}\"");

                var upgradeRows = rows.FindAll(r => r != null &&
                                                    string.Equals(r.ActionText, "Upgrade", StringComparison.Ordinal));
                if (upgradeRows.Count == 0)
                {
                    failures.Add("[case 3] the Defense tab is VISIBLE but emitted no row with ActionText \"Upgrade\" " +
                                 "for the placed '" + subject.id + "' at level 1 of " +
                                 PlacedStructureUpgradeService.MaxLevelFor(subject) + ". A tab that opens onto an empty " +
                                 "list is not a door — it reads to the player exactly like the feature not existing.");
                    return;
                }
                if (upgradeRows[0].Activate == null)
                    failures.Add("[case 3] the Defense Upgrade row has a null Activate — a row that does nothing is " +
                                 "not a door.");
                else
                    log.AppendLine($"  case 3 OK - \"{upgradeRows[0].Label}\" is tappable");

                // ── CASE 4: the CTA is keyed through PlacedUpgradeKey ─────────
                // Regression-locks the 2026-08-16 defect: the row used to pass the BARE id, which
                // UpgradeFamilyResolver classifies as None, so the page rendered "tier 0 of 0 —
                // nothing left to upgrade" for a tower at level 1 of 3. Manage told the player a
                // brand-new tower was maxed. The '@' in the composed key is what makes the
                // resolver answer PlacedStructure.
                string key = PlacedUpgradeKey.Compose(subject.id, 3, 7);
                if (UpgradeFamilyResolver.Resolve(key) != UpgradeFamily.PlacedStructure)
                    failures.Add("[case 4] PlacedUpgradeKey.Compose('" + subject.id + "',3,7) = '" + key +
                                 "' no longer resolves to UpgradeFamily.PlacedStructure — the Defense row's CTA would " +
                                 "land on the BuildUnknown page and tell the player a level-1 structure is maxed.");
                else
                    log.AppendLine("  case 4 OK - the row's job key resolves to UpgradeFamily.PlacedStructure");
            }
            finally
            {
                if (gssGo != null) UnityEngine.Object.DestroyImmediate(gssGo);
                if (throwaway != null) UnityEngine.Object.DestroyImmediate(throwaway);
                SetGssInstance(priorInstance);
                if (priorSave != null) PlayerPrefs.SetString(SaveSchema.PlayerPrefsKey, priorSave);
                else PlayerPrefs.DeleteKey(SaveSchema.PlayerPrefsKey);
            }
        }

        // ── helpers ───────────────────────────────────────────────────────────

        private static string Describe(List<ManageTab> tabs)
        {
            if (tabs == null || tabs.Count == 0) return "(none)";
            var parts = new string[tabs.Count];
            for (int i = 0; i < tabs.Count; i++) parts[i] = tabs[i].ToString();
            return string.Join(", ", parts);
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

        private static bool Verdict(List<string> failures, StringBuilder log, out string reason)
        {
            if (failures.Count == 0)
            {
                reason = "MANAGE DEFENSE DOOR OK - an empty town shows no categories (correctly gated), placing one " +
                         "ladder structure raises the Defense tab, and that tab emits a tappable Upgrade row keyed " +
                         "through PlacedUpgradeKey";
                Debug.Log("MANAGE_DEFENSE_DOOR_OK\n" + log);
                return true;
            }
            reason = $"MANAGE DEFENSE DOOR: {failures.Count} failure(s): " + string.Join(" | ", failures.ToArray());
            Debug.LogError($"MANAGE_DEFENSE_DOOR_FAIL: {failures.Count} failure(s)\n" + log +
                           "\n - " + string.Join("\n - ", failures.ToArray()));
            return false;
        }
    }
}
