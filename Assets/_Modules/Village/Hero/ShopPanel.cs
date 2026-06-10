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
using DeNelle.Core.UI;

namespace DeNelle.Village.Hero
{
    public sealed class ShopPanel : MonoBehaviour
    {
        // ── LIGHT PARCHMENT palette (matches HeroInventoryController's light feel) ──
        // SELF-CONTAINED to the shop (do NOT mutate the global ElarionUi palette —
        // these are local LIGHT values, a tone inversion of the old dark-glass shop).
        // Warm parchment fills, DARK INK text (readable on light), THIN gilt/rune
        // frames. Gold accent tones borrow ElarionUi.Gold's hue so the merchant skin
        // stays cohesive with the inventory/HUD. ASCII-only runtime strings; WebGL-safe.
        //
        // Panel + surfaces — warm parchment, lightly translucent so the scrim warms it.
        private static readonly Color PanelPaper   = new Color(0.945f, 0.918f, 0.851f, 0.985f); // ~#F1EAD9 main panel
        private static readonly Color HeaderPaper  = new Color(0.965f, 0.945f, 0.890f, 1f);      // brighter header band
        private static readonly Color Scrim        = new Color(0.10f, 0.08f, 0.05f, 0.55f);      // warm dim behind modal
        // Rows — clean light paper; cost/refund tones picked for contrast on paper.
        private static readonly Color RowPaper     = new Color(0.957f, 0.933f, 0.875f, 1f);      // light row paper
        private static readonly Color RowAlt       = new Color(0.929f, 0.902f, 0.831f, 1f);      // (reserved) subtle alt
        private static readonly Color RowHover     = new Color(0.969f, 0.910f, 0.741f, 1f);      // warm gilt-tinted hover
        private static readonly Color RowPress     = new Color(0.835f, 0.886f, 0.741f, 1f);      // soft green confirm press
        private static readonly Color WellPaper    = new Color(0.886f, 0.847f, 0.761f, 0.6f);    // recessed scroll well (faint tan)
        // Tab bar — inactive = soft tan, active = gilt glow with a brighter rim.
        private static readonly Color TabRest      = new Color(0.882f, 0.847f, 0.761f, 1f);      // inactive tab (tan)
        private static readonly Color TabActive    = new Color(0.969f, 0.886f, 0.620f, 1f);      // active tab (gilt glow)
        // Gilt frame accents (thin) — gold rims on a light ground.
        private static readonly Color GiltRim      = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.85f);
        private static readonly Color GiltRimSoft  = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.40f);
        // Action buttons on light: green BUY, bronze SELL, blue EQUIP — kept saturated
        // enough to carry white text legibly.
        private static readonly Color BuyGreen     = new Color(0.30f, 0.55f, 0.28f, 1f);
        private static readonly Color SellBronze   = new Color(0.62f, 0.42f, 0.20f, 1f);
        private static readonly Color EquipBlue    = new Color(0.27f, 0.40f, 0.58f, 1f);
        private static readonly Color CloseRed      = new Color(0.72f, 0.30f, 0.26f, 1f);

        // ── INK text tones (dark, readable on the light parchment) ────────────────
        private static readonly Color Ink          = new Color(0.157f, 0.118f, 0.078f, 1f);      // primary dark ink
        private static readonly Color InkDim        = new Color(0.345f, 0.290f, 0.220f, 1f);     // secondary / flavour ink
        // Bronze heading ink — true gilt is too pale on parchment, so titles read bronze.
        private static readonly Color GiltInk      = new Color(0.521f, 0.380f, 0.102f, 1f);      // bronze heading ink
        // Affordable green ink (cost/refund/eco accents) — darkened to read on paper.
        private static readonly Color CostInk      = new Color(0.255f, 0.412f, 0.235f, 1f);      // green cost ink
        private static readonly Color RefundInk    = new Color(0.541f, 0.392f, 0.157f, 1f);      // bronze refund ink

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
            bdImg.color = Scrim;
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
            pImg.color = PanelPaper;

            // Gilt rune-frame around the panel (thin gold border via an Outline image
            // sitting just behind the panel paper). Drawn as a slightly larger image
            // behind the panel so a 2px gold rim shows on all sides — the cohesive
            // gilt frame from the inventory look, on the light ground.
            AddPanelGiltFrame(panel.transform);

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
            _tabBar = tabBar; // kept so ShowBuy/Sell/Equip can light the active tab

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
            _statusText.color = InkDim; // soft ink, readable on parchment
            _statusText.alignment = TMPro.TextAlignmentOptions.Center;
            SetStatus("Browse wares. Transactions use Wood / Iron / Crystals.");

            // Default to Buy
            ShowBuy();

            Debug.Log($"[ShopPanel] Opened for vendor '{_vendorContext}'. Economy + inventory driven.");
        }

        // Thin gilt rune-frame: a gold image one notch larger than the panel, sitting
        // BEHIND the panel paper so a 2px gold rim peeks out on every side. Cheap, no
        // sprite/9-slice needed, and reads as the inventory's gilt frame on the light
        // parchment. Parented to the panel and pushed to the back of the sibling order.
        private void AddPanelGiltFrame(Transform panel)
        {
            var frame = new GameObject("GiltFrame", typeof(Image));
            frame.transform.SetParent(panel, false);
            frame.transform.SetAsFirstSibling(); // draw behind the panel's children
            var fr = frame.GetComponent<RectTransform>();
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = Vector2.one;
            // grow 3px past the panel on each side so the gold rim shows
            fr.offsetMin = new Vector2(-3f, -3f);
            fr.offsetMax = new Vector2(3f, 3f);
            frame.GetComponent<Image>().color = GiltRim;
            frame.GetComponent<Image>().raycastTarget = false;
            // NOTE: this frame is a child of the panel, so it renders ON TOP of the
            // panel paper, not behind it. To make it a rim, the panel paper is opaque
            // and centered, so the 3px overhang is the only gold visible — a clean
            // gilt border. (Children always draw above their parent's own graphic.)
        }

        // Thin gilt rune-frame around a single item row: a soft-gold image one notch
        // larger than the row, behind the row paper, so a 1px gold rim shows. Mirrors
        // AddPanelGiltFrame at row scale. raycast off so the row button still takes taps.
        private void AddRowGiltRim(Transform row)
        {
            var rim = new GameObject("RowRim", typeof(Image));
            rim.transform.SetParent(row, false);
            rim.transform.SetAsFirstSibling();
            var rr = rim.GetComponent<RectTransform>();
            rr.anchorMin = Vector2.zero;
            rr.anchorMax = Vector2.one;
            rr.offsetMin = new Vector2(-1.5f, -1.5f);
            rr.offsetMax = new Vector2(1.5f, 1.5f);
            rim.GetComponent<Image>().color = GiltRimSoft;
            rim.GetComponent<Image>().raycastTarget = false;
        }

        private void CreateHeader(Transform parent, string txt)
        {
            // Header band — a brighter paper strip with a thin gilt rule beneath, so
            // the title sits in a defined masthead rather than floating on the panel.
            var band = new GameObject("HeaderBand", typeof(Image));
            band.transform.SetParent(parent, false);
            var bandR = band.GetComponent<RectTransform>();
            bandR.anchorMin = new Vector2(0.02f, 0.875f);
            bandR.anchorMax = new Vector2(0.98f, 0.975f);
            bandR.offsetMin = Vector2.zero;
            bandR.offsetMax = Vector2.zero;
            var bandImg = band.GetComponent<Image>();
            bandImg.color = HeaderPaper;
            bandImg.raycastTarget = false;

            // thin gilt rule under the masthead
            var rule = new GameObject("HeaderRule", typeof(Image));
            rule.transform.SetParent(parent, false);
            var ruleR = rule.GetComponent<RectTransform>();
            ruleR.anchorMin = new Vector2(0.06f, 0.873f);
            ruleR.anchorMax = new Vector2(0.94f, 0.879f);
            ruleR.offsetMin = Vector2.zero;
            ruleR.offsetMax = Vector2.zero;
            rule.GetComponent<Image>().color = GiltRimSoft;
            rule.GetComponent<Image>().raycastTarget = false;

            var go = new GameObject("Header", typeof(TMPro.TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.02f, 0.88f);
            r.anchorMax = new Vector2(0.98f, 0.97f);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            var t = go.GetComponent<TMPro.TextMeshProUGUI>();
            t.fontSize = 22;
            t.fontStyle = TMPro.FontStyles.Bold;
            t.characterSpacing = 4f;
            t.color = GiltInk; // bronze ink reads on the light masthead
            t.alignment = TMPro.TextAlignmentOptions.Center;
            // small crest glyph flanking the vendor name for the runic merchant feel
            t.text = ElarionUi.CrestGlyph + "  " + txt + "  " + ElarionUi.CrestGlyph;
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
            t.fontSize = 14;
            t.color = CostInk; // green-bronze resource ink, readable on parchment
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
            img.color = tint ?? TabRest; // inactive tan by default; active set via HighlightTab
            var btn = go.GetComponent<Button>();
            btn.transition = Selectable.Transition.None; // we drive the active glow ourselves
            btn.onClick.AddListener(() => onClick());

            // thin gilt rim under the tab — a soft gold underline strip
            var underline = new GameObject("TabRule", typeof(Image));
            underline.transform.SetParent(go.transform, false);
            var ur = underline.GetComponent<RectTransform>();
            ur.anchorMin = new Vector2(0.1f, 0f); ur.anchorMax = new Vector2(0.9f, 0.06f);
            ur.offsetMin = Vector2.zero; ur.offsetMax = Vector2.zero;
            underline.GetComponent<Image>().color = GiltRimSoft;
            underline.GetComponent<Image>().raycastTarget = false;

            var txt = new GameObject("Label", typeof(TMPro.TextMeshProUGUI));
            txt.transform.SetParent(go.transform, false);
            var tr = txt.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
            var tt = txt.GetComponent<TMPro.TextMeshProUGUI>();
            tt.text = label;
            tt.fontSize = 16;
            tt.fontStyle = TMPro.FontStyles.Bold;
            tt.characterSpacing = 2f;
            tt.color = Ink; // dark ink label, readable on the light tab
            tt.alignment = TMPro.TextAlignmentOptions.Center;
        }

        // Light the active tab with the gilt glow + a bright rim; dim the rest to tan.
        // Called by ShowBuy/ShowSell/ShowEquip so the current mode is always obvious.
        // Null-safe: a no-op until the tab bar exists.
        private void HighlightTab(string activeLabel)
        {
            if (_tabBar == null) return;
            foreach (Transform child in _tabBar.transform)
            {
                if (child == null) continue;
                bool isActive = child.name == "Tab_" + activeLabel;
                var img = child.GetComponent<Image>();
                if (img != null) img.color = isActive ? TabActive : TabRest;
                // brighten the gilt underline on the active tab
                var rule = child.Find("TabRule");
                if (rule != null)
                {
                    var rImg = rule.GetComponent<Image>();
                    if (rImg != null) rImg.color = isActive ? GiltRim : GiltRimSoft;
                }
                // bronze-bold the active label so the text reads selected too
                var lbl = child.Find("Label");
                if (lbl != null)
                {
                    var lt = lbl.GetComponent<TMPro.TextMeshProUGUI>();
                    if (lt != null) lt.color = isActive ? GiltInk : Ink;
                }
            }
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
            img.color = bg ?? TabRest;
            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(() => onClick());

            var txt = new GameObject("L", typeof(TMPro.TextMeshProUGUI));
            txt.transform.SetParent(go.transform, false);
            var tr = txt.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            var tt = txt.GetComponent<TMPro.TextMeshProUGUI>();
            tt.text = label;
            tt.fontSize = 15;
            tt.fontStyle = TMPro.FontStyles.Bold;
            tt.color = Ink; // dark ink on the light button
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
            img.color = CloseRed; // muted brick red — clear dismiss, white X reads on it
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
            // Recessed well backing — a faint tan panel behind the scroll list so the
            // rows sit in a defined tray (the parchment mockup's inset). Non-masking,
            // non-interactive; drawn first so the viewport/rows render on top of it.
            var well = new GameObject("ScrollWell", typeof(Image));
            well.transform.SetParent(_contentRoot.transform, false);
            var wr = well.GetComponent<RectTransform>();
            wr.anchorMin = Vector2.zero; wr.anchorMax = Vector2.one;
            wr.offsetMin = new Vector2(-2f, -2f); wr.offsetMax = new Vector2(2f, 2f);
            well.GetComponent<Image>().color = WellPaper;
            well.GetComponent<Image>().raycastTarget = false;

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
            row.GetComponent<Image>().color = RowPaper;
            AddRowGiltRim(row.transform); // thin rune-frame around the item row
            var rowBtn = row.GetComponent<Button>();
            rowBtn.transition = Selectable.Transition.ColorTint;
            var rowColors = rowBtn.colors;
            rowColors.normalColor = Color.white;     // multiplies the RowPaper image -> stays paper
            rowColors.highlightedColor = RowHover;   // warm gilt-tinted hover
            rowColors.pressedColor = RowPress;       // soft green confirm press
            rowColors.fadeDuration = 0.08f;
            rowBtn.colors = rowColors;
            rowBtn.onClick.AddListener(() => buyAction());

            var nameTxt = new GameObject("N", typeof(TMPro.TextMeshProUGUI));
            nameTxt.transform.SetParent(row.transform, false);
            var nr = nameTxt.GetComponent<RectTransform>();
            nr.anchorMin = new Vector2(0.04f, 0.15f); nr.anchorMax = new Vector2(0.5f, 0.85f);
            var nt = nameTxt.GetComponent<TMPro.TextMeshProUGUI>();
            nt.text = label;
            nt.fontSize = 14;
            nt.color = Ink; // dark ink, readable on light row
            nt.raycastTarget = false; // let the row button receive the tap

            var priceTxt = new GameObject("P", typeof(TMPro.TextMeshProUGUI));
            priceTxt.transform.SetParent(row.transform, false);
            var pr = priceTxt.GetComponent<RectTransform>();
            pr.anchorMin = new Vector2(0.5f, 0.15f); pr.anchorMax = new Vector2(0.72f, 0.85f);
            var pt = priceTxt.GetComponent<TMPro.TextMeshProUGUI>();
            pt.text = CostString(cost);
            pt.fontSize = 13;
            pt.fontStyle = TMPro.FontStyles.Bold;
            pt.color = CostInk; // green cost ink
            pt.raycastTarget = false;

            var buyBtn = new GameObject("Buy", typeof(Button), typeof(Image));
            buyBtn.transform.SetParent(row.transform, false);
            var br = buyBtn.GetComponent<RectTransform>();
            br.anchorMin = new Vector2(0.74f, 0.15f); br.anchorMax = new Vector2(0.98f, 0.85f);
            buyBtn.GetComponent<Image>().color = BuyGreen;
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
            row.GetComponent<Image>().color = RowPaper;
            AddRowGiltRim(row.transform);

            var nameTxt = new GameObject("N", typeof(TMPro.TextMeshProUGUI));
            nameTxt.transform.SetParent(row.transform, false);
            var nr = nameTxt.GetComponent<RectTransform>();
            nr.anchorMin = new Vector2(0.04f, 0.15f); nr.anchorMax = new Vector2(0.55f, 0.85f);
            var nt = nameTxt.GetComponent<TMPro.TextMeshProUGUI>();
            nt.text = label;
            nt.fontSize = 13;
            nt.color = Ink;

            var refTxt = new GameObject("R", typeof(TMPro.TextMeshProUGUI));
            refTxt.transform.SetParent(row.transform, false);
            var pr = refTxt.GetComponent<RectTransform>();
            pr.anchorMin = new Vector2(0.55f, 0.15f); pr.anchorMax = new Vector2(0.72f, 0.85f);
            var pt = refTxt.GetComponent<TMPro.TextMeshProUGUI>();
            pt.text = "+" + CostString(refund);
            pt.fontSize = 13;
            pt.fontStyle = TMPro.FontStyles.Bold;
            pt.color = RefundInk; // bronze refund ink

            var sellBtn = new GameObject("Sell", typeof(Button), typeof(Image));
            sellBtn.transform.SetParent(row.transform, false);
            var br = sellBtn.GetComponent<RectTransform>();
            br.anchorMin = new Vector2(0.74f, 0.15f); br.anchorMax = new Vector2(0.98f, 0.85f);
            sellBtn.GetComponent<Image>().color = SellBronze;
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
            t.color = InkDim; // soft ink, readable on parchment
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
            row.GetComponent<Image>().color = RowPaper;
            AddRowGiltRim(row.transform);

            var nameTxt = new GameObject("N", typeof(TMPro.TextMeshProUGUI));
            nameTxt.transform.SetParent(row.transform, false);
            var nr = nameTxt.GetComponent<RectTransform>();
            nr.anchorMin = new Vector2(0.04f, 0.15f); nr.anchorMax = new Vector2(0.62f, 0.85f);
            var nt = nameTxt.GetComponent<TMPro.TextMeshProUGUI>();
            nt.text = label;
            nt.fontSize = 13;
            nt.color = Ink;

            var eqBtn = new GameObject("Equip", typeof(Button), typeof(Image));
            eqBtn.transform.SetParent(row.transform, false);
            var br = eqBtn.GetComponent<RectTransform>();
            br.anchorMin = new Vector2(0.65f, 0.15f); br.anchorMax = new Vector2(0.98f, 0.85f);
            eqBtn.GetComponent<Image>().color = EquipBlue;
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
            _tabBar = null;
        }

        private void OnDestroy()
        {
            if (_ui != null) Destroy(_ui);
        }
    }
}