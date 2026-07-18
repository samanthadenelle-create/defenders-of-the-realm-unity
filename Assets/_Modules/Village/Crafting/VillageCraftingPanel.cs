// =============================================================================
// VillageCraftingPanel — the Workshop crafting station panel. DUMB SKIN (MVVM, Silo F).
// -----------------------------------------------------------------------------
// Code-built uGUI on the Obsidian MASTER-DETAIL template (BuildObsidianModal:
// FrameCrafting):
//   • bodyLeft  (dark well)      = recipe rows (Obsidian buttons, selected=Yellow,
//                                  +/- affordability suffix)
//   • bodyRight (parchment well) = selected recipe detail: description,
//                                  ingredient checklist, output, Craft CTA (dark ink)
//   • footer    (action strip)   = the larder readout
//
// MVVM: the View reads NO service. Recipe list, have/need projection, craftable
// state and the larder line all come from WorkshopCraftVM (it names NO
// VillageInventory.Instance and NO CraftingRecipeCatalog). The View binds
// Rows / Selected / Larder and routes Select / Craft back as commands.
//
// Public API preserved: Toggle() / Open() / Close() / IsOpen / Instance;
// PanelRouter.Register(PanelId.Crafting, Open); arbiter handle "Workshop".
// =============================================================================

using DeNelle.Core.UI;
using DeNelle.Core.UI.Mvvm;
using TMPro;
using UnityEngine;

namespace DeNelle.Village.Crafting
{
    [DisallowMultipleComponent]
    public sealed class VillageCraftingPanel : MonoBehaviour
    {
        public static VillageCraftingPanel Instance { get; private set; }

        private ElarionUiKit.ObsidianModal _modal;
        private Transform _recipeHost;   // bodyLeft — dark list well
        private Transform _detailHost;   // bodyRight — parchment detail well
        private TextMeshProUGUI _larder; // footer strip readout

        private WorkshopCraftVM _vm;
        private bool _open;
        private PanelHandle _panelHandle;

        // Dark ink for text sitting ON the parchment well.
        private static readonly Color Ink     = new Color(0.16f, 0.12f, 0.08f, 1f);
        private static readonly Color InkDim  = new Color(0.34f, 0.28f, 0.20f, 1f);
        private static readonly Color InkGood = new Color(0.10f, 0.42f, 0.16f, 1f);
        private static readonly Color InkBad  = new Color(0.55f, 0.12f, 0.10f, 1f);

        public bool IsOpen => _open;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _panelHandle = PanelManager.Register("Workshop", Close, () => IsOpen);
            // DEF-213: let the Workshop interaction open this panel by id.
            PanelRouter.Register(PanelId.Crafting, Open);
        }

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        private void OnDestroy()
        {
            PanelRouter.Unregister(PanelId.Crafting, Open);
            if (_vm != null) { _vm.Changed -= Repaint; _vm.Dispose(); _vm = null; }
            if (_modal != null && _modal.canvas != null) Destroy(_modal.canvas);
        }

        // ── Public open/close ───────────────────────────────────────────────

        public void Toggle()
        {
            if (IsOpen) Close(); else Open();
        }

        public void Open()
        {
            EnsureBuilt();
            if (_modal == null || _modal.canvas == null) return;
            _open = true;
            _modal.canvas.SetActive(true);
            // Arbiter closes any other open panel first (DEF-212); battle-lock may
            // reject — revert and stay hidden, never force-show.
            if (!PanelManager.NotifyOpened(_panelHandle))
            {
                _open = false;
                _modal.canvas.SetActive(false);
                return;
            }
            Repaint();
        }

        public void Close()
        {
            if (_modal == null || _modal.canvas == null) { _open = false; return; }
            _open = false;
            _modal.canvas.SetActive(false);
            PanelManager.NotifyClosed(_panelHandle);
        }

        // ── Build (kit modal, lazy on first open) ───────────────────────────

        private void EnsureBuilt()
        {
            if (_modal != null && _modal.canvas != null) return;

            _modal = ElarionUiKit.BuildObsidianModal("WorkshopUI", "Workshop",
                new Vector2(0.10f, 0.08f), new Vector2(0.90f, 0.92f), Close,
                frameName: RpgUiCatalog.FrameCrafting, medallionIcon: "hammer");

            var layout = _modal.chrome.layout;
            _recipeHost = layout != null && layout.bodyLeft != null
                ? (Transform)layout.bodyLeft
                : _modal.chrome.content.transform;
            _detailHost = layout != null && layout.bodyRight != null
                ? (Transform)layout.bodyRight
                : _modal.chrome.content.transform;

            var footHost = layout != null && layout.footer != null
                ? (Transform)layout.footer
                : _modal.chrome.content.transform;
            _larder = MakeText(footHost, "", 14, ElarionUi.Parchment, FontStyles.Normal,
                TextAlignmentOptions.Left, new Vector2(0.01f, 0f), new Vector2(0.99f, 1f));

            _vm = WorkshopCraftVM.CreateDefault(Close);
            _vm.Changed += Repaint;

            _modal.canvas.SetActive(false);   // built hidden; Open shows it
        }

