using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using DeNelle.Core.UI;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class CostFormatSourceRegression
    {
        // Zero allowlist: match emission shapes so returns, assignments, interpolation,
        // and TMP .text writes are covered without naming their current owners.
        private static readonly Regex[] LetterSuffixEmitters =
        {
            new Regex("\\+\\s*\"[WIFCG]\"", RegexOptions.Compiled),
            new Regex("\\.Append\\s*\\(\\s*\"[WIFCG]\"\\s*\\)", RegexOptions.Compiled),
            new Regex("(?:\\$@|@\\$|\\$)\"[^\"\\r\\n]*\\{[^}\\r\\n]*(?i:wood|iron|food|stone|crystal|coin|gold|wisdom|magic)[^}\\r\\n]*\\}[WIFCG](?=[^A-Za-z]|\")", RegexOptions.Compiled)
        };

        private sealed class Pin
        {
            public readonly string Path, Method;
            public readonly string[] Required;
            public Pin(string path, string method, params string[] required)
            { Path = path; Method = method; Required = required; }
        }

        private static readonly Pin[] Pins =
        {
            new Pin("_Modules/Village/BuildMode/BuildPaletteUI.cs", "private static string CostLabel(", "CostFormat.Words", "CostParts"),
            new Pin("_Modules/Village/BuildMode/BuildStructureInfoPanel.cs", "private static IReadOnlyList<CostPart> CostParts(", "CostFormat.Parts"),
            new Pin("_Modules/Village/Hero/BarracksPanelVM.cs", "private static string CostStr(", "CostFormat.Words", "CostFormat.Parts"),
            new Pin("_Modules/Village/Hero/PartyShopVM.cs", "private static string CostString(", "CostFormat.Words", "CostFormat.Parts"),
            new Pin("_Modules/Village/Hero/ShopVM.cs", "private string CostString(", "CostFormat.Words", "CostFormat.Parts"),
            new Pin("_Modules/Village/Hero/TroopTrainingVM.cs", "private static string CostString(", "CostFormat.Words", "CostFormat.Parts"),
            new Pin("Editor/Regression/DataRegression.cs", "private static string CostStr(", "CostFormat.Words", "CostFormat.Parts"),
            new Pin("_Modules/Village/Buildings/NPCUpgradeStation.cs", "private string CostString(", "CostFormat.Words", "CostFormat.Parts"),
            new Pin("_Modules/Village/Buildings/Progression/BuildingUpgradeVM.cs", "private string CostString(EcoCost", "CostFormat.Words", "CostFormat.Parts"),
            new Pin("_Modules/Village/Buildings/Progression/BuildingUpgradeVM.cs", "private static string ResourceCostString(", "CostFormat.Words", "CostFormat.Parts"),
            new Pin("_Modules/Village/Items/JewelerVM.cs", "private static string CostLabel(", "CostFormat.Words", "CostFormat.Parts"),
            new Pin("_Modules/Village/UI/Manage/ManageScreenVM.cs", "public static string DescribeCost(", "CostFormat.Words", "CostFormat.Parts"),
            new Pin("_Modules/Village/Walls/WallRepairController.cs", "public static string DescribeMaterials(", "CostFormat.Words", "CostFormat.Parts")
        };

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            string assets = Application.dataPath.Replace('\\', '/');
            ScanSources(assets, failures);
            CheckPins(assets, failures);
            CheckBehavior(assets, failures);
            CheckStoneIcon(assets, failures);
            if (failures.Count != 0)
            {
                reason = "cost-format-source: " + string.Join(" | ", failures);
                return false;
            }
            reason = "COST_FORMAT_SOURCE_OK - zero suffix emitters; 13 adapters pinned; compact/zero/full-word behavior pinned; Stone sprite resolves through canonical map; no reverse parse/direct registry";
            return true;
        }

        private static void ScanSources(string assets, List<string> failures)
        {
            foreach (string path in Directory.GetFiles(Path.Combine(assets, "_Modules"), "*.cs", SearchOption.AllDirectories))
                ScanOne(path, assets, failures);
            ScanOne(Path.Combine(assets, "Editor/Regression/DataRegression.cs"), assets, failures);
        }

        private static void ScanOne(string path, string assets, List<string> failures)
        {
            string source = StripComments(File.ReadAllText(path));
            foreach (Regex regex in LetterSuffixEmitters)
            {
                Match match = regex.Match(source);
                if (match.Success)
                    failures.Add(Rel(path, assets) + " emits a single-letter resource suffix: " + match.Value);
            }
        }

        private static void CheckPins(string assets, List<string> failures)
        {
            if (Pins.Length != 13) failures.Add("ticket-owned adapter pin count is not 13");
            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Pin pin in Pins)
            {
                string path = Path.Combine(assets, pin.Path.Replace('/', Path.DirectorySeparatorChar));
                string body = MethodBody(File.ReadAllText(path), pin.Method);
                if (body == null) { failures.Add(pin.Path + " lost " + pin.Method); continue; }
                files.Add(path);
                foreach (string required in pin.Required)
                    if (!body.Contains(required)) failures.Add(pin.Path + " / " + pin.Method + " bypasses " + required);
            }
            foreach (string path in files)
            {
                string source = StripComments(File.ReadAllText(path));
                if (source.Contains("CurrencyIconFor(") || source.Contains("LeadingNumber(") ||
                    source.Contains("Split('\u00b7')") || source.Contains("RpgUiCatalog.Get(\"currency\""))
                    failures.Add(Rel(path, assets) + " reverse-parses display text or owns a direct currency registry");
            }
        }

        private static void CheckBehavior(string assets, List<string> failures)
        {
            const int amount = 12345;
            var parts = CostFormat.Parts(new[] { ("wood", "Wood", amount), ("iron", "Iron", 0), ("stone", "Stone", -7) });
            if (parts.Count != 1 || parts[0].ConceptId != "wood" || parts[0].Amount != amount)
                failures.Add("Parts does not preserve positives and drop zero/negative amounts");
            else if (parts[0].AmountText != ElarionUi.CompactNumber(amount))
                failures.Add("Parts bypasses the shared compact formatter");
            else if (CostFormat.Words(parts) != "Wood " + ElarionUi.CompactNumber(amount))
                failures.Add("Words lost its full-word fallback");
            if (CostFormat.Words(CostFormat.Parts(new[] { ("wood", "Wood", 0) })) != string.Empty)
                failures.Add("zero-only costs do not format empty");

            string formatter = File.ReadAllText(Path.Combine(assets, "_Modules/Core/UI/CostFormat.cs"));
            if (!formatter.Contains("UiStyle.Icon(part.ConceptId)") ||
                !formatter.Contains("part.Word + \" \" + part.AmountText"))
                failures.Add("renderers lost UiStyle lookup or full-word fallback");
        }

        private static void CheckStoneIcon(string assets, List<string> failures)
        {
            const string spritePath = "Assets/Resources/RpgUi/currency/currency_stone.png";
            var importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
            if (importer == null)
                failures.Add("Stone currency art has no TextureImporter");
            else
            {
                if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
                    failures.Add("Stone currency art is not imported as one Sprite");
                if (!importer.alphaIsTransparency || importer.mipmapEnabled)
                    failures.Add("Stone currency Sprite lost transparent-alpha or no-mipmap UI import settings");
                if (importer.wrapMode != TextureWrapMode.Clamp || importer.npotScale != TextureImporterNPOTScale.None)
                    failures.Add("Stone currency Sprite lost clamp or no-NPOT-scaling UI import settings");
                if (importer.maxTextureSize > 512)
                    failures.Add("Stone currency Sprite exceeds the 512px shipped UI texture ceiling");
                foreach (string platform in new[] { "DefaultTexturePlatform", "Standalone", "Android", "WebGL" })
                {
                    TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
                    if (settings.maxTextureSize > 512)
                        failures.Add("Stone currency Sprite exceeds 512px on " + platform);
                }
            }

            string resourcesMap = Path.Combine(assets, "Resources/Data/Canonical/concept-icons.json");
            string streamingMap = Path.Combine(assets, "StreamingAssets/Data/Canonical/concept-icons.json");
            string resourcesJson = File.ReadAllText(resourcesMap);
            string streamingJson = File.ReadAllText(streamingMap);
            if (!string.Equals(resourcesJson, streamingJson, StringComparison.Ordinal))
                failures.Add("concept-icons canonical mirrors differ");
            else
            {
                var map = JObject.Parse(resourcesJson)["map"];
                foreach (string concept in new[] { "stone", "stones" })
                {
                    if ((string)map?[concept]?["role"] != "currency" ||
                        (string)map?[concept]?["name"] != "currency_stone")
                        failures.Add(concept + " does not map to currency/currency_stone");
                }
            }

            RpgUiCatalog.ClearCache();
            ConceptIconResolver.ClearCache();
            Sprite singular = ConceptIconResolver.Resolve("stone");
            Sprite plural = ConceptIconResolver.Resolve("stones");
            Sprite styled = UiStyle.Icon("stone");
            if (singular == null || plural == null || styled == null)
                failures.Add("Stone currency Sprite does not resolve through ConceptIconResolver and UiStyle");
            else if (singular.name != "currency_stone" || plural != singular || styled != singular)
                failures.Add("Stone aliases do not resolve the one currency_stone Sprite authority");
        }

        private static string MethodBody(string source, string marker)
        {
            int start = source.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0) return null;
            int open = source.IndexOf('{', start);
            int arrow = source.IndexOf("=>", start, StringComparison.Ordinal);
            if (arrow >= 0 && (open < 0 || arrow < open))
            {
                int semi = source.IndexOf(';', arrow);
                return semi < 0 ? null : source.Substring(start, semi - start + 1);
            }
            if (open < 0) return null;
            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}' && --depth == 0) return source.Substring(start, i - start + 1);
            }
            return null;
        }

        // Comments are blanked before the zero-allowlist scan so examples cannot mask regressions.
        private static string StripComments(string source)
        {
            return Regex.Replace(source, @"//[^\r\n]*|/\*[\s\S]*?\*/", m => new string(' ', m.Length));
        }

        private static string Rel(string path, string assets) => "Assets" + path.Replace('\\', '/').Substring(assets.Length);
    }
}
