// =============================================================================
// WeaponTrailController — WO-VFX-WEAPON-TRAILS: one reusable blade-trail flash on
// EVERY attack/cast, driven by the Core-pure ActorAnimator.AttackStarted event.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHY THIS EXISTS:
//   Weapon-trail VFX used to live INSIDE PlayerAttackController (WO-219 / WO-504
//   slice 3) — a code-built TrailRenderer lit at swing start, tinted by the equipped
//   weapon's rarity (WeaponVfxMap). That coupled the trail to the hero's melee
//   controller, so ability casts, pets, and ENEMY swings (which share the same
//   ActorAnimator rig) got no trail. This component GENERALISES that look onto ANY
//   actor: it subscribes to the sibling ActorAnimator.AttackStarted (raised at the
//   end of every PlayAttack/PlayCast — hero, enemy, pet) and flashes the SAME
//   rarity-driven blade trail with ZERO per-ability wiring.
//
// PRESENTATION-LAYER (ARCHITECTURE §2): this only STYLES a signal Core raises — it
// never drives combat. Core cannot reference Village, so the seam is the event, not
// a direct call. Colourblind-friendly by design: the trail reads by MOTION + SHAPE
// (a bright arc that follows the blade), not by colour alone.
//
// BLADE ANCHOR (fallback ladder, mirrors the retired PlayerAttackController path):
//   EquipmentController.GripRoot (the actual held-weapon prop) → the Humanoid
//   RightHand bone → a synthetic child at the actor's attack origin. Re-resolved on
//   each fire, so a weapon swap (which destroys the old grip root) rebuilds the trail
//   on the new prop automatically.
// =============================================================================

using System.Collections;
using UnityEngine;
using DeNelle.Core.Combat;

namespace DeNelle.Village
{
    /// <summary>
    /// Flashes a rarity-tinted blade trail on every attack/cast the sibling
    /// <see cref="ActorAnimator"/> raises via <see cref="ActorAnimator.AttackStarted"/>.
    /// Add once to any actor rig (hero / enemy / pet); self-resolves its dependencies.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponTrailController : MonoBehaviour
    {
        [Tooltip("TrailRenderer 'time' (seconds a trail segment persists). Keep short for a crisp swing arc.")]
        [SerializeField, Range(0.02f, 0.4f)] private float _trailTime = 0.14f;

        [Tooltip("Seconds the trail keeps EMITTING per swing (the active swing arc) before it stops.")]
        [SerializeField, Range(0.05f, 0.4f)] private float _activeWindow = 0.18f;

        [Tooltip("Extra seconds the trail keeps emitting after the active window before fading out.")]
        [SerializeField, Range(0f, 0.3f)] private float _trailLinger = 0.06f;

        [Tooltip("Optional explicit blade/hand transform the trail follows. When null, the controller " +
                 "auto-resolves EquipmentController.GripRoot -> RightHand bone -> a synthetic child.")]
        [SerializeField] private Transform _explicitOrigin;

        // ── Runtime ───────────────────────────────────────────────────────────
        private ActorAnimator     _actor;
        private GearLoadout        _gear;
        private EquipmentController _equipment;
        private Animator           _animator;

        private TrailRenderer _trail;
        private Transform     _trailOrigin;      // the transform the current _trail is parented to
        private Coroutine     _stopRoutine;
        private bool          _subscribed;

        // Cached applied look (rarity-driven, via WeaponVfxMap) — also the test-seam return value.
        private Color _trailColor = WeaponVfxMap.SteelColor;
        private float _trailStartWidth = WeaponVfxMap.CommonWidth;

        private void Awake()
        {
            // The trail is driven by the actor's attack signal — resolve (or add) the sibling driver.
            if (!TryGetComponent(out _actor)) _actor = gameObject.AddComponent<ActorAnimator>();
            _gear      = GetComponent<GearLoadout>();          // null-safe: no loadout -> steel default
            _equipment = GetComponent<EquipmentController>();  // null-safe: no equipment -> bone/synthetic anchor
            Subscribe();
        }

        private void OnEnable()  => Subscribe();
        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Subscribe()
        {
            if (_subscribed || _actor == null) return;
            _actor.AttackStarted += OnAttackStarted;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _actor == null) { _subscribed = false; return; }
            _actor.AttackStarted -= OnAttackStarted;
            _subscribed = false;
        }

