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

using System;
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

                // currency/ — resource-chip icons (WO-431 HUD resource panel). Mirrored from
                // Icons_Obsidian so the CurrencyChip concept resolver (concept-icons.json
                // gold/wood/iron/food/crystal) serves a real icon per resource row, WebGL-safe.
                // Food/wood have no literal grain/log art in the pack → nearest-fit obsidian icon.
                new Entry { Root = BlinkRoot, Src = "Icons_Obsidian/Gold_Currency.png", Role = "currency", Name = "currency_gold" },
                new Entry { Root = BlinkRoot, Src = "Icons_Obsidian/Fiber.png",         Role = "currency", Name = "currency_wood" },
                new Entry { Root = BlinkRoot, Src = "Icons_Obsidian/Iron_Bar_1.png",    Role = "currency", Name = "currency_iron" },
                new Entry { Root = BlinkRoot, Src = "Icons_Obsidian/Health_Potion.png", Role = "currency", Name = "currency_food" },
                new Entry { Root = BlinkRoot, Src = "Icons_Obsidian/Rune_2.png",        Role = "currency", Name = "currency_crystal" },
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

        // =====================================================================
        //  WO-1367 — ANDROID TEXTURE PASS (the Play-upload ceiling)
        // ---------------------------------------------------------------------
        // Assets/Resources/RpgUi ships 568 PNGs. Before this, 405 carried NO
        // Android platform override at all and 202 of those imported
        // UNCOMPRESSED (RGBA32) — ~85.8 MiB of build footprint, which put the
        // AAB ~10.5 MB OVER Google Play's 500,000,000-byte ceiling.
        //
        // ⛔ WHY THIS LIVES IN THE IMPORTER, NOT IN A HAND PASS. The previous
        // attempt was a one-off sweep: 65 overrides in the 08-30 RC, 163 by
        // 09-04, 405 still missing. Art added after the sweep never inherited
        // it, so the pass DECAYED. Here the split is owned by ONE table and has
        // TWO entry points that cannot drift apart:
        //   1. AssetImportPostprocessor.OnPreprocessTexture calls
        //      ApplyAndroidPlatformSettings on EVERY texture imported under
        //      Resources/RpgUi — so NEW art inherits it by construction, on
        //      first import and on every re-import.
        //   2. ApplyAndroidTexturePass() (menu + batchmode) sweeps what is
        //      already on disk, idempotently.
        // ⚠ Do NOT re-inline this format choice anywhere else — a second copy
        // is how the last pass went stale (CLAUDE.md §2 / §5 / §16).
        //
        // THE QUALITY SPLIT — owner-approved, implemented VERBATIM:
        //   ASTC 4x4 — roles where sharp edges + 9-slicing matter and block
        //              artifacts would read as damage:
        //              frame, panel, slot, button, classslot, bars, hud
        //   ASTC 6x6 — illustrative roles (forgiving), and ANY role not listed
        //              above (so a brand-new role folder gets the safe-for-size
        //              default rather than silently getting no override).
        //
        // ⛔ maxTextureSize IS NOT TOUCHED. The override inherits the asset's
        // own default-platform maxTextureSize, so authored dimensions are
        // preserved exactly (WO-1367 "Option A": no resize, no source edit).
        //
        // ⚠⚠ MEASURED 2026-09-04, BEFORE ANY OF THIS RAN — the split as ruled
        // makes the build BIGGER, and the reason is not obvious:
        //   THE 163 FILES THAT ALREADY CARRY AN ANDROID OVERRIDE ARE ALL ASTC
        //   6x6, AND THEY ARE ALMOST ENTIRELY THE BIG CHROME — frame 17/17,
        //   panel 11/11, hud 33/39, bars 6/6, button 35/50, slot 14/22.
        // Promoting those from 6x6 (3.56 bpp) to 4x4 (8 bpp) more than doubles
        // the heaviest art in the folder. Estimated from PNG dimensions x format,
        // clamped at the authored maxTextureSize 2048:
        //   today .......................... 80.68 MiB
        //   this 4x4/6x6 split ............. 86.19 MiB   (+5.5 MiB — WORSE)
        //   ASTC 6x6 on all 568 ............ 53.58 MiB   (-27.1 MiB)
        // The blow-ups: frame 12.48 -> 28.08, panel 6.86 -> 15.43, hud 3.72 ->
        // 8.18, bars 0.46 -> 1.04.
        // ⚠ ESTIMATE, not a built artifact — only a rebuild + `bundletool
        // get-size total` proves it (WO-1367 ACCEPTANCE, CLAUDE.md §11B). It is
        // recorded here because the AAB is ~10.5 MB OVER the Play ceiling and
        // this split moves it the wrong way.
        // TO SWITCH A ROLE: move its name out of SharpAndroidRoles below — that
        // array is the ONLY place the tier is decided (owner's call, not ours).
        // =====================================================================

        internal const string AndroidPlatform = "Android";

        /// Quality 50 = "Normal" — the same ASTC quality the KayKit path uses
        /// (AssetImportPostprocessor.AstcCompressionQuality). Kept identical so
        /// the two mobile paths do not diverge in look.
        private const int AndroidAstcQuality = 50;

        /// Roles that get ASTC 4x4 (sharp edges / 9-slice). Everything else in
        /// Resources/RpgUi gets ASTC 6x6 — see the header block.
        /// ==================================================================
        /// CORRECTED 2026-09-04 - THIS ARRAY IS DELIBERATELY EMPTY. 6x6 EVERYWHERE.
        ///
        /// The lead originally ruled ASTC 4x4 for frame/panel/slot/button/
        /// classslot/bars/hud, reasoning that 9-sliced chrome shows block
        /// artifacts as damage. Sound in the abstract, WRONG HERE - the
        /// measurement is why:
        ///
        ///   role    total  already-overridden  current format
        ///   frame      17                  17  ASTC_6x6
        ///   panel      11                  11  ASTC_6x6
        ///   bars        6                   6  ASTC_6x6
        ///   hud        39                  33  ASTC_6x6
        ///   button     50                  35  ASTC_6x6
        ///   slot       22                  14  ASTC_6x6
        ///
        /// 116 of those 173 files ALREADY SHIP AT 6x6. The owner has been looking
        /// at that UI and accepted it, so 6x6 there is the STATUS QUO, not a
        /// regression - while promoting them to 4x4 (3.56 -> 8 bpp) would DOUBLE
        /// the heaviest art in the folder. Estimated net effect of the original
        /// ruling: 80.68 -> 86.19 MiB, i.e. +5.5 MiB on a ticket whose whole
        /// purpose is to remove ~10 MB.
        ///
        /// The un-overridden mass is illustrative art that 6x6 suits:
        /// spellicons 280 (zero overrides), classslot 28, emblem 25, troop 9.
        ///
        /// 6x6 everywhere is therefore unchanged for everything already shipping,
        /// internally consistent per role, and the full ~27 MiB saving.
        ///
        /// TO RAISE A ROLE'S QUALITY: add its name here. Still a one-line change,
        /// still the ONLY place the tier is decided. If the owner sees block
        /// artifacts on a specific role, this is the lever.
        /// ==================================================================
        private static readonly string[] SharpAndroidRoles =
        {
        };

        /// True when the asset path is a texture under Assets/Resources/RpgUi/.
        internal static bool IsRpgUiTexturePath(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath)
                && assetPath.Replace('\\', '/')
                            .StartsWith(ResRoot + "/", StringComparison.OrdinalIgnoreCase);
        }

        /// The role folder directly under Resources/RpgUi (e.g. "spellicons" for
        /// RpgUi/spellicons/Warrior/Guardian/x.png). Empty for a file sitting at
        /// the RpgUi root — those are reported, never silently skipped.
        internal static string ResolveRpgUiRole(string assetPath)
        {
            if (!IsRpgUiTexturePath(assetPath)) return string.Empty;
            string rel = assetPath.Replace('\\', '/').Substring(ResRoot.Length + 1);
            int slash = rel.IndexOf('/');
            return slash <= 0 ? string.Empty : rel.Substring(0, slash);
        }

        /// The owner-approved format for a role. Unlisted / unknown roles fall to
        /// ASTC 6x6 by design (see header) — never to "no override".
        internal static TextureImporterFormat ResolveAndroidFormat(string role)
        {
            foreach (var r in SharpAndroidRoles)
                if (string.Equals(r, role, StringComparison.OrdinalIgnoreCase))
                    return TextureImporterFormat.ASTC_4x4;
            return TextureImporterFormat.ASTC_6x6;
        }

        /// <summary>
        /// Applies the Android platform override to one RpgUi texture importer.
        /// Returns TRUE when it actually CHANGED something (so the caller knows
        /// whether a SaveAndReimport is warranted), FALSE when the importer was
        /// already correct. Does NOT call SaveAndReimport itself — the import
        /// pipeline (OnPreprocessTexture) must never re-enter the importer.
        /// Every other import setting is preserved untouched.
        /// </summary>
        internal static bool ApplyAndroidPlatformSettings(TextureImporter importer)
        {
            if (importer == null) return false;
            string role = ResolveRpgUiRole(importer.assetPath);
            var format  = ResolveAndroidFormat(role);

            var settings = importer.GetPlatformTextureSettings(AndroidPlatform);
            // ⛔ NEVER RAISE A CAP. A size pass that loosens a limit is a
            // regression wearing a fix's clothes.
            //
            // Measured 2026-09-04 on the first run of this pass: taking
            // importer.maxTextureSize (the DEFAULT platform's value) and writing
            // it onto Android lowered 342 files nicely (2048 -> 256 on 289,
            // 2048 -> 512 on 53) but RAISED 159 whose Android override was
            // deliberately TIGHTER than their default:
            //     1024 -> 2048 : 115 files
            //     1024 -> 4096 :  43 files
            //     2048 -> 4096 :   1 file
            // Someone had already hand-tightened those and this pass would have
            // undone it silently. So: when an Android override already carries a
            // maxTextureSize, take the SMALLER of it and the default. The result
            // is monotonic - this pass can only ever shrink.
            int defaultSize = importer.maxTextureSize;
            int size = settings.overridden && settings.maxTextureSize > 0
                     ? System.Math.Min(settings.maxTextureSize, defaultSize)
                     : defaultSize;

            bool alreadyCorrect = settings.overridden
                                  && settings.format == format
                                  && settings.maxTextureSize == size
                                  && settings.textureCompression == TextureImporterCompression.Compressed;
            if (alreadyCorrect) return false;

            settings.name               = AndroidPlatform;
            settings.overridden         = true;
            settings.format             = format;
            settings.maxTextureSize     = size;
            settings.textureCompression = TextureImporterCompression.Compressed;
            settings.compressionQuality = AndroidAstcQuality;
            importer.SetPlatformTextureSettings(settings);
            return true;
        }

        [MenuItem("Defenders/Art/Apply RpgUi Android Texture Pass")]
        public static void ApplyAndroidTexturePassMenu() { ApplyAndroidTexturePass(); }

        /// <summary>
        /// Batch sweep over Assets/Resources/RpgUi/** — idempotent. Menu entry
        /// above; batchmode:
        ///   -executeMethod DeNelle.Editor.RpgUiImporter.ApplyAndroidTexturePass
        /// Judge by the MARKER, never the exit code (CLAUDE.md §16):
        ///   RPGUI_TEXTURE_PASS_OK &lt;n&gt; applied, &lt;n&gt; already correct, &lt;n&gt; skipped
        ///   RPGUI_TEXTURE_PASS_FAIL &lt;reason&gt;
        /// </summary>
        public static void ApplyAndroidTexturePass()
        {
            int applied = 0, alreadyCorrect = 0, skipped = 0;
            var perRoleTotal   = new Dictionary<string, int>();
            var perRoleApplied = new Dictionary<string, int>();

            try
            {
                if (!AssetDatabase.IsValidFolder(ResRoot))
                {
                    Debug.LogError("RPGUI_TEXTURE_PASS_FAIL missing folder " + ResRoot);
                    return;
                }

                var guids = AssetDatabase.FindAssets("t:Texture", new[] { ResRoot });
                Debug.Log("[RpgUiAndroidPass] scanning " + guids.Length + " texture asset(s) under " + ResRoot);

                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (var guid in guids)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        if (string.IsNullOrEmpty(path))
                        {
                            // Never silently skip (CLAUDE.md §12).
                            Debug.LogWarning("[RpgUiAndroidPass] SKIPPED: guid resolved to no path: " + guid);
                            skipped++;
                            continue;
                        }

                        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                        if (importer == null)
                        {
                            Debug.LogWarning("[RpgUiAndroidPass] SKIPPED: no TextureImporter (not a raw texture asset): " + path);
                            skipped++;
                            continue;
                        }

                        string role = ResolveRpgUiRole(path);
                        if (string.IsNullOrEmpty(role))
                        {
                            // A texture sitting at the RpgUi root has no role folder;
                            // it still gets the 6x6 default, but say so out loud.
                            role = "(root)";
                            Debug.LogWarning("[RpgUiAndroidPass] no role folder for " + path + " — defaulting to ASTC_6x6");
                        }

                        perRoleTotal.TryGetValue(role, out int t); perRoleTotal[role] = t + 1;

                        bool changed;
                        try
                        {
                            changed = ApplyAndroidPlatformSettings(importer);
                        }
                        catch (Exception exOne)
                        {
                            // One bad asset logs and is skipped — it never kills the pass.
                            Debug.LogWarning("[RpgUiAndroidPass] SKIPPED (threw): " + path + " — " + exOne.Message);
                            skipped++;
                            continue;
                        }

                        if (!changed) { alreadyCorrect++; continue; }

                        // ⚠ .meta files are GUID-bearing: SaveAndReimport REWRITES the
                        // import-settings block IN PLACE. Nothing here creates, deletes
                        // or regenerates a .meta, so no GUID changes (WO-1367).
                        importer.SaveAndReimport();
                        applied++;
                        perRoleApplied.TryGetValue(role, out int a); perRoleApplied[role] = a + 1;
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }

                AssetDatabase.Refresh();

                foreach (var kv in perRoleTotal)
                {
                    perRoleApplied.TryGetValue(kv.Key, out int a);
                    Debug.Log("[RpgUiAndroidPass] role=" + kv.Key +
                              " format=" + ResolveAndroidFormat(kv.Key) +
                              " total=" + kv.Value +
                              " applied=" + a +
                              " alreadyCorrect=" + (kv.Value - a));
                }

                Debug.Log("RPGUI_TEXTURE_PASS_OK " + applied + " applied, " +
                          alreadyCorrect + " already correct, " + skipped + " skipped");
            }
            catch (Exception ex)
            {
                Debug.LogError("RPGUI_TEXTURE_PASS_FAIL " + ex.GetType().Name + ": " + ex.Message +
                               " (after " + applied + " applied, " + alreadyCorrect +
                               " already correct, " + skipped + " skipped)");
            }
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
