// =============================================================================
// RpgUiCatalog — runtime, WebGL-safe accessor for the owner's polished RPG UI
// sprite pack ("Tech hud elements"). Mirrors ItemIconCatalog / VillageHud's
// HUD-icon loader pattern EXACTLY: the sprites are mirrored into Resources by the
// editor importer (DeNelle.Editor.RpgUiImporter) under Resources/RpgUi/<role>/...,
// and at runtime we lazily Resources.LoadAll<Sprite>("RpgUi/<role>") into a
// name->Sprite dictionary per role. No AssetDatabase / StreamingAssets / File IO
// → safe on every target including WebGL.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.UI
//
// SPRITE-FIRST CONTRACT: every Get*/TryGet returns a sprite when the pack art is
// present, or NULL when it is not (pack not imported, role empty). EVERY caller is
// expected to keep its existing procedural/glyph fallback for the null case — so
// the UI is correct whether or not the art is on disk. This catalog NEVER throws
// to a caller (all IO is wrapped) and caches results so repeated lookups are free.
//
// ROLE FOLDERS (populated by RpgUiImporter; see its role->file map):
//   bars/        — ornate gold-frame bar backgrounds + colored fills (HP/MP/XP/ATB)
//   icons/       — bronze-on-parchment RPG action icons (settings/inventory/talk/quest/...)
//   potion/      — framed magic-bottle potions (health/mana/...)
//   badge/       — level / rank badges
//   button/      — play / healing button frames
//   panel/       — menu-bar / profile / score / inventory / quest panel frames
//
// Each role is a flat folder of single-Sprite PNGs; we index every sprite by its
// asset name (lower-cased) so a caller can address a specific one (e.g. "b1" for
// the red potion) OR just take the first via Get(role) for a representative frame.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Core.UI
{
    /// <summary>
    /// Runtime catalog for the RPG UI sprite pack mirrored to Resources/RpgUi.
    /// All lookups are sprite-first with a null return when the art is absent
    /// (callers keep their procedural fallback). WebGL-safe (Resources only).
    /// </summary>
    public static class RpgUiCatalog
    {
        private const string ResRoot = "RpgUi/";

        // Per-role caches: role -> (spriteName -> Sprite) and role -> ordered list.
        private static readonly Dictionary<string, Dictionary<string, Sprite>> _byName
            = new Dictionary<string, Dictionary<string, Sprite>>(System.StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, List<Sprite>> _ordered
            = new Dictionary<string, List<Sprite>>(System.StringComparer.OrdinalIgnoreCase);

        // ── Canonical role names (kept in lockstep with RpgUiImporter folders) ──
        public const string RoleBars   = "bars";
        public const string RoleIcons  = "icons";
        public const string RolePotion = "potion";
        public const string RoleBadge  = "badge";
        public const string RoleButton = "button";
        public const string RolePanel  = "panel";
        public const string RoleSlot   = "slot";
        // Blink "OBSIDIAN UI" roles (mirrored by RpgUiImporter from Assets/Blink):
        public const string RoleFrame      = "frame";       // full ornate obsidian panel backgrounds
        public const string RoleSilhouette = "silhouette";  // paper-doll body silhouettes

        // frame/ — one ornate obsidian panel background per screen (Simple, preserve-aspect):
        public const string FrameInventory = "frame_inventory";
        public const string FrameCrafting  = "frame_crafting";
        public const string FrameCharacter = "frame_character"; // Stats_Panel
        public const string FrameCore      = "frame_core";
        public const string FrameTalent    = "frame_talent";
        public const string FrameMerchant  = "frame_merchant";
        public const string FrameDialogue  = "frame_dialogue";
        public const string FrameQuest     = "frame_quest";
        public const string FrameSettings  = "frame_settings";
        public const string FrameOptions   = "frame_options";
        public const string FrameLoot      = "frame_loot";
        public const string FramePet       = "frame_pet";
        public const string FrameElement   = "frame_element"; // inner sub-plate (9-sliced)
        public const string FrameTextBg    = "frame_textbg";  // inner text background (9-sliced)
        // silhouette/:
        public const string SilMale   = "sil_male";
        public const string SilFemale = "sil_female";
        public const string SilPet    = "sil_pet";
        // slot/ — Obsidian sockets (9-sliced):
        public const string SlotArmor     = "slot_armor";
        public const string SlotCharacter = "slot_character";
        public const string SlotAction    = "slot_action";
        public const string SlotSocket    = "slot_socket";
        // slot/ rarity frames Rarity_1..5 -> rarity_1..5; talent borders -> talent_1..4.

        // ── Well-known sprite names within a role (lockstep with the importer map) ──
        // bars/ (ornate gold-frame bar family — frame bg + matching colored fill):
        public const string BarFrameRed   = "bar_frame_red";    // HP frame (Loading 6 bg)
        public const string BarFillRed    = "bar_fill_red";     // HP fill   (Loading 6 fill)
        public const string BarFrameGreen = "bar_frame_green";  // generic frame (Loading 5 bg)
        public const string BarFillGreen  = "bar_fill_green";   // generic fill  (Loading 5 fill)
        public const string BarFrameBlue  = "bar_frame_blue";   // MP frame  (Loading 7 bg, green-glow socket reused as blue-less; see importer)
        public const string BarFillBlue   = "bar_fill_blue";    // MP fill
        // icons/ (bronze RPG icons):
        public const string IconSettings  = "icon_settings";    // Tab icons/icon 4 (gear)
        public const string IconInventory = "icon_inventory";   // Rpg icons/icon 2 (chest)
        public const string IconTalk      = "icon_talk";        // Rpg icons/icon 6 (person)
        public const string IconQuest     = "icon_quest";       // Rpg icons/icon 8 (map)
        public const string IconSword     = "icon_sword";       // Rpg icons/icon 9
        public const string IconShield    = "icon_shield";      // Rpg icons/icon 5
        public const string IconHeart     = "icon_heart";       // Rpg icons/icon 10
        public const string IconTree      = "icon_tree";        // Rpg icons/icon 3 (campfire → crest stand-in)
        public const string IconCombat    = "icon_combat";      // Rpg icons/icon 7 (sword+axe)
        public const string IconCompass   = "icon_compass";     // Tab icons/icon 2 (star)
        // potion/ (framed magic bottles):
        public const string PotionHealth  = "potion_health";    // Magic bottles/b1 (red)
        public const string PotionMana    = "potion_mana";      // Magic bottles/b2 (blue)
        public const string PotionFire    = "potion_fire";      // Magic bottles/b3 (orange)
        // badge/:
        public const string BadgeLevel    = "badge_level";      // Level badage 1
        // button/:
        public const string ButtonGold    = "button_gold";      // Play buttons/button 3 (gold scroll)
        public const string ButtonConfirm = "button_confirm";   // Obsidian green confirm/buy (Buttons_Obsidian/Button2_Green)
        // panel/:
        public const string PanelBar      = "panel_bar";        // Menu bar 1 (warm gold/red plate)
        public const string PanelTab      = "panel_tab";        // Score tabs/Tab 1 (ornate banner)
        public const string PanelInventory= "panel_inventory";  // Ui Elements/Dialogue (grid panel)
        public const string PanelQuest    = "panel_quest";      // Ui Elements/Quest log
        // panel/ — clean ORNATE window frames (WO-438 screen map), 9-sliced:
        public const string PanelWindow     = "panel_window";      // D8 ornate scrollwork window (dialogue/hero)
        public const string PanelWindowDark = "panel_window_dark"; // D5 clean dark carved frame (neutral default)
        public const string PanelVendor     = "panel_vendor";      // D3 dark-wood vendor board (shop)
        public const string PanelGrid       = "panel_grid";        // D4 grid plate (inventory)
        public const string PanelPortrait   = "panel_portrait";    // Model selection ornate wood portrait frame
        public const string PanelProfile    = "profile_frame";     // Profile tab P1 — gold sunburst portrait medallion + name + 2 bar slots
        // The canonical DEFAULT window frame: a clean, neutral ornate frame every framed
        // window inherits via ElarionUiKit.PanelFramed when no explicit frame is named.
        public const string PanelDefault    = PanelWindowDark;
        // button/ — clean (text-free) framed button + exit, 9-sliced:
        public const string ButtonFrame   = "button_frame";    // D2/Button 1 (neutral framed button)
        public const string ButtonExit    = "button_exit";     // D2/Exit (close/exit button)
        // slot/ — per-item article plate (Blink ArticleSlot/ItemBackground), 9-sliced:
        public const string SlotItem      = "slot_item";       // Slots_Obsidian/Inventory_Slot (per-item plate)

        /// <summary>
        /// Representative sprite for a role (the first one loaded, by name order), or
        /// null if the role folder is empty / not imported. Callers keep their fallback
        /// on null.
        /// </summary>
        public static Sprite Get(string role)
        {
            EnsureRole(role);
            var list = _ordered.TryGetValue(role, out var l) ? l : null;
            return (list != null && list.Count > 0) ? list[0] : null;
        }

        /// <summary>
        /// A specific named sprite within a role (e.g. role "potion", name "b1"), or
        /// null when absent. Name match is case-insensitive. The well-known name
        /// constants on this class are the importer's canonical names.
        /// </summary>
        public static Sprite Get(string role, string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return Get(role);
            EnsureRole(role);
            var map = _byName.TryGetValue(role, out var m) ? m : null;
            if (map == null) return null;
            return map.TryGetValue(spriteName, out var s) ? s : null;
        }

        /// <summary>True + out-sprite when the named role/sprite art is present.</summary>
        public static bool TryGet(string role, string spriteName, out Sprite sprite)
        {
            sprite = Get(role, spriteName);
            return sprite != null;
        }

        /// <summary>All sprites in a role (empty list when absent). Read-only use.</summary>
        public static IReadOnlyList<Sprite> All(string role)
        {
            EnsureRole(role);
            return _ordered.TryGetValue(role, out var l) ? l : System.Array.Empty<Sprite>();
        }

        // ── Lazy per-role load (WebGL-safe; cached; never throws to the caller) ──
        private static void EnsureRole(string role)
        {
            if (string.IsNullOrEmpty(role) || _ordered.ContainsKey(role)) return;

            var map = new Dictionary<string, Sprite>(System.StringComparer.OrdinalIgnoreCase);
            var list = new List<Sprite>();
            Sprite[] subs = null;
            try { subs = Resources.LoadAll<Sprite>(ResRoot + role); }
            catch (System.Exception e)
            {
                Debug.LogWarning("[RpgUiCatalog] load failed for role '" + role + "': " + e.Message);
            }
            if (subs != null)
            {
                for (int i = 0; i < subs.Length; i++)
                {
                    var sp = subs[i];
                    if (sp == null || string.IsNullOrEmpty(sp.name)) continue;
                    if (!map.ContainsKey(sp.name)) map[sp.name] = sp;
                    list.Add(sp);
                }
            }
            _byName[role] = map;
            _ordered[role] = list;
            if (list.Count == 0)
                Debug.Log("[RpgUiCatalog] no sprites under Resources/" + ResRoot + role +
                          " — run Defenders/Art/Import RPG UI Pack. Callers keep procedural fallback.");
        }

#if UNITY_EDITOR
        /// <summary>Editor-only: drop caches so a re-import is picked up without a domain reload.</summary>
        public static void ClearCache()
        {
            _byName.Clear();
            _ordered.Clear();
        }
#endif
    }
}
