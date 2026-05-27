// =============================================================================
// SkyProgressionController (DEF-66) — dynamic sky that darkens and reddens as
// waves advance, reinforcing the escalating threat without any authored sky
// sequence or timeline asset.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT IT DOES:
//   Subscribes to WaveManager.OnWaveStarted and lerps RenderSettings values
//   toward the "late-game" palette each time a new wave begins. Three channels:
//     • Fog density     — thin at wave 1, heavier by wave 10+
//     • Ambient light   — bright cool white at wave 1, dimmer red-tinted by late game
//     • Directional light colour/intensity — warm white → deep orange dusk
//
//   All changes are smooth (UpdateLerp in Update) so there is never a hard pop.
//   On scene unload RenderSettings are restored to the values captured in Awake
//   so the sky doesn't permanently override the project's lighting setup.
//
// ARCHITECTURE:
//   * [RequireComponent(typeof(WaveManager))] — lives on the WaveManager GO.
//   * Subscribes/unsubscribes to OnWaveStarted in OnEnable/OnDisable.
//   * Uses AnimationCurves keyed on wave number for full designer control.
//   * No Cinemachine, no Timeline, no post-process — only RenderSettings and
//     an optional DirectionalLight reference.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Gradually darkens and reddens the sky as waves progress.
    /// Attach to the WaveManager GameObject alongside <see cref="WaveManager"/>.
    /// </summary>
    [RequireComponent(typeof(WaveManager))]
    [DisallowMultipleComponent]
    public sealed class SkyProgressionController : MonoBehaviour
    {
        [Header("Directional light (optional — sun/moon)")]
        [Tooltip("The scene's main directional light. Leave blank to skip light colour/intensity changes.")]
        [SerializeField] private Light _sun;

        [Header("Fog curves (x = wave number, y = value)")]
        [Tooltip("Fog density over wave progression. Default: 0.002 → 0.015 by wave 15.")]
        [SerializeField] private AnimationCurve _fogDensityCurve = DefaultFogDensity();

        [Header("Ambient light curves")]
        [Tooltip("Ambient light intensity over wave progression.")]
        [SerializeField] private AnimationCurve _ambientIntensityCurve = DefaultAmbientIntensity();

        [Tooltip("Ambient colour red channel over wave progression (0-1).")]
        [SerializeField] private AnimationCurve _ambientRedCurve = DefaultAmbientRed();

        [Tooltip("Ambient colour green channel over wave progression (0-1).")]
        [SerializeField] private AnimationCurve _ambientGreenCurve = DefaultAmbientGreen();

        [Header("Sun light curves (only used if Sun is assigned)")]
        [Tooltip("Sun intensity over wave progression.")]
        [SerializeField] private AnimationCurve _sunIntensityCurve = DefaultSunIntensity();

        [Tooltip("Sun colour red channel.")]
        [SerializeField] private AnimationCurve _sunRedCurve = DefaultSunRed();

        [Tooltip("Sun colour green channel.")]
        [SerializeField] private AnimationCurve _sunGreenCurve = DefaultSunGreen();

        [Header("Transition")]
        [Tooltip("Speed at which sky values lerp toward the target after a new wave. " +
                 "Higher = snappier transition.")]
        [SerializeField, Min(0.05f)] private float _lerpSpeed = 0.5f;

        // ── Saved scene baseline — restored on disable ────────────────────────
        private float     _baseFogDensity;
        private Color     _baseAmbientLight;
        private float     _baseSunIntensity;
        private Color     _baseSunColor;
        private bool      _baselineSaved;

        // ── Current lerp target ───────────────────────────────────────────────
        private float _targetFogDensity;
        private Color _targetAmbient;
        private float _targetSunIntensity;
        private Color _targetSunColor;

        private WaveManager _wave;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _wave = GetComponent<WaveManager>();

            // Capture the scene's authored baseline so we can restore it.
            _baseFogDensity   = RenderSettings.fogDensity;
            _baseAmbientLight = RenderSettings.ambientLight;
            if (_sun != null)
            {
                _baseSunIntensity = _sun.intensity;
                _baseSunColor     = _sun.color;
            }
            _baselineSaved = true;

            // Start targets at baseline.
            _targetFogDensity   = _baseFogDensity;
            _targetAmbient      = _baseAmbientLight;
            _targetSunIntensity = _baseSunIntensity;
            _targetSunColor     = _baseSunColor;
        }

        private void OnEnable()
        {
            if (_wave != null)
                _wave.OnWaveStarted.AddListener(HandleWaveStarted);
        }

        private void OnDisable()
        {
            if (_wave != null)
                _wave.OnWaveStarted.RemoveListener(HandleWaveStarted);

            // Restore the scene's authored sky so the editor lighting isn't
            // permanently overridden when exiting play mode.
            if (_baselineSaved)
            {
                RenderSettings.fogDensity   = _baseFogDensity;
                RenderSettings.ambientLight = _baseAmbientLight;
                if (_sun != null)
                {
                    _sun.intensity = _baseSunIntensity;
                    _sun.color     = _baseSunColor;
                }
            }
        }

        private void Update()
        {
            float t = _lerpSpeed * Time.deltaTime;

            RenderSettings.fogDensity   = Mathf.Lerp(RenderSettings.fogDensity,   _targetFogDensity, t);
            RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, _targetAmbient,    t);

            if (_sun != null)
            {
                _sun.intensity = Mathf.Lerp(_sun.intensity, _targetSunIntensity, t);
                _sun.color     = Color.Lerp(_sun.color,     _targetSunColor,     t);
            }
        }

        // ── Wave handler ──────────────────────────────────────────────────────

        private void HandleWaveStarted(int waveId)
        {
            float w = (float)waveId;

            _targetFogDensity = _fogDensityCurve.Evaluate(w);

            float ambR = _ambientRedCurve.Evaluate(w);
            float ambG = _ambientGreenCurve.Evaluate(w);
            float ambI = _ambientIntensityCurve.Evaluate(w);
            _targetAmbient = new Color(ambR, ambG, ambG) * ambI;

            if (_sun != null)
            {
                _targetSunIntensity = _sunIntensityCurve.Evaluate(w);
                float sunR = _sunRedCurve.Evaluate(w);
                float sunG = _sunGreenCurve.Evaluate(w);
                _targetSunColor = new Color(sunR, sunG, sunG * 0.8f, 1f);
            }
        }

        // ── Default curve factories ───────────────────────────────────────────

        private static AnimationCurve DefaultFogDensity() =>
            Clamped(new Keyframe(0f, 0.002f), new Keyframe(15f, 0.018f));

        private static AnimationCurve DefaultAmbientIntensity() =>
            Clamped(new Keyframe(0f, 1.0f), new Keyframe(15f, 0.55f));

        private static AnimationCurve DefaultAmbientRed() =>
            Clamped(new Keyframe(0f, 0.85f), new Keyframe(15f, 0.60f));

        private static AnimationCurve DefaultAmbientGreen() =>
            Clamped(new Keyframe(0f, 0.85f), new Keyframe(15f, 0.38f));

        private static AnimationCurve DefaultSunIntensity() =>
            Clamped(new Keyframe(0f, 1.2f), new Keyframe(15f, 0.6f));

        private static AnimationCurve DefaultSunRed() =>
            Clamped(new Keyframe(0f, 1.0f), new Keyframe(15f, 1.0f));

        private static AnimationCurve DefaultSunGreen() =>
            Clamped(new Keyframe(0f, 0.95f), new Keyframe(15f, 0.45f));

        private static AnimationCurve Clamped(Keyframe from, Keyframe to)
        {
            var c = new AnimationCurve(from, to);
            c.preWrapMode  = WrapMode.Clamp;
            c.postWrapMode = WrapMode.Clamp;
            return c;
        }
    }
}
