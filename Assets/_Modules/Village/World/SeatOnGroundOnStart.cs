// =============================================================================
// SeatOnGroundOnStart — drops a scaled centerpiece back onto the ground at Play.
// -----------------------------------------------------------------------------
// Problem (Village2 DEFEND): the visible Heart centerpiece (SM_Tree_Round(Clone),
// scale ~2.769×) sits at raw y=0 but its MESH PIVOT is ABOVE the model's base, so
// at that scale the rendered mesh lifts off the ground and FLOATS. The pivot is in
// the saved mesh, so no transform.position value alone fixes it without re-saving
// the scene.
//
// Fix WITHOUT a scene re-save: on Start, measure the object's combined renderer
// bounds, find the gap between bounds.min.y and the ground, and shift the transform
// down (or up) by that gap so the visible bottom of the mesh sits ON the ground.
// Because this reads the LIVE rendered bounds at runtime, it is robust to the mesh
// pivot, the scale, and any future art swap — no magic offset to keep in sync.
//
// Ground resolution: an optional downward raycast finds real terrain/ground under
// the object; if it hits nothing, falls back to a fixed groundY (0 = village floor).
//
// Self-contained: drop it on the centerpiece. No scene wiring, no Core dependency.
// Village2Playable attaches it during the B-phase wiring (idempotent), so a Village2
// rebuild always re-attaches it; it can also self-find by tag/name if needed.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// On Start, shifts this transform vertically so the bottom of its combined
    /// renderer bounds rests on the ground (a raycast hit, or a fixed ground Y).
    /// Fixes scaled centerpieces whose mesh pivot floats above the model base.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SeatOnGroundOnStart : MonoBehaviour
    {
        [Tooltip("Ground height (world Y) to seat the bounds bottom onto when no " +
                 "raycast ground is found below. 0 = the village floor.")]
        [SerializeField] private float _groundY = 0f;

        [Tooltip("If true, raycast straight down to find real ground under the " +
                 "object and seat onto that instead of the fixed ground Y.")]
        [SerializeField] private bool _raycastGround = true;

        [Tooltip("Layers the ground raycast hits (terrain/floor). Default = everything.")]
        [SerializeField] private LayerMask _groundMask = ~0;

        private void Start()
        {
            if (!TryGetWorldBounds(out Bounds b))
                return; // no renderers — nothing to seat

            float targetGroundY = ResolveGroundY(b);
            float gap = targetGroundY - b.min.y; // +ve = mesh below ground, lift up; -ve = floating, drop
            if (Mathf.Abs(gap) < 0.0005f)
                return; // already seated

            transform.position += new Vector3(0f, gap, 0f);
        }

        /// <summary>Combined world-space renderer bounds of this object + children.</summary>
        private bool TryGetWorldBounds(out Bounds bounds)
        {
            bounds = default;
            var rends = GetComponentsInChildren<Renderer>(true);
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
        /// Ground Y to seat onto: a downward raycast from just above the bounds
        /// (ignoring this object's own colliders), else the fixed _groundY fallback.
        /// </summary>
        private float ResolveGroundY(Bounds b)
        {
            if (!_raycastGround)
                return _groundY;

            // Cast from a little above the current top, down through the object.
            Vector3 origin = new Vector3(b.center.x, b.max.y + 1f, b.center.z);
            float distance = b.size.y + 5f;
            var hits = Physics.RaycastAll(origin, Vector3.down, distance, _groundMask, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            bool found = false;
            foreach (var h in hits)
            {
                // Skip this object's own colliders so we don't seat onto ourselves.
                if (h.collider != null && h.collider.transform.IsChildOf(transform)) continue;
                if (h.point.y > best) { best = h.point.y; found = true; }
            }
            return found ? best : _groundY;
        }
    }
}
