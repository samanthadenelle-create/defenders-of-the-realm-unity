// =============================================================================
// CosmeticShopPanel - the in-game Cosmetic Shop UI (UI Toolkit). Toggled with
// the C key once the player is in a hero scene. Two-column layout: a category
// strip on the left (Hero / Pet / Village), a vertical card list on the right.
// Each card shows the preview swatch, name, description, Glimmer price, and a
// single action button (Buy / Equip / Equipped / Locked).
// -----------------------------------------------------------------------------
// Reflection bridge: DeNelle.HUD does not reference DeNelle.Cosmetics (asmdef
// isolation - same rule that PetHeroLeash follows for DeNelle.Village). We
// resolve the catalog + service types lazily by name, cache the MethodInfo /
// PropertyInfo handles, and treat any miss as "shop unavailable". This keeps
// the HUD compiling even if the Cosmetics module is stripped from a build.
//
// Spawned by CosmeticShopPanelBootstrap once a scene has a hero (mirrors
// DailyQuestHudBootstrap so Title / HeroSelect stay quiet).
// =============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.HUD
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class CosmeticShopPanel : MonoBehaviour
    {
        public const KeyCode ToggleKey = KeyCode.C;

        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _overlay;
        private VisualElement _cardList;
        private Label _glimmerLabel;
        private Label _toast;
        private float _toastUntil;
        private string _activeCategory = "hero";

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

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
            if (_doc == null) _doc = gameObject.AddComponent<UIDocument>();
            if (_doc.panelSettings == null)
            {
                foreach (var existing in UnityEngine.Object.FindObjectsByType<UIDocument>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (existing == _doc || existing.panelSettings == null) continue;
                    _doc.panelSettings = existing.panelSettings;
                    break;
                }
            }
            if (_doc.panelSettings == null)
            {
                Debug.LogWarning("[CosmeticShopPanel] No PanelSettings available - shop hidden.");
                enabled = false;
                return;
            }
            _doc.sortingOrder = 95; // above HUD chips, below the Help overlay (100)
        }

        private void OnEnable()
        {
            ResolveBridge();
            _serviceInstance = ResolveServiceInstance();
            BuildUi();
            SubscribeChanged();
            Repaint();
        }

        private void OnDisable()
        {
            UnsubscribeChanged();
        }

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey))
                ToggleOverlay();

            if (_toast != null && _toastUntil > 0f && Time.unscaledTime > _toastUntil)
            {
                _toast.style.display = DisplayStyle.None;
                _toastUntil = 0f;
            }
        }

        // ─── Reflection bridge ───────────────────────────────────────────────

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
            catch { return null; }
        }

        private int CurrentGlimmer()
        {
            if (_serviceInstance == null || s_glimmerProp == null) return 0;
            try { return (int)s_glimmerProp.GetValue(_serviceInstance); }
            catch { return 0; }
        }

        private bool OwnsId(string id)
        {
            if (_serviceInstance == null || s_ownsMethod == null) return false;
            try { return (bool)s_ownsMethod.Invoke(_serviceInstance, new object[] { id }); }
            catch { return false; }
        }

        private string EquippedFor(string category)
        {
            if (_serviceInstance == null || s_equippedForMethod == null) return null;
            try { return s_equippedForMethod.Invoke(_serviceInstance, new object[] { category }) as string; }
            catch { return null; }
        }

        private bool TryPurchase(string id)
        {
            if (_serviceInstance == null || s_tryPurchaseMethod == null) return false;
            try { return (bool)s_tryPurchaseMethod.Invoke(_serviceInstance, new object[] { id }); }
            catch { return false; }
        }

        private void EquipId(string id)
        {
            if (_serviceInstance == null || s_equipMethod == null) return;
            try { s_equipMethod.Invoke(_serviceInstance, new object[] { id }); }
            catch { /* swallow - reflected event */ }
        }

        private IEnumerable CatalogByCategory(string category)
        {
            if (s_byCategoryMethod == null) yield break;
            object result;
            try { result = s_byCategoryMethod.Invoke(null, new object[] { category }); }
            catch { yield break; }
            if (result is IEnumerable seq)
                foreach (var item in seq) yield return item;
        }

        private void SubscribeChanged()
        {
            if (_serviceInstance == null || s_changedEvent == null) return;
            try
            {
                // Wrap Repaint in an Action so the reflected event (declared as
                // `event Action`) can target a private instance method.
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
            catch { /* swallow */ }
            _changedHandler = null;
        }

        // ─── UI construction ─────────────────────────────────────────────────

        private void BuildUi()
        {
            _root = _doc.rootVisualElement;
            if (_root == null) return;
            _root.pickingMode = PickingMode.Ignore; // don't block HUD beneath
            _root.Clear();
            _root.pickingMode = PickingMode.Ignore;
            _root.style.position = Position.Absolute;
            _root.style.left = 0; _root.style.right = 0;
            _root.style.top = 0;  _root.style.bottom = 0;

            _overlay = new VisualElement { name = "cosmetic-shop-overlay" };
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = 0; _overlay.style.right = 0;
            _overlay.style.top = 0;  _overlay.style.bottom = 0;
            _overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.78f);
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            _overlay.style.display = DisplayStyle.None;
            _root.Add(_overlay);

            var card = new VisualElement();
            card.style.width = 720;
            card.style.maxWidth = 880;
            card.style.height = 520;
            card.style.flexDirection = FlexDirection.Column;
            card.style.paddingTop = 16; card.style.paddingBottom = 16;
            card.style.paddingLeft = 20; card.style.paddingRight = 20;
            card.style.backgroundColor = new Color(0.07f, 0.05f, 0.11f, 0.98f);
            card.style.borderTopLeftRadius = 16; card.style.borderTopRightRadius = 16;
            card.style.borderBottomLeftRadius = 16; card.style.borderBottomRightRadius = 16;
            var rim = new Color(0.78f, 0.66f, 0.16f, 0.6f);
            card.style.borderTopWidth = 1; card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1; card.style.borderRightWidth = 1;
            card.style.borderTopColor = rim; card.style.borderBottomColor = rim;
            card.style.borderLeftColor = rim; card.style.borderRightColor = rim;
            _overlay.Add(card);

            // Header: title + Glimmer balance + close.
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 10;
            card.Add(header);

            var title = new Label("Cosmetic Shop");
            title.style.fontSize = 22;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.95f, 0.90f, 0.78f);
            header.Add(title);

            _glimmerLabel = new Label("Glimmer: 0");
            _glimmerLabel.style.fontSize = 14;
            _glimmerLabel.style.color = new Color(0.95f, 0.85f, 0.45f);
            _glimmerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(_glimmerLabel);

            var closeBtn = new Button(ToggleOverlay) { text = "X" };
            closeBtn.style.width = 32; closeBtn.style.height = 28;
            closeBtn.style.backgroundColor = new Color(0.18f, 0.12f, 0.28f, 1f);
            closeBtn.style.color = Color.white;
            closeBtn.style.borderTopLeftRadius = 6; closeBtn.style.borderTopRightRadius = 6;
            closeBtn.style.borderBottomLeftRadius = 6; closeBtn.style.borderBottomRightRadius = 6;
            header.Add(closeBtn);

            // Body: two columns.
            var body = new VisualElement();
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1;
            card.Add(body);

            // Left rail: category tabs.
            var rail = new VisualElement();
            rail.style.width = 160;
            rail.style.flexDirection = FlexDirection.Column;
            rail.style.marginRight = 12;
            body.Add(rail);

            rail.Add(BuildTab("Hero",    "hero"));
            rail.Add(BuildTab("Pet",     "pet"));
            rail.Add(BuildTab("Village", "village"));

            // Right side: scrollable card list.
            var scroller = new ScrollView(ScrollViewMode.Vertical);
            scroller.style.flexGrow = 1;
            scroller.style.backgroundColor = new Color(0.04f, 0.03f, 0.08f, 0.6f);
            scroller.style.borderTopLeftRadius = 10; scroller.style.borderTopRightRadius = 10;
            scroller.style.borderBottomLeftRadius = 10; scroller.style.borderBottomRightRadius = 10;
            scroller.style.paddingTop = 8; scroller.style.paddingBottom = 8;
            scroller.style.paddingLeft = 10; scroller.style.paddingRight = 10;
            body.Add(scroller);

            _cardList = scroller.contentContainer;

            // Anti-FOMO footer (spec Section 9).
            var footer = new Label("Beauty is earned, never required.");
            footer.style.fontSize = 11;
            footer.style.unityFontStyleAndWeight = FontStyle.Italic;
            footer.style.color = new Color(0.65f, 0.62f, 0.55f);
            footer.style.unityTextAlign = TextAnchor.MiddleCenter;
            footer.style.marginTop = 8;
            card.Add(footer);

            // Toast under the card.
            _toast = new Label(string.Empty);
            _toast.style.position = Position.Absolute;
            _toast.style.bottom = 40; _toast.style.left = 0; _toast.style.right = 0;
            _toast.style.unityTextAlign = TextAnchor.MiddleCenter;
            _toast.style.color = new Color(0.95f, 0.92f, 0.85f);
            _toast.style.fontSize = 13;
            _toast.style.display = DisplayStyle.None;
            _root.Add(_toast);
        }

        private Button BuildTab(string label, string category)
        {
            var b = new Button(() => { _activeCategory = category; Repaint(); }) { text = label };
            b.style.height = 36;
            b.style.marginBottom = 6;
            b.style.fontSize = 14;
            b.style.color = new Color(0.95f, 0.92f, 0.85f);
            b.style.borderTopLeftRadius = 8; b.style.borderTopRightRadius = 8;
            b.style.borderBottomLeftRadius = 8; b.style.borderBottomRightRadius = 8;
            b.userData = category;
            return b;
        }

        private void Repaint()
        {
            if (_root == null) return;

            // Refresh service handle in case the bootstrap raced us.
            if (_serviceInstance == null) _serviceInstance = ResolveServiceInstance();

            if (_glimmerLabel != null)
                _glimmerLabel.text = $"Glimmer: {CurrentGlimmer()}";

            // Highlight the active tab.
            var rail = _overlay?.Q<VisualElement>()?.Q<VisualElement>();
            // We tagged tab userData with the category string above.
            if (_overlay != null)
            {
                _overlay.Query<Button>().ForEach(btn =>
                {
                    if (btn.userData is string cat)
                    {
                        bool active = cat == _activeCategory;
                        btn.style.backgroundColor = active
                            ? new Color(0.42f, 0.30f, 0.62f, 1f)
                            : new Color(0.18f, 0.12f, 0.28f, 1f);
                    }
                });
            }

            if (_cardList == null) return;
            _cardList.Clear();

            bool anyCard = false;
            foreach (var def in CatalogByCategory(_activeCategory))
            {
                if (def == null) continue;
                _cardList.Add(BuildCard(def));
                anyCard = true;
            }
            if (!anyCard)
            {
                var empty = new Label("Nothing here yet - check back next season.");
                empty.style.color = new Color(0.7f, 0.65f, 0.6f);
                empty.style.unityFontStyleAndWeight = FontStyle.Italic;
                empty.style.fontSize = 13;
                empty.style.marginTop = 12;
                empty.style.unityTextAlign = TextAnchor.MiddleCenter;
                _cardList.Add(empty);
            }
        }

        private VisualElement BuildCard(object def)
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

            var card = new VisualElement();
            card.style.flexDirection = FlexDirection.Row;
            card.style.alignItems = Align.Center;
            card.style.marginBottom = 8;
            card.style.paddingTop = 10; card.style.paddingBottom = 10;
            card.style.paddingLeft = 12; card.style.paddingRight = 12;
            card.style.backgroundColor = new Color(0.10f, 0.08f, 0.16f, 0.95f);
            card.style.borderTopLeftRadius = 10; card.style.borderTopRightRadius = 10;
            card.style.borderBottomLeftRadius = 10; card.style.borderBottomRightRadius = 10;

            var swatch = new VisualElement();
            swatch.style.width = 56; swatch.style.height = 56;
            swatch.style.marginRight = 12;
            swatch.style.backgroundColor = preview;
            swatch.style.borderTopLeftRadius = 8; swatch.style.borderTopRightRadius = 8;
            swatch.style.borderBottomLeftRadius = 8; swatch.style.borderBottomRightRadius = 8;
            swatch.style.borderTopWidth = 1; swatch.style.borderBottomWidth = 1;
            swatch.style.borderLeftWidth = 1; swatch.style.borderRightWidth = 1;
            var sw = new Color(1f, 1f, 1f, 0.18f);
            swatch.style.borderTopColor = sw; swatch.style.borderBottomColor = sw;
            swatch.style.borderLeftColor = sw; swatch.style.borderRightColor = sw;
            card.Add(swatch);

            var text = new VisualElement();
            text.style.flexDirection = FlexDirection.Column;
            text.style.flexGrow = 1;
            card.Add(text);

            var name = new Label(displayName);
            name.style.fontSize = 15;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.color = new Color(0.95f, 0.92f, 0.85f);
            text.Add(name);

            var desc = new Label(description);
            desc.style.fontSize = 11;
            desc.style.color = new Color(0.75f, 0.72f, 0.65f);
            desc.style.unityFontStyleAndWeight = FontStyle.Italic;
            desc.style.whiteSpace = WhiteSpace.Normal;
            desc.style.marginTop = 2;
            text.Add(desc);

            string priceText;
            if (isAchievement)
                priceText = owned ? "Earned" : "Earn via play";
            else if (glimmerCost > 0)
                priceText = $"{glimmerCost} Glimmer";
            else
                priceText = "Free";
            var price = new Label(priceText);
            price.style.fontSize = 12;
            price.style.color = new Color(0.95f, 0.85f, 0.45f);
            price.style.marginTop = 4;
            text.Add(price);

            var actionBtn = new Button { text = "Buy" };
            actionBtn.style.minWidth = 88; actionBtn.style.height = 34;
            actionBtn.style.marginLeft = 10;
            actionBtn.style.fontSize = 13;
            actionBtn.style.borderTopLeftRadius = 8; actionBtn.style.borderTopRightRadius = 8;
            actionBtn.style.borderBottomLeftRadius = 8; actionBtn.style.borderBottomRightRadius = 8;

            if (equipped)
            {
                actionBtn.text = "Equipped";
                actionBtn.SetEnabled(false);
                actionBtn.style.backgroundColor = new Color(0.20f, 0.40f, 0.24f, 1f);
                actionBtn.style.color = new Color(0.85f, 0.95f, 0.85f);
            }
            else if (owned)
            {
                actionBtn.text = "Equip";
                actionBtn.style.backgroundColor = new Color(0.30f, 0.42f, 0.66f, 1f);
                actionBtn.style.color = Color.white;
                actionBtn.clicked += () => { EquipId(id); ShowToast($"Equipped {displayName}"); };
            }
            else if (isAchievement)
            {
                actionBtn.text = "Locked";
                actionBtn.SetEnabled(false);
                actionBtn.style.backgroundColor = new Color(0.30f, 0.26f, 0.24f, 1f);
                actionBtn.style.color = new Color(0.7f, 0.66f, 0.6f);
            }
            else
            {
                bool affordable = CurrentGlimmer() >= glimmerCost;
                actionBtn.text = "Buy";
                actionBtn.style.backgroundColor = affordable
                    ? new Color(0.55f, 0.36f, 0.74f, 1f)
                    : new Color(0.28f, 0.22f, 0.36f, 1f);
                actionBtn.style.color = Color.white;
                actionBtn.SetEnabled(affordable);
                actionBtn.clicked += () =>
                {
                    if (TryPurchase(id))
                    {
                        ShowToast($"Unlocked {displayName}");
                        EquipId(id);
                    }
                    else
                    {
                        ShowToast("Not enough Glimmer.");
                    }
                };
            }

            card.Add(actionBtn);
            return card;
        }

        private static int SafeInt(object value)
        {
            if (value == null) return 0;
            try { return Convert.ToInt32(value); } catch { return 0; }
        }

        private static Color SafeColor(object value)
        {
            if (value is Color c) return c;
            return new Color(0.55f, 0.55f, 0.55f, 1f);
        }

        // ─── Visibility ──────────────────────────────────────────────────────

        public void ToggleOverlay()
        {
            if (_overlay == null) return;
            bool open = _overlay.style.display == DisplayStyle.None;
            _overlay.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            _overlay.pickingMode = open ? PickingMode.Position : PickingMode.Ignore;
            if (open) Repaint();
        }

        private void ShowToast(string message)
        {
            if (_toast == null) return;
            _toast.text = message;
            _toast.style.display = DisplayStyle.Flex;
            _toastUntil = Time.unscaledTime + 3f;
        }
    }
}
