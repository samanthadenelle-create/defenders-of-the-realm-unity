// =============================================================================
// InventoryUIBuilder — UI construction for the inventory modal (split from HeroInventoryController).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Extracted exactly from the original for behavior preservation. All construction
// (root, chrome, tabs, footer, shared helpers) lives here. The controller coordinates
// by calling these. Light polish: consistent ElarionUiKit dark-wood + gold (Forge shop
// look) via the kit; Tech pack overrides for W/A where present in original.
// No functionality, layout or behavior changes.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;
using DeNelle.Village.Items;

namespace DeNelle.Village
{
    public sealed partial class HeroInventoryController : MonoBehaviour
    {
        // --- ROOT + CHROME (moved from original BuildRoot) ---
        private void BuildRoot()
        {
            _ui = new GameObject("HeroInventoryUI");

            var canvas = _ui.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31000;
            canvas.overrideSorting = true;

            var scaler = _ui.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            _ui.AddComponent<GraphicRaycaster>();

            ElarionUiKit.Scrim(_ui.transform, Close);

            var backdrop = AddImage(_ui.transform, "InvBackdrop", Vector2.zero, Vector2.one, new Color(0.02f, 0.015f, 0.012f, 0.94f));
            NoRaycast(backdrop);

            // Main dark wood panel
            // Use the neutral window frame, NOT PanelVendor — the inventory was showing the SAME
            // Merchant board as the shop ("the image of store"). Its grid already uses PanelInventory.
            var panel = ElarionUiKit.PanelFramed(_ui.transform, new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.97f),
                                                 deep: true, packSpriteName: RpgUiCatalog.PanelWindowDark);

            bool chrome = !DeNelle.Core.FeatureFlags.BlinkChrome;   // flag OFF = our dressing; flag ON = let the Blink Obsidian panel show clean
            var solidFill = AddImage(panel.transform, "InvSolidFill", new Vector2(0.025f, 0.02f), new Vector2(0.975f, 0.98f),
                                     new Color(0.05f, 0.055f, 0.06f, 0.985f));   // dark obsidian backing (always — matches EquipmentPanel)
            NoRaycast(solidFill);
            solidFill.transform.SetAsFirstSibling();

            // Warm glow accents
            var baseGlow = AddImage(panel.transform, "BaseEmberGlow", new Vector2(0.06f, 0.025f), new Vector2(0.94f, 0.20f),
                                    new Color(0.55f, 0.32f, 0.12f, chrome ? 0.22f : 0f));
            NoRaycast(baseGlow);

            if (chrome) AddRuneStrip(panel.transform, 0.965f, 0.992f);

            // Header
            AddLabelShadow(panel.transform, ElarionUi.CrestGlyph + "  INVENTORY", 0.918f, 0.958f,
                           GiltInk, ElarionUi.FontTitle, 0.05f, 0.80f, spacing: 6f);
            AddRule(panel.transform, 0.908f, 0.04f, 0.96f);

            // Close button (top right)
            var closeBtn = ElarionUiKit.ButtonPack(panel.transform, "X", ElarionUiKit.ButtonKind.Quiet,
                      new Vector2(0.904f, 0.928f), new Vector2(0.916f, 0.962f), Close,
                      packSpriteName: RpgUiCatalog.ButtonFrame);
            CreamLabel(closeBtn);

            // Left: Narrow portrait area to match mockup exactly - ornate gold frame with hero portrait, Lvl, name, colored bars, stats.
            // Matches the mockup's left panel width and style.
            var niche = ElarionUiKit.Niche(panel.transform, new Vector2(0.04f, 0.12f), new Vector2(0.26f, 0.78f));
            _paperDoll = AddImage(niche.transform, "PaperDollArea", new Vector2(0.02f, 0.01f), new Vector2(0.98f, 0.99f), new Color(0, 0, 0, 0));
            NoRaycast(_paperDoll);

            // Tabs above the grid on the right, gold-trimmed to match mockup.
            _tabsRoot = AddImage(panel.transform, "TabsRow", new Vector2(0.28f, 0.82f), new Vector2(0.95f, 0.90f), new Color(0, 0, 0, 0));
            NoRaycast(_tabsRoot);
            BuildTabs(_tabsRoot.transform);

