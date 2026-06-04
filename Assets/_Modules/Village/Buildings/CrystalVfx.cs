// =============================================================================
// CrystalVfx — gentle idle animation for a crystal: a very slow spin + a pulsing
// colour/emission glow. Drop it on a crystal GameObject (or its visual root); it
// finds every Renderer underneath and drives them via MaterialPropertyBlock — no
// per-frame material instances, so batching + memory stay clean.
//
// Tune in the Inspector: spin axis/speed, the two pulse colours, the pulse rate,
// and the emission-intensity range. Defaults read as a slow violet→aether throb.
//
// NOTE: the emission glow only shows if the renderer's material has emission
// enabled (URP Lit "_EMISSION"). The base-colour tint pulse always shows, so the
// effect is visible either way; enable Emission on the crystal material for the
// full glow.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    public sealed class CrystalVfx : MonoBehaviour
    {
        [Header("Spin")]
        [Tooltip("Rotation axis (local). Default = Y (upright spin).")]
        [SerializeField] private Vector3 _spinAxis = Vector3.up;
        [Tooltip("Degrees per second. ~15 = a full slow turn every 24s.")]
        [SerializeField] private float _spinDegreesPerSecond = 15f;

        [Header("Pulse — colour + glow")]
        [Tooltip("Colour at the low point of the pulse.")]
        [SerializeField] private Color _colorLow  = new Color(0.35f, 0.20f, 0.65f); // dim violet
        [Tooltip("Colour at the peak of the pulse.")]
        [SerializeField] private Color _colorHigh = new Color(0.70f, 0.50f, 1.00f); // bright aether
        [Tooltip("Full low→high→low cycles per second. 0.4 = a ~2.5s breath.")]
        [SerializeField, Min(0.01f)] private float _pulsesPerSecond = 0.4f;
        [Tooltip("Emission multiplier at the low / high point of the pulse.")]
        [SerializeField] private float _emissionLow  = 0.3f;
        [SerializeField] private float _emissionHigh = 2.0f;

        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;
        private static readonly int BaseColorId     = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>();
            _mpb = new MaterialPropertyBlock();
        }

        private void Update()
        {
            // Very slow continuous spin around the WORLD axis (default = world-up).
            // World space avoids the Blender→Unity import tilt that makes a local-axis
            // spin tumble the model sideways / dip it through the ground.
            if (_spinDegreesPerSecond != 0f && _spinAxis != Vector3.zero)
                transform.Rotate(_spinAxis.normalized, _spinDegreesPerSecond * Time.deltaTime, Space.World);

            // 0→1→0 triangle pulse. (period = 1 / pulsesPerSecond)
            float t = Mathf.PingPong(Time.time * _pulsesPerSecond * 2f, 1f);
            Color tint = Color.Lerp(_colorLow, _colorHigh, t);
            Color emission = tint * Mathf.Lerp(_emissionLow, _emissionHigh, t);

            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, tint);
                _mpb.SetColor(EmissionColorId, emission);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
