// =============================================================================
// LeanTouchAimDriver — THE ONLY Lean.Touch-dependent file in Defend-the-Tower.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Defend
//
// Maps mobile touch gestures onto the input-agnostic aim core:
//   • one-finger drag in the LEFT screen zone  → TowerAimSystem.AddAimDelta  (move crosshair)
//   • one-finger hold in the RIGHT screen zone → TowerAimSystem.SignalFire   (fire while held)
//   • two-finger pinch                         → HeroOverShoulderCamera.ApplyPinchScale (zoom)
//
// Everything it calls already exists on those two components — adding/removing
// this driver changes NOTHING else (do-once-do-right). The simulated mouse
// (Index < 0) is ignored here so the editor/desktop mouse fallback in
// TowerAimSystem keeps working for in-editor iteration; real touches (Index >= 0)
// take over and claim the external-driver flag.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using Lean.Touch;

namespace DeNelle.Village.Defend
{
    [DisallowMultipleComponent]
    public sealed class LeanTouchAimDriver : MonoBehaviour
    {
        public TowerAimSystem         Aim;   // assigned by PatriciaLightController
        public HeroOverShoulderCamera Cam;

        [Header("Tuning")]
        [Tooltip("Crosshair pixels moved per touch pixel dragged.")]
        [SerializeField] private float _aimSensitivity = 1.0f;
        [Tooltip("Screen fraction from the left: touches starting RIGHT of this fire; LEFT aim.")]
        [Range(0.1f, 0.9f)]
        [SerializeField] private float _fireZoneFromLeft = 0.5f;
        [Tooltip("Pinch APART zooms IN by default; tick to reverse.")]
        [SerializeField] private bool  _invertPinch = false;

        private void Awake()
        {
            // Lean needs a LeanTouch instance in the scene to dispatch gestures.
            if (LeanTouch.Instance == null) gameObject.AddComponent<LeanTouch>();
        }

        private void OnEnable()  => LeanTouch.OnFingerUpdate += HandleFingerUpdate;
        private void OnDisable() => LeanTouch.OnFingerUpdate -= HandleFingerUpdate;

        private void HandleFingerUpdate(LeanFinger finger)
        {
            if (Aim == null || finger == null) return;
            if (finger.Index < 0) return;          // skip simulated mouse / hover (desktop fallback owns those)
            if (finger.IsOverGui) return;          // don't aim/fire through HUD buttons
            if (LeanTouch.Fingers.Count >= 2) return;   // 2+ fingers = pinch (handled in Update)

            Aim.ClaimExternalDriver();             // on a touch device, suppress the desktop mouse fallback

            float split = Screen.width * Mathf.Clamp01(_fireZoneFromLeft);
            if (finger.StartScreenPosition.x <= split)
            {
                // Left zone = aim: nudge the crosshair by the drag delta.
                if (finger.ScreenDelta.sqrMagnitude > 0f)
                    Aim.AddAimDelta(finger.ScreenDelta * _aimSensitivity);
            }
            else
            {
                // Right zone = fire: hold to keep firing (debounced by the shooter cooldown).
                Aim.SignalFire(0.15f);
            }
        }

        private void Update()
        {
            if (Cam == null) return;
            List<LeanFinger> fingers = LeanTouch.GetFingers(true, true, 2);   // ignore GUI, want 2 fingers
            if (fingers == null || fingers.Count < 2) return;

            float scale = LeanGesture.GetPinchScale(fingers);
            if (scale <= 0f || Mathf.Approximately(scale, 1f)) return;

            // Pinch apart (scale > 1) → zoom IN (camera closer) feels natural, so invert by default.
            Cam.ApplyPinchScale(_invertPinch ? scale : 1f / scale);
        }
    }
}
