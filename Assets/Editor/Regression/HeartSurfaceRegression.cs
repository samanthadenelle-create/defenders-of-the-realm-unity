// =============================================================================
// HeartSurfaceRegression [heart-surface] -- WO-2003 / WO-2017.
// -----------------------------------------------------------------------------
// THE HEART HAS A DOOR, AND THE PLAYER READS ONE NAME FOR IT.
//
// WHY THIS SUITE EXISTS (the class of bug, not one instance). Owner 2026-09-06:
// "wire the heart" -- she could not find how to raise her realm tier, and it gates
// nearly everything. MEASURED at source that day: VillageTierService.TryUpgrade is
// the sole writer; its only caller was BuildingUpgradeVM.Select(VillageTierRowId);
// that was reachable only from the VillageGated action band in
// BuildingUpgradePanelMvvm, painted only while the player happened to be looking at
// a building whose NEXT tier was gated. 394 suites were green while the control
// that gates most of the game had no route of its own -- because every oracle asked
// "does this system work", none asked "can a player get here at all".
// That is CLI_DRIVING_PLAN section 1's seam family, and this is its Heart member.
//
// It also pins the SECOND half of the same defect: the gate was spelled FOUR ways
// on screen ("UNLOCKS AT VILLAGE LEVEL n", "Locked - needs Village Tier n",
// "Requires Village Tier n", "Raise Village Tier", plus the rail's "T2"). Canon
// section 6 / owner ruling 11: the player-facing name is HEART LEVEL, one spelling.
// The STORED field stays GameState.VillageTier and the service keeps its type name
// -- save keys and type names are contracts, display words are not -- so this suite
// deliberately checks STRING LITERALS, never identifiers.
//
// Marker: HEART_SURFACE_OK / HEART_SURFACE_FAIL. Expected: GREEN.
//
// REVERT RECIPE (RED), any ONE of these:
//   * delete the `BuildHeartFace();` call in ManageScreenPanel.BuildTabs
//     -> [heart-has-a-door] fires: the screen loses its direct route.
//   * restore `case UpgradeActionState.VillageGated: return "Raise Village Tier";`
//     at BuildingUpgradePanelMvvm.cs:425 -> [one-name-for-the-gate] fires.
//   * make HeartProgression.UnlocksAt return an empty list
//     -> [heart-level-opens-something] fires for levels 1..MaxTier.
//
// Wire (DataRegression.RunAll):
//   DeNelle.Core.Diagnostics.Guard.Try("Regression", "heart-surface suite", () => { if (!DeNelle.Editor.Regression.HeartSurfaceRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[heart-surface] " + r); });
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DeNelle.Village.Buildings.Progression;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class HeartSurfaceRegression
    {
        private const string PanelPath = "Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs";
        private const string VmPath = "Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs";
        private const string HeartPanelPath = "Assets/_Modules/Village/UI/Manage/HeartPanel.cs";
        private const string HeartBootPath = "Assets/_Modules/Village/UI/Manage/HeartPanelBootstrap.cs";
        private const string UpgradePanelPath = "Assets/_Modules/Village/Buildings/Progression/BuildingUpgradePanelMvvm.cs";
        private const string PerkServicePath = "Assets/_Modules/Village/Buildings/Progression/BuildingPerkService.cs";
        private const string HeartArtPath = "Assets/Resources/Portraits/Buildings/heart.png";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder("=== HeartSurfaceRegression (WO-2003 / WO-2017) ===\n");
            try
            {
                CheckHeartHasADoor(failures, log);
                CheckOneNameForTheGate(failures, log);
                CheckHeartLevelOpensSomething(failures, log);
                CheckHeartArtIsPresent(failures, log);
            }
            catch (Exception ex)
            {
                failures.Add("suite threw " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "HEART_SURFACE_OK the Heart has a registered panel, a direct route from Manage, "
                       + "one player-facing name (Heart Level), and a data-derived unlock preview for "
                       + "every level up to VillageTierService.MaxTier=" + VillageTierService.MaxTier;
                Debug.Log(reason + "\n" + log);
                return true;
            }
            reason = "HEART_SURFACE_FAIL: " + string.Join("; ", failures);
            Debug.LogError(reason + "\n" + log);
            return false;
        }

        // -- CASE 1: the Heart is REACHABLE. A control with no door is the whole defect. --------
        private static void CheckHeartHasADoor(List<string> failures, StringBuilder log)
        {
            string panel = ReadSource(PanelPath, failures);
            string heartPanel = ReadSource(HeartPanelPath, failures);
            string boot = ReadSource(HeartBootPath, failures);
            string vm = ReadSource(VmPath, failures);

            // The panel exists and REGISTERS itself, both overloads, like every other routed panel.
            if (heartPanel != null &&
                (!heartPanel.Contains("PanelRouter.Register(PanelId.Heart, (Action)Open)") ||
                 !heartPanel.Contains("PanelManager.NotifyOpened(_panelHandle)")))
                failures.Add("[heart-has-a-door] HeartPanel does not register PanelId.Heart or never calls " +
                             "PanelManager.NotifyOpened - PanelRouter's WO-465 visibility verify would report " +
                             "every open as FAILED");

            // ...and something SPAWNS it. BarracksPanel proved a registered panel with no spawner
            // is a dead system (OWNER_RULINGS_LOCKED section 21).
            if (boot != null &&
                (!boot.Contains("RuntimeInitializeOnLoadMethod") || !boot.Contains("AddComponent<HeartPanel>()")))
                failures.Add("[heart-has-a-door] HeartPanelBootstrap does not scene-independently spawn HeartPanel - " +
                             "the route registers nowhere and PanelId.Heart opens nothing");

            // ...and Manage carries the ALWAYS-PRESENT face. This is the route the owner could not find.
            if (panel != null &&
                (!panel.Contains("BuildHeartFace();") ||
                 !panel.Contains("PanelRouter.Open(PanelId.Heart)") ||
                 !panel.Contains("ManageHeartFace")))
                failures.Add("[heart-has-a-door] the Manage header has no HEART face into PanelId.Heart. The gate " +
                             "that gates nearly all content is back to having no direct route - the exact defect " +
                             "the owner reported on 2026-09-06");

            // ...and the gated CTAs that SAY "UPGRADE THE HEART" open the Heart, not another screen.
            if (vm != null &&
                (!vm.Contains("OpenHeartPanel(rowId)") || !vm.Contains("OpenHeartPanel(subject)") ||
                 !vm.Contains("PanelRouter.Open(PanelId.Heart, subject)")))
                failures.Add("[heart-has-a-door] a Manage 'UPGRADE THE HEART' face does not open the Heart surface. " +
                             "A door must open the thing its own face names");

            log.AppendLine("door: HeartPanel registered + bootstrapped, Manage HEART face present, gated CTAs re-pointed");
        }

        // -- CASE 2: ONE player-facing name. Four spellings is how a gate becomes unfindable. ----
        private static void CheckOneNameForTheGate(List<string> failures, StringBuilder log)
        {
            // ⛔ STRING LITERALS ONLY. Identifiers (VillageTierService, RequiresVillageTier,
            // GameState.VillageTier, the "villageTier" SAVE KEY) are contracts and must NOT be
            // renamed - canon section 6 explicitly permits the internal field to keep its name.
            var retired = new (string path, string literal)[]
            {
                (UpgradePanelPath, "return \"Raise Village Tier\""),
                (PerkServicePath,  "\"Locked - needs Village Tier \""),
                (VmPath,           "\"Needs Village Tier \""),
                // ⚠ ManageScreenPanel is DELIBERATELY NOT scanned for "UNLOCKS AT VILLAGE LEVEL":
                // that literal survives inside the WO-1423 comment that explains why the disabled
                // face was retired, and a scan here would fail on the explanation rather than on
                // the defect. ManageBuildingsCardRegression already pins the live shape
                // (BuildDisabledBuildingFace must not return to the Locked branch).
            };
            for (int i = 0; i < retired.Length; i++)
            {
                string src = ReadSource(retired[i].path, failures);
                if (src == null) continue;
                if (src.Contains(retired[i].literal))
                    failures.Add("[one-name-for-the-gate] " + retired[i].path + " still shows the player " +
                                 retired[i].literal + ". Canon section 6 / owner ruling 11: the player-facing name " +
                                 "is HEART LEVEL, and the reason it is ONE name is that four spellings of one gate " +
                                 "is what made it unfindable");
            }

            // ...and the replacement is actually there, so this case cannot pass by deletion.
            string upgradePanel = ReadSource(UpgradePanelPath, failures);
            if (upgradePanel != null && !upgradePanel.Contains("\"Raise Heart Level to\""))
                failures.Add("[one-name-for-the-gate] the VillageGated action band no longer names HEART LEVEL - " +
                             "the retired literal is gone but nothing replaced it, so the band says nothing");
            string perkService = ReadSource(PerkServicePath, failures);
            if (perkService != null && !perkService.Contains("\"Locked - needs Heart Level \""))
                failures.Add("[one-name-for-the-gate] the research refusal no longer names HEART LEVEL");
            string vmSrc = ReadSource(VmPath, failures);
            if (vmSrc != null &&
                (!vmSrc.Contains("\"Needs Heart Level \"") || !vmSrc.Contains("\" . Heart \"")))
                failures.Add("[one-name-for-the-gate] the locked building card's sentence or its terse rail " +
                             "sub-line no longer names HEART LEVEL (the rail's old 'T2' was the fourth spelling)");

            log.AppendLine("copy: 4 retired village-tier spellings absent, Heart Level replacements present");
        }

        // -- CASE 3: the unlock preview is DERIVED and NON-EMPTY for every buyable level. --------
        private static void CheckHeartLevelOpensSomething(List<string> failures, StringBuilder log)
        {
            int max = VillageTierService.MaxTier;
            for (int level = 1; level <= max; level++)
            {
                var unlocks = HeartProgression.UnlocksAt(level);
                if (unlocks == null || unlocks.Count == 0)
                {
                    // ⚠ The price is DELIBERATELY not restated here. It is a balance constant the
                    // owner rules on (VillageTierService.NextCost) and a copy of it in a suite is the
                    // same duplicated-state species CLAUDE.md §2/§16 spend paragraphs on.
                    failures.Add("[heart-level-opens-something] Heart Level " + level + " opens NOTHING that " +
                                 "building-tiers.json authors. Either the level is a pure crystal sink the player " +
                                 "pays for and receives nothing visible, or HeartProgression.UnlocksAt stopped " +
                                 "reading requiresVillageTier");
                    continue;
                }
                log.AppendLine("  Heart Level " + level + " opens " + unlocks.Count + " authored rows (first: "
                               + unlocks[0].Text + ")");
            }

            // The preview must never be typed. A hardcoded list would survive a data change and lie.
            string heartPanel = ReadSource(HeartPanelPath, failures);
            if (heartPanel != null && !heartPanel.Contains("HeartProgression.UnlocksAt(next)"))
                failures.Add("[heart-level-opens-something] the Heart panel does not ask the model for its unlock " +
                             "preview. A view that composes its own list will keep showing yesterday's content");

            log.AppendLine("unlocks: every level 1.." + max + " opens at least one authored row, derived not typed");
        }

        // -- CASE 4: the art the surface binds actually exists on disk. --------------------------
        private static void CheckHeartArtIsPresent(List<string> failures, StringBuilder log)
        {
            string full = Path.Combine(Directory.GetCurrentDirectory(),
                HeartArtPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full)) failures.Add("[heart-art] missing " + HeartArtPath);
            if (!File.Exists(full + ".meta")) failures.Add("[heart-art] missing " + HeartArtPath + ".meta");
            else
            {
                // textureType: 8 == Sprite. A Texture2D import still renders through the kit's
                // fallback, but it costs a runtime Sprite.Create on every cold load.
                string meta = File.ReadAllText(full + ".meta");
                if (!meta.Contains("textureType: 8"))
                    failures.Add("[heart-art] " + HeartArtPath + " is not imported as a Sprite (textureType: 8)");
            }
            log.AppendLine("art: " + HeartArtPath + " present and Sprite-imported");
        }

        private static string ReadSource(string path, List<string> failures)
        {
            string full = Path.Combine(Directory.GetCurrentDirectory(),
                path.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(full)) return File.ReadAllText(full);
            failures.Add("[heart-surface] source not found: " + path + " - FAIL, not a skip");
            return null;
        }
    }
}
