// =============================================================================
// BuildCategoryRegistry — the DATA behind the generic build verb (owner 2026-07-10).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// ONE generic build entry (BuildModeController.EnterBuildMode(BuildType)) is
// parameterised by BuildType; each value maps — via DATA, not a hardcoded switch —
// to which CatalogType(s) feed its palette + which catalog ids are unlock-gated.
//   build(Town)      → Resource / Collector   (WO-673, displays "Town")
//   build(Defense)   → Tower / Gate           (displays "Defenses")
//   build(Walls)     → Wall                   (WO-673 split-out)
//   build(Collector) → Collector              (legacy verb, kept for back-compat)
//
// DATA-DRIVEN (mirrors CatalogBootstrap): the mapping is loaded from a canonical
// JSON row-set at startup —
//   Assets/StreamingAssets/Data/Canonical/build-categories.json   (source)
//   Assets/Resources/Data/Canonical/build-categories.json         (WebGL copy, WINS)
// — through DeNelle.Core.CanonicalJson (Resources.Load first, WebGL-safe). A tiny
// generated fallback registers ONLY if the JSON fails to load/parse, so the build
// palette is never empty without maintaining a second table by hand. Same startup
// + StringEnumConverter deserialize as CatalogBootstrap.
//
// DeNelle.Village -> DeNelle.Core only (asmdef rule): CatalogType / BuildType live
// in Core; this Village registrar just reads the JSON into them.
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using DeNelle.Core;
using DeNelle.Core.Catalog;

namespace DeNelle.Village
{
    /// <summary>
    /// The resolved palette recipe for one <see cref="BuildType"/>: which catalog
    /// types feed the palette, the verb's label, and the unlock-gated ids to filter.
    /// </summary>
    /// <summary>
    /// WO-1167 — one authored display group of the build palette: a header label plus the
    /// catalog ROLE strings (structures-catalog 'role', the WO-1161 open vocabulary) whose
    /// cards render under it. DISPLAY ONLY — grouping never re-maps, re-sorts or re-gates a
    /// row. Authored in build-categories.json 'paletteGroups'; the roles named here are DATA,
    /// never a C# list (a role list in code is one fact written twice — WO-1161).
    /// </summary>
    public sealed class PaletteGroup
    {
        public string Label = "";
        /// <summary>Role strings, compared ordinal-ignore-case against CatalogEntry.role.</summary>
        public string[] Roles = Array.Empty<string>();
    }

    public sealed class BuildCategory
    {
        public CatalogType[] Types = Array.Empty<CatalogType>();
        public string Label = "Build";
        public HashSet<string> LockedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// WO-1167 — the verb's ordered display groups, or empty = ungrouped (the palette
        /// renders its flat strip exactly as before). A card whose role names no group falls
        /// into a trailing "Other" bucket at projection time — never dropped — so a brand-new
        /// building with a brand-new role appears with zero code change (owner standing rule).
        /// </summary>
        public PaletteGroup[] PaletteGroups = Array.Empty<PaletteGroup>();

        /// <summary>
        /// WO-1013 -- catalog ids rendered VISIBLE-BUT-LOCKED in this verb's palette
        /// (id -> the lock reason IN WORDS, e.g. "Recover the plans"). A DIFFERENT AXIS
        /// from <see cref="LockedIds"/>: lockedIds HIDES a row entirely; a row here is
        /// shown with its normal cost but cannot be armed until its persisted unlock
        /// flag flips (ProgressionUnlocks, keyed by the catalog id). The reason string
        /// is player-facing copy -- words carry the state, never colour alone.
        /// </summary>
        public Dictionary<string, string> VisibleLockedReasons =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Loads build-categories.json into a <see cref="BuildType"/> → <see cref="BuildCategory"/>
    /// lookup at startup (data-driven). Idempotent across play sessions (rebuild-then-fill),
    /// so it survives domain-reload-off like the other bootstrappers. Falls back to a
    /// generated snapshot of the canonical JSON only if the runtime file cannot be
    /// loaded, so <see cref="Get"/> never returns null and no mapping is authored twice.
    /// </summary>
    public static class BuildCategoryRegistry
    {
        /// <summary>StreamingAssets-relative path of the categories JSON (CanonicalJson resolves Resources first).</summary>
        private const string CategoriesRelativePath = "Data/Canonical/build-categories.json";

        private static Dictionary<BuildType, BuildCategory> _byType;

        // ── JSON DTO (StringEnumConverter parses "Defense" / "Tower" into the Core enums) ──
        [Serializable]
        private sealed class CategoriesFile
        {
            [JsonProperty("version")] public int Version;
            [JsonProperty("categories")] public List<CategoryRow> Categories = new List<CategoryRow>();
        }

        [Serializable]
        private sealed class CategoryRow
        {
            [JsonProperty("buildType")]    public BuildType BuildType;
            [JsonProperty("label")]        public string Label;
            [JsonProperty("catalogTypes")] public List<CatalogType> CatalogTypes = new List<CatalogType>();
            [JsonProperty("lockedIds")]    public List<string> LockedIds = new List<string>();
            /// <summary>WO-1013: id -> lock-reason words, rendered as a visible-locked card.</summary>
            [JsonProperty("visibleLockedIds")] public Dictionary<string, string> VisibleLockedIds;
            /// <summary>WO-1167: ordered display groups (label + role strings). Optional.</summary>
            [JsonProperty("paletteGroups")] public List<PaletteGroupRow> PaletteGroups;
        }

