// =============================================================================
// BlinkUiImporter — mirrors the Blink "Obsidian" UI slice into
// Resources/RpgUi/<role>/<canonical>.png so the EXISTING sprite-first UI kit
// (RpgUiCatalog / ElarionUiKit) re-skins the ENTIRE game UI — store, equip,
// inventory, HUD, dialogue — to the Obsidian theme, in ONE editor run.
// -----------------------------------------------------------------------------
// Same pattern as RpgUiImporter (CopyAsset into Resources + force Sprite import).
// It writes the SAME canonical names RpgUiCatalog reads (panel_vendor, button_frame,
// …), so it OVERWRITES the Tech-hud look — and re-running "Import RPG UI Pack"
// reverts. Blink is gitignored + not under Resources, so this copies the USED slice
// INTO Resources (committed) — the asset policy for any runtime-loaded Blink art.
//
// Run: Defenders > Art > Import Blink UI Pack  (or batchmode DeNelle.Editor.BlinkUiImporter.Run)
//
// PROOF SLICE: panels + buttons (the most visible surfaces). Bars / icons / slots
// extend the same table once the look is confirmed.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class BlinkUiImporter
    {
        private const string PackRoot = "Assets/Blink/Art/UI/Obsidian_UI";
        private const string ResRoot  = "Assets/Resources/RpgUi";

        // One sprite to mirror: Blink source (relative to PackRoot), kit role folder,
        // canonical name (matches an RpgUiCatalog constant), and a 9-slice border guess.
        private struct Entry { public string Src; public string Role; public string Name; public int Border; }

        [MenuItem("Defenders/Art/Import Blink UI Pack")]
        public static void ImportMenu() { Run(); }

        public static void Run()
        {
            var entries = BuildTable();
            int copied = 0, missing = 0;
            foreach (var e in entries)
            {
                string src = PackRoot + "/" + e.Src;
                if (!File.Exists(src)) { Debug.LogWarning("[BlinkUiImporter] missing pack sprite (skipped): " + src); missing++; continue; }

                string dstDir = ResRoot + "/" + e.Role;
                EnsureFolder(dstDir);
                string dst = dstDir + "/" + e.Name + ".png";
                if (File.Exists(dst)) AssetDatabase.DeleteAsset(dst);
                if (!AssetDatabase.CopyAsset(src, dst)) { Debug.LogWarning("[BlinkUiImporter] copy failed: " + src + " -> " + dst); continue; }
                ForceSprite(dst, e.Border);
                copied++;
            }

            AssetDatabase.Refresh();
            Debug.Log("[BlinkUiImporter] done — mirrored " + copied + " Obsidian sprite(s) into " + ResRoot +
                      " (" + missing + " missing). The whole UI now reads Obsidian. Re-run 'Import RPG UI Pack' to revert.");
        }

        // Obsidian -> canonical-name map. Panels + buttons first (the visible win).
        private static List<Entry> BuildTable() => new List<Entry>
        {
            // ── PANELS (RolePanel) ──────────────────────────────────────────────
            new Entry { Src = "Panels_Obsidian/Merchant_Panel.png",  Role = "panel", Name = "panel_vendor",      Border = 48 }, // shop
            new Entry { Src = "Panels_Obsidian/Core_2_Panel.png",    Role = "panel", Name = "panel_window_dark", Border = 48 }, // neutral default
            new Entry { Src = "Panels_Obsidian/Core_Panel.png",      Role = "panel", Name = "panel_window",      Border = 48 }, // dialogue/hero
            new Entry { Src = "Panels_Obsidian/Inventory_Panel.png", Role = "panel", Name = "panel_grid",        Border = 48 }, // inventory grid
            new Entry { Src = "Panels_Obsidian/Inventory_Panel.png", Role = "panel", Name = "panel_inventory",   Border = 48 },
            new Entry { Src = "Panels_Obsidian/Quest_Panel.png",     Role = "panel", Name = "panel_quest",       Border = 48 },
            new Entry { Src = "Panels_Obsidian/Stats_Panel.png",     Role = "panel", Name = "panel_portrait",    Border = 48 },
            new Entry { Src = "Panels_Obsidian/Panel_Element.png",   Role = "panel", Name = "panel_bar",         Border = 32 },
            new Entry { Src = "Panels_Obsidian/Panel_Element.png",   Role = "panel", Name = "panel_tab",         Border = 24 },

            // ── BUTTONS (RoleButton) ────────────────────────────────────────────
            new Entry { Src = "Buttons_Obsidian/Button1_Gray.png",       Role = "button", Name = "button_frame", Border = 24 }, // neutral
            new Entry { Src = "Buttons_Obsidian/Button1_Yellow.png",     Role = "button", Name = "button_gold",  Border = 24 }, // primary/gold
            new Entry { Src = "Buttons_Obsidian/Close_Button_Normal.png",Role = "button", Name = "button_exit",  Border = 12 }, // close/exit
        };

        private static void ForceSprite(string assetPath, int border)
        {
            var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (ti == null) { Debug.LogWarning("[BlinkUiImporter] no TextureImporter for " + assetPath); return; }
            ti.textureType        = TextureImporterType.Sprite;
            ti.spriteImportMode   = SpriteImportMode.Single;
            ti.mipmapEnabled      = false;
            ti.alphaIsTransparency = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed; // crisp UI edges
            ti.npotScale          = TextureImporterNPOTScale.None;

            if (border > 0)
            {
                var s = new TextureImporterSettings();
                ti.ReadTextureSettings(s);
                s.spriteBorder   = new Vector4(border, border, border, border); // 9-slice so frames scale clean
                s.spriteMeshType = SpriteMeshType.FullRect;                     // required for 9-slice
                ti.SetTextureSettings(s);
            }
            ti.SaveAndReimport();
        }

        private static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf   = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
