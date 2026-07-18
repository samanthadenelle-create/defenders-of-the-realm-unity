// =============================================================================
// RoomSocketType — standardized doorway / passage / stair sockets for Room Forge.
// -----------------------------------------------------------------------------
// Cell grain = 6u (matches DungeonLayout schema notes + KayKit room footprints).
// Door-touch-door bake requires mating sockets of compatible types.
// =============================================================================

namespace DeNelle.Dungeons.RoomForge
{
    /// <summary>
    /// Socket kinds on a room prefab. Keep the set small for KayKit reuse
    /// (door_wood / door_iron / door_barred, open arches, stairs).
    /// </summary>
    public enum RoomSocketType
    {
        /// <summary>Standard door (door_wood / door_iron / door_barred family).</summary>
        Door = 0,
        /// <summary>Wide open passage (no swinging door mesh required).</summary>
        Arch = 1,
        /// <summary>Vertical connect up (mates with <see cref="StairDown"/>).</summary>
        StairUp = 2,
        /// <summary>Vertical connect down (mates with <see cref="StairUp"/>).</summary>
        StairDown = 3,
    }
}
