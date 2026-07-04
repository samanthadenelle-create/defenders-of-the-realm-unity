// =============================================================================
// SpawnAreaTable (WO-606) — geotagged spawn AREAS as queryable data.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.World
//
// The data-driven successor to the hardcoded RegionSpawnTable roster. Instead of
// deriving the roster from origin-relative geometry (ZoneManager.GetZone) + a
// compiled-in dictionary, a designer authors CIRCLE FOOTPRINTS in world space:
//   spawn-areas.json → [ { id, center{x,z}, radius, families[] (weighted),
//                          levelRange{min,max}, composition{tank,dps,healer},
//                          arenaPreset, seedBudget } ]
//
// A world position resolves to the CONTAINING area (nearest center on overlap);
// outside every authored area it resolves to NULL — so only authored ground
// spawns, and the moat/water/seam/non-play (which no area covers) get no spawns
// (composes with the off-navmesh moat carve — belt + suspenders). This REPLACES
// the ZoneManager.GetZone → RegionSpawnTable.PickEnemyId/HasRoster lookup the two
// spawners did; when the JSON is absent/empty the spawners fall back to the legacy
// path (SpawnAreaTable.HasAny == false), so this is additive + non-breaking.
//
// Loaded WebGL-safe through CanonicalJson (Resources dual-copy first, StreamingAssets
// fallback) + JsonConvert — the SAME pattern GarrisonRecipeCatalog / QuestCatalog use.
// Pure DeNelle.Core (no Village ref, headless-safe): roles are STRINGS here; the
// Village spawners map "tank"/"dps"/"healer" onto EnemyRole and let EnemyFactory /
// EnemyBrain.RoleForId infer per-body role from the id (orc-tank → Tank, etc.).
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.World
{
    // ── JSON-mapped records (Newtonsoft; [JsonProperty] like the other catalogs) ──

    /// <summary>One weighted family entry — a themed archetype set that fills the role slots.</summary>
    public sealed class SpawnFamilyEntry
    {
        [JsonProperty("id")]     public string Id;
        [JsonProperty("weight")] public float  Weight = 1f;
        /// <summary>Enemy id for a TANK slot (e.g. "orc-tank").</summary>
        [JsonProperty("tank")]   public string Tank;
        /// <summary>Enemy id for a DPS/warrior slot (e.g. "orc-warrior").</summary>
        [JsonProperty("dps")]    public string Dps;
        /// <summary>Enemy id for a HEALER/mage slot (e.g. "orc-mage").</summary>
        [JsonProperty("healer")] public string Healer;
    }

    /// <summary>{x,z} world-space circle centre.</summary>
    public sealed class SpawnAreaCenter
    {
        [JsonProperty("x")] public float X;
        [JsonProperty("z")] public float Z;
    }

    /// <summary>Inclusive integer range (levelRange).</summary>
    public sealed class SpawnIntRange
    {
        [JsonProperty("min")] public int Min;
        [JsonProperty("max")] public int Max;
    }

    /// <summary>Role-count composition for a staged group.</summary>
    public sealed class SpawnComposition
    {
        [JsonProperty("tank")]   public int Tank;
        [JsonProperty("dps")]    public int Dps;
        [JsonProperty("healer")] public int Healer;
    }

    /// <summary>One geotagged spawn area (a circle footprint + its content rules).</summary>
    public sealed class SpawnArea
    {
        [JsonProperty("id")]          public string Id;
        [JsonProperty("center")]      public SpawnAreaCenter Center;
        [JsonProperty("radius")]      public float Radius;
        [JsonProperty("families")]    public List<SpawnFamilyEntry> Families;
        [JsonProperty("levelRange")]  public SpawnIntRange LevelRange;
        [JsonProperty("composition")] public SpawnComposition Composition;
        [JsonProperty("arenaPreset")] public string ArenaPreset;
        [JsonProperty("seedBudget")]  public int SeedBudget = 1;
    }

    internal sealed class SpawnAreaFile
    {
        [JsonProperty("version")]       public int Version;
        [JsonProperty("defaultAreaId")] public string DefaultAreaId;
        [JsonProperty("areas")]         public List<SpawnArea> Areas;
    }

    /// <summary>
    /// The resolved spawn plan for one draw at a position: a role-ordered enemy-id
    /// list (index 0 = leader/tank), the enemy LEVEL (feeds the existing 'threat'
    /// int), the area's arena-preset string, and provenance ids. Roles are inferable
    /// from the ids (RoleForId), so only the ids are carried.
    /// </summary>
    public struct SpawnDraw
    {
        public string   AreaId;
        public string   FamilyId;
        /// <summary>Role-ordered enemy ids (tanks, then dps, then healers). Empty when the area yields none.</summary>
        public string[] EnemyIds;
        /// <summary>Enemy level rolled in the area's levelRange (>=1). Passed as the 'threat' int.</summary>
        public int      Level;
        /// <summary>Arena preset string (small|med|large) forwarded to the encounter (no geometry hook yet).</summary>
        public string   ArenaPreset;
        public int      SeedBudget;
        /// <summary>True when a containing area was resolved and produced at least one enemy id.</summary>
        public bool     Valid;
    }

    /// <summary>
    /// Loads + serves the geotagged spawn areas. Static + cached (mirrors
    /// RegionSpawnTable / GarrisonRecipeCatalog). Missing/empty JSON ⇒ HasAny==false
    /// so callers fall back to the legacy roster lookup.
    /// </summary>
    public static class SpawnAreaTable
    {
        /// <summary>StreamingAssets-relative path (CanonicalJson strips the extension for Resources).</summary>
        public const string StreamingRelativePath = "Data/Canonical/spawn-areas.json";

        private static List<SpawnArea> _areas;
        private static string _defaultAreaId;

        /// <summary>All loaded areas (empty when the file is missing/empty).</summary>
        public static IReadOnlyList<SpawnArea> All { get { EnsureLoaded(); return _areas; } }

        /// <summary>True when at least one area is authored — the gate callers use to
        /// choose the data-driven path over the legacy roster lookup.</summary>
        public static bool HasAny { get { EnsureLoaded(); return _areas.Count > 0; } }

        /// <summary>Force a fresh read (after editing the JSON).</summary>
        public static void Reload() { _areas = null; EnsureLoaded(); }

        /// <summary>
        /// Resolve the area CONTAINING <paramref name="worldPos"/> (nearest center on
        /// overlap). Returns null when the position lies outside every authored area —
        /// the emergent-exclusion signal (no area = no spawn). Only X/Z are used.
        /// </summary>
        public static SpawnArea ResolveArea(Vector3 worldPos)
        {
            EnsureLoaded();
            SpawnArea best = null;
            float bestCenterSqr = float.MaxValue;
            for (int i = 0; i < _areas.Count; i++)
            {
                var a = _areas[i];
                if (a == null || a.Center == null) continue;
                float dx = worldPos.x - a.Center.X;
                float dz = worldPos.z - a.Center.Z;
                float dSqr = dx * dx + dz * dz;
                if (dSqr > a.Radius * a.Radius) continue;   // outside this circle
                if (dSqr < bestCenterSqr) { bestCenterSqr = dSqr; best = a; }
            }
            return best;
        }

        /// <summary>True when <paramref name="worldPos"/> lies inside an authored area
        /// (the spawn-here gate — outside all areas = no spawn).</summary>
        public static bool HasAreaAt(Vector3 worldPos) => ResolveArea(worldPos) != null;

        /// <summary>The default area (defaultAreaId, else the first), or null if none.</summary>
        public static SpawnArea Default
        {
            get
            {
                EnsureLoaded();
                if (_areas.Count == 0) return null;
                if (!string.IsNullOrEmpty(_defaultAreaId))
                    for (int i = 0; i < _areas.Count; i++)
                        if (_areas[i] != null && string.Equals(_areas[i].Id, _defaultAreaId, StringComparison.OrdinalIgnoreCase))
                            return _areas[i];
                return _areas[0];
            }
        }

        /// <summary>
        /// Build a spawn plan for <paramref name="worldPos"/>: resolve the containing area,
        /// weighted-pick a family, fill the composition's role slots with that family's
        /// archetype ids (role-ordered, tanks first as the leader), and roll a level in the
        /// area's levelRange. Returns <see cref="SpawnDraw.Valid"/> == false when the position
        /// is outside every authored area (emergent exclusion) or the area yields no ids.
        /// </summary>
        public static SpawnDraw BuildDraw(Vector3 worldPos)
        {
            var area = ResolveArea(worldPos);
            if (area == null) return default;   // Valid == false → caller skips (no spawn here)
            return BuildDrawFor(area);
        }

        /// <summary>Build a draw for a specific area (used by BuildDraw + any default fallback).</summary>
        public static SpawnDraw BuildDrawFor(SpawnArea area)
        {
            if (area == null) return default;

            var fam = PickFamily(area.Families);
            var comp = area.Composition;
            var ids = new List<string>(8);
            if (fam != null && comp != null)
            {
                for (int i = 0; i < comp.Tank;   i++) if (!string.IsNullOrEmpty(fam.Tank))   ids.Add(fam.Tank);
                for (int i = 0; i < comp.Dps;    i++) if (!string.IsNullOrEmpty(fam.Dps))    ids.Add(fam.Dps);
                for (int i = 0; i < comp.Healer; i++) if (!string.IsNullOrEmpty(fam.Healer)) ids.Add(fam.Healer);
            }
            // Never yield an empty family: fall back to one DPS/leader so an authored area
            // always produces a fightable encounter.
            if (ids.Count == 0 && fam != null && !string.IsNullOrEmpty(fam.Dps)) ids.Add(fam.Dps);

            int level = RollLevel(area.LevelRange);

            var draw = new SpawnDraw
            {
                AreaId      = area.Id,
                FamilyId    = fam != null ? fam.Id : null,
                EnemyIds    = ids.ToArray(),
                Level       = level,
                ArenaPreset = area.ArenaPreset,
                SeedBudget  = Mathf.Max(1, area.SeedBudget),
                Valid       = ids.Count > 0,
            };

            FlowTrace.Step("SpawnArea",
                $"BuildDraw area='{draw.AreaId}' family='{draw.FamilyId}' ids=[{string.Join(",", draw.EnemyIds)}] level={draw.Level} preset='{draw.ArenaPreset}' budget={draw.SeedBudget}.");
            return draw;
        }

        // ── internals ─────────────────────────────────────────────────────────────

        private static SpawnFamilyEntry PickFamily(List<SpawnFamilyEntry> families)
        {
            if (families == null || families.Count == 0) return null;
            float total = 0f;
            for (int i = 0; i < families.Count; i++)
                if (families[i] != null) total += Mathf.Max(0f, families[i].Weight);
            if (total <= 0f) return families[0];

            float roll = UnityEngine.Random.value * total;
            for (int i = 0; i < families.Count; i++)
            {
                if (families[i] == null) continue;
                roll -= Mathf.Max(0f, families[i].Weight);
                if (roll <= 0f) return families[i];
            }
            return families[families.Count - 1];
        }

        private static int RollLevel(SpawnIntRange range)
        {
            if (range == null) return 1;
            int min = Mathf.Max(1, range.Min);
            int max = Mathf.Max(min, range.Max);
            return UnityEngine.Random.Range(min, max + 1);
        }

        private static void EnsureLoaded()
        {
            if (_areas != null) return;
            _areas = new List<SpawnArea>();

            try
            {
                string text = CanonicalJson.Read(StreamingRelativePath);
                if (string.IsNullOrEmpty(text))
                {
                    Debug.LogWarning($"[SpawnAreaTable] {StreamingRelativePath} not found (0 areas — spawners fall back to legacy roster).");
                    return;
                }

                var file = JsonConvert.DeserializeObject<SpawnAreaFile>(text);
                if (file != null && file.Areas != null && file.Areas.Count > 0)
                {
                    _defaultAreaId = file.DefaultAreaId;
                    foreach (var a in file.Areas)
                        if (a != null && !string.IsNullOrEmpty(a.Id) && a.Center != null) _areas.Add(a);
                    Debug.Log($"[SpawnAreaTable] Loaded {_areas.Count} spawn area(s).");
                }
                else
                {
                    Debug.LogWarning("[SpawnAreaTable] spawn-areas.json parsed empty.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SpawnAreaTable] Failed to read spawn-areas.json: {ex.Message}");
            }
        }
    }
}
