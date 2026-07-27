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
using DeNelle.Core.UI;            // P23: CombatText — pooled/capped/deduped stamps (§1.8)

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

        // WO-VFX-WEAPON-TRAILS: the swing-trail VFX (WO-219 / WO-504 s3) MOVED OUT of this
        // controller into the reusable DeNelle.Village.WeaponTrailController, which flashes the
        // rarity-tinted blade trail off ActorAnimator.AttackStarted — so hero swings, casts, and
        // ENEMY swings (shared rig) all get a trail with no per-ability wiring. This controller
        // only ENSURES the component is present (Awake) and forwards the headless test seam.

        // ── Runtime ───────────────────────────────────────────────────────────

        private AudioSource  _audioSource;
        private float        _nextAttackTime;
        private float        _swingStartTime;
        private bool         _isInSwing;

        // Owner rule (2026-06-24): combat moves (attack/block/parry) only process while a
        // battle is live (BattleLock.IsInBattle()). This latch tracks the suppressed<->live
        // transition so the FlowTrace fires ONCE per transition (not every frame in town).
        private bool         _combatInputSuppressed;

        // WO-VFX-WEAPON-TRAILS: the shared blade-trail component (ensured in Awake). Cached only to
        // forward the headless ArenaCombatOracle test seam (ApplyWeaponTrailVfxForTest) onto it.
        private WeaponTrailController _trailController;

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

            if (_gear == null) _gear = GetComponent<GearLoadout>();

            // WO-VFX-WEAPON-TRAILS: ensure the shared blade-trail component is present on this rig,
            // so the hero always gets a swing/cast trail (it self-drives off ActorAnimator.
            // AttackStarted). DisallowMultipleComponent makes a double-add a no-op; the trail's own
            // ApplyWeaponTrailVfx re-tints per swing, so no OnGearChanged subscription is needed here.
            _trailController = TryGetComponent<WeaponTrailController>(out var tc)
                            ? tc : gameObject.AddComponent<WeaponTrailController>();
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

            // OWNER RULE 2026-06-24 ("in town / non-combat, NO button creates combat moves"):
            // attack / block / parry are COMBAT moves and must only process while a battle is
            // actually live (in the BattleArena). Gate on the canonical, assembly-neutral
            // BattleLock.IsInBattle() — the single source of truth every battle owner
            // (BattleArena, ArenaMode, ATBCombatManager) registers its in-progress probe into.
            // When NOT in battle (town MainCastle_Hall / overworld walk): suppress the swing,
            // drop any held block, and skip UpdateBlock so RMB/Shift can't raise a shield or
            // fire the parry-ward nova. Movement, camera, interaction, build-mode and NPC-talk
            // are UNTOUCHED — only the combat inputs are gated here.
            if (!BattleLock.IsInBattle())
            {
                if (_blocking)
                {
                    _blocking = false;
                    _actor?.SetBlocking(false);
                }
                if (!_combatInputSuppressed)
                {
                    _combatInputSuppressed = true;
                    FlowTrace.Step("Combat", "input gated: not in battle (town/overworld) - combat moves suppressed");
                }
                // §12 outgoing-attack trace (2026-06-30 "0 damage in dungeon"): PROVE a melee swing was
                // pressed but SUPPRESSED because BattleLock is false — the exact dungeon 0-damage cause
                // (in-place hollows staged no battle). Should stop once a hollow engages (HeroCombatEngagement).
                bool meleePressed =
                    (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
                    (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame) ||
                    UnityEngine.Input.GetMouseButtonDown(0);
                if (meleePressed)
                    FlowTrace.Throttle("Combat", "melee-suppressed", 1f,
                        "MELEE swing pressed but SUPPRESSED — BattleLock.IsInBattle()=false (no active battle). " +
                        "Hero deals 0 damage here until a battle is staged OR a hero-only enemy engages.");
                return;
            }
            if (_combatInputSuppressed)
            {
                _combatInputSuppressed = false;
                FlowTrace.Step("Combat", "input live: battle started - combat moves enabled");
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
            // P23 §0 fix (HUD_OBSIDIAN §1.8): the old DamageNumberSpawner.SpawnLabel (:140) was
            // pooled but UNCAPPED + UN-DEDUPED world-space TextMesh at 1.4x — every parried hit
            // inside the 0.25s window stacked another giant label. CombatText is the pooled(6)/
            // capped/0.5s-deduped screen-space stamp: repeats become ONE "PARRY! x N".
            CombatText.Show(CombatTextKind.Parry, "PARRY!", pos + Vector3.up * 1.4f);
        }

        // ── Attack flow ───────────────────────────────────────────────────────

        /// <summary>
        /// HUD seam (Option 1, owner 2026-06-27): fire ONE basic-attack swing from a UI button —
        /// the big bottom-right "basic attack" button — exactly as a Space / LMB / gamepad-South
        /// press would in <see cref="Update"/>. Honors the SAME gates: only while a battle is live
        /// (BattleLock.IsInBattle), input not suppressed, not already mid-swing, and off the swing
        /// cooldown. Returns true when a swing actually started. The keyboard/mouse path is untouched.
        /// </summary>
        public bool TriggerBasicAttack()
        {
            if (HeroLocomotion.InputSuppressed) return false;
            if (!BattleLock.IsInBattle()) return false;
            if (_isInSwing || Time.time < _nextAttackTime) return false;
            StartAttack();
            return true;
        }

        private void StartAttack()
        {
            _nextAttackTime = Time.time + _attackCooldown;
            _swingStartTime = Time.time;
            _isInSwing      = true;

            // #51: the whoosh on the swing itself (the "swish" before the clash). The clash plays
            // later only when ResolveAttack connects, so swing + hit read as two distinct beats.
            GameSfx.PlaySwordSwing();

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

            // WO-VFX-WEAPON-TRAILS: the blade trail now lights up via WeaponTrailController's
            // subscription to _actor.AttackStarted (fired inside PlayAttack/PlayCast above) — no
            // explicit call here.

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
                // Ticket #61: mark this as a HERO strike so its combo / kill-streak / RAMPAGE
                // feedback fires (tower / pet / DoT never stamp -> never feed the combo).
                (damageable as DeNelle.Core.Combat.IHeroDamageMarkable)?.MarkNextHitFromHero();
                // §12 outgoing-attack trace (2026-06-30): PROVE the melee swing LANDS as a hero-dealt
                // hit — the counterpart to Enemy's "CombatFeedback Hit gated: dealtByHero=..." line. On
                // the next felt-test this MUST read dealtByHero=True (was never emitted while suppressed).
                FlowTrace.Step("Combat",
                    $"hero MELEE hit '{col.transform.root.name}' faction={damageable.Faction} " +
                    $"dealtByHero=True amount={damage:F1} (perfect={isPerfect} riposte={riposte}).");
                damageable.TakeDamage(damage, DamageElement.None);
                anyHit = true;
                lastHitDamage = damage;

                // Owner VfxManualPicks: elemental sword on-hit (Weaponskillsword_Impact —
                // "elemental Sword - New" roster). Layered on TakeDamage's central feedback;
                // null-safe no-op if the catalog row is missing. Perfect hits also get the
                // heavier Knight weaponskill burst so a timed connect reads bigger.
                Guard.Try("Combat", "melee sword impact vfx", () =>
                {
                    VFXManager.PlayKey("Weaponskillsword_Impact", hitPos, Quaternion.identity, null, null);
                    if (isPerfect)
                        VFXManager.PlayKey("KnightWeaponskill_Impact", hitPos, Quaternion.identity, null, null, 1.15f);

                    // ELEMENTAL brand on-hit (data-driven via WeaponDef.element; WeaponVfxMap is
                    // the ONE reader). A weapon carrying element:"fire" ADDS a full multi-layer
                    // fire impact burst at the hit point, layered on the weaponskill impact — so an
                    // elemental blade is VISUALLY read in combat. Reuses the shared VFXManager pool
                    // (no raw Instantiate). No element -> null key -> unchanged behavior. Not
                    // hardcoded to one weapon id: any element:"fire" weapon lights up.
                    string elementKey = WeaponVfxMap.ElementalOnHitKey(_gear != null ? _gear.EquippedWeapon : null);
                    if (!string.IsNullOrEmpty(elementKey))
                        VFXManager.PlayKey(elementKey, hitPos, Quaternion.identity, null, null,
                                           isPerfect ? 1.25f : 0f);
                });

                // WO-566: v2 talent on-hit procs (Knight Emberbrand Strike burn). Apply each owned
                // proc as a DoT on the struck enemy, rolling its chance. Data-driven + identity
                // (no procs) until the node is learned. Enemy is the only hostile melee target.
                var procEnemy = col.GetComponentInParent<Enemy>();
                if (procEnemy != null && !procEnemy.IsDead)
                {
                    string procClass = _abilities != null ? _abilities.HeroClass : "knight";
                    Vector3 procSource = transform.position + Vector3.up * 1.0f;
                    DeNelle.Village.Talents.HeroTalentModifiers.ForEachOnHitProc(procClass, spec =>
                    {
                        if (Random.value > spec.Chance) return;
                        procEnemy.ApplyDamageOverTime(spec.Dps, spec.Duration, procSource);
                        FlowTrace.Throttle("HeroTalents", "proc-" + spec.NodeId, 1f,
                            $"on-hit proc {spec.NodeId}: {spec.Dps:F0} dps for {spec.Duration:F0}s applied to enemy.");
                    });
                }

                if (isPerfect)
                    TriggerPerfectHitFeedback(hitPos);
            }

            // §12 outgoing-attack trace (2026-06-30): the swing FIRED (BattleLock was live) but
            // connected with nothing — splits "hero can't attack (gated)" from "attacked but no
            // hostile in reach / LoS-blocked". Throttled so a flurry of empty swings doesn't spam.
            if (!anyHit)
                FlowTrace.Throttle("Combat", "melee-whiff", 1f,
                    $"hero MELEE swing FIRED but hit nothing (candidates={hits.Length}, reach={EffectiveRange():F1}m) " +
                    "— in battle, but no hostile IDamageable in reach/LoS.");

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
                    // P23 §0 fix (§1.8): screen-space pooled stamp — never a stacking 1.6x TextMesh.
                    CombatText.Show(CombatTextKind.Riposte, "RIPOSTE!", transform.position + Vector3.up * 1.8f);
                    _riposteArmedUntil = 0f;   // one empowered counter per parry
                }
            }

            _isInSwing = false;

            // WO-VFX-WEAPON-TRAILS: trail emission is stopped by WeaponTrailController's own
            // active-window coroutine (started when it received AttackStarted) — nothing to do here.
        }

        // ---------------------------------------------------------------------
        //  HEADLESS ORACLE SEAM (WO-504 s3 -> WO-VFX-WEAPON-TRAILS) — the swing-trail
        //  build + rarity apply now live on WeaponTrailController. This forwarder keeps
        //  the ArenaCombatOracle (which calls attack.ApplyWeaponTrailVfxForTest()) compiling
        //  and exercising the SAME EnsureTrail + ApplyWeaponTrailVfx path a live swing runs.
        //  Ensures the component (the oracle AddComponents this controller in isolation, so
        //  Awake may not have run its ensure yet). Editor/QA seam only — gameplay never calls it.
        // ---------------------------------------------------------------------
        public Color ApplyWeaponTrailVfxForTest()
        {
            if (_trailController == null)
                _trailController = TryGetComponent<WeaponTrailController>(out var tc)
                                ? tc : gameObject.AddComponent<WeaponTrailController>();
            return _trailController.ApplyWeaponTrailVfxForTest();
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
