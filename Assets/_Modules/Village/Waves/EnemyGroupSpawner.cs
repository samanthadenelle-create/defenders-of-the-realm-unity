// =============================================================================
// EnemyGroupSpawner — instantiates a WaveEnemyGroup in formation (DEF-21/72).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT IT DOES:
//   Spawns all entries from a WaveEnemyGroup at a given world position, spread
//   into the group's chosen formation (Line / Wedge / Scattered). Each spawned
//   enemy is:
//     1. NavMesh-snapped so agents start on a valid surface.
//     2. Parented under the provided root transform.
//     3. Configured via Enemy.Configure(instanceId, null, heart) — null def means
//        the prefab's inspector stats are used as-is (no JSON override).
//     4. Given its role via EnemyBrain.Role.
//
//   DEF-72: SpawnGroup now creates one EnemyGroupCoordinator per group.
//   Every EnemyBrain-carrying enemy is registered with the coordinator, which
//   holds them in Suppressed state until all members are spawned, then releases
//   the whole group simultaneously so they charge together.
//
// ARCHITECTURE:
//   • SpawnGroup returns the List<Enemy> it spawned so WaveManager can add them
//     to _liveEnemies and subscribe to Died / ReachedHeart.
//   • The EnemyGroupCoordinator self-destructs 0.1 s after releasing the group.
//   • This MonoBehaviour carries no per-frame cost — it only runs during the
//     spawn call.
//
// INTEGRATION (WaveManager):
//   Add EnemyGroupSpawner as a component to the WaveManager GameObject (or a
//   child). Wire it into WaveManager._groupSpawner in the inspector.
//   Assign WaveEnemyGroup assets to WaveManager._waveGroupSequence, one per
//   wave slot (index 0 = wave 1, index 1 = wave 2, etc.).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace DeNelle.Village
{
    /// <summary>
    /// Instantiates a <see cref="WaveEnemyGroup"/> at a world position in the
    /// group's chosen formation. Call <see cref="SpawnGroup"/> from
    /// <see cref="WaveManager"/> when a wave starts.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyGroupSpawner : MonoBehaviour
    {
        /// <summary>
        /// Spawns every entry in <paramref name="group"/> at <paramref name="spawnPos"/>,
        /// spread by the group's <see cref="SpawnFormation"/>. Each enemy is
        /// NavMesh-snapped, parented under <paramref name="enemyRoot"/>, and wired
        /// to march toward <paramref name="heart"/>.
        /// </summary>
        /// <param name="group">The group SO defining entries, formation, and threat.</param>
        /// <param name="spawnPos">World origin of the group formation.</param>
        /// <param name="heart">The Heart transform enemies march toward.</param>
        /// <param name="enemyRoot">Parent transform; keeps the hierarchy tidy. May be null.</param>
        /// <param name="waveId">The current wave number — used to build stable instance ids.</param>
        /// <param name="instanceCounter">
        /// Incrementing counter shared with WaveManager so instance ids are unique
        /// across both JSON-batch and group-spawned enemies. Pass by ref.
        /// </param>
        /// <returns>Every <see cref="Enemy"/> component that was successfully spawned.</returns>
        public List<Enemy> SpawnGroup(
            WaveEnemyGroup group,
            Vector3        spawnPos,
            Transform      heart,
            Transform      enemyRoot,
            int            waveId,
            ref int        instanceCounter)
        {
            var spawned = new List<Enemy>();
            if (group == null || group.Entries == null) return spawned;

            // Build a flat index list so formation offsets are relative to the
            // whole group (not per-entry) and the spread looks intentional.
            int total = group.TotalCount;
            int globalIndex = 0;

            // DEF-72: Create an EnemyGroupCoordinator for this group so all
            // members with a SuppressDelay > 0 hold in place until every enemy
            // has spawned, then charge simultaneously.
            var coordGo = new GameObject($"[EnemyGroupCoordinator] {group.GroupName} W{waveId}");
            if (enemyRoot != null) coordGo.transform.SetParent(enemyRoot);
            var coordinator = coordGo.AddComponent<EnemyGroupCoordinator>();
            coordinator.Initialise(total);

            foreach (EnemyGroupEntry entry in group.Entries)
            {
                if (entry == null || entry.Count <= 0) continue;
                if (entry.Prefab == null)
                {
                    Debug.LogError($"[EnemyGroupSpawner] Prefab is null for entry in group '{group.GroupName}' (wave {waveId}). Assign a prefab to this EnemyGroupEntry in the WaveEnemyGroup asset.");
                    continue;
                }

                for (int i = 0; i < entry.Count; i++)
                {
                    Vector3 offset = WaveEnemyGroup.GetFormationOffset(
                        globalIndex++, total, group.Formation);

                    Vector3 rawPos = spawnPos + offset;

                    // Snap to the nearest NavMesh surface so agents don't start
                    // off-mesh (same guard as WaveManager.SpawnOne).
                    Vector3 pos = rawPos;
                    if (NavMesh.SamplePosition(rawPos, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                        pos = hit.position;

                    // Face toward the heart so the first step is in the right direction.
                    Vector3 toHeart = heart != null ? (heart.position - pos) : Vector3.forward;
                    toHeart.y = 0f;
                    Quaternion rot = toHeart.sqrMagnitude > 0.001f
                        ? Quaternion.LookRotation(toHeart)
                        : Quaternion.identity;

                    var go = Instantiate(entry.Prefab, pos, rot, enemyRoot);
                    if (go == null) continue;

                    // Wire Enemy (null def → use prefab inspector stats).
                    var enemy = go.GetComponent<Enemy>();
                    if (enemy == null)
                    {
                        Debug.LogWarning(
                            $"[EnemyGroupSpawner] Prefab '{entry.Prefab.name}' in group " +
                            $"'{group.GroupName}' is missing an Enemy component — skipped.");
                        Destroy(go);
                        continue;
                    }

                    string instanceId = $"grp-w{waveId}-{entry.Role}-{instanceCounter++}";
                    enemy.Configure(instanceId, null, heart);

                    // Wire EnemyBrain role (present when the prefab has EnemyBrain;
                    // no-op if absent so plain Enemy prefabs still work).
                    var brain = go.GetComponent<EnemyBrain>();
                    if (brain != null)
                    {
                        brain.Role = entry.Role;

                        // DEF-72: register with coordinator — suppresses the brain
                        // if its TacticalData.SuppressDelay > 0.
                        coordinator.RegisterMember(brain);
                    }

                    spawned.Add(enemy);
                }
            }

            // DEF-72: all members registered — start suppress timer (or release
            // immediately if no members have a SuppressDelay > 0).
            coordinator.FinaliseGroup();

            Debug.Log(
                $"[EnemyGroupSpawner] Wave {waveId} — spawned {spawned.Count} enemies " +
                $"from group '{group.GroupName}' (ThreatValue {group.ThreatValue}) " +
                $"in {group.Formation} formation.");

            return spawned;
        }
    }
}
