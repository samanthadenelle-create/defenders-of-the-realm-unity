// =============================================================================
// Enemy — one Hollow One marching on Elarion (Week-4 wave-loop slice).
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
using DeNelle.Core.Combat;   // IDamageableStructure — moved to Core so all assemblies can reference it

namespace DeNelle.Village
{
    /// <summary>
    /// One Hollow One in the village wave loop. Drives a <see cref="NavMeshAgent"/>
    /// toward the Heart, takes HP damage, attacks the structure in front of it on
    /// contact, and dies at zero HP. Configured by <see cref="WaveManager"/> from
    /// an <see cref="EnemyDef"/>. Instantiated per spawn; pooling is a later pass.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    // Targeting fix (open world): every Enemy auto-gets the EnemyDamageable adapter so
    // the hero's melee + ability OverlapSphere sweeps (GetComponentInParent<IDamageable>,
    // Faction.Hostile) can actually hit it on EVERY spawn path. Previously only
    // WaveManager / PatriciaLight AddComponent'd it, so the open-world RegionMobSpawner /
    // TribeManager roaming mobs had no IDamageable and could not be targeted or damaged.
    [RequireComponent(typeof(EnemyDamageable))]
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

        /// <summary>The authored per-hit contact damage (from enemies.json <c>def.ContactDamage</c>,
        /// post wave-scaling). HeroHealth's contact tick reads THIS per adjacent enemy so each
        /// enemy hits for its real stat (berserker 15 vs necromancer 18 vs walker 8) instead of a
        /// single hardcoded flat value — turns the authored contactDamage column live for hero combat.</summary>
        public float ContactDamage => _contactDamage;

        [Tooltip("Seconds between melee hits while in contact.")]
        [SerializeField] private float _attackInterval = 1.3f;

        [Tooltip("AI archetype — Walker marches straight; Charger / Skirmisher are later waves.")]
        [SerializeField] private EnemyAiKind _ai = EnemyAiKind.Walker;

        [Tooltip("Air/ground targeting matrix: when true this enemy FLIES — only anti-air " +
                 "(or 'both') towers can hit it. Set from the enemies.json 'movement' field " +
                 "in Configure; defaults false (ground) so a hand-placed enemy with no def " +
                 "is ground unless this is ticked in the prefab.")]
        [SerializeField] private bool _isFlying;

        [Header("Contact attack tuning")]
        [Tooltip("Distance ahead the enemy probes for an attackable structure (world units).")]
        [SerializeField] private float _contactProbeDistance = 1.1f;

        [Tooltip("Structure-awareness fix (ff.enemystructureaware): radius of the all-direction sweep a " +
                 "BRAIN-LESS enemy uses to acquire a nearby live structure (side tower/wall, or the Heart " +
                 "tree) it would otherwise march past — the forward-only probe missed ~99.7% of them. " +
                 "Kept short so a locked structure is within striking range. Suppressed while the hero is " +
                 "in aggro range (hero stays primary).")]
        [SerializeField, Range(1f, 8f)] private float _structureSweepRadius = 3f;

        [Tooltip("Distance from the Heart at which the enemy considers itself 'arrived'.")]
        [SerializeField] private float _heartArrivalRadius = 2.5f;

        // WO-419: the agent's stoppingDistance is normally _heartArrivalRadius (2.5 m) so a
        // siege enemy halts a body-length off the Heart/structure it batters. But the HERO
        // has NO physical collider (HeroControlEnsurer destroys it so HeroLocomotion's
        // CapsuleCast can't self-block) and is NOT struck by the enemy's forward contact
        // probe — instead HeroHealth.Update scans for enemies within its own EngageRadius
        // (1.5 m) and self-applies the contact tick. So a hero-CHASING enemy that stops at
        // 2.5 m never enters the 1.5 m damage ring and deals ZERO damage. This is invisible
        // in the castle (enemies batter the gate/Heart/walls — real colliders) but in
        // OuterWorld the hero is the ONLY target, so the seam reads as "enemies don't attack
        // after I cross into OuterWorld" (WO-419). When actively chasing the hero we tighten
        // stoppingDistance to this melee value so the enemy closes INSIDE the hero's engage
        // ring; it is restored to _heartArrivalRadius the moment the chase ends.
        private const float HeroChaseStoppingDistance = 1.1f;
        // CORE-LOOP RCA (EnemyAggro 2026-06-18): a brain-driven enemy whose Rush path to the
        // hero went PARTIAL steers to the last reachable corner (a few metres short of the
        // hero), so the override no longer sits exactly on the hero. We still treat it as a
        // hero-chase — and keep the tight stoppingDistance so it enters the 1.5 m damage ring
        // — whenever the ENEMY ITSELF is within this radius of the hero and its destination
        // points hero-ward. Generous enough to cover the partial-path standoff, tight enough
        // not to mis-fire on a far-off siege march.
        private const float HeroChaseProximity = 4f;
        private bool _stopTightenedForHero;   // tracks which stoppingDistance is currently applied

        [Header("Hero aggro (DEF-224)")]
        [Tooltip("When the hero comes within this radius the enemy breaks off its Heart-siege " +
                 "march and closes on the hero to attack it, then returns to the Heart-siege " +
                 "path once the hero leaves. 0 disables hero-aggro entirely. " +
                 "This is ADDITIVE — when an EnemyBrain is actively steering this enemy " +
                 "(role/tactical/retaliation override set) the brain wins and this stays out of " +
                 "the way, so it only governs the plain wave/roamer enemies that carry no brain.")]
        [SerializeField, Range(0f, 20f)] private float _heroAggroRadius = 7f;

        [Tooltip("Hysteresis: once aggro'd, the enemy keeps chasing the hero until the hero " +
                 "moves THIS much further than _heroAggroRadius — stops the enemy flickering " +
                 "between chase and march at the radius edge.")]
        [SerializeField, Range(0f, 6f)] private float _heroAggroDropMargin = 2.5f;

        /// <summary>
        /// Seconds the dead enemy GameObject lingers so its death animation can
        /// play before <see cref="Die"/> destroys it. Only applied when the enemy
        /// has an Animator; with none it is destroyed immediately.
        /// </summary>
        private const float DeathHoldSeconds = 3.5f;   // owner 2026-06-23: was 1.6f (cut the death anim) -> let the death cycle play through. (The dramatic ~10s camera linger on the BATTLE-WINNING kill is a separate death-cam, WO-493.)

        /// <summary>
        /// Lifetime cap for an authored per-type hit/death VFX prefab spawned via
        /// <see cref="EnemyTypeVfxSet"/>. These prefabs were previously Instantiated
        /// with NO Destroy (a hard GameObject leak per hit / per kill); they now
        /// self-destruct after this window — long enough for the burst to finish,
        /// short enough that nothing accumulates.
        /// </summary>
        private const float TypeVfxSelfDestructSeconds = 3f;

        // ── Runtime refs / state ──────────────────────────────────────────────

        private NavMeshAgent _agent;
        private Transform _heart;
        private EnemyDef _def;
        private float _attackCooldown;
        private bool _dead;
        private bool _navWarned;

        // ── Pooling (EnemyPool) ───────────────────────────────────────────────
        // The key the EnemyPool files this body under (EnemyDef model id / prefab
        // name) so Release routes it back to the queue it can be reused from. Set
        // once by the pool when the body is first built; survives reuse.
        private string _poolKey;

        /// <summary>The EnemyPool key this body is filed under (set by the pool).</summary>
        public string PoolKey => _poolKey;

        /// <summary>Stamps the pool key (called by <see cref="EnemyPool"/> on first build).</summary>
        public void SetPoolKey(string key) => _poolKey = key;
        private bool _telegraphing;   // DEF-48: true during wind-up — blocks double-trigger
        private IDamageableStructure _currentTarget;

        // ── Smooth target/attack facing (anti-snap) ──────────────────────────
        // The old target-facing did `transform.rotation = LookRotation(toTarget)`
        // ONCE per shot/attack — an instant snap that read as the Wights jerking
        // left/right. We instead record a desired (Y-flattened) facing direction
        // here and slerp the root toward it every frame in TickFacing(), exactly
        // mirroring the path-facing slerp (~691). Upright/Y-only is preserved by
        // flattening the direction before building the LookRotation, so the enemy
        // never tips. Turn rate is tunable: a Slerp factor that reads natural for
        // a ground unit pivoting to face. (Velocity path-facing still drives while
        // moving; this fills in when the agent is stopped to attack.)
        private bool _hasFaceTarget;
        private Vector3 _faceTargetDir = Vector3.forward;
        private const float FaceTurnSlerp = 9f; // deg-rate feel ~matches 691's 10f

        private ActorAnimator _actor; // guarded driver (Core) for Speed/Attack/Hit/Dead on the visual child controller

        // ── Position-delta locomotion fallback (anti-slide) ──────────────────
        // DEF-56 / formation-slot drift: during throttled SetDestination + formation
        // slot moves the NavMeshAgent's velocity reads ~0 even while the transform
        // actually slides. That left Speed=0 -> idle pose gliding. We fall back to
        // a position delta (horizontal only) when agent.velocity is near-zero, so
        // the blend tree blends to walk/run in BOTH the arena (formation) and the
        // overworld (solo brain) contexts.
        private Vector3 _lastAnimPos;
        private bool _hasLastAnimPos;

        // ── DEF-21 / DEF-72: EnemyBrain nav-target override ──────────────────
        // DEF-21: EnemyBrain.Update() calls SetBrainTarget each frame with a role-
        //   specific Transform destination. When non-null DriveNav() follows this
        //   instead of _heart. When null (default, or Role == DPS), Heart-march resumes.
        // DEF-72: EnemyBrain.Update() calls SetBrainTargetPosition each frame with
        //   a computed Vector3 destination (flank offset, retreat vector, etc.).
        //   When non-null it overrides both _brainTarget and _heart. This is the
        //   tactical-overlay path; _brainTarget is the role-only (no-tactics) path.
        private Transform _brainTarget;
        private Vector3?  _brainPositionOverride;   // DEF-72

        // ── DEF-224: hero aggro ──────────────────────────────────────────────
        // Plain wave/roamer enemies carry no EnemyBrain (the factory + wave path
        // never AddComponent one), so the brain's hero-engage / retaliation never
        // ran and the owner saw enemies "ignore the hero at point-blank". This is a
        // self-contained, brain-independent aggro: when the hero is inside
        // _heroAggroRadius the enemy steers at it (and ProbeForStructure's SphereCast
        // then hits the hero's CapsuleCollider → the existing contact-attack lands,
        // since HeroHealth implements IDamageableStructure). When the hero leaves
        // (with hysteresis) the Heart-siege march resumes unchanged.
        private Transform _heroTransform;
        private bool      _heroAggroEngaged;     // sticky once in range (hysteresis)
        private float     _heroResolveTimer;     // periodic re-resolve (hero may spawn late / respawn)

        // Structure-awareness fix (ff.enemystructureaware): reused buffer for the brain-less
        // all-direction structure sweep (OverlapSphereNonAlloc) so it never allocs per tick.
        private readonly Collider[] _structureScanBuffer = new Collider[16];

        // ── DEF-56: path throttle ─────────────────────────────────────────────
        // SetDestination is O(navmesh) — calling it every frame for 20+ enemies
        // is the main NavMesh CPU cost. Throttle to _pathRefreshInterval seconds
        // AND only when the heart has moved more than _pathMinMoveDelta world units.
        private float   _pathRefreshTimer;
        private Vector3 _lastPathedDestination;

        /// <summary>DEF-56: Minimum seconds between NavMesh path requests.</summary>
        [SerializeField, Range(0.1f, 1f)] private float _pathRefreshInterval = 0.25f;

