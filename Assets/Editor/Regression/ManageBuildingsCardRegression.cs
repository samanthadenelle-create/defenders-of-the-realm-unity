using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using DeNelle.Core.Jobs;
using DeNelle.Core.State;
using DeNelle.Village;
using DeNelle.Village.UI;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>Headless contract for WO-1418's compact Buildings destination.</summary>
    public static class ManageBuildingsCardRegression
    {
        private const string PanelPath = "Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder("=== ManageBuildingsCardRegression ===\n");
            try
            {
                CheckLiveModel(failures, log);
                CheckPanelSource(failures, log);
            }
            catch (Exception ex)
            {
                failures.Add("suite threw " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "MANAGE_BUILDINGS_CARD_OK compact building rail/card/band contract holds";
                Debug.Log(reason + "\n" + log);
                return true;
            }

            reason = "MANAGE_BUILDINGS_CARD_FAIL " + string.Join(" | ", failures);
            Debug.LogError(reason + "\n" + log);
            return false;
        }

        private static void CheckLiveModel(List<string> failures, StringBuilder log)
        {
            GameStateService prior = GameStateService.Instance;
            GameObject host = null;
            GameState fixture = null;
            try
            {
                fixture = ScriptableObject.CreateInstance<GameState>();
                fixture.Onboarded = true;
                fixture.VillageTier = 4;
                fixture.BaseLayout = new List<PlacedStructureData>
                {
                    new PlacedStructureData("forge", 2, 2, 0, 1),
                    new PlacedStructureData("forge", 3, 2, 0, 1),
                    new PlacedStructureData("lumbermill", 4, 2, 0, 1),
                    new PlacedStructureData("barracks", 6, 2, 0, 1),
                };
                fixture.BuildingTiers["forge"] = BuildingTierCatalog.MaxTier("forge");
                fixture.BuildingTiers["lumbermill"] = 1;
                fixture.BuildingTiers["barracks"] = 2;
                fixture.Wood = 100000;
                fixture.Iron = 100000;
                var balances = fixture.Resources;
                balances.Food = 100000;
                balances.Coins = 100000;
                balances.Crystals = 100000;
                fixture.Resources = balances;
                fixture.ObsidianQueue = ObsidianQueueState.Empty();

                host = new GameObject("GSS (manage-buildings-card oracle)");
                var service = host.AddComponent<GameStateService>();
                if (!InstallState(service, fixture))
                {
                    failures.Add("[fixture] GameStateService state seam is unavailable; live cases cannot run");
                    return;
                }

                var vm = new ManageScreenVM();
                vm.SelectTab(ManageTab.Buildings);
                vm.Rebuild();

                // RED: insert `if (level >= maxLevel) continue;` after maxLevel in BuildBuildingChoices.
                if (vm.BuildingChoices.Count != 3)
                    failures.Add("[one-choice-per-building] expected 3 placed ladder ids including maxed; got " +
                                 vm.BuildingChoices.Count);

                var allowed = new HashSet<string>(StringComparer.Ordinal)
                    { "Upgradable", "Max", "Locked", "Building" };
                for (int i = 0; i < vm.BuildingChoices.Count; i++)
                {
                    var choice = vm.BuildingChoices[i];
                    if (choice == null)
                    {
                        failures.Add("[one-choice-per-building] null choice at index " + i);
                        continue;
                    }
                    // RED: insert `description = "";` immediately before `var cost` in BuildBuildingChoices.
                    if (string.IsNullOrWhiteSpace(choice.Description) || !allowed.Contains(choice.StateWord))
                        failures.Add("[every-choice-speaks] " + choice.Id + " description/state is empty or outside the four words");
                    // RED: restore `Ascii(name) + " -> T" + targetTier` at the choice source.
                    if ((choice.Name ?? "").Contains("->"))
                        failures.Add("[no-arrow-labels] " + choice.Id + " carries developer arrow copy");
                    // RED: assign `AfterUpgradeText = ""` in BuildBuildingChoices.
                    if (!string.Equals(choice.StateWord, "Max", StringComparison.Ordinal) &&
                        string.IsNullOrWhiteSpace(choice.AfterUpgradeText))
                        failures.Add("[benefit-line] " + choice.Id + " has no next-tier benefit");
                }

                // RED: remove ChannelSummary.Describe's Busy == 0 branch.
                string idle = new ChannelSummary { Name = "Builders", Busy = 0, Slots = 2, Depth = 0, DepthCap = 5 }.Describe();
                if (idle.IndexOf("idle", StringComparison.OrdinalIgnoreCase) < 0)
                    failures.Add("[idle-chip-word] zero-busy channel does not say idle: " + idle);

                log.AppendLine("live choices=" + vm.BuildingChoices.Count + " idle='" + idle + "'");
            }
            finally
            {
                SetGssInstance(prior);
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
                if (fixture != null) UnityEngine.Object.DestroyImmediate(fixture);
            }
        }

        private static void CheckPanelSource(List<string> failures, StringBuilder log)
        {
            string panel = ReadSource(PanelPath, failures);
            if (panel == null) return;

            string card = Body(panel, "private void BuildBuildingCard(", "private static void BuildDisabledBuildingFace(");
            // RED: replace selected.StateWord with a colour-only state treatment.
            if (card == null || !card.Contains("selected.StateWord"))
                failures.Add("[card-paints-the-word] BuildBuildingCard does not paint selected.StateWord");

            string destination = Body(panel, "private void RenderBuildingsDestination(", "private void AddBuildingWorkspaceRow(");
            // RED: move the old Showing sentence back into RenderBuildingsDestination.
            if (destination == null || destination.Contains("Showing ") ||
                !panel.Contains("Showing \" + (first + 1)"))
                failures.Add("[no-paging-when-it-fits] Buildings pages, or the still-live Defense/Research pager vanished");

            // RED: lower TroopCtaY1 until the replayed height is under 112px.
            float y0 = Const(panel, "TroopCtaY0"), y1 = Const(panel, "TroopCtaY1"), px = Const(panel, "TroopWorkspacePx");
            if (y0 < 0f || y1 < 0f || px < 0f || (y1 - y0) * px < 112f)
                failures.Add("[touch-floor] building CTA line is below 112 reference px");

            string placement = Body(panel, "private void ApplyDrawerPlacement()", "private void SyncQueueToggleFace()");
            // RED: remove Buildings from DrawerInBandMode or remove BuildingNowPrefix from ApplyDrawerPlacement.
            if (!panel.Contains("ManageTab.Troops || _vm.Tab == ManageTab.Buildings") ||
                placement == null || !placement.Contains("BuildingNowPrefix"))
                failures.Add("[drawer-band-covers-buildings] Buildings drawer can cover the selected card");

            string renderList = Body(panel, "private void RenderList()", "private string FindSummary");
            // RED: leave the former Buildings else-if in RenderList instead of moving this exact footer.
            if (destination == null || !destination.Contains("Need another town structure?") ||
                renderList == null || renderList.Contains("Need another town structure?"))
                failures.Add("[footer-moved-not-lost] Open-build footer is missing or still in the paged path");

            string strip = Body(panel, "private void RenderStrip()", "private void RenderSlotOffer()");
            // RED: replace the two Describe() calls in RenderStrip with Name + Busy/Slots.
            if (strip == null || !strip.Contains("_vm.Channels[i].Describe()") || !strip.Contains("s.Describe()"))
                failures.Add("[idle-chip-word] the VM's idle wording is not painted in both Manage chip surfaces");

            log.AppendLine("source card/destination/touch/drawer/footer/idle-paint checks complete");
        }

        private static bool InstallState(GameStateService service, GameState state)
        {
            var stateField = typeof(GameStateService).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
            if (stateField == null) return false;
            stateField.SetValue(service, state);
            return SetGssInstance(service);
        }

        private static bool SetGssInstance(GameStateService service)
        {
            var instance = typeof(GameStateService).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            if (instance == null) return false;
            instance.SetValue(null, service);
            return true;
        }

        private static string ReadSource(string relativePath, List<string> failures)
        {
            string full = Path.Combine(Directory.GetCurrentDirectory(), relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(full)) return File.ReadAllText(full);
            failures.Add("source file missing: " + relativePath);
            return null;
        }

        private static string Body(string source, string from, string until)
        {
            int start = source.IndexOf(from, StringComparison.Ordinal);
            if (start < 0) return null;
            int end = source.IndexOf(until, start + from.Length, StringComparison.Ordinal);
            return end < 0 ? null : source.Substring(start, end - start);
        }

        private static float Const(string source, string name)
        {
            var match = Regex.Match(source, @"\b" + name + @"\s*=\s*([0-9]+(?:\.[0-9]+)?)f");
            return match.Success && float.TryParse(match.Groups[1].Value,
                System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture,
                out float value) ? value : -1f;
        }
    }
}
