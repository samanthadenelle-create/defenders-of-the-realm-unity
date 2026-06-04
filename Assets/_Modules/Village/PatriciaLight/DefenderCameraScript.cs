
using DeNelle.Village;
using UnityEngine;

namespace DeNelle.Village.Defend   // ← New nested namespace
{
    [DisallowMultipleComponent]
    public sealed class DefendTowerCamera : MonoBehaviour
    {
        [Tooltip("World-space offset from the target (behind on -Z, above on +Y). Higher = more overhead view.")]
        [SerializeField] private Vector3 _offset = new Vector3(0f, 14f, -22f);

        [Tooltip("Height above the target's feet the camera looks at.")]
        [SerializeField] private float _lookAtHeight = 2.2f;

        [Tooltip("Higher = slower / more stationary camera.")]
        [SerializeField] private float _positionSmoothTime = 0.9f;

        private Transform _target;
        private Vector3 _velocity;
        private bool _snapped;

        // ── Camera shake (decaying positional jitter) — parity with the other rigs
        //    so HitStop / CameraShakeBridge work here too. ──────────────────────
        private float _shakeAmplitude;
        private float _shakeTimeLeft;
        private float _shakeDuration;

        /// <summary>Kick a decaying camera shake (peak metres, seconds).</summary>
        public void Shake(float intensity, float duration)
        {
            if (intensity <= 0f || duration <= 0f) return;
            if (intensity >= _shakeAmplitude)
            {
                _shakeAmplitude = intensity;
                _shakeDuration  = duration;
                _shakeTimeLeft  = duration;
            }
            else
            {
                _shakeTimeLeft = Mathf.Max(_shakeTimeLeft, duration);
            }
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

        public Transform Target
        {
            get => _target;
            set
            {
                _target = value;
                _snapped = false;
                if (_target != null) SnapToTarget();
            }
        }

        public void SetOffset(Vector3 offset)
        {
            _offset = offset;
            if (_target != null) SnapToTarget();
        }

        public void SnapToTarget()
        {
            if (_target == null) return;
            transform.position = DesiredPosition();
            AimAtTarget();
            _velocity = Vector3.zero;
            _snapped = true;
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            if (!_snapped)
            {
                SnapToTarget();
                return;
            }

            transform.position = Vector3.SmoothDamp(
                transform.position, DesiredPosition(), ref _velocity, _positionSmoothTime) + ShakeOffset();

            AimAtTarget();
        }

        private Vector3 DesiredPosition()
        {
            if (_target == null) return transform.position;
            return _target.position + _offset;
        }

        private void AimAtTarget()
        {
            if (_target == null) return;
            
            Vector3 lookAt = _target.position + Vector3.up * _lookAtHeight;
            Vector3 dir = lookAt - transform.position;
            
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}