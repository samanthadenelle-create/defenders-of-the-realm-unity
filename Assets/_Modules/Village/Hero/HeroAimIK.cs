// =============================================================================
// HeroAimIK — DEF-70 (1): upper-body aim IK toward a runtime aim target.
// -----------------------------------------------------------------------------
// namespace DeNelle.Village (this is the ONLY hero assembly on this branch — the
// spec's DeNelle.Core.Progression / Village.VFX assemblies do NOT exist here).
//
// SPEC RECONCILIATION (read before touching the Animator):
//   • The spec wants an "Upper Body" Animator layer (index 1) with an Avatar Mask
//     covering spine+, blended at weight 0.7, and uses OnAnimatorIK to orient the
//     RightHand IK goal at the cursor world position from SmartMobileCamera.
//   • On THIS branch the hero controller is built by Assets/Editor/HeroAnimatorSetup.cs
//     and has a SINGLE layer (index 0) with parameters Speed(float) + Cast(trigger).
//     There is NO upper-body layer and NO avatar mask, and there is NO
//     SmartMobileCamera (the village uses ThirdPersonCameraFollow / VillageCamera).
//   • The Animator does NOT live on this GameObject — HeroBodySwapper instantiates
//     the class FBX as a "HeroBody" child at runtime and the Animator rides on it
//     (HeroLocomotion / HeroAbilities resolve it via GetComponentInChildren<Animator>
//     with a transform.Find("HeroBody") self-heal). OnAnimatorIK ONLY fires on a
//     MonoBehaviour that shares the Animator's GameObject, so this component cannot
//     receive the callback directly from the hero root. It therefore attaches a tiny
//     companion (HeroAimIKReceiver) onto the Animator's GameObject and feeds it the
//     weight + target every frame; the receiver owns OnAnimatorIK.
//
// WHAT THE CONTROLLER MUST ADD for full upper-body aim (documented, NOT authored —
// the orchestrator owns controller assets; do this in HeroAnimatorSetup.cs):
//   • A second Animator layer at INDEX 1 named "UpperBody", blendingMode Override,
//     defaultWeight 0.7, with an AvatarMask whose transforms cover Spine and above
//     (so the aim only bends the torso/arms, leaving locomotion on layer 0).
//   • _aimLayerIndex below defaults to 1 to match that layer; if the layer is absent
//     the IK still applies on layer 0 (Unity raises OnAnimatorIK per layer that has
//     IK Pass enabled — see _applyOnAnyLayer) so aim degrades gracefully to a
//     full-body lean rather than doing nothing.
//   • IMPORTANT: the layer's "IK Pass" toggle MUST be ON for OnAnimatorIK to fire
//     for that layer. HeroAnimatorSetup must set layer.ikPass = true.
//
// Until the controller defines layer 1 + mask, this component still compiles and
// runs: it will aim using whatever IK pass the controller exposes (full-body on
// layer 0 if IK Pass is enabled there). It never manipulates the transform directly
// (acceptance criterion) — all orientation goes through Animator IK goals.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Drives upper-body aim IK on the hero rig toward a runtime-assigned aim
    /// target. Lives on the hero ROOT (alongside HeroLocomotion); it locates the
    /// Animator on the swapped-in "HeroBody" child and routes the actual
    /// <c>OnAnimatorIK</c> work through a <see cref="HeroAimIKReceiver"/> attached
    /// to the Animator's GameObject (OnAnimatorIK only fires there).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroAimIK : MonoBehaviour
    {
        [Tooltip("Blend weight of the aim IK (0 = ignore, 1 = full). Spec default 0.7 so it reads on top of locomotion.")]
        [SerializeField, Range(0f, 1f)] private float _ikWeight = 0.7f;

        [Tooltip("Animator layer the aim IK applies to (1 = the upper-body layer the controller must add; see file header). " +
                 "If the controller has no such layer the receiver still applies IK on whatever layer raises OnAnimatorIK.")]
        [SerializeField] private int _aimLayerIndex = 1;

        [Tooltip("Set at runtime (e.g. by a future HeroInputHandler / camera). When null, IK weight collapses to 0.")]
        [SerializeField] private Transform _aimTarget;

        private Animator _animator;
        private HeroAimIKReceiver _receiver;

        /// <summary>Current aim IK blend weight (0..1).</summary>
        public float IkWeight { get => _ikWeight; set => _ikWeight = Mathf.Clamp01(value); }

        private void Awake()
        {
            // Same resolution path HeroLocomotion / HeroAbilities use: the Animator
            // rides on a child (the FBX body), not the hero root.
            _animator = GetComponentInChildren<Animator>();
            EnsureReceiver();
        }

        /// <summary>Assigns the world-space transform the upper body should aim at (null = no aim).</summary>
        public void SetAimTarget(Transform target)
        {
            _aimTarget = target;
            if (_receiver != null) _receiver.Configure(target, _ikWeight, _aimLayerIndex);
        }

        private void OnDisable()
        {
            // Drop IK influence so a disabled hero doesn't hold a stale aim pose.
            if (_receiver != null) _receiver.Configure(null, 0f, _aimLayerIndex);
        }

        private void Update()
        {
            // Self-heal the Animator reference: Awake() runs before HeroBodySwapper
            // swaps in the real FBX body, so the cached Animator can be null/stale.
            // HeroBodySwapper re-caches HeroLocomotion/HeroAbilities by reflection
            // but not this component, so re-resolve here (cheap; only while unwired).
            if (_animator == null || _receiver == null)
            {
                var bodyT = transform.Find("HeroBody");
                if (bodyT != null) _animator = bodyT.GetComponentInChildren<Animator>();
                if (_animator == null) _animator = GetComponentInChildren<Animator>();
                EnsureReceiver();
            }

            // Push the live weight + target each frame so inspector tweaks and a
            // late-assigned aim target both reach the receiver without polling there.
            if (_receiver != null) _receiver.Configure(_aimTarget, _aimTarget != null ? _ikWeight : 0f, _aimLayerIndex);
        }

        /// <summary>Attaches the OnAnimatorIK receiver to the Animator's GameObject (idempotent).</summary>
        private void EnsureReceiver()
        {
            if (_animator == null) return;
            if (_receiver != null && _receiver.gameObject == _animator.gameObject) return;

            _receiver = _animator.GetComponent<HeroAimIKReceiver>();
            if (_receiver == null) _receiver = _animator.gameObject.AddComponent<HeroAimIKReceiver>();
            _receiver.Bind(_animator);
            _receiver.Configure(_aimTarget, _aimTarget != null ? _ikWeight : 0f, _aimLayerIndex);
        }
    }

    /// <summary>
    /// The actual <c>OnAnimatorIK</c> handler. It MUST share the Animator's
    /// GameObject (Unity only raises OnAnimatorIK on the Animator's own
    /// MonoBehaviours), so <see cref="HeroAimIK"/> on the hero root adds this to the
    /// swapped-in "HeroBody" child and feeds it the target / weight. Orients the
    /// RightHand IK goal toward the aim target — no direct transform manipulation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroAimIKReceiver : MonoBehaviour
    {
        private Animator _animator;
        private Transform _aimTarget;
        private float _weight;
        private int _layerIndex = 1;

        /// <summary>Caches the Animator this receiver drives (called by HeroAimIK).</summary>
        public void Bind(Animator animator) => _animator = animator;

        /// <summary>Updates the aim target, blend weight, and the layer the IK applies to.</summary>
        public void Configure(Transform aimTarget, float weight, int layerIndex)
        {
            _aimTarget = aimTarget;
            _weight = Mathf.Clamp01(weight);
            _layerIndex = layerIndex;
        }

        private void Awake()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
        }

        // Unity built-in callback — fires once per Animator layer that has its
        // "IK Pass" enabled. We apply on the configured upper-body layer when the
        // controller exposes it, and tolerate layer 0 as a graceful fallback so aim
        // still reads on the single-layer controller this branch ships today.
        private void OnAnimatorIK(int layerIndex)
        {
            if (_animator == null || _aimTarget == null || _weight <= 0f) return;

            // Only act on the intended upper-body layer when it exists; if the
            // controller has just one layer (index 0), apply there instead so the
            // feature is not silently dead until the layer is authored.
            bool layerExists = _layerIndex >= 0 && _layerIndex < _animator.layerCount;
            int targetLayer = layerExists ? _layerIndex : 0;
            if (layerIndex != targetLayer) return;

            // Aim direction = from the hand's CURRENT rig position toward the target.
            // We read the hand bone BEFORE applying the IK goal so LookRotation is a
            // real bend, not the degenerate (target - target) zero vector you'd get
            // by querying the goal after SetIKPosition. Humanoid rigs expose the bone;
            // generic rigs (this branch's Tripo heroes) return null, in which case we
            // fall back to the animator root position so aim still resolves.
            Transform handBone = _animator.isHuman
                ? _animator.GetBoneTransform(HumanBodyBones.RightHand)
                : null;
            Vector3 handPos = handBone != null ? handBone.position : _animator.transform.position;

            // Position weight is light so the arm leads the aim without ripping the
            // hand off the rig; rotation weight carries the actual aim direction.
            _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, _weight);
            _animator.SetIKPosition(AvatarIKGoal.RightHand, _aimTarget.position);

            Vector3 toTarget = _aimTarget.position - handPos;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                _animator.SetIKRotationWeight(AvatarIKGoal.RightHand, _weight);
                _animator.SetIKRotation(AvatarIKGoal.RightHand, Quaternion.LookRotation(toTarget.normalized));
            }
        }
    }
}
