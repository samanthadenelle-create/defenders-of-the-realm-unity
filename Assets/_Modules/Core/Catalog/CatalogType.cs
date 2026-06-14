// =============================================================================
// Catalog enums — the type taxonomy + structural kinds (CATALOG_SYSTEM.md).
// Pure data, lives in DeNelle.Core. No behavior, no Village refs.
// =============================================================================
namespace DeNelle.Core.Catalog
{
    /// <summary>Palette tabs — the kinds of thing the catalog can place.</summary>
    public enum CatalogType { Wall, Stairs, Floor, Room, Tower, Gate, Resource, Decoration, Troop }

    /// <summary>Build grain: one cell, or a pre-arranged bundle of cells.</summary>
    public enum EntryKind { Cell, Composite }

    /// <summary>What an entry contributes to the NavMesh once placed.</summary>
    public enum NavSurfaceKind { None, Walkable, Blocker }

    /// <summary>The surface an entry must sit on — the "placement = role" rule.</summary>
    public enum PlacementSurface { AnyTerrain, Ground, WallWalk, Floor }
}
