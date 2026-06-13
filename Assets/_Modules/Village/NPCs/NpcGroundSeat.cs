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
        /// <summary>
        /// Drop <paramref name="go"/> so the bottom of its combined renderer bounds
        /// rests on the floor directly below it. Call AFTER any rescale so the bounds
        /// are final. Counters both pivot-at-center float and the navmesh-Y-above-floor
        /// hover. <paramref name="fallbackGroundY"/> is used only when no floor collider
        /// is found below (typically the NavMesh-sampled Y or 0).
        /// </summary>
        public static void Seat(GameObject go, float fallbackGroundY)
        {
            if (go == null) return;
            if (!TryGetWorldBounds(go, out Bounds b)) return;

            float groundY = ResolveGroundY(go.transform, b, fallbackGroundY);
            float gap = b.min.y - groundY;          // >0 = feet float above floor; <0 = sunk in
            if (Mathf.Abs(gap) > 0.01f)
                go.transform.position -= new Vector3(0f, gap, 0f);
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
        private static float ResolveGroundY(Transform self, Bounds b, float fallbackGroundY)
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
            return found ? best : fallbackGroundY;
        }
    }
}
