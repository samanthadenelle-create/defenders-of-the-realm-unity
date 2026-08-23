// =============================================================================
// MeshTaper - the TAPER TEST, in ONE place, in the assembly the oracle lives in.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. DeNelle.Editor references THIS assembly and
// not the other way round, so a taper test owned by JewelerPitchSolver (in
// DeNelle.Editor) is unreachable from StructureOrientationOracle. It moved here
// rather than being copied: a second copy of an orientation measure is exactly how
// a gate and a solver come to disagree while both report success. JewelerPitchSolver
// and StructurePoseCapture now both call this one body.
//
// WHAT IT MEASURES, and WHY IT IS THE ONLY SIGNAL THAT TOLD THE TRUTH
//   Owner's idea, 2026-08-22. Horizontal spread of the real vertices in the TOP 20%
//   of the world-Y range, divided by the spread in the BOTTOM 20%. A building tapers
//   - broad base, narrow peak - so upright reads well below 1 and upside-down well
//   above 1.
//
//   The two signals it replaced BOTH LIED that day:
//     * an AABB is IDENTICAL at +90 and -90, so height, footprint and every numeric
//       gate in this repo read the same for upright and upside-down;
//     * the basis-vector test read meshUp(forward) = -1.00 at the jeweler's PROVEN
//       CORRECT pitch, because .up is the wrong axis for a Z-up mesh.
//
// ⚠ IT IS NOT INFALLIBLE EITHER, AND THE FAILURE MODE IS NAMED HERE SO NOBODY
//   REDISCOVERS IT: it assumes a SINGLE tapering silhouette. A wide low COMPOUND
//   (the barracks: two long halls plus a watchtower plus a fence) has its widest
//   spread near the ROOF RIDGES, so it scores 1.28 "upside down" while a rendered
//   frame shows it perfectly upright
//   (docs/ui-evidence/structure-orientation-2026-08-23/barracks__pitch90.png).
//   Never gate a build on the taper ALONE. Require it to AGREE with a second
//   measure, and where the two disagree, report - do not fail.
// =============================================================================

using UnityEngine;

namespace DeNelle.Editor
{
    public static class MeshTaper
    {
        /// <summary>Below this the silhouette is narrow on top: upright.</summary>
        public const float UprightBelow = 0.80f;

        /// <summary>Above this the silhouette is broad on top: upside down.</summary>
        public const float InvertedAbove = 1.25f;

        /// <summary>
        /// Horizontal spread of the mesh in the TOP 20% of its height, divided by the spread in the
        /// BOTTOM 20%. Returns 1.0 when it cannot decide, so an unreadable mesh is reported
        /// "ambiguous" and never becomes a false pass.
        /// </summary>
        public static float Ratio(Transform root, Bounds b)
        {
            if (root == null) return 1f;
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0 || b.size.y <= 0.0001f) return 1f;

            float yMin = b.min.y, ySpan = b.size.y;
            float topCut = yMin + ySpan * 0.80f;
            float botCut = yMin + ySpan * 0.20f;

            // Spread = mean horizontal distance from the bounds centre, so a wide eave and a wide
            // plinth are compared on equal terms.
            double topSum = 0, botSum = 0; int topN = 0, botN = 0;
            Vector3 c = b.center;

            foreach (var mf in filters)
            {
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;
                var verts = mesh.vertices;
                for (int i = 0; i < verts.Length; i++)
                {
                    Vector3 w = mf.transform.TransformPoint(verts[i]);
                    float d = new Vector2(w.x - c.x, w.z - c.z).magnitude;
                    if (w.y >= topCut) { topSum += d; topN++; }
                    else if (w.y <= botCut) { botSum += d; botN++; }
                }
            }

            if (topN == 0 || botN == 0) return 1f;
            double bot = botSum / botN;
            if (bot <= 0.0001) return 1f;
            return (float)((topSum / topN) / bot);
        }
    }
}
