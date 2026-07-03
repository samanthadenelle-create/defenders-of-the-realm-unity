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
using DeNelle.Core.Diagnostics;

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
            using var _ = FlowTrace.Enter("TowerPersist", "Snapshot");
            var towers = FindObjectsByType<Tower>();
            _records.Clear();
            int kept = 0, skippedNull = 0;
            for (int i = 0; i < towers.Length; i++)
            {
                var t = towers[i];
                if (t == null) continue;
                if (t.Data == null)
                {
                    // §12: a live tower with no Data can't be persisted — if we silently
                    // skipped it the player's placed tower would vanish on re-entry with no
                    // trace. Warn (don't blank) so a "tower disappeared" report self-reports.
                    skippedNull++;
                    FlowTrace.Warn("TowerPersist",
                        $"Snapshot: tower '{t.name}' has null Data — NOT persisted (will not survive scene reload).");
                    continue;
                }
                _records.Add(new Rec { Data = t.Data, Pos = t.transform.position, Level = t.CurrentLevel });
                kept++;
            }
            FlowTrace.Throttle("TowerPersist", "snapshot-result", 5f,
                $"Snapshot: {kept} tower(s) recorded, {skippedNull} skipped (null Data) of {towers.Length} found.");
        }

        // Rebuild persisted towers on Village re-entry (instant — no construction).
        private void Restore()
        {
            using var _ = FlowTrace.Enter("TowerPersist", "Restore");
            if (_records.Count == 0)
            {
                FlowTrace.Step("TowerPersist", "Restore: no records to restore (clean slate).");
                return;
            }
            if (FindObjectsByType<Tower>().Length > 0)
            {
                FlowTrace.Step("TowerPersist", "Restore: towers already present in scene — skipping rebuild.");
                return;   // already present
            }

            // §12: rebuild EACH record under its own Guard so ONE bad record (a destroyed
            // Data asset, an Initialize/Upgrade that throws) is logged + skipped, never
            // aborting the loop and losing EVERY other tower (the audit gap). The base
            // contract: a single corrupt record costs at most that one tower, not all.
            int restored = 0, nullData = 0;
            int index = 0;
            var result = Guard.TryEach("TowerPersist", "restore tower", _records, rec =>
            {
                index++;
                if (rec.Data == null)
                {
                    nullData++;
                    FlowTrace.Warn("TowerPersist",
                        $"Restore: record #{index} has null Data — skipped (one lost tower, rest preserved).");
                    return;
                }
                var go = new GameObject($"Tower_{rec.Data.towerName}");
                go.transform.position = rec.Pos;
                var t = go.AddComponent<Tower>();
                t.Initialize(rec.Data);                                 // build at level 1 (instant)
                for (int lv = 1; lv < rec.Level; lv++) t.Upgrade();     // restore upgrade level
                restored++;
            });

            // VERIFY the result: we held N records but rebuilt fewer (or zero) — self-report
            // so a "my towers didn't come back" report maps straight to the dropped records.
            // result.failed = records whose Initialize/Upgrade THREW (Guard caught + skipped);
            // nullData = records dropped for a null Data. The rest restored cleanly.
            if (restored == 0)
                FlowTrace.Fail("TowerPersist",
                    $"Restore: rebuilt 0 of {_records.Count} persisted tower(s) — all records failed/empty " +
                    $"(threw={result.failed}, nullData={nullData}). Towers lost on re-entry.");
            else
                FlowTrace.Step("TowerPersist",
                    $"Restore: rebuilt {restored} of {_records.Count} persisted tower(s) " +
                    $"(skipped threw={result.failed}, nullData={nullData}).");
        }
    }
}
