// =============================================================================
// PoiRegistry (WO-VFX-POI) — the live set of opt-in POI callout beacons.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// A POI (mine node, harvest site, collector, enemy outpost) self-attaches a
// PoiBeacon, which registers here in OnEnable and unregisters in OnDisable (same
// self-register pattern as ResourceCollectorRegistry). PoiCalloutSystem polls this
// set each frame to drive the near-field node auras + far-field landmark pillars.
// Holds NO VFX itself — it is a pure membership list.
// =============================================================================

using System.Collections.Generic;

namespace DeNelle.Village
{
    /// <summary>Static membership set of the live <see cref="PoiBeacon"/>s. The driver
    /// (<see cref="PoiCalloutSystem"/>) iterates this to place/stop callout VFX. Mirrors
    /// the self-register lifecycle of ResourceCollectorRegistry.</summary>
    public static class PoiRegistry
    {
        private static readonly HashSet<PoiBeacon> s_beacons = new HashSet<PoiBeacon>();

        /// <summary>The live beacons (read-only view for the driver's per-frame scan).</summary>
        public static IReadOnlyCollection<PoiBeacon> All => s_beacons;

        public static void Register(PoiBeacon beacon)
        {
            if (beacon == null) return;
            s_beacons.Add(beacon);
        }

        public static void Unregister(PoiBeacon beacon)
        {
            if (beacon == null) return;
            s_beacons.Remove(beacon);
        }
    }
}
