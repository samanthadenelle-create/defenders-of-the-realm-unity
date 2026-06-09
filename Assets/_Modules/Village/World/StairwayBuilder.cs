// =============================================================================
// StairwayBuilder — the single source of truth for climbable stair GEOMETRY.
// -----------------------------------------------------------------------------
// A reusable, headless-friendly static construct that generates WIDE, VISIBLE,
// CLIMBABLE stairs as REAL walkable geometry (one step piece per tread, each
// with a MeshCollider ON) so a NavMeshSurface set to "Use Geometry = Physics
// Colliders" bakes the climb directly — no hidden ramp standing in for the
// stairs (the mismatch WO-384 eliminates).
//
// FORMS
//   • Straight  — treads tiled along +Z, fixed depth, rising per step.
//   • Curved    — treads tiled along an ARC (polar placement: each step at a
//                 radius, with an angular increment and a per-step rise). Used
//                 for the castle's grand sweeping stair. Pinned algorithm from
//                 the owner's ProceduralCastleBuilder draft (WO-384), with the
//                 five REQUIRED refinements applied (fit-to-bounds, colliders
//                 ON, wide radius / ≥~8 m walkable band, editor-time only, and
//                 a chord NavMeshLink backup).
//
// FIT-TO-BOUNDS (the project's wall-fit philosophy applied to stairs):
//   The caller passes a measured START (courtyard, y≈0) and END (upper
//   battlement edge, y≈11.5). The builder derives the STEP COUNT and the
//   per-step RISE from the measured height so the TOP TREAD lands FLUSH at the
//   battlement edge and OVERLAPS the upper nav plane — never overshooting past
//   the castle or falling short of it. Nothing is scaled from a single blob;
//   fixed-geometry treads are tiled to fit.
//
// NAVMESH
//   The stairs ARE the nav path (their colliders bake as the walkable surface).
//   As a first-class reliability backup the builder optionally drops ONE
//   NavMeshLink across the CHORD (start→end) — links are straight while the
//   stair may curve, so the baked surface is primary and the chord-link is the
//   safety net. NO runtime NavMeshSurface bake is done here — the host scene's
//   editor-baked, persisted navmesh covers the surface (the castle re-bakes via
//   CastleHubBuilder.BatchAddFloorAndBakeCastle). Runtime baking is the right
//   pattern only for dynamically-spawned zones.
//
// This is a catalog-ready construct: it aligns with the StructureFactory thesis
// ("one factory builds authored content AND player base-build AND enemy camps")
// and is reusable for other castles, the base-build catalog, and enemy camps.
// =============================================================================

