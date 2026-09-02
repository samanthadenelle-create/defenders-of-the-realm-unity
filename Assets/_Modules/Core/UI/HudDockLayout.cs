// =============================================================================
// HudDockLayout (WO-1319) — the ONE piece of arithmetic that decides how N action
// medallions share a bottom-dock track, in REFERENCE PIXELS.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.UI
//
// WHAT BROKE (owner screenshot, echoes-of-elarion.vercel.app build 2026.09.02.352005,
// a tall/narrow desktop browser window). The bottom dock printed its five captions as
// one unbroken run — "BUILDTALKHERO...QUEUE MANAGE" — every word running into its
// neighbour. It was NOT a text bug. The chain, measured:
//
//   HudAreasHost canvas: ScaleWithScreenSize 1080x1920, MatchWidthOrHeight 0.5, so the
//   canvas-LOCAL width is  sqrt(W/H) * sqrt(1080*1920) = sqrt(W/H) * 1440 ref px.
//     landscape 16:9 (W/H 1.778) -> 1920 local px
//     narrow    (W/H 0.60)       -> 1115 local px
//   The ActionBar mount is 0.270..0.730 of that = 46% of the canvas width:
//     landscape -> 883 px      narrow -> 513 px
//   HudKitController.BuildPeacefulDockSlot sliced it into 5 equal FRACTIONS
//   (gap 0.018, width (1 - 6*gap)/5 = 0.1784):
//     landscape -> 157.5 px per slot (fine)     narrow -> 91.5 px per slot
//   91.5 is BELOW ElarionUiKit.MinTouchPx (112), so UiKitMinTouchGuard.LateUpdate
//   (ElarionUiKit.cs) grew every slot symmetrically about its centre by (112-91.5)/2 =
//   10.2 px per side into a gap that was only 9.2 px wide. Five 112 px slots need 560 px
//   and the mount offered 513 — so the slot RECTS overlapped by construction, and the
//   caption (anchored 0.06..0.94 of its slot root) rode the overlap. The clamp is right;
//   the FRACTION AUTHORING is what was wrong. Same failure class as WO-865 / WO-1060.
//
// THE FIX, and it is a LADDER, not a number (WO-1319 acceptance 2 — the degradation is
// authored, never "it happens to fit now"):
//
//   1 COMFORTABLE — the authored fraction already yields >= MinSlotPx. Nothing changes;
//     landscape is byte-for-byte what it was — GapFraction below IS the dock's retired
//     literal (0.018), and the oracle re-derives (1 - 6*0.018)/5 at three shipping
//     landscape sizes to prove the bar the owner signed off did not move.
//   2 EXPAND — the track grows RIGHT, past the mount's right edge, up to the caller's
//     maxTrackWidthPx, until every slot is exactly MinSlotPx. It grows RIGHT ONLY: the
//     mount's LEFT edge (canvas 0.270) is the MoveCluster's right edge, and stealing
//     width there would put the dock under the movement stick — a mis-tap is strictly
//     worse than a narrow bar. Nothing else in the HUD area table occupies the bottom
//     band to the right (ActionRail y>=0.77, QueueStatus y>=0.51).
//   3 TIGHTEN — if the widest allowed track still cannot seat N*MinSlotPx plus gaps, the
//     GAPS collapse toward zero before any slot is allowed under the touch floor.
//   4 OVERFLOW — only when N*MinSlotPx exceeds the entire allowed track (roughly W/H
//     below 0.28 — narrower than 1:3.6, measured) is the floor unreachable in one row. There the
//     solver reports Overflowed, drops the captions to icon-only, and splits the track
//     evenly. The caller LOGS it. Nothing silently overlaps.
//
// ⛔ THIS TYPE HOLDS NO UNITY REFERENCES ON PURPOSE. It is pure arithmetic so the
//    editor oracle (HudDockLayoutRegression) can replay it headlessly at real measured
//    aspects and FAIL the build on an overlap, instead of waiting for a felt-test.
//    Every returned geometry is in canvas-LOCAL (reference) pixels.
// =============================================================================
using UnityEngine;

