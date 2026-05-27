// =============================================================================
// NavPathCoordinator (DEF-56) — staggered NavMesh path-request scheduler.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT IT DOES:
//   Without coordination, every enemy that spawns during a wave-start calculates
//   its initial NavMesh path in the same frame, spiking CPU for ~150ms. This
//   singleton staggers the INITIAL SetDestination calls across the first
//   _staggerWindowSeconds after spawn so the cost is spread over several frames.
//
//   After the stagger window each enemy's own per-instance _pathRefreshTimer
//   (DEF-56 — added to Enemy.cs) handles ongoing throttling independently.
//
// HOW TO USE:
//   Enemy.Configure() registers with NavPathCoordinator.Register(). The coordinator
//   calls NavMeshAgent.SetDestination on each agent one-per-update-slot until
//   all pending agents are initialised. No changes to the Enemy prefab required.
//
// ARCHITECTURE:
//   * DontDestroyOnLoad singleton (BeforeSceneLoad bootstrap).
//   * MaxPathsPerFrame limits how many new paths fire per Update tick.
//   * Cleared on scene reload (WaveManager lifetime matches scene lifetime, so
//     the coordinator's queue naturally empties each wave as enemies are killed).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace DeNelle.Village
{
    /// <summary>
    /// Spreads initial NavMesh path requests across multiple frames so a
    /// large enemy wave spawn doesn't cause a single-frame CPU spike.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NavPathCoordinator : MonoBehaviour
    {
        public static NavPathCoordinator Instance { get; private set; }

        [Header("Scheduling")]
        [Tooltip("Maximum path requests issued per Update tick. " +
                 "2–4 is a safe range for 20-enemy waves on mobile.")]
        [SerializeField, Range(1, 16)] private int _maxPathsPerFrame = 3;

        [Tooltip("Maximum seconds an agent waits in the pending queue before it " +
                 "falls back to a direct SetDestination call (safety net).")]
        [SerializeField, Range(1f, 10f)] private float _maxQueueWaitSeconds = 4f;

        // ── Pending queue ─────────────────────────────────────────────────────
        // Each entry: (agent, destination, time enqueued)
        private struct PendingRequest
        {
            public NavMeshAgent Agent;
            public Vector3      Destination;
            public float        EnqueueTime;
        }

        private readonly Queue<PendingRequest> _pending = new Queue<PendingRequest>();

        // ── Bootstrap ─────────────────────────────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("[NavPathCoordinator]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<NavPathCoordinator>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Update — drain the queue ──────────────────────────────────────────

        private void Update()
        {
            int issued = 0;
            while (_pending.Count > 0 && issued < _maxPathsPerFrame)
            {
                var req = _pending.Dequeue();

                // Safety net: if the agent was destroyed (enemy killed while queued)
                // or has waited too long, skip it.
                if (req.Agent == null) continue;
                if (Time.time - req.EnqueueTime > _maxQueueWaitSeconds)
                {
                    // Fallback: issue immediately so the enemy doesn't stand still forever.
                    if (req.Agent.isOnNavMesh)
                        req.Agent.SetDestination(req.Destination);
                    continue;
                }

                if (req.Agent.isOnNavMesh)
                {
                    req.Agent.SetDestination(req.Destination);
                    issued++;
                }
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Enqueues an initial path request to be processed over the next few
        /// frames. Called by <see cref="Enemy.Configure"/> for newly spawned enemies.
        /// </summary>
        public static void RequestInitialPath(NavMeshAgent agent, Vector3 destination)
        {
            if (agent == null) return;

            if (Instance != null)
            {
                Instance._pending.Enqueue(new PendingRequest
                {
                    Agent       = agent,
                    Destination = destination,
                    EnqueueTime = Time.time,
                });
            }
            else
            {
                // Fallback when coordinator bootstrap hasn't run yet.
                if (agent.isOnNavMesh)
                    agent.SetDestination(destination);
            }
        }

        /// <summary>
        /// Clears all pending requests (called on scene unload / wave reset).
        /// </summary>
        public static void Clear()
        {
            Instance?._pending.Clear();
        }
    }
}
