// =============================================================================
// WaveEnemyGroup — editor-tunable wave composition (DEF-21).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT IT DOES:
//   Defines the role taxonomy (EnemyRole), the per-entry spawn record
//   (EnemyGroupEntry), the three formation shapes (SpawnFormation), and the
//   WaveEnemyGroup ScriptableObject that bundles them into one editor asset.
//
// DESIGN:
//   WaveEnemyGroup is the editor-facing alternative to waves.json for
//   designers who want to tune wave composition without touching JSON. Both
//   systems coexist — WaveManager fires JSON batches AND any WaveEnemyGroup
//   assigned to the same wave slot. Leave waves.json entries empty if you want
//   group assets to be the sole composition source for a wave.
//
// ROLES vs AI KINDS:
//   EnemyRole is the *tactical role* (what the enemy targets / does).
//   EnemyAiKind (in WaveData.cs) is the *movement archetype* (how it moves).
//   They are orthogonal — a Charger can be a DPS, a Walker can be a Tank.
//
// FORMATION:
//   GetFormationOffset() returns per-index world-space offsets so a group of
//   enemies spawns in a spread pattern rather than a pile. EnemyGroupSpawner
//   calls this when building spawn positions.
// =============================================================================

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DeNelle.Village
{
    // =========================================================================
    //  EnemyRole — tactical behaviour taxonomy
    // =========================================================================

    /// <summary>
    /// The tactical role an enemy plays within its wave group. Drives
    /// <see cref="EnemyBrain"/>'s target-selection logic.
    /// </summary>
    public enum EnemyRole
    {
        /// <summary>
        /// High HP, charges the highest-threat target (hero or damaging structure)
        /// to pull aggro away from the group's glass-cannon members.
        /// </summary>
        Tank = 0,

        /// <summary>
        /// Stays near wounded allies and periodically restores their HP.
        /// Priority kill-target — must be killed before the group.
        /// </summary>
        Healer = 1,

        /// <summary>
        /// Medium HP, fast, marches straight for the Heart of Elarion.
        /// </summary>
        DPS = 2,

        /// <summary>
        /// Fast; peels off toward high-value towers when in range.
        /// (Same nav logic as DPS today — extended in the behaviour-tree pass.)
        /// </summary>
        Ranged = 3,

        /// <summary>
        /// Boss-tier unit within a group. Uses DPS target priority with
        /// elevated threat scanning range.
        /// </summary>
        MiniBoss = 4,
    }

    // =========================================================================
    //  SpawnFormation — group shape
    // =========================================================================

    /// <summary>
    /// The spatial pattern enemies in a group are released in.
    /// </summary>
    public enum SpawnFormation
    {
        /// <summary>Side-by-side horizontal row.</summary>
        Line = 0,

        /// <summary>Inverted-V arrowhead — flanks lead, centre trails.</summary>
        Wedge = 1,

        /// <summary>Random spread within a bounded rectangle.</summary>
        Scattered = 2,
    }

    // =========================================================================
    //  EnemyGroupEntry — one spawn batch within a group
    // =========================================================================

    /// <summary>One type of enemy within a <see cref="WaveEnemyGroup"/>.</summary>
    [System.Serializable]
    public sealed class EnemyGroupEntry
    {
        [Tooltip("Enemy prefab to instantiate. Must carry Enemy + EnemyBrain + NavMeshAgent.")]
        public GameObject Prefab;

        [Tooltip("Tactical role assigned to EnemyBrain on spawn.")]
        public EnemyRole Role = EnemyRole.DPS;

        [Tooltip("How many of this entry to spawn.")]
        [Min(1)] public int Count = 1;
    }

    // =========================================================================
    //  WaveEnemyGroup ScriptableObject
    // =========================================================================

    /// <summary>
    /// One group of enemies released together at a spawn point. Create via
    /// <b>Assets → Create → Defenders / Wave Enemy Group</b> and assign to
    /// <see cref="WaveManager._waveGroupSequence"/> in the inspector.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WaveGroup_LightRaid",
        menuName  = "Defenders/Waves/Wave Enemy Group",
        order     = 51)]
    public sealed class WaveEnemyGroup : ScriptableObject
    {
        [Tooltip("Human-readable name shown in WaveManager inspector list.")]
        public string GroupName = "New Group";

        [Tooltip("How enemies are spread at the spawn point.")]
        public SpawnFormation Formation = SpawnFormation.Line;

        [Tooltip("Abstract difficulty value used by WaveManager to balance waves. " +
                 "Light Raid = 3 | Balanced Push = 5 | Heavy Assault = 9 | Boss Wave = 18.")]
        [Min(0)] public int ThreatValue = 5;

        [Tooltip("The enemy types and counts that make up this group.")]
        public List<EnemyGroupEntry> Entries = new List<EnemyGroupEntry>();

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>Total enemy count across all entries.</summary>
        public int TotalCount => Entries == null ? 0 : Entries.Sum(e => e != null ? Mathf.Max(0, e.Count) : 0);

        /// <summary>
        /// Returns the world-space spawn offset for a unit at <paramref name="index"/>
        /// (0-based) within a group of <paramref name="total"/> units using the
        /// given <paramref name="formation"/>. The offset is relative to the
        /// group's spawn origin — add to the spawn point's world position.
        /// </summary>
        public static Vector3 GetFormationOffset(int index, int total, SpawnFormation formation)
        {
            if (total <= 1) return Vector3.zero;

            switch (formation)
            {
                case SpawnFormation.Line:
                    // Horizontal row centred on the spawn point.
                    return new Vector3((index - total / 2f) * 1.5f, 0f, 0f);

                case SpawnFormation.Wedge:
                    // Arrowhead: each row steps 0.8 m further back; members spread 1.2 m apart.
                    return new Vector3((index - total / 2f) * 1.2f, 0f, index * 0.8f);

                case SpawnFormation.Scattered:
                    // Deterministic spread from a seeded offset so the pattern is
                    // consistent across playtests. Uses index as seed.
                    UnityEngine.Random.InitState(index * 1327 + total * 37);
                    return new Vector3(
                        UnityEngine.Random.Range(-3f, 3f), 0f,
                        UnityEngine.Random.Range(-2f, 2f));

                default:
                    return Vector3.zero;
            }
        }
    }
}