namespace DeNelle.Core.UI
{
    /// <summary>Solves the medallion track for a bottom action dock (WO-1319). Pure math.</summary>
    public static class HudDockLayout
    {
        /// <summary>The touch floor a slot may never go under (== <see cref="ElarionUiKit.MinTouchPx"/>).
        /// Aliased, never re-typed: one ceiling, one source (CLAUDE.md §8 "never re-hardcode a floor").</summary>
        public const float MinSlotPx = ElarionUiKit.MinTouchPx;   // 112

        /// <summary>The dock's authored outer/inner gap, as a fraction of the MOUNT width. This is
        /// the literal the peaceful dock shipped with (0.018) — kept so tier 1 reproduces the old
        /// landscape geometry exactly rather than "nearly".</summary>
        public const float GapFraction = 0.018f;

        /// <summary>The COMBAT dock's authored gap fraction (six medallions, tighter air). Same
        /// literal it shipped with (0.010) — the combat dock sits in the SAME ActionBar mount and
        /// therefore had the SAME defect one posture away; it is solved by the same ladder.</summary>
        public const float CombatGapFraction = 0.010f;

        /// <summary>Gaps sit on BOTH outer edges as well as between slots, so N slots consume
        /// N+1 gaps. (x0 = gap + i*(w+gap) — the peaceful dock's own arithmetic.)</summary>
        public static int GapCount(int slotCount) { return Mathf.Max(1, slotCount) + 1; }

        /// <summary>A solved dock track, all values in canvas-local (reference) pixels.</summary>
        public struct Solution
        {
            /// <summary>Slots the solution was built for (clamped to >= 1).</summary>
            public int Count;
            /// <summary>The authored mount width the caller measured.</summary>
            public float MountWidthPx;
            /// <summary>Width the dock should actually occupy — never less than the mount.</summary>
            public float TrackWidthPx;
            /// <summary>How far past the mount's RIGHT edge the track grew (>= 0).</summary>
            public float RightExpansionPx;
            /// <summary>Resolved per-slot width.</summary>
            public float SlotWidthPx;
            /// <summary>Resolved gap (outer edges and between slots).</summary>
            public float GapPx;
            /// <summary>False only in tier 4 — the caller renders icon-only faces.</summary>
            public bool ShowCaptions;
            /// <summary>Tier 4: the touch floor is unreachable in one row at this width.</summary>
            public bool Overflowed;
            /// <summary>Which rung of the ladder produced this (1..4), for the trace.</summary>
            public int Tier;

            /// <summary>Left edge of slot <paramref name="index"/>, measured from the track's left edge.</summary>
            public float SlotLeftPx(int index)
            {
                return GapPx + Mathf.Max(0, index) * (SlotWidthPx + GapPx);
            }

            /// <summary>Right edge of slot <paramref name="index"/>, from the track's left edge.</summary>
            public float SlotRightPx(int index) { return SlotLeftPx(index) + SlotWidthPx; }

            public override string ToString()
            {
                return "tier " + Tier + ": " + Count + " slot(s) of " + SlotWidthPx.ToString("0.#") +
                       "px, gap " + GapPx.ToString("0.#") + "px, track " + TrackWidthPx.ToString("0.#") +
                       "px (mount " + MountWidthPx.ToString("0.#") + " + right " +
                       RightExpansionPx.ToString("0.#") + "), captions " + (ShowCaptions ? "on" : "off") +
                       (Overflowed ? ", OVERFLOWED (touch floor unreachable in one row)" : "");
            }
        }

