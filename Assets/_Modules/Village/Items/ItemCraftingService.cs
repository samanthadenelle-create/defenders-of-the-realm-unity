// =============================================================================
// ItemCraftingService - "combine N drop materials -> 1 consumable" for the lane.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Items
//
// HARD ISOLATION CONTRACT (mirrors the lane):
//   * NEW type. Touches NO existing file. It composes existing PUBLIC APIs:
//       - DeNelle.Village.Items.ConsumableCraftingCatalog (lane recipes, read)
//       - DeNelle.Village.Crafting.VillageInventory       (Get / TryConsume / Add)
//   * It deliberately does NOT call VillageInventory.TryCraft, because that path
//     keys off the dungeon CraftingRecipeCatalog (crafting-recipes.json). This lane
//     owns its OWN recipe file, so it consumes ingredients + grants the output here
//     - leaving the existing crafting station completely untouched.
//   * SHIPS DARK. Every method no-ops unless ItemDropSystem.Enabled.
//
// GRACEFUL: returns false if disabled, the recipe is unknown, the larder is short,
// or not yet bootstrapped. ASCII strings only. Canon: village is Elarion.
// =============================================================================

using DeNelle.Village.Crafting;
using UnityEngine;

namespace DeNelle.Village.Items
{
    public static class ItemCraftingService
    {
        /// <summary>True when the larder can cover every ingredient of <paramref name="recipeId"/>.
        /// False when the lane is off, the recipe is unknown, or the larder is short.</summary>
        public static bool CanCraft(string recipeId)
        {
            if (!ItemDropSystem.Enabled) return false;
            var recipe = ConsumableCraftingCatalog.Find(recipeId);
            if (recipe == null || recipe.Ingredients == null) return false;

            var inv = VillageInventory.Instance;
            if (inv == null) return false;

            foreach (var line in recipe.Ingredients)
            {
                if (line == null || string.IsNullOrEmpty(line.Id)) continue;
                if (inv.Get(line.Id) < line.Count) return false;
            }
            return true;
        }

        /// <summary>
        /// Craft <paramref name="recipeId"/>: verify every ingredient is covered, then
        /// consume them and grant one of the recipe's output consumable into the larder.
        /// Atomic: consumes nothing unless every ingredient is present. No-op + false on
        /// any failure / when the lane is off.
        /// </summary>
        public static bool TryCraft(string recipeId)
        {
            if (!ItemDropSystem.Enabled) return false;       // SHIPS DARK.

            var recipe = ConsumableCraftingCatalog.Find(recipeId);
            if (recipe == null || recipe.Ingredients == null || string.IsNullOrEmpty(recipe.Output))
                return false;

            var inv = VillageInventory.Instance;
            if (inv == null) return false;

            // Verify BEFORE consuming any (atomic).
            foreach (var line in recipe.Ingredients)
            {
                if (line == null || string.IsNullOrEmpty(line.Id)) continue;
                if (inv.Get(line.Id) < line.Count) return false;
            }

            // Consume ingredients.
            foreach (var line in recipe.Ingredients)
            {
                if (line == null || string.IsNullOrEmpty(line.Id) || line.Count <= 0) continue;
                inv.TryConsume(line.Id, line.Count);
            }

            // Grant the output consumable (keyed by the consumable id).
            inv.Add(recipe.Output, 1);
            return true;
        }
    }
}
