// =============================================================================
// DefaultStairwellRoomBuilder — WO-930. THE STAIRWELL IS ONE ROOM.
// -----------------------------------------------------------------------------
// Menu:  Defenders/Dungeon/Build Stairwell Room Prefab
// Batch: DeNelle.Editor.RoomForge.DefaultStairwellRoomBuilder.BuildAll
//
// THE OWNER'S DESIGN (2026-08-08, from her section drawing). One room volume:
//
//   +---------------------------------------------------------------+
//   |   S=========+           [ GAP ]           +==========S        |  <- UPPER: two partial
//   |    floor     \             |             /   floor            |     floors, gap between
//   |               \        staircase        /                     |
//   |   S=======================================================S   |  <- LOWER: full length
//   +---------------------------------------------------------------+
//        S = socket, on a floor EDGE, at EITHER level
//
// WHY THIS REPLACES THE _Up/_Down PAIR (WO-927 root cause, found by the owner by eye):
// the pair model needed THREE things in TWO prefabs to agree — a hole cut in one
// room's floor, a shaft cut in another's ceiling, and a flight in a third frame.
// Nothing enforced that agreement, and on 2026-08-08 it was measured broken: the
// flight was yawed 180 while the openings were not, so half the stairs in the game
// pointed at solid floor while every gate read green (matesFail=0).
//
// Here there is nothing to agree WITH:
//   * no floor hole      — the lower floor is solid and the stair lands ON it
//   * no ceiling shaft   — the upper level is PARTIAL; the stair rises through the GAP
//   * no pair, no vertical mate, no delta-yaw, no placement order
//   * no slab over the stair — clearance is the room, not a 0.36 m squeeze
//
// AND THE COMPOSER NEEDS NO CHANGE. A socket already carries its own local position
// INCLUDING Y, and SolveMate solves pos = pPos - rotatedSocket, so height resolves
// for free. An upper-level socket mates by the ordinary planar door path.
//
// RUN IS DERIVED, NEVER AUTHORED. Slope = atan(FloorSeparationY / run). The 45 deg
// agent maximum is a CLIFF, not a target — DefaultStairConnectorRoomsBuilder's own
// header records that at 45.0 "the ramp stops carving at all". We target ~27 deg.
//
// WALK SURFACE (same proven contract as the connector kit):
//   Visual steps = cubes, colliders DESTROYED.
//   Ramp = thin Cube on the nose line, BoxCollider KEPT, MeshRenderer stripped.
//   NEVER PrimitiveType.Plane.
//
// Cell / wall / door / floor metrics come from RoomForgeCanon and DungeonBakerChecks.
// NEVER re-typed here.
// =============================================================================

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Dungeons.RoomForge;

namespace DeNelle.Editor.RoomForge
{
    public static class DefaultStairwellRoomBuilder
    {
        private const string RoomsFolder = "Assets/Dungeon/Rooms";
        private const string Sys = "RoomForgeStairwell";

        // ── Footprint ────────────────────────────────────────────────────────
        // Claims TWO cells on the long axis and one on the short. The owner's rule:
        // "it is still limited to a room, but that room is owned as a stairs object,
        // which itself is four subrooms that you can make as large as you want."
        // So the GRID does not change — RoomForgeCanon.Cell stays 10 and stays EVEN
        // (GraphDungeonComposer's header: an odd cell puts sockets on halves and
        // RoundToInt quantises a unit of drift per stairwell) — the ROOM claims more.
        private const int CellsX = 2;
        private const int CellsZ = 1;

        /// <summary>Depth of each partial upper floor, measured in from its end wall.</summary>
        private const float UpperFloorDepth = 5f;

        /// <summary>Flat lower-floor pad between the bottom nose and the east doorway. Without it the
        /// flight terminates IN the wall and the door, with no walkable span between them.</summary>
        private const float BottomPadDepth = 3f;

        /// <summary>Tread width. Matches the connector kit so the two read as one family.</summary>
        private const float StairWidth = 2.4f;
        private const float RampThickness = 0.15f;
        private const int StepCount = 16;
        /// <summary>Ramp overshoot past each nose so the walk surface OVERLAPS its landing (the nav seam).</summary>
        private const float LandingOverlap = 0.35f;

