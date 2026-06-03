// =============================================================================
// VillageCraftingPanel — UI Toolkit panel for the Workshop crafting station.
// -----------------------------------------------------------------------------
// Layout:
//   ┌─────────────────────────────────────────────┐
//   │ Workshop                              [ X ] │
//   ├──────────────┬──────────────────────────────┤
//   │ Recipes      │ <Selected recipe name>       │
//   │ • Torch      │ <description>                │
//   │              │                              │
//   │              │ Ingredients:                 │
//   │              │  ✓ Dry Reed       1/1        │
//   │              │  ✗ Ember Resin    0/1        │
//   │              │                              │
//   │              │ Output: T  Torch             │
//   │              │ [        Craft        ]      │
//   ├──────────────┴──────────────────────────────┤
//   │ Larder: Reed x5 · Cloth x5 · Resin x5       │
//   └─────────────────────────────────────────────┘
//
// The panel is spawned by VillageCraftingPanelBootstrap. Public API:
//   • Toggle()  — flip visibility
//   • Open()    — show + refresh
//   • Close()   — hide
//   • IsOpen   — read-only state
//
// Single instance: BuildingInteractable (which we cannot modify) routes the
// Workshop F press to a "Workshop crafting — Week 7" toast. We piggy-back via
// VillageCraftingPanelBootstrap's separate proximity watcher.
// =============================================================================

