// =============================================================================
// ShopPanel — basic working vendor / gear shop UI (buy, sell, equip).
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
// Keep simple + functional per chunk request.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Village;
using DeNelle.Village.Crafting;

namespace DeNelle.Village.Hero
{
    public sealed class ShopPanel : MonoBehaviour
    {
        private GameObject _ui;
        private string _vendorContext;
        private GearLoadout _activeLoadout;
        private GameObject _contentRoot;
        private TMPro.TextMeshProUGUI _statusText; // use TMPro for consistency with EquipmentPanel + to avoid any legacy UI asm quirks
        private TMPro.TextMeshProUGUI _ecoText;
        private System.Action<ResourceSnapshot> _ecoHandler; // stored so Close() can unsubscribe (no leak)

        private readonly List<string> _potionIds = new List<string> { "minor-heal-potion", "minor-mana-potion" };

        public void Open(string vendorContext = null)
        {
            Close();

            _vendorContext = vendorContext ?? "";
            ResolveActiveHero();

            _ui = new GameObject("ShopPanelUI");
            _ui.transform.SetParent(null, false);

            var canvas = _ui.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var scaler = _ui.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            _ui.AddComponent<GraphicRaycaster>();

            // Backdrop / scrim — full-screen, blocks click-through AND closes on tap-outside.
            // Mirrors HeroInventoryController's scrim: the backdrop is itself a Button wired to
            // Close, so tapping anywhere outside the panel dismisses the shop. (Previously this
            // was a plain Image with no dismiss, so if the small Close button was missed there
            // was no way out of the modal.)
            var backdrop = new GameObject("Backdrop", typeof(Image), typeof(Button));
            backdrop.transform.SetParent(_ui.transform, false);
            var bdRect = backdrop.GetComponent<RectTransform>();
            bdRect.anchorMin = Vector2.zero;
            bdRect.anchorMax = Vector2.one;
            bdRect.offsetMin = Vector2.zero;
            bdRect.offsetMax = Vector2.zero;
            var bdImg = backdrop.GetComponent<Image>();
            bdImg.color = new Color(0.04f, 0.03f, 0.02f, 0.65f);
            var bdBtn = backdrop.GetComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;
            bdBtn.onClick.AddListener(Close);

            // Panel frame
            var panel = new GameObject("Panel", typeof(Image));
            panel.transform.SetParent(_ui.transform, false);
            var pr = panel.GetComponent<RectTransform>();
            pr.anchorMin = new Vector2(0.1f, 0.1f);
            pr.anchorMax = new Vector2(0.9f, 0.9f);
            pr.offsetMin = Vector2.zero;
            pr.offsetMax = Vector2.zero;
            var pImg = panel.GetComponent<Image>();
            pImg.color = new Color(0.12f, 0.09f, 0.07f, 0.97f);

            // Header
            string title = "Vendor Wares";
            if (_vendorContext.ToLowerInvariant().Contains("armor")) title = "Armorer's Shop";
            else if (_vendorContext.ToLowerInvariant().Contains("forge") || _vendorContext.ToLowerInvariant().Contains("blacksmith")) title = "Blacksmith's Forge";
            CreateHeader(panel.transform, title);

            // Economy readout (live)
            CreateEconomyReadout(panel.transform);

            // Tabs / mode buttons
            var tabBar = new GameObject("TabBar", typeof(RectTransform));
            tabBar.transform.SetParent(panel.transform, false);
            var tbRect = tabBar.GetComponent<RectTransform>();
            tbRect.anchorMin = new Vector2(0.02f, 0.78f);
            tbRect.anchorMax = new Vector2(0.98f, 0.86f);
            tbRect.offsetMin = Vector2.zero;
            tbRect.offsetMax = Vector2.zero;

            CreateTabButton(tabBar.transform, "BUY", new Vector2(-0.33f, 0), () => ShowBuy());
            CreateTabButton(tabBar.transform, "SELL", new Vector2(0f, 0), () => ShowSell());
            CreateTabButton(tabBar.transform, "EQUIP", new Vector2(0.33f, 0), () => ShowEquip());

            // Content area (replaced per mode)
            _contentRoot = new GameObject("Content", typeof(RectTransform));
            _contentRoot.transform.SetParent(panel.transform, false);
            var cr = _contentRoot.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0.02f, 0.08f);
            cr.anchorMax = new Vector2(0.98f, 0.76f);
            cr.offsetMin = Vector2.zero;
            cr.offsetMax = Vector2.zero;

