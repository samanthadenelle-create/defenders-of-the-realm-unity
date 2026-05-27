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
        [Tooltip("Multiplier applied to this enemy's target-priority score. " +
                 ">1 = more likely to be scored as a priority target by the AI brain.")]
        [Min(0.1f)] public float TargetPriorityBias = 1f;
    }
}
