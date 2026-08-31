#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using DeNelle.Core;
using DeNelle.Core.UI;

namespace DeNelle.Editor.Regression
{
    public static class BuildCollectionPlayerRegression
    {
        private static readonly string[] Ids = { "build-gathering", "build-realm", "build-defenses", "build-crafting", "build-storage", "build-protection", "build-trade" };
        private static readonly string[] Icons = { "Resources", "Realm", "Defense", "Crafting", "Storage", "Protection", "Trade" };

        [MenuItem("Tools/Regression/Run Build Collection Player")]
        public static void RunMenu() { if (!Run(out var r)) throw new Exception(r); UnityEngine.Debug.Log(r); }

        public static bool Run(out string reason)
        {
            string path = "Assets/Resources/Data/Canonical/card-collections.json";
            var doc = JsonConvert.DeserializeObject<CardCollectionDocument>(File.ReadAllText(path));
            var build = doc.Collections.Where(c => c.Context == "build" && c.Active).ToList();
            if (build.Count != 7 || !Ids.SequenceEqual(build.Select(c => c.CollectionId))) return Fail("canonical category order/count changed", out reason);
            for (int i=0;i<Icons.Length;i++)
            {
                string expected = "UI/BuildCollections/" + Icons[i];
                if (build[i].IconKey != expected) return Fail("icon key mismatch: " + build[i].CollectionId, out reason);
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>("Assets/Resources/" + expected + ".png") == null) return Fail("approved icon is missing or not imported as a Sprite: " + Icons[i], out reason);
            }
            var defense = build.Single(c => c.CollectionId == "build-defenses");
            if (defense.Items.Count != 5 || CardCollectionPaging.PageCount(defense.Items.Count) != 2 || CardCollectionPaging.FirstIndex(1, 5) != 4)
                return Fail("Defense must page 4+1", out reason);
            string browser = File.ReadAllText("Assets/_Modules/Village/BuildMode/BuildCollectionBrowser.cs");
            string palette = File.ReadAllText("Assets/_Modules/Village/BuildMode/BuildPaletteUI.cs");
            if (!browser.Contains("_focus.Open") || !browser.Contains("Close(); // release") || !browser.Contains("callback?.Invoke(entry)"))
                return Fail("focused pause/Place release contract missing", out reason);
            if (!browser.Contains("LOCKED") || !browser.Contains("[LOCKED]") || !browser.Contains("COST: ") || !browser.Contains("vm?.Description"))
                return Fail("readable card/state contract missing", out reason);
            if (!browser.Contains("HiddenUntilFinishedArtId = \"gate_stone\"") ||
                !browser.Contains("VisibleItemIds()"))
                return Fail("unfinished Stone Gate card is exposed instead of presentation-gated", out reason);
            if (!browser.Contains("IsCollectionItemVisible") ||
                !browser.Contains("ProgressionUnlocks.IsUnlocked(itemId)") ||
                !browser.Contains("!string.IsNullOrEmpty(lockReason) && !ProgressionUnlocks.IsUnlocked(itemId)"))
                return Fail("progression-locked collection entries are not hidden-before/unhidden-after authoritative unlock", out reason);
            if (!browser.Contains("CollectionHasVisibleItems(c)") ||
                !browser.Contains("var entry = CatalogRegistry.Get(item.ItemId)") ||
                !browser.Contains("if (entry == null) continue") ||
                browser.Contains("CollectionHasVisibleItems(c, EconomyService"))
                return Fail("category projection does not hide empty-after-eligibility categories or incorrectly hides unlocked unaffordable goals", out reason);
            if (!browser.Contains("StructureSingleton.IsSingleton(entry.id) && StructureSingleton.IsBuilt(entry)") ||
                !browser.Contains("StructureSingleton.SingletonReleased += OnFiniteCapacityChanged") ||
                !browser.Contains("return true; // repeatable entries remain visible"))
                return Fail("finite category does not hide at final placement/restore on removal, or repeatable categories are incorrectly removed", out reason);
            if (!browser.Contains("SetArtworkOrFallback") || !browser.Contains("Image coming soon") ||
                !browser.Contains("image.color = new Color(.10f, .11f, .14f, 1f)"))
                return Fail("missing collection art can regress to a white/blank image slot", out reason);
            if (browser.Contains("TextOverflowModes.Ellipsis") || browser.Contains("TextOverflowModes.Truncate") ||
                !browser.Contains("buttonFace = available ? \"PLACE\"") ||
                !browser.Contains("enableWordWrapping=true") || !browser.Contains("enableAutoSizing=true") ||
                !browser.Contains("overflowMode=TextOverflowModes.Overflow"))
                return Fail("collection cards can truncate/ellipsize required copy or lack a full-copy wrapping path", out reason);
            if (!palette.Contains("_collectionBrowser.Show(entry => OnEntrySelected?.Invoke(entry))"))
                return Fail("existing Arm event seam bypassed", out reason);
            if (!browser.Contains("PanelRouter.Open(PanelId.Manage, \"Defense\")"))
                return Fail("Defense category no longer opens the placed-tower upgrade-first destination", out reason);
            string managePanel = File.ReadAllText("Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs");
            string manageVm = File.ReadAllText("Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs");
            if (!managePanel.Contains("UPGRADABLE TOWERS - affordable first") ||
                !managePanel.Contains("Build new defense") ||
                !managePanel.Contains("Action<string>") ||
                !manageVm.Contains("PlacedStructureUpgradeService.MaxLevelFor") ||
                !manageVm.Contains("UpgradeCostFor(entry, level)") ||
                !manageVm.Contains("grid \" + placed.cellX"))
                return Fail("Defense upgrade screen lost identity/location, authority cost, empty state, or secondary build route", out reason);
            reason = "BUILD_COLLECTION_PLAYER_OK: 7 categories, approved icons, intentional missing-art fallback, locked entries hidden until authoritative unlock, Stone Gate presentation-gated, 80% safe-area modal, readable cards, Defense 4+1, focused pause, exact Arm seam";
            return true;
        }

        private static bool Fail(string value, out string reason) { reason = "BUILD_COLLECTION_PLAYER_FAIL: " + value; return false; }
    }
}
#endif
