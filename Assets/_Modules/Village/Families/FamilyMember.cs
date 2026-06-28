// =============================================================================
// FamilyMember — one follower in a Monster Family (WO-146).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Attached alongside Enemy (+ EnemyBrain) on a follower. Reads its assigned
// desired world slot from the FamilyLeader and drives movement SOLELY via
// Enemy.SetBrainTargetPosition — never the NavMeshAgent directly.
//
// THE OWNERSHIP RULE (load-bearing): SetBrainTargetPosition has exactly ONE
// writer per Enemy. While following, this member is the writer and the follower's
// EnemyBrain is DISABLED (its OnDisable clears the override, leaving us clean).
// On disband / engage-break the brain is re-enabled and this member stops writing.
//
// STABILITY: a reposition threshold suppresses micro-jitter; every slot is
// NavMesh.SamplePosition-validated and falls back to following the leader if the
// slot is off-mesh (never freeze, never path into the void). When stationary the
// member lerps its facing toward the leader so the pack reads as a unit.
// =============================================================================

using UnityEngine;
using UnityEngine.AI;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>One follower that holds a dynamic slot relative to its family leader.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Enemy))]
    public sealed class FamilyMember : MonoBehaviour
    {
        [Tooltip("Distance the slot must drift before issuing a new destination (anti-jitter).")]
        [SerializeField, Min(0.1f)] private float _repositionThreshold = 0.6f;

        [Tooltip("NavMesh sample radius when snapping a desired slot onto the mesh.")]
        [SerializeField, Min(0.5f)] private float _sampleRadius = 2f;

        [Tooltip("Agent speed below which the member is 'stationary' and matches leader facing.")]
        [SerializeField, Min(0.01f)] private float _stationaryEpsilon = 0.15f;

        private Enemy        _enemy;
        private EnemyBrain   _brain;
        private NavMeshAgent _agent;
        private FamilyLeader _leader;
        private int          _slotIndex = -1;
        private Vector3      _lastCommittedSlot;
        private bool         _following;

        /// <summary>Slot index assigned by the leader (0-based, dense).</summary>
        public int SlotIndex => _slotIndex;

        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
            _brain = GetComponent<EnemyBrain>();
            _agent = GetComponent<NavMeshAgent>();
        }

        /// <summary>
        /// Called by the leader on registration. Binds the leader + slot and enters
        /// follow mode (disables the brain so this member is the sole nav writer).
        /// </summary>
        public void Bind(FamilyLeader leader, int slotIndex)
        {
            _leader = leader;
            _slotIndex = slotIndex;
            BeginFollowing();
        }

        private void BeginFollowing()
        {
            _following = true;
            // Disable the brain so it stops writing SetBrainTargetPosition; its
            // OnDisable clears the override, leaving this member the clean sole writer.
            if (_brain != null) _brain.enabled = false;
            FlowTrace.Step("BattleArena", $"MARCH follower brain DISABLED (slot wiring) slot={_slotIndex}.");
        }

        /// <summary>
        /// Stops following and hands movement back to the EnemyBrain (disband, or the
        /// member breaks off to engage). Idempotent + null-safe.
        /// </summary>
        public void StopFollowing()
        {
            if (!_following) return;
            _following = false;
            _enemy?.SetBrainTargetPosition(null);
            if (_brain != null) _brain.enabled = true;
            FlowTrace.Step("BattleArena", $"MARCH follower brain RE-ENABLED slot={_slotIndex}.");
        }

        private void OnDisable()
        {
            // Don't leave a stale override if we're torn down mid-follow.
            if (_following) _enemy?.SetBrainTargetPosition(null);
        }

        private void Update()
        {
            if (!_following || _enemy == null || _enemy.IsDead || _leader == null) return;

            // The leader publishes desired world slots; read ours.
            Vector3 desired = _leader.GetDesiredSlotWorld(_slotIndex);

            // Anti-jitter: only re-commit when the slot has drifted past the threshold.
            if ((desired - _lastCommittedSlot).sqrMagnitude >= _repositionThreshold * _repositionThreshold)
            {
                // NavMesh-sample; fall back to following the leader if the slot is off-mesh.
                Vector3 committed;
                if (NavMesh.SamplePosition(desired, out NavMeshHit hit, _sampleRadius, NavMesh.AllAreas))
                    committed = hit.position;
                else if (NavMesh.SamplePosition(_leader.transform.position, out NavMeshHit lh, _sampleRadius, NavMesh.AllAreas))
                    committed = lh.position;
                else
                    committed = _leader.transform.position;

                _lastCommittedSlot = committed;
                _enemy.SetBrainTargetPosition(committed);
            }

            // Rotation: when stationary at slot, lerp facing toward the leader so the
            // pack reads as a unit. While moving, NavMeshAgent faces velocity (default).
            if (_agent != null && _agent.isOnNavMesh && _agent.velocity.sqrMagnitude < _stationaryEpsilon * _stationaryEpsilon)
            {
                Vector3 fwd = _leader.transform.forward; fwd.y = 0f;
                if (fwd.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation, Quaternion.LookRotation(fwd), 5f * Time.deltaTime);
            }
        }
    }
}
