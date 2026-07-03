// =============================================================================
// VillageCraftingPanel — the Workshop crafting station panel.
// -----------------------------------------------------------------------------
// WO-F conversion (2026-07-03, coverage matrix row #2): UIDocument/UITK card ->
// code-built uGUI on the Obsidian MASTER-DETAIL template (BuildObsidianModal:
// FrameCrafting — the owner-ratified "spot on 100% perfect" split frame).
//   • bodyLeft  (dark well)      = recipe rows (Obsidian buttons, selected=Yellow,
//                                  ✓/✗ affordability suffix)
//   • bodyRight (parchment well) = selected recipe detail: description,
//                                  ingredient checklist, output, Craft CTA
//                                  (dark-INK text — the well is parchment)
//   • footer    (action strip)   = the larder readout
// The ONE shared Close is the chrome's (the old per-panel "X" chip is retired).
//
// Public API preserved: Toggle() / Open() / Close() / IsOpen / Instance;
// PanelRouter.Register(PanelId.Crafting, Open); arbiter handle "Workshop";
// VillageInventory.Changed -> Repaint. Spawned by VillageCraftingPanelBootstrap.
// =============================================================================

using System.Collections.Generic;
using System.Text;
using DeNelle.Core.UI;
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

        private string _selectedRecipeId;
        private bool _open;
        private PanelHandle _panelHandle;

        // Dark ink for text sitting ON the parchment well (light Parchment text
        // is unreadable there — the well IS parchment).
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

        private void OnEnable()
        {
            if (VillageInventory.Instance != null)
                VillageInventory.Instance.Changed += Repaint;
        }

        private void OnDisable()
        {
            if (VillageInventory.Instance != null)
                VillageInventory.Instance.Changed -= Repaint;
            if (Instance == this) Instance = null;
        }

        private void OnDestroy()
        {
            PanelRouter.Unregister(PanelId.Crafting, Open);
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
            if (string.IsNullOrEmpty(_selectedRecipeId))
            {
                var first = CraftingRecipeCatalog.All;
                if (first != null && first.Count > 0) _selectedRecipeId = first[0].Id;
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

            _modal.canvas.SetActive(false);   // built hidden; Open shows it
        }

        // ── Repaint ─────────────────────────────────────────────────────────

        private void Repaint()
        {
            if (!_open || _recipeHost == null || _detailHost == null) return;

            // Recipe rows (dark well, left).
            for (int i = _recipeHost.childCount - 1; i >= 0; i--)
                Destroy(_recipeHost.GetChild(i).gameObject);

            var recipes = CraftingRecipeCatalog.All;
            var inv = VillageInventory.Instance;
            if (recipes == null || recipes.Count == 0)
            {
                MakeText(_recipeHost, "No recipes loaded.", 14, ElarionUi.ParchmentDim,
                    FontStyles.Italic, TextAlignmentOptions.Center,
                    new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.98f));
            }
            else
            {
                const float rowH = 0.105f, gap = 0.015f;
                float top = 0.98f;
                foreach (var r in recipes)
                {
                    if (r == null) continue;
                    string id = r.Id;
                    bool selected = id == _selectedRecipeId;
                    bool canCraft = inv != null && inv.CanCraft(id);
                    // ASCII markers (eyes-on 2026-07-03: ✓/✗ are missing from the TMP font
                    // and rendered as boxes in the 14:46 capture).
                    string label = (string.IsNullOrEmpty(r.DisplayName) ? id : r.DisplayName)
                                 + (canCraft ? "  +" : "  -");
                    ElarionUiKit.BuildObsidianButton(_recipeHost, label,
                        ElarionUiKit.ObsidianButtonStyle.Style1,
                        selected ? ElarionUiKit.ObsidianButtonColor.Yellow
                                 : ElarionUiKit.ObsidianButtonColor.Gray,
                        new Vector2(0.04f, top - rowH), new Vector2(0.96f, top),
                        () => { _selectedRecipeId = id; Repaint(); });
                    top -= rowH + gap;
                    if (top - rowH < 0f) break;   // bounded: never overflow the well
                }
            }

            // Detail (parchment well, right — dark ink).
            for (int i = _detailHost.childCount - 1; i >= 0; i--)
                Destroy(_detailHost.GetChild(i).gameObject);
            var selectedRecipe = CraftingRecipeCatalog.Find(_selectedRecipeId);
            if (selectedRecipe != null) BuildDetail(selectedRecipe);
            else
                MakeText(_detailHost, "Select a recipe.", 15, InkDim, FontStyles.Italic,
                    TextAlignmentOptions.Center, new Vector2(0.05f, 0.45f), new Vector2(0.95f, 0.55f));

            BuildLarder();
        }

        private void BuildDetail(RecipeDef recipe)
        {
            var inv = VillageInventory.Instance;
            string display = string.IsNullOrEmpty(recipe.DisplayName) ? recipe.Id : recipe.DisplayName;

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
                    if (line == null) continue;
                    int have = inv != null ? inv.Get(line.IngredientId) : 0;
                    bool ok = have >= line.Count;
                    MakeText(_detailHost,
                        (ok ? "+  " : "-  ") + CraftingRecipeCatalog.DisplayNameFor(line.IngredientId),
                        14, ok ? InkGood : InkBad, FontStyles.Normal,
                        TextAlignmentOptions.Left, new Vector2(0.08f, y - 0.055f), new Vector2(0.70f, y));
                    MakeText(_detailHost, $"{have}/{line.Count}", 14, InkDim, FontStyles.Normal,
                        TextAlignmentOptions.Right, new Vector2(0.70f, y - 0.055f), new Vector2(0.92f, y));
                    y -= 0.06f;
                }
            }

            // Output preview.
            y -= 0.03f;
            int held = inv != null ? inv.Get(recipe.OutputId) : 0;
            string glyph = string.IsNullOrEmpty(recipe.ResultGlyph) ? "" : recipe.ResultGlyph + "  ";
            MakeText(_detailHost, "Output", 15, Ink, FontStyles.Bold,
                TextAlignmentOptions.Left, new Vector2(0.06f, y - 0.06f), new Vector2(0.94f, y));
            y -= 0.065f;
            MakeText(_detailHost, $"{glyph}{display}  x1  (have {held})", 14, Ink, FontStyles.Normal,
                TextAlignmentOptions.Left, new Vector2(0.08f, y - 0.055f), new Vector2(0.94f, y));

            // Craft CTA — Green when affordable, Gray (still tappable; repaint keeps
            // it honest) when short.
            bool canCraft = inv != null && inv.CanCraft(recipe.Id);
            var btn = ElarionUiKit.BuildObsidianButton(_detailHost, "Craft",
                ElarionUiKit.ObsidianButtonStyle.Style1,
                canCraft ? ElarionUiKit.ObsidianButtonColor.Green
                         : ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.28f, 0.03f), new Vector2(0.72f, 0.13f),
                () => OnCraftClicked(recipe));
            btn.interactable = canCraft;
        }

        private void OnCraftClicked(RecipeDef recipe)
        {
            if (recipe == null) return;
            var inv = VillageInventory.Instance;
            if (inv == null) return;
            bool ok = inv.TryCraft(recipe.Id);
            if (ok)
            {
                if (!string.IsNullOrEmpty(recipe.CraftedToast))
                    Debug.Log("[VillageCrafting] " + recipe.CraftedToast);
                else
                    Debug.Log("[VillageCrafting] Crafted " + recipe.DisplayName);
            }
            // Repaint pulls fresh counts whether or not the craft went through.
            Repaint();
        }

        private void BuildLarder()
        {
            if (_larder == null) return;
            var inv = VillageInventory.Instance;
            if (inv == null || inv.Counts.Count == 0)
            {
                _larder.text = "Larder:  (empty)";
                return;
            }

            // Stable order: ingredients first (catalog order), then recipe outputs,
            // then any orphan keys (defensive) — same ordering as the old panel.
            var seen = new HashSet<string>();
            var ordered = new List<string>();
            foreach (var ing in CraftingRecipeCatalog.Ingredients)
            {
                if (ing == null || string.IsNullOrEmpty(ing.Id)) continue;
                if (inv.Get(ing.Id) > 0) { ordered.Add(ing.Id); seen.Add(ing.Id); }
            }
            foreach (var r in CraftingRecipeCatalog.All)
            {
                if (r == null || string.IsNullOrEmpty(r.Id)) continue;
                if (seen.Contains(r.Id)) continue;
                if (inv.Get(r.Id) > 0) { ordered.Add(r.Id); seen.Add(r.Id); }
            }
            foreach (var kv in inv.Counts)
                if (!seen.Contains(kv.Key)) ordered.Add(kv.Key);

            var sb = new StringBuilder("Larder:  ");
            bool first = true;
            foreach (var id in ordered)
            {
                int n = inv.Get(id);
                if (n <= 0) continue;
                if (!first) sb.Append("  ·  ");
                string glyph = ResolveGlyph(id);
                if (!string.IsNullOrEmpty(glyph)) sb.Append(glyph).Append(' ');
                sb.Append(CraftingRecipeCatalog.DisplayNameFor(id)).Append(" x").Append(n);
                first = false;
            }
            _larder.text = sb.ToString();
        }

        private static string ResolveGlyph(string id)
        {
            var ing = CraftingRecipeCatalog.FindIngredient(id);
            if (ing != null && !string.IsNullOrEmpty(ing.Glyph)) return ing.Glyph;
            var r = CraftingRecipeCatalog.Find(id);
            if (r != null && !string.IsNullOrEmpty(r.ResultGlyph)) return r.ResultGlyph;
            return null;
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
