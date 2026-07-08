// =============================================================================
// GarrisonRecipeCatalog — WebGL-safe loader for garrison-recipes.json.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.World
//
// Reads Data/Canonical/garrison-recipes.json through CanonicalJson (Resources
// dual-copy first => works in WebGL, StreamingAssets fallback on desktop) and
// deserializes with JsonConvert — the SAME pattern QuestCatalog / Theme use.
//
// Used by the generic GarrisonSceneBuilder (Editor) to enumerate scenes to build,
// and available at runtime to the GarrisonController (Village) to look a recipe up
// by id. One load, cached; Reload() re-reads after a JSON edit.
//
// Missing/empty file => LogWarning + an empty list (never an error; the bake just
// builds nothing rather than throwing). Canon: the village is Elarion.
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace DeNelle.Core.World
{
    /// <summary>Loads + caches the garrison/dungeon recipes from canonical JSON.</summary>
    public static class GarrisonRecipeCatalog
    {
        /// <summary>StreamingAssets-relative path (CanonicalJson strips the extension for Resources).</summary>
        public const string StreamingRelativePath = "Data/Canonical/garrison-recipes.json";

        private static List<GarrisonRecipe> _recipes;

        /// <summary>All loaded recipes (empty list if the file is missing/empty).</summary>
        public static IReadOnlyList<GarrisonRecipe> All
        {
            get { EnsureLoaded(); return _recipes; }
        }

        /// <summary>Find a recipe by id (case-insensitive). Null if unknown.</summary>
        public static GarrisonRecipe Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureLoaded();
            for (int i = 0; i < _recipes.Count; i++)
                if (_recipes[i] != null && string.Equals(_recipes[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return _recipes[i];
            return null;
        }

        /// <summary>Force a fresh read (after editing the JSON).</summary>
        public static void Reload() { _recipes = null; EnsureLoaded(); }

        private static void EnsureLoaded()
        {
            if (_recipes != null) return;
            _recipes = new List<GarrisonRecipe>();

            DeNelle.Core.Diagnostics.FlowTrace.Step("Garrison", "EnsureLoaded — reading garrison-recipes.json.");
            try
            {
                string text = CanonicalJson.Read(StreamingRelativePath);
                if (string.IsNullOrEmpty(text))
                {
                    DeNelle.Core.Diagnostics.FlowTrace.Warn("Garrison", $"{StreamingRelativePath} not found/empty (0 recipes — data-empty).");
                    Debug.LogWarning($"[GarrisonRecipeCatalog] {StreamingRelativePath} not found (no recipes loaded).");
                    return;
                }

                var file = JsonConvert.DeserializeObject<GarrisonRecipeFile>(text);
                if (file != null && file.Recipes != null && file.Recipes.Count > 0)
                {
                    int skipped = 0;
                    foreach (var r in file.Recipes)
                        if (r != null && !string.IsNullOrEmpty(r.Id)) _recipes.Add(r);
                        else skipped++;
                    if (skipped > 0)
                        DeNelle.Core.Diagnostics.FlowTrace.Warn("Garrison", $"skipped {skipped} null/id-less recipe row(s).");
                    DeNelle.Core.Diagnostics.FlowTrace.Step("Garrison", $"loaded {_recipes.Count} recipe(s) (from {file.Recipes.Count} row(s)).");
                    Debug.Log($"[GarrisonRecipeCatalog] Loaded {_recipes.Count} garrison/dungeon recipe(s).");
                }
                else
                {
                    DeNelle.Core.Diagnostics.FlowTrace.Warn("Garrison", "garrison-recipes.json parsed EMPTY (json present but 0 recipes — mapping break?).");
                    Debug.LogWarning("[GarrisonRecipeCatalog] garrison-recipes.json parsed empty.");
                }
            }
            catch (Exception ex)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Fail("Garrison", $"read/parse garrison-recipes.json threw {ex.GetType().Name}: {ex.Message}");
                Debug.LogWarning($"[GarrisonRecipeCatalog] Failed to read garrison-recipes.json: {ex.Message}");
            }
        }
    }
}
