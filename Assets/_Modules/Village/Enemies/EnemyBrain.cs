// =============================================================================
// EnemyBrain — role-based AI overlay (DEF-21) + tactical states (DEF-72).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT IT DOES:
//   Adds a tactical layer on top of Enemy.cs's basic "march toward the Heart"
//   behaviour. Attach alongside Enemy on every enemy prefab.
//
//   Each frame it:
//   1. Chooses a TARGET based on EnemyRole (WHAT to attack).
//   2. Computes a DESTINATION based on EnemyTacticalState (HOW to approach).
//   3. Passes the destination to Enemy via SetBrainTargetPosition so DriveNav
//      follows the right position.
//
//   ROLE BEHAVIOURS (DEF-21):
//   • Tank    — charges hero within aggro radius; otherwise nearest structure.
//   • Healer  — moves to most-damaged ally and periodically calls Enemy.Heal().
//   • DPS / Ranged / MiniBoss — return null → Enemy's own Heart-march runs.
//
//   TACTICAL STATES (DEF-72 — requires TacticalData assigned in inspector):
//   • Rush       — direct path to target (default, same as pre-DEF-72).
//   • Flank      — arc around target by FlankAngleOffset degrees.
//   • Retreat    — move away from target when HP drops below threshold.
//   • Suppressed — hold in place; EnemyGroupCoordinator releases the group.
//
// ARCHITECTURE:
//   Enemy owns NavMeshAgent, HP, death, VFX. EnemyBrain overrides the nav
//   destination via Enemy.SetBrainTargetPosition(). When TacticalData is null
//   (most enemies), the tactical system is a complete no-op — only role-based
//   targeting runs.
//
// INTEGRATION:
//   • EnemyGroupSpawner sets brain.Role from WaveEnemyGroup.Entries.
//   • EnemyGroupCoordinator calls SetTacticalState() for group suppression.
//   • Assign TacticalData SO in the inspector for advanced archetypes.
//
// WO-49 / WO-92: tag-based target finding (FindClosestTarget / SearchByTag)
//   supplements role targeting as a scene-agnostic fallback. Tag "HeroTarget"
//   on the hero GameObject and "HeartTarget" on HeartController. NavMesh path
//   validity is checked before setting a Rush destination.
// WO-90: TryAttack() damages both HeroHealth and IDamageableStructure targets.
// =============================================================================

using UnityEngine;
using UnityEngine.AI;
using DeNelle.Core.Combat;
using DeNelle.Core.Data;
using DeNelle.Data;           // EnemyData SO — WO-86

namespace DeNelle.Village
{
    /// <summary>
    /// Role-based AI overlay + optional tactical state machine. Attach alongside
    /// <see cref="Enemy"/> on every enemy prefab.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Enemy))]
    public sealed class EnemyBrain : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Role (DEF-21)")]
        [Tooltip("The tactical role this enemy plays — determines WHAT to target.")]
        public EnemyRole Role = EnemyRole.DPS;

        [Header("Tank scan")]
        [Tooltip("Radius within which the Tank scans for threatening targets (hero / structure).")]
        [SerializeField, Min(1f)] private float _threatScanRadius = 12f;

        [Header("Tower targeting (all roles)")]
        [Tooltip("All enemy roles will detour to attack any Tower within this radius " +
                 "instead of marching past it to the Heart.")]
        [SerializeField, Min(1f)] private float _towerScanRadius = 20f;

        [Header("Hero engagement (all roles)")]
        [Tooltip("Non-Tank roles engage the hero when it comes within this radius, " +
                 "instead of ignoring it and marching past to towers/Heart.")]
        [SerializeField, Min(1f)] private float _heroEngageRadius = 4f;

        [Header("Healer scan")]
        [Tooltip("Radius within which the Healer scans for wounded allies.")]
        [SerializeField, Min(1f)] private float _healScanRadius = 6f;

        [Tooltip("HP fraction below which an ally is 'wounded' and worth healing (0-1).")]
        [SerializeField, Range(0.1f, 0.9f)] private float _healThreshold = 0.7f;

        [Tooltip("HP restored per heal tick.")]
        [SerializeField, Min(1f)] private float _healAmount = 15f;

        [Tooltip("Seconds between heal ticks when adjacent to a wounded ally.")]
        [SerializeField, Range(0.5f, 5f)] private float _healInterval = 2f;

        [Header("Attack (WO-90)")]
        [Tooltip("Damage dealt per TryAttack() call to HeroHealth or IDamageableStructure targets.")]
        [SerializeField, Min(0f)] private float damage = 8f;

        [Tooltip("Minimum seconds between TryAttack() hits.")]
        [SerializeField, Range(0.1f, 5f)] private float attackCooldown = 1.0f;

