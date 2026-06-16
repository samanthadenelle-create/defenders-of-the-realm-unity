using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Village;
using DeNelle.Village.Crafting;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;
using TMPro;

namespace DeNelle.Village.Hero
{
    public sealed class ShopPanel : MonoBehaviour
    {
        private GameObject _ui;
        private string _vendorContext;

        // AutoPilot/test surface — the dev AutoPilotDriver asserts the built BUY stock respects
        // VendorStockContract. Re-exposed for the polished ShopPanel rewrite (additive only).
        public struct StockEntry { public string id; public GearKind kind; }
        private readonly List<StockEntry> _currentStock = new List<StockEntry>();
        /// <summary>Vendor context this panel last opened for (lowercased).</summary>
        public string VendorContext => _vendorContext;
        /// <summary>id + kind of each BUY row built in the last PopulateBuyItems pass.</summary>
        public IReadOnlyList<StockEntry> CurrentStock => _currentStock;

        private GearLoadout _activeLoadout;
        private GameObject _contentRoot;
        private TMPro.TextMeshProUGUI _statusText;
        private TMPro.TextMeshProUGUI _ecoText;
        private System.Action _selectedAction;

        // Preview elements (right panel) - cached for clean updates
        private Image _previewIcon;
        private TMPro.TextMeshProUGUI _previewName;
        private TMPro.TextMeshProUGUI _previewStat1;
        private TMPro.TextMeshProUGUI _previewStat2;
        private TMPro.TextMeshProUGUI _previewFlavor;
        private TMPro.TextMeshProUGUI _previewActionText;  // the button label with price

        // Selection state for highlight + data
        private GameObject _selectedRow;
        private string _selectedId;
        private bool _selectedIsWeapon;
        private int _selectedPrice;
        private Sprite _selectedIcon;

        private GameObject _rightPanel; // for live tuning reposition

        // Mode
        private bool _isBuyMode = true;

        [Header("=== LIVE TUNING - Adjust while playing ===")]
        [Header("Left Scroll List")]
        public Vector2 ListAnchorMin = new Vector2(0.05f, 0.16f);
        public Vector2 ListAnchorMax = new Vector2(0.48f, 0.74f);

        [Header("Right Detail Panel")]
        public Vector2 RightAnchorMin = new Vector2(0.52f, 0.16f);
        public Vector2 RightAnchorMax = new Vector2(0.96f, 0.74f);

        [Header("Item Icon Size")]
        public Vector2 IconSize = new Vector2(160, 160);

        [Header("Row Height in List")]
        public float RowHeight = 78f;

        public void Open(string vendorContext = null, string displayName = null)
        {
            Close();
            _vendorContext = vendorContext?.ToLowerInvariant() ?? "";
            ResolveActiveHero();

            // Hide dialogue
            var dialogueSys = GameObject.Find("DialogueSystem");
            if (dialogueSys != null)
            {
                foreach (var cg in dialogueSys.GetComponentsInChildren<CanvasGroup>(true))
                {
                    if (cg != null)
                    {
                        cg.alpha = 0f;
                        cg.blocksRaycasts = false;
                        cg.interactable = false;
                    }
                }
                foreach (var c in dialogueSys.GetComponentsInChildren<Canvas>(true))
                {
                    if (c != null) c.enabled = false;
                }
            }

            _ui = ElarionUiKit.BuildModalCanvas("ShopPanelUI", 31000);
            var shopCanvas = _ui.GetComponent<Canvas>();
            if (shopCanvas != null) shopCanvas.overrideSorting = true;

            ElarionUiKit.Scrim(_ui.transform, onTapClose: Close);

            ElarionUiKit.AddImage(_ui.transform, "ShopBackdrop", Vector2.zero, Vector2.one,
                new Color(0.02f, 0.015f, 0.012f, 0.94f));

            string title = GetVendorTitle(displayName);

            var panelGo = ElarionUiKit.PanelFramed(_ui.transform, new Vector2(0.12f, 0.06f), new Vector2(0.88f, 0.94f), deep: true);
            var contentParent = panelGo.transform;

            ElarionUiKit.AddImage(contentParent, "ShopSolidFill", new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.97f),
                new Color(0.07f, 0.055f, 0.042f, 0.985f));

