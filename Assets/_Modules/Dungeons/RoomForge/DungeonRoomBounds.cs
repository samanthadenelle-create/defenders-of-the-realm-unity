// =============================================================================
// DungeonRoomBounds (WO-797) - the ONE room-AABB computation for composed dungeons.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Dungeons (runtime)   Namespace: DeNelle.Dungeons.RoomForge
//
// Deliberately in the RUNTIME assembly (the DungeonBakerChecks idiom) so the editor
// DungeonBaker (bake-time spawner seating), the runtime DungeonRoomBinder
// (already-baked scenes), and the headless regression oracle all compute a room's
// world-space AABB with the EXACT SAME code - never three drifting copies.
//
// Primary source: the RoomPrefabMeta the baked room instance already carries
// (authored footprintCells x cellSize, rotated by the room's yaw - deterministic,
// data-driven). Fallback: renderer-encapsulated bounds for meta-less placeholders.
// Never a hardcoded position.
// =============================================================================

using UnityEngine;

namespace DeNelle.Dungeons.RoomForge
{
    /// <summary>WO-797: shared room world-AABB math for composed dungeons.</summary>
    public static class DungeonRoomBounds
    {
        /// <summary>Vertical size given to a footprint-derived AABB (room kit walls are ~4u).</summary>
        public const float FootprintHeight = 4f;

        /// <summary>
        /// Compute the room instance's world-space AABB. RoomPrefabMeta footprint first
        /// (rotated by the room yaw - 90/270 swap width/depth), renderer bounds fallback.
        /// Returns a zero-size bounds at the room position when neither source exists.
        /// </summary>
        public static Bounds Compute(GameObject roomInstance)
        {
            if (roomInstance == null) return new Bounds(Vector3.zero, Vector3.zero);

            var meta = roomInstance.GetComponent<RoomPrefabMeta>();
            if (meta != null)
            {
                Vector2 fp = meta.FootprintWorld;
                // Yaw in multiples of 90 swaps the XZ extents; anything else keeps the
                // conservative max square (kit rooms only ever bake at right angles).
                float yaw = Mathf.Repeat(roomInstance.transform.eulerAngles.y, 360f);
                bool swapped = Mathf.Abs(Mathf.DeltaAngle(yaw, 90f)) < 1f ||
                               Mathf.Abs(Mathf.DeltaAngle(yaw, 270f)) < 1f;
                bool axisAligned = swapped ||
                               Mathf.Abs(Mathf.DeltaAngle(yaw, 0f)) < 1f ||
                               Mathf.Abs(Mathf.DeltaAngle(yaw, 180f)) < 1f;
                float sx = swapped ? fp.y : fp.x;
                float sz = swapped ? fp.x : fp.y;
                if (!axisAligned)
                {
                    float m = Mathf.Max(fp.x, fp.y);
                    sx = m; sz = m;
                }
                Vector3 center = roomInstance.transform.position + Vector3.up * (FootprintHeight * 0.5f);
                return new Bounds(center, new Vector3(sx, FootprintHeight, sz));
            }

            // Fallback: encapsulate every renderer under the instance (placeholder rooms).
            var renderers = roomInstance.GetComponentsInChildren<Renderer>(false);
            if (renderers != null && renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    b.Encapsulate(renderers[i].bounds);
                return b;
            }

            return new Bounds(roomInstance.transform.position, Vector3.zero);
        }

        /// <summary>
        /// Planar (XZ) containment test - is <paramref name="point"/> inside the room
        /// footprint expanded by <paramref name="slack"/> metres? Y is ignored.
        /// </summary>
        public static bool ContainsXZ(Bounds area, Vector3 point, float slack = 0f)
        {
            return point.x >= area.min.x - slack && point.x <= area.max.x + slack &&
                   point.z >= area.min.z - slack && point.z <= area.max.z + slack;
        }

        /// <summary>Planar (XZ) squared distance from <paramref name="point"/> to the footprint (0 inside).</summary>
        public static float SqrDistanceXZ(Bounds area, Vector3 point)
        {
            float dx = Mathf.Max(0f, Mathf.Max(area.min.x - point.x, point.x - area.max.x));
            float dz = Mathf.Max(0f, Mathf.Max(area.min.z - point.z, point.z - area.max.z));
            return dx * dx + dz * dz;
        }
    }
}
