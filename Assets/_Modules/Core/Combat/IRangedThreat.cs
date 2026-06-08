// =============================================================================
// IRangedThreat — optional cross-module marker for a ranged / kiting attacker.
// -----------------------------------------------------------------------------
// Module-isolation seam (port spec Part 2): DeNelle.Pets cannot reference
// DeNelle.Village, so a pet's combat code cannot see the concrete Enemy type or
// its EnemyRole / EnemyArchetype (those live in Village). To let a pet ability
// COUNTER ranged enemies specifically (WO-128: pet anti-ranged ability), it needs
// a Core-side signal for "this damageable attacks from range".
//
// This is a tiny, OPTIONAL companion to IDamageable — exactly like IDamageTintable.
// A damageable that fights at range implements it; a pet (or any defender) reads
// it via GetComponentInParent<IRangedThreat>() on a candidate target. A target
// that does NOT implement it is simply treated as melee — so this is fully
// additive: nothing breaks if no enemy implements it yet.
//
// INTEGRATOR SEAM (one line, NOT done here to keep WO-128 inside the pet):
//   The Village enemy adapter (EnemyDamageable) — or Enemy itself — should add
//   IRangedThreat and return true when its EnemyBrain.Role == EnemyRole.Ranged or
//   its TacticalData.Archetype == EnemyArchetype.Kiter. Until then the pet's
//   anti-ranged dash gracefully no-ops and normal hunting is unchanged.
// =============================================================================

namespace DeNelle.Core.Combat
{
    /// <summary>
    /// Optional companion to <see cref="IDamageable"/>: marks a target as a
    /// RANGED attacker (archer / kiter / caster) that strikes from a standoff
    /// distance rather than in melee. Lets a defender ability (e.g. a pet's
    /// anti-ranged dash) single these out and disrupt them, without referencing
    /// the concrete enemy type from another module. Cross-module (Core) so both
    /// DeNelle.Village (enemies) and DeNelle.Pets (the counter) can use it.
    /// </summary>
    public interface IRangedThreat
    {
        /// <summary>
        /// True while this target is actively functioning as a ranged attacker
        /// (e.g. a live kiter holding standoff). An implementor may return false
        /// once disabled / converted to melee so the counter stops prioritising it.
        /// </summary>
        bool IsRangedAttacker { get; }
    }
}