        [Header("Data (WO-86)")]
        [Tooltip("Optional ScriptableObject with balance stats. Overlays damage/attackCooldown at Awake. Leave null to keep existing inspector values (legacy prefab safe).")]
        [SerializeField] private EnemyData _enemyData;

        [Header("Tactical overlay (DEF-72 — optional)")]
        [Tooltip("Assign a TacticalData SO to enable flanking, retreat, and group suppression. " +
                 "Leave blank for default role-only targeting (Rush to target).")]
        [SerializeField] private TacticalData _tactics;

        // ── Runtime ───────────────────────────────────────────────────────────

        private Enemy    _enemy;
        private Transform _heartTransform;
        private Transform _heroTransform;
        private float     _healCooldown;
        private float     _suppressTimer;

        private EnemyTacticalState _tacticalState = EnemyTacticalState.Rush;

        private readonly Collider[] _scanBuffer = new Collider[32];

        // DEF-72: throttle target-priority re-evaluation (not per-frame).
        private float _targetEvalTimer;
        private const float TargetEvalInterval = 2f;

        // DEF-43: optional BehaviorTree override — wired in Awake if present.
        private EnemyBehaviorTree _bt;

        // WO-90: attack state for TryAttack().
        private float     _nextAttackTime;
        private Animator  _animator;
        private Transform _currentTarget;

        // WO-92: cached NavMeshAgent for NavMesh path validation.
        private NavMeshAgent _navAgent;

        // ── Public properties (EnemyGroupCoordinator needs these) ─────────────

        /// <summary>Current tactical posture. Read by <see cref="EnemyGroupCoordinator"/>.</summary>
        public EnemyTacticalState TacticalState => _tacticalState;

        /// <summary>
        /// Suppress delay from the assigned <see cref="TacticalData"/>; 0 when no
        /// tactics SO is assigned. Read by <see cref="EnemyGroupCoordinator"/>.
        /// </summary>
        public float SuppressDelay => _tactics != null ? _tactics.SuppressDelay : 0f;

        // DEF-43: properties read by EnemyBehaviorTree leaf nodes.

        /// <summary>True when the underlying Enemy has died. Read by EnemyBehaviorTree.</summary>
        public bool IsDead => _enemy != null && _enemy.IsDead;

        /// <summary>Current HP value. Read by EnemyBehaviorTree low-health branch.</summary>
        public float CurrentHealth => _enemy != null ? _enemy.Hp : 0f;

        /// <summary>
        /// Hook called by EnemyBehaviorTree's StopAndEngage leaf. Currently a
        /// no-op because Enemy.TickContactAttack fires automatically on the same
        /// frame the agent stops. Reserved for future ranged / special-attack logic.
        /// </summary>
        public void TriggerAttack()
        {
            // Enemy handles contact damage in its own Update via TickContactAttack.
            // Stopping the NavMeshAgent (via SetBrainTargetPosition) is sufficient
            // to enter contact-attack mode. Expand here for ranged enemies.
        }

        /// <summary>
        /// Fired when Enemy.Died fires — allows EnemyGroupCoordinator to prune
        /// the member list without polling.
        /// </summary>
        public event System.Action<Enemy> Died;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
            _enemy.Died += e => Died?.Invoke(e);

            // DEF-43: wire BT if present on this GameObject.
            _bt = GetComponent<EnemyBehaviorTree>();

            // WO-90: cache Animator and NavMeshAgent from this GameObject.
            _animator  = GetComponentInChildren<Animator>();
            _navAgent  = GetComponent<NavMeshAgent>();

            // WO-86: overlay balance stats from EnemyData SO if assigned.
            if (_enemyData != null)
            {
                damage         = _enemyData.damage;
                attackCooldown = _enemyData.attackCooldown;
            }

            // Cache scene-wide refs once — FindAnyObjectByType is expensive per frame.
            var hc = FindAnyObjectByType<HeartController>();
            _heartTransform = hc != null ? hc.transform : null;

