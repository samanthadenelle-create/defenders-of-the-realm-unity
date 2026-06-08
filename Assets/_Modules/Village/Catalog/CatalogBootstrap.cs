// =============================================================================
// CatalogBootstrap — populates the CatalogRegistry at startup (WO-148 P0 fix).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// THE P0: CatalogRegistry (DeNelle.Core.Catalog) had ZERO Register() callers —
// it was empty at runtime, so OfType()/Get() returned nothing and the catalog
// data path was unproven. This registrar fills it, so StructureFactory + future
// build-mode UI have real "buckets" to read.
//
// DATA-DRIVEN (Build Mode S3): the catalog content is no longer hardcoded C#.
// It is loaded from a canonical JSON row-set —
//   Assets/StreamingAssets/Data/Canonical/structures-catalog.json   (source)
//   Assets/Resources/Data/Canonical/structures-catalog.json         (WebGL copy, WINS)
// — through DeNelle.Core.CanonicalJson (Resources.Load first, WebGL-safe; the
// proven pattern CosmeticCatalog / PetCatalog / Theme already use). Adding a
// wall / mine / gate / tower is now a JSON row, not a code change.
//
// A tiny hardcoded fallback (the two proven placement=role towers) registers ONLY
// if the JSON fails to load/parse, so the build palette is never empty.
//
// Pattern mirrors WaveSystemBridgeBootstrap / AudioBootstrap: a
// [RuntimeInitializeOnLoadMethod] that Clear()s then registers, guarded so a
// domain-reload-off second Play re-registers cleanly. behaviorId strings resolve
// to Village components in StructureFactory.AttachBehavior (the Core/Village
// boundary — a switch, no reflection).
// =============================================================================

using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using DeNelle.Core;
using DeNelle.Core.Catalog;
using DeNelle.Core.Combat;

namespace DeNelle.Village
{
    /// <summary>
    /// Registers the build-mode catalog entries into <see cref="CatalogRegistry"/>
    /// at startup by LOADING structures-catalog.json (data-driven). Idempotent
    /// across play sessions (Clear-then-register), so it survives domain-reload-off
    /// like the other bootstrappers. Falls back to a tiny hardcoded set only if the
    /// JSON cannot be loaded, so the palette is never empty.
    /// </summary>
    public static class CatalogBootstrap
    {
        /// <summary>StreamingAssets-relative path of the catalog JSON (CanonicalJson resolves Resources first).</summary>
        private const string CatalogRelativePath = "Data/Canonical/structures-catalog.json";

        /// <summary>Parsed root of structures-catalog.json.</summary>
        [System.Serializable]
        private sealed class CatalogFile
        {
            [JsonProperty("version")] public int Version;
            [JsonProperty("entries")] public List<CatalogEntry> Entries = new List<CatalogEntry>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            // Clear first so a domain-reload-off second Play doesn't double-register.
            CatalogRegistry.Clear();

            int loaded = LoadFromJson();
            if (loaded > 0)
            {
                Debug.Log($"[CatalogBootstrap] Registered {CatalogRegistry.Count} catalog " +
                          $"entrie(s) from structures-catalog.json — data-driven path is live.");
                return;
            }

            // JSON missing / empty / unparseable — keep the palette alive with the
            // two proven placement=role towers so the build path never dead-ends.
            RegisterFallback();
            Debug.LogWarning($"[CatalogBootstrap] structures-catalog.json unavailable — " +
                             $"registered {CatalogRegistry.Count} hardcoded fallback entrie(s).");
        }

