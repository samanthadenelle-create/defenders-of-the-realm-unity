// =============================================================================
// HeroEquipHud — single compact "open inventory" icon button on the in-world HUD.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// One small, clearly-tappable bag/satchel icon button anchored bottom-right.
// Tapping it opens the full inventory modal (HeroInventoryController). Replaces
// the former 4-slot quick-equip cluster (Weapon/Armor/2 consumables) — the owner
// wanted a single compact entry point instead of the always-on slot strip.
// Code-built uGUI ONLY (same reliable path + helper recipe as ArenaPanel /
// HeroInventoryController — UXML doesn't render in builds, PIPELINE_STATE §8).
//
// Entry points mirror the controller idiom: EnsureExists() spawns a persistent
// host; it self-builds its overlay. ASCII/glyph-only runtime strings (sprite/
// text). WebGL-safe (rounded sprite fallback). No new equip system.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;

namespace DeNelle.Village
{
    /// <summary>Single compact bag-icon button on the HUD; tap opens the inventory modal.</summary>
    public sealed class HeroEquipHud : MonoBehaviour
    {
        public static HeroEquipHud Instance { get; private set; }

        // Bag/satchel glyph denotes inventory; falls back to a sack/gear glyph if a
        // device font lacks it (purposely an emoji that reads universally as "bag").
        private const string BagGlyph = "\U0001F392"; // 🎒 backpack/satchel

        private GameObject _ui;

        private static readonly Color Cell      = new Color(0.10f, 0.11f, 0.14f, 0.90f);
        private static readonly Color AccentSoft = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.30f);

