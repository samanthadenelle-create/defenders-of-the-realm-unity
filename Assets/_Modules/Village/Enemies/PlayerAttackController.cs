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
        private int   _comboIndex;
        private float _lastSwingTime;
        private const int   ComboLength    = 3;     // Knight melee combo swings
        private const float ComboResetGap  = 1.1f;  // seconds idle before the combo resets to 0

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _animator    = GetComponentInChildren<Animator>();
            _actor       = GetComponent<ActorAnimator>() ?? gameObject.AddComponent<ActorAnimator>();
            _abilities   = GetComponent<HeroAbilities>();
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 0f; // 2D — punchy response without attenuation

            // Bug-fix (audit 2026-05-30): an unset _enemyLayer (mask 0 = "Nothing") makes every
            // OverlapSphere return empty, so a runtime-built hero's melee silently hits nothing.
            // Default to the Enemy layer, then to Everything — matching HeroHealth/HeroAbilities.
            if (_enemyLayer == 0) _enemyLayer = LayerMask.GetMask("Enemy");
            if (_enemyLayer == 0) _enemyLayer = ~0;
        }

        private void Update()
        {
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
            foreach (var col in hits)
            {
                if (col == null) continue;
                var damageable = col.GetComponentInParent<IDamageable>();
                if (damageable == null || !damageable.IsAlive) continue;
                if (damageable.Faction != CombatFaction.Hostile)      continue;

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

            // Colour gradient: bright at the swing edge → transparent tail.
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(_trailColor, 0f), new GradientColorKey(_trailColor, 1f) },
                new[] { new GradientAlphaKey(_trailColor.a, 0f), new GradientAlphaKey(0f, 1f) });
            _swingTrail.colorGradient = grad;

            // URP-safe unlit material so the trail isn't magenta in a URP build
            // (same missing-shader guard the ability VFX uses). Only swap when a
            // known shader resolves in THIS build.
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Sprites/Default");
            if (sh != null) _swingTrail.material = new Material(sh);
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
