// =============================================================================
// CraftingPanelController — drives the UI Toolkit crafting panel (Workstream C).
// -----------------------------------------------------------------------------
// The controller behind CraftingPanel.uxml — the modal the Keeper opens at a
// crafting pedestal. It is a PASSIVE view: it owns no crafting state. The
// CraftingPedestal raises typed UnityEvents (OpenRequested / CloseRequested /
// Crafted); this controller renders the snapshot they carry and calls back into
// the pedestal's TryCraft() / ClosePanel() on the button clicks.
//
// MODULE ISOLATION: the panel lives INSIDE DeNelle.Dungeons (the dungeon owns
// its own HUD — there is no dungeon HUD in DeNelle.HUD, which ships only the
// village HUD). It references only the dungeon's own crafting types, which is
// allowed — same module.
//
// The ingredient cells are built ONCE at runtime into "crafting-ingredient-list"
// from the recipe; each Refresh() repaints the have/need numbers off the live
// inventory so the panel always reflects the current state — including after a
// craft consumes the ingredients.
//
// All UI is UI Toolkit (UXML/USS) — project mandate. The panel hides itself
// when no pedestal is open, so the UIDocument can host it permanently.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Dungeons
{
    /// <summary>
    /// Drives the dungeon crafting panel (CraftingPanel.uxml) — renders the
    /// recipe a <see cref="CraftingPedestal"/> hands over, shows live have/need
    /// counts off the <see cref="DungeonInventory"/>, and forwards the Craft /
    /// Close clicks back to the pedestal. A passive UI view.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class CraftingPanelController : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("UIDocument hosting CraftingPanel.uxml. Falls back to the component on this GameObject.")]
        [SerializeField] private UIDocument _document;

        // ── UXML element-name contract with CraftingPanel.uxml ───────────────
        private const string RootName = "crafting-panel-root";
        private const string CardName = "crafting-card";
        private const string RecipeNameName = "crafting-recipe-name";
        private const string RecipeDescName = "crafting-recipe-desc";
        private const string IngredientListName = "crafting-ingredient-list";
        private const string ResultGlyphName = "crafting-result-glyph";
        private const string ResultLabelName = "crafting-result-label";
        private const string CraftButtonName = "crafting-craft-button";
        private const string CloseButtonName = "crafting-close-button";

        // ── USS class names styled by CraftingPanel.uss ──────────────────────
        private const string CellClass = "crafting-ingredient-cell";
        private const string CellHaveClass = "crafting-ingredient-cell--have";
        private const string CellNeedClass = "crafting-ingredient-cell--need";
        private const string GlyphClass = "crafting-ingredient-glyph";
        private const string NameClass = "crafting-ingredient-name";
        private const string CountClass = "crafting-ingredient-count";
        private const string CountMetClass = "crafting-ingredient-count--met";
        private const string TickClass = "crafting-ingredient-tick";
        private const string CraftReadyClass = "crafting-craft-button--ready";
        private const string ResultLabelReadyClass = "crafting-result-label--ready";
        private const string ResultLabelDoneClass = "crafting-result-label--done";

        // LOCALIZE: player-facing crafting-panel copy. Kept here as clearly-marked
        // constants — Workstream C does NOT touch the shared en.json (other
        // agents own it). A future localisation pass moves these into a data file.
        private const string MsgGather = "Gather the ingredients";
        private const string MsgReady = "Ready to craft";
        private const string MsgCraftedFmt = "{0} crafted";
        private const string TickChar = "✔"; // heavy check mark

        // ── Bound UI elements ────────────────────────────────────────────────
        private VisualElement _root;
        private Label _recipeName;
        private Label _recipeDesc;
        private VisualElement _ingredientList;
        private Label _resultGlyph;
        private Label _resultLabel;
        private Button _craftButton;
        private Button _closeButton;

        /// <summary>One built ingredient cell — the count + tick handles for a repaint.</summary>
        private struct IngredientCell
        {
            public VisualElement Root;
            public Label Count;
            public Label Tick;
            public string IngredientId;
            public int Needed;
        }

        private readonly List<IngredientCell> _cells = new List<IngredientCell>();

        // ── Live request being shown ─────────────────────────────────────────
        private CraftingPanelRequest _request;
        private bool _bound;

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private void Awake()
        {
            if (_document == null) _document = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            BindElements();
            Hide();
        }

        private void OnDisable()
        {
            if (_craftButton != null) _craftButton.clicked -= OnCraftClicked;
            if (_closeButton != null) _closeButton.clicked -= OnCloseClicked;
            _bound = false;
        }

        // =====================================================================
        //  UI Toolkit binding
        // =====================================================================

        private void BindElements()
        {
            _root = _document != null ? _document.rootVisualElement : null;
            if (_root == null)
            {
                Debug.LogWarning(
                    "[CraftingPanelController] No UIDocument root — crafting panel will not display.");
                return;
            }

            // The crafting panel may share its UIDocument with the dungeon HUD.
            // Resolve the panel sub-tree by its root name so both can co-exist.
            VisualElement panel = _root.Q<VisualElement>(RootName) ?? _root;

            _recipeName = panel.Q<Label>(RecipeNameName);
            _recipeDesc = panel.Q<Label>(RecipeDescName);
            _ingredientList = panel.Q<VisualElement>(IngredientListName);
            _resultGlyph = panel.Q<Label>(ResultGlyphName);
            _resultLabel = panel.Q<Label>(ResultLabelName);
            _craftButton = panel.Q<Button>(CraftButtonName);
            _closeButton = panel.Q<Button>(CloseButtonName);

            if (_craftButton != null)
            {
                _craftButton.clicked -= OnCraftClicked; // guard a double OnEnable
                _craftButton.clicked += OnCraftClicked;
            }
            if (_closeButton != null)
            {
                _closeButton.clicked -= OnCloseClicked;
                _closeButton.clicked += OnCloseClicked;
            }

            _bound = true;
        }

        // =====================================================================
        //  Pedestal wiring — the DungeonController hooks these to the pedestal.
        // =====================================================================

        /// <summary>
        /// Subscribes this panel to a crafting pedestal's events. Called by the
        /// dungeon controller once the pedestal is configured — keeps the pedestal
        /// a pure scene actor and the panel a pure UI view.
        /// </summary>
        public void BindPedestal(CraftingPedestal pedestal)
        {
            if (pedestal == null) return;
            pedestal.OpenRequested.AddListener(Show);
            pedestal.CloseRequested.AddListener(Hide);
        }

        // =====================================================================
        //  Show / hide
        // =====================================================================

        /// <summary>Opens the panel against the pedestal request and paints it.</summary>
        public void Show(CraftingPanelRequest request)
        {
            if (!_bound) BindElements();
            _request = request;
            if (_request == null || _request.Recipe == null)
            {
                Hide();
                return;
            }

            BuildIngredientCells();

            if (_recipeName != null) _recipeName.text = _request.Recipe.DisplayName ?? "Recipe";
            if (_recipeDesc != null) _recipeDesc.text = _request.Recipe.Description ?? string.Empty;
            if (_resultGlyph != null)
                _resultGlyph.text = string.IsNullOrEmpty(_request.Recipe.ResultGlyph)
                    ? "?" : _request.Recipe.ResultGlyph;

            Refresh();

            if (_root != null) _root.style.display = DisplayStyle.Flex;
        }

        /// <summary>Hides the panel — gameplay carries on behind it.</summary>
        public void Hide()
        {
            if (_root != null) _root.style.display = DisplayStyle.None;
        }

        /// <summary>True while the panel is shown.</summary>
        public bool IsShown => _root != null && _root.style.display == DisplayStyle.Flex;

        // =====================================================================
        //  Cell construction + repaint
        // =====================================================================

        /// <summary>Builds one cell per recipe ingredient into the list container.</summary>
        private void BuildIngredientCells()
        {
            _cells.Clear();
            if (_ingredientList == null || _request?.Recipe?.Ingredients == null) return;
            _ingredientList.Clear();

            foreach (var line in _request.Recipe.Ingredients)
            {
                if (line == null) continue;

                CraftingIngredient ing = _request.CraftingData?.FindIngredient(line.IngredientId);

                var cell = new VisualElement();
                cell.AddToClassList(CellClass);

                var glyph = new Label(ing != null && !string.IsNullOrEmpty(ing.Glyph)
                    ? ing.Glyph : "?");
                glyph.AddToClassList(GlyphClass);
                glyph.pickingMode = PickingMode.Ignore;
                ApplyGlyphTint(glyph, ing);
                cell.Add(glyph);

                var name = new Label(ing != null ? ing.DisplayName : line.IngredientId);
                name.AddToClassList(NameClass);
                name.pickingMode = PickingMode.Ignore;
                cell.Add(name);

                var count = new Label(string.Empty);
                count.AddToClassList(CountClass);
                count.pickingMode = PickingMode.Ignore;
                cell.Add(count);

                var tick = new Label(string.Empty);
                tick.AddToClassList(TickClass);
                tick.pickingMode = PickingMode.Ignore;
                cell.Add(tick);

                _ingredientList.Add(cell);
                _cells.Add(new IngredientCell
                {
                    Root = cell,
                    Count = count,
                    Tick = tick,
                    IngredientId = line.IngredientId,
                    Needed = line.Count,
                });
            }
        }

        /// <summary>
        /// Repaints every ingredient cell + the result row + the Craft button
        /// off the live inventory. Safe to call any time the panel is shown.
        /// </summary>
        private void Refresh()
        {
            if (_request == null) return;
            DungeonInventory inv = _request.Inventory;
            CraftingRecipe recipe = _request.Recipe;
            bool alreadyCrafted = inv != null && recipe != null && inv.HasCrafted(recipe.Id);

            // ── Ingredient cells ─────────────────────────────────────────────
            for (int i = 0; i < _cells.Count; i++)
            {
                IngredientCell cell = _cells[i];
                int have = inv != null ? inv.CountOf(cell.IngredientId) : 0;
                // A crafted recipe has consumed its ingredients — show the cells
                // as satisfied (the need was met at craft time) so the panel
                // reads as a finished recipe rather than an empty larder.
                bool met = alreadyCrafted || have >= cell.Needed;
                int shown = alreadyCrafted ? cell.Needed : have;

                if (cell.Count != null)
                {
                    cell.Count.text = $"{shown} / {cell.Needed}";
                    cell.Count.EnableInClassList(CountMetClass, met);
                }
                if (cell.Tick != null)
                    cell.Tick.text = met ? TickChar : string.Empty;
                if (cell.Root != null)
                {
                    cell.Root.EnableInClassList(CellHaveClass, met);
                    cell.Root.EnableInClassList(CellNeedClass, !met);
                }
            }

            // ── Result row + Craft button ────────────────────────────────────
            bool canCraft = !alreadyCrafted && inv != null && inv.CanCraft(recipe);

            if (_resultLabel != null)
            {
                if (alreadyCrafted)
                    _resultLabel.text = string.Format(MsgCraftedFmt, recipe.DisplayName);
                else if (canCraft)
                    _resultLabel.text = MsgReady;
                else
                    _resultLabel.text = MsgGather;

                _resultLabel.EnableInClassList(ResultLabelReadyClass, canCraft && !alreadyCrafted);
                _resultLabel.EnableInClassList(ResultLabelDoneClass, alreadyCrafted);
            }

            if (_resultGlyph != null)
            {
                // Glyph plate warms amber when ready, gold once crafted.
                Color plate = alreadyCrafted
                    ? new Color(1f, 0.812f, 0.420f)
                    : canCraft ? new Color(0.910f, 0.659f, 0.290f)
                    : new Color(0.471f, 0.408f, 0.588f);
                _resultGlyph.style.backgroundColor = plate;
            }

            if (_craftButton != null)
            {
                _craftButton.SetEnabled(canCraft);
                _craftButton.EnableInClassList(CraftReadyClass, canCraft);
                _craftButton.text = alreadyCrafted ? "Crafted" : "Craft";
            }
        }

        /// <summary>Tints an ingredient cell's glyph plate from the data's hex tint.</summary>
        private static void ApplyGlyphTint(Label glyph, CraftingIngredient ing)
        {
            if (glyph == null || ing == null || string.IsNullOrEmpty(ing.Tint)) return;
            if (ColorUtility.TryParseHtmlString("#" + ing.Tint, out Color c))
                glyph.style.backgroundColor = c;
        }

        // =====================================================================
        //  Button handlers
        // =====================================================================

        /// <summary>Forwards the Craft click to the pedestal, then repaints.</summary>
        private void OnCraftClicked()
        {
            if (_request?.Pedestal == null) return;
            CraftingPanelRequest updated = _request.Pedestal.TryCraft();
            if (updated != null) _request = updated;
            Refresh();
        }

        /// <summary>Closes the panel through the pedestal so its state stays in sync.</summary>
        private void OnCloseClicked()
        {
            if (_request?.Pedestal != null)
                _request.Pedestal.ClosePanel();
            else
                Hide();
        }
    }
}
