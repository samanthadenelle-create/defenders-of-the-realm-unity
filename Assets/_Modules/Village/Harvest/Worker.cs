// =============================================================================
// Worker — a dispatched harvest unit (WO-117 Phase 1).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The Warcraft "send a peon to the mine" verb. A Worker is dispatched to a
// (claimed) MineNode, walks there on the baked NavMesh exactly like Enemy.cs's
// agent, then auto-collects on station — driving the node's EXISTING extract /
// banking path (MineNode.TryAutoExtract) on the node's own cooldown. No parallel
// economy: every unit of resource still flows through MineNode → GameState.
//
// RECONCILIATION: this layers on top of MineNode (WO-142). The worker does not
// bank resource itself; it only triggers MineNode's extract, so yield, region
// danger bonus and depletion stay identical to a manual [F] tap.
//
// Phase 1 ships the dispatch → travel → auto-collect → idle loop. The Fleeing
// state is declared for Phase 2's raid layer but is inert here (no invasions yet).
// NavMeshAgent is optional — if the worker has no agent / the node is off-mesh it
// degrades to a teleport-to-node so the collect verb still demos.
// =============================================================================
using UnityEngine;
using UnityEngine.AI;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>Worker lifecycle. Phase 1 uses Idle→Traveling→Collecting; Returning
    /// and Fleeing are reserved for the recall and raid layers (Phase 2).</summary>
    public enum WorkerState { Idle, Traveling, Collecting, Returning, Fleeing }

    [DisallowMultipleComponent]
    public sealed class Worker : MonoBehaviour
    {
        [Tooltip("How close the worker must get to its node before it starts collecting.")]
        [Min(0.5f)] public float ArriveRadius = 2.5f;

        [Tooltip("Fallback move speed used only when there is no NavMeshAgent.")]
        [Min(0.1f)] public float FallbackMoveSpeed = 6f;

        public WorkerState State { get; private set; } = WorkerState.Idle;
        public MineNode TargetNode { get; private set; }

        private NavMeshAgent _agent;
        private bool _usingAgent;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _usingAgent = _agent != null && _agent.isOnNavMesh;
        }

        /// <summary>True while this worker is actively collecting at the given node.</summary>
        public bool IsCollectingAt(MineNode node) => State == WorkerState.Collecting && TargetNode == node;

        /// <summary>True when the worker is free to take a new assignment.</summary>
        public bool IsAvailable => State == WorkerState.Idle && TargetNode == null;

        /// <summary>Dispatch the worker to a node. Claims the node and begins travel.
        /// Caller (WorkerManager) is responsible for refusing if the node is already
        /// claimed. Returns false if the node is null or depleted.</summary>
        public bool DispatchTo(MineNode node)
        {
            if (node == null || node.IsDepleted) { FlowTrace.Warn("Worker", $"DispatchTo refused: node {(node == null ? "<null>" : "depleted")}"); return false; }

            FlowTrace.Step("Worker", "DispatchTo — claimed node, entering Traveling");
            TargetNode = node;
            node.SetWorkerClaim(true);
            State = WorkerState.Traveling;

            // Re-resolve the agent at dispatch time (it may have warped onto the mesh
            // after Awake, e.g. runtime-spawned worker placed via NavMesh.SamplePosition).
            if (_agent == null) _agent = GetComponent<NavMeshAgent>();
            _usingAgent = _agent != null && _agent.isOnNavMesh;

            if (_usingAgent)
            {
                _agent.isStopped = false;
                _agent.SetDestination(node.transform.position);
            }
            return true;
        }

        /// <summary>Stop collecting, release the node, and return to Idle. Phase-2
        /// recall/flee will route through here after a Returning walk; Phase 1 just
        /// frees the worker immediately.</summary>
        public void Release()
        {
            if (TargetNode != null) TargetNode.SetWorkerClaim(false);
            TargetNode = null;
            State = WorkerState.Idle;
            if (_usingAgent && _agent != null && _agent.isOnNavMesh)
                _agent.ResetPath();
        }

        private void Update()
        {
            switch (State)
            {
                case WorkerState.Traveling: TickTravel(); break;
                // Collecting is driven by WorkerManager (it owns the harvest tick so
                // offline catch-up and the per-frame loop share one code path).
                default: break;
            }
        }

        private void TickTravel()
        {
            if (TargetNode == null) { State = WorkerState.Idle; return; }

            // Node depleted before we arrived — give up gracefully.
            if (TargetNode.IsDepleted) { Release(); return; }

            float distSqr = (TargetNode.transform.position - transform.position).sqrMagnitude;
            bool arrived;

            if (_usingAgent && _agent != null && _agent.isOnNavMesh)
            {
                arrived = !_agent.pathPending &&
                          _agent.remainingDistance <= Mathf.Max(ArriveRadius, _agent.stoppingDistance);
            }
            else
            {
                // No NavMesh: lerp straight toward the node so the verb still demos.
                Vector3 to = TargetNode.transform.position - transform.position;
                to.y = 0f;
                transform.position += Vector3.ClampMagnitude(to, FallbackMoveSpeed * Time.deltaTime);
                arrived = distSqr <= ArriveRadius * ArriveRadius;
            }

            if (arrived)
            {
                if (_usingAgent && _agent != null && _agent.isOnNavMesh) _agent.isStopped = true;
                FlowTrace.Step("Worker", $"arrived at node (agent={_usingAgent}) — entering Collecting");
                State = WorkerState.Collecting;
            }
        }
    }
}
