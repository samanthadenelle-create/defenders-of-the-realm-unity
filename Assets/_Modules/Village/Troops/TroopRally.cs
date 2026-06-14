// =============================================================================
// TroopRally — the ONE global rally flag for deployed troops (WO-453 Step 4).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The rally verb is a single world point every idle deployed troop walks to.
// TroopController reads it in its foe==null idle branch: with no foe in range, a
// troop that is farther than an arrival epsilon from Point moves toward it; a
// foe in range ALWAYS wins (rally only fills the idle gap, owner-decided default).
//
// Static + nullable so:
//   * the rally state survives across the deploy HUD's lifetime without a wiring
//     ref (every troop reads the same flag, same shape as SceneRouter's statics);
//   * null = "no rally set" (troops idle in place), a set value = the muster point.
//
// RaidDeployController is the ONLY writer (Rally toggle → tap sets Point); it also
// clears Point on scene teardown so a stale rally can't leak into the next raid.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// The single global rally point for deployed troops. Null when no rally is
    /// active (troops idle); set by <see cref="RaidDeployController"/>'s Rally tap.
    /// Read by <see cref="TroopController"/> in its idle branch (foe-in-range wins).
    /// </summary>
    public static class TroopRally
    {
        /// <summary>The world muster point, or null when no rally is set.</summary>
        public static Vector3? Point;

        /// <summary>Clear the rally (troops return to idle). Called on raid teardown.</summary>
        public static void Clear() => Point = null;
    }
}