using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace DeNelle.Village
{
    /// <summary>Stair layout shape. Curved = wide polar sweep; Straight = linear run.</summary>
    public enum StairwayForm
    {
        Straight,
        Curved,
    }

    /// <summary>
    /// Stateless generator for climbable stair geometry (treads + MeshColliders),
    /// optional cosmetic railings, and an optional chord NavMeshLink. The single
    /// source of truth consumed by CastleHubBuilder and StairwayStructure.
    /// </summary>
    public static class StairwayBuilder
    {
        /// <summary>Tunable parameters for one stairway. Sensible WO-384 defaults.</summary>
        public struct Params
        {
            /// <summary>Straight run or wide curved sweep.</summary>
            public StairwayForm Form;

            /// <summary>World-space bottom anchor (courtyard, y≈0).</summary>
            public Vector3 Start;
            /// <summary>World-space top anchor (battlement edge, y≈11.5). The top tread lands flush here.</summary>
            public Vector3 End;

            /// <summary>Walkable tread width in metres (≥~8 for the multi-agent siege climb).</summary>
            public float Width;
            /// <summary>Target rise per step in metres (~0.3–0.5). Step count is derived from the measured rise.</summary>
            public float StepHeight;
            /// <summary>Tread depth (front-to-back) in metres. Fixed per piece; the run tiles to fit.</summary>
            public float TreadDepth;

            /// <summary>Curved only: total sweep angle in degrees (e.g. 200 ≈ the draft's 1.8 factor; 90 = quarter turn).</summary>
            public float SweepDegrees;
            /// <summary>Curved only: centreline radius of the sweep. Kept ≥ Width so the inner band never pinches below the agent radius.</summary>
            public float Radius;
            /// <summary>Curved only: world-space pivot the arc sweeps around. Defaults derived from Start/End when zero.</summary>
            public Vector3 CurveCenter;
            /// <summary>Curved only: whether CurveCenter was explicitly supplied.</summary>
            public bool HasCurveCenter;

            /// <summary>Add thin cosmetic railing posts along both edges (colliders off).</summary>
            public bool Railings;
            /// <summary>Add ONE NavMeshLink across the chord (start→end) as the reliability backup.</summary>
            public bool ChordNavLink;

            /// <summary>Material applied to the tread renderers (null = default).</summary>
            public Material StepMaterial;

            /// <summary>WO-384 castle grand-stair defaults: wide curved sweep, fit-to-bounds, colliders on, chord link.</summary>
            public static Params CastleGrandStair(Vector3 start, Vector3 end)
            {
                return new Params
                {
                    Form          = StairwayForm.Curved,
                    Start         = start,
                    End           = end,
                    Width         = 9f,        // ≥8 m walkable band for side-by-side climbers
                    StepHeight    = 0.4f,      // within the 0.3–0.5 agent step height
                    TreadDepth    = 0.55f,
                    SweepDegrees  = 110f,      // generous quarter-plus sweep, NOT a tight full spiral
                    Radius        = 12f,       // ≥ Width so the inner edge stays well above the agent radius
                    Railings      = true,
                    ChordNavLink  = true,
                };
            }
        }

        /// <summary>
        /// Builds the stairway under a new child GameObject of <paramref name="parent"/>
        /// and returns that root. Idempotent on name: any existing child with the same
        /// name is destroyed first. Editor-time generation only — no runtime bake.
        /// </summary>
        public static GameObject Build(Transform parent, string rootName, Params p)
        {
            if (parent == null)
            {
                Debug.LogWarning("[StairwayBuilder] Build called with a null parent — skipped.");
                return null;
            }

            // Idempotency — drop any prior generation under the same name.
            var prior = parent.Find(rootName);
            if (prior != null)
            {
                if (Application.isPlaying) Object.Destroy(prior.gameObject);
                else Object.DestroyImmediate(prior.gameObject);
            }

            var root = new GameObject(rootName);
            root.transform.SetParent(parent, false);
            // Pivot the whole structure at its BASE (Start) so it behaves like a prefab you can
            // rotate IN PLACE: select the root, spin Y, and the steps + railings + chord link turn
            // as ONE unit about the courtyard base — instead of swinging across the yard from the
            // castle origin. Steps are placed at world positions below, so this only sets the pivot.
            root.transform.position = p.Start;

            // FIT-TO-BOUNDS: derive the step count from the MEASURED rise so the top tread
            // lands flush at End. Guard against degenerate params.
            float rise = Mathf.Max(0.01f, p.End.y - p.Start.y);
            float stepH = Mathf.Max(0.05f, p.StepHeight);
            int stepCount = Mathf.Max(1, Mathf.RoundToInt(rise / stepH));
            // Recompute the EXACT per-step rise so stepCount * actualStepH == rise (flush top).
            float actualStepH = rise / stepCount;

            if (p.Form == StairwayForm.Curved)
                BuildCurved(root.transform, p, stepCount, actualStepH);
            else
                BuildStraight(root.transform, p, stepCount, actualStepH);

            // Reliability backup: ONE NavMeshLink across the chord (start→end). Links are
            // straight while the stair may curve, so the baked surface is primary and this
            // is the safety net. Hosted at Start; local endpoints relative to it.
            if (p.ChordNavLink)
                AddChordLink(root.transform, p);

            Debug.Log($"[StairwayBuilder] Built '{rootName}' ({p.Form}) — {stepCount} steps, " +
                      $"rise {rise:0.0}m, step {actualStepH:0.00}m, width {p.Width:0.0}m, " +
                      $"top flush at {p.End}. Colliders ON (the stairs ARE the nav path).");
            return root;
        }

        // -------------------------------------------------------------------------
        // Straight form — treads tiled along the START→END horizontal direction.
        // -------------------------------------------------------------------------
        private static void BuildStraight(Transform root, Params p, int stepCount, float stepH)
        {
            Vector3 startFlat = new Vector3(p.Start.x, 0f, p.Start.z);
            Vector3 endFlat   = new Vector3(p.End.x,   0f, p.End.z);
            Vector3 dir = endFlat - startFlat;
            float run = dir.magnitude;
            Vector3 fwd = run > 0.001f ? dir / run : Vector3.forward;
            float yaw = Quaternion.LookRotation(fwd, Vector3.up).eulerAngles.y;

            // Tread depth tiles to fill the measured run exactly (fit-to-bounds horizontally too).
            float treadDepth = run / stepCount;

            for (int i = 0; i < stepCount; i++)
            {
                // Centre of tread i along the run; top of tread at (i+1)*stepH above Start.y.
                float along = (i + 0.5f) * treadDepth;
                Vector3 centerFlat = startFlat + fwd * along;
                float topY = p.Start.y + (i + 1) * stepH;
                Vector3 pos = new Vector3(centerFlat.x, topY - stepH * 0.5f, centerFlat.z);

                CreateStep(root, $"Step_{i}", pos, Quaternion.Euler(0f, yaw, 0f),
                           new Vector3(p.Width, stepH, treadDepth), p.StepMaterial);
            }

            if (p.Railings)
            {
                AddStraightRailing(root, p, stepCount, stepH, startFlat, fwd, treadDepth, +1);
                AddStraightRailing(root, p, stepCount, stepH, startFlat, fwd, treadDepth, -1);
            }
        }

        private static void AddStraightRailing(Transform root, Params p, int stepCount, float stepH,
                                                Vector3 startFlat, Vector3 fwd, float treadDepth, int side)
        {
            Vector3 sideDir = Vector3.Cross(Vector3.up, fwd).normalized * side * (p.Width * 0.5f);
            for (int i = 0; i < stepCount; i++)
            {
                float along = (i + 0.5f) * treadDepth;
                Vector3 centerFlat = startFlat + fwd * along + sideDir;
                float topY = p.Start.y + (i + 1) * stepH;
                Vector3 pos = new Vector3(centerFlat.x, topY + 0.5f, centerFlat.z);
                CreateRailingPost(root, $"Rail_{(side > 0 ? "R" : "L")}_{i}", pos);
            }
        }

        // -------------------------------------------------------------------------
        // Curved form — polar placement of treads (the pinned WO-384 algorithm with
        // the required refinements). Each step sits at the centreline Radius, swept
        // by an angular increment, rising actualStepH per step; the whole arc is
        // offset so step 0 starts at the courtyard anchor and the top tread lands
        // flush on the upper battlement edge.
        // -------------------------------------------------------------------------
        private static void BuildCurved(Transform root, Params p, int stepCount, float stepH)
        {
            float radius = Mathf.Max(p.Radius, p.Width); // refinement #3: keep the band wide, never pinch
            float sweep = Mathf.Abs(p.SweepDegrees) < 1f ? 110f : p.SweepDegrees;
            float angleStep = sweep / stepCount;

            // Curve centre: explicit, or derived so step 0 lands at the courtyard Start.
            // We place the pivot "inland" from Start, perpendicular toward End, at Radius.
            Vector3 center = p.HasCurveCenter ? p.CurveCenter : DeriveCurveCenter(p, radius);

            // Start angle = direction from centre to the Start anchor, so step 0 == Start.
            Vector3 toStart = new Vector3(p.Start.x - center.x, 0f, p.Start.z - center.z);
            float startAngle = Mathf.Atan2(toStart.z, toStart.x) * Mathf.Rad2Deg;

            // Sweep sign: turn toward End (choose the direction whose final tread lands nearer End).
            float sign = SweepSign(p, center, startAngle, sweep, radius);

            for (int i = 0; i < stepCount; i++)
            {
                float angle = startAngle + sign * i * angleStep;
                float rad = angle * Mathf.Deg2Rad;
                float x = center.x + Mathf.Cos(rad) * radius;
                float z = center.z + Mathf.Sin(rad) * radius;
                float topY = p.Start.y + (i + 1) * stepH;

                // Tread depth ≈ arc length per step so adjacent treads meet with no gap.
                float arcDepth = Mathf.Max(p.TreadDepth, (Mathf.Abs(angleStep) * Mathf.Deg2Rad) * radius);

                // WO-384 fix: orient so the WIDE axis (Width) spans the band RADIALLY (inner→outer)
                // and the DEPTH axis (arcDepth) follows the arc/climb tangentially. yaw = -angle
                // makes local +X point radially. The prior "+90" rotated every tread 90° off — the
                // wide axis ran ALONG the arc, so the steps looked like scattered bars/posts.
                Quaternion rot = Quaternion.Euler(0f, -angle, 0f);

                Vector3 pos = new Vector3(x, topY - stepH * 0.5f, z);
                CreateStep(root, $"Step_{i}", pos, rot,
                           new Vector3(p.Width, stepH, arcDepth), p.StepMaterial);

                if (p.Railings)
                {
                    // Outer + inner posts at radius ± Width/2 (cosmetic).
                    Vector3 outDir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
                    Vector3 outerPos = new Vector3(center.x + outDir.x * (radius + p.Width * 0.5f), topY + 0.5f,
                                                   center.z + outDir.z * (radius + p.Width * 0.5f));
                    Vector3 innerPos = new Vector3(center.x + outDir.x * (radius - p.Width * 0.5f), topY + 0.5f,
                                                   center.z + outDir.z * (radius - p.Width * 0.5f));
                    CreateRailingPost(root, $"Rail_Out_{i}", outerPos);
                    CreateRailingPost(root, $"Rail_In_{i}", innerPos);
                }
            }
        }

        // Pivot "inland": perpendicular to the Start→End horizontal direction, Radius away,
        // so the arc bows out and step 0 sits on the Start anchor.
        private static Vector3 DeriveCurveCenter(Params p, float radius)
        {
            Vector3 startFlat = new Vector3(p.Start.x, 0f, p.Start.z);
            Vector3 endFlat   = new Vector3(p.End.x,   0f, p.End.z);
            Vector3 chord = endFlat - startFlat;
            Vector3 perp = chord.sqrMagnitude > 0.001f
                ? Vector3.Cross(Vector3.up, chord.normalized)
                : Vector3.right;
            // Bow toward the midpoint side so the sweep wraps between the anchors.
            return startFlat + perp * radius;
        }

        // Pick the sweep direction (±) whose terminal tread lands closest to End (flush top).
        private static float SweepSign(Params p, Vector3 center, float startAngle, float sweep, float radius)
        {
            Vector3 endFlat = new Vector3(p.End.x, 0f, p.End.z);
            float best = float.MaxValue;
            float chosen = 1f;
            foreach (float s in new[] { 1f, -1f })
            {
                float a = (startAngle + s * sweep) * Mathf.Deg2Rad;
                Vector3 term = new Vector3(center.x + Mathf.Cos(a) * radius, 0f, center.z + Mathf.Sin(a) * radius);
                float d = (term - endFlat).sqrMagnitude;
                if (d < best) { best = d; chosen = s; }
            }
            return chosen;
        }

        // -------------------------------------------------------------------------
        // Primitives.
        // -------------------------------------------------------------------------

        // A single climbable tread: a Cube with its MeshCollider ON (refinement #2) so
        // the NavMeshSurface (Physics Colliders) bakes the climb. Renderer stays ON
        // (the stairs are VISIBLE — no hidden ramp).
        private static void CreateStep(Transform parent, string name, Vector3 worldPos,
                                       Quaternion worldRot, Vector3 scale, Material mat)
        {
            var step = GameObject.CreatePrimitive(PrimitiveType.Cube); // Cube = MeshFilter + Renderer + BoxCollider
            step.name = name;
            step.transform.SetParent(parent, false);
            step.transform.position = worldPos;
            step.transform.rotation = worldRot;
            step.transform.localScale = scale;

            // Keep the BoxCollider (physics-collider bake). It is a primitive cube collider,
            // which the NavMeshSurface collects under Use Geometry = Physics Colliders.
            if (mat != null)
            {
                var r = step.GetComponent<MeshRenderer>();
                if (r != null) r.sharedMaterial = mat;
            }
        }

        // Thin cosmetic railing post — renderer ON, collider OFF (visual only, refinement #2).
        private static void CreateRailingPost(Transform parent, string name, Vector3 worldPos)
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = name;
            post.transform.SetParent(parent, false);
            post.transform.position = worldPos;
            post.transform.localScale = new Vector3(0.15f, 1.0f, 0.15f);
            var col = post.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Object.Destroy(col);
                else Object.DestroyImmediate(col);
            }
        }

        // ONE NavMeshLink across the chord, hosted at Start. Snaps both ends onto the
        // navmesh when one exists; otherwise leaves the requested endpoints (the surface
        // bake is the primary path, this is the backup). Bidirectional, default agent 0
        // (shared hero + enemy), width = stair width.
        private static void AddChordLink(Transform root, Params p)
        {
            var go = new GameObject("StairChord_NavMeshLink");
            go.transform.SetParent(root, false);
            go.transform.position = p.Start;

            Vector3 startLocal = Vector3.zero;
            Vector3 endLocal = p.End - p.Start;

            // Snap onto the mesh if it already exists at bake/edit time (best-effort).
            if (NavMesh.SamplePosition(p.Start, out var bHit, 4f, NavMesh.AllAreas))
            {
                go.transform.position = bHit.position;
                startLocal = Vector3.zero;
                endLocal = (NavMesh.SamplePosition(p.End, out var tHit, 4f, NavMesh.AllAreas)
                                ? tHit.position : p.End) - bHit.position;
            }

            var link = go.AddComponent<NavMeshLink>();
            link.startPoint    = startLocal;
            link.endPoint      = endLocal;
            link.width         = p.Width;
            link.bidirectional = true;       // climb up AND walk down
            link.area          = 0;          // default walkable area — matches the bake
            link.agentTypeID   = 0;          // default Humanoid agent (shared hero + enemy)
            link.autoUpdate    = true;
            link.UpdateLink();
        }
    }
}
