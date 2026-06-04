// =============================================================================
// CraftingRecipeCatalog — loads crafting-recipes.json (StreamingAssets) and
// exposes typed lookup over the village-side crafting recipes.
// -----------------------------------------------------------------------------
// Source: Assets/StreamingAssets/Data/Canonical/crafting-recipes.json. Shape:
//   {
//     ingredients[]: { id, displayName, description, glyph, tint },
//     recipes[]: {
//       id, displayName, description, resultGlyph,
//       ingredients[]: { ingredientId, count },
//       craftedToast
//     }
//   }
// Mirrors PetSkillTreeCatalog line-for-line: static surface, EnsureLoaded()
// reading from Application.streamingAssetsPath, Reload() for dev tools.
//
// Save / inventory state lives in VillageInventory, not here.
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using DeNelle.Core;

namespace DeNelle.Village.Crafting
{
    [Serializable]
    public sealed class IngredientDef
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("displayName")] public string DisplayName;
        [JsonProperty("description")] public string Description;
        [JsonProperty("glyph")] public string Glyph;
        [JsonProperty("tint")] public string Tint;
    }

    [Serializable]
    public sealed class RecipeIngredientLine
    {
        [JsonProperty("ingredientId")] public string IngredientId;
        [JsonProperty("count")] public int Count;
    }

    [Serializable]
    public sealed class RecipeDef
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("displayName")] public string DisplayName;
        [JsonProperty("description")] public string Description;
        [JsonProperty("resultGlyph")] public string ResultGlyph;
        [JsonProperty("ingredients")] public List<RecipeIngredientLine> Ingredients = new List<RecipeIngredientLine>();
        [JsonProperty("craftedToast")] public string CraftedToast;

        /// <summary>
        /// The output of a crafted recipe is keyed by the recipe id itself — the
        /// village inventory stores e.g. "torch" alongside ingredient ids.
        /// </summary>
        public string OutputId => Id;
    }

    [Serializable]
    public sealed class CraftingRecipeData
    {
        [JsonProperty("version")] public int Version;
        [JsonProperty("ingredients")] public List<IngredientDef> Ingredients = new List<IngredientDef>();
        [JsonProperty("recipes")] public List<RecipeDef> Recipes = new List<RecipeDef>();
    }

    public static class CraftingRecipeCatalog
    {
        private const string StreamingRelativePath = "Data/Canonical/crafting-recipes.json";
        private static CraftingRecipeData _data;

        public static IReadOnlyList<RecipeDef> All
        { get { EnsureLoaded(); return _data.Recipes; } }

        public static IReadOnlyList<IngredientDef> Ingredients
        { get { EnsureLoaded(); return _data.Ingredients; } }

        public static RecipeDef Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureLoaded();
            foreach (var r in _data.Recipes)
                if (r != null && r.Id == id) return r;
            return null;
        }

        public static IngredientDef FindIngredient(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureLoaded();
            foreach (var ing in _data.Ingredients)
                if (ing != null && ing.Id == id) return ing;
            return null;
        }

        /// <summary>Returns the display name for an ingredient id, falling back to the id.</summary>
        public static string DisplayNameFor(string ingredientId)
        {
            var def = FindIngredient(ingredientId);
            if (def != null && !string.IsNullOrEmpty(def.DisplayName)) return def.DisplayName;
            // If it's a recipe output rather than an ingredient.
            var recipe = Find(ingredientId);
            if (recipe != null && !string.IsNullOrEmpty(recipe.DisplayName)) return recipe.DisplayName;
            return ingredientId;
        }

        public static void Reload() { _data = null; EnsureLoaded(); }

        private static void EnsureLoaded()
        {
            if (_data != null) return;

            // WebGL-safe read: Resources.Load first (works in browser builds),
            // StreamingAssets File.ReadAllText fallback for desktop/editor.
            // File.ReadAllText THROWS in WebGL, which is why recipes came up
            // empty ("No recipes loaded") in the browser build.
            try
            {
                var json = CanonicalJson.Read(StreamingRelativePath);
                if (!string.IsNullOrEmpty(json))
                {
                    var parsed = JsonConvert.DeserializeObject<CraftingRecipeData>(json);
                    if (parsed != null && parsed.Recipes != null && parsed.Recipes.Count > 0)
                    { _data = parsed; return; }
                    Debug.LogError($"[CraftingRecipeCatalog] {StreamingRelativePath} parsed empty.");
                }
                else Debug.LogError($"[CraftingRecipeCatalog] no Resources copy or StreamingAssets file for {StreamingRelativePath}.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CraftingRecipeCatalog] Read failed: {ex.Message}");
            }
            _data = new CraftingRecipeData();
        }
    }
}
