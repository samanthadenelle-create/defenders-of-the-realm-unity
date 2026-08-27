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
            CheckCurrencyConceptIcons(assets, failures);
            if (failures.Count != 0)
            {
                reason = "cost-format-source: " + string.Join(" | ", failures);
                return false;
            }
            reason = "COST_FORMAT_SOURCE_OK - zero suffix emitters; 13 adapters pinned; compact/zero/full-word behavior pinned; Stone sprite resolves through canonical map; " + CurrencyKindCount + " CurrencyKinds resolve owner-ruled art via ConceptIdFor (Food->stone); no reverse parse/direct registry";
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

        // =====================================================================
        //  WO-1195 - the owner's four art rulings, and the one translator that
        //  makes CurrencyKind.Food render STONE.
        // ---------------------------------------------------------------------
        //  ⭐ WHY THIS IS AN ORACLE AND NOT A LOOK: WO-1205 dropped the NAME label
        //  from the town resource rows on the ruled path, so the icon is now the
        //  ONLY identity a row carries. The owner is red/green colourblind. A
        //  concept row that goes missing, or an enum name lower-cased into a
        //  concept id, no longer produces a slightly-wrong picture - it produces a
        //  resource the player cannot name. That has to be a failing build.
        //
        //  ⛔ EVERY BRANCH BELOW ADDS A FAILURE. There is no "art not present, skip"
        //  path: a missing sprite IS the defect this check exists for, so an early
        //  return on absence would land it in the green column.
        // =====================================================================

        /// <summary>Number of <see cref="ElarionUiKit.CurrencyKind"/> members, read off the enum so
        /// adding a currency cannot silently escape the sweep.</summary>
        private static int CurrencyKindCount => Enum.GetValues(typeof(ElarionUiKit.CurrencyKind)).Length;

        private static void CheckCurrencyConceptIcons(string assets, List<string> failures)
        {
            // ── 1. The enum-name-is-not-a-concept-id rule, asserted at SOURCE ──────────
            // Before WO-1195 the kit resolved `UiStyle.Icon(kind.ToString().ToLowerInvariant())`,
            // which asked concept-icons.json for "food" on the slot the town rail labels "Stone".
            // Pin the shape so nobody reintroduces it while ConceptIdFor still exists.
            string kitPath = Path.Combine(assets, "_Modules/Core/UI/ElarionUiKitObsidian.cs");
            string kit = StripComments(File.ReadAllText(kitPath));
            if (kit.Contains("UiStyle.Icon(kind.ToString()"))
                failures.Add("ElarionUiKitObsidian resolves a concept id from the CurrencyKind NAME "
                             + "(CurrencyKind.Food is the Stone slot - canon sec.7); use ConceptIdFor");
            if (!kit.Contains("public static string ConceptIdFor(CurrencyKind kind)"))
                failures.Add("ElarionUiKit.ConceptIdFor is gone - the CurrencyKind->concept translator");

            // ── 2. No SECOND currency registry anywhere under _Modules ────────────────
            // The kit's own `RpgUiCatalog.Get("currency", "currency_" + conceptId)` is the ONE
            // sanctioned mirrored-art fallback and is keyed off ConceptIdFor's output, so it is
            // exempt by path. Every other file naming that role folder is a rival registry.
            foreach (string path in Directory.GetFiles(Path.Combine(assets, "_Modules"), "*.cs", SearchOption.AllDirectories))
            {
                if (string.Equals(Path.GetFullPath(path), Path.GetFullPath(kitPath), StringComparison.OrdinalIgnoreCase))
                    continue;
                string src = StripComments(File.ReadAllText(path));
                if (src.Contains("RpgUiCatalog.Get(\"currency\"") || src.Contains("RpgUiCatalog.Get(CurrencyRole"))
                    failures.Add(Rel(path, assets) + " owns a second currency icon registry "
                                 + "(resolve through ElarionUiKit.ConceptIdFor + UiStyle.Icon)");
            }

            // ── 3. The owner's rulings, as data ───────────────────────────────────────
            // 2026-08-26 contact-sheet choices: food -> the distinct HudIcons art, magic -> the
            // Arcanist emblem, wisdom -> the spellbook tome, stone KEPT. Each is one row.
            string resourcesMap = Path.Combine(assets, "Resources/Data/Canonical/concept-icons.json");
            string streamingMap = Path.Combine(assets, "StreamingAssets/Data/Canonical/concept-icons.json");
            if (!File.Exists(resourcesMap) || !File.Exists(streamingMap))
            {
                failures.Add("concept-icons.json is missing from Resources and/or StreamingAssets");
                return;   // nothing further is measurable, and the miss is already RED
            }
            string resourcesJson = File.ReadAllText(resourcesMap);
            if (!string.Equals(resourcesJson, File.ReadAllText(streamingMap), StringComparison.Ordinal))
                failures.Add("concept-icons canonical mirrors differ (WO-1195 requires them byte-identical)");
            JToken map = JObject.Parse(resourcesJson)["map"];

            RpgUiCatalog.ClearCache();
            ConceptIconResolver.ClearCache();

            var seenSprites = new HashSet<string>(StringComparer.Ordinal);
            foreach (ElarionUiKit.CurrencyKind kind in Enum.GetValues(typeof(ElarionUiKit.CurrencyKind)))
            {
                string concept = ElarionUiKit.ConceptIdFor(kind);
                if (string.IsNullOrEmpty(concept) ||
                    string.Equals(concept, kind.ToString(), StringComparison.Ordinal))
                {
                    failures.Add(kind + " has no concept id (ConceptIdFor returned the raw enum name)");
                    continue;
                }
                // ⛔ The one hardcoded expectation in this file, and it is deliberate: canon sec.7
                // retired Food for Stone. If ConceptIdFor ever answers "food" here the town rail's
                // Stone row silently becomes a tractor again.
                if (kind == ElarionUiKit.CurrencyKind.Food && concept != "stone")
                    failures.Add("CurrencyKind.Food must map to the concept 'stone' (canon sec.7), not '" + concept + "'");

                string expected = "currency_" + concept;
                if ((string)map?[concept]?["role"] != "currency" || (string)map?[concept]?["name"] != expected)
                {
                    failures.Add("concept-icons.json has no currency/" + expected + " row for '" + concept + "' (" + kind + ")");
                    continue;
                }

                Sprite viaConcept = ConceptIconResolver.Resolve(concept);
                Sprite viaStyle = UiStyle.Icon(concept);
                if (viaConcept == null || viaStyle == null)
                {
                    failures.Add(kind + " concept '" + concept + "' resolves NO sprite - the chip would ship as a word");
                    continue;
                }
                if (viaStyle != viaConcept || viaConcept.name != expected)
                    failures.Add(kind + " concept '" + concept + "' resolves '" + viaConcept.name + "', expected " + expected);
                if (!seenSprites.Add(expected))
                    failures.Add("two CurrencyKinds share the sprite " + expected + " - identity by silhouette is lost");

                CheckCurrencySpriteImport(expected, failures);
            }

            // ── 4. 'magic' is a ruled cost concept but NOT a CurrencyKind ─────────────
            // BuildingUpgradeVM.ResourceCostString emits a Magic cost (spec site B3), so the row
            // has to exist and resolve even though no chip enum member carries it.
            if ((string)map?["magic"]?["role"] != "currency" || (string)map?["magic"]?["name"] != "currency_magic")
                failures.Add("concept-icons.json has no currency/currency_magic row (owner ruling 2, 2026-08-26)");
            else if (ConceptIconResolver.Resolve("magic") == null)
                failures.Add("concept 'magic' resolves NO sprite");
            else
                CheckCurrencySpriteImport("currency_magic", failures);

            // ── 5. THE ART ITSELF, pinned to the source the owner chose ──────────────
            // ⭐ A row can point at the right filename while the file behind it is the wrong
            // picture - which is exactly how the 1200px agribusiness LOGO stayed on the food chip
            // through every green marker. So the ruling is pinned by BYTES against the art the
            // owner picked off the 96px contact sheet, not by name.
            CheckRuledArt(assets, "currency_food",   "Resources/HudIcons/hud_food.png",
                          "owner ruling 1 - the distinct 332x321 art, NOT the 1200px illustration", failures);
            CheckRuledArt(assets, "currency_magic",  "Resources/RpgUi/emblem/Arcanist.png",
                          "owner ruling 2 - the aether shard was DISQUALIFIED as silhouette-identical to crystal", failures);
            CheckRuledArt(assets, "currency_wisdom", "Resources/ItemIcons/blink_spellbook1h_01.png",
                          "owner ruling 3 - the spellbook tome", failures);

            // ── 6. The RE-IMPORTER must reproduce the ruling, not the superseded pick ─
            // ⛔ BlinkUiImporter REGENERATES currency_* from a Src path. Pinning only the bytes
            // would leave a green build one import run away from reverting to the 2026-07-07 food
            // pick, so the generator is pinned too - the copy and the recipe must agree.
            string importerPath = Path.Combine(assets, "Editor/BlinkUiImporter.cs");
            if (!File.Exists(importerPath))
                failures.Add("Editor/BlinkUiImporter.cs is missing - the currency re-import recipe");
            else
            {
                string src = StripComments(File.ReadAllText(importerPath));
                if (src.Contains("HudIcons/food.png"))
                    failures.Add("BlinkUiImporter still regenerates currency art from the SUPERSEDED "
                                 + "HudIcons/food.png (WO-1195 owner ruling 1 chose hud_food.png)");
                foreach (var pair in new[]
                         {
                             new[] { "currency_food",   "HudIcons/hud_food.png" },
                             new[] { "currency_magic",  "emblem/Arcanist.png" },
                             new[] { "currency_wisdom", "ItemIcons/blink_spellbook1h_01.png" },
                         })
                {
                    int at = src.IndexOf("Name = \"" + pair[0] + "\"", StringComparison.Ordinal);
                    int line = at < 0 ? -1 : src.LastIndexOf("new Entry", at, StringComparison.Ordinal);
                    // The Src path must sit INSIDE this entry - i.e. at or after "new Entry" and
                    // before the Name it belongs to. A missing path (-1) is a failure, not a pass.
                    int srcAt = line < 0 ? -1 : src.IndexOf(pair[1], line, StringComparison.Ordinal);
                    if (at < 0 || line < 0 || srcAt < 0 || srcAt > at)
                        failures.Add("BlinkUiImporter has no " + pair[0] + " entry sourced from " + pair[1]);
                }
            }
        }

        /// <summary>Assert a currency sprite's bytes ARE the art the owner ruled for it.</summary>
        private static void CheckRuledArt(string assets, string spriteName, string sourceRel, string ruling, List<string> failures)
        {
            string target = Path.Combine(assets, "Resources/RpgUi/currency/" + spriteName + ".png");
            string source = Path.Combine(assets, sourceRel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(target)) { failures.Add(spriteName + ".png is missing (" + ruling + ")"); return; }
            if (!File.Exists(source)) { failures.Add("ruled source art " + sourceRel + " is missing (" + ruling + ")"); return; }
            byte[] a = File.ReadAllBytes(target), b = File.ReadAllBytes(source);
            if (a.Length != b.Length)
            {
                failures.Add(spriteName + " is not the ruled art from " + sourceRel + " (" + ruling + ")");
                return;
            }
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i])
                {
                    failures.Add(spriteName + " is not the ruled art from " + sourceRel + " (" + ruling + ")");
                    return;
                }
        }

        /// <summary>UI-import policy for one currency sprite - the same settings the Stone art is
        /// already pinned to, applied to every kind so a newly authored icon cannot ship mipmapped,
        /// opaque, tiling or oversized.</summary>
        private static void CheckCurrencySpriteImport(string spriteName, List<string> failures)
        {
            string spritePath = "Assets/Resources/RpgUi/currency/" + spriteName + ".png";
            var importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
            if (importer == null)
            {
                failures.Add(spriteName + ".png has no TextureImporter (art missing or not imported)");
                return;
            }
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
                failures.Add(spriteName + " is not imported as one Sprite");
            if (!importer.alphaIsTransparency || importer.mipmapEnabled)
                failures.Add(spriteName + " lost transparent-alpha or no-mipmap UI import settings");
            if (importer.wrapMode != TextureWrapMode.Clamp || importer.npotScale != TextureImporterNPOTScale.None)
                failures.Add(spriteName + " lost clamp or no-NPOT-scaling UI import settings");
            foreach (string platform in new[] { "DefaultTexturePlatform", "Standalone", "Android", "WebGL" })
            {
                TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
                if (settings.maxTextureSize > 512)
                    failures.Add(spriteName + " exceeds the 512px shipped UI texture ceiling on " + platform);
            }
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
