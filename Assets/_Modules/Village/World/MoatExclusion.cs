// =============================================================================
// MoatExclusion — shared "no enemy in the moat / on the seam" spawn guard.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The castle moat (CastleMoatBuilder) is a FILLED WATER ANNULUS around the castle
// that ALSO hides the RegionGate seam between the castle region and OuterWorld.
// Enemies must NEVER spawn in that water channel or on the seam — a mob standing in
// the water (or straddling the region cut) reads as broken and can strand off-mesh.
//
// Every runtime spawner that picks a position by RANDOM RADIUS around a moving anchor
// (RegionMobSpawner, OverworldEncounterSpawner) routes its candidate through
// IsInMoatBand() and RE-ROLLS on a hit, so nothing lands in the band.
//
// RADII SOURCE — mirrored from CastleMoatBuilder (its consts are PRIVATE; this silo may
// only READ them, not add an accessor there):
//   • CastleMoatBuilder.MoatInnerRadius = 44f  (= CastleHubBuilder.PlinthHalf mirror)
//   • CastleMoatBuilder.MoatOuterRadius = 62f  (owner ruling: ~18m band, 44..62)
// KEEP IN SYNC if that band ever changes. (Cited: CastleMoatBuilder.cs lines 70-72.)
//
// GEOMETRY — the moat mesh is a MITRED SQUARE ring (built about the WORLD ORIGIN: every
// ring vert is positioned relative to (0,0,0) and each crossing is RotateAround(Vector3.
// zero)). Its radius is therefore MAX-NORM (Chebyshev), not Euclidean — so we test the
// max-norm distance to match the actual square water footprint EXACTLY. A plain Euclidean
// test would leave the diagonal CORNERS of the band uncovered (a mob could spawn in the
// corner water). A small bank MARGIN keeps spawns off the wet lip / seam edge too.
// =============================================================================
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Shared spawn-exclusion for the castle moat / RegionGate seam band. Returns true for any
    /// world position inside the moat water annulus (plus a bank margin) so a spawner rejects it.
    /// </summary>
    public static class MoatExclusion
    {
        // MIRROR of CastleMoatBuilder.MoatInnerRadius / MoatOuterRadius (private there — see header).
        public const float MoatInnerRadius = 44f;   // = CastleMoatBuilder.MoatInnerRadius (line 71)
        public const float MoatOuterRadius = 62f;   // = CastleMoatBuilder.MoatOuterRadius (line 72)

        // Keep spawns off the bank / seam lip too (owner spec: ~+/-2m margin around the band).
        public const float BankMargin = 2f;

        // The castle / world centre. CastleMoatBuilder builds the whole ring about the world origin.
        public static readonly Vector3 CastleCentre = Vector3.zero;

        /// <summary>
        /// True when <paramref name="pos"/> lies inside the moat water band (or on its bank/seam
        /// margin) about the castle centre — i.e. a spawner MUST reject it and re-roll. Uses the
        /// MAX-NORM (Chebyshev) radius to match the square water annulus, so diagonal corners are
        /// covered. Cheap: two abs + a max + two compares, no sqrt.
        /// </summary>
        public static bool IsInMoatBand(Vector3 pos)
        {
            float dx = Mathf.Abs(pos.x - CastleCentre.x);
            float dz = Mathf.Abs(pos.z - CastleCentre.z);
            float chebyshev = Mathf.Max(dx, dz);   // max-norm radius — matches the mitred square ring
            return chebyshev >= (MoatInnerRadius - BankMargin)
                && chebyshev <= (MoatOuterRadius + BankMargin);
        }
    }
}
