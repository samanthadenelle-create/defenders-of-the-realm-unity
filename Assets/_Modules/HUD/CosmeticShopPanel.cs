// =============================================================================
// CosmeticShopPanel - the in-game Cosmetic Shop UI. Opened via its world
// interactable (Marketplace). Category tabs (Hero / Pet / Village) + a
// scrollable card list; each card shows the preview, name, description,
// Glimmer price (with DEF-197 "short by N" honesty) and ONE action button
// (Buy / Equip / Equipped / Locked).
// -----------------------------------------------------------------------------
// WO-F conversion (2026-07-03, coverage matrix row #4): UIDocument/UITK ->
// code-built uGUI on the Obsidian master frame (BuildObsidianModal:
// FrameMerchant + coin medallion + the ONE shared Close + scrim), per the
// HelpMenu reference recipe. Scroll list composed inline (ScrollRect +
// VerticalLayoutGroup) like LeaderboardPanel.
//
// Reflection bridge UNCHANGED: DeNelle.HUD does not reference DeNelle.Cosmetics
// (asmdef isolation). Catalog + service resolved lazily by name; any miss =
// "shop unavailable" so the HUD compiles even with Cosmetics stripped.
//
// Spawned by CosmeticShopPanelBootstrap once a scene has a hero.
// =============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;

namespace DeNelle.HUD
{
    [DisallowMultipleComponent]
    public sealed class CosmeticShopPanel : MonoBehaviour
    {
        private ElarionUiKit.ObsidianModal _modal;
        private Transform _tabHost;
        private Transform _listContent;      // ScrollRect content
        private TextMeshProUGUI _glimmerLabel;
        private ElarionUiKit.ToastParts _toast;
        private float _toastUntil;
        private string _activeCategory = "hero";
        private bool _open;

        // Reflection handles into DeNelle.Cosmetics. Cached after first hit.
        private static Type s_serviceType;
        private static PropertyInfo s_instanceProp;
        private static PropertyInfo s_glimmerProp;
        private static MethodInfo s_ownsMethod;
        private static MethodInfo s_equippedForMethod;
        private static MethodInfo s_tryPurchaseMethod;
        private static MethodInfo s_equipMethod;
        private static EventInfo s_changedEvent;

        private static Type s_catalogType;
        private static MethodInfo s_byCategoryMethod;

        private static Type s_defType;
        private static FieldInfo s_defIdField;
        private static FieldInfo s_defCategoryField;
        private static FieldInfo s_defDisplayNameField;
        private static FieldInfo s_defDescriptionField;
        private static FieldInfo s_defGlimmerCostField;
        private static FieldInfo s_defUnlockMethodField;
        private static PropertyInfo s_defPreviewColorProp;

        private object _serviceInstance;
        private Delegate _changedHandler;
        // Modal arbiter handle (DEF-212). CloseOverlay is the close action.
        private PanelHandle _panelHandle;

        private void Awake()
        {
            _panelHandle = PanelManager.Register("Cosmetic Shop", CloseOverlay, IsOverlayOpen);
            // DEF-213: let the Marketplace / Store interaction open this panel by id.
            PanelRouter.Register(PanelId.CosmeticShop, OpenOverlay);
        }

        private bool IsOverlayOpen() => _open;

        private void OnEnable()
        {
            ResolveBridge();
            _serviceInstance = ResolveServiceInstance();
            SubscribeChanged();
        }

        private void OnDisable()
        {
            UnsubscribeChanged();
        }

        private void OnDestroy()
        {
            PanelRouter.Unregister(PanelId.CosmeticShop, OpenOverlay);
            if (_modal != null && _modal.canvas != null) Destroy(_modal.canvas);
        }

        private void Update()
        {
            // WO-437: no global hotkey — opens only via the Marketplace interactable.
            if (_toast != null && _toast.card != null && _toastUntil > 0f
                && Time.unscaledTime > _toastUntil)
            {
                _toast.card.SetActive(false);
                _toastUntil = 0f;
            }
        }

        // ─── Reflection bridge (UNCHANGED) ───────────────────────────────────

