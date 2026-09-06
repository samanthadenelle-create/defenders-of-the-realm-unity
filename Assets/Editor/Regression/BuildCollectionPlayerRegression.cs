#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
            var craftingIds = build.Single(c => c.CollectionId == "build-crafting").Items
                .OrderBy(i => i.Order).Select(i => i.ItemId).ToArray();
            var tradeIds = build.Single(c => c.CollectionId == "build-trade").Items
                .OrderBy(i => i.Order).Select(i => i.ItemId).ToArray();
            if (!new[] { "workshop", "jeweler" }.SequenceEqual(craftingIds))
                return Fail("Crafting membership/order must be workshop,jeweler only", out reason);
            if (!new[] { "market", "forge", "armorer" }.SequenceEqual(tradeIds))
                return Fail("Trade membership/order must be market,forge,armorer only", out reason);
            string browser = File.ReadAllText("Assets/_Modules/Village/BuildMode/BuildCollectionBrowser.cs");
            string palette = File.ReadAllText("Assets/_Modules/Village/BuildMode/BuildPaletteUI.cs");
            if (!browser.Contains("BuildCollectionBrowser : ObsidianNavigationWorkspace<BuildCollectionPage>") ||
                !browser.Contains("public override void Close()") ||
                !browser.Contains("base.Close();") ||
                !browser.Contains("Done(BuildFirstUseGuide.ItemSelected") || !browser.Contains("callback?.Invoke(entry)"))
                return Fail("shared workspace/Place release contract missing", out reason);
            if (!browser.Contains("\"Locked\"") || !browser.Contains("COST: ") || !browser.Contains("vm?.Description"))
                return Fail("readable card/state contract missing", out reason);
            // -- WO-1417 [kit-card] ----------------------------------------------
            // The palette item card is a KIT SURFACE, and its two copy lines are
            // player English. Three pins, each RED against the pre-WO-1417 file:
            //  (a) the card face is the kit's obsidian plate, not a bespoke literal
            //      colour, and it carries the same gold perimeter the sibling
            //      category card uses. The Outline that carried locked-vs-unlocked
            //      by HUE ALONE must stay gone (the owner is red/green colourblind).
            //  (b) the basket runs through the ONE shared cost formatter, so this
            //      surface cannot re-grow a private second wording for a price.
            //  (c) NO STRING LITERAL in the browser contains a '[' bracket glyph or
            //      the retired "NO COST". Scanned over QUOTED LITERALS ONLY -- the
            //      source is full of attribute/indexer brackets, so a whole-file
            //      Contains("[") would pin nothing and fail always.
            if (!browser.Contains("Box(\"BuildCard_\" + itemId, slot.transform, ElarionUiKit.ObsidianFill)") ||
                !browser.Contains("AddGoldPerimeter(card.transform)") ||
                browser.Contains("AddComponent<Outline>(); outline.effectColor = locked"))
                return Fail("palette item card is not the kit obsidian plate + gold bezel, or state is carried by an outline hue", out reason);
            if (!browser.Contains("CostFormat.Words(CostParts(vm.EffectiveCost))") ||
                !browser.Contains("CostFormat.Parts(new[]"))
                return Fail("palette card cost line no longer runs through the one shared cost formatter", out reason);
            foreach (string literal in StringLiterals(browser))
            {
                if (literal.Contains("["))
                    return Fail("palette string carries a bracket glyph: " + literal, out reason);
                if (literal.Contains("NO COST"))
                    return Fail("palette string carries the retired literal NO COST: " + literal, out reason);
            }
            if (!browser.Contains("BuildCardSlot_") ||
                !browser.Contains("Box(\"BuildCard_\" + itemId, slot.transform") ||
                !browser.Contains("ButtonBox(slot.transform, buttonFace") ||
                !browser.Contains("card.anchorMin = new Vector2(0f, .18f)"))
                return Fail("shared collection action is no longer a footer below the card; mobile copy can be covered again", out reason);
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
            if (!browser.Contains("upgradeCard.name = \"DefenseUpgradeCard\"") ||
                !browser.Contains("\"Upgrade Defenses\"") ||
                !browser.Contains("PanelRouter.Open(PanelId.Manage, \"Defense\")"))
                return Fail("separate Upgrade Defenses card no longer opens the placed-defense upgrade destination", out reason);
            string managePanel = File.ReadAllText("Assets/_Modules/Village/UI/Manage/ManageScreenPanel.cs");
            string manageVm = File.ReadAllText("Assets/_Modules/Village/UI/Manage/ManageScreenVM.cs");
            if (!managePanel.Contains("UPGRADABLE TOWERS - affordable first") ||
                !managePanel.Contains("\"Build defense\", OpenDefenseBuilder") ||
                !managePanel.Contains("controller?.EnterBuildMode(DeNelle.Core.Catalog.BuildType.Defense)") ||
                !managePanel.Contains("Action<string>") ||
                !manageVm.Contains("PlacedStructureUpgradeService.MaxLevelFor") ||
                !manageVm.Contains("UpgradeCostFor(entry, level)") ||
                !manageVm.Contains("grid \" + placed.cellX"))
                return Fail("Defense upgrade screen lost identity/location, authority cost, empty state, or secondary build route", out reason);
            reason = "BUILD_COLLECTION_PLAYER_OK: 7 build categories plus separate Upgrade Defenses card, approved icons, intentional missing-art fallback, locked entries hidden until authoritative unlock, Stone Gate presentation-gated, shared Obsidian workspace, readable cards, Defense 4+1, pause released before exact Arm seam; WO-1417 kit-card: item card is the kit obsidian plate + gold bezel, cost through the one shared formatter, no bracket glyph and no NO COST in any palette string literal";
            return true;
        }

        private static bool Fail(string value, out string reason) { reason = "BUILD_COLLECTION_PLAYER_FAIL: " + value; return false; }

        /// <summary>WO-1417: every double-quoted string literal in a C# source, with comments,
        /// char literals and escapes excluded. A single-state character walk rather than a regex,
        /// because the cheap regex both mis-reads an escaped quote and cannot tell a '[' in an
        /// attribute or an indexer from a '[' the player will read on a card.</summary>
        private static List<string> StringLiterals(string source)
        {
            var found = new List<string>();
            var buffer = new StringBuilder();
            int i = 0, n = source == null ? 0 : source.Length;
            while (i < n)
            {
                char c = source[i];
                if (c == '/' && i + 1 < n && source[i + 1] == '/')
                {
                    while (i < n && source[i] != '\n') i++;
                    continue;
                }
                if (c == '/' && i + 1 < n && source[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < n && !(source[i] == '*' && source[i + 1] == '/')) i++;
                    i += 2;
                    continue;
                }
                if (c == '\'')
                {
                    i++;
                    while (i < n && source[i] != '\'') i += source[i] == '\\' ? 2 : 1;
                    i++;
                    continue;
                }
                if (c == '@' && i + 1 < n && source[i + 1] == '"')
                {
                    i += 2; buffer.Length = 0;
                    while (i < n)
                    {
                        if (source[i] == '"' && i + 1 < n && source[i + 1] == '"') { buffer.Append('"'); i += 2; continue; }
                        if (source[i] == '"') break;
                        buffer.Append(source[i]); i++;
                    }
                    i++; found.Add(buffer.ToString());
                    continue;
                }
                if (c == '"')
                {
                    i++; buffer.Length = 0;
                    while (i < n && source[i] != '"')
                    {
                        if (source[i] == '\\' && i + 1 < n) { buffer.Append(source[i + 1]); i += 2; continue; }
                        buffer.Append(source[i]); i++;
                    }
                    i++; found.Add(buffer.ToString());
                    continue;
                }
                i++;
            }
            return found;
        }
    }
}
#endif
