// =============================================================================
// ItemHud - the code-built, flag-gated UI for the item-drops lane: a use-hotbar
// (use a healing potion / eat food DURING a fight, with a cooldown) plus a tiny
// crafting panel (combine drop materials -> a consumable). Inert when off.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Items
//
// HARD ISOLATION CONTRACT (mirrors CampPromptUI):
//   * NEW type. Touches NO existing file. Composes lane + existing PUBLIC APIs:
//       - ConsumableUseService.TryUse (heal mid-fight)   [lane]
//       - ItemCraftingService.TryCraft / CanCraft        [lane]
//       - ItemInventory.OwnedConsumables / Owned         [lane]
//       - ConsumableCatalog / ConsumableCraftingCatalog  [lane content]
//   * SELF-BOOTSTRAPS ONLY when ItemDropSystem.Enabled (SHIPS DARK): the
//     RuntimeInitializeOnLoadMethod returns immediately when the flag is off, so
//     no canvas, no buttons, no input polling - zero footprint in the build.
//   * Build-safe uGUI: LegacyRuntime.ttf font, NO UXML, NO EventSystem - taps are
//     polled + hit-tested manually (the proven CampPromptUI / GameOverScreen path).
//   * A simple shared cooldown gates mid-fight potion use.
//
// ASCII strings only. Canon: village is Elarion (never Avalon).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;   // shared Elarion palette — item HUD matches the one game UI

namespace DeNelle.Village.Items
{
    /// <summary>Drives the lane's use-hotbar + crafting panel. One DDOL instance.</summary>
    public sealed class ItemHud : MonoBehaviour
    {
        public static ItemHud Instance { get; private set; }

        // Use-cooldown so a held tap / key doesn't chug the whole larder at once.
        private const float UseCooldown = 1.0f;
        private float _nextUseTime;

        private Canvas _canvas;

        // Use-hotbar: one button per known consumable, "<glyph> Name xN".
        private readonly List<(RectTransform rect, string consumableId, Text label)> _useButtons =
            new List<(RectTransform, string, Text)>();

        // Craft panel: toggled by a button; one button per recipe.
        private GameObject _craftPanel;
        private RectTransform _craftToggleRect;
        private readonly List<(RectTransform rect, string recipeId, Text label)> _craftButtons =
            new List<(RectTransform, string, Text)>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!ItemDropSystem.Enabled) return;     // SHIPS DARK.
            if (Instance != null) return;
            new GameObject("ItemHud").AddComponent<ItemHud>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildCanvas();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // If the lane flips off at runtime, hide everything + stop polling.
            if (!ItemDropSystem.Enabled)
            {
                if (_canvas != null) _canvas.gameObject.SetActive(false);
                return;
            }
            if (_canvas != null && !_canvas.gameObject.activeSelf) _canvas.gameObject.SetActive(true);

            RefreshUseLabels();

            // Keybind: [Q] quick-uses the first owned, fight-usable consumable.
            if (Input.GetKeyDown(KeyCode.Q)) QuickUse();

            // Tap routing.
            if (!TryGetTap(out Vector2 tap)) return;

            // Craft toggle.
            if (_craftToggleRect != null &&
                RectTransformUtility.RectangleContainsScreenPoint(_craftToggleRect, tap, null))
            {
                if (_craftPanel != null) _craftPanel.SetActive(!_craftPanel.activeSelf);
                if (_craftPanel != null && _craftPanel.activeSelf) RefreshCraftLabels();
                return;
            }

            // Craft buttons (only when the panel is open).
            if (_craftPanel != null && _craftPanel.activeSelf)
            {
                for (int i = 0; i < _craftButtons.Count; i++)
                {
                    var (rect, recipeId, _) = _craftButtons[i];
                    if (rect != null &&
                        RectTransformUtility.RectangleContainsScreenPoint(rect, tap, null))
                    {
                        ItemCraftingService.TryCraft(recipeId);
                        RefreshCraftLabels();
                        RefreshUseLabels();
                        return;
                    }
                }
            }

