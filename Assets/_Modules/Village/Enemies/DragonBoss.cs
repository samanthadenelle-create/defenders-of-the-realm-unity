// =============================================================================
// DragonBoss — the Black Dragon flying boss encounter (apex village wave-boss).
// -----------------------------------------------------------------------------
// THE ASSET. Assets/Black Dragon/Dragon_Baked_Actions_fbx_7.4_binary.fbx — a
// rigged dragon (Generic rig) with FOUR baked animation takes the .fbx.meta
// exposes: Fly_New / Idel_New (sic) / Run_New / Walk_New. There is NO bespoke
// Attack or Death take, so this script DRIVES the encounter in code: the dragon
// stays on the Fly clip throughout, and Attack / Death are realised as movement
// + VFX beats rather than dedicated clips (see docs/port-notes/dragon-boss.md).
//
// WHERE IT FITS. The Black Dragon is NOT in docs/enemy-codex.md's roster — the
// codex is a humanoid/quadruped KayKit slate. This boss is an apex encounter:
// "Syndrath the Devourer" — a sky-boss that circles Elarion and dives on the
// Heart, a set-piece above the eight-boss slate. The name was owner-ratified
// (2026-05-19); the placement — a special apex village wave, above the
// Necromancer — is the working design.
//
// MODULE ISOLATION. Lives in DeNelle.Village (it threatens the village Heart,
// like Enemy). It implements the cross-module DeNelle.Core.Combat.IDamageable
// seam DIRECTLY (unlike Enemy, which uses the EnemyDamageable adapter) so the
// hero's abilities and the isolated DeNelle.Pets module can damage it without
// referencing this concrete type — exactly the seam IDamageable was built for.
//
// FLIGHT, NOT NAVMESH. A flying boss does not path a baked NavMesh. DragonBoss
// owns its own kinematic flight (hover / circle / swoop) — no NavMeshAgent, no
// Rigidbody. It needs only its anchor (the Heart) and clear sky.
//
// PHASES. HP-gated behaviour, mirroring the codex's boss vocabulary (§4):
//   Phase 1 (100-60%) — The Circling: high lazy orbit, occasional fire-breath.
//   Phase 2 (60-25%)  — The Stooping: tighter/faster orbit, dive-bomb swoops.
//   Phase 3 (25-0%)   — The Last Wing: relentless swoops, no respite.
//   Death             — a long spiralling fall, then destroy.
//
// ANIMATOR. The dragon rig carries an Animator; the integrator assigns
// Assets/Generated/Animators/Dragon.controller (built by DragonAnimatorSetup).
// This script drives it by the parameter names DragonAnimatorSetup writes:
// Speed (float), Attack (trigger), Dead (bool). Every Animator call is
// null-guarded — a dragon with no Animator still flies its full encounter.
// =============================================================================