            ElarionUiKit.Header(contentParent, title);

            CreateEconomyReadout(contentParent);

            // Compact BUY / SELL mode tabs (Elarion kit, gold/quiet, top of main area)
            ElarionUiKit.ButtonPack(contentParent, "BUY", ElarionUiKit.ButtonKind.Gold,
                new Vector2(0.05f, 0.82f), new Vector2(0.20f, 0.90f), () => ShowBuy());
            ElarionUiKit.ButtonPack(contentParent, "SELL", ElarionUiKit.ButtonKind.Quiet,
                new Vector2(0.22f, 0.82f), new Vector2(0.37f, 0.90f), () => ShowSell());

            // Left list label - directly under BUY tab, constrained to left list column (0.05-0.48)
            ElarionUiKit.Label(contentParent, "Wares", 0.76f, 0.80f, new Color(0.9f, 0.82f, 0.6f), 16, TextAlignmentOptions.Left, 0.05f, 0.48f);

            // Thin gold vertical divider between left list and right (spans list height)
            ElarionUiKit.AddImage(contentParent, "Divider", new Vector2(0.495f, 0.12f), new Vector2(0.505f, 0.81f), new Color(0.85f, 0.7f, 0.3f, 0.8f));

            // Right preview card (Niche for display feel)
            var preview = ElarionUiKit.Niche(contentParent, RightAnchorMin, RightAnchorMax);
            _rightPanel = preview;

            // Large icon (square, centered in right panel)
            var iconGo = new GameObject("PreviewIcon", typeof(Image));
            iconGo.transform.SetParent(preview.transform, false);
            _previewIcon = iconGo.GetComponent<Image>();
            var iRect = _previewIcon.GetComponent<RectTransform>();
            iRect.anchorMin = new Vector2(0.5f, 0.55f);
            iRect.anchorMax = new Vector2(0.5f, 0.55f);
            iRect.sizeDelta = IconSize;
            _previewIcon.preserveAspect = true;

            // Name text directly on top of the Viewer (icon square). Moved upwards (to ~Viewer local Y + higher position).
            _previewName = ElarionUiKit.Label(preview.transform, "Select an item", 0.77f, 0.81f, new Color(0.95f, 0.88f, 0.6f), 20, TextAlignmentOptions.Center); // moved up by ~0.15 in local Y inside the right panel

            // Stats
            _previewStat1 = ElarionUiKit.Label(preview.transform, "Damage: --", 0.12f, 0.40f, Color.white, 16, TextAlignmentOptions.Left);
            _previewStat2 = ElarionUiKit.Label(preview.transform, "Speed: --", 0.12f, 0.34f, Color.white, 16, TextAlignmentOptions.Left);

            // Description below the viewer
            _previewFlavor = ElarionUiKit.Label(preview.transform, "Select an item from the list to see details and price.", 0.12f, 0.20f, new Color(0.8f, 0.78f, 0.72f), 13, TextAlignmentOptions.Left);
            var fRect = _previewFlavor.GetComponent<RectTransform>();
            fRect.sizeDelta = new Vector2(0, 80);

            // Big action button at bottom of preview
            var actionGo = ElarionUiKit.ButtonPack(preview.transform, "Buy for 0 Gold", ElarionUiKit.ButtonKind.Gold,
                new Vector2(0.12f, 0.02f), new Vector2(0.88f, 0.10f),
                () => { if (_selectedAction != null) _selectedAction(); });
            _previewActionText = actionGo.GetComponentInChildren<TMPro.TextMeshProUGUI>();

            // Build the left list (will use ListAnchorMin/Max)
            BuildScrollContent();

            ShowBuy();

            RefreshUI(); // apply any initial tuning
        }