            // Close (X) — top-right corner, large + unmistakable. This is the primary dismiss,
            // mirroring HeroInventoryController's top-right "X". It sits inside the panel (always
            // on top of the content) and is wired straight to Close().
            CreateCloseX(panel.transform);

            // Status line
            var statusGo = new GameObject("Status", typeof(TMPro.TextMeshProUGUI));
            statusGo.transform.SetParent(panel.transform, false);
            var sRect = statusGo.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0.02f, 0.01f);
            sRect.anchorMax = new Vector2(0.98f, 0.07f);
            _statusText = statusGo.GetComponent<TMPro.TextMeshProUGUI>();
            _statusText.fontSize = 14;
            _statusText.color = new Color(0.85f, 0.8f, 0.7f);
            _statusText.alignment = TMPro.TextAlignmentOptions.Center;
            SetStatus("Browse wares. Transactions use Wood / Iron / Crystals.");

            // Default to Buy
            ShowBuy();

            Debug.Log($"[ShopPanel] Opened for vendor '{_vendorContext}'. Economy + inventory driven.");
        }

        private void CreateHeader(Transform parent, string txt)
        {
            var go = new GameObject("Header", typeof(TMPro.TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.02f, 0.88f);
            r.anchorMax = new Vector2(0.98f, 0.97f);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            var t = go.GetComponent<TMPro.TextMeshProUGUI>();
            t.fontSize = 22;
            t.color = new Color(0.95f, 0.88f, 0.7f);
            t.alignment = TMPro.TextAlignmentOptions.Center;
            t.text = txt;
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
            t.fontSize = 13;
            t.color = new Color(0.7f, 0.85f, 0.65f);
            t.alignment = TMPro.TextAlignmentOptions.Center;
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

        private void CreateTabButton(Transform parent, string label, Vector2 anchorX, System.Action onClick, Color? tint = null)
        {
            var go = new GameObject("Tab_" + label, typeof(Button), typeof(Image));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.5f + anchorX.x - 0.14f, 0.1f);
            r.anchorMax = new Vector2(0.5f + anchorX.x + 0.14f, 0.9f);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = tint ?? new Color(0.22f, 0.17f, 0.12f);
            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(() => onClick());

            var txt = new GameObject("Label", typeof(TMPro.TextMeshProUGUI));
            txt.transform.SetParent(go.transform, false);
            var tr = txt.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
            var tt = txt.GetComponent<TMPro.TextMeshProUGUI>();
            tt.text = label;
            tt.fontSize = 16;
            tt.color = Color.white;
            tt.alignment = TMPro.TextAlignmentOptions.Center;
        }

        private void CreateBigButton(Transform parent, string label, Vector2 anchor, System.Action onClick, Color? bg = null)
        {
            var go = new GameObject("Btn_" + label, typeof(Button), typeof(Image));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(anchor.x - 0.08f, anchor.y - 0.03f);
            r.anchorMax = new Vector2(anchor.x + 0.08f, anchor.y + 0.03f);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = bg ?? new Color(0.18f, 0.14f, 0.10f);
            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(() => onClick());

            var txt = new GameObject("L", typeof(TMPro.TextMeshProUGUI));
            txt.transform.SetParent(go.transform, false);
            var tr = txt.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            var tt = txt.GetComponent<TMPro.TextMeshProUGUI>();
            tt.text = label;
            tt.fontSize = 15;
            tt.color = new Color(0.95f, 0.9f, 0.8f);
            tt.alignment = TMPro.TextAlignmentOptions.Center;
        }

        // Top-right close (X) — the primary, unmistakable dismiss. Large square touch target in
        // the panel's top-right corner, wired directly to Close(). Added last in Open() so it
        // draws on top of the header/content and always receives the tap.
        private void CreateCloseX(Transform parent)
        {
            var go = new GameObject("CloseX", typeof(Button), typeof(Image));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.88f, 0.90f);
            r.anchorMax = new Vector2(0.985f, 0.985f);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = new Color(0.45f, 0.18f, 0.14f, 0.95f);
            var btn = go.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(Close);

            var txt = new GameObject("X", typeof(TMPro.TextMeshProUGUI));
            txt.transform.SetParent(go.transform, false);
            var tr = txt.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
            var tt = txt.GetComponent<TMPro.TextMeshProUGUI>();
            tt.text = "X";
            tt.fontSize = 24;
            tt.fontStyle = TMPro.FontStyles.Bold;
            tt.color = Color.white;
            tt.alignment = TMPro.TextAlignmentOptions.Center;
            tt.raycastTarget = false; // let the button image take the tap
        }

        private void SetStatus(string s)
        {
            if (_statusText != null) _statusText.text = s;
        }

        // --- BUY ---
        private void ShowBuy()
        {
            ClearContent();
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

            float y = 0.999f;
            foreach (var w in weapons)
            {
                var wCopy = w; // capture for the closure
                CreateBuyRow(listRoot, BuyLabel(w.name, GearAppraisal.Appraise(w)), GearCatalog.GetBuyCost(w), () => TryBuyWeapon(wCopy), ref y);
            }
            foreach (var a in armors)
            {
                var aCopy = a; // capture for the closure
                CreateBuyRow(listRoot, BuyLabel(a.name, GearAppraisal.Appraise(a)), GearCatalog.GetBuyCost(a), () => TryBuyArmor(aCopy), ref y);
            }
            foreach (var pid in _potionIds)
            {
                var cost = new ResourceCost(wood: 4, iron: 0, crystals: 0); // cheap early potions
                if (pid.Contains("mana")) cost = new ResourceCost(wood: 3, crystals: 1);
                string pidCopy = pid; var costCopy = cost; // capture for the closure
                CreateBuyRow(listRoot, pidCopy, costCopy, () => TryBuyPotion(pidCopy, costCopy), ref y);
            }
        }

        // Row height as a fraction of the SCROLL CONTENT's height (not the viewport).
        // Each row occupies this normalized slice; the content is grown to fit all rows
        // so nothing is pushed off-panel and every BUY button is reachable.
        private const float RowSlice = 0.085f;

        // Builds a scrollable list area inside _contentRoot and returns the content
        // transform that rows should be parented to. Rows are still anchored from the
        // top (y starting at ~1.0) — the content RectTransform is sized to rowCount so
        // the ScrollRect can pan through every item. Without this the fixed _contentRoot
        // clipped/overflowed long catalogs and most BUY buttons fell off the panel
        // (the reported "no way to complete a purchase"). Vertical scroll, clipped.
        private Transform BuildScrollContent(int rowCount)
        {
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

            // Content — grown taller than the viewport when there are many rows so the
            // list can scroll. height multiplier = how many "viewport heights" of rows.
            var content = new GameObject("ScrollContent", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var cr = content.GetComponent<RectTransform>();
            // Anchor to the top of the viewport, stretch horizontally, pivot at top.
            cr.anchorMin = new Vector2(0f, 1f);
            cr.anchorMax = new Vector2(1f, 1f);
            cr.pivot = new Vector2(0.5f, 1f);
            // Total rows take rowCount * RowSlice of viewport height. If that is <= 1 the
            // list fits and the content equals the viewport (size delta 0). Otherwise grow.
            float totalSlices = Mathf.Max(1f, rowCount * RowSlice);
            // sizeDelta.y in normalized-to-viewport terms: a value of 0 == viewport height.
            // We express extra height as (totalSlices - 1) * viewportHeight via a layout-free
            // trick: set anchors to top edge and use sizeDelta height = extra fraction * a
            // reference. Use the panel reference resolution height slice for a stable size.
            float viewportPixels = 1920f * (0.76f - 0.08f); // content area height in ref px
            cr.sizeDelta = new Vector2(0f, Mathf.Max(0f, (totalSlices - 1f) * viewportPixels));

            var scroll = viewport.GetComponent<ScrollRect>();
            scroll.viewport = vr;
            scroll.content = cr;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25f;

            return content.transform;
        }

        private void CreateBuyRow(Transform parent, string label, ResourceCost cost, System.Action buyAction, ref float y)
        {
            // Whole row is a Button now (tap-to-buy) AND keeps an explicit BUY button — both
            // route to the same purchase action, so "selecting an item" completes the buy.
            var row = new GameObject("BuyRow_" + label, typeof(Image), typeof(Button));
            row.transform.SetParent(parent, false);
            var rr = row.GetComponent<RectTransform>();
            rr.anchorMin = new Vector2(0.02f, y - RowSlice);
            rr.anchorMax = new Vector2(0.98f, y - 0.005f);
            rr.offsetMin = Vector2.zero;
            rr.offsetMax = Vector2.zero;
            row.GetComponent<Image>().color = new Color(0.08f, 0.07f, 0.06f, 0.6f);
            var rowBtn = row.GetComponent<Button>();
            rowBtn.transition = Selectable.Transition.ColorTint;
            var rowColors = rowBtn.colors;
            rowColors.highlightedColor = new Color(0.16f, 0.18f, 0.12f, 0.8f);
            rowColors.pressedColor = new Color(0.12f, 0.22f, 0.12f, 0.9f);
            rowBtn.colors = rowColors;
            rowBtn.onClick.AddListener(() => buyAction());

            var nameTxt = new GameObject("N", typeof(TMPro.TextMeshProUGUI));
            nameTxt.transform.SetParent(row.transform, false);
            var nr = nameTxt.GetComponent<RectTransform>();
            nr.anchorMin = new Vector2(0.02f, 0.15f); nr.anchorMax = new Vector2(0.48f, 0.85f);
            var nt = nameTxt.GetComponent<TMPro.TextMeshProUGUI>();
            nt.text = label;
            nt.fontSize = 14;
            nt.color = Color.white;
            nt.raycastTarget = false; // let the row button receive the tap

            var priceTxt = new GameObject("P", typeof(TMPro.TextMeshProUGUI));
            priceTxt.transform.SetParent(row.transform, false);
            var pr = priceTxt.GetComponent<RectTransform>();
            pr.anchorMin = new Vector2(0.48f, 0.15f); pr.anchorMax = new Vector2(0.72f, 0.85f);
            var pt = priceTxt.GetComponent<TMPro.TextMeshProUGUI>();
            pt.text = CostString(cost);
            pt.fontSize = 12;
            pt.color = new Color(0.75f, 0.85f, 0.65f);
            pt.raycastTarget = false;

            var buyBtn = new GameObject("Buy", typeof(Button), typeof(Image));
            buyBtn.transform.SetParent(row.transform, false);
            var br = buyBtn.GetComponent<RectTransform>();
            br.anchorMin = new Vector2(0.74f, 0.15f); br.anchorMax = new Vector2(0.98f, 0.85f);
            buyBtn.GetComponent<Image>().color = new Color(0.18f, 0.32f, 0.18f);
            var bb = buyBtn.GetComponent<Button>();
            bb.onClick.AddListener(() => buyAction());

            var bl = new GameObject("BL", typeof(TMPro.TextMeshProUGUI));
            bl.transform.SetParent(buyBtn.transform, false);
            var blr = bl.GetComponent<RectTransform>();
            blr.anchorMin = Vector2.zero; blr.anchorMax = Vector2.one;
            var blt = bl.GetComponent<TMPro.TextMeshProUGUI>();
            blt.text = "BUY";
            blt.fontSize = 13;
            blt.color = Color.white;
            blt.alignment = TMPro.TextAlignmentOptions.Center;
            blt.raycastTarget = false;

            y -= RowSlice;
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

            float y = 0.999f;
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
                CreateSellRow(listRoot, display, refundCopy, () => TrySell(idCopy, refundCopy), ref y);
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

        private void CreateSellRow(Transform parent, string label, ResourceCost refund, System.Action sellAction, ref float y)
        {
            var row = new GameObject("SellRow_" + label, typeof(Image));
            row.transform.SetParent(parent, false);
            var rr = row.GetComponent<RectTransform>();
            rr.anchorMin = new Vector2(0.02f, y - RowSlice);
            rr.anchorMax = new Vector2(0.98f, y - 0.005f);
            rr.offsetMin = Vector2.zero;
            rr.offsetMax = Vector2.zero;
            row.GetComponent<Image>().color = new Color(0.09f, 0.06f, 0.05f, 0.6f);

            var nameTxt = new GameObject("N", typeof(TMPro.TextMeshProUGUI));
            nameTxt.transform.SetParent(row.transform, false);
            var nr = nameTxt.GetComponent<RectTransform>();
            nr.anchorMin = new Vector2(0.02f, 0.15f); nr.anchorMax = new Vector2(0.55f, 0.85f);
            var nt = nameTxt.GetComponent<TMPro.TextMeshProUGUI>();
            nt.text = label;
            nt.fontSize = 13;
            nt.color = Color.white;

            var refTxt = new GameObject("R", typeof(TMPro.TextMeshProUGUI));
            refTxt.transform.SetParent(row.transform, false);
            var pr = refTxt.GetComponent<RectTransform>();
            pr.anchorMin = new Vector2(0.55f, 0.15f); pr.anchorMax = new Vector2(0.72f, 0.85f);
            var pt = refTxt.GetComponent<TMPro.TextMeshProUGUI>();
            pt.text = "+" + CostString(refund);
            pt.fontSize = 12;
            pt.color = new Color(0.85f, 0.75f, 0.55f);

            var sellBtn = new GameObject("Sell", typeof(Button), typeof(Image));
            sellBtn.transform.SetParent(row.transform, false);
            var br = sellBtn.GetComponent<RectTransform>();
            br.anchorMin = new Vector2(0.74f, 0.15f); br.anchorMax = new Vector2(0.98f, 0.85f);
            sellBtn.GetComponent<Image>().color = new Color(0.32f, 0.18f, 0.12f);
            var bb = sellBtn.GetComponent<Button>();
            bb.onClick.AddListener(() => sellAction());

            var bl = new GameObject("BL", typeof(TMPro.TextMeshProUGUI));
            bl.transform.SetParent(sellBtn.transform, false);
            var blr = bl.GetComponent<RectTransform>();
            blr.anchorMin = Vector2.zero; blr.anchorMax = Vector2.one;
            var blt = bl.GetComponent<TMPro.TextMeshProUGUI>();
            blt.text = "SELL";
            blt.fontSize = 13;
            blt.color = Color.white;
            blt.alignment = TMPro.TextAlignmentOptions.Center;

            y -= RowSlice;
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

            float y = 0.999f;
            CreateLabel(listRoot, current, y); y -= RowSlice;
            foreach (var id in equippable)
            {
                int owned = inv.Get(id);
                var w = GearCatalog.FindWeapon(id);
                string label = (w != null ? w.name : GearCatalog.FindArmor(id).name) + " (owned " + owned + ")";
                string idCopy = id; bool isWeapon = w != null;
                CreateEquipRow(listRoot, label, idCopy, isWeapon, ref y);
            }
        }

        private void CreateLabel(Transform parent, string txt, float y)
        {
            var go = new GameObject("Label", typeof(TMPro.TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.02f, y - 0.06f);
            r.anchorMax = new Vector2(0.98f, y);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            var t = go.GetComponent<TMPro.TextMeshProUGUI>();
            t.text = txt;
            t.fontSize = 13;
            t.color = new Color(0.9f, 0.85f, 0.7f);
        }

        private void CreateEquipRow(Transform parent, string label, string id, bool isWeapon, ref float y)
        {
            var row = new GameObject("EquipRow_" + id, typeof(Image));
            row.transform.SetParent(parent, false);
            var rr = row.GetComponent<RectTransform>();
            rr.anchorMin = new Vector2(0.02f, y - RowSlice);
            rr.anchorMax = new Vector2(0.98f, y - 0.005f);
            rr.offsetMin = Vector2.zero;
            rr.offsetMax = Vector2.zero;
            row.GetComponent<Image>().color = new Color(0.07f, 0.06f, 0.05f, 0.5f);

            var nameTxt = new GameObject("N", typeof(TMPro.TextMeshProUGUI));
            nameTxt.transform.SetParent(row.transform, false);
            var nr = nameTxt.GetComponent<RectTransform>();
            nr.anchorMin = new Vector2(0.02f, 0.15f); nr.anchorMax = new Vector2(0.62f, 0.85f);
            var nt = nameTxt.GetComponent<TMPro.TextMeshProUGUI>();
            nt.text = label;
            nt.fontSize = 13;
            nt.color = Color.white;

            var eqBtn = new GameObject("Equip", typeof(Button), typeof(Image));
            eqBtn.transform.SetParent(row.transform, false);
            var br = eqBtn.GetComponent<RectTransform>();
            br.anchorMin = new Vector2(0.65f, 0.15f); br.anchorMax = new Vector2(0.98f, 0.85f);
            eqBtn.GetComponent<Image>().color = new Color(0.22f, 0.28f, 0.38f);
            var bb = eqBtn.GetComponent<Button>();
            bb.onClick.AddListener(() => TryEquip(id, isWeapon));

            var bl = new GameObject("BL", typeof(TMPro.TextMeshProUGUI));
            bl.transform.SetParent(eqBtn.transform, false);
            var blr = bl.GetComponent<RectTransform>();
            blr.anchorMin = Vector2.zero; blr.anchorMax = Vector2.one;
            var blt = bl.GetComponent<TMPro.TextMeshProUGUI>();
            blt.text = "EQUIP";
            blt.fontSize = 13;
            blt.color = Color.white;
            blt.alignment = TMPro.TextAlignmentOptions.Center;

            y -= RowSlice;
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
        }

        private void OnDestroy()
        {
            if (_ui != null) Destroy(_ui);
        }
    }
}