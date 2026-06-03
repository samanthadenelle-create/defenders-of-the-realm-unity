// =============================================================================
// ItemDropWatcher - the hidden MonoBehaviour that subscribes to every enemy/boss
// Died event (READ-ONLY) and rolls drops via ItemDropSystem. Spawned only when
// the feature flag is ON (ItemDropSystem.StartNow). Inert otherwise (never built).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Items
//
// ISOLATION: it only SUBSCRIBES to existing public events:
//   - DeNelle.Village.Enemy.Died        (Action<Enemy>)
//   - DeNelle.Village.DragonBoss.Died   (Action<DragonBoss>)
// It mutates nothing on the enemy. On death it picks the right loot table id
// (enemy def id if a table exists, else the default) and asks ItemDropSystem to
// roll + deposit the materials into the village larder.
//
// Rescan model (mirrors how camp/zone watchers pick up dynamically-spawned
// enemies): a light periodic FindObjectsByType pass subscribes any newly-spawned
// enemy ONCE (tracked by instance id) so wave spawns are covered without touching
// the spawner. Unsubscribing happens implicitly when the enemy is destroyed - the
// tracked-set is pruned of dead/destroyed entries each scan.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Village.Items
{
    [DisallowMultipleComponent]
    public sealed class ItemDropWatcher : MonoBehaviour
    {
        private float _scanInterval = 1.5f;
        private float _nextScan;

        // Enemies we've already wired (by instance id) so we never double-subscribe.
        private readonly HashSet<int> _wiredEnemies = new HashSet<int>();
        private readonly HashSet<int> _wiredBosses = new HashSet<int>();

        public void Configure(float scanInterval)
        {
            _scanInterval = Mathf.Max(0.25f, scanInterval);
            _nextScan = 0f; // scan on first Update
        }

        private void Update()
        {
            if (!ItemDropSystem.Enabled) return;
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + _scanInterval;
            Scan();
        }

        private void Scan()
        {
            // Subscribe any new enemies.
            var enemies = Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);
            if (enemies != null)
            {
                foreach (var e in enemies)
                {
                    if (e == null) continue;
                    int id = e.GetInstanceID();
                    if (_wiredEnemies.Contains(id)) continue;
                    _wiredEnemies.Add(id);
                    e.Died += OnEnemyDied;
                }
            }

            // Subscribe any new bosses.
            var bosses = Object.FindObjectsByType<DragonBoss>(FindObjectsSortMode.None);
            if (bosses != null)
            {
                foreach (var b in bosses)
                {
                    if (b == null) continue;
                    int id = b.GetInstanceID();
                    if (_wiredBosses.Contains(id)) continue;
                    _wiredBosses.Add(id);
                    b.Died += OnBossDied;
                }
            }

            // Prune the tracked sets so they don't grow unbounded across waves.
            // (Destroyed Unity objects compare == null; their ids are simply dropped.)
            PruneDestroyed(_wiredEnemies, enemies);
        }

        private static void PruneDestroyed(HashSet<int> tracked, Enemy[] live)
        {
            if (tracked.Count < 256) return; // cheap guard; only prune when it grows
            var liveIds = new HashSet<int>();
            if (live != null)
                foreach (var e in live) if (e != null) liveIds.Add(e.GetInstanceID());
            tracked.RemoveWhere(id => !liveIds.Contains(id));
        }

        private void OnEnemyDied(Enemy enemy)
        {
            if (enemy == null) return;
            string tableId = ResolveEnemyTable(enemy.EnemyDefId);
            ItemDropSystem.RollAndDeposit(tableId);
        }

        private void OnBossDied(DragonBoss boss)
        {
            string tableId = LootTableCatalog.DefaultBossTableId;
            ItemDropSystem.RollAndDeposit(tableId);
        }

        /// <summary>Prefer a table whose id matches the enemy def id; else the default.</summary>
        private static string ResolveEnemyTable(string enemyDefId)
        {
            if (!string.IsNullOrEmpty(enemyDefId) && LootTableCatalog.Find(enemyDefId) != null)
                return enemyDefId;
            return LootTableCatalog.DefaultEnemyTableId;
        }
    }
}