        private string GetVendorTitle(string displayName)
        {
            if (!string.IsNullOrEmpty(displayName)) return displayName;
            string vc = _vendorContext?.ToLowerInvariant() ?? "";
            if (vc.Contains("armor") || vc.Contains("armorer")) return "Armorer's Shop";
            if (vc.Contains("forge") || vc.Contains("blacksmith")) return "Blacksmith's Forge";
            if (vc.Contains("market") || vc.Contains("stall")) return "Market Stalls";
            if (vc.Contains("jewel") || vc.Contains("jeweler")) return "Jeweler's Bench";
            if (vc.Contains("arcane") || vc.Contains("mage") || vc.Contains("tower")) return "Arcane Tower";
            return "Vendor Shop";
        }

        // (BuildDetailsPane removed - all preview now built cleanly in Open using Elarion kit for consistent style)

        private void ShowBuy()
        {
            _isBuyMode = true;
            ClearContent();
            ClearSelection();
            SetStatus("Tap an item to view details and buy.");
            BuildScrollContent();
        }

        private void ShowSell()
        {
            _isBuyMode = false;
            ClearContent();
            ClearSelection();
            SetStatus("Tap equipped gear to sell for gold.");
            BuildScrollContent();
        }

        private void ClearContent()
        {
            if (_contentRoot != null)
                Destroy(_contentRoot);
        }

        private void BuildScrollContent()
        {
            FlowTrace.Step("Shop", $"BuildScrollContent start (isBuy={_isBuyMode}, vendorContext='{_vendorContext}')");

            ClearContent();

            _contentRoot = new GameObject("ItemScrollContent", typeof(RectTransform));
            _contentRoot.transform.SetParent(_ui.transform, false);
            var cr = _contentRoot.GetComponent<RectTransform>();
            // Scrollable rect name: _contentRoot (the one that gets ScrollRect).
            // Make scrollable Y height/position exactly match viewer Y (Right* / _rightPanel) for alignment.
            // X stays from List* (left half ~0.05-0.48).
            Vector2 listMin = new Vector2(ListAnchorMin.x, RightAnchorMin.y);
            Vector2 listMax = new Vector2(ListAnchorMax.x, RightAnchorMax.y);
            cr.anchorMin = listMin;
            cr.anchorMax = listMax;

            var scroll = _contentRoot.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 30f;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(_contentRoot.transform, false);
            var vpRect = viewport.GetComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = new Vector2(0.98f, 1f); // room for thinner scrollbar (1/3 previous width) on the right side of the scrollable rect
            var vpImg = viewport.GetComponent<Image>();
            vpImg.color = new Color(0, 0, 0, 0); // fully transparent - only text rows and subtle gold accents visible

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlHeight = true;

            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = vpRect;
            scroll.content = content.GetComponent<RectTransform>();

            // Thin gold scrollbar on the right side of the scrollable rectangle (_contentRoot).
            // Width reduced to 1/3 of previous (0.01 instead of 0.03) and kept flush on the right edge.
            var sbGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            sbGo.transform.SetParent(_contentRoot.transform, false);
            var sbR = sbGo.GetComponent<RectTransform>();
            sbR.anchorMin = new Vector2(0.98f, 0f);
            sbR.anchorMax = new Vector2(0.99f, 1f);
            sbR.offsetMin = sbR.offsetMax = Vector2.zero;
            var sbBg = sbGo.GetComponent<Image>();
            sbBg.color = new Color(0, 0, 0, 0); // transparent scrollbar track; only gold handle (subtle accent) visible
            var sb = sbGo.GetComponent<Scrollbar>();
            sb.direction = Scrollbar.Direction.BottomToTop;
            var slide = new GameObject("Slide", typeof(RectTransform));
            slide.transform.SetParent(sbGo.transform, false);
            var saR = slide.GetComponent<RectTransform>();
            saR.anchorMin = Vector2.zero; saR.anchorMax = Vector2.one; saR.offsetMin = saR.offsetMax = Vector2.zero;
            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGo.transform.SetParent(slide.transform, false);
            var hR = handleGo.GetComponent<RectTransform>();
            hR.anchorMin = new Vector2(0, 0); hR.anchorMax = new Vector2(1, 0.2f); hR.offsetMin = hR.offsetMax = Vector2.zero;
            var hImg = handleGo.GetComponent<Image>();
            hImg.color = new Color(0.9f, 0.78f, 0.35f, 0.95f); // gold handle
            sb.handleRect = hR;
            sb.targetGraphic = hImg;
            scroll.verticalScrollbar = sb;

            if (_isBuyMode)
                PopulateBuyItems(content.transform);
            else
                PopulateSellItems(content.transform);

            // Critical for "creme text appears blank on transparent rows":
            // The rows use anchored children (the Label) whose size is derived from the row's height.
            // Force the layout system to settle *after* population so the labels get real positive dimensions
            // and TMP actually renders the Parchment creme text. Without this the text rect can stay 0-size.
            FlowTrace.Step("Shop", "Forcing layout rebuild so row labels get sized (fixes blank creme text)");
            Canvas.ForceUpdateCanvases();
            if (scroll.content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);

            FlowTrace.Step("Shop", "BuildScrollContent complete");
        }

