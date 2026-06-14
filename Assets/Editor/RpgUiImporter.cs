// =============================================================================
// RpgUiImporter — copies the owner's polished RPG UI sprite pack ("Tech hud
// elements") into Assets/Resources/RpgUi/<role>/<canonical>.png and imports each
// as a SINGLE Sprite, so the runtime RpgUiCatalog can load them WebGL-safe via
// Resources.LoadAll<Sprite>("RpgUi/<role>"). Mirrors ItemIconSlicer's
// CopyToResources pattern (CopyAsset into Resources + force the importer settings).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   (editor-only)
//
// WHY COPY (not slice): unlike the item-art ATLAS sheets, this pack ships every
// element as its OWN transparent PNG. So there's nothing to slice — we just select
// the HIGH-IMPACT, COHESIVE subset (one warm gold-frame / bronze-on-parchment
// design variant that matches the existing light/parchment UI), copy each chosen
// PNG into a role folder under Resources with a CANONICAL name (so the catalog can
// address it by a stable id regardless of the pack's loose file names), and force
// the destination texture to import as a single Sprite (alpha-transparent, no
// mips, clamp, uncompressed so the gilt edges stay crisp).
//
// DESIGN VARIANT CHOSEN — the WARM gold-frame + bronze-on-parchment family:
//   • bars   : Loading 5/6/7 (ornate gold frame + gem socket, colored fills)
//   • icons  : Rpg icons (warm bronze-gold on dark-parchment frame) + Tab icons gear/star
//   • potion : Magic bottles b1/b2/b3 (framed health/mana/fire bottles)
//   • badge  : Level badage 1
//   • button : Play buttons/button 3 (gold scroll)
//   • panel  : Menu bar 1 + Score Tab 1 + Ui Elements inventory/quest panels
// This avoids the neon/blue "Loading 1-2, Play button 1" tech variants which clash
// with the warm parchment north-star.
//
// RUN (batchmode-safe):  run-unity-method DeNelle.Editor.RpgUiImporter.Run
//   (menu: Defenders/Art/Import RPG UI Pack)
// Missing source files are warned + skipped (the pack may be partially present),
// so a partial pack still imports whatever it can. Re-run after editing the map.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class RpgUiImporter
    {
        private const string PackRoot = "Assets/Tech hud elements/Sprites";
        private const string ResRoot  = "Assets/Resources/RpgUi";

        // One element to copy: source path, destination role folder, canonical name.
        private struct Entry
        {
            public string Src;    // full project path under PackRoot
            public string Role;   // role subfolder under Resources/RpgUi
            public string Name;   // canonical file name (no extension) used by the catalog
            public int    Border; // uniform 9-slice border in px (0 = Simple, no slicing). Ornate
                                   // window/button frames need this so corners stay crisp when the
                                   // panel is stretched to any size (Image.Type.Sliced).
        }

        [MenuItem("Defenders/Art/Import RPG UI Pack")]
        public static void ImportMenu() { Run(); }

        // Public, batchmode-runnable.
        public static void Run()
        {
            EnsureFolders();

            var entries = BuildEntryTable();
            int copied = 0, missing = 0;
            foreach (var e in entries)
            {
                string src = PackRoot + "/" + e.Src;
                if (!File.Exists(src))
                {
                    Debug.LogWarning("[RpgUiImporter] missing pack sprite (skipped): " + src);
                    missing++;
                    continue;
                }

                string ext = Path.GetExtension(src); // keep the source extension (.png)
                string dstDir = ResRoot + "/" + e.Role;
                EnsureFolder(dstDir);
                string dst = dstDir + "/" + e.Name + ext;

                if (File.Exists(dst)) AssetDatabase.DeleteAsset(dst);
                if (!AssetDatabase.CopyAsset(src, dst))
                {
                    Debug.LogWarning("[RpgUiImporter] copy failed: " + src + " -> " + dst);
                    continue;
                }
                ForceSpriteImport(dst, e.Border);
                copied++;
            }

            AssetDatabase.Refresh();
            Debug.Log("[RpgUiImporter] done — copied " + copied + " sprite(s) into " + ResRoot +
                      " (" + missing + " missing/skipped). Roles: bars/icons/potion/badge/button/panel.");
        }

        // ---------------------------------------------------------------------
        // The role -> pack-file map. Canonical names match RpgUiCatalog constants.
        // ---------------------------------------------------------------------
        private static List<Entry> BuildEntryTable()
        {
            return new List<Entry>
            {
                // ── bars/ — ornate gold-frame bar family (frame bg + matching fill) ──
                // HP = Loading 6 (red gem socket + red fill).
                new Entry { Src = "Loading 6/bg.png",   Role = "bars", Name = "bar_frame_red" },
                new Entry { Src = "Loading 6/fill.png", Role = "bars", Name = "bar_fill_red" },
                // generic/castle = Loading 5 (green gem socket + green fill).
                new Entry { Src = "Loading 5/bg.png",   Role = "bars", Name = "bar_frame_green" },
                new Entry { Src = "Loading 5/fill.png", Role = "bars", Name = "bar_fill_green" },
                // MP = Loading 7 (gem socket + green-glow fill; tinted blue by the HUD).
                new Entry { Src = "Loading 7/bg.png",   Role = "bars", Name = "bar_frame_blue" },
                new Entry { Src = "Loading 7/fill.png", Role = "bars", Name = "bar_fill_blue" },

                // ── icons/ — warm bronze-gold RPG action icons (Rpg icons 1-10) ──
                new Entry { Src = "Tab icons/icon 4.png", Role = "icons", Name = "icon_settings" },  // gear
                new Entry { Src = "Tab icons/icon 2.png", Role = "icons", Name = "icon_compass" },   // star/compass
                new Entry { Src = "Rpg icons/icon 2.png", Role = "icons", Name = "icon_inventory" }, // chest
                new Entry { Src = "Rpg icons/icon 6.png", Role = "icons", Name = "icon_talk" },      // person
                new Entry { Src = "Rpg icons/icon 8.png", Role = "icons", Name = "icon_quest" },     // map
                new Entry { Src = "Rpg icons/icon 9.png", Role = "icons", Name = "icon_sword" },     // sword
                new Entry { Src = "Rpg icons/icon 5.png", Role = "icons", Name = "icon_shield" },    // shield
                new Entry { Src = "Rpg icons/icon 10.png",Role = "icons", Name = "icon_heart" },     // heart
                new Entry { Src = "Rpg icons/icon 3.png", Role = "icons", Name = "icon_tree" },      // campfire → crest
                new Entry { Src = "Rpg icons/icon 7.png", Role = "icons", Name = "icon_combat" },    // sword+axe

                // ── potion/ — framed magic bottles ──
                new Entry { Src = "Magic bottles/b1.png", Role = "potion", Name = "potion_health" }, // red
                new Entry { Src = "Magic bottles/b2.png", Role = "potion", Name = "potion_mana" },   // blue
                new Entry { Src = "Magic bottles/b3.png", Role = "potion", Name = "potion_fire" },   // orange

                // ── badge/ — level badge ──
                new Entry { Src = "Level badage/Level badage 1.png", Role = "badge", Name = "badge_level" },

                // ── button/ — gold scroll button frame ──
                new Entry { Src = "Play buttons/button 3.png", Role = "button", Name = "button_gold" },

                // ── panel/ — warm plate + banner + inventory/quest panels ──
                new Entry { Src = "Menu Bars/Menu bar 1.png",     Role = "panel", Name = "panel_bar" },
                new Entry { Src = "Score tabs/Tab 1.png",         Role = "panel", Name = "panel_tab" },
                new Entry { Src = "Ui Elements/Dialogue.png",     Role = "panel", Name = "panel_inventory" },
                new Entry { Src = "Ui Elements/Quest log.png",    Role = "panel", Name = "panel_quest" },

                // ── panel/ — clean ORNATE window frames (WO-438 screen map). These are
                // empty framed plates meant to BE a window background, 9-sliced (Border)
                // so they stretch to any panel size without distorting the carved corners.
                //   D8 = ornate scrollwork window  (dialogue / hero windows: D1/D5/D6 family)
                //   D5 = clean dark carved frame    (neutral default window)
                //   D3 = dark-wood vendor board     (vendor / shop)
                //   D4 = grid plate                 (inventory)
                // Borders sized to each frame's actual corner art (px): D8 1446x945 ornate
                // scrollwork corners ~120; D5 1392x945 carved cusps ~90; D3/D4 720-wide plates.
                new Entry { Src = "D8/Dialogue.png",   Role = "panel", Name = "panel_window",      Border = 120 },
                new Entry { Src = "D5/Dialogue_.png",  Role = "panel", Name = "panel_window_dark", Border = 90  },
                new Entry { Src = "D3/Dialogue.png",   Role = "panel", Name = "panel_vendor",      Border = 56  },
                new Entry { Src = "D4/Dialogue_.png",  Role = "panel", Name = "panel_grid",        Border = 44  },

                // ── button/ — clean framed (text-free) button + exit, 9-sliced ──
                new Entry { Src = "D2/Button 1.png",   Role = "button", Name = "button_frame", Border = 28 },
                new Entry { Src = "D2/Exit.png",       Role = "button", Name = "button_exit",  Border = 20 },
            };
        }

        // Force the copied texture to import as a single Sprite (UI-ready, crisp edges).
        // border>0 sets a uniform 9-slice border so ornate window/button frames stretch
        // to any panel size without distorting the carved corners (Image.Type.Sliced).
        private static void ForceSpriteImport(string assetPath, int border = 0)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning("[RpgUiImporter] no TextureImporter for " + assetPath);
                return;
            }
            importer.textureType        = TextureImporterType.Sprite;
            importer.spriteImportMode    = SpriteImportMode.Single;   // individual PNG, NOT multiple
            importer.mipmapEnabled       = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode            = TextureWrapMode.Clamp;
            importer.filterMode          = FilterMode.Bilinear;
            importer.textureCompression  = TextureImporterCompression.Uncompressed; // keep gilt edges clean
            importer.npotScale           = TextureImporterNPOTScale.None;
            if (border > 0)
            {
                // 9-slice border (L,B,R,T) lives on the importer's sprite settings.
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteBorder = new Vector4(border, border, border, border);
                settings.spriteMeshType = SpriteMeshType.FullRect; // sliced frames need FullRect
                importer.SetTextureSettings(settings);
            }
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        // ── Folder helpers (create the Resources/RpgUi/<role> tree as needed) ──
        private static void EnsureFolders()
        {
            EnsureFolder(ResRoot);
            EnsureFolder(ResRoot + "/bars");
            EnsureFolder(ResRoot + "/icons");
            EnsureFolder(ResRoot + "/potion");
            EnsureFolder(ResRoot + "/badge");
            EnsureFolder(ResRoot + "/button");
            EnsureFolder(ResRoot + "/panel");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