        // ── Repaint (VM -> View, one direction) ─────────────────────────────

        private void Repaint()
        {
            if (!_open || _vm == null || _recipeHost == null || _detailHost == null) return;

            // Recipe rows (dark well, left).
            for (int i = _recipeHost.childCount - 1; i >= 0; i--)
                Destroy(_recipeHost.GetChild(i).gameObject);

            var rows = _vm.Rows;
            if (rows == null || rows.Count == 0)
            {
                MakeText(_recipeHost, "No recipes loaded.", 14, ElarionUi.ParchmentDim,
                    FontStyles.Italic, TextAlignmentOptions.Center,
                    new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.98f));
            }
            else
            {
                const float rowH = 0.105f, gap = 0.015f;
                float top = 0.98f;
                foreach (var row in rows)
                {
                    string id = row.Id;
                    // ASCII markers (✓/✗ are missing from the TMP font).
                    string label = row.Name + (row.Affordable ? "  +" : "  -");
                    ElarionUiKit.BuildObsidianButton(_recipeHost, label,
                        ElarionUiKit.ObsidianButtonStyle.Style1,
                        row.Equipped ? ElarionUiKit.ObsidianButtonColor.Yellow
                                     : ElarionUiKit.ObsidianButtonColor.Gray,
                        new Vector2(0.04f, top - rowH), new Vector2(0.96f, top),
                        () => { _vm.Select(id); });
                    top -= rowH + gap;
                    if (top - rowH < 0f) break;   // bounded: never overflow the well
                }
            }

            // Detail (parchment well, right — dark ink).
            for (int i = _detailHost.childCount - 1; i >= 0; i--)
                Destroy(_detailHost.GetChild(i).gameObject);
            if (_vm.HasSelection) BuildDetail(_vm.Selected);
            else
                MakeText(_detailHost, "Select a recipe.", 15, InkDim, FontStyles.Italic,
                    TextAlignmentOptions.Center, new Vector2(0.05f, 0.45f), new Vector2(0.95f, 0.55f));

            if (_larder != null) _larder.text = _vm.Larder;
        }

        private void BuildDetail(CraftRecipeVM recipe)
        {
            string display = recipe.DisplayName;

            MakeText(_detailHost, display, 20, Ink, FontStyles.Bold,
                TextAlignmentOptions.Left, new Vector2(0.06f, 0.90f), new Vector2(0.94f, 0.99f));

            float y = 0.88f;
            if (!string.IsNullOrEmpty(recipe.Description))
            {
                MakeText(_detailHost, recipe.Description, 14, InkDim, FontStyles.Normal,
                    TextAlignmentOptions.TopLeft, new Vector2(0.06f, y - 0.14f), new Vector2(0.94f, y));
                y -= 0.16f;
            }

            MakeText(_detailHost, "Ingredients", 15, Ink, FontStyles.Bold,
                TextAlignmentOptions.Left, new Vector2(0.06f, y - 0.06f), new Vector2(0.94f, y));
            y -= 0.07f;

            if (recipe.Ingredients != null)
            {
                foreach (var line in recipe.Ingredients)
                {
                    MakeText(_detailHost,
                        (line.Met ? "+  " : "-  ") + line.DisplayName,
                        14, line.Met ? InkGood : InkBad, FontStyles.Normal,
                        TextAlignmentOptions.Left, new Vector2(0.08f, y - 0.055f), new Vector2(0.70f, y));
                    MakeText(_detailHost, $"{line.Have}/{line.Need}", 14, InkDim, FontStyles.Normal,
                        TextAlignmentOptions.Right, new Vector2(0.70f, y - 0.055f), new Vector2(0.92f, y));
                    y -= 0.06f;
                }
            }

            // Output preview.
            y -= 0.03f;
            int held = recipe.OutputHeld;
            string glyph = string.IsNullOrEmpty(recipe.ResultGlyph) ? "" : recipe.ResultGlyph + "  ";
            MakeText(_detailHost, "Output", 15, Ink, FontStyles.Bold,
                TextAlignmentOptions.Left, new Vector2(0.06f, y - 0.06f), new Vector2(0.94f, y));
            y -= 0.065f;
            MakeText(_detailHost, $"{glyph}{display}  x1  (have {held})", 14, Ink, FontStyles.Normal,
                TextAlignmentOptions.Left, new Vector2(0.08f, y - 0.055f), new Vector2(0.94f, y));

            // Craft CTA — Green when affordable, Gray (still tappable) when short.
            bool canCraft = recipe.CanCraft;
            var btn = ElarionUiKit.BuildObsidianButton(_detailHost, "Craft",
                ElarionUiKit.ObsidianButtonStyle.Style1,
                canCraft ? ElarionUiKit.ObsidianButtonColor.Green
                         : ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.28f, 0.03f), new Vector2(0.72f, 0.13f),
                () => _vm.Craft());
            btn.interactable = canCraft;
        }

        // ── uGUI helper ──────────────────────────────────────────────────────

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