            // Use buttons.
            for (int i = 0; i < _useButtons.Count; i++)
            {
                var (rect, consumableId, _) = _useButtons[i];
                if (rect != null &&
                    RectTransformUtility.RectangleContainsScreenPoint(rect, tap, null))
                {
                    TryUse(consumableId);
                    return;
                }
            }
        }

        // =====================================================================
        // Use flow (mid-fight) + cooldown.
        // =====================================================================
        private void TryUse(string consumableId)
        {
            if (Time.unscaledTime < _nextUseTime) return;         // on cooldown
            // inFight: true -> tent kits (rest-only) are correctly refused by the service.
            if (ConsumableUseService.TryUse(consumableId, inFight: true))
            {
                _nextUseTime = Time.unscaledTime + UseCooldown;
                RefreshUseLabels();
            }
        }

        /// <summary>Use the first owned consumable that is usable mid-fight ([Q] keybind).</summary>
        private void QuickUse()
        {
            var owned = ItemInventory.OwnedConsumables();
            foreach (var kv in owned)
            {
                var def = ConsumableCatalog.Find(kv.Key);
                if (def != null && def.UsableInFight) { TryUse(kv.Key); return; }
            }
        }

        // =====================================================================
        // Code-built uGUI (build-safe font, no EventSystem).
        // =====================================================================
        private void BuildCanvas()
        {
            var go = new GameObject("ItemHudCanvas");
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 29000;     // below the camp UI's 30000
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            BuildUseHotbar();
            BuildCraftToggle();
            BuildCraftPanel();
        }

        // A column of use-buttons up the bottom-left for each known consumable.
        private void BuildUseHotbar()
        {
            var consumables = ConsumableCatalog.All;
            if (consumables == null) return;

            float y = 0.04f;                  // bottom anchor, climbs per button
            const float h = 0.07f;            // button height (anchor space)
            const float gap = 0.01f;

            foreach (var def in consumables)
            {
                if (def == null || string.IsNullOrEmpty(def.Id)) continue;

                var (rect, label) = MakeButton(
                    _canvas.transform, "Use_" + def.Id,
                    new Vector2(0.02f, y), new Vector2(0.24f, y + h),
                    ElarionUiKit.Glass);

                _useButtons.Add((rect, def.Id, label));
                y += h + gap;
            }
        }

        private void BuildCraftToggle()
        {
            var (rect, label) = MakeButton(
                _canvas.transform, "CraftToggle",
                new Vector2(0.02f, 0.90f), new Vector2(0.18f, 0.97f),
                ElarionUi.GoldButton);
            label.text = "Craft";
            label.color = ElarionUi.Ink;   // dark ink on the gold CTA
            _craftToggleRect = rect;
        }

        private void BuildCraftPanel()
        {
            // Warm-stone craft panel framed with the shared kit (dark glass + gold rim).
            _craftPanel = ElarionUiKit.Panel(_canvas.transform,
                                             new Vector2(0.20f, 0.30f), new Vector2(0.62f, 0.88f),
                                             deep: true, innerRim: true);

            // Title — gilt parchment header, sourced from the palette.
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(_craftPanel.transform, false);
            var title = titleGo.AddComponent<Text>();
            title.font = BuiltinFont();
            title.alignment = TextAnchor.MiddleCenter;
            title.color = ElarionUi.Gilt;
            title.fontSize = 24;
            title.fontStyle = FontStyle.Bold;
            title.text = "Craft Consumables";
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0.05f, 0.90f); trt.anchorMax = new Vector2(0.95f, 0.99f);
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

            var recipes = ConsumableCraftingCatalog.All;
            if (recipes != null)
            {
                float yMax = 0.88f;
                const float h = 0.12f;
                const float gap = 0.015f;
                foreach (var r in recipes)
                {
                    if (r == null || string.IsNullOrEmpty(r.Id)) continue;
                    float yMin = yMax - h;
                    var (rect, label) = MakeButton(
                        _craftPanel.transform, "Craft_" + r.Id,
                        new Vector2(0.06f, yMin), new Vector2(0.94f, yMax),
                        ElarionUiKit.Cell);
                    _craftButtons.Add((rect, r.Id, label));
                    yMax = yMin - gap;
                }
            }

            _craftPanel.SetActive(false);
        }

        // =====================================================================
        // Label refresh (counts + craftability).
        // =====================================================================
        private void RefreshUseLabels()
        {
            for (int i = 0; i < _useButtons.Count; i++)
            {
                var (_, id, label) = _useButtons[i];
                if (label == null) continue;
                var def = ConsumableCatalog.Find(id);
                int have = ItemInventory.Owned(id);
                string name = (def != null && !string.IsNullOrEmpty(def.DisplayName)) ? def.DisplayName : id;
                string glyph = (def != null && !string.IsNullOrEmpty(def.Glyph)) ? def.Glyph + " " : "";
                label.text = glyph + name + "  x" + have;
                label.color = have > 0 ? ElarionUi.Parchment : ElarionUi.ParchmentDim;
            }
        }

        private void RefreshCraftLabels()
        {
            for (int i = 0; i < _craftButtons.Count; i++)
            {
                var (_, recipeId, label) = _craftButtons[i];
                if (label == null) continue;
                var recipe = ConsumableCraftingCatalog.Find(recipeId);
                string name = (recipe != null && !string.IsNullOrEmpty(recipe.DisplayName)) ? recipe.DisplayName : recipeId;
                bool can = ItemCraftingService.CanCraft(recipeId);
                label.text = name + (can ? "" : "  (need materials)");
                label.color = can ? ElarionUi.Affordable : ElarionUi.ParchmentDim;
            }
        }

        // =====================================================================
        // uGUI helpers (mirror CampPromptUI).
        // =====================================================================
        private static (RectTransform rect, Text label) MakeButton(
            Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            ElarionUiKit.ApplyRounded(img);   // shared rounded corners (the one UI language)
            var rt = img.rectTransform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var lblGo = new GameObject("Label");
            lblGo.transform.SetParent(go.transform, false);
            var lbl = lblGo.AddComponent<Text>();
            lbl.font = BuiltinFont();
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.color = ElarionUi.Parchment;
            lbl.fontSize = 20;
            lbl.fontStyle = FontStyle.Bold;
            lbl.text = name;
            var lrt = lbl.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(8f, 0f); lrt.offsetMax = new Vector2(-8f, 0f);

            return (rt, lbl);
        }

        private static Font BuiltinFont() =>
            Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// <summary>First-touch / mouse-down screen position this frame (no EventSystem).</summary>
        private static bool TryGetTap(out Vector2 pos)
        {
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                if (t.phase == UnityEngine.TouchPhase.Began) { pos = t.position; return true; }
            }
            if (Input.GetMouseButtonDown(0)) { pos = (Vector2)Input.mousePosition; return true; }
            pos = default;
            return false;
        }
    }
}
