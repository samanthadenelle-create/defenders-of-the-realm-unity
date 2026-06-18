// =============================================================================
// ItemCapability — the explicit, composable capability flags of an Entry.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.State
//
// THE foundation of the data-driven item model (docs/ITEM_MODEL.md, WO-Item-1).
// Owner-ratified 2026-06-18: capabilities are EXPLICIT flags on the entry — a
// system reads the flag, NEVER the catalog-of-origin. "Which catalog the entry
// sits in" no longer stands in for the capability.
//
// This is a [Flags] enum so an entry composes its behavior as a bitwise OR of
// the capabilities it retains (§1 of the spec): a sword is Carriable|Equippable,
// a potion is Carriable|Usable, an enemy is Targetable|Destructible|AI. The same
// generic reader (inventory, equip, target, …) serves every entry that retains
// the matching flag — no `if (kind == Weapon)`.
//
// Lives in DeNelle.Core (not Village) on purpose: EVERY module — Village, HUD,
// BattleATB, combat — must be able to read the same capability vocabulary.
//
// INVARIANTS (asserted by the regression suite, DeNelle.Editor.DataRegression):
//   - every Weapon/Gear retains Carriable|Equippable
//   - every Consumable retains Carriable|Usable
//   - NO entry retains both Carriable and AI (an item is never an enemy)
// =============================================================================

namespace DeNelle.Core.State
{
    /// <summary>
    /// The composable capabilities an Entry may retain. Behavior is the bitwise sum
    /// of these flags, read off data (§1 of docs/ITEM_MODEL.md) — not a class hierarchy.
    /// </summary>
    [System.Flags]
    public enum ItemCapability
    {
        /// <summary>No capabilities.</summary>
        None = 0,
        /// <summary>In inventory; pickup / carry / stack / sell.</summary>
        Carriable = 1,
        /// <summary>Occupies an equip slot; modifies the wearer.</summary>
        Equippable = 2,
        /// <summary>Consumed / triggered for an effect.</summary>
        Usable = 4,
        /// <summary>Enemies / towers may target it.</summary>
        Targetable = 8,
        /// <summary>Takes damage / can be destroyed.</summary>
        Destructible = 16,
        /// <summary>Player can engage (talk / mine / enter).</summary>
        Interactable = 32,
        /// <summary>Has an upgrade path.</summary>
        Upgradable = 64,
        /// <summary>Self-acts (perceive / path / attack).</summary>
        AI = 128,
    }
}
