// =============================================================================
// RecipeUnlocks (WO-850) - the persisted "this recipe has been taught" record.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Crafting
//
// WHY THIS EXISTS: before WO-850 the project had NO recipe-unlock mechanism at
// all. Nothing in GameState / SaveSchema carried a known-recipes set, and the
// only recipe gate anywhere was gear-recipes.json's `requiresQuestId`, enforced
// in GearCraftingService. The deepest-dungeon cache (DungeonTreasureCache) needs
// to teach a recipe on FIRST CLEAR, so it needs a place to record that.
//
// NO SAVE-SCHEMA BUMP. The record rides GameState.SeenTutorials - the free-form
// string->bool map that already round-trips through SaveSchema - via
// GameStateService.MarkTutorialSeen, which writes the key AND Save()s in one
// call. This is the established one-shot idiom (TorchWardenDress.GrantTorchOnce).
// Keys are namespaced "recipe_unlocked:<recipeId>" so they can never collide with
// a real tutorial key.
//
// SCOPE BOUNDARY (deliberate, do not "finish" this without an owner ruling):
// this type only RECORDS unlocks. It gates NOTHING. No existing crafting catalog,
// service, or UI consults IsUnlocked, and none should be retro-fitted to without
// the owner saying so - the crafting pedestal (crafting-recipes.json "pedestal",
// recipeId "torch") must keep working exactly as it does today. We are ADDING a
// persisted record, not restricting existing behaviour.
// =============================================================================

using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;

namespace DeNelle.Village.Crafting
{
    /// <summary>
    /// Minimal persisted store of which crafting recipes the player has been taught.
    /// Records only - it gates nothing today (see the file header's scope boundary).
    /// </summary>
    public static class RecipeUnlocks
    {
        private const string Sys = "Crafting";

        /// <summary>SeenTutorials key namespace, so an unlock can never collide with a tutorial key.</summary>
        private const string KeyPrefix = "recipe_unlocked:";

        /// <summary>
        /// The recipe the deepest-dungeon treasure cache teaches on first clear (owner
        /// ruling 2026-08-02). "torch" is recipes[0] in crafting-recipes.json - an
        /// EXISTING recipe; WO-850 authored no new content.
        /// </summary>
        public const string DungeonCacheRecipeId = "torch";

        /// <summary>The SeenTutorials key that records <paramref name="recipeId"/> as taught.</summary>
        public static string KeyFor(string recipeId) => KeyPrefix + (string.IsNullOrEmpty(recipeId) ? "unknown" : recipeId);

        /// <summary>
        /// True when <paramref name="recipeId"/> has been taught and persisted. False when
        /// the id is empty or no GameState is live (fail-open: an unknown state never
        /// claims the player knows something).
        /// </summary>
        public static bool IsUnlocked(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId)) return false;
            var svc = GameStateService.Instance;
            var state = svc != null ? svc.State : null;
            if (state == null || state.SeenTutorials == null) return false;
            return state.SeenTutorials.TryGetValue(KeyFor(recipeId), out bool known) && known;
        }

        /// <summary>
        /// Record <paramref name="recipeId"/> as taught and PERSIST it. Idempotent - a
        /// second call is a no-op (MarkTutorialSeen itself skips an already-set key).
        /// Warns rather than throwing when no GameState is live, so a teach moment in a
        /// stateless scene is visible in the trace instead of silently lost.
        /// </summary>
        public static void Unlock(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId))
            {
                FlowTrace.Warn(Sys, "RecipeUnlocks.Unlock called with an empty recipeId - ignored.");
                return;
            }
            if (IsUnlocked(recipeId))
            {
                FlowTrace.Step(Sys, $"recipe '{recipeId}' was already unlocked - no-op.");
                return;
            }
            var svc = GameStateService.Instance;
            if (svc == null)
            {
                FlowTrace.Warn(Sys,
                    $"no GameStateService live - unlock of recipe '{recipeId}' was NOT persisted.");
                return;
            }
            svc.MarkTutorialSeen(KeyFor(recipeId));   // sets the key AND Save()s
            FlowTrace.Step(Sys, $"recipe '{recipeId}' UNLOCKED and persisted (key '{KeyFor(recipeId)}').");
        }

        /// <summary>
        /// The first-clear teach from a dungeon's deepest treasure cache. Unlocks the
        /// torch recipe; <paramref name="dungeonId"/> is trace context only (the caller
        /// owns the per-dungeon one-shot key, so this stays a plain unlock).
        /// </summary>
        public static void UnlockFromDungeonCache(string dungeonId)
        {
            FlowTrace.Step(Sys,
                $"deepest cache in '{(string.IsNullOrEmpty(dungeonId) ? "unknown" : dungeonId)}' " +
                $"teaches recipe '{DungeonCacheRecipeId}'.");
            Unlock(DungeonCacheRecipeId);
        }
    }
}
