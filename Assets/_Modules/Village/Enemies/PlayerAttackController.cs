// =============================================================================
// PlayerAttackController — DEF-47: Player attack with perfect-hit timing window.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Gives player attacks timing depth — a "Perfect Hit" window on each swing that
// deals bonus damage and triggers dramatic feedback. Rewards skilled play without
// requiring complex input chains.
//
// ADAPTION NOTES (from spec against this codebase):
//   • IDamageable.TakeDamage(float, DamageElement) — correct signature here.
//   • DamageNumberSpawner.Spawn() / SpawnLabel() — replaces FloatingTextSpawner
//     which doesn't exist; DamageNumberSpawner is the project's equivalent.
//   • CombatFeedbackManager.Hit(worldPos, damage) — static helper, no Instance call.
//   • No HitIntensity enum in this project; bonus damage is the sole feedback signal.
// =============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using DeNelle.Core.Combat;
using DeNelle.Core.Diagnostics;   // WO-449 §12: FlowTrace on the melee LoS gate

namespace DeNelle.Village
{
    /// <summary>
    /// Handles player melee attacks with a timing-based perfect-hit window.
    /// Attach to the Hero root alongside <see cref="HeroLocomotion"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerAttackController : MonoBehaviour
    {
        [Header("Base Attack")]
        [Tooltip("Flat damage per hit before any talent multipliers.")]
        [SerializeField] private float _baseDamage = 30f;

        [Tooltip("Radius of the OverlapSphere damage check around the hero (fallback when " +
                 "the equipped weapon sets no reach).")]
        [SerializeField, Min(0.1f)] private float _attackRange = 3.2f;

        /// <summary>
        /// Effective melee hitbox radius (m) — read by HeroReachRing to draw the reach ring,
        /// and by the swing's OverlapSphere. Weapon-driven: a melee weapon with reach &gt; 0
        /// (Knight's greatsword/polearm/axe outreach a dagger) overrides the fixed
        /// <see cref="_attackRange"/>. Ranged classes (mage/ranger) never set reach, so they
        /// keep the fixed range — their real attacks route through AbilityDef.Range, unchanged.
        /// </summary>
        public float AttackRange => EffectiveRange();

        // Gear v1: lazily-resolved equipped-gear loadout — its EquippedWeapon.reach (when >0)
        // overrides the fixed melee range. Lazily attached so every hero gets gear with no
        // builder change; graceful — no loadout / no weapon / reach 0 leaves the fixed range.
        private GearLoadout _gear;

        /// <summary>
        /// The melee reach to use this swing: the equipped weapon's reach when set (&gt; 0),
        /// otherwise the serialized fixed <see cref="_attackRange"/>. Preserves today's
        /// behaviour whenever no weapon reach is authored (all ranged classes, and any melee
        /// weapon without a reach value).
        /// </summary>
        private float EffectiveRange()
        {
            if (_gear == null) _gear = GetComponent<GearLoadout>();
            var w = _gear != null ? _gear.EquippedWeapon : null;
            return (w != null && w.reach > 0f) ? w.reach : _attackRange;
        }

        [Tooltip("Minimum seconds between attacks.")]
        [SerializeField, Min(0.1f)] private float _attackCooldown = 0.6f;

        [Tooltip("Layer mask covering enemy colliders.")]
        [SerializeField] private LayerMask _enemyLayer;

        // WO-449: line-of-sight gate for the MELEE swing. The swing's damage OverlapSphere
        // (ResolveAttack) hit EVERY hostile in radius — including one on the far side of a
        // wall the hero is standing against — because it was a pure radius test, no LoS.
        // This mask names the blockers that should occlude an attack (wall/structure geometry,
        // built onto the dedicated "Structure" layer by CastleWallsFromRecipe / CastleHubBuilder.
        // BuildInnerWallRing). It must NOT include the Enemy or Player layers, or the target's
        // own collider (or the hero) would self-block the linecast. When unset (value == 0) the
        // gate degrades OFF (HasLoS returns true) so a misconfigured mask never makes the hero
        // unable to hit ANYTHING. Mirrors HeroTargetIndicator's proven WO-449 pattern.
        [Tooltip("WO-449: layers that BLOCK a melee swing's line-of-sight (walls/structures on " +
                 "the 'Structure' layer). Do NOT include Enemy or Player. Empty disables the gate.")]
        [SerializeField] private LayerMask _losMask;

        [Header("Attack Weight (WO-217: anticipation → impact → recovery)")]
        [Tooltip("When ON, the swing's tempo is shaped via Animator.speed: a brief slow wind-up " +
                 "(anticipation), a fast snap through the contact frame (impact), then a settle " +
                 "back to normal (recovery) — so the swing reads with weight instead of flat.")]
        [SerializeField] private bool _shapeAttackTempo = true;

        [Tooltip("Seconds of slow wind-up before the strike (the coil).")]
        [SerializeField, Range(0f, 0.4f)] private float _anticipationDuration = 0.07f;

        [Tooltip("Animator.speed during the wind-up. <1 = slower/heavier coil.")]
        [SerializeField, Range(0.2f, 1f)] private float _windUpSpeed = 0.55f;

        [Tooltip("Seconds the fast contact speed is held through the impact frame (the snap).")]
        [SerializeField, Range(0f, 0.25f)] private float _impactHold = 0.05f;

        [Tooltip("Animator.speed at the contact frame. >1 = snappy, punchy strike.")]
        [SerializeField, Range(1f, 3f)] private float _impactSpeed = 1.9f;

        [Tooltip("Seconds to ease back to normal speed after impact (the follow-through settle).")]
        [SerializeField, Range(0f, 0.5f)] private float _recoveryDuration = 0.16f;

        [Tooltip("Seconds after swing input when the weapon CONTACTS the target — the damage lands " +
                 "here so the hit syncs to the impact frame, not the swing start. When 0, falls back " +
                 "to the perfect-hit window start (legacy behaviour).")]
        [SerializeField, Min(0f)] private float _impactFrameDelay = 0.13f;

        [Header("Perfect Hit Window")]
        [Tooltip("Seconds after swing input when the perfect-hit window opens.")]
        [SerializeField, Min(0f)] private float _perfectHitWindowStart = 0.08f;

        [Tooltip("Seconds after swing input when the perfect-hit window closes.")]
        [SerializeField, Min(0f)] private float _perfectHitWindowEnd = 0.18f;

        [Tooltip("Damage multiplier applied when the player hits in the perfect window.")]
        [SerializeField, Min(1f)] private float _perfectHitMultiplier = 1.75f;

        [Tooltip("Sound played on a perfect hit (optional).")]
        [SerializeField] private AudioClip _perfectHitSound;

        [Header("Weapon Whoosh")]
        [Tooltip("Pool of whoosh sounds — one is chosen at random per swing.")]
        [SerializeField] private AudioClip[] _whooshSounds;

        [Tooltip("Pitch variation range for the whoosh sample.")]
        [SerializeField] private Vector2 _whooshPitchRange = new Vector2(0.9f, 1.1f);

        [Header("Weapon Trail (WO-219)")]
        [Tooltip("Optional explicit weapon/hand transform the swing trail follows. " +
                 "When null, the controller auto-resolves a right-hand bone, then falls " +
                 "back to a child placed at the hero's attack origin.")]
        [SerializeField] private Transform _trailOrigin;

        [Tooltip("TrailRenderer 'time' (seconds the trail segment persists). Keep short for a crisp swing arc.")]
        [SerializeField, Range(0.02f, 0.4f)] private float _trailTime = 0.14f;

        [Tooltip("Trail width at the swing-start end (tapers to 0 at the tail).")]
        [SerializeField, Range(0.02f, 0.6f)] private float _trailStartWidth = 0.18f;

        [Tooltip("Extra seconds the trail stays enabled after the active hit window before fading out.")]
        [SerializeField, Range(0f, 0.3f)] private float _trailLinger = 0.06f;

        [Tooltip("Trail colour (a cool steel arc by default).")]
        [SerializeField] private Color _trailColor = new Color(0.75f, 0.85f, 1.0f, 0.85f);

        // ── Runtime ───────────────────────────────────────────────────────────

        private Animator     _animator;
        private AudioSource  _audioSource;
        private float        _nextAttackTime;
        private float        _swingStartTime;
        private bool         _isInSwing;

        // WO-219: code-built swing trail. Enabled at swing start, disabled after the
        // active window (+ a short linger). Lazily built on the resolved trail origin.
        private TrailRenderer _swingTrail;

        // WO-284/285: animation now routes through the canonical ActorAnimator driver
        // (no local StringToHash). PlayAttack cycles a combo for melee classes; casters
        // route to PlayCast so they cast instead of swinging.
        private ActorAnimator _actor;
        private HeroAbilities _abilities;   // class source (knight = combo, casters = cast)

        // WO-423: face-the-target on swing. The hero used to fire its melee/360° hit toward
        // its last MOVE direction, so a stationary attack faced the wrong way. StartAttack
        // resolves the intended target (the reticle-locked one, else the nearest hostile in
        // reach) and asks HeroLocomotion — the sole rotation writer — to yaw-slew toward it
        // before the swing lands; the impact delay covers the turn. Both components sit on
        // the hero root, so they're plain sibling GetComponent lookups.
        private HeroLocomotion     _loco;
        private HeroTargetIndicator _targetIndicator;

        // WO-497 cheap wire: rumble on LANDING a melee hit. HeroImpactFeedback.PlayHaptic
        // previously fired only when the hero TOOK damage (HeroHealth). Resolved lazily +
        // null-safe (no component / no gamepad = silent no-op).
        private HeroImpactFeedback _impactFeedback;
        private int   _comboIndex;
        private float _lastSwingTime;
        private const int   ComboLength    = 3;     // Knight melee combo swings
        private const float ComboResetGap  = 1.1f;  // seconds idle before the combo resets to 0

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _animator    = GetComponentInChildren<Animator>();
            if (!TryGetComponent(out _actor)) _actor = gameObject.AddComponent<ActorAnimator>();
            _abilities   = GetComponent<HeroAbilities>();
            _loco            = GetComponent<HeroLocomotion>();          // WO-423: sole rotation writer
            _targetIndicator = GetComponent<HeroTargetIndicator>();     // WO-423: reticle-locked target
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 0f; // 2D — punchy response without attenuation

            // Bug-fix (audit 2026-05-30): an unset _enemyLayer (mask 0 = "Nothing") makes every
            // OverlapSphere return empty, so a runtime-built hero's melee silently hits nothing.
            // Default to the Enemy layer, then to Everything — matching HeroHealth/HeroAbilities.
            if (_enemyLayer == 0) _enemyLayer = LayerMask.GetMask("Enemy");
            if (_enemyLayer == 0) _enemyLayer = ~0;

            // WO-449: activate the melee LoS gate out-of-the-box against the dedicated
            // "Structure" wall layer (same layer HeroTargetIndicator's reticle gate uses, set on
            // the wall geometry by CastleWallsFromRecipe / CastleHubBuilder.BuildInnerWallRing).
            // Only seed when unset in the inspector; if "Structure" doesn't exist GetMask returns
            // 0 and HasLoS's degrade rule (value == 0 → clear) keeps the swing able to hit.
            if (_losMask.value == 0) _losMask = LayerMask.GetMask("Structure");

            // WO-504 slice 3: re-tint the swing trail the instant the equipped weapon changes
            // (rarity-driven color/width), so a swap is FELT without waiting for the next swing.
            // Null-safe lazy resolve; unsubscribed in OnDestroy.
            if (_gear == null) _gear = GetComponent<GearLoadout>();
            if (_gear != null) _gear.OnGearChanged += OnGearChangedReTrail;
        }

