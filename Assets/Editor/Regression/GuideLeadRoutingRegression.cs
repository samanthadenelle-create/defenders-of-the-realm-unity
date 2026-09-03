// =============================================================================
// GuideLeadRoutingRegression [guide-lead-route]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (Editor-only).
//
// WO-1336. PROVEN CAUSE (owner F8 seq 4225, and identically 3604/3605/3606/4162,
// Main_Castle_Overworld):
//
//   [Flow:Pets] guide-lead TICK 'pet-ice-wolf': moved=0.00 m/s over 1.00s ->
//     BODY DID NOT MOVE (carrot written, zero displacement - the write is being
//     ignored downstream). dist=21.09m heroDist=13.00m mode=Defend
//     agent(enabled=True, onNavMesh=True, isStopped=False, velocity=0.00)
//     carrot=(-1.31, 0.08, -17.65) homePost=(-1.31, 0.08, -17.65).
//
// and, in the SAME capture, every downstream gate passing:
//
//   [Flow:Pets] guide-lead LANE ACTIVE on 'pet-ice-wolf': lead PASSES the mode gate
//     (mode=Defend) and the ff.petcombat gate (PetCombat=False) ->
//     MoveToward(_homePost=(-1.31, 0.08, -17.65)) IS being integrated this frame.
//
// The agent was ENABLED, ON the mesh and NOT stopped, the carrot was written every
// frame, MoveToward ran - and the body covered zero metres at a distance that never
// changed. The mechanism is that the pet has NO PATHFINDING: Pet.MoveToward integrates
// the carrot with NavMeshAgent.Move(), which slides the body and CLAMPS that slide to
// the walkable surface. The lead carrot was a dead-straight projection toward the
// anchor, so with a build-mode structure on the line (BaseLayoutLoader gives every
// placed structure a carving NavMeshObstacle) the carrot sat inside the carve, Move()
// clamped the step to nothing, and the guide pressed into the tower face forever.
//
// THE INVARIANT THIS SUITE DEFENDS:
//   (1) the guide-lead carrot is placed along a WALKABLE ROUTE, so it steps AROUND a
//       blocking structure instead of into it - for ANY structure, since the route is
//       whatever the live navmesh says (the town is player-built and movable); and
//   (2) it degrades to the historical straight-line projection on open ground, so the
//       escort's feel on a clear route is unchanged; and
//   (3) the runtime seam that supplies the route (NavMesh.CalculatePath), the
//       unreachable-anchor snap, and the no-progress Warn all still exist at source.
//
// WHAT IT PROVES AND HOW:
//   (a) LIVE RULE PROBE - PetHeroLeash.CarrotAlongCorners is a PURE static function, so
//       the real shipped rule is executed against synthetic corner geometry with no
//       navmesh bake required. DeNelle.EditorRegression does not reference DeNelle.Pets,
//       so the call goes through reflection over the loaded assembly (a test-harness
//       lookup, not a bridge-script reflection). A missing type/method FAILS loudly.
//   (b) SOURCE LINT - routing, the anchor snap and the blocked Warn cannot be observed
//       from a pure function; they are pinned at source, comment-stripped.
//
//   THE RED PROOF (the mutation that must fail this suite): revert CarrotAlongCorners to
//   the pre-fix rule `from + normalize(anchor - from) * leadDistance`. Case "dogleg"
//   then yields carrot=(0.85, _, 3.40) instead of (3.50, _, 0.00) - i.e. it aims into
//   the tower that carves the direct line - and the case FAILS. Verified numerically
//   before this suite was written.
//
//   NOT provable here: the wolf visibly walking around the tower - owner felt-verify.
//
// Markers: GUIDE_LEAD_ROUTE_OK / GUIDE_LEAD_ROUTE_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.GuideLeadRoutingRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class GuideLeadRoutingRegression
    {
        private const string LeashSrc = "Assets/_Modules/Pets/PetHeroLeash.cs";

        // Must match PetHeroLeash.LeadDistance - the carrot budget the shipped rule uses.
        private const float LeadDistance = 3.5f;

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("GUIDE_LEAD_ROUTE_OK - " + reason);
            else Debug.LogError("GUIDE_LEAD_ROUTE_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            try
            {
                Case(failures, "dogleg",      () => Case1_RoutesAroundABlocker(failures));
                Case(failures, "straight",    () => Case2_StraightRouteParity(failures));
                Case(failures, "short-route", () => Case3_ShortRouteAimsAtItsEnd(failures));
                Case(failures, "no-route",    () => Case4_NoRouteFallsBackToStraight(failures));
                Case(failures, "seam",        () => Case5_RuntimeSeamsAtSource(failures));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "GUIDE LEAD ROUTE OK - the guide-lead carrot follows the walkable corner " +
                         "polyline (so a carving structure on the route is walked AROUND, not into), " +
                         "keeps straight-line parity on open ground, aims at the route end when the " +
                         "route is shorter than the carrot, falls back to the straight projection when " +
                         "there is no route at all, and the runtime route/anchor-snap/blocked-warn seams " +
                         "are all present at source.";
                return true;
            }
            reason = "guide-lead-route FAIL x" + failures.Count + ": " + string.Join(" | ", failures);
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  Case 1 - THE TICKET: the carrot must route AROUND a blocking structure
        // =====================================================================
        // Geometry: the guide stands at the origin, the gate anchor is at (5,0,20), and a
        // tower carves the direct line. The walkable route doglegs east to (5,0,0) first.
        // A carrot on that route is (3.50, 0, 0.00): straight down the open leg.
        // The pre-fix straight-line rule instead returns (0.85, 0, 3.40) - 3.5m along the
        // line the tower closes, which is exactly the point Move() clamps to zero.
        private static void Case1_RoutesAroundABlocker(List<string> failures)
        {
            Vector3 from = Vector3.zero;
            Vector3 anchor = new Vector3(5f, 0f, 20f);
            var corners = new[] { new Vector3(0f, 0f, 0f), new Vector3(5f, 0f, 0f), new Vector3(5f, 0f, 20f) };

            if (!TryCarrot(failures, corners, corners.Length, from, anchor - from, out Vector3 carrot)) return;

            if (carrot.x < 3.0f || carrot.z > 0.5f)
                failures.Add("[dogleg] the guide-lead carrot is " + Fmt(carrot) + ", not on the walkable dogleg " +
                             "(expected ~(3.50, 0.00) - the whole first leg is open). It is aiming at the " +
                             "straight line to the anchor, which a carving structure closes: that is the WO-1336 " +
                             "stick verbatim (F8 seq 4225, moved=0.00 m/s with an enabled, on-mesh, " +
                             "NOT-stopped agent). Pet.MoveToward has NO pathfinding - NavMeshAgent.Move clamps " +
                             "the slide - so a carrot inside the carve means the guide never moves again.");

            float alongLeg = Vector3.Distance(from, carrot);
            if (Mathf.Abs(alongLeg - LeadDistance) > 0.05f)
                failures.Add("[dogleg] the carrot is " + alongLeg.ToString("0.00") + "m from the guide, not the " +
                             LeadDistance.ToString("0.0") + "m LeadDistance. The carrot must stay further than " +
                             "Pet.ArrivalDamp (1.6m) ahead or the pet brakes and stop-starts all the way to the gate.");
        }

        // =====================================================================
        //  Case 2 - open ground behaves EXACTLY as it always did
        // =====================================================================
        private static void Case2_StraightRouteParity(List<string> failures)
        {
            Vector3 from = Vector3.zero;
            Vector3 anchor = new Vector3(0f, 0f, 20f);
            var corners = new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 20f) };

            if (!TryCarrot(failures, corners, corners.Length, from, anchor - from, out Vector3 carrot)) return;

            Vector3 expected = new Vector3(0f, 0f, LeadDistance);
            if (Vector3.Distance(carrot, expected) > 0.05f)
                failures.Add("[straight] on a CLEAR straight route the carrot is " + Fmt(carrot) + ", expected " +
                             Fmt(expected) + ". Routing must not change the escort's feel where nothing blocks " +
                             "it - the fix is for blocked routes only.");
        }

        // =====================================================================
        //  Case 3 - a route shorter than the carrot aims at its END
        // =====================================================================
        // Otherwise the carrot would be projected past the last reachable corner and back
        // into whatever closed the route - re-creating the wedge at the final metre.
        private static void Case3_ShortRouteAimsAtItsEnd(List<string> failures)
        {
            var corners = new[] { new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(1f, 0f, 1f) };
            if (!TryCarrot(failures, corners, corners.Length, Vector3.zero, new Vector3(1f, 0f, 1f),
                           out Vector3 carrot)) return;

            if (Vector3.Distance(carrot, new Vector3(1f, 0f, 1f)) > 0.01f)
                failures.Add("[short-route] a route totalling 2m (shorter than the " +
                             LeadDistance.ToString("0.0") + "m carrot) produced " + Fmt(carrot) +
                             ", expected its last corner (1.00, 1.00). Projecting past the reachable end aims " +
                             "the guide back into the blockage on the final approach.");
        }

        // =====================================================================
        //  Case 4 - no usable route: the historical straight projection
        // =====================================================================
        // A pet or anchor briefly off the navmesh must not freeze the guide in place; the
        // old behaviour is the safe floor, not a hard stop.
        private static void Case4_NoRouteFallsBackToStraight(List<string> failures)
        {
            var corners = new[] { new Vector3(0f, 0f, 0f) };
            if (!TryCarrot(failures, corners, 1, Vector3.zero, new Vector3(0f, 0f, 10f), out Vector3 carrot))
                return;

            Vector3 expected = new Vector3(0f, 0f, LeadDistance);
            if (Vector3.Distance(carrot, expected) > 0.05f)
                failures.Add("[no-route] with fewer than two corners the carrot is " + Fmt(carrot) + ", expected " +
                             "the straight-line fallback " + Fmt(expected) + ". A momentary off-mesh query must " +
                             "not freeze the guide - degrade to the old rule, never to nothing.");

            if (!TryCarrot(failures, null, 0, Vector3.zero, new Vector3(0f, 0f, 10f), out Vector3 nullCarrot))
                return;
            if (Vector3.Distance(nullCarrot, expected) > 0.05f)
                failures.Add("[no-route] a null corner buffer produced " + Fmt(nullCarrot) + ", expected " +
                             Fmt(expected) + ".");
        }

        // =====================================================================
        //  Case 5 - the runtime seams a pure function cannot show
        // =====================================================================
        private static void Case5_RuntimeSeamsAtSource(List<string> failures)
        {
            if (!File.Exists(LeashSrc))
            {
                failures.Add("[seam] " + LeashSrc + " is missing - the guide-lead seam cannot be verified.");
                return;
            }
            string src = StripComments(File.ReadAllText(LeashSrc));

            if (src.IndexOf("NavMesh.CalculatePath", StringComparison.Ordinal) < 0)
                failures.Add("[seam] " + LeashSrc + " no longer calls NavMesh.CalculatePath - the carrot has no " +
                             "route to follow, so it is a straight-line projection again and the first carving " +
                             "structure on the way to the gate re-wedges the guide (WO-1336).");

            if (src.IndexOf("CarrotAlongCorners(", StringComparison.Ordinal) < 0)
                failures.Add("[seam] " + LeashSrc + " no longer places the lead carrot via CarrotAlongCorners - " +
                             "the routed rule is computed but not used.");

            if (src.IndexOf("NavMesh.SamplePosition(s_leadTarget", StringComparison.Ordinal) < 0)
                failures.Add("[seam] " + LeashSrc + " no longer snaps the lead ANCHOR onto the navmesh. An anchor " +
                             "that sits inside a structure's carve is a destination no agent can ever stand on, " +
                             "and the guide would latch on it forever instead of resolving to a sane nearby point.");

            if (src.IndexOf("guide-lead BLOCKED", StringComparison.Ordinal) < 0)
                failures.Add("[seam] " + LeashSrc + " no longer emits the 'guide-lead BLOCKED' warn. A silent " +
                             "stick is what cost WO-1336 a felt-test; the next one must name itself in one " +
                             "capture (CLAUDE.md sec.12 - instrumentation is PERMANENT, never stripped).");

            if (src.IndexOf("pathStatus=", StringComparison.Ordinal) < 0 ||
                src.IndexOf("anchorOnNavMesh=", StringComparison.Ordinal) < 0)
                failures.Add("[seam] the guide-lead forensics in " + LeashSrc + " no longer report the path " +
                             "status and anchor reachability. Without them a stick reads only as 'moved=0.00' " +
                             "and the closed-route / stopped-agent / partial-path / unreachable-anchor shapes " +
                             "are indistinguishable from outside - exactly the ambiguity this WO removed.");
        }

        // =====================================================================
        //  Harness
        // =====================================================================

        private static bool TryCarrot(List<string> failures, Vector3[] corners, int count,
                                      Vector3 from, Vector3 straightDir, out Vector3 carrot)
        {
            carrot = Vector3.zero;
            MethodInfo rule = ResolveRule();
            if (rule == null)
            {
                failures.Add("[rule] DeNelle.Pets.PetHeroLeash.CarrotAlongCorners(Vector3[], int, Vector3, " +
                             "Vector3, float) could not be resolved in the loaded assemblies - the routed " +
                             "guide-lead carrot is gone, so the founding companion walks into the first " +
                             "structure on its way to the gate again (WO-1336).");
                return false;
            }
            carrot = (Vector3)rule.Invoke(null, new object[] { corners, count, from, straightDir, LeadDistance });
            return true;
        }

        private static MethodInfo ResolveRule()
        {
            Type t = FindType("DeNelle.Pets.PetHeroLeash");
            if (t == null) return null;
            return t.GetMethod("CarrotAlongCorners", BindingFlags.Public | BindingFlags.Static);
        }

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        private static string Fmt(Vector3 v) => "(" + v.x.ToString("0.00") + ", " + v.z.ToString("0.00") + ")";

        /// <summary>Strip // line and /* */ block comments so a lint never matches doc text.</summary>
        private static string StripComments(string src)
        {
            src = Regex.Replace(src, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            src = Regex.Replace(src, @"//[^\r\n]*", string.Empty);
            return src;
        }
    }
}
