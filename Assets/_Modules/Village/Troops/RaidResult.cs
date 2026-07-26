// =============================================================================
// RaidResult — the settled outcome of one raid (WO-771.6 V1 scoring, LOCKED
// teleport/deploy loop). Pure data: the star tier (0-3), the %-destruction of the
// garrison, the elapsed clock, and whether the base was fully cleared.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Produced by RaidScoring.Finalize at victory (RaidVictoryController) and consumed
// by the victory screen (EndStateVM.FromRaidVictory) + the loot grant. It is the
// V1, deterministic-ENOUGH result (no fixed-point sim — that is V2 / WO-771.3); the
// numbers come straight off the real-time clear (garrison cleared / boss down /
// under the 180s clock), so a re-watch reproduces the feel, not a byte-exact replay.
// =============================================================================

namespace DeNelle.Village
{
    /// <summary>
    /// The settled outcome of a raid: star tier, destruction fraction, elapsed
    /// seconds, and full-clear flag. Pure value object — no Unity dependency.
    /// </summary>
    public sealed class RaidResult
    {
        /// <summary>Earned stars, 0..3 (see <see cref="RaidScoring.ComputeStars"/>).</summary>
        public int Stars;

        /// <summary>Fraction of the garrison destroyed, 0..1 (1 = full clear).</summary>
        public float DestructionPct;

        /// <summary>Seconds elapsed from raid start to this result.</summary>
        public float ElapsedSeconds;

        /// <summary>The 180s (tunable) raid clock this result was scored against.</summary>
        public float ClockSeconds;

        /// <summary>True when the whole garrison (incl. the boss) was wiped.</summary>
        public bool Cleared;

        /// <summary>Destruction as a whole-number percent 0..100 (for HUD / victory copy).</summary>
        public int DestructionPercent
        {
            get
            {
                int p = UnityEngine.Mathf.RoundToInt(DestructionPct * 100f);
                return UnityEngine.Mathf.Clamp(p, 0, 100);
            }
        }
    }
}