        private void OnDestroy()
        {
            if (_gear != null) _gear.OnGearChanged -= OnGearChangedReTrail;
        }

        /// <summary>WO-504 slice 3: weapon swapped -> re-resolve the swing-trail VFX. No-op until
        /// the trail is built (the next EnsureSwingTrail applies the current weapon's look anyway).</summary>
        private void OnGearChangedReTrail()
        {
            if (_swingTrail != null) ApplyWeaponTrailVfx();
        }

        private void Update()
        {
            // WO-377: no melee swings / blocks while a Yarn dialogue is on screen (a click
            // on the dialogue box used to fall through to a swing). HeroLocomotion owns the
            // gate. If a block was being held when the dialogue opened, drop it cleanly so
            // the hero doesn't freeze mid-block through the conversation.
            if (HeroLocomotion.InputSuppressed)
            {
                if (_blocking)
                {
                    _blocking = false;
                    _actor?.SetBlocking(false);
                }
                return;
            }

            bool attackPressed = false;

            // New Input System path.
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame) attackPressed = true;

            var gp = Gamepad.current;
            if (gp != null && gp.buttonSouth.wasPressedThisFrame) attackPressed = true;

            // Legacy Input fallback.
            if (!attackPressed && UnityEngine.Input.GetMouseButtonDown(0)) attackPressed = true;

