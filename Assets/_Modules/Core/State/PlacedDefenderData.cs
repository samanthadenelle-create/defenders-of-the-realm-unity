// =============================================================================
// PlacedDefenderData — the persisted Arena-defense record (WO-389 Phase 1).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.State
//
// The defense-layer twin of PlacedStructureData: one of these per pre-placed
// Arena DEFENDER (the CoC-style troops the defender spends a point pool on so
// that when their castle is raided in the Arena these spawn and auto-fight).
// SEPARATE from the normal base buildings (GameState.BaseLayout) — this is the
// defense layout/pool, persisted as GameState.ArenaDefense.
//
// Deliberately grid-cell + discrete yaw (NOT a world transform), exactly like
// PlacedStructureData, so the layout snaps clean AND an async-raid server can
// re-verify it (the defender is offline; their layout runs as a simulation the
// backend must be able to validate — same server-replayable rationale).
//   - itemId   → an ArenaDefenseDef id (ArenaDefenseCatalog.Get) → unit/structure
//   - cellX/Z  → PlacementGrid cell coordinates (snap-clean, integer)
//   - yawSteps → 0..3, multiplied by 90 deg at load
//
// ASSEMBLY NOTE: this lives in DeNelle.Core (NOT Village) even though the spawn
// behaviour lives in Village. Same reason as PlacedStructureData: GameState
// .ArenaDefense is a Core field and Core may not reference Village (CS0234
// circular). Pure data — no Village types, no Unity refs beyond [Serializable].
// Round-trips through the save layer (SaveSchema / GameState), so it stays
// JSON-friendly (Newtonsoft) and additive.
// =============================================================================

using System;

namespace DeNelle.Core.State
{
    /// <summary>
    /// One persisted Arena defender: an <c>ArenaDefenseDef</c> id at a grid cell
    /// with a discrete 90 deg yaw. Grid-relative, not world-relative, so the layout
    /// snaps clean and an async-raid server can re-verify it (the defender is
    /// offline; their defense runs as a simulation). Mirrors
    /// <see cref="PlacedStructureData"/>.
    /// </summary>
    [Serializable]
    public struct PlacedDefenderData
    {
        /// <summary>The ArenaDefenseDef id this defender was placed from.</summary>
        public string itemId;

        /// <summary>PlacementGrid X cell.</summary>
        public int cellX;

        /// <summary>PlacementGrid Z cell.</summary>
        public int cellZ;

        /// <summary>Discrete yaw in 90 deg steps (0..3). World yaw = yawSteps * 90.</summary>
        public int yawSteps;

        public PlacedDefenderData(string itemId, int cellX, int cellZ, int yawSteps)
        {
            this.itemId = itemId;
            this.cellX = cellX;
            this.cellZ = cellZ;
            this.yawSteps = yawSteps;
        }
    }
}
