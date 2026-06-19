// =============================================================================
// ICraftableCatalog — the Core seam that surfaces CRAFTABLE recipes to the shop.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Catalog
//
// WHY THIS EXISTS (the reconciled "crafting as shoppable" path):
// The shop's unified resolver (ShopCatalog, DeNelle.Village) filters everything a
// vendor offers behind ONE entry point — IsShoppable(gear) AND IsShoppable(craftable).
// Gear lives in DeNelle.Village; crafting lives in DeNelle.Dungeons. DeNelle.Village
// does NOT (and should not) reference DeNelle.Dungeons. So crafting is surfaced through
// THIS thin Core interface instead: the resolver depends only on Core (legal), and the
// crafting module REGISTERS its provider at boot via CraftableCatalogRegistry. The shop
// asks Core "what's craftable?"; Core answers from whatever provider Dungeons registered
// (or yields nothing when crafting is not present — never throws, never blanks the shop).
//
// One vocabulary, two producers — mirrors the VendorStockContract "one contract, two
// consumers" pattern: the shop and the forge both speak ShoppableCraftable, so the shop
// validates the craftable INTENT, not a duplicated copy of the recipe data.
// =============================================================================

using System.Collections.Generic;

namespace DeNelle.Core.Catalog
{
    /// <summary>
    /// One craftable recipe as the shop sees it — the minimal, presentation-free shape the
    /// resolver needs to build a shoppable row. The crafting module maps its richer
    /// CraftingRecipe onto this at the seam, so the shop never references the crafting types.
    /// </summary>
    public readonly struct ShoppableCraftable
    {
        /// <summary>Stable recipe id (keys the forge's recipeId, e.g. "torch").</summary>
        public readonly string Id;
        /// <summary>Player-facing recipe name. // LOCALIZE</summary>
        public readonly string DisplayName;
        /// <summary>One-line description of the crafted item. // LOCALIZE</summary>
        public readonly string Description;
        /// <summary>Single-char UI stand-in for the crafted result.</summary>
        public readonly string ResultGlyph;
        /// <summary>
        /// True when the recipe is actually craftable — it has at least one ingredient line
        /// defined. A recipe with no ingredients is data-incomplete and is NOT offered.
        /// </summary>
        public readonly bool Craftable;

        public ShoppableCraftable(string id, string displayName, string description,
                                  string resultGlyph, bool craftable)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            ResultGlyph = resultGlyph;
            Craftable = craftable;
        }
    }

    /// <summary>
    /// A provider of craftable recipes for the shop. Implemented in the crafting module
    /// (DeNelle.Dungeons) and registered via <see cref="CraftableCatalogRegistry"/> so the
    /// shop — which only references DeNelle.Core — can list craftables without a Dungeons dep.
    /// </summary>
    public interface ICraftableCatalog
    {
        /// <summary>
        /// Every craftable recipe currently known. Never null (return an empty sequence when
        /// the crafting data has not loaded yet — the shop treats that as "no craftables here").
        /// </summary>
        IReadOnlyList<ShoppableCraftable> Craftables();
    }

    /// <summary>
    /// The single registration point for the craftable provider. The crafting module sets
    /// <see cref="Provider"/> at boot; the shop resolver reads <see cref="GetCraftables"/>.
    /// Null-safe both ways: no provider registered => an empty list, never an exception.
    /// </summary>
    public static class CraftableCatalogRegistry
    {
        /// <summary>The registered provider, or null when crafting is not present in this build/scene.</summary>
        public static ICraftableCatalog Provider { get; set; }

        /// <summary>
        /// The craftable recipes from the registered provider, or an empty list when none is
        /// registered (or the provider returns null). Never null; never throws.
        /// </summary>
        public static IReadOnlyList<ShoppableCraftable> GetCraftables()
        {
            var p = Provider;
            if (p == null) return System.Array.Empty<ShoppableCraftable>();
            return p.Craftables() ?? System.Array.Empty<ShoppableCraftable>();
        }
    }
}
