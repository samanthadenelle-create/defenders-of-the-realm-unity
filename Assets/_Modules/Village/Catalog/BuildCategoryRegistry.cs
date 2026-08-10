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
// hardcoded 2-row fallback registers ONLY if the JSON fails to load/parse, so the
// build palette is never empty. Same [RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]
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
    public sealed class BuildCategory
    {
        public CatalogType[] Types = Array.Empty<CatalogType>();
        public string Label = "Build";
        public HashSet<string> LockedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
    /// hardcoded set mirroring the JSON (WO-673: Town = Resource/Collector, Defense =
    /// Tower/Gate, Walls = Wall, plus the legacy Collector/Support verbs) only if the
    /// JSON cannot be loaded, so <see cref="Get"/> never returns null.
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

            _byType = BuildFallback();
            Debug.LogWarning($"[BuildCategoryRegistry] build-categories.json unavailable — " +
                             $"using {_byType.Count} hardcoded fallback categorie(s).");
        }

        /// <summary>
        /// Resolve the palette recipe for <paramref name="type"/>. Never null: a missing
        /// row (or a failed load) returns the hardcoded fallback for that type, so the
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
                if (map == null || map.Count == 0) map = _byType = BuildFallback();
            }
            if (map.TryGetValue(type, out var cat) && cat != null) return cat;

            // Row absent — fall back to the hardcoded recipe for this single type.
            var fb = BuildFallback();
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
                Debug.LogWarning($"[BuildCategoryRegistry] parse of {CategoriesRelativePath} failed: {ex.Message}");
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
                };
                map[row.BuildType] = cat;
            }
            return map.Count > 0 ? map : null;
        }

        // ── Hardcoded fallback — used ONLY when the JSON cannot be loaded ──────────
        // WO-673 taxonomy (owner ruling 2026-07-11, displays "Town / Defenses / Walls"):
        // Town → Resource+Collector (player-placed functional buildings, always on — WO-682),
        // Defense → Tower/Gate, Walls → Wall (split out — claimed-outpost wall canon).
        // Mirrors build-categories.json v2; keep the two in sync.
        private static Dictionary<BuildType, BuildCategory> BuildFallback()
        {
            return new Dictionary<BuildType, BuildCategory>
            {
                [BuildType.Town] = new BuildCategory
                {
                    Types = new[] { CatalogType.Resource, CatalogType.Collector },
                    Label = "Build Town",
                    // Jeweler stays unlock-gated (moved here from Defense — it is a
                    // Resource row and belongs to the Town verb).
                    // WO-707 (owner taxonomy 2026-07-13, one building per trade): the palette
                    // retires mine_crystal (mining = world nodes), mill (Farm is the food
                    // producer), lumbermill (superseded by collector_lumbermill), armorer
                    // (weapons=Forge / armor=Armorer via ids workshop/forge; the old
                    // "Blacksmith" tile retires) and collector_forge (folded into the Forge
                    // trade tile). Rows stay in the catalog (saves replay); mirrors
                    // build-categories.json v2 — keep the two in sync.
                    LockedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "jeweler",
                        "mine_crystal",
                        "mill",
                        "lumbermill",
                        "armorer",
                        "collector_forge",
                    },
                },
                [BuildType.Defense] = new BuildCategory
                {
                    // WO-673: walls split out to BuildType.Walls; Defense keeps towers/gates.
                    // Rendered set is unchanged vs the pre-673 recipe (every wall id was
                    // already in lockedIds, so none ever rendered under Defense).
                    Types = new[] { CatalogType.Tower, CatalogType.Gate },
                    Label = "Build Defenses",
                    LockedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "tower_siege_tower", "tower_catapult", "gate_stone",
                    },
                    // WO-1013: the Arcane Spire is VISIBLE from minute one but locked in
                    // words until the Castle Defense Plans are recovered (wave-2 drop).
                    // Mirrors build-categories.json 'visibleLockedIds'; keep the two in sync.
                    VisibleLockedReasons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "tower_arcane_spire", "Recover the plans" },
                    },
                },
                [BuildType.Walls] = new BuildCategory
                {
                    Types = new[] { CatalogType.Wall },
                    // WO-1010 D21 / owner D8 resolution 2026-08-09: the Walls category
                    // DISPLAYS as "Castle Structures" (walls + gates-to-come, verticality
                    // later). DISPLAY STRING ONLY — the BuildType.Walls key and the
                    // CatalogType.Wall rows are unchanged. Mirrors build-categories.json v2
                    // (both copies); keep the two in sync.
                    Label = "Castle Structures",
                    // WO-948 (owner ruling 2026-08-10): walls BUILD at level 1 ONLY — like
                    // CoC, higher tiers exist only by UPGRADING the placed piece. wall_stone
                    // is therefore palette-locked (its catalog row survives: existing saves
                    // replay/sell placed stone walls via BaseLayoutLoader → CatalogRegistry,
                    // which never consults lockedIds). Mirrors build-categories.json.
                    LockedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "wall_stone",
                    },
                },
                [BuildType.Collector] = new BuildCategory
                {
                    Types = new[] { CatalogType.Collector },
                    Label = "Build Collectors",
                    LockedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                },
                // Support — the Healing Caravan (out-of-battle Heart heal). The
                // fountain is unlock-gated behind the arcane-tower 'arcane-wellspring'
                // research perk; the palette layer keeps it locked until that perk is owned.
                [BuildType.Support] = new BuildCategory
                {
                    Types = new[] { CatalogType.Support },
                    Label = "Build Support",
                    LockedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "healing_caravan",
                    },
                },
            };
        }
    }
}
