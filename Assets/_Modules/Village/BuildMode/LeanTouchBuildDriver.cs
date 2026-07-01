// =============================================================================
// LeanTouchBuildDriver — THE ONLY Lean.Touch-dependent file in Build Mode (S6).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Makes Build Mode thumb-playable on a phone by implementing IBuildInput from
// touch gestures + a tiny code-built button bar. Mirrors LeanTouchAimDriver: the
// controller's place / move / select / rotate / cancel loops never reference
// Lean.Touch — only this file does, and the DeNelle.Village asmdef already refs
// LeanTouch / LeanCommon / CW.Common, so it compiles in-assembly (verified).
//
// GESTURE MAP
//   one-finger TAP   → PlaceOrSelect (arm-place / select a structure / commit move)
//   one-finger DRAG  → moves the ground-ray point so the ghost / selected follows
//                      the finger; the point is OFFSET above the fingertip so the
//                      ghost isn't hidden under the thumb.
//   two-finger PAN   → slides the overview camera across the plot.
//   two-finger PINCH → raises / lowers the overview camera (zoom).
//   [Rotate] button  → Rotate (+90° yaw)         — replaces the desktop R key.
//   [Cancel] button  → Cancel (back out arm/move) — replaces right-click / Escape.
//
// The controller installs this via Install() on Enter and Uninstall() on Exit, so
// on a desktop build where it is never installed the mouse path is untouched.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Lean.Touch;
using DeNelle.Core.Diagnostics;   // EDIT-ONLY: FlowTrace breadcrumbs (touch install + tap)

namespace DeNelle.Village
{
    /// <summary>
    /// Touch driver + on-screen Rotate/Cancel buttons that feed Build Mode's
    /// <see cref="IBuildInput"/> seam. The single Lean.Touch-dependent Build Mode
    /// file; everything it touches already exists, so adding/removing it changes
    /// nothing else (do-once-do-right, per LeanTouchAimDriver).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LeanTouchBuildDriver : MonoBehaviour, IBuildInput
    {
        // ── Tuning ────────────────────────────────────────────────────────────
        [Tooltip("Screen pixels the ground-ray point is lifted ABOVE the fingertip so the ghost isn't hidden under the thumb.")]
        [SerializeField] private float _thumbLiftPx = 90f;
        [Tooltip("World metres the overview camera pans per screen pixel of two-finger drag.")]
        [SerializeField] private float _panMetresPerPx = 0.06f;
        [Tooltip("Overview camera height (Y) clamp — min.")]
        [SerializeField] private float _minHeight = 22f;
        [Tooltip("Overview camera height (Y) clamp — max.")]
        [SerializeField] private float _maxHeight = 90f;

        // ── Wiring (set by BuildModeController.Install) ────────────────────────
        private Camera _overviewCamera;

        // ── IBuildInput state ─────────────────────────────────────────────────
        private Vector2 _screenPoint;
        private bool _placeOrSelectLatched;   // raised by a Lean tap, consumed next controller poll
        private bool _cancelLatched;           // raised by the Cancel button
        private bool _rotateLatched;           // raised by the Rotate button

        // ── On-screen button bar ──────────────────────────────────────────────
        private UIDocument _document;
        private VisualElement _root;

        // =====================================================================
        //  Install / Uninstall (called by BuildModeController)
        // =====================================================================

        /// <summary>
        /// Wire the driver to the live overview camera and show the button bar.
        /// Seeds <see cref="ScreenPoint"/> to screen-centre so the first frame casts
        /// somewhere sensible before the player touches.
        /// </summary>
        public void Install(Camera overviewCamera)
        {
            FlowTrace.Step("Build", $"LeanTouchBuildDriver.Install — LeanTouch.Instance={(LeanTouch.Instance != null)}");
            _overviewCamera = overviewCamera;
            _screenPoint = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            // Lean needs a LeanTouch instance in the scene to dispatch gestures.
            if (LeanTouch.Instance == null) gameObject.AddComponent<LeanTouch>();

            EnsureBuilt();
            if (_root != null) _root.style.display = DisplayStyle.Flex;
            enabled = true;
        }

        /// <summary>Hide the button bar and stop driving (controller calls on Exit).</summary>
        public void Uninstall()
        {
            if (_root != null) _root.style.display = DisplayStyle.None;
            _overviewCamera = null;
            enabled = false;
        }

        // =====================================================================
        //  IBuildInput — high-level intents the controller polls each frame
        // =====================================================================

        public Vector2 ScreenPoint => _screenPoint;

        public bool PlaceOrSelect { get { bool v = _placeOrSelectLatched; _placeOrSelectLatched = false; return v; } }

        public bool Cancel { get { bool v = _cancelLatched; _cancelLatched = false; return v; } }

        public bool Rotate { get { bool v = _rotateLatched; _rotateLatched = false; return v; } }

        // =====================================================================
        //  Lean.Touch gesture handling
        // =====================================================================

        private void OnEnable()
        {
            LeanTouch.OnFingerTap += HandleFingerTap;
            LeanTouch.OnFingerUpdate += HandleFingerUpdate;
        }

        private void OnDisable()
        {
            LeanTouch.OnFingerTap -= HandleFingerTap;
            LeanTouch.OnFingerUpdate -= HandleFingerUpdate;
        }

        /// <summary>
        /// One-finger tap = a place/select/commit. Lifted above the fingertip and
        /// stored as the ray point so the controller validates + acts at the spot the
        /// ghost actually sits. Taps over the GUI (the Rotate/Cancel buttons) are
        /// ignored so the bar can't double-fire a placement.
        /// </summary>
        private void HandleFingerTap(LeanFinger finger)
        {
            if (finger == null || finger.Index < 0) return;   // skip simulated mouse
            FlowTrace.Step("Build", $"finger tap idx={finger.Index} overGui={finger.IsOverGui} screen={finger.ScreenPosition}");
            if (finger.IsOverGui) return;                      // don't place through the button bar
            if (LeanTouch.Fingers.Count >= 2) return;          // 2-finger = camera gesture

            _screenPoint = LiftAboveThumb(finger.ScreenPosition);
            _placeOrSelectLatched = true;
        }

        /// <summary>
        /// One-finger drag tracks the ground-ray point so the ghost / selected
        /// structure follows the thumb (offset up so it isn't hidden). Two+ fingers
        /// are camera gestures, handled in Update via LeanGesture.
        /// </summary>
        private void HandleFingerUpdate(LeanFinger finger)
        {
            if (finger == null || finger.Index < 0) return;    // skip simulated mouse
            if (finger.IsOverGui) return;
            if (LeanTouch.Fingers.Count >= 2) return;          // camera gesture owns 2 fingers

            _screenPoint = LiftAboveThumb(finger.ScreenPosition);
        }

        /// <summary>Lift a fingertip screen point up so the ghost renders above the thumb (clamped on-screen).</summary>
        private Vector2 LiftAboveThumb(Vector2 fingerScreen)
        {
            float y = Mathf.Min(fingerScreen.y + _thumbLiftPx, Screen.height);
            return new Vector2(fingerScreen.x, y);
        }

        // =====================================================================
        //  Two-finger camera pan + pinch-zoom of the overview
        // =====================================================================

        private void Update()
        {
            if (_overviewCamera == null) return;

            List<LeanFinger> fingers = LeanTouch.GetFingers(true, true, 2);   // ignore GUI, want 2
            if (fingers == null || fingers.Count < 2) return;

            // PINCH → raise / lower the camera (zoom). scale > 1 = fingers apart = zoom IN.
            float scale = LeanGesture.GetPinchScale(fingers);
            if (scale > 0f && !Mathf.Approximately(scale, 1f))
            {
                Vector3 p = _overviewCamera.transform.position;
                p.y = Mathf.Clamp(p.y / scale, _minHeight, _maxHeight);
                _overviewCamera.transform.position = p;
            }

            // PAN → slide the camera across the plot opposite the two-finger drag.
            Vector2 panDelta = LeanGesture.GetScreenDelta(fingers);
            if (panDelta.sqrMagnitude > 0f)
            {
                Vector3 p = _overviewCamera.transform.position;
                // Top-down: screen-X → world-X, screen-Y → world-Z. Drag right pushes
                // the view left (content follows the fingers), so subtract.
                p.x -= panDelta.x * _panMetresPerPx;
                p.z -= panDelta.y * _panMetresPerPx;
                _overviewCamera.transform.position = p;
            }
        }

        // =====================================================================
        //  Code-built Rotate / Cancel button bar (no UXML — repo rule)
        // =====================================================================

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            if (_document == null) _document = gameObject.AddComponent<UIDocument>();
            AdoptPanelSettings();
        }

