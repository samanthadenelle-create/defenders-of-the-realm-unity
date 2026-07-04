// =============================================================================
// JewelerPanelMvvm — the jeweler jewelry-crafting VIEW (MVVM slice). A DUMB SKIN:
// it INHERITS the shared kit chrome (BuildObsidianPanel: FrameCrafting master-detail
// + zones + the ONE shared Close) and BINDS a JewelerVM. ALL state/logic (recipes,
// base/gem have-need, can-craft, craft command) lives in the VM — the View never
// reads or defines game state; it only DISPLAYS and routes commands.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Items
//
// WO-F conversion (2026-07-03): the old 3-column card grid -> the owner-ratified
// FrameCrafting MASTER-DETAIL template (matches VillageCraftingPanel/Workshop +
// CraftingPanelMvvm):
//   * bodyLeft  (dark well)      = recipe rows (Obsidian buttons, selected=Yellow,
//                                  +/- affordability suffix)
//   * bodyRight (parchment well) = the selected recipe's detail in dark INK
//                                  (base + gem checklist, cost line, Set-Gems CTA)
//   * footer    (action strip)   = a one-line bench hint
// The ONE shared Close is the chrome's (no per-panel X / close_normal / footer close).
// Mobile-first: compact rows in the narrow left well, centered detail + a compact
// centered CTA — never full-bleed bars.
//
// Code-built uGUI ONLY (no UXML — §8). Registers PanelId.JewelerCrafting.
// Spawned by JewelerPanelBootstrap once a hero exists.
// =============================================================================

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Core.UI.Mvvm;

namespace DeNelle.Village.Items
{
    [DisallowMultipleComponent]
    public sealed class JewelerPanelMvvm : MonoBehaviour, IPanelView
    {
        private JewelerVM _vm;

        private GameObject _ui;
        private Transform _recipeHost;   // bodyLeft — dark list well
        private Transform _detailHost;   // bodyRight — parchment detail well
        private TextMeshProUGUI _headerLabel;

        private string _selectedRecipeId;
        private PanelHandle _panelHandle;

        public bool IsOpen => _ui != null;

        // Dark ink for text sitting ON the parchment detail well (mirrors the family).
        private static readonly Color Ink     = new Color(0.16f, 0.12f, 0.08f, 1f);
        private static readonly Color InkDim  = new Color(0.34f, 0.28f, 0.20f, 1f);
        private static readonly Color InkGood = new Color(0.10f, 0.42f, 0.16f, 1f);
        private static readonly Color InkBad  = new Color(0.55f, 0.12f, 0.10f, 1f);

        // ── Registration (mirror CraftingPanelMvvm) ───────────────────────────────

        private void Awake()
        {
            _panelHandle = PanelManager.Register("Jeweler's Bench", Close, () => IsOpen);
            PanelRouter.Register(PanelId.JewelerCrafting, Open);
        }

        private void OnDestroy()
        {
            Unbind();
            if (_vm != null) { _vm.Dispose(); _vm = null; }
            if (_ui != null) Destroy(_ui);
            _ui = null;
            PanelRouter.Unregister(PanelId.JewelerCrafting, Open);
        }

        // ── Open: build chrome, construct + bind the VM ───────────────────────────

        public void Open()
        {
            Close();
            BuildChrome();

            _vm = new JewelerVM(Close);
            Bind(_vm);

            if (!PanelManager.NotifyOpened(_panelHandle))
                return; // rejected (e.g. in battle) — NotifyOpened already invoked Close.

            Debug.Log("[JewelerPanelMvvm] Opened. Bound JewelerVM (MVVM).");
        }

        // ── IPanelView ────────────────────────────────────────────────────────────

        public void Bind(IPanelViewModel vm)
        {
            Unbind();
            _vm = vm as JewelerVM;
            if (_vm == null) return;
            _vm.Changed += Render;
            Render();
        }

        public void Unbind()
        {
            if (_vm != null) _vm.Changed -= Render;
        }

        // ── Render: repaint from vm.* ONLY ────────────────────────────────────────

        private void Render()
        {
            if (_vm == null) return;
            if (_headerLabel != null) _headerLabel.text = _vm.Title;
            RebuildMasterDetail();
        }

        // ── Master-detail (recipe list left / parchment-ink detail right) ─────────

