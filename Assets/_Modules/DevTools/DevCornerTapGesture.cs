// =============================================================================
// DevCornerTapGesture — hidden 5-tap-in-the-LEFT-corner opener for the DevPanel.
// -----------------------------------------------------------------------------
// WHY THIS EXISTS (owner 2026-07-04): on the web / mobile build there is no
// keyboard, so F1/F10 can never pop the QA dev console. This driver gives the
// console a TOUCH + MOUSE entry: FIVE taps inside a small BOTTOM-LEFT hotspot,
// within ~3 seconds, toggles the console open (tap five more times to close it).
//
// WHY A POLLING MonoBehaviour (not the old UITK ClickEvent zone):
// DevPanelController's own `dev-corner-tap` VisualElement gesture is DISABLED
// (F10CornerTapRetired) AND UITK synthetic/pointer clicks are unreliable in the
// built WebGL player (memory: uitk-synthetic-click-navigationsubmit — manual
// Mouse/ClickEvents never reach Clickable in the player). Polling the legacy
// Input API for touch-began / mouse-down is the robust WebGL-safe path, so this
// driver reads raw taps and calls the existing open path — DevPanelController.
// Toggle() — rather than re-plumbing the flaky UITK zone.
//
// RELEASE-SAFE: the whole file is `#if DEVELOPMENT_BUILD || UNITY_EDITOR` and it
// lives in DeNelle.DevTools, whose asmdef carries the matching define constraint
// (UNITY_EDITOR || DEVELOPMENT_BUILD). A shipped (non-development) player build
// compiles this to nothing — same gate as the dev console it opens. DevBootstrap
// adds this component to the same DontDestroyOnLoad console GameObject, so it is
// always present in every scene of a dev build with zero per-scene wiring.
// =============================================================================

#if DEVELOPMENT_BUILD || UNITY_EDITOR

using DeNelle.Core.Diagnostics;
using UnityEngine;

namespace DeNelle.DevTools
{
    /// <summary>
    /// DEV-ONLY input driver: FIVE taps/clicks inside the bottom-left screen
    /// hotspot within a short window toggles the sibling <see cref="DevPanelController"/>.
    /// Works for touch (mobile / WebGL) and mouse (desktop web / editor).
    /// Compiled out of release builds (see file header + asmdef constraint).
    /// </summary>
    [RequireComponent(typeof(DevPanelController))]
    public sealed class DevCornerTapGesture : MonoBehaviour
    {
        /// <summary>Taps required inside the hotspot, within the window, to toggle.</summary>
        private const int TapsToToggle = 5;

        /// <summary>Whole-gesture time budget: all taps must land within this window.</summary>
        private const float WindowSec = 3.0f;

        /// <summary>Hotspot size as a fraction of screen width/height (bottom-LEFT ~15% × ~15%).</summary>
        private const float HotspotFrac = 0.15f;

        private DevPanelController _panel;
        private int _tapCount;
        private float _firstTapTime;

        private void Awake() => _panel = GetComponent<DevPanelController>();

        private void Update()
        {
            if (!TryGetTapPosition(out Vector2 pos)) return;

            float now = Time.unscaledTime;

            // Time window lapsed since the first tap → start a fresh streak.
            if (_tapCount > 0 && now - _firstTapTime > WindowSec)
                _tapCount = 0;

            // Input.mousePosition / touch.position use origin bottom-left, so the
            // bottom-LEFT corner is small x AND small y.
            bool inHotspot = pos.x <= Screen.width * HotspotFrac
                          && pos.y <= Screen.height * HotspotFrac;

            if (!inHotspot)
            {
                // A tap that strays outside the corner breaks the streak.
                _tapCount = 0;
                return;
            }

            if (_tapCount == 0) _firstTapTime = now;
            _tapCount++;

            if (_tapCount >= TapsToToggle)
            {
                _tapCount = 0;
                if (_panel == null) _panel = GetComponent<DevPanelController>();
                if (_panel != null)
                {
                    FlowTrace.Step("UI", $"DevPanel toggled via {TapsToToggle}-tap bottom-LEFT corner gesture (touch/mouse).");
                    _panel.Toggle();
                }
            }
        }

        /// <summary>
        /// Returns the screen position of a NEW tap/click this frame (touch-began or
        /// mouse-button-down), false if none. Touch is checked first (mobile / WebGL);
        /// mouse only when there is no active touch, so a touch-as-mouse platform
        /// never double-counts a single physical tap.
        /// </summary>
        private static bool TryGetTapPosition(out Vector2 pos)
        {
            pos = default;

            if (Input.touchCount > 0)
            {
                foreach (var t in Input.touches)
                {
                    if (t.phase == TouchPhase.Began) { pos = t.position; return true; }
                }
                return false;
            }

            if (Input.GetMouseButtonDown(0)) { pos = (Vector2)Input.mousePosition; return true; }
            return false;
        }
    }
}

#endif // DEVELOPMENT_BUILD || UNITY_EDITOR
