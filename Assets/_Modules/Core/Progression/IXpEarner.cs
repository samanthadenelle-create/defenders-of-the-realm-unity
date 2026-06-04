// =============================================================================
// IXpEarner — a thing that earns XP and levels up (the hero, a deployed pet).
// -----------------------------------------------------------------------------
// Lives in DeNelle.Core so the Village ProgressionManager can grant shared
// kill-XP to a Pet (DeNelle.Pets) WITHOUT either gameplay module referencing
// the other — module isolation is the project's key design call, and Village
// and Pets both reference Core but never each other. Earners self-register in
// XpEarnerRegistry under a stable id (the same id the damage ledger attributes
// hits to), so the granter can resolve "who dealt this damage" -> "who to level".
// =============================================================================

using UnityEngine;

namespace DeNelle.Core.Progression
{
    /// <summary>A combatant that accumulates XP and levels up (hero / pet).</summary>
    public interface IXpEarner
    {
        /// <summary>
        /// Stable id used by <see cref="DeNelle.Core.Combat.DamageAttribution"/>
        /// to credit this earner's damage — "hero" for the hero, the pet id for a pet.
        /// </summary>
        string EarnerId { get; }

        /// <summary>World point to float a "LEVEL UP" popup from.</summary>
        Vector3 WorldPosition { get; }

        /// <summary>Current level (1-based).</summary>
        int Level { get; }

        /// <summary>
        /// Adds XP and applies any resulting level-ups. Returns the number of
        /// levels gained this call (0 if none) so the granter can show feedback.
        /// </summary>
        int AddXp(float amount);
    }
}
