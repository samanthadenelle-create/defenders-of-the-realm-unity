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
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.Hero
{
    public sealed class ShopPanel : MonoBehaviour, IPanelView
    {
        private ShopVM _vm;

        private GameObject _ui;
        private string _vendorContext;
        private GearLoadout _activeLoadout;
        private GameObject _contentRoot;
        private GameObject _tabBar;
        private RectTransform _scrollContent;
        private TMPro.TextMeshProUGUI _actionLabel;

        // WO-714 W1 (P1 kit tabs): mode + filter rows are kit BuildTab handles — selection is
        // the kit's PLATE/underline highlight (shape + luminance), never a hue-only tint.
        private ElarionUiKit.TabHandle _tabBuy, _tabEquip, _tabSell;
        private readonly List<(GearKind kind, ElarionUiKit.TabHandle tab)> _filterTabs =
            new List<(GearKind, ElarionUiKit.TabHandle)>();

        // WO-714 W1 (P2): the wallet is CurrencyChip rows in the frame's FOOTER zone — the ONE
        // currency read (CompactNumber inside the chip; a wallet line never ellipsizes).
        private readonly List<(ElarionUiKit.CurrencyKind kind, ElarionUiKit.CurrencyChipHandle chip)> _walletChips =
            new List<(ElarionUiKit.CurrencyKind, ElarionUiKit.CurrencyChipHandle)>();

        // WO-714 W1 (P5): transient VM status surfaces as a kit ToastCard, not a stuck strip.
        private GameObject _toastCard;
        private string _lastStatus;
        private bool _statusBaselined;

        // RIGHT details pane widgets.
        private Image _detailsIcon;
        private TMPro.TextMeshProUGUI _detailsName;
        private TMPro.TextMeshProUGUI _detailsDesc;
        private TMPro.TextMeshProUGUI _detailsStats;
        private TMPro.TextMeshProUGUI _detailsCost;

        private GameObject _filterBar;

        // Part B (WO-433): rows recorded per list rebuild as (id, plate Image) so Render can
        // visibly HOLD the row whose id == vm.SelectedId. Cleared each RebuildList.
        private readonly List<(string id, Image plate)> _rowPlates = new List<(string id, Image plate)>();
        // Active-row "hold" tint — reuses the same selected/accent feel as the tab tint.
        private static readonly Color RowSelectedTint = new Color(1.15f, 1.10f, 0.92f, 1f);

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

            // WO-714 W1 (P2): the wallet renders through the ONE CurrencyChip component —
            // the chip owns formatting (CompactNumber) + count-tween; never a raw text line.
            for (int i = 0; i < _walletChips.Count; i++)
                _walletChips[i].chip?.SetAmount(WalletAmountFor(_walletChips[i].kind));

            // WO-714 W1 (P5): transient status surfaces as a toast, never a stuck strip.
            MaybeToastStatus();

            if (_actionLabel != null) _actionLabel.text = _vm.ActionLabel;

            HighlightTab();

            if (_filterBar != null) _filterBar.SetActive(_vm.FilterBarVisible);
            if (_vm.FilterBarVisible) HighlightFilter();

            RebuildList();
            HighlightSelectedRow();
            RenderDetails();
        }

        private void RebuildList()
        {
            using var _ = FlowTrace.Enter("Store", $"ShopPanel.RebuildList mode={_vm.Mode}");
            ClearContent();
            _rowPlates.Clear();

            // EQUIP shows the "Current:" header line as row 0 (mirrors the old ShowEquip layout).
            int extra = _vm.Mode == ShopMode.Equip ? 1 : 0;
            int wantCount = _vm.Items.Count;
            var listRoot = BuildScrollContent(wantCount + extra);

            if (_vm.Mode == ShopMode.Equip)
                CreateLabelRow(listRoot, _vm.EquipCurrentLine());

            // Build EACH row guarded so one bad ItemVM is logged + skipped — never aborts the
            // whole list (the "blank BUY tab because item #3 threw" class, WO-412/406).
            var (built, failed) = Guard.TryEach("Store", "build shop row", _vm.Items,
                item => CreateRow(listRoot, item));

            // STOCKED-N COMMIT SEAM (the data-empty vs built-but-invisible split): how many rows
            // the VM offered vs how many actually built. 0 wanted => genuinely data-empty; wanted
            // but 0 built => every row threw (built-but-broken), not an empty store.
            FlowTrace.Step("Store",
                $"ShopPanel stocked {built} row(s) (wanted {wantCount}, failed {failed}, extra {extra}).");

            // VERIFY rows>0: a genuinely empty list (or a list that fully failed to build) shows a
            // VISIBLE empty-state row instead of a blank panel — never a silent empty screen.
            if (built == 0 && extra == 0)
            {
                if (wantCount == 0)
                    FlowTrace.Warn("Store",
                        $"ShopPanel has NO items for mode {_vm.Mode} (filter={_vm.BuyFilter}) — showing empty-state row (data-empty).");
                else
                    FlowTrace.Fail("Store",
                        $"ShopPanel had {wantCount} item(s) but built 0 rows ({failed} failed) — showing empty-state row (built-but-broken, NOT data-empty).");
                CreateEmptyStateRow(listRoot, EmptyShopNote());
            }

            FinalizeScroll();
        }

        // The visible empty-state copy per mode/filter — the never-blank fallback.
        // WO-598: the BUY empty state reads the vendor's AUTHORED emptyLine (vendors.json
        // via VendorStockResolver) — a vendor never renders "No wares in stock." raw.
        private string EmptyShopNote()
        {
            if (_vm == null) return "Nothing available.";
            switch (_vm.Mode)
            {
                case ShopMode.Sell:  return "Nothing to sell.";
                case ShopMode.Equip: return "No gear to equip.";
                default:             return VendorStockResolver.EmptyLineFor(_vm.VendorContext);
            }
        }

        // A single visible row carrying the empty-state copy (mirrors CreateLabelRow's layout so
        // it sits in the scroll list, not a blank panel). Presentation-only, no restyle.
        private void CreateEmptyStateRow(Transform parent, string msg)
        {
            var go = new GameObject("EmptyStateRow", typeof(TMPro.TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = RowHeightPx;
            le.minHeight = RowHeightPx;
            var t = go.GetComponent<TMPro.TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(t);   // font-safe before first generation (no NRE on force-build)
            t.text = msg;
            t.fontSize = ElarionUi.FontLabel;
            t.color = ElarionUi.ParchmentDim;
            t.alignment = TMPro.TextAlignmentOptions.Center;
            t.raycastTarget = false;
            ElarionUiKit.FitSingleLine(t);   // authored empty-lines fit the row
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
            // Terminal null: an unrecognised icon role resolves to NO sprite (the details icon
            // simply hides). Log it so a missing-glyph never goes silent (built-but-invisible icon).
            FlowTrace.Warn("Store",
                $"ShopPanel.ResolveIcon: no sprite for role='{role ?? "<null>"}' id='{id ?? "<null>"}' — icon hidden.");
            return null;
        }

        // ── Chrome build (presentation only; unchanged behavior) ──────────────────

        private void BuildChrome(string displayName)
        {
            _ui = ElarionUiKit.BuildModalCanvas("ShopPanelUI", 31000);
            var shopCanvas = _ui.GetComponent<Canvas>();
            if (shopCanvas != null) shopCanvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: () => _vm?.Close());

            // SHARED Obsidian chrome (WO-554): black panel + gold trim + gold header + ONE Close.
            // Replaces the old backdrop + brown PanelFramed + per-vendor solidFill. The header text
            // comes from the VM (vm.Title) — set after Bind in Render via _headerLabel.
            var chrome = ElarionUiKit.BuildObsidianPanel(_ui.transform, "Vendor Wares",
                new Vector2(0.14f, 0.07f), new Vector2(0.86f, 0.93f), () => _vm?.Close(),
                headerX0: 0.04f, headerX1: 0.96f, frameName: RpgUiCatalog.FrameMerchant,
                medallionIcon: "coin");
            var panel = chrome.content.transform;
            _headerLabel = chrome.title;

            // WO-582 (Blink frame zone-fit): fit ALL content into the frame's BODY drop-zone (the
            // templated inner well) instead of floating over the whole panel rect — this stops the
            // economy/tabs/list/details/buttons from overlapping the frame's ornate border. Falls back
            // to the panel rect when no frame is used. Mirrors CraftingPanelMvvm.BuildChrome. The shared
            // Close (chrome.close) + the per-vendor glow stay on the full panel (chrome, not content).
            var bodyHost = (chrome.layout != null && chrome.layout.body != null)
                ? chrome.layout.body : (RectTransform)panel;

            // Subtle per-vendor accent glow (atmosphere over the shared black chrome).
            string vcLow = (_vendorContext ?? "").ToLowerInvariant();
            Color glowColor;
            if (vcLow.Contains("forge") || vcLow.Contains("blacksmith")) glowColor = new Color(0.55f, 0.22f, 0.05f, 0.22f);
            else if (vcLow.Contains("armor")) glowColor = new Color(0.30f, 0.45f, 0.65f, 0.18f);
            else if (vcLow.Contains("jewel")) glowColor = new Color(0.55f, 0.30f, 0.65f, 0.18f);
            else if (vcLow.Contains("arcane") || vcLow.Contains("magic") || vcLow.Contains("tower")) glowColor = new Color(0.35f, 0.30f, 0.75f, 0.20f);
            else if (vcLow.Contains("market") || vcLow.Contains("granary") || vcLow.Contains("farm")) glowColor = new Color(0.45f, 0.40f, 0.12f, 0.18f);
            else if (vcLow.Contains("lumber")) glowColor = new Color(0.25f, 0.42f, 0.18f, 0.18f);
            else glowColor = new Color(0.45f, 0.35f, 0.18f, 0.16f);
            if (DeNelle.Core.FeatureFlags.BlinkChrome) glowColor.a = 0f;

            var glow = ElarionUiKit.AddImage(panel, "VendorGlow",
                new Vector2(0.05f, 0.015f), new Vector2(0.95f, 0.17f), glowColor, rounded: false);
            var glowImg = glow.GetComponent<Image>();
            if (glowImg != null) glowImg.raycastTarget = false;
            glow.transform.SetAsFirstSibling();

            // WO-714 W1 (P2): wallet chips ride the frame's FOOTER drop-zone (WO-675 §5 grammar);
            // art-absent fallback = a synthesized strip in the old eco band so the wallet never blanks.
            RectTransform walletHost = chrome.layout != null ? chrome.layout.footer : null;
            if (walletHost == null)
            {
                var fb = new GameObject("Zone_WalletFallback", typeof(RectTransform));
                fb.transform.SetParent(bodyHost, false);
                walletHost = fb.GetComponent<RectTransform>();
                walletHost.anchorMin = new Vector2(0.02f, 0.875f);
                walletHost.anchorMax = new Vector2(0.98f, 0.93f);
                walletHost.offsetMin = Vector2.zero;
                walletHost.offsetMax = Vector2.zero;
            }
            BuildWalletChips(walletHost);

            // WO-714 W1 (P1): the mode row is kit BuildTab — element_tab plates with the kit's
            // plate/underline selected state (shape + luminance carry the meaning, never hue alone).
            var tabBar = new GameObject("TabBar", typeof(RectTransform));
            tabBar.transform.SetParent(bodyHost, false);
            var tbRect = tabBar.GetComponent<RectTransform>();
            tbRect.anchorMin = new Vector2(0.02f, 0.78f);
            tbRect.anchorMax = new Vector2(0.98f, 0.86f);
            tbRect.offsetMin = Vector2.zero;
            tbRect.offsetMax = Vector2.zero;

            _tabBuy   = ElarionUiKit.BuildTab(tabBar.transform, "BUY",
                new Vector2(0.02f, 0.05f), new Vector2(0.33f, 0.95f), () => _vm?.SetMode(ShopMode.Buy));
            _tabEquip = ElarionUiKit.BuildTab(tabBar.transform, "EQUIP",
                new Vector2(0.35f, 0.05f), new Vector2(0.65f, 0.95f), () => _vm?.SetMode(ShopMode.Equip));
            _tabSell  = ElarionUiKit.BuildTab(tabBar.transform, "SELL",
                new Vector2(0.67f, 0.05f), new Vector2(0.98f, 0.95f), () => _vm?.SetMode(ShopMode.Sell));
            _tabBar = tabBar;

            _contentRoot = new GameObject("Content", typeof(RectTransform));
            _contentRoot.transform.SetParent(bodyHost, false);
            var cr = _contentRoot.GetComponent<RectTransform>();
            // Eyes-sweep 2026-07-06 rule 2: the list must end ABOVE the shared bottom-centre
            // Close band (SeatSharedCloseInside seats a fixed 360x120 box there) — was 0.13.
            cr.anchorMin = new Vector2(0.02f, 0.165f);
            cr.anchorMax = new Vector2(0.62f, 0.71f);
            cr.offsetMin = Vector2.zero;
            cr.offsetMax = Vector2.zero;

            BuildFilterBar(bodyHost);
            BuildDetailsPane(bodyHost);

            // Purchase stays bottom-LEFT (x-disjoint from the shared centre Close). WO-714 W1:
            // built as the kit BuildObsidianButton confirm family (Style2/Green) — the kit owns
            // plate art, label ink (contrast law) and text-fit; no per-screen label styling.
            var purchaseBtn = ElarionUiKit.BuildObsidianButton(bodyHost, "Purchase",
                ElarionUiKit.ObsidianButtonStyle.Style2, ElarionUiKit.ObsidianButtonColor.Green,
                new Vector2(0.04f, 0.03f), new Vector2(0.30f, 0.105f),
                () => { if (_vm != null) { if (_vm.Mode == ShopMode.Sell) _vm.Sell(); else _vm.Buy(); } });
            _actionLabel = purchaseBtn != null ? purchaseBtn.GetComponentInChildren<TMPro.TextMeshProUGUI>() : null;
            // Close is the SHARED bottom-centre Obsidian Close button (WO-554) — no per-panel footer Close.
            // Keep it above the dynamically-rebuilt rows so a row can never cover/eat it (fleet soft-trap).
            if (chrome.close != null) chrome.close.transform.SetAsLastSibling();

            // WO-714 W1 (P5): no stuck status strip — vm.Status changes surface as a kit toast
            // (MaybeToastStatus in Render). Baseline the open-time idle hint so it never toasts.
            _statusBaselined = false;
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

            // WO-714 W1 (P1): filter chips are kit BuildTab — selection is the kit's plate/
            // underline state (shape + luminance), never a hue-only tint.
            _filterTabs.Clear();
            GearKind all = GearKind.Weapon | GearKind.Armor | GearKind.Potion;
            CreateFilterTab(bar.transform, "All",     new Vector2(0.01f, 0.245f), all);
            CreateFilterTab(bar.transform, "Weapons", new Vector2(0.255f, 0.49f), GearKind.Weapon);
            CreateFilterTab(bar.transform, "Armor",   new Vector2(0.505f, 0.745f), GearKind.Armor);
            CreateFilterTab(bar.transform, "Potions", new Vector2(0.755f, 0.99f), GearKind.Potion);
        }

        private void CreateFilterTab(Transform parent, string label, Vector2 anchorX, GearKind kind)
        {
            var tab = ElarionUiKit.BuildTab(parent, label,
                new Vector2(anchorX.x, 0.05f), new Vector2(anchorX.y, 0.95f),
                () => _vm?.SetFilter(kind));
            if (tab != null) _filterTabs.Add((kind, tab));
        }

        private void HighlightFilter()
        {
            if (_vm == null) return;
            GearKind all = GearKind.Weapon | GearKind.Armor | GearKind.Potion;
            var f = _vm.BuyFilter;
            for (int i = 0; i < _filterTabs.Count; i++)
            {
                bool isAllTab = _filterTabs[i].kind == all;
                bool active = f == all ? isAllTab : (!isAllTab && _filterTabs[i].kind == f);
                _filterTabs[i].tab?.SetSelected(active);
            }
        }

        private void BuildDetailsPane(Transform panel)
        {
            var pane = new GameObject("DetailsPane", typeof(RectTransform));
            pane.transform.SetParent(panel, false);
            var pr = pane.GetComponent<RectTransform>();
            // Bottom raised 0.12 -> 0.165: the pane ends above the shared Close band (rule 2).
            pr.anchorMin = new Vector2(0.63f, 0.165f);
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

            // Eyes-sweep 2026-07-06: every details label fits its OWN band (§1.14 NoWrap+ellipsis)
            // so long names/costs/stats never paint over each other or the description below.
            _detailsName = ElarionUiKit.Label(frameC.transform, "Select an item", 0.285f, 0.375f,
                ElarionUi.Gilt, ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center, 0.20f, 0.80f, bold: true);
            _detailsName.raycastTarget = false;
            ElarionUiKit.FitSingleLine(_detailsName, 0f, ElarionUi.FontBody);

            _detailsCost = ElarionUiKit.Label(frameC.transform, "", 0.245f, 0.29f,
                ElarionUi.Gilt, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.20f, 0.80f, bold: true);
            _detailsCost.raycastTarget = false;
            ElarionUiKit.FitSingleLine(_detailsCost, 0f, ElarionUi.FontLabel);

            _detailsStats = ElarionUiKit.Label(frameC.transform, "", 0.115f, 0.235f,
                ElarionUi.Affordable, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.20f, 0.80f, bold: true);
            _detailsStats.raycastTarget = false;
            ElarionUiKit.FitSingleLine(_detailsStats, 0f, ElarionUi.FontLabel);

            // Desc top pulled 0.31 -> 0.26: the stats band (frameC 0.115–0.235 = pane ~0.274–0.373)
            // used to share pane-space with the desc top. FitBlock truncates inside the band.
            _detailsDesc = ElarionUiKit.Label(pane.transform, "Tap an item to inspect it.", 0.02f, 0.26f,
                ElarionUi.ParchmentDim, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Top, 0.06f, 0.94f);
            _detailsDesc.textWrappingMode = TMPro.TextWrappingModes.Normal;
            _detailsDesc.raycastTarget = false;
            ElarionUiKit.FitBlock(_detailsDesc, 0f, ElarionUi.FontLabel);
        }

        // WO-714 W1 (P2): one CurrencyChip per spendable currency, Gold primary first, laid
        // evenly across the footer zone (the WO-675 §5 wallet grammar). The chip owns
        // formatting/count-tween; the View only calls SetAmount from vm.* in Render.
        private void BuildWalletChips(RectTransform host)
        {
            _walletChips.Clear();
            if (host == null) return;

            var kinds = new[]
            {
                ElarionUiKit.CurrencyKind.Gold,
                ElarionUiKit.CurrencyKind.Wood,
                ElarionUiKit.CurrencyKind.Iron,
                ElarionUiKit.CurrencyKind.Food,
                ElarionUiKit.CurrencyKind.Crystal,
            };
            int n = kinds.Length;
            const float gap = 0.008f;
            for (int i = 0; i < n; i++)
            {
                float x0 = (float)i / n + gap;
                float x1 = (float)(i + 1) / n - gap;
                bool primary = kinds[i] == ElarionUiKit.CurrencyKind.Gold;
                var chip = ElarionUiKit.CurrencyChip(host, kinds[i],
                    new Vector2(x0, 0.10f), new Vector2(x1, 0.90f),
                    primary: primary, tag: kinds[i].ToString());
                _walletChips.Add((kinds[i], chip));
            }
        }

        // Map a chip kind to the bound VM's wallet values (presentation read of VM data only).
        private long WalletAmountFor(ElarionUiKit.CurrencyKind kind)
        {
            if (_vm == null) return 0;
            switch (kind)
            {
                case ElarionUiKit.CurrencyKind.Wood:    return _vm.Wood;
                case ElarionUiKit.CurrencyKind.Iron:    return _vm.Iron;
                case ElarionUiKit.CurrencyKind.Food:    return _vm.Food;
                case ElarionUiKit.CurrencyKind.Crystal: return _vm.Crystals;
                default:                                return _vm.Coins;
            }
        }

        private void HighlightTab()
        {
            if (_vm == null) return;
            _tabBuy?.SetSelected(_vm.Mode == ShopMode.Buy);
            _tabEquip?.SetSelected(_vm.Mode == ShopMode.Equip);
            _tabSell?.SetSelected(_vm.Mode == ShopMode.Sell);
        }

        // ── Status toast (WO-714 W1, P5) ─────────────────────────────────────────
        // vm.Status is transient feedback ("Not enough gold.", "Purchased ...") — surface each
        // CHANGE as a kit ToastCard that auto-dismisses; the open-time idle hint is baselined
        // (never toasted). One live toast at a time; presentation only.

        private void MaybeToastStatus()
        {
            if (_vm == null) return;
            string s = _vm.Status;
            if (!_statusBaselined)
            {
                _statusBaselined = true;
                _lastStatus = s;
                return;
            }
            if (s == _lastStatus) return;
            _lastStatus = s;
            if (string.IsNullOrEmpty(s) || _ui == null) return;

            if (_toastCard != null) Destroy(_toastCard);
            var parts = ElarionUiKit.ToastCard(_ui.transform, ElarionUiKit.ToastTone.Gold,
                accentLeft: false, TextAnchor.MiddleCenter);
            if (parts == null || parts.card == null) return;
            var rt = parts.card.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.28f, 0.79f);
            rt.anchorMax = new Vector2(0.72f, 0.86f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            if (parts.label != null) parts.label.text = s;
            _toastCard = parts.card;
            if (isActiveAndEnabled) StartCoroutine(DismissToast(parts.card, 2.8f));
        }

        private System.Collections.IEnumerator DismissToast(GameObject go, float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (go != null) Destroy(go);
            if (_toastCard == go) _toastCard = null;
        }

        // ── Active-row hold (WO-433): reflect vm.SelectedId visually ──────────────
        // Reset every recorded row to its normal plate look (DressRowPlate — Blink slot
        // white when flag ON, else ElarionUiKit.Cell), then multiply-tint the row whose
        // id == vm.SelectedId so it visibly "holds". Selection AFFORDANCE — kept in BOTH
        // flag states. Reflects state only; selection LOGIC is untouched (vm.Select).
        private void HighlightSelectedRow()
        {
            if (_vm == null) return;
            string sel = _vm.SelectedId;
            for (int i = 0; i < _rowPlates.Count; i++)
            {
                var plate = _rowPlates[i].plate;
                if (plate == null) continue;
                DressRowPlate(plate); // normal look (respects WO-432 flag state)
                if (sel != null && _rowPlates[i].id == sel)
                {
                    var c = plate.color;
                    plate.color = new Color(c.r * RowSelectedTint.r, c.g * RowSelectedTint.g,
                                            c.b * RowSelectedTint.b, c.a);
                }
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
            _rowPlates.Add((item.Id, rowImg));
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

            // Eyes-sweep 2026-07-06: name + price each get a DISJOINT band and fit-or-ellipsize
            // (§1.14 NoWrap+ellipsis) — long names used to wrap onto neighbouring rows so every
            // item name painted onto the next ("Apprentice Wand / Arcane Heart ... illegible").
            float nameX0 = isSell ? 0.04f : 0.20f;
            float nameX1 = isSell ? 0.52f : 0.48f;
            var nameLbl = ElarionUiKit.Label(row.transform, item.Name, 0.15f, 0.85f, ElarionUi.Parchment,
                ElarionUi.FontBody, TMPro.TextAlignmentOptions.Left, nameX0, nameX1);
            ElarionUiKit.FitSingleLine(nameLbl, 0f, ElarionUi.FontBody);

            // COLORBLIND CANON (owner is red/green colorblind — meaning is NEVER hue-only).
            // The old encoding here was green(ElarionUi.Affordable) vs red(ElarionUi.Danger)
            // price hue ALONE. Now: affordable = normal parchment price; unaffordable =
            // luminance-DIMMED price + an ASCII bracket marker "[needs N]" (mirrors the
            // PartyShop "[Lv 6]" lock cue — shape + luminance carry the meaning, not hue).
            string priceText = isSell ? "+" + PriceString(item) : PriceString(item);
            bool canAfford = isSell || item.Price <= 0 || item.Affordable;
            if (!canAfford) priceText += "  [needs " + ElarionUi.CompactNumber(item.Price) + "]";   // WO-697
            Color priceColor = canAfford ? ElarionUi.Parchment : ElarionUi.ParchmentDim;
            float px0 = isSell ? 0.54f : 0.50f;
            float px1 = 0.97f;
            var priceLbl = ElarionUiKit.Label(row.transform, priceText, 0.15f, 0.85f, priceColor,
                ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Left, px0, px1, bold: canAfford);
            ElarionUiKit.FitSingleLine(priceLbl, 0f, ElarionUi.FontLabel);
        }

        // Render the price text from the item's Price (the VM denominates everything in gold for
        // shop rows; Free when 0). Matches the old CostString output for a coins-only cost.
        private static string PriceString(ItemVM item)
        {
            // WO-697: price through the ONE kit formatter (compact >= 10k).
            return item.Price > 0 ? ElarionUi.CompactNumber(item.Price) + " Gold" : "Free";
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
            ElarionUiKit.EnsureFont(t);   // font-safe before first generation (no NRE on force-build)
            t.text = txt;
            t.fontSize = ElarionUi.FontLabel;
            t.color = ElarionUi.ParchmentDim;
            t.alignment = TMPro.TextAlignmentOptions.Left;
            t.raycastTarget = false;
            ElarionUiKit.FitSingleLine(t);   // "Current: ..." header line fits its row
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
            _rowPlates.Add((item.Id, rowImg));

            var eqName = ElarionUiKit.Label(row.transform, item.Name, 0.15f, 0.85f, ElarionUi.Parchment,
                ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Left, 0.04f, 0.62f);
            ElarionUiKit.FitSingleLine(eqName, 0f, ElarionUi.FontLabel);   // never wraps under the EQUIP button

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
            var loco = FindAnyObjectByType<HeroLocomotion>();
            if (loco != null) return loco.gameObject;
            foreach (var t in FindObjectsByType<Transform>())
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
            _actionLabel = null;
            _headerLabel = null;
            _walletChips.Clear();
            _filterTabs.Clear();
            _tabBuy = null; _tabEquip = null; _tabSell = null;
            _toastCard = null;   // destroyed with _ui below
            _lastStatus = null;
            _statusBaselined = false;
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
