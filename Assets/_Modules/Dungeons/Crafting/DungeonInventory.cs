// =============================================================================
// DungeonInventory — runtime-only ScriptableObject holding the Keeper's
// crafting ingredients for the active dungeon run.
// -----------------------------------------------------------------------------
// A minimal inventory for the v2 dungeon crafting system (Workstream C). It
// holds ONLY crafting ingredients — counts keyed by ingredient id — plus the
// set of pickup ids already collected (so a re-walked pickup is not double
// counted) and the set of recipes already crafted this run.
//
// Pattern parallel: mirrors DungeonRuntimeState — a non-persisted
// ScriptableObject that carries the live run surface and raises UnityEvents on
// mutation (port spec Part 3 "ScriptableObject + UnityEvent for state"). UI /
// scene MonoBehaviours subscribe in OnEnable, unsubscribe in OnDisable.
//
// A dungeon run is meant to be finished in one sitting — NOTHING here is
// serialised to PlayerPrefs. The SO carries [CreateAssetMenu] only so the
// dungeon scene builder can drop a shared instance into the crafting
// interactables in the inspector. It is pinned in memory with
// HideFlags.DontUnloadUnusedAsset so the inventory rides the ATB battle-scene
// round-trip with the run; a FRESH run is emptied explicitly by
// DungeonController.EnterDungeon, and a real dungeon exit calls Clear().
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace DeNelle.Dungeons
{
    /// <summary>
    /// Runtime-only ScriptableObject — the Keeper's crafting-ingredient inventory
    /// for the active dungeon run. Holds ingredient counts, the collected-pickup
    /// set and the crafted-recipe set, and raises <see cref="InventoryChanged"/>
    /// on every mutation so the crafting UI can refresh.
    /// </summary>
    [CreateAssetMenu(
        fileName = "DungeonInventory",
        menuName = "DeNelle/Dungeons/Dungeon Inventory")]
    public sealed class DungeonInventory : ScriptableObject
    {
        // ── Live inventory surface (runtime — not serialised to disk) ────────

        [Tooltip("Collected ingredient ids, parallel to _ingredientCounts.")]
        [SerializeField] private List<string> _ingredientIds = new List<string>();

        [Tooltip("Counts held, parallel to _ingredientIds.")]
        [SerializeField] private List<int> _ingredientCounts = new List<int>();

        [Tooltip("Pickup ids already collected this run — dedupes a re-walked pickup.")]
        [SerializeField] private List<string> _collectedPickupIds = new List<string>();

        [Tooltip("Recipe ids already crafted this run.")]
        [SerializeField] private List<string> _craftedRecipeIds = new List<string>();

        // ── Mutation events ──────────────────────────────────────────────────

        /// <summary>Raised whenever the inventory changes (a pickup or a craft).</summary>
        [Header("Events")]
        public UnityEvent InventoryChanged = new UnityEvent();

        /// <summary>
        /// Raised with the ingredient id whenever an ingredient is collected —
        /// lets a toast / pickup-feedback layer react to a specific grab.
        /// </summary>
        public UnityEvent<string> IngredientCollected = new UnityEvent<string>();

        /// <summary>Raised with the recipe id the first time a recipe is crafted.</summary>
        public UnityEvent<string> RecipeCrafted = new UnityEvent<string>();

        // ── Read-only accessors ──────────────────────────────────────────────

        /// <summary>How many of <paramref name="ingredientId"/> the Keeper holds.</summary>
        public int CountOf(string ingredientId)
        {
            if (string.IsNullOrEmpty(ingredientId)) return 0;
            int i = _ingredientIds.IndexOf(ingredientId);
            return i < 0 ? 0 : _ingredientCounts[i];
        }

        /// <summary>True when the pickup with this id has already been collected.</summary>
        public bool HasCollectedPickup(string pickupId) =>
            !string.IsNullOrEmpty(pickupId) && _collectedPickupIds.Contains(pickupId);

        /// <summary>True when the recipe with this id has already been crafted this run.</summary>
        public bool HasCrafted(string recipeId) =>
            !string.IsNullOrEmpty(recipeId) && _craftedRecipeIds.Contains(recipeId);

        /// <summary>Ingredient ids currently held (at least one).</summary>
        public IReadOnlyList<string> HeldIngredientIds => _ingredientIds;

        // ── Mutation ─────────────────────────────────────────────────────────

        /// <summary>
        /// Grants <paramref name="amount"/> of an ingredient from the pickup with
        /// <paramref name="pickupId"/>. No-op (returns false) when that pickup
        /// has already been collected — a pickup grants its ingredient once.
        /// </summary>
        public bool CollectPickup(string pickupId, string ingredientId, int amount = 1)
        {
            if (string.IsNullOrEmpty(pickupId) || string.IsNullOrEmpty(ingredientId))
                return false;
            if (_collectedPickupIds.Contains(pickupId)) return false;

            _collectedPickupIds.Add(pickupId);
            AddIngredient(ingredientId, Mathf.Max(1, amount));
            IngredientCollected.Invoke(ingredientId);
            InventoryChanged.Invoke();
            return true;
        }

        /// <summary>Adds an ingredient count directly (no pickup dedupe).</summary>
        public void AddIngredient(string ingredientId, int amount)
        {
            if (string.IsNullOrEmpty(ingredientId) || amount <= 0) return;
            int i = _ingredientIds.IndexOf(ingredientId);
            if (i < 0)
            {
                _ingredientIds.Add(ingredientId);
                _ingredientCounts.Add(amount);
            }
            else
            {
                _ingredientCounts[i] += amount;
            }
        }

        /// <summary>
        /// True when the inventory holds at least every ingredient count the
        /// recipe lists — i.e. the recipe can be crafted right now.
        /// </summary>
        public bool CanCraft(CraftingRecipe recipe)
        {
            if (recipe == null || recipe.Ingredients == null) return false;
            foreach (var line in recipe.Ingredients)
            {
                if (line == null) continue;
                if (CountOf(line.IngredientId) < line.Count) return false;
            }
            return true;
        }

        /// <summary>
        /// Crafts <paramref name="recipe"/>: checks the ingredients are present,
        /// consumes them, records the recipe as crafted, and raises
        /// <see cref="RecipeCrafted"/> + <see cref="InventoryChanged"/>. Returns
        /// false when the recipe cannot be afforded or was already crafted.
        /// </summary>
        public bool Craft(CraftingRecipe recipe)
        {
            if (recipe == null || string.IsNullOrEmpty(recipe.Id)) return false;
            if (_craftedRecipeIds.Contains(recipe.Id)) return false;
            if (!CanCraft(recipe)) return false;

            // Consume the ingredients.
            foreach (var line in recipe.Ingredients)
            {
                if (line == null) continue;
                int i = _ingredientIds.IndexOf(line.IngredientId);
                if (i >= 0)
                {
                    _ingredientCounts[i] = Mathf.Max(0, _ingredientCounts[i] - line.Count);
                    if (_ingredientCounts[i] == 0)
                    {
                        _ingredientCounts.RemoveAt(i);
                        _ingredientIds.RemoveAt(i);
                    }
                }
            }

            _craftedRecipeIds.Add(recipe.Id);
            RecipeCrafted.Invoke(recipe.Id);
            InventoryChanged.Invoke();
            return true;
        }

        /// <summary>Clears the whole inventory — called on run teardown.</summary>
        public void Clear()
        {
            _ingredientIds.Clear();
            _ingredientCounts.Clear();
            _collectedPickupIds.Clear();
            _craftedRecipeIds.Clear();
            InventoryChanged.Invoke();
        }

        // The larder must ride the ATB battle-scene round-trip with the run — a
        // Keeper who hits an encounter mid-gather keeps their ingredients.
        // DontUnloadUnusedAsset keeps this SO instance alive across the
        // dungeon -> ATBBattle -> dungeon scene loads; without it the asset is
        // unloaded between scenes and reloads empty from disk. A FRESH run is
        // emptied explicitly by DungeonController.EnterDungeon (and a real
        // dungeon exit calls Clear()), so OnEnable must NOT wipe the larder.
        private void OnEnable()
        {
            hideFlags = HideFlags.DontUnloadUnusedAsset;
        }
    }
}
