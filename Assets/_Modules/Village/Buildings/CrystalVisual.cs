// =============================================================================
// CrystalVisual — slow spin + colour pulse for the on-map Aether crystal (the
// node the owner wanted "slowly spinning and pulsing colours"). Pure cosmetic,
// mobile-cheap: rotates the transform and cycles base/emission colour through a
// palette via a MaterialPropertyBlock — NO per-frame material instancing.
// -----------------------------------------------------------------------------
// Attach to the crystal GameObject (or any GO whose child renderer is the
// crystal mesh). Self-contained — zero gameplay dependencies, so it compiles and
// runs on its own. Wire it onto the crystal-mine node when that hookup lands.
//
// NOTE on the glow: the base-colour tint always pulses. For the EMISSION glow to
// show, the crystal material must have the URP "_EMISSION" keyword enabled (tick
// "Emission" on the URP/Lit material) — a MaterialPropertyBlock can set the
// colour but cannot toggle the shader keyword.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>Cosmetic slow-spin + colour-pulse driver for the Aether crystal.</summary>
    [DisallowMultipleComponent]
    public sealed class CrystalVisual : MonoBehaviour
    {
        [Header("Spin")]
        [Tooltip("Degrees per second around the spin axis. Slow + hypnotic by default.")]
        [SerializeField] private float _spinDegreesPerSecond = 18f;
        [SerializeField] private Vector3 _spinAxis = Vector3.up;

        [Header("Colour pulse")]
        [Tooltip("Colours the crystal cycles through (tint + emission). Loops smoothly.")]
        [SerializeField] private Color[] _palette =
        {
            new Color(0.30f, 0.70f, 1.00f), // aether blue
            new Color(0.55f, 0.40f, 1.00f), // arcane violet
            new Color(0.30f, 1.00f, 0.85f), // teal
        };
        [Tooltip("Seconds to traverse the full palette once.")]
        [SerializeField] private float _pulsePeriod = 6f;
        [Tooltip("Peak emission intensity (HDR multiplier).")]
        [SerializeField] private float _emissionIntensity = 2.2f;
        [Tooltip("How much the brightness breathes (0 = steady, 1 = full breathe).")]
        [Range(0f, 1f)] [SerializeField] private float _breathAmount = 0.35f;

        [Header("Optional bob")]
        [Tooltip("Vertical bob amplitude in metres. 0 = no bob.")]
        [SerializeField] private float _bobAmplitude = 0f;
        [SerializeField] private float _bobPeriod = 4f;

        private Renderer _renderer;
        private MaterialPropertyBlock _mpb;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private float _t;
        private Vector3 _baseLocalPos;

        private void Awake()
        {
            _renderer = GetComponentInChildren<Renderer>();
            _mpb = new MaterialPropertyBlock();
            _baseLocalPos = transform.localPosition;
            if (_palette == null || _palette.Length == 0)
                _palette = new[] { new Color(0.30f, 0.70f, 1.00f) };
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;

            // Spin (world-space so it reads as a steady rotation regardless of parent).
            if (_spinDegreesPerSecond != 0f && _spinAxis != Vector3.zero)
                transform.Rotate(_spinAxis.normalized, _spinDegreesPerSecond * dt, Space.World);

            // Optional vertical bob.
            if (_bobAmplitude > 0f && _bobPeriod > 0f)
            {
                float bob = Mathf.Sin((_t / _bobPeriod) * Mathf.PI * 2f) * _bobAmplitude;
                transform.localPosition = _baseLocalPos + Vector3.up * bob;
            }

            if (_renderer == null) return;
            // Reload-safe: a mid-Play assembly reload restores _renderer (a UnityEngine.Object)
            // but nulls _mpb (a plain managed MaterialPropertyBlock) without re-running Awake →
            // GetPropertyBlock(null) would NRE every frame. Lazily re-create it.
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            // Colour: lerp smoothly through the palette; brightness breathes on a
            // separate, slower sine so the glow never looks mechanical.
            Color col = SamplePalette(_pulsePeriod > 0f ? (_t / _pulsePeriod) : 0f);
            float breathPeriod = Mathf.Max(0.01f, _pulsePeriod * 0.5f);
            float breath = 1f - _breathAmount * 0.5f *
                           (1f - Mathf.Cos((_t / breathPeriod) * Mathf.PI * 2f));

            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, col);
            _mpb.SetColor(EmissionColorId, col * (_emissionIntensity * breath));
            _renderer.SetPropertyBlock(_mpb);
        }

        /// <summary>Smoothly samples the colour palette at normalised time t (wraps/loops).</summary>
        private Color SamplePalette(float t)
        {
            int n = _palette.Length;
            if (n <= 1) return _palette[0];
            float scaled = Mathf.Repeat(t, 1f) * n;        // 0 .. n
            int i = Mathf.FloorToInt(scaled) % n;
            int j = (i + 1) % n;
            float f = scaled - Mathf.Floor(scaled);
            return Color.Lerp(_palette[i], _palette[j], f);
        }
    }
}
