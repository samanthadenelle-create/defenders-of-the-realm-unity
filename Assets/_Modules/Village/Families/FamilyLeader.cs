// =============================================================================
// FamilyLeader — the leader of a Monster Family (WO-146).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Attached alongside Enemy (+ EnemyBrain) on the lead enemy. Owns the roster of
// FamilyMembers + their slot indices, the current FormationType/FormationContext,
// and the cached slot-offset table. The leader pathfinds NORMALLY via its own
// Enemy/EnemyBrain (untouched).
//
// EACH TICK (throttled): reads its own EnemyBrain engage target → picks context →
// if the shape changed, blends the cached offset table → publishes each member's
// desired world slot (leaderPos + leaderRotation * cached local offset).
//
// PERF: local offsets are cached; recomputed only on shape/count change (and during
// a blend). The world conversion is the only per-frame work. Context evaluation is
// throttled. No per-frame allocations (reused offset buffers, no LINQ).
//
// SHARED PERCEPTION (WO-147 §5): the leader aggregates members' AwarenessSensor
// states and pushes RaiseSharedAlert so a member that hasn't personally seen the
// threat still reacts (one sees → all react). No separate GroupPerception.
//
// LEADER DEATH: v1 ships DISBAND — each surviving member re-enables its brain and
// reverts to solo (promotion is a later refinement, flagged for owner).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>The decision-maker of a monster family — roster, formation, slot publish.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Enemy))]
    public sealed class FamilyLeader : MonoBehaviour
    {
        [Header("Formation tuning")]
        [Tooltip("Base spacing between members (world units).")]
        [SerializeField, Min(0.5f)] private float _spacing = 1.8f;

        [Tooltip("Ring radius for LooseRing / TightPack.")]
        [SerializeField, Min(0.5f)] private float _ringRadius = 3f;

        [Tooltip("Seconds to blend between formations on a context switch (flow, not snap).")]
        [SerializeField, Min(0f)] private float _formationBlendSeconds = 0.6f;

        [Tooltip("Seconds between FormationContext re-evaluations (throttle).")]
        [SerializeField, Min(0.1f)] private float _contextEvalInterval = 0.5f;

        [Tooltip("Use a wide Line front on engage instead of a Wedge charge.")]
        [SerializeField] private bool _wideFront = false;

        private Enemy      _enemy;
        private EnemyBrain _brain;

        private readonly List<FamilyMember> _members = new List<FamilyMember>(8);

        private FormationType _shape = FormationType.LooseRing;
        private FormationContext _context = FormationContext.Roam;

        // Cached local-offset tables (old + new during a blend) and the published worlds.
        private Vector3[] _localOld = new Vector3[8];
        private Vector3[] _localNew = new Vector3[8];
        private float _blendT = 1f;       // 1 = no blend in progress
        private int   _cachedCount = -1;
        private FormationType _cachedShape = (FormationType)(-1);

        private float _contextTimer;

        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
            _brain = GetComponent<EnemyBrain>();
        }

        // ── Roster ────────────────────────────────────────────────────────────

        /// <summary>Registers a follower, assigning the next dense slot index.</summary>
        public void RegisterMember(FamilyMember member)
        {
            if (member == null || _members.Contains(member)) return;
            int slot = _members.Count;
            _members.Add(member);
            EnsureCapacity(_members.Count);
            member.Bind(this, slot);
            _cachedCount = -1;   // force slot recompute
        }

        /// <summary>Removes a member (death / break-off) and re-packs slot indices.</summary>
        public void UnregisterMember(FamilyMember member)
        {
            if (member == null) return;
            if (_members.Remove(member))
            {
                // Re-pack dense indices so the shape stays even.
                for (int i = 0; i < _members.Count; i++)
                    _members[i]?.Bind(this, i);
                _cachedCount = -1;
            }
        }

        private void EnsureCapacity(int count)
        {
            if (_localOld.Length < count)
            {
                System.Array.Resize(ref _localOld, Mathf.Max(count, _localOld.Length * 2));
                System.Array.Resize(ref _localNew, _localOld.Length);
            }
        }

        // ── Slot publish (read by FamilyMember) ───────────────────────────────

        /// <summary>
        /// World-space desired slot for <paramref name="slotIndex"/>: leaderPos +
        /// leaderRotation * (blended local offset). Cheap per-frame conversion.
        /// </summary>
        public Vector3 GetDesiredSlotWorld(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _members.Count) return transform.position;
            Vector3 local = (_blendT >= 1f)
                ? _localNew[slotIndex]
                : Vector3.Lerp(_localOld[slotIndex], _localNew[slotIndex], _blendT);
            return transform.position + transform.rotation * local;
        }

        /// <summary>
        /// DEV: force a specific formation (used by FamilyTestSpawner's 'L' cycler so
        /// the owner can watch every shape blend live). Auto-context resumes on the
        /// next context change. Public so the dev tool can drive it without a target.
        /// </summary>
        public void DevForceShape(FormationType shape) => SetShape(shape);

        // ── Tick ──────────────────────────────────────────────────────────────

        private void Update()
        {
            if (_enemy == null || _enemy.IsDead)
            {
                FlowTrace.Warn("BattleArena", "MARCH leader DEAD -> Disband (followers must re-enable brains).");
                Disband();
                return;
            }
            if (_members.Count == 0) return;

            // Throttled context evaluation → shape selection.
            _contextTimer -= Time.deltaTime;
            if (_contextTimer <= 0f)
            {
                _contextTimer = _contextEvalInterval;
                FormationContext ctx = EvaluateContext();
                if (ctx != _context)
                {
                    _context = ctx;
                    SetShape(ShapeForContext(ctx));
                }
                AggregateSharedAwareness();
            }

            // Recompute the local-offset table only on shape/count change.
            if (_cachedCount != _members.Count || _cachedShape != _shape)
                RecomputeLocalOffsets();

            // Advance the formation blend.
            if (_blendT < 1f && _formationBlendSeconds > 0.01f)
                _blendT = Mathf.Min(1f, _blendT + Time.deltaTime / _formationBlendSeconds);
        }

        private FormationContext EvaluateContext()
        {
            // Flee when the leader's tactical state is retreating/repositioning.
            if (_brain != null)
            {
                var ts = _brain.TacticalState;
                if (ts == EnemyTacticalState.Retreat || ts == EnemyTacticalState.Reposition)
                    return FormationContext.Flee;
                // Engage when the leader has committed to a target.
                if (_brain.CurrentTarget != null) return FormationContext.Engage;
            }
            return FormationContext.Roam;
        }

        private FormationType ShapeForContext(FormationContext ctx)
        {
            switch (ctx)
            {
                case FormationContext.Engage: return _wideFront ? FormationType.Line : FormationType.Wedge;
                case FormationContext.Flee:   return FormationType.TightPack;
                default:                      return FormationType.LooseRing;
            }
        }

        private void SetShape(FormationType next)
        {
            if (next == _shape) return;
            // Snapshot the current blended state as the blend's "old" table, then retarget.
            for (int i = 0; i < _members.Count; i++)
                _localOld[i] = (_blendT >= 1f) ? _localNew[i]
                             : Vector3.Lerp(_localOld[i], _localNew[i], _blendT);
            _shape = next;
            _cachedShape = (FormationType)(-1);   // force RecomputeLocalOffsets
            _blendT = (_formationBlendSeconds > 0.01f) ? 0f : 1f;
        }

        private void RecomputeLocalOffsets()
        {
            int n = _members.Count;
            EnsureCapacity(n);
            for (int i = 0; i < n; i++)
                _localNew[i] = FormationController.LocalSlotOffset(
                    _shape, i, n, _spacing, _ringRadius);
            _cachedCount = n;
            _cachedShape = _shape;
        }

        // ── Shared awareness aggregation (WO-147 §5) ──────────────────────────

        private void AggregateSharedAwareness()
        {
            // Max awareness across living members (+ leader's own sensor if present).
            AwarenessState maxState = AwarenessState.Unaware;
            for (int i = 0; i < _members.Count; i++)
            {
                var m = _members[i];
                if (m == null) continue;
                var s = m.GetComponent<AwarenessSensor>();
                if (s != null && (int)s.State > (int)maxState) maxState = s.State;
            }
            var leaderSensor = GetComponent<AwarenessSensor>();
            if (leaderSensor != null && (int)leaderSensor.State > (int)maxState)
                maxState = leaderSensor.State;

            if (maxState <= AwarenessState.Unaware) return;

            // One sees → all react: push the floor down to every member.
            for (int i = 0; i < _members.Count; i++)
                _members[i]?.GetComponent<AwarenessSensor>()?.RaiseSharedAlert(maxState);
            leaderSensor?.RaiseSharedAlert(maxState);
        }

        // ── Disband (v1 leader-death policy) ──────────────────────────────────

        private void Disband()
        {
            for (int i = 0; i < _members.Count; i++)
                _members[i]?.StopFollowing();
            _members.Clear();
        }

        private void OnDisable() => Disband();
    }
}