        private static void ResolveBridge()
        {
            if (s_serviceType != null && s_catalogType != null && s_defType != null) return;
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (s_serviceType == null)
                        s_serviceType = asm.GetType("DeNelle.Cosmetics.GlimmerCurrencyService", false);
                    if (s_catalogType == null)
                        s_catalogType = asm.GetType("DeNelle.Cosmetics.CosmeticCatalog", false);
                    if (s_defType == null)
                        s_defType = asm.GetType("DeNelle.Cosmetics.CosmeticDef", false);
                    if (s_serviceType != null && s_catalogType != null && s_defType != null) break;
                }

                if (s_serviceType != null)
                {
                    s_instanceProp = s_serviceType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    s_glimmerProp  = s_serviceType.GetProperty("Glimmer", BindingFlags.Public | BindingFlags.Instance);
                    s_ownsMethod          = s_serviceType.GetMethod("Owns",       new[] { typeof(string) });
                    s_equippedForMethod   = s_serviceType.GetMethod("EquippedFor", new[] { typeof(string) });
                    s_tryPurchaseMethod   = s_serviceType.GetMethod("TryPurchase", new[] { typeof(string) });
                    s_equipMethod         = s_serviceType.GetMethod("Equip",       new[] { typeof(string) });
                    s_changedEvent        = s_serviceType.GetEvent("Changed");
                }
                if (s_catalogType != null)
                {
                    s_byCategoryMethod = s_catalogType.GetMethod("ByCategory",
                        BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                }
                if (s_defType != null)
                {
                    s_defIdField           = s_defType.GetField("Id");
                    s_defCategoryField     = s_defType.GetField("Category");
                    s_defDisplayNameField  = s_defType.GetField("DisplayName");
                    s_defDescriptionField  = s_defType.GetField("Description");
                    s_defGlimmerCostField  = s_defType.GetField("GlimmerCost");
                    s_defUnlockMethodField = s_defType.GetField("UnlockMethod");
                    s_defPreviewColorProp  = s_defType.GetProperty("PreviewUnityColor");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CosmeticShopPanel] reflection bridge resolve failed: " + ex.Message);
            }
        }

        private object ResolveServiceInstance()
        {
            try { return s_instanceProp?.GetValue(null); }
            catch (Exception e) { FlowTrace.Warn("CosmeticShop", $"ResolveServiceInstance reflected get threw: {e.GetType().Name}: {e.Message}"); return null; }
        }

        private int CurrentGlimmer()
        {
            if (_serviceInstance == null || s_glimmerProp == null) return 0;
            try { return (int)s_glimmerProp.GetValue(_serviceInstance); }
            catch (Exception e) { FlowTrace.Warn("CosmeticShop", $"CurrentGlimmer reflected get threw: {e.GetType().Name}: {e.Message}"); return 0; }
        }

        private bool OwnsId(string id)
        {
            if (_serviceInstance == null || s_ownsMethod == null) return false;
            try { return (bool)s_ownsMethod.Invoke(_serviceInstance, new object[] { id }); }
            catch (Exception e) { FlowTrace.Warn("CosmeticShop", $"OwnsId('{id}') reflected call threw: {e.GetType().Name}: {e.Message}"); return false; }
        }

        private string EquippedFor(string category)
        {
            if (_serviceInstance == null || s_equippedForMethod == null) return null;
            try { return s_equippedForMethod.Invoke(_serviceInstance, new object[] { category }) as string; }
            catch (Exception e) { FlowTrace.Warn("CosmeticShop", $"EquippedFor('{category}') reflected call threw: {e.GetType().Name}: {e.Message}"); return null; }
        }

        private bool TryPurchase(string id)
        {
            if (_serviceInstance == null || s_tryPurchaseMethod == null) return false;
            try { return (bool)s_tryPurchaseMethod.Invoke(_serviceInstance, new object[] { id }); }
            catch (Exception e) { FlowTrace.Warn("CosmeticShop", $"TryPurchase('{id}') reflected call threw: {e.GetType().Name}: {e.Message}"); return false; }
        }