        private void PopulateBuyItems(Transform contentParent)
        {
            FlowTrace.Step("Shop", "PopulateBuyItems start");
            _currentStock.Clear();

            // Real data - filtered by vendor contract (best practice, single source of truth)
            var allowed = VendorStockContract.AllowedFor(_vendorContext);
            bool showW = (allowed & GearKind.Weapon) != 0;
            bool showA = (allowed & GearKind.Armor) != 0;

            int added = 0;
            if (showW)
            {
                foreach (var w in GearCatalog.AllWeapons())
                {
                    if (w == null) continue;
                    var rc = GearCatalog.GetBuyCost(w);
                    int price = rc.Coins;
                    Sprite icon = ItemIconCatalog.ForWeapon(w);
                    string stat = $"Damage x{w.damageMult:0.00}";
                    string displayName = w.name ?? w.id;
                    FlowTrace.Once("Shop", $"buy-weapon:{displayName}", $"Adding buy row '{displayName}'");
                    AddShopRow(contentParent, displayName, stat, price, icon, true, w.id);
                    _currentStock.Add(new StockEntry { id = w.id, kind = GearKind.Weapon });
                    added++;
                }
            }
            if (showA)
            {
                foreach (var a in GearCatalog.AllArmors())
                {
                    if (a == null) continue;
                    var rc = GearCatalog.GetBuyCost(a);
                    int price = rc.Coins;
                    Sprite icon = ItemIconCatalog.ForArmor(a);
                    string stat = $"Defense +{a.defense:0.00}";
                    string displayName = a.name ?? a.id;
                    FlowTrace.Once("Shop", $"buy-armor:{displayName}", $"Adding buy row '{displayName}'");
                    AddShopRow(contentParent, displayName, stat, price, icon, false, a.id);
                    _currentStock.Add(new StockEntry { id = a.id, kind = GearKind.Armor });
                    added++;
                }
            }
            FlowTrace.Step("Shop", $"PopulateBuyItems done, added {added} rows");
        }

        private void PopulateSellItems(Transform contentParent)
        {
            FlowTrace.Step("Shop", "PopulateSellItems start");
            if (_activeLoadout == null)
            {
                FlowTrace.Warn("Shop", "PopulateSellItems - no active GearLoadout, nothing to sell");
                return;
            }

            int added = 0;
            if (_activeLoadout.EquippedWeapon != null)
            {
                var w = _activeLoadout.EquippedWeapon;
                var rc = GearCatalog.GetBuyCost(w);
                int price = Mathf.Max(1, rc.Coins / 2);
                Sprite icon = ItemIconCatalog.ForWeapon(w);
                string stat = $"Damage x{w.damageMult:0.00}";
                string displayName = w.name ?? w.id;
                AddShopRow(contentParent, displayName, stat, price, icon, true, w.id);
                added++;
            }
            if (_activeLoadout.EquippedArmor != null)
            {
                var a = _activeLoadout.EquippedArmor;
                var rc = GearCatalog.GetBuyCost(a);
                int price = Mathf.Max(1, rc.Coins / 2);
                Sprite icon = ItemIconCatalog.ForArmor(a);
                string stat = $"Defense +{a.defense:0.00}";
                string displayName = a.name ?? a.id;
                AddShopRow(contentParent, displayName, stat, price, icon, false, a.id);
                added++;
            }
            FlowTrace.Step("Shop", $"PopulateSellItems done, added {added} rows");
        }

