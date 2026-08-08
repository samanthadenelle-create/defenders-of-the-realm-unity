// =============================================================================
// DungeonMultiLevelRegression — pins the VERTICAL (multi-level) dungeon contract.
// -----------------------------------------------------------------------------
// TWO MODELS LIVE IN THIS TREE AND THIS SUITE GUARDS BOTH, ON PURPOSE.
//
//   THE SHIPPING MODEL (WO-930, 2026-08-08) — "the stairwell is ONE room".
//     One prefab, StairwellRoom, holds a solid lower floor, TWO PARTIAL upper
//     floors with a GAP between them, and a flight that rises through the gap.
//     There is no floor hole, no ceiling shaft, no pair, no vertical socket type
//     and no vertical mate. Height rides the SOCKET (an ordinary Door socket that
//     happens to sit at y = FloorSeparationY), so the composer needs no special
//     case. dg_sunken_vault, dg_bonecrypt, dg_ember_deep and dg_stairwell_probe
//     are all on it.
//
//   THE RETIRED PAIR MODEL — StairDown/StairUp prefabs + StairConnector_* prefabs,
//     joined by a real StairUp<->StairDown vertical mate. WO-930 §5 schedules its
//     deletion. It is NOT deleted yet, and that is deliberate: dg_stair_rig and
//     dg_descent_probe are kept on it as the A/B CONTROL GROUP, and WO-930 §6
//     forbids removing the only working traversal before the replacement is proven.
//
// WHY THIS FILE EXISTED IN THE FIRST PLACE, AND WHY IT WENT BLIND
// Before WO-930 this suite guarded ONLY the pair model — it pinned StairDown.prefab
// and StairUp.prefab and nothing else. On 2026-08-08 the shipping dungeons moved to
// StairwellRoom and this suite stayed GREEN while guarding a model no shipping
// dungeon used. Zero assertions covered the room that is now in every one of them.
//
// That is the SAME failure shape as the bug WO-930 fixed. DungeonBakerChecks.TryMate
// scores a mate on `align = dot(a.Outward, -b.Outward)`. For a VERTICAL pair both
// normals are +/-Y, so align is 1.0 at EVERY yaw — the term is structurally blind to
// the one axis that mattered. The flight was yawed 180 while the openings were not,
// half the stairs in the game pointed at solid floor, and every gate read matesFail=0
// for five days. A check that cannot see the failing axis is not a check.
//
// So the new cases are built to bite on that axis specifically:
//   * [stairwell-yaw] mates a probe to the shipped prefab at ZERO distance and proves
//     that a 90 and a 180 yaw both FAIL on Alignment. Under the pair model that same
//     construction passed at every yaw. This is the case that would have caught WO-927.
//   * [stairwell-slope] derives rise and run from the SHIPPED STEP GEOMETRY (never from
//     a builder constant) and cross-checks it against the ramp collider's own pitch —
//     geometry and walk surface disagreeing IS the WO-927 bug class.
//   * [graphs-converted] reads the graph JSON and goes RED if a shipping dungeon is
//     reverted to the retired prefabs.
//
// DECISION: THE OLD COVERAGE IS QUARANTINED, NOT DELETED.
// The three [legacy-*] cases below guard the pair model. They are kept because the
// code they cover is still LIVE (RoomSocketType.StairUp/StairDown, IsVertical, the 3D
// stair nudge in TryMate, the SEALED_VERTICAL branch) and still LOADED by three graphs
// (dg_stair_rig, dg_descent_probe, dg_starter_loop). Deleting them would leave live
// code with no oracle AND let the A/B control group rot silently, destroying the
// ability to re-run the comparison that proved the new model.
//
//   ⚠ DO NOT DELETE dg_stair_rig OR dg_descent_probe. They are TEST FIXTURES, not
//     stale content. [graphs-converted] asserts they STILL name the retired prefabs
//     and that those prefabs still exist on disk, precisely so a tidy-up cannot
//     remove the control group by accident.
//
//   WHEN the pair model is genuinely deleted (WO-930 §5), delete the three [legacy-*]
//   cases, the ControlGroupGraphs/ControlGroupPrefabs arrays, and the control-group
//   half of [graphs-converted] IN THE SAME COMMIT. They are the thing to delete
//   alongside it — not before.
//
// EVERYTHING IS READ, NEVER RE-TYPED. Metrics come from RoomForgeCanon and
// DungeonBakerChecks; the agent slope cliff comes from the project's own NavMesh
// settings. A copied oracle constant is not an oracle (RoomForgeCanon.cs:5-18) — this
// suite already shipped that exact bug once, with a private `const float WallHeight
// = 2.8f` that silently went false when the builder's walls were raised.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using DeNelle.Dungeons.RoomForge;

namespace DeNelle.Editor.Regression
{
    public static class DungeonMultiLevelRegression
    {
        // ── The SHIPPING model ────────────────────────────────────────────────
        private const string StairwellPrefab = "Assets/Dungeon/Rooms/StairwellRoom.prefab";
        private const string StairwellStem = "StairwellRoom";

        // ── The QUARANTINED pair model (control-group fixtures — see header) ──
        private const string RoomsFolder = "Assets/Dungeon/Rooms";
        private const string StairDownPrefab = RoomsFolder + "/StairDown.prefab";
        private const string StairUpPrefab = RoomsFolder + "/StairUp.prefab";

        // ── Graph data. BOTH copies are read: a half-converted dual copy is a real
        //    failure mode and reading only one of them cannot see it.
        private const string GraphsResourcesDir = "Assets/Resources/Data/Canonical/dungeon-graphs";
        private const string GraphsStreamingDir = "Assets/StreamingAssets/Data/Canonical/dungeon-graphs";

        /// <summary>Graphs that WO-930 converted. These must be PURE StairwellRoom.</summary>
        private static readonly string[] ConvertedGraphs =
        {
            "dg_sunken_vault", "dg_bonecrypt", "dg_ember_deep", "dg_stairwell_probe",
        };

        /// <summary>
        /// ⚠ THE A/B CONTROL GROUP — deliberately kept on the RETIRED pair model so the
        /// comparison that proved WO-930 (PathComplete vs PathPartial in one bake) can be
        /// re-run. These are FIXTURES. Do not "clean them up".
        /// </summary>
        private static readonly string[] ControlGroupGraphs =
        {
            "dg_stair_rig", "dg_descent_probe",
        };

        /// <summary>
        /// dg_starter_loop is neither: it INSTANTIATES StairUp/StairDown rooms but joins them
        /// with ordinary door edges only — their stair sockets are in NO edge, so they seal
        /// (SEALED_VERTICAL). It is a FLAT loop that borrows two stair-shaped rooms as dressing.
        /// That premise used to live only in a comment on the seal case; it is asserted as data now.
        /// </summary>
        private const string FlatLegacyGraph = "dg_starter_loop";

        /// <summary>Control-group prefab assets that must survive on disk.</summary>
        private static readonly string[] ControlGroupPrefabs =
        {
            "StairDown", "StairUp",
            "StairConnector_Vertical_Down", "StairConnector_Vertical_Up",
            "StairConnector_Left_Down", "StairConnector_Left_Up",
            "StairConnector_Right_Down", "StairConnector_Right_Up",
        };

        /// <summary>
        /// DefaultStairwellRoomBuilder refuses to build above this. It is a PRIVATE const there,
        /// so this is a restatement — and that is deliberate and different in kind from the
        /// WallHeight bug this file used to carry. That was a copied METRIC (the oracle guarded
        /// the number it had copied, so the two moved together and the check evaporated). This is
        /// an independently-held BOUND: if the builder ever raises its own limit, this case goes
        /// RED and forces the conversation instead of following it silently. The 45 deg cliff
        /// underneath it is read from the project, not typed.
        /// </summary>
        private const float BuilderMaxSlopeDeg = 40f;

        /// <summary>Slope margin (deg) demanded below the agent cliff. At the cliff the ramp stops
        /// carving ENTIRELY (DefaultStairConnectorRoomsBuilder.cs:115-117), so "just under" is not
        /// a safe place to sit.</summary>
        private const float SlopeCliffMargin = 3f;

        private static readonly List<GameObject> s_spawned = new List<GameObject>();

        /// <summary>Standalone batch entry — prints the DUNGEON_MULTILEVEL_OK/_FAIL marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("DUNGEON_MULTILEVEL_OK - " + reason);
            else Debug.LogError("DUNGEON_MULTILEVEL_FAIL: " + reason);
        }

        /// <summary>Covenant contract for DataRegression.RunAll ([dungeon-multilevel]). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                // ── The SHIPPING model (WO-930) ──────────────────────────────
                Case(failures, "stairwell-shape", () => CaseA_StairwellShape(failures));
                Case(failures, "stairwell-no-shaft", () => CaseB_NoShaftNoHole(failures));
                Case(failures, "stairwell-sockets", () => CaseC_FourDoorSocketsTwoLevels(failures));
                Case(failures, "stairwell-slope", () => CaseD_DerivedSlope(failures, notes));
                Case(failures, "stairwell-walk", () => CaseE_WalkSurfaceIsTheRamp(failures));
                Case(failures, "stairwell-yaw", () => CaseF_AlignmentStillBitesOnYaw(failures));
                Case(failures, "stairwell-meta", () => CaseG_StairwellDeclaresItsFootprint(failures, notes));
                Case(failures, "graphs-converted", () => CaseH_ShippingGraphsUseTheStairwell(failures));

