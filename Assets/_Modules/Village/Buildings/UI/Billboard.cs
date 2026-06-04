// =============================================================================
// Billboard — DEF-76 (Linear, ProgressBar clarification). Rotates its GameObject
// to face the main camera each frame, so a world-space progress bar always reads
// flat to the viewer. Verbatim to the spec's snippet.
// namespace DeNelle.Village.UI.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village.UI
{
    /// <summary>Faces the transform toward the main camera every LateUpdate.</summary>
    public class Billboard : MonoBehaviour
    {
        private Camera _cam;

        private void Start() => _cam = Camera.main;

        private void LateUpdate()
        {
            if (_cam == null) { _cam = Camera.main; if (_cam == null) return; }
            transform.LookAt(
                transform.position + _cam.transform.rotation * Vector3.forward,
                _cam.transform.rotation * Vector3.up);
        }
    }
}
