// =============================================================================
// ShopPanel — vendor / gear shop VIEW (WO-431 MVVM slice). A DUMB SKIN: it builds
// the presentation (dark-glass panels + gold-rune frames + RPG-pack art, the SHARED
// ElarionUiKit) and BINDS a ShopVM. ALL state/logic (economy reads, catalog->row
// building, buy/sell/equip, vendor-gold, affordability, never-empty fallback, stock
// contract) now lives in ShopVM — the View never reads game state or calls a service.
// -----------------------------------------------------------------------------
// Code-built (no UXML), screen-space overlay, large touch targets for mobile.
// Opened via Yarn "OpenShop" (or "OpenShop armorer") from NPCCommandBridge:
//   Open(ctx, name) constructs a ShopVM (injecting EconomyService.Instance as IEconomy)
//   and Bind()s it; Render() repaints from vm.* on every vm.Changed.
//
// PRESENTATION: every surface is assembled from ElarionUiKit sourcing the canonical
// ElarionUi dark-glass + gold palette. The scroll list keeps its VerticalLayoutGroup +
// ContentSizeFitter + per-row LayoutElement rendering mechanism (the fix that cured the
// "no stock" bug) — guarded by ShopPanelRowRenderTests. Icons are resolved from the
// VM's IconRole/IconName KEYS via ItemIconCatalog (presentation, not a state-pull).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Core.UI.Mvvm;

namespace DeNelle.Village.Hero
{
    public sealed class ShopPanel : MonoBehaviour, IPanelView
    {
        // Active-tab tint vs. inactive (multiply tint over the kit's button image).
        private static readonly Color TabSelectedTint = new Color(1.15f, 1.10f, 0.92f, 1f);
        private static readonly Color TabRestTint     = new Color(0.58f, 0.55f, 0.50f, 1f);

        private ShopVM _vm;

        private GameObject _ui;
        private string _vendorContext;
        private GearLoadout _activeLoadout;
        private GameObject _contentRoot;
        private GameObject _tabBar;
        private RectTransform _scrollContent;
        private TMPro.TextMeshProUGUI _statusText;
        private TMPro.TextMeshProUGUI _ecoText;
        private TMPro.TextMeshProUGUI _actionLabel;

        // RIGHT details pane widgets.
        private Image _detailsIcon;
        private TMPro.TextMeshProUGUI _detailsName;
        private TMPro.TextMeshProUGUI _detailsDesc;
        private TMPro.TextMeshProUGUI _detailsStats;
        private TMPro.TextMeshProUGUI _detailsCost;

        private GameObject _filterBar;

        /// <summary>The store's ACTUAL built stock (id + category), delegated to the VM — for the
        /// AutoPilot bot to assert against <see cref="VendorStockContract"/>.</summary>
        public IReadOnlyList<(string id, GearKind kind)> CurrentStock =>
            _vm != null ? _vm.CurrentStock : (IReadOnlyList<(string id, GearKind kind)>)System.Array.Empty<(string, GearKind)>();

        /// <summary>The vendor context this panel last opened/built for.</summary>
        public string VendorContext => _vendorContext;

        // ── Open: build the chrome, construct + bind the ViewModel ────────────────

        public void Open(string vendorContext = null, string displayName = null)
        {
            Close();

            _vendorContext = vendorContext == null ? "" : vendorContext;
            ResolveActiveHero();

            BuildChrome(displayName);

            // Construct the ViewModel: inject EconomyService.Instance as the IEconomy seam, the
            // active hero loadout as the equip target, and the View's Close as the dismiss command.
            var economy = EconomyService.Instance;   // resolved at the open-site, injected into the pure VM
            _vm = new ShopVM(_vendorContext, economy, displayName,
                             new LoadoutEquipTarget(this),
                             onClose: Close,
                             onEquipRefreshHero: _ => ResolveActiveHero());

            Bind(_vm);

            Debug.Log($"[ShopPanel] Opened for vendor '{_vendorContext}'. Bound ShopVM (MVVM).");
        }

        // ── IPanelView ────────────────────────────────────────────────────────────

        public void Bind(IPanelViewModel vm)
        {
            Unbind();
            _vm = vm as ShopVM;
            if (_vm == null) return;
            _vm.Changed += Render;
            Render();
        }

