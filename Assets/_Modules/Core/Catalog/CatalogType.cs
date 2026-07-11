// =============================================================================
// Catalog enums — the type taxonomy + structural kinds (CATALOG_SYSTEM.md).
// Pure data, lives in DeNelle.Core. No behavior, no Village refs.
// =============================================================================
namespace DeNelle.Core.Catalog
{
    /// <summary>Palette tabs — the kinds of thing the catalog can place.</summary>
    public enum CatalogType { Wall, Stairs, Floor, Room, Tower, Gate, Resource, Decoration, Troop, Collector, Support }

    /// <summary>
    /// The BUILD VERB (owner 2026-07-10). ONE generic build-mode entry
    /// (<c>EnterBuildMode(BuildType)</c>) is parameterised by this enum; each value maps
    /// via DATA (build-categories.json → BuildCategoryRegistry) to which
    /// <see cref="CatalogType"/>s feed its palette. <c>Defense</c> → Tower/Wall/Gate;
    /// <c>Collector</c> → the Collector type. Placement/persist stay generic — a collector
    /// places exactly like a tower.
    /// </summary>
    public enum BuildType { Defense, Collector, Support }

    /// <summary>Build grain: one cell, or a pre-arranged bundle of cells.</summary>
    public enum EntryKind { Cell, Composite }

    /// <summary>What an entry contributes to the NavMesh once placed.</summary>
    public enum NavSurfaceKind { None, Walkable, Blocker }

    /// <summary>The surface an entry must sit on — the "placement = role" rule.</summary>
    public enum PlacementSurface { AnyTerrain, Ground, WallWalk, Floor }
}