        /// <summary>Hard ceiling on derived slope. The agent max is 45; we refuse anything near it.</summary>
        private const float MaxSlopeDeg = 40f;

        [MenuItem("Defenders/Dungeon/Build Stairwell Room Prefab")]
        public static void BuildAll()
        {
            using var _ = FlowTrace.Enter(Sys, "BuildAll");

            float hx = CellsX * RoomForgeCanon.Cell * 0.5f;   // 10
            float hz = CellsZ * RoomForgeCanon.Cell * 0.5f;   // 5
            float rise = DungeonBakerChecks.FloorSeparationY; // 6

            // ── The derivation. Run is what is LEFT once both upper floors are seated,
            // plus the reach under them. The flight starts at the inner edge of the WEST
            // upper floor and lands on the lower floor to the EAST, passing UNDER the east
            // upper floor — which is exactly what the owner's section drawing shows, and is
            // where the shallow angle comes from. Nothing here is a magic number: change the
            // footprint or UpperFloorDepth and the slope moves with it.
            float startX = -hx + UpperFloorDepth;             // inner edge of the west upper floor
            // Land CLEAR of the east wall, leaving a flat pad between the bottom nose and the doorway.
            //
            // The first cut of this ran to exactly +hx - i.e. the flight terminated IN the east wall,
            // in the same place as the s_lower_e socket - so the bottom of the stair and the door
            // occupied one spot with no floor between them. A walkable span needs somewhere to BE
            // before it becomes a doorway; the owner's own rule from the top of the flight applies
            // just as much at the bottom ("we need that edge").
            float run = (hx - BottomPadDepth) - startX;
            float slopeDeg = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;

            if (slopeDeg > MaxSlopeDeg)
            {
                FlowTrace.Fail(Sys, $"STAIRWELL_BUILD_FAIL: derived slope {slopeDeg:F1} deg exceeds the " +
                    $"{MaxSlopeDeg} deg limit (run {run:F2} m over rise {rise:F2} m). The 45 deg agent " +
                    "maximum is a carve CLIFF, not a target - widen the footprint (CellsX) or shrink " +
                    "UpperFloorDepth. Refusing to build a stair the navmesh will fragment.");
                return;
            }

            // Same material path the rest of the kit uses. REUSED, never re-authored: a stairwell
            // that skins itself would drift from the room it opens into the first time the atlas
            // changes, and the owner's whole point is that this reads as one continuous space.
            RoomForgeMaterials.EnsureMenu();

            var go = new GameObject("StairwellRoom");

            BuildLowerFloor(go.transform, hx, hz);
            BuildUpperFloors(go.transform, hx, hz, rise);
            BuildPerimeter(go.transform, hx, hz, rise);
            BuildFlight(go.transform, startX, run, rise);
            BuildSockets(go.transform, hx, rise);

            // Walls/floors/ceiling take the shared KayKit stone. The ramp is skipped for free -
            // its MeshRenderer is destroyed, and ApplyToRoomRoot only walks renderers.
            RoomForgeMaterials.ApplyToRoomRoot(go, useAccentFloor: false);

            int badSurfaces = RoomForgeMaterials.VerifyRoomSurfaces(go, "StairwellRoom", false);
            if (badSurfaces > 0)
                FlowTrace.Warn(Sys, $"{badSurfaces} surface(s) did not take the shared material - " +
                    "the stairwell will read as untextured next to the rooms it connects.");

            string path = $"{RoomsFolder}/StairwellRoom.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();

            FlowTrace.Step(Sys, $"STAIRWELL_BUILD_OK: {path} - footprint {hx * 2:F0}x{hz * 2:F0} m, " +
                $"rise {rise:F1} m over run {run:F1} m = {slopeDeg:F1} deg " +
                $"({MaxSlopeDeg - slopeDeg:F1} deg of margin to the {MaxSlopeDeg} limit, " +
                $"{45f - slopeDeg:F1} to the agent cliff), {StepCount} steps, " +
                $"skinned with the shared RoomForge stone ({badSurfaces} bad surface(s)).");
        }

