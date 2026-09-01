// =============================================================================
// GateTraversalInjector (owner on-device 2026-07-15) -- walk THROUGH the gate.
// -----------------------------------------------------------------------------
// OWNER ASK: "on approaching a door, should navigate to the other side of the door."
//
// RCA CONTEXT: the live world is the ONE merged scene Main_Castle_Overworld
// (ff.mergedworld ON). The hero is a NavMeshAgent (moves by transform;
// HeroLocomotion.cs). There is NO gate-traversal system on the merged scene -- the
// old RuntimeRegionGate crossing markers were deleted. The only gate-adjacent
// traversal is HomeReturnPortalInjector's INBOUND return portals at the OUTER
// bridge mouths (r~72). Nothing carries the hero THROUGH a castle gate opening.
//
// FIX: mirror the proven HomeReturnPortalInjector authoring pattern
// ([RuntimeInitializeOnLoadMethod(AfterSceneLoad)] + sceneLoaded re-arm,
// runtime-authored -- NO scene edit, NO navmesh rebake, guarded try/catch that
// never throws into gameplay, idempotent per load, gated behind a FeatureFlags
// flag). At each of the 4 castle gates (N/S/E/W) author a bidirectional NavMeshLink
// across the open arch. When the optional hero warp is enabled, it uses the same
// measured inner/outer anchors:
//   * the gate opening faces OUTWARD on its side; the castle walls sit at ~r=44,
//     the north opening near +Z ~40 (BuildNorthDiag measure; sides symmetric at
//     +/-40 on their axis).
//   * INNER anchor at r=37 (courtyard side), OUTER landing at r=41 (exterior side).
//     Both are NavMesh-seated at author time so agents cross the wall thickness.
//   * a hero walking OUT hits the inner anchor -> warped to the outer landing,
//     FACING OUTWARD; a hero walking IN hits the outer anchor -> warped to the
//     inner landing, FACING INWARD. Bidirectional, so the gate works both ways.
//
// ANTI-BOUNCE: one crossing owns BOTH anchors and re-arms ONLY when the hero is
// clear of BOTH radii (the proven HeroLinkCrossing arm/disarm rule) -- warping ONTO
// the partner anchor never bounces back.
//
// NO OVERLAP with HomeReturnPortalInjector: those OUTER return portals sit at
// r~72; the gate landings stay at r~41, so the two
// systems never fight over the same ground.
//
// WARP API: reuses HeroLocomotion.WarpTo(Vector3, Quaternion?) -- it disables the
// agent, moves the transform, re-Warps the agent onto the destination NavMesh, and
// raises OnTeleported for the follow camera. We never set transform.position
// directly (that would strand the agent off-mesh).
// =============================================================================

