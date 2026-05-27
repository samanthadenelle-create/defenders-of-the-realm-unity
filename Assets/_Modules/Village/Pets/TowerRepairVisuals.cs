// =============================================================================
// TowerRepairVisuals (DEF-71) — emissive shimmer when the defended Heart heals.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT IT DOES:
//   Watches the defended structure (HeartController) for a HEAL — a frame-to-
//   frame increase in HP — and plays a brief satisfying emissive shimmer on the
//   tower/Heart renderers. The shimmer drives only the "_EmissionColor" shader
//   property through a MaterialPropertyBlock (per-instance, no shared-material
//   mutation), animated with Mathf.PingPong over a short window. It NEVER calls
//   SetActive on a renderer or particle GameObject.
//
// RECONCILIATION WITH THE DEF-71 SPEC (review-repo lineage, not this branch):
//   * The spec subscribes to `HeartOfTown.OnHealthChanged(pct)`. That type/event
//     DO NOT EXIST on this branch — the defended structure is HeartController,
//     which has NO health-changed event and an HP on the 0-100 scale (Hp /
//     SetHp). We therefore POLL HeartController.Hp on a throttle and detect a
//     heal by comparing to a cached `_previousHp` (current > previous = heal),
//     exactly the spec's `_previousHealthPct` comparison, just polled.
//   * The spec's RepairParticles burst is dropped in favour of an emission-only
//     shimmer per the DEF-71 reconciliation brief (never SetActive on particle
//     GameObjects). A renderer shimmer reads cleanly without authored particles.
//
// ARCHITECTURE (non-negotiable):
//   * GetComponent / renderer caches happen in Awake.
//   * No FindObjectOfType — serialized Heart ref with a guarded FindObjectsByType
//     fallback resolved once in Awake.
//   * Detection is throttled (HP polled on an interval, never per-frame Find).
//   * Emission uses a MaterialPropertyBlock — never per-renderer material
//     mutation, never SetActive.
//   * The shimmer runs as a self-terminating coroutine, not an always-on Update
//     lerp.
// =============================================================================

using System.Collections;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Plays a brief emissive shimmer on the Heart/tower renderers whenever the
    /// defended <see cref="HeartController"/> gains HP (a repair). Heals are
    /// detected by polling <see cref="HeartController.Hp"/> on a throttle and
    /// comparing to the previous sample; the shimmer animates "_EmissionColor"
    /// through a <see cref="MaterialPropertyBlock"/> via <see cref="Mathf.PingPong"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TowerRepairVisuals : MonoBehaviour
    {
        [Header("Refs (auto-resolved in Awake when left blank)")]
        [Tooltip("The defended structure whose HP we watch for repairs. Left blank: " +
                 "found via FindObjectsByType in Awake (guarded).")]
        [SerializeField] private HeartController _heart;

        [Tooltip("Renderers that shimmer on a repair. Left blank: auto-filled from " +
                 "this GameObject's children in Awake.")]
        [SerializeField] private Renderer[] _renderers;

        [Header("Heal detection")]
        [Tooltip("Seconds between Heart-HP polls. Never per-frame.")]
        [SerializeField, Min(0.05f)] private float _hpPollInterval = 0.25f;

        [Tooltip("Minimum HP gain (0-100 scale) that counts as a repair worth a " +
                 "shimmer — filters float jitter.")]
        [SerializeField, Min(0f)] private float _healEpsilon = 0.01f;

        [Header("Shimmer")]
        [Tooltip("Colour the emission shimmers toward at the peak of the pulse.")]
        [SerializeField] private Color _shimmerColor = Color.white;

        [Tooltip("Total shimmer duration (seconds) — keep it brief + satisfying.")]
        [SerializeField, Min(0.05f)] private float _shimmerSeconds = 0.5f;

        [Tooltip("Peak emission intensity multiplier at the top of the shimmer.")]
        [SerializeField, Min(0f)] private float _shimmerIntensity = 2f;

        // Cached emission state: the previous HP sample + the throttle accumulator.
        private float _previousHp;
        private float _hpPollTimer;

        private MaterialPropertyBlock _mpb;
        private Coroutine _shimmerRoutine;

        // Shader property id — cached once (never Shader.PropertyToID per frame).
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            // Cache refs in Awake.
            if (_renderers == null || _renderers.Length == 0)
                _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);

            if (_heart == null)
                _heart = ResolveHeart();

            _mpb = new MaterialPropertyBlock();

            // Seed the previous HP so the first poll doesn't read a heal from a
            // cold start (matches the spec's `_previousHealthPct = 1f` intent).
            _previousHp = _heart != null ? _heart.Hp : 100f;
        }

        private void OnEnable()
        {
            // Re-seed so a re-enable (scene reload / pooling) never reads a phantom
            // heal from a stale sample.
            _previousHp = _heart != null ? _heart.Hp : _previousHp;
            _hpPollTimer = 0f;
        }

        private void OnDisable()
        {
            if (_shimmerRoutine != null)
            {
                StopCoroutine(_shimmerRoutine);
                _shimmerRoutine = null;
            }
            // Clear any residual emission override so the renderers don't latch
            // a half-finished shimmer when disabled mid-pulse.
            ClearEmission();
        }

        private void Update()
        {
            if (_heart == null) return;

            // Throttled poll — HeartController has no health-changed event.
            _hpPollTimer += Time.deltaTime;
            if (_hpPollTimer < _hpPollInterval) return;
            _hpPollTimer = 0f;

            float currentHp = _heart.Hp;
            if (currentHp > _previousHp + _healEpsilon)
            {
                // HP increased -> a repair. Restart the shimmer (a new repair
                // during a shimmer simply refreshes it).
                if (_shimmerRoutine != null) StopCoroutine(_shimmerRoutine);
                _shimmerRoutine = StartCoroutine(ShimmerRoutine());
            }

            _previousHp = currentHp;
        }

        /// <summary>
        /// Brief emissive pulse: PingPong the emission intensity up to the peak
        /// and back to black over <see cref="_shimmerSeconds"/>, written through a
        /// MaterialPropertyBlock so the shared base material is never mutated.
        /// </summary>
        private IEnumerator ShimmerRoutine()
        {
            float elapsed = 0f;
            while (elapsed < _shimmerSeconds)
            {
                elapsed += Time.deltaTime;
                // PingPong 0 -> peak -> 0 across the window (one up-down sweep):
                // map elapsed into [0, _shimmerSeconds] and ping-pong on the half.
                float t = Mathf.PingPong(elapsed / _shimmerSeconds * 2f, 1f);
                ApplyEmission(_shimmerColor * (t * _shimmerIntensity));
                yield return null;
            }

            ClearEmission();
            _shimmerRoutine = null;
        }

        private void ApplyEmission(Color emission)
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissionColorId, emission);
                r.SetPropertyBlock(_mpb);
            }
        }

        private void ClearEmission() => ApplyEmission(Color.black);

        /// <summary>Resolves the Heart without FindObjectOfType (guarded fallback).</summary>
        private static HeartController ResolveHeart()
        {
            var all = Object.FindObjectsByType<HeartController>(FindObjectsSortMode.None);
            return (all != null && all.Length > 0) ? all[0] : null;
        }
    }
}
