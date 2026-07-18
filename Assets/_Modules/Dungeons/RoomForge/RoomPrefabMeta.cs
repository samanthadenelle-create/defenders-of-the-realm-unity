// =============================================================================
// RoomPrefabMeta — catalog metadata on a forged room prefab root.
// -----------------------------------------------------------------------------
// Archetype drives pacing lints (combat / lore / reward ~ 60/20/20 when baking).
// Cell size default 6u matches dungeon layout canon.
// =============================================================================

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

        [Tooltip("Footprint in 6u cells (width x depth on XZ).")]
        public Vector2Int footprintCells = new Vector2Int(1, 1);

        [Tooltip("World units per cell (canon 6).")]
        public float cellSize = 6f;

        /// <summary>World footprint size on XZ.</summary>
        public Vector2 FootprintWorld =>
            new Vector2(footprintCells.x * cellSize, footprintCells.y * cellSize);
    }
}
