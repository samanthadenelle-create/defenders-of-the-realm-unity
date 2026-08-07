// =============================================================================
// DungeonDressingRegression [dungeon-dressing] -- BEHAVIORAL proof of seating.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. A composed dungeon should read as a DRESSED
// place -- torches, barrels, decor seated into each composed room. The composed
// (RoomForge) pipeline now HAS a dressing pass: DungeonDresser.DressRoom seats
// cosmetic props (colliders stripped, against walls, doorway clearance) into each
// composed room, wired into DungeonBaker before the NavMesh bake.
//
// This oracle no longer NAME-SCANS (a stub method would fool that). It PROVES
// seating behaviorally: it builds the smallest real composed room the dresser
// needs (a room root with RoomPrefabMeta + floor + four cardinal door sockets,
// mirroring DungeonBaker.CreatePlaceholderRoom), invokes the REAL
// DungeonDresser.DressRoom by reflection (DeNelle.EditorRegression cannot
// reference DeNelle.Editor -- the baker/composer are resolved the same way), and
// asserts the room gained > 0 "Dressing_*" prop children. It cleans up the
// instantiated GameObjects with DestroyImmediate.
//
// Marker: DUNGEON_DRESSING_OK / DUNGEON_DRESSING_FAIL. Expected: GREEN (seated).
//
// Wire (DataRegression.RunAll):
//   if (!DungeonDressingRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dungeon-dressing] " + r);
// =============================================================================
using System;
using System.Reflection;
using System.Text;
using UnityEngine;
using DeNelle.Dungeons.RoomForge;

namespace DeNelle.Editor
{
    public static class DungeonDressingRegression
    {
        private const string DresserTypeName = "DeNelle.Editor.RoomForge.DungeonDresser";
        private const int MinProps = 1; // a dressed room must gain at least one real prop child

        public static bool Run(out string reason)
        {
            var log = new StringBuilder();
            log.AppendLine("--- DUNGEON DRESSING (composed room gains >0 prop children after DungeonDresser.DressRoom) ---");

            // Resolve the real dressing entrypoint by reflection.
            var dresser = FindType(DresserTypeName);
            if (dresser == null)
            {
                reason = "dungeon-dressing: DungeonDresser type not found -- the composed (RoomForge) pipeline has no dressing pass. " +
                         "Add DeNelle.Editor.RoomForge.DungeonDresser.DressRoom(GameObject,int) that seats props into composed rooms.";
                Debug.LogError(log.ToString() + "DUNGEON_DRESSING_FAIL: " + reason);
                return false;
            }
            var dressRoom = dresser.GetMethod("DressRoom", BindingFlags.Public | BindingFlags.Static,
                                              null, new[] { typeof(GameObject), typeof(int) }, null);
            if (dressRoom == null)
            {
                reason = "dungeon-dressing: DungeonDresser.DressRoom(GameObject,int) not found -- dressing entrypoint missing.";
                Debug.LogError(log.ToString() + "DUNGEON_DRESSING_FAIL: " + reason);
                return false;
            }

            GameObject room = null;
            try
            {
                room = BuildMinimalRoom();
                int before = CountDressingChildren(room);

                object ret = dressRoom.Invoke(null, new object[] { room, 0 });
                int reported = ret is int ri ? ri : -1;

                int after = CountDressingChildren(room);
                int seated = after - before;

                log.AppendLine($"  built minimal room (RoomPrefabMeta {RoomForgeCanon.Cell:0.#}u footprint + floor + 4 door sockets); " +
                               $"DressRoom returned {reported}; room gained {seated} 'Dressing_*' prop child(ren) (before={before} after={after})");

                if (seated < MinProps)
                {
                    reason = $"dungeon-dressing: DressRoom seated {seated} prop children (expected >= {MinProps}) -- " +
                             "the dressing pass did not seat real props into the room.";
                    Debug.LogError(log.ToString() + "DUNGEON_DRESSING_FAIL: " + reason);
                    return false;
                }
                if (reported >= 0 && reported != seated)
                    log.AppendLine($"  (note: reported count {reported} != counted 'Dressing_*' children {seated})");

                reason = $"DUNGEON DRESSING OK -- DungeonDresser.DressRoom seated {seated} real prop children into a composed room";
                Debug.Log(log.ToString() + "DUNGEON_DRESSING_OK");
                return true;
            }
            catch (Exception ex)
            {
                reason = "dungeon-dressing: exception invoking DressRoom -- " + ex.GetBaseException().Message;
                Debug.LogError(log.ToString() + "DUNGEON_DRESSING_FAIL: " + reason);
                return false;
            }
            finally
            {
                if (room != null) UnityEngine.Object.DestroyImmediate(room);
            }
        }

        // Smallest real composed room the dresser needs: a root with RoomPrefabMeta (footprint),
        // a floor, and four cardinal door sockets (the doorway clearance the dresser must respect).
        // Mirrors DungeonBaker.CreatePlaceholderRoom.
        private static GameObject BuildMinimalRoom()
        {
            var room = new GameObject("DressingRegressionRoom");
            var meta = room.AddComponent<RoomPrefabMeta>();
            meta.roomId = "DressingRegressionRoom";
            meta.archetype = "combat";
            // WO-922: derived from the canon cell, not a hardcoded 6. The dresser insets props
            // from halfW/halfD and skips anchors NearSocket(...) - both of which are measured in
            // metres - so a fixture stuck at 6u while the kit ships 10u would stop exercising the
            // real corner/doorway clearances this suite exists to guard.
            float span = RoomForgeCanon.Cell;
            float half = span * 0.5f;

            meta.cellSize = span;
            meta.footprintCells = Vector2Int.one;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(room.transform, false);
            floor.transform.localPosition = new Vector3(0f, -RoomForgeCanon.FloorSlabThickness * 0.5f, 0f);
            floor.transform.localScale = new Vector3(span, RoomForgeCanon.FloorSlabThickness, span);

            AddSocket(room, "n_door_01", new Vector3(0f, 0f, half), Vector3.forward);
            AddSocket(room, "s_door_01", new Vector3(0f, 0f, -half), Vector3.back);
            AddSocket(room, "e_door_01", new Vector3(half, 0f, 0f), Vector3.right);
            AddSocket(room, "w_door_01", new Vector3(-half, 0f, 0f), Vector3.left);
            return room;
        }

        private static void AddSocket(GameObject room, string id, Vector3 local, Vector3 outward)
        {
            var sgo = new GameObject($"Socket_{id}");
            sgo.transform.SetParent(room.transform, false);
            sgo.transform.localPosition = local;
            sgo.transform.localRotation = Quaternion.LookRotation(outward);
            var sock = sgo.AddComponent<RoomSocket>();
            sock.id = id;
            sock.type = RoomSocketType.Door;
        }

        // Count DIRECT children the dresser seated (its holders are named "Dressing_*").
        private static int CountDressingChildren(GameObject room)
        {
            int n = 0;
            foreach (Transform child in room.transform)
                if (child != null && child.name.StartsWith("Dressing_", StringComparison.Ordinal)) n++;
            return n;
        }

        private static Type FindType(string full)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(full, false);
                if (t != null) return t;
            }
            return null;
        }
    }
}
