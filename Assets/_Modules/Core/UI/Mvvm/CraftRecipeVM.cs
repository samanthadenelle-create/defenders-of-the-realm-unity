// =============================================================================
// CraftRecipeVM -- the PROMOTED-TO-CORE crafting-recipe projection (MVVM seam).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.UI.Mvvm
//
// One crafting recipe, fully projected into pure, view-agnostic DATA: display
// strings + an ingredient checklist carrying have/need/met + the craftable flag.
// It carries NO UnityEngine UI types (no GameObject/Image/Sprite/RectTransform) so
// the SAME struct drives BOTH crafting Views:
//   * WorkshopCraftVM (DeNelle.Village)  -- the village Workshop station
//   * DungeonCraftVM  (DeNelle.Dungeons) -- the dungeon crafting pedestal
// Each VM fills this from ITS OWN inventory + catalog; neither module references
// the other (module isolation -- Dungeons never references Village). The have/need
// math lives in the VMs, never in a View body (UI_MVVM_MIGRATION_PLAN Silo F).
//
// A readonly struct (no per-repaint allocation on hot craft lists). default(...)
// is the "no recipe selected" state (HasRecipe == false).
// =============================================================================

using System.Collections.Generic;

namespace DeNelle.Core.UI.Mvvm
{
    /// <summary>One ingredient line of a crafting recipe, fully projected: the display name +
    /// optional glyph/tint, plus the live have/need/met state. Pure data (no Unity UI types).</summary>
    public readonly struct CraftIngredientVM
    {
        /// <summary>Stable ingredient id (keys the inventory).</summary>
        public readonly string IngredientId;
        /// <summary>Player-facing ingredient name (already resolved from the catalog).</summary>
        public readonly string DisplayName;
        /// <summary>Short single-char UI glyph stand-in, or null/"" when unused (Workshop leaves this null).</summary>
        public readonly string Glyph;
        /// <summary>RRGGBB hex tint for the glyph plate, or null when unused.</summary>
        public readonly string Tint;
        /// <summary>How many the player currently holds.</summary>
        public readonly int Have;
        /// <summary>How many the recipe needs.</summary>
        public readonly int Need;
        /// <summary>The count to DISPLAY (== Have, except a finished dungeon recipe shows Need so a
        /// consumed larder still reads as satisfied).</summary>
        public readonly int Shown;
        /// <summary>True when the requirement is met (Have >= Need, or the recipe is already crafted).</summary>
        public readonly bool Met;

        public CraftIngredientVM(string ingredientId, string displayName, string glyph, string tint,
                                 int have, int need, int shown, bool met)
        {
            IngredientId = ingredientId;
            DisplayName = displayName;
            Glyph = glyph;
            Tint = tint;
            Have = have;
            Need = need;
            Shown = shown;
            Met = met;
        }
    }

    /// <summary>
    /// A crafting recipe projected into pure display data + a have/need ingredient checklist +
    /// the craftable flag. Shared by the Workshop and Dungeon crafting VMs. <c>default(CraftRecipeVM)</c>
    /// is the "no selection" sentinel (<see cref="HasRecipe"/> == false).
    /// </summary>
    public readonly struct CraftRecipeVM
    {
        /// <summary>True when this projects a real recipe (default(struct) is the no-selection sentinel).</summary>
        public readonly bool HasRecipe;
        /// <summary>Stable recipe id.</summary>
        public readonly string Id;
        /// <summary>Player-facing recipe name.</summary>
        public readonly string DisplayName;
        /// <summary>One-line recipe description (may be null/"").</summary>
        public readonly string Description;
        /// <summary>Short UI glyph for the crafted result, or null/"".</summary>
        public readonly string ResultGlyph;
        /// <summary>The ingredient checklist (never null when <see cref="HasRecipe"/>).</summary>
        public readonly IReadOnlyList<CraftIngredientVM> Ingredients;
        /// <summary>True when the recipe can be crafted right now.</summary>
        public readonly bool CanCraft;
        /// <summary>True when the recipe has already been crafted this run (dungeon one-shot; false for
        /// the repeatable Workshop).</summary>
        public readonly bool AlreadyCrafted;
        /// <summary>How many of the crafted output the player already holds (Workshop output preview; 0
        /// for the dungeon panel).</summary>
        public readonly int OutputHeld;

        public CraftRecipeVM(string id, string displayName, string description, string resultGlyph,
                             IReadOnlyList<CraftIngredientVM> ingredients, bool canCraft,
                             bool alreadyCrafted = false, int outputHeld = 0)
        {
            HasRecipe = true;
            Id = id;
            DisplayName = displayName;
            Description = description;
            ResultGlyph = resultGlyph;
            Ingredients = ingredients ?? System.Array.Empty<CraftIngredientVM>();
            CanCraft = canCraft;
            AlreadyCrafted = alreadyCrafted;
            OutputHeld = outputHeld;
        }
    }
}