        /// <summary>
        /// Fired at the end of every PlayAttack/PlayCast. <paramref name="code"/> is the swing combo
        /// index (&gt;= 0) or a cast code (&lt; 0) — we flash the same blade trail for both.
        /// </summary>
        private void OnAttackStarted(int code)
        {
            Transform origin = ResolveBladeOrigin(out bool isGripRootAnchor);
            EnsureTrail(origin, isGripRootAnchor);
            ApplyWeaponTrailVfx();

            if (_trail != null)
            {
                _trail.Clear();            // drop any stale segment from the previous swing
                _trail.emitting = true;
                if (_stopRoutine != null) StopCoroutine(_stopRoutine);
                _stopRoutine = StartCoroutine(StopAfterActive());
            }

            // Per-swing "Melee_Slash" burst RETIRED (owner directive 2026-07-12: motion VFX are
            // owner-authored only — this hardcoded key fired on EVERY swing AND cast regardless
            // of ability, the one non-data-driven hero fire site). The blade TrailRenderer above
            // stays: it is the weapon-trail feature itself, not a Hovl motion effect. To give a
            // swing a burst again, bind a vfxKey on its motion-castings row in the Motion Caster.
        }

        private IEnumerator StopAfterActive()
        {
            yield return new WaitForSeconds(Mathf.Max(0f, _activeWindow) + Mathf.Max(0f, _trailLinger));
            if (_trail != null) _trail.emitting = false;
            _stopRoutine = null;
        }