        private void EquipId(string id)
        {
            if (_serviceInstance == null || s_equipMethod == null) return;
            try { s_equipMethod.Invoke(_serviceInstance, new object[] { id }); }
            catch (Exception ex)
            {
                FlowTrace.Warn("CosmeticShop", $"EquipId('{id}') reflected call threw: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private IEnumerable CatalogByCategory(string category)
        {
            if (s_byCategoryMethod == null) yield break;
            object result;
            try { result = s_byCategoryMethod.Invoke(null, new object[] { category }); }
            catch (Exception e) { FlowTrace.Warn("CosmeticShop", $"CatalogByCategory('{category}') reflected call threw: {e.GetType().Name}: {e.Message}"); yield break; }
            if (result is IEnumerable seq)
                foreach (var item in seq) yield return item;
        }

        private void SubscribeChanged()
        {
            if (_serviceInstance == null || s_changedEvent == null) return;
            try
            {
                Action onChanged = Repaint;
                _changedHandler = Delegate.CreateDelegate(s_changedEvent.EventHandlerType,
                    onChanged.Target, onChanged.Method);
                var addMethod = s_changedEvent.GetAddMethod();
                addMethod?.Invoke(_serviceInstance, new object[] { _changedHandler });
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CosmeticShopPanel] Changed subscribe failed: " + ex.Message);
                _changedHandler = null;
            }
        }

        private void UnsubscribeChanged()
        {
            if (_serviceInstance == null || s_changedEvent == null || _changedHandler == null) return;
            try
            {
                var removeMethod = s_changedEvent.GetRemoveMethod();
                removeMethod?.Invoke(_serviceInstance, new object[] { _changedHandler });
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("CosmeticShop", $"UnsubscribeChanged reflected call threw: {ex.GetType().Name}: {ex.Message}");
            }
            _changedHandler = null;
        }

        // ─── UI construction (kit modal, lazy on first open) ─────────────────

        private void EnsureBuilt()
        {
            if (_modal != null && _modal.canvas != null) return;
            using var _ = FlowTrace.Enter("CosmeticShop", "BuildUi");

            // PORTRAIT sizing (UI review 05): Merchant_Panel is a PORTRAIT frame (~1005x1507). Anchor
            // to a narrow, tall center column so the rendered aspect matches the template instead of
            // stretching the ornate frame into a landscape slab.
            // Shared store size (owner felt-test 2026-07-15: all stores same size / matching Y).
            _modal = ElarionUiKit.BuildObsidianModal("CosmeticShopUI", "Cosmetic Shop",
                ElarionUiKit.StorePanelAnchorMin, ElarionUiKit.StorePanelAnchorMax, CloseOverlay,
                frameName: RpgUiCatalog.FrameMerchant, medallionIcon: "coin");

            var layout = _modal.chrome.layout;
            var body = layout != null && layout.body != null
                ? (Transform)layout.body
                : _modal.chrome.content.transform;

            // Mobile-first (owner rule): compact CENTERED column, not full-bleed edge-to-edge
            // bars — the tabs + card list share one thumb-zone band (0.10–0.90) so cards read as
            // centered plates with side margins on a phone.
            const float BandMin = 0.10f, BandMax = 0.90f;

            // Glimmer balance — its OWN band at the top of the body well. EYES-SWEEP 2026-07-06
            // (#6): the chip lived in the frame's HEADER zone (0.70–0.99) where the centered
            // "Cosmetic Shop" title painted straight over it on the narrow portrait frame. The
            // title keeps the header; the balance gets the first body band, right-aligned + fitted.
            _glimmerLabel = MakeText(body, "0", 16, ElarionUi.Gold, FontStyles.Bold,
                TextAlignmentOptions.Right, new Vector2(BandMin, 0.955f), new Vector2(BandMax, 0.995f));
            ElarionUiKit.FitSingleLine(_glimmerLabel);

            // Category tabs — shifted down one band to make room for the balance line.
            _tabHost = ZoneRect(body, "TabRail", new Vector2(BandMin, 0.875f), new Vector2(BandMax, 0.945f));
            BuildTabs();

            // Scrollable card list.
            var scrollHost = ZoneRect(body, "CardScroll", new Vector2(BandMin, 0.09f), new Vector2(BandMax, 0.865f));
            _listContent = BuildScrollColumn(scrollHost);

            // Anti-FOMO footer (spec Section 9).
            MakeText(body, "Beauty is earned, never required.", 12, ElarionUi.ParchmentDim,
                FontStyles.Italic, TextAlignmentOptions.Center,
                new Vector2(0.10f, 0.01f), new Vector2(0.90f, 0.07f));

            // Toast — low center of the modal canvas.
            _toast = ElarionUiKit.ToastCard(_modal.canvas.transform,
                ElarionUiKit.ToastTone.Gold, accentLeft: true, TextAnchor.MiddleCenter);
            var trt = _toast.card.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.20f, 0.015f);
            trt.anchorMax = new Vector2(0.80f, 0.075f);
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            _toast.card.SetActive(false);

            _modal.canvas.SetActive(false);   // built hidden; OpenOverlay shows it
        }

        private void BuildTabs()
        {
            for (int i = _tabHost.childCount - 1; i >= 0; i--)
                Destroy(_tabHost.GetChild(i).gameObject);
            AddTab("Hero", "hero", 0);
            AddTab("Pet", "pet", 1);
            AddTab("Village", "village", 2);
        }

        private void AddTab(string label, string category, int index)
        {
            float x0 = 0.005f + index * (1f / 3f);
            float x1 = x0 + (1f / 3f) - 0.01f;
            ElarionUiKit.BuildObsidianButton(_tabHost, label,
                ElarionUiKit.ObsidianButtonStyle.Style1,
                category == _activeCategory ? ElarionUiKit.ObsidianButtonColor.Yellow
                                            : ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(x0, 0.05f), new Vector2(x1, 0.95f),
                () => { _activeCategory = category; BuildTabs(); Repaint(); });
        }

        private void Repaint()
        {
            if (!_open || _modal == null || _listContent == null) return;

            // Refresh service handle in case the bootstrap raced us.
            if (_serviceInstance == null) _serviceInstance = ResolveServiceInstance();

            if (_glimmerLabel != null)
                // WO-697: currency through the ONE kit formatter (compact >= 10k, no N0 grouping).
                _glimmerLabel.text = ElarionUi.CompactNumber(CurrentGlimmer()) + "  Glimmer";

            for (int i = _listContent.childCount - 1; i >= 0; i--)
                Destroy(_listContent.GetChild(i).gameObject);

            int cardCount = 0;
            foreach (var def in CatalogByCategory(_activeCategory))
            {
                if (def == null) continue;
                BuildCard(def);
                cardCount++;
            }
            if (cardCount == 0)
            {
                // Empty-data roll-up: a category with zero cards is shown AND self-reported,
                // so a missing/empty catalog never reads as a blank panel with no trace.
                FlowTrace.Warn("CosmeticShop",
                    $"Repaint: category '{_activeCategory}' produced 0 cosmetic cards — " +
                    "showing the visible empty-state placeholder (data-empty).");
                var rowGo = new GameObject("Empty", typeof(RectTransform), typeof(LayoutElement));
                rowGo.transform.SetParent(_listContent, false);
                rowGo.GetComponent<LayoutElement>().preferredHeight = 48f;
                MakeText(rowGo.transform, "Nothing here yet - check back next season.", 14,
                    ElarionUi.ParchmentDim, FontStyles.Italic, TextAlignmentOptions.Center,
                    Vector2.zero, Vector2.one);
            }
        }

        private void BuildCard(object def)
        {
            string id          = s_defIdField?.GetValue(def) as string ?? string.Empty;
            string category    = s_defCategoryField?.GetValue(def) as string ?? string.Empty;
            string displayName = s_defDisplayNameField?.GetValue(def) as string ?? id;
            string description = s_defDescriptionField?.GetValue(def) as string ?? string.Empty;
            int glimmerCost    = SafeInt(s_defGlimmerCostField?.GetValue(def));
            string unlockMethod = (s_defUnlockMethodField?.GetValue(def) as string ?? "buy").ToLowerInvariant();
            Color preview      = SafeColor(s_defPreviewColorProp?.GetValue(def));

            bool owned    = OwnsId(id);
            bool equipped = owned && string.Equals(EquippedFor(category), id, StringComparison.OrdinalIgnoreCase);
            bool isAchievement = unlockMethod == "achievement";

            // DEF-197: affordability computed once so the price line + button agree.
            bool isBuyable  = !owned && !isAchievement && glimmerCost > 0;
            int  balance    = CurrentGlimmer();
            bool affordable = balance >= glimmerCost;
            int  shortfall  = isBuyable && !affordable ? glimmerCost - balance : 0;

            var cardGo = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            cardGo.transform.SetParent(_listContent, false);
            cardGo.GetComponent<LayoutElement>().preferredHeight = 92f;
            var bg = cardGo.GetComponent<Image>();
            var slotSprite = RpgUiCatalog.Get(RpgUiCatalog.RoleSlot, "slot_item");
            if (slotSprite != null) { bg.sprite = slotSprite; bg.type = Image.Type.Sliced; bg.color = Color.white; }
            else bg.color = new Color(0f, 0f, 0f, 0.35f);

            // Preview tile — real render when present, tinted swatch fallback.
            var iconGo = new GameObject("Preview", typeof(RectTransform));
            iconGo.transform.SetParent(cardGo.transform, false);
            var irt = iconGo.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0.015f, 0.14f);
            irt.anchorMax = new Vector2(0.115f, 0.86f);
            irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
            var tex = ResolvePreviewTexture(def);
            if (tex != null)
            {
                var raw = iconGo.AddComponent<RawImage>();
                raw.texture = tex;
                raw.raycastTarget = false;
            }
            else
            {
                var tile = iconGo.AddComponent<Image>();
                tile.color = preview;
                tile.raycastTarget = false;
            }

            // Name / description / price column.
            MakeText(cardGo.transform, displayName, 16, ElarionUi.Parchment, FontStyles.Bold,
                TextAlignmentOptions.Left, new Vector2(0.13f, 0.60f), new Vector2(0.72f, 0.95f));
            MakeText(cardGo.transform, description, 12, ElarionUi.ParchmentDim, FontStyles.Italic,
                TextAlignmentOptions.TopLeft, new Vector2(0.13f, 0.30f), new Vector2(0.72f, 0.58f));

            string priceText;
            if (isAchievement) priceText = owned ? "Earned" : "Earn via play";
            else if (glimmerCost > 0) priceText = $"{ElarionUi.CompactNumber(glimmerCost)} Glimmer" + (shortfall > 0 ? $"   (short {ElarionUi.CompactNumber(shortfall)})" : "");   // WO-697
            else priceText = "Free";
            Color priceTint = (isBuyable && !affordable) ? ElarionUi.ParchmentDim : ElarionUi.Gold;
            MakeText(cardGo.transform, priceText, 13, priceTint, FontStyles.Normal,
                TextAlignmentOptions.Left, new Vector2(0.13f, 0.05f), new Vector2(0.72f, 0.28f));

            // ONE action button — state machine unchanged.
            if (equipped)
            {
                var b = ElarionUiKit.BuildObsidianButton(cardGo.transform, "Equipped",
                    ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Green,
                    new Vector2(0.74f, 0.28f), new Vector2(0.985f, 0.72f), null);
                b.interactable = false;
            }
            else if (owned)
            {
                ElarionUiKit.BuildObsidianButton(cardGo.transform, "Equip",
                    ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Yellow,
                    new Vector2(0.74f, 0.28f), new Vector2(0.985f, 0.72f),
                    () => { EquipId(id); ShowToast($"Equipped {displayName}"); Repaint(); });
            }
            else if (isAchievement)
            {
                var b = ElarionUiKit.BuildObsidianButton(cardGo.transform, "Locked",
                    ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                    new Vector2(0.74f, 0.28f), new Vector2(0.985f, 0.72f), null);
                b.interactable = false;
            }
            else
            {
                var b = ElarionUiKit.BuildObsidianButton(cardGo.transform, "Buy",
                    ElarionUiKit.ObsidianButtonStyle.Style1,
                    affordable ? ElarionUiKit.ObsidianButtonColor.Yellow : ElarionUiKit.ObsidianButtonColor.Gray,
                    new Vector2(0.74f, 0.28f), new Vector2(0.985f, 0.72f),
                    () =>
                    {
                        if (TryPurchase(id)) { ShowToast($"Unlocked {displayName}"); EquipId(id); }
                        else ShowToast("Not enough Glimmer.");
                        Repaint();
                    });
                b.interactable = affordable;
            }
        }

