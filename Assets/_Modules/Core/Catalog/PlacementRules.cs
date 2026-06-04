// =============================================================================
// PlacementRules — declarative "does this naturally work HERE" conditions, as
// DATA (CATALOG_SYSTEM.md). Evaluated at the free cursor (NO grid). Extracted
// from already-coded checks: TowerPlacementSystem (overlap/cost) +
// ValidateBuildingGateClearance (distance). One rule-set per entry, so every
// item carries its own valid-placement logic.
// =============================================================================
namespace DeNelle.Core.Catalog
{
    [System.Serializable]
    public sealed class PlacementRules
    {
        /// <summary>Must-sit-on surface (Ground vs WallWalk = the F7 placement=role split).</summary>
        public PlacementSurface mustSitOn = PlacementSurface.AnyTerrain;

        /// <summary>No-overlap footprint check (TowerPlacementSystem).</summary>
        public bool  noOverlap = true;

        /// <summary>Footprint radius in metres (defaults to the 3 m Wall_Stone_3x3 grain).</summary>
        public float footprint = 3f;

        /// <summary>Clearance from gates (ValidateBuildingGateClearance). 0 = no rule.</summary>
        public float minDistanceFromGate = 0f;

        /// <summary>Needs a floor/support under it (e.g. stairs require a wall top).</summary>
        public bool  requiresSupport = false;

        /// <summary>Enforce the build-cost (affordable) rule. Cost itself is RepoProps.buildCost.</summary>
        public bool  checkAffordable = true;

        /// <summary>Ownership-gate id for cosmetics; null = always available.</summary>
        public string ownedGate = null;
    }
}
