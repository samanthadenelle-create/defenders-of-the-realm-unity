// =============================================================================
// CraftingData — typed records + loader for the canonical crafting-recipes.json.
// -----------------------------------------------------------------------------
// Port spec Part 4 (data-extraction protocol): crafting-recipes.json is a
// canonical data file. This file is the strongly-typed C# mirror — Newtonsoft.Json
// deserialises the JSON into these PLAIN records (no MonoBehaviour, no
// ScriptableObject) so they unit-test cleanly and the loader serves what the
// JSON carries — JSON is the source of truth (port spec Part 4 "JSON wins").
//
// LOADER PATTERN: mirrors DeNelle.Dungeons.DungeonLayoutLoader and
// DeNelle.Dungeons.LoreFragmentsLoader — canonical JSON under
// Application.streamingAssetsPath, read via UniTask (async because Android
// packs StreamingAssets inside the APK), parsed by Newtonsoft.Json. See
// unity-decisions.md 2026-05-18 (Newtonsoft over JsonUtility).
//
// The Healer's Cottage ships ONE recipe — the torch. The crafting pedestal,
// the ingredient pickups and the crafting UI all hydrate from this file:
//   - CraftingPedestal reads the pedestal block + the recipe it fulfils.
//   - IngredientPickup reads one ingredientPlacements[] entry.
//   - CraftingPanelController renders the recipe + the live have/need state.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace DeNelle.Dungeons
{
    /// <summary>
    /// A 3D point in a crafting placement (X east, Y up, Z south) — the same
    /// coordinate convention as <see cref="DungeonPoint"/>. Declared here so the
    /// crafting JSON deserialises without depending on the dungeon-layout shape.
    /// </summary>
    [Serializable]
    public struct CraftPoint
    {
        public float x;
        public float y;
        public float z;

        /// <summary>The point as a world-space <see cref="Vector3"/>.</summary>
        public Vector3 ToWorld() => new Vector3(x, y, z);
    }

    /// <summary>
    /// One collectible crafting ingredient — the deserialised shape of a
    /// <c>crafting-recipes.json</c> <c>ingredients[]</c> entry.
    /// </summary>
    [Serializable]
    public sealed class CraftingIngredient
    {
        /// <summary>Stable id — keys pickups + recipe entries (e.g. <c>dry-reed</c>).</summary>
        [JsonProperty("id")] public string Id;

        /// <summary>Player-facing name shown in the crafting UI. // LOCALIZE</summary>
        [JsonProperty("displayName")] public string DisplayName;

        /// <summary>One-line flavour shown under the name in the crafting UI. // LOCALIZE</summary>
        [JsonProperty("description")] public string Description;

        /// <summary>Single-char UI stand-in until ingredient icon art lands.</summary>
        [JsonProperty("glyph")] public string Glyph;

        /// <summary>RRGGBB hex — the scene builder tints the pickup mote with this.</summary>
        [JsonProperty("tint")] public string Tint;
    }

    /// <summary>One ingredient line of a recipe — an ingredient id + the count needed.</summary>
    [Serializable]
    public sealed class RecipeIngredient
    {
        /// <summary>The ingredient id this line requires (keys <see cref="CraftingIngredient.Id"/>).</summary>
        [JsonProperty("ingredientId")] public string IngredientId;

        /// <summary>How many of the ingredient the recipe consumes.</summary>
        [JsonProperty("count")] public int Count = 1;
    }

    /// <summary>One craftable recipe — the deserialised shape of a <c>recipes[]</c> entry.</summary>
    [Serializable]
    public sealed class CraftingRecipe
    {
        /// <summary>Stable id — keys the pedestal's <c>recipeId</c> (e.g. <c>torch</c>).</summary>
        [JsonProperty("id")] public string Id;

        /// <summary>Player-facing recipe name. // LOCALIZE</summary>
        [JsonProperty("displayName")] public string DisplayName;

        /// <summary>One-line description of the crafted item. // LOCALIZE</summary>
        [JsonProperty("description")] public string Description;

        /// <summary>Single-char UI stand-in for the crafted result.</summary>
        [JsonProperty("resultGlyph")] public string ResultGlyph;

        /// <summary>The ingredients (with counts) the recipe consumes.</summary>
        [JsonProperty("ingredients")] public RecipeIngredient[] Ingredients = Array.Empty<RecipeIngredient>();

        /// <summary>Canon bible-voice toast raised when the item is crafted. // LOCALIZE</summary>
        [JsonProperty("craftedToast")] public string CraftedToast;
    }

    /// <summary>One ingredient pickup placement — a <c>ingredientPlacements[]</c> entry.</summary>
    [Serializable]
    public sealed class IngredientPlacement
    {
        /// <summary>The ingredient this pickup grants (keys <see cref="CraftingIngredient.Id"/>).</summary>
        [JsonProperty("ingredientId")] public string IngredientId;

        /// <summary>Stable, unique id for this pickup — used to dedupe a collected pickup.</summary>
        [JsonProperty("pickupId")] public string PickupId;

        /// <summary>The room the pickup sits in (informational — keys the layout's rooms).</summary>
        [JsonProperty("roomId")] public string RoomId;

        /// <summary>The pickup's world position.</summary>
        [JsonProperty("position")] public CraftPoint Position;
    }

    /// <summary>The crafting-pedestal placement — the <c>pedestal</c> block.</summary>
    [Serializable]
    public sealed class CraftingPedestalDef
    {
        /// <summary>Stable id of the pedestal interactable.</summary>
        [JsonProperty("id")] public string Id;

        /// <summary>The room the pedestal sits in (keys the layout's rooms).</summary>
        [JsonProperty("roomId")] public string RoomId;

        /// <summary>The pedestal's world position.</summary>
        [JsonProperty("position")] public CraftPoint Position;

        /// <summary>Activation radius — within this the Keeper can open the crafting UI.</summary>
        [JsonProperty("radius")] public float Radius = 3f;

        /// <summary>The recipe id the pedestal fulfils (keys <see cref="CraftingRecipe.Id"/>).</summary>
        [JsonProperty("recipeId")] public string RecipeId;
    }

    /// <summary>The deserialised root of <c>crafting-recipes.json</c>.</summary>
    [Serializable]
    public sealed class CraftingDataSet
    {
        /// <summary>Schema version — bumped when the crafting shape changes.</summary>
        [JsonProperty("version")] public int Version;

        /// <summary>Every collectible ingredient in the file.</summary>
        [JsonProperty("ingredients")] public List<CraftingIngredient> Ingredients = new List<CraftingIngredient>();

        /// <summary>Every craftable recipe in the file.</summary>
        [JsonProperty("recipes")] public List<CraftingRecipe> Recipes = new List<CraftingRecipe>();

        /// <summary>Every ingredient pickup placement.</summary>
        [JsonProperty("ingredientPlacements")] public List<IngredientPlacement> IngredientPlacements = new List<IngredientPlacement>();

        /// <summary>The crafting-pedestal placement.</summary>
        [JsonProperty("pedestal")] public CraftingPedestalDef Pedestal;

        /// <summary>Finds an ingredient by id, or null when the set has no such ingredient.</summary>
        public CraftingIngredient FindIngredient(string id)
        {
            if (string.IsNullOrEmpty(id) || Ingredients == null) return null;
            foreach (var ing in Ingredients)
                if (ing != null && ing.Id == id) return ing;
            return null;
        }

        /// <summary>Finds a recipe by id, or null when the set has no such recipe.</summary>
        public CraftingRecipe FindRecipe(string id)
        {
            if (string.IsNullOrEmpty(id) || Recipes == null) return null;
            foreach (var r in Recipes)
                if (r != null && r.Id == id) return r;
            return null;
        }
    }

    /// <summary>
    /// Loads the canonical <c>crafting-recipes.json</c> from
    /// <c>StreamingAssets/Data/Canonical/</c>. Reading from StreamingAssets is
    /// async on Android (the files live inside the compressed APK), so the load
    /// returns a <see cref="UniTask"/> — never <c>async void</c> (port spec
    /// Part 3 mandate). Mirrors <see cref="LoreFragmentsLoader"/>.
    /// </summary>
    public static class CraftingDataLoader
    {
        /// <summary>StreamingAssets-relative path to the canonical crafting data.</summary>
        public const string CraftingDataRelativePath = "Data/Canonical/crafting-recipes.json";

        private static CraftingDataSet _cached;

        /// <summary>The most recently loaded crafting set, or null before the first load.</summary>
        public static CraftingDataSet Cached => _cached;

        /// <summary>
        /// Loads and deserialises the crafting data set, caching the result.
        /// Returns null and logs an error when the file is missing or malformed.
        /// </summary>
        public static async UniTask<CraftingDataSet> LoadAsync()
        {
            if (_cached != null) return _cached;

            string json = await ReadTextAsync(CraftingDataRelativePath);
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError($"[CraftingDataLoader] Could not read '{CraftingDataRelativePath}'.");
                return null;
            }

            try
            {
                var set = JsonConvert.DeserializeObject<CraftingDataSet>(json);
                if (set == null || set.Recipes == null || set.Recipes.Count == 0)
                {
                    Debug.LogError(
                        "[CraftingDataLoader] crafting-recipes.json deserialised to an empty set.");
                    return null;
                }
                _cached = set;
                return set;
            }
            catch (JsonException ex)
            {
                Debug.LogError(
                    $"[CraftingDataLoader] Malformed JSON in crafting-recipes.json: {ex.Message}");
                return null;
            }
        }

        /// <summary>Forces a re-read on the next <see cref="LoadAsync"/> (tests / data sync).</summary>
        public static void Invalidate()
        {
            _cached = null;
        }

        /// <summary>
        /// Reads canonical JSON for a StreamingAssets-relative path. Tries the
        /// WebGL-safe Resources copy FIRST (Resources.Load, synchronous on every
        /// platform incl. WebGL — File.ReadAllText / a StreamingAssets URL is
        /// unreliable in a browser). Falls back to a direct file read on desktop /
        /// editor, then a UnityWebRequest for packed StreamingAssets (Android).
        /// </summary>
        private static async UniTask<string> ReadTextAsync(string relativePath)
        {
            // 1) Resources-first (WebGL-safe). Never throws.
            string fromResources = DeNelle.Core.CanonicalJson.Read(relativePath);
            if (!string.IsNullOrEmpty(fromResources)) return fromResources;

            // 2) StreamingAssets fallback — desktop/editor plain file, else web request.
            string fullPath = Path.Combine(Application.streamingAssetsPath, relativePath);
            bool needsWebRequest = fullPath.Contains("://") || fullPath.Contains(":///");

            if (!needsWebRequest)
            {
                try
                {
                    if (!File.Exists(fullPath)) return null;
                    return await File.ReadAllTextAsync(fullPath);
                }
                catch { return null; }
            }

            using var req = UnityWebRequest.Get(fullPath);
            await req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[CraftingDataLoader] UnityWebRequest failed: {req.error}");
                return null;
            }
            return req.downloadHandler.text;
        }
    }
}
