// =============================================================================
// ISiegeLootTarget — high-value siege target (CoC collector, WO-664).
// Assembly: DeNelle.Core
// =============================================================================

using UnityEngine;

namespace DeNelle.Core.Combat
{
    /// <summary>
    /// A structure raiders prefer when it holds uncollected loot (pending fill).
    /// </summary>
    public interface ISiegeLootTarget
    {
        Transform LootTransform { get; }
        bool IsLootTargetAlive { get; }
        float PendingLoot { get; }
        /// <summary>0..1 — drives AI priority scaling.</summary>
        float FillFraction { get; }
        /// <summary>Base role weight for EnemyBrain scoring (before distance bias).</summary>
        float SiegeRoleValue { get; }
    }
}