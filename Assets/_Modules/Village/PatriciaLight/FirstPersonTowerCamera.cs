// =============================================================================
// FirstPersonTowerCamera — first-person view from the hero's ledge.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The hero defends from a ledge in front of the tower, facing OUT over the
// field, and strafes left/right along it (PatriciaLightController drives the
// movement + auto-targeting). This camera simply rides at the hero's eye height
// and looks forward + slightly down, so you see the ledge rail and the troops
// below — the tower sits behind the camera and never blocks the view.
//
// Sits slightly forward of the hero so the hero's own weapon/body doesn't fill
// the frame. Includes the same Shake() entry point as ThirdPersonCameraFollow.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>First-person ledge camera: rides the hero's eyes, looks out and
    /// down over the field. Set <see cref="Target"/> to the hero.</summary>
    [DisallowMultipleComponent]
    public sealed class FirstPersonTowerCamera : MonoBehaviour
    {
        [Tooltip("The hero the camera rides. Assigned at runtime by PatriciaLightController.")]
        public Transform Target;

        /// <summary>Optional world-space point the camera should look directly at (set by controller from the current reticle/target).
        /// When set, the camera orients to look exactly at this point (great for FP surveying ground or sky by moving the reticle).
        /// Falls back to the hero-forward + pitch logic if not set.</summary>
        public Vector3? AimLookPoint;

        [SerializeField] private float _eyeHeight    = 1.85f;   // eyes on the high tower perch
        [SerializeField] private float _forwardNudge  = 0.75f;  // bring the glowing bow nicely into the lower FP frame for animation feedback
        [SerializeField] private float _pitchDown     = 18f;    // base pitch when no explicit AimLookPoint (see ground + sky balance)

        private float _shakeAmplitude, _shakeTimeLeft, _shakeDuration;

        public void Shake(float intensity, float duration)
        {
            if (intensity <= 0f || duration <= 0f) return;
            if (intensity >= _shakeAmplitude) { _shakeAmplitude = intensity; _shakeDuration = duration; _shakeTimeLeft = duration; }
            else { _shakeTimeLeft = Mathf.Max(_shakeTimeLeft, duration); }
        }

        private void LateUpdate()
        {
            if (Target == null) return;

            Vector3 pos = Target.position + Vector3.up * _eyeHeight + Target.forward * _forwardNudge + ShakeOffset();

            if (AimLookPoint.HasValue)
            {
                // Direct look-at the current survey/target point (reticle projected or locked enemy).
                // This gives true first-person "move reticle to look around ground and sky" feel
                // while perched on the tower. Perfect for fast targeting.
                Vector3 dir = (AimLookPoint.Value - pos);
                if (dir.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                else
                    transform.rotation = Target.rotation * Quaternion.Euler(_pitchDown, 0f, 0f);
            }
            else
            {
                Vector3 fwd = Target.forward; fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
                fwd.Normalize();
                transform.rotation = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(_pitchDown, 0f, 0f);
            }

            transform.position = pos;
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

        private void OnGUI()
        {
            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f, s = 11f, t = 2f;
            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.75f);
            GUI.DrawTexture(new Rect(cx - s, cy - t * 0.5f, s * 2f, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - t * 0.5f, cy - s, t, s * 2f), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
