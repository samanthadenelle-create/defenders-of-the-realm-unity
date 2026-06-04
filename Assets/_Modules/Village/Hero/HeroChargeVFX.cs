// =============================================================================
// HeroChargeVFX — DEF-70 (2): charge-up VFX before an ability cast.
// -----------------------------------------------------------------------------
// namespace DeNelle.Village (the only hero assembly on this branch).
//
// SPEC INTENT: hold a ParticleSystem on the hero's hand bone; StartCharge() plays
// it and ramps emission with charge time; ReleaseCharge() stops emitting (so live
// particles fade out naturally). A charge SFX one-shot plays on StartCharge. The
// emission is toggled via the ParticleSystem API — NEVER SetActive (acceptance
// criterion).
//
// RECONCILIATION TO THIS BRANCH:
//   • There is no HeroCombat yet, so StartCharge / ReleaseCharge are public and
//     left for a future caller to wire on ability input down / cast (see TODO).
//   • The hand bone reference is serialized but optional: when no _chargeParticles
//     is assigned, this builds a small URP-friendly built-in burst at runtime
//     (mirrors HeroAbilities' placeholder-VFX approach) so the feature is visible
//     before authored art exists. The placeholder parents to the hero so it tracks
//     the hand roughly even without a bone reference.
//   • Audio: optional AudioSource + clip, played one-shot on StartCharge. Both are
//     placeholder-ready (null-guarded) so a missing clip never throws.
//
// Emission ramp: charge intensity is scaled by elapsed charge time toward a max
// rate, so a longer hold reads as a stronger charge. Time uses unscaled delta so
// the ramp keeps animating even if the game is time-scaled / paused for a cast.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Charge-up particle + audio feedback for hero ability casts. Call
    /// <see cref="StartCharge"/> on ability input-down and <see cref="ReleaseCharge"/>
    /// on cast. Emission is toggled through the ParticleSystem API (no SetActive).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroChargeVFX : MonoBehaviour
    {
        [Header("Particles")]
        [Tooltip("Charge particle system — assign a child of the hero's hand bone in the Inspector. " +
                 "Left null: a URP-friendly placeholder burst is built at runtime.")]
        [SerializeField] private ParticleSystem _chargeParticles;

        [Tooltip("Emission rate (particles/sec) at full charge. The rate ramps from 0 to this over _rampToFullSeconds.")]
        [SerializeField, Min(0f)] private float _maxEmissionRate = 40f;

        [Tooltip("Seconds of charge to reach _maxEmissionRate.")]
        [SerializeField, Min(0.01f)] private float _rampToFullSeconds = 1.0f;

        [Header("Audio (placeholder-ready)")]
        [Tooltip("Optional — plays a one-shot on StartCharge. Left null: no charge SFX.")]
        [SerializeField] private AudioSource _chargeAudioSource;

        [Tooltip("Optional charge SFX clip. Left null: PlayOneShot is skipped.")]
        [SerializeField] private AudioClip _chargeClip;

        private bool _charging;
        private float _chargeTime;

        private void Awake()
        {
            if (_chargeParticles == null) _chargeParticles = BuildPlaceholderCharge();
            // Start silent — emission is driven entirely by StartCharge/ReleaseCharge.
            StopEmitting();
        }

        /// <summary>
        /// Begins the charge VFX: plays the particle system, opens the emission
        /// ramp, and fires the one-shot charge SFX.
        /// TODO: wire from HeroCombat on ability input-DOWN once that component lands.
        /// </summary>
        public void StartCharge()
        {
            _charging = true;
            _chargeTime = 0f;

            if (_chargeParticles != null)
            {
                SetEmissionRate(0f);
                if (!_chargeParticles.isPlaying) _chargeParticles.Play();
            }

            if (_chargeAudioSource != null && _chargeClip != null)
                _chargeAudioSource.PlayOneShot(_chargeClip);
        }

        /// <summary>
        /// Ends the charge: stops EMITTING (live particles linger and fade) — never
        /// deactivates the GameObject.
        /// TODO: wire from HeroCombat on cast / input-UP once that component lands.
        /// </summary>
        public void ReleaseCharge()
        {
            _charging = false;
            _chargeTime = 0f;
            StopEmitting();
        }

        private void Update()
        {
            if (!_charging || _chargeParticles == null) return;

            // Ramp emission with how long the player has held the charge. Unscaled
            // time so the ramp is unaffected by a time-scale pause during the cast.
            _chargeTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_chargeTime / _rampToFullSeconds);
            SetEmissionRate(_maxEmissionRate * t);
        }

        // --- Emission helpers (ParticleSystem API only — never SetActive) ---------

        private void SetEmissionRate(float rate)
        {
            if (_chargeParticles == null) return;
            var emission = _chargeParticles.emission;
            emission.enabled = true;
            emission.rateOverTime = rate;
        }

        private void StopEmitting()
        {
            if (_chargeParticles == null) return;
            _chargeParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        /// <summary>
        /// Builds a small, looping, URP-friendly charge particle system parented to
        /// the hero so the feature is visible before an authored hand-bone system is
        /// assigned. Mirrors the URP particle-material fix used elsewhere (a runtime
        /// ParticleSystem ships the legacy material that URP renders as magenta).
        /// </summary>
        private ParticleSystem BuildPlaceholderCharge()
        {
            var go = new GameObject("HeroChargeVFX_Placeholder");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 1.2f, 0.4f); // roughly chest/hand height

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.startLifetime = 0.6f;
            main.startSpeed = 0.6f;
            main.startSize = 0.12f;
            main.startColor = new Color(0.55f, 0.75f, 1f); // arcane blue charge glow
            main.maxParticles = 128;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f; // driven by StartCharge/ReleaseCharge

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;

            var psr = go.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                         ?? Shader.Find("Particles/Standard Unlit")
                         ?? Shader.Find("Sprites/Default");
                if (sh != null) psr.material = new Material(sh);
            }
            return ps;
        }
    }
}
