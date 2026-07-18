// =============================================================================
// WorkshopCraftVM -- the Workshop crafting-station ViewModel (MVVM, Silo F).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Crafting
//
// Owns EVERYTHING VillageCraftingPanel used to read inline: the CraftingRecipeCatalog
// recipe list, the VillageInventory have-counts, the per-recipe have/need projection,
// the craftable flag, and the larder readout. The View becomes a dumb skin that binds
// Rows / Selected / Larder and routes Select / Craft back as commands -- it names NO
// VillageInventory.Instance and NO CraftingRecipeCatalog (Silo F banned symbols).
//
// Each recipe projects into the PROMOTED Core CraftRecipeVM (shared with the dungeon
// crafting VM). The projection is a set of PURE statics (unit-testable with fabricated
// RecipeDefs + a have-count map); the live instance wires the real catalog + inventory
// behind the <see cref="IWorkshopLarder"/> seam (the ONLY resolution site is CreateDefault).
// =============================================================================
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using DeNelle.Core.UI.Mvvm;

namespace DeNelle.Village.Crafting
{
    /// <summary>The minimal larder surface the VM reads. Implemented live by
    /// <see cref="VillageInventoryLarder"/> (VillageInventory) and by a fake in tests.</summary>
    public interface IWorkshopLarder
    {
        int Get(string id);
        bool CanCraft(string recipeId);
        bool TryCraft(string recipeId);
        IReadOnlyDictionary<string, int> Counts { get; }
        event Action Changed;
    }

    /// <summary>ViewModel for the Workshop crafting panel. Projects recipes into Core
    /// <see cref="CraftRecipeVM"/>s + the larder line; raises <see cref="Changed"/> on any inventory change.</summary>
    public sealed class WorkshopCraftVM : IPanelViewModel, IDisposable
    {
        private readonly IReadOnlyList<RecipeDef> _recipes;
        private readonly IWorkshopLarder _larder;
        private readonly Action _onClose;
        private readonly Func<string, string> _displayNameFor;
        private readonly Func<string, string> _glyphFor;
        private readonly IReadOnlyList<IngredientDef> _ingredientDefs;
        private bool _disposed;

        private string _selectedRecipeId;

        private readonly List<ItemVM> _rows = new List<ItemVM>();
        private CraftRecipeVM _selected;
        private string _larderText = "Larder:  (empty)";

        /// <summary>Live constructor: wires the real CraftingRecipeCatalog name/glyph resolvers.</summary>
        public WorkshopCraftVM(IReadOnlyList<RecipeDef> recipes, IWorkshopLarder larder, Action onClose)
            : this(recipes, larder, onClose,
                   CraftingRecipeCatalog.DisplayNameFor, ResolveGlyph, CraftingRecipeCatalog.Ingredients)
        {
        }

        /// <summary>Full constructor (hermetic -- tests inject name/glyph resolvers + ingredient defs so
        /// the projection needs no JSON catalog).</summary>
        public WorkshopCraftVM(IReadOnlyList<RecipeDef> recipes, IWorkshopLarder larder, Action onClose,
            Func<string, string> displayNameFor, Func<string, string> glyphFor,
            IReadOnlyList<IngredientDef> ingredientDefs)
        {
            _recipes = recipes ?? Array.Empty<RecipeDef>();
            _larder = larder;
            _onClose = onClose;
            _displayNameFor = displayNameFor ?? (id => id);
            _glyphFor = glyphFor ?? (_ => null);
            _ingredientDefs = ingredientDefs;

            // Default selection = the first recipe (mirrors VillageCraftingPanel.Open).
            if (string.IsNullOrEmpty(_selectedRecipeId) && _recipes.Count > 0 && _recipes[0] != null)
                _selectedRecipeId = _recipes[0].Id;

            if (_larder != null) _larder.Changed += OnLarderChanged;
            Recompute();
        }

        /// <summary>The ONLY resolution site: the live CraftingRecipeCatalog + VillageInventory.</summary>
        public static WorkshopCraftVM CreateDefault(Action onClose)
        {
            return new WorkshopCraftVM(CraftingRecipeCatalog.All,
                new VillageInventoryLarder(VillageInventory.Instance), onClose);
        }