        /// <summary>
        /// Solve the track. <paramref name="mountWidthPx"/> is the authored area mount's width and
        /// <paramref name="maxTrackWidthPx"/> the widest the dock may grow to (mount width plus the
        /// free space to its RIGHT). Never throws; degenerate inputs return a safe tier-4 answer.
        /// </summary>
        public static Solution Solve(int slotCount, float mountWidthPx, float maxTrackWidthPx,
            float gapFraction = GapFraction)
        {
            var s = new Solution();
            s.Count = Mathf.Max(1, slotCount);
            s.MountWidthPx = Mathf.Max(0f, mountWidthPx);
            s.ShowCaptions = true;
            s.Tier = 1;

            float maxTrack = Mathf.Max(s.MountWidthPx, maxTrackWidthPx);
            int gaps = GapCount(s.Count);

            // The gap is authored off the MOUNT, not the track: expanding the dock buys touch
            // width for the faces, never fatter air between them.
            float gap = s.MountWidthPx * Mathf.Clamp(gapFraction, 0f, 0.09f);

            // ── Tier 1: the authored fraction already clears the floor. ──────────────
            float slot = (s.MountWidthPx - gaps * gap) / s.Count;
            if (slot >= MinSlotPx)
            {
                s.TrackWidthPx = s.MountWidthPx;
                s.RightExpansionPx = 0f;
                s.SlotWidthPx = slot;
                s.GapPx = gap;
                return s;
            }

            // ── Tier 2: grow the track RIGHT until every slot is exactly the floor. ──
            s.Tier = 2;
            float required = s.Count * MinSlotPx + gaps * gap;
            s.TrackWidthPx = Mathf.Clamp(required, s.MountWidthPx, maxTrack);
            s.RightExpansionPx = s.TrackWidthPx - s.MountWidthPx;
            slot = (s.TrackWidthPx - gaps * gap) / s.Count;
            if (slot >= MinSlotPx)
            {
                s.SlotWidthPx = slot;
                s.GapPx = gap;
                return s;
            }

            // ── Tier 3: spend the gaps before spending the touch floor. ──────────────
            s.Tier = 3;
            gap = Mathf.Max(0f, (s.TrackWidthPx - s.Count * MinSlotPx) / gaps);
            slot = (s.TrackWidthPx - gaps * gap) / s.Count;
            if (slot >= MinSlotPx)
            {
                s.SlotWidthPx = slot;
                s.GapPx = gap;
                return s;
            }

            // ── Tier 4: one row physically cannot hold the floor. Split evenly, drop
            //    the captions to icon-only, and TELL the caller so it can log it. The
            //    faces still never overlap each other — that is the whole point.
            s.Tier = 4;
            s.Overflowed = true;
            s.ShowCaptions = false;
            s.GapPx = 0f;
            s.SlotWidthPx = s.TrackWidthPx / s.Count;
            return s;
        }

        /// <summary>True when no two solved slots overlap and none starts left of the track or
        /// ends right of it. The property the WO-1319 defect violated; asserted by the oracle.</summary>
        public static bool IsNonOverlapping(Solution s, float epsilonPx = 0.01f)
        {
            if (s.Count < 1 || s.SlotWidthPx <= 0f) return false;
            if (s.SlotLeftPx(0) < -epsilonPx) return false;
            if (s.SlotRightPx(s.Count - 1) > s.TrackWidthPx + epsilonPx) return false;
            for (int i = 1; i < s.Count; i++)
                if (s.SlotLeftPx(i) < s.SlotRightPx(i - 1) - epsilonPx) return false;
            return true;
        }

        /// <summary>
        /// The canvas-LOCAL width, in reference px, that a ScaleWithScreenSize /
        /// MatchWidthOrHeight canvas resolves to at a given surface size. Exposed so the oracle
        /// can replay a real device/browser aspect without a Canvas — it is the same log-space
        /// lerp Unity's CanvasScaler runs (mirrored in ElarionUiKit.PostScaleCanvasHeight).
        /// </summary>
        public static float CanvasLocalWidthPx(float surfaceW, float surfaceH,
            float refW = 1080f, float refH = 1920f, float match = 0.5f)
        {
            if (surfaceW <= 1f || surfaceH <= 1f || refW <= 1f || refH <= 1f) return refW;
            float scale = Mathf.Pow(2f, Mathf.Lerp(
                Mathf.Log(surfaceW / refW, 2f),
                Mathf.Log(surfaceH / refH, 2f),
                Mathf.Clamp01(match)));
            if (scale <= 0.0001f) return refW;
            return surfaceW / scale;
        }
    }
}
