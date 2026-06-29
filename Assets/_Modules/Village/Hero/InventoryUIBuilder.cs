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
using DeNelle.Village.Hero;
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

            // SHARED Obsidian chrome (WO-554): black panel + gold trim + gold header +
            // the ONE standard Close button. Replaces the old backdrop + brown PanelFramed +
            // dark solidFill + ember glow + rune strip + per-panel "X".
            var panelChrome = ElarionUiKit.BuildObsidianPanel(_ui.transform, "INVENTORY",
                new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.97f),
                Close, headerX0: 0.05f, headerX1: 0.80f,
                frameName: RpgUiCatalog.FrameInventory);
            var panel = panelChrome.content;

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
            // WO-582 frame pass: the grid sits in the Blink frame's own dark well now, so the grid
            // root is TRANSPARENT (no grey Well sub-frame, no old PanelInventory gold-grid sprite that
            // double-framed the middle). Items render directly on the frame's central well.
            _gridRoot = AddImage(panel.transform, "GridRoot",
                                 new Vector2(0.30f, 0.16f), new Vector2(0.93f, 0.80f), new Color(0f, 0f, 0f, 0f));
            NoRaycast(_gridRoot);

            // WO-585 — selection DETAIL strip: a thin host between the grid bottom (0.16) and the
            // footer top (0.10). Empty/transparent until an item is tapped; RebuildSidebar then drops
            // the selected item's name + stats + an explicit Equip/Use CTA + the equip Status line
            // into it, so a tap has a visible, separate-from-equip response (was the inert feel).
            _sidebarRoot = AddImage(panel.transform, "DetailStrip",
                                    new Vector2(0.30f, 0.103f), new Vector2(0.93f, 0.156f), new Color(0f, 0f, 0f, 0f));
            NoRaycast(_sidebarRoot);

            BuildFooterBar(panel.transform);
        }

        // ── Footer bar (mockup #41 bottom) ---
        private void BuildFooterBar(Transform panel)
        {
            // WO-582 frame pass: the footer sits on the Blink frame's ornate base now, so the tray is
            // a transparent layout host (no Track fill / rim / rule that boxed the bottom over the art).
            var tray = AddImage(panel, "FooterTray",
                                new Vector2(0.30f, 0.035f), new Vector2(0.93f, 0.100f), new Color(0f, 0f, 0f, 0f));
            NoRaycast(tray);

            // WO-565: the Sort + Filter buttons were wired to EMPTY lambdas — visible controls
            // that silently did nothing. HIDDEN rather than ship a half-feature: category Filter
            // is already provided by the tab row (Weapons/Armor/Accessories/Consumables), and a
            // real Sort needs VM-level ordering + grid re-bind (non-trivial). Re-add here only
            // once InventoryVM exposes a sort/filter the grid can project. The wallet wells below
            // keep their right-aligned positions; the freed left of the footer simply stays clear.

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

        // ── Live 3D dressed-hero preview in the paper-doll niche ─────────────────────
        // REUSE (no new system): the SAME proven HeroPreviewViewer the Character / Gear screen
        // (EquipmentPanel.BuildPreviewWidget + BeginOrRetargetPreview) drives — it renders the
        // active hero, with the equipped weapon / shield / armor tier, into a RenderTexture a UI
        // RawImage shows. The viewer is PERSISTED across paper-doll rebuilds (the RT is reused and
        // RefreshGear mirrors equip changes) and disposed on Close (DisposeHeroPreview) so there is
        // no RenderTexture leak. Null-safe per §12: any failure leaves the niche to the 2D portrait/
        // crest fallback — no crash, no error spam (one FlowTrace line, never a silent swallow).
        private DeNelle.Village.Hero.HeroPreviewViewer _heroPreview;
        private RawImage _paperDollPreview;

        // Mount the live hero into <paramref name="parent"/> via a child RawImage. Returns true when
        // a live preview is showing; false when there is no hero body or the viewer can't build — the
        // caller then falls back to the static 2D portrait. Called from RebuildPaperDoll (first call
        // happens on Open via Bind->Render, so the preview begins when the panel opens).
        private bool TryMountHeroPreview(Transform parent)
        {
            if (parent == null) return false;
            try
            {
                var body = ResolvePreviewBody();
                if (body == null)
                {
                    FlowTrace.Step("Inventory", "Paper-doll: no hero body — using 2D portrait fallback.");
                    return false;
                }

                string weaponId  = _loadout != null && _loadout.EquippedWeapon  != null ? _loadout.EquippedWeapon.id  : null;
                string offHandId = _loadout != null && _loadout.EquippedOffHand != null ? _loadout.EquippedOffHand.id : null;
                int    armorTier = _loadout != null ? GearLoadout.ArmorVisualTier(_loadout.EquippedArmor) : 0;

                if (_heroPreview == null)
                {
                    _heroPreview = new DeNelle.Village.Hero.HeroPreviewViewer();
                    if (!_heroPreview.Begin(body, textureSize: 512, weaponId: weaponId,
                                            offHandId: offHandId, armorTier: armorTier))
                    {
                        DisposeHeroPreview();
                        return false;
                    }
                    _heroPreview.SetRotation(18f);   // same 3/4 hero angle as the Gear screen
                }
                else
                {
                    // Persisted across rebuilds: mirror the latest equipped look (weapon+shield+armor),
                    // reusing the existing RenderTexture — no re-Begin, no RT churn.
                    _heroPreview.RefreshGear(weaponId, offHandId, armorTier);
                }

                if (!_heroPreview.IsValid || _heroPreview.Texture == null)
                {
                    DisposeHeroPreview();
                    return false;
                }

                var imgGo = new GameObject("HeroPreviewRawImage", typeof(RectTransform), typeof(RawImage));
                imgGo.transform.SetParent(parent, false);
                var rt = imgGo.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.05f, 0.05f);
                rt.anchorMax = new Vector2(0.95f, 0.95f);
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                _paperDollPreview = imgGo.GetComponent<RawImage>();
                _paperDollPreview.raycastTarget = false;
                _paperDollPreview.color = Color.white;
                _paperDollPreview.texture = _heroPreview.Texture;
                return true;
            }
            catch (System.Exception ex)
            {
                // No silent failure (§12): log once, drop to the 2D portrait, never crash the modal.
                FlowTrace.Warn("Inventory",
                    $"TryMountHeroPreview threw ({ex.GetType().Name}: {ex.Message}) — using 2D portrait fallback.");
                DisposeHeroPreview();
                return false;
            }
        }

        // The actor body to clone for the preview — mirrors EquipmentPanel.ResolveBody: the live
        // hero's "HeroBody" child (the visual rig) or the tagged root itself, falling back to the
        // resolved loadout's GameObject. Null when no hero is present (caller skips the preview).
        private GameObject ResolvePreviewBody()
        {
            var hero = GameObject.FindWithTag("Player");
            if (hero == null) hero = SafeFindByTag("HeroTarget");
            if (hero != null)
            {
                var t = hero.transform.Find("HeroBody");
                return t != null ? t.gameObject : hero;
            }
            return _loadout != null ? _loadout.gameObject : null;
        }

        // Free the preview rig + its RenderTexture. Called on Close / OnDestroy (and on any build
        // failure) so the off-screen clone + RT never leak. Safe to call repeatedly.
        private void DisposeHeroPreview()
        {
            _heroPreview?.Dispose();
            _heroPreview = null;
            _paperDollPreview = null;
        }

        // (Shared UI primitives Add*/Dress*/AddCircle*/Rarity*/glyphs/Has/Cap/Hero* live once in the main partial file.
        // High-level builder chrome (root/tabs/footer) live here as the UIBuilder concern.
        // This guarantees single definition for the merged partial class.)
    }
}