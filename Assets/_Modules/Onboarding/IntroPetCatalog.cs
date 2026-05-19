// =============================================================================
// IntroPetCatalog — a lightweight read of pets.json for the pet-select screen.
// -----------------------------------------------------------------------------
// The pet-select screen (PetSelectController) needs the three starter Wardens'
// id / name / archetype / element / colours to build its cards. The full typed
// pet model + loader is DeNelle.Pets.PetCatalog — but DeNelle.Onboarding
// deliberately references only DeNelle.Core + DeNelle.Data (module isolation,
// port-spec Part 2): an onboarding-flow module must not depend on a gameplay
// module like DeNelle.Pets.
//
// pets.json is CANONICAL DATA, not code, so the clean answer is to read the
// JSON directly here — exactly the StreamingAssets + Newtonsoft pattern
// CanonStrings already uses for en.json. This catalog is intentionally a
// SUBSET: only the few display fields the select cards render. It is NOT a
// second pet model — gameplay still uses DeNelle.Pets.PetCatalog; this just
// lets the intro screen show pet cards without a cross-module reference.
//
// The chosen pet id is written to GameState.StarterPetId — the same id space
// DeNelle.Pets.PetCatalog uses ("pet-aether-sprite" etc.), so the village /
// pet system resolves the player's pick through its own catalog later.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace DeNelle.Onboarding
{
    /// <summary>
    /// The display subset of one pets.json entry the pet-select card renders.
    /// </summary>
    public sealed class IntroPetInfo
    {
        /// <summary>Stable pet id — e.g. <c>pet-aether-sprite</c>. Written to GameState.</summary>
        [JsonProperty("id")] public string Id;
        /// <summary>Canon pet name — Aether Sprite / Flame Pup / Ice Wolf.</summary>
        [JsonProperty("name")] public string Name;
        /// <summary>Element token — aether / flame / ice.</summary>
        [JsonProperty("element")] public string Element;
        /// <summary>Archetype flavour label — e.g. Heart-Ward.</summary>
        [JsonProperty("archetype")] public string Archetype;
        /// <summary>Body tint hex (e.g. <c>#b9a0ff</c>).</summary>
        [JsonProperty("tint")] public string Tint;
        /// <summary>Glow colour hex.</summary>
        [JsonProperty("glowColor")] public string GlowColor;

        /// <summary>The glow colour parsed to a Unity <see cref="Color"/> (white fallback).</summary>
        public Color GlowUnityColor =>
            ColorUtility.TryParseHtmlString(GlowColor ?? "#ffffff", out var c) ? c : Color.white;

        /// <summary>The body tint parsed to a Unity <see cref="Color"/> (white fallback).</summary>
        public Color TintUnityColor =>
            ColorUtility.TryParseHtmlString(Tint ?? "#ffffff", out var c) ? c : Color.white;
    }

    /// <summary>The shape of the pets.json root we deserialise (display subset).</summary>
    [Serializable]
    internal sealed class IntroPetCatalogData
    {
        [JsonProperty("pets")] public List<IntroPetInfo> Pets = new List<IntroPetInfo>();
    }

    /// <summary>
    /// Static read-only access to the three starter pets' display data, parsed
    /// from the canonical <c>pets.json</c> under StreamingAssets. Loaded lazily
    /// on first access. The en.json-style pattern CanonStrings uses.
    /// </summary>
    public static class IntroPetCatalog
    {
        /// <summary>StreamingAssets-relative path to the canonical pets.json.</summary>
        private const string PetsRelativePath = "Data/Canonical/pets.json";

        private static List<IntroPetInfo> _pets;

        /// <summary>
        /// The three starter pets in catalog order (Aether / Flame / Ice).
        /// Empty only if pets.json is missing or malformed (an error is logged).
        /// </summary>
        public static IReadOnlyList<IntroPetInfo> Pets
        {
            get
            {
                EnsureLoaded();
                return _pets;
            }
        }

        private static void EnsureLoaded()
        {
            if (_pets != null) return;
            _pets = Load();
        }

        private static List<IntroPetInfo> Load()
        {
            var full = Path.Combine(Application.streamingAssetsPath, PetsRelativePath);
            try
            {
                if (File.Exists(full))
                {
                    var data = JsonConvert.DeserializeObject<IntroPetCatalogData>(File.ReadAllText(full));
                    if (data != null && data.Pets != null && data.Pets.Count > 0)
                        return data.Pets;
                    Debug.LogError($"[IntroPetCatalog] pets.json at {full} parsed empty.");
                }
                else
                {
                    Debug.LogError($"[IntroPetCatalog] pets.json not found at {full}.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[IntroPetCatalog] Failed to read {full}: {ex.Message}");
            }
            return new List<IntroPetInfo>();
        }
    }
}