                // ── Shared: still load-bearing under BOTH models ─────────────
                Case(failures, "floor-drop", () => CaseI_FloorSeparationClearsAFloor(failures));
                Case(failures, "stack-not-overlap", () => CaseJ_StackIsNotOverlap(failures));
                Case(failures, "door-planar", () => CaseK_DoorsKeepPlanarNudge(failures));

                // ── QUARANTINED: the retired pair model, kept for the control group ──
                Case(failures, "legacy-oppose", () => CaseL_LegacyStairSocketsOppose(failures));
                Case(failures, "legacy-prefab-poses", () => CaseM_LegacyPrefabsCarryThePoses(failures));
                Case(failures, "legacy-vertical-seal", () => CaseN_LegacyUnmatedStairSealsInvisibly(failures));
            }
            finally
            {
                Cleanup();
            }

            string noteStr = notes.Count > 0 ? " [notes: " + string.Join("; ", notes) + "]" : "";
            if (failures.Count == 0)
            {
                reason = "DUNGEON-MULTILEVEL OK - 14/14 cases pass (WO-930 stairwell: solid full lower " +
                         "floor topped at y=0, TWO partial upper floors with a gap, no shaft/hole, four " +
                         "Door sockets at two levels " +
                         $"(0 and {DungeonBakerChecks.FloorSeparationY:0.#}u), derived slope under " +
                         $"{BuilderMaxSlopeDeg:0.#} deg agreeing with the ramp pitch, ramp cube carries " +
                         "the collider and the steps do not, alignment still bites on yaw, shipping " +
                         "graphs converted; shared: floor separation clears " +
                         $"{RoomForgeCanon.FloorOccupiedHeight:0.##}u of occupied floor, stacked rooms " +
                         "are not an overlap, doors keep the planar-only nudge; quarantined pair model " +
                         "+ control group intact)" + noteStr;
                return true;
            }
            reason = "DUNGEON-MULTILEVEL FAIL x" + failures.Count + ": " + string.Join(" | ", failures) + noteStr;
            return false;
        }

        // =====================================================================
        //  CASE A — the stairwell's SHAPE: solid full lower floor, two partial
        //           upper floors, and a real GAP between them
        // =====================================================================
        //
        //  PROVE IT BITES: in DefaultStairwellRoomBuilder.BuildUpperFloors, replace the two
        //  AddBox calls with a single full-width slab (scale.x = hx * 2), re-run
        //  "Defenders/Dungeon/Build Stairwell Room Prefab", and this case must report
        //  "expected exactly 2 upper floor piece(s)" and the lost gap.
        //
        private static void CaseA_StairwellShape(List<string> failures)
        {
            var root = LoadStairwell(failures);
            if (root == null) return;

            // Footprint is DERIVED from the shell, not declared: Wall_N spans the long axis and
            // sits at +hz. Everything below compares against what the room was actually built to.
            var wallN = FindChild(root, "Wall_N");
            if (wallN == null)
            {
                failures.Add("[stairwell-shape] StairwellRoom has no 'Wall_N' - the shell was not built, " +
                             "or the perimeter naming changed and every derived metric below is unmeasurable");
                return;
            }
            float fx = wallN.localScale.x;
            float fz = Mathf.Abs(wallN.localPosition.z) * 2f;
            if (fx <= 0.01f || fz <= 0.01f)
            {
                failures.Add($"[stairwell-shape] degenerate derived footprint {fx:0.##}x{fz:0.##} from Wall_N");
                return;
            }

            // ── LOWER FLOOR: solid, FULL footprint, top face at local y = 0. This is the contract
            //    EVERY other room honours, and it is exactly what lets an ordinary socket mate to it.
            var lower = FindChild(root, "Floor_Lower");
            if (lower == null)
            {
                failures.Add("[stairwell-shape] StairwellRoom has no 'Floor_Lower' - the stair has nothing to land ON");
            }
            else
            {
                float top = lower.localPosition.y + lower.localScale.y * 0.5f;
                if (Mathf.Abs(top) > 0.01f)
                    failures.Add($"[stairwell-shape] Floor_Lower top face is at y={top:0.###}, expected 0. " +
                                 "y=0 is the shared floor plane every room in the kit is built to; a stairwell " +
                                 "that breaks it puts a step in every doorway it opens into.");
                if (Mathf.Abs(lower.localScale.y - RoomForgeCanon.FloorSlabThickness) > 0.01f)
                    failures.Add($"[stairwell-shape] Floor_Lower slab is {lower.localScale.y:0.###}u thick, " +
                                 $"canon FloorSlabThickness is {RoomForgeCanon.FloorSlabThickness:0.###}u");
                if (Mathf.Abs(lower.localScale.x - fx) > 0.01f || Mathf.Abs(lower.localScale.z - fz) > 0.01f)
                    failures.Add($"[stairwell-shape] Floor_Lower is {lower.localScale.x:0.##}x{lower.localScale.z:0.##} " +
                                 $"but the shell footprint is {fx:0.##}x{fz:0.##} - the lower floor must be FULL. " +
                                 "A partial lower floor is a floor hole by another name, which is the whole thing " +
                                 "WO-930 removed.");
                if (lower.GetComponent<Collider>() == null)
                    failures.Add("[stairwell-shape] Floor_Lower has no Collider - it is not SOLID, so nothing " +
                                 "carves navmesh there and the flight lands on nothing");
            }

            // ── UPPER LEVEL: TWO partial floors with a GAP. NOT one full floor.
            var uppers = CollectChildren(root, "Floor_Upper");
            if (uppers.Count != 2)
            {
                failures.Add($"[stairwell-shape] expected exactly 2 upper floor piece(s) named 'Floor_Upper*', " +
                             $"found {uppers.Count}. The upper level is TWO PARTIAL floors with a gap between " +
                             "them - that gap IS the stairwell void, and it is the reason no hole has to be cut " +
                             "through anything. One full slab re-introduces the shaft this design deleted.");
                return;
            }

            float sep = DungeonBakerChecks.FloorSeparationY;
            float combined = 0f;
            foreach (var u in uppers)
            {
                float top = u.localPosition.y + u.localScale.y * 0.5f;
                if (Mathf.Abs(top - sep) > 0.01f)
                    failures.Add($"[stairwell-shape] '{u.name}' top face is at y={top:0.###}, expected " +
                                 $"FloorSeparationY {sep:0.###} - the upper walk surface and the upper SOCKETS " +
                                 "must sit on the same plane or the mate lands you inside the slab");
                if (Mathf.Abs(u.localScale.y - RoomForgeCanon.FloorSlabThickness) > 0.01f)
                    failures.Add($"[stairwell-shape] '{u.name}' slab is {u.localScale.y:0.###}u thick, canon is " +
                                 $"{RoomForgeCanon.FloorSlabThickness:0.###}u");
                if (u.GetComponent<Collider>() == null)
                    failures.Add($"[stairwell-shape] '{u.name}' has no Collider - the upper level is not walkable, " +
                                 "so an upper-level socket opens onto nothing");
                combined += u.localScale.x;
            }

            var ordered = uppers.OrderBy(t => t.localPosition.x).ToList();
            float leftMax = ordered[0].localPosition.x + ordered[0].localScale.x * 0.5f;
            float rightMin = ordered[1].localPosition.x - ordered[1].localScale.x * 0.5f;
            float gap = rightMin - leftMax;
            if (gap <= 0.01f)
                failures.Add($"[stairwell-shape] the two upper floors meet or overlap (gap {gap:0.###}u) - there " +
                             "is no void for the flight to rise through, so it would need a ceiling shaft cut " +
                             "for it, which is the retired model");
            if (combined >= fx - 0.01f)
                failures.Add($"[stairwell-shape] the upper floors cover {combined:0.##}u of a {fx:0.##}u " +
                             "footprint - that is a FULL upper floor, not two partial ones");
        }

        // =====================================================================
        //  CASE B — NO floor hole and NO ceiling shaft. Their ABSENCE is the point.
        // =====================================================================
        //
        //  The pair model needed three things in two prefabs to agree: a hole cut in one room's
        //  floor, a shaft cut in another's ceiling, and a flight in a third frame. Nothing
        //  enforced that agreement and on 2026-08-08 it was measured broken. Here there is
        //  nothing to agree WITH - so the absence has to be asserted, or it will creep back in
        //  as a "small fix" the first time something does not line up.
        //
        //  PROVE IT BITES: add one Rect to RoomPrefabMeta.floorShafts on the shipped prefab, or
        //  add a child named "Shaft_Ceiling" under StairwellRoom, and re-run.
        //
        private static void CaseB_NoShaftNoHole(List<string> failures)
        {
            var root = LoadStairwell(failures);
            if (root == null) return;

            var meta = root.GetComponent<RoomPrefabMeta>();
            if (meta != null)
            {
                int fs = meta.floorShafts != null ? meta.floorShafts.Count : 0;
                int cs = meta.ceilingShafts != null ? meta.ceilingShafts.Count : 0;
                if (fs != 0 || cs != 0)
                    failures.Add($"[stairwell-no-shaft] StairwellRoom declares {fs} floor shaft(s) and {cs} " +
                                 "ceiling shaft(s). It must declare ZERO of each: the stair rises through the " +
                                 "GAP between the two upper floors, so there is nothing to cut and nothing to " +
                                 "align. A declared shaft here means the pair model's geometry has come back.");
            }

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                string n = t.name;
                if (n.IndexOf("Shaft", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Hole", StringComparison.OrdinalIgnoreCase) >= 0)
                    failures.Add($"[stairwell-no-shaft] StairwellRoom contains a child named '{n}' - the WO-930 " +
                                 "design has no hole and no shaft anywhere in it");
            }

            // A ring/fragmented lower floor is a hole spelled differently, so there must be exactly ONE piece.
            var lowerPieces = CollectChildren(root, "Floor_Lower");
            if (lowerPieces.Count != 1)
                failures.Add($"[stairwell-no-shaft] found {lowerPieces.Count} 'Floor_Lower*' piece(s), expected " +
                             "exactly 1. A floor built as several pieces can carry a permanently open centre - " +
                             "that is precisely how the connector kit shipped a ceiling ring that left every " +
                             "stairwell open to sky, and union-bounds checks cannot see it.");

            // The ceiling roofs the WHOLE footprint - no opening above the flight.
            var wallN = FindChild(root, "Wall_N");
            var ceiling = FindChild(root, "Ceiling");
            if (ceiling == null)
            {
                failures.Add("[stairwell-no-shaft] StairwellRoom has no 'Ceiling' - the room is open to sky (WO-919)");
            }
            else if (wallN != null)
            {
                float fx = wallN.localScale.x;
                float fz = Mathf.Abs(wallN.localPosition.z) * 2f;
                if (Mathf.Abs(ceiling.localScale.x - fx) > 0.01f || Mathf.Abs(ceiling.localScale.z - fz) > 0.01f)
                    failures.Add($"[stairwell-no-shaft] Ceiling is {ceiling.localScale.x:0.##}x" +
                                 $"{ceiling.localScale.z:0.##} over a {fx:0.##}x{fz:0.##} footprint - it must " +
                                 "roof the room COMPLETELY. An undersized ceiling is a shaft.");
            }
        }

        // =====================================================================
        //  CASE C — FOUR sockets, all ordinary Doors, at TWO distinct levels
        // =====================================================================
        //
        //  THIS IS THE PROOF THAT HEIGHT RIDES THE SOCKET. A socket already carries its own
        //  local position INCLUDING Y, and SolveMate solves pos = pPos - rotatedSocket, so an
        //  upper-level door mates through the ORDINARY planar path with no elevation field in
        //  the graph schema and no special case in the composer. The moment one of these
        //  becomes a StairUp/StairDown again, the vertical branch is back and so is WO-927.
        //
        //  PROVE IT BITES: set one AddSocket's type to RoomSocketType.StairUp in
        //  DefaultStairwellRoomBuilder.AddSocket, rebuild the prefab, re-run.
        //
        private static void CaseC_FourDoorSocketsTwoLevels(List<string> failures)
        {
            var root = LoadStairwell(failures);
            if (root == null) return;

            var socks = root.GetComponentsInChildren<RoomSocket>(true);
            if (socks.Length != 4)
                failures.Add($"[stairwell-sockets] StairwellRoom has {socks.Length} socket(s), expected 4 " +
                             "(both ends of the lower floor, both ends of the upper floors)");
            if (socks.Length == 0) return;

            float sep = DungeonBakerChecks.FloorSeparationY;
            int atLower = 0, atUpper = 0;

            foreach (var s in socks)
            {
                if (s == null) continue;

                if (s.type != RoomSocketType.Door)
                    failures.Add($"[stairwell-sockets] socket '{s.id}' is type {s.type}, expected Door. Every " +
                                 "stairwell socket is an ORDINARY door - no vertical socket type is used, which " +
                                 "is exactly why the composer needs no change.");
                if (DungeonBakerChecks.IsVertical(s.type))
                    failures.Add($"[stairwell-sockets] socket '{s.id}' reports IsVertical - it would take the " +
                                 "3D-nudge / vertical-mate branch, the branch WO-930 exists to stop using");

                // Outward must be HORIZONTAL. This is the anti-blindness assertion: a +/-Y normal is
                // what made `align` read 1.0 at every yaw and hid WO-927 for five days.
                Vector3 fwd = s.transform.forward.normalized;
                if (Mathf.Abs(Vector3.Dot(fwd, Vector3.up)) > 0.01f)
                    failures.Add($"[stairwell-sockets] socket '{s.id}' outward {fwd} is not horizontal. A vertical " +
                                 "normal makes TryMate's align term 1.0 at EVERY yaw - structurally blind to the " +
                                 "one axis that matters.");

                float half = RoomForgeCanon.DoorGap * 0.5f;
                if (Mathf.Abs(s.halfWidth - half) > 0.01f)
                    failures.Add($"[stairwell-sockets] socket '{s.id}' halfWidth {s.halfWidth:0.###} != canon " +
                                 $"DoorGap/2 {half:0.###} - a socket that under-reports its width lets the mate " +
                                 "checks accept a join narrower than the door actually is");

                // Grid: sockets sit on a half-cell and on the room axis. A fractional offset gets
                // quantised by the composer's RoundToInt into a WHOLE unit of drift per stairwell.
                Vector3 lp = s.transform.localPosition;
                float halfCell = RoomForgeCanon.Cell * 0.5f;
                if (Mathf.Abs(lp.x / halfCell - Mathf.Round(lp.x / halfCell)) > 0.001f)
                    failures.Add($"[stairwell-sockets] socket '{s.id}' local X {lp.x:0.###} is not a multiple of " +
                                 $"half a canon cell ({halfCell:0.###}) - the composer emits " +
                                 "cell=[round(x),round(y),round(z)] and would quantise a unit of drift per stairwell");
                if (Mathf.Abs(lp.z) > 0.001f)
                    failures.Add($"[stairwell-sockets] socket '{s.id}' local Z is {lp.z:0.###}, expected 0 " +
                                 "(sockets sit on the room's long axis)");

                if (Mathf.Abs(lp.y) < 0.01f) atLower++;
                else if (Mathf.Abs(lp.y - sep) < 0.01f) atUpper++;
                else
                    failures.Add($"[stairwell-sockets] socket '{s.id}' local Y is {lp.y:0.###} - the only two legal " +
                                 $"levels are 0 (lower floor) and FloorSeparationY {sep:0.###} (upper floors). A " +
                                 "third level means the room no longer matches what it mates against.");
            }

            if (atLower != 2 || atUpper != 2)
                failures.Add($"[stairwell-sockets] found {atLower} socket(s) at the lower level and {atUpper} at " +
                             "the upper, expected 2 and 2. TWO DISTINCT LOCAL Y VALUES is the whole claim: it is " +
                             "what proves height rides the socket and the composer needs no special case.");
        }

        // =====================================================================
        //  CASE D — the derived slope, measured off the SHIPPED STEPS
        // =====================================================================
        //
        //  Run is DERIVED, never authored. Nothing here reads a builder constant: rise and run
        //  come out of the step cubes' own positions and scales, so a builder edit that was never
        //  baked into the prefab fails loudly instead of looking fixed.
        //
        //  45 deg is the agent maximum and a carve CLIFF, not a target -
        //  DefaultStairConnectorRoomsBuilder.cs:115-117 records that at 3.0 m of run "the slope
        //  reaches 45.0 deg - exactly the agent maximum, i.e. the ramp stops carving at all".
        //  The cliff is READ from the project's own NavMesh settings, never typed.
        //
        //  The last assertion is the WO-927 one: the ramp's pitch must AGREE with the steps.
        //  WO-927 was geometry and walk surface disagreeing about orientation while every gate
        //  read green. Two independent measurements of the same flight is the only way to see it.
        //
        //  PROVE IT BITES: set DefaultStairwellRoomBuilder.UpperFloorDepth to 8 (run collapses to
        //  6 m, slope 45.0), rebuild the prefab, re-run - the builder itself should refuse, and if
        //  it is bypassed this case must report the slope over the limit and at the cliff.
        //
        private static void CaseD_DerivedSlope(List<string> failures, List<string> notes)
        {
            var root = LoadStairwell(failures);
            if (root == null) return;

            var flight = FindChild(root, "Flight");
            if (flight == null)
            {
                failures.Add("[stairwell-slope] StairwellRoom has no 'Flight' - there is no staircase in the stairwell");
                return;
            }

            var steps = CollectChildren(flight, "Step_");
            if (steps.Count == 0)
            {
                failures.Add("[stairwell-slope] the Flight has no 'Step_*' children - nothing to measure, and a " +
                             "vacuous pass here is how this suite went blind in the first place");
                return;
            }

            float topY = float.NegativeInfinity, botY = float.PositiveInfinity;
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
            foreach (var s in steps)
            {
                topY = Mathf.Max(topY, s.localPosition.y + s.localScale.y * 0.5f);
                botY = Mathf.Min(botY, s.localPosition.y - s.localScale.y * 0.5f);
                minX = Mathf.Min(minX, s.localPosition.x - s.localScale.x * 0.5f);
                maxX = Mathf.Max(maxX, s.localPosition.x + s.localScale.x * 0.5f);
            }
            float rise = topY - botY;
            float run = maxX - minX;
            if (run <= 0.01f)
            {
                failures.Add($"[stairwell-slope] degenerate run {run:0.###}u measured off {steps.Count} step(s)");
                return;
            }

            float sep = DungeonBakerChecks.FloorSeparationY;
            if (Mathf.Abs(botY) > 0.05f)
                failures.Add($"[stairwell-slope] the flight bottoms out at y={botY:0.###}, not 0 - it does not land " +
                             "ON the solid lower floor. The stair landing on the floor rather than through a hole " +
                             "in it is the WO-930 design.");
            if (Mathf.Abs(topY - sep) > 0.05f)
                failures.Add($"[stairwell-slope] the flight tops out at y={topY:0.###}, expected FloorSeparationY " +
                             $"{sep:0.###} - it does not meet the upper level the upper sockets sit on");
            if (Mathf.Abs(rise - sep) > 0.05f)
                failures.Add($"[stairwell-slope] the flight rises {rise:0.###}u, expected FloorSeparationY " +
                             $"{sep:0.###}u - one stairwell must be exactly one floor");

            float slopeDeg = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            if (slopeDeg > BuilderMaxSlopeDeg)
                failures.Add($"[stairwell-slope] derived slope is {slopeDeg:0.#} deg ({rise:0.##}u rise over " +
                             $"{run:0.##}u run), over the {BuilderMaxSlopeDeg:0.#} deg limit. Widen the footprint " +
                             "(CellsX) or shrink UpperFloorDepth in DefaultStairwellRoomBuilder and REBUILD the " +
                             "prefab - a source edit alone changes nothing on disk.");

            // The cliff, read from the project rather than typed.
            float agentSlope = 0f;
            try { agentSlope = UnityEngine.AI.NavMesh.GetSettingsByID(0).agentSlope; }
            catch (Exception ex) { notes.Add("agent slope unreadable (" + ex.GetType().Name + ") - cliff check skipped"); }

            if (agentSlope <= 0f)
            {
                notes.Add("NavMesh agentTypeID 0 reported agentSlope <= 0 - the carve cliff could not be checked");
            }
            else if (slopeDeg > agentSlope - SlopeCliffMargin)
            {
                failures.Add($"[stairwell-slope] derived slope {slopeDeg:0.#} deg is within {SlopeCliffMargin:0.#} " +
                             $"deg of the agent maximum {agentSlope:0.#} deg. That maximum is a CLIFF, not a " +
                             "target: at it the ramp stops carving navmesh ENTIRELY and every descent reports " +
                             "PathPartial with no error anywhere.");
            }

            // ── The WO-927 cross-check: the walk surface must agree with the visual flight.
            var ramp = FindChild(flight, "RampCollider");
            if (ramp == null)
            {
                failures.Add("[stairwell-slope] no 'RampCollider' under Flight - the walk surface cannot be " +
                             "cross-checked against the steps, which is the exact measurement WO-927 lacked");
            }
            else
            {
                float pitch = Mathf.Abs(NormalizeAngle(ramp.localEulerAngles.z));
                if (Mathf.Abs(pitch - slopeDeg) > 0.5f)
                    failures.Add($"[stairwell-slope] the ramp collider is pitched {pitch:0.##} deg but the STEPS " +
                                 $"describe {slopeDeg:0.##} deg. Geometry and walk surface disagreeing about the " +
                                 "flight is the WO-927 bug class verbatim - there, a container was yawed 180 while " +
                                 "the openings were not, and every gate still read matesFail=0.");
                float yaw = Mathf.Abs(NormalizeAngle(ramp.localEulerAngles.y));
                if (yaw > 0.5f)
                    failures.Add($"[stairwell-slope] the ramp collider carries a {yaw:0.##} deg YAW. Nothing in " +
                                 "this room may be yawed: WO-927's root cause was exactly such a rotation applied " +
                                 "after the plan was derived from it.");
            }
        }

        // =====================================================================
        //  CASE E — the ramp IS the walk surface; the steps are decoration
        // =====================================================================
        //
        //  NavMeshSurface collects PhysicsColliders, so a stepped visual rasterises as a saw and
        //  fragments. The proven contract is: visual steps with their colliders DESTROYED, and one
        //  invisible CUBE carrying a BoxCollider on the nose line. NEVER PrimitiveType.Plane - a
        //  plane is single-sided and zero-thickness, and the owner's own diagnosis of WO-927 was
        //  "see steps extend through actual floor ... which is why the plane couldnt make a level".
        //
        //  PROVE IT BITES: pass keepCollider:true on the Step_ AddBox call in
        //  DefaultStairwellRoomBuilder.BuildFlight, rebuild the prefab, re-run.
        //
        private static void CaseE_WalkSurfaceIsTheRamp(List<string> failures)
        {
            var root = LoadStairwell(failures);
            if (root == null) return;

            var flight = FindChild(root, "Flight");
            if (flight == null) { failures.Add("[stairwell-walk] StairwellRoom has no 'Flight'"); return; }

            var cols = flight.GetComponentsInChildren<Collider>(true);
            if (cols.Length != 1)
                failures.Add($"[stairwell-walk] the Flight carries {cols.Length} collider(s), expected exactly 1. " +
                             "Every extra one is a step that will rasterise as a saw tooth and fragment the navmesh - " +
                             "which reads as 'the stairs are not walkable' with nothing in any log.");

            var ramp = FindChild(flight, "RampCollider");
            if (ramp == null)
            {
                failures.Add("[stairwell-walk] no 'RampCollider' under Flight - there is no walk surface");
            }
            else
            {
                if (ramp.GetComponent<BoxCollider>() == null)
                    failures.Add("[stairwell-walk] RampCollider has no BoxCollider - a MeshCollider or no collider " +
                                 "at all means the flight carves nothing");
                if (ramp.GetComponent<MeshRenderer>() != null)
                    failures.Add("[stairwell-walk] RampCollider still has its MeshRenderer - it must be invisible " +
                                 "but solid, or the player sees a slab lying over the steps");

                var mf = ramp.GetComponent<MeshFilter>();
                string mesh = mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "<none>";
                if (mesh != "Cube")
                    failures.Add($"[stairwell-walk] RampCollider's mesh is '{mesh}', expected 'Cube'. NEVER " +
                                 "PrimitiveType.Plane: a plane is zero-thickness and single-sided, and the owner " +
                                 "named it by eye as why the flight could not make a level.");
            }

            var steps = CollectChildren(flight, "Step_");
            if (steps.Count == 0)
            {
                failures.Add("[stairwell-walk] no 'Step_*' children - a flight with no visible steps is a ramp");
                return;
            }
            foreach (var s in steps)
            {
                if (s.GetComponent<Collider>() != null)
                    failures.Add($"[stairwell-walk] step '{s.name}' still has a Collider - step colliders must be " +
                                 "DESTROYED; they are what turns the walk surface into a saw");
                if (s.GetComponent<MeshRenderer>() == null)
                    failures.Add($"[stairwell-walk] step '{s.name}' has no MeshRenderer - the steps are the only " +
                                 "VISIBLE part of the flight, so an invisible step is an invisible staircase");
            }
        }

        // =====================================================================
        //  CASE F — alignment STILL BITES on yaw (the anti-WO-927 case)
        // =====================================================================
        //
        //  THIS IS THE CASE THIS SUITE EXISTS FOR. TryMate scores `align = dot(a.Outward,
        //  -b.Outward)`. Under the retired VERTICAL pair both normals were +/-Y, so this
        //  construction passed at EVERY yaw - the check was structurally blind on the only axis
        //  that could fail, and reported matesFail=0 for five days while half the stairs pointed
        //  at solid floor.
        //
        //  Under WO-930 the sockets are horizontal Doors, so the same construction must now REJECT
        //  a 90 and a 180 yaw at ZERO distance. Zero distance matters: it isolates the alignment
        //  term from the distance term, so a pass cannot be borrowed from proximity.
        //
        //  It also proves the second WO-930 claim in the same breath - mating an UPPER socket
        //  through the ordinary planar door path lands the neighbour at y = FloorSeparationY with
        //  no elevation field and no special case, and the LOWER socket lands it at y = 0.
        //
        //  PROVE IT BITES: change DungeonBakerChecks.AlignThreshold to a negative number and
        //  re-run - the 90 and 180 sub-cases must start passing and this case must report them.
        //
        private static void CaseF_AlignmentStillBitesOnYaw(List<string> failures)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(StairwellPrefab);
            if (asset == null) { failures.Add("[stairwell-yaw] " + StairwellPrefab + " does not load"); return; }

            var inst = UnityEngine.Object.Instantiate(asset);
            s_spawned.Add(inst);
            inst.transform.position = Vector3.zero;
            inst.transform.rotation = Quaternion.identity;

            // s_upper_w's outward is -X (socket yaw 270), so a probe whose outward is +X (room yaw 90)
            // opposes it; s_lower_e's outward is +X, so its mate sits at room yaw 270. The two are
            // deliberately different sockets at DIFFERENT LEVELS taking the SAME code path.
            float sep = DungeonBakerChecks.FloorSeparationY;
            ProbeMateAtSocket(failures, inst, "s_upper_w", 90f, sep);
            ProbeMateAtSocket(failures, inst, "s_lower_e", 270f, 0f);
        }

        /// <summary>
        /// Mate a synthetic Door probe to one shipped stairwell socket, mirroring
        /// GraphDungeonComposer.SolveMate (pos = parentSocketWorld - rotatedChildSocketLocal) rather
        /// than leaning on TryMate's nudge — the door nudge is planar by design and can never close
        /// a Y gap, which is precisely why the composer SOLVES height instead of nudging to it.
        /// </summary>
        /// <param name="mateYaw">Child room yaw at which the probe's outward OPPOSES this socket.</param>
        /// <param name="expectY">World Y the probe room must land at — read off the socket, not typed.</param>
        private static void ProbeMateAtSocket(List<string> failures, GameObject inst, string socketId,
                                              float mateYaw, float expectY)
        {
            var pSock = DungeonBakerChecks.FindSocket(inst, socketId);
            if (pSock == null)
            {
                failures.Add($"[stairwell-yaw] the shipped StairwellRoom has no socket '{socketId}' - the graphs " +
                             "name it, so a rename here silently drops every stairwell from every dungeon");
                return;
            }

            float maxD = DungeonBakerChecks.DefaultMaxMateDistance;

            // The CORRECT yaw must mate, and must land the neighbour at this socket's height.
            var good = SolveProbeAgainst(pSock, mateYaw);
            var r = DungeonBakerChecks.TryMate(pSock, DungeonBakerChecks.FindSocket(good, "p"), good, maxD);
            if (!r.ok)
                failures.Add($"[stairwell-yaw] a correctly-yawed Door probe must mate '{socketId}' " +
                             $"(ok={r.ok} reason={r.reason} dist={r.dist:F2} align={r.align:F2})");
            if (Mathf.Abs(good.transform.position.y - expectY) > 0.01f)
                failures.Add($"[stairwell-yaw] mating '{socketId}' landed the neighbour at y=" +
                             $"{good.transform.position.y:0.###}, expected {expectY:0.###}. Height must ride the " +
                             "SOCKET - that is what lets an upper-level door mate through the ordinary planar path " +
                             "with no elevation field in the graph and no special case in the composer.");

            // A 90 and a 180 yaw must both be REJECTED, at zero distance, on Alignment.
            foreach (float off in new[] { 90f, 180f })
            {
                var bad = SolveProbeAgainst(pSock, mateYaw + off);
                var br = DungeonBakerChecks.TryMate(pSock, DungeonBakerChecks.FindSocket(bad, "p"), bad, maxD);
                if (br.ok || br.reason != MateFailReason.Alignment)
                    failures.Add($"[stairwell-yaw] a probe yawed {off:0} deg off must FAIL '{socketId}' on " +
                                 $"Alignment (got ok={br.ok} reason={br.reason} align={br.align:F2}). The align " +
                                 "term being unable to reject a yaw is EXACTLY the blindness that hid WO-927 - " +
                                 "on a vertical pair it read 1.0 at every yaw and the gate said matesFail=0 while " +
                                 "half the stairs in the game pointed at solid floor.");
            }
        }

        /// <summary>Build a probe room at <paramref name="yaw"/> and solve it onto <paramref name="pSock"/>.</summary>
        private static GameObject SolveProbeAgainst(RoomSocket pSock, float yaw)
        {
            var probe = MakeRoom("SW_probe_" + Mathf.RoundToInt(yaw),
                                 Sock("p", RoomSocketType.Door, new Vector3(0f, 0f, 3f), Vector3.forward));
            Place(probe, Vector3.zero, yaw);
            var cSock = DungeonBakerChecks.FindSocket(probe, "p");
            Vector3 rotatedLocal = cSock.transform.position - probe.transform.position;
            probe.transform.position = pSock.WorldPosition - rotatedLocal;
            return probe;
        }

        // =====================================================================
        //  CASE G — the stairwell DECLARES the footprint it actually occupies
        // =====================================================================
        //
        //  DungeonBakerChecks.RoomsOverlap reads RoomPrefabMeta.FootprintWorld and falls back to
        //  ONE canon cell when the meta is missing. The stairwell claims TWO cells on its long
        //  axis (DefaultStairwellRoomBuilder CellsX=2), so a missing meta makes the overlap gate
        //  under-report it by a full cell - on the one room that is now in every shipping dungeon.
        //  WO-930 §6 names this under "what must NOT be broken": a stairwell can be placed straight
        //  through a neighbour and the check will never look.
        //
        //  The expected footprint is DERIVED from the shipped shell, so it cannot drift from the
        //  geometry the way a typed 2x1 would.
        //
        //  PROVE IT BITES: it bites today (see the RESULT note). To re-prove after the fix, change
        //  meta.footprintCells to (1,1) on the shipped prefab and re-run.
        //
        private static void CaseG_StairwellDeclaresItsFootprint(List<string> failures, List<string> notes)
        {
            var root = LoadStairwell(failures);
            if (root == null) return;

            var wallN = FindChild(root, "Wall_N");
            if (wallN == null) return;   // already reported by [stairwell-shape]

            float fx = wallN.localScale.x;
            float fz = Mathf.Abs(wallN.localPosition.z) * 2f;
            int cx = Mathf.RoundToInt(fx / RoomForgeCanon.Cell);
            int cz = Mathf.RoundToInt(fz / RoomForgeCanon.Cell);

            if (Mathf.Abs(fx - cx * RoomForgeCanon.Cell) > 0.01f || Mathf.Abs(fz - cz * RoomForgeCanon.Cell) > 0.01f)
                failures.Add($"[stairwell-meta] the shell measures {fx:0.##}x{fz:0.##}u, which is not a whole " +
                             $"number of {RoomForgeCanon.Cell:0.#}u cells. The GRID does not change - the ROOM " +
                             "claims more cells (owner's rule); a fractional claim quantises to drift.");

            var meta = root.GetComponent<RoomPrefabMeta>();
            if (meta == null)
            {
                failures.Add("[stairwell-meta] StairwellRoom.prefab carries NO RoomPrefabMeta - it is the only " +
                             "room in the kit without one (DefaultDungeonRoomsBuilder:269 and " +
                             "DefaultStairConnectorRoomsBuilder:247 both stamp it; DefaultStairwellRoomBuilder " +
                             $"does not). DungeonBakerChecks.RoomsOverlap therefore falls back to ONE " +
                             $"{RoomForgeCanon.Cell:0.#}u cell for a {fx:0.##}x{fz:0.##}u room, so the overlap " +
                             "gate under-reports the stairwell by a whole cell and a neighbour can be placed " +
                             "straight through it - WO-930 §6, 'what must NOT be broken'. The composer also emits " +
                             "archetype=null for every stairwell node. FIX: stamp a RoomPrefabMeta in " +
                             $"DefaultStairwellRoomBuilder.BuildAll (roomId '{StairwellStem}', footprintCells " +
                             $"({cx},{cz}), cellSize RoomForgeCanon.Cell) and rebuild the prefab.");
                return;
            }

            if (meta.roomId != StairwellStem)
                failures.Add($"[stairwell-meta] RoomPrefabMeta.roomId is '{meta.roomId}', expected '{StairwellStem}'");
            if (Mathf.Abs(meta.cellSize - RoomForgeCanon.Cell) > 0.01f)
                failures.Add($"[stairwell-meta] cellSize={meta.cellSize:0.##}, canon Cell is " +
                             $"{RoomForgeCanon.Cell:0.##} - the prefab is STALE, rebuild it");
            if (meta.footprintCells.x != cx || meta.footprintCells.y != cz)
                failures.Add($"[stairwell-meta] declares footprintCells ({meta.footprintCells.x}," +
                             $"{meta.footprintCells.y}) but the shell measures {fx:0.##}x{fz:0.##}u = " +
                             $"({cx},{cz}) cells. RoomsOverlap trusts the DECLARATION, so an under-declared " +
                             "footprint is an overlap gate that cannot see a collision.");

            // Not a failure: RoomPrefabMeta has no vertical-extent field yet, so RoomsOverlap:190 still
            // treats anything more than half a floor apart in Y as non-overlapping. Correct for a
            // single-storey room, WRONG for a room that IS two storeys. WO-930 §6 owns the fix.
            notes.Add("RoomPrefabMeta carries no vertical extent, so RoomsOverlap still cannot see a room " +
                      "placed on the level INSIDE a two-storey stairwell's volume (WO-930 §6)");
        }

        // =====================================================================
        //  CASE H — THE ONE THAT MATTERS MOST: the shipping graphs are converted,
        //           and the control group is still intact
        // =====================================================================
        //
        //  A suite that cannot be made to fail is not an oracle. This case is the one that goes RED
        //  if someone reverts a shipping graph to the retired prefabs - which is the single change
        //  that would put the WO-927 geometry back into a dungeon players walk.
        //
        //  PROVE IT BITES (both halves, ~10 seconds each):
        //    * conversion: in Assets/Resources/Data/Canonical/dungeon-graphs/dg_bonecrypt.json,
        //      change one  "prefab": "StairwellRoom"  to  "prefab": "StairDown"  and re-run.
        //      [graphs-converted] must name dg_bonecrypt and that node id.
        //    * control group: change dg_stair_rig's "StairConnector_Vertical_Down" to
        //      "StairwellRoom" and re-run. It must report the A/B control group destroyed.
        //
        private static void CaseH_ShippingGraphsUseTheStairwell(List<string> failures)
        {
            var stairwellAsset = AssetDatabase.LoadAssetAtPath<GameObject>(StairwellPrefab);

            foreach (string dir in new[] { GraphsResourcesDir, GraphsStreamingDir })
            {
                if (!Directory.Exists(dir))
                {
                    failures.Add($"[graphs-converted] graph directory missing: {dir}");
                    continue;
                }

                // ── The converted set: StairwellRoom only, no retired prefab anywhere.
                foreach (string id in ConvertedGraphs)
                {
                    var g = LoadGraph(failures, dir, id);
                    if (g == null) continue;

                    var nodePrefab = NodePrefabs(g);
                    var stairwellNodes = nodePrefab.Where(kv => kv.Value == StairwellStem)
                                                   .Select(kv => kv.Key).ToList();
                    if (stairwellNodes.Count == 0)
                        failures.Add($"[graphs-converted] {id} ({DirLabel(dir)}) has NO '{StairwellStem}' node - " +
                                     "it is a shipping multi-level dungeon and WO-930 converted it. A graph with " +
                                     "no stairwell has no descent.");

                    foreach (var kv in nodePrefab)
                    {
                        if (IsRetiredStairStem(kv.Value))
                            failures.Add($"[graphs-converted] {id} ({DirLabel(dir)}) node '{kv.Key}' is back on the " +
                                         $"RETIRED prefab '{kv.Value}'. That model needs a floor hole, a ceiling " +
                                         "shaft and a flight in three frames to agree, nothing enforces the " +
                                         "agreement, and on 2026-08-08 it was measured broken with every gate " +
                                         $"green. Use '{StairwellStem}'.");
                    }

                    // Every socket the graph names on a stairwell must EXIST on the shipped prefab, and each
                    // stairwell must be entered at one level and left at the other - a stairwell used at one
                    // level only is a stairwell nobody descends.
                    foreach (string node in stairwellNodes)
                    {
                        var used = SocketsUsedBy(g, node);
                        if (used.Count == 0)
                        {
                            failures.Add($"[graphs-converted] {id} ({DirLabel(dir)}) stairwell '{node}' is in NO " +
                                         "edge - it would be emitted at the origin and reached by nothing");
                            continue;
                        }
                        foreach (string sid in used)
                        {
                            if (stairwellAsset != null && DungeonBakerChecks.FindSocket(stairwellAsset, sid) == null)
                                failures.Add($"[graphs-converted] {id} ({DirLabel(dir)}) edge names socket '{sid}' " +
                                             $"on stairwell '{node}', but {StairwellStem}.prefab has no such " +
                                             "socket - the mate silently never happens");
                        }
                        bool anyUpper = used.Any(s => s.StartsWith("s_upper", StringComparison.Ordinal));
                        bool anyLower = used.Any(s => s.StartsWith("s_lower", StringComparison.Ordinal));
                        if (!anyUpper || !anyLower)
                        {
                            string usedList = string.Join(",", used);
                            failures.Add($"[graphs-converted] {id} ({DirLabel(dir)}) stairwell '{node}' uses " +
                                         $"[{usedList}] - it must be joined at BOTH levels (an " +
                                         "s_upper_* AND an s_lower_*). Entered and left on one level, it changes " +
                                         "no floor and the dungeon is flat.");
                        }
                    }
                }

                // ── The A/B CONTROL GROUP must still be on the retired model. This is the guard that
                //    stops a tidy-up deleting the fixtures that make the comparison re-runnable.
                foreach (string id in ControlGroupGraphs)
                {
                    var g = LoadGraph(failures, dir, id);
                    if (g == null) continue;

                    var nodePrefab = NodePrefabs(g);
                    if (!nodePrefab.Values.Any(IsRetiredStairStem))
                        failures.Add($"[graphs-converted] {id} ({DirLabel(dir)}) no longer names any retired stair " +
                                     "prefab. It is NOT stale content - it is the A/B CONTROL GROUP that WO-930 " +
                                     "was proved against (dg_stairwell_probe PathComplete vs dg_descent_probe " +
                                     "PathPartial in one bake). Converting or deleting it destroys the ability to " +
                                     "re-run that comparison. Revert it.");
                }

                // ── dg_starter_loop: a FLAT loop that borrows two stair-shaped rooms as dressing. Its
                //    stair sockets are in no edge, which is the premise [legacy-vertical-seal] rests on.
                var flat = LoadGraph(failures, dir, FlatLegacyGraph);
                if (flat != null)
                {
                    foreach (var kv in NodePrefabs(flat))
                    {
                        if (!IsRetiredStairStem(kv.Value)) continue;
                        var used = SocketsUsedBy(flat, kv.Key);
                        foreach (string sid in used)
                        {
                            if (sid.StartsWith("stair_", StringComparison.Ordinal))
                                failures.Add($"[graphs-converted] {FlatLegacyGraph} ({DirLabel(dir)}) node " +
                                             $"'{kv.Key}' now uses the VERTICAL socket '{sid}'. This graph is a " +
                                             "FLAT loop - its stair rooms are dressing whose stair sockets seal " +
                                             "(SEALED_VERTICAL). Making it multi-level puts it on the retired " +
                                             $"model; use '{StairwellStem}' instead.");
                        }
                    }
                }
            }

            // ── The control-group PREFAB ASSETS must survive on disk. WO-930 §5 schedules their
            //    deletion; §6 forbids it before the replacement is proven. Until then they are fixtures.
            foreach (string stem in ControlGroupPrefabs)
            {
                string path = $"{RoomsFolder}/{stem}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                    failures.Add($"[graphs-converted] control-group fixture {path} is GONE. dg_stair_rig and " +
                                 "dg_descent_probe still instantiate these; deleting them breaks the A/B " +
                                 "comparison and leaves the still-live vertical-mate code with no coverage. " +
                                 "When WO-930 §5 genuinely retires the pair model, delete these prefabs, those " +
                                 "two graphs and this suite's [legacy-*] cases IN ONE COMMIT.");
            }
        }

        // =====================================================================
        //  CASE I — the floor separation clears what a floor OCCUPIES
        // =====================================================================
        //
        //  Shared by both models: FloorSeparationY is the Y a stairwell's UPPER sockets sit at, so
        //  it is still the number a descent is expressed in.
        //
        //  THIS ASSERTION USED TO BE A LIE. It read `const float WallHeight = 2.8f; //
        //  DefaultDungeonRoomsBuilder.BuildPerimeterWalls` - a hand-copied duplicate of a number
        //  that lives in another assembly. The moment WO-919 raised the builder's walls to 4.0 the
        //  comment became false, and the case would have kept passing at a wall height of 7u while
        //  stacked floors ran through each other. An oracle that re-types the value it is guarding
        //  guards nothing.
        //
        //  PROVE IT BITES: raise RoomForgeCanon.WallHeight to 7 and re-run.
        //
        private static void CaseI_FloorSeparationClearsAFloor(List<string> failures)
        {
            float sep = DungeonBakerChecks.FloorSeparationY;
            float occupied = RoomForgeCanon.FloorOccupiedHeight;
            if (sep <= occupied)
                failures.Add($"[floor-drop] FloorSeparationY {sep:0.##}u does not clear a floor's occupied " +
                             $"height {occupied:0.##}u (slab {RoomForgeCanon.FloorSlabThickness:0.##} + wall " +
                             $"{RoomForgeCanon.WallHeight:0.##} + ceiling {RoomForgeCanon.CeilingThickness:0.##}) - " +
                             "stacked floors would interpenetrate. Raise DungeonBakerChecks.FloorSeparationY " +
                             "(and re-bake every multi-level layout) or lower the shell in RoomForgeCanon.");

            // The composer's position solve, on the shipping model's own numbers: a neighbour mated to an
            // upper socket lands exactly one floor above one mated to a lower socket - with yaw held, no
            // XZ drift, and no elevation field anywhere in the graph schema.
            var upper = MakeRoom("ML_solve_upper", Sock("u", RoomSocketType.Door, new Vector3(0f, sep, 3f), Vector3.forward));
            var lower = MakeRoom("ML_solve_lower", Sock("l", RoomSocketType.Door, new Vector3(0f, 0f, 3f), Vector3.forward));
            Place(upper, Vector3.zero, 0f);
            Place(lower, Vector3.zero, 0f);

            float dy = DungeonBakerChecks.FindSocket(upper, "u").WorldPosition.y -
                       DungeonBakerChecks.FindSocket(lower, "l").WorldPosition.y;
            if (Mathf.Abs(dy - sep) > 0.001f)
                failures.Add($"[floor-drop] two door sockets one floor apart measure {dy:0.###}u, expected " +
                             $"{sep:0.###}u - the socket no longer carries its own height, which is the single " +
                             "property that lets the composer stay unchanged");
        }

        // =====================================================================
        //  CASE J — a correct vertical stack is NOT an overlap; same floor still is
        // =====================================================================
        //
        //  Still load-bearing under WO-930: a converted dungeon still stacks whole FLOORS, and
        //  their footprints still coincide. Without the Y escape hatch every multi-level bake
        //  aborts; with it applied too loosely, real same-floor collisions vanish.
        //
        //  PROVE IT BITES: delete the `if (Mathf.Abs(aPos.y - bPos.y) > FloorSeparationY * 0.5f)`
        //  early-out in DungeonBakerChecks.RoomsOverlap and re-run.
        //
        private static void CaseJ_StackIsNotOverlap(List<string> failures)
        {
            var a = MakeRoom("ML_ov_a");
            var b = MakeRoom("ML_ov_b");
            var ma = a.GetComponent<RoomPrefabMeta>();
            var mb = b.GetComponent<RoomPrefabMeta>();
            float sep = DungeonBakerChecks.FloorSeparationY;
            float tol = DungeonBakerChecks.OverlapTolerance;

            if (DungeonBakerChecks.RoomsOverlap(ma, Vector3.zero, 0f, mb, new Vector3(0f, -sep, 0f), 0f, tol))
                failures.Add("[stack-not-overlap] rooms one floor apart were reported as overlapping - this aborts every multi-level bake");

            if (!DungeonBakerChecks.RoomsOverlap(ma, Vector3.zero, 0f, mb, Vector3.zero, 0f, tol))
                failures.Add("[stack-not-overlap] two rooms at the SAME position must still overlap");

            if (!DungeonBakerChecks.RoomsOverlap(ma, Vector3.zero, 0f, mb, new Vector3(0f, 0.4f, 0f), 0f, tol))
                failures.Add("[stack-not-overlap] a 0.4u Y jitter is not a floor change - the rooms still overlap");

            // Side by side on the same floor - a shared wall, not an overlap. Offset is ONE canon cell,
            // read from the same const MakeRoom stamps on the meta, so this stays an exact touch through
            // WO-922's 6 -> 10 widen and any future change.
            if (DungeonBakerChecks.RoomsOverlap(ma, Vector3.zero, 0f, mb,
                                                new Vector3(RoomForgeCanon.Cell, 0f, 0f), 0f, tol))
                failures.Add("[stack-not-overlap] adjacent rooms sharing a wall must not count as overlapping");
        }

        // =====================================================================
        //  CASE K — doors keep the PLANAR-only nudge (a Y gap is still an error)
        // =====================================================================
        //
        //  MORE important under WO-930, not less: EVERY stairwell mate is now a DOOR mate. If the
        //  door nudge were made 3D "so upper-level sockets mate more easily", a room authored at
        //  the wrong height would be silently lifted into place - and the no-drift property that
        //  the whole one-room design rests on would be gone with nothing to show for it.
        //
        //  PROVE IT BITES: drop the IsVertical condition in TryMate's slide so every mate takes
        //  the full 3D delta, and re-run.
        //
        private static void CaseK_DoorsKeepPlanarNudge(List<string> failures)
        {
            var a = MakeRoom("ML_door_a", Sock("n", RoomSocketType.Door, new Vector3(0, 0, 3), Vector3.forward));
            var b = MakeRoom("ML_door_b", Sock("s", RoomSocketType.Door, new Vector3(0, 0, -3), Vector3.back));
            Place(b, new Vector3(0, 3f, 6), 0f);   // 3u vertical gap between two DOORS

            var r = DungeonBakerChecks.TryMate(DungeonBakerChecks.FindSocket(a, "n"),
                                               DungeonBakerChecks.FindSocket(b, "s"), b,
                                               DungeonBakerChecks.DefaultMaxMateDistance);
            if (r.ok || r.reason != MateFailReason.Distance)
                failures.Add($"[door-planar] a door pair with a 3u Y gap must FAIL on distance, not be lifted into place (ok={r.ok} reason={r.reason} dist={r.dist:F2})");
            if (Mathf.Abs(b.transform.position.y - 3f) > 0.001f)
                failures.Add($"[door-planar] the door nudge moved the room in Y (y={b.transform.position.y:0.###}) - it must stay planar");
        }

        // =====================================================================
        //  ⚠ QUARANTINED BELOW — the RETIRED pair model (WO-1001 slice 1).
        //    Kept because the code is still LIVE and dg_stair_rig / dg_descent_probe /
        //    dg_starter_loop still load it. See the file header for the delete plan.
        // =====================================================================

        // ---------------------------------------------------------------------
        //  CASE L (legacy) — a StairDown/StairUp pair OPPOSES; the old pose does not
        // ---------------------------------------------------------------------
        private static void CaseL_LegacyStairSocketsOppose(List<string> failures)
        {
            float sep = DungeonBakerChecks.FloorSeparationY;
            float h = sep * 0.5f;
            float maxD = DungeonBakerChecks.DefaultMaxMateDistance;

            var upper = MakeRoom("ML_upper", StairSock("stair_down_01", RoomSocketType.StairDown, -h, Vector3.down));
            var lower = MakeRoom("ML_lower", StairSock("stair_up_01", RoomSocketType.StairUp, h, Vector3.up));
            Place(upper, Vector3.zero, 0f);
            Place(lower, new Vector3(0f, -sep, 0f), 0f);

            var r = DungeonBakerChecks.TryMate(DungeonBakerChecks.FindSocket(upper, "stair_down_01"),
                                               DungeonBakerChecks.FindSocket(lower, "stair_up_01"), lower, maxD);
            if (!r.ok)
                failures.Add($"[legacy-oppose] a stacked StairDown/StairUp pair MUST mate (ok={r.ok} reason={r.reason} dist={r.dist:F2} align={r.align:F2})");

            // The pre-WO-1001 authoring: BOTH sockets pointing down at local Y=0. Must still fail.
            var oldA = MakeRoom("ML_oldA", StairSock("stair_down_01", RoomSocketType.StairDown, 0f, Vector3.down));
            var oldB = MakeRoom("ML_oldB", StairSock("stair_up_01", RoomSocketType.StairUp, 0f, Vector3.down));
            Place(oldA, Vector3.zero, 0f);
            Place(oldB, Vector3.zero, 0f);
            var old = DungeonBakerChecks.TryMate(DungeonBakerChecks.FindSocket(oldA, "stair_down_01"),
                                                 DungeonBakerChecks.FindSocket(oldB, "stair_up_01"), oldB, maxD);
            if (old.ok)
                failures.Add($"[legacy-oppose] two DOWN-facing stair sockets must NOT mate - the alignment gate has been loosened (align={old.align:F2})");

            if (!DungeonBakerChecks.TypesCompatible(RoomSocketType.StairDown, RoomSocketType.StairUp))
                failures.Add("[legacy-oppose] StairDown/StairUp are no longer type-compatible");
            if (DungeonBakerChecks.TypesCompatible(RoomSocketType.Door, RoomSocketType.StairDown))
                failures.Add("[legacy-oppose] a Door must never mate a stair socket");
            if (!DungeonBakerChecks.IsVertical(RoomSocketType.StairUp) ||
                !DungeonBakerChecks.IsVertical(RoomSocketType.StairDown) ||
                DungeonBakerChecks.IsVertical(RoomSocketType.Door))
                failures.Add("[legacy-oppose] IsVertical must be true for stairs and false for doors");
        }

        // ---------------------------------------------------------------------
        //  CASE M (legacy) — the retired stair prefabs still carry their poses
        // ---------------------------------------------------------------------
        //
        //  These prefabs are GENERATED, so editing the builder changes nothing on disk until
        //  "Defenders/Dungeon/Build Default Room Prefabs" is re-run. This case reads the actual
        //  assets, so a builder edit that was never baked fails loudly instead of looking fixed.
        //
        private static void CaseM_LegacyPrefabsCarryThePoses(List<string> failures)
        {
            float h = DungeonBakerChecks.FloorSeparationY * 0.5f;
            CheckLegacyStairPrefab(failures, StairDownPrefab, "stair_down_01", RoomSocketType.StairDown, -h, Vector3.down);
            CheckLegacyStairPrefab(failures, StairUpPrefab, "stair_up_01", RoomSocketType.StairUp, h, Vector3.up);
        }

        private static void CheckLegacyStairPrefab(List<string> failures, string path, string socketId,
                                                   RoomSocketType expectType, float expectY, Vector3 expectOutward)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                failures.Add($"[legacy-prefab-poses] {path} does not load - it is a CONTROL-GROUP fixture for " +
                             "dg_descent_probe / dg_starter_loop, not dead content");
                return;
            }

            var sock = DungeonBakerChecks.FindSocket(prefab, socketId);
            if (sock == null)
            {
                failures.Add($"[legacy-prefab-poses] {path} has no socket '{socketId}'");
                return;
            }
            if (sock.type != expectType)
                failures.Add($"[legacy-prefab-poses] {path} socket '{socketId}' type is {sock.type}, expected {expectType}");

            Vector3 lp = sock.transform.localPosition;
            if (Mathf.Abs(lp.x) > 0.001f || Mathf.Abs(lp.z) > 0.001f)
                failures.Add($"[legacy-prefab-poses] {path} socket '{socketId}' has a fractional/offset X or Z ({lp.x:0.###}, {lp.z:0.###}) - a stair socket must sit on the room axis or the emitted cell grid drifts by a whole unit per floor");

            if (Mathf.Abs(lp.y - expectY) > 0.01f)
                failures.Add($"[legacy-prefab-poses] {path} socket '{socketId}' local Y is {lp.y:0.###}, expected {expectY:0.###}. " +
                             "The generated prefabs are STALE - re-run Defenders/Dungeon/Build Default Room Prefabs " +
                             "(DefaultDungeonRoomsBuilder.BuildAll) after editing the builder.");

            float dot = Vector3.Dot(sock.transform.forward.normalized, expectOutward);
            if (dot < 0.99f)
                failures.Add($"[legacy-prefab-poses] {path} socket '{socketId}' points {sock.transform.forward}, expected {expectOutward} " +
                             $"(dot={dot:0.###}). Stale prefabs - re-run DefaultDungeonRoomsBuilder.BuildAll.");
        }

        // ---------------------------------------------------------------------
        //  CASE N (legacy) — an UNMATED stair socket seals invisibly
        // ---------------------------------------------------------------------
        //
        //  dg_starter_loop's StairUp/StairDown rooms have their stair sockets in NO edge (asserted
        //  as DATA by [graphs-converted], not just claimed here), so they get sealed. A wall slab is
        //  meaningless on a floor hole, and half a floor up it would hang in mid-air in a room the
        //  owner actually plays.
        //
        private static void CaseN_LegacyUnmatedStairSealsInvisibly(List<string> failures)
        {
            float h = DungeonBakerChecks.FloorSeparationY * 0.5f;

            var up = MakeRoom("ML_seal_up", StairSock("stair_up_01", RoomSocketType.StairUp, h, Vector3.up));
            var sock = DungeonBakerChecks.FindSocket(up, "stair_up_01");
            bool spawnedGeometry = DungeonBakerChecks.SealSocket(sock);

            if (spawnedGeometry)
                failures.Add("[legacy-vertical-seal] an unmated stair socket spawned wall geometry - it would float half a floor up");
            if (sock.matedTo != "SEALED_VERTICAL")
                failures.Add($"[legacy-vertical-seal] expected matedTo=SEALED_VERTICAL, got '{sock.matedTo}' - the seal kind must stay distinguishable from SECRET in the bake trace");
            if (sock.transform.childCount != 0)
                failures.Add($"[legacy-vertical-seal] the stair socket gained {sock.transform.childCount} child object(s) - an invisible seal must spawn nothing");

            // A normal unmated DOOR must still get its wall - this must not blank real walls. This one
            // is NOT legacy: every stairwell socket is a Door, so it is the live path.
            var room = MakeRoom("ML_seal_door", Sock("n", RoomSocketType.Door, new Vector3(0, 0, 3), Vector3.forward));
            var door = DungeonBakerChecks.FindSocket(room, "n");
            if (!DungeonBakerChecks.SealSocket(door))
                failures.Add("[legacy-vertical-seal] an unmated DOOR must still be sealed with wall geometry");
            if (door.matedTo != "SEALED_WALL")
                failures.Add($"[legacy-vertical-seal] door seal expected matedTo=SEALED_WALL, got '{door.matedTo}'");

            var secretRoom = MakeRoom("ML_seal_secret", Sock("s", RoomSocketType.Door, new Vector3(0, 0, -3), Vector3.back));
            var sec = DungeonBakerChecks.FindSocket(secretRoom, "s");
            sec.isSecret = true;
            if (DungeonBakerChecks.SealSocket(sec))
                failures.Add("[legacy-vertical-seal] a secret socket must not spawn wall geometry");
            if (sec.matedTo != "SEALED_SECRET")
                failures.Add($"[legacy-vertical-seal] secret seal expected matedTo=SEALED_SECRET, got '{sec.matedTo}'");
        }

        // =====================================================================
        //  helpers — prefab
        // =====================================================================

        private static GameObject LoadStairwell(List<string> failures)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StairwellPrefab);
            if (prefab == null)
                failures.Add($"[stairwell] {StairwellPrefab} does not load. It is the room EVERY shipping " +
                             "multi-level dungeon is built from - re-run " +
                             "\"Defenders/Dungeon/Build Stairwell Room Prefab\" " +
                             "(DeNelle.Editor.RoomForge.DefaultStairwellRoomBuilder.BuildAll).");
            return prefab;
        }

        private static Transform FindChild(GameObject root, string name) => FindChild(root.transform, name);

        private static Transform FindChild(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != null && t.name == name) return t;
            return null;
        }

        /// <summary>Every transform under <paramref name="root"/> whose name starts with the prefix.</summary>
        private static List<Transform> CollectChildren(GameObject root, string prefix)
            => CollectChildren(root.transform, prefix);

        private static List<Transform> CollectChildren(Transform root, string prefix)
        {
            var list = new List<Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != null && t != root && t.name.StartsWith(prefix, StringComparison.Ordinal)) list.Add(t);
            return list;
        }

        /// <summary>Fold a Unity euler component into (-180, 180] so a pitch reads as a signed angle.</summary>
        private static float NormalizeAngle(float deg)
        {
            deg = Mathf.Repeat(deg, 360f);
            return deg > 180f ? deg - 360f : deg;
        }

        // =====================================================================
        //  helpers — graph JSON
        // =====================================================================

        private static string DirLabel(string dir)
            => dir == GraphsStreamingDir ? "StreamingAssets" : "Resources";

        private static bool IsRetiredStairStem(string stem)
            => !string.IsNullOrEmpty(stem) &&
               (stem == "StairDown" || stem == "StairUp" ||
                stem.StartsWith("StairConnector_", StringComparison.Ordinal));

        private static JObject LoadGraph(List<string> failures, string dir, string graphId)
        {
            string path = Path.Combine(dir, graphId + ".json");
            if (!File.Exists(path))
            {
                failures.Add($"[graphs-converted] {path} is MISSING");
                return null;
            }
            try { return JObject.Parse(File.ReadAllText(path)); }
            catch (Exception ex)
            {
                failures.Add($"[graphs-converted] {path} does not parse ({ex.GetType().Name}: {ex.Message})");
                return null;
            }
        }

        /// <summary>node id -> prefab stem, for every node in the graph.</summary>
        private static Dictionary<string, string> NodePrefabs(JObject graph)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            var nodes = graph["nodes"] as JArray;
            if (nodes == null) return map;
            foreach (var n in nodes)
            {
                string id = (string)n["id"];
                if (string.IsNullOrEmpty(id)) continue;
                map[id] = (string)n["prefab"];
            }
            return map;
        }

        /// <summary>Every socket id any edge names ON <paramref name="nodeId"/>.</summary>
        private static List<string> SocketsUsedBy(JObject graph, string nodeId)
        {
            var used = new List<string>();
            var edges = graph["edges"] as JArray;
            if (edges == null) return used;
            foreach (var e in edges)
            {
                if (string.Equals((string)e["from"], nodeId, StringComparison.Ordinal))
                {
                    string s = (string)e["fromSocket"];
                    if (!string.IsNullOrEmpty(s) && !used.Contains(s)) used.Add(s);
                }
                if (string.Equals((string)e["to"], nodeId, StringComparison.Ordinal))
                {
                    string s = (string)e["toSocket"];
                    if (!string.IsNullOrEmpty(s) && !used.Contains(s)) used.Add(s);
                }
            }
            return used;
        }

        // =====================================================================
        //  helpers — synthetic rooms
        // =====================================================================

        private struct SockSpec
        {
            public string id;
            public RoomSocketType type;
            public Vector3 local;
            public Vector3 outward;
        }

        private static SockSpec Sock(string id, RoomSocketType type, Vector3 local, Vector3 outward)
            => new SockSpec { id = id, type = type, local = local, outward = outward };

        /// <summary>A retired-model stair socket: on the room's vertical axis, half a floor off the origin.</summary>
        private static SockSpec StairSock(string id, RoomSocketType type, float localY, Vector3 outward)
            => new SockSpec { id = id, type = type, local = new Vector3(0f, localY, 0f), outward = outward };

        private static GameObject MakeRoom(string id, params SockSpec[] socks)
        {
            var go = new GameObject(id);
            s_spawned.Add(go);
            var meta = go.AddComponent<RoomPrefabMeta>();
            meta.roomId = id;
            meta.archetype = "hub";
            // Canon cell, not a literal: [stack-not-overlap]'s shared-wall offset is derived from the
            // same const, so the pair moves together (WO-922). The +/-3 socket literals elsewhere in
            // this suite are pure mate-math distances and never touch the footprint.
            meta.cellSize = RoomForgeCanon.Cell;
            meta.footprintCells = Vector2Int.one;
            foreach (var s in socks)
            {
                var sg = new GameObject("Socket_" + s.id);
                sg.transform.SetParent(go.transform, false);
                sg.transform.localPosition = s.local;
                Vector3 fwd = s.outward.sqrMagnitude > 0.0001f ? s.outward : Vector3.forward;
                // Explicit up-vector: LookRotation(up) / LookRotation(down) alone is degenerate
                // against the default world up and yields an arbitrary roll.
                Vector3 up = Mathf.Abs(Vector3.Dot(fwd.normalized, Vector3.up)) > 0.99f
                    ? Vector3.forward
                    : Vector3.up;
                sg.transform.localRotation = Quaternion.LookRotation(fwd, up);
                var rs = sg.AddComponent<RoomSocket>();
                rs.id = s.id;
                rs.type = s.type;
                rs.halfWidth = 1.2f;
            }
            return go;
        }

        private static void Place(GameObject go, Vector3 pos, float yaw)
        {
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // Guard each case so one throw becomes a labelled failure, not a dead suite.
        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add($"[{name}] THREW {ex.GetType().Name}: {ex.Message}"); }
        }

        private static void Cleanup()
        {
            foreach (var go in s_spawned) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            s_spawned.Clear();
        }
    }
}
