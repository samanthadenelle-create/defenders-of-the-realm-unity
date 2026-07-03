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
using DeNelle.Core.Diagnostics;

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

        // OWNER F8 2026-07-02 "tree of life STILL reads buried": the derived seat puts the
        // model's LOWEST VERTEX on the floor, but some authored models (the Tripo
        // Tree_Of_Life root FLARE) have geometry that is MEANT to sit above grade — the
        // lowest vertex on the floor buries the visible base. This is an ART judgment no
        // derivation can make, so it is an AUTHORED offset, applied AFTER the derived seat.
        [Tooltip("Authored per-model lift (m) added AFTER the derived seat — for models " +
                 "whose lowest geometry (e.g. a root flare) should sit above grade. 0 = none.")]
        [SerializeField] private float _baseLiftOverride = 0f;

        [Tooltip("Optional PlayerPrefs float key that OVERRIDES the authored base lift at " +
                 "runtime (owner felt-tunes without code/rebake). Empty = authored value only.")]
        [SerializeField] private string _baseLiftPrefsKey = "";

        // On Start: measure the VISUAL BASE of the object, find the ground, and shift the
        // transform vertically so that base rests on the ground.
        //
        // OWNER F8 2026-07-02 "tree of life in ground not on ground" (§12 residual of the
        // WO-593 parent-skip fix): the ray now finds the raised floor correctly, but the
        // Tripo Tree_Of_Life fbx's COMBINED RENDERER BOUNDS are skewed (authored/imported
        // AABB extends far below the visible root ball), so seating bounds.min.y onto the
        // floor BURIED the trunk. Renderer.bounds is a conservative transform of the
        // authored mesh AABB — it can lie. The base is now derived in trust order:
        //   1. COLLIDER bounds (physics geometry is authored tight) — when any exists;
        //   2. MESH-VERTEX minimum world Y over readable meshes (exact visible bottom,
        //      immune to skewed authored bounds + rotation-inflated AABBs);
        //   3. combined renderer bounds (the old behavior) as the last resort.
        // The measured values are FlowTrace'd so the next capture PROVES the seat.
        private void Start()
        {
            if (!TryGetWorldBounds(out Bounds b))
            {
                Debug.LogWarning("[SeatOnGround] No renderers found on '" + name +
                                 "' — cannot seat; leaving position unchanged.");
                return;
            }

            float baseY = MeasureVisualBaseY(transform, b, out string baseSource);
            float groundY = ResolveGroundY(b);
            float gap = baseY - groundY;            // >0 = base floats above ground; <0 = sunk in

            // Authored per-model lift, applied AFTER the derived seat (owner F8 2026-07-02:
            // the tree's root FLARE belongs above grade — the derived lowest-vertex seat
            // buried it). PlayerPrefs key (when set) lets the owner felt-tune live.
            // NOTE: the AutoPilot PROP_SEATING oracle tolerates |base - floor| <= 0.75m
            // (AutoPilotProbes.PropSeatTolerance) — lifts above that will flag as FLOATING.
            float baseLift = _baseLiftOverride;
            if (!string.IsNullOrEmpty(_baseLiftPrefsKey))
                baseLift = PlayerPrefs.GetFloat(_baseLiftPrefsKey, _baseLiftOverride);

            if (Mathf.Abs(gap) > 0.0001f || Mathf.Abs(baseLift) > 0.0001f)
            {
                Vector3 p = transform.position;
                p.y -= gap;                          // drop (or lift) so the visual base lands on groundY
                p.y += baseLift;                     // authored lift on top of the derived seat
                transform.position = p;
            }

            // §12 capture: measured bounds vs derived base vs ground, so a headless run proves the seat.
            FlowTrace.Step("Seat",
                $"'{name}' seated: rendererBounds min.y={b.min.y:0.###} max.y={b.max.y:0.###} " +
                $"baseY={baseY:0.###} (source={baseSource}) groundY={groundY:0.###} gap={gap:0.###} " +
                $"baseLift={baseLift:0.###} (authored={_baseLiftOverride:0.###}, prefsKey='{_baseLiftPrefsKey}') " +
                $"-> final pos {transform.position}");
            Debug.Log($"[SeatOnGround] '{name}' final position: {transform.position} " +
                      $"(baseY={baseY:0.###} via {baseSource}, groundY={groundY:0.###})");
        }

        /// <summary>
        /// World Y of the object's VISUAL BASE, robust to skewed renderer bounds.
        /// Trust order: non-trigger collider bounds min → readable mesh-vertex min →
        /// combined renderer bounds min (fallback). Public + static so the AutoPilot
        /// PROP-SEATING oracle measures with the EXACT same derivation the seat uses.
        /// </summary>
        public static float MeasureVisualBaseY(Transform root, Bounds rendererBounds, out string source)
        {
            // 1. Collider bounds: physics geometry is authored tight to the model.
            bool haveCol = false;
            float colMin = float.PositiveInfinity;
            foreach (var c in root.GetComponentsInChildren<Collider>(true))
            {
                if (c == null || c.isTrigger || !c.enabled) continue;
                colMin = Mathf.Min(colMin, c.bounds.min.y);
                haveCol = true;
            }
            if (haveCol) { source = "collider"; return colMin; }

            // 2. Mesh-vertex minimum world Y (exact visible bottom; needs CPU-readable meshes).
            bool haveVtx = false;
            float vtxMin = float.PositiveInfinity;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf != null ? mf.sharedMesh : null;
                if (mesh == null || !mesh.isReadable) continue;
                var m = mf.transform.localToWorldMatrix;
                var verts = mesh.vertices;
                for (int i = 0; i < verts.Length; i++)
                {
                    // World Y of the vertex only (row 1 of the matrix) — cheap one-time scan.
                    float wy = m.m10 * verts[i].x + m.m11 * verts[i].y + m.m12 * verts[i].z + m.m13;
                    if (wy < vtxMin) vtxMin = wy;
                }
                if (verts.Length > 0) haveVtx = true;
            }
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = smr != null ? smr.sharedMesh : null;
                if (mesh == null || !mesh.isReadable) continue;
                var m = smr.transform.localToWorldMatrix;
                var verts = mesh.vertices;
                for (int i = 0; i < verts.Length; i++)
                {
                    float wy = m.m10 * verts[i].x + m.m11 * verts[i].y + m.m12 * verts[i].z + m.m13;
                    if (wy < vtxMin) vtxMin = wy;
                }
                if (verts.Length > 0) haveVtx = true;
            }
            if (haveVtx) { source = "mesh-vertices"; return vtxMin; }

            // 3. Last resort: the (possibly skewed) combined renderer bounds.
            source = "renderer-bounds";
            return rendererBounds.min.y;
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
        /// (ignoring this object's own + its anchor's colliders), else a lift-aware fallback.
        /// </summary>
        private float ResolveGroundY(Bounds b)
        {
            if (!_raycastGround)
                return FallbackGroundY();

            // Cast from a little above the current top, down through the object.
            Vector3 origin = new Vector3(b.center.x, b.max.y + 1f, b.center.z);
            float distance = b.size.y + 5f;
            var hits = Physics.RaycastAll(origin, Vector3.down, distance, _groundMask, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            bool found = false;
            foreach (var h in hits)
            {
                // Skip our OWN subtree + our ANCHOR (direct parent) subtree so we don't seat onto
                // ourselves or the Heart blocker capsule (WO-430: HeartController.EnsureBlocker's
                // height-8 capsule lives on the tree's PARENT anchor — ray hit its top, tree
                // floated 8m).
                // WO-593 (§12 captured proof, Player.log "[SeatOnGround] Tree final position:
                // (-5.77, 12.28, 18.69) (groundY=0)"): the previous fix skipped transform.ROOT —
                // in MainCastle_Hall the tree lives under CastleHubRoot, so the skip excluded the
                // ENTIRE raised castle (plinth top y=liftY, courtyard floor, everything) → no
                // ground was ever found → the magic _groundY=0 fallback seated the tree 3m below
                // the raised floor and hoisted its pivot into the sky. Skipping only self + the
                // direct parent keeps the WO-430 capsule fix AND lets the ray land on the real
                // (raised) floor the tree stands on.
                if (h.collider == null) continue;
                Transform ct = h.collider.transform;
                if (ct.IsChildOf(transform)) continue;                                            // self subtree
                if (transform.parent != null && ct.IsChildOf(transform.parent)) continue;         // anchor (blocker capsule) + siblings on it
                if (h.point.y > best) { best = h.point.y; found = true; }
            }
            return found ? best : FallbackGroundY();
        }

        // WO-593: when no ground is hit, derive the fallback from the AUTHORED anchor height
        // (the builder seats the anchor ON the floor — e.g. the raised castle courtyard at
        // castle.liftY) instead of the magic world-0 _groundY, so a raised base can't strand
        // the seat 3m low. Keeps _groundY for un-anchored (scene-root) users.
        private float FallbackGroundY()
        {
            return transform.parent != null ? transform.parent.position.y : _groundY;
        }
    }
}
