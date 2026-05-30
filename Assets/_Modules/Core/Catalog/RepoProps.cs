// =============================================================================
// RepoProps — the BEHAVIOR half of a CatalogEntry (catalog ⊥ repo). Pure data.
// Combat stats are EXTRACTED VERBATIM from DefenseTower (rules-from-code); a
// cosmetic swaps the entry's visualPrefabPath and NEVER these — the structural
// cosmetic-only guarantee. behaviorId is resolved to a component by Village
// (keeps Core free of Village refs).
// =============================================================================
using DeNelle.Core.Combat;

namespace DeNelle.Core.Catalog
{
    [System.Serializable]
    public sealed class RepoProps
    {
        /// <summary>What this contributes to the NavMesh once placed.</summary>
        public NavSurfaceKind navSurface = NavSurfaceKind.None;

        /// <summary>Build cost (the affordable rule reads this).</summary>
        public int buildCost = 0;

        /// <summary>Village resolves this string -> the actual behaviour component (Core stays pure).</summary>
        public string behaviorId = null;

        /// <summary>Placement conditions, evaluated at the free cursor.</summary>
        public PlacementRules placement = new PlacementRules();

        // --- Combat stats (Tower defs) — copied straight off DefenseTower's public fields ---
        public float         range     = 0f;
        public float         damage    = 0f;
        public float         fireRate  = 0f;     // shots per second
        public bool          canHitAir = false;  // ground = false · wall-walk = true
        public DamageElement element   = DamageElement.None;
    }
}