            // Right: Large grid filling most of the screen (5 cols landscape, ornate frames from pack, items fit with margins, scroll for more).
            // No sidebar/detail panel in main layout to match the mockup's full grid view (selection highlights in grid, equip on tap).
            // Grid extends lower for the big area in the mockup.
            _gridRoot = ElarionUiKit.Well(panel.transform, new Vector2(0.28f, 0.18f), new Vector2(0.95f, 0.80f));
            DressPanel(_gridRoot, RpgUiCatalog.PanelInventory, keepWhite: true);

            BuildFooterBar(panel.transform);
        }

        // ── Footer bar (mockup #41 bottom) ---
        private void BuildFooterBar(Transform panel)
        {
            var tray = AddImage(panel, "FooterTray",
                                new Vector2(0.04f, 0.035f), new Vector2(0.96f, 0.100f), Track);
            AddInnerRim(tray, AccentSoft);
            AddRule(tray.transform, 0.97f, 0.02f, 0.98f);

            CreamLabel(ElarionUiKit.ButtonPack(tray.transform, "Sort", ElarionUiKit.ButtonKind.Quiet,
                      new Vector2(0.030f, 0.205f), new Vector2(0.18f, 0.82f),
                      () => { /* TODO owned-list re-sort */ }, packSpriteName: RpgUiCatalog.ButtonFrame));
            CreamLabel(ElarionUiKit.ButtonPack(tray.transform, "Filter", ElarionUiKit.ButtonKind.Quiet,
                      new Vector2(0.225f, 0.400f), new Vector2(0.18f, 0.82f),
                      () => { /* TODO owned-list filter */ }, packSpriteName: RpgUiCatalog.ButtonFrame));

            int coins = 0, crystals = 0;
            try
            {
                var s = DeNelle.Core.State.GameStateService.Instance;
                if (s != null && s.State != null)
                {
                    coins = s.State.Resources.Coins;
                    crystals = s.State.Resources.Crystals;
                }
            }
            catch (System.Exception ex)
            {
                // No silent failure (§12): a state read that throws leaves the footer at 0/0, but
                // it must be logged — never swallowed blind.
                FlowTrace.Warn("Inventory",
                    $"BuildFooterBar: resource read threw ({ex.GetType().Name}: {ex.Message}) — wallet shows 0.");
            }

            const float wEnd = 0.985f, wStart = 0.470f, wGap = 0.012f;
            float wW = (wEnd - wStart - wGap * 2f) / 3f;
            float wx = wStart;
            ResourceWell(tray.transform, "GoldWell", wx, wx + wW, "o " + coins, "GOLD", GiltInk); wx += wW + wGap;
            ResourceWell(tray.transform, "CrystalWell", wx, wx + wW, "* " + crystals, "CRYSTALS",
                         new Color(0.42f, 0.26f, 0.62f, 1f)); wx += wW + wGap;
            ResourceWell(tray.transform, "SkrWell", wx, wx + wW, "* SKR", "WALLET",
                         new Color(0.18f, 0.43f, 0.40f, 1f));
        }

        private void ResourceWell(Transform tray, string name, float x0, float x1,
                                  string value, string caps, Color valueColor)
        {
            var well = AddImage(tray, name, new Vector2(x0, 0.10f), new Vector2(x1, 0.90f), GlassDeep);
            AddInnerRim(well, new Color(valueColor.r, valueColor.g, valueColor.b, 0.55f));
            NoRaycast(AddImage(well.transform, "Glint", new Vector2(0.42f, 0.80f), new Vector2(0.58f, 0.96f),
                               new Color(valueColor.r, valueColor.g, valueColor.b, 0.85f)));
            AddLabel(well.transform, value, 0.40f, 0.96f, valueColor,
                     ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);
            AddLabel(well.transform, caps, 0.06f, 0.40f, InkMicro,
                     ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, spacing: 2f);
        }

