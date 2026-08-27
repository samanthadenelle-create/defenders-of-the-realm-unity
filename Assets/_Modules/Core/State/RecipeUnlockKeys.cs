// =============================================================================
// RecipeUnlockKeys (WO-1235) - the ONE authority for recipe-unlock save keys and
// for WHICH recipes are gated at all.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.State
//
// WHY THIS LIVES IN CORE AND NOT BESIDE THE GATE. Two seats need these strings
// and they are in different assemblies:
//   - DeNelle.Village.Crafting.RecipeUnlocks   READS them (the runtime gate)
//   - DeNelle.Core.State.SaveMigrator          WRITES them (the v40 grandfather)
// Village references Core; Core cannot reference Village. Writing the same key
// literal in both places is exactly the duplicated-state failure CLAUDE.md sec.2
// and sec.5 keep paying for - the copy goes stale and the gate quietly changes
// meaning between the migrator and the reader. So the strings live once, HERE,
// at the lower altitude, and both seats point at them.
//
// ⛔ GATED IS AN ALLOW-LIST, NOT A DEFAULT. Read GatedRecipeIds twice: it names
// the ONLY recipes the unlock gate applies to. Every recipe NOT in this array is
// craftable by everybody, always, exactly as it was before WO-1235. That is not a
// convenience - it is the whole retro-lock defence. The game is LIVE; a gate that
// defaults to "locked" would silently remove crafting from every player who
// already had it, and there is no way to give it back once a save has moved on.
// Owner ruling WO-1235 #2, verbatim: the scroll "unlocks Crafting as a VISIBLE
// SYSTEM" but grants "only the Mana Potion recipe" - "Do NOT unlock the recipe
// book". An allow-list of one is the literal shape of that ruling.
// =============================================================================

using System;

namespace DeNelle.Core.State
{
    /// <summary>Save-key vocabulary for the "this recipe has been taught" record (WO-850),
    /// plus the WO-1235 gated-recipe allow-list. Strings only - no behaviour.</summary>
    public static class RecipeUnlockKeys
    {
        /// <summary>SeenTutorials key namespace, so an unlock can never collide with a
        /// tutorial key. Authored by WO-850; hoisted to Core by WO-1235.</summary>
        public const string KeyPrefix = "recipe_unlocked:";

        /// <summary>The consumable-recipes.json id the WO-1235 scroll teaches: the Mana
        /// Draught brew. This is the ONE recipe the FTUE gate applies to.</summary>
        public const string ManaPotionRecipeId = "craft-survival-mana-potion";

        /// <summary>
        /// The complete set of recipe ids subject to the unlock gate. Anything absent is
        /// ungated and always craftable (see the file header - this is the retro-lock
        /// defence, not a shortcut).
        /// </summary>
        public static readonly string[] GatedRecipeIds = { ManaPotionRecipeId };

        /// <summary>The SeenTutorials key that records <paramref name="recipeId"/> as taught.</summary>
        public static string KeyFor(string recipeId)
            => KeyPrefix + (string.IsNullOrEmpty(recipeId) ? "unknown" : recipeId);

        /// <summary>True when the unlock gate applies to <paramref name="recipeId"/> at all.
        /// Ordinal-ignore-case, matching how recipe ids compare everywhere else.</summary>
        public static bool IsGated(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId)) return false;
            for (int i = 0; i < GatedRecipeIds.Length; i++)
                if (string.Equals(GatedRecipeIds[i], recipeId, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