            // WO-49/WO-92: prefer tagged lookup; fall back to "Player" tag for
            // scenes that haven't been updated to use HeroTarget yet.
            var heroTagged = GameObject.FindWithTag("HeroTarget");
            if (heroTagged == null) heroTagged = GameObject.FindWithTag("Player");
            _heroTransform = heroTagged != null ? heroTagged.transform : null;
        }

        private void Update()
        {
            if (_enemy == null || _enemy.IsDead) return;

            // DEF-43: if a BehaviorTree is wired and ready, yield all targeting to it.
            if (_bt != null && _bt.IsInitialized)
            {
                _bt.Evaluate();
                return;
            }

            // DEF-72: evaluate tactical state first (health-based retreat trigger).
            if (_tactics != null) UpdateTacticalState();

            // Suppressed — hold in place (group coordinator hasn't released yet).
            if (_tacticalState == EnemyTacticalState.Suppressed)
            {
                _enemy.SetBrainTargetPosition(null);
                return;
            }

            // Choose target based on role.
            Transform target = ChooseTarget();
            _currentTarget = target;

            // Compute the final destination with tactical overlay applied.
            Vector3? dest = ComputeTacticalDestination(target);
            _enemy.SetBrainTargetPosition(dest);

            // Healer: cast heal pulse when we are adjacent to a wounded ally.
            if (Role == EnemyRole.Healer && target != null)
                TickHeal(target);
        }

        private void OnDisable()
        {
            _enemy?.SetBrainTargetPosition(null);
        }

        // ── DEF-72: tactical state update ─────────────────────────────────────

        private void UpdateTacticalState()
        {
            // Don't interrupt an externally-set Suppressed state.
            if (_tacticalState == EnemyTacticalState.Suppressed)
            {
                _suppressTimer -= Time.deltaTime;
                if (_suppressTimer <= 0f)
                    _tacticalState = EnemyTacticalState.Rush;
                return;
            }

            // Retreat if HP has dropped below threshold.
            if (_tactics.RetreatHealthThreshold > 0f
                && _enemy.HpFraction < _tactics.RetreatHealthThreshold)
            {
                _tacticalState = EnemyTacticalState.Retreat;
                return;
            }

            // Assign archetype-default tactical state when not retreating.
            _tacticalState = _tactics.Archetype switch
            {
                EnemyArchetype.Flanker => EnemyTacticalState.Flank,
                _                      => EnemyTacticalState.Rush,
            };
        }

        /// <summary>
        /// DEF-72: Set the tactical posture externally (called by
        /// <see cref="EnemyGroupCoordinator"/> to suppress/release the group).
        /// </summary>
        public void SetTacticalState(EnemyTacticalState state)
        {
            _tacticalState = state;
            if (state == EnemyTacticalState.Suppressed && _tactics != null)
                _suppressTimer = _tactics.SuppressDelay;
        }

        // ── DEF-72: tactical destination computation ──────────────────────────

        private Vector3? ComputeTacticalDestination(Transform target)
        {
            if (target == null) return null;

            switch (_tacticalState)
            {
                case EnemyTacticalState.Retreat:
                {
                    // Move directly away from the primary target.
                    Vector3 away = (transform.position - target.position).normalized;
                    if (away.sqrMagnitude < 0.001f) away = transform.forward;
                    return transform.position + away * 8f;
                }

                case EnemyTacticalState.Flank:
                {
                    float angle = _tactics != null ? _tactics.FlankAngleOffset : 90f;
                    // Rotate the direct-path vector by the flank angle (in the XZ plane).
                    Vector3 direct = (target.position - transform.position);
                    direct.y = 0f;
                    if (direct.sqrMagnitude < 0.01f) return target.position;
                    Vector3 flankDir = Quaternion.AngleAxis(angle, Vector3.up) * direct.normalized;
                    float dist = direct.magnitude;
                    return target.position + flankDir * (dist * 0.5f);
                }

                default:
                {
                    // Rush: go directly to the target's position.
                    // WO-92: validate that a complete NavMesh path exists before
                    // committing to this destination.
                    if (_navAgent != null && _navAgent.isOnNavMesh)
                    {
                        var path = new NavMeshPath();
                        if (!_navAgent.CalculatePath(target.position, path) ||
                            path.status != NavMeshPathStatus.PathComplete)
                        {
                            Debug.LogWarning(
                                $"[EnemyBrain] {name}: No complete NavMesh path to target '{target.name}' — holding.", this);
                            return null;
                        }
                    }
                    return target.position;
                }
            }
        }

        // ── Role-based target selection (DEF-21) ──────────────────────────────

        private Transform ChooseTarget()
        {
            switch (Role)
            {
                case EnemyRole.Tank:
                    return FindHighestThreatTarget() ?? FindNearestTower() ?? _heartTransform;

                case EnemyRole.Healer:
                    return FindMostDamagedAlly() ?? _heartTransform;

                // DPS / Ranged / MiniBoss: engage the hero first if it is within
                // close range (so the hero can body-block / fight instead of being
                // ignored), then towers, then drop through to Enemy's Heart-march.
                default:
                    // WO-49/WO-92: fall back to tag-based FindClosestTarget.
                    return FindNearbyHero() ?? FindNearestTower() ?? FindClosestTarget();
            }
        }

        // ── Tank: find the biggest nearby threat ──────────────────────────────

        private Transform FindHighestThreatTarget()
        {
            if (_heroTransform != null)
            {
                float dist = (_heroTransform.position - transform.position).sqrMagnitude;
                if (dist <= _threatScanRadius * _threatScanRadius)
                    return _heroTransform;
            }
            return FindNearestStructure();
        }

        // ── All roles: opportunistic close-range hero engage ──────────────────

        /// <summary>
        /// Returns the hero if it is within <see cref="_heroEngageRadius"/>, else null.
        /// Lets non-Tank roles attack the hero when it physically gets in their way
        /// instead of walking straight past it (DEF playtest: "enemies ignore me").
        /// </summary>
        private Transform FindNearbyHero()
        {
            if (_heroTransform == null) return null;
            float r = _heroEngageRadius;
            return (_heroTransform.position - transform.position).sqrMagnitude <= r * r
                ? _heroTransform : null;
        }

        // ── Healer: find the most wounded living ally ─────────────────────────

        private Transform FindMostDamagedAlly()
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position, _healScanRadius, _scanBuffer);

            Enemy worstAlly = null;
            float worstFraction = _healThreshold;

            for (int i = 0; i < count; i++)
            {
                if (_scanBuffer[i] == null) continue;
                var ally = _scanBuffer[i].GetComponentInParent<Enemy>();
                if (ally == null || ally == _enemy || ally.IsDead) continue;
                float frac = ally.HpFraction;
                if (frac < worstFraction) { worstFraction = frac; worstAlly = ally; }
            }

            return worstAlly != null ? worstAlly.transform : null;
        }

        // ── Shared: nearest live IDamageableStructure ─────────────────────────

        private Transform FindNearestStructure()
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position, _threatScanRadius, _scanBuffer);

            Transform nearest = null;
            float nearestSqr = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                if (_scanBuffer[i] == null) continue;
                var structure = _scanBuffer[i].GetComponentInParent<IDamageableStructure>();
                if (structure == null || !structure.IsAlive) continue;
                float sqr = (_scanBuffer[i].transform.position - transform.position).sqrMagnitude;
                if (sqr < nearestSqr) { nearestSqr = sqr; nearest = _scanBuffer[i].transform; }
            }

            return nearest;
        }

        // ── Tower targeting (all roles) ───────────────────────────────────────

        /// <summary>
        /// Scans within <see cref="_towerScanRadius"/> for the nearest live
        /// <see cref="Tower"/>. All enemy roles use this so they detour to attack
        /// towers rather than marching past them to the Heart.
        /// Returns null when no live tower is in range.
        /// </summary>
        private Transform FindNearestTower()
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position, _towerScanRadius, _scanBuffer);

            Transform nearest = null;
            float nearestSqr = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                if (_scanBuffer[i] == null) continue;
                var tower = _scanBuffer[i].GetComponentInParent<Tower>();
                if (tower == null || !tower.IsAlive) continue;
                float sqr = (tower.transform.position - transform.position).sqrMagnitude;
                if (sqr < nearestSqr) { nearestSqr = sqr; nearest = tower.transform; }
            }

            return nearest;
        }

        // ── WO-49/WO-92: tag-based fallback target finding ─────────────────────

        /// <summary>
        /// Falls back to tag-based search when role targeting returns nothing.
        /// Searches "HeroTarget" then "HeartTarget" tags.
        /// </summary>
        private Transform FindClosestTarget()
        {
            var hero = GameObject.FindWithTag("HeroTarget");
            if (hero != null) return hero.transform;
            var heart = GameObject.FindWithTag("HeartTarget");
            return heart != null ? heart.transform : _heartTransform;
        }

        // ── Healer tick ───────────────────────────────────────────────────────

        private void TickHeal(Transform target)
        {
            _healCooldown -= Time.deltaTime;
            if (_healCooldown > 0f) return;

            var ally = target.GetComponent<Enemy>();
            if (ally == null || ally.IsDead || ally.HpFraction >= _healThreshold) return;

            ally.Heal(_healAmount);
            _healCooldown = _healInterval;
        }

        // ── Gizmos ────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (Role == EnemyRole.Tank || Role == EnemyRole.MiniBoss)
            {
                Gizmos.color = new Color(0.9f, 0.3f, 0.1f, 0.35f);
                Gizmos.DrawWireSphere(transform.position, _threatScanRadius);
            }
            if (Role == EnemyRole.Healer)
            {
                Gizmos.color = new Color(0.2f, 0.9f, 0.2f, 0.35f);
                Gizmos.DrawWireSphere(transform.position, _healScanRadius);
            }
        }
#endif
    }
}
