// =============================================================================
// UpgradeFamilyResolver — the ONE place that decides which upgrade LADDER a
// building id belongs to. WO: dual-family completion mis-route (owner F8:
// "when i upgrade the lumbermill, on complete with crystals doesnt seem to
// trigger lumbermill level up. Seems to dead end.").
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Buildings.Progression
//
// WHY THIS TYPE EXISTS (the defect it retires)
//   farm / lumbermill / forge are DUAL-FAMILY: they appear in BOTH
//   building-tiers.json (the WO-430 city tier + perk ladder) AND
//   ResourceBuildingProgression (the legacy per-building PlayerPrefs level
//   ladder). Every site that resolves the family therefore has to pick a
//   precedence, and the rule is "CITY TIERS WIN; else legacy":
//     * BuildingUpgradeVM (the START side)      — comment "city tiers win; else legacy"
//     * DialogueCommandSink.structure_upgrade   — "BLIND-03-02 dual-authority guard"
//   CompletedUpgradeApplier (the COMPLETE side) checked them in the OPPOSITE
//   order, so a lumbermill/farm/forge upgrade was STARTED on the city ladder and
//   APPLIED to the resource ladder: BuildingUpgradeService.ApplyTier — the ONLY
//   writer of GameState.BuildingTiers, Save(), ModifierService.Recompute() and
//   ApplyStructureHp — never ran, the tier/perk panel never moved, and the trace
//   still read as a clean success. Two hand-written precedence orders in two
//   files is the bug; ONE resolver both sides call is the fix.
//
// RULE (do not reorder — a reorder re-opens the exact dead-end above):
//   1. a placed-structure job key ("itemId@cellX_cellZ") -> PlacedStructure
//   2. a city-tier catalog id                            -> City      (WINS on overlap)
//   3. a legacy resource-building id                     -> Resource
//   4. otherwise                                         -> None
// =============================================================================

using DeNelle.Core.State;

namespace DeNelle.Village.Buildings.Progression
{
    /// <summary>Which upgrade ladder owns a building id (see <see cref="UpgradeFamilyResolver"/>).</summary>
    public enum UpgradeFamily
    {
        /// <summary>No upgrade ladder knows this id.</summary>
        None = 0,
        /// <summary>WO-430 city tier + perk ladder — GameState.BuildingTiers (the authoritative store).</summary>
        City = 1,
        /// <summary>Legacy resource-building level ladder — ResourceBuildingState PlayerPrefs.</summary>
        Resource = 2,
        /// <summary>A placed structure, keyed "itemId@cellX_cellZ" — BaseLayout record + live PlacedStructure.</summary>
        PlacedStructure = 3,
    }

    /// <summary>
    /// The single authority on upgrade-family precedence. EVERY site that has to choose
    /// between the city-tier ladder and the legacy resource ladder calls this — the START
    /// side and the COMPLETE side must never re-derive it independently.
    /// </summary>
    public static class UpgradeFamilyResolver
    {
        /// <summary>Resolve the ladder that owns <paramref name="buildingId"/>. City wins on overlap.</summary>
        public static UpgradeFamily Resolve(string buildingId)
        {
            if (string.IsNullOrEmpty(buildingId)) return UpgradeFamily.None;
            if (buildingId.IndexOf('@') >= 0) return UpgradeFamily.PlacedStructure;
            if (BuildingTierCatalog.IsUpgradable(buildingId)) return UpgradeFamily.City;
            if (ResourceBuildingProgression.IsResourceBuilding(buildingId)) return UpgradeFamily.Resource;
            return UpgradeFamily.None;
        }

        /// <summary>
        /// True when the id sits in BOTH ladders (farm / lumbermill / forge today). Used by the
        /// completion trace so an F8 capture can tell the two ladders apart in ONE read.
        /// </summary>
        public static bool IsDualFamily(string buildingId)
        {
            if (string.IsNullOrEmpty(buildingId) || buildingId.IndexOf('@') >= 0) return false;
            return BuildingTierCatalog.IsUpgradable(buildingId)
                && ResourceBuildingProgression.IsResourceBuilding(buildingId);
        }

        /// <summary>Human-readable ladder name + its backing store, for traces. ASCII only.</summary>
        public static string LadderName(UpgradeFamily family)
        {
            switch (family)
            {
                case UpgradeFamily.City:            return "CITY-TIER ladder (GameState.BuildingTiers)";
                case UpgradeFamily.Resource:        return "RESOURCE-LEVEL ladder (PlayerPrefs dotr.resbuilding.level.*)";
                case UpgradeFamily.PlacedStructure: return "PLACED-STRUCTURE ladder (BaseLayout record)";
                default:                            return "NO ladder";
            }
        }
    }
}
