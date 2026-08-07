// =============================================================================
// RoomPrefabMeta — catalog metadata on a forged room prefab root.
// -----------------------------------------------------------------------------
// Archetype drives pacing lints (combat / lore / reward ~ 60/20/20 when baking).
// Cell size default comes from RoomForgeCanon.Cell (WO-922 widened it 6u -> 10u);
// never re-type the number here, the builder and the oracles read the same const.
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

        [Tooltip("Footprint in cells (width x depth on XZ). One cell = RoomForgeCanon.Cell metres.")]
        public Vector2Int footprintCells = new Vector2Int(1, 1);

        [Tooltip("World units per cell (canon = RoomForgeCanon.Cell, 10 m since WO-922).")]
        public float cellSize = RoomForgeCanon.Cell;

        /// <summary>World footprint size on XZ.</summary>
        public Vector2 FootprintWorld =>
            new Vector2(footprintCells.x * cellSize, footprintCells.y * cellSize);
    }
}