        /// <summary>Solid, FULL footprint, top face at local y = 0 — the same contract every
        /// other room honours, which is what lets an ordinary socket mate to it.</summary>
        private static void BuildLowerFloor(Transform parent, float hx, float hz)
        {
            AddBox(parent, "Floor_Lower",
                new Vector3(0f, -RoomForgeCanon.FloorSlabThickness * 0.5f, 0f),
                new Vector3(hx * 2f, RoomForgeCanon.FloorSlabThickness, hz * 2f), keepCollider: true);
        }

        /// <summary>TWO partial floors, one at each end, with the GAP between them. The gap is a
        /// deliberate structural element - it is the stairwell void, and the reason the stair
        /// never needs a hole cut through anything.</summary>
        private static void BuildUpperFloors(Transform parent, float hx, float hz, float rise)
        {
            float half = UpperFloorDepth * 0.5f;
            float y = rise - RoomForgeCanon.FloorSlabThickness * 0.5f;

            AddBox(parent, "Floor_Upper_W", new Vector3(-hx + half, y, 0f),
                new Vector3(UpperFloorDepth, RoomForgeCanon.FloorSlabThickness, hz * 2f), keepCollider: true);

            AddBox(parent, "Floor_Upper_E", new Vector3(hx - half, y, 0f),
                new Vector3(UpperFloorDepth, RoomForgeCanon.FloorSlabThickness, hz * 2f), keepCollider: true);
        }

        /// <summary>Perimeter walls tall enough to enclose BOTH levels, plus the ceiling above the
        /// upper floor. Door gaps are cut at BOTH levels on the two end walls, because a socket may
        /// sit at either one.</summary>
        private static void BuildPerimeter(Transform parent, float hx, float hz, float rise)
        {
            float top = rise + RoomForgeCanon.WallHeight;   // 10 m of enclosed volume
            float t = RoomForgeCanon.WallThickness;

            // Long side walls: solid, full height.
            AddBox(parent, "Wall_N", new Vector3(0f, top * 0.5f, hz), new Vector3(hx * 2f, top, t), true);
            AddBox(parent, "Wall_S", new Vector3(0f, top * 0.5f, -hz), new Vector3(hx * 2f, top, t), true);

            // End walls carry the door gaps - built as pillars either side of the gap, at each level.
            BuildEndWall(parent, "Wall_W", -hx, hz, t, top, rise);
            BuildEndWall(parent, "Wall_E", hx, hz, t, top, rise);

            AddBox(parent, "Ceiling", new Vector3(0f, top + RoomForgeCanon.CeilingThickness * 0.5f, 0f),
                new Vector3(hx * 2f, RoomForgeCanon.CeilingThickness, hz * 2f), keepCollider: false);
        }

        /// <summary>One end wall with a door gap at the LOWER level and another at the UPPER level.
        /// Built as flanking pillars + lintels rather than one slab, so both gaps are real openings.</summary>
        private static void BuildEndWall(Transform parent, string name, float x, float hz,
                                         float t, float top, float rise)
        {
            float gapHalf = RoomForgeCanon.DoorGap * 0.5f;
            float side = hz - gapHalf;                       // width of each flanking pillar
            float doorH = RoomForgeCanon.DoorGap;            // door opening height at each level

            // Flanking pillars, full height, both sides of the door line.
            AddBox(parent, $"{name}_L", new Vector3(x, top * 0.5f, hz - side * 0.5f),
                new Vector3(t, top, side), true);
            AddBox(parent, $"{name}_R", new Vector3(x, top * 0.5f, -hz + side * 0.5f),
                new Vector3(t, top, side), true);

            // Between the pillars: lintel above the lower door, the slab between the two doors,
            // and the lintel above the upper door. The two voids left are the openings.
            float lowerTop = doorH;
            float upperBottom = rise;
            float upperTop = rise + doorH;

            AddBox(parent, $"{name}_Mid", new Vector3(x, (lowerTop + upperBottom) * 0.5f, 0f),
                new Vector3(t, upperBottom - lowerTop, RoomForgeCanon.DoorGap), true);
            AddBox(parent, $"{name}_Head", new Vector3(x, (upperTop + top) * 0.5f, 0f),
                new Vector3(t, top - upperTop, RoomForgeCanon.DoorGap), true);
        }

