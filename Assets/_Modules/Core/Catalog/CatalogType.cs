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
    /// <see cref="CatalogType"/>s feed its palette. Placement/persist stay generic — a
    /// collector places exactly like a tower.
    ///
    /// WO-673 taxonomy (owner ruling 2026-07-11, display names "Town / Defenses / Walls"):
    /// <c>Town</c> → Resource + Collector (the player-placed functional buildings, behind
    /// ff.strategicplacement); <c>Defense</c> → Tower/Gate (displays "Defenses");
    /// <c>Walls</c> → Wall (split out — claimed-outpost wall canon). <c>Collector</c> /
    /// <c>Support</c> remain as standalone verbs for back-compat (nothing in the HUD
    /// invokes them directly today; Town lists their catalog types).
    /// </summary>
    public enum BuildType { Defense, Collector, Support, Town, Walls }

    /// <summary>Build grain: one cell, or a pre-arranged bundle of cells.</summary>
    public enum EntryKind { Cell, Composite }

    /// <summary>What an entry contributes to the NavMesh once placed.</summary>
    public enum NavSurfaceKind { None, Walkable, Blocker }

    /// <summary>The surface an entry must sit on — the "placement = role" rule.</summary>
    public enum PlacementSurface { AnyTerrain, Ground, WallWalk, Floor }
}