        private static void CreamLabel(Button btn)
        {
            if (btn == null) return;
            var lbl = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (lbl == null) return;
            lbl.color = ElarionUi.Parchment;
            lbl.fontStyle = TMPro.FontStyles.Bold;
            lbl.outlineColor = new Color32(20, 12, 4, 235);
            lbl.outlineWidth = 0.22f;
            lbl.transform.SetAsLastSibling();
        }

        // ── TABS ---
        // The four item tabs PLUS a "Skills" pseudo-tab. Skills isn't a content category
        // (the tree is a full MVVM modal — HeroSkillTreePanelMvvm); tapping it OPENS that
        // panel via PanelRouter (PanelManager swaps the inventory out, one-modal-at-a-time).
        // Consistent with how the other tabs switch the right-pane content.
        private void BuildTabs(Transform host)
        {
            string[] names = { "Weapons", "Armor", "Accessories", "Consumables", "Skills" };
            // Skills carries a null Tab (handled specially below); the other indices map 1:1.
            Tab[] tabs = { Tab.Weapons, Tab.Armor, Tab.Outfits, Tab.Consumables, Tab.Weapons };
            const int skillsIndex = 4;
            float y0 = 0.06f, y1 = 0.94f;
            float gap = 0.012f;
            float w = (1f - gap * (names.Length - 1)) / names.Length;
            float x = 0f;
            for (int i = 0; i < names.Length; i++)
            {
                bool isSkills = i == skillsIndex;
                Tab t = tabs[i];
                bool sel = !isSkills && _tab == t;
                float cx = x + w * 0.5f;
                Color inactive = new Color(0.847f, 0.804f, 0.710f, 1f);
                Color bg = sel ? ElarionUi.GoldButton : inactive;
                System.Action onTap = isSkills ? (System.Action)OpenSkillTree : () => SelectTab(t);
                var btn = AddButton(host, names[i], new Vector2(cx, w * 0.5f), new Vector2(y0, y1),
                                    bg, onTap, sel ? ButtonKind.Gold : ButtonKind.Neutral);
                if (sel) 
                { 
                    DressButtonPack(btn);
                }
                else 
                { 
                    // no special for inactive here
                }

                // Completely redesigned elegant tabs for mobile RPG raid style game.
                // Strict use of Tech Profile tabs (P1/P2 fills) for all category tabs – ornate, clean, no action "Play" sprites or text leak ever.
                // RPG kit reserved for main panels, grid tiles, and CTAs for professional inventory aesthetic.
                // Active tab: gold-tinted P1 with glow + underline.
                // Icons from pack (Sword for Weapons, Profile for Armor, Healing for others).
                var pi = btn.targetGraphic as Image; 
                if (pi != null) 
                { 
                    Sprite tabBg = null;
                    if (sel) {
                        tabBg = Resources.Load<Sprite>("Tech hud elements/Sprites/Profile tabs/P1/fill.png");
                        if (tabBg == null) tabBg = Resources.Load<Sprite>("Tech hud elements/Sprites/Profile tabs/P1/bg.png");
                    } else {
                        tabBg = Resources.Load<Sprite>("Tech hud elements/Sprites/Profile tabs/P2/fill.png");
                        if (tabBg == null) tabBg = Resources.Load<Sprite>("Tech hud elements/Sprites/Profile tabs/P1/fill.png");
                    }
                    // Clean-build fallback (Tech pack gitignored): committed RpgUi ornate tab banner.
                    if (tabBg == null) tabBg = RpgUiCatalog.Get(RpgUiCatalog.RolePanel, RpgUiCatalog.PanelTab);
                    if (tabBg != null) {
                        pi.sprite = tabBg;
                        pi.type = Image.Type.Sliced;
                        pi.color = sel ? Color.white : new Color(0.75f, 0.7f, 0.6f, 1f);
                    } else {
                        pi.color = sel ? ElarionUi.GoldButton : inactive;
                        ApplyRounded(pi);
                    }
                }
                Sprite tabIcon = isSkills ? null : TabPackIcon(t);
                if (tabIcon != null)
                {
                    var ic = AddImage(btn.transform, "TabIcon",
                                      new Vector2(0.04f, 0.30f), new Vector2(0.30f, 0.92f), new Color(0, 0, 0, 0));
                    NoRaycast(ic);
                    var im = ic.GetComponent<Image>();
                    im.sprite = tabIcon; im.color = Color.white; im.type = Image.Type.Simple;
                    im.preserveAspect = true;
                }
                if (sel)
                {
                    var glow = AddImage(host, "TabGlow_" + names[i],
                                        new Vector2(cx - w * 0.5f - 0.006f, y0 - 0.06f),
                                        new Vector2(cx + w * 0.5f + 0.006f, y1 + 0.06f),
                                        new Color(ElarionUi.Gilt.r, ElarionUi.Gilt.g, ElarionUi.Gilt.b, 0.30f));
                    NoRaycast(glow);
                    glow.transform.SetSiblingIndex(btn.transform.GetSiblingIndex());
                }
                if (sel)
                {
                    var rule = new GameObject("TabUnderline", typeof(Image));
                    rule.transform.SetParent(btn.transform, false);
                    var rr = rule.GetComponent<RectTransform>();
                    rr.anchorMin = new Vector2(0.12f, 0f); rr.anchorMax = new Vector2(0.88f, 0f);
                    rr.pivot = new Vector2(0.5f, 0f);
                    rr.sizeDelta = new Vector2(0f, 3f);
                    rr.anchoredPosition = new Vector2(0f, -4f);
                    var ri = rule.GetComponent<Image>();
                    ri.color = GiltInk; ri.raycastTarget = false;
                }
                x += w + gap;
            }
        }

