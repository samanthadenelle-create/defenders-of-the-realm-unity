// =============================================================================
// TowerDamageVisuals (DEF-66) — emissive damage states on a Building/tower.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT IT DOES:
//   Subscribes to Building.HpChanged and drives a damage-state tint + emissive
//   shift through a MaterialPropertyBlock so the tower visually communicates
//   its current health without mutating shared materials.
//
//   Four HP bands map to four visual states:
//     Healthy   (HP ≥ 75%)  — no tint, white base emission
//     Damaged   (HP ≥ 50%)  — slight amber tint, emission up slightly
//     Critical  (HP ≥ 25%)  — orange tint, emission higher, slow flicker
//     Failing   (HP <  25%) — red tint, high emission, fast flicker
//
//   The flicker is a Mathf.Sin wave applied in Update — only active when HP
//   is below the Damaged threshold. Update returns immediately at Healthy/
//   Damaged so there is zero per-frame cost outside of combat damage.
//
// ARCHITECTURE:
//   * Requires Building on the same or parent GameObject.
//   * Uses MaterialPropertyBlock — never mutates the shared material.
//   * Subscribes/unsubscribes in OnEnable/OnDisable (same pattern as
//     TowerRepairVisuals and PetContextualBehaviour).
//   * Renderers resolved in Awake — no per-frame Find.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Drives emissive damage-state visuals on a <see cref="Building"/>'s
    /// renderers based on its HP fraction.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TowerDamageVisuals : MonoBehaviour
    {
        [Header("Refs (auto-resolved in Awake when left blank)")]
        [Tooltip("The Building whose HP drives the visual state. " +
                 "Left blank: found on this GameObject or its parents.")]
        [SerializeField] private Building _building;

        [Tooltip("Renderers to tint. Left blank: GetComponentsInChildren fills this.")]
        [SerializeField] private Renderer[] _renderers;

        [Header("Damage tint colours")]
        [SerializeField] private Color _healthyEmission  = Color.white;
        [SerializeField] private Color _damagedEmission  = new Color(1f, 0.75f, 0.2f);  // amber
        [SerializeField] private Color _criticalEmission = new Color(1f, 0.35f, 0.05f); // orange
        [SerializeField] private Color _failingEmission  = new Color(1f, 0.05f, 0.05f); // red

        [Header("Emission intensity per state")]
        [SerializeField, Min(0f)] private float _healthyIntensity  = 0.8f;
        [SerializeField, Min(0f)] private float _damagedIntensity  = 1.2f;
        [SerializeField, Min(0f)] private float _criticalIntensity = 1.8f;
        [SerializeField, Min(0f)] private float _failingIntensity  = 2.4f;

        [Header("Flicker (active below Damaged threshold)")]
        [Tooltip("Flicker speed in Hz when Critical.")]
        [SerializeField, Min(0f)] private float _criticalFlickerHz = 1.5f;
        [Tooltip("Flicker speed in Hz when Failing.")]
        [SerializeField, Min(0f)] private float _failingFlickerHz  = 3.5f;
        [Tooltip("0–1 depth of the flicker modulation (0 = no flicker).")]
        [SerializeField, Range(0f, 1f)] private float _flickerDepth = 0.3f;

        // HP fraction thresholds
        private const float DamagedThreshold  = 0.75f;
        private const float CriticalThreshold = 0.50f;
        private const float FailingThreshold  = 0.25f;

        private MaterialPropertyBlock _mpb;
        private Building _subscribedBuilding;
        private float _currentHpFraction = 1f;
        private bool _needsFlicker;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId     = Shader.PropertyToID("_BaseColor");

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_building == null)
                _building = GetComponentInParent<Building>();

            if (_renderers == null || _renderers.Length == 0)
                _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);

            _mpb = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (_building != null)
            {
                _subscribedBuilding = _building;
                _building.HpChanged += OnHpChanged;
                // Seed the initial visual.
                OnHpChanged(_building.Hp, _building.MaxHp);
            }
        }

        private void OnDisable()
        {
            if (_subscribedBuilding != null)
            {
                _subscribedBuilding.HpChanged -= OnHpChanged;
                _subscribedBuilding = null;
            }
            // Clear any tint so the tower doesn't latch a damage colour when
            // this component is toggled off (e.g. after a repair that rebuilds it).
            ApplyEmission(_healthyEmission, _healthyIntensity);
        }

        private void Update()
        {
            if (!_needsFlicker) return;

            float hz      = _currentHpFraction < FailingThreshold ? _failingFlickerHz : _criticalFlickerHz;
            float baseInt = _currentHpFraction < FailingThreshold ? _failingIntensity : _criticalIntensity;
            Color baseCol = _currentHpFraction < FailingThreshold ? _failingEmission  : _criticalEmission;

            float flicker   = 1f - _flickerDepth * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * hz * Mathf.PI * 2f));
            ApplyEmission(baseCol, baseInt * flicker);
        }

        // ── HP handler ────────────────────────────────────────────────────────

        private void OnHpChanged(float hp, float maxHp)
        {
            _currentHpFraction = maxHp > 0f ? Mathf.Clamp01(hp / maxHp) : 1f;

            if (_currentHpFraction >= DamagedThreshold)
            {
                _needsFlicker = false;
                ApplyEmission(_healthyEmission, _healthyIntensity);
            }
            else if (_currentHpFraction >= CriticalThreshold)
            {
                _needsFlicker = false;
                ApplyEmission(_damagedEmission, _damagedIntensity);
            }
            else if (_currentHpFraction >= FailingThreshold)
            {
                _needsFlicker = true; // Update() now drives flicker
            }
            else
            {
                _needsFlicker = true;
            }
        }

        // ── Emission helpers ──────────────────────────────────────────────────

        private void ApplyEmission(Color color, float intensity)
        {
            if (_renderers == null) return;
            Color emission = color * intensity;
            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissionColorId, emission);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
