// =============================================================================
// CosmeticCatalog - typed model + loader for cosmetics.json (StreamingAssets).
// -----------------------------------------------------------------------------
// Ports docs/cosmetic-shop-spec.md (React repo, Section 7 catalog data
// structure) into the Unity port. Twelve seed items: four hero skins, four
// pet skins, four village skins. Each cosmetic is purely visual; equipping
// one never alters a combat stat (spec Section 1 - "Zero pay-to-win, ever.").
//
// Mirrors PetSkillTreeCatalog.cs: read once from
// Application.streamingAssetsPath, cache, expose typed lookups. Reload() exists
// so dev tools can swap in updated JSON without restarting the editor.
// The GlimmerCurrencyService consumes this catalog through CosmeticCatalog.Find.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace DeNelle.Cosmetics
{
    /// <summary>
    /// One row of cosmetics.json - the typed shape of a single shop item.
    /// Fields stay flat (no nested apply{}) for this skeleton; visual effects
    /// land in the application layer once renderers know how to read the id.
    /// </summary>
    [Serializable]
    public sealed class CosmeticDef
    {
        /// <summary>Stable id - e.g. <c>hero-mage-embergrove</c>.</summary>
        [JsonProperty("id")] public string Id;

        /// <summary>One of <c>hero</c> / <c>pet</c> / <c>village</c>.</summary>
        [JsonProperty("category")] public string Category;

        /// <summary>What this cosmetic targets - mage / aether-sprite / wall-tier-2 / etc.</summary>
        [JsonProperty("appliesTo")] public string AppliesTo;

        /// <summary>Shop card title.</summary>
        [JsonProperty("displayName")] public string DisplayName;

        /// <summary>Short flavour line shown under the title.</summary>
        [JsonProperty("description")] public string Description;

        /// <summary>Glimmer price; 0 for achievement-gated items.</summary>
        [JsonProperty("glimmerCost")] public int GlimmerCost;

        /// <summary>How this cosmetic is unlocked: <c>buy</c> or <c>achievement</c>.</summary>
        [JsonProperty("unlockMethod")] public string UnlockMethod;

        /// <summary>Hex colour used as a swatch placeholder before real previews land.</summary>
        [JsonProperty("previewColor")] public string PreviewColor;

        /// <summary>True for items the player must earn through gameplay.</summary>
        public bool IsAchievement =>
            string.Equals(UnlockMethod, "achievement", StringComparison.OrdinalIgnoreCase);

        /// <summary>True for items the player can buy with Glimmer.</summary>
        public bool IsBuyable =>
            string.Equals(UnlockMethod, "buy", StringComparison.OrdinalIgnoreCase);

        /// <summary>Preview hex parsed to a Unity Color; falls back to grey on a parse miss.</summary>
        public Color PreviewUnityColor =>
            ColorUtility.TryParseHtmlString(PreviewColor ?? "#888888", out var c)
                ? c
                : new Color(0.55f, 0.55f, 0.55f, 1f);
    }

    /// <summary>Parsed cosmetics.json root.</summary>
    [Serializable]
    public sealed class CosmeticCatalogData
    {
        [JsonProperty("version")] public int Version;
        [JsonProperty("items")] public List<CosmeticDef> Items = new List<CosmeticDef>();
    }

    /// <summary>
    /// Static surface over cosmetics.json. Read-only; the wallet + ownership
    /// state lives in GlimmerCurrencyService.
    /// </summary>
    public static class CosmeticCatalog
    {
        private const string StreamingRelativePath = "Data/Canonical/cosmetics.json";

        private static CosmeticCatalogData _data;

        /// <summary>All cosmetic defs, in catalog order.</summary>
        public static IReadOnlyList<CosmeticDef> All
        {
            get { EnsureLoaded(); return _data.Items; }
        }

        /// <summary>Looks up a cosmetic by stable id. Returns null when not found.</summary>
        public static CosmeticDef Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureLoaded();
            foreach (var c in _data.Items)
                if (c != null && c.Id == id) return c;
            return null;
        }

        /// <summary>
        /// All cosmetics whose <c>category</c> equals <paramref name="category"/>
        /// (case-insensitive). Returns an empty sequence on a miss.
        /// </summary>
        public static IEnumerable<CosmeticDef> ByCategory(string category)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(category)) yield break;
            foreach (var c in _data.Items)
            {
                if (c == null) continue;
                if (string.Equals(c.Category, category, StringComparison.OrdinalIgnoreCase))
                    yield return c;
            }
        }

        /// <summary>Forces a re-read of cosmetics.json (used by dev tools).</summary>
        public static void Reload()
        {
            _data = null;
            EnsureLoaded();
        }

        private static void EnsureLoaded()
        {
            if (_data != null) return;
            // DEF-212: the old path used File.ReadAllText(StreamingAssets), which
            // THROWS in WebGL (no filesystem) → the shop loaded with ZERO items and
            // showed "empty/broken". Route through DeNelle.Core.CanonicalJson
            // (Resources.Load<TextAsset> first — WebGL-safe — then StreamingAssets on
            // desktop). Dual copy lives at Assets/Resources/Data/Canonical/cosmetics.json;
            // keep it in sync with the StreamingAssets source.
            try
            {
                var json = DeNelle.Core.CanonicalJson.Read(StreamingRelativePath);
                if (!string.IsNullOrEmpty(json))
                {
                    var parsed = JsonConvert.DeserializeObject<CosmeticCatalogData>(json);
                    if (parsed != null && parsed.Items != null && parsed.Items.Count > 0)
                    { _data = parsed; return; }
                    Debug.LogError($"[CosmeticCatalog] {StreamingRelativePath} parsed empty.");
                }
                else
                {
                    Debug.LogError($"[CosmeticCatalog] {StreamingRelativePath} not found (Resources or StreamingAssets).");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CosmeticCatalog] Failed to read {StreamingRelativePath}: {ex.Message}");
            }
            _data = new CosmeticCatalogData { Items = new List<CosmeticDef>() };
        }
    }
}
