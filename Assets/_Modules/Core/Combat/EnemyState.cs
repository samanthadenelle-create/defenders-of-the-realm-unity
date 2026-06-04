// =============================================================================
// EnemyState — village enemy animation / behaviour state enum (DEF-42).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Combat
//
// Shared across both the animation state machine (Animator.SetInteger("State"))
// and the combat-feedback layer. Lives in Core so EnemyBrain (DeNelle.Village)
// can reference it without a Village → Village circular dependency.
//
// ANIMATOR SETUP (integrator note):
//   Create an Integer parameter "State" on each enemy Animator Controller and
//   cast the enum to int when setting it:
//     _animator.SetInteger("State", (int)EnemyState.Chase);
//   Recommended transitions:
//     Idle   → Chase on State == 1
//     Chase  → Attack on State == 2
//     Attack → Chase on State == 1
//     Any    → Hit on Hit trigger
//     Any    → Dead on Dead bool
// =============================================================================

namespace DeNelle.Core.Combat
{
    /// <summary>
    /// The observable behaviour state of a village enemy. Used to drive both the
    /// Animator state machine and the combat-feedback layer (DEF-42).
    /// </summary>
    public enum EnemyState
    {
        /// <summary>Standing still — no target, spawn delay, or suppressed.</summary>
        Idle = 0,

        /// <summary>Marching toward the active nav target.</summary>
        Chase = 1,

        /// <summary>In attack range — winding up or striking.</summary>
        Attack = 2,

        /// <summary>Flinching from a hit — brief interrupt before resuming chase.</summary>
        Hit = 3,

        /// <summary>Dead — death animation playing; GameObject will be destroyed shortly.</summary>
        Dead = 4,
    }
}
