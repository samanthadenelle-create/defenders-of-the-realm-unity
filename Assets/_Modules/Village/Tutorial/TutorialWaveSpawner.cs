// =============================================================================
// TutorialWaveSpawner — the scripted first-combat wave for the FTUE (WO-277).
// -----------------------------------------------------------------------------
// Scene 3: "HORN BLAST — enemies spawn AT THIS GATE (nearest to hero, not
// random)". This spawns a small, fixed roster (2-3 enemies) at ONE specific
// gate's WaveSpawnPoint by reusing the wave loop's own spawn path —
// WaveManager.SpawnEnemyForExternalMode — so the tutorial enemies are real
// Enemy instances the hero + companion can fight and kill, configured to march
// the Heart exactly like a normal wave.
//
// It deliberately does NOT touch the WaveManager wave loop / wave balance: it
// borrows the spawn helper (already public, built for external modes like the
// Defend-the-Tower shooter) and owns the spawned enemies' lifecycle itself. The
// director awaits IsCleared before granting the post-battle supplies.
//
// Isolation/safety: lives in DeNelle.Village (drives WaveManager + Enemy
// directly). Null-safe — with no WaveManager / no spawn point / no enemy def it
// reports cleared so the tutorial proceeds rather than wedging.
// =============================================================================

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Spawns the tutorial's small first wave at one specific gate (the gate
    /// nearest the hero) via <see cref="WaveManager.SpawnEnemyForExternalMode"/>
    /// and tracks the spawned enemies until they are all dead.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TutorialWaveSpawner : MonoBehaviour
    {
        // Preferred basic enemy id for the teaching wave — a plain melee walker.
        // Falls back to the first catalog entry if this id isn't present.
        private const string PreferredEnemyId = "hollow-walker";

        private WaveManager _wave;
        private readonly List<Enemy> _spawned = new List<Enemy>();
        private bool _spawnRequested;

        /// <summary>True once every spawned tutorial enemy is dead / gone.</summary>
        public bool IsCleared
        {
            get
            {
                // Not yet asked to spawn → not "cleared" (the director spawns first).
                if (!_spawnRequested) return false;
                PruneDead();
                return _spawned.Count == 0;
            }
        }

        /// <summary>Assigns the wave manager whose spawn path the tutorial reuses.</summary>
        public void SetWaveManager(WaveManager wave) => _wave = wave;

        /// <summary>
        /// Spawns <paramref name="count"/> enemies at <paramref name="spawnPoint"/>
        /// (the gate nearest the hero), marching the Heart. Reuses the wave loop's
        /// own instantiate/Configure path so they are real, killable wave enemies.
        /// </summary>
        public async UniTask SpawnAt(WaveSpawnPoint spawnPoint, int count)
        {
            _spawnRequested = true;
            _spawned.Clear();

            if (_wave == null || spawnPoint == null)
            {
                Debug.LogWarning("[TutorialWaveSpawner] No WaveManager / spawn point — " +
                                 "skipping the tutorial wave (IsCleared returns true).");
                return;
            }

            // Pull a basic enemy def from the SAME catalog the wave loop uses.
            EnemyCatalog catalog = await _wave.GetEnemyCatalogAsync();
            EnemyDef def = ResolveEnemyDef(catalog);
            if (def == null)
            {
                Debug.LogWarning("[TutorialWaveSpawner] Enemy catalog empty — no tutorial enemies spawned.");
                return;
            }

            // The wave loop is held closed during the FTUE, so WaveManager.Heart may
            // not be resolved yet — find the HeartController directly so the tutorial
            // enemies still march the Heart (not a forward-fallback heading).
            HeartController heartCtrl = _wave.Heart != null
                ? _wave.Heart
                : FindObjectOfType<HeartController>();
            Transform heart = heartCtrl != null ? heartCtrl.transform : null;
            Vector3 basePos = spawnPoint.transform.position;
            Vector3 heading = spawnPoint.HeadingToGate;
            Vector3 lateral = Vector3.Cross(Vector3.up, heading);

            int n = Mathf.Clamp(count, 1, 5);
            for (int i = 0; i < n; i++)
            {
                // Fan them out laterally so they advance as a small mob, not a
                // single-file line (mirrors WaveManager.SpawnOne's spread intent).
                float side = (i - (n - 1) * 0.5f) * 2.2f;
                Vector3 pos = basePos + lateral * side;

                string id = $"tutorial-{def.Id}-{i}";
                Enemy e = _wave.SpawnEnemyForExternalMode(def, pos, heart, id);
                if (e == null) continue;

                e.Died += OnEnemyDied;
                _spawned.Add(e);
            }

            Debug.Log($"[TutorialWaveSpawner] Spawned {_spawned.Count} tutorial enemy(ies) " +
                      $"at {spawnPoint.Direction} gate ({spawnPoint.SpawnId}).");
        }

        private EnemyDef ResolveEnemyDef(EnemyCatalog catalog)
        {
            if (catalog == null) return null;
            var preferred = catalog.Find(PreferredEnemyId);
            if (preferred != null) return preferred;
            if (catalog.Enemies != null)
                foreach (var d in catalog.Enemies)
                    if (d != null) return d;
            return null;
        }

        private void OnEnemyDied(Enemy enemy)
        {
            if (enemy != null) enemy.Died -= OnEnemyDied;
            _spawned.Remove(enemy);
        }

        private void PruneDead()
        {
            _spawned.RemoveAll(e => e == null || e.IsDead);
        }

        private void OnDestroy()
        {
            // Detach + clean up any lingering tutorial enemies so a torn-down
            // spawner never leaves orphan combatants or stale subscriptions.
            foreach (var e in _spawned)
            {
                if (e == null) continue;
                e.Died -= OnEnemyDied;
                e.Kill();
            }
            _spawned.Clear();
        }
    }
}