using System;
using DeNelle.Core.Combat;
using DeNelle.Core.Diagnostics;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// The flight behaviour phase the dragon is currently in. HP-threshold gated
    /// — see <see cref="DragonBoss.ResolvePhase"/>.
    /// </summary>
    public enum DragonPhase
    {
        /// <summary>Phase 1 (100-60% HP) — high lazy orbit, occasional fire-breath.</summary>
        Circling = 0,
        /// <summary>Phase 2 (60-25% HP) — tighter, faster orbit punctuated by dive-swoops.</summary>
        Stooping = 1,
        /// <summary>Phase 3 (25-0% HP) — relentless swoops, no respite.</summary>
        LastWing = 2,
        /// <summary>HP zero — the long spiralling fall to the ground.</summary>
        Falling = 3,
    }

    /// <summary>
    /// The Black Dragon flying boss — an apex set-piece encounter for the Elarion
    /// village. Circles the Heart on its own kinematic flight, dives to strike,
    /// loses HP through three behaviour phases and dies in a spiralling fall.
    /// Implements <see cref="IDamageable"/> directly so the hero and the isolated
    /// pets module can damage it through the Core seam.
    /// </summary>
    /// <remarks>
    /// Boss name "Syndrath the Devourer" — owner-ratified 2026-05-19. The
    /// apex-wave placement is the working design (see docs/port-notes/dragon-boss.md).
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class DragonBoss : MonoBehaviour, IDamageable, ICombatLayered
    {
        // ── Identity ──────────────────────────────────────────────────────────

        [Header("Identity")]
        [Tooltip("Stable per-instance id — e.g. 'boss-dragon-1'.")]
        [SerializeField] private string _bossId = "boss-dragon";

        // ── Stats ─────────────────────────────────────────────────────────────
        // Anchored well above the Necromancer of the Wound (canon hp 1700) — the
        // dragon is the apex village boss. Tunable; the data layer can override
        // via Configure.

        [Header("Stats")]
        [Tooltip("Current hit points.")]
        [SerializeField] private float _hp = 4200f;

        [Tooltip("Max hit points.")]
        [SerializeField] private float _maxHp = 4200f;

        [Tooltip("Damage dealt to the Heart / a structure per swoop strike.")]
        [SerializeField] private float _swoopDamage = 60f;

        [Tooltip("Damage dealt by a fire-breath pass.")]
        [SerializeField] private float _breathDamage = 34f;

        // ── Flight tuning ─────────────────────────────────────────────────────

        [Header("Flight — orbit")]
        [Tooltip("Radius of the lazy Phase-1 orbit around the anchor (world units).")]
        [SerializeField] private float _orbitRadius = 26f;

        [Tooltip("Cruise height the dragon holds above the anchor while orbiting.")]
        [SerializeField] private float _orbitHeight = 22f;

        [Tooltip("Angular orbit speed in Phase 1 (degrees / second).")]
        [SerializeField] private float _orbitSpeed = 32f;

        [Tooltip("Forward flight speed used to drive the Animator Speed blend.")]
        [SerializeField] private float _cruiseSpeed = 14f;

        [Header("Flight — swoop")]
        [Tooltip("Lowest height the dragon reaches at the bottom of a dive-swoop.")]
        [SerializeField] private float _swoopLowHeight = 4.5f;

        [Tooltip("Seconds a full dive-and-climb swoop takes.")]
        [SerializeField] private float _swoopDuration = 3.4f;

        [Tooltip("Distance from the anchor at which a swoop counts as 'striking'.")]
        [SerializeField] private float _strikeRadius = 7f;

        [Header("Attack cadence")]
        [Tooltip("Seconds between attacks in Phase 1 (fire-breath passes).")]
        [SerializeField] private float _phase1AttackInterval = 6.5f;

        [Tooltip("Seconds between attacks in Phase 2 (mixed breath + swoop).")]
        [SerializeField] private float _phase2AttackInterval = 4.2f;

        [Tooltip("Seconds between attacks in Phase 3 (relentless swoops).")]
        [SerializeField] private float _phase3AttackInterval = 2.6f;

        [Header("Death")]
        [Tooltip("Seconds the spiralling death fall takes before the dragon is destroyed.")]
        [SerializeField] private float _deathFallSeconds = 4.5f;

        // ── Phase VFX (WO-66) ─────────────────────────────────────────────────
        // Boss-fight visual phases driven entirely off the boss's own HP/state
        // events (PhaseChanged / StruckHeart / Died) through the canonical
        // DeNelle.Village VFXManager. Pooled (VFXManager owns the pool) — no
        // per-frame alloc; auras are loop handles swapped once per transition.

        [Header("Phase VFX (WO-66)")]
        [Tooltip("Master toggle — play phase-transition bursts, phase auras and " +
                 "attack telegraphs through VFXManager. Off = silent (e.g. low-end).")]
        [SerializeField] private bool _phaseVfxEnabled = true;

        [Tooltip("One-shot enrage burst played at the boss when it crosses an HP threshold.")]
        [SerializeField] private VFXType _phaseTransitionVfx = VFXType.Boss_PhaseTransition;

        [Tooltip("Wind-up tell played on the boss just before a swoop / fire-breath special.")]
        [SerializeField] private VFXType _telegraphVfx = VFXType.Boss_Telegraph;

        [Tooltip("Oneshot impact burst at the Heart when a swoop / breath strike lands.")]
        [SerializeField] private VFXType _strikeImpactVfx = VFXType.Boss_AttackImpact;

        [Tooltip("Oneshot burst at the boss on death.")]
        [SerializeField] private VFXType _deathVfx = VFXType.Boss_Death;

        [Tooltip("Persistent phase aura for Phase 1 (Circling) — calm.")]
        [SerializeField] private VFXType _phase1Aura = VFXType.Boss_Aura_Phase1;

        [Tooltip("Persistent phase aura for Phase 2 (Stooping) — enraged.")]
        [SerializeField] private VFXType _phase2Aura = VFXType.Boss_Aura_Phase2;

        [Tooltip("Persistent phase aura for Phase 3 (LastWing) — seething.")]
        [SerializeField] private VFXType _phase3Aura = VFXType.Boss_Aura_Phase3;

        // The single live aura loop handle — swapped (Stop old, Play new) on each
        // phase transition so only ONE aura is ever attached to the boss.
        private VFXHandle _auraHandle;

        // ── Phase thresholds (HP fraction) ────────────────────────────────────

        /// <summary>Phase 1 → Phase 2 boundary — 60% HP.</summary>
        private const float Phase2Threshold = 0.60f;

        /// <summary>Phase 2 → Phase 3 boundary — 25% HP.</summary>
        private const float Phase3Threshold = 0.25f;

        // ── Runtime refs / state ──────────────────────────────────────────────

        private Transform _anchor;            // the Heart — what the dragon menaces
        private IDamageableStructure _heartStructure; // optional — set by Configure
        private DragonPhase _phase = DragonPhase.Circling;
        private float _orbitAngleDeg;         // current position on the orbit ring
        private float _attackCooldown;
        private float _swoopElapsed;          // >0 while a swoop is mid-flight
        private bool _swoopStruck;            // true once the current swoop landed its hit
        private float _deathElapsed;
        private bool _dead;
        private Vector3 _anchorFallback;      // home position if no anchor is wired
        private float _shownSpeed;            // smoothed Speed pushed to the Animator

        // ── Animation ─────────────────────────────────────────────────────────
        // Dragon.controller (DragonAnimatorSetup) — parameter names MUST match.
        private Animator _animator;
        private static readonly int AnimSpeed  = Animator.StringToHash("Speed");
        private static readonly int AnimAttack = Animator.StringToHash("Attack");
        private static readonly int AnimDead   = Animator.StringToHash("Dead");
        // WO-163: cached once when the Animator resolves — whether this rig's
        // controller declares each param. Driving an absent param logs every
        // frame (Speed) / on each event (Attack/Dead). Guard all setter calls.
        private bool _hasSpeedParam;
        private bool _hasAttackParam;
        private bool _hasDeadParam;

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>Raised when the dragon's HP reaches zero. Arg = this boss.</summary>
        public event Action<DragonBoss> Died;

        /// <summary>Raised each time the boss crosses into a new flight phase.</summary>
        public event Action<DragonPhase> PhaseChanged;

        /// <summary>Raised when a swoop strike or fire-breath lands on the anchor.</summary>
        public event Action<float> StruckHeart;

        // ── Public surface ────────────────────────────────────────────────────

        /// <summary>Stable per-instance id.</summary>
        public string BossId => _bossId;

        /// <summary>Max hit points.</summary>
        public float MaxHp => _maxHp;

        /// <summary>HP as a 0..1 fraction — drives the boss HP bar.</summary>
        public float HpFraction => _maxHp > 0f ? Mathf.Clamp01(_hp / _maxHp) : 0f;

        /// <summary>The flight phase the dragon is currently in.</summary>
        public DragonPhase Phase => _phase;

        /// <summary>True once the dragon has died (HP hit zero).</summary>
        public bool IsDead => _dead;

        // ── IDamageable (Core combat seam) ────────────────────────────────────

        /// <summary>The dragon is hostile to the village's defenders.</summary>
        public CombatFaction Faction => CombatFaction.Hostile;

        /// <summary>
        /// ICombatLayered — the apex dragon FLIES, so only anti-air (or "both")
        /// towers can acquire and fire at it. This is the canonical flyer in the
        /// air/ground targeting matrix.
        /// </summary>
        public CombatLayer Layer => CombatLayer.Flying;

        /// <summary>World position of the dragon — used by range / nearest queries.</summary>
        public Vector3 WorldPosition => transform.position;

        /// <summary>Current hit points. Non-positive means dead.</summary>
        public float Hp => _hp;

        /// <summary>True while the dragon is alive and a valid attack target.</summary>
        public bool IsAlive => !_dead && _hp > 0f;

        // ---------------------------------------------------------------------
        // Configuration
        // ---------------------------------------------------------------------

        /// <summary>
        /// Wires the dragon for an encounter. Called by the wave / encounter
        /// controller right after instantiation.
        /// </summary>
        /// <param name="bossId">Stable per-instance id.</param>
        /// <param name="anchor">The Heart transform — the dragon's orbit centre and strike goal.</param>
        /// <param name="maxHp">Optional max HP override (≤0 keeps the inspector value).</param>
        public void Configure(string bossId, Transform anchor, float maxHp = 0f)
        {
            if (!string.IsNullOrEmpty(bossId)) _bossId = bossId;
            _anchor = anchor;

            if (maxHp > 0f)
            {
                _maxHp = maxHp;
            }
            _hp = _maxHp;

            // The anchor may also be an attackable structure (the Heart). If it
            // is, swoop strikes route real contact damage into it; if not, the
            // StruckHeart event still fires for the encounter controller to use.
            if (anchor != null)
                _heartStructure = anchor.GetComponentInParent<IDamageableStructure>();

            _dead = false;
            _deathElapsed = 0f;
            _phase = DragonPhase.Circling;
            _attackCooldown = _phase1AttackInterval;
            _swoopElapsed = 0f;
            _swoopStruck = false;

            // Boss-fight phase VFX — attach the calm Phase-1 aura now. Later
            // phases swap it via PlayPhaseAura on each PhaseChanged.
            PlayPhaseAura(_phase);
        }

        // ---------------------------------------------------------------------
        // Lifecycle
        // ---------------------------------------------------------------------

        private void Awake()
        {
            EnsureAnimator();
            EnsureHitCollider();
            // Remember a home point so a dragon with no wired anchor still flies
            // a sane orbit instead of collapsing onto the origin.
            _anchorFallback = transform.position;
            // Seed the orbit angle from the spawn position so multiple dragons
            // (or a re-spawn) do not all start at the same point on the ring.
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

            ResolvePhase();

            // Flight — a swoop, if one is mid-flight, otherwise the orbit.
            float frameSpeed;
            if (_swoopElapsed > 0f)
                frameSpeed = TickSwoop(dt);
            else
                frameSpeed = TickOrbit(dt);

            TickAttackCadence(dt);
            DriveAnimator(frameSpeed);
        }

        // ---------------------------------------------------------------------
        // Phase resolution
        // ---------------------------------------------------------------------

        /// <summary>
        /// Re-derives the flight phase from current HP and raises
        /// <see cref="PhaseChanged"/> on a transition.
        /// </summary>
        private void ResolvePhase()
        {
            float frac = HpFraction;
            DragonPhase next = _phase;

            if (frac > Phase2Threshold) next = DragonPhase.Circling;
            else if (frac > Phase3Threshold) next = DragonPhase.Stooping;
            else next = DragonPhase.LastWing;

            if (next != _phase)
            {
                DragonPhase prev = _phase;
                _phase = next;
                // §12 — phase transitions are captured so a headless run shows the
                // boss is being worn down (i.e. an air-defense IS connecting).
                FlowTrace.Step("DragonBoss",
                    $"'{_bossId}' phase {prev} -> {_phase} at HP {_hp:0.#}/{_maxHp:0.#} ({HpFraction:P0}).");
                // Boss-fight phase VFX: a one-shot enrage burst on the boss, then
                // swap the persistent phase aura to the new phase's loop.
                PlayPhaseTransition();
                PlayPhaseAura(_phase);
                PhaseChanged?.Invoke(_phase);
            }
        }

        /// <summary>Attack interval for the current phase.</summary>
        private float CurrentAttackInterval()
        {
            switch (_phase)
            {
                case DragonPhase.Stooping: return _phase2AttackInterval;
                case DragonPhase.LastWing: return _phase3AttackInterval;
                default:                   return _phase1AttackInterval;
            }
        }

        /// <summary>
        /// Orbit speed for the current phase — the orbit tightens and quickens
        /// as the dragon's HP falls.
        /// </summary>
        private float CurrentOrbitSpeed()
        {
            switch (_phase)
            {
                case DragonPhase.Stooping: return _orbitSpeed * 1.45f;
                case DragonPhase.LastWing: return _orbitSpeed * 1.9f;
                default:                   return _orbitSpeed;
            }
        }

        /// <summary>Orbit radius for the current phase — tighter in later phases.</summary>
        private float CurrentOrbitRadius()
        {
            switch (_phase)
            {
                case DragonPhase.Stooping: return _orbitRadius * 0.78f;
                case DragonPhase.LastWing: return _orbitRadius * 0.6f;
                default:                   return _orbitRadius;
            }
        }

        // ---------------------------------------------------------------------
        // Flight — orbit
        // ---------------------------------------------------------------------

        /// <summary>
        /// Advances the dragon one frame along its circular orbit around the
        /// anchor at cruise height, banking to face its travel direction.
        /// Returns the flight speed for the Animator blend.
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

        // ---------------------------------------------------------------------
        // Flight — swoop (the dive attack)
        // ---------------------------------------------------------------------

        /// <summary>
        /// Begins a dive-swoop: the dragon drops from the orbit toward the anchor,
        /// strikes at the bottom of the arc, then climbs back to cruise height.
        /// No-op if a swoop is already running.
        /// </summary>
        private void BeginSwoop()
        {
            if (_swoopElapsed > 0f) return;
            _swoopElapsed = Mathf.Epsilon; // mark active; TickSwoop drives it
            _swoopStruck = false;
            if (_animator != null && _hasAttackParam) _animator.SetTrigger(AnimAttack);
            PlayTelegraph(); // wind-up tell before the dive
            DeNelle.Village.GameSfx.PlayDragonRoar(); // #51: roar as the dragon commits to a swoop
        }

        /// <summary>
        /// Advances an in-progress dive-swoop. The dragon arcs down toward the
        /// anchor and back up over <see cref="_swoopDuration"/> seconds; at the
        /// low point it deals <see cref="_swoopDamage"/>. Returns flight speed.
        /// </summary>
        private float TickSwoop(float dt)
        {
            _swoopElapsed += dt;
            float t = Mathf.Clamp01(_swoopElapsed / _swoopDuration);

            Vector3 centre = AnchorPosition();

            // Height arc: cruise -> low at the midpoint -> cruise (a parabola).
            float arc = 1f - 4f * (t - 0.5f) * (t - 0.5f); // 0 at ends, 1 mid
            float height = Mathf.Lerp(_orbitHeight, _swoopLowHeight, arc);

            // Horizontal: sweep in toward the anchor on the way down, out on the
            // way up — so the swoop reads as a real dive-past, not a hover-drop.
            float r = Mathf.Lerp(CurrentOrbitRadius(), _strikeRadius * 0.5f, arc);
            float rad = _orbitAngleDeg * Mathf.Deg2Rad;
            // Keep drifting around the ring during the swoop so it spirals.
            _orbitAngleDeg += CurrentOrbitSpeed() * 0.5f * dt;

            Vector3 target = centre + new Vector3(
                Mathf.Cos(rad) * r,
                height,
                Mathf.Sin(rad) * r);

            Vector3 prev = transform.position;
            // The dive is faster than cruise — a stoop accelerates.
            float swoopSpeed = _cruiseSpeed * 1.8f;
            transform.position = Vector3.MoveTowards(prev, target, swoopSpeed * dt);
            FaceTravel(transform.position - prev);

            // Strike once, at the bottom of the arc, while inside strike range.
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
                // Swoop complete — back to orbiting.
                _swoopElapsed = 0f;
                _swoopStruck = false;
            }
            return swoopSpeed;
        }

        // ---------------------------------------------------------------------
        // Attacks
        // ---------------------------------------------------------------------

        /// <summary>
        /// Counts down to the next attack. Phase 1 favours a stationary
        /// fire-breath pass; Phase 2+ launch dive-swoops. The cadence quickens
        /// with each phase.
        /// </summary>
        private void TickAttackCadence(float dt)
        {
            if (_swoopElapsed > 0f) return; // already attacking

            _attackCooldown -= dt;
            if (_attackCooldown > 0f) return;

            _attackCooldown = CurrentAttackInterval();

            switch (_phase)
            {
                case DragonPhase.Circling:
                    // Mostly fire-breath; an occasional probing swoop.
                    if (UnityEngine.Random.value < 0.55f) BeginSwoop();
                    else FireBreath();
                    break;

                case DragonPhase.Stooping:
                    // A mix — swoops dominate.
                    if (UnityEngine.Random.value < 0.65f) BeginSwoop();
                    else FireBreath();
                    break;

                case DragonPhase.LastWing:
                    // Relentless swoops.
                    BeginSwoop();
                    break;
            }
        }

        /// <summary>
        /// A fire-breath pass — the dragon does not leave the orbit; it strafes
        /// the anchor with breath. Deals <see cref="_breathDamage"/>.
        /// </summary>
        private void FireBreath()
        {
            if (_animator != null && _hasAttackParam) _animator.SetTrigger(AnimAttack);
            PlayTelegraph(); // wind-up tell before the breath pass
            DealStrike(_breathDamage);
        }

        /// <summary>
        /// Applies <paramref name="amount"/> damage to the anchor structure (the
        /// Heart) if one is wired, and always raises <see cref="StruckHeart"/> so
        /// the encounter controller can react (camera shake, Heart threat state).
        /// </summary>
        private void DealStrike(float amount)
        {
            if (_heartStructure != null && _heartStructure.IsAlive)
                _heartStructure.ApplyContactDamage(amount);

            // Oneshot impact burst at the strike point (the anchor / Heart).
            if (_phaseVfxEnabled && _strikeImpactVfx != VFXType.None)
                VFXManager.Play(_strikeImpactVfx, AnchorPosition());

            StruckHeart?.Invoke(amount);
        }

        // ---------------------------------------------------------------------
        // HP / death
        // ---------------------------------------------------------------------

        /// <summary>
        /// Applies <paramref name="amount"/> damage. At zero HP the dragon dies.
        /// The <paramref name="element"/> is accepted for the <see cref="IDamageable"/>
        /// contract; per-element resist math is a later tuning pass.
        /// </summary>
        public void TakeDamage(float amount, DamageElement element)
        {
            if (_dead || amount <= 0f) return;
            _hp = Mathf.Max(0f, _hp - amount);
            // §12 — throttled so a headless run PROVES a tower/air-defense is
            // actually connecting (the F8-era "12 towers did 0 damage" symptom
            // self-reports here the instant a real hit lands).
            FlowTrace.Throttle("DragonBoss", $"hit:{GetInstanceID()}", 1f,
                $"'{_bossId}' took {amount:0.#} {element} dmg -> HP {_hp:0.#}/{_maxHp:0.#} " +
                $"({HpFraction:P0}, phase {_phase}).");
            if (_hp <= 0f) Die();
        }

        /// <summary>
        /// Status effects (slow / freeze / burn) — a logged no-op for now. A
        /// flying boss does not model a ground slow; a future pass could let
        /// Freeze interrupt a swoop. Kept to satisfy the <see cref="IDamageable"/>
        /// contract.
        /// </summary>
        public void ApplyStatus(StatusEffect effect, float seconds)
        {
            // Intentionally inert — see summary. The dragon's flight is not
            // affected by ground-target statuses.
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
            if (_animator != null && _hasDeadParam) _animator.SetBool(AnimDead, true);

            // Phase VFX: stop the persistent aura and play the death burst at the
            // boss. (The body keeps spiralling down; the burst marks the kill.)
            StopPhaseAura();
            if (_phaseVfxEnabled && _deathVfx != VFXType.None)
                VFXManager.Play(_deathVfx, transform.position);

            Died?.Invoke(this);
            PhaseChanged?.Invoke(DragonPhase.Falling);
        }

        /// <summary>
        /// Drives the spiralling death fall — the dragon corkscrews down to the
        /// ground over <see cref="_deathFallSeconds"/>, then is destroyed. The
        /// rig has no death clip, so the fall is the death animation.
        /// </summary>
        private void TickDeathFall(float dt)
        {
            _deathElapsed += dt;
            float t = Mathf.Clamp01(_deathElapsed / _deathFallSeconds);

            Vector3 centre = AnchorPosition();

            // Spiral inward and down — radius and height both ease to ground.
            _orbitAngleDeg += 220f * dt; // tight death spin
            float rad = _orbitAngleDeg * Mathf.Deg2Rad;
            float r = Mathf.Lerp(CurrentOrbitRadius(), 3f, t);
            float height = Mathf.Lerp(transform.position.y - centre.y, 0.5f, t * t);

            transform.position = centre + new Vector3(
                Mathf.Cos(rad) * r,
                Mathf.Max(0.5f, height),
                Mathf.Sin(rad) * r);

            // Tumble — nose pitches down as it falls.
            float pitch = Mathf.Lerp(0f, 70f, t);
            transform.rotation = Quaternion.Euler(pitch, _orbitAngleDeg, Mathf.Sin(t * 12f) * 18f);

            if (t >= 1f)
                Destroy(gameObject);
        }

        // ---------------------------------------------------------------------
        // Animation
        // ---------------------------------------------------------------------

        /// <summary>
        /// Feeds the Animator's <c>Speed</c> float (smoothed) so Dragon.controller
        /// blends Idle ↔ Fly. The dragon is airborne almost always, so Speed is
        /// near its cruise value the whole encounter. Null-guarded.
        /// </summary>
        private void DriveAnimator(float rawSpeed)
        {
            if (_animator == null || !_hasSpeedParam) return;
            _shownSpeed = Mathf.Lerp(_shownSpeed, rawSpeed, Time.deltaTime * 4f);
            _animator.SetFloat(AnimSpeed, _shownSpeed);
        }

        // ---------------------------------------------------------------------
        // Phase VFX (WO-66)
        // ---------------------------------------------------------------------
        // All boss-fight visual phases route through the canonical VFXManager
        // (DeNelle.Village). Bursts are pooled oneshots; the phase aura is a
        // single VFXHandle loop swapped per transition — no per-frame alloc.

        /// <summary>The looping aura VFXType for the given flight phase.</summary>
        private VFXType AuraForPhase(DragonPhase phase)
        {
            switch (phase)
            {
                case DragonPhase.Stooping: return _phase2Aura;
                case DragonPhase.LastWing: return _phase3Aura;
                default:                   return _phase1Aura; // Circling
            }
        }

        /// <summary>
        /// Swaps the persistent phase aura to the one for <paramref name="phase"/>.
        /// Stops the previous aura first so only one is ever attached. No-op when
        /// phase VFX are disabled or VFXManager is not present.
        /// </summary>
        private void PlayPhaseAura(DragonPhase phase)
        {
            if (!_phaseVfxEnabled) return;
            var mgr = VFXManager.Instance;
            if (mgr == null) return;

            VFXType type = AuraForPhase(phase);
            if (type == VFXType.None) { StopPhaseAura(); return; }

            // Swap: stop the old loop, then attach the new one to the boss.
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

        /// <summary>Tear down the live aura loop so it does not leak the pooled object.</summary>
        private void OnDisable()
        {
            StopPhaseAura();
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        /// <summary>The orbit centre — the wired anchor, or the spawn home point.</summary>
        private Vector3 AnchorPosition()
        {
            return _anchor != null ? _anchor.position : _anchorFallback;
        }

        /// <summary>
        /// Rotates the dragon to face its direction of travel (a small bank-in
        /// would be a later polish pass; yaw-to-travel reads well enough).
        /// </summary>
        private void FaceTravel(Vector3 delta)
        {
            delta.y *= 0.5f; // flatten the pitch a little — keep it level-ish
            if (delta.sqrMagnitude < 1e-5f) return;
            Quaternion want = Quaternion.LookRotation(delta.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, want, Time.deltaTime * 4f);
        }

        /// <summary>
        /// Resolves the Animator on the dragon rig — it sits on the FBX mesh
        /// child, so children are searched too. Null when the prefab carries no
        /// rig / no controller; every Animator call is null-guarded.
        /// </summary>
        private void EnsureAnimator()
        {
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();

            // WO-163: cache which params the controller actually declares so the
            // per-frame Speed drive (and Attack/Dead) never hit an absent param.
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                foreach (var p in _animator.parameters)
                {
                    if (p.nameHash == AnimSpeed)  _hasSpeedParam  = true;
                    if (p.nameHash == AnimAttack) _hasAttackParam = true;
                    if (p.nameHash == AnimDead)   _hasDeadParam   = true;
                }
            }
        }

        /// <summary>
        /// Guarantees a NON-TRIGGER collider on the flying dragon so a dedicated
        /// AIR-DEFENSE structure's ray / projectile physics query can HIT it at
        /// altitude (mirrors DefenseTower.EnsureContactCollider). Idempotent: skips
        /// if the rig already carries a solid collider. The <see cref="IDamageable"/>
        /// + the <see cref="ICombatLayered"/> (<see cref="CombatLayer.Flying"/>) hook
        /// both live on THIS root, so a child-collider hit resolves the boss via
        /// GetComponentInParent&lt;IDamageable&gt;() / GetComponentInParent&lt;ICombatLayered&gt;().
        /// The collider stays on the prefab's (default, raycastable) layer — a
        /// cruising or swooping dragon is a real physics target either way. Towers
        /// damage via a direct TakeDamage call once acquired, so this collider is
        /// specifically for the new air-defense's ray/overlap acquisition + VFX hit.
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
                // World extent -> local radius (guards a zero/negative lossyScale axis).
                Vector3 ls = transform.lossyScale;
                float sMax = Mathf.Max(0.01f,
                    Mathf.Max(Mathf.Abs(ls.x), Mathf.Max(Mathf.Abs(ls.y), Mathf.Abs(ls.z))));
                sc.radius = Mathf.Max(0.5f, maxExt / sMax);
            }
            else
            {
                sc.center = Vector3.zero;
                sc.radius = 2.5f;   // fallback body radius when the rig has no renderer
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 c = Application.isPlaying ? AnchorPosition() : transform.position;
            Gizmos.color = new Color(0.85f, 0.35f, 0.15f, 0.7f);
            // Orbit ring at cruise height.
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
            // Strike-radius marker at ground level.
            Gizmos.color = new Color(0.95f, 0.2f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(c, _strikeRadius);
        }
#endif
    }
}
