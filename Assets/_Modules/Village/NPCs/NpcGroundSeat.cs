// =============================================================================
// NpcGroundSeat — shared ground-snap for runtime-spawned castle/village NPCs.
// -----------------------------------------------------------------------------
// T-033 ("NPCs seem to be floating"): the three NPC injectors (vendor, companion
// introducer, village townsfolk) used to seat each body's feet onto the
// NavMesh-sampled Y. That Y is the baked navmesh SURFACE, which Unity voxelizes a
// fraction ABOVE the visual floor (cell height + the castle's forced-walkable
// floor planes at y≈0 / 0.12 / 11.5 vs. the visible courtyard tiles at y=0.01).
// Seating to the navmesh Y therefore leaves the model hovering — the float the
// owner flagged.
//
// THE FIX (mirrors the proven SeatOnGroundOnStart centerpiece pattern): raycast
// straight DOWN through the body to the real floor collider and seat the bottom of
// the combined renderer bounds onto THAT surface — robust to the model's pivot
// (some People-pack bodies pivot at center, some at feet) and to the rescale the
// injectors apply. If no floor collider is found below (e.g. a fresh clone with the
// nav-floor planes absent, or a capsule placeholder), it falls back to the supplied
// ground Y so behaviour never gets worse than before.
//
// Pure transform math + Physics — no Core dependency, no scene edit.
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Seats a runtime-spawned NPC body's feet onto the real floor beneath it:
    /// measures the live combined renderer bounds, raycasts down to the floor
    /// collider, and shifts the transform so the bounds bottom rests on the hit
    /// surface (falling back to <paramref name="fallbackGroundY"/> if nothing is hit).
    /// Pivot-agnostic and scale-agnostic by design.
    /// </summary>
    internal static class NpcGroundSeat
    {
        // Max height a raycast floor-hit may sit ABOVE the navmesh ground and still be
        // accepted as a real step/floor. A hit higher than this is a raised BUILDING
        // PLATFORM (e.g. Lumbermill visual raised +1.5m via HubStructureVisualInjector),
        // not the floor the NPC should stand on - reject it and use the navmesh ground.
        // Owner-tunable (felt-verify): raise if a legit step is being rejected.
        private const float AcceptedFloorBandAboveGround = 0.4f;

        // Max depth a raycast floor-hit may sit BELOW the navmesh ground and still be accepted as the
        // real floor. In the merged Main_Castle_Overworld the true courtyard floor voxelizes a few cm
        // under the navmesh sample, so a hit just below navmesh IS the floor; a genuine deep basement
        // (the old -0.55 foundation plane) is still below this band and correctly rejected.
        // Owner 2026-07-10 (data-proven via [Flow:NpcSeat]): raise cautiously if a real floor floats.
        private const float AcceptedFloorBandBelowGround = 0.35f;

        /// <summary>
        /// Drop <paramref name="go"/> so the bottom of its combined renderer bounds
        /// rests on the floor directly below it. Call AFTER any rescale so the bounds
        /// are final. Counters both pivot-at-center float and the navmesh-Y-above-floor
        /// hover. <paramref name="fallbackGroundY"/> is used only when no floor collider
        /// is found below (typically the NavMesh-sampled Y or 0).
        /// </summary>
        public static float Seat(GameObject go, float fallbackGroundY)
        {
            if (go == null) return 0f;
            if (!TryGetWorldBounds(go, out Bounds b)) return 0f;

            float groundY = ResolveGroundY(go.transform, b, fallbackGroundY, out bool hitFloor, out float rawHitY);
            float gap = b.min.y - groundY;          // >0 = feet float above floor; <0 = sunk in
            // vendor-sink triage (2026-06-23): one-shot trace of the seat decision (remove once stable).
            string state = gap < 0f ? "SUNK-below-floor" : "float-above";
            FlowTrace.Step("NpcSeat", $"'{go.name}': boundsMinY={b.min.y:F2} hitFloor={hitFloor} hitY={rawHitY:F2} fallbackY={fallbackGroundY:F2} -> groundY={groundY:F2} gap={gap:F2} ({state})");
            // Step-in data (owner 2026-07-10 "rca why with step in step out"): the pivot-to-feet offset —
            // if a walking NPC re-floats after its NavMeshAgent takes over, this offset is the suspect.
            FlowTrace.Step("NpcSeat", $"'{go.name}': pivotY={go.transform.position.y:F3} boundsMinY={b.min.y:F3} pivotAboveFeet={(go.transform.position.y - b.min.y):F3}");
            float appliedDeltaY = 0f;
            if (Mathf.Abs(gap) > 0.01f)
            {
                go.transform.position -= new Vector3(0f, gap, 0f);
                appliedDeltaY = -gap;   // the vertical correction that put feet on the floor
            }
            FlowTrace.Step("NpcSeat", $"'{go.name}': seated final pos.y={go.transform.position.y:F2}");
            // Return the correction so an agent-driven (walking) NPC can hold feet on the floor via
            // NavMeshAgent.baseOffset — the agent otherwise re-snaps Y to the (inflated) navmesh each frame.
            return appliedDeltaY;
        }

        /// <summary>Combined world-space renderer bounds of the body + its children.</summary>
        private static bool TryGetWorldBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            var rends = go.GetComponentsInChildren<Renderer>(true);
            bool have = false;
            foreach (var r in rends)
            {
                if (r == null) continue;
                if (!have) { bounds = r.bounds; have = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return have;
        }

        /// <summary>
        /// Floor Y to seat onto: the highest non-self collider hit by a downward ray
        /// from just above the body, ignoring this body's own (trigger) colliders.
        /// Falls back to <paramref name="fallbackGroundY"/> when nothing is hit.
        /// </summary>
        private static float ResolveGroundY(Transform self, Bounds b, float fallbackGroundY, out bool hitFloor, out float rawHitY)
        {
            Vector3 origin = new Vector3(b.center.x, b.max.y + 1f, b.center.z);
            float distance = b.size.y + 5f;
            var hits = Physics.RaycastAll(
                origin, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore);

            float best = float.NegativeInfinity;
            bool found = false;
            foreach (var h in hits)
            {
                // Skip our own colliders so we never seat the body onto itself.
                if (h.collider != null && h.collider.transform.IsChildOf(self)) continue;
                if (h.point.y > best) { best = h.point.y; found = true; }
            }
            hitFloor = found;
            rawHitY = found ? best : fallbackGroundY;
            if (!found)
                return fallbackGroundY;

            // FIX (2026-06-24, data-proven via [Flow:NpcSeat]): the navmesh-sampled ground
            // (fallbackGroundY) is the TRUE floor. The downward ray can punch through it BELOW
            // (castle courtyard foundation plane at y=-0.55 under tiles whose navmesh sits at
            // y=0.06 -> vendor sank ~0.5m underground) OR hit a raised BUILDING PLATFORM ABOVE
            // (Lumbermill visual raised +1.5m -> vendor floated). Both are wrong. Accept the
            // raycast hit ONLY when it is a genuine floor in a small band around the navmesh
            // ground: clamp it to [fallbackGroundY, fallbackGroundY + AcceptedFloorBandAboveGround].
            //  - hit BELOW ground (basement): clamp UP to fallbackGroundY (preserves the -0.55 fix).
            //  - hit far ABOVE ground (building platform): reject, use fallbackGroundY.
            // Accept a floor hit slightly BELOW the navmesh Y as the real floor (merged
            // Main_Castle_Overworld: true floor ~y0.00 sits under a navmesh sampled at ~0.08-0.44,
            // so the old [fallbackGroundY, +band] clamp rejected the REAL floor and floated every
            // NPC). A genuine deep basement (old -0.55 plane) is below this band and still rejected.
            float floorBand = fallbackGroundY - AcceptedFloorBandBelowGround;
            float ceiling   = fallbackGroundY + AcceptedFloorBandAboveGround;
            // Out of band → REJECT to the navmesh ground (do NOT clamp to the ceiling). The old
            // Mathf.Clamp pinned an ABOVE-band hit (a raised BUILDING PLATFORM — Lumbermill +1.5m deck,
            // barracks deck) to fallbackGroundY + 0.40, floating the NPC exactly +0.40m. Data-proven
            // 2026-07-10 [Flow:NpcSeat]: BarracksDrillmaster hitY=3.34 -> 0.48, Lumbermill hitY=1.11
            // -> 0.84 (both = navmesh + 0.40). A deep basement below floorBand is likewise rejected.
            if (best < floorBand || best > ceiling)
            {
                FlowTrace.Step("NpcSeat", $"'{self.name}': rejected raw hitY={best:F2} (outside band [{floorBand:F2}..{ceiling:F2}]) -> fallbackY={fallbackGroundY:F2}");
                return fallbackGroundY;
            }
            return best;
        }
    }
}
