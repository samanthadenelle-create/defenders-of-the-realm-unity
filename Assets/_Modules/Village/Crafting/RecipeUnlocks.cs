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
// SCOPE BOUNDARY (WO-850, deliberate): this type only RECORDS unlocks. It gates
// NOTHING - and nothing may be retro-fitted to it without the owner saying so.
//
// ⭐ THE OWNER HAS NOW SAID SO, ONCE, NARROWLY (WO-1235 ruling #2, 2026-08-26).
// The FTUE scroll teaches the Mana Draught brew, so exactly ONE recipe is gated:
// RecipeUnlockKeys.ManaPotionRecipeId. The ruling is verbatim "Introduces the
// mechanic without vomiting the whole crafting catalog onto a new player" and
// "Do NOT unlock the recipe book" - so this is a one-entry allow-list, not a
// policy change.
//
// ⛔ THE WO-850 BOUNDARY OTHERWISE STANDS, INCLUDING ITS EXAMPLE. The dungeon
// crafting pedestal (crafting-recipes.json "pedestal", recipeId "torch") is a
// DIFFERENT catalog and is NOT in the allow-list, so it keeps working exactly as
// it does today. IsUnlocked() is unchanged and still gates nothing on its own;
// the gate is the NEW IsCraftable(), which is fail-open for every ungated id.
//
// ⛔ AND THE LIVE-GAME HALF: a player who already had the mana recipe keeps it.
// SaveMigrator.MigrateToV40 grandfathers every gated id onto any save at v<=39.
// This file must never be the place that decides that - the migrator is, because
// the save version is the only thing that can tell an existing player from a new
// one. See RecipeUnlockKeys (Core) for why the strings live down there.
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

        /// <summary>SeenTutorials key namespace, so an unlock can never collide with a tutorial
        /// key. WO-1235 hoisted the literal to Core (RecipeUnlockKeys) because SaveMigrator has
        /// to write the same key and cannot reference Village - one authority, two readers.</summary>
        private const string KeyPrefix = RecipeUnlockKeys.KeyPrefix;

        /// <summary>The recipe the WO-1235 FTUE scroll teaches. Re-exported here so callers in
        /// this assembly need not reach into Core for it; the VALUE lives in RecipeUnlockKeys.</summary>
        public const string ManaPotionRecipeId = RecipeUnlockKeys.ManaPotionRecipeId;

        /// <summary>
        /// The recipe the deepest-dungeon treasure cache teaches on first clear (owner
        /// ruling 2026-08-02). "torch" is recipes[0] in crafting-recipes.json - an
        /// EXISTING recipe; WO-850 authored no new content.
        /// </summary>
        public const string DungeonCacheRecipeId = "torch";

        /// <summary>The SeenTutorials key that records <paramref name="recipeId"/> as taught.</summary>
        public static string KeyFor(string recipeId) => RecipeUnlockKeys.KeyFor(recipeId);

        /// <summary>
        /// ⭐ THE GATE (WO-1235). True when <paramref name="recipeId"/> may be crafted and shown.
        ///
        /// FAIL-OPEN BY CONSTRUCTION, and that direction is the whole point: a recipe that is
        /// not on the RecipeUnlockKeys.GatedRecipeIds allow-list returns TRUE without consulting
        /// any save state at all. The game is LIVE, so the failure mode of a wrong answer here
        /// is asymmetric - wrongly-craftable is a cosmetic FTUE miss, wrongly-locked silently
        /// removes content from a player who already had it and cannot be undone on their save.
        /// Every ambiguity therefore resolves toward "craftable".
        ///
        /// Existing players are covered a second, independent way: SaveMigrator.MigrateToV40
        /// grandfathers every gated id onto any save at v&lt;=39, so IsUnlocked already answers
        /// true for them. The two defences are deliberately redundant.
        /// </summary>
        public static bool IsCraftable(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId)) return false;
            if (!RecipeUnlockKeys.IsGated(recipeId)) return true;   // ungated = always craftable
            return IsUnlocked(recipeId);
        }

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