using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.World
{
    public static class GateTraversalInjector
    {
        private const string GatePrefix = "GatePassage_";

        // Castle walls ~r=44; gate openings face outward at ~+/-40 on each axis.
        // Inner anchor just inside the arch, outer landing just past the wall -- kept
        // well inside the HomeReturnPortal outer ring (r~72) so the two never fight.
        // Current open-gate geometry is narrower than the retired perimeter: the measured walkable
        // seats are r=37 and r=41. The link bridges that short non-walkable seam without putting a
        // trigger in normal courtyard traffic.
        private const float InnerRadius = 37f;
        private const float OuterRadius = 41f;
        private const float PassageWidth = 3.5f;

        // South 0deg . West 90deg . North 180deg . East 270deg is the locked bridge
        // clone convention (HomeReturnPortalInjector); here we just need the 4 cardinal
        // outward directions, one per gate.
        private static readonly string[]  Sides    = { "North", "South", "East", "West" };
        private static readonly Vector3[] Outward   =
        {
            new Vector3(0f, 0f, 1f),   // North -> +Z
            new Vector3(0f, 0f, -1f),  // South -> -Z
            new Vector3(1f, 0f, 0f),   // East  -> +X
            new Vector3(-1f, 0f, 0f),  // West  -> -X
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SafeBuild();   // also cover the scene already active at app start
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SafeBuild();

        // Never throw out of a sceneLoaded handler (an uncaught throw halts the WebGL player).
        private static void SafeBuild()
        {
            try { BuildGateWarps(); }
            catch (System.Exception e)
            {
                Debug.LogWarning("[GateWarp] gate-warp authoring threw (non-fatal): " + e);
            }
        }

        /// <summary>Author the four paired gate warps on the merged overworld scene.
        /// Public so a test/probe can drive it directly. Idempotent per scene load.</summary>
        public static void BuildGateWarps()
        {
            string active = SceneManager.GetActiveScene().name;
            if (!DeNelle.Core.HubScenes.IsOverworld(active)) return;   // overworld-only authoring

            // Idempotency: a gate-warp set already authored on this scene is left alone.
            var existing = GameObject.Find(GatePrefix + Sides[0]);
            if (existing != null)
            {
                FlowTrace.Step("GateWarp",
                    $"gate passages already authored on '{active}' -- idempotent skip.");
                return;
            }

            using var _ = FlowTrace.Enter("GateWarp", $"BuildGateWarps on '{active}'");

            int built = 0;
            for (int i = 0; i < Sides.Length; i++)
            {
                Vector3 dir     = Outward[i];
                Vector3 inner   = dir * InnerRadius;
                Vector3 outer   = dir * OuterRadius;

                // Seat both anchors ON the baked navmesh so the hero warps onto walkable
                // mesh (WarpTo re-samples within 5m too, but seat here for a clean landing).
                inner = SeatOnMesh(inner, Sides[i], "inner");
                outer = SeatOnMesh(outer, Sides[i], "outer");

                var go = new GameObject(GatePrefix + Sides[i]);
                go.transform.position = inner;   // parked at the inner anchor (presentation-free)

                // Pathfinding agents (enemies and troops) need a REAL graph edge. The visual
                // opening alone cannot reconnect two navmesh regions separated by the baked wall.
                var link = go.AddComponent<NavMeshLink>();
                link.startPoint = Vector3.zero;
                link.endPoint = outer - inner;
                link.width = PassageWidth;
                link.bidirectional = true;
                link.area = 0;
                link.agentTypeID = 0;
                link.autoUpdate = true;
                link.UpdateLink();

                // The player is input-driven (NavMeshAgent.Move), so it cannot auto-traverse a
                // NavMeshLink. Keep the paired crossing, now narrowed to the wall thickness.
                if (DeNelle.Core.FeatureFlags.GateTraversal)
                {
                    var warp = go.AddComponent<GateWarp>();
                    warp.Init(Sides[i], inner, outer, dir);
                }

                built++;
                FlowTrace.Step("GateWarp",
                    $"gate={Sides[i]} passage ONLINE -- hero crossing + NavMeshLink " +
                    $"inner {inner} <-> outer {outer}, width {PassageWidth:F1} (outward {dir}).");
            }

            FlowTrace.Step("GateWarp",
                $"{built}/4 gate passages authored on '{active}' -- hero + enemy traversal wired.");
        }

        private static Vector3 SeatOnMesh(Vector3 pos, string side, string which)
        {
            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 25f, NavMesh.AllAreas))
                return hit.position;

            FlowTrace.Warn("GateWarp",
                $"gate={side} {which} anchor: NavMesh.SamplePosition found no mesh within 25m of {pos} -- " +
                "using the raw anchor (WarpTo re-samples on fire).");
            return pos;
        }
    }

    /// <summary>
    /// One castle gate's PAIRED warp. Owns an INNER anchor (just inside the arch) and an
    /// OUTER anchor (just outside). Polls the hero each frame: entering the inner radius
    /// while walking out warps the hero to the outer landing FACING OUTWARD; entering the
    /// outer radius warps to the inner landing FACING INWARD. Re-arms only when the hero is
    /// clear of BOTH radii, so landing on a partner anchor never bounces back (the proven
    /// HeroLinkCrossing arm/disarm rule). Presentation-free: no renderer, no collider -- it
    /// never perturbs the baked navmesh.
    /// </summary>
    public sealed class GateWarp : MonoBehaviour
    {
        // Trigger reach around each anchor. Small enough that the outer trigger's max reach
        // (OuterRadius + TriggerRadius = ~55.5) stays well inside the HomeReturnPortal ring
        // (r~72), and large enough to catch a NavMeshAgent stepping through in one frame.
        private const float TriggerRadius = 1.5f;

        private string  _side;
        private Vector3 _inner;
        private Vector3 _outer;
        private Vector3 _outward;   // unit XZ direction pointing away from the castle
        private bool    _armed = true;

        private static DeNelle.Village.HeroLocomotion s_hero;   // shared cache across gates

        public void Init(string side, Vector3 inner, Vector3 outer, Vector3 outward)
        {
            _side    = side;
            _inner   = inner;
            _outer   = outer;
            outward.y = 0f;
            _outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.forward;
            _armed   = true;
        }

        private void Update()
        {
            var hero = ResolveHero();
            if (hero == null) return;

            Vector3 here = hero.transform.position;
            bool inInner = HorizDist(here, _inner) <= TriggerRadius;
            bool inOuter = HorizDist(here, _outer) <= TriggerRadius;

            if (!inInner && !inOuter)
            {
                _armed = true;   // clear of both anchors -> ready to fire again
                return;
            }

            if (!_armed) return;

            if (inInner)
            {
                // Walking OUT: carry the hero just past the arch, facing outward.
                hero.WarpTo(_outer, Quaternion.LookRotation(_outward, Vector3.up));
                _armed = false;
                FlowTrace.Step("GateWarp",
                    $"gate={_side} inner->outer warp at {_inner} -> {_outer} (facing outward).");
            }
            else // inOuter
            {
                // Walking IN: carry the hero just inside the arch, facing inward.
                hero.WarpTo(_inner, Quaternion.LookRotation(-_outward, Vector3.up));
                _armed = false;
                FlowTrace.Step("GateWarp",
                    $"gate={_side} outer->inner warp at {_outer} -> {_inner} (facing inward).");
            }
        }

        private static DeNelle.Village.HeroLocomotion ResolveHero()
        {
            if (s_hero != null) return s_hero;
            s_hero = Object.FindFirstObjectByType<DeNelle.Village.HeroLocomotion>();
            return s_hero;
        }

        private static float HorizDist(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
