// =============================================================================
// BlinkFontImporter — generates the three role TMP SDF font assets from the
// Blink Obsidian pack fonts (docs/HUD_OBSIDIAN_ARCHITECTURE_2026-07-03.md §1.13):
//
//   FontRoleTitle → Merriweather-Bold  → Resources/RpgUi/font/font_title.asset
//   FontRoleBody  → Alata-Regular      → Resources/RpgUi/font/font_body.asset
//   FontRoleStamp → Acme-Regular       → Resources/RpgUi/font/font_stamp.asset
//
// STATIC atlases (baked glyph set, no runtime rasterizing): Basic Latin
// (ASCII 32–126: letters/digits/punctuation) + the game's typographic extras
// (×, en/em dash, ellipsis, curly quotes, degree, bullet). Titillium is
// reserved — NOT generated in v1 (TMP atlas cost).
//
// The generated .asset files live under committed Resources/ so the game is
// fresh-clone safe; the gitignored Blink pack is only needed to REGENERATE.
// Pack absent ⇒ Debug.LogWarning + no-op (game runs on the existing fallback
// chain: sprite-first → TMP_Settings.defaultFontAsset → LiberationSans).
//
// Run: Defenders > Art > Import Blink Fonts
//      (or batchmode DeNelle.Editor.BlinkFontImporter.Run)
// =============================================================================

using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace DeNelle.Editor
{
    public static class BlinkFontImporter
    {
        private const string FontsRoot = "Assets/Blink/Art/UI/Obsidian_UI/Fonts_Obsidian";
        private const string DstDir    = "Assets/Resources/RpgUi/font";

        // Role → (source .ttf under FontsRoot, canonical asset name).
        private static readonly (string Src, string Name)[] Table =
        {
            ("Merriweather/Merriweather-Bold.ttf", "font_title"), // serif — forged-fantasy headers
            ("Alata/Alata-Regular.ttf",            "font_body"),  // clean geometric sans — body default
            ("Acme/Acme-Regular.ttf",              "font_stamp"), // display — combat stamps, toast headlines
        };

        [MenuItem("Defenders/Art/Import Blink Fonts")]
        public static void ImportMenu() { Run(); }

        public static void Run()
        {
            // Fresh-clone safety: the Blink pack is gitignored — warn + no-op when absent.
            if (!AssetDatabase.IsValidFolder(FontsRoot))
            {
                Debug.LogWarning("[BlinkFontImporter] Blink pack not present (" + FontsRoot +
                                 ") — skipping font generation. UI keeps the existing font fallback chain.");
                return;
            }

            EnsureFolder(DstDir);
            string chars = BuildCharacterSet();

            int made = 0, missing = 0;
            foreach (var e in Table)
            {
                string srcPath = FontsRoot + "/" + e.Src;
                var font = AssetDatabase.LoadAssetAtPath<Font>(srcPath);
                if (font == null)
                {
                    Debug.LogWarning("[BlinkFontImporter] missing pack font (skipped): " + srcPath);
                    missing++;
                    continue;
                }
                if (GenerateStaticSdf(font, DstDir + "/" + e.Name + ".asset", chars)) made++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[BlinkFontImporter] done — generated " + made + " TMP SDF font asset(s) into " +
                      DstDir + " (" + missing + " source font(s) missing).");
        }

        // Baked glyph set: printable ASCII + the typographic extras our UI uses.
        private static string BuildCharacterSet()
        {
            var sb = new StringBuilder(160);
            for (char c = (char)32; c <= (char)126; c++) sb.Append(c); // Basic Latin: space..~ (letters, digits, punctuation)
            sb.Append('×'); // × (CombatText "PARRY! ×3")
            sb.Append('–'); // – en dash
            sb.Append('—'); // — em dash
            sb.Append('…'); // … ellipsis
            sb.Append('‘'); // ' left single quote
            sb.Append('’'); // ' right single quote / apostrophe
            sb.Append('“'); // " left double quote
            sb.Append('”'); // " right double quote
            sb.Append('°'); // ° degree
            sb.Append('•'); // • bullet
            sb.Append(' '); // no-break space
            return sb.ToString();
        }

        private static bool GenerateStaticSdf(Font font, string dstPath, string chars)
        {
            // Create DYNAMIC first so TryAddCharacters can rasterize the set, then
            // freeze to STATIC — the committed asset never rasterizes at runtime.
            var fa = TMP_FontAsset.CreateFontAsset(
                font, 64, 8, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: false);
            if (fa == null)
            {
                Debug.LogWarning("[BlinkFontImporter] CreateFontAsset failed for " + font.name);
                return false;
            }

            if (!fa.TryAddCharacters(chars, out string missingChars) && !string.IsNullOrEmpty(missingChars))
                Debug.LogWarning("[BlinkFontImporter] " + font.name + " lacks glyphs for: " + missingChars +
                                 " (asset still generated; TMP falls back per-glyph).");

            fa.atlasPopulationMode = AtlasPopulationMode.Static;

            string assetName = System.IO.Path.GetFileNameWithoutExtension(dstPath);
            fa.name = assetName;

            // Replace any previous generation cleanly (sub-assets and all).
            if (AssetDatabase.LoadAssetAtPath<Object>(dstPath) != null)
                AssetDatabase.DeleteAsset(dstPath);

            AssetDatabase.CreateAsset(fa, dstPath);

            // Atlas texture + material ride as sub-assets of the font asset.
            if (fa.atlasTextures != null && fa.atlasTextures.Length > 0 && fa.atlasTextures[0] != null)
            {
                fa.atlasTextures[0].name = assetName + " Atlas";
                AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            }
            if (fa.material != null)
            {
                fa.material.name = assetName + " Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
            }

            EditorUtility.SetDirty(fa);
            Debug.Log("[BlinkFontImporter] generated " + dstPath + " from " + font.name +
                      " (static SDF atlas, " + chars.Length + " chars requested).");
            return true;
        }

        private static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf   = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
