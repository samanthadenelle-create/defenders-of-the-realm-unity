// =============================================================================
// HeroEquipHud — small always-visible quick-equip cluster on the in-world HUD.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// A compact bottom-right slot cluster showing the hero's equipped Weapon + Armor
// and a couple of consumable slots. Tapping ANY slot opens the full inventory
// modal (HeroInventoryController). Code-built uGUI ONLY (same reliable path +
// helper recipe as ArenaPanel / HeroInventoryController — UXML doesn't render in
// builds, PIPELINE_STATE §8).
//
// DRIVES THE EXISTING MODEL: reads GearLoadout (EquippedWeapon / EquippedArmor) on
// the live hero and refreshes on its OnGearChanged event. Consumable slots read
// the persisted larder via ItemInventory.OwnedConsumables(). No new equip system.
//
// Entry points mirror the controller idiom: EnsureExists() spawns a persistent
// host; it self-builds its overlay and self-heals if the hero spawns later.
// ASCII-only runtime strings (sprite/text). WebGL-safe (rounded sprite fallback).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Village.Items;

namespace DeNelle.Village
{
    /// <summary>Always-visible quick-equip slot cluster; taps open the inventory modal.</summary>
    public sealed class HeroEquipHud : MonoBehaviour
    {
        public static HeroEquipHud Instance { get; private set; }

        private GameObject _ui;
        private GameObject _cluster;
        private GearLoadout _loadout;
        private float _heroSearchTimer;

        private static readonly Color Glass     = new Color(0.06f, 0.07f, 0.09f, 0.78f);
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

        // Auto-spawn the quick-equip cluster in the gameplay hubs where a hero +
        // GearLoadout exist. Mirrors the injector bootstrap idiom (NPCs/HUD).
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
            Unsubscribe();
            if (_ui != null) Destroy(_ui);
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            try { BuildRoot(); ResolveLoadout(); Refresh(); }
            catch (System.Exception e)
            {
                Debug.LogError("[HeroEquipHud] build failed (UI may be partial): " + e);
            }
        }

        private void Update()
        {
            // Self-heal: if the hero/loadout wasn't present at Start, retry every ~1s.
            if (_loadout == null)
            {
                _heroSearchTimer += Time.unscaledDeltaTime;
                if (_heroSearchTimer >= 1f)
                {
                    _heroSearchTimer = 0f;
                    ResolveLoadout();
                    if (_loadout != null) Refresh();
                }
            }
        }

        // -- hero / gear resolution -----------------------------------------
        private void ResolveLoadout()
        {
            if (_loadout != null) return;
            var hero = GameObject.FindWithTag("Player");
            if (hero == null) hero = SafeFindByTag("HeroTarget");
            if (hero != null)
            {
                _loadout = hero.GetComponentInChildren<GearLoadout>();
                if (_loadout != null) _loadout.OnGearChanged += Refresh;
            }
        }

        private static GameObject SafeFindByTag(string tag)
        {
            try { return GameObject.FindWithTag(tag); }
            catch { return null; }
        }

        private void Unsubscribe()
        {
            if (_loadout != null) _loadout.OnGearChanged -= Refresh;
        }

        // ====================================================================
        // BUILD
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

