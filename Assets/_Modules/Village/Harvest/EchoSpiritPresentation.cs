// =============================================================================
// EchoSpiritPresentation -- founding Echo "ethereal floating spirit" layer.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// PO creative call (2026-07-16): the founding Echo KEEPS its current decimated
// model + baked bind pose (we do NOT fix the arms) but becomes an ethereal
// FLOATING SPIRIT. This is a pure PRESENTATION layer (HP B2B: presentation never
// touches the objects/skeleton) attached to the Hollow-born Echo instance by
// TutorialFlow.ApplyStarterPetGrant right after PetDeployer.SummonAt:
//   1. gentle vertical HOVER  -- sine bob on the visual child's localPosition.y,
//   2. faint slow yaw DRIFT   -- a continuous, very slow spin so it never reads
//      static (a spirit turning), and
//   3. a soft AURA/GLOW loop  -- the SAME VFXManager.PlayAura path the Heart of
//      Elarion uses (Aura_HeartPulse), parented to a child pivot that tracks the
//      hovering body (worldPositionStays:false).
//
// It drives the VISUAL CHILD (the spawned mesh), never the root, so PetHeroLeash
// / Pet locomotion (which own the ROOT's world transform + facing) are never
// fought, and it never touches bones / the Animator -- the T-pose stays baked, on
// purpose. Module note: this lives in DeNelle.Village (NOT _Modules/Pets, which is
// DeNelle.Pets and may not reference VFXManager per S5); the founding attach site
// (TutorialFlow) is Village too, so AddComponent<> is in-assembly.
//
// Colorblind law: life/identity reads from MOTION + LUMINANCE (hover, drift,
// glow), never color alone. Every cross-module call is null-conditional (?.) and
// FlowTrace-instrumented (S12). ASCII-only strings.
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Ethereal-spirit presentation for the founding Echo: hover + slow yaw drift
    /// on the visual child, plus a Heart-style aura loop on a tracking pivot. Pure
    /// presentation -- reads/writes only its own visual child + a child aura pivot,
    /// never the skeleton or the leash-driven root. PO-tunable via the serialized
    /// fields below.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EchoSpiritPresentation : MonoBehaviour
    {
        [Header("Hover (vertical bob)")]
        [Tooltip("Peak hover height above the seated pose, world units. PO-tunable.")]
        [SerializeField] private float _hoverAmplitude = 0.15f;
        [Tooltip("Seconds for one full up-down hover cycle. PO-tunable.")]
        [SerializeField] private float _hoverPeriod = 2.5f;

        [Header("Yaw drift (slow spirit spin)")]
        [Tooltip("Continuous spin speed of the spirit body, degrees/second. PO-tunable.")]
        [SerializeField] private float _yawDriftDegPerSec = 10f;

        [Header("Aura")]
        [Tooltip("Persistent aura loop -- reuses VFXManager.PlayAura like the Heart of Elarion.")]
        [SerializeField] private VFXType _auraType = VFXType.Aura_HeartPulse;
        [Tooltip("Local height of the aura pivot on the body, world units. PO-tunable.")]
        [SerializeField] private float _auraHeight = 0.9f;

        private Transform _floatRoot;      // the visual child we hover/spin (never the root)
        private Vector3 _baseLocalPos;
        private float _phase;
        private VFXHandle _auraHandle;

        private void Start()
        {
            // Float the VISUAL child (the spawned mesh), not the root -- the root's
            // world position + facing are owned by PetHeroLeash / Pet locomotion.
            _floatRoot = ResolveVisualChild();
            _baseLocalPos = _floatRoot.localPosition;
            _phase = Random.Range(0f, Mathf.PI * 2f);   // desync if several ever coexist

            FlowTrace.Step("Echo",
                $"EchoSpiritPresentation attached: hover(amp={_hoverAmplitude}, period={_hoverPeriod}s), " +
                $"yawDrift={_yawDriftDegPerSec} deg/s, target='{_floatRoot.name}' (visual child, skeleton untouched).");

            StartAura();
        }

        // First descendant carrying a Renderer = the spawned mesh; fall back to self
        // so the layer is always safe even on a rig-less / placeholder body.
        private Transform ResolveVisualChild()
        {
            var rend = GetComponentInChildren<Renderer>();
            if (rend != null && rend.transform != transform) return rend.transform;
            return transform;
        }

        private void StartAura()
        {
            // Child pivot so the aura tracks the hovering body (worldPositionStays:false).
            var pivot = new GameObject("EchoAura");
            pivot.transform.SetParent(_floatRoot, worldPositionStays: false);
            pivot.transform.localPosition = new Vector3(0f, _auraHeight, 0f);

            var mgr = VFXManager.Instance;
            if (mgr == null)
            {
                FlowTrace.Warn("Echo",
                    "EchoSpiritPresentation: no VFXManager in scene -- aura skipped (hover + drift still play).");
                return;
            }

            _auraHandle = mgr.PlayAura(_auraType, pivot.transform);
            if (_auraHandle != null)
                FlowTrace.Step("Echo", $"EchoSpiritPresentation aura started ({_auraType}) on tracking pivot.");
            else
                FlowTrace.Warn("Echo",
                    $"EchoSpiritPresentation aura returned no handle for {_auraType} -- hover + drift still play.");
        }

        private void Update()
        {
            if (_floatRoot == null) return;

            // 1) Vertical HOVER -- sine bob around the seated local pose.
            float period = Mathf.Max(0.1f, _hoverPeriod);
            float bob = Mathf.Sin((Time.time / period) * Mathf.PI * 2f + _phase) * _hoverAmplitude;
            _floatRoot.localPosition = _baseLocalPos + new Vector3(0f, bob, 0f);

            // 2) Slow continuous yaw DRIFT (incremental -- preserves the mesh's
            //    authored forward correction as the starting orientation).
            if (!Mathf.Approximately(_yawDriftDegPerSec, 0f))
                _floatRoot.Rotate(0f, _yawDriftDegPerSec * Time.deltaTime, 0f, Space.Self);
        }

        private void OnDestroy()
        {
            _auraHandle?.Stop();
        }
    }
}
