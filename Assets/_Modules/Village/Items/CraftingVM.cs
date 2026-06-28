// =============================================================================
// CraftingVM — the consumable-crafting panel's PURE ViewModel (MVVM slice).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Items
//
// ALL crafting STATE + LOGIC lives here, view-agnostic. Mirrors HeroSkillTreeVM:
//   * implements DeNelle.Core.UI.Mvvm.IPanelViewModel (Title / Changed / Close / Dispose)
//   * NO UnityEngine UI types (no GameObject/Image/Sprite/RectTransform/Color); the
//     View resolves all presentation. The VM is unit-testable without a scene
//     (ARCHITECTURE_PRINCIPLES §2 / §2c).
//   * the View binds it, re-renders on Changed, and routes user input back as
//     commands; the View NEVER reads game state (ui-mvvm-binding-seam rule).
//
// REUSES the existing lane wholesale (no new systems):
//   * recipes  -> ConsumableCraftingCatalog.All (consumable-recipes.json)
//   * outputs  -> ConsumableCatalog.Find (consumables.json: name + iconPath)
//   * mats     -> MaterialCatalog.Find (materials.json: name + iconPath)
//   * have-cnt -> VillageInventory.Instance.Get(id) (the persisted larder)
//   * craft    -> ItemCraftingService.CanCraft / TryCraft (atomic; gated by
//                 ItemDropSystem.Enabled)
// Subscribes to VillageInventory.Changed so a drop/craft re-renders the cards.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.UI.Mvvm;
using DeNelle.Village.Crafting;

namespace DeNelle.Village.Items
{
    /// <summary>One ingredient line for a recipe card: id + display name + icon + have/need.</summary>
    public readonly struct CraftIngredientVM
    {
        public readonly string Id;
        public readonly string Name;
        public readonly string IconPath;
        public readonly int Have;
        public readonly int Need;
        public bool Met => Have >= Need;

        public CraftIngredientVM(string id, string name, string iconPath, int have, int need)
        {
            Id = id;
            Name = string.IsNullOrEmpty(name) ? (id ?? "") : name;
            IconPath = iconPath ?? "";
            Have = have;
            Need = need;
        }
    }

    /// <summary>One recipe card's view-agnostic payload: output identity + ingredient
    /// checklist + whether it can be crafted right now.</summary>
    public readonly struct CraftRecipeVM
    {
        public readonly string RecipeId;
        public readonly string OutputId;
        public readonly string DisplayName;     // the brew/assemble label
        public readonly string OutputName;      // the consumable's display name
        public readonly string OutputIconPath;
        public readonly IReadOnlyList<CraftIngredientVM> Ingredients;
        public readonly bool CanCraft;

        public CraftRecipeVM(string recipeId, string outputId, string displayName, string outputName,
                             string outputIconPath, IReadOnlyList<CraftIngredientVM> ingredients, bool canCraft)
        {
            RecipeId = recipeId;
            OutputId = outputId;
            DisplayName = displayName ?? "";
            OutputName = outputName ?? "";
            OutputIconPath = outputIconPath ?? "";
            Ingredients = ingredients ?? Array.Empty<CraftIngredientVM>();
            CanCraft = canCraft;
        }
    }

    /// <summary>
    /// Pure ViewModel for the consumable crafting bench. Exposes <see cref="Recipes"/>
    /// (one <see cref="CraftRecipeVM"/> per authored recipe) and the <see cref="Craft"/>
    /// command. Raises <see cref="Changed"/> after each craft and on any larder change.
    /// </summary>
    public sealed class CraftingVM : IPanelViewModel, IDisposable
    {
        private readonly Action _onClose;
        private readonly Action _larderHandler;
        private bool _disposed;

        private readonly List<CraftRecipeVM> _recipes = new List<CraftRecipeVM>();

        public CraftingVM(Action onClose)
        {
            _onClose = onClose;

            var inv = VillageInventory.Instance;
            if (inv != null)
            {
                _larderHandler = Raise;
                inv.Changed += _larderHandler;
            }

            Rebuild();
        }

        // ── IPanelViewModel ───────────────────────────────────────────────────

        public event Action Changed;

        public string Title { get; private set; } = "Alchemy";

        public void Close() => _onClose?.Invoke();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            var inv = VillageInventory.Instance;
            if (inv != null && _larderHandler != null) inv.Changed -= _larderHandler;
            Changed = null;
        }

        // ── Read-only data the View renders ─────────────────────────────────────

        /// <summary>Every craftable recipe (output + ingredient checklist + can-craft). Never null.</summary>
        public IReadOnlyList<CraftRecipeVM> Recipes => _recipes;

        // ── Commands ────────────────────────────────────────────────────────────

        /// <summary>Craft a recipe via the atomic ItemCraftingService.TryCraft (gated by
        /// ItemDropSystem.Enabled). Re-projects on success or failure so the cards reflect
        /// the new larder counts.</summary>
        public void Craft(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId)) return;
            ItemCraftingService.TryCraft(recipeId);
            Rebuild();   // VillageInventory.Changed also fires on success; rebuild is idempotent
            Raise();
        }

        // ── Projection (no Unity types) ──────────────────────────────────────────

        private void Rebuild()
        {
            _recipes.Clear();

            var recipes = ConsumableCraftingCatalog.All;
            if (recipes == null) return;

            var inv = VillageInventory.Instance;

            foreach (var r in recipes)
            {
                if (r == null || string.IsNullOrEmpty(r.Id)) continue;

                var lines = new List<CraftIngredientVM>();
                if (r.Ingredients != null)
                {
                    foreach (var ing in r.Ingredients)
                    {
                        if (ing == null || string.IsNullOrEmpty(ing.Id)) continue;
                        int have = inv != null ? inv.Get(ing.Id) : 0;
                        lines.Add(new CraftIngredientVM(
                            ing.Id,
                            MaterialCatalog.DisplayName(ing.Id),
                            MaterialCatalog.IconPath(ing.Id),
                            have,
                            ing.Count));
                    }
                }

                var outDef = ConsumableCatalog.Find(r.Output);
                string outName = outDef != null && !string.IsNullOrEmpty(outDef.DisplayName)
                    ? outDef.DisplayName : (r.Output ?? "");
                string outIcon = outDef != null ? outDef.IconPath : null;

                _recipes.Add(new CraftRecipeVM(
                    r.Id,
                    r.Output,
                    r.DisplayName,
                    outName,
                    outIcon,
                    lines,
                    ItemCraftingService.CanCraft(r.Id)));
            }
        }

        private void Raise() { if (!_disposed) Changed?.Invoke(); }
    }
}
