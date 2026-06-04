// =============================================================================
// VillageStrings — read-only canon-string resolver for the Village module (Wk4)
// -----------------------------------------------------------------------------
// The Village UI must NEVER hardcode a canon string (port spec Part 4: "the
// Unity agent never types these inline"). The build menu needs the five
// building display names ("Crystal Mine" ...) and their descriptions, all of
// which already live in the canonical JSON:
//
//   canon-strings.json — proper nouns: crystalMine / petHouse / arcaneTower /
//                        workshop / farm  (the BuildingDef.displayName keys).
//   en.json            — localizable copy: buildingDesc.* description strings.
//
// CanonStrings.cs (DeNelle.Onboarding) does the identical job for the title
// scene, but the Village asmdef does NOT reference Onboarding and must not
// grow a cross-module dependency just for string lookup (asmdef isolation,
// port spec Part 2). So this is a Village-local twin of that loader — same
// StreamingAssets read, same flat-map parse, same [[missing:key]] marker. The
// Localization package owns these strings long-term (port spec Part 3); this
// is the Week-4 bridge until the string table is wired.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Static read-only access to the canonical strings the Village module
    /// needs — <c>canon-strings.json</c> (proper nouns) and <c>en.json</c>
    /// (localizable copy). Loaded lazily from StreamingAssets on first access.
    /// </summary>
    public static class VillageStrings
    {
        /// <summary>StreamingAssets-relative path to the canon proper-noun file.</summary>
        private const string CanonRelativePath = "Data/Canonical/canon-strings.json";

        /// <summary>StreamingAssets-relative path to the English localizable strings.</summary>
        private const string LocaleRelativePath = "Data/Canonical/en.json";

        private static Dictionary<string, string> _canon;
        private static Dictionary<string, string> _locale;

        /// <summary>
        /// Resolves a key from <c>canon-strings.json</c> (the proper nouns —
        /// e.g. <c>crystalMine</c> -> "Crystal Mine"). Returns a visible
        /// <c>[[missing:key]]</c> marker for an unknown key.
        /// </summary>
        public static string Canon(string key)
        {
            EnsureLoaded();
            return Resolve(_canon, key);
        }

        /// <summary>
        /// Resolves a key from <c>en.json</c> (the localizable copy — e.g.
        /// <c>buildingDesc.crystalMine</c>). Returns a visible
        /// <c>[[missing:key]]</c> marker for an unknown key.
        /// </summary>
        public static string Locale(string key)
        {
            EnsureLoaded();
            return Resolve(_locale, key);
        }

        /// <summary>
        /// Resolves a building's display name from its <see cref="BuildingDef"/>.
        /// <see cref="BuildingDef.DisplayName"/> is a canon-strings KEY, not a
        /// literal — this is the single place the Village UI turns it into the
        /// player-facing string.
        /// </summary>
        public static string BuildingName(BuildingDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.DisplayName)) return "[[missing:building]]";
            return Canon(def.DisplayName);
        }

        /// <summary>Resolves a building's flavour description from its <see cref="BuildingDef"/>.</summary>
        public static string BuildingDescription(BuildingDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.DescriptionKey)) return string.Empty;
            return Locale(def.DescriptionKey);
        }

        // =====================================================================
        //  Loading  (mirrors CanonStrings.LoadMap — flat string->string maps)
        // =====================================================================

        private static void EnsureLoaded()
        {
            if (_canon == null) _canon = LoadMap(CanonRelativePath);
            if (_locale == null) _locale = LoadMap(LocaleRelativePath);
        }

        private static Dictionary<string, string> LoadMap(string relativePath)
        {
            // WebGL-safe load via CanonicalJson (Resources first, StreamingAssets fallback).
            try
            {
                string json = DeNelle.Core.CanonicalJson.Read(relativePath);
                if (!string.IsNullOrEmpty(json))
                {
                    // Both canonical files are flat maps with some leading "_"
                    // metadata keys (e.g. "_comment") and a couple of nested
                    // "_sources" objects — deserialize loosely, keep only the
                    // string-valued entries (the CanonStrings.cs convention).
                    var raw = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                    var map = new Dictionary<string, string>();
                    if (raw != null)
                    {
                        foreach (var kv in raw)
                            if (kv.Value is string s) map[kv.Key] = s;
                    }
                    return map;
                }
                Debug.LogError($"[VillageStrings] Canonical file not found (Resources or StreamingAssets): {relativePath}.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VillageStrings] Failed to read {relativePath}: {ex.Message}");
            }
            return new Dictionary<string, string>();
        }

        private static string Resolve(Dictionary<string, string> map, string key)
        {
            if (map != null && key != null && map.TryGetValue(key, out var value) && value != null)
                return value;
            Debug.LogWarning($"[VillageStrings] Missing canonical key '{key}'.");
            return $"[[missing:{key}]]";
        }
    }
}
