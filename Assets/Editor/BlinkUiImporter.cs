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
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

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
                // An "Assets/..." Src is used verbatim (owner-directed art that already lives in the
                // project, e.g. HudIcons); anything else is relative to the gitignored Blink pack.
                string src = e.Src.StartsWith("Assets/") ? e.Src : PackRoot + "/" + e.Src;
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
            EnsureAtlas();
            Debug.Log("[BlinkUiImporter] done — mirrored " + copied + " Obsidian sprite(s) into " + ResRoot +
                      " (" + missing + " missing). The whole UI now reads Obsidian. Re-run 'Import RPG UI Pack' to revert.");
        }

        // ── SpriteAtlases: batch the rebuilt HUD into minimal draw calls ────────
        // TWO atlases (BLINK_OBSIDIAN_UI_UNDERSTANDING §5: atlas the icons
        // separately from the panels): sliced chrome vs simple glyphs/bars.
        // Settings chosen for 9-slice safety: rotation OFF and tight packing OFF
        // (both break sliced-sprite geometry), include-in-build ON.
        private const string AtlasSlicedPath = ResRoot + "/RpgUiAtlas_Sliced.spriteatlas";
        private const string AtlasSimplePath = ResRoot + "/RpgUiAtlas_Simple.spriteatlas";
        private static readonly string[] SlicedRoles = { "button", "element", "slot", "frame" }; // 9-sliced chrome
        private static readonly string[] SimpleRoles = { "hud", "icons" };                       // simple glyphs/bars/cores

        private static void EnsureAtlas()
        {
            EnsureOneAtlas(AtlasSlicedPath, SlicedRoles);
            EnsureOneAtlas(AtlasSimplePath, SimpleRoles);
        }

        private static void EnsureOneAtlas(string atlasPath, string[] roles)
        {
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
            bool created = atlas == null;
            if (created)
            {
                atlas = new SpriteAtlas();
                AssetDatabase.CreateAsset(atlas, atlasPath);
            }

            atlas.SetPackingSettings(new SpriteAtlasPackingSettings
            {
                enableRotation    = false, // rotated packing breaks 9-slice borders
                enableTightPacking = false, // tight packing breaks sliced/full-rect sprites
                padding           = 4,
            });
            atlas.SetTextureSettings(new SpriteAtlasTextureSettings
            {
                readable        = false,
                generateMipMaps = false,
                sRGB            = true,
                filterMode      = FilterMode.Bilinear,
            });
            atlas.SetIncludeInBuild(true);

            // Folder packables: everything imported into a role folder is covered,
            // including future re-runs that add sprites — no per-sprite bookkeeping.
            var already = new HashSet<Object>(atlas.GetPackables());
            var toAdd = new List<Object>();
            foreach (var role in roles)
            {
                string folder = ResRoot + "/" + role;
                if (!AssetDatabase.IsValidFolder(folder)) continue; // fresh-clone safe: nothing imported yet
                var f = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder);
                if (f != null && !already.Contains(f)) toAdd.Add(f);
            }
            if (toAdd.Count > 0) atlas.Add(toAdd.ToArray());

            EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssets();
            Debug.Log("[BlinkUiImporter] atlas " + (created ? "created" : "updated") + " at " + atlasPath +
                      " (" + atlas.GetPackables().Length + " folder packable(s); rotation OFF, tight packing OFF, include-in-build ON).");
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
            new Entry { Src = "Buttons_Obsidian/Button2_Green.png",      Role = "button", Name = "button_confirm", Border = 24 }, // confirm/buy (obsidian green)
            new Entry { Src = "Buttons_Obsidian/Close_Button_Normal.png",Role = "button", Name = "button_exit",  Border = 12 }, // close/exit

            // ── SLOTS (RoleSlot) — per-item article plate for shop/inventory rows ──
            new Entry { Src = "Slots_Obsidian/Inventory_Slot.png", Role = "slot", Name = "slot_item", Border = 24 },

            // ── TALENT NODE-GRAPH (Path B) — node frames + tree window + flourishes ──
            // 6 ornate node-border frames (used per node-state / tier; slot_talent = the default = 1).
            new Entry { Src = "Slots_Obsidian/Talent_Border_1.png", Role = "slot", Name = "slot_talent",   Border = 24 },
            new Entry { Src = "Slots_Obsidian/Talent_Border_1.png", Role = "slot", Name = "slot_talent_1", Border = 24 },
            new Entry { Src = "Slots_Obsidian/Talent_Border_2.png", Role = "slot", Name = "slot_talent_2", Border = 24 },
            new Entry { Src = "Slots_Obsidian/Talent_Border_3.png", Role = "slot", Name = "slot_talent_3", Border = 24 },
            new Entry { Src = "Slots_Obsidian/Talent_Border_4.png", Role = "slot", Name = "slot_talent_4", Border = 24 },
            new Entry { Src = "Slots_Obsidian/Talent_Border_5.png", Role = "slot", Name = "slot_talent_5", Border = 24 },
            new Entry { Src = "Slots_Obsidian/Talent_Border_6.png", Role = "slot", Name = "slot_talent_6", Border = 24 }, // capstone frame
            // Tree window frame (large 9-slice, matches the other panels at 48).
            new Entry { Src = "Panels_Obsidian/Talent_Tree_Panel.png", Role = "panel", Name = "panel_talent", Border = 48 },
            // Ornamental flourishes (Phase-5 polish; no slice — placed at native aspect).
            new Entry { Src = "Decoration_Obsidian/TalentTree_Decoration_1.png", Role = "decoration", Name = "deco_talent_1", Border = 0 },
            new Entry { Src = "Decoration_Obsidian/TalentTree_Decoration_2.png", Role = "decoration", Name = "deco_talent_2", Border = 0 },

            // ── ICONS (RoleIcons) — whole glyphs, no 9-slice (border 0) ─────────
            new Entry { Src = "Icons_Obsidian/Sword.png",          Role = "icons", Name = "icon_sword",     Border = 0 },
            new Entry { Src = "Icons_Obsidian/settings-icon.png",  Role = "icons", Name = "icon_settings",  Border = 0 },
            new Entry { Src = "Icons_Obsidian/inventory-icon.png", Role = "icons", Name = "icon_inventory", Border = 0 },
            new Entry { Src = "Icons_Obsidian/quest-icon.png",     Role = "icons", Name = "icon_quest",     Border = 0 },
            // WO-611 attack pill (F8-3 2026-07-07): owner AttackIcon — the importer owns the Sprite
            // import settings; the hand-copied icon_energy_sword.png shipped textureType:0 (not a
            // Sprite) so LoadAll<Sprite> never returned it and the pill fell back to icon_sword.
            new Entry { Src = "Icons_Obsidian/AttackIcon.png",     Role = "icons", Name = "icon_energy_sword", Border = 0 },

            // ── CURRENCY (RoleCurrency) — owner directive 2026-07-07: Gold_Currency beside gold.
            // concept-icons.json already maps gold -> currency/currency_gold; the folder was never
            // mirrored, so every resource row fell back to its glyph (the "resource rows without
            // identifiers" F8 board ticket). Wood/food/crystal picks await the owner's art call.
            new Entry { Src = "Icons_Obsidian/Gold_Currency.png",  Role = "currency", Name = "currency_gold", Border = 0 },
            // Owner 2026-07-07: the Wood icon = the HudIcons log-pile art (already committed).
            new Entry { Src = "Assets/Resources/HudIcons/hud_wood.png", Role = "currency", Name = "currency_wood", Border = 0 },
            // Owner 2026-07-07: food = HudIcons/food.png (her pick — NOT hud_food.png), crystal =
            // HudIcons/hud_crystal.png, iron = HudIcons/hud_iron.png.
            new Entry { Src = "Assets/Resources/HudIcons/food.png",        Role = "currency", Name = "currency_food",    Border = 0 },
            new Entry { Src = "Assets/Resources/HudIcons/hud_crystal.png", Role = "currency", Name = "currency_crystal", Border = 0 },
            new Entry { Src = "Assets/Resources/HudIcons/hud_iron.png",    Role = "currency", Name = "currency_iron",    Border = 0 },
            // (icon_shield / icon_talk / icon_heart kept on the Tech-hud fallback — no clean Obsidian
            //  match in the sampled set.)

            // =================================================================
            // HUD → OBSIDIAN CONVERSION (docs/HUD_OBSIDIAN_ARCHITECTURE_2026-07-03.md §2)
            // Source paths are VERBATIM (including the pack's filename typos:
            // Castt_Bar_Fill, STamina_Bar, Potrait_Border, HIUD_Core, Socketing_Border_!).
            // Canonical names are ALWAYS clean — the catalog never sees a typo.
            // Bars/cores/nameplates/cast = Simple (border 0: ornate silhouettes
            // distort under 9-slice); buttons/slots/plates/tabs = 9-sliced.
            // =================================================================

            // ── HUD (role hud/) — HUD_Obsidian ─────────────────────────────────
            new Entry { Src = "HUD_Obsidian/Health_Bar.png",               Role = "hud", Name = "bar_health",             Border = 0 },
            new Entry { Src = "HUD_Obsidian/Mana_Bar.png",                 Role = "hud", Name = "bar_mana",               Border = 0 },
            new Entry { Src = "HUD_Obsidian/Energy_Bar.png",               Role = "hud", Name = "bar_energy",             Border = 0 },
            new Entry { Src = "HUD_Obsidian/STamina_Bar.png",              Role = "hud", Name = "bar_stamina",            Border = 0 },  // typo'd source "STamina"
            new Entry { Src = "HUD_Obsidian/Stat_Bar_Background.png",      Role = "hud", Name = "bar_stat_bg",            Border = 12 },
            new Entry { Src = "HUD_Obsidian/Stat_Bar_White.png",           Role = "hud", Name = "bar_stat_fill",          Border = 12 },
            new Entry { Src = "HUD_Obsidian/Cast_Bar_1.png",               Role = "hud", Name = "bar_cast_1",             Border = 0 },
            new Entry { Src = "HUD_Obsidian/Cast_Bar_2.png",               Role = "hud", Name = "bar_cast_2",             Border = 0 },
            new Entry { Src = "HUD_Obsidian/Cast_Bar_3.png",               Role = "hud", Name = "bar_cast_3",             Border = 0 },
            new Entry { Src = "HUD_Obsidian/Castt_Bar_Fill.png",           Role = "hud", Name = "bar_cast_fill",          Border = 0 },  // TYPO #1 — source is "Castt"
            new Entry { Src = "HUD_Obsidian/hud-xpbar.png",                Role = "hud", Name = "bar_xp",                 Border = 0 },
            new Entry { Src = "HUD_Obsidian/Target_Core.png",              Role = "hud", Name = "target_core",            Border = 0 },
            new Entry { Src = "HUD_Obsidian/Nameplate_Bar.png",            Role = "hud", Name = "nameplate_bar",          Border = 0 },
            new Entry { Src = "HUD_Obsidian/Nameplate_Health.png",         Role = "hud", Name = "nameplate_health",       Border = 0 },
            new Entry { Src = "HUD_Obsidian/Nameplate_Health_Enemy.png",   Role = "hud", Name = "nameplate_health_enemy", Border = 0 },
            new Entry { Src = "HUD_Obsidian/Nameplate_Health_Neutral.png", Role = "hud", Name = "nameplate_health_neutral", Border = 0 },
            new Entry { Src = "HUD_Obsidian/Nameplate_Mana.png",           Role = "hud", Name = "nameplate_mana",         Border = 0 },
            new Entry { Src = "HUD_Obsidian/Nameplate_Portrait.png",       Role = "hud", Name = "nameplate_portrait",     Border = 0 },
            new Entry { Src = "HUD_Obsidian/Nameplate_Boss_Border.png",    Role = "hud", Name = "nameplate_boss",         Border = 0 },
            new Entry { Src = "HUD_Obsidian/Nameplate_Rare_Border.png",    Role = "hud", Name = "nameplate_rare",         Border = 0 },
            new Entry { Src = "HUD_Obsidian/Nameplate_Enemy_Background.png", Role = "hud", Name = "nameplate_enemy_bg",   Border = 0 },
            new Entry { Src = "HUD_Obsidian/Party_Nameplate.png",          Role = "hud", Name = "nameplate_party",        Border = 0 },
            new Entry { Src = "HUD_Obsidian/Potrait_Border.png",           Role = "hud", Name = "portrait_border",        Border = 0 },  // typo'd source "Potrait"
            new Entry { Src = "HUD_Obsidian/HIUD_Core.png",                Role = "hud", Name = "hud_core",               Border = 0 },  // typo'd source "HIUD"
            new Entry { Src = "HUD_Obsidian/HUD_Diablo-Core.png",          Role = "hud", Name = "hud_core_diablo",        Border = 0 },
            new Entry { Src = "HUD_Obsidian/Stat_Orb_Diablo.png",          Role = "hud", Name = "stat_orb",               Border = 0 },
            new Entry { Src = "HUD_Obsidian/Chat_Core.png",                Role = "hud", Name = "chat_core",              Border = 48 },
            new Entry { Src = "HUD_Obsidian/Chat_Tab.png",                 Role = "hud", Name = "chat_tab",               Border = 12 },
            new Entry { Src = "HUD_Obsidian/Quest_Tracker.png",            Role = "hud", Name = "quest_tracker",          Border = 24 },
            new Entry { Src = "HUD_Obsidian/Quest_Tracker_BAR.png",        Role = "hud", Name = "quest_tracker_bar",      Border = 0 },
            new Entry { Src = "HUD_Obsidian/Collapse.png",                 Role = "hud", Name = "hud_collapse",           Border = 0 },
            new Entry { Src = "HUD_Obsidian/Expand.png",                   Role = "hud", Name = "hud_expand",             Border = 0 },
            new Entry { Src = "HUD_Obsidian/Block.png",                    Role = "hud", Name = "hud_block",              Border = 0 },
            new Entry { Src = "HUD_Obsidian/Interaction.png",              Role = "hud", Name = "hud_interaction",        Border = 0 },
            new Entry { Src = "HUD_Obsidian/Arc_1.png",                    Role = "hud", Name = "hud_arc_1",              Border = 0 },
            new Entry { Src = "HUD_Obsidian/Arc_2.png",                    Role = "hud", Name = "hud_arc_2",              Border = 0 },
            // Lock-on crosshair frames (WO-611 F2): target-frame lock badge animation
            // (Crosshair_1 unlocked → Crosshair_2 acquiring → Crosshair_3 locked). Simple.
            new Entry { Src = "Cursors_Obsidian/Crosshair_1.png",          Role = "hud", Name = "crosshair_1",            Border = 0 },
            new Entry { Src = "Cursors_Obsidian/Crosshair_2.png",          Role = "hud", Name = "crosshair_2",            Border = 0 },
            new Entry { Src = "Cursors_Obsidian/Crosshair_3.png",          Role = "hud", Name = "crosshair_3",            Border = 0 },

            // ── BUTTONS (role button/) — Buttons_Obsidian ──────────────────────
            // The full 5×4 family (styles 1..5 × gray/green/red/yellow), 9-sliced.
            new Entry { Src = "Buttons_Obsidian/Button1_Gray.png",   Role = "button", Name = "button1_gray",   Border = 24 },
            new Entry { Src = "Buttons_Obsidian/Button1_Green.png",  Role = "button", Name = "button1_green",  Border = 24 },
            new Entry { Src = "Buttons_Obsidian/Button1_Red.png",    Role = "button", Name = "button1_red",    Border = 24 },
            new Entry { Src = "Buttons_Obsidian/Button1_Yellow.png", Role = "button", Name = "button1_yellow", Border = 24 },
            new Entry { Src = "Buttons_Obsidian/Button2_Gray.png",   Role = "button", Name = "button2_gray",   Border = 24 },
            new Entry { Src = "Buttons_Obsidian/Button2_Green.png",  Role = "button", Name = "button2_green",  Border = 24 },
            new Entry { Src = "Buttons_Obsidian/Button2_Red.png",    Role = "button", Name = "button2_red",    Border = 24 },
            new Entry { Src = "Buttons_Obsidian/Button2_Yellow.png", Role = "button", Name = "button2_yellow", Border = 24 },
            new Entry { Src = "Buttons_Obsidian/Button3_Gray.png",   Role = "button", Name = "button3_gray",   Border = 24 },
            new Entry { Src = "Buttons_Obsidian/Button3_Green.png",  Role = "button", Name = "button3_green",  Border = 24 },
            new Entry { Src = "Buttons_Obsidian/Button3_Red.png",    Role = "button", Name = "button3_red",    Border = 24 },
            new Entry { Src = "Buttons_Obsidian/Button3_Yellow.png", Role = "button", Name = "button3_yellow", Border = 24 },
            new Entry { Src = "Buttons_Obsidian/Button4_Gray.png",   Role = "button", Name = "button4_gray",   Border = 24 },
            new Entry { Src = "Buttons_Obsidian/Button4_Green.png",  Role = "button", Name = "button4_green",  Border = 24 },
            new Entry { Src = "Buttons_Obsidian/Button4_Red.png",    Role = "button", Name = "button4_red",    Border = 24 },
            new Entry { Src = "Buttons_Obsidian/Button4_Yellow.png", Role = "button", Name = "button4_yellow", Border = 24 },
            new Entry { Src = "Buttons_Obsidian/Button5_Gray.png",   Role = "button", Name = "button5_gray",   Border = 24 },
            new Entry { Src = "Buttons_Obsidian/Button5_Green.png",  Role = "button", Name = "button5_green",  Border = 24 },
            new Entry { Src = "Buttons_Obsidian/Button5_Red.png",    Role = "button", Name = "button5_red",    Border = 24 },
            new Entry { Src = "Buttons_Obsidian/Button5_Yellow.png", Role = "button", Name = "button5_yellow", Border = 24 },
            // 3-state Close (SpriteSwap states for the ONE shared ObsidianCloseButton).
            new Entry { Src = "Buttons_Obsidian/Close_Button_Normal.png", Role = "button", Name = "close_normal", Border = 0 },
            new Entry { Src = "Buttons_Obsidian/Close_Button_On.png",     Role = "button", Name = "close_on",     Border = 0 },
            new Entry { Src = "Buttons_Obsidian/Close_Button_Off.png",    Role = "button", Name = "close_off",    Border = 0 },
            new Entry { Src = "Buttons_Obsidian/Toggle_On.png",           Role = "button", Name = "toggle_on",    Border = 0 },
            new Entry { Src = "Buttons_Obsidian/Toggle_Off.png",          Role = "button", Name = "toggle_off",   Border = 0 },
            new Entry { Src = "Buttons_Obsidian/Slider_Background.png",   Role = "button", Name = "slider_bg",    Border = 12 },
            new Entry { Src = "Buttons_Obsidian/Slider_Fill.png",         Role = "button", Name = "slider_fill",  Border = 12 },
            new Entry { Src = "Buttons_Obsidian/Slider_Handle.png",       Role = "button", Name = "slider_handle", Border = 0 },
            new Entry { Src = "Buttons_Obsidian/Dropdown_1.png",          Role = "button", Name = "dropdown_1",   Border = 16 },
            new Entry { Src = "Buttons_Obsidian/Dropdown_2.png",          Role = "button", Name = "dropdown_2",   Border = 16 },
            new Entry { Src = "Buttons_Obsidian/Dropdown_3.png",          Role = "button", Name = "dropdown_3",   Border = 16 },
            new Entry { Src = "Buttons_Obsidian/Notification_1.png",      Role = "button", Name = "notif_btn_1",  Border = 32 },
            new Entry { Src = "Buttons_Obsidian/Notification_2.png",      Role = "button", Name = "notif_btn_2",  Border = 32 },
            new Entry { Src = "Buttons_Obsidian/Popup.png",               Role = "button", Name = "popup",        Border = 32 },
            new Entry { Src = "Buttons_Obsidian/Chat_Element_1.png",      Role = "button", Name = "chat_element_1", Border = 12 },
            new Entry { Src = "Buttons_Obsidian/Chat_Element_2.png",      Role = "button", Name = "chat_element_2", Border = 12 },
            new Entry { Src = "Buttons_Obsidian/Chat_Element_3.png",      Role = "button", Name = "chat_element_3", Border = 12 },
            new Entry { Src = "Buttons_Obsidian/Chat_Element_4.png",      Role = "button", Name = "chat_element_4", Border = 12 },
            new Entry { Src = "Buttons_Obsidian/Map_Zoom.png",            Role = "button", Name = "map_zoom",     Border = 0 },
            new Entry { Src = "Buttons_Obsidian/Map_Unzoom.png",          Role = "button", Name = "map_unzoom",   Border = 0 },
            new Entry { Src = "Buttons_Obsidian/Arrow.png",               Role = "button", Name = "arrow",        Border = 0 },

            // ── ELEMENTS (role element/) — Elements_Obsidian ───────────────────
            new Entry { Src = "Elements_Obsidian/Stat_Element.png",        Role = "element", Name = "element_stat",     Border = 24 },
            new Entry { Src = "Panels_Obsidian/Panel_Element.png",         Role = "element", Name = "element_bar",      Border = 32 },
            new Entry { Src = "Elements_Obsidian/Tab.png",                 Role = "element", Name = "element_tab",      Border = 16 },
            new Entry { Src = "Elements_Obsidian/Notification_1.png",      Role = "element", Name = "notif_1",          Border = 32 },
            new Entry { Src = "Elements_Obsidian/Notification_2.png",      Role = "element", Name = "notif_2",          Border = 32 },
            new Entry { Src = "Elements_Obsidian/Notification_4.png",      Role = "element", Name = "notif_4",          Border = 32 },
            new Entry { Src = "Elements_Obsidian/Bar_1.png",               Role = "element", Name = "element_bar_1",    Border = 0 },
            new Entry { Src = "Elements_Obsidian/Bar_5.png",               Role = "element", Name = "element_bar_5",    Border = 0 },
            new Entry { Src = "Elements_Obsidian/Bar_5_Fill.png",          Role = "element", Name = "element_bar_5_fill", Border = 0 },
            new Entry { Src = "Elements_Obsidian/Loading_Background.png",  Role = "element", Name = "loading_bg",       Border = 24 },
            new Entry { Src = "Elements_Obsidian/Loading_Bar.png",         Role = "element", Name = "loading_fill",     Border = 12 },
            new Entry { Src = "Elements_Obsidian/Scroll_Background.png",   Role = "element", Name = "scroll_bg",        Border = 12 },
            new Entry { Src = "Elements_Obsidian/Scroll_Up.png",           Role = "element", Name = "scroll_up",        Border = 0 },
            new Entry { Src = "Elements_Obsidian/ToggleBox_On.png",        Role = "element", Name = "togglebox_on",     Border = 0 },
            new Entry { Src = "Elements_Obsidian/ToggleBox_Off.png",       Role = "element", Name = "togglebox_off",    Border = 0 },
            new Entry { Src = "Elements_Obsidian/Handle.png",              Role = "element", Name = "handle",           Border = 0 },
            new Entry { Src = "Elements_Obsidian/Cross.png",               Role = "element", Name = "cross",            Border = 0 },
            new Entry { Src = "Elements_Obsidian/Arrow_Box.png",           Role = "element", Name = "arrow_box",        Border = 0 },
            new Entry { Src = "Elements_Obsidian/Arrow_Box_On.png",        Role = "element", Name = "arrow_box_on",     Border = 0 },
            new Entry { Src = "Elements_Obsidian/GameMenu_Button_1.png",   Role = "element", Name = "menu_btn_1",       Border = 0 },
            new Entry { Src = "Elements_Obsidian/GameMenu_Button_2.png",   Role = "element", Name = "menu_btn_2",       Border = 0 },
            new Entry { Src = "Elements_Obsidian/GameMenu_Button_3.png",   Role = "element", Name = "menu_btn_3",       Border = 0 },
            new Entry { Src = "Elements_Obsidian/Enchanting_Element.png",  Role = "element", Name = "enchant_element",  Border = 24 },
            new Entry { Src = "Elements_Obsidian/Enchanting_Slot.png",     Role = "element", Name = "enchant_slot",     Border = 24 },
            new Entry { Src = "Elements_Obsidian/Socketing_Border_!.png",  Role = "element", Name = "border_socket_1",  Border = 24 },  // TYPO #2 — source ends in "!"
            new Entry { Src = "Elements_Obsidian/Socketing_Border_2.png",  Role = "element", Name = "border_socket_2",  Border = 24 },
            new Entry { Src = "Elements_Obsidian/Socketing_Border_3.png",  Role = "element", Name = "border_socket_3",  Border = 24 },
            new Entry { Src = "Elements_Obsidian/Socketing_Border_4.png",  Role = "element", Name = "border_socket_4",  Border = 24 },

            // ── SLOTS (role slot/) additions — populate the reserved RpgUiCatalog names ──
            new Entry { Src = "Slots_Obsidian/Action_Bar_Slot.png", Role = "slot", Name = "slot_action",    Border = 24 },
            new Entry { Src = "Slots_Obsidian/Armor_Slot.png",      Role = "slot", Name = "slot_armor",     Border = 24 },
            new Entry { Src = "Slots_Obsidian/Character_Slot.png",  Role = "slot", Name = "slot_character", Border = 24 },
            new Entry { Src = "Slots_Obsidian/Rarity_1.png",        Role = "slot", Name = "rarity_1",       Border = 24 },
            new Entry { Src = "Slots_Obsidian/Rarity_2.png",        Role = "slot", Name = "rarity_2",       Border = 24 },
            new Entry { Src = "Slots_Obsidian/Rarity_3.png",        Role = "slot", Name = "rarity_3",       Border = 24 },
            new Entry { Src = "Slots_Obsidian/Rarity_4.png",        Role = "slot", Name = "rarity_4",       Border = 24 },
            new Entry { Src = "Slots_Obsidian/Rarity_5.png",        Role = "slot", Name = "rarity_5",       Border = 24 },
            new Entry { Src = "Slots_Obsidian/Socketing_Slot.png",  Role = "slot", Name = "slot_socket",    Border = 24 },

            // ── FRAMES (role frame/) gap-fill — NPC dialogue card variant ──────
            new Entry { Src = "Panels_Obsidian/Dialogue_2_Panel.png", Role = "frame", Name = "frame_dialogue_2", Border = 0 },
        };

        /// If a pack-relative source (e.g. "HUD_Obsidian/Cast_Bar_1.png") has a canonical
        /// table mapping, return its committed Resources destination + slice border.
        /// Used by BlinkPrefabMirror so prefab dependencies reuse the canonical mirror
        /// instead of duplicating the texture.
        internal static bool TryCanonicalDst(string srcRel, out string dstPath, out int border)
        {
            foreach (var e in BuildTable())
            {
                if (e.Src == srcRel)
                {
                    dstPath = ResRoot + "/" + e.Role + "/" + e.Name + ".png";
                    border = e.Border;
                    return true;
                }
            }
            dstPath = null; border = 0; return false;
        }

        internal static void ForceSprite(string assetPath, int border)
        {
            var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (ti == null) { Debug.LogWarning("[BlinkUiImporter] no TextureImporter for " + assetPath); return; }
            ti.textureType        = TextureImporterType.Sprite;
            ti.spriteImportMode   = SpriteImportMode.Single;
            ti.mipmapEnabled      = false;
            ti.alphaIsTransparency = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed; // deliberate — compression artifacts show on the thin borders (BLINK_OBSIDIAN_UI_UNDERSTANDING §5)
            ti.npotScale          = TextureImporterNPOTScale.None;

            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            // PACK-META WINS (BLINK_OBSIDIAN_UI_UNDERSTANDING §5): CopyAsset preserved the
            // pack's own import settings — if the vendor baked a 9-slice border, keep it.
            // The table border is the STARTING POINT used only when the pack has none.
            // (Surveyed 2026-07-03: every pack .png.meta currently ships all-zero borders,
            // so the table values are operative today — but the vendor's numbers win if
            // a pack update ever adds them.)
            bool packHasBorder = s.spriteBorder != Vector4.zero;
            if (!packHasBorder && border > 0)
                s.spriteBorder = new Vector4(border, border, border, border); // 9-slice so frames scale clean
            if (packHasBorder || border > 0)
                s.spriteMeshType = SpriteMeshType.FullRect;                    // required for 9-slice
            ti.SetTextureSettings(s);
            ti.SaveAndReimport();
        }

        internal static void EnsureFolder(string path)
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
