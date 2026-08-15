// =============================================================================
// CatalogBootstrap â€” populates the CatalogRegistry at startup (WO-148 P0 fix).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// THE P0: CatalogRegistry (DeNelle.Core.Catalog) had ZERO Register() callers â€”
// it was empty at runtime, so OfType()/Get() returned nothing and the catalog
// data path was unproven. This registrar fills it, so StructureFactory + future
// build-mode UI have real "buckets" to read.
//
// DATA-DRIVEN (Build Mode S3): the catalog content is no longer hardcoded C#.
// It is loaded from a canonical JSON row-set â€”
//   Assets/StreamingAssets/Data/Canonical/structures-catalog.json   (source)
//   Assets/Resources/Data/Canonical/structures-catalog.json         (WebGL copy, WINS)
// â€” through DeNelle.Core.CanonicalJson (Resources.Load first, WebGL-safe; the
// proven pattern CosmeticCatalog / PetCatalog / Theme already use). Adding a
// wall / mine / gate / tower is now a JSON row, not a code change.
//
// A tiny hardcoded fallback (the three tower rows) registers ONLY if the JSON
// fails to load/parse, so the build palette is never empty. It MUST stay a
// field-for-field MIRROR of those rows in structures-catalog.json â€” see the
// banner on RegisterFallback; BuildEconomyRegression gate 12 enforces it.
//
// Pattern mirrors WaveSystemBridgeBootstrap / AudioBootstrap: a
// [RuntimeInitializeOnLoadMethod] that Clear()s then registers, guarded so a
// domain-reload-off second Play re-registers cleanly. behaviorId strings resolve
// to Village components in StructureFactory.AttachBehavior (the Core/Village
// boundary â€” a switch, no reflection).
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
    /// JSON cannot be loaded, so the palette is never empty â€” that hardcoded set is a
    /// field-for-field MIRROR of its structures-catalog.json rows (see RegisterFallback).
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
                          $"entrie(s) from structures-catalog.json â€” data-driven path is live.");
                // Owner-dialed poses saved by the Orient tool overlay the shipped data
                // (local wins â€” the 2026-07-08 "save locally" directive; gear-offsets pattern).
                StructureOrientationLocalStore.ApplyAll();
                return;
            }

            // JSON missing / empty / unparseable â€” keep the palette alive with the
            // two proven placement=role towers so the build path never dead-ends.
            RegisterFallback();
            Debug.LogWarning($"[CatalogBootstrap] structures-catalog.json unavailable â€” " +
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

        // â•â•â• Hardcoded fallback â€” the three tower rows â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //
        // âš  THIS METHOD MUST MIRROR structures-catalog.json, FIELD FOR FIELD. âš 
        //
        // It is the JSON-load-FAILURE path: when the catalog cannot be read, these rows
        // ARE the game's content. Every value that drifts from its catalog counterpart
        // silently SHIPS DIFFERENT CONTENT on that path â€” a different model, a different
        // price, a different footprint â€” and nobody sees it until a player hits the
        // failure path in the wild. Historic drifts caught here: footprint 2.5 vs the
        // catalog's 1.75 (commit 0ac59581) and visualPrefabPath "PatriciaLight/tower2",
        // art from the Defend-the-Tower module DELETED on 2026-06-09.
        //
        // When you edit a tower row in structures-catalog.json, edit it HERE in the same
        // breath. BuildEconomyRegression gate 12 ("[fallback-parity]") reflects over these
        // constructed rows and fails the build on ANY field divergence, so drift is now a
        // red gate rather than a silent content swap.
        //
        // Used ONLY when the JSON cannot be loaded, so the palette is never empty.
        private static void RegisterFallback()
        {
            // Ground Archer Tower â€” short range, fast, CANNOT hit air.
            // MIRRORS: structures-catalog.json entry "tower_ground_archer".
            CatalogRegistry.Register(new CatalogEntry
            {
                id          = "tower_ground_archer",
                displayName = "Archer Tower",
                type        = CatalogType.Tower,
                kind        = EntryKind.Cell,
                // ALL-WOOD ladder (owner 2026-08-06: "New Wooden Tower Level 1", then L2 + L3).
                // Owner-sourced Tripo art, git-TRACKED under Resources/Structures and authored by
                // DeNelle.Editor.WoodenWatchtowerBuilder.Build. Supersedes the Castle_Round /
                // Castle_Square / Medieval_Big polyperfect ladder (WO-902) and, before it, the
                // Tribal _T ladder. NOT PatriciaLight - that module was REMOVED 2026-06-09.
                visualPrefabPath = "Structures/Tower_Wooden_Watchtower",
                repo = new RepoProps
                {
                    behaviorId = "DefenseTower",
                    buildCost  = 100,
                    cost       = new DeNelle.Core.Catalog.ResourceCost { wood = 90, food = 0, iron = 40, crystals = 0 },
                    maxLevel   = 3,
                    upgradeCost = new[]
                    {
                        new DeNelle.Core.Catalog.ResourceCost { wood = 108, food = 0, iron =  48, crystals = 0 },   // L1â†’L2
                        new DeNelle.Core.Catalog.ResourceCost { wood = 225, food = 0, iron = 100, crystals = 0 },   // L2â†’L3
                    },
                    upgradeVisualPath = new[]
                    {
                        "Structures/Tower_Wooden_Watchtower_L2",   // L2
                        "Structures/Tower_Wooden_Watchtower_L3",   // L3
                    },
                    // MIRRORS the catalog row's "upgradeTexturePath". Only L3 reskins; the empty
                    // string is L2's "no texture override" slot and must be preserved, because the
                    // array is indexed by upgrade step - dropping it would shift the L3 basecolor
                    // onto L2. This was ABSENT here while the catalog carried it, and gate 12
                    // [fallback-parity] only caught it once WO-928 brought this row under the
                    // deep-compare; the gap had been latent, so a fallback-path L3 tower rendered
                    // untextured. Parity is the point: every public RepoProps field, or a red build.
                    upgradeTexturePath = new[]
                    {
                        "",                                                                    // L2 - no override
                        "Structures/Tower_Wooden_Watchtower_L3_Tex/WoodenWatchtowerL3_basecolor", // L3
                    },
                    // WO-928 defect A - MIRRORS the catalog row's "preservePrefabRotation": true.
                    // This row's art keeps its own authored root rotation instead of DEF-232's
                    // identity reset (that reset is what shipped the L3 tower on its side, and then
                    // let VisualFactory.Fit measure its short axis and oversize it 8.34x). It is the
                    // ONLY row in the catalog that sets it. Required here, not optional: gate 12
                    // [fallback-parity] deep-compares EVERY public RepoProps field between this
                    // constructed row and the parsed catalog row, so a missing bool is a red build.
                    preservePrefabRotation = true,
                    navSurface = NavSurfaceKind.Blocker,
                    heightMul = 1.2f,    // owner 2026-08-05: archer tower = 1.2 Ã— base (4 m) = 4.8 m
                    range = 14f, damage = 6f, fireRate = 2.5f,
                    canHitAir = false, element = DamageElement.None,
                    projectileStyle = "bolt",   // owner 2026-07-08: arrows, not pellets
                    placement = new PlacementRules
                    {
                        mustSitOn = PlacementSurface.Ground,
                        footprint = 1.75f, noOverlap = true, checkAffordable = true,
                    },
                },
                orientation = new OrientationFix
                {
                    // manual:true at ZERO is deliberate and must match the catalog row, or the
                    // reflective [fallback-parity] gate fails on the bool. It follows the
                    // tower_arcane_spire precedent: human-verified, so no tool re-bakes an
                    // advisory euler that would DOUBLE-APPLY on top of the -90 the builder
                    // bakes into each prefab. The correction does NOT belong here - ReskinForLevel
                    // never applies this block to a tier model, and Create applies it AFTER
                    // VisualFactory.Skin has already fit to height, so a -90 here would fit the
                    // model's SHORT axis and ship a 9.25 m tower. See the catalog row's note.
                    corrected = false, manual = true,
                    euler  = new[] { 0f, 0f, 0f },
                    offset = new[] { 0f, 0f, 0f },
                    scale  = 1f,
                    note   = "mirrors the catalog row (zero + manual-locked; the -90 upright correction is baked into the three prefabs, not applied from the row)",
                },
            });

            // Ballista — WO-989 id is tower_ballista (was tower_wall_wizard).
            // Owner ruling 2026-07-08: model IS a ballista, GROUND-placed, not wall-walk.
            // MIRRORS: structures-catalog.json entry "tower_ballista".
            CatalogRegistry.Register(new CatalogEntry
            {
                id          = "tower_ballista",
                displayName = "Ballista",
                type        = CatalogType.Tower,
                kind        = EntryKind.Cell,
                visualPrefabPath = "Structures/WizardTower_1",
                repo = new RepoProps
                {
                    behaviorId = "DefenseTower",
                    buildCost  = 150,
                    // WO-947 cost basket, pin 4 (OWNER 2026-08-14 verbatim: "thats a baliista
                    // mechanical"): this row is REGULAR -- wood + iron, NEVER crystals. The
                    // "wizard" in the id is stale naming, not a classification; the row's own
                    // data (displayName "Ballista", element None, projectileStyle "bolt") is what
                    // the owner ruled on. The former crystals (70 / 84 / 175) were folded 1:1
                    // into IRON so every basket TOTAL is unchanged (160 / 192 / 400).
                    // MIRRORS structures-catalog.json v18 -- gate 12 [fallback-parity] enforces.
                    cost       = new DeNelle.Core.Catalog.ResourceCost { wood = 60, food = 0, iron = 100, crystals = 0 },
                    maxLevel   = 3,
                    upgradeCost = new[]
                    {
                        new DeNelle.Core.Catalog.ResourceCost { wood =  72, food = 0, iron = 120, crystals = 0 },   // L1â†’L2
                        new DeNelle.Core.Catalog.ResourceCost { wood = 150, food = 0, iron = 250, crystals = 0 },   // L2â†’L3
                    },
                    navSurface = NavSurfaceKind.Blocker,
                    // 2026-08-05 cadence pass (owner ruling: every structure on ONE cadence,
                    // "all relatively the same size... all scaled to the same point"). The TOWER
                    // CLASS is now pinned to the owner-ruled archer ANCHOR of 1.2 x base = 4.8 m,
                    // measured at 49.9% of a house's diameter. Was the WO-764 class value 1.25;
                    // a tower sitting 4% off the anchor was the in-family outlier the ruling removes.
                    // Mirrors structures-catalog.json "tower_ballista" (parity gate 12 enforces).
                    heightMul = 1.2f,
                    range = 22f, damage = 30f, fireRate = 0.5f,
                    canHitAir = true, element = DamageElement.None,
                    projectileStyle = "bolt",
                    placement = new PlacementRules
                    {
                        mustSitOn = PlacementSurface.Ground,
                        footprint = 1.4f, noOverlap = true, checkAffordable = true,
                    },
                },
                orientation = new OrientationFix
                {
                    // manual=true so the auto baker never re-tips it; the Tripo fbx needs
                    // the standard X-90 to stand up. Without this the fallback ships a
                    // ballista lying on its side.
                    corrected = false, manual = true,
                    euler  = new[] { -90f, 0f, 0f },
                    offset = new[] { 0f, 0f, 0f },
                    scale  = 1f,
                    note   = "mirrors the catalog row (manual X-90 â€” the Tripo fbx stands upright with it)",
                },
            });

            // Arcane Spire (WO-113) â€” slow-firing AoE MAGIC tower: every shot blasts a
            // radius around the impact for Aether damage + a Slow debuff. Ground-placed,
            // crystal-heavy cost. The optional aoeRadius / slowSeconds / splashFraction
            // tune the blast (the ArcaneTower component falls back to its own defaults
            // when a field is 0).
            // MIRRORS: structures-catalog.json entry "tower_arcane_spire".
            CatalogRegistry.Register(new CatalogEntry
            {
                id          = "tower_arcane_spire",
                displayName = "Arcane Spire",
                type        = CatalogType.Tower,
                kind        = EntryKind.Cell,
                visualPrefabPath  = "Structures/ArcaneSpire_1",
                // WO-707: the Tripo albedo lives outside the fbm folder, so it must be
                // FORCED on or the spire renders pure white.
                visualTexturePath = "Structures/ArcaneSpire_Albedo",
                repo = new RepoProps
                {
                    behaviorId = "ArcaneTower",
                    buildCost  = 200,
                    // WO-947 cost basket, pin 1 (OWNER 2026-08-14 verbatim: "Crystals and Iron"):
                    // this row is MAGICAL (element Aether, behaviorId ArcaneTower, projectileStyle
                    // "spell"), so its basket is CRYSTALS + IRON and WOOD IS REMOVED. The former
                    // wood (40 / 48 / 100) was folded 1:1 into CRYSTALS -- the ruling calls magical
                    // structures crystal-BASED -- so every basket TOTAL is unchanged (165/198/412).
                    // MIRRORS structures-catalog.json v18 -- gate 12 [fallback-parity] enforces.
                    cost       = new DeNelle.Core.Catalog.ResourceCost { wood = 0, food = 0, iron = 40, crystals = 125 },
                    maxLevel   = 3,
                    upgradeCost = new[]
                    {
                        new DeNelle.Core.Catalog.ResourceCost { wood = 0, food = 0, iron =  48, crystals = 150 },   // L1â†’L2
                        new DeNelle.Core.Catalog.ResourceCost { wood = 0, food = 0, iron = 100, crystals = 312 },   // L2â†’L3
                    },
                    upgradeVisualPath  = new[] { "Structures/ArcaneSpire_2",        "Structures/ArcaneSpire_3" },
                    upgradeTexturePath = new[] { "Structures/ArcaneSpire_2_Albedo", "Structures/ArcaneSpire_3_Albedo" },
                    navSurface = NavSurfaceKind.Blocker,
                    // 2026-08-05 cadence pass: pinned to the owner-ruled tower ANCHOR 1.2 x base
                    // (4.8 m), same as tower_wall_wizard above. Was the WO-764 class value 1.25.
                    // Mirrors structures-catalog.json "tower_arcane_spire" (parity gate 12 enforces).
                    heightMul = 1.2f,
                    range = 16f, damage = 20f, fireRate = 0.6f,   // owner 2026-07-08: mid-range zone caster
                    canHitAir = true, element = DamageElement.Aether,
                    projectileStyle = "spell",
                    aoeRadius = 6f, slowSeconds = 2.5f, splashFraction = 0.7f,
                    placement = new PlacementRules
                    {
                        mustSitOn = PlacementSurface.Ground,
                        footprint = 2.1f, noOverlap = true, checkAffordable = true,
                    },
                },
                orientation = new OrientationFix
                {
                    corrected = false, manual = true,
                    euler  = new[] { 0f, 0f, 0f },
                    offset = new[] { 0f, 0f, 0f },
                    scale  = 1f,
                    note   = "mirrors the catalog row (owner 2026-07-08 'needs 0 0 0'; manual so the baker never re-tips it)",
                },
            });
        }
    }
}
