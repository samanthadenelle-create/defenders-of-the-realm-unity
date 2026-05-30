// =============================================================================
// ZoneManager — classifies a world position into a RegionId (WO-142 / WO-107).
// -----------------------------------------------------------------------------
// THE shared region classifier. Harvest nodes (WO-141), raids (WO-143), and
// regional crystal grades (WO-144) all call GetZone(worldPos) so "which region
// am I in" has ONE source of truth. Pure logic in DeNelle.Core — no Village ref,
// no scene dependency (works headless).
//
// Geometry: the village sits at world origin inside its walls (~±42 X / ±33 Z).
// The four outer regions fan out by cardinal direction beyond the walls, matching
// ExteriorTerrainBuilder's directional biomes:
//   East  (+X) = Goldfields · West (-X) = Stoneback · South (-Z) = Mirewood ·
//   North (+Z) = Ashwood. Inside the wall footprint = Village.
// Region is chosen by the DOMINANT axis (whichever of |x|,|z| is larger and which
// sign), so the map divides into four diagonal quadrants around the village.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Core.World
{
    public static class ZoneManager
    {
        // Wall footprint half-extents (mirror VillageSceneBuilder WallHalfX/Z =
        // 28/21 at base, ×~1.5 for the poly curtain ≈ 42/33). Inside this box on
        // both axes = the safe Village home zone.
        private const float VillageHalfX = 42f;
        private const float VillageHalfZ = 33f;

        /// <summary>Static region table — danger tier + display name per RegionId.</summary>
        public static readonly IReadOnlyDictionary<RegionId, RegionZone> Regions =
            new Dictionary<RegionId, RegionZone>
            {
                { RegionId.Village,    new RegionZone(RegionId.Village,    "Elarion",    0, "Centre") },
                { RegionId.Goldfields, new RegionZone(RegionId.Goldfields, "Goldfields", 1, "East")   },
                { RegionId.Stoneback,  new RegionZone(RegionId.Stoneback,  "Stoneback",  2, "West")   },
                { RegionId.Mirewood,   new RegionZone(RegionId.Mirewood,   "Mirewood",   3, "South")  },
                { RegionId.Ashwood,    new RegionZone(RegionId.Ashwood,    "Ashwood",    4, "North")  },
            };

        /// <summary>Classify a world position into its <see cref="RegionId"/>.</summary>
        public static RegionId GetZone(Vector3 worldPos)
        {
            // Inside the wall footprint on BOTH axes → the safe home zone.
            if (Mathf.Abs(worldPos.x) <= VillageHalfX && Mathf.Abs(worldPos.z) <= VillageHalfZ)
                return RegionId.Village;

            // Outside: pick the region by the dominant axis. Normalise each axis by
            // its village half-extent so the diagonal split is fair (a point just
            // past the short Z wall doesn't beat a point far past the long X wall).
            float nx = worldPos.x / VillageHalfX;
            float nz = worldPos.z / VillageHalfZ;

            if (Mathf.Abs(nx) >= Mathf.Abs(nz))
                return nx >= 0f ? RegionId.Goldfields  // +X East
                                : RegionId.Stoneback;  // -X West
            return nz >= 0f ? RegionId.Ashwood          // +Z North (front line)
                            : RegionId.Mirewood;        // -Z South (toward the Wound)
        }

        /// <summary>The region record for a position (danger tier, name, cardinal).</summary>
        public static RegionZone ZoneAt(Vector3 worldPos) => Regions[GetZone(worldPos)];

        /// <summary>Danger tier of a position (0 Village … 4 Ashwood). Convenience for
        /// raid scaling (WO-143) and crystal-grade gating (WO-144).</summary>
        public static int DangerTierAt(Vector3 worldPos) => Regions[GetZone(worldPos)].DangerTier;
    }
}