        // The "Skills" pseudo-tab: open the code-built MVVM skill tree (HeroSkillTreePanelMvvm).
        // Routes through PanelRouter so the inventory needs NO reference to the Talents panel type;
        // PanelManager swaps this modal out for the skill tree (one-panel-at-a-time). When the
        // panel isn't registered (e.g. no hero), Close the inventory and log — never silently nothing.
        private void OpenSkillTree()
        {
            if (DeNelle.Core.UI.PanelRouter.Open(DeNelle.Core.UI.PanelId.HeroSkillTree))
                return;
            // Not registered — close the inventory so the tap isn't a dead end, and report.
            Close();
            FlowTrace.Warn("UI", "Skills tab: HeroSkillTree panel not registered (no hero?) — nothing to open.");
        }

        private static Sprite TabPackIcon(Tab t)
        {
            try
            {
                Sprite sp;
                switch (t)
                {
                    case Tab.Weapons:     sp = Resources.Load<Sprite>("Tech hud elements/Sprites/Sword icons/Sword icons");
                                          return sp != null ? sp : RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconSword);
                    case Tab.Armor:       sp = Resources.Load<Sprite>("Tech hud elements/Sprites/Profile tabs/P1/fill.png");
                                          return sp != null ? sp : RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconShield);
                    case Tab.Outfits:     sp = Resources.Load<Sprite>("Tech hud elements/Sprites/Healing Tabs/H5");
                                          return sp != null ? sp : RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconHeart);
                    case Tab.Consumables: sp = Resources.Load<Sprite>("Tech hud elements/Sprites/GreenUielements/Icons/Icon 5");
                                          return sp != null ? sp : RpgUiCatalog.Get(RpgUiCatalog.RolePotion, RpgUiCatalog.PotionHealth);
                    default:              return null;
                }
            }
            catch (System.Exception ex)
            {
                // No silent failure (§12): a tab-icon load that throws falls back to no icon, but
                // it must be logged — never swallowed blind.
                FlowTrace.Warn("Inventory",
                    $"TabPackIcon: load threw for tab {t} ({ex.GetType().Name}: {ex.Message}) — tab shows no icon.");
                return null;
            }
        }

        // (Shared UI primitives Add*/Dress*/AddCircle*/Rarity*/glyphs/Has/Cap/Hero* live once in the main partial file.
        // High-level builder chrome (root/tabs/footer) live here as the UIBuilder concern.
        // This guarantees single definition for the merged partial class.)
    }
}