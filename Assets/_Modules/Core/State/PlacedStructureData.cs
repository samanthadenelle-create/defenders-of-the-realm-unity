// =============================================================================
// PlacedStructureData — the persisted base-layout record (WO-108 P0 data spine).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.State
//
// The LOAD-BEARING new piece of Build Mode (build-mode-architecture.md §2): the
// compact, server-replayable unit GameState.BaseLayout stores one of, per placed
// structure. Deliberately grid cells + discrete yaw (NOT a world transform):
//   - itemId   → a CatalogEntry id (CatalogRegistry.Get) → prefab via StructureFactory
//   - cellX/Z  → PlacementGrid cell coordinates (snap-clean, integer)
//   - yawSteps → 0..3, multiplied by 90° at load
//   - level    → upgrade level (1-based; reserved for the P2 upgrade verb)
//
// ASSEMBLY NOTE: this lives in DeNelle.Core (NOT Village) even though the build
// behaviour (PlacementGrid / BuildModeController / loader) lives in Village. The
// reason is the cross-assembly rule: GameState.BaseLayout is a Core field and
// Core may not reference Village (CS0234 circular). The persisted-state record
// types GameState already holds (ZoneState, TribeState, …) follow the exact same
// pattern — pure data in Core, behaviour in Village. Build behaviour reads this
// struct freely because Village → Core is allowed.
//
// Pure serializable data — no logic, no Unity refs beyond [Serializable]. It is
// round-tripped through the save layer (SaveSchema / SaveMigrator / GameState),
// so it stays JSON-friendly (Newtonsoft) and additive.
// =============================================================================

using System;

namespace DeNelle.Core.State
{
    /// <summary>
    /// One persisted player-placed structure: a catalog id at a grid cell with a
    /// discrete 90° yaw and an upgrade level. Grid-relative, not world-relative,
    /// so the layout snaps clean and an async-raid server can re-verify it.
    /// </summary>
    [Serializable]
    public struct PlacedStructureData
    {
        /// <summary>The CatalogEntry id this structure was built from.</summary>
        public string itemId;

        /// <summary>PlacementGrid X cell.</summary>
        public int cellX;

        /// <summary>PlacementGrid Z cell.</summary>
        public int cellZ;

        /// <summary>Discrete yaw in 90° steps (0..3). World yaw = yawSteps * 90.</summary>
        public int yawSteps;

        /// <summary>Upgrade level (1-based). Fresh-placed structures are level 1.</summary>
        public int level;

        public PlacedStructureData(string itemId, int cellX, int cellZ, int yawSteps, int level)
        {
            this.itemId = itemId;
            this.cellX = cellX;
            this.cellZ = cellZ;
            this.yawSteps = yawSteps;
            this.level = level;
        }
    }
}