            // Cluster panel: bottom-right vertical strip of slots.
            _cluster = AddImage(_ui.transform, "EquipCluster",
                                new Vector2(0.84f, 0.30f), new Vector2(0.995f, 0.70f), Glass);
            AddRimUnderline(_cluster);
        }

        /// <summary>Rebuild the slot tiles from the live equipped gear + owned consumables.</summary>
        private void Refresh()
        {
            if (_cluster == null) return;
            for (int i = _cluster.transform.childCount - 1; i >= 0; i--)
            {
                var ch = _cluster.transform.GetChild(i);
                if (ch.name == "Accent") continue;       // keep the rim
                Destroy(ch.gameObject);
            }

            AddLabel(_cluster.transform, "GEAR", 0.92f, 0.99f, ElarionUi.ParchmentDim,
                     ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, spacing: 3f);

            // Slot 1 — Weapon.
            WeaponDef w = _loadout != null ? _loadout.EquippedWeapon : null;
            AddSlot("WPN", w != null ? Glyph(w.icon) : "-",
                    w != null ? RarityColor(w.rarity) : ElarionUi.ParchmentDim,
                    0.715f, 0.90f);

            // Slot 2 — Armor.
            ArmorDef a = _loadout != null ? _loadout.EquippedArmor : null;
            AddSlot("ARM", a != null ? Glyph(a.icon) : "-",
                    a != null ? RarityColor(a.rarity) : ElarionUi.ParchmentDim,
                    0.515f, 0.70f);

            // Slots 3-4 — first two owned consumables (or empty).
            var owned = SafeOwnedConsumables();
            string c1Glyph = "-", c2Glyph = "-";
            int c1Count = 0, c2Count = 0;
            int idx = 0;
            foreach (var kv in owned)
            {
                var def = ConsumableCatalog.Find(kv.Key);
                string g = def != null && !string.IsNullOrEmpty(def.Glyph) ? def.Glyph : "!";
                if (idx == 0) { c1Glyph = g; c1Count = kv.Value; }
                else if (idx == 1) { c2Glyph = g; c2Count = kv.Value; break; }
                idx++;
            }
            AddSlot("ITM", c1Glyph, ElarionUi.Parchment, 0.315f, 0.50f, c1Count);
            AddSlot("ITM", c2Glyph, ElarionUi.Parchment, 0.115f, 0.30f, c2Count);

            // The whole cluster is tappable (any slot) -> open inventory.
            // Each slot is a Button; also add a catch-all on the cluster background.
            EnsureClusterButton();
        }

        private static Dictionary<string, int> SafeOwnedConsumables()
        {
            try { return ItemInventory.OwnedConsumables() ?? new Dictionary<string, int>(); }
            catch { return new Dictionary<string, int>(); }
        }

        private void EnsureClusterButton()
        {
            var btn = _cluster.GetComponent<Button>();
            if (btn == null)
            {
                btn = _cluster.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(OpenInventory);
            }
        }

        private void AddSlot(string tag, string glyph, Color glyphColor, float y0, float y1, int count = -1)
        {
            var tile = new GameObject("Slot_" + tag, typeof(Image), typeof(Button));
            tile.transform.SetParent(_cluster.transform, false);
            var rt = tile.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.10f, y0); rt.anchorMax = new Vector2(0.90f, y1);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = tile.GetComponent<Image>();
            img.color = Cell;
            ApplyRounded(img);

            var btn = tile.GetComponent<Button>();
            btn.targetGraphic = img;
            StyleButtonColors(btn);
            btn.onClick.AddListener(OpenInventory);

            AddLabel(tile.transform, glyph, 0.28f, 0.95f, glyphColor, ElarionUi.FontHead,
                     TMPro.TextAlignmentOptions.Center, 0.02f, 0.98f, bold: true);
            AddLabel(tile.transform, tag, 0.02f, 0.28f, ElarionUi.ParchmentDim, ElarionUi.FontMicro,
                     TMPro.TextAlignmentOptions.Center, 0.02f, 0.98f, spacing: 2f);

            if (count >= 0)
            {
                var chip = AddImage(tile.transform, "Count", new Vector2(0.55f, 0.62f), new Vector2(0.98f, 0.98f),
                                    new Color(0f, 0f, 0f, 0.6f));
                AddLabel(chip.transform, "x" + count, 0f, 1f, ElarionUi.Parchment, ElarionUi.FontMicro,
                         TMPro.TextAlignmentOptions.Center, 0f, 1f, bold: true);
            }
        }

        private void OpenInventory()
        {
            try { HeroInventoryController.EnsureExists().Open(); }
            catch (System.Exception e) { Debug.LogError("[HeroEquipHud] open inventory failed: " + e); }
        }

        private static string Glyph(string icon) => string.IsNullOrEmpty(icon) ? "?" : icon;

        // ── shared visual helpers (mirrored from ArenaPanel) ──────────────────
        private static Color RarityColor(string rarity)
        {
            switch ((rarity ?? "common").ToLowerInvariant())
            {
                case "uncommon":  return new Color(0.46f, 0.74f, 0.42f, 1f);
                case "rare":      return new Color(0.32f, 0.58f, 0.92f, 1f);
                case "epic":      return new Color(0.66f, 0.42f, 0.86f, 1f);
                case "legendary": return new Color(0.92f, 0.62f, 0.24f, 1f);
                default:          return new Color(0.80f, 0.80f, 0.78f, 1f);
            }
        }

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