        // Optional real preview render, loaded by cosmetic id from Resources. Cached.
        private static readonly Dictionary<string, Texture2D> s_previewCache = new Dictionary<string, Texture2D>();

        private Texture2D ResolvePreviewTexture(object def)
        {
            string id = s_defIdField?.GetValue(def) as string;
            if (string.IsNullOrEmpty(id)) return null;
            if (s_previewCache.TryGetValue(id, out var cached)) return cached;
            Texture2D tex = null;
            try { tex = Resources.Load<Texture2D>($"Cosmetics/Previews/{id}"); }
            catch (Exception ex)
            {
                FlowTrace.Warn("CosmeticShop", $"ResolvePreviewTexture('{id}') threw: {ex.GetType().Name}: {ex.Message} — using swatch fallback.");
                tex = null;
            }
            s_previewCache[id] = tex;
            return tex;
        }

        private static int SafeInt(object value)
        {
            if (value == null) return 0;
            try { return Convert.ToInt32(value); } catch (Exception e) { FlowTrace.Warn("CosmeticShop", $"SafeInt convert threw: {e.GetType().Name}: {e.Message}"); return 0; }
        }

        private static Color SafeColor(object value)
        {
            if (value is Color c) return c;
            return new Color(0.55f, 0.55f, 0.55f, 1f);
        }

