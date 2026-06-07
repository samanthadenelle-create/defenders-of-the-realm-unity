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
    /// WO-316: one composed member of a RUNTIME role-mix group — a stat block
    /// (<see cref="EnemyDef"/>), the tactical <see cref="EnemyRole"/> to stamp on
    /// the spawned EnemyBrain, and how many to spawn. Built at runtime by
    /// WaveManager / RegionMobSpawner from a family's catalog members, so no
    /// hand-authored <see cref="WaveEnemyGroup"/> asset is required.
    /// </summary>
    public struct ComposedGroupMember
    {
        public EnemyDef  Def;
        public EnemyRole Role;
        public int       Count;

        public ComposedGroupMember(EnemyDef def, EnemyRole role, int count)
        {
            Def   = def;
            Role  = role;
            Count = Mathf.Max(0, count);
        }
    }

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

        /// <summary>
        /// WO-316: spawns a RUNTIME-composed role-mix group (tank + healer + DPS,
        /// etc.) built from <see cref="EnemyDef"/> stat blocks instead of a hand-
        /// authored <see cref="WaveEnemyGroup"/> asset. Each body is built by the
        /// shared <see cref="EnemyFactory"/> (skinned, NavMesh-snapped, on the
        /// Enemy layer), <see cref="Enemy.Configure"/>'d from its REAL def stats,
        /// spread by <paramref name="formation"/>, given its <see cref="EnemyRole"/>
        /// via EnemyBrain, and registered with one <see cref="EnemyGroupCoordinator"/>
        /// so the whole squad holds then charges together. Same tactical wiring as
        /// the prefab path — just composed at runtime, no SO required.
        /// </summary>
        /// <param name="members">The composed role-mix (def + role + count per entry).</param>
        /// <param name="spawnPos">World origin of the group formation.</param>
        /// <param name="heart">The Heart transform enemies march toward (may be null for roamers).</param>
        /// <param name="enemyRoot">Parent transform; keeps the hierarchy tidy. May be null.</param>
        /// <param name="formation">How the squad is spread at the spawn point.</param>
        /// <param name="groupLabel">Human-readable label for logs / coordinator name.</param>
        /// <param name="waveId">Wave number — used to build stable instance ids.</param>
        /// <param name="instanceCounter">Shared, by-ref unique-id counter.</param>
        /// <returns>Every <see cref="Enemy"/> spawned (caller hooks Died / ReachedHeart).</returns>
        public List<Enemy> SpawnComposedGroup(
            IReadOnlyList<ComposedGroupMember> members,
            Vector3        spawnPos,
            Transform      heart,
            Transform      enemyRoot,
            SpawnFormation formation,
            string         groupLabel,
            int            waveId,
            ref int        instanceCounter)
        {
            var spawned = new List<Enemy>();
            if (members == null || members.Count == 0) return spawned;

            // Flat total so formation offsets spread across the whole squad.
            int total = 0;
            foreach (var m in members) total += Mathf.Max(0, m.Count);
            if (total <= 0) return spawned;

            int globalIndex = 0;

            // One coordinator per group — holds suppressed members until the whole
            // squad has spawned, then releases simultaneously (the "charge together"
            // beat). Same lifecycle as the prefab SpawnGroup path.
            var coordGo = new GameObject($"[EnemyGroupCoordinator] {groupLabel} W{waveId}");
            if (enemyRoot != null) coordGo.transform.SetParent(enemyRoot);
            var coordinator = coordGo.AddComponent<EnemyGroupCoordinator>();
            coordinator.Initialise(total);

            foreach (ComposedGroupMember member in members)
            {
                if (member.Def == null || member.Count <= 0) continue;

                for (int i = 0; i < member.Count; i++)
                {
                    Vector3 offset = WaveEnemyGroup.GetFormationOffset(globalIndex++, total, formation);
                    Vector3 rawPos = spawnPos + offset;

                    // Snap to the NavMesh (EnemyFactory also snaps, but face the Heart
                    // from a valid point so the first step reads right).
                    Vector3 pos = rawPos;
                    if (NavMesh.SamplePosition(rawPos, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                        pos = hit.position;

                    Vector3 toHeart = heart != null ? (heart.position - pos) : Vector3.forward;
                    toHeart.y = 0f;
                    Quaternion rot = toHeart.sqrMagnitude > 0.001f
                        ? Quaternion.LookRotation(toHeart)
                        : Quaternion.identity;

                    // Build the body via the shared factory (skinned, hittable, agent).
                    Enemy enemy = EnemyFactory.Build(member.Def, pos, rot, enemyRoot);
                    if (enemy == null) continue;

                    string instanceId = $"grp-w{waveId}-{member.Role}-{instanceCounter++}";
                    enemy.Configure(instanceId, member.Def, heart);   // REAL stats from the def

                    // Stamp the tactical role + register with the coordinator. The
                    // shared EnemyFactory builds a plain Enemy body (no brain — the
                    // wave/roam path uses Enemy's own Heart-march), so a composed
                    // role-mix squad ADDS an EnemyBrain here to drive role targeting
                    // (Tank charges threats, Healer mends allies, DPS focus-fires).
                    var brain = enemy.GetComponent<EnemyBrain>();
                    if (brain == null) brain = enemy.gameObject.AddComponent<EnemyBrain>();
                    brain.Role = member.Role;
                    coordinator.RegisterMember(brain);

                    spawned.Add(enemy);
                }
            }

            coordinator.FinaliseGroup();

            Debug.Log(
                $"[EnemyGroupSpawner] Wave {waveId} — spawned {spawned.Count} enemies " +
                $"from RUNTIME group '{groupLabel}' in {formation} formation " +
                $"(composed role-mix, no SO).");

            return spawned;
        }
    }
}