        [Serializable]
        private sealed class PaletteGroupRow
        {
            [JsonProperty("label")] public string Label;
            [JsonProperty("roles")] public List<string> Roles = new List<string>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            _byType = LoadFromJson();
            if (_byType != null && _byType.Count > 0)
            {
                Debug.Log($"[BuildCategoryRegistry] Loaded {_byType.Count} build categorie(s) " +
                          "from build-categories.json — data-driven build verb is live.");
                return;
            }

            _byType = LoadGeneratedFallback();
            Debug.LogWarning($"[BuildCategoryRegistry] build-categories.json unavailable — " +
                             $"using {_byType.Count} generated fallback categorie(s) " +
                             $"(sha256={BuildCategoryFallbackData.SourceSha256}).");
        }

        /// <summary>
        /// Resolve the palette recipe for <paramref name="type"/>. Never null: a missing
        /// row (or a failed load) returns the generated fallback for that type, so the
        /// build palette always has a source.
        /// </summary>
        public static BuildCategory Get(BuildType type)
        {
            var map = _byType;
            if (map == null)
            {
                // Register() runs at BeforeSceneLoad, but guard a pre-init call (edit-mode
                // test / manual invoke) so Get is always safe (no silent null, §12).
                map = _byType = LoadFromJson();
                if (map == null || map.Count == 0) map = _byType = LoadGeneratedFallback();
            }
            if (map.TryGetValue(type, out var cat) && cat != null) return cat;

            // Row absent — consult the generated canonical snapshot for this single type.
            var fb = LoadGeneratedFallback();
            return fb.TryGetValue(type, out var fbc) ? fbc : new BuildCategory();
        }

        /// <summary>
        /// Reads build-categories.json via the WebGL-safe loader, parses each row into a
        /// <see cref="BuildCategory"/>. Returns null on any load/parse failure (caller falls back).
        /// </summary>
        private static Dictionary<BuildType, BuildCategory> LoadFromJson()
        {
            string json;
            try
            {
                json = CanonicalJson.Read(CategoriesRelativePath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BuildCategoryRegistry] read of {CategoriesRelativePath} threw: {ex.Message}");
                return null;
            }

            return ParseJson(json, CategoriesRelativePath);
        }

        private static Dictionary<BuildType, BuildCategory> LoadGeneratedFallback()
        {
            var map = ParseJson(BuildCategoryFallbackData.Json, "generated build-category fallback");
            if (map != null && map.Count > 0) return map;

            Debug.LogError("[BuildCategoryRegistry] generated fallback is invalid; refusing to invent " +
                           "build-category mappings. Regenerate with: " +
                           BuildCategoryFallbackData.RegenerateCommand);
            return new Dictionary<BuildType, BuildCategory>();
        }

        private static Dictionary<BuildType, BuildCategory> ParseJson(string json, string source)
        {
            if (string.IsNullOrEmpty(json)) return null;

            CategoriesFile file;
            try
            {
                var settings = new JsonSerializerSettings
                {
                    Converters = { new StringEnumConverter() },
                    NullValueHandling = NullValueHandling.Ignore,
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                };
                file = JsonConvert.DeserializeObject<CategoriesFile>(json, settings);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BuildCategoryRegistry] parse of {source} failed: {ex.Message}");
                return null;
            }

            if (file == null || file.Categories == null || file.Categories.Count == 0) return null;

            var map = new Dictionary<BuildType, BuildCategory>();
            foreach (var row in file.Categories)
            {
                if (row == null) continue;
                var cat = new BuildCategory
                {
                    Types = row.CatalogTypes != null ? row.CatalogTypes.ToArray() : Array.Empty<CatalogType>(),
                    Label = string.IsNullOrEmpty(row.Label) ? "Build" : row.Label,
                    LockedIds = row.LockedIds != null
                        ? new HashSet<string>(row.LockedIds, StringComparer.OrdinalIgnoreCase)
                        : new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                    VisibleLockedReasons = row.VisibleLockedIds != null
                        ? new Dictionary<string, string>(row.VisibleLockedIds, StringComparer.OrdinalIgnoreCase)
                        : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                    PaletteGroups = ParsePaletteGroups(row.PaletteGroups),
                };
                map[row.BuildType] = cat;
            }
            return map.Count > 0 ? map : null;
        }

        /// <summary>
        /// WO-1167 — parse the authored group rows, dropping only rows that could never
        /// render (no label AND no roles). A labelled row with roles nobody authors yet is
        /// KEPT: it simply matches nothing and renders nothing, which is the correct way for
        /// a group authored ahead of its buildings to behave.
        /// </summary>
        private static PaletteGroup[] ParsePaletteGroups(List<PaletteGroupRow> rows)
        {
            if (rows == null || rows.Count == 0) return Array.Empty<PaletteGroup>();
            var groups = new List<PaletteGroup>(rows.Count);
            foreach (var r in rows)
            {
                if (r == null) continue;
                var roles = new List<string>(r.Roles != null ? r.Roles.Count : 0);
                if (r.Roles != null)
                    foreach (var role in r.Roles)
                        if (!string.IsNullOrEmpty(role)) roles.Add(role);
                if (string.IsNullOrEmpty(r.Label) && roles.Count == 0) continue;
                groups.Add(new PaletteGroup
                {
                    Label = r.Label ?? "",
                    Roles = roles.ToArray(),
                });
            }
            return groups.ToArray();
        }
    }
}
