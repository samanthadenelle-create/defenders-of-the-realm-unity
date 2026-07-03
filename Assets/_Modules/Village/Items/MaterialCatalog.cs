// =============================================================================
// MaterialCatalog - typed model + WebGL-safe loader for materials.json (the
// crafting INGREDIENT roster: name + icon for each drop material).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Items
//
// Mirrors ConsumableCatalog / LootTableCatalog EXACTLY: canonical JSON via
// DeNelle.Core.CanonicalJson (Resources first, StreamingAssets fallback), parsed
// by Newtonsoft.Json. Materials are CONTENT - add/retune by editing the JSON.
//
// A material's id is its VillageInventory larder key AND a loot-tables.json drop
// materialId AND a consumable-recipes.json ingredient id - so a dropped
// "ing_moonbloom" is the same key the Mending Salve recipe consumes. This catalog
// only adds the LOOK half (displayName + iconPath) so the crafting UI can render
// an ingredient checklist with real names + icons instead of raw ids.
//
// SHAPE (materials.json):
//   {
//     "version": 1,
//     "materials": [
//       { "id": "ing_moonbloom", "displayName": "Moonbloom Herb",
//         "kind": "material", "category": "herb",
//         "glyph": "*", "iconPath": "ItemIcons/ing_moonbloom" }
//     ]
//   }
//
// GRACEFUL: a missing/empty catalog yields no materials; the UI falls back to the
// raw id + a generic glyph. Never throws (every query null-guards).
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace DeNelle.Village.Items
{
    /// <summary>One crafting-ingredient material definition (the LOOK half: name + icon).</summary>
    [Serializable]
    public sealed class MaterialDef
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("displayName")] public string DisplayName;
        [JsonProperty("kind")] public string Kind;          // "material"
        [JsonProperty("category")] public string Category;  // herb | crystal | fungus | ...
        [JsonProperty("glyph")] public string Glyph;        // ASCII fallback
        [JsonProperty("iconPath")] public string IconPath;  // Resources sprite path (ItemIcons/<id>)
        // WO-598: authored gold BUY price at goods/jeweler vendors (Market materials,
        // Jeweler gems). 0/absent = VendorStockResolver.PriceFor's default.
        [JsonProperty("price")] public int Price;
    }

    [Serializable]
    public sealed class MaterialData
    {
        [JsonProperty("version")] public int Version;
        [JsonProperty("materials")] public List<MaterialDef> Materials = new List<MaterialDef>();
    }

    /// <summary>Static loader + query surface for the crafting-ingredient materials.</summary>
    public static class MaterialCatalog
    {
        private const string CanonicalRelativePath = "Data/Canonical/materials.json";
        private static MaterialData _data;

        public static void Reload() { _data = null; EnsureLoaded(); }

        public static IReadOnlyList<MaterialDef> All { get { EnsureLoaded(); return _data.Materials; } }

        public static MaterialDef Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureLoaded();
            foreach (var m in _data.Materials)
                if (m != null && m.Id == id) return m;
            return null;
        }

        /// <summary>Display name for a material id, or the id itself when unknown.</summary>
        public static string DisplayName(string id)
        {
            var m = Find(id);
            return m != null && !string.IsNullOrEmpty(m.DisplayName) ? m.DisplayName : (id ?? "");
        }

        /// <summary>Resources icon path for a material id, or null when unknown.</summary>
        public static string IconPath(string id)
        {
            var m = Find(id);
            return m != null ? m.IconPath : null;
        }

        private static void EnsureLoaded()
        {
            if (_data != null) return;
            try
            {
                string json = DeNelle.Core.CanonicalJson.Read(CanonicalRelativePath);
                if (!string.IsNullOrEmpty(json))
                {
                    var parsed = JsonConvert.DeserializeObject<MaterialData>(json);
                    if (parsed != null)
                    {
                        _data = parsed;
                        if (_data.Materials == null) _data.Materials = new List<MaterialDef>();
                        return;
                    }
                }
                Debug.LogWarning("[MaterialCatalog] materials.json not found (Resources or StreamingAssets) - ingredient names/icons disabled.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[MaterialCatalog] Read failed: " + ex.Message);
            }
            _data = new MaterialData();
        }
    }
}