        // -- IPanelViewModel ----------------------------------------------------
        public event Action Changed;
        public string Title => "Workshop";
        public void Close() => _onClose?.Invoke();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_larder != null)
            {
                _larder.Changed -= OnLarderChanged;
                if (_larder is IDisposable d) d.Dispose();
            }
            Changed = null;
        }

        private void OnLarderChanged() { Recompute(); Raise(); }

        // -- Read-only data the View renders ------------------------------------

        /// <summary>One row per recipe: Name=display, Affordable=canCraft, Equipped=selected.</summary>
        public IReadOnlyList<ItemVM> Rows => _rows;
        /// <summary>The selected recipe's full have/need projection (HasRecipe==false when none).</summary>
        public CraftRecipeVM Selected => _selected;
        public bool HasSelection => _selected.HasRecipe;
        /// <summary>The composed larder readout line.</summary>
        public string Larder => _larderText;
        public string SelectedRecipeId => _selectedRecipeId;

        // -- Commands -----------------------------------------------------------

        /// <summary>Select a recipe row (repaints the detail).</summary>
        public void Select(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId)) return;
            _selectedRecipeId = recipeId;
            Recompute();
            Raise();
        }

        /// <summary>Craft the selected recipe (honest whether or not it went through).</summary>
        public void Craft()
        {
            if (_larder != null && !string.IsNullOrEmpty(_selectedRecipeId))
            {
                bool ok = _larder.TryCraft(_selectedRecipeId);
                if (ok)
                {
                    var r = FindRecipe(_selectedRecipeId);
                    string toast = r != null && !string.IsNullOrEmpty(r.CraftedToast)
                        ? r.CraftedToast : "Crafted " + (r != null ? r.DisplayName : _selectedRecipeId);
                    Debug.Log("[VillageCrafting] " + toast);
                }
            }
            Recompute();
            Raise();
        }

        // -- Recompute (live catalog + inventory) -------------------------------

        private void Recompute()
        {
            _rows.Clear();
            foreach (var r in _recipes)
            {
                if (r == null) continue;
                bool canCraft = _larder != null && _larder.CanCraft(r.Id);
                string name = string.IsNullOrEmpty(r.DisplayName) ? r.Id : r.DisplayName;
                _rows.Add(new ItemVM(r.Id, name, null, null, 0, "", canCraft,
                                     rarity: null, equipped: r.Id == _selectedRecipeId, locked: false));
            }

            var selectedRecipe = FindRecipe(_selectedRecipeId);
            if (selectedRecipe != null)
            {
                bool canCraft = _larder != null && _larder.CanCraft(selectedRecipe.Id);
                _selected = ProjectRecipe(selectedRecipe, Have, canCraft, _displayNameFor);
            }
            else _selected = default;

            _larderText = BuildLarder(
                _larder != null ? _larder.Counts : null,
                _ingredientDefs, _recipes, Have, _displayNameFor, _glyphFor);
        }

        private int Have(string id) => _larder != null ? _larder.Get(id) : 0;

        private RecipeDef FindRecipe(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var r in _recipes)
                if (r != null && r.Id == id) return r;
            return null;
        }

        // -- PURE projections (unit-testable, no service/catalog dependency) ----

        /// <summary>Project one recipe into a Core <see cref="CraftRecipeVM"/> (Workshop shape: no glyphs on
        /// ingredient rows, output-held preview). <paramref name="have"/> supplies live counts;
        /// <paramref name="displayNameFor"/> resolves ingredient names.</summary>
        public static CraftRecipeVM ProjectRecipe(RecipeDef recipe, Func<string, int> have, bool canCraft,
            Func<string, string> displayNameFor)
        {
            if (recipe == null) return default;
            var ingredients = new List<CraftIngredientVM>();
            if (recipe.Ingredients != null)
            {
                foreach (var line in recipe.Ingredients)
                {
                    if (line == null) continue;
                    int h = have != null ? have(line.IngredientId) : 0;
                    bool met = h >= line.Count;
                    string dn = displayNameFor != null ? displayNameFor(line.IngredientId) : line.IngredientId;
                    ingredients.Add(new CraftIngredientVM(line.IngredientId, dn, null, null,
                        h, line.Count, h, met));
                }
            }
            int outputHeld = have != null ? have(recipe.OutputId) : 0;
            return new CraftRecipeVM(recipe.Id,
                string.IsNullOrEmpty(recipe.DisplayName) ? recipe.Id : recipe.DisplayName,
                recipe.Description, recipe.ResultGlyph, ingredients, canCraft,
                alreadyCrafted: false, outputHeld: outputHeld);
        }

        /// <summary>Build the larder readout line: ingredients (catalog order) then recipe outputs then
        /// orphan keys, "&lt;glyph&gt; &lt;name&gt; xN" joined by " · ". PURE (mirrors the old panel exactly).</summary>
        public static string BuildLarder(IReadOnlyDictionary<string, int> counts,
            IReadOnlyList<IngredientDef> ingredients, IReadOnlyList<RecipeDef> recipes,
            Func<string, int> get, Func<string, string> displayNameFor, Func<string, string> glyphFor)
        {
            if (counts == null || counts.Count == 0) return "Larder:  (empty)";

            var seen = new HashSet<string>();
            var ordered = new List<string>();
            if (ingredients != null)
                foreach (var ing in ingredients)
                {
                    if (ing == null || string.IsNullOrEmpty(ing.Id)) continue;
                    if (Amount(get, ing.Id) > 0) { ordered.Add(ing.Id); seen.Add(ing.Id); }
                }
            if (recipes != null)
                foreach (var r in recipes)
                {
                    if (r == null || string.IsNullOrEmpty(r.Id)) continue;
                    if (seen.Contains(r.Id)) continue;
                    if (Amount(get, r.Id) > 0) { ordered.Add(r.Id); seen.Add(r.Id); }
                }
            foreach (var kv in counts)
                if (!seen.Contains(kv.Key)) ordered.Add(kv.Key);

            var sb = new StringBuilder("Larder:  ");
            bool first = true;
            foreach (var id in ordered)
            {
                int n = Amount(get, id);
                if (n <= 0) continue;
                if (!first) sb.Append("  -  ");
                string glyph = glyphFor != null ? glyphFor(id) : null;
                if (!string.IsNullOrEmpty(glyph)) sb.Append(glyph).Append(' ');
                string dn = displayNameFor != null ? displayNameFor(id) : id;
                sb.Append(dn).Append(" x").Append(n);
                first = false;
            }
            return sb.ToString();
        }

        private static int Amount(Func<string, int> get, string id) => get != null ? get(id) : 0;

        /// <summary>Live glyph resolver: ingredient glyph, else recipe result glyph (mirrors the old ResolveGlyph).</summary>
        private static string ResolveGlyph(string id)
        {
            var ing = CraftingRecipeCatalog.FindIngredient(id);
            if (ing != null && !string.IsNullOrEmpty(ing.Glyph)) return ing.Glyph;
            var r = CraftingRecipeCatalog.Find(id);
            if (r != null && !string.IsNullOrEmpty(r.ResultGlyph)) return r.ResultGlyph;
            return null;
        }

        private void Raise() { if (!_disposed) Changed?.Invoke(); }
    }

    /// <summary>Live <see cref="IWorkshopLarder"/> over the <see cref="VillageInventory"/> singleton (null-safe).</summary>
    public sealed class VillageInventoryLarder : IWorkshopLarder, IDisposable
    {
        private readonly VillageInventory _inv;
        private static readonly IReadOnlyDictionary<string, int> Empty = new Dictionary<string, int>();

        public VillageInventoryLarder(VillageInventory inv)
        {
            _inv = inv;
            if (_inv != null) _inv.Changed += RaiseChanged;
        }

        public void Dispose()
        {
            if (_inv != null) _inv.Changed -= RaiseChanged;
            Changed = null;
        }

        private void RaiseChanged() => Changed?.Invoke();

        public int Get(string id) => _inv != null ? _inv.Get(id) : 0;
        public bool CanCraft(string recipeId) => _inv != null && _inv.CanCraft(recipeId);
        public bool TryCraft(string recipeId) => _inv != null && _inv.TryCraft(recipeId);
        public IReadOnlyDictionary<string, int> Counts => _inv != null ? _inv.Counts : Empty;
        public event Action Changed;
    }
}