        public static HeroEquipHud EnsureExists()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("HeroEquipHud");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<HeroEquipHud>();
            return Instance;
        }

        // Auto-spawn the inventory button in the gameplay hubs.
        // Mirrors the injector bootstrap idiom (NPCs/HUD).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) =>
            {
                if (IsHubScene(scene.name)) EnsureExists();
            };
            if (IsHubScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name))
                EnsureExists();
        }

        private static bool IsHubScene(string n) =>
            n == "MainCastle_Hall" || n == "Village2" || n == "CastleHub";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_invEvent != null && _invListener != null) _invEvent.RemoveListener(_invListener);
            if (_ui != null) Destroy(_ui);
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            // WO-411: NO free-floating BAG panel anymore — the TOWN ACTIONS row's BAG drives this. Wire
            // the HUD's InventoryRequested (DeNelle.HUD; Village can't reference it → reflection) to
            // OpenInventory. Re-wire on each scene load (the HUD may not be up when we Start).
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            TryWireToHud();
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
            => TryWireToHud();

        private UnityEngine.Events.UnityAction _invListener;
        private UnityEngine.Events.UnityEvent _invEvent;
        private static System.Type _hudType;

        private void TryWireToHud()
        {
            if (_hudType == null) _hudType = ResolveHudType();
            if (_hudType == null) return;
            var hud = FindObjectOfType(_hudType) as Component;
            if (hud == null) return;
            var field = _hudType.GetField("InventoryRequested",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            var evt = field != null ? field.GetValue(hud) as UnityEngine.Events.UnityEvent : null;
            if (evt == null || ReferenceEquals(evt, _invEvent)) return;
            if (_invEvent != null && _invListener != null) _invEvent.RemoveListener(_invListener);
            _invListener = OpenInventory;
            evt.AddListener(_invListener);
            _invEvent = evt;
        }

        private static System.Type ResolveHudType()
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType("DeNelle.HUD.VillageHudController", false);
                if (t != null) return t;
            }
            return null;
        }

        // ====================================================================
        // BUILD — one compact bag-icon button, bottom-right.
        // ====================================================================
        private void BuildRoot()
        {
            if (_ui != null) return;
            _ui = new GameObject("HeroEquipHudUI");
            _ui.transform.SetParent(transform, false);

            var canvas = _ui.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;                  // under modals (Arena 1100, Inventory 2600)

            var scaler = _ui.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            _ui.AddComponent<GraphicRaycaster>();

            // Single framed, gold-accent square button anchored bottom-right —
            // sits where the old 4-slot cluster lived (~0.84..0.995 x, lower band).
            var frame = AddImage(_ui.transform, "EquipFrame",
                                 new Vector2(0.855f, 0.305f), new Vector2(0.985f, 0.405f), AccentSoft);
            frame.GetComponent<Image>().raycastTarget = false;
            AddRimUnderline(frame);

            var button = new GameObject("InventoryButton", typeof(Image), typeof(Button));
            button.transform.SetParent(frame.transform, false);
            var rt = button.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.06f, 0.06f); rt.anchorMax = new Vector2(0.94f, 0.94f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = button.GetComponent<Image>();
            img.color = Cell;
            ApplyRounded(img);

            var btn = button.GetComponent<Button>();
            btn.targetGraphic = img;
            StyleButtonColors(btn);
            btn.onClick.AddListener(OpenInventory);

            // Bag glyph (icon) + small label so the affordance reads as "inventory".
            AddLabel(button.transform, BagGlyph, 0.30f, 0.98f, ElarionUi.Gilt, ElarionUi.FontHead,
                     TMPro.TextAlignmentOptions.Center, 0.02f, 0.98f, bold: true);
            AddLabel(button.transform, "BAG", 0.02f, 0.30f, ElarionUi.ParchmentDim, ElarionUi.FontMicro,
                     TMPro.TextAlignmentOptions.Center, 0.02f, 0.98f, spacing: 2f);
        }

        private void OpenInventory()
        {
            try { HeroInventoryController.EnsureExists().Open(); }
            catch (System.Exception e) { Debug.LogError("[HeroEquipHud] open inventory failed: " + e); }
        }

        // ── shared visual helpers (mirrored from ArenaPanel) ──────────────────
        private static GameObject AddImage(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = min; r.anchorMax = max;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = color;
            ApplyRounded(img);
            return go;
        }

        private static void ApplyRounded(Image img)
        {
            var sprite = RoundedSprite;
            if (sprite != null) { img.sprite = sprite; img.type = Image.Type.Sliced; }
        }

        private void AddRimUnderline(GameObject panel)
        {
            var go = new GameObject("Accent", typeof(Image));
            go.transform.SetParent(panel.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.06f, 0f);
            rt.anchorMax = new Vector2(0.94f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(0f, 1.5f);
            rt.anchoredPosition = new Vector2(0f, 1.5f);
            var img = go.GetComponent<Image>();
            img.color = AccentSoft;
            img.raycastTarget = false;
            go.transform.SetAsLastSibling();
        }

        private static TMPro.TextMeshProUGUI AddLabel(Transform parent, string text, float y0, float y1,
            Color color, int size, TMPro.TextAlignmentOptions align,
            float x0 = 0.03f, float x1 = 0.97f, float spacing = 0f, bool bold = false)
        {
            var go = new GameObject("Label", typeof(TMPro.TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1);
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            var t = go.GetComponent<TMPro.TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.characterSpacing = spacing;
            t.raycastTarget = false;
            if (bold) t.fontStyle = TMPro.FontStyles.Bold;
            return t;
        }

        private static void StyleButtonColors(Button button)
        {
            if (button == null) return;
            button.transition = Selectable.Transition.ColorTint;
            var cb = button.colors;
            cb.normalColor      = Color.white;
            cb.highlightedColor = new Color(1.10f, 1.10f, 1.10f, 1f);
            cb.pressedColor     = new Color(0.82f, 0.82f, 0.82f, 1f);
            cb.selectedColor    = cb.highlightedColor;
            cb.disabledColor    = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            cb.colorMultiplier  = 1f;
            cb.fadeDuration     = 0.07f;
            button.colors = cb;
        }

        // ── Procedural rounded sprite (lazily built once; WebGL failure-safe) ──
        private static Sprite _rounded;
        private static bool _roundedTried;
        private static Sprite RoundedSprite
        {
            get
            {
                if (!_roundedTried)
                {
                    _roundedTried = true;
                    try { _rounded = BuildRoundedSprite(); }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning("[HeroEquipHud] rounded sprite build failed (flat quad): " + e.Message);
                        _rounded = null;
                    }
                }
                return _rounded;
            }
        }

        private static Sprite BuildRoundedSprite()
        {
            const int size = 32;
            const int radius = 6;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedRectDistance(x, y, size, size, radius);
                    byte alpha = (byte)Mathf.Clamp((int)((1f - d) * 255f), 0, 255);
                    px[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f,
                                 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        private static float RoundedRectDistance(int x, int y, int w, int h, int radius)
        {
            float fx = x + 0.5f, fy = y + 0.5f;
            float dx = Mathf.Max(Mathf.Max(radius - fx, fx - (w - radius)), 0f);
            float dy = Mathf.Max(Mathf.Max(radius - fy, fy - (h - radius)), 0f);
            float dist = Mathf.Sqrt(dx * dx + dy * dy) - radius;
            return Mathf.Clamp01(dist + 0.5f);
        }
    }
}