        /// <summary>
        /// Adopt a sibling UIDocument's PanelSettings so the bar renders (a freshly
        /// AddComponent'd UIDocument has none → invisible). Mirrors BuildPaletteUI;
        /// sorts above the palette so the buttons are tappable on top.
        /// </summary>
        private void AdoptPanelSettings()
        {
            if (_document == null) return;
            UIDocument hud = null, any = null;
            foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (doc == null || doc == _document || doc.panelSettings == null) continue;
                if (any == null) any = doc;
                if (doc.gameObject.name.IndexOf("Hud", System.StringComparison.OrdinalIgnoreCase) >= 0) { hud = doc; break; }
            }
            var src = hud ?? any;
            if (src != null)
            {
                _document.panelSettings = src.panelSettings;
                _document.sortingOrder = src.sortingOrder + 7;   // above HUD + palette, below selection panel
            }
            else
            {
                Debug.LogWarning("[LeanTouchBuildDriver] No sibling PanelSettings found — touch buttons will not render.");
            }
        }

        /// <summary>
        /// Build a right-edge vertical stack of two big thumb-sized buttons: Rotate
        /// (+90° yaw) and Cancel (back out the armed entry / move). Done/Exit stays on
        /// the palette's top bar; these are the in-placement verbs the keyboard owned.
        /// </summary>
        private void EnsureBuilt()
        {
            if (_root != null) return;
            var docRoot = _document != null ? _document.rootVisualElement : null;
            if (docRoot == null) return;

            _root = new VisualElement { name = "build-touch-controls" };
            _root.style.position = Position.Absolute;
            _root.style.right = 16;
            _root.style.bottom = 150;                  // above the bottom palette strip (~120 tall)
            _root.style.flexDirection = FlexDirection.Column;
            _root.style.alignItems = Align.FlexEnd;
            docRoot.Add(_root);

            var rotateBtn = new Button(() => _rotateLatched = true) { text = "Rotate" };
            StyleBigButton(rotateBtn, new Color(0.20f, 0.42f, 0.66f, 0.98f));
            _root.Add(rotateBtn);

            var cancelBtn = new Button(() => _cancelLatched = true) { text = "Cancel" };
            StyleBigButton(cancelBtn, new Color(0.30f, 0.32f, 0.40f, 0.98f));
            _root.Add(cancelBtn);
        }

        /// <summary>Thumb-sized button styling — large hit target for mobile.</summary>
        private static void StyleBigButton(Button b, Color bg)
        {
            b.style.height = 56; b.style.minWidth = 120;
            b.style.marginTop = 8; b.style.marginBottom = 8;
            b.style.fontSize = 18;
            b.style.unityFontStyleAndWeight = FontStyle.Bold;
            b.style.color = Color.white;
            b.style.backgroundColor = bg;
        }
    }
}
