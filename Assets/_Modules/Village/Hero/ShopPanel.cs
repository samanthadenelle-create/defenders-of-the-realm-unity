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

            // Backdrop
            var backdrop = new GameObject("Backdrop", typeof(Image));
            backdrop.transform.SetParent(_ui.transform, false);
            var bdRect = backdrop.GetComponent<RectTransform>();
            bdRect.anchorMin = Vector2.zero;
            bdRect.anchorMax = Vector2.one;
            bdRect.offsetMin = Vector2.zero;
            bdRect.offsetMax = Vector2.zero;
            var bdImg = backdrop.GetComponent<Image>();
            bdImg.color = new Color(0.04f, 0.03f, 0.02f, 0.65f);

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
            var tabBar = new GameObject("TabBar");
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
            _contentRoot = new GameObject("Content");
            _contentRoot.transform.SetParent(panel.transform, false);
            var cr = _contentRoot.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0.02f, 0.08f);
            cr.anchorMax = new Vector2(0.98f, 0.76f);
            cr.offsetMin = Vector2.zero;
            cr.offsetMax = Vector2.zero;

            // Close
            CreateBigButton(panel.transform, "Close", new Vector2(0.42f, 0.92f), Close, new Color(0.35f, 0.18f, 0.12f));

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

        private void SetStatus(string s)
        {
            if (_statusText != null) _statusText.text = s;
        }

        // --- BUY ---
        private void ShowBuy()
        {
            ClearContent();
            SetStatus("Buy gear or potions. Cost deducted via Economy.");

            GearCatalog.Reload(); // pick up any live data change

            // De-hardcoded: enumerate the real catalog (weapons.json / armor.json) instead of a
            // fixed starter-id list (which showed nothing if the json ids differed). Vendor
            // flavour: forge sells weapons, armorer sells armor, a generic vendor sells both.
            string ctx = _vendorContext.ToLowerInvariant();
            bool armorerOnly = ctx.Contains("armor");
            bool forgeOnly   = ctx.Contains("forge") || ctx.Contains("blacksmith");
            var weapons = new List<WeaponDef>();
            var armors  = new List<ArmorDef>();
            if (!armorerOnly) foreach (var w in GearCatalog.AllWeapons()) if (w != null) weapons.Add(w);
            if (!forgeOnly)   foreach (var a in GearCatalog.AllArmors())  if (a != null) armors.Add(a);

            float y = 0.92f;
            foreach (var w in weapons)
            {
                CreateBuyRow(_contentRoot.transform, w.name, GearCatalog.GetBuyCost(w), () => TryBuyWeapon(w), ref y);
            }
            foreach (var a in armors)
            {
                CreateBuyRow(_contentRoot.transform, a.name, GearCatalog.GetBuyCost(a), () => TryBuyArmor(a), ref y);
            }
            foreach (var pid in _potionIds)
            {
                var cost = new ResourceCost(wood: 4, iron: 0, crystals: 0); // cheap early potions
                if (pid.Contains("mana")) cost = new ResourceCost(wood: 3, crystals: 1);
                CreateBuyRow(_contentRoot.transform, pid, cost, () => TryBuyPotion(pid, cost), ref y);
            }
        }

        private void CreateBuyRow(Transform parent, string label, ResourceCost cost, System.Action buyAction, ref float y)
        {
            var row = new GameObject("BuyRow_" + label, typeof(Image));
            row.transform.SetParent(parent, false);
            var rr = row.GetComponent<RectTransform>();
            rr.anchorMin = new Vector2(0.02f, y - 0.07f);
            rr.anchorMax = new Vector2(0.98f, y);
            rr.offsetMin = Vector2.zero;
            rr.offsetMax = Vector2.zero;
            row.GetComponent<Image>().color = new Color(0.08f, 0.07f, 0.06f, 0.6f);

            var nameTxt = new GameObject("N", typeof(TMPro.TextMeshProUGUI));
            nameTxt.transform.SetParent(row.transform, false);
            var nr = nameTxt.GetComponent<RectTransform>();
            nr.anchorMin = new Vector2(0.02f, 0.15f); nr.anchorMax = new Vector2(0.48f, 0.85f);
            var nt = nameTxt.GetComponent<TMPro.TextMeshProUGUI>();
            nt.text = label;
            nt.fontSize = 14;
            nt.color = Color.white;

            var priceTxt = new GameObject("P", typeof(TMPro.TextMeshProUGUI));
            priceTxt.transform.SetParent(row.transform, false);
            var pr = priceTxt.GetComponent<RectTransform>();
            pr.anchorMin = new Vector2(0.48f, 0.15f); pr.anchorMax = new Vector2(0.72f, 0.85f);
            var pt = priceTxt.GetComponent<TMPro.TextMeshProUGUI>();
            pt.text = CostString(cost);
            pt.fontSize = 12;
            pt.color = new Color(0.75f, 0.85f, 0.65f);

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

            y -= 0.085f;
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
            if (EconomyService.Instance != null && EconomyService.Instance.TrySpend(cost))
            {
                if (VillageInventory.Instance != null) VillageInventory.Instance.Add(w.id, 1);
                SetStatus($"Bought {w.name}.");
                ShowBuy(); // refresh
            }
            else
            {
                SetStatus("Cannot afford.");
            }
        }

        private void TryBuyArmor(ArmorDef a)
        {
            if (a == null) return;
            var cost = GearCatalog.GetBuyCost(a);
            if (EconomyService.Instance != null && EconomyService.Instance.TrySpend(cost))
            {
                if (VillageInventory.Instance != null) VillageInventory.Instance.Add(a.id, 1);
                SetStatus($"Bought {a.name}.");
                ShowBuy();
            }
            else
            {
                SetStatus("Cannot afford.");
            }
        }

        private void TryBuyPotion(string id, ResourceCost cost)
        {
            if (EconomyService.Instance != null && EconomyService.Instance.TrySpend(cost))
            {
                if (VillageInventory.Instance != null) VillageInventory.Instance.Add(id, 1);
                SetStatus($"Bought {id}.");
                ShowBuy();
            }
            else SetStatus("Cannot afford.");
        }

        // --- SELL ---
        private void ShowSell()
        {
            ClearContent();
            SetStatus("Sell owned gear/potions for partial refund (Economy).");

            var inv = VillageInventory.Instance;
            if (inv == null) { SetStatus("No inventory."); return; }

            float y = 0.92f;
            foreach (var kv in inv.Counts)
            {
                if (kv.Value <= 0) continue;
                string id = kv.Key;
                int owned = kv.Value;

                WeaponDef w = GearCatalog.FindWeapon(id);
                ArmorDef a = GearCatalog.FindArmor(id);
                bool isPotion = _potionIds.Contains(id);

                if (w == null && a == null && !isPotion) continue;

                string display = (w != null ? w.name : (a != null ? a.name : id)) + " x" + owned;
                ResourceCost refund = w != null ? ScaleCost(GearCatalog.GetBuyCost(w), 0.6f) :
                                    a != null ? ScaleCost(GearCatalog.GetBuyCost(a), 0.6f) :
                                    new ResourceCost(wood: 2);

                CreateSellRow(_contentRoot.transform, display, refund, () => TrySell(id, refund), ref y);
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
            rr.anchorMin = new Vector2(0.02f, y - 0.07f);
            rr.anchorMax = new Vector2(0.98f, y);
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

            y -= 0.085f;
        }

        private void TrySell(string id, ResourceCost refund)
        {
            var inv = VillageInventory.Instance;
            if (inv == null || inv.Get(id) <= 0) return;
            // Consume one via the standard API (added for crafting/equip flows).
            inv.TryConsume(id, 1);
            if (EconomyService.Instance != null) EconomyService.Instance.Grant(refund);
            SetStatus("Sold.");
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
            CreateLabel(_contentRoot.transform, current, 0.92f);

            var inv = VillageInventory.Instance;
            if (inv == null) return;

            float y = 0.82f;
            foreach (var kv in inv.Counts)
            {
                if (kv.Value <= 0) continue;
                string id = kv.Key;
                var w = GearCatalog.FindWeapon(id);
                var a = GearCatalog.FindArmor(id);
                if (w == null && a == null) continue;

                string label = (w != null ? w.name : a.name) + " (owned " + kv.Value + ")";
                CreateEquipRow(_contentRoot.transform, label, id, w != null, ref y);
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
            rr.anchorMin = new Vector2(0.02f, y - 0.07f);
            rr.anchorMax = new Vector2(0.98f, y);
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

            y -= 0.085f;
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