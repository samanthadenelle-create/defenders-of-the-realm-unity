// =============================================================================
// RoomPrefabMeta — catalog metadata on a forged room prefab root.
// -----------------------------------------------------------------------------
// Archetype drives pacing lints (combat / lore / reward ~ 60/20/20 when baking).
// Cell size default comes from RoomForgeCanon.Cell (WO-922 widened it 6u -> 10u);
// never re-type the number here, the builder and the oracles read the same const.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Dungeons.RoomForge
{
    /// <summary>Author metadata stamped on the room prefab root by Room Forge.</summary>
    [DisallowMultipleComponent]
    public sealed class RoomPrefabMeta : MonoBehaviour
    {
        [Tooltip("Stable room id / prefab stem, e.g. EntryHall.")]
        public string roomId = "NewRoom";

        [Tooltip("combat | lore | reward | hub | secret | boss")]
        public string archetype = "combat";

        [Tooltip("Theme palette key for KayKit skin variants (optional).")]
        public string themePalette = "default";

        [Tooltip("Footprint in cells (width x depth on XZ). One cell = RoomForgeCanon.Cell metres.")]
        public Vector2Int footprintCells = new Vector2Int(1, 1);

        [Tooltip("World units per cell (canon = RoomForgeCanon.Cell, 10 m since WO-922).")]
        public float cellSize = RoomForgeCanon.Cell;

        // ── DECLARED VERTICAL SHAFTS (architect review 2026-08-07 §3.2) ──────
        //  A stairwell room's floor has a HOLE in it and its ceiling has a HOLE through it.
        //  Neither can be a single slab covering the footprint, which is what the old
        //  [room-shell] oracle demanded — so it reported "has no 'Floor' child" on six
        //  perfectly correct prefabs and had to be answered rather than obeyed.
        //
        //  WHY DECLARE IT RATHER THAN LOOSEN THE ORACLE. The cheap fix was union-bounds:
        //  collect the pieces, check their combined extent covers the footprint. That would
        //  have passed the EXACT bug found the same day — the connectors shipped a ceiling
        //  built as Ceil_N/S/E/W, a perimeter RING with a permanently open centre, and every
        //  stairwell was open to sky. Union bounds cannot see a hole. Declaring the opening
        //  lets the oracle assert two things instead of one: the surface covers everything
        //  OUTSIDE the shaft, and the shaft is genuinely OPEN.
        //
        //  Rects are in the room's LOCAL XZ, treating Rect.x/.y as the min corner on X/Z.
        //  Empty list = a solid surface, which is every room in the kit except the stair
        //  connectors — so the default costs existing prefabs nothing.

        [Tooltip("Openings in the FLOOR, local XZ (x,y = min corner on X,Z). Empty = solid floor.")]
        public List<Rect> floorShafts = new List<Rect>();

        [Tooltip("Openings in the CEILING, local XZ. NOT the same rects as floorShafts - a climbing " +
                 "flight crosses the ceiling plane at a different, earlier stretch of its run than " +
                 "it crosses the floor plane above.")]
        public List<Rect> ceilingShafts = new List<Rect>();

        /// <summary>World footprint size on XZ.</summary>
        public Vector2 FootprintWorld =>
            new Vector2(footprintCells.x * cellSize, footprintCells.y * cellSize);

        /// <summary>True when the given local XZ point falls inside any declared shaft in the set.</summary>
        public static bool InAnyShaft(List<Rect> shafts, float x, float z)
        {
            if (shafts == null) return false;
            for (int i = 0; i < shafts.Count; i++)
                if (shafts[i].Contains(new Vector2(x, z))) return true;
            return false;
        }
    }
}
