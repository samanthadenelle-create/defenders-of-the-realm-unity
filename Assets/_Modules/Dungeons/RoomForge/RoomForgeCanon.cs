// =============================================================================
// RoomForgeCanon — THE single source of truth for composed-dungeon room metrics.
// -----------------------------------------------------------------------------
// WO-922 (all rooms much wider) + WO-919 (enclose: taller walls + ceilings).
//
// WHY THIS FILE EXISTS
// Before it, every room-shell number lived as a LITERAL in four places that had
// to agree and had no mechanism to: the prefab builder (DefaultDungeonRoomsBuilder),
// the baker's placeholder room (DungeonBaker.CreatePlaceholderRoom), the dresser's
// fallback footprint (DungeonDresser.DressRoom) and - worst - the regression
// oracles, which held a private COPY of the builder's wall height.
//
// A copied oracle constant is not an oracle. DungeonMultiLevelRegression Case 2
// asserted that FloorSeparationY "clears the 2.8u walls" using its OWN
// `const float WallHeight = 2.8f`, so raising the builder's walls to 4.0 - or to
// 7.0 - would have left that case GREEN while stacked floors interpenetrated. The
// oracle must read the same number the geometry is built from; that is this file.
//
// WHERE IT LIVES AND WHY
// The RUNTIME DeNelle.Dungeons assembly, not the editor builder. The assembly
// graph runs DeNelle.Editor -> DeNelle.EditorRegression, so the oracles CANNOT
// reference DefaultDungeonRoomsBuilder without a reference cycle. Both editor
// assemblies already reference DeNelle.Dungeons. This is the same reasoning that
// put DungeonBakerChecks here rather than in the baker.
//
// RULE: read these values, never re-type them. Changing Cell or WallHeight here
// requires a prefab rebuild (Defenders/Dungeon/Build Default Room Prefabs), a
// recompose of every graph, and a re-bake - the generated prefabs on disk do not
// follow a source edit on their own.
// =============================================================================

namespace DeNelle.Dungeons.RoomForge
{
    /// <summary>Shared metric canon for the RoomForge composed-dungeon room kit.</summary>
    public static class RoomForgeCanon
    {
        /// <summary>
        /// WO-922: world units per room cell. Was 6 (a 1x1 room was 6x6 m and read as
        /// cramped); the owner asked for "much wider" rooms, so 10 - a 1x1 room is now
        /// 10x10 m and a 2x2 combat/boss room is 20x20 m (~2.8x the floor area).
        /// Every socket sits on a half-cell (5 m), so the composer's emitted
        /// cell=[round(x),round(y),round(z)] at cellSize=1 stays a lossless integer
        /// round-trip exactly as it was at 6.
        /// </summary>
        public const float Cell = 10f;

        /// <summary>Perimeter/interior wall slab thickness (metres).</summary>
        public const float WallThickness = 0.4f;

        /// <summary>Clear doorway width at an open facing (metres). Human-scale door.</summary>
        public const float DoorGap = 2.2f;

        /// <summary>
        /// WO-919: perimeter wall height. Was 2.8 - roughly chest height on the third-person
        /// camera seat, which is why the owner's 2026-08-07 screenshots are half blue sky
        /// over an open-top box maze. 4.0 puts the wall line well above both the hero's head
        /// and a modest over-the-shoulder camera.
        /// </summary>
        public const float WallHeight = 4f;

        /// <summary>
        /// Choke-room interior masses. WO-919 requires these to be at least
        /// <see cref="WallHeight"/> - 0.2 so a choke cannot be seen over; kept exactly
        /// 0.2 below the perimeter so the interior mass still reads as an inner mass
        /// rather than a second perimeter (it was 0.4 below at the old 2.8/2.4 pair,
        /// which the WO's bound no longer allows).
        /// </summary>
        public const float ChokeWallHeight = WallHeight - 0.2f;

        /// <summary>Floor slab thickness (metres). The slab's TOP surface is local y = 0.</summary>
        public const float FloorSlabThickness = 0.1f;

        /// <summary>
        /// WO-919 ceiling slab thickness (metres). The slab is seated ON the wall top, so a
        /// room occupies y in [-FloorSlabThickness, WallHeight + CeilingThickness].
        /// </summary>
        public const float CeilingThickness = 0.3f;

        /// <summary>
        /// Total vertical space one composed floor consumes: floor slab underside up to the
        /// ceiling slab's top face. THE number a multi-level descent has to clear -
        /// DungeonBakerChecks.FloorSeparationY must exceed this or stacked floors
        /// interpenetrate. At 0.1 + 4.0 + 0.3 = 4.4 m against a 6 m separation there is
        /// 1.6 m of dead space between floors.
        /// </summary>
        public const float FloorOccupiedHeight = FloorSlabThickness + WallHeight + CeilingThickness;
    }
}