        /// <summary>
        /// Reads structures-catalog.json via the WebGL-safe loader, parses each row
        /// into a <see cref="CatalogEntry"/>, and registers it. Returns the number of
        /// entries registered (0 = load/parse failure, caller falls back).
        /// </summary>
        private static int LoadFromJson()
        {
            string json;
            try
            {
                json = CanonicalJson.Read(CatalogRelativePath);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[CatalogBootstrap] read of {CatalogRelativePath} threw: {ex.Message}");
                return 0;
            }

            if (string.IsNullOrEmpty(json))
                return 0;

            CatalogFile file;
            try
            {
                // StringEnumConverter so "Tower"/"Ground"/"Aether"/etc. parse to the
                // Core enums; null-handling so a sparse row keeps RepoProps defaults.
                var settings = new JsonSerializerSettings
                {
                    Converters = { new StringEnumConverter() },
                    NullValueHandling = NullValueHandling.Ignore,
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                };
                file = JsonConvert.DeserializeObject<CatalogFile>(json, settings);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[CatalogBootstrap] parse of {CatalogRelativePath} failed: {ex.Message}");
                return 0;
            }

            if (file == null || file.Entries == null || file.Entries.Count == 0)
                return 0;

            int count = 0;
            foreach (var entry in file.Entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.id)) continue;
                if (entry.repo == null) entry.repo = new RepoProps();
                if (entry.repo.placement == null) entry.repo.placement = new PlacementRules();
                CatalogRegistry.Register(entry);
                count++;
            }
            return count;
        }

        // ── Hardcoded fallback — the two proven placement=role towers ──────────
        // Used ONLY when the JSON cannot be loaded, so the palette is never empty.
        private static void RegisterFallback()
        {
            // Ground Archer Tower — short range, fast, CANNOT hit air.
            CatalogRegistry.Register(new CatalogEntry
            {
                id          = "tower_ground_archer",
                displayName = "Archer Tower",
                type        = CatalogType.Tower,
                kind        = EntryKind.Cell,
                visualPrefabPath = "PatriciaLight/tower2",
                repo = new RepoProps
                {
                    behaviorId = "DefenseTower",
                    buildCost  = 100,
                    navSurface = NavSurfaceKind.Blocker,
                    visualHeight = 5f,
                    range = 16f, damage = 6f, fireRate = 2.5f,
                    canHitAir = false, element = DamageElement.None,
                    placement = new PlacementRules
                    {
                        mustSitOn = PlacementSurface.Ground,
                        footprint = 2.5f, noOverlap = true, checkAffordable = true,
                    },
                },
            });

            // Wall Wizard Tower — long range, slower, HITS AIR, wall-top only.
            CatalogRegistry.Register(new CatalogEntry
            {
                id          = "tower_wall_wizard",
                displayName = "Wizard Tower",
                type        = CatalogType.Tower,
                kind        = EntryKind.Cell,
                visualPrefabPath = "PatriciaLight/tower2",
                repo = new RepoProps
                {
                    behaviorId = "DefenseTower",
                    buildCost  = 150,
                    navSurface = NavSurfaceKind.Blocker,
                    visualHeight = 5f,
                    range = 55f, damage = 14f, fireRate = 1f,
                    canHitAir = true, element = DamageElement.Aether,
                    placement = new PlacementRules
                    {
                        mustSitOn = PlacementSurface.WallWalk,
                        footprint = 2f, noOverlap = true,
                        requiresSupport = true, checkAffordable = true,
                    },
                },
            });

            // Arcane Spire (WO-113) — slow-firing AoE MAGIC tower: every shot blasts a
            // radius around the impact for Aether damage + a Slow debuff. Ground-placed,
            // crystal-heavy cost. The optional aoeRadius / slowSeconds / splashFraction
            // tune the blast (the ArcaneTower component falls back to its own defaults
            // when a field is 0).
            CatalogRegistry.Register(new CatalogEntry
            {
                id          = "tower_arcane_spire",
                displayName = "Arcane Spire",
                type        = CatalogType.Tower,
                kind        = EntryKind.Cell,
                visualPrefabPath = "Structures/Tower_Medieval_Big",
                repo = new RepoProps
                {
                    behaviorId = "ArcaneTower",
                    buildCost  = 200,
                    navSurface = NavSurfaceKind.Blocker,
                    visualHeight = 6f,
                    range = 24f, damage = 16f, fireRate = 0.6f,
                    canHitAir = true, element = DamageElement.Aether,
                    aoeRadius = 6f, slowSeconds = 2.5f, splashFraction = 0.7f,
                    maxLevel = 3,
                    placement = new PlacementRules
                    {
                        mustSitOn = PlacementSurface.Ground,
                        footprint = 3f, noOverlap = true, checkAffordable = true,
                    },
                },
            });
        }
    }
}