        // ─── Visibility ──────────────────────────────────────────────────────

        public void ToggleOverlay()
        {
            if (_open) CloseOverlay();
            else OpenOverlay();
        }

        public void OpenOverlay()
        {
            using var _ = FlowTrace.Enter("CosmeticShop", "OpenOverlay");
            EnsureBuilt();
            if (_modal == null || _modal.canvas == null)
            {
                FlowTrace.Fail("CosmeticShop", "OpenOverlay: kit modal failed to build — open is a no-op.");
                return;
            }
            _open = true;
            _modal.canvas.SetActive(true);
            // Tell the arbiter we're open; it closes any other panel first. Battle-lock
            // may reject — revert and stay hidden.
            if (!PanelManager.NotifyOpened(_panelHandle))
            {
                _open = false;
                _modal.canvas.SetActive(false);
                return;
            }
            Repaint();
        }

        public void CloseOverlay()
        {
            if (_modal == null || _modal.canvas == null) { _open = false; return; }
            _open = false;
            _modal.canvas.SetActive(false);
            PanelManager.NotifyClosed(_panelHandle);
        }

        private void ShowToast(string message)
        {
            if (_toast == null || _toast.card == null || _toast.label == null) return;
            _toast.label.text = message;
            _toast.card.SetActive(true);
            _toastUntil = Time.unscaledTime + 3f;
        }

        // ─── uGUI helpers (same shapes as LeaderboardPanel) ──────────────────

        private static Transform ZoneRect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return go.transform;
        }

        private static Transform BuildScrollColumn(Transform host)
        {
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(RectMask2D), typeof(Image));
            scrollGo.transform.SetParent(host, false);
            var srt = scrollGo.GetComponent<RectTransform>();
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
            srt.offsetMin = Vector2.zero; srt.offsetMax = Vector2.zero;
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(scrollGo.transform, false);
            var crt = contentGo.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = Vector2.one;
            crt.pivot = new Vector2(0.5f, 1f);
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
            var layout = contentGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            // SWEEP 9413 R2 (#5): bottom padding = one card row (92 + spacing) so the last card
            // scrolls fully clear of the RectMask2D instead of slicing mid-glyph at max scroll.
            layout.padding = new RectOffset(8, 8, 8, 100);
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            contentGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.content = crt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
            return contentGo.transform;
        }

        private static TextMeshProUGUI MakeText(Transform parent, string text, float size,
            Color color, FontStyles style, TextAlignmentOptions align, Vector2 min, Vector2 max)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min; rt.anchorMax = max;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            ElarionUiKit.EnsureFont(t);
            return t;
        }
    }
}
