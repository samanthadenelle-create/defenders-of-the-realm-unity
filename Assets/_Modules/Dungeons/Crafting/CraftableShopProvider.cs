// =============================================================================
// CraftableShopProvider — feeds CRAFTABLE recipes to the shop via the Core seam.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Dungeons   Namespace: DeNelle.Dungeons
//
// THE crafting side of "crafting-as-shoppable". The shop's unified resolver
// (DeNelle.Village.Hero.ShopCatalog) lists craftables through the Core interface
// DeNelle.Core.Catalog.ICraftableCatalog — it cannot reference DeNelle.Dungeons
// (Village must not depend on Dungeons; that would couple/cycle the assemblies).
//
// So crafting REGISTERS itself here: this provider maps the loaded CraftingDataSet's
// recipes onto the seam's ShoppableCraftable shape, and a RuntimeInitializeOnLoad hook
// installs it into CraftableCatalogRegistry at boot. The shop then asks Core "what's
// craftable?" and Core answers from this provider — one vocabulary, two producers.
//
// Craftability = the recipe has at least one ingredient line defined. A data-incomplete
// recipe (no ingredients) is reported as NOT craftable, so the resolver skips it.
//
// Null/empty-safe: before CraftingDataLoader has loaded, Cached is null and Craftables()
// returns an empty list — the shop treats that as "no craftables here", never throws.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Catalog;

namespace DeNelle.Dungeons
{
    /// <summary>
    /// Maps the loaded crafting recipes onto the Core <see cref="ICraftableCatalog"/> seam so the
    /// shop can list them without a Dungeons dependency. Reads the live <see cref="CraftingDataLoader.Cached"/>
    /// set each call, so newly-loaded crafting data is reflected without re-registering.
    /// </summary>
    public sealed class CraftableShopProvider : ICraftableCatalog
    {
        /// <summary>
        /// The current craftable recipes as the shop sees them. Never null. A recipe with at least
        /// one ingredient line is marked craftable; one without is surfaced as NOT craftable so the
        /// resolver (which only offers craftable ones) skips it.
        /// </summary>
        public IReadOnlyList<ShoppableCraftable> Craftables()
        {
            var set = CraftingDataLoader.Cached;
            if (set == null || set.Recipes == null || set.Recipes.Count == 0)
                return System.Array.Empty<ShoppableCraftable>();

            var list = new List<ShoppableCraftable>(set.Recipes.Count);
            foreach (var r in set.Recipes)
            {
                if (r == null || string.IsNullOrEmpty(r.Id)) continue;
                bool craftable = r.Ingredients != null && r.Ingredients.Length > 0;
                list.Add(new ShoppableCraftable(
                    r.Id, r.DisplayName, r.Description, r.ResultGlyph, craftable));
            }
            return list;
        }

        /// <summary>
        /// Installs the provider into the Core registry at boot, so the shop can list craftables the
        /// moment crafting data loads. Runs before the first scene loads; idempotent (overwrites the
        /// single registry slot). RuntimeInitializeOnLoad fires in player + play-mode (not EditMode
        /// tests — those register a fake directly, see ShopCatalogShoppableTests).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            CraftableCatalogRegistry.Provider = new CraftableShopProvider();
        }
    }
}