            if (attackPressed && !_isInSwing && Time.time >= _nextAttackTime)
                StartAttack();

            UpdateBlock();
        }

        // WO-285 (D): shield classes (Knight) hold a block while RMB / Left-Shift is
        // held. Routed through the canonical driver (Block bool); a guarded no-op for
        // classes/controllers without a Block state. Resolved live so it follows the
        // body swap. Only the Knight blocks (sword-and-shield); casters/ranged don't.
        private bool _blocking;

        // ── Perfect parry / riposte ───────────────────────────────────────────
        // Raising block opens a brief PARRY WINDOW; an enemy hit landing inside it is negated
        // (see HeroHealth) → a slow-time beat + the next swing becomes a big RIPOSTE. OpenParryWindow()
        // is the public seam a caster's magical deflect spell reuses for the same payoff.
        private float _parryWindowUntil;
        private float _riposteArmedUntil;
        private const float ParryWindow       = 0.25f;  // sec after block-raise a hit is parried
        private const float RiposteWindow     = 2.0f;   // sec after a parry the next swing is empowered
        private const float RiposteMultiplier = 3.0f;   // counter-swing damage multiplier

        private void UpdateBlock()
        {
            string cls = _abilities != null ? _abilities.HeroClass : null;
            bool canBlock = cls == "knight";

            bool held = false;
            if (canBlock)
            {
                var kb = Keyboard.current;
                if (kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed)) held = true;
                if (!held && UnityEngine.Input.GetMouseButton(1)) held = true;   // RMB
                var gp = Gamepad.current;
                if (!held && gp != null && gp.leftTrigger.isPressed) held = true;
            }

            if (held != _blocking)
            {
                _blocking = held;
                _actor.SetBlocking(_blocking);
                if (held)
                {
                    _parryWindowUntil = Time.time + ParryWindow; // raising block opens the parry window
                    // WO-359: parry-READY visual cue. A brief cool-steel flash at the shield the
                    // instant the window opens, so the player can SEE the (otherwise invisible)
                    // 0.25 s timing window they're aiming for. Reuses the central VFXManager
                    // (Impact_ShockwaveRing → procedural shield-ward nova); static + null-safe, so
                    // it's a silent no-op when no VFXManager is present. No new VFX subsystem.
                    VFXManager.Play(VFXType.Impact_ShockwaveRing, transform.position + Vector3.up * 1.2f);
                }
            }
        }

        // ── Parry API (HeroHealth calls these on incoming hits; the deflect seam is public) ──

        /// <summary>True (and consumes the window) if an enemy hit lands during the parry window.</summary>
        public bool TryConsumeParry()
        {
            if (Time.time > _parryWindowUntil) return false;
            _parryWindowUntil = 0f;   // consume so one block-raise parries one hit
            return true;
        }

        /// <summary>Open a parry window NOW — the seam a caster's magical deflect spell calls so it
        /// routes through the same parry → slo-mo → riposte payoff (just with magic VFX/anim).</summary>
        public void OpenParryWindow(float seconds = ParryWindow)
        {
            _parryWindowUntil = Time.time + Mathf.Max(0.05f, seconds);
        }

        /// <summary>Payoff for a successful parry: slow-time beat + deflect clang + arm the riposte.</summary>
        public void OnParrySuccess(Vector3 pos)
        {
            CombatFeedbackManager.Parry(pos);
            GameSfx.PlaySwordClash();   // metallic deflect clang
            _riposteArmedUntil = Time.time + RiposteWindow;
            DamageNumberSpawner.SpawnLabel("PARRY!", pos + Vector3.up * 1.4f, new Color(0.6f, 0.9f, 1f), 1.4f);
        }

        // ── Attack flow ───────────────────────────────────────────────────────

        private void StartAttack()
        {
            _nextAttackTime = Time.time + _attackCooldown;
            _swingStartTime = Time.time;
            _isInSwing      = true;

            // WO-423: turn to face the intended target BEFORE the swing plays, so the
            // hit (a 360° OverlapSphere in ResolveAttack) and any weapon VFX read as
            // facing the foe instead of the last move direction. Prefer the reticle's
            // locked target; else the nearest hostile inside attack reach. HeroLocomotion
            // (the sole rotation writer) slews the yaw; the _impactFrameDelay covers it.
            FaceAttackTarget();

            // WO-285: melee classes swing a cycling combo; casters (Mage/Cleric) cast
            // instead of swinging. Class resolved live from HeroAbilities (set after the
            // body swap). The combo index resets after a short idle gap so consecutive
            // hits flow Attack0→1→2 but a paused-then-resumed attack starts fresh.
            string cls = _abilities != null ? _abilities.HeroClass : null;
            bool caster = cls == "mage" || cls == "cleric";
            if (caster)
            {
                _actor.PlayCast();
            }
            else
            {
                if (Time.time - _lastSwingTime > ComboResetGap) _comboIndex = 0;
                _actor.PlayAttack(_comboIndex);
                _comboIndex    = (_comboIndex + 1) % ComboLength;
                _lastSwingTime = Time.time;
            }
            // WO-217: shape the swing's tempo so it reads anticipation → impact →
            // recovery instead of a flat uniform swing. Drives Animator.speed through
            // the canonical driver (a global multiplier, NOT a guarded param). Data
            // -driven via the serialized timing fields; restores speed = 1 on settle.
            if (_shapeAttackTempo && _actor != null)
                _actor.ShapeAttackTempo(_anticipationDuration, _windUpSpeed,
                                        _impactHold, _impactSpeed, _recoveryDuration);

            PlayWhoosh();

            // WO-219: light up the swing trail for the duration of the swing arc.
            EnsureSwingTrail();
            if (_swingTrail != null)
            {
                _swingTrail.Clear();          // drop any stale segment from the last swing
                _swingTrail.emitting = true;
            }

            StartCoroutine(ResolveAttack());
        }

        /// <summary>
        /// WO-423: resolve the intended swing target and ask HeroLocomotion to yaw-slew
        /// toward it. Target priority: the reticle-locked <see cref="HeroTargetIndicator.CurrentTarget"/>,
        /// else the nearest living hostile inside the effective attack reach. Null-guarded —
        /// no locomotion or no target leaves facing untouched (attack proceeds as before).
        /// </summary>
        private void FaceAttackTarget()
        {
            if (_loco == null) return;

            // 1) reticle-locked target (registry — exactly what the ring shows). The reticle
            //    already applies its own WO-449 LoS gate (HeroTargetIndicator.HasLoS), so a
            //    locked target is guaranteed visible — no wall between hero and it.
            IDamageable target = _targetIndicator != null ? _targetIndicator.CurrentTarget : null;
            if (target != null && !target.IsAlive) target = null;

            // 2) fall back to the nearest hostile inside the swing's reach WITH a clear
            //    line-of-sight (WO-449) — so the hero never turns to face / swing at a foe
            //    walled off from them.
            if (target == null)
            {
                float range = EffectiveRange();
                Collider[] hits = Physics.OverlapSphere(transform.position, range, _enemyLayer);
                float bestSqr = float.MaxValue;
                foreach (var col in hits)
                {
                    if (col == null) continue;
                    var d = col.GetComponentInParent<IDamageable>();
                    if (d == null || !d.IsAlive || d.Faction != CombatFaction.Hostile) continue;
                    if (!HasLoS(d)) continue;   // WO-449: skip walled-off foes
                    float sqr = (d.WorldPosition - transform.position).sqrMagnitude;
                    if (sqr < bestSqr) { bestSqr = sqr; target = d; }
                }
            }

            if (target == null) return;   // nothing to face — swing as before
            FlowTrace.Step("Combat", "PlayerAttack: facing melee target (LoS-cleared, WO-449)");
            _loco.FaceToward(target.WorldPosition);
        }

        /// <summary>
        /// WO-449: true when the hero has a clear line-of-sight to <paramref name="target"/>
        /// (no wall/structure between them) — so a melee swing can't damage an enemy through a
        /// wall the hero is standing against. Linecasts from an eye point on the hero to a TORSO
        /// point on the target (not feet) so a slope/curb under either doesn't false-block.
        /// DEGRADE: an unset mask (value == 0) → treat LoS as always clear (radius-only, legacy
        /// behaviour) so a misconfigured mask never makes the hero unable to hit anything.
        /// Mirrors HeroTargetIndicator.HasLoS (the reticle's proven WO-449 gate).
        /// </summary>
        private bool HasLoS(IDamageable target)
        {
            if (target == null) return false;
            if (_losMask.value == 0) return true;   // degrade: LoS gate disabled
            Vector3 eye   = transform.position + Vector3.up * 1.4f;
            Vector3 torso = target.WorldPosition + Vector3.up * 1.0f;
            // Clear when the linecast hits NOTHING between eye and torso.
            return !Physics.Linecast(eye, torso, _losMask, QueryTriggerInteraction.Ignore);
        }

        private IEnumerator ResolveAttack()
        {
            // WO-217: land the hit on the IMPACT FRAME (when the weapon contacts the
            // target) rather than at the swing start, so the damage + "connect" feel
            // sync to the snap of the animation, not the wind-up. Data-driven via
            // _impactFrameDelay; falls back to the perfect-window start when unset (0).
            float hitDelay = _impactFrameDelay > 0f ? _impactFrameDelay : _perfectHitWindowStart;
            yield return new WaitForSeconds(hitDelay);

            float elapsed   = Time.time - _swingStartTime;
            bool isPerfect  = elapsed >= _perfectHitWindowStart
                           && elapsed <= _perfectHitWindowEnd;
            bool riposte    = Time.time <= _riposteArmedUntil;   // empowered counter after a parry

            Collider[] hits = Physics.OverlapSphere(transform.position, EffectiveRange(), _enemyLayer);

            // Gear v1: the equipped weapon's damageMult multiplies the melee swing — the SAME
            // scalar HeroAbilities folds into ability casts (base x talent x level x timing x
            // WEAPON). Previously the melee basic attack only read the weapon for reach, so a
            // better blade changed range but not damage; now equipping a stronger weapon is
            // FELT on every swing (e.g. Iron Longsword's 1.25 = +25% melee damage vs starter).
            // Same field, same lazy _gear cache used by EffectiveRange — no new stat system.
            // Graceful: no loadout / no weapon = 1.0, so combat is unchanged.
            if (_gear == null) _gear = GetComponent<GearLoadout>();
            float weaponMult = _gear != null ? _gear.WeaponMult : 1f;

            bool anyHit = false;
            float lastHitDamage = 0f;   // WO-497: scales the landing rumble
            foreach (var col in hits)
            {
                if (col == null) continue;
                var damageable = col.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive) continue;
                if (damageable.Faction != CombatFaction.Hostile)      continue;

                // WO-449: reject a hit when a wall/structure blocks the swing's line-of-sight,
                // so the hero standing against a wall can't damage an enemy on the far side.
                // Degrades clear when the mask is unset (HasLoS) so a misconfig never no-ops melee.
                if (!HasLoS(damageable))
                {
                    FlowTrace.Warn("Combat", "PlayerAttack: hostile in melee radius REJECTED — wall blocks line-of-sight (WO-449)");
                    continue;
                }

                float damage = _baseDamage * weaponMult;
                if (isPerfect) damage *= _perfectHitMultiplier;
                if (riposte)   damage *= RiposteMultiplier;   // parry counter — big hit on the tank

                Vector3 hitPos = col.transform.position + Vector3.up;

                // WO-219 reconcile: damage routes through Enemy.TakeDamageFrom, which
                // already fires the floating damage number + CombatFeedbackManager.Hit
                // (hit-stop/combo/shake) + the impact burst centrally. Calling them again
                // here double-spawned the number and restarted the hit-stop twice per
                // enemy. Drop the duplicate calls — TakeDamage is the single feedback
                // entry point. (Non-Enemy IDamageable targets simply skip the extra feel,
                // which is acceptable — only enemies are hostile melee targets.)
                damageable.TakeDamage(damage, DamageElement.None);
                anyHit = true;
                lastHitDamage = damage;

                if (isPerfect)
                    TriggerPerfectHitFeedback(hitPos);
            }

            // Impact audio — the meaty "connect" the swing was missing. Melee classes get a
            // weapon clash; casters get a spell-hit zap. (TakeDamageFrom already plays the
            // enemy's own hit grunt + the central hit-stop/shake; this is the WEAPON sound.)
            if (anyHit)
            {
                string cls = _abilities != null ? _abilities.HeroClass : null;
                if (cls == "mage" || cls == "cleric") GameSfx.PlaySpellCast();
                else GameSfx.PlaySwordClash();

                // WO-497: rumble on the swing CONNECTING (was rumble-on-take-damage only).
                // Intensity scales with the damage dealt; null-safe (no gamepad = no-op).
                if (_impactFeedback == null) _impactFeedback = GetComponent<HeroImpactFeedback>();
                _impactFeedback?.PlayHaptic(Mathf.Clamp(0.2f + lastHitDamage * 0.004f, 0.2f, 0.6f), 0.10f);

                if (riposte)
                {
                    DamageNumberSpawner.SpawnLabel("RIPOSTE!", transform.position + Vector3.up * 1.8f,
                        new Color(1f, 0.85f, 0.2f), 1.6f);
                    _riposteArmedUntil = 0f;   // one empowered counter per parry
                }
            }

            _isInSwing = false;

            // WO-219: stop EMITTING new trail segments once the active window + a short
            // linger has passed, so the swing arc tapers off instead of snapping. The
            // existing tail keeps rendering until _trailTime elapses; the next swing
            // Clears it. This runs after _isInSwing is cleared so it never gates input.
            StartCoroutine(StopTrailAfterLinger());
        }

        /// <summary>WO-219: ends trail emission after the active window's linger.</summary>
        private IEnumerator StopTrailAfterLinger()
        {
            float activeWindow = Mathf.Max(0f, _perfectHitWindowEnd - _perfectHitWindowStart);
            yield return new WaitForSeconds(activeWindow + _trailLinger);
            if (_swingTrail != null) _swingTrail.emitting = false;
        }

        /// <summary>
        /// WO-219: lazily builds the code-built swing TrailRenderer on the resolved
        /// origin transform. Origin priority: explicit <see cref="_trailOrigin"/> →
        /// a right-hand humanoid bone → a child placed at the hero's attack origin.
        /// Cheap (short time, 2-point gradient, additive-ish unlit) and asset-free.
        /// </summary>
        private void EnsureSwingTrail()
        {
            if (_swingTrail != null) return;

            Transform origin = _trailOrigin;
            if (origin == null && _animator != null && _animator.isHuman)
                origin = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (origin == null)
            {
                // Fallback: a child at the hero's attack origin (forward + waist height),
                // so the trail still draws even on a non-humanoid / unrigged test body.
                var holder = new GameObject("SwingTrailOrigin");
                holder.transform.SetParent(transform, false);
                holder.transform.localPosition = new Vector3(0.4f, 1.1f, 0.5f);
                origin = holder.transform;
            }

            var go = new GameObject("SwingTrail");
            go.transform.SetParent(origin, false);
            go.transform.localPosition = Vector3.zero;

            _swingTrail = go.AddComponent<TrailRenderer>();
            _swingTrail.time = _trailTime;
            _swingTrail.startWidth = _trailStartWidth;
            _swingTrail.endWidth = 0f;
            _swingTrail.numCornerVertices = 2;
            _swingTrail.numCapVertices = 2;
            _swingTrail.minVertexDistance = 0.02f;
            _swingTrail.autodestruct = false;
            _swingTrail.emitting = false;
            _swingTrail.alignment = LineAlignment.View;

            // URP-safe unlit material so the trail isn't magenta in a URP build
            // (same missing-shader guard the ability VFX uses). Only swap when a
            // known shader resolves in THIS build.
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Sprites/Default");
            if (sh != null) _swingTrail.material = new Material(sh);

            // WO-504 slice 3: color + width are driven by the EQUIPPED WEAPON's rarity
            // (and a makersMark theme tint) so a legendary blade reads legendary even on
            // the shared mesh. Applied here on build and re-applied on every OnGearChanged.
            ApplyWeaponTrailVfx();
        }

        /// <summary>
        /// WO-504 slice 3: drive the swing trail's color + width from the equipped weapon's
        /// rarity via the pure <see cref="WeaponVfxMap"/> resolver (null-safe — no loadout /
        /// no weapon -> the steel common default, identical to the legacy hard-coded look).
        /// Re-applied on OnGearChanged so swapping a blade re-tints the arc immediately.
        /// </summary>
        private void ApplyWeaponTrailVfx()
        {
            if (_swingTrail == null) return;

            if (_gear == null) _gear = GetComponent<GearLoadout>();
            WeaponDef w = _gear != null ? _gear.EquippedWeapon : null;
            WeaponVfxProfile vfx = WeaponVfxMap.Resolve(w);

            _trailColor = vfx.TrailColor;
            _trailStartWidth = vfx.TrailWidth;

            _swingTrail.startWidth = _trailStartWidth;
            _swingTrail.endWidth = 0f;

            // Colour gradient: bright at the swing edge -> transparent tail.
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(_trailColor, 0f), new GradientColorKey(_trailColor, 1f) },
                new[] { new GradientAlphaKey(_trailColor.a, 0f), new GradientAlphaKey(0f, 1f) });
            _swingTrail.colorGradient = grad;
        }

        private void TriggerPerfectHitFeedback(Vector3 hitPos)
        {
            if (_perfectHitSound != null)
                _audioSource.PlayOneShot(_perfectHitSound, 1.0f);

            // "PERFECT!" label above the hit — uses the project's DamageNumberSpawner.
            DamageNumberSpawner.SpawnLabel("PERFECT!", hitPos + Vector3.up * 1.2f,
                new Color(1f, 0.93f, 0.2f), 1.5f);
        }

        // ── Audio ─────────────────────────────────────────────────────────────

        private void PlayWhoosh()
        {
            if (_whooshSounds == null || _whooshSounds.Length == 0) return;
            var clip = _whooshSounds[Random.Range(0, _whooshSounds.Length)];
            if (clip == null) return;

            _audioSource.pitch = Random.Range(_whooshPitchRange.x, _whooshPitchRange.y);
            _audioSource.PlayOneShot(clip, 0.7f);
            StartCoroutine(ResetPitchAfter(clip.length));
        }

        private IEnumerator ResetPitchAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            _audioSource.pitch = 1f;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, Application.isPlaying ? EffectiveRange() : _attackRange);
        }
#endif
    }
}