        private void RebuildMasterDetail()
        {
            if (_recipeHost == null || _detailHost == null || _vm == null) return;

            var recipes = _vm.Recipes;
            int n = recipes != null ? recipes.Count : 0;

            if (n > 0 && (string.IsNullOrEmpty(_selectedRecipeId) || FindIndex(recipes) < 0))
                _selectedRecipeId = recipes[0].RecipeId;

            // Recipe rows (dark well, left).
            for (int i = _recipeHost.childCount - 1; i >= 0; i--)
                Destroy(_recipeHost.GetChild(i).gameObject);

            if (n == 0)
            {
                MakeText(_recipeHost, "No jewelry recipes available.", 13,
                    ElarionUi.ParchmentDim, FontStyles.Italic, TextAlignmentOptions.Center,
                    new Vector2(0.05f, 0.40f), new Vector2(0.95f, 0.60f));
            }
            else
            {
                const float rowH = 0.105f, gap = 0.015f;
                float top = 0.98f;
                for (int i = 0; i < n; i++)
                {
                    var r = recipes[i];
                    string id = r.RecipeId;
                    bool selected = id == _selectedRecipeId;
                    string name = string.IsNullOrEmpty(r.OutputName) ? r.DisplayName : r.OutputName;
                    string label = name + (r.CanCraft ? "  +" : "  -");
                    ElarionUiKit.BuildObsidianButton(_recipeHost, label,
                        ElarionUiKit.ObsidianButtonStyle.Style1,
                        selected ? ElarionUiKit.ObsidianButtonColor.Yellow
                                 : ElarionUiKit.ObsidianButtonColor.Gray,
                        new Vector2(0.04f, top - rowH), new Vector2(0.96f, top),
                        () => { _selectedRecipeId = id; RebuildMasterDetail(); });
                    top -= rowH + gap;
                    if (top - rowH < 0f) break;   // bounded: never overflow the well
                }
            }

            // Detail (parchment well, right — dark ink).
            for (int i = _detailHost.childCount - 1; i >= 0; i--)
                Destroy(_detailHost.GetChild(i).gameObject);

            int sel = FindIndex(recipes);
            if (sel >= 0) BuildDetail(recipes[sel]);
            else
                MakeText(_detailHost, "Select a recipe.", 15, InkDim, FontStyles.Italic,
                    TextAlignmentOptions.Center, new Vector2(0.05f, 0.45f), new Vector2(0.95f, 0.55f));
        }

        private int FindIndex(IReadOnlyList<JewelerRecipeVM> recipes)
        {
            if (recipes == null || string.IsNullOrEmpty(_selectedRecipeId)) return -1;
            for (int i = 0; i < recipes.Count; i++)
                if (recipes[i].RecipeId == _selectedRecipeId) return i;
            return -1;
        }

        private void BuildDetail(JewelerRecipeVM recipe)
        {
            string display = string.IsNullOrEmpty(recipe.OutputName) ? recipe.DisplayName : recipe.OutputName;

            // Title (ink, bold).
            MakeText(_detailHost, display, 20, Ink, FontStyles.Bold,
                TextAlignmentOptions.Center, new Vector2(0.06f, 0.90f), new Vector2(0.94f, 0.99f));

            // Output (upgraded) icon (centered under the title) when the atlas is sliced.
            var outSprite = LoadIcon(recipe.OutputIconPath);
            if (outSprite != null)
            {
                var iconGo = new GameObject("OutIcon", typeof(Image));
                iconGo.transform.SetParent(_detailHost, false);
                var ir = iconGo.GetComponent<RectTransform>();
                ir.anchorMin = new Vector2(0.40f, 0.74f); ir.anchorMax = new Vector2(0.60f, 0.885f);
                ir.offsetMin = Vector2.zero; ir.offsetMax = Vector2.zero;
                var iImg = iconGo.GetComponent<Image>();
                iImg.sprite = outSprite;
                iImg.preserveAspect = true;
                iImg.raycastTarget = false;
            }

            float y = outSprite != null ? 0.70f : 0.86f;
            if (!string.IsNullOrEmpty(recipe.DisplayName) && recipe.DisplayName != display)
            {
                MakeText(_detailHost, recipe.DisplayName, 13, InkDim, FontStyles.Italic,
                    TextAlignmentOptions.Center, new Vector2(0.06f, y - 0.06f), new Vector2(0.94f, y));
                y -= 0.075f;
            }

            MakeText(_detailHost, "Requires", 15, Ink, FontStyles.Bold,
                TextAlignmentOptions.Left, new Vector2(0.08f, y - 0.06f), new Vector2(0.92f, y));
            y -= 0.07f;

            var lines = recipe.Ingredients;
            int li = lines != null ? lines.Count : 0;
            for (int k = 0; k < li; k++)
            {
                var ing = lines[k];
                MakeText(_detailHost, (ing.Met ? "+  " : "-  ") + ing.Name,
                    14, ing.Met ? InkGood : InkBad, FontStyles.Normal,
                    TextAlignmentOptions.Left, new Vector2(0.10f, y - 0.055f), new Vector2(0.66f, y));
                MakeText(_detailHost, ing.Have + "/" + ing.Need, 14, InkDim, FontStyles.Normal,
                    TextAlignmentOptions.Right, new Vector2(0.66f, y - 0.055f), new Vector2(0.90f, y));
                y -= 0.06f;
            }

            // Wallet cost line (iron/crystals) — only when the recipe carries one.
            if (!string.IsNullOrEmpty(recipe.CostLabel))
            {
                y -= 0.02f;
                MakeText(_detailHost, "Cost:  " + recipe.CostLabel, 13, InkDim, FontStyles.Normal,
                    TextAlignmentOptions.Center, new Vector2(0.08f, y - 0.055f), new Vector2(0.92f, y));
            }

            // Set-Gems CTA — Green when affordable, Gray (non-interactable) when short.
            // Compact + centered (mobile-first).
            var btn = ElarionUiKit.BuildObsidianButton(_detailHost, "Set Gems",
                ElarionUiKit.ObsidianButtonStyle.Style1,
                recipe.CanCraft ? ElarionUiKit.ObsidianButtonColor.Green
                                : ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.24f, 0.03f), new Vector2(0.76f, 0.13f),
                () => { if (_vm != null) _vm.Craft(recipe.RecipeId); });
            if (btn != null) btn.interactable = recipe.CanCraft;
        }