        private void AddShopRow(Transform parent, string name, string statLine, int price, Sprite icon, bool isWeapon, string id)
        {
            FlowTrace.Step("Shop", $"AddShopRow name='{name}' id='{id}'");

            // Guard the entire row construction so one bad catalog entry doesn't blank the whole list.
            Guard.Try("Shop", $"row:{name}", () =>
            {
                // Rich row styled to match the desired shop list visual (yellow/gold icon square left,
                // name, red price, yellow BUY button right). The scroll viewport itself stays fully
                // transparent per spec; the individual rows provide subtle dark fill + gold accents
                // (left icon square + bottom hairline) so only the "text rows + gold accents" are the
                // visible list content over the panel fill. Clean, readable, good spacing via RowHeight.
                var row = new GameObject("ShopRow", typeof(RectTransform), typeof(Image), typeof(Button));
                row.transform.SetParent(parent, false);
                var r = row.GetComponent<RectTransform>();
                r.anchorMin = new Vector2(0, 0);
                r.anchorMax = new Vector2(1, 0);
                r.offsetMin = Vector2.zero;
                r.offsetMax = Vector2.zero;

                var le = row.AddComponent<LayoutElement>();
                le.preferredHeight = RowHeight; // live tuning

                // Explicit size so anchored children (icon, labels, button) get real rects immediately.
                r.sizeDelta = new Vector2(0, RowHeight);

                // Very subtle dark row fill (the list viewport + overall scroll area must stay fully transparent
                // per spec — only the text rows and subtle gold accents visible). Low alpha so the rows
                // read as light "text rows" (creme name + red price + gold icon square + gold accent line)
                // over the shop panel fill, not heavy dark bars. Matches "transparent background with creme text".
                var bg = row.GetComponent<Image>();
                bg.color = new Color(0.04f, 0.03f, 0.025f, 0.18f);

                // Left gold/yellow icon square (matches the reference screenshot's left accent blocks).
                // Uses the passed icon sprite if available, else solid gold block as accent.
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(row.transform, false);
                var iconRect = iconGo.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.02f, 0.15f);
                iconRect.anchorMax = new Vector2(0.12f, 0.85f);
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;
                var iconImg = iconGo.GetComponent<Image>();
                iconImg.sprite = icon;
                iconImg.color = icon != null ? Color.white : new Color(0.85f, 0.72f, 0.35f, 1f); // gold accent block
                iconImg.preserveAspect = true;

                // Name (creme / parchment, prominent, left of center).
                var nameLabel = ElarionUiKit.Label(row.transform, name, 0.14f, 0.55f, ElarionUi.Parchment, 16, TextAlignmentOptions.Left, 0.14f, 0.55f);
                nameLabel.verticalAlignment = VerticalAlignmentOptions.Middle;

                // Price in red (matches reference "15 Gold" styling).
                var priceLabel = ElarionUiKit.Label(row.transform, $"{price} Gold", 0.56f, 0.78f,
                    new Color(0.85f, 0.25f, 0.2f, 1f), 14, TextAlignmentOptions.Left, 0.56f, 0.78f);
                priceLabel.verticalAlignment = VerticalAlignmentOptions.Middle;

                // Per-row BUY button (yellow/gold, right side) – immediate transact for this item.
                // Row tap still selects for the right detail viewer.
                var buyBtn = ElarionUiKit.ButtonPack(row.transform, "BUY", ElarionUiKit.ButtonKind.Gold,
                    new Vector2(0.80f, 0.15f), new Vector2(0.98f, 0.85f),
                    () =>
                    {
                        // Immediate buy/sell for this row (no need to select first).
                        var rc = new ResourceCost(coins: price);
                        if (isWeapon) // treat as buy for weapons/armor in this context; sell path similar if needed
                        {
                            if (EconomyService.Instance != null && EconomyService.Instance.TrySpend(rc))
                            {
                                if (_activeLoadout != null) _activeLoadout.EquipWeaponById(id);
                                SetStatus("Purchased!");
                                RefreshCurrentList();
                            }
                            else SetStatus("Not enough gold.");
                        }
                        else
                        {
                            if (EconomyService.Instance != null)
                            {
                                EconomyService.Instance.AddCoins(price);
                                if (_activeLoadout != null) _activeLoadout.EquipArmorById(id);
                                SetStatus("Sold!");
                                RefreshCurrentList();
                            }
                        }
                    });

                // Subtle gold accent line at very bottom (the "subtle gold accents" part of transparent list spec).
                ElarionUiKit.AddImage(row.transform, "GoldAccent", new Vector2(0.01f, 0.01f), new Vector2(0.99f, 0.04f),
                    new Color(0.88f, 0.76f, 0.4f, 0.6f));

                // Row click = select for right detail + highlight (does not auto-buy).
                var rowBtn = row.GetComponent<Button>();
                if (rowBtn == null) rowBtn = row.AddComponent<Button>();
                rowBtn.onClick.AddListener(() => SelectItem(name, statLine, price, icon, isWeapon, id, row));

                FlowTrace.Step("Shop", $"  -> rich row for '{name}' (icon + name + red price + per-row BUY + gold accent)");
            });
        }

        private void SelectItem(string name, string statLine, int price, Sprite icon, bool isWeapon, string id, GameObject rowGO)
        {
            // Clear previous highlight (restore transparent)
            if (_selectedRow != null)
            {
                var prevImg = _selectedRow.GetComponent<Image>();
                if (prevImg != null) prevImg.color = new Color(0f, 0f, 0f, 0f);
            }
            _selectedRow = rowGO;
            if (rowGO != null)
            {
                var img = rowGO.GetComponent<Image>();
                if (img != null) img.color = new Color(0.28f, 0.22f, 0.13f, 0.22f); // subtle gold wash over transparent for selected
            }

            // Store selection
            _selectedId = id;
            _selectedIsWeapon = isWeapon;
            _selectedPrice = price;
            _selectedIcon = icon;

            UpdatePreview(name, statLine, price, icon, isWeapon);

            // Prepare the transact action (buy or sell)
            var rc = new ResourceCost(coins: price);
            if (isWeapon)
            {
                _selectedAction = () =>
                {
                    if (EconomyService.Instance != null && EconomyService.Instance.TrySpend(rc))
                    {
                        if (_activeLoadout != null) _activeLoadout.EquipWeaponById(id);
                        SetStatus("Purchased! List refreshed.");
                        RefreshCurrentList();
                        ClearSelection();
                    }
                    else
                    {
                        SetStatus("Not enough gold.");
                    }
                };
            }
            else
            {
                _selectedAction = () =>
                {
                    if (EconomyService.Instance != null)
                    {
                        EconomyService.Instance.AddCoins(price);
                        if (_activeLoadout != null) _activeLoadout.EquipArmorById(id);
                        SetStatus("Sold! List refreshed.");
                        RefreshCurrentList();
                        ClearSelection();
                    }
                };
            }
        }

        private void UpdatePreview(string name, string statLine, int price, Sprite icon, bool isWeapon)
        {
            if (_previewIcon != null)
            {
                _previewIcon.sprite = icon;
                _previewIcon.color = icon != null ? Color.white : new Color(0.4f, 0.4f, 0.4f, 1f);
            }
            if (_previewName != null) _previewName.text = name;

            float current = 0f;
            string label = isWeapon ? "Damage" : "Defense";
            if (_activeLoadout != null)
            {
                current = isWeapon
                    ? (_activeLoadout.EquippedWeapon != null ? _activeLoadout.EquippedWeapon.damageMult : 1f)
                    : (_activeLoadout.EquippedArmor != null ? _activeLoadout.EquippedArmor.defense : 0f);
            }
            float selected = isWeapon ? 0f : 0f; // parse from statLine or pass better, simple for now
            // For clean display, just show the selected stat + current for comparison
            if (_previewStat1 != null) _previewStat1.text = $"{label}: {statLine}  (current {current:0.00})";
            if (_previewStat2 != null) _previewStat2.text = isWeapon ? "A fine weapon for the realm." : "Solid protection.";

            if (_previewFlavor != null)
            {
                _previewFlavor.text = isWeapon
                    ? "A fine blade. Equip it to boost your damage multiplier."
                    : "Good armor. Equip it to reduce incoming damage.";
            }

            if (_previewActionText != null)
            {
                _previewActionText.text = isWeapon ? $"Buy for {price} Gold" : $"Sell for {price} Gold";
            }
        }

        private void RefreshCurrentList()
        {
            ClearContent();
            BuildScrollContent(); // rebuilds for current _isBuyMode
        }

        private void ClearSelection()
        {
            if (_selectedRow != null)
            {
                var img = _selectedRow.GetComponent<Image>();
                if (img != null) img.color = new Color(0f, 0f, 0f, 0f);
            }
            _selectedRow = null;
            _selectedId = null;
            _selectedAction = null;

            if (_previewName != null) _previewName.text = "Select an item";
            if (_previewStat1 != null) _previewStat1.text = "";
            if (_previewStat2 != null) _previewStat2.text = "";
            if (_previewFlavor != null) _previewFlavor.text = "Select an item from the list to see details.";
            if (_previewActionText != null) _previewActionText.text = "Select an item";
        }

        private void CreateEconomyReadout(Transform parent)
        {
            var go = new GameObject("EcoReadout", typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.05f, 0.88f);
            r.anchorMax = new Vector2(0.95f, 0.93f);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.fontSize = 24;
            t.color = new Color(0.95f, 0.85f, 0.6f);
            t.alignment = TextAlignmentOptions.Center;
            _ecoText = t;
            UpdateEcoText();
        }

        private void UpdateEcoText()
        {
            if (_ecoText == null || EconomyService.Instance == null) return;
            var e = EconomyService.Instance;
            _ecoText.text = $"Gold: {e.Coins}   Wood: {e.Wood}   Iron: {e.Iron}   Food: {e.Food}";
        }

        public void RefreshUI()
        {
            // Re-apply live tuning anchors
            if (_rightPanel != null)
            {
                var pr = _rightPanel.GetComponent<RectTransform>();
                pr.anchorMin = RightAnchorMin;
                pr.anchorMax = RightAnchorMax;
            }
            if (_contentRoot != null)
            {
                // Scrollable Y must match viewer Y (Right*) even on live tweak + RefreshUI
                var cr = _contentRoot.GetComponent<RectTransform>();
                Vector2 listMin = new Vector2(ListAnchorMin.x, RightAnchorMin.y);
                Vector2 listMax = new Vector2(ListAnchorMax.x, RightAnchorMax.y);
                cr.anchorMin = listMin;
                cr.anchorMax = listMax;
            }

            // Rebuild the list content for current mode and selection
            ClearContent();
            BuildScrollContent();

            // Re-apply current selection to preview if any
            if (_selectedId != null && _previewName != null)
            {
                // Simple re-select to refresh preview texts
                // For full live, could store more but this rebuilds list
            }
        }

        // (FindChildByName removed - no longer needed after cleanup of prefab/custom code)

        private void SetStatus(string message)
        {
            if (_statusText == null)
            {
                var statusGo = new GameObject("StatusText", typeof(TextMeshProUGUI));
                statusGo.transform.SetParent(_ui.transform, false);
                var sRect = statusGo.GetComponent<RectTransform>();
                sRect.anchorMin = new Vector2(0.05f, 0.08f);
                sRect.anchorMax = new Vector2(0.45f, 0.13f);
                _statusText = statusGo.GetComponent<TextMeshProUGUI>();
                _statusText.fontSize = 18;
                _statusText.color = new Color(0.9f, 0.8f, 0.6f);
            }
            _statusText.text = message;
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
            return null;
        }
             
        private void Close()
        {
            if (_ui != null) Destroy(_ui);
            _ui = null;
        }

        private void OnDestroy()
        {
            Close();
        }
    }
}