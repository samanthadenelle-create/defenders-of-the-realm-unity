// =============================================================================
// WallLayout — the curtain wall's segment layout (shaped rectangle).
// -----------------------------------------------------------------------------
// SUPERSEDES the earlier square port of segments.ts. Per
// docs/avalon-village-layout-spec.md §4 the Elarion curtain wall is a SHAPED
// RECTANGLE — wider east-west than north-south, ~30×24 hex interior — NOT a
// tight defensive square. The owner-directed creative latitude (§2) lets the
// Unity port replace the React square layout entirely.
//
// SHAPE (spec §4.1 + §4.2):
//   * A rectangle, half-extents WallHalfX (E-W) > WallHalfZ (N-S).
//   * One creative variation: a slight BOW-OUT on the SOUTH side that adds a
//     few units of frontage for the orchard / Farm plot (§4.2 option 1).
//   * Four cardinal gates (§4.3) — one centred break per side, the only gaps.
//   * Corners use the KayKit wall_corner pieces; straights use wall_straight.
//   * No diagonal walls — KayKit hex walls are axis-aligned (§4.1).
//
// GEOMETRY MODEL:
//   The perimeter is walked as an ordered ring of CORNER vertices. Between
//   consecutive corners a straight RUN is split into ~SectionLen-long sections
//   with a centred gate gap left on the four cardinal mid-runs. The south side
//   has an extra pair of corners forming the bow-out, so the south "side" is
//   three runs (W-leg, bow face, E-leg) rather than one.
//
// API SHAPE (unchanged so VillageController + VillageSceneBuilder keep working
// by reflection): exposes `Segments` (IReadOnlyList<WallSegmentData>) and
// `Gates` (IReadOnlyList<GateGap>); the WallSegmentData / GateGap structs keep
// their field names (Id / Index / X / Z / Rot / Length / Corner ; Id / Index /
// Direction / Angle / X / Z / Rot). WallThickness / GateHalfWidth constants are
// preserved for the section collider + the builder's placeholder sizing.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// One straight wall section or corner piece on the shaped curtain wall.
    /// Field names match the earlier square layout so the village
    /// MonoBehaviours and the Editor scene builder bind to it unchanged.
    /// </summary>
    [Serializable]
    public struct WallSegmentData
    {
        /// <summary>Stable damage id -- <c>wall-&lt;index&gt;</c>.</summary>
        public string Id;
        /// <summary>Ordinal position in the generated layout.</summary>
        public int Index;
        /// <summary>World X of the section centre.</summary>
        public float X;
        /// <summary>World Z of the section centre.</summary>
        public float Z;
        /// <summary>Y rotation (radians) so the section's long (local X) axis runs along its side.</summary>
        public float Rot;
        /// <summary>Length (world units) of this section along its side.</summary>
        public float Length;
        /// <summary>True for the corner pieces (a short block that turns the wall).</summary>
        public bool Corner;

        /// <summary>World position of the section's footprint centre (Y = 0, ground level).</summary>
        public Vector3 Position => new Vector3(X, 0f, Z);

        /// <summary>Y-axis rotation as a Unity quaternion (degrees converted from the radian <see cref="Rot"/>).</summary>
        public Quaternion Rotation => Quaternion.Euler(0f, Rot * Mathf.Rad2Deg, 0f);
    }

    /// <summary>
    /// One cardinal gate gap centred in a side of the curtain wall.
    /// </summary>
    [Serializable]
    public struct GateGap
    {
        /// <summary>Stable damage id -- <c>gate-0</c> (N) .. <c>gate-3</c> (W).</summary>
        public string Id;
        /// <summary>Index into the cardinal table -- 0 N, 1 E, 2 S, 3 W.</summary>
        public int Index;
        /// <summary>Cardinal direction label -- "north" / "east" / "south" / "west".</summary>
        public string Direction;
        /// <summary>Cardinal heading (radians) from village centre toward this gate's side.</summary>
        public float Angle;
        /// <summary>World X of the gate centre.</summary>
        public float X;
        /// <summary>World Z of the gate centre.</summary>
        public float Z;
        /// <summary>Y rotation (radians) so the gate's span runs along its side.</summary>
        public float Rot;

        /// <summary>World position of the gate centre (Y = 0, ground level).</summary>
        public Vector3 Position => new Vector3(X, 0f, Z);

        /// <summary>Outward facing direction (unit vector) of this gate.</summary>
        public Vector3 OutwardNormal
        {
            get
            {
                switch (Index)
                {
                    case 0: return Vector3.forward; // North
                    case 1: return Vector3.right;   // East
                    case 2: return Vector3.back;    // South
                    default: return Vector3.left;   // West
                }
            }
        }

        /// <summary>Y-axis rotation as a Unity quaternion.</summary>
        public Quaternion Rotation => Quaternion.Euler(0f, Rot * Mathf.Rad2Deg, 0f);
    }

    /// <summary>
    /// Static generator for the shaped curtain wall -- a rectangle (wider E-W
    /// than N-S) with a south bow-out and four centred cardinal gate gaps.
    /// Pure layout math; no MonoBehaviour. Per docs/avalon-village-layout-spec.md
    /// §4. Replaces the earlier square port of <c>segments.ts</c>.
    /// </summary>
    public static class WallLayout
    {
        // ── Curtain-wall dimensions (world units) ────────────────────────────
        // Interior target ~30×24 hex (spec §4.1). KayKit hex pitch is ~1.732u
        // flat-to-flat and 1.5u per row; 30 hex E-W ≈ 52u, 24 hex N-S ≈ 36u.
        // The wall LINE sits a little outside the buildable interior.

        /// <summary>Half the wall's east-west extent (world units). The wall is wider E-W.</summary>
        public const float WallHalfX = 28f;

        /// <summary>Half the wall's north-south extent (world units).</summary>
        public const float WallHalfZ = 21f;

        /// <summary>How far the south side bows OUTWARD (−Z) to frame the orchard / Farm (spec §4.2).</summary>
        public const float SouthBowDepth = 4f;

        /// <summary>Half-width (along X) of the flat face of the south bow-out.</summary>
        public const float SouthBowHalfWidth = 9f;

        /// <summary>Half-width (world units) of a gate opening. Kept from the square layout.</summary>
        public const float GateHalfWidth = 1.4f;

        /// <summary>Half-width of the centred gate GAP left in a run (gate width + seating margin).</summary>
        public const float GateGapHalf = GateHalfWidth + 0.6f;

        /// <summary>Target length of one straight wall section (world units). Runs are split into ~this.</summary>
        public const float SectionLen = 5.2f;

        /// <summary>Thickness (world units) of a wall section box -- its short (radial) axis.</summary>
        public const float WallThickness = 0.62f;

        // ── Cardinal gate table ──────────────────────────────────────────────

        private static readonly string[] GateDirections = { "north", "east", "south", "west" };
        private static readonly string[] GateDamageIds = { "gate-0", "gate-1", "gate-2", "gate-3" };
        private static readonly float[] GateAngles =
        {
            0f,                 // North
            Mathf.PI / 2f,      // East
            Mathf.PI,           // South
            -Mathf.PI / 2f,     // West
        };

        // ── Lazy caches ──────────────────────────────────────────────────────

        private static WallSegmentData[] _segments;
        private static GateGap[] _gates;
        private static Vector2[] _corners;

        /// <summary>
        /// Every wall section and corner piece on the shaped curtain wall.
        /// Computed once and cached.
        /// </summary>
        public static IReadOnlyList<WallSegmentData> Segments
        {
            get
            {
                if (_segments == null) Build();
                return _segments;
            }
        }

        /// <summary>
        /// The four cardinal gate-gap transforms (N / E / S / W), each centred
        /// in its side.
        /// </summary>
        public static IReadOnlyList<GateGap> Gates
        {
            get
            {
                if (_gates == null) Build();
                return _gates;
            }
        }

        /// <summary>The ordered ring of corner vertices (clockwise from NE).</summary>
        public static IReadOnlyList<Vector2> CornerVertices
        {
            get
            {
                if (_corners == null) Build();
                return _corners;
            }
        }

        /// <summary>Count of straight (non-corner) wall sections in the layout.</summary>
        public static int StraightSectionCount
        {
            get
            {
                int n = 0;
                foreach (var s in Segments) if (!s.Corner) n++;
                return n;
            }
        }

        /// <summary>True when a point offset from a run's centre falls inside the centred gate gap.</summary>
        public static bool IsAtGateGap(float offsetFromCenter)
        {
            return Mathf.Abs(offsetFromCenter) < GateGapHalf;
        }

        // ── One ring run between two corner vertices ─────────────────────────

        private struct Run
        {
            public Vector2 A;        // start corner (XZ)
            public Vector2 B;        // end corner (XZ)
            public bool HasGate;     // true if a cardinal gate is centred on this run
            public int GateIndex;    // 0 N / 1 E / 2 S / 3 W when HasGate
        }

        // ── The generator ───────────────────────────────────────────────────

        private static void Build()
        {
            // Corner ring, clockwise starting at the NE corner. The south side
            // bows outward: instead of one straight S edge, it steps down to a
            // flat bow face and back up — adding two extra corner vertices.
            //
            //   NE ─────────────── NW
            //   │                   │
            //   E gate           W gate
            //   │                   │
            //   SE ──┐         ┌── SW
            //        │ bow     │
            //   bowE └──S gate─┘ bowW
            //
            float hx = WallHalfX;
            float hz = WallHalfZ;
            float bowZ = -(hz + SouthBowDepth);
            float bowHX = SouthBowHalfWidth;

            var nw = new Vector2(-hx, hz);
            var ne = new Vector2(hx, hz);
            var se = new Vector2(hx, -hz);
            var sw = new Vector2(-hx, -hz);
            var bowE = new Vector2(bowHX, bowZ);
            var bowW = new Vector2(-bowHX, bowZ);

            _corners = new[] { ne, se, bowE, bowW, sw, nw };

            // Runs, clockwise. Cardinal gates sit on the mid-run of each side.
            //   East side  : NE -> SE          (gate 1 East, centred)
            //   South-E leg: SE -> bowE         (no gate — short connector)
            //   South face : bowE -> bowW       (gate 2 South, centred on the bow)
            //   South-W leg: bowW -> SW         (no gate)
            //   West side  : SW -> NW           (gate 3 West, centred)
            //   North side : NW -> NE           (gate 0 North, centred)
            var runs = new[]
            {
                new Run { A = ne,   B = se,   HasGate = true,  GateIndex = 1 }, // East
                new Run { A = se,   B = bowE, HasGate = false },                // SE leg
                new Run { A = bowE, B = bowW, HasGate = true,  GateIndex = 2 }, // South (bow)
                new Run { A = bowW, B = sw,   HasGate = false },                // SW leg
                new Run { A = sw,   B = nw,   HasGate = true,  GateIndex = 3 }, // West
                new Run { A = nw,   B = ne,   HasGate = true,  GateIndex = 0 }, // North
            };

            var segments = new List<WallSegmentData>();
            var gateSlots = new GateGap[4];
            int index = 0;

            // Corner pieces — one short block at each corner vertex. The corner
            // is rotated so it faces the OUTWARD angle bisector of its two
            // adjacent runs (a KayKit wall_corner mesh turns the wall here).
            int cornerCount = _corners.Length;
            for (int ci = 0; ci < cornerCount; ci++)
            {
                Vector2 c = _corners[ci];
                Vector2 prev = _corners[(ci - 1 + cornerCount) % cornerCount];
                Vector2 next = _corners[(ci + 1) % cornerCount];
                // Direction toward each neighbour; the outward bisector is the
                // negative of their sum (pointing away from the interior).
                Vector2 toPrev = (prev - c).normalized;
                Vector2 toNext = (next - c).normalized;
                Vector2 outward = -(toPrev + toNext);
                float cornerRot = outward.sqrMagnitude > 0.0001f
                    ? Mathf.Atan2(outward.x, outward.y)
                    : 0f;
                segments.Add(new WallSegmentData
                {
                    Id = "wall-" + index,
                    Index = index,
                    X = c.x,
                    Z = c.y,
                    Rot = cornerRot,
                    Length = WallThickness + 0.55f,
                    Corner = true,
                });
                index++;
            }

            // Straight runs — split each run into sections, leaving a centred
            // gate gap on the four cardinal runs.
            foreach (var run in runs)
            {
                Vector2 a = run.A;
                Vector2 b = run.B;
                Vector2 delta = b - a;
                float runLen = delta.magnitude;
                Vector2 dir = delta / runLen;            // along-run unit dir
                float rot = Mathf.Atan2(dir.x, dir.y);   // Y-rotation: local X -> dir
                Vector2 mid = (a + b) * 0.5f;

                // Pull the run ends in off the corner blocks so straights don't
                // overlap the corner pieces.
                float cornerInset = (WallThickness + 0.55f) * 0.5f;
                float usableLen = runLen - cornerInset * 2f;
                float startS = cornerInset;              // distance from A to first section start

                if (run.HasGate)
                {
                    // Record the gate at the run's centre.
                    int gi = run.GateIndex;
                    gateSlots[gi] = new GateGap
                    {
                        Id = GateDamageIds[gi],
                        Index = gi,
                        Direction = GateDirections[gi],
                        Angle = GateAngles[gi],
                        X = mid.x,
                        Z = mid.y,
                        Rot = rot,
                    };

                    // Two half-runs either side of the centred gate gap.
                    float halfRun = usableLen * 0.5f - GateGapHalf;
                    if (halfRun > 0.5f)
                    {
                        // -side half (from A toward mid)
                        BuildRunSections(segments, ref index, a, dir, rot,
                            startS, halfRun);
                        // +side half (from gate-gap-far edge toward B)
                        float plusStart = startS + halfRun + GateGapHalf * 2f;
                        BuildRunSections(segments, ref index, a, dir, rot,
                            plusStart, halfRun);
                    }
                }
                else
                {
                    // Solid run — no gate.
                    BuildRunSections(segments, ref index, a, dir, rot,
                        startS, usableLen);
                }
            }

            _segments = segments.ToArray();

            // Order the gate list 0 N .. 3 W for stable indexing.
            _gates = new[] { gateSlots[0], gateSlots[1], gateSlots[2], gateSlots[3] };
        }

        /// <summary>
        /// Splits one straight stretch (starting <paramref name="startDist"/>
        /// along the run direction from corner <paramref name="a"/>, of length
        /// <paramref name="stretchLen"/>) into ~<see cref="SectionLen"/> sections
        /// and appends a <see cref="WallSegmentData"/> for each.
        /// </summary>
        private static void BuildRunSections(List<WallSegmentData> outList,
            ref int index, Vector2 a, Vector2 dir, float rot,
            float startDist, float stretchLen)
        {
            if (stretchLen <= 0.05f) return;
            int sectionCount = Mathf.Max(1, Mathf.RoundToInt(stretchLen / SectionLen));
            float segLen = stretchLen / sectionCount;
            for (int s = 0; s < sectionCount; s++)
            {
                float centreDist = startDist + (s + 0.5f) * segLen;
                Vector2 p = a + dir * centreDist;
                outList.Add(new WallSegmentData
                {
                    Id = "wall-" + index,
                    Index = index,
                    X = p.x,
                    Z = p.y,
                    Rot = rot,
                    Length = segLen,
                    Corner = false,
                });
                index++;
            }
        }
    }
}
