// =============================================================================
// JewelerVM — the jeweler jewelry-crafting panel's PURE ViewModel (MVVM slice).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Items
//
// Mirrors CraftingVM (the Apothecary lane). ALL state + logic lives here,
// view-agnostic:
//   * implements DeNelle.Core.UI.Mvvm.IPanelViewModel (Title / Changed / Close / Dispose)
//   * NO UnityEngine UI types — the View resolves all presentation. Unit-testable
//     without a scene (ARCHITECTURE_PRINCIPLES §2).
//   * the View binds it, re-renders on Changed, and routes user input back as commands.
//
// REUSES existing systems (no new architecture):
//   * recipes -> JewelerRecipeCatalog.All (jeweler-recipes.json)
//   * output / base -> GearCatalog.FindAccessory (accessories.json: name + iconPath)
//   * gems    -> MaterialCatalog (materials.json crystal ingredients: name + iconPath)
//   * have    -> VillageInventory.Instance.Get(id)
//   * craft   -> JewelerCraftingService.CanCraft / Craft (atomic)
// Subscribes to VillageInventory.Changed so a drop/craft re-renders the cards.
// Reuses CraftIngredientVM (CraftingVM.cs) for the base + gem checklist lines.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.UI.Mvvm;
using DeNelle.Village;            // GearCatalog
using DeNelle.Village.Crafting;   // JewelerRecipeCatalog, JewelerCraftingService, VillageInventory

namespace DeNelle.Village.Items
{
    /// <summary>One jeweler recipe card's view-agnostic payload: output identity + the base/gem
    /// checklist + an optional wallet cost line + whether it can be crafted right now.</summary>
    public readonly struct JewelerRecipeVM
    {
        public readonly string RecipeId;
        public readonly string OutputId;
        public readonly string DisplayName;     // the "Set the …" label
        public readonly string OutputName;      // the upgraded accessory's display name
        public readonly string OutputIconPath;
        public readonly IReadOnlyList<CraftIngredientVM> Ingredients; // base first, then gems
        public readonly string CostLabel;       // "Iron 60, Crystals 10" or "" when free
        public readonly bool CanCraft;

        public JewelerRecipeVM(string recipeId, string outputId, string displayName, string outputName,
                               string outputIconPath, IReadOnlyList<CraftIngredientVM> ingredients,
                               string costLabel, bool canCraft)
        {
            RecipeId = recipeId;
            OutputId = outputId;
            DisplayName = displayName ?? "";
            OutputName = outputName ?? "";
            OutputIconPath = outputIconPath ?? "";
            Ingredients = ingredients ?? Array.Empty<CraftIngredientVM>();
            CostLabel = costLabel ?? "";
            CanCraft = canCraft;
        }
    }

    /// <summary>
    /// Pure ViewModel for the Jeweler's Bench. Exposes <see cref="Recipes"/> (one
    /// <see cref="JewelerRecipeVM"/> per authored recipe) and the <see cref="Craft"/> command.
    /// Raises <see cref="Changed"/> after each craft and on any inventory change.
    /// </summary>
    public sealed class JewelerVM : IPanelViewModel, IDisposable
    {
        private readonly Action _onClose;
        private readonly Action _invHandler;
        private bool _disposed;

        private readonly List<JewelerRecipeVM> _recipes = new List<JewelerRecipeVM>();

        public JewelerVM(Action onClose)
        {
            _onClose = onClose;

            var inv = VillageInventory.Instance;
            if (inv != null)
            {
                _invHandler = Raise;
                inv.Changed += _invHandler;
            }

            Rebuild();
        }

        // ── IPanelViewModel ───────────────────────────────────────────────────

        public event Action Changed;

        public string Title { get; private set; } = "Jeweler's Bench";

        public void Close() => _onClose?.Invoke();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            var inv = VillageInventory.Instance;
            if (inv != null && _invHandler != null) inv.Changed -= _invHandler;
            Changed = null;
        }

        // ── Read-only data the View renders ─────────────────────────────────────

        /// <summary>Every jeweler recipe (output + base/gem checklist + cost + can-craft). Never null.</summary>
        public IReadOnlyList<JewelerRecipeVM> Recipes => _recipes;

        // ── Commands ────────────────────────────────────────────────────────────

        /// <summary>Craft a recipe via the atomic JewelerCraftingService.Craft. Re-projects on
        /// success or failure so the cards reflect the new inventory counts.</summary>
        public void Craft(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId)) return;
            JewelerCraftingService.Craft(recipeId);
            Rebuild();   // VillageInventory.Changed also fires on success; rebuild is idempotent
            Raise();
        }

        // ── Projection (no Unity types) ──────────────────────────────────────────

        private void Rebuild()
        {
            _recipes.Clear();

            var recipes = JewelerRecipeCatalog.All;
            if (recipes == null) return;

            var inv = VillageInventory.Instance;

            foreach (var r in recipes)
            {
                if (r == null || string.IsNullOrEmpty(r.Id)) continue;

                var lines = new List<CraftIngredientVM>();

                // BASE accessory line (icon + name + have/need) — resolved via GearCatalog.
                if (r.Base != null && !string.IsNullOrEmpty(r.Base.Id))
                {
                    var baseDef = GearCatalog.FindAccessory(r.Base.Id);
                    string baseName = baseDef != null && !string.IsNullOrEmpty(baseDef.name) ? baseDef.name : r.Base.Id;
                    string baseIcon = baseDef != null ? baseDef.iconPath : null;
                    int baseHave = inv != null ? inv.Get(r.Base.Id) : 0;
                    lines.Add(new CraftIngredientVM(r.Base.Id, baseName, baseIcon, baseHave, r.Base.Count));
                }

                // GEM lines — resolved via MaterialCatalog (crystal ingredients reused as gems).
                if (r.Gems != null)
                {
                    foreach (var g in r.Gems)
                    {
                        if (g == null || string.IsNullOrEmpty(g.Id)) continue;
                        int have = inv != null ? inv.Get(g.Id) : 0;
                        lines.Add(new CraftIngredientVM(
                            g.Id,
                            MaterialCatalog.DisplayName(g.Id),
                            MaterialCatalog.IconPath(g.Id),
                            have,
                            g.Count));
                    }
                }

                var outDef = GearCatalog.FindAccessory(r.OutputAccessoryId);
                string outName = outDef != null && !string.IsNullOrEmpty(outDef.name)
                    ? outDef.name : (r.OutputAccessoryId ?? "");
                string outIcon = outDef != null ? outDef.iconPath : null;

                _recipes.Add(new JewelerRecipeVM(
                    r.Id,
                    r.OutputAccessoryId,
                    r.DisplayName,
                    outName,
                    outIcon,
                    lines,
                    CostLabel(r.Cost),
                    JewelerCraftingService.CanCraft(r.Id)));
            }
        }

        /// <summary>"Iron 60, Crystals 10" — only the non-zero wallet costs; "" when free.</summary>
        private static string CostLabel(JewelerRecipeCost cost)
        {
            if (cost == null) return "";
            var parts = new List<string>();
            if (cost.Wood > 0)     parts.Add("Wood " + cost.Wood);
            if (cost.Food > 0)     parts.Add("Food " + cost.Food);
            if (cost.Iron > 0)     parts.Add("Iron " + cost.Iron);
            if (cost.Crystals > 0) parts.Add("Crystals " + cost.Crystals);
            return parts.Count == 0 ? "" : string.Join(", ", parts);
        }

        private void Raise() { if (!_disposed) Changed?.Invoke(); }
    }
}