        public void Unbind()
        {
            if (_vm != null) _vm.Changed -= Render;
        }

        // ── Render: repaint widgets from vm.* ONLY ────────────────────────────────

        private void Render()
        {
            if (_vm == null) return;

            if (_headerLabel != null) _headerLabel.text = _vm.Title;

            if (_ecoText != null)
                _ecoText.text = $"Gold: {_vm.Coins}   Wood: {_vm.Wood}   Iron: {_vm.Iron}   Food: {_vm.Food}   Crystals: {_vm.Crystals}";

            if (_statusText != null) _statusText.text = _vm.Status;
            if (_actionLabel != null) _actionLabel.text = _vm.ActionLabel;

            HighlightTab(_vm.Mode == ShopMode.Buy ? "BUY" : _vm.Mode == ShopMode.Equip ? "EQUIP" : "SELL");

            if (_filterBar != null) _filterBar.SetActive(_vm.FilterBarVisible);
            if (_vm.FilterBarVisible) HighlightFilter();

            RebuildList();
            RenderDetails();
        }

        private void RebuildList()
        {
            ClearContent();

            // EQUIP shows the "Current:" header line as row 0 (mirrors the old ShowEquip layout).
            int extra = _vm.Mode == ShopMode.Equip ? 1 : 0;
            var listRoot = BuildScrollContent(_vm.Items.Count + extra);

            if (_vm.Mode == ShopMode.Equip)
                CreateLabelRow(listRoot, _vm.EquipCurrentLine());

            foreach (var item in _vm.Items)
                CreateRow(listRoot, item);

            FinalizeScroll();
        }

        private void RenderDetails()
        {
            var sel = _vm.Selected;
            if (sel == null)
            {
                if (_detailsName != null) _detailsName.text = "Select an item";
                if (_detailsDesc != null) _detailsDesc.text = "Tap an item to inspect it.";
                if (_detailsStats != null) _detailsStats.text = "";
                if (_detailsCost != null) _detailsCost.text = "";
                if (_detailsIcon != null) _detailsIcon.enabled = false;
                return;
            }
            var d = sel.Value;
            if (_detailsName != null) _detailsName.text = d.Name;
            if (_detailsDesc != null) _detailsDesc.text = d.Description;
            if (_detailsStats != null) _detailsStats.text = d.Stats;
            if (_detailsCost != null) _detailsCost.text = d.CostString;
            if (_detailsIcon != null)
            {
                var icon = ResolveIcon(d.IconRole, d.IconName);
                _detailsIcon.sprite = icon;
                _detailsIcon.enabled = icon != null;
            }
        }