        /// <summary>
        /// DEF-56: Minimum world-unit delta the Heart must move before a new path
        /// request fires early. Prevents redundant requests when the Heart is idle.
        /// </summary>
        [SerializeField, Range(0.1f, 2f)] private float _pathMinMoveDelta = 0.5f;

        // ── DEF-46: per-type VFX / audio + directional hit reactions ─────────

        [Header("Type VFX + Audio (DEF-46)")]
        [Tooltip("Per-archetype hit/death/attack VFX and audio. " +
                 "Leave blank to use the built-in VfxPool fallbacks.")]
        [SerializeField] private EnemyTypeVfxSet _typeVfxSet;

        // AudioSource is optional — Enemy does not require one, but needs it to
        // actually play the clip. Resolved in Awake if not set in Inspector.
        [Tooltip("AudioSource used to play hit / death / attack clips. " +
                 "Resolved in Awake from the same or child GameObjects if blank.")]
        [SerializeField] private AudioSource _audioSource;

        [Header("Death VFX Override (WO-84)")]
        [Tooltip("Override the death VFX type per prefab — leave Death_Generic to use " +
                 "the VfxPool fallback. Elite/Boss always delegate to EliteVFXController.")]
        [SerializeField] private VFXType _deathVFXOverride = VFXType.Death_Generic;

        [Header("Heavy Hit (WO-84)")]
        [Tooltip("Damage at or above this value triggers the heavy-hit path: " +
                 "larger VFX spawn + stronger camera shake.")]
        [SerializeField] private float _heavyHitThreshold = 20f;

        /// <summary>
        /// The cardinal quadrant a hit came from, relative to the enemy's facing.
        /// Drives the directional flinch sub-state in the Animator.
        /// </summary>
        private enum HitDirection { Front = 0, Left = 1, Right = 2, Back = 3 }

        // ── Animation ─────────────────────────────────────────────────────────
        // The KayKit skeleton mesh carries an Animator (the AnimatorSetup editor
        // script builds HumanoidEnemy/LargeEnemy/Boss.controller; the integrator
        // assigns one to the enemy prefab — see docs/port-notes/animation-setup.md).
        // Enemy DRIVES it: Speed float from movement, Attack/Hit triggers on the
        // contact strike + damage, Dead bool on death. All parameter sets are
        // null-guarded so an enemy with no Animator still runs its gameplay.
        private Animator _animator;

        // Combat-feel (additive): red hit-flash on each non-lethal hit. Auto-added
        // in Awake so it needs no prefab wiring; flashed from the hit branch below.
        private EnemyHitReaction _hitReaction;

        // WO-178: floating world-space HP bar. Auto-added in Awake (no prefab
        // wiring); reads HpFraction / IsDead and tears itself down on death.
        private FloatingHealthBar _healthBar;

        // Animator parameter hashes — must match AnimatorSetup.cs's parameter
        // names ("Speed" / "Attack" / "Hit" / "Dead" / "HitDir").
        private static readonly int AnimSpeed   = Animator.StringToHash("Speed");
        private static readonly int AnimAttack  = Animator.StringToHash("Attack");
        private static readonly int AnimWindUp  = Animator.StringToHash("WindUp");  // DEF-48 telegraph
        private static readonly int AnimHit     = Animator.StringToHash("Hit");
        private static readonly int AnimDead    = Animator.StringToHash("Dead");

        /// <summary>
        /// DEF-46: int parameter (0=Front, 1=Left, 2=Right, 3=Back) set BEFORE
        /// the Hit trigger fires so the sub-state can blend to the right flinch.
        /// </summary>
        private static readonly int AnimHitDir = Animator.StringToHash("HitDir");

        // WO-163: cached once when the Animator resolves — whether THIS enemy's
        // controller actually declares each param. Driving an absent param logs an
        // error EVERY frame (the 3,351-error spam). Guard every SetFloat/SetBool/
        // SetTrigger/SetInteger with these. A controller with no
        // runtimeAnimatorController has no parameters → all stay false.
        private bool _hasSpeedParam;
        private bool _hasAttackParam;
        private bool _hasWindUpParam;
        private bool _hasHitParam;
        private bool _hasDeadParam;
        private bool _hasHitDirParam;

        /// <summary>Raised when this enemy's HP reaches zero. Arg = this enemy.</summary>
        public event Action<Enemy> Died;

        /// <summary>
        /// Raised when this enemy reaches the Heart without being killed. The
        /// WaveManager listens to escalate the Heart's threat state.
        /// </summary>
        public event Action<Enemy> ReachedHeart;

        /// <summary>
        /// Raised when this enemy takes (non-lethal) damage. Arg = world-space
        /// position of the damage source. EnemyBrain listens to RETALIATE — a
        /// struck enemy turns on its attacker (the hero/pet) instead of marching
        /// past to the Heart (owner 2026-06-02: "they just walked on past me").
        /// </summary>
        public event Action<Vector3> Damaged;

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

        /// <summary>Alive and a valid target — used by <see cref="TargetManager"/>'s
        /// registry queries (the reticle / towers).</summary>
        public bool IsAlive => !_dead && _hp > 0f;

        /// <summary>AI archetype this enemy runs.</summary>
        public EnemyAiKind Ai => _ai;

        /// <summary>
        /// True when this enemy flies — the air/ground targeting matrix gate.
        /// Anti-ground towers skip a flyer; anti-air (or "both") towers can hit it.
        /// Set from the enemies.json "movement" field in <see cref="Configure"/>;
        /// false (ground) by default. Read by <c>EnemyDamageable</c> (ICombatLayered)
        /// so a tower's acquisition loop can gate on it via the Core seam.
        /// </summary>
        public bool IsFlying => _isFlying;

        /// <summary>The air/ground <see cref="DeNelle.Core.Combat.CombatLayer"/> this enemy occupies.</summary>
        public DeNelle.Core.Combat.CombatLayer CombatLayer =>
            _isFlying ? DeNelle.Core.Combat.CombatLayer.Flying
                      : DeNelle.Core.Combat.CombatLayer.Ground;

        // ── DEF-21: EnemyBrain integration ────────────────────────────────────

        /// <summary>
        /// DEF-21: Override the nav destination used by <see cref="DriveNav"/> with
        /// a scene Transform (role-based path, no TacticalData). Pass null to revert
        /// to the Heart-march (the default and the DPS path).
        /// </summary>
        public void SetBrainTarget(Transform target) => _brainTarget = target;

        /// <summary>
        /// DEF-72: Override the nav destination with an explicit world-space
        /// <see cref="Vector3"/> computed by <see cref="EnemyBrain"/>'s tactical
        /// overlay (flank arc, retreat vector, etc.). Supersedes both
        /// <see cref="_brainTarget"/> and <see cref="_heart"/> when non-null.
        /// Pass null to clear the override and revert to the role/Heart-march path.
        /// </summary>
        public void SetBrainTargetPosition(Vector3? pos) => _brainPositionOverride = pos;

        /// <summary>
        /// DEF-21: Restore HP by <paramref name="amount"/> up to <see cref="MaxHp"/>.
        /// Called by <see cref="EnemyBrain"/> (Healer role) on adjacent wounded allies.
        /// </summary>
        public void Heal(float amount)
        {
            if (_dead || amount <= 0f) return;
            _hp = Mathf.Min(_maxHp, _hp + amount);
        }

