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
            // Hot path (raids/harvest/spawns call this often) — throttle so the trace
            // proves classification is live without flooding the log (§12 hot-loop rule).
            if (DeNelle.Core.Diagnostics.FlowTrace.Enabled)
                DeNelle.Core.Diagnostics.FlowTrace.Throttle("Zone", "getzone", 1f,
                    $"GetZone(x={worldPos.x:F1},z={worldPos.z:F1}).");

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
        public static RegionZone ZoneAt(Vector3 worldPos)
        {
            var id = GetZone(worldPos);
            if (!Regions.ContainsKey(id))
                DeNelle.Core.Diagnostics.FlowTrace.Fail("Zone", $"ZoneAt: Regions table missing key '{id}' (classified region has no record) — indexer will throw.");
            return Regions[id];
        }

        /// <summary>Danger tier of a position (0 Village … 4 Ashwood). Convenience for
        /// raid scaling (WO-143) and crystal-grade gating (WO-144).</summary>
        public static int DangerTierAt(Vector3 worldPos) => Regions[GetZone(worldPos)].DangerTier;

        // =====================================================================
        // WO-164 — the two-axis difficulty read (depth × danger) + zone graph.
        // The single shared difficulty API: region enemies (WO-155), node
        // settlements (WO-159), and tribes (WO-160) all call ThreatLevel(pos)
        // to scale, instead of hard-coding placeholders. Pure Core, no Village.
        // =====================================================================

        // --- Tunable progression curve (kept here, not scattered as magic
        // numbers at call sites). If a ProgressionConstants SO is later authored,
        // these are the values to migrate; callers keep using the same API. ---

        /// <summary>World-space distance (out past the village wall edge) at which a
        /// region is considered fully "deep" (Depth == 1). Beyond this, depth stays
        /// clamped at 1.</summary>
        private const float RegionDepthSpan = 220f;

        /// <summary>Levels granted purely by a region's danger tier (the tier axis):
        /// <c>ThreatPerTier × dangerTier</c>.</summary>
        private const int ThreatPerTier = 5;

        /// <summary>Max additional levels contributed by depth at the region core
        /// (the depth axis). Depth 0 adds 0, Depth 1 adds this.</summary>
        private const int ThreatDepthBand = 4;

        /// <summary>
        /// How deep into a region a position sits, 0 → 1. 0 at the region's safe edge
        /// (the village wall footprint), 1 once it is <see cref="RegionDepthSpan"/>
        /// metres or more out toward the region core. Village (the safe home zone) is
        /// always 0. Clamped 0..1.
        /// </summary>
        /// <remarks>Measured as the outward distance past the wall footprint along the
        /// dominant axis, normalised by <see cref="RegionDepthSpan"/> — mirrors
        /// <see cref="GetZone"/>'s dominant-axis model. Pure geometry, headless-safe.</remarks>
        public static float Depth(Vector3 worldPos)
        {
            if (GetZone(worldPos) == RegionId.Village)
                return 0f;

            // Outward distance past the wall edge on each axis (0 while inside the box).
            float overX = Mathf.Max(0f, Mathf.Abs(worldPos.x) - VillageHalfX);
            float overZ = Mathf.Max(0f, Mathf.Abs(worldPos.z) - VillageHalfZ);
            // The dominant overrun drives depth (matches the dominant-axis region pick).
            float overrun = Mathf.Max(overX, overZ);
            return Mathf.Clamp01(overrun / RegionDepthSpan);
        }

        /// <summary>
        /// The combined enemy-level a position scales against — the two-axis read.
        /// Combines the region's danger tier with how deep into it the position is:
        /// <c>ThreatPerTier × dangerTier + round(ThreatDepthBand × Depth)</c>.
        /// Village returns 0. Tunable via the constants above (curve, not magic numbers
        /// at the call sites). Pure Core — no Village ref. This is the single shared
        /// difficulty read for WO-155 / WO-159 / WO-160.
        /// </summary>
        public static int ThreatLevel(Vector3 worldPos)
        {
            int tier = DangerTierAt(worldPos);
            if (tier <= 0)
                return 0; // Village / safe home zone.

            int depthBand = Mathf.RoundToInt(ThreatDepthBand * Depth(worldPos));
            return ThreatPerTier * tier + depthBand;
        }

        // --- Zone graph + City/Horde tagging (WO-164) ------------------------

        /// <summary>
        /// The authored zone graph — neighbor adjacency + City/Horde destination per
        /// region. Village is the hub: every outer region borders it (cardinal fan-out),
        /// and adjacent cardinals border each other. Destination is derived from danger
        /// (low → City, high → Horde, Village → Neutral), with this table as the explicit
        /// override point. Returns fresh <see cref="ZoneState"/> instances (Discovered /
        /// Cleared default false) suitable for seeding a new save's <c>GameState.Zones</c>.
        /// </summary>
        public static IReadOnlyList<ZoneState> DefaultZoneGraph()
        {
            return new List<ZoneState>
            {
                // Village is the safe hub — borders all four outer regions.
                new ZoneState(RegionId.Village, NodeType.Neutral,
                    RegionId.Goldfields, RegionId.Stoneback, RegionId.Mirewood, RegionId.Ashwood),

                // Goldfields (E, tier 1, safe) — a friendly City. Borders Village +
                // the two adjacent cardinals (N Ashwood, S Mirewood).
                new ZoneState(RegionId.Goldfields, NodeType.City,
                    RegionId.Village, RegionId.Ashwood, RegionId.Mirewood),

                // Stoneback (W, tier 2) — neutral uplands. Borders Village + N/S cardinals.
                new ZoneState(RegionId.Stoneback, NodeType.Neutral,
                    RegionId.Village, RegionId.Ashwood, RegionId.Mirewood),

                // Mirewood (S, tier 3) — heavy, toward the Wound: Horde staging.
                new ZoneState(RegionId.Mirewood, NodeType.Horde,
                    RegionId.Village, RegionId.Goldfields, RegionId.Stoneback),

                // Ashwood (N, tier 4) — the ruined front line: Horde staging.
                new ZoneState(RegionId.Ashwood, NodeType.Horde,
                    RegionId.Village, RegionId.Goldfields, RegionId.Stoneback),
            };
        }

        /// <summary>
        /// Default City/Horde tag for a region, derived from its danger tier: Village →
        /// Neutral, low danger (tier ≤ 1) → City, high danger (tier ≥ 3) → Horde, mid →
        /// Neutral. The authored <see cref="DefaultZoneGraph"/> may override this; use it
        /// for any region not yet present in a save's zone list.
        /// </summary>
        public static NodeType DefaultDestination(RegionId id)
        {
            int tier = Regions.TryGetValue(id, out var z) ? z.DangerTier : 0;
            if (tier <= 0) return NodeType.Neutral;
            if (tier <= 1) return NodeType.City;
            if (tier >= 3) return NodeType.Horde;
            return NodeType.Neutral;
        }
    }
}
