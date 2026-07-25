// =============================================================================
// DragonBoss - Syndrath the Devourer, the apex village wave-boss (WO-760).
// -----------------------------------------------------------------------------
// THE ASSET (2026-07-23). Assets/Dragon/Prefab/Dragon.prefab - the licensed
// Asset-Store dragon (Unity Asset Store product 71047 "Dragon Animated",
// WDallgraphics; commercial license). It carries a full clip set
// (dragon@idle/walk/run/fly/glide/takeoff/landing/attack1-3/bite/hit/die/die2)
// under Assets/Dragon/Animations. It REPLACES the old free 3DHaupt dragon
// (formerly wired via Resources/Enemies/Boss_Dragon -> Dragon.fbx) which shipped
// under CC-BY-NC - a commercial-ship blocker - and has been removed.
//
// This script DRIVES the encounter in code. It sets the Animator params named in
// the DragonAnim contract (Speed/Attack/Dead + Takeoff/Fly/Landing/Grounded +
// Attack1-3); the wired controller (DragonAnimatorSetup) exposes exactly those.
// Every Animator call is presence-guarded (see EnsureAnimator / the _params set)
// so a rig whose controller lacks a param simply skips it.
//
// WHERE IT FITS. "Syndrath the Devourer" - a sky-boss set-piece above the
// eight-boss slate (owner-ratified 2026-05-19). The apex village wave releases it.
//
// MODULE ISOLATION. Lives in DeNelle.Village (it threatens the village Heart). It
// implements the cross-module DeNelle.Core.Combat.IDamageable seam DIRECTLY so
// the hero's abilities and the isolated DeNelle.Pets module can damage it without
// referencing this concrete type.
//
// FLIGHT, NOT NAVMESH. The dragon owns its own kinematic flight (fly-in / descend
// / grounded-fire / climb / orbit / swoop) - no NavMeshAgent, no Rigidbody. Ground
// height for the land-point is sampled with a guarded downward raycast (fallback
// y = 0); nothing here depends on a baked NavMesh.
//
// SEQUENCE (WO-760, owner intent verbatim): the dragon "flies into town, lands,
// and uses fire attacks to burn towers. After all towers are destroyed then
// targets the tree of life." The ARC is a state machine (DragonState), not a pure
// HP-orbit:
//   Approaching  - spawns off-map at altitude and flies in toward the town.
//   Landing      - descends to a ground point beside the nearest tower.
//   BurnTowers   - grounded; fire-attacks the nearest live DefenseTower/ArcaneTower,
//                  advancing target-to-target until none remain alive.
//   AirAttack    - airborne; DIVES to fire-attack the current tower from the air.
//   RetargetTree - takes off, retargets the Heart (Heart -> Boss state).
//   Finale       - the original orbit + dive-swoop + fire-breath, aimed at the Heart.
//   Death        - a long spiralling fall, then destroy.
// AI-DRIVEN ATTACK STYLE (owner directive 2026-07-24): against the towers the dragon
// does NOT follow a fixed "always land" order - the air-vs-land choice for each attack
// is made by an EnemyBrain-style decision hook (DecideAttackMode), mirroring
// EnemyBrain.UpdateTacticalState/ArchetypeDefaultState: a per-tick posture enum
// (DragonAttackMode.Air/Land) selected from HP phase + engagement geometry, with a
// Land fail-safe. DragonBoss is a kinematic flyer (no NavMeshAgent + no Enemy), so it
// can't host the ground EnemyBrain component; it REUSES the brain's decision PATTERN
// instead (approach iii). The fly-in, tower-advance, and retarget-Heart arc are all
// preserved; only the per-attack style is now dynamic.
// HP still modulates aggression (DragonPhase: Circling/Stooping/LastWing) for the
// phase auras + boss-bar label; the STATE progression above is the sequence.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Combat;
using DeNelle.Core.Diagnostics;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// The shared Animator parameter-name contract between <see cref="DragonBoss"/>
    /// (which sets these) and <see cref="DeNelle.Editor.DragonAnimatorSetup"/> (which
    /// declares them on the built controller). DeNelle.Editor cannot reference this
    /// assembly, so the builder mirrors these string literals verbatim - they MUST
    /// stay identical on both sides.
    /// </summary>
    public static class DragonAnim
    {
        /// <summary>Float - locomotion blend (Idle &lt;-&gt; Fly).</summary>
        public const string Speed = "Speed";
        /// <summary>Trigger - the legacy generic strike beat (finale swoop / breath).</summary>
        public const string Attack = "Attack";
        /// <summary>Bool - death latch.</summary>
        public const string Dead = "Dead";
        /// <summary>Trigger - launch from the ground into flight.</summary>
        public const string Takeoff = "Takeoff";
        /// <summary>Bool - true while airborne.</summary>
        public const string Fly = "Fly";
        /// <summary>Trigger - descend and settle to the ground.</summary>
        public const string Landing = "Landing";
        /// <summary>Bool - true while grounded (burning towers).</summary>
        public const string Grounded = "Grounded";
        /// <summary>Trigger - grounded fire attack variant 1.</summary>
        public const string Attack1 = "Attack1";
        /// <summary>Trigger - grounded fire attack variant 2.</summary>
        public const string Attack2 = "Attack2";
        /// <summary>Trigger - grounded fire attack variant 3.</summary>
        public const string Attack3 = "Attack3";
    }

    /// <summary>
    /// HP-threshold aggression phase - drives the phase auras + the boss-bar label
    /// (BossHealthBar). Distinct from <see cref="DragonState"/>, which drives the
    /// fly-in -&gt; land -&gt; burn -&gt; Tree sequence.
    /// </summary>
    public enum DragonPhase
    {
        /// <summary>Phase 1 (100-60% HP) - calm.</summary>
        Circling = 0,
        /// <summary>Phase 2 (60-25% HP) - enraged.</summary>
        Stooping = 1,
        /// <summary>Phase 3 (25-0% HP) - seething.</summary>
        LastWing = 2,
        /// <summary>HP zero - the long spiralling fall to the ground.</summary>
        Falling = 3,
    }

    /// <summary>
    /// The behaviour-sequence state the dragon is currently in (WO-760). The ARC is
    /// scripted (fly in -&gt; engage towers -&gt; take off and finish on the Heart), but
    /// the ATTACK STYLE against the towers - a grounded burn (<see cref="Landing"/> -&gt;
    /// <see cref="BurnTowers"/>) versus an aerial fire pass (<see cref="AirAttack"/>) -
    /// is chosen per attack by the EnemyBrain-style decision hook
    /// (<see cref="DragonBoss"/>.DecideAttackMode), not fixed.
    /// </summary>
    public enum DragonState
    {
        /// <summary>Spawned off-map at altitude; flying in toward the town.</summary>
        Approaching = 0,
        /// <summary>Descending to a ground point beside the nearest tower.</summary>
        Landing = 1,
        /// <summary>Grounded; fire-attacking towers one after another.</summary>
        BurnTowers = 2,
        /// <summary>Taking off and retargeting the Heart of Elarion.</summary>
        RetargetTree = 3,
        /// <summary>Orbit + dive-swoop + fire-breath finale, aimed at the Heart.</summary>
        Finale = 4,
        /// <summary>Airborne; diving to fire-attack the current tower from the air (AI-chosen).</summary>
        AirAttack = 5,
    }

    /// <summary>
    /// The attack posture the AI brain chooses for a single tower strike
    /// (<see cref="DragonBoss"/>.DecideAttackMode). Mirrors <see cref="DeNelle.Village.EnemyTacticalState"/>:
    /// a posture enum selected each decision tick from state (HP phase + geometry),
    /// so air-vs-land is a dynamic decision rather than a scripted order.
    /// </summary>
    public enum DragonAttackMode
    {
        /// <summary>Descend and burn the tower on the ground (WO-760 default / fail-safe).</summary>
        Land = 0,
        /// <summary>Stay airborne and strike the tower in a diving fire pass.</summary>
        Air = 1,
    }

    /// <summary>
    /// The apex dragon boss - Syndrath the Devourer. Flies into the village, lands,
    /// burns the defensive towers, then takes off and finishes on the Heart in a
    /// swooping fire-breath finale. Implements <see cref="IDamageable"/> directly so
    /// the hero and the isolated pets module can damage it through the Core seam.
    /// </summary>
    /// <remarks>Boss name "Syndrath the Devourer" - owner-ratified 2026-05-19.</remarks>
    [DisallowMultipleComponent]
    public sealed class DragonBoss : MonoBehaviour, IDamageable, ICombatLayered
    {
        // -- Identity --------------------------------------------------------------

        [Header("Identity")]
        [Tooltip("Stable per-instance id - e.g. 'boss-dragon-1'.")]
        [SerializeField] private string _bossId = "boss-dragon";

        // -- Stats -----------------------------------------------------------------

        [Header("Stats")]
        [Tooltip("Current hit points.")]
        [SerializeField] private float _hp = 4200f;

        [Tooltip("Max hit points.")]
        [SerializeField] private float _maxHp = 4200f;

        // Owner directive 2026-07-10: dragon damage reduced 75% (now 0.25x). Was 60/34.
        [Tooltip("Damage dealt to the Heart / a structure per swoop strike (finale).")]
        [SerializeField] private float _swoopDamage = 15f;

        [Tooltip("Damage dealt by a fire-breath pass (finale).")]
        [SerializeField] private float _breathDamage = 8.5f;

        // -- Flight tuning ---------------------------------------------------------

        [Header("Flight - orbit (finale)")]
        [Tooltip("Radius of the lazy Phase-1 orbit around the Heart (world units).")]
        [SerializeField] private float _orbitRadius = 26f;

        [Tooltip("Cruise height the dragon holds above the Heart while orbiting.")]
        [SerializeField] private float _orbitHeight = 22f;

        [Tooltip("Angular orbit speed in Phase 1 (degrees / second).")]
        [SerializeField] private float _orbitSpeed = 32f;

        [Tooltip("Forward flight speed used to drive the Animator Speed blend.")]
        [SerializeField] private float _cruiseSpeed = 14f;

        [Header("Flight - swoop (finale)")]
        [Tooltip("Lowest height the dragon reaches at the bottom of a dive-swoop.")]
        [SerializeField] private float _swoopLowHeight = 4.5f;

        [Tooltip("Seconds a full dive-and-climb swoop takes.")]
        [SerializeField] private float _swoopDuration = 3.4f;

        [Tooltip("Distance from the Heart at which a swoop counts as 'striking'.")]
        [SerializeField] private float _strikeRadius = 7f;

        [Header("Attack cadence (finale)")]
        [Tooltip("Seconds between attacks in Phase 1 (fire-breath passes).")]
        [SerializeField] private float _phase1AttackInterval = 6.5f;

        [Tooltip("Seconds between attacks in Phase 2 (mixed breath + swoop).")]
        [SerializeField] private float _phase2AttackInterval = 4.2f;

        [Tooltip("Seconds between attacks in Phase 3 (relentless swoops).")]
        [SerializeField] private float _phase3AttackInterval = 2.6f;

        // -- Fly-in / land sequence (WO-760) ---------------------------------------

        [Header("Fly-in / land sequence (WO-760)")]
        [Tooltip("Horizontal distance off-map the dragon spawns before flying in.")]
        [SerializeField] private float _approachDistance = 85f;

        [Tooltip("Altitude the dragon holds on the fly-in approach.")]
        [SerializeField] private float _approachHeight = 34f;

        [Tooltip("Forward speed during the fly-in approach.")]
        [SerializeField] private float _approachSpeed = 22f;

        [Tooltip("Horizontal distance to the town aim-point at which the dragon begins its landing descent.")]
        [SerializeField] private float _landTriggerDist = 14f;

        [Tooltip("Descent speed while landing.")]
        [SerializeField] private float _descendSpeed = 12f;

        [Tooltip("How far from the target tower the dragon sets down (metres).")]
        [SerializeField] private float _landStandoff = 7f;

        [Tooltip("Distance to the land spot that counts as 'grounded'.")]
        [SerializeField] private float _groundReachDist = 1.2f;

        [Tooltip("Seconds the takeoff climb to the orbit ring takes before the finale.")]
        [SerializeField] private float _takeoffSeconds = 1.6f;

        // -- Burn towers (WO-760) --------------------------------------------------

        [Header("Burn towers (WO-760)")]
        [Tooltip("Seconds between grounded fire attacks on a tower.")]
        [SerializeField] private float _groundBurnInterval = 2.2f;

        [Tooltip("Damage dealt to a tower per grounded fire attack.")]
        [SerializeField] private float _towerFireDamage = 22f;

        [Tooltip("Seconds of Burn applied to a tower per fire attack (if the target supports it).")]
        [SerializeField] private float _burnSeconds = 3f;

        [Tooltip("EnemyBrain-style air/land decision: horizontal distance to the tower " +
                 "beyond which the AI favours an aerial fire pass over a full grounded descent.")]
        [SerializeField] private float _airEngageDistance = 22f;

        [Tooltip("Placeholder fire impact burst played at the target on each fire attack. WO-757 replaces this with the sustained breath cone.")]
        [SerializeField] private VFXType _fireAttackVfx = VFXType.Impact_ExplosionFire;

        [Header("Death")]
        [Tooltip("Seconds the spiralling death fall takes before the dragon is destroyed.")]
        [SerializeField] private float _deathFallSeconds = 4.5f;

        // -- Phase VFX (WO-66) -----------------------------------------------------

        [Header("Phase VFX (WO-66)")]
        [Tooltip("Master toggle - play phase-transition bursts, phase auras and " +
                 "attack telegraphs through VFXManager. Off = silent (e.g. low-end).")]
        [SerializeField] private bool _phaseVfxEnabled = true;

        [Tooltip("One-shot enrage burst played at the boss when it crosses an HP threshold.")]
        [SerializeField] private VFXType _phaseTransitionVfx = VFXType.Boss_PhaseTransition;

        [Tooltip("Wind-up tell played on the boss just before a swoop / fire attack.")]
        [SerializeField] private VFXType _telegraphVfx = VFXType.Boss_Telegraph;

        [Tooltip("Oneshot impact burst at the target when a swoop / breath / fire attack lands.")]
        [SerializeField] private VFXType _strikeImpactVfx = VFXType.Boss_AttackImpact;

        [Tooltip("Oneshot burst at the boss on death.")]
        [SerializeField] private VFXType _deathVfx = VFXType.Boss_Death;

        [Tooltip("Persistent phase aura for Phase 1 (Circling) - calm.")]
        [SerializeField] private VFXType _phase1Aura = VFXType.Boss_Aura_Phase1;

        [Tooltip("Persistent phase aura for Phase 2 (Stooping) - enraged.")]
        [SerializeField] private VFXType _phase2Aura = VFXType.Boss_Aura_Phase2;

        [Tooltip("Persistent phase aura for Phase 3 (LastWing) - seething.")]
        [SerializeField] private VFXType _phase3Aura = VFXType.Boss_Aura_Phase3;

        // The single live aura loop handle - swapped (Stop old, Play new) on each
        // phase transition so only ONE aura is ever attached to the boss.
        private VFXHandle _auraHandle;

        // -- Phase thresholds (HP fraction) ----------------------------------------

        /// <summary>Phase 1 -> Phase 2 boundary - 60% HP.</summary>
        private const float Phase2Threshold = 0.60f;

        /// <summary>Phase 2 -> Phase 3 boundary - 25% HP.</summary>
        private const float Phase3Threshold = 0.25f;

        // -- Runtime refs / state --------------------------------------------------

        private Transform _anchor;                     // the Heart - the finale goal
        private IDamageableStructure _heartStructure;  // the Heart as a damageable structure
        private HeartController _heart;                 // the Heart controller (SetState hook)
        private DragonPhase _phase = DragonPhase.Circling;
        private DragonState _state = DragonState.Approaching;

        // The current fire target - a tower while burning, the Heart in the finale.
        private IDamageableStructure _currentTarget;
        private Transform _currentTargetTf;

        // Tower-destroyed subscriptions (advance-to-next signal) - one at a time.
        private DefenseTower _subbedTower;
        private ArcaneTower _subbedArcane;
        private bool _retargetNow;                     // set by a Destroyed event

        private Vector3 _landSpot;                      // ground point to set down on
        private float _retargetElapsed;                 // takeoff-climb timer
        private int _attackVariant;                     // cycles Attack1/2/3

        private float _orbitAngleDeg;                   // current position on the orbit ring
        private float _attackCooldown;
        private float _swoopElapsed;                    // >0 while a swoop is mid-flight
        private bool _swoopStruck;                      // true once the current swoop landed its hit
        private float _deathElapsed;
        private Vector3 _deathCenter;                   // where the death spiral is centred
        private bool _dead;
        private Vector3 _anchorFallback;                // home position if no anchor is wired
        private float _shownSpeed;                      // smoothed Speed pushed to the Animator

        // -- Animation -------------------------------------------------------------
        // Dragon.controller (DragonAnimatorSetup) - parameter names MUST match the
        // DragonAnim contract. Presence is cached into _params so a controller that
        // omits a param never logs a per-call "parameter not found".
        private Animator _animator;
        private static readonly int HSpeed    = Animator.StringToHash(DragonAnim.Speed);
        private static readonly int HAttack   = Animator.StringToHash(DragonAnim.Attack);
        private static readonly int HDead     = Animator.StringToHash(DragonAnim.Dead);
        private static readonly int HTakeoff  = Animator.StringToHash(DragonAnim.Takeoff);
        private static readonly int HFly      = Animator.StringToHash(DragonAnim.Fly);
        private static readonly int HLanding  = Animator.StringToHash(DragonAnim.Landing);
        private static readonly int HGrounded = Animator.StringToHash(DragonAnim.Grounded);
        private static readonly int HAttack1  = Animator.StringToHash(DragonAnim.Attack1);
        private static readonly int HAttack2  = Animator.StringToHash(DragonAnim.Attack2);
        private static readonly int HAttack3  = Animator.StringToHash(DragonAnim.Attack3);
        private readonly HashSet<int> _params = new HashSet<int>();

        // -- Events ----------------------------------------------------------------

        /// <summary>Raised when the dragon's HP reaches zero. Arg = this boss.</summary>
        public event Action<DragonBoss> Died;

        /// <summary>Raised each time the boss crosses into a new HP-aggression phase.</summary>
        public event Action<DragonPhase> PhaseChanged;

        /// <summary>Raised when a swoop / fire-breath strike lands on the Heart (finale).</summary>
        public event Action<float> StruckHeart;

        // -- Public surface --------------------------------------------------------

        /// <summary>Stable per-instance id.</summary>
        public string BossId => _bossId;

        /// <summary>Max hit points.</summary>
        public float MaxHp => _maxHp;

        /// <summary>HP as a 0..1 fraction - drives the boss HP bar.</summary>
        public float HpFraction => _maxHp > 0f ? Mathf.Clamp01(_hp / _maxHp) : 0f;

        /// <summary>The HP-aggression phase the dragon is currently in.</summary>
        public DragonPhase Phase => _phase;

        /// <summary>The behaviour-sequence state the dragon is currently in (WO-760).</summary>
        public DragonState State => _state;

        /// <summary>True once the dragon has died (HP hit zero).</summary>
        public bool IsDead => _dead;

        // -- IDamageable (Core combat seam) ----------------------------------------

        /// <summary>The dragon is hostile to the village's defenders.</summary>
        public CombatFaction Faction => CombatFaction.Hostile;

        /// <summary>
        /// ICombatLayered - the dragon is Flying while airborne. It briefly sets down
        /// to burn towers, but stays the canonical flyer in the air/ground targeting
        /// matrix (the anti-air Ballista is its counter) throughout the encounter.
        /// </summary>
        public CombatLayer Layer => CombatLayer.Flying;

        /// <summary>World position of the dragon - used by range / nearest queries.</summary>
        public Vector3 WorldPosition => transform.position;

        /// <summary>Current hit points. Non-positive means dead.</summary>
        public float Hp => _hp;

        /// <summary>True while the dragon is alive and a valid attack target.</summary>
        public bool IsAlive => !_dead && _hp > 0f;

        // -------------------------------------------------------------------------
        // Configuration
        // -------------------------------------------------------------------------

        /// <summary>
        /// Wires the dragon for an encounter. Called by the wave / encounter
        /// controller right after instantiation. Repositions the dragon to an
        /// off-map approach start so it FLIES IN (WO-760) - the caller's spawn
        /// position is only a seed for the anchor/home point.
        /// </summary>
        /// <param name="bossId">Stable per-instance id.</param>
        /// <param name="anchor">The Heart transform - the finale target + orbit centre.</param>
        /// <param name="maxHp">Optional max HP override (&lt;=0 keeps the inspector value).</param>
        public void Configure(string bossId, Transform anchor, float maxHp = 0f)
        {
            if (!string.IsNullOrEmpty(bossId)) _bossId = bossId;
            _anchor = anchor;

            if (maxHp > 0f) _maxHp = maxHp;
            _hp = _maxHp;

            // The anchor is the Heart: both the finale damage target and the state hook.
            if (anchor != null)
            {
                _heartStructure = anchor.GetComponentInParent<IDamageableStructure>();
                _heart = anchor.GetComponentInParent<HeartController>();
            }

            _dead = false;
            _deathElapsed = 0f;
            _phase = DragonPhase.Circling;
            _state = DragonState.Approaching;
            _attackCooldown = _phase1AttackInterval;
            _swoopElapsed = 0f;
            _swoopStruck = false;
            _retargetNow = false;
            _retargetElapsed = 0f;
            _currentTarget = null;
            _currentTargetTf = null;
            UnsubTower();

            // Fly-in start: off-map at altitude, on a random compass bearing from the
            // town centre, so the dragon reads as arriving from the horizon.
            Vector3 centre = AnchorPosition();
            float ang = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            transform.position = centre + dir * _approachDistance + Vector3.up * _approachHeight;
            SetGroundedFlags(false);

            FlowTrace.Step("DragonBoss",
                $"'{_bossId}' Configure -> Approaching from {transform.position} " +
                $"(town centre {centre}, maxHp {_maxHp:0}).");

            // Boss-fight phase VFX - attach the calm Phase-1 aura now.
            PlayPhaseAura(_phase);
        }

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------

        private void Awake()
        {
            EnsureAnimator();
            EnsureHitCollider();
            _anchorFallback = transform.position;
            _orbitAngleDeg = UnityEngine.Random.Range(0f, 360f);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_dead)
            {
                TickDeathFall(dt);
                DriveAnimator(0f);
                return;
            }

            ResolvePhase();   // HP-aggression phase (aura + boss-bar label)

            float frameSpeed;
            switch (_state)
            {
                case DragonState.Approaching:  frameSpeed = TickApproach(dt);     break;
                case DragonState.Landing:      frameSpeed = TickLanding(dt);      break;
                case DragonState.BurnTowers:   frameSpeed = TickBurnTowers(dt);   break;
                case DragonState.AirAttack:    frameSpeed = TickAirAttack(dt);    break;
                case DragonState.RetargetTree: frameSpeed = TickRetargetTree(dt); break;
                default:                       frameSpeed = TickFinale(dt);       break;
            }

            DriveAnimator(frameSpeed);
        }

        // -------------------------------------------------------------------------
        // Sequence: Approaching (fly-in)
        // -------------------------------------------------------------------------

        /// <summary>
        /// Flies in from the off-map spawn toward the town aim-point (the nearest live
        /// tower, or the Heart if there are none), descending from the approach height.
        /// Begins the landing descent once within <see cref="_landTriggerDist"/>.
        /// </summary>
        private float TickApproach(float dt)
        {
            SetGroundedFlags(false);

            Vector3 aim = TownAimPos();
            Vector3 goal = new Vector3(aim.x, AnchorPosition().y + _orbitHeight, aim.z);

            Vector3 prev = transform.position;
            transform.position = Vector3.MoveTowards(prev, goal, _approachSpeed * dt);
            FaceTravel(transform.position - prev);

            Vector3 flat = transform.position - aim;
            flat.y = 0f;
            if (flat.magnitude <= _landTriggerDist)
                EnterTowerEngagement(aim);

            return _approachSpeed;
        }

        // -------------------------------------------------------------------------
        // Tower engagement - EnemyBrain-style air/land decision (per attack)
        // -------------------------------------------------------------------------

        /// <summary>
        /// Arrival at the town: acquire the nearest live tower and let the AI brain
        /// open on it - an aerial fire pass or a grounded burn. No towers present
        /// (already levelled) skips straight to the Heart.
        /// </summary>
        private void EnterTowerEngagement(Vector3 aimPos)
        {
            if (!NearestAliveTower(out var s, out var mb, out var tp))
            {
                EnterRetargetTree("no towers present on arrival");
                return;
            }
            SetTowerTarget(s, mb);
            BeginTowerAttack(tp);
        }

        /// <summary>
        /// The per-attack AIR-vs-LAND CHOICE. Reads <see cref="DecideAttackMode"/> and
        /// routes to an aerial fire pass (<see cref="EnterAirAttack"/>) or a grounded
        /// descent+burn (<see cref="EnterLanding"/>). Called on arrival, after every
        /// air pass, and each time the grounded loop re-evaluates - so the attack style
        /// is a live AI decision, never a scripted order.
        /// </summary>
        private void BeginTowerAttack(Vector3 towerPos)
        {
            if (DecideAttackMode(towerPos) == DragonAttackMode.Air)
                EnterAirAttack(towerPos);
            else
                EnterLanding(towerPos);
        }

        /// <summary>
        /// The AI decision hook - mirrors <see cref="EnemyBrain"/>.UpdateTacticalState /
        /// ArchetypeDefaultState: a priority-ordered read of HP-phase aggression then
        /// engagement geometry that returns a posture enum, in place of a fixed order.
        /// Fully guarded; the fail-safe default is <see cref="DragonAttackMode.Land"/>
        /// (the WO-760 sane behaviour) so an undecidable state never freezes the dragon.
        /// </summary>
        private DragonAttackMode DecideAttackMode(Vector3 targetPos)
        {
            DragonAttackMode mode = DragonAttackMode.Land;   // fail-safe default
            Guard.Try("DragonBoss", "decide air/land attack mode", () =>
            {
                Vector3 flat = targetPos - transform.position;
                flat.y = 0f;
                float dist = flat.magnitude;

                // 1) HP-phase aggression: the seething LastWing phase drives relentless
                //    aerial swoops (mirror of EnemyBrain's low-HP posture switch).
                if (_phase == DragonPhase.LastWing) { mode = DragonAttackMode.Air; return; }

                // 2) Engagement geometry: a distant tower is reached faster by an air
                //    pass than a full descent-and-ground commit (mirror of Rush/Kite by
                //    range in ComputeTacticalDestination).
                if (dist > _airEngageDistance) { mode = DragonAttackMode.Air; return; }

                // 3) Enraged Stooping phase mixes both so it uses air AND land.
                if (_phase == DragonPhase.Stooping)
                {
                    mode = (_attackVariant % 2 == 0) ? DragonAttackMode.Air : DragonAttackMode.Land;
                    return;
                }

                // 4) Calm Circling + close target: land and burn (WO-760 default).
                mode = DragonAttackMode.Land;
            });
            return mode;
        }

        /// <summary>
        /// Begins an airborne diving fire pass on the current tower. Triggers a takeoff
        /// (harmless if already airborne) and reuses the swoop timer machinery; the pass
        /// itself is driven by <see cref="TickAirAttack"/>.
        /// </summary>
        private void EnterAirAttack(Vector3 towerPos)
        {
            SetGroundedFlags(false);
            AnimTrigger(HTakeoff);   // lifts off if grounded; a no-op trigger if already flying
            AnimTrigger(HAttack);
            PlayTelegraph();
            _swoopElapsed = Mathf.Epsilon;
            _swoopStruck = false;
            _state = DragonState.AirAttack;
            FlowTrace.Step("DragonBoss", $"'{_bossId}' -> AirAttack (aerial fire pass) on tower at {towerPos}.");
        }

        /// <summary>
        /// Drives one airborne diving fire pass at the current tower: a cruise-height ->
        /// low-over-tower -> climb arc that fires the shared tower payload
        /// (<see cref="FireAtTowerCore"/>) at the low point. Advances to the next tower
        /// (or the Heart) when the current one falls; re-runs the air/land decision at
        /// the end of each pass so the style stays dynamic.
        /// </summary>
        private float TickAirAttack(float dt)
        {
            SetGroundedFlags(false);

            // Tower fell (Destroyed event or dead) -> advance to the next, or take the
            // fight to the Heart. Same advance/retarget gate as the grounded loop.
            if (_retargetNow || _currentTarget == null || !_currentTarget.IsAlive)
            {
                _retargetNow = false;
                _swoopElapsed = 0f;
                _swoopStruck = false;
                if (NearestAliveTower(out var s, out var mb, out var np))
                {
                    SetTowerTarget(s, mb);
                    BeginTowerAttack(np);   // re-decide air/land for the new tower
                }
                else
                {
                    EnterRetargetTree("all towers destroyed (air)");
                }
                return _cruiseSpeed * 1.8f;
            }

            Vector3 tp = _currentTargetTf != null ? _currentTargetTf.position : AnchorPosition();

            _swoopElapsed += dt;
            float t = Mathf.Clamp01(_swoopElapsed / _swoopDuration);

            // Dive arc: cruise height at the ends, low over the tower at mid-pass.
            float arc = 1f - 4f * (t - 0.5f) * (t - 0.5f);
            float cruiseY = AnchorPosition().y + _orbitHeight;
            float lowY = tp.y + _swoopLowHeight;
            float wantY = Mathf.Lerp(cruiseY, lowY, arc);

            Vector3 prev = transform.position;
            Vector3 hereXZ = new Vector3(prev.x, 0f, prev.z);
            Vector3 tpXZ = new Vector3(tp.x, 0f, tp.z);
            Vector3 nextXZ = Vector3.MoveTowards(hereXZ, tpXZ, _cruiseSpeed * 1.8f * dt);
            float nextY = Mathf.MoveTowards(prev.y, wantY, _descendSpeed * 2f * dt);
            transform.position = new Vector3(nextXZ.x, nextY, nextXZ.z);
            FaceTravel(transform.position - prev);

            // Strike at the low point of the pass - reuse the ONE fire payload.
            if (!_swoopStruck && arc > 0.85f)
            {
                Vector3 f = transform.position - tp;
                f.y = 0f;
                if (f.magnitude <= _strikeRadius * 1.6f)
                {
                    _swoopStruck = true;
                    FireAtTowerCore(tp);
                }
            }

            // Pass complete -> AI re-decides (another air pass, or land and burn).
            if (t >= 1f)
            {
                _swoopElapsed = 0f;
                _swoopStruck = false;
                BeginTowerAttack(tp);
            }
            return _cruiseSpeed * 1.8f;
        }

        /// <summary>Begins the landing descent onto a ground spot beside <paramref name="aimPos"/>.</summary>
        private void EnterLanding(Vector3 aimPos)
        {
            _landSpot = LandSpotNear(aimPos);
            AnimTrigger(HLanding);
            _state = DragonState.Landing;
            FlowTrace.Step("DragonBoss", $"'{_bossId}' Approaching -> Landing at {_landSpot}.");
        }

        // -------------------------------------------------------------------------
        // Sequence: Landing
        // -------------------------------------------------------------------------

        /// <summary>
        /// Descends to <see cref="_landSpot"/> and settles grounded. On touchdown it
        /// enters BurnTowers if any tower is alive, else goes straight to RetargetTree.
        /// </summary>
        private float TickLanding(float dt)
        {
            Vector3 prev = transform.position;
            transform.position = Vector3.MoveTowards(prev, _landSpot, _descendSpeed * dt);
            FaceTravel(transform.position - prev);

            if ((transform.position - _landSpot).sqrMagnitude <= _groundReachDist * _groundReachDist)
            {
                SetGroundedFlags(true);
                if (NearestAliveTower(out var s, out var mb, out _))
                {
                    SetTowerTarget(s, mb);
                    _attackCooldown = 0.4f;   // a beat before the first fire attack
                    _state = DragonState.BurnTowers;
                    FlowTrace.Step("DragonBoss",
                        $"'{_bossId}' Landing -> BurnTowers (first target '{(mb != null ? mb.name : "<tower>")}').");
                }
                else
                {
                    EnterRetargetTree("no towers present at touchdown");
                }
            }

            return _descendSpeed;
        }

        // -------------------------------------------------------------------------
        // Sequence: BurnTowers (grounded)
        // -------------------------------------------------------------------------

        /// <summary>
        /// Grounded fire loop: faces the current tower and fire-attacks it on a
        /// cadence. Advances to the nearest remaining tower when the current one dies
        /// (driven by the Destroyed event or an IsAlive re-check). Once no tower is
        /// alive it takes off and retargets the Heart.
        /// </summary>
        private float TickBurnTowers(float dt)
        {
            SetGroundedFlags(true);

            if (_retargetNow || _currentTarget == null || !_currentTarget.IsAlive)
            {
                _retargetNow = false;
                if (NearestAliveTower(out var s, out var mb, out _))
                {
                    SetTowerTarget(s, mb);
                }
                else
                {
                    EnterRetargetTree("all towers destroyed");
                    return 0f;
                }
            }

            Vector3 tp = _currentTargetTf != null ? _currentTargetTf.position : AnchorPosition();
            FacePoint(tp, dt);

            _attackCooldown -= dt;
            if (_attackCooldown <= 0f)
            {
                _attackCooldown = _groundBurnInterval;

                // Per-attack AI choice (EnemyBrain-style): keep burning on the ground,
                // or take off for an aerial pass if the brain now favours air (the phase
                // dropped, or the advanced-to tower is far). Dynamic, not scripted.
                if (DecideAttackMode(tp) == DragonAttackMode.Air)
                {
                    EnterAirAttack(tp);
                    return 0f;
                }

                FireAtTower(tp);
            }

            return 0f;   // grounded - Speed near zero so the idle/grounded pose plays
        }

        /// <summary>
        /// One grounded fire attack on the current tower: an Attack1/2/3 clip, a fire
        /// impact burst at the target, contact damage, and a Burn if the target
        /// supports it. WO-757 replaces the placeholder impact with the breath cone.
        /// </summary>
        private void FireAtTower(Vector3 targetPos)
        {
            // Cycle the three grounded attack clips for variety, then fire the payload.
            _attackVariant = (_attackVariant + 1) % 3;
            switch (_attackVariant)
            {
                case 0:  AnimTrigger(HAttack1); break;
                case 1:  AnimTrigger(HAttack2); break;
                default: AnimTrigger(HAttack3); break;
            }

            FireAtTowerCore(targetPos);
        }

        /// <summary>
        /// The shared fire payload - telegraph, fire VFX, contact damage + Burn, roar,
        /// trace - used by BOTH the grounded burn (<see cref="FireAtTower"/>, which adds
        /// the ground clip) and the aerial pass (<see cref="TickAirAttack"/>, which plays
        /// its own Attack trigger). ONE VFXManager pool - no second VFX stack.
        /// </summary>
        private void FireAtTowerCore(Vector3 targetPos)
        {
            PlayTelegraph();

            // FIRE VFX (placeholder). Everything routes through the single VFXManager
            // pool - no second VFX stack (WO-760 landmine).
            // WO-757: Boss_FireBreath cone slots in here (a sustained mouth-socket cone
            // aimed at the tower) to replace these one-shot impact bursts.
            if (_phaseVfxEnabled)
            {
                if (_fireAttackVfx != VFXType.None) VFXManager.Play(_fireAttackVfx, targetPos);
                if (_strikeImpactVfx != VFXType.None) VFXManager.Play(_strikeImpactVfx, targetPos);
            }

            if (_currentTarget != null && _currentTarget.IsAlive)
                _currentTarget.ApplyContactDamage(_towerFireDamage);

            TryApplyBurn(_currentTargetTf);

            DeNelle.Village.GameSfx.PlayDragonRoar();

            FlowTrace.Throttle("DragonBoss", $"burn:{GetInstanceID()}", 1f,
                $"'{_bossId}' fire-attacks tower for {_towerFireDamage:0.#} (variant {_attackVariant + 1}).");
        }

        /// <summary>
        /// Applies Burn to the target if it supports the <see cref="IDamageable"/>
        /// status seam. Towers are <see cref="IDamageableStructure"/> (no ApplyStatus),
        /// so this is a guarded no-op for them today - forward-compatible for a burnable
        /// tower. The dragon's contact damage always lands regardless.
        /// </summary>
        private void TryApplyBurn(Transform targetTf)
        {
            if (targetTf == null || _burnSeconds <= 0f) return;
            Guard.Try("DragonBoss", "apply burn to fire target", () =>
            {
                var dmg = targetTf.GetComponentInParent<IDamageable>();
                if (dmg != null) dmg.ApplyStatus(StatusEffect.Burn, _burnSeconds);
            });
        }

        // -------------------------------------------------------------------------
        // Sequence: RetargetTree (takeoff -> Heart)
        // -------------------------------------------------------------------------

        /// <summary>
        /// Takes off, retargets the Heart of Elarion, flips the Heart into its Boss
        /// state, and starts the climb to the orbit ring. The finale resumes once the
        /// climb completes.
        /// </summary>
        private void EnterRetargetTree(string why)
        {
            UnsubTower();
            _currentTarget = _heartStructure;
            _currentTargetTf = _anchor;
            if (_heart != null) _heart.SetState(HeartState.Boss);

            SetGroundedFlags(false);
            AnimTrigger(HTakeoff);
            _retargetElapsed = 0f;
            _state = DragonState.RetargetTree;

            FlowTrace.Step("DragonBoss",
                $"'{_bossId}' BurnTowers -> RetargetTree ({why}) - takeoff, Heart -> Boss, finale next.");
        }

        /// <summary>Climbs from the ground back up to the orbit ring, then hands to the finale.</summary>
        private float TickRetargetTree(float dt)
        {
            SetGroundedFlags(false);

            Vector3 centre = AnchorPosition();
            Vector3 target = new Vector3(transform.position.x, centre.y + _orbitHeight, transform.position.z);
            Vector3 prev = transform.position;
            transform.position = Vector3.MoveTowards(prev, target, _cruiseSpeed * 1.2f * dt);
            FaceTravel(transform.position - prev);

            _retargetElapsed += dt;
            bool atRing = Mathf.Abs(transform.position.y - (centre.y + _orbitHeight)) < 3f;
            if (_retargetElapsed >= _takeoffSeconds && atRing)
            {
                // Seed the orbit angle from the current position so the finale orbit
                // continues smoothly from where the climb ended.
                Vector3 d = transform.position - centre;
                _orbitAngleDeg = Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg;
                _attackCooldown = CurrentAttackInterval();
                _state = DragonState.Finale;
                FlowTrace.Step("DragonBoss", $"'{_bossId}' RetargetTree -> Finale (orbit + swoop on the Heart).");
            }

            return _cruiseSpeed * 1.2f;
        }

        // -------------------------------------------------------------------------
        // Sequence: Finale (the original orbit / swoop / fire-breath, on the Heart)
        // -------------------------------------------------------------------------

        /// <summary>
        /// The finale: orbit the Heart, punctuated by dive-swoops and fire-breath
        /// passes - the original apex-boss behaviour, now retargeted onto
        /// <see cref="_currentTarget"/> (the Heart). HP still gates the aggression.
        /// </summary>
        private float TickFinale(float dt)
        {
            float frameSpeed = _swoopElapsed > 0f ? TickSwoop(dt) : TickOrbit(dt);
            TickAttackCadence(dt);
            return frameSpeed;
        }

        // -------------------------------------------------------------------------
        // Target enumeration + selection
        // -------------------------------------------------------------------------

        /// <summary>
        /// The horizontal aim-point for the fly-in / land: the nearest live tower's
        /// position, or the Heart if there are none.
        /// </summary>
        private Vector3 TownAimPos()
        {
            if (NearestAliveTower(out _, out _, out var p)) return p;
            return AnchorPosition();
        }

        /// <summary>
        /// Finds the nearest live defensive tower (<see cref="DefenseTower"/> or
        /// <see cref="ArcaneTower"/>) by the established <c>IsAlive</c> filter. Returns
        /// false when no tower is alive - the "all towers destroyed" gate.
        /// </summary>
        private bool NearestAliveTower(out IDamageableStructure best, out MonoBehaviour bestMb, out Vector3 bestPos)
        {
            best = null;
            bestMb = null;
            bestPos = default;
            float bestSqr = float.MaxValue;
            Vector3 here = transform.position;

            foreach (var t in FindObjectsByType<DefenseTower>(FindObjectsSortMode.None))
            {
                if (t == null || !t.IsAlive) continue;
                float sqr = (t.transform.position - here).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = t; bestMb = t; bestPos = t.transform.position; }
            }
            foreach (var a in FindObjectsByType<ArcaneTower>(FindObjectsSortMode.None))
            {
                if (a == null || !a.IsAlive) continue;
                float sqr = (a.transform.position - here).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = a; bestMb = a; bestPos = a.transform.position; }
            }
            return best != null;
        }

        /// <summary>
        /// Sets the current fire target to a tower and subscribes to its Destroyed
        /// event so the burn loop advances the instant it falls. Unsubscribes from any
        /// previous tower first (one live subscription at a time).
        /// </summary>
        private void SetTowerTarget(IDamageableStructure structure, MonoBehaviour mb)
        {
            UnsubTower();
            _currentTarget = structure;
            _currentTargetTf = mb != null ? mb.transform : null;

            if (mb is DefenseTower dt) { dt.Destroyed += OnTowerDestroyed; _subbedTower = dt; }
            else if (mb is ArcaneTower at) { at.Destroyed += OnArcaneDestroyed; _subbedArcane = at; }
        }

        /// <summary>Drops both tower Destroyed subscriptions, if any.</summary>
        private void UnsubTower()
        {
            if (_subbedTower != null) { _subbedTower.Destroyed -= OnTowerDestroyed; _subbedTower = null; }
            if (_subbedArcane != null) { _subbedArcane.Destroyed -= OnArcaneDestroyed; _subbedArcane = null; }
        }

        private void OnTowerDestroyed(DefenseTower t) => _retargetNow = true;
        private void OnArcaneDestroyed(ArcaneTower t) => _retargetNow = true;

        /// <summary>
        /// A ground point beside <paramref name="targetPos"/>, offset back toward the
        /// dragon by <see cref="_landStandoff"/> and dropped to the sampled ground
        /// height - so the dragon sets down next to the tower, not on top of it.
        /// </summary>
        private Vector3 LandSpotNear(Vector3 targetPos)
        {
            Vector3 back = transform.position - targetPos;
            back.y = 0f;
            if (back.sqrMagnitude < 0.01f) back = Vector3.back;
            Vector3 xz = targetPos + back.normalized * _landStandoff;
            float gy = SampleGroundY(xz);
            return new Vector3(xz.x, gy, xz.z);
        }

        /// <summary>
        /// Guarded downward raycast for a ground height at <paramref name="xz"/>;
        /// falls back to y = 0 (NO NavMesh dependency - WO-760). The XZ's own y is
        /// ignored; the cast starts well above it.
        /// </summary>
        private float SampleGroundY(Vector3 xz)
        {
            float y = 0f;
            Guard.Try("DragonBoss", "sample ground height", () =>
            {
                Vector3 from = new Vector3(xz.x, transform.position.y + 50f, xz.z);
                if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 400f,
                        ~0, QueryTriggerInteraction.Ignore))
                    y = hit.point.y;
            });
            return y;
        }

        // -------------------------------------------------------------------------
        // Phase resolution (HP aggression - aura + boss-bar label)
        // -------------------------------------------------------------------------

        /// <summary>
        /// Re-derives the HP-aggression phase from current HP and raises
        /// <see cref="PhaseChanged"/> on a transition. This gates the phase auras and
        /// the boss-bar label; it does NOT drive the behaviour sequence (that is
        /// <see cref="DragonState"/>).
        /// </summary>
        private void ResolvePhase()
        {
            float frac = HpFraction;
            DragonPhase next;

            if (frac > Phase2Threshold) next = DragonPhase.Circling;
            else if (frac > Phase3Threshold) next = DragonPhase.Stooping;
            else next = DragonPhase.LastWing;

            if (next != _phase)
            {
                DragonPhase prev = _phase;
                _phase = next;
                FlowTrace.Step("DragonBoss",
                    $"'{_bossId}' phase {prev} -> {_phase} at HP {_hp:0.#}/{_maxHp:0.#} ({HpFraction:P0}).");
                PlayPhaseTransition();
                PlayPhaseAura(_phase);
                PhaseChanged?.Invoke(_phase);
            }
        }

        /// <summary>Attack interval for the current phase (finale cadence).</summary>
        private float CurrentAttackInterval()
        {
            switch (_phase)
            {
                case DragonPhase.Stooping: return _phase2AttackInterval;
                case DragonPhase.LastWing: return _phase3AttackInterval;
                default:                   return _phase1AttackInterval;
            }
        }

        /// <summary>Orbit speed for the current phase - quickens as HP falls.</summary>
        private float CurrentOrbitSpeed()
        {
            switch (_phase)
            {
                case DragonPhase.Stooping: return _orbitSpeed * 1.45f;
                case DragonPhase.LastWing: return _orbitSpeed * 1.9f;
                default:                   return _orbitSpeed;
            }
        }

        /// <summary>Orbit radius for the current phase - tighter in later phases.</summary>
        private float CurrentOrbitRadius()
        {
            switch (_phase)
            {
                case DragonPhase.Stooping: return _orbitRadius * 0.78f;
                case DragonPhase.LastWing: return _orbitRadius * 0.6f;
                default:                   return _orbitRadius;
            }
        }

        // -------------------------------------------------------------------------
        // Finale flight - orbit
        // -------------------------------------------------------------------------

        /// <summary>
        /// Advances one frame along the circular orbit around the Heart at cruise
        /// height, banking to face travel. Returns the flight speed for the blend.
        /// </summary>
        private float TickOrbit(float dt)
        {
            Vector3 centre = AnchorPosition();

            _orbitAngleDeg += CurrentOrbitSpeed() * dt;
            if (_orbitAngleDeg >= 360f) _orbitAngleDeg -= 360f;

            float rad = _orbitAngleDeg * Mathf.Deg2Rad;
            float r = CurrentOrbitRadius();
            Vector3 target = centre + new Vector3(
                Mathf.Cos(rad) * r,
                _orbitHeight,
                Mathf.Sin(rad) * r);

            Vector3 prev = transform.position;
            transform.position = Vector3.MoveTowards(prev, target, _cruiseSpeed * dt);

            FaceTravel(transform.position - prev);
            return _cruiseSpeed;
        }

        // -------------------------------------------------------------------------
        // Finale flight - swoop (the dive attack)
        // -------------------------------------------------------------------------

        /// <summary>Begins a dive-swoop toward the Heart. No-op if one is already running.</summary>
        private void BeginSwoop()
        {
            if (_swoopElapsed > 0f) return;
            _swoopElapsed = Mathf.Epsilon;
            _swoopStruck = false;
            AnimTrigger(HAttack);
            PlayTelegraph();
            DeNelle.Village.GameSfx.PlayDragonRoar();
        }

        /// <summary>
        /// Advances an in-progress dive-swoop toward the Heart and back up, dealing
        /// <see cref="_swoopDamage"/> at the low point. Returns flight speed.
        /// </summary>
        private float TickSwoop(float dt)
        {
            _swoopElapsed += dt;
            float t = Mathf.Clamp01(_swoopElapsed / _swoopDuration);

            Vector3 centre = AnchorPosition();

            float arc = 1f - 4f * (t - 0.5f) * (t - 0.5f);
            float height = Mathf.Lerp(_orbitHeight, _swoopLowHeight, arc);

            float r = Mathf.Lerp(CurrentOrbitRadius(), _strikeRadius * 0.5f, arc);
            float rad = _orbitAngleDeg * Mathf.Deg2Rad;
            _orbitAngleDeg += CurrentOrbitSpeed() * 0.5f * dt;

            Vector3 target = centre + new Vector3(
                Mathf.Cos(rad) * r,
                height,
                Mathf.Sin(rad) * r);

            Vector3 prev = transform.position;
            float swoopSpeed = _cruiseSpeed * 1.8f;
            transform.position = Vector3.MoveTowards(prev, target, swoopSpeed * dt);
            FaceTravel(transform.position - prev);

            if (!_swoopStruck && arc > 0.85f)
            {
                Vector3 flat = transform.position - centre;
                flat.y = 0f;
                if (flat.magnitude <= _strikeRadius)
                {
                    _swoopStruck = true;
                    DealStrike(_swoopDamage);
                }
            }

            if (t >= 1f)
            {
                _swoopElapsed = 0f;
                _swoopStruck = false;
            }
            return swoopSpeed;
        }

        // -------------------------------------------------------------------------
        // Finale attacks
        // -------------------------------------------------------------------------

        /// <summary>Counts down to the next finale attack (fire-breath / dive-swoop).</summary>
        private void TickAttackCadence(float dt)
        {
            if (_swoopElapsed > 0f) return;

            _attackCooldown -= dt;
            if (_attackCooldown > 0f) return;

            _attackCooldown = CurrentAttackInterval();

            switch (_phase)
            {
                case DragonPhase.Circling:
                    if (UnityEngine.Random.value < 0.55f) BeginSwoop();
                    else FireBreath();
                    break;

                case DragonPhase.Stooping:
                    if (UnityEngine.Random.value < 0.65f) BeginSwoop();
                    else FireBreath();
                    break;

                default: // LastWing - relentless swoops
                    BeginSwoop();
                    break;
            }
        }

        /// <summary>A stationary fire-breath pass on the current target (the Heart).</summary>
        private void FireBreath()
        {
            AnimTrigger(HAttack);
            PlayTelegraph();
            DealStrike(_breathDamage);
        }

        /// <summary>
        /// Applies <paramref name="amount"/> damage to the current target structure
        /// (the Heart in the finale) and raises <see cref="StruckHeart"/> so the
        /// encounter controller can react (camera shake, Heart threat state).
        /// </summary>
        private void DealStrike(float amount)
        {
            IDamageableStructure tgt = _currentTarget ?? _heartStructure;
            if (tgt != null && tgt.IsAlive)
                tgt.ApplyContactDamage(amount);

            if (_phaseVfxEnabled && _strikeImpactVfx != VFXType.None)
                VFXManager.Play(_strikeImpactVfx, TargetPosition());

            StruckHeart?.Invoke(amount);
        }

        // -------------------------------------------------------------------------
        // HP / death
        // -------------------------------------------------------------------------

        /// <summary>Applies <paramref name="amount"/> damage. At zero HP the dragon dies.</summary>
        public void TakeDamage(float amount, DamageElement element)
        {
            if (_dead || amount <= 0f) return;
            _hp = Mathf.Max(0f, _hp - amount);
            FlowTrace.Throttle("DragonBoss", $"hit:{GetInstanceID()}", 1f,
                $"'{_bossId}' took {amount:0.#} {element} dmg -> HP {_hp:0.#}/{_maxHp:0.#} " +
                $"({HpFraction:P0}, phase {_phase}, state {_state}).");
            if (_hp <= 0f) Die();
        }

        /// <summary>
        /// Status effects - a logged no-op for the dragon RECEIVING them. The dragon
        /// applies Burn to towers; its own kinematic flight is not affected by
        /// ground-target statuses. Kept to satisfy the <see cref="IDamageable"/> contract.
        /// </summary>
        public void ApplyStatus(StatusEffect effect, float seconds)
        {
            // Intentionally inert - the dragon does not take ground statuses.
        }

        /// <summary>Kills the dragon immediately (e.g. an encounter time-out).</summary>
        public void Kill()
        {
            if (!_dead) Die();
        }

        private void Die()
        {
            _dead = true;
            _phase = DragonPhase.Falling;
            _swoopElapsed = 0f;
            _deathElapsed = 0f;
            _deathCenter = transform.position;   // spiral where it died, not at the Heart
            UnsubTower();
            AnimBool(HDead, true);

            StopPhaseAura();
            if (_phaseVfxEnabled && _deathVfx != VFXType.None)
                VFXManager.Play(_deathVfx, transform.position);

            Died?.Invoke(this);
            PhaseChanged?.Invoke(DragonPhase.Falling);
        }

        /// <summary>
        /// Drives the spiralling death fall - the dragon corkscrews down to the ground
        /// over <see cref="_deathFallSeconds"/> around where it died, then is destroyed.
        /// </summary>
        private void TickDeathFall(float dt)
        {
            _deathElapsed += dt;
            float t = Mathf.Clamp01(_deathElapsed / _deathFallSeconds);

            Vector3 centre = _deathCenter;

            _orbitAngleDeg += 220f * dt;
            float rad = _orbitAngleDeg * Mathf.Deg2Rad;
            float r = Mathf.Lerp(6f, 1.5f, t);
            float height = Mathf.Lerp(transform.position.y, 0.5f, t * t);

            transform.position = new Vector3(
                centre.x + Mathf.Cos(rad) * r,
                Mathf.Max(0.5f, height),
                centre.z + Mathf.Sin(rad) * r);

            float pitch = Mathf.Lerp(0f, 70f, t);
            transform.rotation = Quaternion.Euler(pitch, _orbitAngleDeg, Mathf.Sin(t * 12f) * 18f);

            if (t >= 1f)
                Destroy(gameObject);
        }

        // -------------------------------------------------------------------------
        // Animation
        // -------------------------------------------------------------------------

        /// <summary>Feeds the smoothed Speed float so the controller blends Idle &lt;-&gt; Fly.</summary>
        private void DriveAnimator(float rawSpeed)
        {
            if (_animator == null) return;
            _shownSpeed = Mathf.Lerp(_shownSpeed, rawSpeed, Time.deltaTime * 4f);
            AnimFloat(HSpeed, _shownSpeed);
        }

        /// <summary>Sets the airborne/grounded animator bools (presence-guarded).</summary>
        private void SetGroundedFlags(bool grounded)
        {
            AnimBool(HFly, !grounded);
            AnimBool(HGrounded, grounded);
        }

        private void AnimTrigger(int hash)
        {
            if (_animator != null && _params.Contains(hash)) _animator.SetTrigger(hash);
        }

        private void AnimBool(int hash, bool value)
        {
            if (_animator != null && _params.Contains(hash)) _animator.SetBool(hash, value);
        }

        private void AnimFloat(int hash, float value)
        {
            if (_animator != null && _params.Contains(hash)) _animator.SetFloat(hash, value);
        }

        // -------------------------------------------------------------------------
        // Phase VFX (WO-66) - all through the canonical VFXManager (one pool).
        // -------------------------------------------------------------------------

        /// <summary>The looping aura VFXType for the given HP-aggression phase.</summary>
        private VFXType AuraForPhase(DragonPhase phase)
        {
            switch (phase)
            {
                case DragonPhase.Stooping: return _phase2Aura;
                case DragonPhase.LastWing: return _phase3Aura;
                default:                   return _phase1Aura;
            }
        }

        /// <summary>Swaps the persistent phase aura to the one for <paramref name="phase"/>.</summary>
        private void PlayPhaseAura(DragonPhase phase)
        {
            if (!_phaseVfxEnabled) return;
            var mgr = VFXManager.Instance;
            if (mgr == null) return;

            VFXType type = AuraForPhase(phase);
            if (type == VFXType.None) { StopPhaseAura(); return; }

            StopPhaseAura();
            _auraHandle = mgr.PlayAura(type, transform);
        }

        /// <summary>Stops and clears the live phase aura handle, if any.</summary>
        private void StopPhaseAura()
        {
            if (_auraHandle != null)
            {
                if (_auraHandle.IsAlive) _auraHandle.Stop();
                _auraHandle = null;
            }
        }

        /// <summary>Oneshot enrage burst at the boss when it crosses an HP threshold.</summary>
        private void PlayPhaseTransition()
        {
            if (!_phaseVfxEnabled || _phaseTransitionVfx == VFXType.None) return;
            VFXManager.Play(_phaseTransitionVfx, transform.position);
        }

        /// <summary>Wind-up telegraph burst on the boss just before a special attack.</summary>
        private void PlayTelegraph()
        {
            if (!_phaseVfxEnabled || _telegraphVfx == VFXType.None) return;
            VFXManager.Play(_telegraphVfx, transform.position, transform.rotation);
        }

        /// <summary>Tear down the live aura loop + tower subs so nothing leaks.</summary>
        private void OnDisable()
        {
            StopPhaseAura();
            UnsubTower();
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        /// <summary>The town centre / orbit centre - the wired Heart, or the spawn home point.</summary>
        private Vector3 AnchorPosition()
        {
            return _anchor != null ? _anchor.position : _anchorFallback;
        }

        /// <summary>The current fire target's world position (Heart in the finale).</summary>
        private Vector3 TargetPosition()
        {
            return _currentTargetTf != null ? _currentTargetTf.position : AnchorPosition();
        }

        /// <summary>Rotates the dragon to face its direction of travel (yaw-biased).</summary>
        private void FaceTravel(Vector3 delta)
        {
            delta.y *= 0.5f;
            if (delta.sqrMagnitude < 1e-5f) return;
            Quaternion want = Quaternion.LookRotation(delta.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, want, Time.deltaTime * 4f);
        }

        /// <summary>Rotates the dragon to face a world point (yaw only) - grounded aiming.</summary>
        private void FacePoint(Vector3 point, float dt)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 1e-4f) return;
            Quaternion want = Quaternion.LookRotation(d.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, want, dt * 4f);
        }

        /// <summary>
        /// Resolves the Animator on the dragon rig (children searched) and caches which
        /// DragonAnim params the controller actually declares, so a Set on an absent
        /// param is skipped instead of logging every call.
        /// </summary>
        private void EnsureAnimator()
        {
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();

            _params.Clear();
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                foreach (var p in _animator.parameters)
                    _params.Add(p.nameHash);
            }
        }

        /// <summary>
        /// Guarantees a NON-TRIGGER collider on the dragon so an air-defense structure's
        /// ray / projectile query can HIT it (mirrors DefenseTower.EnsureContactCollider).
        /// Idempotent: skips if the rig already carries a solid collider.
        /// </summary>
        private void EnsureHitCollider()
        {
            foreach (var c in GetComponentsInChildren<Collider>(true))
                if (c != null && !c.isTrigger) return;   // already hittable

            var rends = GetComponentsInChildren<Renderer>(true);
            var sc = gameObject.AddComponent<SphereCollider>();
            sc.isTrigger = false;

            if (rends != null && rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                sc.center = transform.InverseTransformPoint(b.center);
                Vector3 ext = b.extents;
                float maxExt = Mathf.Max(ext.x, Mathf.Max(ext.y, ext.z));
                Vector3 ls = transform.lossyScale;
                float sMax = Mathf.Max(0.01f,
                    Mathf.Max(Mathf.Abs(ls.x), Mathf.Max(Mathf.Abs(ls.y), Mathf.Abs(ls.z))));
                sc.radius = Mathf.Max(0.5f, maxExt / sMax);
            }
            else
            {
                sc.center = Vector3.zero;
                sc.radius = 2.5f;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 c = Application.isPlaying ? AnchorPosition() : transform.position;
            Gizmos.color = new Color(0.85f, 0.35f, 0.15f, 0.7f);
            const int seg = 48;
            Vector3 prev = c + new Vector3(_orbitRadius, _orbitHeight, 0f);
            for (int i = 1; i <= seg; i++)
            {
                float a = (i / (float)seg) * Mathf.PI * 2f;
                Vector3 p = c + new Vector3(
                    Mathf.Cos(a) * _orbitRadius, _orbitHeight, Mathf.Sin(a) * _orbitRadius);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
            Gizmos.color = new Color(0.95f, 0.2f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(c, _strikeRadius);
        }
#endif
    }
}
