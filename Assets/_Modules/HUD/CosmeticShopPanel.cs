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
using DeNelle.Core.UI;

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
            ShopTheme.StyleScrim(_overlay);
            _overlay.style.display = DisplayStyle.None;
            _root.Add(_overlay);

            // Themed shop-window frame (carved wood + aether rim).
            var card = new VisualElement();
            card.style.width = 720;
            card.style.maxWidth = 880;
            card.style.height = 520;
            card.style.flexDirection = FlexDirection.Column;
            ShopTheme.StylePanelFrame(card);
            _overlay.Add(card);

            // Header: crest title + Glimmer chip + close.
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            card.Add(header);

            header.Add(ShopTheme.MakeTitle("Cosmetic Shop"));

            var headerRight = new VisualElement();
            headerRight.style.flexDirection = FlexDirection.Row;
            headerRight.style.alignItems = Align.Center;
            header.Add(headerRight);

            var glimmerChip = ShopTheme.MakeGlimmerChip(out _glimmerLabel, 0);
            glimmerChip.style.marginRight = 10;
            headerRight.Add(glimmerChip);

            var closeBtn = new Button(ToggleOverlay);
            ShopTheme.StyleCloseButton(closeBtn);
            headerRight.Add(closeBtn);

            card.Add(ShopTheme.MakeRule());

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

            // Right side: scrollable card list (themed well, no OS scrollbar).
            var scroller = new ScrollView(ScrollViewMode.Vertical);
            scroller.style.flexGrow = 1;
            ShopTheme.StyleScrollWell(scroller);
            body.Add(scroller);

            _cardList = scroller.contentContainer;

            // Anti-FOMO footer (spec Section 9).
            card.Add(ShopTheme.MakeRule());
            var footer = new Label("Beauty is earned, never required.");
            footer.style.fontSize = 12;
            footer.style.unityFontStyleAndWeight = FontStyle.Italic;
            footer.style.color = ShopTheme.ParchmentDim;
            footer.style.unityTextAlign = TextAnchor.MiddleCenter;
            footer.style.marginTop = 2;
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
            b.userData = category;
            ShopTheme.StyleTab(b, category == _activeCategory);
            return b;
        }

        private void Repaint()
        {
            if (_root == null) return;

            // Refresh service handle in case the bootstrap raced us.
            if (_serviceInstance == null) _serviceInstance = ResolveServiceInstance();

            if (_glimmerLabel != null)
                _glimmerLabel.text = CurrentGlimmer().ToString("N0");

            // Re-apply themed tab state (selected vs unselected). Tabs carry their
            // category in userData.
            if (_overlay != null)
            {
                _overlay.Query<Button>().ForEach(btn =>
                {
                    if (btn.userData is string cat)
                        ShopTheme.StyleTab(btn, cat == _activeCategory);
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
            ShopTheme.StyleCard(card);

            // Framed item-portrait slot — prefers a real preview render; falls back
            // to a framed gem tile (never a bare swatch). Preview textures land in a
            // later art pass; until then the tint reads the cosmetic's accent colour.
            var iconSlot = ShopTheme.MakeIconSlot(ResolvePreviewTexture(def), preview, 60f);
            card.Add(iconSlot);

            var text = new VisualElement();
            text.style.flexDirection = FlexDirection.Column;
            text.style.flexGrow = 1;
            card.Add(text);

            var name = new Label(displayName);
            name.style.fontSize = 15;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.color = ShopTheme.Parchment;
            text.Add(name);

            var desc = new Label(description);
            desc.style.fontSize = 11;
            desc.style.color = ShopTheme.ParchmentDim;
            desc.style.unityFontStyleAndWeight = FontStyle.Italic;
            desc.style.whiteSpace = WhiteSpace.Normal;
            desc.style.marginTop = 2;
            text.Add(desc);

            // Price line — coin glyph + amount in gold (matches the header chip).
            var priceRow = new VisualElement();
            priceRow.style.flexDirection = FlexDirection.Row;
            priceRow.style.alignItems = Align.Center;
            priceRow.style.marginTop = 4;
            if (!isAchievement && glimmerCost > 0)
            {
                var coin = new Label(ShopTheme.CoinGlyph);
                coin.style.fontSize = 12;
                coin.style.color = ShopTheme.Glimmer;
                coin.style.marginRight = 4;
                priceRow.Add(coin);
            }
            string priceText;
            if (isAchievement)
                priceText = owned ? "Earned" : "Earn via play";
            else if (glimmerCost > 0)
                priceText = $"{glimmerCost:N0} Glimmer";
            else
                priceText = "Free";
            var price = new Label(priceText);
            price.style.fontSize = 12;
            price.style.color = ShopTheme.Glimmer;
            priceRow.Add(price);
            text.Add(priceRow);

            var actionBtn = new Button { text = "Buy" };
            actionBtn.style.minWidth = 92;
            actionBtn.style.marginLeft = 10;

            if (equipped)
            {
                actionBtn.text = "Equipped";
                ShopTheme.StyleButton(actionBtn, ShopTheme.ButtonKind.Confirm);
                actionBtn.SetEnabled(false);
            }
            else if (owned)
            {
                actionBtn.text = "Equip";
                ShopTheme.StyleButton(actionBtn, ShopTheme.ButtonKind.Aether);
                actionBtn.clicked += () => { EquipId(id); ShowToast($"Equipped {displayName}"); };
            }
            else if (isAchievement)
            {
                actionBtn.text = "Locked";
                ShopTheme.StyleButton(actionBtn, ShopTheme.ButtonKind.Disabled);
                actionBtn.SetEnabled(false);
            }
            else
            {
                bool affordable = CurrentGlimmer() >= glimmerCost;
                actionBtn.text = "Buy";
                ShopTheme.StyleButton(actionBtn,
                    affordable ? ShopTheme.ButtonKind.Aether : ShopTheme.ButtonKind.Disabled);
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

        // Optional real preview render, loaded by cosmetic id from Resources. When
        // absent (the current state — previews are a later art pass) we return null
        // and the icon slot falls back to a framed gem tile. Cached per id so the
        // Resources lookup runs once per item.
        private static readonly Dictionary<string, Texture2D> s_previewCache = new Dictionary<string, Texture2D>();

        private Texture2D ResolvePreviewTexture(object def)
        {
            string id = s_defIdField?.GetValue(def) as string;
            if (string.IsNullOrEmpty(id)) return null;
            if (s_previewCache.TryGetValue(id, out var cached)) return cached;
            Texture2D tex = null;
            try { tex = Resources.Load<Texture2D>($"Cosmetics/Previews/{id}"); }
            catch { tex = null; }
            s_previewCache[id] = tex;
            return tex;
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