        // Resolve a display sprite from the VM-supplied KEYS (role + id). This is presentation —
        // mapping a key to art, not pulling state. Mirrors the old per-row icon resolution.
        private Sprite ResolveIcon(string role, string id)
        {
            switch (role)
            {
                case ShopVM.IconRoleWeapon:
                {
                    var w = GearCatalog.FindWeapon(id);
                    var s = ItemIconCatalog.ForWeapon(w);
                    return s != null ? s : RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconSword);
                }
                case ShopVM.IconRoleArmor:
                {
                    var a = GearCatalog.FindArmor(id);
                    var s = ItemIconCatalog.ForArmor(a);
                    return s != null ? s : RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconShield);
                }
                case ShopVM.IconRolePotion:
                {
                    var s = RpgUiCatalog.Get(RpgUiCatalog.RolePotion,
                        id != null && id.Contains("mana") ? RpgUiCatalog.PotionMana : RpgUiCatalog.PotionHealth);
                    return s != null ? s : ItemIconCatalog.ForConsumable(id, id);
                }
            }
            return null;
        }

        // ── Chrome build (presentation only; unchanged behavior) ──────────────────

        private void BuildChrome(string displayName)
        {
            _ui = ElarionUiKit.BuildModalCanvas("ShopPanelUI", 31000);
            var shopCanvas = _ui.GetComponent<Canvas>();
            if (shopCanvas != null) shopCanvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: () => _vm?.Close());

            var backdrop = ElarionUiKit.AddImage(_ui.transform, "ShopBackdrop",
                Vector2.zero, Vector2.one, new Color(0.02f, 0.015f, 0.012f, 0.94f), rounded: false);
            var bdImg = backdrop.GetComponent<Image>();
            if (bdImg != null) bdImg.raycastTarget = false;

            var panelGo = ElarionUiKit.PanelFramed(_ui.transform, new Vector2(0.14f, 0.07f), new Vector2(0.86f, 0.93f),
                                                   deep: true, packSpriteName: RpgUiCatalog.PanelVendor);
            var panel = panelGo.transform;

            string vcLow = (_vendorContext ?? "").ToLowerInvariant();
            Color fillColor, glowColor;
            if (vcLow.Contains("forge") || vcLow.Contains("blacksmith"))
            { fillColor = new Color(0.11f, 0.055f, 0.032f, 0.985f); glowColor = new Color(0.55f, 0.22f, 0.05f, 0.22f); }
            else if (vcLow.Contains("armor"))
            { fillColor = new Color(0.055f, 0.065f, 0.085f, 0.985f); glowColor = new Color(0.30f, 0.45f, 0.65f, 0.18f); }
            else if (vcLow.Contains("jewel"))
            { fillColor = new Color(0.085f, 0.055f, 0.10f, 0.985f); glowColor = new Color(0.55f, 0.30f, 0.65f, 0.18f); }
            else if (vcLow.Contains("arcane") || vcLow.Contains("magic") || vcLow.Contains("tower"))
            { fillColor = new Color(0.06f, 0.05f, 0.11f, 0.985f); glowColor = new Color(0.35f, 0.30f, 0.75f, 0.20f); }
            else if (vcLow.Contains("market") || vcLow.Contains("granary") || vcLow.Contains("farm"))
            { fillColor = new Color(0.08f, 0.07f, 0.04f, 0.985f); glowColor = new Color(0.45f, 0.40f, 0.12f, 0.18f); }
            else if (vcLow.Contains("lumber"))
            { fillColor = new Color(0.055f, 0.07f, 0.045f, 0f); glowColor = new Color(0.25f, 0.42f, 0.18f, 0.18f); }
            else
            { fillColor = new Color(0.07f, 0.055f, 0.042f, 0f); glowColor = new Color(0.45f, 0.35f, 0.18f, 0.16f); }

            if (DeNelle.Core.FeatureFlags.BlinkChrome)
            { fillColor.a = 0f; glowColor.a = 0f; }

            var solidFill = ElarionUiKit.AddImage(panel, "ShopSolidFill",
                new Vector2(0.025f, 0.02f), new Vector2(0.975f, 0.98f), fillColor);
            var sfImg = solidFill.GetComponent<Image>();
            if (sfImg != null) sfImg.raycastTarget = false;
            solidFill.transform.SetAsFirstSibling();

            var glow = ElarionUiKit.AddImage(panel, "VendorGlow",
                new Vector2(0.05f, 0.015f), new Vector2(0.95f, 0.17f), glowColor, rounded: false);
            var glowImg = glow.GetComponent<Image>();
            if (glowImg != null) glowImg.raycastTarget = false;
            glow.transform.SetSiblingIndex(1);

            // Header text comes from the VM (vm.Title) — set after Bind in Render via a header label.
            _headerLabel = ElarionUiKit.Header(panel, "Vendor Wares", x0: 0.04f, x1: 0.96f, y0: 0.9f, y1: 0.97f);

            CreateEconomyReadout(panel);

            var tabBar = new GameObject("TabBar", typeof(RectTransform));
            tabBar.transform.SetParent(panel, false);
            var tbRect = tabBar.GetComponent<RectTransform>();
            tbRect.anchorMin = new Vector2(0.02f, 0.78f);
            tbRect.anchorMax = new Vector2(0.98f, 0.86f);
            tbRect.offsetMin = Vector2.zero;
            tbRect.offsetMax = Vector2.zero;

            CreateTabButton(tabBar.transform, "BUY", new Vector2(0.02f, 0.33f), () => _vm?.SetMode(ShopMode.Buy));
            CreateTabButton(tabBar.transform, "EQUIP", new Vector2(0.35f, 0.65f), () => _vm?.SetMode(ShopMode.Equip));
            CreateTabButton(tabBar.transform, "SELL", new Vector2(0.67f, 0.98f), () => _vm?.SetMode(ShopMode.Sell));
            _tabBar = tabBar;

            _contentRoot = new GameObject("Content", typeof(RectTransform));
            _contentRoot.transform.SetParent(panel, false);
            var cr = _contentRoot.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0.02f, 0.13f);
            cr.anchorMax = new Vector2(0.62f, 0.71f);
            cr.offsetMin = Vector2.zero;
            cr.offsetMax = Vector2.zero;

            BuildFilterBar(panel);
            BuildDetailsPane(panel);

            var purchaseBtn = ElarionUiKit.ButtonPack(panel, "Purchase", ElarionUiKit.ButtonKind.Gold,
                new Vector2(0.34f, 0.03f), new Vector2(0.60f, 0.105f),
                () => { if (_vm != null) { if (_vm.Mode == ShopMode.Sell) _vm.Sell(); else _vm.Buy(); } },
                packSpriteName: RpgUiCatalog.ButtonGold);
            _actionLabel = purchaseBtn != null ? purchaseBtn.GetComponentInChildren<TMPro.TextMeshProUGUI>() : null;
            if (_actionLabel != null)
            {
                _actionLabel.color = ElarionUi.Parchment; _actionLabel.fontStyle = TMPro.FontStyles.Bold;
                _actionLabel.outlineColor = new Color32(20, 12, 4, 235); _actionLabel.outlineWidth = 0.22f;
                _actionLabel.transform.SetAsLastSibling();
            }
            var closeBtn = ElarionUiKit.ButtonPack(panel, "Close", ElarionUiKit.ButtonKind.Quiet,
                new Vector2(0.06f, 0.03f), new Vector2(0.32f, 0.105f), () => _vm?.Close(),
                packSpriteName: RpgUiCatalog.ButtonFrame);
            var closeLbl = closeBtn != null ? closeBtn.GetComponentInChildren<TMPro.TextMeshProUGUI>() : null;
            if (closeLbl != null)
            {
                closeLbl.color = ElarionUi.Parchment; closeLbl.fontStyle = TMPro.FontStyles.Bold;
                closeLbl.outlineColor = new Color32(20, 12, 4, 235); closeLbl.outlineWidth = 0.22f;
                closeLbl.transform.SetAsLastSibling();
            }

            var statusGo = new GameObject("Status", typeof(TMPro.TextMeshProUGUI));
            statusGo.transform.SetParent(panel, false);
            var sRect = statusGo.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0.64f, 0.035f);
            sRect.anchorMax = new Vector2(0.98f, 0.095f);
            sRect.offsetMin = Vector2.zero;
            sRect.offsetMax = Vector2.zero;
            _statusText = statusGo.GetComponent<TMPro.TextMeshProUGUI>();
            _statusText.fontSize = ElarionUi.FontLabel;
            _statusText.color = ElarionUi.ParchmentDim;
            _statusText.alignment = TMPro.TextAlignmentOptions.Center;
            _statusText.raycastTarget = false;
        }

        private TMPro.TextMeshProUGUI _headerLabel;

        private void BuildFilterBar(Transform panel)
        {
            var bar = new GameObject("FilterBar", typeof(RectTransform));
            bar.transform.SetParent(panel, false);
            var br = bar.GetComponent<RectTransform>();
            br.anchorMin = new Vector2(0.02f, 0.715f);
            br.anchorMax = new Vector2(0.62f, 0.76f);
            br.offsetMin = Vector2.zero;
            br.offsetMax = Vector2.zero;
            _filterBar = bar;

            GearKind all = GearKind.Weapon | GearKind.Armor | GearKind.Potion;
            CreateFilterButton(bar.transform, "All",     new Vector2(0.01f, 0.245f), all);
            CreateFilterButton(bar.transform, "Weapons", new Vector2(0.255f, 0.49f), GearKind.Weapon);
            CreateFilterButton(bar.transform, "Armor",   new Vector2(0.505f, 0.745f), GearKind.Armor);
            CreateFilterButton(bar.transform, "Potions", new Vector2(0.755f, 0.99f), GearKind.Potion);
        }

        private void CreateFilterButton(Transform parent, string label, Vector2 anchorX, GearKind kind)
        {
            ElarionUiKit.ButtonPack(parent, label, ElarionUiKit.ButtonKind.Quiet,
                new Vector2(anchorX.x, 0.05f), new Vector2(anchorX.y, 0.95f),
                () => _vm?.SetFilter(kind),
                packSpriteName: RpgUiCatalog.ButtonFrame);
        }

        private void HighlightFilter()
        {
            if (_filterBar == null || _vm == null) return;
            GearKind all = GearKind.Weapon | GearKind.Armor | GearKind.Potion;
            var f = _vm.BuyFilter;
            string active = f == GearKind.Weapon ? "Btn_Weapons"
                          : f == GearKind.Armor  ? "Btn_Armor"
                          : f == GearKind.Potion ? "Btn_Potions"
                          : "Btn_All";
            if (f == all) active = "Btn_All";
            foreach (Transform child in _filterBar.transform)
            {
                if (child == null) continue;
                var img = child.GetComponent<Image>();
                if (img != null) img.color = child.name == active ? TabSelectedTint : TabRestTint;
            }
        }

        private void BuildDetailsPane(Transform panel)
        {
            var pane = new GameObject("DetailsPane", typeof(RectTransform));
            pane.transform.SetParent(panel, false);
            var pr = pane.GetComponent<RectTransform>();
            pr.anchorMin = new Vector2(0.63f, 0.12f);
            pr.anchorMax = new Vector2(0.985f, 0.76f);
            pr.offsetMin = Vector2.zero;
            pr.offsetMax = Vector2.zero;

            var frameC = new GameObject("PortraitFrame", typeof(RectTransform));
            frameC.transform.SetParent(pane.transform, false);
            var fc = frameC.GetComponent<RectTransform>();
            fc.anchorMin = new Vector2(0f, 0.18f); fc.anchorMax = new Vector2(1f, 1f);
            fc.offsetMin = Vector2.zero; fc.offsetMax = Vector2.zero;

            var frameSprite = RpgUiCatalog.Get(RpgUiCatalog.RolePanel, RpgUiCatalog.PanelPortrait);
            if (frameSprite != null)
            {
                var fImg = frameC.AddComponent<Image>();
                fImg.sprite = frameSprite; fImg.color = Color.white; fImg.type = Image.Type.Simple;
                fImg.preserveAspect = false;
                fImg.raycastTarget = false;
            }
            else
            {
                var well = ElarionUiKit.Well(frameC.transform, Vector2.zero, Vector2.one);
                var wImg = well.GetComponent<Image>(); if (wImg != null) wImg.raycastTarget = false;
            }

            var iconGo = ElarionUiKit.AddImage(frameC.transform, "DetailIcon",
                new Vector2(0.21f, 0.45f), new Vector2(0.79f, 0.80f), Color.white, rounded: false);
            _detailsIcon = iconGo.GetComponent<Image>();
            _detailsIcon.preserveAspect = true;
            _detailsIcon.raycastTarget = false;
            _detailsIcon.enabled = false;

            _detailsName = ElarionUiKit.Label(frameC.transform, "Select an item", 0.285f, 0.375f,
                ElarionUi.Gilt, ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center, 0.20f, 0.80f, bold: true);
            _detailsName.raycastTarget = false;

            _detailsCost = ElarionUiKit.Label(frameC.transform, "", 0.245f, 0.29f,
                ElarionUi.Gilt, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.20f, 0.80f, bold: true);
            _detailsCost.raycastTarget = false;

            _detailsStats = ElarionUiKit.Label(frameC.transform, "", 0.115f, 0.235f,
                ElarionUi.Affordable, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.20f, 0.80f, bold: true);
            _detailsStats.raycastTarget = false;

            _detailsDesc = ElarionUiKit.Label(pane.transform, "Tap an item to inspect it.", 0.02f, 0.31f,
                ElarionUi.ParchmentDim, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Top, 0.06f, 0.94f);
            _detailsDesc.enableWordWrapping = true;
            _detailsDesc.raycastTarget = false;
        }

        private void CreateEconomyReadout(Transform parent)
        {
            var go = new GameObject("EcoReadout", typeof(TMPro.TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.02f, 0.82f);
            r.anchorMax = new Vector2(0.98f, 0.87f);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            var t = go.GetComponent<TMPro.TextMeshProUGUI>();
            t.fontSize = ElarionUi.FontLabel;
            t.color = ElarionUi.Gilt;
            t.alignment = TMPro.TextAlignmentOptions.Center;
            t.raycastTarget = false;
            _ecoText = t;
        }

        private void CreateTabButton(Transform parent, string label, Vector2 anchorX, System.Action onClick)
        {
            var btn = ElarionUiKit.ButtonPack(parent, label, ElarionUiKit.ButtonKind.Gold,
                                    new Vector2(anchorX.x, 0.05f), new Vector2(anchorX.y, 0.95f), onClick,
                                    packSpriteName: RpgUiCatalog.ButtonFrame);
            var tab = btn != null ? btn.GetComponentInChildren<TMPro.TextMeshProUGUI>() : null;
            if (tab != null)
            {
                tab.color = ElarionUi.Parchment;
                tab.fontStyle = TMPro.FontStyles.Bold;
                tab.outlineColor = new Color32(20, 12, 4, 235);
                tab.outlineWidth = 0.22f;
                tab.transform.SetAsLastSibling();
            }
        }

        private void HighlightTab(string activeLabel)
        {
            if (_tabBar == null) return;
            foreach (Transform child in _tabBar.transform)
            {
                if (child == null) continue;
                bool isActive = child.name == "Btn_" + activeLabel;
                var img = child.GetComponent<Image>();
                if (img != null) img.color = isActive ? TabSelectedTint : TabRestTint;
            }
        }

        // ── Scroll list (rendering mechanism — UNCHANGED, guarded by ShopPanelRowRenderTests) ──

        private const float RowHeightPx = 58f;
        private const float RowGapPx    = 3f;

        private Transform BuildScrollContent(int rowCount)
        {
            var well = ElarionUiKit.Well(_contentRoot.transform, Vector2.zero, Vector2.one);
            var wImg = well.GetComponent<Image>();
            if (wImg != null)
            {
                wImg.raycastTarget = false;
                // BlinkChrome ON: neutralize the shared well so the Blink panel shows through
                // behind the per-item slot plates (Blink uses no shared well). Object kept for
                // layout (alpha-0), same technique as the *SolidFill neutralize. Flag OFF → unchanged.
                if (DeNelle.Core.FeatureFlags.BlinkChrome)
                {
                    var c = wImg.color; c.a = 0f; wImg.color = c;
                }
            }

            var viewport = new GameObject("Viewport", typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(_contentRoot.transform, false);
            var vr = viewport.GetComponent<RectTransform>();
            vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one;
            vr.offsetMin = Vector2.zero; vr.offsetMax = Vector2.zero;
            var vImg = viewport.GetComponent<Image>();
            vImg.color = new Color(0f, 0f, 0f, 0.001f);

            var content = new GameObject("ScrollContent", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var cr = content.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0f, 1f);
            cr.anchorMax = new Vector2(1f, 1f);
            cr.pivot = new Vector2(0.5f, 1f);
            cr.anchoredPosition = Vector2.zero;
            cr.sizeDelta = new Vector2(0f, 0f);

            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.spacing = RowGapPx;
            vlg.padding = new RectOffset(3, 3, 3, 3);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.GetComponent<ScrollRect>();
            scroll.viewport = vr;
            scroll.content = cr;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25f;

            _scrollContent = cr;
            return content.transform;
        }

        private void FinalizeScroll()
        {
            if (_scrollContent == null) return;
            Canvas.ForceUpdateCanvases();
            var contentArea = _contentRoot != null ? _contentRoot.transform as RectTransform : null;
            if (contentArea != null) LayoutRebuilder.ForceRebuildLayoutImmediate(contentArea);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollContent);
        }

        // ── Row plate dressing (flag-gated, sprite-first) ─────────────────────────
        // BlinkChrome ON + slot plate present → dress the row Image with the Blink
        // per-item slot plate (9-sliced, white). Flag OFF (or plate missing) → the
        // exact current Cell tint + procedural rounded look. Single gated choice point
        // shared by CreateRow + CreateEquipRow (binding-map §3: one slot recipe, reused).
        private static void DressRowPlate(Image rowImg)
        {
            if (rowImg == null) return;
            if (DeNelle.Core.FeatureFlags.BlinkChrome)
            {
                var plate = RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, RpgUiCatalog.SlotItem);
                if (plate != null)
                {
                    rowImg.sprite = plate;
                    rowImg.type   = Image.Type.Sliced;
                    rowImg.color  = Color.white;
                    return;
                }
            }
            // Fallback (flag OFF, or plate not imported): the original look, verbatim.
            rowImg.color = ElarionUiKit.Cell;
            ElarionUiKit.ApplyRounded(rowImg);
        }

        // ── Row factory (presentation; data comes from the bound ItemVM) ──────────

        private void CreateRow(Transform parent, ItemVM item)
        {
            if (_vm != null && _vm.Mode == ShopMode.Equip) { CreateEquipRow(parent, item); return; }

            bool isSell = _vm != null && _vm.Mode == ShopMode.Sell;
            string prefix = isSell ? "SellRow_" : "BuyRow_";
            var row = new GameObject(prefix + item.Name, typeof(Image), typeof(Button), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            var le = row.GetComponent<LayoutElement>();
            le.preferredHeight = RowHeightPx;
            le.minHeight = RowHeightPx;
            var rowImg = row.GetComponent<Image>();
            DressRowPlate(rowImg);
            var rowBtn = row.GetComponent<Button>();
            rowBtn.targetGraphic = rowImg;
            ElarionUiKit.StyleButtonColors(rowBtn);
            string id = item.Id;
            rowBtn.onClick.AddListener(() => _vm?.Select(id));

            if (!isSell)
            {
                var viewBtn = ElarionUiKit.ButtonPack(row.transform, "View", ElarionUiKit.ButtonKind.Quiet,
                    new Vector2(0.02f, 0.14f), new Vector2(0.18f, 0.86f),
                    () => _vm?.Select(id),
                    packSpriteName: RpgUiCatalog.ButtonFrame);
                if (viewBtn != null)
                {
                    var vLbl = viewBtn.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                    if (vLbl != null) { vLbl.color = ElarionUi.Parchment; vLbl.fontStyle = TMPro.FontStyles.Bold; }
                }
            }

            float nameX0 = isSell ? 0.04f : 0.20f;
            ElarionUiKit.Label(row.transform, item.Name, 0.15f, 0.85f, ElarionUi.Parchment,
                ElarionUi.FontBody, TMPro.TextAlignmentOptions.Left, nameX0, isSell ? 0.55f : 0.5f);

            // Affordability colour comes from the VM (item.Affordable) — the View only maps bool->colour.
            string priceText = isSell ? "+" + PriceString(item) : PriceString(item);
            Color priceColor = isSell ? ElarionUi.Affordable
                             : (item.Affordable ? ElarionUi.Affordable : ElarionUi.Danger);
            float px0 = isSell ? 0.55f : 0.5f;
            float px1 = 0.72f;
            ElarionUiKit.Label(row.transform, priceText, 0.15f, 0.85f, priceColor,
                ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Left, px0, px1, bold: true);
        }

        // Render the price text from the item's Price (the VM denominates everything in gold for
        // shop rows; Free when 0). Matches the old CostString output for a coins-only cost.
        private static string PriceString(ItemVM item)
        {
            return item.Price > 0 ? item.Price + " Gold" : "Free";
        }

        private void CreateLabel(Transform parent, string txt, float y)
        {
            ElarionUiKit.Label(parent, txt, y - 0.06f, y, ElarionUi.ParchmentDim,
                ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Left, 0.02f, 0.98f);
        }

        private void CreateLabelRow(Transform parent, string txt)
        {
            var go = new GameObject("LabelRow", typeof(TMPro.TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = RowHeightPx * 0.5f;
            le.minHeight = RowHeightPx * 0.5f;
            var t = go.GetComponent<TMPro.TextMeshProUGUI>();
            t.text = txt;
            t.fontSize = ElarionUi.FontLabel;
            t.color = ElarionUi.ParchmentDim;
            t.alignment = TMPro.TextAlignmentOptions.Left;
            t.raycastTarget = false;
        }

        private void CreateEquipRow(Transform parent, ItemVM item)
        {
            var row = new GameObject("EquipRow_" + item.Id, typeof(Image), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            var le = row.GetComponent<LayoutElement>();
            le.preferredHeight = RowHeightPx;
            le.minHeight = RowHeightPx;
            var rowImg = row.GetComponent<Image>();
            DressRowPlate(rowImg);

            ElarionUiKit.Label(row.transform, item.Name, 0.15f, 0.85f, ElarionUi.Parchment,
                ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Left, 0.04f, 0.62f);

            string id = item.Id;
            ElarionUiKit.ButtonPack(row.transform, "EQUIP", ElarionUiKit.ButtonKind.Gold,
                new Vector2(0.65f, 0.15f), new Vector2(0.98f, 0.85f),
                () => { _vm?.Select(id); _vm?.Buy(); });   // arm + fire the row's equip action via the VM
        }

        // ── Hero loadout resolution (View-side; supplied to the VM as IShopEquipTarget) ──

        private void ResolveActiveHero()
        {
            var go = FindActiveHeroGO();
            if (go != null)
                _activeLoadout = go.GetComponent<GearLoadout>();
        }

        private GameObject FindActiveHeroGO()
        {
            var byTag = GameObject.FindWithTag("Player");
            if (byTag != null) return byTag;
            var loco = FindFirstObjectByType<HeroLocomotion>();
            if (loco != null) return loco.gameObject;
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t != null && t.name.StartsWith("Hero (")) return t.gameObject;
            }
            return null;
        }

        // Lazily attach + return the active hero's loadout (mirrors the old TryEquip behavior).
        private GearLoadout EnsureLoadout()
        {
            ResolveActiveHero();
            if (_activeLoadout == null)
            {
                var hero = FindActiveHeroGO();
                if (hero != null) _activeLoadout = hero.AddComponent<GearLoadout>();
            }
            return _activeLoadout;
        }

        // Adapter: exposes the active hero's GearLoadout to the pure VM as IShopEquipTarget.
        private sealed class LoadoutEquipTarget : IShopEquipTarget
        {
            private readonly ShopPanel _panel;
            public LoadoutEquipTarget(ShopPanel panel) { _panel = panel; }

            public string EquippedWeaponName => _panel._activeLoadout?.EquippedWeapon?.name;
            public string EquippedArmorName  => _panel._activeLoadout?.EquippedArmor?.name;
            public float EquippedWeaponDamageMult => _panel._activeLoadout?.EquippedWeapon != null
                ? _panel._activeLoadout.EquippedWeapon.damageMult : 1f;
            public float EquippedArmorDefense => _panel._activeLoadout?.EquippedArmor != null
                ? _panel._activeLoadout.EquippedArmor.defense : 0f;

            public void EquipWeaponById(string id) { var l = _panel.EnsureLoadout(); if (l != null) l.EquipWeaponById(id); }
            public void EquipArmorById(string id)  { var l = _panel.EnsureLoadout(); if (l != null) l.EquipArmorById(id); }
        }

        // ── Teardown ──────────────────────────────────────────────────────────────

        private void ClearContent()
        {
            _scrollContent = null;
            if (_contentRoot == null) return;
            for (int i = _contentRoot.transform.childCount - 1; i >= 0; i--)
            {
                var c = _contentRoot.transform.GetChild(i);
                if (c != null) Destroy(c.gameObject);
            }
        }

        private void Close()
        {
            Unbind();
            _vm?.Dispose();
            _vm = null;
            _ecoText = null;
            _statusText = null;
            _actionLabel = null;
            _headerLabel = null;
            if (_ui != null) Destroy(_ui);
            _ui = null;
            _contentRoot = null;
            _tabBar = null;
            _scrollContent = null;
            _filterBar = null;
        }

        private void OnDestroy()
        {
            Unbind();
            _vm?.Dispose();
            _vm = null;
            if (_ui != null) Destroy(_ui);
        }
    }
}