        /// <summary>
        /// The engine def id the breach trigger maps this enemy to when handing
        /// the ATB scene a battle. Maps village enemies.json ids to the ATB engine's
        /// <see cref="DeNelle.BattleATB.Engine.Defs.ENEMY_DEFS"/> keys (WO-94).
        /// <list type="bullet">
        ///   <item>hollow-warrior  → "bruiser"     (high-HP tank archetype)</item>
        ///   <item>hollow-rogue    → "skeleton"     (fast skirmisher — closest grunt)</item>
        ///   <item>hollow-walker   → "skeleton"     (basic grunt)</item>
        ///   <item>necromancer     → "necromancer"  (exact match)</item>
        ///   <item>anything else   → "skeleton"     (safe fallback)</item>
        /// </list>
        /// </summary>
        public string EngineDefId
        {
            get
            {
                switch (_enemyDefId)
                {
                    case "necromancer":   return "necromancer";
                    case "hollow-warrior": return "bruiser";
                    // hollow-walker and hollow-rogue both map to the standard grunt.
                    default:              return "skeleton";
                }
            }
        }

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
                // Owner 2026-06-02: global -5% enemy speed — early-game generosity so new
                // players get the "winning while I learn" feel as they scale up + learn the
                // movement. One central dial; every enemy (roamer/wave/tribe) routes through
                // Configure, so all of them slow together.
                _moveSpeed = Mathf.Max(0.1f, def.MoveSpeed) * 0.95f;
                _contactDamage = Mathf.Max(0f, def.ContactDamage);
                _attackInterval = Mathf.Max(0.1f, def.AttackInterval);
                _ai = def.AiKind;
                // Air/ground targeting matrix — flyers can only be hit by anti-air
                // (or "both") towers. Sourced from the enemies.json "movement" field.
                _isFlying = def.IsFlying;
                // WO-397: honour the per-def aggro radius. EnemyDef.AggroRadius was
                // authored on every roster (enemies.json + the code-built outpost /
                // camp defs: 14 m guards, 16 m boss) but was NEVER applied — the field
                // silently fell back to the 7 m inspector default, so a guard with an
                // intended 14 m aggro only woke at 7 m. Map it here (clamped to the
                // field's 0–20 inspector range) so the data dial actually governs how
                // far an enemy detects the hero. A def value <= 0 keeps the prefab/
                // inspector default (legacy-safe: a hand-placed enemy with no def, or a
                // def that deliberately opts out of hero aggro, is untouched).
                if (def.AggroRadius > 0f)
                    _heroAggroRadius = Mathf.Clamp(def.AggroRadius, 0f, 20f);
            }

            // DPS-MAGE (owner 2026-06-13): caster-role enemies (orc-shaman, hollow-acolyte,
            // orc-necromancer, future overboss) should HOLD DISTANCE and fire ranged, not charge.
            // Spawned enemies carry no inspector TacticalData, so everyone defaulted to Rush (even
            // casters). Assign the shared runtime Kiter archetype → EnemyBrain enters the Kite
            // state (standoff band ~10 m, back off at 6 m) and fires Enemy.RangedAttack on
            // cooldown. The spawner wires the EnemyBrain before Configure, so it's present.
            if (def != null && def.Role == "caster")
            {
                var brain = GetComponent<EnemyBrain>();
                if (brain != null) brain.SetTactics(EnemyBrain.KiterTactics);
            }

            EnsureAgent();
            if (_agent != null)
            {
                _agent.speed            = _moveSpeed;
                _agent.stoppingDistance = _heartArrivalRadius;
                // DEF-56: disable Unity's automatic re-path so Enemy.DriveNav()'s
                // throttle is the sole trigger for SetDestination calls. Without
                // this the agent silently re-paths on NavMesh topology changes,
                // partially defeating the ~80% CPU saving the throttle provides.
                _agent.autoRepath = false;
                // DEF-56: randomise avoidance priority so swarm enemies don't all
                // push the same direction when crowding. Range 50–79 leaves 0–49
                // free for bosses / high-priority agents.
                _agent.avoidancePriority = UnityEngine.Random.Range(50, 80);
            }

            _dead = false;
            _attackCooldown = 0f;
            _navWarned = false;

            // Animation + turning fixes for core TD battle characters.
            // Attach guarded driver (so Enemy.cs can call SetLocomotion/PlayAttack/Die
            // and the HeroAnimatorFactory-style or Enemy shared controllers play).
            if (!TryGetComponent(out ActorAnimator actor)) actor = gameObject.AddComponent<ActorAnimator>();
            _actor = actor;

            if (_agent != null)
            {
                _agent.updateRotation = false; // we control facing (to target on attack, or path dir)
            }

            // DEF-56: reset path throttle and stagger the initial SetDestination
            // call through NavPathCoordinator so a 20-enemy spawn doesn't spike
            // NavMesh pathing in a single frame.
            _pathRefreshTimer      = 0f;
            _lastPathedDestination = Vector3.zero;
            if (_agent != null && _heart != null)
                NavPathCoordinator.RequestInitialPath(_agent, _heart.position);

            // EnemyAggro observability (no-brain hypothesis): record at init whether this
            // enemy carries an EnemyBrain (ranged tower/structure awareness) or is a plain
            // wave/roamer relying ONLY on the narrow forward contact probe. The spawner wires
            // the brain before Configure, so GetComponent here is authoritative.
            var aggroBrain = GetComponent<EnemyBrain>();
            DeNelle.Core.Diagnostics.FlowTrace.Once("EnemyAggro", $"brain-{_enemyId}",
                $"{_enemyId}: init hasEnemyBrain={(aggroBrain != null)} " +
                $"role={(def != null ? def.Role : "<no-def>")} ai={_ai} " +
                $"(no brain => structure awareness = forward ProbeForStructure only)");
        }

        // ---------------------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------------------

        /// <summary>
        /// Applies wave-scaling multipliers after <see cref="Configure"/>. Called by
        /// <see cref="WaveManager"/> immediately after Configure when a
        /// <see cref="WaveScalingCurve"/> is assigned. Multipliers of 1 are no-ops.
        /// </summary>
        public void ApplyWaveScaling(float hpMult, float speedMult, float damageMult)
        {
            if (hpMult > 1f)
            {
                _maxHp = Mathf.Max(1f, _maxHp * hpMult);
                _hp    = _maxHp;
            }
            if (speedMult != 1f)
            {
                _moveSpeed = Mathf.Max(0.1f, _moveSpeed * speedMult);
                if (_agent != null && _agent.isOnNavMesh)
                    _agent.speed = _moveSpeed;
                else if (_agent != null)
                    _agent.speed = _moveSpeed;
            }
            if (damageMult > 1f)
            {
                _contactDamage = Mathf.Max(0f, _contactDamage * damageMult);
            }
        }

        private void Awake()
        {
            EnsureAgent();
            EnsureAnimator();
            EnsureAudio();
            EnsureHitReaction();
            EnsureHealthBar();
        }

        // Registry membership (TargetManager): every enemy — wave, roamer, tribe,
        // ward — auto-joins on enable and leaves on disable, so the reticle/towers
        // query a clean live list instead of an overflow-prone physics sweep. Covers
        // pooling too (re-enable re-registers; Register dedups).
        private void OnEnable()  => TargetManager.Register(this);
        private void OnDisable() => TargetManager.Unregister(this);

        private void EnsureHitReaction()
        {
            // Auto-attach so every enemy gets the hit-flash with zero prefab wiring.
            _hitReaction = GetComponent<EnemyHitReaction>();
            if (_hitReaction == null) _hitReaction = gameObject.AddComponent<EnemyHitReaction>();
        }

        /// <summary>
        /// WO-178: auto-attach the floating world-space HP bar (zero prefab wiring).
        /// Height is taken from the rendered mesh bounds so the bar clears the
        /// model regardless of import scale; it reads HpFraction / IsDead and hides
        /// itself until the enemy first takes damage.
        /// </summary>
        private void EnsureHealthBar()
        {
            float headOffset = 2.4f;
            Renderer rend = GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                // Local-space height of the mesh top above this transform, plus a gap.
                float worldTop = rend.bounds.max.y - transform.position.y;
                if (worldTop > 0.1f) headOffset = worldTop + 0.4f;
            }
            // DEF-206: hideAtFull:true — enemies start with NO bar (the "raw green
            // bar on everything" noise the owner flagged). The bar reveals only once
            // the enemy is ENGAGED: it takes damage, OR HeroTargetIndicator flags it
            // as the player's current/locked target (FloatingHealthBar.SetTargeted),
            // then fades out a few seconds after combat ends. Full-HP idle mobs in
            // the distance now read clean.
            _healthBar = FloatingHealthBar.Attach(
                gameObject,
                fraction: () => HpFraction,
                isDead:   () => _dead,
                heightOffset: headOffset,
                hideAtFull: true);
        }

        private void EnsureAudio()
        {
            if (_audioSource == null)
                _audioSource = GetComponentInChildren<AudioSource>();
            // Don't add one automatically — AudioSource requires spatial settings
            // the integrator should configure (3D falloff, volume rolloff, etc.).
            // If null, PlayTypeSound is a no-op and the enemy runs silently until
            // an AudioSource is added to the prefab.
        }

        /// <summary>The kind of combat beat a fallback SFX covers (WO-220).</summary>
        private enum CombatSfxFallback { None, Hit, Death }

        /// <summary>
        /// Plays a clip via the enemy's AudioSource (one-shot, non-interrupting).
        /// No-op when either the source or the clip is null.
        ///
        /// WO-220: when the type-set provided NO clip (clip == null) and a
        /// <paramref name="fallback"/> kind is given, play a generated fallback SFX
        /// through the EXISTING audio surface (CoreServices.Audio) so the enemy is
        /// never silent on a hit / death — even before any EnemyTypeVfxSet clips are
        /// authored. The type-set clip is always preferred when present.
        /// </summary>
        private void PlayTypeSound(AudioClip clip, CombatSfxFallback fallback = CombatSfxFallback.None)
        {
            if (clip != null)
            {
                if (_audioSource != null) _audioSource.PlayOneShot(clip);
                return;
            }

            // No authored clip — fill the gap via the central audio service.
            switch (fallback)
            {
                case CombatSfxFallback.Hit:   EnemyCombatAudio.PlayHit();   break;
                case CombatSfxFallback.Death: EnemyCombatAudio.PlayDeath(); break;
            }
        }

        private void Update()
        {
            if (_dead) return;

            TickContactAttack();
            DriveNav();
            TickFacing();
            DriveAnimator();
        }

        // ---------------------------------------------------------------------
        // Facing — smooth pivot toward a requested target direction (anti-snap)
        // ---------------------------------------------------------------------

        /// <summary>
        /// Records a desired facing direction for the smooth pivot. The vector is
        /// Y-flattened so the enemy turns upright only (never tips). Replaces the
        /// old instant <c>transform.rotation = LookRotation(...)</c> snap used by
        /// the target/attack facing — call this instead; the actual rotation is
        /// integrated frame-by-frame in <see cref="TickFacing"/>.
        /// </summary>
        private void RequestFacing(Vector3 worldDir)
        {
            worldDir.y = 0f;
            if (worldDir.sqrMagnitude <= 0.0001f) return;
            _faceTargetDir = worldDir.normalized;
            _hasFaceTarget = true;
        }

        /// <summary>
        /// Slerps the root toward the requested facing direction each frame, at a
        /// natural ground-unit turn rate. Only drives when the agent is effectively
        /// stopped — while moving, the velocity path-facing (DriveNav, ~707) owns
        /// rotation and following travel is correct. Y-only/upright is guaranteed
        /// because <see cref="RequestFacing"/> flattens the direction. Observable via
        /// a throttled FlowTrace (~1/sec).
        /// </summary>
        private void TickFacing()
        {
            if (!_hasFaceTarget || _dead) return;

            // While the agent is genuinely moving, velocity-facing drives — defer.
            if (_agent != null && _agent.isOnNavMesh)
            {
                Vector3 v = _agent.velocity; v.y = 0f;
                if (v.sqrMagnitude > 0.1f * 0.1f) return;
            }

            Quaternion face = Quaternion.LookRotation(_faceTargetDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, face, FaceTurnSlerp * Time.deltaTime);

            float deg = Quaternion.Angle(transform.rotation, face);
            DeNelle.Core.Diagnostics.FlowTrace.Throttle(
                "EnemyFacing", $"turn-{_enemyId}", 1f,
                $"smooth-pivot id={_enemyId} remaining={deg:0.0}deg rate={FaceTurnSlerp}");

            // Settled — stop re-slerping a tiny residual (avoids endless micro-turn).
            if (deg < 0.5f) _hasFaceTarget = false;
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
            if (_animator == null || !_hasSpeedParam) return;
            float speed = (_agent != null && _agent.isOnNavMesh)
                ? _agent.velocity.magnitude
                : 0f;

            // Anti-slide fallback: NavMeshAgent velocity reads ~0 during throttled
            // SetDestination (DEF-56) + formation-slot drift, so estimate speed from
            // the horizontal transform delta when velocity is near-zero but we're
            // actually moving. Y is zeroed so a vertical settle never reads as motion.
            // Clamp to _moveSpeed so a teleport/warp (e.g. NavMesh respawn) can't spike
            // the blend into a sprint. Covers arena (formation) AND overworld (solo brain).
            if (speed < 0.05f && _hasLastAnimPos && Time.deltaTime > 0f)
            {
                Vector3 cur = transform.position;
                Vector3 d = cur - _lastAnimPos;
                d.y = 0f;
                float est = d.magnitude / Time.deltaTime;
                speed = Mathf.Min(est, Mathf.Max(0.1f, _moveSpeed));
            }
            _lastAnimPos = transform.position;
            _hasLastAnimPos = true;

            // Drive the (new) ActorAnimator for locomotion (idle/walk/run blendtree in the
            // shared enemy controllers). Also keep the legacy direct _animator.SetFloat
            // for any old listeners. Guarded inside ActorAnimator.
            _actor?.SetLocomotion(speed);
            _animator.SetFloat(AnimSpeed, speed);

            // WO-491: wounded stance below the low-HP cutoff — the orc reads "hurt" (limp /
            // stagger locomotion sub-tree). Flag-gated + guarded inside ActorAnimator
            // (no-op on controllers without an Injured param). Drives every frame; the
            // Animator only transitions on a change of the bool.
            if (DeNelle.Core.FeatureFlags.EnemyInjuredStance)
                _actor?.SetInjured(HpFraction < 0.3f);

            // Battle idle / ready when stopped with target (for family enemies in combat).
            // Combined with speed drive gives Idle (0 no target), Movement, Engagement/BattleReady.
            // Hit/Death/Attack driven from damage/contact (PlayHit/PlayAttack/Die on _actor
            // in TickContactAttack / OnDamage hooks if wired; see EnemyHitReaction).
            if (speed < 0.1f && _currentTarget != null)
                _actor?.SetCombatStance(true);
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
            if (_agent == null) return;

            // HEARTLESS HOOK (overworld encounter rep) — DATA-PROVEN root of "no chase" 2026-06-23:
            // a rep has NO Heart (Configure(...,null)), so the old `_heart == null` bail returned
            // IMMEDIATELY and the agent was NEVER driven -> the rep stood still (no roam, no chase)
            // despite RepEngageWatcher setting _brainPositionOverride every frame. Drive a heartless
            // agent straight to its override (roam point while idle, hero while aggro'd) and RETURN,
            // never touching the Heart-based logic below (zero risk to normal Heart-driven enemies).
            if (_heart == null)
            {
                if (_agent.isOnNavMesh && _brainPositionOverride.HasValue)
                {
                    if (_agent.isStopped) _agent.isStopped = false;
                    Vector3 hv = _agent.velocity; hv.y = 0f;
                    if (hv.sqrMagnitude > 0.01f)
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(hv.normalized), 10f * Time.deltaTime);
                    _agent.SetDestination(_brainPositionOverride.Value);
                }
                else if (_agent.isOnNavMesh && !_agent.isStopped) _agent.isStopped = true;
                return;
            }

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

            // WO-315: face the travel direction. _agent.updateRotation is OFF (so
            // RangedAttack / contact facing can override), but with no driver the
            // enemy kept its spawn orientation and "walked backwards". We are here
            // only when NOT locked to a contact target (that path returns above), so
            // slerp the root toward flattened agent velocity. Guard near-zero velocity
            // so a stopped/arriving enemy doesn't jitter. Mirrors HeroLocomotion's
            // root-facing (LookRotation on velocity, no extra Euler — the visual child's
            // rig-forward correction is applied at skin time in EnemyFactory).
            Vector3 vel = _agent.velocity; vel.y = 0f;
            if (vel.sqrMagnitude > 0.1f * 0.1f)
            {
                Quaternion face = Quaternion.LookRotation(vel.normalized);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, face, 10f * Time.deltaTime);
            }

            // DEF-72 / DEF-21 / DEF-224 / WO-397: resolve the nav destination in
            // priority order:
            //   1. _brainPositionOverride  — tactical Vector3 (flank, retreat, etc.)
            //                                A LIVE EnemyBrain drives this EVERY frame
            //                                (Rush returns target.position), so a
            //                                brain-steered enemy is decided here and
            //                                never reaches the steps below.
            //   2. hero aggro              — DEF-224: chase the hero when it is in
            //                                range. WO-397: moved ABOVE the static
            //                                _brainTarget tether. Brain-less enemies
            //                                that carry only a STATIC tether transform
            //                                (EnemyOutpost garrison guards tethered to
            //                                their stand-ring anchor) previously stood
            //                                idle on the anchor at point-blank because
            //                                the anchor (step 3) shadowed hero aggro —
            //                                the "brute idle at melee range" P1. Hero
            //                                aggro now wins over a static tether, so a
            //                                tethered guard breaks off to fight the hero
            //                                when she closes, then (hysteresis) returns
            //                                to its tether when she leaves. A live brain
            //                                is unaffected (it decided at step 1).
            //   3. _brainTarget Transform  — role/tether target (Tank/Healer/outpost
            //                                anchor / roam anchor) when the hero is out
            //                                of aggro range.
            //   4. _heart                  — default Heart-march.
            Vector3 destPos;
            bool chasingHero = false;
            if (_brainPositionOverride.HasValue)
            {
                destPos = _brainPositionOverride.Value;
                // A LIVE EnemyBrain steers here (provoke/taunt/role-on-hero). Detect a
                // hero chase so we still close to melee range and keep the TIGHT
                // stoppingDistance (HeroChaseStoppingDistance) — otherwise the agent
                // parks at the 2.5 m siege radius, a metre OUTSIDE HeroHealth's 1.5 m
                // contact ring, and never lands a hit (the "enemies won't engage" RCA,
                // EnemyAggro 2026-06-18). ResolveHeroTransform is throttled (≈1/sec).
                ResolveHeroTransform();
                if (_heroTransform != null)
                {
                    // (a) the override sits ON the hero (brain Rush — complete path), OR
                    // (b) the ENEMY itself is near the hero AND the override points
                    //     hero-ward (brain Rush whose path went PARTIAL now steers to the
                    //     last reachable corner short of the hero — EnemyBrain.TryGetPartial-
                    //     Approach). Either way we are converging on the hero and must hold
                    //     the melee stopping distance to enter the damage ring.
                    Vector3 heroPlanar = Vector3.ProjectOnPlane(
                        _heroTransform.position - destPos, Vector3.up);
                    bool overrideOnHero = heroPlanar.sqrMagnitude <= 1.5f * 1.5f;

                    Vector3 selfToHero = Vector3.ProjectOnPlane(
                        _heroTransform.position - transform.position, Vector3.up);
                    Vector3 selfToDest = Vector3.ProjectOnPlane(
                        destPos - transform.position, Vector3.up);
                    bool nearHero = selfToHero.sqrMagnitude <= HeroChaseProximity * HeroChaseProximity;
                    bool destHeroward =
                        selfToHero.sqrMagnitude < 0.01f ||
                        selfToDest.sqrMagnitude < 0.01f ||
                        Vector3.Dot(selfToHero.normalized, selfToDest.normalized) > 0.5f;

                    chasingHero = overrideOnHero || (nearHero && destHeroward);
                }
            }
            else if (TryGetHeroAggroDestination(out Vector3 heroDest))
            {
                destPos = heroDest;
                chasingHero = true;
            }
            else if (_brainTarget != null && _brainTarget.gameObject.activeInHierarchy)
            {
                destPos = _brainTarget.position;
            }
            else
            {
                destPos = _heart.position;
            }

            // WO-419: tighten stoppingDistance to a melee value while chasing the hero so the
            // agent closes INSIDE HeroHealth's 1.5 m engage ring (the hero has no collider /
            // is not hit by the forward contact probe — see HeroChaseStoppingDistance). Restore
            // the siege arrival radius the moment the chase ends. Only writes on a change.
            if (chasingHero != _stopTightenedForHero)
            {
                _stopTightenedForHero = chasingHero;
                _agent.stoppingDistance = chasingHero ? HeroChaseStoppingDistance : _heartArrivalRadius;
                // Force the next frame to re-issue the path so the agent acts on the new
                // stoppingDistance immediately (an enemy already halted at 2.5 m must resume
                // closing to 1.1 m even though the destination hasn't moved).
                _pathRefreshTimer = 0f;
                DeNelle.Core.Diagnostics.FlowTrace.Throttle("EnemyAggro", $"stop-{_enemyId}", 2f,
                    $"{_enemyId}: hero-chase={chasingHero} -> stoppingDistance={_agent.stoppingDistance:F2} " +
                    $"(scene='{gameObject.scene.name}')");
            }

            // WO-419: per-enemy closest-approach trace while chasing — shows in a headless run
            // whether a hero-aggro'd enemy actually reaches the hero's 1.5 m damage ring (where
            // HeroHealth.Update applies the contact tick) or stalls short. Throttled ~1/sec.
            if (chasingHero && _heroTransform != null)
            {
                float planar = Vector3.ProjectOnPlane(
                    _heroTransform.position - transform.position, Vector3.up).magnitude;
                DeNelle.Core.Diagnostics.FlowTrace.Throttle("EnemyAggro", $"reach-{_enemyId}", 1f,
                    $"{_enemyId}: chasing hero, planarDist={planar:F2}m (engageRing=1.50m, " +
                    $"inRange={(planar <= 1.5f)})");
            }

            // DEF-56: throttle SetDestination — only re-path when the timer expires
            // OR the destination has moved significantly. This cuts NavMesh CPU by
            // ~80% on a 20-enemy wave without visible path quality regression.
            _pathRefreshTimer -= Time.deltaTime;
            float distMoved = (destPos - _lastPathedDestination).sqrMagnitude;
            bool destMoved = distMoved > _pathMinMoveDelta * _pathMinMoveDelta;
            if (_pathRefreshTimer <= 0f || destMoved)
            {
                _pathRefreshTimer = _pathRefreshInterval;
                _lastPathedDestination = destPos;
                _agent.SetDestination(destPos);
            }

            // Arrived at the Heart without being repelled — report the breach.
            float planarDist = Vector3.ProjectOnPlane(
                _heart.position - transform.position, Vector3.up).magnitude;
            if (planarDist <= _heartArrivalRadius)
            {
                ReachedHeart?.Invoke(this);
            }
        }

        // ---------------------------------------------------------------------
        // DEF-224: hero aggro — break off the Heart-siege to attack a nearby hero
        // ---------------------------------------------------------------------

        /// <summary>
        /// Returns true (and the hero's world position) when this enemy should be
        /// chasing the hero this frame: the hero exists, is alive, and is within
        /// <see cref="_heroAggroRadius"/> (sticky out to + <see cref="_heroAggroDropMargin"/>
        /// once engaged, so the enemy doesn't flicker at the edge). Returns false —
        /// and clears the engaged latch — when aggro is disabled (radius 0) or the
        /// hero is out of range / gone, so the caller falls through to the
        /// Heart-siege march. Brain-driven enemies never reach here (DriveNav gives
        /// the brain override priority), so this is purely additive.
        /// </summary>
        private bool TryGetHeroAggroDestination(out Vector3 heroPos)
        {
            heroPos = default;
            if (_heroAggroRadius <= 0f) { _heroAggroEngaged = false; return false; }

            ResolveHeroTransform();
            if (_heroTransform == null) { _heroAggroEngaged = false; return false; }

            // The hero may have died (HeroHealth.IsAlive false) — don't chase a
            // downed/invulnerable hero; resume the Heart-siege so the wave keeps
            // pressuring the win condition.
            var heroHealth = _heroTransform.GetComponentInParent<HeroHealth>();
            if (heroHealth != null && !heroHealth.IsAlive) { _heroAggroEngaged = false; return false; }

            float planarSqr = Vector3.ProjectOnPlane(
                _heroTransform.position - transform.position, Vector3.up).sqrMagnitude;

            // Hysteresis: enter at _heroAggroRadius, leave only past the drop margin.
            float engageR = _heroAggroRadius;
            float dropR   = _heroAggroRadius + _heroAggroDropMargin;
            float threshold = _heroAggroEngaged ? dropR : engageR;
            if (planarSqr > threshold * threshold) { _heroAggroEngaged = false; return false; }

            _heroAggroEngaged = true;
            heroPos = _heroTransform.position;
            return true;
        }

        /// <summary>
        /// Lazily resolves (and periodically refreshes) the hero transform.
        /// WO-450: resolves by COMPONENT (HeroLocomotion — the one component every hero
        /// variant carries) rather than the (undeclared) "HeroTarget" tag; falls back to
        /// the built-in "Player" tag the village hero now carries. The result is re-checked
        /// on an interval so an enemy that spawned before the hero (or after a hero respawn)
        /// still acquires it.
        /// </summary>
        private void ResolveHeroTransform()
        {
            _heroResolveTimer -= Time.deltaTime;
            bool valid = _heroTransform != null && _heroTransform.gameObject.activeInHierarchy;
            if (valid && _heroResolveTimer > 0f) return;

            _heroResolveTimer = 1f;   // cheap: at most once/sec per enemy
            if (valid) return;

            var loco = FindFirstObjectByType<HeroLocomotion>();   // WO-450: component lookup
            _heroTransform = loco != null ? loco.transform : SafeFindByTag("Player");

            // WO-419: trace the acquire across the seam — confirms a brain-less OuterWorld
            // guard finds the (additively-loaded) hero by component, not an empty tag scan.
            if (_heroTransform == null)
                DeNelle.Core.Diagnostics.FlowTrace.Throttle("EnemyAggro", $"acq-miss-{_enemyId}", 2f,
                    $"{_enemyId}: hero acquire MISS (no HeroLocomotion / 'Player' tag in any loaded scene).");
            else
                DeNelle.Core.Diagnostics.FlowTrace.Once("EnemyAggro", $"acq-{_enemyId}",
                    $"{_enemyId}: acquired hero '{_heroTransform.name}' in scene " +
                    $"'{_heroTransform.gameObject.scene.name}'.");
        }

        /// <summary>Null-safe tag lookup tolerating an undefined tag (Unity throws otherwise).</summary>
        private static Transform SafeFindByTag(string tag)
        {
            try
            {
                var go = GameObject.FindWithTag(tag);
                return go != null ? go.transform : null;
            }
            catch (UnityEngine.UnityException)
            {
                return null;
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

            // DEF-224: drop a still-alive target that has MOVED out of reach (the
            // hero runs away). Without this the enemy would stay frozen in place
            // attacking nothing because the lock only released on the target's
            // death. Static structures never move past this radius, so they keep
            // their lock; only a fleeing hero/mobile target is released — at which
            // point the agent resumes chasing (hero aggro) or marching (Heart).
            if (_currentTarget != null)
            {
                var heldMb = _currentTarget as MonoBehaviour;
                if (heldMb != null)
                {
                    float dropSqr = (_contactProbeDistance + 1.5f) * (_contactProbeDistance + 1.5f);
                    if ((heldMb.transform.position - transform.position).sqrMagnitude > dropSqr)
                        _currentTarget = null;
                }
            }

            if (_currentTarget == null)
            {
                _currentTarget = ProbeForStructure();

                // EnemyAggro observability: trace BOTH outcomes of structure acquisition so a
                // headless run shows whether the forward-only SphereCast ever finds defenses /
                // the Heart, or returns null (off-axis miss) leaving the enemy on a Heart-march.
                if (_currentTarget == null)
                    DeNelle.Core.Diagnostics.FlowTrace.Throttle("EnemyAggro", $"probe-fail-{_enemyId}", 1f,
                        $"{_enemyId}: ProbeForStructure null -> no structure target (Heart-march / roam only)");
                else
                    DeNelle.Core.Diagnostics.FlowTrace.Step("EnemyAggro",
                        $"{_enemyId}: ProbeForStructure hit '{(_currentTarget as MonoBehaviour)?.name}' -> stopping agent to attack");
            }

            if (_currentTarget == null)
            {
                _attackCooldown = 0f;
                return;
            }

            _attackCooldown -= Time.deltaTime;
            if (_attackCooldown <= 0f && !_telegraphing)
            {
                _attackCooldown = _attackInterval;

                // DEF-48 / WO-560: ALWAYS telegraph the contact strike. Arena orcs are
                // built by EnemyFactory with a synthesized EnemyDef and never receive a
                // _typeVfxSet, so the legacy "telegraphDuration==0 -> instant hit" path
                // gave the V1 fight NO readable wind-up (owner F8: enemies hit with no
                // tell). We now floor the wind-up at ContactTelegraphFloor (>=1.0s) so the
                // melee read is reactable even with no SO configured, and the ground-ring
                // warning is drawn unconditionally in TelegraphThenAttack.
                float telegraphDuration = _typeVfxSet != null && _typeVfxSet.TelegraphDuration > 0f
                    ? _typeVfxSet.TelegraphDuration : ContactTelegraphFloor;
                telegraphDuration = Mathf.Max(telegraphDuration, ContactTelegraphFloor);
                StartCoroutine(TelegraphThenAttack(telegraphDuration));
            }
        }

        // ── DEF-48 / WO-560: telegraph → attack ───────────────────────────────

        /// <summary>
        /// WO-560: minimum readable wind-up for a contact (melee) strike. Floors the
        /// telegraph so arena orcs (no _typeVfxSet) still show a reactable tell. Matches
        /// the rooted-cast floor (RootedCast uses 1.0s) so melee and cast read alike.
        /// </summary>
        private const float ContactTelegraphFloor = 1.0f;

        /// <summary>
        /// DEF-48 / WO-560: Plays the wind-up animation + a ground-ring danger tell at
        /// the target's feet, then deals damage after <paramref name="duration"/> seconds.
        /// The ground ring is drawn UNCONDITIONALLY (procedural <see cref="VFXManager"/>
        /// shockwave ring) so the warning shows even when no <see cref="EnemyTypeVfxSet"/>
        /// is assigned (arena path). An authored TelegraphVFXPrefab, when present, is
        /// spawned in addition. Guards against double-trigger via <see cref="_telegraphing"/>.
        /// </summary>
        private System.Collections.IEnumerator TelegraphThenAttack(float duration)
        {
            _telegraphing = true;

            // Wind-up animation trigger — Animator must have a "WindUp" state.
            if (_animator != null && _hasWindUpParam) _animator.SetTrigger(AnimWindUp);

            // WO-560: ALWAYS draw a ground-ring danger tell at the target's feet so the
            // player can read + react to the incoming melee strike. Uses the procedural
            // Impact_ShockwaveRing fallback (committed asset / AbilityVfxKit) — no pack
            // prefab, no SO required. FlowTrace each fire so the telegraph is observable
            // in a headless run (acceptance: telegraph window > 0).
            var targetMb = _currentTarget as MonoBehaviour;
            if (targetMb != null)
            {
                Vector3 feet = targetMb.transform.position;
                VFXManager.Play(VFXType.Impact_ShockwaveRing, feet,
                    Quaternion.identity, playSound: false);
                DeNelle.Core.Diagnostics.FlowTrace.Step("VFXTelegraph",
                    $"{_enemyId}: MELEE telegraph fired (dur={duration:F2}s) ground-ring @ {feet} target='{targetMb.name}'");
            }

            // Authored ground-ring warning VFX at the target's position (in addition).
            GameObject telegraphVFX = null;
            if (_typeVfxSet != null && _typeVfxSet.TelegraphVFXPrefab != null
                && targetMb != null)
            {
                telegraphVFX = Instantiate(
                    _typeVfxSet.TelegraphVFXPrefab,
                    targetMb.transform.position,
                    Quaternion.identity);
            }

            yield return new WaitForSeconds(duration);

            // Clean up the VFX regardless of whether the attack lands.
            if (telegraphVFX != null) Destroy(telegraphVFX);

            // Re-check viability after the delay — target may have died or moved.
            if (!_dead && _currentTarget != null && _currentTarget.IsAlive)
                ExecuteContactAttack();

            _telegraphing = false;
        }

        /// <summary>
        /// DEF-48: The actual damage + animation + audio tick, extracted from
        /// <see cref="TickContactAttack"/> so both the instant and telegraph paths
        /// share one call site.
        /// </summary>
        private void ExecuteContactAttack()
        {
            if (_currentTarget == null || !_currentTarget.IsAlive) return;

            // Smooth pivot to face the struck target so the melee read is correct
            // (anti-snap: request the facing; TickFacing slerps over frames). The
            // contact path runs while the agent is stopped, so velocity-facing is
            // silent and this is the only facing driver — but it turns, never snaps.
            var targetMb = _currentTarget as MonoBehaviour;
            if (targetMb != null)
                RequestFacing(targetMb.transform.position - transform.position);

            _currentTarget.ApplyContactDamage(_contactDamage);
            if (_animator != null && _hasAttackParam) _animator.SetTrigger(AnimAttack);
            PlayTypeSound(_typeVfxSet != null ? _typeVfxSet.RandomAttackClip() : null);
        }

        /// <summary>
        /// WO-145 (Tactic B): deals hit-scan ranged damage to <paramref name="target"/>
        /// without closing to contact. Resolves an <see cref="IDamageableStructure"/>
        /// on the target (HeroHealth / Tower / Heart all implement it) and routes the
        /// hit through the same <c>ApplyContactDamage</c> path as melee, so HP,
        /// floating numbers and death are handled identically. Fires the Attack
        /// animator trigger (null-safe). No projectile / VFX — instant damage only
        /// (juice is a follow-on WO). Called by <see cref="EnemyBrain"/> while kiting.
        /// </summary>
        /// <param name="target">The transform to strike (its root may host the interface).</param>
        /// <param name="damage">Damage to apply this shot.</param>
        /// <returns>True when a live damageable target was hit.</returns>
        public bool RangedAttack(Transform target, float damage)
        {
            if (_dead || target == null || damage <= 0f) return false;

            // The interface may live on the target or a parent (collider is often a child).
            var structure = target.GetComponentInParent<IDamageableStructure>();
            if (structure == null || !structure.IsAlive) return false;

            // Face the target so the shot reads (smooth pivot, anti-snap).
            Vector3 toTarget = target.position - transform.position; toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.001f)
                RequestFacing(toTarget);

            // WO-491: ROOTED + TELEGRAPHED cast (flag-gated). The caster plays a WindUp ->
            // Cast animation + an audio charge cue and ROOTS the NavMeshAgent for the cast
            // window so the strike is readable/dodgeable and the caster does NOT slide while
            // casting. Damage lands at the END of the wind-up (re-checked for viability).
            // When the flag is OFF (or already mid-cast) the legacy instant hit runs.
            if (DeNelle.Core.FeatureFlags.EnemyRootedCast && !_casting)
            {
                StartCoroutine(RootedCast(target, damage));
                return true;
            }

            // ── Legacy instant ranged hit (flag OFF) ─────────────────────────
            structure.ApplyContactDamage(damage);
            if (_animator != null && _hasAttackParam) _animator.SetTrigger(AnimAttack);
            PlayTypeSound(_typeVfxSet != null ? _typeVfxSet.RandomAttackClip() : null);
            return true;
        }

        // WO-491: true while a rooted cast wind-up is in flight — blocks a second cast
        // (and the TickContactAttack telegraph) from overlapping it.
        private bool _casting;

        // Visible-cast VFX for ranged/mage casters (owner F8: "could not tell he was casting").
        // Lazily added so the enemy fires a real arcane orb that the player SEES leave + land.
        private RangedAttackVFX _castVfx;
        private RangedAttackVFX EnsureCastVfx()
        {
            if (_castVfx == null)
                _castVfx = GetComponent<RangedAttackVFX>() ?? gameObject.AddComponent<RangedAttackVFX>();
            return _castVfx;
        }

        /// <summary>
        /// WO-491: rooted, telegraphed ranged cast (mirrors <see cref="TelegraphThenAttack"/>'s
        /// shape). Stops the agent, plays WindUp -> charge audio -> Cast, waits the wind-up,
        /// then applies the damage if the target is still viable, then resumes the agent.
        /// </summary>
        private System.Collections.IEnumerator RootedCast(Transform target, float damage)
        {
            _casting = true;

            // Telegraph window — readable wind-up before the strike. Reuse the type-set's
            // configured telegraph duration when present, else a sane default.
            float windUp = (_typeVfxSet != null && _typeVfxSet.TelegraphDuration > 0f)
                ? _typeVfxSet.TelegraphDuration : 1.2f;
            // Readable-telegraph floor (owner F8: "animations from enemy very boring, could
            // not tell he was casting"). A sub-second wind-up doesn't register; hold >=1.0s so
            // the WindUp pose reads as a deliberate, reactable channel before the strike.
            windUp = Mathf.Max(windUp, 1.0f);

            // Root the agent for the cast (commit to it — no slide while casting).
            bool wasStopped = false;
            if (_agent != null && _agent.isOnNavMesh)
            {
                wasStopped = _agent.isStopped;
                _agent.isStopped = true;
            }

            // Telegraph: wind-up pose + audio charge cue.
            _actor?.PlayWindUp();
            EnemyCombatAudio.PlayCastCharge();

            // WO-560: draw a ground-ring danger tell at the AIM POINT (target's feet) so
            // the incoming cast is readable + dodgeable, mirroring the melee tell. Uses the
            // procedural Impact_ShockwaveRing fallback (committed asset, no pack prefab).
            if (target != null)
            {
                Vector3 castFeet = target.position;
                VFXManager.Play(VFXType.Impact_ShockwaveRing, castFeet,
                    Quaternion.identity, playSound: false);
                DeNelle.Core.Diagnostics.FlowTrace.Step("VFXTelegraph",
                    $"{_enemyId}: CAST telegraph fired (windUp={windUp:F2}s) ground-ring @ {castFeet}");
            }

            yield return new WaitForSeconds(windUp * 0.6f);

            // The cast itself (rooted): cast animation pose at the strike moment.
            if (!_dead) _actor?.PlayCast();

            yield return new WaitForSeconds(windUp * 0.4f);

            // Release: fire a VISIBLE arcane orb from the caster to the target so the cast
            // READS (owner F8: "could not tell he was casting"). Damage lands on orb ARRIVAL
            // (re-checked for viability), syncing the hit to the visible impact. Falls back to
            // instant damage only if the VFX component can't be resolved.
            if (!_dead && target != null)
            {
                Vector3 aim = target.position + Vector3.up * 1.0f;
                var vfx = EnsureCastVfx();
                System.Action land = () =>
                {
                    if (_dead || target == null) return;
                    var s = target.GetComponentInParent<IDamageableStructure>();
                    if (s != null && s.IsAlive)
                    {
                        s.ApplyContactDamage(damage);
                        PlayTypeSound(_typeVfxSet != null ? _typeVfxSet.RandomAttackClip() : null);
                    }
                };
                if (vfx != null) vfx.FireSpellOrb(aim, land);
                else land();
            }

            // Resume movement (only if WE rooted it — don't un-stop a contact-locked agent).
            if (_agent != null && _agent.isOnNavMesh && !wasStopped && _currentTarget == null)
                _agent.isStopped = false;

            _casting = false;
        }

        /// <summary>
        /// Acquires the structure (or hero) this enemy should stop and strike.
        /// 1. FORWARD probe (legacy): the short SphereCast ahead — this is ALSO how a
        ///    brain-less enemy lands contact damage on the HERO (HeroHealth implements
        ///    IDamageableStructure), so it always runs and is returned first.
        /// 2. STRUCTURE-AWARENESS sweep (ff.enemystructureaware, DATA-PROVEN fix): when the
        ///    forward lane is clear AND the hero is NOT in aggro range, a short all-direction
        ///    sweep returns the nearest live SIEGE structure (side tower/wall, or the Heart
        ///    tree) so a brain-less enemy attacks a defence it would otherwise march straight
        ///    past — the [Flow:EnemyAggro] "no brain => forward ProbeForStructure only" root,
        ///    where the forward-only cast missed ~99.7%. HERO-PRIMARY is preserved: the sweep
        ///    is suppressed while the hero is engageable, and it never targets the hero.
        /// </summary>
        private IDamageableStructure ProbeForStructure()
        {
            // 1. Legacy forward probe — keeps hero contact damage + path-blocking structures.
            IDamageableStructure forward = ProbeForStructureForward();
            if (forward != null) return forward;

            // Flag OFF => exact legacy (forward-only) behaviour — fully reversible.
            if (!DeNelle.Core.FeatureFlags.EnemyStructureAwareness) return null;

            // HERO-PRIMARY: while the hero is near, keep chasing it — do NOT peel onto a
            // side structure. (A structure literally ahead was already returned above.)
            // SKIP-REASON instrumentation (§12, behaviour-NEUTRAL): the verify-capture showed
            // 0 sweep acquires; these traces PROVE which branch suppressed it so we read data,
            // not theory — "hero in aggro" vs "swept but nothing in range".
            if (IsHeroWithinAggro())
            {
                DeNelle.Core.Diagnostics.FlowTrace.Throttle("EnemyAggro", $"skip-heroaggro-{_enemyId}", 1f,
                    $"{_enemyId}: structure sweep SUPPRESSED — hero within aggro (hero stays primary); forward-probe lane was clear");
                return null;
            }

            // 2. Hero not near => short all-direction sweep for the nearest live structure.
            var swept = SweepForNearestStructure();
            if (swept == null)
                // Once (not Throttle 1/sec): this inert-sweep signal was emitting ~3000 lines/run
                // (one per rep per second). Once collapses it to one line per enemy while keeping the
                // "feature still acquires nothing" diagnostic needed to finish verifying ff.enemystructureaware.
                DeNelle.Core.Diagnostics.FlowTrace.Once("EnemyAggro", $"skip-norange-{_enemyId}",
                    $"{_enemyId}: structure sweep RAN (hero not in aggro) but found NO live structure within " +
                    $"{Mathf.Max(_contactProbeDistance, _structureSweepRadius):F1}m");
            return swept;
        }

        /// <summary>
        /// Legacy forward SphereCast (extracted): the first live
        /// <see cref="IDamageableStructure"/> directly ahead, or null. Skirmishers probe
        /// slightly wider so they peel toward walls. This is the flag-OFF path AND the
        /// hero-chase path (the cast hits the hero's collider so contact damage lands).
        /// </summary>
        private IDamageableStructure ProbeForStructureForward()
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

        /// <summary>
        /// True when the hero is currently within this enemy's aggro range (generous: out
        /// to the drop margin so we don't peel onto a structure right at the chase edge).
        /// Reads the cached hero ref (refreshed by DriveNav.ResolveHeroTransform ~1/sec);
        /// re-resolves cheaply if it's gone so a late/streamed hero still suppresses the
        /// sweep. Used to keep the hero the PRIMARY target over the structure sweep.
        /// </summary>
        private bool IsHeroWithinAggro()
        {
            if (_heroAggroRadius <= 0f) return false;          // aggro disabled => sweep is free to fire
            if (_heroTransform == null) ResolveHeroTransform();
            if (_heroTransform == null || !_heroTransform.gameObject.activeInHierarchy) return false;

            float r = _heroAggroRadius + _heroAggroDropMargin; // don't peel near the chase edge
            float planarSqr = Vector3.ProjectOnPlane(
                _heroTransform.position - transform.position, Vector3.up).sqrMagnitude;
            return planarSqr <= r * r;
        }

        /// <summary>
        /// Structure-awareness fix: nearest live SIEGE <see cref="IDamageableStructure"/>
        /// within <see cref="_structureSweepRadius"/> in ANY direction (the "short sweep"
        /// that replaces the forward-only miss). Excludes the hero (handled by the aggro
        /// path) so the enemy never treats the hero as a siege target here. Reuses
        /// <see cref="_structureScanBuffer"/> (no per-tick alloc). Traces the acquire on
        /// [Flow:EnemyAggro] so a capture PROVES the fix (probe-fail count drops + a
        /// "ranged sweep acquired" line appears).
        /// </summary>
        private IDamageableStructure SweepForNearestStructure()
        {
            float radius = Mathf.Max(_contactProbeDistance, _structureSweepRadius);
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            int count = Physics.OverlapSphereNonAlloc(
                origin, radius, _structureScanBuffer, ~0, QueryTriggerInteraction.Ignore);

            IDamageableStructure nearest = null;
            float nearestSqr = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var c = _structureScanBuffer[i];
                if (c == null) continue;
                var structure = c.GetComponentInParent<IDamageableStructure>();
                if (structure == null || !structure.IsAlive) continue;
                // The hero implements IDamageableStructure — never grab it via the siege
                // sweep (the verified hero-aggro path owns hero engagement).
                if (structure is HeroHealth) continue;
                float sqr = (c.transform.position - transform.position).sqrMagnitude;
                if (sqr < nearestSqr) { nearestSqr = sqr; nearest = structure; }
            }

            if (nearest != null)
                DeNelle.Core.Diagnostics.FlowTrace.Throttle("EnemyAggro", $"sweep-{_enemyId}", 1f,
                    $"{_enemyId}: ranged sweep acquired structure '{(nearest as MonoBehaviour)?.name}' " +
                    $"within {radius:F1}m (hero not in aggro) -> stopping to attack " +
                    "(was: ProbeForStructure forward-only miss)");
            return nearest;
        }

        // ---------------------------------------------------------------------
        // HP / death
        // ---------------------------------------------------------------------

        /// <summary>
        /// Applies <paramref name="amount"/> damage. At zero HP the enemy dies,
        /// raises <see cref="Died"/> and is destroyed. Hero abilities, pets and
        /// towers route their damage through here.
        /// </summary>
        // Colour stamped by the damage source (hero / pet) for the NEXT number only;
        // consumed and cleared in TakeDamageFrom. Null = magnitude-based default colour.
        private Color? _nextNumberTint;

        /// <summary>Source-tint the next floating damage number (see IDamageTintable).</summary>
        public void SetNextDamageTint(Color color) => _nextNumberTint = color;

        // WO-219: element stamped by the damage source (spell / weapon) for the NEXT hit
        // only; consumed and cleared in TakeDamageFrom so the impact burst is tinted by
        // element (flame / ice / aether) instead of the generic grey physical spark.
        // Null = physical (the existing VfxPool / Impact_Physical fallback).
        private DamageElement? _nextImpactElement;

        /// <summary>WO-219: element-tint the impact VFX for the NEXT hit (spell hits read
        /// flame/ice/aether; melee stays physical). Set by EnemyDamageable.TakeDamage.</summary>
        public void SetNextImpactElement(DamageElement element) => _nextImpactElement = element;

        // Ticket #61: stamped TRUE by the HERO's attack/ability paths (via
        // EnemyDamageable.MarkNextHitFromHero) for the NEXT hit only; consumed in
        // TakeDamageFrom. Gates the combo / kill-streak / RAMPAGE feedback so tower,
        // pet, DoT and environmental damage NEVER drive the combo. Default false =
        // non-hero (only the hero stamps it true).
        private bool _nextDealtByHero;

        /// <summary>Ticket #61: mark the NEXT hit as hero-dealt so its combo/streak/RAMPAGE
        /// feedback fires. Set by EnemyDamageable.MarkNextHitFromHero (hero paths only).</summary>
        public void SetNextDealtByHero(bool value) => _nextDealtByHero = value;

        public void TakeDamage(float amount)
        {
            // No source position — default flinch direction is Front (attacker
            // presumed to be in front of the enemy; the Nav destination is always
            // ahead, so most hits do come from that direction).
            TakeDamageFrom(amount, transform.position + transform.forward * 5f);
        }

        /// <summary>
        /// DEF-46: Directional overload — takes the world-space position of the
        /// damage source so the flinch animation matches the hit direction.
        /// Called directly by anything that knows its own world position.
        /// </summary>
        public void TakeDamageFrom(float amount, Vector3 sourceWorldPos)
        {
            if (_dead || amount <= 0f) return;

            // Ticket #61: consume the hero-source stamp ONCE for this hit. Only the hero's
            // attack/ability paths stamp it true (via EnemyDamageable.MarkNextHitFromHero);
            // tower / pet / DoT / environment leave it false. Gates the combo / kill-streak /
            // RAMPAGE feedback below (and the kill feedback inside Die) to hero strikes only.
            bool dealtByHero = _nextDealtByHero;
            _nextDealtByHero = false;

            // Floating combat text — pop the damage number at the enemy's head so
            // the player can see the hit (and watch it rise after a damage talent).
            // Spawned BEFORE death so the killing blow still shows its number even
            // though this GameObject may be destroyed below. A source may have
            // stamped a tint (hero vs pet) for this hit — consume it once.
            if (_nextNumberTint.HasValue)
            {
                DamageNumberSpawner.Spawn(amount, HeadWorldPosition(), _nextNumberTint.Value);
                _nextNumberTint = null;
            }
            else
            {
                DamageNumberSpawner.Spawn(amount, HeadWorldPosition());
            }

            _hp = Mathf.Max(0f, _hp - amount);
            if (_hp <= 0f)
            {
                Die(killed: true, dealtByHero: dealtByHero);
            }
            else
            {
                // Retaliate: notify the brain it was struck so it aggros the
                // attacker (regardless of engage radius / role / behaviour tree).
                Damaged?.Invoke(sourceWorldPos);

                // DEF-46: compute cardinal hit direction from source and drive the
                // directional flinch sub-state before firing the Hit trigger.
                HitDirection dir = ComputeHitDirection(sourceWorldPos);
                if (_animator != null)
                {
                    if (_hasHitDirParam) _animator.SetInteger(AnimHitDir, (int)dir);
                    if (_hasHitParam)    _animator.SetTrigger(AnimHit);
                }

                // DEF-46: per-type hit VFX — use SO prefab when assigned, fall
                // back to the procedural VfxPool burst otherwise.
                // WO-219: when the damage source stamped an element (a spell hit),
                // route through the existing VFXManager element impacts so flame /
                // ice / aether hits read distinctly. Melee (null element) keeps the
                // original SO-prefab / VfxPool grey physical spark. Consume once.
                Vector3 hitPos = transform.position + Vector3.up * 0.6f;
                DamageElement? impactElement = _nextImpactElement;
                _nextImpactElement = null;
                VFXType elementImpact = ImpactVfxFor(impactElement);
                if (elementImpact != VFXType.None)
                {
                    VFXManager.Play(elementImpact, hitPos);
                }
                else
                {
                    GameObject hitPrefab = _typeVfxSet != null ? _typeVfxSet.RandomHitVfxPrefab() : null;
                    if (hitPrefab != null)
                    {
                        // LEAK FIX (#3): the SO hit-VFX prefab was Instantiated with NO
                        // Destroy — every non-lethal hit leaked a GameObject forever
                        // (a HARD leak; the procedural VfxPool path self-returns). Give
                        // the spawned VFX a bounded lifetime so it self-destructs.
                        var hitGo = Instantiate(hitPrefab, hitPos, Quaternion.identity);
                        if (hitGo != null) Destroy(hitGo, TypeVfxSelfDestructSeconds);
                    }
                    else
                        VfxPool.SpawnHitImpact(hitPos);
                }

                // Combat feel: blink the enemy red on the hit (additive, null-safe).
                _hitReaction?.Flash();

                // WO-84: heavy-hit path — bigger shake + explosion VFX on large hits.
                if (amount >= _heavyHitThreshold)
                    CameraShakeBridge.Shake(0.32f, 0.22f);   // heavier than the per-hit default

                // DEF-46: per-type hit audio. WO-220: fall back to a generated hit
                // SFX (via CoreServices.Audio) when no type-set clip is authored.
                PlayTypeSound(_typeVfxSet != null ? _typeVfxSet.RandomHitClip() : null,
                              CombatSfxFallback.Hit);

                // DEF-44/45: hit-stop + combo counter. Ticket #61: HERO hits ONLY — tower /
                // pet / DoT / environment hits must NOT drive the combo / RAMPAGE feedback.
                DeNelle.Core.Diagnostics.FlowTrace.Step("Combat",
                    "CombatFeedback Hit gated: dealtByHero=" + dealtByHero + " amount=" + amount);
                if (dealtByHero)
                    CombatFeedbackManager.Hit(hitPos, amount);
            }
        }

        /// <summary>
        /// WO-566: talent on-hit DoT (Knight Emberbrand Strike burn / ranger Poison Tip bleed).
        /// Applies <paramref name="dps"/> damage per second for <paramref name="duration"/> seconds
        /// as 1-second ticks, each routed through <see cref="TakeDamageFrom"/> so every tick shows a
        /// number + flinches toward the source. Data-driven (the caller passes the node's value /
        /// duration). No-op on a dead enemy / non-positive params. A re-proc simply runs a second
        /// concurrent burn (cheap; bounded by duration).
        /// </summary>
        public void ApplyDamageOverTime(float dps, float duration, Vector3 sourceWorldPos)
        {
            if (_dead || dps <= 0f || duration <= 0f) return;
            StartCoroutine(DamageOverTimeRoutine(dps, duration, sourceWorldPos));
        }

        private System.Collections.IEnumerator DamageOverTimeRoutine(float dps, float duration, Vector3 sourceWorldPos)
        {
            float remaining = duration;
            while (remaining > 0f && !_dead)
            {
                yield return new WaitForSeconds(1f);
                if (_dead) yield break;
                remaining -= 1f;
                TakeDamageFrom(dps, sourceWorldPos);
            }
        }

        /// <summary>
        /// DEF-46: Maps a world-space source position onto the enemy's local axes
        /// to determine which of the four cardinal quadrants the hit came from.
        /// </summary>
        private HitDirection ComputeHitDirection(Vector3 sourceWorldPos)
        {
            Vector3 toSource = (sourceWorldPos - transform.position);
            toSource.y = 0f;
            if (toSource.sqrMagnitude < 0.01f) return HitDirection.Front;

            // Project onto the enemy's forward/right to get local [-1..1] coords.
            float fwdDot   = Vector3.Dot(toSource.normalized, transform.forward);
            float rightDot = Vector3.Dot(toSource.normalized, transform.right);

            // Dominant axis picks Front/Back/Left/Right.
            if (Mathf.Abs(fwdDot) >= Mathf.Abs(rightDot))
                return fwdDot >= 0f ? HitDirection.Front : HitDirection.Back;
            else
                return rightDot >= 0f ? HitDirection.Right : HitDirection.Left;
        }

        /// <summary>Kills the enemy immediately (e.g. consumed into an ATB breach).</summary>
        public void Kill()
        {
            if (!_dead) Die(killed: false);
        }

        // =====================================================================
        //  Pooling reset contract (EnemyPool) — EXHAUSTIVE on purpose.
        //  A missed reset = an enemy that spawns dead / untargetable / with stale
        //  HP / double-subscribed events. RELEASE tears the body down to a clean
        //  dormant state; REUSE re-arms it for a fresh spawn (the spawner then
        //  calls Configure(), which re-seeds stats / agent / id as it always has).
        // =====================================================================

        /// <summary>
        /// RELEASE side — called by <see cref="Die"/> right before the body returns
        /// to the <see cref="EnemyPool"/>. Drops EVERYTHING that must not survive into
        /// the next reuse: live coroutines, the targeting registry membership, the
        /// damage-attribution ledger (keyed on BOTH this Enemy and its EnemyDamageable
        /// — the hero/pet record against the adapter, ProgressionManager drains against
        /// the Enemy, so both buckets must be forgotten or they leak across reuses),
        /// the brain nav overrides, the contact target, status timers and VFX/tint
        /// state. The GameObject is left ACTIVE here; the pool deactivates it (so the
        /// death-hold animation has already played by the time Die calls us).
        /// </summary>
        public void ResetForPool()
        {
            // 1. Stop every coroutine this body started (telegraph wind-up, secondary
            //    death burst, hit-flash is owned by EnemyHitReaction's OnDisable).
            StopAllCoroutines();
            _telegraphing = false;

            // 2. Leave the targeting registry so the reticle/towers/pets can't pick a
            //    pooled body. (Die already unregistered on death; idempotent here.)
            TargetManager.Unregister(this);

            // 3. Forget the damage ledger under BOTH keys (see summary) so a reused
            //    body never inherits a previous life's attribution. ReportKill already
            //    Drained this Enemy on a real kill; Forget is idempotent + also clears
            //    the adapter-keyed bucket that Record() actually wrote to.
            DeNelle.Core.Combat.DamageAttribution.Forget(this);
            var dmg = GetComponent<EnemyDamageable>();
            if (dmg != null) DeNelle.Core.Combat.DamageAttribution.Forget(dmg);

            // 4. Clear AI / nav overrides so the reused body re-acquires from scratch
            //    (brain re-scores on its own interval; these clear the Enemy-side seam).
            _brainTarget = null;
            _brainPositionOverride = null;
            _currentTarget = null;
            _heroAggroEngaged = false;
            _heroTransform = null;
            _heroResolveTimer = 0f;

            // 5. Halt + detach the agent so a dormant body never paths.
            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;

            // 6. Clear the next-hit VFX/tint stamps so a reused body starts neutral.
            _nextNumberTint = null;
            _nextImpactElement = null;
            _nextDealtByHero = false;   // ticket #61: don't leak a hero stamp across pooling
        }

        /// <summary>
        /// ACQUIRE side — called by <see cref="EnemyPool.Get"/> on a REUSED body right
        /// after it is re-enabled + re-placed. Re-arms the body to a live, full-HP,
        /// targetable, animating state. Stat-specific re-seeding (HP from def, agent
        /// speed, id) is then done by the spawner's <see cref="Configure"/> call (and
        /// <see cref="ApplyWaveScaling"/>) exactly as on a fresh spawn — so this method
        /// only undoes the DEATH state the body was pooled in.
        /// </summary>
        /// <param name="pos">Re-spawn position (already NavMesh-snapped by the pool).</param>
        /// <param name="rot">Re-spawn rotation.</param>
        public void PrepareForReuse(Vector3 pos, Quaternion rot)
        {
            // Resolve refs (the body's components persist across pooling, but re-cache
            // defensively in case anything was lost — mirrors Awake).
            EnsureAgent();
            EnsureAnimator();
            EnsureAudio();
            EnsureHitReaction();

            // 1. Clear the dead latch FIRST so Update()/targeting see a live enemy.
            _dead = false;
            _telegraphing = false;
            _navWarned = false;
            _attackCooldown = 0f;
            _currentTarget = null;
            _brainTarget = null;
            _brainPositionOverride = null;
            _heroAggroEngaged = false;
            _heroTransform = null;
            _heroResolveTimer = 0f;
            _nextNumberTint = null;
            _nextImpactElement = null;
            _nextDealtByHero = false;   // ticket #61

            // 2. Restore HP to full so the body isn't handed out at 0 HP / instantly
            //    re-dead. Configure() re-seeds _maxHp from the def right after this;
            //    we set _hp = _maxHp so there is never a 1-frame zero-HP window.
            _hp = _maxHp;

            // 3. Reset the Animator out of its latched Death state (the Dead bool is a
            //    sticky latch — without clearing it the reused body stays collapsed).
            _actor?.Revive();
            if (_animator != null)
            {
                if (_hasDeadParam) _animator.SetBool(AnimDead, false);
                if (_hasSpeedParam) _animator.SetFloat(AnimSpeed, 0f);
                // Rebind drops any in-flight death-clip pose so the controller restarts
                // clean at its default (idle) state on the next frame.
                if (_animator.runtimeAnimatorController != null && _animator.isActiveAndEnabled)
                    _animator.Rebind();
            }

            // 4. Re-place + warp the agent onto the NavMesh and re-enable pathing so a
            //    reused body actually moves (Warp is the supported way to teleport a
            //    NavMeshAgent; SetPosition alone desyncs the internal agent state).
            if (_agent != null)
            {
                if (_agent.isOnNavMesh) _agent.isStopped = true;
                _agent.Warp(pos);
                if (_agent.isOnNavMesh) _agent.isStopped = false;
            }

            // 5. Status timers (freeze/slow/burn) live on EnemyDamageable and key off
            //    Time.time + seconds; by the time a body is reused, Time.time has long
            //    passed any expiry set on its previous life, so IsFrozen/IsSlowed/
            //    IsBurning already read false. No explicit clear needed for the normal
            //    case. (Edge case: a multi-minute status applied the frame before death
            //    could carry over — flagged for the owner playtest.)

            // 6. Re-register with the targeting registry. OnEnable already fired
            //    Register when the pool SetActive(true)'d us, but Register dedups, so
            //    this belt-and-braces a body whose OnEnable ran before _dead cleared.
            TargetManager.Register(this);

            // 7. Reset the path throttle so the first reuse frame re-paths immediately.
            _pathRefreshTimer = 0f;
            _lastPathedDestination = Vector3.zero;
        }

        /// <param name="killed">
        /// True when HP reached zero (a real defender kill — grants shared XP);
        /// false when force-removed (ATB breach) — no XP, just drop its ledger.
        /// </param>
        /// <param name="dealtByHero">
        /// Ticket #61: true only when the KILLING blow came from the player's HERO. Gates
        /// the combo / kill-streak / RAMPAGE feedback (CombatFeedbackManager.Kill) to hero
        /// kills only — tower / pet / DoT / environment kills still die + drop loot + grant
        /// XP, they just don't feed the combo. Forced removals default to non-hero.
        /// </param>
        private void Die(bool killed, bool dealtByHero = false)
        {
            _dead = true;
            _telegraphing = false;   // audit 2026-05-30: clear the wind-up latch on death (safe for future pooling)
            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = true;

            // WO-531: snap the body down to the ground the instant it dies so the corpse
            // (and every position-derived effect below — deathPos VFX, scorch decal,
            // CombatFeedbackManager.Kill) sits ON the ground instead of floating at the
            // Y it died at (e.g. mid-air over a wall top). Done FIRST so all downstream
            // transform.position reads pick up the grounded position.
            SnapBodyToGround();
            _currentTarget = null;
            TargetManager.Unregister(this);   // drop from targeting the instant it dies

            // Drive death anim (latches Dead bool so last frame holds; see ActorAnimator + controllers).
            _actor?.Die();
            // PERMANENT live instrumentation (owner steer 2026-06-23 "enemy animation in effect"):
            // emit AT the point the Dead state is driven so a kill self-PROVES the death animation
            // FIRED at runtime (headless encounter run / F8 break-log) — not inferred from the asset existing.
            DeNelle.Core.Diagnostics.FlowTrace.Step("Enemy", "DEATH ANIM playing actor=" + gameObject.name + " state=Dead");
            Died?.Invoke(this);

            // DEF-52 / DEF-46: death burst VFX + audio + micro screen shake.
            // DEF-45: kill streak tracked via CombatFeedbackManager.
            if (killed)
            {
                Vector3 deathPos = transform.position + Vector3.up * 0.5f;

                // WO-66: elite/boss enemies use EliteVFXController for death VFX.
                // Falls through to the normal per-type SO / VfxPool path for regular enemies.
                var eliteVfx = GetComponent<EliteVFXController>();
                if (eliteVfx != null)
                {
                    eliteVfx.OnEliteDeath();
                }
                else
                {
                    // WO-84: use the per-prefab deathVFXOverride when set; otherwise fall
                    // back to the SO prefab pool, then the procedural VfxPool burst.
                    if (_deathVFXOverride != VFXType.Death_Generic && _deathVFXOverride != VFXType.None)
                    {
                        VFXManager.Play(_deathVFXOverride, deathPos);
                    }
                    else
                    {
                        // DEF-46: per-type death VFX — SO prefab when assigned, else VfxPool.
                        GameObject deathPrefab = _typeVfxSet != null ? _typeVfxSet.RandomDeathVfxPrefab() : null;
                        if (deathPrefab != null)
                        {
                            // LEAK FIX (#3): the SO death-VFX prefab was Instantiated with
                            // NO Destroy — every kill leaked a GameObject forever. Bound
                            // its lifetime so it self-destructs (the VfxPool path already
                            // self-returns; this matches that behaviour).
                            var deathGo = Instantiate(deathPrefab, deathPos, Quaternion.identity);
                            if (deathGo != null) Destroy(deathGo, TypeVfxSelfDestructSeconds);
                        }
                        else
                            VfxPool.SpawnDeathBurst(deathPos);
                    }

                    // WO-84: secondary burst 0.28 s after the primary death VFX.
                    StartCoroutine(SecondaryDeathBurst(deathPos));
                }

                // DEF-46: per-type death audio (always plays, even for elite kills).
                // WO-220: fall back to a generated death SFX (via CoreServices.Audio)
                // when no type-set clip is authored.
                PlayTypeSound(_typeVfxSet != null ? _typeVfxSet.RandomDeathClip() : null,
                              CombatSfxFallback.Death);

                // Regular shake only when no EliteVFXController handled it.
                if (eliteVfx == null) CameraShakeBridge.Shake(0.18f, 0.22f);

                // Ticket #61: combo / kill-streak / RAMPAGE + crystal feedback fires for HERO
                // kills ONLY. A tower / pet / DoT / environmental kill still bursts VFX, shakes,
                // drops loot and grants XP (all above/below) — it just must NOT feed the combo.
                DeNelle.Core.Diagnostics.FlowTrace.Step("Combat",
                    "CombatFeedback Kill gated: dealtByHero=" + dealtByHero + " enemy=" + gameObject.name);
                if (dealtByHero)
                    CombatFeedbackManager.Kill(transform.position);

                // DEF-178: a brief hit-stop "punch" on the kill — the satisfying
                // weight beat that was missing (kills only shook, never froze). Just
                // the freeze (not DoImpact, which would add a second shake on top of
                // the CameraShakeBridge one above); HitStopManager is null-safe + its
                // own quality gate skips this on Low. Short + capped (mobile-safe).
                // HitStopManager dedups overlaps, so a multi-kill frame won't stack.
                HitStopManager.Instance?.HitStop(0.05f, 0.04f);

                // Combat feel: leave a persistent ground scorch where the enemy
                // fell. Null-safe — no DecalSpawner in the scene = no-op.
                DecalSpawner.Instance?.SpawnScorch(transform.position);
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

            // WO-432/433: grant GOLD (Coins) on kill so the Gold-cost building research has a kill-driven
            // source. Data-driven (EnemyDef.CoinReward) with an XP-derived fallback so EVERY enemy pays out
            // (~6-20 early game; tougher enemies have more XP -> more gold). EconomyService.AddCoins is the
            // single Coins grant + HUD/save path.
            if (killed && _def != null)
            {
                int gold = _def.CoinReward > 0 ? _def.CoinReward : Mathf.Max(4, Mathf.RoundToInt(_def.XpReward * 0.4f));
                if (gold > 0) EconomyService.Instance?.AddCoins(gold);
            }

            // Play the death (collapse) animation, then RETURN TO THE POOL (no longer
            // Destroy — pooling reuses the body to kill the per-spawn GameObject churn
            // / stray accumulation). The Dead bool latches the controller's Death state;
            // the body is held DeathHoldSeconds so the collapse clip is visible, then
            // ResetForPool tears it down and EnemyPool.Release parks it dormant. With no
            // Animator there is nothing to hold for, so it is released this frame.
            if (_animator != null)
            {
                if (_hasDeadParam) _animator.SetBool(AnimDead, true);
                StartCoroutine(ReturnToPoolAfterDeathHold());
            }
            else
            {
                ResetForPool();
                EnemyPool.Release(this);
            }
        }

        /// <summary>
        /// Holds the dead body <see cref="DeathHoldSeconds"/> so its collapse clip is
        /// visible, then resets it and returns it to the <see cref="EnemyPool"/>. This
        /// replaces the old <c>Destroy(gameObject, DeathHoldSeconds)</c>. If the body
        /// were re-killed in that window the dead latch prevents a second Die; and if
        /// the pool is gone (shutdown) Release falls back to Destroy.
        /// </summary>
        private System.Collections.IEnumerator ReturnToPoolAfterDeathHold()
        {
            yield return new WaitForSeconds(DeathHoldSeconds);
            ResetForPool();
            EnemyPool.Release(this);
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        /// <summary>
        /// WO-84: Small secondary impact burst 0.28 s after the primary death VFX —
        /// gives the kill a two-beat punch. Null-safe if VFXManager is absent.
        /// </summary>
        private System.Collections.IEnumerator SecondaryDeathBurst(Vector3 pos)
        {
            yield return new WaitForSeconds(0.28f);
            VFXManager.Play(VFXType.Impact_Physical, pos + Vector3.up * 0.3f);
        }

        /// <summary>
        /// WO-219: maps a damage element to its existing impact VFXType. Returns
        /// <see cref="VFXType.None"/> for a null element (melee / physical) so the
        /// caller keeps the original SO-prefab / VfxPool grey-spark path. No new
        /// VFXType added — these all already exist in VFXType.cs.
        /// </summary>
        private static VFXType ImpactVfxFor(DamageElement? element)
        {
            if (!element.HasValue) return VFXType.None;
            switch (element.Value)
            {
                case DamageElement.Flame: return VFXType.Impact_Flame;
                case DamageElement.Ice:    return VFXType.Impact_Ice;
                case DamageElement.Aether: return VFXType.Impact_Aether;
                // Physical / None spell damage uses the existing per-type / VfxPool path.
                default:                   return VFXType.None;
            }
        }

        private void EnsureAgent()
        {
            if (_agent == null) _agent = GetComponent<NavMeshAgent>();
        }

        /// <summary>
        /// WO-531: snap the dead body straight down onto the ground so a corpse never
        /// floats at the Y it died at (e.g. mid-air over a wall top). Owner directive:
        /// on death the body falls/snaps to ground regardless of where it died.
        /// (DragonBoss is its OWN class with its own death spiral and never runs this
        /// <see cref="Die"/>, so apex bosses are already excluded.)
        ///
        /// Primary: raycast DOWN against the ground/terrain layers and snap Y to the hit.
        /// Fallback: <see cref="NavMesh.SamplePosition"/> and snap Y to the sampled point.
        /// If neither resolves, the position is left unchanged and a Warn is logged.
        /// </summary>
        private void SnapBodyToGround()
        {
            try
            {
                Vector3 pos = transform.position;

                // Ground/terrain/default layers only — this naturally excludes the
                // enemy's own collider (on the "Enemy" layer) so the ray never self-hits.
                int groundMask = LayerMask.GetMask("Default", "Terrain", "Ground");
                if (groundMask == 0) groundMask = Physics.DefaultRaycastLayers;

                Vector3 origin = pos + Vector3.up * 2f;
                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 50f,
                                    groundMask, QueryTriggerInteraction.Ignore))
                {
                    pos.y = hit.point.y;
                    transform.position = pos;
                    return;
                }

                // Fallback: nearest point on the navmesh (the walkable surface the enemy
                // traverses) when no physics ground collider answered.
                if (NavMesh.SamplePosition(pos, out NavMeshHit navHit, 5f, NavMesh.AllAreas))
                {
                    pos.y = navHit.position.y;
                    transform.position = pos;
                    return;
                }

                DeNelle.Core.Diagnostics.FlowTrace.Warn("Enemy",
                    $"SnapBodyToGround({gameObject.name}) found no ground (raycast + navmesh both missed) at {pos} — body left in place");
            }
            catch (Exception e)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Warn("Enemy",
                    $"SnapBodyToGround({gameObject.name}) threw (best-effort, death path unaffected): {e.GetType().Name}: {e.Message}");
            }
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
            catch (Exception e) { DeNelle.Core.Diagnostics.FlowTrace.Warn("Enemy", $"TryAwardGlimmer({amount}) reflected reward threw (best-effort, kill path unaffected): {e.GetType().Name}: {e.Message}"); }
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

            // WO-163: cache which params the controller actually declares so the
            // per-frame DriveAnimator (and the trigger/bool calls) never drive an
            // absent param — that spams "Parameter does not exist" every frame.
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                foreach (var p in _animator.parameters)
                {
                    if (p.nameHash == AnimSpeed)  _hasSpeedParam  = true;
                    if (p.nameHash == AnimAttack) _hasAttackParam = true;
                    if (p.nameHash == AnimWindUp) _hasWindUpParam = true;
                    if (p.nameHash == AnimHit)    _hasHitParam    = true;
                    if (p.nameHash == AnimDead)   _hasDeadParam   = true;
                    if (p.nameHash == AnimHitDir) _hasHitDirParam = true;
                }
            }
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
