// =============================================================================
// TacticalData — per-archetype tactical AI config ScriptableObject (DEF-72).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Data
//
// WHAT IT DOES:
//   Holds the tuning knobs that differentiate enemy archetypes at the tactical
//   AI level (how they approach, when they retreat, how long they wait in a
//   suppressed group). Assign one asset per archetype in the enemy prefab's
//   EnemyBrain inspector slot.
//
// USAGE:
//   1. Create via Assets → Create → Defenders / Enemies / Tactical Data.
//   2. Configure archetype, flank angle, retreat threshold, suppress delay.
//   3. Assign to EnemyBrain._tactics in the enemy prefab inspector.
//   4. Leave blank for default Rush behaviour (no tactical overlay).
//
// ARCHETYPES vs ROLES:
//   EnemyArchetype (this file) = movement/tactical pattern.
//   EnemyRole (WaveEnemyGroup.cs) = group function (Tank/Healer/DPS).
//   They are orthogonal — a Tank can be a Flanker, a DPS can be a Siege unit.
// =============================================================================

using UnityEngine;

namespace DeNelle.Core.Data
{
    /// <summary>
    /// Movement archetype — controls how <see cref="EnemyBrain"/> computes its
    /// nav destination when a <see cref="TacticalData"/> asset is assigned.
    /// </summary>
    public enum EnemyArchetype
    {
        /// <summary>Default — straight march, no tactical modifier.</summary>
        Standard = 0,

        /// <summary>Arcs wide around the target and attacks from the side/rear.</summary>
        Flanker = 1,

        /// <summary>Slow, tanky, direct path — high damage, large agent radius.</summary>
        Siege = 2,

        /// <summary>
        /// Flying unit — uses a separate NavMesh surface baked at altitude.
        /// Falls back to ground path if the air surface is not baked.
        /// </summary>
        Flyer = 3,

        /// <summary>Stays in the centre of the nearest ally cluster to provide support.</summary>
        Support = 4,

        /// <summary>Boss-tier — uses BossWaveConfig phases + optional add summoning.</summary>
        Boss = 5,

        /// <summary>
        /// WO-145 (Tactic B): maintains a standoff range and fires ranged attacks
        /// (the canonical "ranged kiter" when combined with EnemyRole.Ranged).
        /// </summary>
        Kiter = 6,
    }

    /// <summary>
    /// Tactical AI configuration for one enemy archetype. Assign to
    /// <c>EnemyBrain._tactics</c> in the enemy prefab inspector.
    /// Create via <b>Assets → Create → Defenders / Enemies / Tactical Data</b>.
    /// </summary>
    [CreateAssetMenu(
        fileName = "TacticalData_Standard",
        menuName  = "Defenders/Enemies/Tactical Data",
        order     = 52)]
    public sealed class TacticalData : ScriptableObject
    {
        [Header("Archetype")]
        [Tooltip("Movement/tactical pattern for this enemy type.")]
        public EnemyArchetype Archetype = EnemyArchetype.Standard;

        [Header("Flanker settings")]
        [Tooltip("Degrees to offset the nav destination perpendicular to the direct-path vector. " +
                 "90 = pure side approach; 180 = rear approach. Only used by Flanker archetype.")]
        [Range(30f, 180f)] public float FlankAngleOffset = 90f;

        [Header("Retreat")]
        [Tooltip("HP fraction (0-1) below which the enemy switches to Retreat state. " +
                 "0 = never retreat.")]
        [Range(0f, 0.5f)] public float RetreatHealthThreshold = 0.15f;

        [Header("Suppressed group")]
        [Tooltip("Seconds this enemy waits in Suppressed state after spawning, " +
                 "before EnemyGroupCoordinator releases the whole group at once. " +
                 "0 = no suppression (charges immediately).")]
        [Min(0f)] public float SuppressDelay = 1.5f;

        [Header("Target priority")]
        [Tooltip("WO-145 (Tactic A): multiplier this attacker applies to its whole " +
                 "target-priority cluster (role-value + low-HP + threat). >1 = this " +
                 "enemy abandons a nearer target to dive a high-priority one (pet / " +
                 "wounded defender). 1 ≈ today's nearest-ish behaviour. NOW READ by " +
                 "EnemyBrain.ScoreAndPickTarget.")]
        [Min(0.1f)] public float TargetPriorityBias = 1f;

        // ── WO-145 additions (append-only — existing fields above unchanged) ──

        [Header("Target scoring (WO-145 Tactic A)")]
        [Tooltip("Weight of the designer role-value term (pet / healer-class high, Heart low).")]
        [Min(0f)] public float RoleValueWeight = 1f;

        [Tooltip("Weight of the (1 - target HpFraction) term — higher score for wounded / squishy targets.")]
        [Min(0f)] public float LowHpWeight = 1f;

        [Tooltip("Weight of the target's damage-threat term (hero / live tower pressure).")]
        [Min(0f)] public float ThreatWeight = 1f;

        [Tooltip("Weight of the nearness term (nearer = slightly preferred). Dominant " +
                 "for the Standard archetype so defaults reproduce today's nearest-ish feel.")]
        [Min(0f)] public float DistanceWeight = 1f;

        [Header("Kiting (WO-145 Tactic B — requires EnemyArchetype.Kiter)")]
        [Tooltip("Preferred standoff distance the kiter holds from its target.")]
        [Min(0f)] public float KiteDesiredRange = 8f;

        [Tooltip("Back off when the target closes inside this distance.")]
        [Min(0f)] public float KiteMinRange = 5f;

        [Tooltip("0 = stand still in the band; >0 = lateral weave magnitude while holding (feels alive).")]
        [Min(0f)] public float KiteStrafeJitter = 0f;

        [Tooltip("Seconds between ranged shots while a kiter holds the band (slower than melee = not oppressive).")]
        [Min(0.1f)] public float KiteAttackCooldown = 1.6f;

        [Header("Coordinated flank (WO-145 Tactic C)")]
        [Tooltip("When set, a Suppressed group of these enemies fans to distinct L/R/rear " +
                 "bearings on release (a real pincer) via EnemyGroupCoordinator, instead of " +
                 "every member arcing the same FlankAngleOffset side.")]
        public bool CoordinatedFlank = false;

        [Header("Reposition (WO-145 Tactic D)")]
        [Tooltip("When set, dropping below RetreatHealthThreshold enters Reposition " +
                 "(rally to the ally cluster, then re-engage) instead of a blind Retreat away.")]
        public bool RepositionInsteadOfFlee = false;

        [Tooltip("Search radius for the living-ally cluster centroid to rally toward.")]
        [Min(1f)] public float RallyScanRadius = 12f;

        [Tooltip("Standoff fallback distance from the target when no allies are found to rally to.")]
        [Min(1f)] public float RepositionFallbackDistance = 8f;

        [Tooltip("HP fraction at or above which a repositioning enemy re-commits and rejoins the fight.")]
        [Range(0f, 1f)] public float ReengageHealthThreshold = 0.5f;

        [Tooltip("Seconds spent at the rally point before re-engaging even if HP hasn't recovered.")]
        [Min(0f)] public float RepositionRegroupSeconds = 3f;
    }
}
