// =============================================================================
// TowerConstructionQueue — DEF-76 (Linear). Singleton that builds placed towers
// one at a time: each AddToQueue enqueues a TowerQueueItem and, if idle, kicks off
// a build coroutine; on completion it drains the next item.
// -----------------------------------------------------------------------------
// namespace DeNelle.Village. DEF-76 Correction Pass 1 applied in full:
//   • Full singleton guard + OnDestroy clear (Issue 2).
//   • using System.Collections for IEnumerator (Issue 3).
//   • Tower.Initialize() is reached for every spawned tower (Issue 4) — see the
//     reconciliation note below for WHERE it happens in this codebase.
//   • _isBuilding cleared in a finally block so an exception can never deadlock
//     the queue (Issue 5); ProcessQueue() re-runs there to drain the rest.
//   • TowerQueueItem lives in its own file (Issue 6).
//
// CODEBASE RECONCILIATION (same as the rest of GROUP 0): the spec instantiates
// `item.Data.prefab` and relies on a TowerConstruction baked into that prefab. This
// project's TowerData has NO `prefab` field (DEF-73 removed it) and there is no
// authored tower prefab — TowerPlacementSystem spawns a bare GameObject and the
// visual is produced procedurally by Tower.Initialize. So here the queue creates
// the bare root, attaches Tower (left UN-initialized) + TowerConstruction, and
// TowerConstruction.CompleteConstruction() is what calls Tower.Initialize once the
// build timer elapses — i.e. the tower's visual is revealed only on completion,
// which is exactly the spec's intent (final tower appears only when built).
//
// Self-bootstrap (like EconomyService / SkillSystem) so Instance is non-null at
// runtime without scene authoring.
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Data;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>Sequential tower-build queue. Singleton.</summary>
    public class TowerConstructionQueue : MonoBehaviour
    {
        public static TowerConstructionQueue Instance;

        private readonly Queue<TowerQueueItem> _queue = new Queue<TowerQueueItem>();
        private bool _isBuilding;

        /// <summary>How many builds are waiting (excludes the one in progress).</summary>
        public int PendingCount => _queue.Count;
        /// <summary>True while a tower is actively being raised.</summary>
        public bool IsBuilding => _isBuilding;

        // Self-install so Instance is never null at runtime (mirrors EconomyService).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("TowerConstructionQueue");
            DontDestroyOnLoad(go);
            go.AddComponent<TowerConstructionQueue>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Queue a tower build at <paramref name="position"/>; starts it if idle.</summary>
        public void AddToQueue(TowerData data, Vector3 position)
        {
            if (data == null) return;
            _queue.Enqueue(new TowerQueueItem(data, position));
            ProcessQueue();
        }

        private void ProcessQueue()
        {
            if (_isBuilding || _queue.Count == 0) return;
            _isBuilding = true;
            StartCoroutine(BuildTowerRoutine(_queue.Dequeue()));
        }

        private IEnumerator BuildTowerRoutine(TowerQueueItem item)
        {
            // §12: a `catch` cannot wrap a `yield return` in C#, so the original try/finally
            // had NO catch — an exception during GameObject/Tower/TowerConstruction creation
            // propagated out of the coroutine UNLOGGED (the audit gap). We now GUARD the risky
            // (non-yielding) construction step in Guard.Try (catch -> Fail, never silent), then
            // yield OUTSIDE the try, and keep the try/finally only as the deadlock guard.
            try
            {
                TowerConstruction construction = null;
                bool built = Guard.Try("TowerQueue", $"build tower '{item.Data?.towerName ?? "<null>"}'", () =>
                {
                    var towerObj = new GameObject($"Tower_{item.Data.towerName}");
                    towerObj.transform.position = item.Position;

                    // Tower is added but NOT Initialized here — TowerConstruction reveals
                    // it (calls Initialize) on completion (see header reconciliation).
                    towerObj.AddComponent<Tower>();
                    construction = towerObj.AddComponent<TowerConstruction>();
                    construction.StartConstruction(item.Data);
                });

                // VERIFY: if the build threw (Guard caught + logged it) there is no
                // construction to wait on — fall straight through to the finally so the
                // queue drains the next item instead of WaitUntil-ing on a null forever.
                if (!built || construction == null)
                {
                    FlowTrace.Fail("TowerQueue",
                        $"BuildTowerRoutine: construction not created for '{item.Data?.towerName ?? "<null>"}' " +
                        "(threw or null) — skipping to next queued build, queue not deadlocked.");
                }
                else
                {
                    // Hold the queue until this build finishes (or the object is gone). This
                    // yield is OUTSIDE any catch — legal — and the local 'construction' is
                    // captured by the lambda so its WaitUntil sees the live instance.
                    yield return new WaitUntil(() => construction == null || construction.IsComplete);
                }
            }
            finally
            {
                _isBuilding = false;
                ProcessQueue();   // drain the next item even if the build threw
            }
        }
    }
}
