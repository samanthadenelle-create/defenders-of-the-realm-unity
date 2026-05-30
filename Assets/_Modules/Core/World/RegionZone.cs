// =============================================================================
// RegionZone — the outer-world region model (WO-142 / WO-107). Pure data, lives
// in DeNelle.Core so harvest nodes (WO-141), raids (WO-143), and regional crystal
// grades (WO-144) can all classify a world position into ONE shared region set
// without referencing Village.
// -----------------------------------------------------------------------------
// The four regions map onto ExteriorTerrainBuilder's existing directional biomes
// (N forest / E farmland / S barren-Wound / W river valley) and carry the
// warmth-in / dread-out danger dial: Goldfields (E, safe) -> Stoneback (W) ->
// Mirewood (S) -> Ashwood (N, the ruined front line nearest the Wound).
// =============================================================================
namespace DeNelle.Core.World
{
    /// <summary>
    /// The outer-world regions. Order follows the danger dial (Village = 0 safest
    /// → Ashwood = most dangerous). Stable enum — append new regions, never
    /// renumber (saved data / spawn rules key off these).
    /// </summary>
    public enum RegionId
    {
        /// <summary>Inside the walls — the safe home zone (not an outer region).</summary>
        Village = 0,
        /// <summary>East — peopled breadbasket farmland. Safest outer region (DangerTier 1).</summary>
        Goldfields = 1,
        /// <summary>West — old stony river-valley uplands. Neutral (DangerTier 2).</summary>
        Stoneback = 2,
        /// <summary>South — the drowned valley toward the Wound. Heavy (DangerTier 3).</summary>
        Mirewood = 3,
        /// <summary>North — the ruined front line nearest the Wound. Most dangerous (DangerTier 4).</summary>
        Ashwood = 4,
    }

    /// <summary>Static facts about a region — danger tier, display name, cardinal sign.</summary>
    public sealed class RegionZone
    {
        public RegionId Id;
        public string   DisplayName;
        /// <summary>1 (safe) … 4 (deadly). Village = 0. Drives raid size (WO-143) and crystal grade (WO-144).</summary>
        public int      DangerTier;
        /// <summary>Cardinal direction of the region from the village centre (for the terrain-biome map).</summary>
        public string   Cardinal;

        public RegionZone(RegionId id, string displayName, int dangerTier, string cardinal)
        {
            Id = id;
            DisplayName = displayName;
            DangerTier = dangerTier;
            Cardinal = cardinal;
        }
    }
}
