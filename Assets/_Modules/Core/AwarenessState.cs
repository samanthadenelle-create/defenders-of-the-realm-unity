// =============================================================================
// AwarenessState — escalating perception/awareness level (WO-147).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core
//
// AwarenessState is ORTHOGONAL to EnemyTacticalState (Village):
//   • EnemyTacticalState = the strategic POSTURE (Rush / Flank / Retreat / Kite…)
//                          — HOW the enemy moves.
//   • AwarenessState     = HOW MUCH the enemy has PERCEIVED (Unaware / Alerted /
//                          Engaged) — drives scan cadence (LOD) and the Animator
//                          "IsAlert" pose.
//
// Lives in Core so a future HUD / save / SO and the Animator IsAlert mapping can
// reference it without a Village dependency. Append-only — do not renumber.
// =============================================================================

namespace DeNelle.Core
{
    /// <summary>
    /// How much an enemy has perceived of its surroundings. Computed by
    /// <c>AwarenessSensor</c> (Village) and escalated/decayed from perceived
    /// threats. Orthogonal to the enemy's tactical posture.
    /// </summary>
    public enum AwarenessState
    {
        /// <summary>No hero / pet / threat perceived — marching or roaming. Slow scan cadence.</summary>
        Unaware = 0,

        /// <summary>
        /// A threat was perceived (hero / pet entered radius, an ally is dying, took
        /// damage, or a shared family alert arrived) but not yet committed. Fast
        /// cadence; Animator <c>IsAlert</c> = true.
        /// </summary>
        Alerted = 1,

        /// <summary>
        /// A committed offensive target is in range (or the enemy is taking hits).
        /// Full combat responsiveness; Animator <c>IsAlert</c> = true.
        /// </summary>
        Engaged = 2,
    }
}
