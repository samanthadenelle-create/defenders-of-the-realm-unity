// =============================================================================
// ConsumableCraftingCatalog - typed model + WebGL-safe loader for the item-drops
// lane's consumable-recipes.json (combine N drop materials -> 1 consumable).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Items
//
// Mirrors LootTableCatalog / ConsumableCatalog: canonical JSON via
// DeNelle.Core.CanonicalJson (Resources first, StreamingAssets fallback), parsed
// by Newtonsoft.Json. This is a SEPARATE catalog + file from the dungeon-side
// CraftingRecipeCatalog (crafting-recipes.json) so the two never collide - this
// lane owns consumable-recipes.json exclusively.
//
// SHAPE (consumable-recipes.json):
//   {
//     "version": 1,
//     "recipes": [
//       {
//         "id": "craft-minor-heal-potion",
//         "output": "minor-heal-potion",       // a consumables.json id + larder key
//         "displayName": "Brew Minor Healing Draught",
//         "ingredients": [ { "id": "wild-herb", "count": 1 } ]
//       }
//     ]
//   }
//
// GRACEFUL: a missing/empty catalog yields no recipes; the craft service finds
// nothing to craft. The whole feature is gated by ItemDropSystem.Enabled.
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace DeNelle.Village.Items
{
    /// <summary>One ingredient line of a consumable recipe.</summary>
    [Serializable]
    public sealed class ConsumableRecipeIngredient
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("count")] public int Count = 1;
    }

    /// <summary>A recipe: combine the ingredient materials -> one output consumable.</summary>
    [Serializable]
    public sealed class ConsumableRecipeDef
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("output")] public string Output;        // consumable id + larder key
        [JsonProperty("displayName")] public string DisplayName;
        [JsonProperty("ingredients")] public List<ConsumableRecipeIngredient> Ingredients =
            new List<ConsumableRecipeIngredient>();
    }

    [Serializable]
    public sealed class ConsumableRecipeData
    {
        [JsonProperty("version")] public int Version;
        [JsonProperty("recipes")] public List<ConsumableRecipeDef> Recipes =
            new List<ConsumableRecipeDef>();
    }

    /// <summary>Static loader + query surface for the consumable crafting recipes.</summary>
    public static class ConsumableCraftingCatalog
    {
        private const string CanonicalRelativePath = "Data/Canonical/consumable-recipes.json";
        private static ConsumableRecipeData _data;

        public static void Reload() { _data = null; EnsureLoaded(); }

        public static IReadOnlyList<ConsumableRecipeDef> All
        { get { EnsureLoaded(); return _data.Recipes; } }

        public static ConsumableRecipeDef Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureLoaded();
            foreach (var r in _data.Recipes)
                if (r != null && r.Id == id) return r;
            return null;
        }

        private static void EnsureLoaded()
        {
            if (_data != null) return;
            try
            {
                string json = DeNelle.Core.CanonicalJson.Read(CanonicalRelativePath);
                if (!string.IsNullOrEmpty(json))
                {
                    var parsed = JsonConvert.DeserializeObject<ConsumableRecipeData>(json);
                    if (parsed != null)
                    {
                        _data = parsed;
                        if (_data.Recipes == null) _data.Recipes = new List<ConsumableRecipeDef>();
                        return;
                    }
                }
                Debug.LogWarning("[ConsumableCraftingCatalog] consumable-recipes.json not found (Resources or StreamingAssets) - crafting disabled.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[ConsumableCraftingCatalog] Read failed: " + ex.Message);
            }
            _data = new ConsumableRecipeData();
        }
    }
}
