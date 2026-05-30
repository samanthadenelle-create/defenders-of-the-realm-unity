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
// Pattern mirrors WaveSystemBridgeBootstrap / AudioBootstrap: a
// [RuntimeInitializeOnLoadMethod] that Clear()s then registers, guarded so a
// domain-reload-off second Play re-registers cleanly.
//
// CONTENT: the two proven placement=role tower archetypes (ground archer =
// can't hit air; wall wizard = elevated, hits air) — the exact split the deleted
// DefenseTestSetup validated in-game. behaviorId "DefenseTower" resolves in
// StructureFactory.AttachBehavior. Structural content (walls/stairs/gates) is
// deferred to WO-140; the factory + registry are already type-agnostic.
// =============================================================================

using UnityEngine;
using DeNelle.Core.Catalog;
using DeNelle.Core.Combat;

namespace DeNelle.Village
{
    /// <summary>
    /// Registers the starter catalog entries into <see cref="CatalogRegistry"/>
    /// at startup. Idempotent across play sessions (Clear-then-register), so it
    /// survives domain-reload-off like the other bootstrappers.
    /// </summary>
    public static class CatalogBootstrap
    {
        // Candidate polyperfect visuals (gitignored pack; LogWarning-safe if absent).
        private const string TowerBigPrefab =
            "polyperfect/Tower_Medieval_Big";
        private const string TowerWoodPrefab =
            "polyperfect/Tower_Medieval_Wood";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            // Clear first so a domain-reload-off second Play doesn't double-register.
            CatalogRegistry.Clear();

            // ── Ground Archer Tower — short range, fast, CANNOT hit air ──────────
            CatalogRegistry.Register(new CatalogEntry
            {
                id          = "tower_ground_archer",
                displayName = "Archer Tower",
                type        = CatalogType.Tower,
                kind        = EntryKind.Cell,
                visualPrefabPath = TowerBigPrefab,
                repo = new RepoProps
                {
                    behaviorId = "DefenseTower",
                    buildCost  = 100,
                    navSurface = NavSurfaceKind.Blocker,
                    range = 16f, damage = 6f, fireRate = 2.5f,
                    canHitAir = false, element = DamageElement.None,
                    placement = new PlacementRules
                    {
                        mustSitOn = PlacementSurface.Ground,
                        footprint = 2.5f, noOverlap = true, checkAffordable = true,
                    },
                },
            });

            // ── Wall Wizard Tower — long range, slower, HITS AIR, wall-top only ──
            CatalogRegistry.Register(new CatalogEntry
            {
                id          = "tower_wall_wizard",
                displayName = "Wizard Tower",
                type        = CatalogType.Tower,
                kind        = EntryKind.Cell,
                visualPrefabPath = TowerWoodPrefab,
                repo = new RepoProps
                {
                    behaviorId = "DefenseTower",
                    buildCost  = 150,
                    navSurface = NavSurfaceKind.Blocker,
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

            Debug.Log($"[CatalogBootstrap] Registered {CatalogRegistry.Count} catalog " +
                      "entrie(s) — catalog data path is now live.");
        }
    }
}