using System.Collections.Generic;
using System.Text;
using DeNelle.Core.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Village.Crafting
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class VillageCraftingPanel : MonoBehaviour
    {
        public static VillageCraftingPanel Instance { get; private set; }

        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _shell;        // the modal card
        private VisualElement _recipeList;
        private VisualElement _detailPane;
        private VisualElement _footerStrip;

        private string _selectedRecipeId;
        // Modal arbiter handle (DEF-212): one panel open at a time.
        private PanelHandle _panelHandle;

        public bool IsOpen => _shell != null && _shell.style.display.value != DisplayStyle.None;

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _panelHandle = PanelManager.Register("Workshop", Close, () => IsOpen);
        }

        private void OnEnable()
        {
            Build();
            if (VillageInventory.Instance != null)
                VillageInventory.Instance.Changed += Repaint;
        }

        private void OnDisable()
        {
            if (VillageInventory.Instance != null)
                VillageInventory.Instance.Changed -= Repaint;
            if (Instance == this) Instance = null;
        }

        // ── Public open/close ───────────────────────────────────────────────

        public void Toggle()
        {
            if (IsOpen) Close(); else Open();
        }

        public void Open()
        {
            if (_shell == null) Build();
            if (_shell == null) return;
            _shell.style.display = DisplayStyle.Flex;
            // Arbiter closes any other open panel first (DEF-212).
            PanelManager.NotifyOpened(_panelHandle);
            // Dim + capture input only while open, so a closed panel never eats
            // touches or leaves a permanent darkened backdrop on screen. DEF-212
            // item 5: near-opaque so world-space labels don't bleed through.
            if (_root != null)
            {
                _root.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.92f));
                _root.pickingMode = PickingMode.Position;
                _root.Focus(); // so the ESC KeyDownEvent reaches us on desktop
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
            if (_shell == null) return;
            _shell.style.display = DisplayStyle.None;
            // Release the backdrop so the HUD beneath is interactive again.
            if (_root != null)
            {
                _root.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
                _root.pickingMode = PickingMode.Ignore;
            }
            PanelManager.NotifyClosed(_panelHandle);
        }

        // ── Build ────────────────────────────────────────────────────────────

        private void Build()
        {
            _root = _doc.rootVisualElement;
            if (_root == null) return;
            _root.pickingMode = PickingMode.Ignore; // don't block HUD beneath

            _root.Clear();
            _root.style.position = Position.Absolute;
            _root.style.left = 0; _root.style.right = 0;
            _root.style.top = 0;  _root.style.bottom = 0;
            _root.style.alignItems = Align.Center;
            _root.style.justifyContent = Justify.Center;
            _root.RegisterCallback<KeyDownEvent>(OnRootKey, TrickleDown.TrickleDown);
            // Mobile close affordance: a tap on the dimmed backdrop (i.e. NOT inside
            // the card) dismisses the panel. ESC is keyboard-only, so without this a
            // touch player who can't hit the small X has no way out (DEF-218).
            _root.RegisterCallback<PointerDownEvent>(OnBackdropPointerDown);

            _shell = new VisualElement { name = "CraftingShell" };
            _shell.style.width = 760;
            _shell.style.maxWidth = new Length(95, LengthUnit.Percent);
            _shell.style.height = 520;
            _shell.style.maxHeight = new Length(95, LengthUnit.Percent);
            _shell.style.flexDirection = FlexDirection.Column;
            _shell.style.backgroundColor = new StyleColor(new Color(0.09f, 0.07f, 0.06f, 0.98f));
            _shell.style.borderTopLeftRadius = 12;
            _shell.style.borderTopRightRadius = 12;
            _shell.style.borderBottomLeftRadius = 12;
            _shell.style.borderBottomRightRadius = 12;
            ApplyBorder(_shell, new Color(0.85f, 0.66f, 0.30f, 1f), 2);
            _root.Add(_shell);

            BuildHeader(_shell);

            var body = new VisualElement { name = "Body" };
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1;
            body.style.paddingLeft = 12; body.style.paddingRight = 12;
            body.style.paddingTop = 8; body.style.paddingBottom = 8;
            _shell.Add(body);

            _recipeList = new VisualElement { name = "RecipeList" };
            _recipeList.style.width = 230;
            _recipeList.style.flexDirection = FlexDirection.Column;
            _recipeList.style.marginRight = 10;
            _recipeList.style.paddingTop = 6; _recipeList.style.paddingBottom = 6;
            _recipeList.style.paddingLeft = 6; _recipeList.style.paddingRight = 6;
            _recipeList.style.backgroundColor = new StyleColor(new Color(0.05f, 0.04f, 0.03f, 0.85f));
            _recipeList.style.borderTopLeftRadius = 8;
            _recipeList.style.borderTopRightRadius = 8;
            _recipeList.style.borderBottomLeftRadius = 8;
            _recipeList.style.borderBottomRightRadius = 8;
            body.Add(_recipeList);

            _detailPane = new VisualElement { name = "DetailPane" };
            _detailPane.style.flexGrow = 1;
            _detailPane.style.flexDirection = FlexDirection.Column;
            _detailPane.style.paddingTop = 8; _detailPane.style.paddingBottom = 8;
            _detailPane.style.paddingLeft = 14; _detailPane.style.paddingRight = 14;
            _detailPane.style.backgroundColor = new StyleColor(new Color(0.05f, 0.04f, 0.03f, 0.85f));
            _detailPane.style.borderTopLeftRadius = 8;
            _detailPane.style.borderTopRightRadius = 8;
            _detailPane.style.borderBottomLeftRadius = 8;
            _detailPane.style.borderBottomRightRadius = 8;
            body.Add(_detailPane);

            _footerStrip = new VisualElement { name = "FooterStrip" };
            _footerStrip.style.flexDirection = FlexDirection.Row;
            _footerStrip.style.flexWrap = Wrap.Wrap;
            _footerStrip.style.alignItems = Align.Center;
            _footerStrip.style.paddingLeft = 14; _footerStrip.style.paddingRight = 14;
            _footerStrip.style.paddingTop = 8; _footerStrip.style.paddingBottom = 10;
            _footerStrip.style.borderTopWidth = 1;
            _footerStrip.style.borderTopColor = new StyleColor(new Color(0.85f, 0.66f, 0.30f, 0.45f));
            _shell.Add(_footerStrip);

            // Start hidden; opened by bootstrap.
            _shell.style.display = DisplayStyle.None;

            Repaint();
        }

        private void BuildHeader(VisualElement parent)
        {
            var header = new VisualElement { name = "Header" };
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.paddingLeft = 16; header.style.paddingRight = 10;
            header.style.paddingTop = 10; header.style.paddingBottom = 10;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new StyleColor(new Color(0.85f, 0.66f, 0.30f, 0.45f));
            parent.Add(header);

            var title = new Label("Workshop");
            title.style.color = new StyleColor(new Color(0.99f, 0.86f, 0.50f, 1f));
            title.style.fontSize = 20;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(title);

            // 44x44 min touch target (DEF-218) so the close button is reachable
            // with a finger on mobile, not just a mouse.
            var closeBtn = new Button(Close) { text = "X" };
            closeBtn.style.width = 44; closeBtn.style.height = 44;
            closeBtn.style.fontSize = 18;
            closeBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            closeBtn.style.color = new StyleColor(new Color(0.97f, 0.94f, 0.84f, 1f));
            closeBtn.style.backgroundColor = new StyleColor(new Color(0.18f, 0.10f, 0.06f, 1f));
            closeBtn.style.borderTopLeftRadius = 6;
            closeBtn.style.borderTopRightRadius = 6;
            closeBtn.style.borderBottomLeftRadius = 6;
            closeBtn.style.borderBottomRightRadius = 6;
            ApplyBorder(closeBtn, new Color(0.85f, 0.66f, 0.30f, 0.65f), 1);
            header.Add(closeBtn);
        }

        // ── Repaint ─────────────────────────────────────────────────────────

        private void Repaint()
        {
            if (_recipeList == null || _detailPane == null) return;

            _recipeList.Clear();
            var recipes = CraftingRecipeCatalog.All;
            if (recipes == null || recipes.Count == 0)
            {
                var empty = new Label("No recipes loaded.");
                empty.style.color = new StyleColor(new Color(0.75f, 0.72f, 0.65f, 1f));
                empty.style.fontSize = 12;
                _recipeList.Add(empty);
            }
            else
            {
                foreach (var r in recipes)
                {
                    if (r == null) continue;
                    _recipeList.Add(BuildRecipeRow(r));
                }
            }

            _detailPane.Clear();
            var selected = CraftingRecipeCatalog.Find(_selectedRecipeId);
            if (selected != null) BuildDetail(_detailPane, selected);
            else
            {
                var hint = new Label("Select a recipe.");
                hint.style.color = new StyleColor(new Color(0.75f, 0.72f, 0.65f, 1f));
                _detailPane.Add(hint);
            }

            BuildFooter();
        }

        private VisualElement BuildRecipeRow(RecipeDef recipe)
        {
            bool selected = recipe.Id == _selectedRecipeId;
            var inv = VillageInventory.Instance;
            bool canCraft = inv != null && inv.CanCraft(recipe.Id);

            var row = new Button(() => { _selectedRecipeId = recipe.Id; Repaint(); });
            row.style.marginBottom = 4;
            row.style.paddingLeft = 10; row.style.paddingRight = 10;
            row.style.paddingTop = 8; row.style.paddingBottom = 8;
            row.style.borderTopLeftRadius = 6;
            row.style.borderTopRightRadius = 6;
            row.style.borderBottomLeftRadius = 6;
            row.style.borderBottomRightRadius = 6;
            row.style.backgroundColor = new StyleColor(selected
                ? new Color(0.25f, 0.17f, 0.06f, 1f)
                : new Color(0.12f, 0.09f, 0.05f, 1f));
            ApplyBorder(row,
                selected ? new Color(0.99f, 0.86f, 0.50f, 1f)
                         : new Color(0.85f, 0.66f, 0.30f, 0.35f),
                selected ? 2 : 1);

            row.text = string.Empty;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.justifyContent = Justify.SpaceBetween;

            var name = new Label(string.IsNullOrEmpty(recipe.DisplayName) ? recipe.Id : recipe.DisplayName);
            name.style.color = new StyleColor(new Color(0.97f, 0.94f, 0.84f, 1f));
            name.style.fontSize = 13;
            name.style.flexGrow = 1;
            row.Add(name);

            var badge = new Label(canCraft ? "✓" : "✗"); // ✓ / ✗
            badge.style.color = new StyleColor(canCraft
                ? new Color(0.55f, 0.85f, 0.45f, 1f)
                : new Color(0.85f, 0.45f, 0.40f, 1f));
            badge.style.fontSize = 14;
            badge.style.marginLeft = 6;
            row.Add(badge);

            return row;
        }

        private void BuildDetail(VisualElement parent, RecipeDef recipe)
        {
            var inv = VillageInventory.Instance;

            var title = new Label(string.IsNullOrEmpty(recipe.DisplayName) ? recipe.Id : recipe.DisplayName);
            title.style.color = new StyleColor(new Color(0.99f, 0.86f, 0.50f, 1f));
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 4;
            parent.Add(title);

            if (!string.IsNullOrEmpty(recipe.Description))
            {
                var desc = new Label(recipe.Description);
                desc.style.color = new StyleColor(new Color(0.86f, 0.82f, 0.74f, 1f));
                desc.style.fontSize = 12;
                desc.style.whiteSpace = WhiteSpace.Normal;
                desc.style.marginBottom = 10;
                parent.Add(desc);
            }

            var ingHeader = new Label("Ingredients");
            ingHeader.style.color = new StyleColor(new Color(0.97f, 0.92f, 0.74f, 1f));
            ingHeader.style.fontSize = 13;
            ingHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            ingHeader.style.marginTop = 4;
            ingHeader.style.marginBottom = 4;
            parent.Add(ingHeader);

            if (recipe.Ingredients != null)
            {
                foreach (var line in recipe.Ingredients)
                {
                    if (line == null) continue;
                    int have = inv != null ? inv.Get(line.IngredientId) : 0;
                    bool ok = have >= line.Count;

                    var row = new VisualElement();
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.alignItems = Align.Center;
                    row.style.marginBottom = 3;

                    var check = new Label(ok ? "✓" : "✗"); // ✓ / ✗
                    check.style.color = new StyleColor(ok
                        ? new Color(0.55f, 0.85f, 0.45f, 1f)
                        : new Color(0.85f, 0.45f, 0.40f, 1f));
                    check.style.fontSize = 14;
                    check.style.width = 18;
                    row.Add(check);

                    var label = new Label(CraftingRecipeCatalog.DisplayNameFor(line.IngredientId));
                    label.style.color = new StyleColor(ok
                        ? new Color(0.97f, 0.94f, 0.84f, 1f)
                        : new Color(0.80f, 0.70f, 0.65f, 1f));
                    label.style.fontSize = 12;
                    label.style.flexGrow = 1;
                    row.Add(label);

                    var counts = new Label($"{have}/{line.Count}");
                    counts.style.color = new StyleColor(new Color(0.78f, 0.78f, 0.78f, 1f));
                    counts.style.fontSize = 12;
                    counts.style.marginLeft = 8;
                    row.Add(counts);

                    parent.Add(row);
                }
            }

            // Output preview.
            var outHeader = new Label("Output");
            outHeader.style.color = new StyleColor(new Color(0.97f, 0.92f, 0.74f, 1f));
            outHeader.style.fontSize = 13;
            outHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            outHeader.style.marginTop = 12;
            outHeader.style.marginBottom = 4;
            parent.Add(outHeader);

            var outRow = new VisualElement();
            outRow.style.flexDirection = FlexDirection.Row;
            outRow.style.alignItems = Align.Center;
            outRow.style.marginBottom = 12;

            if (!string.IsNullOrEmpty(recipe.ResultGlyph))
            {
                var glyph = new Label(recipe.ResultGlyph);
                glyph.style.color = new StyleColor(new Color(0.99f, 0.86f, 0.50f, 1f));
                glyph.style.fontSize = 20;
                glyph.style.width = 28;
                glyph.style.unityTextAlign = TextAnchor.MiddleCenter;
                glyph.style.unityFontStyleAndWeight = FontStyle.Bold;
                outRow.Add(glyph);
            }

            int held = inv != null ? inv.Get(recipe.OutputId) : 0;
            var outLabel = new Label($"{(string.IsNullOrEmpty(recipe.DisplayName) ? recipe.Id : recipe.DisplayName)}  x1  (have {held})");
            outLabel.style.color = new StyleColor(new Color(0.97f, 0.94f, 0.84f, 1f));
            outLabel.style.fontSize = 13;
            outRow.Add(outLabel);
            parent.Add(outRow);

            // Craft button.
            bool canCraft = inv != null && inv.CanCraft(recipe.Id);
            var craftBtn = new Button(() => OnCraftClicked(recipe)) { text = "Craft" };
            craftBtn.style.height = 44;
            craftBtn.style.fontSize = 16;
            craftBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            craftBtn.style.marginTop = 8;
            craftBtn.style.borderTopLeftRadius = 8;
            craftBtn.style.borderTopRightRadius = 8;
            craftBtn.style.borderBottomLeftRadius = 8;
            craftBtn.style.borderBottomRightRadius = 8;
            craftBtn.SetEnabled(canCraft);
            craftBtn.style.color = new StyleColor(canCraft
                ? new Color(0.10f, 0.08f, 0.04f, 1f)
                : new Color(0.55f, 0.50f, 0.42f, 1f));
            craftBtn.style.backgroundColor = new StyleColor(canCraft
                ? new Color(0.99f, 0.86f, 0.50f, 1f)
                : new Color(0.30f, 0.24f, 0.18f, 1f));
            ApplyBorder(craftBtn,
                canCraft ? new Color(1f, 0.78f, 0.32f, 1f) : new Color(0.50f, 0.40f, 0.22f, 1f),
                2);
            parent.Add(craftBtn);
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

        private void BuildFooter()
        {
            if (_footerStrip == null) return;
            _footerStrip.Clear();

            var label = new Label("Larder:");
            label.style.color = new StyleColor(new Color(0.97f, 0.92f, 0.74f, 1f));
            label.style.fontSize = 12;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginRight = 10;
            _footerStrip.Add(label);

            var inv = VillageInventory.Instance;
            if (inv == null || inv.Counts.Count == 0)
            {
                var empty = new Label("(empty)");
                empty.style.color = new StyleColor(new Color(0.75f, 0.72f, 0.65f, 1f));
                empty.style.fontSize = 12;
                _footerStrip.Add(empty);
                return;
            }

            // Stable order: ingredients first (in catalog order), then any
            // recipe outputs (also in catalog order).
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
            // Catch any orphan keys (defensive).
            foreach (var kv in inv.Counts)
            {
                if (!seen.Contains(kv.Key)) ordered.Add(kv.Key);
            }

            foreach (var id in ordered)
            {
                int n = inv.Get(id);
                if (n <= 0) continue;
                _footerStrip.Add(BuildLarderChip(id, n));
            }
        }

        private static VisualElement BuildLarderChip(string id, int count)
        {
            var chip = new VisualElement();
            chip.style.flexDirection = FlexDirection.Row;
            chip.style.alignItems = Align.Center;
            chip.style.marginRight = 10;
            chip.style.marginBottom = 2;
            chip.style.paddingLeft = 8; chip.style.paddingRight = 8;
            chip.style.paddingTop = 3; chip.style.paddingBottom = 3;
            chip.style.borderTopLeftRadius = 5;
            chip.style.borderTopRightRadius = 5;
            chip.style.borderBottomLeftRadius = 5;
            chip.style.borderBottomRightRadius = 5;
            chip.style.backgroundColor = new StyleColor(new Color(0.13f, 0.10f, 0.06f, 0.9f));
            ApplyBorder(chip, new Color(0.85f, 0.66f, 0.30f, 0.35f), 1);

            var glyph = ResolveGlyph(id);
            if (!string.IsNullOrEmpty(glyph))
            {
                var g = new Label(glyph);
                g.style.color = new StyleColor(new Color(0.99f, 0.86f, 0.50f, 1f));
                g.style.fontSize = 13;
                g.style.marginRight = 4;
                chip.Add(g);
            }

            var label = new Label($"{CraftingRecipeCatalog.DisplayNameFor(id)} x{count}");
            label.style.color = new StyleColor(new Color(0.97f, 0.94f, 0.84f, 1f));
            label.style.fontSize = 12;
            chip.Add(label);
            return chip;
        }

        private static string ResolveGlyph(string id)
        {
            var ing = CraftingRecipeCatalog.FindIngredient(id);
            if (ing != null && !string.IsNullOrEmpty(ing.Glyph)) return ing.Glyph;
            var r = CraftingRecipeCatalog.Find(id);
            if (r != null && !string.IsNullOrEmpty(r.ResultGlyph)) return r.ResultGlyph;
            return null;
        }

        private void OnRootKey(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape && IsOpen)
            {
                Close();
                evt.StopPropagation();
            }
        }

        // Tap-outside-to-close for touch (DEF-218). Fires only when the press
        // landed on the dimmed backdrop itself, never on the card or its children,
        // so taps inside the panel are never swallowed as a "close".
        private void OnBackdropPointerDown(PointerDownEvent evt)
        {
            if (!IsOpen) return;
            if (evt.target == _root)
            {
                Close();
                evt.StopPropagation();
            }
        }

        private static void ApplyBorder(VisualElement ve, Color color, float width)
        {
            ve.style.borderLeftColor = new StyleColor(color);
            ve.style.borderRightColor = new StyleColor(color);
            ve.style.borderTopColor = new StyleColor(color);
            ve.style.borderBottomColor = new StyleColor(color);
            ve.style.borderLeftWidth = width;
            ve.style.borderRightWidth = width;
            ve.style.borderTopWidth = width;
            ve.style.borderBottomWidth = width;
        }
    }
}
