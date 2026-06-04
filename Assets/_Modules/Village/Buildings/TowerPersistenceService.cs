// =============================================================================
// TowerPersistenceService — persists placed towers across scene loads, so leaving
// the village (e.g. into an ATB battle) and returning doesn't wipe your towers.
// -----------------------------------------------------------------------------
// Placed towers are runtime GameObjects in the Village scene; a scene change
// destroys them and re-entry reloads Village fresh (the reported bug). This DDOL
// singleton keeps a lightweight snapshot (TowerData + world position + level) and
// rebuilds the towers on Village re-entry.
//
// Snapshot-based (not per-event) on purpose: it re-reads the live towers on a
// short throttle while in the village, so it captures placements, upgrades AND
// razes automatically — no hooks in TowerConstruction / BuildMenu / the raze
// paths (which are being edited elsewhere). Rebuilt towers are created instantly
// via Tower.Initialize(data, level) — no construction timer replay.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.Data;

namespace DeNelle.Village
{
    /// <summary>Snapshots + restores placed towers across scene loads. DDOL singleton.</summary>
    public sealed class TowerPersistenceService : MonoBehaviour
    {
        public static TowerPersistenceService Instance { get; private set; }

        private struct Rec { public TowerData Data; public Vector3 Pos; public int Level; }

        private readonly List<Rec> _records = new List<Rec>();
        private bool _inTowerScene;
        private float _nextSnapshot;

        private const float SnapshotInterval = 0.6f;
        private const string TowerSceneName = "Village2";   // the scene placed towers live in

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("TowerPersistenceService").AddComponent<TowerPersistenceService>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);   // handle the current scene
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _inTowerScene = scene.name == TowerSceneName;
            if (_inTowerScene)
            {
                Restore();
                _nextSnapshot = Time.unscaledTime + SnapshotInterval;
            }
        }

        private void Update()
        {
            if (!_inTowerScene || Time.unscaledTime < _nextSnapshot) return;
            _nextSnapshot = Time.unscaledTime + SnapshotInterval;
            Snapshot();
        }

        // Re-read the live towers into records (captures placements/upgrades/razes).
        private void Snapshot()
        {
            var towers = FindObjectsByType<Tower>(FindObjectsSortMode.None);
            _records.Clear();
            for (int i = 0; i < towers.Length; i++)
            {
                var t = towers[i];
                if (t == null || t.Data == null) continue;
                _records.Add(new Rec { Data = t.Data, Pos = t.transform.position, Level = t.CurrentLevel });
            }
        }

        // Rebuild persisted towers on Village re-entry (instant — no construction).
        private void Restore()
        {
            if (_records.Count == 0) return;
            if (FindObjectsByType<Tower>(FindObjectsSortMode.None).Length > 0) return;   // already present

            foreach (var rec in _records)
            {
                if (rec.Data == null) continue;
                var go = new GameObject($"Tower_{rec.Data.towerName}");
                go.transform.position = rec.Pos;
                var t = go.AddComponent<Tower>();
                t.Initialize(rec.Data);                                 // build at level 1 (instant)
                for (int lv = 1; lv < rec.Level; lv++) t.Upgrade();     // restore upgrade level
            }
        }
    }
}