        /// <summary>
        /// Blade anchor, re-resolved each fire (fallback ladder — mirrors the retired
        /// PlayerAttackController.EnsureSwingTrail): explicit override -> the equipped weapon's
        /// grip root -> the Humanoid RightHand bone -> a synthetic child at the attack origin.
        /// <paramref name="isGripRootAnchor"/> reports WHICH rung answered, because the grip-root
        /// rung is scale-normalized by EquipmentController and the others are not — see EnsureTrail.
        /// </summary>
        private Transform ResolveBladeOrigin(out bool isGripRootAnchor)
        {
            isGripRootAnchor = false;

            if (_explicitOrigin != null) return _explicitOrigin;

            // The actual held-weapon prop — best anchor so the trail rides the blade tip.
            if (_equipment == null) _equipment = GetComponent<EquipmentController>();
            if (_equipment != null && _equipment.GripRoot != null)
            {
                isGripRootAnchor = true;
                return _equipment.GripRoot;
            }

            // Humanoid RightHand bone.
            if (_animator == null) _animator = GetComponentInChildren<Animator>(true);
            if (_animator != null && _animator.isHuman)
            {
                var hand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (hand != null) return hand;
            }

            // Synthetic child so the trail still draws on a non-humanoid / unrigged test body.
            var holder = transform.Find("WeaponTrailOrigin");
            if (holder == null)
            {
                var go = new GameObject("WeaponTrailOrigin");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0.4f, 1.1f, 0.5f);
                holder = go.transform;
            }
            return holder;
        }

        /// <summary>
        /// Lazily (re)builds the code-built TrailRenderer on <paramref name="origin"/>. Rebuilds when
        /// the origin changed (a weapon swap destroys the old grip root, so the cached trail becomes
        /// null and we build afresh on the new prop). Cheap, asset-free, URP-safe.
        /// </summary>
        private void EnsureTrail(Transform origin, bool isGripRootAnchor)
        {
            if (origin == null) return;
            if (_trail != null && _trailOrigin == origin) return;   // still valid on the same anchor

            if (_trail != null) Destroy(_trail.gameObject);         // origin changed -> drop the stale trail

            var go = new GameObject("WeaponTrail");
            go.transform.SetParent(origin, false);
            go.transform.localPosition = Vector3.zero;
            // TrailRenderer width is multiplied by the transform's lossyScale, so the anchor's scale
            // leaks into the trail. The two rungs of the ladder need OPPOSITE treatment — the
            // asymmetry is deliberate, not an oversight:
            //
            //   NOT the grip root (enemy / explicit override / synthetic child): the anchor is a raw
            //   bone, whose lossyScale carries VisualFactory's spawn Fit factor undivided — 1.887x on
            //   the orc shaman, which rendered its trail ~1.9x too wide (owner F8 seq=652). That
            //   factor is body normalization, nothing an author chose, so divide it out.
            //
            //   THE GRIP ROOT (hero): EquipmentController.CompensateParentScale already divided the
            //   bone's Fit factor out, and deliberately LEAVES the grip root at the owner-dialled
            //   offsets.json scale (the 2026-07-07 scale-parity fix). Compensating here would divide
            //   out that authored value — sword_D at 0.47 would render its trail 2.13x too wide. So
            //   the hero keeps Unity's default localScale of one and is untouched by this fix.
            if (!isGripRootAnchor)
                go.transform.localScale = EquipmentController.ParentScaleCompensation(origin);

            _trail = go.AddComponent<TrailRenderer>();
            _trail.time = _trailTime;
            _trail.startWidth = _trailStartWidth;
            _trail.endWidth = 0f;
            _trail.numCornerVertices = 2;
            _trail.numCapVertices = 2;
            _trail.minVertexDistance = 0.02f;
            _trail.autodestruct = false;
            _trail.emitting = false;
            _trail.alignment = LineAlignment.View;

            // URP-safe unlit material so the trail isn't magenta in a URP build (same missing-shader
            // guard the ability VFX uses). Only swap when a known shader resolves in THIS build.
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Sprites/Default");
            if (sh != null) _trail.material = new Material(sh);

            _trailOrigin = origin;
            ApplyWeaponTrailVfx();   // seat the current weapon's rarity look immediately
        }

        /// <summary>
        /// Drive the trail's colour + width from the equipped weapon's rarity via the pure
        /// <see cref="WeaponVfxMap"/> resolver (null-safe — no loadout / no weapon -> the steel
        /// common default, identical to the legacy WO-504 look). Re-applied on every fire so a
        /// blade swap re-tints the arc without a dedicated OnGearChanged subscription.
        /// </summary>
        private void ApplyWeaponTrailVfx()
        {
            if (_gear == null) _gear = GetComponent<GearLoadout>();
            WeaponDef w = _gear != null ? _gear.EquippedWeapon : null;
            WeaponVfxProfile vfx = WeaponVfxMap.Resolve(w);

            _trailColor = vfx.TrailColor;
            _trailStartWidth = vfx.TrailWidth;

            if (_trail == null) return;

            _trail.startWidth = _trailStartWidth;
            _trail.endWidth = 0f;

            // Colour gradient: bright HDR head at the swing edge -> transparent tail (bloom-aware,
            // mirrors WO-504 slice 3 — a mild ~1.6x HDR head so the arc catches the soft bloom halo
            // while the rarity colour still reads true through the tail).
            Color head = new Color(_trailColor.r * 1.6f, _trailColor.g * 1.6f, _trailColor.b * 1.6f, _trailColor.a);
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(head, 0f), new GradientColorKey(_trailColor, 0.35f), new GradientColorKey(_trailColor, 1f) },
                new[] { new GradientAlphaKey(_trailColor.a, 0f), new GradientAlphaKey(0f, 1f) });
            _trail.colorGradient = grad;
        }

        // ---------------------------------------------------------------------
        //  HEADLESS ORACLE SEAM (WO-504 s3 -> WO-VFX-WEAPON-TRAILS): build the real
        //  trail and apply the equipped weapon's rarity-driven VFX, then report the
        //  applied colour so DeNelle.Editor's ArenaCombatOracle can PROVE the controller
        //  applied a NON-default (non-steel) trail for a high-rarity blade — exercising
        //  the SAME EnsureTrail + ApplyWeaponTrailVfx path a live swing runs (no fork).
        //  Editor/QA seam only — gameplay never calls this. PlayerAttackController
        //  .ApplyWeaponTrailVfxForTest delegates here so the oracle keeps compiling.
        // ---------------------------------------------------------------------
        public Color ApplyWeaponTrailVfxForTest()
        {
            Transform origin = ResolveBladeOrigin(out bool isGripRootAnchor);
            EnsureTrail(origin, isGripRootAnchor);   // real lazy build + first ApplyWeaponTrailVfx
            ApplyWeaponTrailVfx();     // re-apply against the now-current equipped weapon
            return _trailColor;
        }
    }
}