        // ── Chrome (presentation only; INHERITS the shared kit frame + zones) ─────

        private void BuildChrome()
        {
            _ui = ElarionUiKit.BuildModalCanvas("JewelerPanelMvvmUI", 31000);
            var canvas = _ui.GetComponent<Canvas>();
            if (canvas != null) canvas.overrideSorting = true;
            ElarionUiKit.Scrim(_ui.transform, onTapClose: () => { if (_vm != null) _vm.Close(); });

            // SHARED Obsidian chrome (FrameCrafting master-detail): black panel + gold trim +
            // gold header + medallion + the ONE shared Close — all built by the kit. The panel
            // adds NO chrome and NO close of its own.
            var chrome = ElarionUiKit.BuildObsidianPanel(_ui.transform, "Jeweler's Bench",
                new Vector2(0.10f, 0.08f), new Vector2(0.90f, 0.92f), () => { if (_vm != null) _vm.Close(); },
                frameName: RpgUiCatalog.FrameCrafting, medallionIcon: "gem");
            _headerLabel = chrome.title;

            var layout = chrome.layout;
            _recipeHost = layout != null && layout.bodyLeft != null
                ? (Transform)layout.bodyLeft
                : (layout != null && layout.body != null ? (Transform)layout.body : chrome.content.transform);
            _detailHost = layout != null && layout.bodyRight != null
                ? (Transform)layout.bodyRight
                : (layout != null && layout.body != null ? (Transform)layout.body : chrome.content.transform);

            var footHost = layout != null && layout.footer != null
                ? (Transform)layout.footer : chrome.content.transform;
            MakeText(footHost, "Set gems into a ring or amulet to forge a finer piece.",
                13, ElarionUi.Parchment, FontStyles.Normal, TextAlignmentOptions.Center,
                new Vector2(0.01f, 0f), new Vector2(0.99f, 1f));

            // Close is the SHARED kit Close (top-right) — no per-panel close.
        }

        // Icon cache — Resources.Load is cheap but cached avoids reloading every Render.
        private static readonly Dictionary<string, Sprite> s_iconCache = new Dictionary<string, Sprite>();
        private static Sprite LoadIcon(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (s_iconCache.TryGetValue(path, out var cached)) return cached;
            Sprite sp = Resources.Load<Sprite>(path);
            s_iconCache[path] = sp;   // cache nulls too (atlas not sliced yet) so we don't retry each frame
            return sp;
        }

        // ── uGUI helper (mirrors VillageCraftingPanel.MakeText) ───────────────────

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

        // ── Teardown ──────────────────────────────────────────────────────────────

        private void Close()
        {
            Unbind();
            if (_vm != null) { _vm.Dispose(); _vm = null; }
            _headerLabel = null;
            _recipeHost = null;
            _detailHost = null;
            if (_ui != null) Destroy(_ui);
            _ui = null;
            PanelManager.NotifyClosed(_panelHandle);
        }
    }
}
