// =============================================================================
// DungeonRoomSense (WO-958) — the room-bounds blackboard for the dungeon camera.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.World
//
// WHY THIS EXISTS. WO-958 wants the dungeon camera ROOM-AWARE (shorter boom /
// raised pitch in small rooms), and the room authority — RoomPrefabMeta +
// DungeonRoomBounds — lives in DeNelle.Dungeons, which DeNelle.Village CANNOT
// reference (circular asmdef; the exact constraint DungeonCameraProfile's header
// documents). Reflection is banned. So the data crosses the boundary the same way
// the camera numbers already do: through Core, which BOTH assemblies reference.
//
//   publisher (DeNelle.Dungeons.DungeonRoomSensePublisher, sceneLoaded hook)
//       — computes each composed room's world AABB with the ONE shared math
//         (DungeonRoomBounds.Compute, WO-797) and writes it here;
//   reader (DeNelle.Village.SmartMobileCamera, dungeon profile path only)
//       — asks "which room is the hero in, and how big is it".
//
// Pure storage + a planar containment query. No Unity lifecycle, no allocation
// on the query path. Empty (RoomCount == 0) simply means "no room data" — every
// reader must treat that as the neutral default, never an error (the hand-built
// dungeons and KayKitChallengeOutpost legitimately publish nothing).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Core.World
{
    /// <summary>
    /// WO-958: published world-space room bounds of the currently loaded composed
    /// dungeon, for room-aware camera framing. Written by the Dungeons-side
    /// publisher; read by the Village-side camera. Empty = no room data (fine).
    /// </summary>
    public static class DungeonRoomSense
    {
        /// <summary>One composed room: stable instance id + world AABB.</summary>
        public struct Room
        {
            public string Id;
            public Bounds Bounds;
        }

        private static readonly List<Room> s_rooms = new List<Room>();

        /// <summary>Scene the current room set was published for (null = none).</summary>
        public static string SceneName { get; private set; }

        /// <summary>Number of rooms currently published (0 = no room data).</summary>
        public static int RoomCount => s_rooms.Count;

        /// <summary>
        /// Replace the published room set. Copies the list (caller keeps ownership).
        /// Null or empty behaves like <see cref="Clear"/> with the scene name kept.
        /// </summary>
        public static void Publish(string sceneName, List<Room> rooms)
        {
            s_rooms.Clear();
            if (rooms != null)
                s_rooms.AddRange(rooms);
            SceneName = sceneName;
        }

        /// <summary>Drop all room data (leaving a dungeon / entering a room-less scene).</summary>
        public static void Clear()
        {
            s_rooms.Clear();
            SceneName = null;
        }

        /// <summary>
        /// Find the room whose footprint contains <paramref name="worldPos"/> on XZ
        /// (Y ignored — multi-level rooms stack their AABBs by floor position, and the
        /// camera only needs the footprint the hero stands in). First match wins;
        /// composed rooms do not overlap. False = between rooms / no data.
        /// </summary>
        public static bool TryGetRoomAt(Vector3 worldPos, out Room room)
        {
            for (int i = 0; i < s_rooms.Count; i++)
            {
                if (ContainsXZ(s_rooms[i], worldPos, 0f))
                {
                    room = s_rooms[i];
                    return true;
                }
            }
            room = default;
            return false;
        }

        /// <summary>
        /// Planar containment with slack — used by the camera's sticky current-room
        /// cache so a hero skirting a doorway edge doesn't flap the room (and with it
        /// the seat target) every frame.
        /// </summary>
        public static bool ContainsXZ(in Room room, Vector3 point, float slack)
        {
            Bounds b = room.Bounds;
            return point.x >= b.min.x - slack && point.x <= b.max.x + slack &&
                   point.z >= b.min.z - slack && point.z <= b.max.z + slack;
        }
    }
}