        /// <summary>Visual steps (NO colliders) plus ONE invisible ramp Cube that carries the
        /// BoxCollider. This split is what makes the flight walkable: NavMeshSurface collects
        /// PhysicsColliders, so a stepped visual would rasterise as a saw and fragment.</summary>
        private static void BuildFlight(Transform parent, float startX, float run, float rise)
        {
            var flight = new GameObject("Flight");
            flight.transform.SetParent(parent, false);

            // NOTE: no rotation is applied to this container. WO-927's root cause was a 180 deg
            // yaw on exactly such a container, applied AFTER the openings were derived from the
            // same plan - so the flight turned and the openings did not. There is nothing to
            // rotate against here, and nothing should ever be added.

            float stepRun = run / StepCount;
            float stepRise = rise / StepCount;

            for (int i = 0; i < StepCount; i++)
            {
                float x = startX + stepRun * (i + 0.5f);
                float y = rise - stepRise * (i + 0.5f);
                AddBox(flight.transform, $"Step_{i:00}",
                    new Vector3(x, y, 0f), new Vector3(stepRun, stepRise, StairWidth),
                    keepCollider: false);            // VISUAL ONLY - the ramp is the walk surface
            }

            // The walk surface: one slab on the nose line, overshooting both ends so it OVERLAPS
            // each landing rather than meeting it exactly (an exact meeting is a seam the
            // voxeliser can drop).
            float len = Mathf.Sqrt(run * run + rise * rise) + LandingOverlap * 2f;
            var ramp = AddBox(flight.transform, "RampCollider",
                new Vector3(startX + run * 0.5f, rise * 0.5f, 0f),
                new Vector3(len, RampThickness, StairWidth), keepCollider: true);
            ramp.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-rise, run) * Mathf.Rad2Deg);

            var mr = ramp.GetComponent<MeshRenderer>();
            if (mr != null) Object.DestroyImmediate(mr);      // invisible, but solid
        }

        /// <summary>Four sockets: both ends of the LOWER floor, both ends of the UPPER floors.
        /// Each carries its own Y, which SolveMate resolves for free - so an upper-level door
        /// mates through the ORDINARY planar path. This is why the composer needs no change.</summary>
        private static void BuildSockets(Transform parent, float hx, float rise)
        {
            // Outward points AWAY from the room (west sockets face -X = yaw 270, east +X = yaw 90),
            // because SolveMate mates a child's outward against the opposite of the parent's.
            AddSocket(parent, "s_lower_w", new Vector3(-hx, 0f, 0f), 270f, "W");
            AddSocket(parent, "s_lower_e", new Vector3(hx, 0f, 0f), 90f, "E");
            AddSocket(parent, "s_upper_w", new Vector3(-hx, rise, 0f), 270f, "W");
            AddSocket(parent, "s_upper_e", new Vector3(hx, rise, 0f), 90f, "E");
        }

        private static void AddSocket(Transform parent, string id, Vector3 localPos, float yaw, string facing)
        {
            var go = new GameObject(id);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            var sock = go.AddComponent<RoomSocket>();
            sock.id = id;
            sock.type = RoomSocketType.Door;   // an ORDINARY door. No vertical socket type is used.
            sock.facing = facing;
            // Half the canon door gap, not the field's 1.0 default - a socket that under-reports its
            // width lets the mate checks accept a join narrower than the door actually is.
            sock.halfWidth = RoomForgeCanon.DoorGap * 0.5f;
        }

        private static GameObject AddBox(Transform parent, string name, Vector3 localPos,
                                         Vector3 localScale, bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;

            if (!keepCollider)
            {
                var col = go.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
            }
            return go;
        }
    }
}
