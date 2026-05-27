// =============================================================================
// EnemyTacticalState — higher-level tactical posture enum (DEF-72).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// EnemyTacticalState is orthogonal to EnemyState (Core.Combat):
//   • EnemyState     = what the Animator is doing right now (Chase / Attack / Hit…)
//   • EnemyTacticalState = the strategic posture EnemyBrain is in (Rush / Flank…)
//
// The tactical state is set by EnemyBrain based on TacticalData config; it
// changes how DriveNav computes the nav destination rather than what animation
// plays. EnemyGroupCoordinator can also set it (Suppressed → Rush on full spawn).
// =============================================================================

namespace DeNelle.Village
{
    /// <summary>
    /// Strategic posture of an <see cref="EnemyBrain"/>. Controls how the brain
    /// computes its nav destination (direct path, flanking arc, retreat, hold).
    /// </summary>
    public enum EnemyTacticalState
    {
        /// <summary>Default — direct path to the primary target (Heart / structure / hero).</summary>
        Rush = 0,

        /// <summary>
        /// Arc around the target and approach from a perpendicular or rear angle
        /// (<see cref="TacticalData.FlankAngleOffset"/> degrees off the direct path).
        /// </summary>
        Flank = 1,

        /// <summary>
        /// HP has dropped below <see cref="TacticalData.RetreatHealthThreshold"/> — move
        /// away from the primary target until HP recovers or time elapses.
        /// </summary>
        Retreat = 2,

        /// <summary>
        /// Holding in place — waiting for <see cref="EnemyGroupCoordinator.ReleaseAll"/>
        /// to send the whole group at once. Timer counts down from
        /// <see cref="TacticalData.SuppressDelay"/>.
        /// </summary>
        Suppressed = 3,
    }
}
