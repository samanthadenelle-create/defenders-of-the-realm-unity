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
        [SerializeField, Min(1f)] private float _heroEngageRadius = 11f;

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

        /// <summary>
        /// Assign tactical config at RUNTIME (spawned enemies have no inspector SO, so they
        /// all defaulted to Rush — even casters charged). EnemyFactory/Enemy.Configure calls
        /// this to give caster-role enemies the Kiter archetype. Null-safe; ignores null.
        /// </summary>
        public void SetTactics(TacticalData t) { if (t != null) _tactics = t; }

        /// <summary>
        /// WO-482 (arena): mark this brain as a HERO-ONLY duelist (isolated BattleArena).
        /// Target selection then always picks the hero and never falls back to the far-off
        /// HeartOfElarion (there is no base to siege in the arena). No effect on
        /// village/overworld enemies, which never call this.
        /// </summary>
        public void SetHeroOnlyTarget(bool on) { _heroOnlyTarget = on; }

        /// <summary>
        /// True when this brain is a HERO-ONLY duelist (set by the isolated BattleArena and
        /// the in-place OutpostEnemyGroupSpawner dungeon/outpost groups). Read by
        /// <see cref="Enemy"/> to raise the in-scene <see cref="DeNelle.Core.Combat.HeroCombatEngagement"/>
        /// battle-lock so the hero's attack input is live for these fights (2026-06-30 dungeon 0-damage fix).
        /// </summary>
        public bool HeroOnlyTarget => _heroOnlyTarget;

        // Shared runtime Kiter config (WO-145 Tactic B): hold ~10 m, back off inside 6 m,
        // gentle weave, fire ranged every 1.6 s. ONE instance reused across all caster
        // enemies — DPS-mage behaviour: "casts from a distance, AI keeps its distance"
        // (owner request 2026-06-13). Built lazily; Date/Random-free so it's deterministic.
        private static TacticalData s_kiterTactics;
        public static TacticalData KiterTactics
        {
            get
            {
                if (s_kiterTactics == null)
                {
                    s_kiterTactics = ScriptableObject.CreateInstance<TacticalData>();
                    s_kiterTactics.name = "TacticalData_Kiter(runtime)";
                    s_kiterTactics.Archetype = EnemyArchetype.Kiter;
                    s_kiterTactics.KiteDesiredRange = 10f;
                    s_kiterTactics.KiteMinRange = 6f;
                    s_kiterTactics.KiteStrafeJitter = 1.5f;
                    s_kiterTactics.KiteAttackCooldown = 1.6f;
                }
                return s_kiterTactics;
            }
        }

        // Shared runtime Flanker config: arcs ~90 deg off the direct path so the unit
        // approaches from the side/rear instead of charging straight in. ONE instance
        // reused across all flanker enemies (e.g. the arena warrior). Built lazily;
        // Date/Random-free so it's deterministic. Mirrors the KiterTactics pattern.
        private static TacticalData s_flankerTactics;
        public static TacticalData FlankerTactics
        {
            get
            {
                if (s_flankerTactics == null)
                {
                    s_flankerTactics = ScriptableObject.CreateInstance<TacticalData>();
                    s_flankerTactics.name = "TacticalData_Flanker(runtime)";
                    s_flankerTactics.Archetype = EnemyArchetype.Flanker;
                    s_flankerTactics.FlankAngleOffset = 90f;
                }
                return s_flankerTactics;
            }
        }

        // ── Runtime ───────────────────────────────────────────────────────────

        private Enemy    _enemy;
        private Transform _heartTransform;
        private Transform _heroTransform;
        // WO-482 (arena): when set, this brain belongs to an ISOLATED duel (BattleArena) where
        // there is NO base to siege. Target selection then ALWAYS resolves to the hero and the
        // HeartOfElarion (village-siege) win-condition fallback is suppressed. Off by default, so
        // village/overworld enemies keep their normal heart-siege behaviour unchanged.
        private bool      _heroOnlyTarget;
        private Transform _petTransform;        // WO-145: resolved by "PetTarget" tag (null-safe).
        private float     _healCooldown;
        private float     _suppressTimer;
        private float     _heroResolveTimer;   // WO-419: throttles periodic hero re-acquire.
        private float     _provokedHeroResolveTimer;   // Audit P3: throttles retaliation-path hero re-acquire.

        private EnemyTacticalState _tacticalState = EnemyTacticalState.Rush;

        // A ranged enemy reads as a ranger only if it actually HOLDS a bow. The role
        // (EnemyRole.Ranged) is assigned by the spawner AFTER Awake (brain.Role = ...),
        // so we can't equip in Awake — we latch a one-time, idempotent equip on the first
        // Update tick once the role is known and the skinned body+Animator exist. Reuses
        // the hero's bow logic (HeroBowAttachment is a generic LeftHand-bone attacher; the
        // "Hero" naming is cosmetic). Non-ranged enemies never enter this path.
        private bool _bowEquipChecked;

        private readonly Collider[] _scanBuffer = new Collider[32];

        // DEF-72: throttle target-priority re-evaluation (not per-frame).
        // WO-145: now WIRED — gates ScoreAndPickTarget so the offensive roles
        // re-score on the interval and reuse the cached _currentTarget between ticks.
        private float _targetEvalTimer;
        private const float TargetEvalInterval = 2f;

        // WO-145 (Tactic C): a signed flank bearing assigned by EnemyGroupCoordinator
        // for a coordinated pincer; overrides the per-enemy FlankAngleOffset when set.
        private bool  _coordinatedFlankSet;
        private float _coordinatedFlankAngle;

        // WO-145 (Tactic D): reposition (rally → re-engage) timer.
        private float _repositionTimer;
        private bool  _atRallyPoint;

        // WO-145 (Tactic B): kite strafe sign, flips on a timer for a lively weave.
        private float _kiteStrafeTimer;
        private float _kiteStrafeSign = 1f;

        // DEF-43: optional BehaviorTree override — wired in Awake if present.
        private EnemyBehaviorTree _bt;

        // Retaliation: when the hero/pet damages this enemy it is "provoked" and
        // chases + attacks the attacker for this window, overriding role/BT/radius
        // (owner 2026-06-02: enemies "just walked on past me"). Re-armed on each hit.
        private float _provokedUntil;
        private const float ProvokeDuration = 6f;

        // Tier-2 party teamwork (Knight TAUNT): a companion Knight can FORCE this
        // enemy to fix on the knight for a window, pulling aggro off the hero / the
        // wounded backline. Modeled on the retaliation provoke above — same single
        // owner of enemy targeting (EnemyBrain), no second targeting authority. The
        // taunt overrides role/BT/engage-radius while active, exactly like provoke.
        private float _tauntUntil;
        private Transform _taunter;

        // WO-90: attack state for TryAttack().
        private float     _nextAttackTime;
        private Animator  _animator;
        private Transform _currentTarget;

        // WO-147: consolidated perception sensor (auto-added in Awake) + IsAlert drive.
        private AwarenessSensor _sensor;
        private float _sensorScanTimer;
        private DeNelle.Core.AwarenessState _lastAwareness = DeNelle.Core.AwarenessState.Unaware;
        private static readonly int AnimIsAlert = Animator.StringToHash("IsAlert");
        // WO-163: cached once at init — whether this enemy's controller declares
        // "IsAlert". Driving an absent param logs "Parameter does not exist".
        private bool _hasIsAlertParam;

        // WO-92: cached NavMeshAgent for NavMesh path validation.
        private NavMeshAgent _navAgent;

        // WO-410 (perf): the Rush path-validity check is the #1 GC source (~13-22MB/frame
        // at OuterWorld caps) — it allocated a new NavMeshPath() and ran a full CalculatePath
        // EVERY frame, per enemy. Reuse ONE path object (CalculatePath overwrites it each call)
        // and recompute the validity only on the target-eval cadence (~2s), caching the result
        // between ticks. The destination barely moves frame-to-frame and Enemy.DriveNav already
        // throttles the actual SetDestination (DEF-56), so per-frame revalidation was pure waste.
        private NavMeshPath _rushPath;   // lazily created — `new NavMeshPath()` in a field initializer throws
                                         // (Unity: InitializeNavMeshPath not allowed from a MonoBehaviour ctor).
        private float _rushPathTimer;
        private bool  _rushPathValid = true;   // assume reachable until first check proves otherwise

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
        /// Hook called by EnemyBehaviorTree's StopAndEngage leaf, and by this brain
        /// while kiting. For melee enemies it remains a no-op (Enemy.TickContactAttack
        /// fires automatically once the agent stops). WO-145 (#7): when this enemy is
        /// in the <see cref="EnemyTacticalState.Kite"/> state with its target inside
        /// the standoff band, it fires a hit-scan ranged attack on cooldown.
        /// </summary>
        public void TriggerAttack()
        {
            // Melee path: Enemy handles contact damage in its own Update. Stopping the
            // NavMeshAgent (via SetBrainTargetPosition) is sufficient to enter contact.
            if (_tacticalState != EnemyTacticalState.Kite) return;

            // WO-145 (Tactic B): ranged fire while kiting a live target in the band.
            if (_currentTarget == null) return;
            if (Time.time < _nextAttackTime) return;

            float dist = (_currentTarget.position - transform.position).magnitude;
            float desired = _tactics != null ? _tactics.KiteDesiredRange : 8f;
            if (dist > desired + 0.5f) return;   // out of band — close first, don't fire

            float cooldown = _tactics != null && _tactics.KiteAttackCooldown > 0f
                ? _tactics.KiteAttackCooldown : attackCooldown;
            if (_enemy != null && _enemy.RangedAttack(_currentTarget, damage))
                _nextAttackTime = Time.time + cooldown;
        }

        // ── WO-145 (Tactic C): coordinated-flank surface read by EnemyGroupCoordinator ──

        /// <summary>True when this enemy's tactics opt it into a coordinated group pincer.</summary>
        public bool WantsCoordinatedFlank =>
            _tactics != null && _tactics.CoordinatedFlank && _tactics.Archetype == EnemyArchetype.Flanker;

        /// <summary>The per-enemy flank angle from tactics (fallback when no coordinated angle is set).</summary>
        public float FlankAngleOffset => _tactics != null ? _tactics.FlankAngleOffset : 90f;

        /// <summary>
        /// WO-145 (Tactic C): EnemyGroupCoordinator assigns a distinct signed bearing
        /// (left / right / rear) so the released group envelops the target from
        /// multiple angles at once. Overrides <see cref="FlankAngleOffset"/> in the
        /// Flank destination math until cleared.
        /// </summary>
        public void SetCoordinatedFlankAngle(float signedDegrees)
        {
            _coordinatedFlankAngle = signedDegrees;
            _coordinatedFlankSet   = true;
        }

        /// <summary>
        /// WO-145/146/147: the brain's currently chosen offensive target. Read by the
        /// family leader (WO-146) for engage context and the perception aggregation
        /// (WO-147). Read-only; no behaviour change.
        /// </summary>
        public Transform CurrentTarget => _currentTarget;

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
            _enemy.Damaged += OnEnemyDamaged;   // retaliate when struck

            // DEF-43: wire BT if present on this GameObject.
            _bt = GetComponent<EnemyBehaviorTree>();

            // WO-90: cache Animator and NavMeshAgent from this GameObject.
            _animator  = GetComponentInChildren<Animator>();
            _navAgent  = GetComponent<NavMeshAgent>();

            // WO-163: cache whether the controller declares "IsAlert" so we never
            // drive an absent param (logs an error each state change otherwise).
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                foreach (var p in _animator.parameters)
                    if (p.nameHash == AnimIsAlert) { _hasIsAlertParam = true; break; }
            }

            // WO-147: ensure a perception sensor exists (auto-add — backward-safe so
            // existing enemy prefabs gain perception with zero prefab wiring).
            _sensor = GetComponent<AwarenessSensor>();
            if (_sensor == null) _sensor = gameObject.AddComponent<AwarenessSensor>();

            // WO-86: overlay balance stats from EnemyData SO if assigned.
            if (_enemyData != null)
            {
                damage         = _enemyData.damage;
                attackCooldown = _enemyData.attackCooldown;
            }

            // Cache scene-wide refs once — FindAnyObjectByType is expensive per frame.
            var hc = FindAnyObjectByType<HeartController>();
            _heartTransform = hc != null ? hc.transform : null;

            // WO-450: resolve the hero by component (HeroLocomotion), not the (undeclared)
            // "HeroTarget" tag. The hero now carries the "Player" tag (one tag per object).
            _heroTransform = FindHeroTransform();

            // WO-145 (Tactic A): resolve the player's pet by tag (null-safe). The pet
            // is DeNelle.Pets — we never reference it for AI, only target its tagged
            // transform. Absent tag ⇒ no pet candidate (backward-safe). Wrapped because
            // FindWithTag throws if the "PetTarget" tag is undefined in the project.
            _petTransform = TryFindByTag("PetTarget");
        }

        /// <summary>
        /// Null-safe tag lookup that tolerates an undefined tag (Unity throws on an
        /// unknown tag string). Returns null when the tag is absent or unused.
        /// </summary>
        private static Transform TryFindByTag(string tag)
        {
            try
            {
                var go = GameObject.FindWithTag(tag);
                return go != null ? go.transform : null;
            }
            catch (UnityEngine.UnityException)
            {
                return null;   // tag not defined in this project — no candidate.
            }
        }

        /// <summary>
        /// WO-450: resolve the hero by COMPONENT, not tag. A GameObject has exactly one
        /// tag and the hero now carries "Player" (HeroControlEnsurer) — the old "HeroTarget"
        /// tag was never declared and always missed. HeroLocomotion is the single component
        /// every hero variant (real/swapped/emergency) carries, so it is the canonical
        /// scene-agnostic hero handle. Falls back to the "Player" tag for safety. Null-safe.
        /// </summary>
        private static Transform FindHeroTransform()
        {
            var loco = FindFirstObjectByType<HeroLocomotion>();
            if (loco != null) return loco.transform;
            return TryFindByTag("Player");
        }

        /// <summary>Hero/pet struck this enemy — provoke it to chase + hit back.</summary>
        private void OnEnemyDamaged(Vector3 sourceWorldPos)
        {
            _provokedUntil = Time.time + ProvokeDuration;
        }

        /// <summary>
        /// Tier-2 Knight TAUNT — forces this enemy to fix on <paramref name="taunter"/>
        /// (the companion Knight) for <paramref name="seconds"/>, pulling its aggro off
        /// the hero / wounded allies. Reuses the retaliation-override seam so EnemyBrain
        /// stays the SINGLE owner of enemy targeting (no second authority). Null-safe.
        /// </summary>
        public void TauntTo(Transform taunter, float seconds)
        {
            if (taunter == null || seconds <= 0f) return;
            _taunter   = taunter;
            _tauntUntil = Time.time + seconds;
        }

        private void Update()
        {
            if (_enemy == null || _enemy.IsDead) return;

            // GEAR: give a Ranged enemy a VISIBLE bow on its bow (LEFT) hand so a ranger
            // reads as a ranger (it fires Enemy.RangedAttack but previously held nothing).
            // The role is set by the spawner after Awake, so this is the first point where
            // the role is known AND the skinned body + Animator exist. Runs ONCE (latched),
            // is fully guarded inside HeroBowAttachment (missing Animator / LeftHand bone →
            // FlowTrace.Warn + skip, never an NRE), and is idempotent (its own _bow guard +
            // DisallowMultipleComponent). A NON-Ranged role never reaches here, so it never
            // gets a bow.
            if (!_bowEquipChecked)
            {
                _bowEquipChecked = true;
                if (Role == EnemyRole.Ranged)
                {
                    DeNelle.Core.Diagnostics.FlowTrace.Step("Equip",
                        $"enemy '{name}' is Ranged — attaching bow to bow hand");
                    // Pass the enemy root as both root + body: AttachTo resolves the Animator
                    // via GetComponentInChildren on the body, which finds the vis child's rig.
                    HeroBowAttachment.AttachTo(gameObject, gameObject);
                }
            }

            // TAUNT OVERRIDE (Tier-2 Knight): a taunting companion Knight fixes this
            // enemy onto itself, ahead of even retaliation — it's a deliberate tank
            // pull, so it wins over an incidental provoke. Same override shape: lock the
            // target, stop on it, attack. Drops through once the taunter dies/leaves.
            if (Time.time < _tauntUntil && _taunter != null)
            {
                _currentTarget = _taunter;
                _enemy.SetBrainTarget(_taunter);
                _enemy.SetBrainTargetPosition(_taunter.position);
                TriggerAttack();
                return;
            }

            // RETALIATION OVERRIDE: a recently-struck enemy locks onto the hero and
            // chases + attacks it, ignoring role/BT/engage-radius for the window.
            // Runs BEFORE the BT yield so even BT-driven enemies fight back when hit.
            if (Time.time < _provokedUntil)
            {
                // Lazily resolve the hero if it wasn't present at Awake (the enemy was
                // just struck, so the hero definitely exists now). Audit P3: FindHeroTransform
                // is an O(n) scene scan — throttle it (0.5s) so an enemy provoked while the hero
                // is absent doesn't spam the lookup every frame for the whole provoke window.
                _provokedHeroResolveTimer -= Time.deltaTime;
                if (_heroTransform == null && _provokedHeroResolveTimer <= 0f)
                {
                    _provokedHeroResolveTimer = 0.5f;
                    _heroTransform = FindHeroTransform();   // WO-450: component lookup
                }
            }
            if (Time.time < _provokedUntil && _heroTransform != null)
            {
                _currentTarget = _heroTransform;
                _enemy.SetBrainTarget(_heroTransform);
                _enemy.SetBrainTargetPosition(_heroTransform.position);
                TriggerAttack();
                return;
            }

            // DEF-43: if a BehaviorTree is wired and ready, yield all targeting to it.
            if (_bt != null && _bt.IsInitialized)
            {
                _bt.Evaluate();
                return;
            }

            // WO-419: re-acquire the hero on a 1s cadence (matches Enemy.ResolveHeroTransform) so a
            // brain-driven enemy that cached null/stale at Awake under additive OuterWorld streaming
            // still engages. Only re-finds when the cached ref is gone (cheap). Explicit null check
            // on the UnityEngine.Object (?? would return a fake-null); the TryFindByTag chain below is
            // safe because TryFindByTag returns a real null literal, not a destroyed-object reference.
            _heroResolveTimer -= Time.deltaTime;
            // Audit P2 (enemies-ai): treat a DISABLED (not just destroyed) hero as invalid so a
            // stale ref to a deactivated hero gets re-resolved — matches Enemy.cs's activeInHierarchy
            // check. Explicit null test first (?? would return a fake-null on the UnityEngine.Object).
            bool heroValid = _heroTransform != null && _heroTransform.gameObject.activeInHierarchy;
            if (!heroValid && _heroResolveTimer <= 0f)
            {
                _heroResolveTimer = 1f;
                _heroTransform = FindHeroTransform();   // WO-450: component lookup
            }

            // WO-147: drive the consolidated perception sensor on the (LOD-scaled)
            // eval cadence — one throttled scan, NOT per frame — then push the
            // resulting AwarenessState to the Animator "IsAlert" param on change.
            TickPerception();

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

            // WO-145 (Tactic B): while kiting, fire ranged attacks on cooldown when
            // the target is inside the standoff band (TriggerAttack no-ops otherwise).
            if (_tacticalState == EnemyTacticalState.Kite)
                TriggerAttack();

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

            // WO-145 (Tactic D): if currently repositioning, stay until re-engage.
            if (_tacticalState == EnemyTacticalState.Reposition)
            {
                _repositionTimer -= Time.deltaTime;
                bool healed   = _enemy.HpFraction >= _tactics.ReengageHealthThreshold;
                bool regrouped = _atRallyPoint && _repositionTimer <= 0f;
                if (healed || regrouped)
                {
                    _atRallyPoint = false;
                    _tacticalState = ArchetypeDefaultState();
                }
                return;
            }

            // Retreat / Reposition if HP has dropped below threshold.
            if (_tactics.RetreatHealthThreshold > 0f
                && _enemy.HpFraction < _tactics.RetreatHealthThreshold)
            {
                if (_tactics.RepositionInsteadOfFlee)
                {
                    // WO-145 (Tactic D): rally to allies, then re-engage (not blind flee).
                    _tacticalState   = EnemyTacticalState.Reposition;
                    _repositionTimer = _tactics.RepositionRegroupSeconds;
                    _atRallyPoint    = false;
                }
                else
                {
                    _tacticalState = EnemyTacticalState.Retreat;
                }
                return;
            }

            // Assign archetype-default tactical state when not retreating.
            _tacticalState = ArchetypeDefaultState();
        }

        /// <summary>
        /// WO-145: maps the assigned archetype to its default movement posture.
        /// Flanker → Flank, Kiter → Kite, everything else → Rush.
        /// </summary>
        private EnemyTacticalState ArchetypeDefaultState()
        {
            if (_tactics == null) return EnemyTacticalState.Rush;
            return _tactics.Archetype switch
            {
                EnemyArchetype.Flanker => EnemyTacticalState.Flank,
                EnemyArchetype.Kiter   => EnemyTacticalState.Kite,
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
                    // WO-145 (Tactic C): use the coordinator-assigned envelope bearing
                    // when set (distinct L/R/rear per group member), else the per-enemy
                    // FlankAngleOffset (legacy, backward-compatible).
                    float angle = _coordinatedFlankSet
                        ? _coordinatedFlankAngle
                        : (_tactics != null ? _tactics.FlankAngleOffset : 90f);
                    // Rotate the direct-path vector by the flank angle (in the XZ plane).
                    Vector3 direct = (target.position - transform.position);
                    direct.y = 0f;
                    if (direct.sqrMagnitude < 0.01f) return target.position;
                    Vector3 flankDir = Quaternion.AngleAxis(angle, Vector3.up) * direct.normalized;
                    float dist = direct.magnitude;
                    return target.position + flankDir * (dist * 0.5f);
                }

                case EnemyTacticalState.Kite:
                    return ComputeKiteDestination(target);

                case EnemyTacticalState.Reposition:
                    return ComputeRepositionDestination(target);

                default:
                {
                    // Rush: go directly to the target's position.
                    // WO-92: validate that a complete NavMesh path exists before
                    // committing to this destination.
                    // WO-410 (perf): throttle this validity check to the target-eval cadence
                    // (~2s) and reuse a single pooled NavMeshPath instead of allocating one
                    // per frame, per enemy. The destination barely moves frame-to-frame, and
                    // Enemy.DriveNav already throttles the real SetDestination (DEF-56) — so
                    // the cached validity is good enough and the per-frame alloc + full path
                    // scan (the #1 GC source) is eliminated. Per-frame movement is untouched.
                    if (_navAgent != null && _navAgent.isOnNavMesh)
                    {
                        _rushPathTimer -= Time.deltaTime;
                        if (_rushPathTimer <= 0f)
                        {
                            if (_rushPath == null) _rushPath = new NavMeshPath();   // lazy (ctor illegal at field-init)
                            _rushPathTimer = TargetEvalInterval;
                            _rushPathValid =
                                _navAgent.CalculatePath(target.position, _rushPath) &&
                                _rushPath.status == NavMeshPathStatus.PathComplete;
                        }

                        if (!_rushPathValid)
                        {
                            // CORE-LOOP RCA (EnemyAggro, 2026-06-18): the old behaviour HARD-HELD
                            // here (returned null → enemy freezes at range). The headless capture
                            // showed 14,207 "No complete NavMesh path — holding" lines: brain-driven
                            // raid/region enemies stalling out of reach of the hero instead of
                            // closing, so a base can never be cleared. A PartialPath to a MOVING
                            // hero (who can stand a hair off the baked mesh, or behind a thin
                            // unbaked seam) still has a reachable last corner that gets the enemy
                            // ADJACENT — inside HeroHealth's 1.5 m contact ring. So instead of
                            // freezing we STEER to the path's last reachable corner and keep
                            // closing; we only truly hold when even a partial path is empty
                            // (genuinely walled off). Structures don't move, so this also lets a
                            // siege enemy creep to the nearest reachable point of a half-blocked
                            // wall rather than stand idle.
                            Vector3 approach;
                            bool haveApproach = TryGetPartialApproach(target.position, out approach);
                            DeNelle.Core.Diagnostics.FlowTrace.Throttle(
                                "EnemyAggro", $"partial-{name}", 2f,
                                $"{name}: no COMPLETE path to '{target.name}' — " +
                                (haveApproach
                                    ? $"steering to last reachable corner {approach} (was: hold)."
                                    : "no partial corner either — holding (walled off)."));
                            return haveApproach ? approach : (Vector3?)null;
                        }
                    }
                    return target.position;
                }
            }
        }

        // ── WO-145 (Tactic B): kite standoff destination ──────────────────────

        /// <summary>
        /// WO-145: keeps the kiter inside [KiteMinRange, KiteDesiredRange] of its
        /// target — backs off when the target closes inside the min, closes to the
        /// outer band when too far, holds (with optional lateral weave) while in
        /// band. Off-NavMesh standoff points snap to the nearest valid point.
        /// </summary>
        private Vector3? ComputeKiteDestination(Transform target)
        {
            float desired = _tactics != null ? _tactics.KiteDesiredRange : 8f;
            float min     = _tactics != null ? _tactics.KiteMinRange : 5f;
            float jitter  = _tactics != null ? _tactics.KiteStrafeJitter : 0f;

            Vector3 toSelf = transform.position - target.position; toSelf.y = 0f;
            float dist = toSelf.magnitude;
            Vector3 dir = dist > 0.001f ? toSelf / dist : transform.forward;

            Vector3 destPos;
            if (dist < min)
            {
                // Too close — back off to the desired band edge.
                destPos = target.position + dir * desired;
            }
            else if (dist > desired)
            {
                // Too far — close to the outer band edge.
                destPos = target.position + dir * desired;
            }
            else
            {
                // In band — hold, with an optional perpendicular weave so the kiter
                // feels alive rather than frozen.
                destPos = transform.position;
                if (jitter > 0.01f)
                {
                    _kiteStrafeTimer -= Time.deltaTime;
                    if (_kiteStrafeTimer <= 0f)
                    {
                        _kiteStrafeSign  = -_kiteStrafeSign;
                        _kiteStrafeTimer = 1.2f;
                    }
                    Vector3 perp = Vector3.Cross(Vector3.up, dir).normalized;
                    destPos += perp * (_kiteStrafeSign * jitter);
                }
            }

            return SampleOnNavMesh(destPos);
        }

        // ── WO-145 (Tactic D): reposition (rally → re-engage) destination ─────

        /// <summary>
        /// WO-145: retreat TO a better position — the centroid of the nearest living
        /// ally cluster (regroup behind the pack), or a standoff fallback away from
        /// the target when alone. Snaps onto the NavMesh so the rally is reachable.
        /// </summary>
        private Vector3? ComputeRepositionDestination(Transform target)
        {
            float scanR = _tactics != null ? _tactics.RallyScanRadius : 12f;

            // 1. Nearest ally cluster centroid (reuse the shared scan buffer).
            int count = Physics.OverlapSphereNonAlloc(transform.position, scanR, _scanBuffer);
            Vector3 sum = Vector3.zero;
            int allies = 0;
            for (int i = 0; i < count; i++)
            {
                if (_scanBuffer[i] == null) continue;
                var ally = _scanBuffer[i].GetComponentInParent<Enemy>();
                if (ally == null || ally == _enemy || ally.IsDead) continue;
                sum += ally.transform.position;
                allies++;
            }

            Vector3 rally;
            if (allies > 0)
            {
                rally = sum / allies;
            }
            else
            {
                // 2. No allies — standoff fallback away from the target.
                float fallback = _tactics != null ? _tactics.RepositionFallbackDistance : 8f;
                Vector3 away = (transform.position - target.position); away.y = 0f;
                if (away.sqrMagnitude < 0.001f) away = transform.forward;
                rally = transform.position + away.normalized * fallback;
            }

            // Mark arrival so UpdateTacticalState can time the regroup → re-engage.
            if ((rally - transform.position).sqrMagnitude < 2.25f)   // within ~1.5 m
                _atRallyPoint = true;

            return SampleOnNavMesh(rally);
        }

        /// <summary>
        /// CORE-LOOP RCA (EnemyAggro): returns the last reachable corner of a
        /// PARTIAL NavMesh path toward <paramref name="dest"/> so a brain-driven
        /// enemy keeps CLOSING on a hero/structure it cannot completely path to,
        /// instead of freezing out of reach (the "enemies won't engage" blocker).
        /// Recomputes the path into the pooled <see cref="_rushPath"/> (the cached
        /// one may be stale by up to TargetEvalInterval, and a frozen enemy must
        /// re-aim now). Returns false only when there is no reachable corner at all
        /// (genuinely walled off) — the caller then holds. Null-safe / no alloc.
        /// </summary>
        private bool TryGetPartialApproach(Vector3 dest, out Vector3 approach)
        {
            approach = default;
            if (_navAgent == null || !_navAgent.isOnNavMesh) return false;
            if (_rushPath == null) _rushPath = new NavMeshPath();

            // CalculatePath fills corners even for a Partial result; the LAST corner
            // is the closest reachable point toward the destination.
            if (!_navAgent.CalculatePath(dest, _rushPath)) return false;
            var corners = _rushPath.corners;
            if (corners == null || corners.Length == 0) return false;

            Vector3 last = corners[corners.Length - 1];
            // Reject a degenerate "corner" that is essentially our own feet (no progress).
            if ((last - transform.position).sqrMagnitude < 0.25f) return false;
            approach = last;
            return true;
        }

        /// <summary>
        /// WO-145: snaps a desired world point onto the baked NavMesh (within 2 m),
        /// returning the sampled point or the original if no nearby mesh is found.
        /// </summary>
        private Vector3? SampleOnNavMesh(Vector3 worldPos)
        {
            if (NavMesh.SamplePosition(worldPos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                return hit.position;
            return worldPos;
        }

        // ── Role-based target selection (DEF-21) ──────────────────────────────

        private Transform ChooseTarget()
        {
            // WO-482 (arena): isolated duel -> the hero is the ONLY valid target. Never score
            // structures, never fall back to the (7000m-away, out-of-scene) HeartOfElarion, which
            // is what produced the "no COMPLETE path to 'HeartOfElarion'" milling. Re-acquire the
            // hero if the cached ref went stale so a late-streamed hero still engages.
            if (_heroOnlyTarget)
            {
                bool valid = _heroTransform != null && _heroTransform.gameObject.activeInHierarchy;
                if (!valid) _heroTransform = FindHeroTransform();
                return _heroTransform;
            }

            switch (Role)
            {
                case EnemyRole.Tank:
                    return FindHighestThreatTarget() ?? FindNearestTower() ?? _heartTransform;

                case EnemyRole.Healer:
                    return FindMostDamagedAlly() ?? _heartTransform;

                // DPS / Ranged / MiniBoss: WO-145 (Tactic A) weighted scorer picks the
                // best target (focus-fire the pet / wounded defender), throttled to the
                // eval interval. When tactics weights aren't assigned the scorer
                // degrades to the legacy nearest-ish chain (FindNearbyHero ?? Tower ?? tag).
                default:
                    // Basic strategy (per user req): DPS focus-fire healers first (protect
                    // the support), then score for low-HP/threat. Tanks protect healer by
                    // choosing hero/structure near healers. Healers already prioritize allies.
                    if ((Role == EnemyRole.DPS || Role == EnemyRole.Ranged) && FindMostDamagedAlly() != null)
                        return FindMostDamagedAlly() ?? ScoreAndPickTarget();
                    return ScoreAndPickTarget();
            }
        }

        // ── WO-147: perception cadence + IsAlert drive ────────────────────────

        /// <summary>
        /// WO-147: throttles the consolidated <see cref="AwarenessSensor"/> scan on
        /// the eval interval (state/distance LOD: distant Unaware enemies scan less
        /// often) and pushes the resulting <see cref="DeNelle.Core.AwarenessState"/>
        /// to the Animator <c>IsAlert</c> bool on change (null-safe no-op if absent).
        /// </summary>
        private void TickPerception()
        {
            if (_sensor == null) return;

            _sensorScanTimer -= Time.deltaTime;
            if (_sensorScanTimer <= 0f)
            {
                _sensor.Scan();

                // LOD: Unaware + far from the hero ⇒ slower cadence; alerted/near ⇒ tight.
                float cadence = TargetEvalInterval;
                if (_sensor.State == DeNelle.Core.AwarenessState.Unaware && IsFarFromHero())
                    cadence *= 2.5f;
                _sensorScanTimer = cadence;

                // Push IsAlert only on a state change (not per frame).
                var aware = _sensor.SharedState;
                if (aware != _lastAwareness)
                {
                    _lastAwareness = aware;
                    if (_animator != null && _hasIsAlertParam)
                        _animator.SetBool(AnimIsAlert, aware >= DeNelle.Core.AwarenessState.Alerted);
                }
            }
        }

        /// <summary>True when the hero is far (or unknown) — used for scan LOD.</summary>
        private bool IsFarFromHero()
        {
            if (_heroTransform == null) return true;
            const float NearSqr = 18f * 18f;
            return (_heroTransform.position - transform.position).sqrMagnitude > NearSqr;
        }

        // ── WO-145 (Tactic A): weighted candidate scorer ──────────────────────

        /// <summary>
        /// WO-145: picks the best offensive target by a data-driven weighted score
        /// (role-value + low-HP + threat − distance, scaled by TargetPriorityBias) so
        /// the enemy focus-fires squishy / wounded targets (the pet, a low-HP
        /// defender) rather than whatever is merely nearest. Throttled by
        /// <see cref="_targetEvalTimer"/> — re-scores on the interval and reuses the
        /// cached <see cref="_currentTarget"/> between ticks (no per-frame thrash).
        /// With no <see cref="_tactics"/> assigned it falls back to the legacy chain.
        /// </summary>
        private Transform ScoreAndPickTarget()
        {
            // No tactics SO ⇒ preserve today's EXACT behaviour (per-frame legacy chain,
            // no scoring, no throttle) so the common case has zero regression.
            if (_tactics == null)
                return FindNearbyHero() ?? FindNearestTower() ?? FindClosestTarget();

            // Throttle: reuse the cached pick between eval ticks (and drop a dead/
            // disabled cached target immediately so we don't aim at a corpse).
            _targetEvalTimer -= Time.deltaTime;
            bool cachedValid = _currentTarget != null && _currentTarget.gameObject.activeInHierarchy;
            if (_targetEvalTimer > 0f && cachedValid)
                return _currentTarget;
            _targetEvalTimer = TargetEvalInterval;

            float roleW  = _tactics.RoleValueWeight;
            float lowHpW = _tactics.LowHpWeight;
            float threatW = _tactics.ThreatWeight;
            float distW  = _tactics.DistanceWeight;
            float bias   = Mathf.Max(0.1f, _tactics.TargetPriorityBias);

            Transform best = null;
            float bestScore = float.NegativeInfinity;
            float scanR = Mathf.Max(_threatScanRadius, _towerScanRadius);

            // ── Cached known transforms: pet, hero, Heart ──
            ConsiderCandidate(_petTransform,  /*roleVal*/ 1.0f, PetHpFraction(),  /*threat*/ 0.4f,
                              scanR, roleW, lowHpW, threatW, distW, bias, ref best, ref bestScore);
            ConsiderCandidate(_heroTransform, /*roleVal*/ 0.7f, HeroHpFraction(), /*threat*/ 0.9f,
                              scanR, roleW, lowHpW, threatW, distW, bias, ref best, ref bestScore);

            // ── Overlap scan once for towers / structures (reuse _scanBuffer) ──
            int count = Physics.OverlapSphereNonAlloc(transform.position, scanR, _scanBuffer);
            for (int i = 0; i < count; i++)
            {
                if (_scanBuffer[i] == null) continue;

                var tower = _scanBuffer[i].GetComponentInParent<Tower>();
                if (tower != null && tower.IsAlive)
                {
                    ConsiderCandidate(tower.transform, 0.5f, 1f, 0.6f,
                                      scanR, roleW, lowHpW, threatW, distW, bias, ref best, ref bestScore);
                    continue;
                }

                var structure = _scanBuffer[i].GetComponentInParent<IDamageableStructure>();
                if (structure != null && structure.IsAlive)
                    ConsiderCandidate(_scanBuffer[i].transform, 0.3f, 1f, 0.3f,
                                      scanR, roleW, lowHpW, threatW, distW, bias, ref best, ref bestScore);
            }

            // Heart is the low-priority win-condition fallback.
            ConsiderCandidate(_heartTransform, 0.15f, 1f, 0.1f,
                              scanR, roleW, lowHpW, threatW, distW, bias, ref best, ref bestScore);

            // Nothing scored (e.g. all out of range) ⇒ legacy fallback chain.
            return best != null ? best : (FindNearbyHero() ?? FindNearestTower() ?? FindClosestTarget());
        }

        /// <summary>
        /// WO-145: scores one candidate and keeps it if it beats the running best.
        /// score = (roleVal·roleW + (1-hpFrac)·lowHpW + threat·threatW − normDist·distW) · bias.
        /// </summary>
        private void ConsiderCandidate(
            Transform t, float roleValue, float hpFraction, float threat,
            float scanRadius, float roleW, float lowHpW, float threatW, float distW,
            float bias, ref Transform best, ref float bestScore)
        {
            if (t == null || !t.gameObject.activeInHierarchy) return;

            float dist = (t.position - transform.position).magnitude;
            if (dist > scanRadius) return;   // out of perception — not a candidate.

            float normDist = scanRadius > 0.01f ? Mathf.Clamp01(dist / scanRadius) : 0f;
            float score = (roleValue * roleW
                         + (1f - Mathf.Clamp01(hpFraction)) * lowHpW
                         + threat * threatW
                         - normDist * distW) * bias;

            if (score > bestScore) { bestScore = score; best = t; }
        }

        /// <summary>HP fraction of the hero if resolvable (HeroHealth), else 1 (unknown = full).</summary>
        private float HeroHpFraction()
        {
            if (_heroTransform == null) return 1f;
            var hh = _heroTransform.GetComponentInParent<HeroHealth>();
            return hh != null ? hh.Fraction : 1f;
        }

        /// <summary>HP fraction of the pet — unknown without a Pets reference, so treat as full.</summary>
        private float PetHpFraction() => 1f;

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
        /// Falls back to component/tag search when role targeting returns nothing.
        /// WO-450: hero by component (HeroLocomotion); Heart still by "HeartTarget" tag.
        /// </summary>
        private Transform FindClosestTarget()
        {
            // WO-450: resolve the hero by component, not the (undeclared) "HeroTarget" tag.
            var hero = FindHeroTransform();
            if (hero != null) return hero;
            // "HeartTarget" may be undefined (FindWithTag throws) — TryFindByTag guards it.
            var heart = TryFindByTag("HeartTarget");
            return heart != null ? heart : _heartTransform;
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
