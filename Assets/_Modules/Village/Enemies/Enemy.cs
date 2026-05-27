// =============================================================================
// Enemy — one Hollow One marching on Avalon (Week-4 wave-loop slice).
// -----------------------------------------------------------------------------
// Port spec Part 3 row: src/modules/village/enemies/ -> Enemy.cs.
// Port spec Part 5 Week 4: "KayKit skeleton mesh, NavMeshAgent, walks toward the
// Heart, attacks buildings/walls on contact, dies on HP zero."
//
// One Enemy MonoBehaviour drives the nav, HP and on-contact attack of a single
// wave enemy. It is configured from an EnemyDef (the deserialised enemies.json
// stat block) by the WaveManager right after instantiation.
//
// NAVMESH: the enemy uses a UnityEngine.AI.NavMeshAgent (the legacy AI module —
// com.unity.modules.ai, already in the manifest). The agent walks toward the
// Heart's world position. ** The village scene MUST have a baked NavMesh for
// this to move ** — see docs/port-notes/week4-waves.md. This script assumes one
// exists and degrades gracefully (logs once, holds position) if it does not.
//
// CONTACT ATTACK: the enemy raycasts/overlaps for an IDamageableStructure ahead
// of it (a building / wall / gate). On contact it stops and deals contactDamage
// every attackInterval seconds. IDamageableStructure is defined here so Enemy
// has NO compile dependency on a specific Building/Gate damage API — the
// integrator adds the interface to those MonoBehaviours when their HP gameplay
// lands. Until then enemies simply path to the Heart.
//
// BREACH: the WaveManager owns inner-ring breach detection (it knows the ring
// radius). Enemy just exposes its EnemyId / EnemyDefId / EngineDefId so the
// breach trigger can hand the breaching roster to the ATB scene.
// =============================================================================

using System;
using UnityEngine;
using UnityEngine.AI;

namespace DeNelle.Village
{
    /// <summary>
    /// A village structure that can take contact damage from an <see cref="Enemy"/>
    /// — a building, wall section or gate. Defined here so <see cref="Enemy"/>
    /// stays free of any Building/Gate API dependency. The integrator implements
    /// this on <c>Building</c> / <c>WallSegment</c> / <c>Gate</c> when their HP
    /// gameplay lands (port spec Week 4).
    /// </summary>
    public interface IDamageableStructure
    {
        /// <summary>True while the structure still stands and can be attacked.</summary>
        bool IsAlive { get; }

        /// <summary>Applies <paramref name="amount"/> contact damage from an enemy hit.</summary>
        void ApplyContactDamage(float amount);
    }

