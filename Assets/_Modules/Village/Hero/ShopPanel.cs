// =============================================================================
// ShopPanel — vendor / gear shop UI (buy, sell, equip), routed through the SHARED
// presentation kit (DeNelle.Core.UI.ElarionUiKit) so the store reads as the SAME
// designed game as the town HUD: dark-glass panels + gold-rune frames + RPG-pack
// art, instead of its old private light-parchment styling.
// -----------------------------------------------------------------------------
// Code-built (no UXML), screen-space overlay, large touch targets for mobile.
// Opened via Yarn "OpenShop" (or "OpenShop armorer") from NPCCommandBridge.
// Uses EconomyService for all transactions (TrySpend/Grant with ResourceCost).
// Uses VillageInventory for "owning" gear pieces and potions (Add/Get).
// Forces equip via GearLoadout (updates stats + triggers GearVisualApplier).
// Vendors: context string filters flavor ("armorer" prioritizes armor, "forge"
// prioritizes weapons); potions always available as basic stock.
//
// Starter catalog comes from GearCatalog (populated by weapons.json / armor.json).
// Sell refunds ~60% of buy cost. Equip updates the active hero (by tag "Player"
// or first HeroLocomotion) immediately.
//
// PRESENTATION: every surface is assembled from ElarionUiKit (BuildModalCanvas /
// Scrim / Panel / Header / Well / Button / Card / Label) sourcing the canonical
// ElarionUi dark-glass + gold palette — NO private colour block, NO bespoke gilt
// frames. The scroll list still uses a VerticalLayoutGroup + ContentSizeFitter +
// per-row LayoutElement (the rendering fix that cured the "no stock" bug) — only
// COLOURS / SPRITES / frames moved to the kit, never the layout mechanism.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Village;
using DeNelle.Village.Crafting;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.Hero
{
    public sealed class ShopPanel : MonoBehaviour
    {
        // Active-tab tint vs. inactive: the active mode reads bright gilt; inactive tabs
        // are clearly muted (dimmed gold) so the selected mode stands out at a glance.
        // Applied as a multiply tint over the kit's Gold button image (works whether the
        // button is the procedural rounded glass or the RPG-pack gold frame).
        private static readonly Color TabSelectedTint = new Color(1.15f, 1.10f, 0.92f, 1f);
        private static readonly Color TabRestTint     = new Color(0.58f, 0.55f, 0.50f, 1f);

        private GameObject _ui;
        private string _vendorContext;
        private GearLoadout _activeLoadout;
        private GameObject _contentRoot;
        private GameObject _tabBar; // tab row host (for active-tab highlight)
        private RectTransform _scrollContent; // the active scroll list content (for the post-populate rebuild)
        private TMPro.TextMeshProUGUI _statusText; // use TMPro for consistency with EquipmentPanel + to avoid any legacy UI asm quirks
        private TMPro.TextMeshProUGUI _ecoText;
        private System.Action<ResourceSnapshot> _ecoHandler; // stored so Close() can unsubscribe (no leak)

        // Unified Shop WO #3: RIGHT details pane. Built once per Open(); populated when a
        // buy/sell row is tapped (SelectForDetails) IN ADDITION to the row's own buy/sell action.
        private Image _detailsIcon;
        private TMPro.TextMeshProUGUI _detailsName;
        private TMPro.TextMeshProUGUI _detailsDesc;
        private TMPro.TextMeshProUGUI _detailsStats;
        private TMPro.TextMeshProUGUI _detailsCost;

        // Unified Shop WO #4: current Type filter for the BUY list. Intersected with the
        // vendor contract's allowed kinds; default = the contract's full allowance ("All").
        private GearKind _buyFilter = GearKind.Weapon | GearKind.Armor | GearKind.Potion;
        private GameObject _filterBar; // the Type-filter button row (for active-filter highlight)

        private readonly List<string> _potionIds = new List<string> { "minor-heal-potion", "minor-mana-potion" };

        // WO-444: the ACTUAL built stock, recorded as ShowBuy creates each row. This is
        // the store's real output (independent of the selection logic), so the AutoPilot
        // bot can read it AFTER ShowBuy and assert it against VendorStockContract.AllowedFor
        // — catching a filter regression even if the filter and the contract drift apart.
        private readonly List<(string id, GearKind kind)> _currentStock = new List<(string id, GearKind kind)>();

        /// <summary>The store's ACTUAL built stock (id + category) after the last ShowBuy,
        /// for the AutoPilot bot to assert against <see cref="VendorStockContract"/>.</summary>
        public IReadOnlyList<(string id, GearKind kind)> CurrentStock => _currentStock;

        /// <summary>The vendor context this panel last opened/built for (lowercased not guaranteed;
        /// pass through VendorStockContract.AllowedFor which lowercases).</summary>
        public string VendorContext => _vendorContext;

        public void Open(string vendorContext = null, string displayName = null)
        {
            Close();

            _vendorContext = vendorContext == null ? "" : vendorContext;
            ResolveActiveHero();

            // Modal canvas + tap-outside-to-close scrim, both from the shared kit.
            // BUG 2 FIX: the kit canvas rode sortingOrder 1000 — FAR below the always-on
            // world-HUD band (VirtualJoystick/CampPrompt 30000, MobileInteractButton 30050,
            // and crucially the town-HUD wave "PLAY"/Start-Wave banner from
            // VillageHudController). At 1000 the store rendered UNDER the HUD, so the HUD's
            // PLAY banner bled through across the top of the store. Mirror the inventory
            // modal: pin sortingOrder 31000 + overrideSorting so the store (and its
            // full-screen scrim) render ABOVE the HUD band but below the true top overlays
            // (GameOver / load fade = 32760). The kit Scrim already blocks raycasts; at
            // 31000 it now also visually covers the HUD so nothing bleeds through.
            _ui = ElarionUiKit.BuildModalCanvas("ShopPanelUI", 31000);
            var shopCanvas = _ui.GetComponent<Canvas>();
            if (shopCanvas != null) shopCanvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: Close);

            // Framed dark-glass panel (deep) — the canonical store backboard, dressed
            // sprite-FIRST with the RPG tech-pack's ornate inventory/dialogue frame
            // (gold-rune border over dark glass) so the store reads as the SAME designed
            // game as the town HUD. Falls back to the procedural glass+gilt-rim panel
            // when the pack art is absent. T-024: a THIN, minimized footprint suited to
            // web UI — a slim centred column (not a full-screen sheet) so the store reads
            // as a compact dark-glass drawer rather than taking over the screen.
            // Backboard: plain DEEP dark-glass (NOT the panel_inventory grid-of-slots sprite —
            // that decorative gold grid made the store read as "empty slots / no stock" even with
            // 18 real rows on top). Dark glass = the rows show clearly, same as the town HUD.
            // WIDENED (Unified Shop WO #3) from 0.22–0.78 to 0.14–0.86 so a RIGHT details
            // column fits beside the LEFT scroll list. Dressed in the canonical D3 dark-wood
            // vendor board (WO #2: "dark wood frames, gold accents") via the single kit seam —
            // packSpriteName: RpgUiCatalog.PanelVendor — instead of the old plain dark glass.
            var panelGo = ElarionUiKit.PanelFramed(_ui.transform, new Vector2(0.14f, 0.07f), new Vector2(0.86f, 0.93f),
                                                   deep: true, packSpriteName: RpgUiCatalog.PanelVendor);
            var panel = panelGo.transform;

            // Header — read as a named storefront, not a generic "Vendor Wares" wall. The
            // structure flow passes the building's own id ("forge"/"market"/"jeweler"/…) so
            // every shop gets a themed title; the hand-authored NPC vendors keep their named
            // signs. Unknown ids titleize the id ("lumbermill" → "Lumbermill") rather than
            // falling through to a bare default.
            string vc = _vendorContext.ToLowerInvariant();
            string title;
            if (vc.Contains("armor")) title = "Armorer's Shop";
            else if (vc.Contains("forge") || vc.Contains("blacksmith")) title = "Blacksmith's Forge";
            else if (vc.Contains("market")) title = "Market Stalls";
            else if (vc.Contains("jewel")) title = "Jeweler's Bench";
            else if (vc.Contains("lumber")) title = "Lumbermill Stores";
            else if (vc.Contains("granary") || vc.Contains("farm")) title = "Granary Goods";
            else if (vc.Contains("stable")) title = "Stable Supplies";
            else if (string.IsNullOrEmpty(_vendorContext)) title = "Vendor Wares";
            else title = TitleizeVendor(_vendorContext) + " Wares";

            // FIX B: an explicit vendor LABEL (the storefront's own sign/display name) wins
            // over the id-derived title — so a storefront can never read the wrong header just
            // because its id collapsed onto another vendor's id. Falls through to the id-derived
            // title above when no label is supplied (the param is optional; old callers unchanged).
            if (!string.IsNullOrEmpty(displayName)) title = displayName;
            ElarionUiKit.Header(panel, title, x0: 0.04f, x1: 0.96f, y0: 0.9f, y1: 0.97f);

            // Economy readout (live)
            CreateEconomyReadout(panel);

            // Tabs / mode buttons — kit buttons; the active one is brightened via tint.
            var tabBar = new GameObject("TabBar", typeof(RectTransform));
            tabBar.transform.SetParent(panel, false);
            var tbRect = tabBar.GetComponent<RectTransform>();
            tbRect.anchorMin = new Vector2(0.02f, 0.78f);
            tbRect.anchorMax = new Vector2(0.98f, 0.86f);
            tbRect.offsetMin = Vector2.zero;
            tbRect.offsetMax = Vector2.zero;

            // Unified Shop WO #1: EQUIP tab REMOVED — "Equip belongs elsewhere" (EquipmentPanel,
            // opened via the Yarn "OpenEquip" command). BUY/SELL re-spaced to split the bar evenly.
            // ShowEquip()/equip rows are left in the file (harmless, no longer reachable from a tab).
            CreateTabButton(tabBar.transform, "BUY", new Vector2(0.02f, 0.49f), () => ShowBuy());
            CreateTabButton(tabBar.transform, "SELL", new Vector2(0.51f, 0.98f), () => ShowSell());
            _tabBar = tabBar; // kept so ShowBuy/Sell can light the active tab

            // Content area (replaced per mode) — Unified Shop WO #3: now the LEFT list column
            // only (was full width 0.02–0.98). The RIGHT details pane sits beside it at 0.64–0.98.
            // The scroll-list mechanism inside (Well + masked viewport + VerticalLayoutGroup +
            // ContentSizeFitter + per-row LayoutElement) is UNCHANGED — only its host's width moved.
            _contentRoot = new GameObject("Content", typeof(RectTransform));
            _contentRoot.transform.SetParent(panel, false);
            var cr = _contentRoot.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0.02f, 0.08f);
            cr.anchorMax = new Vector2(0.62f, 0.71f);
            cr.offsetMin = Vector2.zero;
            cr.offsetMax = Vector2.zero;

            // Unified Shop WO #4: compact Type filter row above the LEFT list (All / Weapons /
            // Armor / Potions). Built once; each button re-runs ShowBuy with a GearKind filter.
            BuildFilterBar(panel);

            // RIGHT details pane (selected item: icon, name, description, stats, cost).
            BuildDetailsPane(panel);

            // Close (X) — top-right corner, ornate pack-frame danger button. Added last
            // so it draws on top of the header/content and always receives the tap.
            ElarionUiKit.ButtonPack(panel, "X", ElarionUiKit.ButtonKind.Danger,
                                    new Vector2(0.9f, 0.9f), new Vector2(0.985f, 0.985f), Close);

            // Status line
            var statusGo = new GameObject("Status", typeof(TMPro.TextMeshProUGUI));
            statusGo.transform.SetParent(panel, false);
            var sRect = statusGo.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0.02f, 0.01f);
            sRect.anchorMax = new Vector2(0.98f, 0.07f);
            sRect.offsetMin = Vector2.zero;
            sRect.offsetMax = Vector2.zero;
            _statusText = statusGo.GetComponent<TMPro.TextMeshProUGUI>();
            _statusText.fontSize = ElarionUi.FontLabel;
            _statusText.color = ElarionUi.ParchmentDim; // soft cream on dark glass
            _statusText.alignment = TMPro.TextAlignmentOptions.Center;
            _statusText.raycastTarget = false;
            SetStatus("Browse wares. Transactions use Gold.");

            // Default to Buy
            ShowBuy();

            Debug.Log($"[ShopPanel] Opened for vendor '{_vendorContext}'. Economy + inventory driven.");
        }

        // Turn a raw vendor/structure id ("pet-house", "lumber_yard") into a display
        // word for the header fallback. Keeps the storefront from reading as a generic
        // "Vendor Wares" when an id has no bespoke title above.
        private static string TitleizeVendor(string id)
        {
            if (string.IsNullOrEmpty(id)) return "Vendor";
            id = id.Replace('-', ' ').Replace('_', ' ').Trim();
            if (id.Length == 0) return "Vendor";
            return char.ToUpper(id[0]) + (id.Length > 1 ? id.Substring(1) : "");
        }

        // Unified Shop WO #4: a compact Type-filter row over the LEFT list. Four small kit
        // buttons (All / Weapons / Armor / Potions) set _buyFilter and re-run ShowBuy. The
        // filter is INTERSECTED with the vendor contract in ShowBuy, so e.g. an armorer's
        // "Weapons" filter simply yields nothing (the contract still wins) — additive + safe.
        // NOTE: Class / Level filters were SKIPPED — the catalog defs carry job + rarity but no
        // player-level gate, and a Class control needs a hero-class picker we don't surface here.
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
                () => { _buyFilter = kind; ShowBuy(); });
        }

        // Brighten the active Type filter, dim the rest (same tint scheme as the mode tabs).
        private void HighlightFilter()
        {
            if (_filterBar == null) return;
            GearKind all = GearKind.Weapon | GearKind.Armor | GearKind.Potion;
            string active = _buyFilter == GearKind.Weapon ? "Btn_Weapons"
                          : _buyFilter == GearKind.Armor  ? "Btn_Armor"
                          : _buyFilter == GearKind.Potion ? "Btn_Potions"
                          : "Btn_All";
            if (_buyFilter == all) active = "Btn_All";
            foreach (Transform child in _filterBar.transform)
            {
                if (child == null) continue;
                var img = child.GetComponent<Image>();
                if (img != null) img.color = child.name == active ? TabSelectedTint : TabRestTint;
            }
        }

        // Unified Shop WO #3: the RIGHT details pane — a kit Well + an icon image and the
        // name / description / stats / cost labels. Built ONCE per Open(); a row tap fills it
        // in via SelectForDetails. Starts with a neutral "tap an item" prompt.
        private void BuildDetailsPane(Transform panel)
        {
            var pane = new GameObject("DetailsPane", typeof(RectTransform));
            pane.transform.SetParent(panel, false);
            var pr = pane.GetComponent<RectTransform>();
            pr.anchorMin = new Vector2(0.64f, 0.08f);
            pr.anchorMax = new Vector2(0.98f, 0.76f);
            pr.offsetMin = Vector2.zero;
            pr.offsetMax = Vector2.zero;

            // Recessed well backing so the details column reads as a defined inset.
            var well = ElarionUiKit.Well(pane.transform, Vector2.zero, Vector2.one);
            var wImg = well.GetComponent<Image>();
            if (wImg != null) wImg.raycastTarget = false;

            // Item icon (top), centred in a square-ish slot.
            var iconGo = ElarionUiKit.AddImage(pane.transform, "DetailIcon",
                new Vector2(0.30f, 0.68f), new Vector2(0.70f, 0.97f), Color.white, rounded: false);
            _detailsIcon = iconGo.GetComponent<Image>();
            _detailsIcon.preserveAspect = true;
            _detailsIcon.raycastTarget = false;
            _detailsIcon.enabled = false; // hidden until a row populates it

            _detailsName = ElarionUiKit.Label(pane.transform, "Select an item", 0.60f, 0.67f,
                ElarionUi.Gilt, ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);
            _detailsName.raycastTarget = false;

            _detailsDesc = ElarionUiKit.Label(pane.transform, "Tap a row to see its details.", 0.36f, 0.59f,
                ElarionUi.ParchmentDim, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.TopLeft, 0.06f, 0.94f);
            _detailsDesc.raycastTarget = false;

            _detailsStats = ElarionUiKit.Label(pane.transform, "", 0.16f, 0.35f,
                ElarionUi.Affordable, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.TopLeft, 0.06f, 0.94f);
            _detailsStats.raycastTarget = false;

            _detailsCost = ElarionUiKit.Label(pane.transform, "", 0.04f, 0.15f,
                ElarionUi.Gilt, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f, bold: true);
            _detailsCost.raycastTarget = false;
        }

        // Populate the right details pane for a selected item. Called by a buy/sell row's tap
        // IN ADDITION to (not instead of) the row's own buy/sell action. icon may be null
        // (no art -> the icon slot is hidden, name+stats still show).
        private void SelectForDetails(string name, string description, string stats, ResourceCost cost, Sprite icon, bool isRefund)
        {
            if (_detailsName != null) _detailsName.text = name;
            if (_detailsDesc != null) _detailsDesc.text = description;
            if (_detailsStats != null) _detailsStats.text = stats;
            if (_detailsCost != null) _detailsCost.text = (isRefund ? "Refund: +" : "Cost: ") + CostString(cost);
            if (_detailsIcon != null)
            {
                _detailsIcon.sprite = icon;
                _detailsIcon.enabled = icon != null;
            }
        }

        // Build a short flavor/description line from the def's job + rarity (the catalog has no
        // dedicated description field) so the details pane reads as more than just a name.
        private static string DescribeGear(string job, string rarity)
        {
            string r = string.IsNullOrEmpty(rarity) ? "" : char.ToUpper(rarity[0]) + (rarity.Length > 1 ? rarity.Substring(1) : "");
            string j = string.IsNullOrEmpty(job) || job == "any" ? "any class" : "the " + job;
            return (string.IsNullOrEmpty(r) ? "" : r + " gear. ") + "Suited to " + j + ".";
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
            t.color = ElarionUi.Gilt; // gold resource ink on dark glass
            t.alignment = TMPro.TextAlignmentOptions.Center;
            t.raycastTarget = false;
            _ecoText = t;
            UpdateEcoText(t);
            // Live update while open — store the handler so Close() can unsubscribe (the old
            // anonymous lambda could never be removed -> a listener leaked on every Open).
            if (EconomyService.Instance != null)
            {
                _ecoHandler = _ => { if (_ecoText != null) UpdateEcoText(_ecoText); };
                EconomyService.Instance.OnChanged += _ecoHandler;
            }
        }

        private void UpdateEcoText(TMPro.TextMeshProUGUI t)
        {
            if (t == null || EconomyService.Instance == null) return;
            var e = EconomyService.Instance;
            t.text = $"Gold: {e.Coins}   Wood: {e.Wood}   Iron: {e.Iron}   Food: {e.Food}   Crystals: {e.Crystals}";
        }

        // A mode tab built from the shared kit ButtonPack (Gold kind) dressed sprite-FIRST
        // with the RPG tech-pack's ornate gold button frame (button/button_gold, the kit's
        // RoleButton default) so the tabs carry the same gilded plate as the town HUD;
        // falls back to the procedural gold glass when the pack is absent. anchorX =
        // (min,max) fractions across the tab bar. The active tab is brightened by
        // HighlightTab (tint over the pack frame image).
        private void CreateTabButton(Transform parent, string label, Vector2 anchorX, System.Action onClick)
        {
            ElarionUiKit.ButtonPack(parent, label, ElarionUiKit.ButtonKind.Gold,
                                    new Vector2(anchorX.x, 0.05f), new Vector2(anchorX.y, 0.95f), onClick);
        }

        // Light the active tab brighter; dim the rest. The kit names its buttons
        // "Btn_<label>", and tints the targetGraphic Image — so we just brighten the
        // selected one's image colour. Null-safe: a no-op until the tab bar exists.
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

        private void SetStatus(string s)
        {
            if (_statusText != null) _statusText.text = s;
        }

        // --- BUY ---
        private void ShowBuy()
        {
            ClearContent();
            HighlightTab("BUY");
            HighlightFilter();
            SetStatus("Buy gear or potions. Tap a row or BUY to purchase.");

            GearCatalog.Reload(); // pick up any live data change

            // Fresh build -> reset the recorded actual stock for the bot's assertion.
            _currentStock.Clear();

            // WO-444: ONE CONTRACT. VendorStockContract is the single source of truth for
            // which gear categories this vendor TYPE sells (forge -> Weapon, armorer ->
            // Armor, market -> Potion, jeweler -> Armor, unknown -> all). Replaces the old
            // ad-hoc ctx.Contains armorerOnly/forgeOnly checks that fell through to "sell
            // everything" for market/jeweler — the bug that put swords in the marketplace.
            string ctx = _vendorContext.ToLowerInvariant();
            GearKind allowed = VendorStockContract.AllowedFor(ctx);

            // Unified Shop WO #4: intersect the contract with the player's chosen Type filter.
            // The contract STILL wins (an armorer filtered to "Weapons" yields nothing); "All"
            // (_buyFilter = Weapon|Armor|Potion) is a no-op pass-through. The never-empty
            // safeguard below uses 'allowed' (post-intersection) so an empty filter just shows
            // an empty list rather than force-falling-back to the full general stock.
            allowed &= _buyFilter;

            // Full catalog (null-safe — AllWeapons/AllArmors never return null).
            var allWeapons = new List<WeaponDef>();
            var allArmors  = new List<ArmorDef>();
            foreach (var w in GearCatalog.AllWeapons()) if (w != null) allWeapons.Add(w);
            foreach (var a in GearCatalog.AllArmors())  if (a != null) allArmors.Add(a);

            // Apply the contract filter.
            var weapons = new List<WeaponDef>();
            var armors  = new List<ArmorDef>();
            if ((allowed & GearKind.Weapon) != 0) weapons.AddRange(allWeapons);
            if ((allowed & GearKind.Armor)  != 0) armors.AddRange(allArmors);
            bool potionsAllowed = (allowed & GearKind.Potion) != 0;

            // WO-406 never-empty safeguard: if the contract yields an EMPTY shop (no
            // weapons, no armors, AND no potions) but the catalog actually HAS gear, fall
            // back to the general default so the player always has wares to buy. This guards
            // both a category-empty catalog (an Armor-only vendor whose armor.json is empty)
            // and a Potion-only vendor with no gear but whose potions are configured. (If the
            // catalog itself failed to load AND no potions are configured, the shop is
            // genuinely empty and only that is logged.)
            bool wouldBeEmpty = weapons.Count == 0 && armors.Count == 0 &&
                                (!potionsAllowed || _potionIds.Count == 0);
            // WO #4: only force the general-stock fallback when the player has NOT narrowed the
            // Type filter ("All"). A deliberate Weapons-at-an-armorer filter should show an empty
            // list, not get overridden back to full stock.
            bool filterIsAll = _buyFilter == (GearKind.Weapon | GearKind.Armor | GearKind.Potion);
            if (wouldBeEmpty && filterIsAll && (allWeapons.Count + allArmors.Count) > 0)
            {
                weapons.Clear(); weapons.AddRange(allWeapons);
                armors.Clear();  armors.AddRange(allArmors);
                potionsAllowed = true;
                Debug.LogWarning($"[ShopPanel] Vendor '{_vendorContext}' contract ({allowed}) yielded an empty shop; falling back to general stock ({weapons.Count} weapons, {armors.Count} armors, +potions).");
            }
            else if (wouldBeEmpty)
            {
                Debug.LogWarning("[ShopPanel] Gear catalog is empty (weapons.json / armor.json failed to load) and no potions allowed/configured — shop has no wares.");
            }

            // Count rows up front so the scroll content can be sized to fit them all.
            int potionCount = potionsAllowed ? _potionIds.Count : 0;
            int rowCount = weapons.Count + armors.Count + potionCount;

            // STORE CONTENTS DUMP (owner directive): log exactly what the merchant will stock,
            // so a "no stock" report can be split into (a) catalog gave us nothing vs (b) we had
            // stock but it didn't render. Lists every id by section.
            FlowTrace.Step("Store", $"ShowBuy vendor='{_vendorContext}' allowed={allowed} -> stocking {rowCount} rows " +
                $"({weapons.Count} weapons, {armors.Count} armors, {potionCount} potions)");
            if (rowCount == 0)
                FlowTrace.Warn("Store", "STOCK EMPTY: catalog returned no weapons/armors and no potions allowed/configured.");
            else
            {
                var sb = new System.Text.StringBuilder();
                foreach (var w in weapons) sb.Append("W:").Append(w != null ? w.name : "<null>").Append(' ');
                foreach (var a in armors)  sb.Append("A:").Append(a != null ? a.name : "<null>").Append(' ');
                if (potionsAllowed) foreach (var p in _potionIds) sb.Append("P:").Append(p).Append(' ');
                FlowTrace.Step("Store", "stock = " + sb.ToString().TrimEnd());
            }

            var listRoot = BuildScrollContent(rowCount);

            // Owner directive: try/catch + log EVERY object we turn into a row, so one bad
            // item (null field, appraisal throw, missing icon) logs and is skipped instead of
            // silently aborting the whole list — the classic "store shows nothing, no error".
            // Wrapped in a perf Measure scope (16ms = one frame budget) so a slow store-build
            // surfaces as a tagged log line, not a guessed-at stutter.
            using var _buildTimer = FlowTrace.Measure("Store", "ShowBuy row build", warnAboveMs: 16f);
            int built = 0, failed = 0;
            foreach (var w in weapons)
            {
                var wCopy = w; // capture for the closure
                try
                {
                    var wDetailCost = GearCatalog.GetBuyCost(w);
                    int wDmgPct = Mathf.RoundToInt((Mathf.Max(0.1f, w.damageMult) - 1f) * 100f);
                    string wStats = $"+{wDmgPct}% dmg" + (w.reach > 0f ? $"   reach {w.reach:0.#}m" : "");
                    var wIcon = ItemIconCatalog.ForWeapon(w);
                    CreateBuyRow(listRoot, BuyLabel(w.name, GearAppraisal.Appraise(w)), GearCatalog.GetBuyCost(w), () => TryBuyWeapon(wCopy), null,
                        () => SelectForDetails(string.IsNullOrEmpty(w.name) ? w.id : w.name, DescribeGear(w.job, w.rarity), wStats, wDetailCost, wIcon, isRefund: false));
                    _currentStock.Add((w.id, GearKind.Weapon)); // record ACTUAL built stock for the bot's assertion
                    built++;
                }
                catch (System.Exception ex) { failed++; FlowTrace.Fail("Store", $"weapon row '{(w != null ? w.id : "<null>")}' threw: {ex.Message}"); }
            }
            foreach (var a in armors)
            {
                var aCopy = a; // capture for the closure
                try
                {
                    var aDetailCost = GearCatalog.GetBuyCost(a);
                    int aDefPct = Mathf.RoundToInt(Mathf.Clamp(a.defense, 0f, 0.9f) * 100f);
                    string aStats = $"+{aDefPct}% def" + (a.hpBonus > 0f ? $"   +{a.hpBonus:0.#} hp" : "");
                    var aIcon = ItemIconCatalog.ForArmor(a);
                    CreateBuyRow(listRoot, BuyLabel(a.name, GearAppraisal.Appraise(a)), GearCatalog.GetBuyCost(a), () => TryBuyArmor(aCopy), null,
                        () => SelectForDetails(string.IsNullOrEmpty(a.name) ? a.id : a.name, DescribeGear(a.job, a.rarity), aStats, aDetailCost, aIcon, isRefund: false));
                    _currentStock.Add((a.id, GearKind.Armor)); // record ACTUAL built stock for the bot's assertion
                    built++;
                }
                catch (System.Exception ex) { failed++; FlowTrace.Fail("Store", $"armor row '{(a != null ? a.id : "<null>")}' threw: {ex.Message}"); }
            }
            if (potionsAllowed)
            foreach (var pid in _potionIds)
            {
                var cost = new ResourceCost(coins: 8); // cheap early potions — GOLD
                if (pid.Contains("mana")) cost = new ResourceCost(coins: 12);
                string pidCopy = pid; var costCopy = cost; // capture for the closure
                try
                {
                    // Pack potion art on the row when present (red heal / blue mana bottle).
                    var potionIcon = RpgUiCatalog.Get(RpgUiCatalog.RolePotion,
                        pid.Contains("mana") ? RpgUiCatalog.PotionMana : RpgUiCatalog.PotionHealth);
                    string potionDesc = pidCopy.Contains("mana") ? "Restores mana in a pinch." : "Restores health in a pinch.";
                    CreateBuyRow(listRoot, pidCopy, costCopy, () => TryBuyPotion(pidCopy, costCopy), potionIcon,
                        () => SelectForDetails(pidCopy, potionDesc, "Consumable", costCopy, potionIcon, isRefund: false));
                    _currentStock.Add((pidCopy, GearKind.Potion)); // record ACTUAL built stock for the bot's assertion
                    built++;
                }
                catch (System.Exception ex) { failed++; FlowTrace.Fail("Store", $"potion row '{pid}' threw: {ex.Message}"); }
            }
            FlowTrace.Step("Store", $"row build complete: {built} rows built, {failed} failed (of {rowCount} planned).");

            FinalizeScroll(); // force the content to size to the rows NOW (anti-collapse)
        }

        // Fixed PIXEL height per row (reference-resolution px). Rows are laid out by a
        // VerticalLayoutGroup (top-down), so this is each row's LayoutElement height and
        // the content auto-grows (ContentSizeFitter) to exactly fit all rows. This
        // replaced the old normalized-fraction placement, whose row anchors were a
        // fraction of the *content* height while the slice was a fraction of the
        // *viewport* — so any list longer than one viewport pushed later rows into
        // NEGATIVE anchor space, off the content, unreachable even by scrolling (the
        // reported "items loaded from the catalog but the panel showed nothing").
        // T-024 thin-panel pass: slimmer rows + tighter gap for a denser, legible,
        // web-suited list (was 86/6 — a chunky mobile-touch height). 58px still
        // comfortably fits the name + cost + BUY/SELL button at the kit's font sizes.
        private const float RowHeightPx = 58f;   // slim row height, fixed px
        private const float RowGapPx    = 3f;    // tight spacing between rows

        // Builds a scrollable list area inside _contentRoot and returns the content
        // transform that rows should be parented to. The content RectTransform is sized
        // to rowCount by a VerticalLayoutGroup + ContentSizeFitter so the ScrollRect can
        // pan through every item. This is the rendering FIX — do not revert it.
        // Vertical scroll, clipped, with a kit Well backing the tray.
        private Transform BuildScrollContent(int rowCount)
        {
            // Recessed well backing — the kit's dark recessed tray so the rows sit in a
            // defined inset. Non-interactive; drawn first so the viewport/rows sit on top.
            var well = ElarionUiKit.Well(_contentRoot.transform, Vector2.zero, Vector2.one);
            var wImg = well.GetComponent<Image>();
            if (wImg != null) wImg.raycastTarget = false;

            // Viewport (clipped) fills the content area. Uses RectMask2D, NOT Mask:
            // the trace proved 23/23 rows are built + visible (alpha 0.84) at correct
            // posY, contentH=1406 over a 596 viewport — yet the list rendered into a
            // "separate area below" the panel. That is a CLIP failure: a stencil Mask
            // with a near-invisible (alpha 0.001) Image can fail to clip, so the
            // 1406px top-anchored content spills 810px BELOW the viewport onto the
            // screen instead of being cut to the viewport rect. RectMask2D does a
            // straight rectangular clip to this rect with no masking-graphic dependency,
            // so the overflow is contained and the ScrollRect pans within the window.
            var viewport = new GameObject("Viewport", typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(_contentRoot.transform, false);
            var vr = viewport.GetComponent<RectTransform>();
            vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one;
            vr.offsetMin = Vector2.zero; vr.offsetMax = Vector2.zero;
            var vImg = viewport.GetComponent<Image>();
            vImg.color = new Color(0f, 0f, 0f, 0.001f); // near-invisible; ScrollRect needs a raycast graphic

            // Content — TOP-anchored, full width, height AUTO-COMPUTED by a
            // ContentSizeFitter from the stacked rows. A VerticalLayoutGroup lays the
            // rows out top-down at a fixed pixel height each, so the content is always
            // exactly tall enough to hold every row (it grows past the viewport => the
            // ScrollRect pans; it stays short => no scroll). This is robust for ANY
            // rowCount, unlike the prior normalized-fraction math (see RowHeightPx note).
            var content = new GameObject("ScrollContent", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var cr = content.GetComponent<RectTransform>();
            // Anchor to the TOP edge, stretched horizontally; pivot top so the layout
            // group fills downward from the top of the viewport.
            cr.anchorMin = new Vector2(0f, 1f);
            cr.anchorMax = new Vector2(1f, 1f);
            cr.pivot = new Vector2(0.5f, 1f);
            cr.anchoredPosition = Vector2.zero;
            cr.sizeDelta = new Vector2(0f, 0f); // height driven by the ContentSizeFitter

            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.spacing = RowGapPx;
            vlg.padding = new RectOffset(3, 3, 3, 3); // T-024: slimmer tray inset
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;     // honor each row's LayoutElement height
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

            // BUG 1 FIX: remember the content so FinalizeScroll() can FORCE the layout
            // pass once the rows exist. The content is TOP-anchored (anchorMin.y==anchorMax.y
            // ==1) with sizeDelta.y starting at 0 — that is ZERO height until the
            // ContentSizeFitter computes the preferred height from the stacked rows. If that
            // layout pass is starved/deferred (it does not always run on the frame a freshly
            // built, inactive-then-active modal populates), the content stays collapsed and
            // EVERY row — gear AND the always-present potions — renders at zero height: the
            // reported "framed store, empty list". Forcing the rebuild after the rows are
            // added guarantees a non-zero content height on the same frame.
            _scrollContent = cr;
            return content.transform;
        }

        // Force the scroll content's layout to compute NOW (after the caller has added all
        // rows), so the ContentSizeFitter drives the content height immediately instead of
        // waiting on a possibly-starved layout pass — the guard against the zero-height
        // collapse that blanked the BUY list. Null-safe + no-op when there is no content.
        private void FinalizeScroll()
        {
            if (_scrollContent == null) return;
            // The whole modal is built AND populated in one synchronous frame (Open -> ShowBuy),
            // before the ScreenSpaceOverlay canvas resolves its screen->canvas rects — so the
            // content's ancestors (panel/content-area/viewport) can still be ZERO-size when the
            // ContentSizeFitter runs, collapsing every row to nothing. Force the canvas rects to
            // resolve FIRST, then rebuild the content layout, so the rows get real height now.
            Canvas.ForceUpdateCanvases();
            var contentArea = _contentRoot != null ? _contentRoot.transform as RectTransform : null;
            if (contentArea != null) LayoutRebuilder.ForceRebuildLayoutImmediate(contentArea);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollContent);
            // DIAGNOSTIC (so a still-empty store is pinpointed, not re-guessed): rows created +
            // the computed heights. rows=0 -> ShowBuy didn't build them; height 0 -> layout/rect;
            // rows>0 & height>0 but invisible -> row visibility (Cell colour / mask clip).
            Debug.Log($"[ShopPanel] FinalizeScroll: rows={_scrollContent.childCount}, " +
                      $"contentH={_scrollContent.rect.height:F0}, " +
                      $"contentAreaH={(contentArea != null ? contentArea.rect.height : -1f):F0}");

            // PER-ROW VISIBILITY (owner directive: trace the contents all the way to the
            // merchant screen). The earlier diagnostic proved rows exist with height, yet the
            // store reads empty -> the rows are present but not VISIBLE. For the first few rows
            // log: actual height, vertical position, whether the row Image is enabled + its alpha,
            // and whether it falls inside the viewport rect. This separates "row tile invisible"
            // (Cell alpha ~0 / Image disabled) from "row clipped outside the mask" (position).
            int n = _scrollContent.childCount;
            float viewportH = contentArea != null ? contentArea.rect.height : 0f;
            int visibleTiles = 0;
            for (int i = 0; i < n; i++)
            {
                var child = _scrollContent.GetChild(i) as RectTransform;
                if (child == null) continue;
                var cimg = child.GetComponent<Image>();
                float a = cimg != null ? cimg.color.a : -1f;
                bool imgOn = cimg != null && cimg.enabled && child.gameObject.activeInHierarchy;
                if (imgOn && a > 0.02f) visibleTiles++;
                if (i < 3)
                    FlowTrace.Step("Store", $"row[{i}] '{child.name}' h={child.rect.height:F0} " +
                        $"posY={child.anchoredPosition.y:F0} active={child.gameObject.activeInHierarchy} " +
                        $"imgEnabled={(cimg != null && cimg.enabled)} alpha={a:F2}");
            }
            FlowTrace.Step("Store", $"render summary: {visibleTiles}/{n} row tiles have a visible Image " +
                $"(alpha>0.02 & enabled & active); viewportH={viewportH:F0}. " +
                $"If visibleTiles>0 but you see nothing -> mask/clip or draw-order; if 0 -> Cell alpha/Image disabled.");
        }

        // A buy row built from kit pieces: a dark-glass Cell panel (LayoutElement-sized
        // for the scroll layout), an optional pack-art icon well on the left, the item
        // name + cost labels, and a green Confirm BUY button. The whole row is also a
        // tap-to-buy Button. iconSprite may be null (no art / gear).
        private void CreateBuyRow(Transform parent, string label, ResourceCost cost, System.Action buyAction, Sprite iconSprite, System.Action selectAction = null)
        {
            var row = new GameObject("BuyRow_" + label, typeof(Image), typeof(Button), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            var le = row.GetComponent<LayoutElement>();
            le.preferredHeight = RowHeightPx;
            le.minHeight = RowHeightPx;
            var rowImg = row.GetComponent<Image>();
            rowImg.color = ElarionUiKit.Cell; // dark-glass row tile
            ElarionUiKit.ApplyRounded(rowImg);
            var rowBtn = row.GetComponent<Button>();
            rowBtn.targetGraphic = rowImg;
            ElarionUiKit.StyleButtonColors(rowBtn);
            // Unified Shop WO #3: the row's existing tap now ALSO populates the details pane
            // (select), in addition to its purchase action. Tapping a row selects + buys; the
            // dedicated BUY button below stays the explicit purchase CTA.
            rowBtn.onClick.AddListener(() => { if (selectAction != null) selectAction(); buyAction(); });

            float nameX0 = 0.04f;
            if (iconSprite != null)
            {
                // Recessed icon well with the pack art (potion bottle), left side.
                var iconWell = ElarionUiKit.AddImage(row.transform, "IconWell",
                    new Vector2(0.03f, 0.15f), new Vector2(0.15f, 0.85f), new Color(0f, 0f, 0f, 0.30f));
                iconWell.GetComponent<Image>().raycastTarget = false;
                var ic = ElarionUiKit.AddImage(iconWell.transform, "Icon",
                    new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f), Color.white, rounded: false);
                var icImg = ic.GetComponent<Image>();
                icImg.sprite = iconSprite;
                icImg.preserveAspect = true;
                icImg.raycastTarget = false;
                nameX0 = 0.17f;
            }
            else
            {
                // Weapons / Armor: use the same TechGearSocket flow as the inventory weapons armor UI
                // for consistent ornate frames from the Tech hud elements pack.
                // Differentiate weapon vs armor using the pack's best frames (Healing Tabs for weapons, Profile for armor)
                bool isWeaponRow = label.Contains("Sword") || label.Contains("Staff") || label.Contains("Bow") || label.Contains("Mace") || label.Contains("Dagger") || label.Contains("Axe");
                var techSock = ElarionUiKit.TechGearSocket(row.transform, "TechGearIcon",
                    new Vector2(0.02f, 0.12f), new Vector2(0.14f, 0.88f),
                    new Color(0.85f, 0.7f, 0.2f, 0.9f), isWeapon: isWeaponRow);
                techSock.GetComponent<Image>().raycastTarget = false;
                nameX0 = 0.17f;
            }

            ElarionUiKit.Label(row.transform, label, 0.15f, 0.85f, ElarionUi.Parchment,
                ElarionUi.FontBody, TMPro.TextAlignmentOptions.Left, nameX0, 0.5f);

            // Price tag tinted by affordability: gold-green when the player can pay,
            // muted danger-red when they cannot (the canonical Affordable/Danger state
            // colours from ElarionUi, so the store speaks the same state language as the
            // rest of the game). Falls back to gilt if the economy isn't resolvable.
            bool affordable = EconomyService.Instance == null || EconomyService.Instance.CanAfford(cost);
            Color priceColor = EconomyService.Instance == null ? ElarionUi.Gilt
                             : (affordable ? ElarionUi.Affordable : ElarionUi.Danger);
            ElarionUiKit.Label(row.transform, CostString(cost), 0.15f, 0.85f, priceColor,
                ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Left, 0.5f, 0.72f, bold: true);

            // BUY is the primary CTA -> use the same TechPrimaryButton flow as the inventory
            // weapons armor UI (ornate Play-button frame from Tech hud elements pack, large
            // thumb target, dark ink on gold). Dimmed when unaffordable.
            var buyBtn = ElarionUiKit.TechPrimaryButton(row.transform, "BUY",
                new Vector2(0.74f, 0.12f), new Vector2(0.98f, 0.88f), buyAction);
            if (buyBtn != null) buyBtn.interactable = affordable;
        }

        // WO-300: enrich a buy-row name with its Elarion maker's mark, so a vendor
        // (Sable / Coppin) surfaces the appraisal at a glance. Additive + read-only:
        // unmarked / ordinary gear shows just its name.
        private string BuyLabel(string baseName, GearAppraisalResult appraisal)
        {
            if (appraisal == null || !appraisal.isElarionMarked) return baseName;
            return $"{baseName}  [{appraisal.makersMark}]";
        }

        private string CostString(ResourceCost c)
        {
            var parts = new List<string>();
            // GOLD is the shop currency — show it first/full. Wood/Iron/Food/Crystals are
            // still rendered (compact suffixes) if a cost ever carries them, for safety.
            if (c.Coins > 0) parts.Add(c.Coins + " Gold");
            if (c.Wood > 0) parts.Add(c.Wood + "W");
            if (c.Iron > 0) parts.Add(c.Iron + "I");
            if (c.Food > 0) parts.Add(c.Food + "F");
            if (c.Crystals > 0) parts.Add(c.Crystals + "C");
            return parts.Count == 0 ? "Free" : string.Join(" ", parts);
        }

        private void TryBuyWeapon(WeaponDef w)
        {
            if (w == null) return;
            var cost = GearCatalog.GetBuyCost(w);
            if (EconomyService.Instance == null) { SetStatus("Economy unavailable."); return; }
            if (EconomyService.Instance.TrySpend(cost))
            {
                if (VillageInventory.Instance != null) VillageInventory.Instance.Add(w.id, 1);
                var ap = GearAppraisal.Appraise(w);
                SetStatus(ap != null && ap.isElarionMarked ? $"Purchased {w.name}! {ap.Summary()} (added to inventory — see EQUIP)" : $"Purchased {w.name}! Added to inventory — see EQUIP.");
                RefreshEco();
                ShowBuy(); // refresh
            }
            else
            {
                SetStatus($"Not enough resources for {w.name} — needs {CostString(cost)}.");
            }
        }

        private void TryBuyArmor(ArmorDef a)
        {
            if (a == null) return;
            var cost = GearCatalog.GetBuyCost(a);
            if (EconomyService.Instance == null) { SetStatus("Economy unavailable."); return; }
            if (EconomyService.Instance.TrySpend(cost))
            {
                if (VillageInventory.Instance != null) VillageInventory.Instance.Add(a.id, 1);
                var ap = GearAppraisal.Appraise(a);
                SetStatus(ap != null && ap.isElarionMarked ? $"Purchased {a.name}! {ap.Summary()} (added to inventory — see EQUIP)" : $"Purchased {a.name}! Added to inventory — see EQUIP.");
                RefreshEco();
                ShowBuy();
            }
            else
            {
                SetStatus($"Not enough resources for {a.name} — needs {CostString(cost)}.");
            }
        }

        private void TryBuyPotion(string id, ResourceCost cost)
        {
            if (EconomyService.Instance == null) { SetStatus("Economy unavailable."); return; }
            if (EconomyService.Instance.TrySpend(cost))
            {
                if (VillageInventory.Instance != null) VillageInventory.Instance.Add(id, 1);
                SetStatus($"Purchased {id}!");
                RefreshEco();
                ShowBuy();
            }
            else SetStatus($"Not enough resources for {id} — needs {CostString(cost)}.");
        }

        // Force the live economy readout to redraw immediately after a transaction. The
        // EconomyService.OnChanged event already does this, but calling it directly keeps
        // the readout correct even if the event order/timing changes. Null-safe.
        private void RefreshEco()
        {
            if (_ecoText != null) UpdateEcoText(_ecoText);

            // Owner: "connect the economy and the HUD to sync on subtract." A purchase
            // deducts from EconomyService (TrySpend), which fires OnChanged -> HeartHudBridge
            // -> HUD.SetResources. But that bridge resolves off the Heart and may not be live
            // in the castle hub, so the town resource bar didn't visibly drop on a buy. Push
            // the fresh snapshot straight to the town HUD here too (idempotent, and the bridge
            // pushes the same values when it IS live) so the subtraction always shows.
            var eco = EconomyService.Instance;
            if (eco != null)
                DeNelle.Core.CoreServices.Hud?.SetResources(eco.Wood, eco.Iron, eco.Food, eco.Crystals);
        }

        // --- SELL ---
        private void ShowSell()
        {
            ClearContent();
            HighlightTab("SELL");
            SetStatus($"Sell owned gear/potions for gold.  Vendor gold: {VendorGold()}.");

            var inv = VillageInventory.Instance;
            if (inv == null) { SetStatus("No inventory."); return; }

            // Build the sellable list first so the scroll content can be sized to it.
            var sellable = new List<string>();
            foreach (var kv in inv.Counts)
            {
                if (kv.Value <= 0) continue;
                string id = kv.Key;
                bool isPotion = _potionIds.Contains(id);
                if (GearCatalog.FindWeapon(id) == null && GearCatalog.FindArmor(id) == null && !isPotion) continue;
                sellable.Add(id);
            }
            var listRoot = BuildScrollContent(sellable.Count);

            foreach (var id in sellable)
            {
                int owned = inv.Get(id);
                WeaponDef w = GearCatalog.FindWeapon(id);
                ArmorDef a = GearCatalog.FindArmor(id);

                string display = (w != null ? w.name : (a != null ? a.name : id)) + " x" + owned;
                // Sell-rate WO #2: Weapons 50% · Armor 50% · Consumables 30% (× base buy price).
                ResourceCost refund = w != null ? ScaleCost(GearCatalog.GetBuyCost(w), 0.50f) :
                                    a != null ? ScaleCost(GearCatalog.GetBuyCost(a), 0.50f) :
                                    ScaleCost(PotionBuyCost(id), 0.30f);

                string idCopy = id; var refundCopy = refund;
                bool isPotionSell = _potionIds.Contains(id);
                string sellDesc = w != null ? DescribeGear(w.job, w.rarity)
                                : a != null ? DescribeGear(a.job, a.rarity)
                                : (isPotionSell ? "Consumable you own." : "Owned item.");
                string sellStats = w != null ? $"+{Mathf.RoundToInt((Mathf.Max(0.1f, w.damageMult) - 1f) * 100f)}% dmg"
                                 : a != null ? $"+{Mathf.RoundToInt(Mathf.Clamp(a.defense, 0f, 0.9f) * 100f)}% def"
                                 : "Consumable";
                var sellIcon = w != null ? ItemIconCatalog.ForWeapon(w)
                             : a != null ? ItemIconCatalog.ForArmor(a)
                             : ItemIconCatalog.ForConsumable(id, id);
                string sellName = w != null ? w.name : (a != null ? a.name : id);
                CreateSellRow(listRoot, display, refundCopy, () => TrySell(idCopy, refundCopy),
                    () => SelectForDetails(sellName, sellDesc, sellStats, refundCopy, sellIcon, isRefund: true));
            }

            FinalizeScroll(); // force the content to size to the rows NOW (anti-collapse)
        }

        private ResourceCost ScaleCost(ResourceCost c, float f)
        {
            return new ResourceCost(
                Mathf.RoundToInt(c.Wood * f),
                Mathf.RoundToInt(c.Food * f),
                Mathf.RoundToInt(c.Iron * f),
                Mathf.RoundToInt(c.Crystals * f),
                Mathf.RoundToInt(c.Coins * f));   // GOLD — ~60% sell refund carries gold
        }

        // A sell row from kit pieces: dark-glass Cell tile + name + bronze refund + a
        // gold SELL button. LayoutElement-sized so the scroll layout stacks it.
        private void CreateSellRow(Transform parent, string label, ResourceCost refund, System.Action sellAction, System.Action selectAction = null)
        {
            var row = new GameObject("SellRow_" + label, typeof(Image), typeof(Button), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            var le = row.GetComponent<LayoutElement>();
            le.preferredHeight = RowHeightPx;
            le.minHeight = RowHeightPx;
            var rowImg = row.GetComponent<Image>();
            rowImg.color = ElarionUiKit.Cell;
            ElarionUiKit.ApplyRounded(rowImg);
            // Unified Shop WO #3: tapping the row body selects it for the details pane (the
            // dedicated SELL button below stays the explicit sell action). No purchase fires
            // from the row body on the sell side, so this is select-only here.
            if (selectAction != null)
            {
                var rowBtn = row.GetComponent<Button>();
                rowBtn.targetGraphic = rowImg;
                ElarionUiKit.StyleButtonColors(rowBtn);
                rowBtn.onClick.AddListener(() => selectAction());
            }

            ElarionUiKit.Label(row.transform, label, 0.15f, 0.85f, ElarionUi.Parchment,
                ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Left, 0.04f, 0.55f);

            // Refund reads as a GAIN -> affordable-green "+" tag (consistent with the
            // buy-side state colours; a sale always credits the player).
            ElarionUiKit.Label(row.transform, "+" + CostString(refund), 0.15f, 0.85f, ElarionUi.Affordable,
                ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Left, 0.55f, 0.72f, bold: true);

            // SELL is the secondary action -> the kit's neutral Quiet glass button (not
            // the gold CTA, so BUY stays the visually dominant primary action).
            ElarionUiKit.Button(row.transform, "SELL", ElarionUiKit.ButtonKind.Quiet,
                new Vector2(0.74f, 0.15f), new Vector2(0.98f, 0.85f), sellAction);
        }

        private void TrySell(string id, ResourceCost refund)
        {
            var inv = VillageInventory.Instance;
            if (inv == null || inv.Get(id) <= 0) return;
            // Vendor gold limit (WO #3): the vendor pays from a finite pool. If it can't
            // cover this sale, refuse with the canon message and bank nothing.
            int price = refund.Coins;
            if (VendorGold() < price)
            {
                SetStatus("I don't have enough coin for that right now.");
                return;
            }
            // Consume one via the standard API (added for crafting/equip flows).
            inv.TryConsume(id, 1);
            SpendVendorGold(price);
            if (EconomyService.Instance != null) EconomyService.Instance.Grant(refund);
            SetStatus($"Sold for +{CostString(refund)}.  (Vendor gold: {VendorGold()})");
            RefreshEco();
            ShowSell();
        }

        // ── Vendor gold pools (WO #3) — each vendor pays sells from a finite purse that
        // resets on game restart (static, in-memory, MVP). Keyed by vendor TYPE so the
        // same storefront shares one purse across opens.
        private static readonly Dictionary<string, int> _vendorGold = new Dictionary<string, int>();
        private string VendorKey()
        {
            string vc = (_vendorContext ?? "").ToLowerInvariant();
            if (vc.Contains("forge")) return "forge";
            if (vc.Contains("blacksmith") || vc.Contains("armor")) return "blacksmith";
            if (vc.Contains("arcane") || vc.Contains("tower") || vc.Contains("magic")) return "arcane";
            return "general";
        }
        private static int StartGoldFor(string key)
        {
            switch (key)
            {
                case "forge":      return 8000;
                case "blacksmith": return 9000;
                case "arcane":     return 7000;
                default:           return 5000;   // General Store
            }
        }
        private int VendorGold()
        {
            string k = VendorKey();
            if (!_vendorGold.ContainsKey(k)) _vendorGold[k] = StartGoldFor(k);
            return _vendorGold[k];
        }
        private void SpendVendorGold(int amt)
        {
            _vendorGold[VendorKey()] = Mathf.Max(0, VendorGold() - amt);
        }
        // Consumable base buy price (matches the ShowBuy potion costs) — for the 30% sell rate.
        private ResourceCost PotionBuyCost(string id)
        {
            return new ResourceCost(coins: (id != null && id.Contains("mana")) ? 12 : 8);
        }

        // --- EQUIP ---
        private void ShowEquip()
        {
            ClearContent();
            HighlightTab("EQUIP");
            SetStatus("Equip owned gear to the active hero (updates visuals + stats).");

            ResolveActiveHero();
            string current = "Current: ";
            if (_activeLoadout != null)
            {
                current += (_activeLoadout.EquippedWeapon != null ? _activeLoadout.EquippedWeapon.name : "no weapon") + " / ";
                current += (_activeLoadout.EquippedArmor != null ? _activeLoadout.EquippedArmor.name : "no armor");
            }
            else current += "none";

            var inv = VillageInventory.Instance;
            if (inv == null) { CreateLabel(_contentRoot.transform, current, 0.92f); return; }

            // Build the equippable list first so the scroll content sizes correctly. The
            // "Current:" line is row 0 inside the scroll content.
            var equippable = new List<string>();
            foreach (var kv in inv.Counts)
            {
                if (kv.Value <= 0) continue;
                string id = kv.Key;
                if (GearCatalog.FindWeapon(id) == null && GearCatalog.FindArmor(id) == null) continue;
                equippable.Add(id);
            }
            var listRoot = BuildScrollContent(equippable.Count + 1); // +1 for the Current line

            CreateLabelRow(listRoot, current);
            foreach (var id in equippable)
            {
                int owned = inv.Get(id);
                var w = GearCatalog.FindWeapon(id);
                string label = (w != null ? w.name : GearCatalog.FindArmor(id).name) + " (owned " + owned + ")";
                string idCopy = id; bool isWeapon = w != null;
                CreateEquipRow(listRoot, label, idCopy, isWeapon);
            }

            FinalizeScroll(); // force the content to size to the rows NOW (anti-collapse)
        }

        // Absolute-positioned label on a NON-layout parent (e.g. _contentRoot directly,
        // the "no inventory" fallback). y is a normalized top anchor within the parent.
        private void CreateLabel(Transform parent, string txt, float y)
        {
            ElarionUiKit.Label(parent, txt, y - 0.06f, y, ElarionUi.ParchmentDim,
                ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Left, 0.02f, 0.98f);
        }

        // Layout-child label for the scroll content (VerticalLayoutGroup). Fixed pixel
        // height via LayoutElement so it stacks like a row and the content sizes to it.
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

        // An equip row from kit pieces: dark-glass Cell tile + name + a gold EQUIP button.
        private void CreateEquipRow(Transform parent, string label, string id, bool isWeapon)
        {
            var row = new GameObject("EquipRow_" + id, typeof(Image), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            var le = row.GetComponent<LayoutElement>();
            le.preferredHeight = RowHeightPx;
            le.minHeight = RowHeightPx;
            var rowImg = row.GetComponent<Image>();
            rowImg.color = ElarionUiKit.Cell;
            ElarionUiKit.ApplyRounded(rowImg);

            ElarionUiKit.Label(row.transform, label, 0.15f, 0.85f, ElarionUi.Parchment,
                ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Left, 0.04f, 0.62f);

            ElarionUiKit.ButtonPack(row.transform, "EQUIP", ElarionUiKit.ButtonKind.Gold,
                new Vector2(0.65f, 0.15f), new Vector2(0.98f, 0.85f), () => TryEquip(id, isWeapon));
        }

        private void TryEquip(string id, bool isWeapon)
        {
            ResolveActiveHero();
            if (_activeLoadout == null)
            {
                var hero = FindActiveHeroGO();
                if (hero != null) _activeLoadout = hero.AddComponent<GearLoadout>();
            }
            if (_activeLoadout == null) { SetStatus("No hero to equip."); return; }

            if (isWeapon)
                _activeLoadout.EquipWeaponById(id);
            else
                _activeLoadout.EquipArmorById(id);

            SetStatus("Equipped. Visuals + stats updated.");
            ShowEquip();
        }

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
            // fallback by name convention
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t != null && t.name.StartsWith("Hero (")) return t.gameObject;
            }
            return null;
        }

        private void ClearContent()
        {
            _scrollContent = null; // the old content is about to be destroyed; drop the stale ref
            if (_contentRoot == null) return;
            int destroyed = _contentRoot.transform.childCount;
            FlowTrace.Step("Store", $"ClearContent: destroying {destroyed} content children (tab switch / re-open).");
            for (int i = _contentRoot.transform.childCount - 1; i >= 0; i--)
            {
                var c = _contentRoot.transform.GetChild(i);
                if (c != null) Destroy(c.gameObject);
            }
        }

        private void Close()
        {
            if (_ecoHandler != null && EconomyService.Instance != null)
                EconomyService.Instance.OnChanged -= _ecoHandler;
            _ecoHandler = null;
            _ecoText = null;
            if (_ui != null) Destroy(_ui);
            _ui = null;
            _contentRoot = null;
            _tabBar = null;
            _scrollContent = null;
        }

        private void OnDestroy()
        {
            if (_ui != null) Destroy(_ui);
        }
    }
}
