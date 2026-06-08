// =============================================================================
// ThirdPersonCameraFollow — over-the-shoulder follow camera for Patricia Light.
// -----------------------------------------------------------------------------
// WO-47 Phase 2 ("Defend the Tower"). A dead-simple smoothed follow camera that
// rides BEHIND + ABOVE the hero on the tower balcony and looks slightly DOWN at
// it, so the streaming enemies fill the lower screen. It is the dedicated-scene
// analog of the in-village VillageCamera, but with no Cinemachine / input
// dependency — the hero barely moves here (it fires from the balcony), so a
// fixed-offset SmoothDamp is all the framing this mode needs.
//
// PLAYTEST FIX (feat/patricia-light): the old build read UPWARD from below the
// tower because the camera could initialise at the world origin (0,0,0) and then
// lerp toward a high balcony, and the look-at target sat ABOVE the camera while
// it was still low. This rewrite makes the framing deterministic:
//   • The offset is WORLD-SPACE behind (-Z) + above (+Y) — never rotated under a
//     facing flip that could swing it in front of / below the hero.
//   • The camera SNAPS to the resting pose the moment a Target is assigned (and
//     on the first LateUpdate as a backstop), so it never starts at the origin.
//   • AimAtTarget looks at a point only slightly above the hero's feet, and the
//     camera always sits above that point, so the view is always angled DOWN.
//
// HARD CONSTRAINT (WO-47): lives in DeNelle.Village (no new asmdef). The scene
// builder adds it to the Main Camera by reflection (the editor asmdef cannot
// reference DeNelle.Village); PatriciaLightController hands it the hero
// transform at runtime once the hero rig is spawned.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// A smoothed third-person follow camera. Set <see cref="Target"/> at runtime
    /// (PatriciaLightController does this after spawning the hero on the balcony);
    /// the camera snaps to <c>target.position + offset</c> (behind + above) and
    /// looks slightly down at the hero. No-op until a target is assigned.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThirdPersonCameraFollow : MonoBehaviour
    {
        [Tooltip("World-space offset from the followed target — behind on -Z and above on +Y. " +
                 "Default sits the camera 9 m behind and 3.5 m above the hero.")]
        [SerializeField] private Vector3 _offset = new Vector3(0f, 3.5f, -9f);

        [Tooltip("Height above the target's feet the camera aims at (head height). " +
                 "The camera always sits above this point, so the view angles DOWN.")]
        [SerializeField] private float _lookAtHeight = 1.5f;

        [Tooltip("Position smoothing time (seconds) — bigger = lazier follow.")]
        [SerializeField] private float _positionSmoothTime = 0.18f;

        private Transform _target;
        private Vector3 _velocity;
        private bool _snapped;   // false until we have placed the camera at least once

        // ── Camera shake (decaying random positional offset) ──────────────────
        private float _shakeAmplitude;   // current peak offset (metres)
        private float _shakeTimeLeft;    // seconds remaining
        private float _shakeDuration;    // full duration for the linear decay

        /// <summary>The transform the camera follows. Assigning it snaps the camera
        /// to its resting pose immediately (no lerp-from-origin).</summary>
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

        /// <summary>Sets the follow offset (behind on -Z + above on +Y). Re-snaps so
        /// a mid-flight offset change can't leave a stale framing.</summary>
        public void SetOffset(Vector3 offset)
        {
            _offset = offset;
            if (_target != null) SnapToTarget();
        }

        /// <summary>
        /// Kicks a decaying camera shake — a random positional jitter layered on
        /// top of the follow pose in <see cref="LateUpdate"/> that fades to zero
        /// over <paramref name="duration"/>. Re-issuing keeps the stronger shake
        /// (so a tower hit during a boss flourish doesn't cut the big shake short).
        /// </summary>
        /// <param name="intensity">Peak offset in metres (e.g. 0.25 small, 0.7 big).</param>
        /// <param name="duration">Seconds the shake takes to decay to zero.</param>
        public void Shake(float intensity, float duration)
        {
            if (intensity <= 0f || duration <= 0f) return;
            // Keep whichever shake is stronger / longer so overlaps don't truncate.
            if (intensity >= _shakeAmplitude)
            {
                _shakeAmplitude = intensity;
                _shakeDuration = duration;
                _shakeTimeLeft = duration;
            }
            else
            {
                _shakeTimeLeft = Mathf.Max(_shakeTimeLeft, duration);
            }
        }

        /// <summary>Immediately places the camera at its resting pose behind + above
        /// the target and aims it down at the hero.</summary>
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

            // Backstop: if Target was assigned before this component's first
            // LateUpdate ran (or the offset changed) we may not have snapped yet —
            // do it now so the camera never lerps in from the world origin.
            if (!_snapped) { SnapToTarget(); return; }

            transform.position = Vector3.SmoothDamp(
                transform.position, DesiredPosition(), ref _velocity, _positionSmoothTime);
            AimAtTarget();
            ApplyShake();
        }

        /// <summary>Layers the decaying random shake offset on top of the framed pose.</summary>
        private void ApplyShake()
        {
            if (_shakeTimeLeft <= 0f) return;
            _shakeTimeLeft -= Time.deltaTime;
            if (_shakeTimeLeft <= 0f) { _shakeAmplitude = 0f; return; }

            float k = _shakeDuration > 0f ? _shakeTimeLeft / _shakeDuration : 0f; // 1 → 0 linear decay
            float mag = _shakeAmplitude * k;
            Vector3 jitter = new Vector3(
                (Random.value * 2f - 1f),
                (Random.value * 2f - 1f),
                (Random.value * 2f - 1f)) * mag;
            transform.position += jitter;
        }

        /// <summary>
        /// The resting camera position: a WORLD-SPACE offset from the target —
        /// behind on -Z, above on +Y. Deliberately NOT rotated into the target's
        /// facing, so a hero facing flip can never swing the camera in front of /
        /// below the hero (the inverted-view bug).
        /// </summary>
        private Vector3 DesiredPosition()
        {
            return _target.position + _offset;
        }

        /// <summary>
        /// Aims at a point slightly above the hero's feet. The camera sits above
        /// that point (offset.y &gt; lookAtHeight is the intended setup), so the
        /// resulting direction always angles DOWN — never up at the tower.
        /// </summary>
        private void AimAtTarget()
        {
            Vector3 lookAt = _target.position + Vector3.up * _lookAtHeight;
            Vector3 dir = lookAt - transform.position;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}
