// =============================================================================
// EnemyBehaviorTree — DEF-43: BT wrapper attached alongside EnemyBrain.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Replaces EnemyBrain's per-frame targeting logic when present. EnemyBrain.Update()
// checks for this component and calls Evaluate() instead of its own ChooseTarget
// loop. The BT drives Enemy.SetBrainTargetPosition() directly — the same API
// EnemyBrain uses — so no new engine coupling is introduced.
//
// PRIORITY ORDER (Selector root — first Success/Running wins):
//   1. Dead             — do nothing (IsDead guard)
//   2. Low HP (<25%)    — hold position (retreat-in-place)
//   3. In attack range  — stop moving, let Enemy.TickContactAttack fire
//   4. Has hero target  — chase toward hero
// =============================================================================

using UnityEngine;
using DeNelle.AI;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Role-agnostic Behavior Tree that drives <see cref="Enemy"/> movement via
    /// <see cref="Enemy.SetBrainTargetPosition"/>. Attach alongside
    /// <see cref="EnemyBrain"/>; EnemyBrain.Update() will yield to this tree
    /// automatically once <see cref="IsInitialized"/> is true.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyBrain))]
    public sealed class EnemyBehaviorTree : MonoBehaviour
    {
        [Tooltip("Distance within which the enemy stops moving and enters contact-attack mode.")]
        [SerializeField, Min(0.5f)] private float _attackRange = 2.2f;

        // ── Runtime ───────────────────────────────────────────────────────────

        private BTNode    _root;
        private Enemy     _enemy;
        private EnemyBrain _brain;
        private Transform  _target;

        /// <summary>True once <see cref="Initialize"/> has been called successfully.</summary>
        public bool IsInitialized { get; private set; }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
            _brain = GetComponent<EnemyBrain>();

            // WO-450: resolve the hero by component (HeroLocomotion), not the (undeclared)
            // "HeroTarget" tag — the hero now carries the "Player" tag (one tag per object).
            // Re-acquired each tick in Evaluate() while null, so an enemy that inits before the
            // hero streams in (additive OuterWorld load) still aggros.
            _target = FindHeroTransform();
        }

        private float _nextReacquire;

        // Guarded tag lookup — FindWithTag throws if the tag is undefined in the project.
        private static Transform TryFindByTag(string tag)
        {
            try { var go = GameObject.FindWithTag(tag); return go != null ? go.transform : null; }
            catch (System.Exception e) { FlowTrace.Warn("EnemyBT", $"TryFindByTag('{tag}') threw (tag likely undefined): {e.GetType().Name}: {e.Message}"); return null; }
        }

        /// <summary>WO-450: resolve the hero by component (every hero variant carries
        /// HeroLocomotion), with a "Player"-tag fallback. Null-safe, tag-independent.</summary>
        private static Transform FindHeroTransform()
        {
            var loco = FindFirstObjectByType<HeroLocomotion>();
            if (loco != null) return loco.transform;
            return TryFindByTag("Player");
        }

        private void Start()
        {
            if (_enemy != null && _brain != null)
                Initialize();
        }

        /// <summary>
        /// Builds the BT root and marks this instance as ready. Called from
        /// <see cref="Start"/>; can also be called by a spawner if the target
        /// changes after spawn.
        /// </summary>
        public void Initialize()
        {
            _root = BuildTree();
            IsInitialized = true;

            Debug.Log($"[EnemyBehaviorTree] Initialized on '{gameObject.name}' — " +
                      $"attackRange={_attackRange:F1}, target={(_target != null ? _target.name : "none")}");
        }

        /// <summary>Evaluate the BT this frame. Called by EnemyBrain.Update().</summary>
        public void Evaluate()
        {
            // WO-419: re-acquire the hero while the target is null (it may stream in after Awake
            // under additive scene load). Throttled to 0.5s; stops once found, so no per-frame
            // scan once the enemy has aggroed.
            if (_target == null && Time.time >= _nextReacquire)
            {
                _nextReacquire = Time.time + 0.5f;
                _target = FindHeroTransform();   // WO-450: component lookup
            }
            _root?.Evaluate();
        }

        // ── Tree construction ─────────────────────────────────────────────────

        private BTNode BuildTree()
        {
            return new Selector(

                // 1. Dead — do nothing (EnemyBrain's IsDead guard handles the Unity
                //    lifecycle; this branch makes the BT not fight it).
                new Condition(() => _enemy == null || _enemy.IsDead),

                // 2. Low HP — stop and hold position (retreat-in-place).
                new Sequence(
                    new Condition(() => _enemy.HpFraction < 0.25f),
                    new ActionNode(HoldPosition)
                ),

                // 3. In attack range — stop moving, Enemy.TickContactAttack handles damage.
                new Sequence(
                    new Condition(IsInAttackRange),
                    new ActionNode(StopAndEngage)
                ),

                // 4. Has hero target — chase.
                new Sequence(
                    new Condition(() => _target != null),
                    new ActionNode(ChaseTarget)
                )
            );
        }

        // ── Leaf actions ──────────────────────────────────────────────────────

        private bool IsInAttackRange()
        {
            if (_target == null || _enemy == null) return false;
            return Vector3.Distance(transform.position, _target.position) <= _attackRange;
        }

        private BTNode.NodeState ChaseTarget()
        {
            if (_enemy == null || _target == null) return BTNode.NodeState.Failure;
            // Drive the NavMeshAgent via the same override slot EnemyBrain uses.
            _enemy.SetBrainTargetPosition(_target.position);
            return BTNode.NodeState.Running;
        }

        private BTNode.NodeState StopAndEngage()
        {
            if (_enemy == null) return BTNode.NodeState.Failure;
            // Pass current position so the NavMeshAgent stops in place.
            // Enemy.TickContactAttack() fires on the next frame and deals damage.
            _enemy.SetBrainTargetPosition(transform.position);
            _brain.TriggerAttack(); // hook for future ranged/special-attack logic
            return BTNode.NodeState.Success;
        }

        private BTNode.NodeState HoldPosition()
        {
            if (_enemy == null) return BTNode.NodeState.Failure;
            _enemy.SetBrainTargetPosition(transform.position);
            return BTNode.NodeState.Running;
        }
    }
}
