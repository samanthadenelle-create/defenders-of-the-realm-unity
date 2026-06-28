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
        // Second source pack: the Blink "OBSIDIAN UI" art (gitignored). We MIRROR the
        // ornate obsidian panel/slot/silhouette/button PNGs into committed Resources/RpgUi
        // so the runtime catalog can serve them on a fresh clone / WebGL (the source pack
        // need not be present once mirrored). CopyAsset generates a FRESH GUID for each
        // copy, so there is no duplicate-GUID clash with the gitignored originals.
        private const string BlinkRoot = "Assets/Blink/Art/UI/Obsidian_UI";
        private const string ResRoot  = "Assets/Resources/RpgUi";

        // One element to copy: source path, destination role folder, canonical name.
        private struct Entry
        {
            public string Src;    // path under Root (defaults to PackRoot)
            public string Root;   // source pack root; null/empty => PackRoot. Set BlinkRoot for Obsidian art.
            public string Role;   // role subfolder under Resources/RpgUi
            public string Name;   // canonical file name (no extension) used by the catalog
            public int    Border; // uniform 9-slice border in px (0 = Simple, no slicing). Ornate
                                   // window/button frames need this so corners stay crisp when the
                                   // panel is stretched to any size (Image.Type.Sliced).
            public Vector4 Border4; // per-side 9-slice border (L,B,R,T) in px; (0,0,0,0) => use uniform Border.
                                    // Obsidian panels have a tall header band + footer that differ from the
                                    // sides, so they need asymmetric borders to slice without distorting.
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
                string baseRoot = string.IsNullOrEmpty(e.Root) ? PackRoot : e.Root;
                string src = baseRoot + "/" + e.Src;
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
                ForceSpriteImport(dst, e.Border, e.Border4);
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

                // =====================================================================
                // BLINK "OBSIDIAN UI" — the canonical black+gold RPG panel art the owner
                // pinned as the template. Mirrored out of the gitignored Assets/Blink into
                // committed Resources/RpgUi so every screen renders the REAL ornate frame
                // (fresh-clone / WebGL safe). Panels are big complete backgrounds → import
                // Simple (Border 0) and the kit renders them preserve-aspect. Slots/buttons
                // are small + scale → 9-sliced with a corner border.
                // =====================================================================

                // frame/ — full ornate obsidian panel backgrounds (one per screen). Simple.
                new Entry { Root = BlinkRoot, Src = "Panels_Obsidian/Inventory_Panel.png",   Role = "frame", Name = "frame_inventory" },
                new Entry { Root = BlinkRoot, Src = "Panels_Obsidian/Crafting_Panel.png",    Role = "frame", Name = "frame_crafting" },
                new Entry { Root = BlinkRoot, Src = "Panels_Obsidian/Stats_Panel.png",       Role = "frame", Name = "frame_character" },
                new Entry { Root = BlinkRoot, Src = "Panels_Obsidian/Core_Panel.png",        Role = "frame", Name = "frame_core" },
                new Entry { Root = BlinkRoot, Src = "Panels_Obsidian/Core_2_Panel.png",      Role = "frame", Name = "frame_core_2" },
                new Entry { Root = BlinkRoot, Src = "Panels_Obsidian/Talent_Tree_Panel.png", Role = "frame", Name = "frame_talent" },
                new Entry { Root = BlinkRoot, Src = "Panels_Obsidian/Merchant_Panel.png",    Role = "frame", Name = "frame_merchant" },
                new Entry { Root = BlinkRoot, Src = "Panels_Obsidian/Dialogue_Panel.png",    Role = "frame", Name = "frame_dialogue" },
                new Entry { Root = BlinkRoot, Src = "Panels_Obsidian/Quest_Log_Panel.png",   Role = "frame", Name = "frame_quest" },
                new Entry { Root = BlinkRoot, Src = "Panels_Obsidian/Settings_Panel.png",    Role = "frame", Name = "frame_settings" },
                new Entry { Root = BlinkRoot, Src = "Panels_Obsidian/Options_Panel.png",     Role = "frame", Name = "frame_options" },
                new Entry { Root = BlinkRoot, Src = "Panels_Obsidian/Loot_Panel.png",        Role = "frame", Name = "frame_loot" },
                new Entry { Root = BlinkRoot, Src = "Panels_Obsidian/Pet_Panel.png",         Role = "frame", Name = "frame_pet" },
                new Entry { Root = BlinkRoot, Src = "Panels_Obsidian/Stats_Panel.png",       Role = "frame", Name = "frame_stats" },
                // Panel_Element + Text_Background are inner sub-plates (9-sliced, they scale).
                new Entry { Root = BlinkRoot, Src = "Panels_Obsidian/Panel_Element.png",     Role = "frame", Name = "frame_element",  Border = 40 },
                new Entry { Root = BlinkRoot, Src = "Panels_Obsidian/Text_Background.png",   Role = "frame", Name = "frame_textbg",   Border = 32 },

                // silhouette/ — the paper-doll body silhouettes behind the equipment slots.
                new Entry { Root = BlinkRoot, Src = "Panels_Obsidian/Male_Silouhette.png",   Role = "silhouette", Name = "sil_male" },
                new Entry { Root = BlinkRoot, Src = "Panels_Obsidian/Female_Silouhette.png", Role = "silhouette", Name = "sil_female" },
                new Entry { Root = BlinkRoot, Src = "Panels_Obsidian/Pet_Silouhette.png",    Role = "silhouette", Name = "sil_pet" },

                // slot/ — ornate square item/gear/talent sockets, 9-sliced (corner ~28px).
                new Entry { Root = BlinkRoot, Src = "Slots_Obsidian/Inventory_Slot.png",  Role = "slot", Name = "slot_item",      Border = 28 },
                new Entry { Root = BlinkRoot, Src = "Slots_Obsidian/Armor_Slot.png",      Role = "slot", Name = "slot_armor",     Border = 28 },
                new Entry { Root = BlinkRoot, Src = "Slots_Obsidian/Armor_Slot_2.png",    Role = "slot", Name = "slot_armor_2",   Border = 28 },
                new Entry { Root = BlinkRoot, Src = "Slots_Obsidian/Character_Slot.png",  Role = "slot", Name = "slot_character", Border = 28 },
                new Entry { Root = BlinkRoot, Src = "Slots_Obsidian/Action_Bar_Slot.png", Role = "slot", Name = "slot_action",    Border = 24 },
                new Entry { Root = BlinkRoot, Src = "Slots_Obsidian/Socketing_Slot.png",  Role = "slot", Name = "slot_socket",    Border = 24 },
                new Entry { Root = BlinkRoot, Src = "Slots_Obsidian/Rarity_1.png",        Role = "slot", Name = "rarity_1",       Border = 24 },
                new Entry { Root = BlinkRoot, Src = "Slots_Obsidian/Rarity_2.png",        Role = "slot", Name = "rarity_2",       Border = 24 },
                new Entry { Root = BlinkRoot, Src = "Slots_Obsidian/Rarity_3.png",        Role = "slot", Name = "rarity_3",       Border = 24 },
                new Entry { Root = BlinkRoot, Src = "Slots_Obsidian/Rarity_4.png",        Role = "slot", Name = "rarity_4",       Border = 24 },
                new Entry { Root = BlinkRoot, Src = "Slots_Obsidian/Rarity_5.png",        Role = "slot", Name = "rarity_5",       Border = 24 },
                new Entry { Root = BlinkRoot, Src = "Slots_Obsidian/Talent_Border_1.png", Role = "slot", Name = "talent_1",       Border = 30 },
                new Entry { Root = BlinkRoot, Src = "Slots_Obsidian/Talent_Border_2.png", Role = "slot", Name = "talent_2",       Border = 30 },
                new Entry { Root = BlinkRoot, Src = "Slots_Obsidian/Talent_Border_3.png", Role = "slot", Name = "talent_3",       Border = 30 },
                new Entry { Root = BlinkRoot, Src = "Slots_Obsidian/Talent_Border_4.png", Role = "slot", Name = "talent_4",       Border = 30 },

                // button/ — obsidian buttons (framed, 9-sliced) + the close glyph (Simple).
                new Entry { Root = BlinkRoot, Src = "Buttons_Obsidian/Button1_Gray.png",   Role = "button", Name = "obsidian_gray",   Border = 24 },
                new Entry { Root = BlinkRoot, Src = "Buttons_Obsidian/Button1_Green.png",  Role = "button", Name = "obsidian_green",  Border = 24 },
                new Entry { Root = BlinkRoot, Src = "Buttons_Obsidian/Button1_Red.png",    Role = "button", Name = "obsidian_red",    Border = 24 },
                new Entry { Root = BlinkRoot, Src = "Buttons_Obsidian/Button1_Yellow.png", Role = "button", Name = "obsidian_yellow", Border = 24 },
                new Entry { Root = BlinkRoot, Src = "Buttons_Obsidian/Button2_Green.png",  Role = "button", Name = "button_confirm",  Border = 24 },
                new Entry { Root = BlinkRoot, Src = "Buttons_Obsidian/Button2_Red.png",    Role = "button", Name = "button_deny",     Border = 24 },
                new Entry { Root = BlinkRoot, Src = "Buttons_Obsidian/Close_Button_Normal.png", Role = "button", Name = "close_normal" },
                new Entry { Root = BlinkRoot, Src = "Buttons_Obsidian/Close_Button_On.png",     Role = "button", Name = "close_on" },
                new Entry { Root = BlinkRoot, Src = "Buttons_Obsidian/Arrow.png",               Role = "button", Name = "arrow" },
            };
        }

        // Force the copied texture to import as a single Sprite (UI-ready, crisp edges).
        // border>0 sets a uniform 9-slice border so ornate window/button frames stretch
        // to any panel size without distorting the carved corners (Image.Type.Sliced).
        private static void ForceSpriteImport(string assetPath, int border = 0)
        {
            ForceSpriteImport(assetPath, border, Vector4.zero);
        }

        private static void ForceSpriteImport(string assetPath, int border, Vector4 border4)
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
            // Keep full source resolution for the big ornate panels so the carved edges stay crisp.
            importer.maxTextureSize      = 4096;
            bool hasB4 = border4.sqrMagnitude > 0.01f;
            if (border > 0 || hasB4)
            {
                // 9-slice border (L,B,R,T) lives on the importer's sprite settings.
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteBorder = hasB4 ? border4 : new Vector4(border, border, border, border);
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
            EnsureFolder(ResRoot + "/frame");      // Blink Obsidian full-panel backgrounds
            EnsureFolder(ResRoot + "/silhouette"); // paper-doll body silhouettes
            EnsureFolder(ResRoot + "/slot");       // Obsidian item/gear/talent sockets
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
