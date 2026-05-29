// =============================================================================
// HeroOverShoulderCamera — over-the-shoulder follow for the Defend-the-Tower hero.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Defend
//
// Sits just behind + above + to one side of the hero (a HERO-RELATIVE offset, so
// it stays over the shoulder whichever way the hero faces) and looks PAST the
// hero in the hero's forward direction — so you see over the shoulder at the
// tower and the battle ahead, not at the hero's back.
//
// All framing is driven by the inspector fields below (set Target at runtime;
// PatriciaLightController does that). Tune ShoulderOffset / LookAhead / LookDown
// live in the inspector — the camera ignores its own Transform and re-derives
// its pose from the hero each frame.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village.Defend
{
    [DisallowMultipleComponent]
    public sealed class HeroOverShoulderCamera : MonoBehaviour
    {
        [Tooltip("The hero to ride. Assigned at runtime by PatriciaLightController.")]
        public Transform Target;

        [Header("Over-the-shoulder placement (hero-relative: +X right, +Y up, -Z behind)")]
        [SerializeField] private Vector3 _shoulderOffset = new Vector3(0.9f, 2.0f, -4.0f);

        [Header("Aim")]
        [Tooltip("How far ahead of the hero (along its forward) the camera looks.")]
        [SerializeField] private float _lookAhead = 12f;
        [Tooltip("How far below that point to bias the look, so the field/enemies fill the frame.")]
        [SerializeField] private float _lookDown = 2.5f;

        [Tooltip("Position smoothing time (bigger = lazier). 0 = rigid.")]
        [SerializeField] private float _smoothTime = 0.10f;

        private Vector3 _velocity;
        private bool _snapped;

        // ── Shake (decaying jitter) — parity with the other rigs (HitStop hook) ──
        private float _shakeAmplitude, _shakeTimeLeft, _shakeDuration;

        public void Shake(float intensity, float duration)
        {
            if (intensity <= 0f || duration <= 0f) return;
            if (intensity >= _shakeAmplitude) { _shakeAmplitude = intensity; _shakeDuration = duration; _shakeTimeLeft = duration; }
            else { _shakeTimeLeft = Mathf.Max(_shakeTimeLeft, duration); }
        }

        private void OnDisable() => _snapped = false;

        private void LateUpdate()
        {
            if (Target == null) return;

            Vector3 desired = DesiredPosition();
            transform.position = _snapped
                ? Vector3.SmoothDamp(transform.position, desired, ref _velocity, _smoothTime) + ShakeOffset()
                : desired;                                   // snap on the first frame, no lerp-from-origin
            _snapped = true;

            AimAhead();
        }

        private Vector3 DesiredPosition()
        {
            // Hero-relative offset → genuinely "over the shoulder" regardless of facing.
            return Target.position + Target.rotation * _shoulderOffset;
        }

        private void AimAhead()
        {
            // Look at a point AHEAD of the hero (its forward), biased downward so the
            // tower + the field of enemies fill the view — not the hero's back.
            Vector3 lookPoint = Target.position
                              + Target.forward * _lookAhead
                              - Vector3.up * _lookDown;
            Vector3 dir = lookPoint - transform.position;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }

        private Vector3 ShakeOffset()
        {
            if (_shakeTimeLeft <= 0f) return Vector3.zero;
            _shakeTimeLeft -= Time.deltaTime;
            if (_shakeTimeLeft <= 0f) { _shakeAmplitude = 0f; return Vector3.zero; }
            float k = _shakeDuration > 0f ? _shakeTimeLeft / _shakeDuration : 0f;
            float m = _shakeAmplitude * k;
            return new Vector3(Random.value * 2f - 1f, Random.value * 2f - 1f, Random.value * 2f - 1f) * m;
        }
    }
}