    /// <summary>
    /// One Hollow One in the village wave loop. Drives a <see cref="NavMeshAgent"/>
    /// toward the Heart, takes HP damage, attacks the structure in front of it on
    /// contact, and dies at zero HP. Configured by <see cref="WaveManager"/> from
    /// an <see cref="EnemyDef"/>. Instantiated per spawn; pooling is a later pass.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class Enemy : MonoBehaviour
    {
        // ── Inspector tuning (overridden by Configure from the EnemyDef) ──────

        [Header("Identity")]
        [Tooltip("Stable per-instance id — e.g. 'wave1-hollow-walker-3'. The breach roster key.")]
        [SerializeField] private string _enemyId;

        [Tooltip("enemies.json def id this enemy was spawned from — e.g. 'hollow-walker'.")]
        [SerializeField] private string _enemyDefId;

        [Header("Stats (set by Configure from enemies.json)")]
        [Tooltip("Current hit points.")]
        [SerializeField] private float _hp = 52f;

        [Tooltip("Max hit points.")]
        [SerializeField] private float _maxHp = 52f;

        [Tooltip("NavMeshAgent speed — world units/sec.")]
        [SerializeField] private float _moveSpeed = 2.5f;

        [Tooltip("Damage dealt to a structure per melee hit.")]
        [SerializeField] private float _contactDamage = 6f;

        [Tooltip("Seconds between melee hits while in contact.")]
        [SerializeField] private float _attackInterval = 1.3f;

        [Tooltip("AI archetype — Walker marches straight; Charger / Skirmisher are later waves.")]
        [SerializeField] private EnemyAiKind _ai = EnemyAiKind.Walker;

        [Header("Contact attack tuning")]
        [Tooltip("Distance ahead the enemy probes for an attackable structure (world units).")]
        [SerializeField] private float _contactProbeDistance = 1.1f;

        [Tooltip("Distance from the Heart at which the enemy considers itself 'arrived'.")]
        [SerializeField] private float _heartArrivalRadius = 2.5f;

        /// <summary>
        /// Seconds the dead enemy GameObject lingers so its death animation can
        /// play before <see cref="Die"/> destroys it. Only applied when the enemy
        /// has an Animator; with none it is destroyed immediately.
        /// </summary>
        private const float DeathHoldSeconds = 1.6f;

        // ── Runtime refs / state ──────────────────────────────────────────────

        private NavMeshAgent _agent;
        private Transform _heart;
        private EnemyDef _def;
        private float _attackCooldown;
        private bool _dead;
        private bool _navWarned;
        private IDamageableStructure _currentTarget;

        // ── Animation ─────────────────────────────────────────────────────────
        // The KayKit skeleton mesh carries an Animator (the AnimatorSetup editor
        // script builds HumanoidEnemy/LargeEnemy/Boss.controller; the integrator
        // assigns one to the enemy prefab — see docs/port-notes/animation-setup.md).
        // Enemy DRIVES it: Speed float from movement, Attack/Hit triggers on the
        // contact strike + damage, Dead bool on death. All parameter sets are
        // null-guarded so an enemy with no Animator still runs its gameplay.
        private Animator _animator;

        // Animator parameter hashes — must match AnimatorSetup.cs's parameter
        // names ("Speed" / "Attack" / "Hit" / "Dead").
        private static readonly int AnimSpeed  = Animator.StringToHash("Speed");
        private static readonly int AnimAttack = Animator.StringToHash("Attack");
        private static readonly int AnimHit    = Animator.StringToHash("Hit");
        private static readonly int AnimDead   = Animator.StringToHash("Dead");

        /// <summary>Raised when this enemy's HP reaches zero. Arg = this enemy.</summary>
        public event Action<Enemy> Died;

        /// <summary>
        /// Raised when this enemy reaches the Heart without being killed. The
        /// WaveManager listens to escalate the Heart's threat state.
        /// </summary>
        public event Action<Enemy> ReachedHeart;

        /// <summary>Stable per-instance id — the breach-roster key.</summary>
        public string EnemyId => _enemyId;

        /// <summary>The <c>enemies.json</c> def id this enemy was spawned from.</summary>
        public string EnemyDefId => _enemyDefId;

        /// <summary>Current hit points.</summary>
        public float Hp => _hp;

        /// <summary>Max hit points.</summary>
        public float MaxHp => _maxHp;

        /// <summary>HP as a 0..1 fraction — drives the floating HP bar.</summary>
        public float HpFraction => _maxHp > 0f ? Mathf.Clamp01(_hp / _maxHp) : 0f;

        /// <summary>True once the enemy has died (HP hit zero).</summary>
        public bool IsDead => _dead;

        /// <summary>AI archetype this enemy runs.</summary>
        public EnemyAiKind Ai => _ai;

        /// <summary>
        /// The engine def id the breach trigger maps this enemy to when handing
        /// the ATB scene a battle. The KayKit village skeletons map onto the ATB
        /// engine's <c>"skeleton"</c> combatant def (BattleController's fallback);
        /// the Necromancer maps onto <c>"necromancer"</c> when that def exists.
        /// </summary>
        public string EngineDefId => _enemyDefId == "necromancer" ? "necromancer" : "skeleton";

        // ---------------------------------------------------------------------
        // Configuration — called by WaveManager right after Instantiate
        // ---------------------------------------------------------------------

        /// <summary>
        /// Wires this enemy from its stat block and the scene context. Called by
        /// <see cref="WaveManager"/> immediately after instantiation.
        /// </summary>
        /// <param name="enemyId">Stable per-instance id (the breach-roster key).</param>
        /// <param name="def">The deserialised <c>enemies.json</c> stat block.</param>
        /// <param name="heart">The Heart transform — the enemy's march goal.</param>
        public void Configure(string enemyId, EnemyDef def, Transform heart)
        {
            _enemyId = enemyId;
            _heart = heart;
            _def = def;

            if (def != null)
            {
                _enemyDefId = def.Id;
                _maxHp = Mathf.Max(1f, def.Hp);
                _hp = _maxHp;
                _moveSpeed = Mathf.Max(0.1f, def.MoveSpeed);
                _contactDamage = Mathf.Max(0f, def.ContactDamage);
                _attackInterval = Mathf.Max(0.1f, def.AttackInterval);
                _ai = def.AiKind;
            }

            EnsureAgent();
            if (_agent != null)
            {
                _agent.speed = _moveSpeed;
                _agent.stoppingDistance = _heartArrivalRadius;
            }

            _dead = false;
            _attackCooldown = 0f;
            _navWarned = false;
        }

        // ---------------------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------------------

        private void Awake()
        {
            EnsureAgent();
            EnsureAnimator();
        }

        private void Update()
        {
            if (_dead) return;

            TickContactAttack();
            DriveNav();
            DriveAnimator();
        }

        // ---------------------------------------------------------------------
        // Animation — push the locomotion speed to the Animator each frame
        // ---------------------------------------------------------------------

        /// <summary>
        /// Feeds the Animator's <c>Speed</c> float from the agent's actual
        /// velocity so the controller blends idle &lt;-&gt; move. No-op when the
        /// enemy has no Animator (parameter sets are all null-guarded).
        /// </summary>
        private void DriveAnimator()
        {
            if (_animator == null) return;
            float speed = (_agent != null && _agent.isOnNavMesh)
                ? _agent.velocity.magnitude
                : 0f;
            _animator.SetFloat(AnimSpeed, speed);
        }

        // ---------------------------------------------------------------------
        // Navigation — march toward the Heart
        // ---------------------------------------------------------------------

        /// <summary>
        /// Steers the agent toward the Heart. While the enemy is locked onto a
        /// structure (contact attack) the agent is held in place. Logs ONCE if
        /// the agent is not on a baked NavMesh — the village scene needs baking.
        /// </summary>
        private void DriveNav()
        {
            if (_agent == null || _heart == null) return;

            // Locked onto a structure — stand and fight, do not path past it.
            if (_currentTarget != null && _currentTarget.IsAlive)
            {
                if (_agent.isOnNavMesh && !_agent.isStopped) _agent.isStopped = true;
                return;
            }

            if (!_agent.isOnNavMesh)
            {
                if (!_navWarned)
                {
                    Debug.LogWarning(
                        $"[Enemy:{_enemyId}] NavMeshAgent is not on a baked NavMesh — " +
                        "the enemy cannot move. The village scene needs NavMesh baking " +
                        "(see docs/port-notes/week4-waves.md).");
                    _navWarned = true;
                }
                return;
            }

            if (_agent.isStopped) _agent.isStopped = false;
            _agent.SetDestination(_heart.position);

            // Arrived at the Heart without being repelled — report the breach.
            float planarDist = Vector3.ProjectOnPlane(
                _heart.position - transform.position, Vector3.up).magnitude;
            if (planarDist <= _heartArrivalRadius)
            {
                ReachedHeart?.Invoke(this);
            }
        }

        // ---------------------------------------------------------------------
        // Contact attack — strike the structure directly ahead
        // ---------------------------------------------------------------------

        /// <summary>
        /// Probes for an <see cref="IDamageableStructure"/> directly ahead; when
        /// one is in reach the enemy stops and deals <see cref="_contactDamage"/>
        /// every <see cref="_attackInterval"/> seconds until it falls.
        /// </summary>
        private void TickContactAttack()
        {
            // Drop a dead / destroyed target.
            if (_currentTarget != null && !_currentTarget.IsAlive)
                _currentTarget = null;

            if (_currentTarget == null)
                _currentTarget = ProbeForStructure();

            if (_currentTarget == null)
            {
                _attackCooldown = 0f;
                return;
            }

            _attackCooldown -= Time.deltaTime;
            if (_attackCooldown <= 0f)
            {
                _currentTarget.ApplyContactDamage(_contactDamage);
                _attackCooldown = _attackInterval;
                // Fire the melee-strike animation in sync with the damage tick.
                if (_animator != null) _animator.SetTrigger(AnimAttack);
            }
        }

        /// <summary>
        /// Casts a short sphere ahead of the enemy and returns the first
        /// <see cref="IDamageableStructure"/> it hits, or null when the lane is
        /// clear. Skirmishers probe slightly wider so they peel toward walls.
        /// </summary>
        private IDamageableStructure ProbeForStructure()
        {
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            Vector3 forward = transform.forward;
            float radius = _ai == EnemyAiKind.Skirmisher ? 0.6f : 0.4f;

            if (Physics.SphereCast(origin, radius, forward, out RaycastHit hit,
                    _contactProbeDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                // The structure may host the interface on the collider's object
                // or a parent (the collider is often a child blocker).
                var structure = hit.collider.GetComponentInParent<IDamageableStructure>();
                if (structure != null && structure.IsAlive)
                    return structure;
            }
            return null;
        }

        // ---------------------------------------------------------------------
        // HP / death
        // ---------------------------------------------------------------------

        /// <summary>
        /// Applies <paramref name="amount"/> damage. At zero HP the enemy dies,
        /// raises <see cref="Died"/> and is destroyed. Hero abilities, pets and
        /// towers route their damage through here.
        /// </summary>
        public void TakeDamage(float amount)
        {
            if (_dead || amount <= 0f) return;

            // Floating combat text — pop the damage number at the enemy's head so
            // the player can see the hit (and watch it rise after a damage talent).
            // Spawned BEFORE death so the killing blow still shows its number even
            // though this GameObject may be destroyed below. Self-contained + asset
            // free; it null-guards the camera internally.
            DamageNumberSpawner.Spawn(amount, HeadWorldPosition());

            _hp = Mathf.Max(0f, _hp - amount);
            if (_hp <= 0f)
            {
                Die(killed: true);
            }
            else
            {
                // Survived — flinch animation + hit-impact pop (DEF-52) + hit stop / combo (DEF-44/45).
                if (_animator != null) _animator.SetTrigger(AnimHit);
                VfxPool.SpawnHitImpact(transform.position + Vector3.up * 0.6f);
                CombatFeedbackManager.Hit(transform.position + Vector3.up * 0.6f, amount);
            }
        }

        /// <summary>Kills the enemy immediately (e.g. consumed into an ATB breach).</summary>
        public void Kill()
        {
            if (!_dead) Die(killed: false);
        }

        /// <param name="killed">
        /// True when HP reached zero (a real defender kill — grants shared XP);
        /// false when force-removed (ATB breach) — no XP, just drop its ledger.
        /// </param>
        private void Die(bool killed)
        {
            _dead = true;
            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;
            _currentTarget = null;
            Died?.Invoke(this);

            // DEF-52: death burst VFX + micro screen shake so kills feel impactful.
            // DEF-45: kill streak tracked via CombatFeedbackManager.
            if (killed)
            {
                VfxPool.SpawnDeathBurst(transform.position + Vector3.up * 0.5f);
                CameraShakeBridge.Shake(0.18f, 0.22f);
                CombatFeedbackManager.Kill(transform.position);
            }

            // Kill-XP attribution: a genuine kill shares this enemy's XP across
            // the combatants that damaged it; a forced removal (breach) just
            // discards its damage ledger so nothing leaks and no XP is granted.
            if (killed) DeNelle.Village.Progression.ProgressionManager.ReportKill(this);
            else DeNelle.Core.Combat.DamageAttribution.Forget(this);

            // DEF-88: grant the flat per-enemy XP reward directly to the hero.
            if (killed && _def != null && HeroProgression.Instance != null)
                HeroProgression.Instance.AddXp(_def.XpReward);

            // DEF-32: grant Glimmer (cosmetic currency) on kill. Resolved via
            // reflection — GlimmerCurrencyService lives in DeNelle.Cosmetics,
            // which DeNelle.Village does not reference (asmdef stays decoupled,
            // mirroring PetDeployer's bridge).
            if (killed && _def != null && _def.GlimmerReward > 0)
                TryAwardGlimmer(_def.GlimmerReward);

            // Play the death (collapse) animation, then destroy. The Dead bool
            // latches the controller's Death state from anywhere; the GameObject
            // is held DeathHoldSeconds so the collapse clip is visible before
            // it is removed. With no Animator the enemy is destroyed at once.
            if (_animator != null)
            {
                _animator.SetBool(AnimDead, true);
                Destroy(gameObject, DeathHoldSeconds);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        private void EnsureAgent()
        {
            if (_agent == null) _agent = GetComponent<NavMeshAgent>();
        }

        // ── Glimmer reflection bridge (DEF-32) ───────────────────────────────
        // GlimmerCurrencyService lives in DeNelle.Cosmetics, which DeNelle.Village
        // does not reference. Resolve + invoke by reflection so the asmdef stays
        // decoupled (same pattern as PetDeployer). The Type/Method lookups are
        // cached; the live singleton is re-fetched each call so a scene reload
        // never leaves a stale (destroyed) instance reference.
        private static System.Type _glimmerType;
        private static System.Reflection.PropertyInfo _glimmerInstanceProp;
        private static System.Reflection.MethodInfo _glimmerTryAdd;
        private static bool _glimmerResolved;

        private static void TryAwardGlimmer(int amount)
        {
            try
            {
                if (!_glimmerResolved)
                {
                    _glimmerResolved = true;
                    foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        var t = asm.GetType("DeNelle.Cosmetics.GlimmerCurrencyService", false);
                        if (t != null) { _glimmerType = t; break; }
                    }
                    if (_glimmerType != null)
                    {
                        _glimmerInstanceProp = _glimmerType.GetProperty("Instance",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        _glimmerTryAdd = _glimmerType.GetMethod("TryAddGlimmer", new[] { typeof(int) });
                    }
                }
                if (_glimmerInstanceProp == null || _glimmerTryAdd == null) return;
                var instance = _glimmerInstanceProp.GetValue(null);
                if (instance == null) return;
                _glimmerTryAdd.Invoke(instance, new object[] { amount });
            }
            catch { /* cosmetic reward is best-effort; never break the kill path */ }
        }

        /// <summary>
        /// World point just above the enemy's head, where floating damage numbers
        /// spawn. Uses the rendered mesh bounds when available so the number clears
        /// the model's actual height; falls back to a fixed offset above the
        /// transform when the enemy has no Renderer yet.
        /// </summary>
        private Vector3 HeadWorldPosition()
        {
            Renderer rend = GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                Bounds b = rend.bounds;
                return new Vector3(b.center.x, b.max.y + 0.4f, b.center.z);
            }
            return transform.position + Vector3.up * 2.0f;
        }

        /// <summary>
        /// Resolves the Animator on the enemy rig (it sits on the KayKit skeleton
        /// mesh child, so search children too). Null when the prefab has no rig /
        /// no controller assigned — every Animator call is null-guarded.
        /// </summary>
        private void EnsureAnimator()
        {
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.86f, 0.27f, 0.27f, 0.9f);
            Gizmos.DrawRay(transform.position + Vector3.up * 0.5f,
                transform.forward * _contactProbeDistance);
        }
#endif
    }
}
