// =============================================================================
// BuildingCatalog — typed model + loader for the canonical buildings.json (Wk 4)
// -----------------------------------------------------------------------------
// Port spec Part 4 data row: data/buildings.json -> BuildingCatalog.cs.
//
// The five gameplay buildings are CONTENT, not code. BuildingCatalog reads
// Assets/StreamingAssets/Data/Canonical/buildings.json at load time and hydrates
// typed BuildingDef records. Mirrors the PackCatalog.cs / Theme.cs pattern
// exactly — canonical JSON under StreamingAssets, read via
// Application.streamingAssetsPath, parsed by Newtonsoft.Json (see
// unity-decisions.md, 2026-05-18; the StreamingAssets-over-Resources call).
//
// PLACEMENT NOTE: every canonical loader in this project lives co-located with
// its consumer module (Theme in Core/Theme, PackCatalog in Wallet, CanonStrings
// in Onboarding) rather than in the empty Assets/Data module. This loader feeds
// Building.cs and BuildMenu.cs — both DeNelle.Village — so it lives here, in
// _Modules/Village/Buildings/. See docs/port-notes/week4-buildings.md.
//
// Building.cs consumes BuildingDef for HP; BuildMenu.cs reads crystalCost +
// the displayName canon-strings key. Building names / costs are never typed
// inline (port spec Part 4 — canon strings flow from JSON).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Hex footprint size of a building. The build menu's tile-validity check
    /// reads this — a <see cref="Large"/> footprint needs more clear neighbour
    /// tiles than a <see cref="Small"/> one. Verbatim port of the
    /// <c>footprint</c> string enum in buildings.json.
    /// </summary>
    public enum BuildingFootprint
    {
        /// <summary>One hex tile (e.g. Crystal Mine).</summary>
        Small = 0,
        /// <summary>A small cluster (e.g. Pet House, Workshop, Farm).</summary>
        Medium = 1,
        /// <summary>The tall Arcane Tower silhouette — needs clear neighbours.</summary>
        Large = 2,
    }

    /// <summary>
    /// One building's canonical definition — the C# port of an entry in
    /// <c>buildings.json</c>. Hydrated by <see cref="BuildingCatalog"/>; never
    /// constructed inline by game code.
    /// </summary>
    [Serializable]
    public sealed class BuildingDef
    {
        /// <summary>Stable id — e.g. <c>crystal-mine</c>. The build / cooldown key.</summary>
        [JsonProperty("id")] public string Id;

        /// <summary>
        /// Which of the five canonical buildings this is. Parsed into the
        /// <see cref="BuildingType"/> enum by <see cref="ResolvedType"/>.
        /// </summary>
        [JsonProperty("type")] public string Type;

        /// <summary>
        /// A KEY into canon-strings.json (e.g. <c>crystalMine</c>) — NOT a
        /// literal. The build menu resolves it so the canon names are never
        /// typed inline (port spec Part 4).
        /// </summary>
        [JsonProperty("displayName")] public string DisplayName;

        /// <summary>A key into en.json (e.g. <c>buildingDesc.crystalMine</c>).</summary>
        [JsonProperty("descriptionKey")] public string DescriptionKey;

        /// <summary>Building HP. Authored — see buildings.json _authoringNotes.</summary>
        [JsonProperty("hp")] public float Hp = 100f;

        /// <summary>Building max HP. Authored — see buildings.json _authoringNotes.</summary>
        [JsonProperty("maxHp")] public float MaxHp = 100f;

        /// <summary>Crystal cost to raise this building from the build menu.</summary>
        [JsonProperty("crystalCost")] public int CrystalCost;

        /// <summary>KayKit Medieval Hexagon Pack mesh name (e.g. <c>tower_A</c>).</summary>
        [JsonProperty("model")] public string Model;

        /// <summary>Hex footprint string — <c>small</c> / <c>medium</c> / <c>large</c>.</summary>
        [JsonProperty("footprint")] public string Footprint = "small";

        /// <summary>Display order in the build menu (0 first).</summary>
        [JsonProperty("buildMenuOrder")] public int BuildMenuOrder;

        /// <summary>WO-413: capability — opens its UPGRADE flow (Upgrade / Add Perks), never a
        /// Buy/Sell shop. Resource/production buildings (mine, farm, lumbermill, forge). Data, not
        /// type/id matching (ARCHITECTURE §2b capability-on-entry).</summary>
        [JsonProperty("isUpgradable")] public bool IsUpgradable;

        /// <summary>WO-413: capability — opens the Buy/Sell shop dialogue. Mutually distinct from
        /// IsUpgradable (a building is one or the other, or neither for special panels).</summary>
        [JsonProperty("isShoppable")] public bool IsShoppable;

        /// <summary>
        /// The <see cref="Type"/> string parsed into the <see cref="BuildingType"/>
        /// enum. Falls back to <see cref="BuildingType.CrystalMine"/> with a
        /// warning for an unknown value.
        /// </summary>
        public BuildingType ResolvedType
        {
            get
            {
                if (Enum.TryParse<BuildingType>(Type, true, out var t)) return t;
                Debug.LogWarning($"[BuildingCatalog] Unknown building type '{Type}' for id '{Id}'.");
                return BuildingType.CrystalMine;
            }
        }

        /// <summary>
        /// The <see cref="Footprint"/> string parsed into the
        /// <see cref="BuildingFootprint"/> enum. Falls back to
        /// <see cref="BuildingFootprint.Small"/> for an unknown value.
        /// </summary>
        public BuildingFootprint ResolvedFootprint
        {
            get
            {
                if (Enum.TryParse<BuildingFootprint>(Footprint, true, out var f)) return f;
                Debug.LogWarning($"[BuildingCatalog] Unknown footprint '{Footprint}' for id '{Id}'.");
                return BuildingFootprint.Small;
            }
        }
    }

    /// <summary>The parsed buildings.json root.</summary>
    [Serializable]
    public sealed class BuildingCatalogData
    {
        /// <summary>Schema version — bumped on a breaking shape change.</summary>
        [JsonProperty("version")] public int Version;

        /// <summary>The five gameplay buildings, in build-menu order.</summary>
        [JsonProperty("buildings")] public List<BuildingDef> Buildings = new List<BuildingDef>();
    }

    /// <summary>
    /// Static surface over the canonical buildings.json — loads + caches the
    /// five <see cref="BuildingDef"/>s, exposes lookups by id / type. The
    /// <c>PackCatalog.cs</c> loading pattern.
    /// </summary>
    public static class BuildingCatalog
    {
        /// <summary>StreamingAssets-relative path to the canonical building data.</summary>
        private const string StreamingRelativePath = "Data/Canonical/buildings.json";

        private static BuildingCatalogData _data;

        /// <summary>All five buildings, ordered by <see cref="BuildingDef.BuildMenuOrder"/>.</summary>
        public static IReadOnlyList<BuildingDef> Buildings
        {
            get { EnsureLoaded(); return _data.Buildings; }
        }

        /// <summary>Looks up a building by its stable id. Returns null when not found.</summary>
        public static BuildingDef Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureLoaded();
            foreach (var def in _data.Buildings)
                if (def != null && def.Id == id) return def;
            return null;
        }

        /// <summary>Looks up a building by its <see cref="BuildingType"/>. Returns null when not found.</summary>
        public static BuildingDef Find(BuildingType type)
        {
            EnsureLoaded();
            foreach (var def in _data.Buildings)
                if (def != null && def.ResolvedType == type) return def;
            return null;
        }

        /// <summary>Forces a re-read of buildings.json (used by tests / the Monday sync).</summary>
        public static void Reload()
        {
            _data = null;
            EnsureLoaded();
        }

        // =====================================================================
        //  Loading
        // =====================================================================

        private static void EnsureLoaded()
        {
            if (_data != null) return;
            _data = LoadCatalog();
            _data.Buildings.Sort((a, b) => a.BuildMenuOrder.CompareTo(b.BuildMenuOrder));
        }

        private static BuildingCatalogData LoadCatalog()
        {
            // WebGL-safe load via CanonicalJson: Resources.Load first (works in a
            // browser, where File.ReadAllText(streamingAssetsPath) THROWS), then a
            // StreamingAssets fallback on desktop/editor. buildings.json is mirrored
            // into Assets/Resources/Data/Canonical/ so the Resources path resolves.
            try
            {
                string json = DeNelle.Core.CanonicalJson.Read(StreamingRelativePath);
                if (!string.IsNullOrEmpty(json))
                {
                    var parsed = JsonConvert.DeserializeObject<BuildingCatalogData>(json);
                    if (parsed != null && parsed.Buildings != null)
                        return parsed;
                    Debug.LogError("[BuildingCatalog] buildings.json parsed empty.");
                }
                else
                {
                    Debug.LogError("[BuildingCatalog] buildings.json not found (Resources or StreamingAssets).");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BuildingCatalog] Failed to read buildings.json: {ex.Message}");
            }

            Debug.LogError("[BuildingCatalog] buildings.json could not be loaded — using an empty catalog.");
            return new BuildingCatalogData { Buildings = new List<BuildingDef>() };
        }
    }
}
