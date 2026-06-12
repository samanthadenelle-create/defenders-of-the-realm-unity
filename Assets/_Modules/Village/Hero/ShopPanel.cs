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
        private TMPro.TextMeshProUGUI _statusText; // use TMPro for consistency with EquipmentPanel + to avoid any legacy UI asm quirks
        private TMPro.TextMeshProUGUI _ecoText;
        private System.Action<ResourceSnapshot> _ecoHandler; // stored so Close() can unsubscribe (no leak)

        private readonly List<string> _potionIds = new List<string> { "minor-heal-potion", "minor-mana-potion" };

        public void Open(string vendorContext = null)
        {
            Close();

            _vendorContext = vendorContext == null ? "" : vendorContext;
            ResolveActiveHero();

            // Modal canvas + tap-outside-to-close scrim, both from the shared kit.
            _ui = ElarionUiKit.BuildModalCanvas("ShopPanelUI", 1000);
            ElarionUiKit.Scrim(_ui.transform, onTapClose: Close);

            // Framed dark-glass panel (deep) — the canonical store backboard.
            var panelGo = ElarionUiKit.Panel(_ui.transform, new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f), deep: true);
            var panel = panelGo.transform;

            // Header
            string title = "Vendor Wares";
            if (_vendorContext.ToLowerInvariant().Contains("armor")) title = "Armorer's Shop";
            else if (_vendorContext.ToLowerInvariant().Contains("forge") || _vendorContext.ToLowerInvariant().Contains("blacksmith")) title = "Blacksmith's Forge";
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

            CreateTabButton(tabBar.transform, "BUY", new Vector2(0.02f, 0.32f), () => ShowBuy());
            CreateTabButton(tabBar.transform, "SELL", new Vector2(0.355f, 0.655f), () => ShowSell());
            CreateTabButton(tabBar.transform, "EQUIP", new Vector2(0.69f, 0.98f), () => ShowEquip());
            _tabBar = tabBar; // kept so ShowBuy/Sell/Equip can light the active tab

            // Content area (replaced per mode)
            _contentRoot = new GameObject("Content", typeof(RectTransform));
            _contentRoot.transform.SetParent(panel, false);
            var cr = _contentRoot.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0.02f, 0.08f);
            cr.anchorMax = new Vector2(0.98f, 0.76f);
            cr.offsetMin = Vector2.zero;
            cr.offsetMax = Vector2.zero;

            // Close (X) — top-right corner, kit danger button. Added last so it draws on
            // top of the header/content and always receives the tap.
            ElarionUiKit.Button(panel, "X", ElarionUiKit.ButtonKind.Danger,
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
            SetStatus("Browse wares. Transactions use Wood / Iron / Crystals.");

            // Default to Buy
            ShowBuy();

            Debug.Log($"[ShopPanel] Opened for vendor '{_vendorContext}'. Economy + inventory driven.");
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
            t.text = $"Wood: {e.Wood}   Iron: {e.Iron}   Food: {e.Food}   Crystals: {e.Crystals}";
        }

        // A mode tab built from the shared kit Button (Gold kind). anchorX = (min,max)
        // fractions across the tab bar. The active tab is brightened by HighlightTab.
        private void CreateTabButton(Transform parent, string label, Vector2 anchorX, System.Action onClick)
        {
            ElarionUiKit.Button(parent, label, ElarionUiKit.ButtonKind.Gold,
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
            SetStatus("Buy gear or potions. Tap a row or BUY to purchase.");

            GearCatalog.Reload(); // pick up any live data change

            // De-hardcoded: enumerate the real catalog (weapons.json / armor.json) instead of a
            // fixed starter-id list (which showed nothing if the json ids differed). Vendor
            // flavour: forge sells weapons, armorer sells armor, a generic vendor sells both.
            string ctx = _vendorContext.ToLowerInvariant();
            bool armorerOnly = ctx.Contains("armor");
            bool forgeOnly   = ctx.Contains("forge") || ctx.Contains("blacksmith");

            // Full catalog (null-safe — AllWeapons/AllArmors never return null).
            var allWeapons = new List<WeaponDef>();
            var allArmors  = new List<ArmorDef>();
            foreach (var w in GearCatalog.AllWeapons()) if (w != null) allWeapons.Add(w);
            foreach (var a in GearCatalog.AllArmors())  if (a != null) allArmors.Add(a);

            // Apply the vendor flavour filter.
            var weapons = new List<WeaponDef>();
            var armors  = new List<ArmorDef>();
            if (!armorerOnly) weapons.AddRange(allWeapons);
            if (!forgeOnly)   armors.AddRange(allArmors);

            // WO-406: a vendor must NEVER be empty. If the flavour filter excluded
            // everything BUT the catalog actually has gear (e.g. an "armor"-only vendor
            // whose armor catalog is empty, or a "forge" vendor whose weapon catalog is
            // empty), fall back to the general stock so the player always has wares to
            // buy. Potions are always offered below regardless, so this only guards the
            // gear rows. (If the catalog itself failed to load, allWeapons+allArmors are
            // both empty and only potions show — still a non-empty, usable shop.)
            if (weapons.Count == 0 && armors.Count == 0)
            {
                weapons.AddRange(allWeapons);
                armors.AddRange(allArmors);
                if ((weapons.Count + armors.Count) > 0)
                    Debug.LogWarning($"[ShopPanel] Vendor '{_vendorContext}' filter excluded all gear; falling back to general stock ({weapons.Count} weapons, {armors.Count} armors).");
                else
                    Debug.LogWarning("[ShopPanel] Gear catalog is empty (weapons.json / armor.json failed to load) — only potions will be offered.");
            }

            // Count rows up front so the scroll content can be sized to fit them all.
            int rowCount = weapons.Count + armors.Count + _potionIds.Count;
            var listRoot = BuildScrollContent(rowCount);

            foreach (var w in weapons)
            {
                var wCopy = w; // capture for the closure
                CreateBuyRow(listRoot, BuyLabel(w.name, GearAppraisal.Appraise(w)), GearCatalog.GetBuyCost(w), () => TryBuyWeapon(wCopy), null);
            }
            foreach (var a in armors)
            {
                var aCopy = a; // capture for the closure
                CreateBuyRow(listRoot, BuyLabel(a.name, GearAppraisal.Appraise(a)), GearCatalog.GetBuyCost(a), () => TryBuyArmor(aCopy), null);
            }
            foreach (var pid in _potionIds)
            {
                var cost = new ResourceCost(wood: 4, iron: 0, crystals: 0); // cheap early potions
                if (pid.Contains("mana")) cost = new ResourceCost(wood: 3, crystals: 1);
                string pidCopy = pid; var costCopy = cost; // capture for the closure
                // Pack potion art on the row when present (red heal / blue mana bottle).
                var potionIcon = RpgUiCatalog.Get(RpgUiCatalog.RolePotion,
                    pid.Contains("mana") ? RpgUiCatalog.PotionMana : RpgUiCatalog.PotionHealth);
                CreateBuyRow(listRoot, pidCopy, costCopy, () => TryBuyPotion(pidCopy, costCopy), potionIcon);
            }
        }

        // Fixed PIXEL height per row (reference-resolution px). Rows are laid out by a
        // VerticalLayoutGroup (top-down), so this is each row's LayoutElement height and
        // the content auto-grows (ContentSizeFitter) to exactly fit all rows. This
        // replaced the old normalized-fraction placement, whose row anchors were a
        // fraction of the *content* height while the slice was a fraction of the
        // *viewport* — so any list longer than one viewport pushed later rows into
        // NEGATIVE anchor space, off the content, unreachable even by scrolling (the
        // reported "items loaded from the catalog but the panel showed nothing").
        private const float RowHeightPx = 86f;   // ~RowSlice * panel-content height, fixed px
        private const float RowGapPx    = 6f;    // spacing between rows

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

            // Viewport (masked) fills the content area.
            var viewport = new GameObject("Viewport", typeof(Image), typeof(Mask), typeof(ScrollRect));
            viewport.transform.SetParent(_contentRoot.transform, false);
            var vr = viewport.GetComponent<RectTransform>();
            vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one;
            vr.offsetMin = Vector2.zero; vr.offsetMax = Vector2.zero;
            var vImg = viewport.GetComponent<Image>();
            vImg.color = new Color(0f, 0f, 0f, 0.001f); // near-invisible but a valid mask graphic
            var mask = viewport.GetComponent<Mask>();
            mask.showMaskGraphic = false;

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
            vlg.padding = new RectOffset(6, 6, 6, 6);
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

            return content.transform;
        }

        // A buy row built from kit pieces: a dark-glass Cell panel (LayoutElement-sized
        // for the scroll layout), an optional pack-art icon well on the left, the item
        // name + cost labels, and a green Confirm BUY button. The whole row is also a
        // tap-to-buy Button. iconSprite may be null (no art / gear).
        private void CreateBuyRow(Transform parent, string label, ResourceCost cost, System.Action buyAction, Sprite iconSprite)
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
            rowBtn.onClick.AddListener(() => buyAction());

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

            // BUY is the primary CTA -> the gold kit button (dark-ink label). Dimmed +
            // non-interactable when unaffordable so the affordance reads disabled.
            var buyBtn = ElarionUiKit.Button(row.transform, "BUY", ElarionUiKit.ButtonKind.Gold,
                new Vector2(0.74f, 0.15f), new Vector2(0.98f, 0.85f), buyAction);
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
        }

        // --- SELL ---
        private void ShowSell()
        {
            ClearContent();
            HighlightTab("SELL");
            SetStatus("Sell owned gear/potions for partial refund (Economy).");

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
                ResourceCost refund = w != null ? ScaleCost(GearCatalog.GetBuyCost(w), 0.6f) :
                                    a != null ? ScaleCost(GearCatalog.GetBuyCost(a), 0.6f) :
                                    new ResourceCost(wood: 2);

                string idCopy = id; var refundCopy = refund;
                CreateSellRow(listRoot, display, refundCopy, () => TrySell(idCopy, refundCopy));
            }
        }

        private ResourceCost ScaleCost(ResourceCost c, float f)
        {
            return new ResourceCost(
                Mathf.RoundToInt(c.Wood * f),
                Mathf.RoundToInt(c.Food * f),
                Mathf.RoundToInt(c.Iron * f),
                Mathf.RoundToInt(c.Crystals * f));
        }

        // A sell row from kit pieces: dark-glass Cell tile + name + bronze refund + a
        // gold SELL button. LayoutElement-sized so the scroll layout stacks it.
        private void CreateSellRow(Transform parent, string label, ResourceCost refund, System.Action sellAction)
        {
            var row = new GameObject("SellRow_" + label, typeof(Image), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            var le = row.GetComponent<LayoutElement>();
            le.preferredHeight = RowHeightPx;
            le.minHeight = RowHeightPx;
            var rowImg = row.GetComponent<Image>();
            rowImg.color = ElarionUiKit.Cell;
            ElarionUiKit.ApplyRounded(rowImg);

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
            // Consume one via the standard API (added for crafting/equip flows).
            inv.TryConsume(id, 1);
            if (EconomyService.Instance != null) EconomyService.Instance.Grant(refund);
            SetStatus($"Sold for +{CostString(refund)}.");
            RefreshEco();
            ShowSell();
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

            ElarionUiKit.Button(row.transform, "EQUIP", ElarionUiKit.ButtonKind.Gold,
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
            if (_contentRoot == null) return;
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
        }

        private void OnDestroy()
        {
            if (_ui != null) Destroy(_ui);
        }
    }
}
