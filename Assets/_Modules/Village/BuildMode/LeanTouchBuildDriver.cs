// =============================================================================
// LeanTouchBuildDriver — THE ONLY Lean.Touch-dependent file in Build Mode (S6).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Makes Build Mode thumb-playable on a phone by implementing IBuildInput from
// touch gestures + an on-screen verb bar. Mirrors LeanTouchAimDriver: the
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
//   [Rotate Left/Right] → RotateCcw / RotateCw (±45° yaw, WO-673 L5) — mirrors Q/E.
//   [Cancel] button  → Cancel (back out arm/move) — replaces right-click / Escape.
//   [D-pad cross]    → the SAME kit d-pad the combat/town HUD hosts (WO-683):
//                      publishes HudMoveInput.Move (loose reflection), which the
//                      controller merges into the arrow-key move vector so the
//                      armed ghost / in-progress move nudges exactly like keys.
//
// WO-677 (MOB-1, 2026-07-12): the verb bar is CODE-BUILT uGUI on its own
// Screen-Space-Overlay canvas via ElarionUiKit — the SAME pattern as
// BuildPlaceButton, whose taps are PROVEN working on the owner's phone. It was
// previously a UIToolkit UIDocument that adopted a sibling's PanelSettings
// (AdoptPanelSettings): fleet census 2026-07-12 proved adoption *can* succeed
// (AdminOverlay et al.), but the bar then still rode UITK — the one UI class this
// project has banned on web builds (PIPELINE_STATE landmine; BuildPaletteUI was
// converted off UIDocument for the same reason). The uGUI rebuild removes the
// whole silent-non-render/unpickable class: no PanelSettings, no adoption, no
// UITK picking. Buttons register with the EventSystem via GraphicRaycaster, so
// LeanTouch's finger.IsOverGui still suppresses world taps over the bar.
//
// The controller installs this via Install() on Enter and Uninstall() on Exit, so
// on a desktop build where it is never installed the mouse path is untouched.
// =============================================================================

using System.Collections.Generic;
using System.Reflection;          // WO-683: loose-reflection publish into HudMoveInput.Set (no Village->HUD edge)
using UnityEngine;
using UnityEngine.UI;
using Lean.Touch;
using DeNelle.Core.Diagnostics;   // FlowTrace breadcrumbs (touch install + tap + bar verbs)

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
        private bool _rotateLatched;           // legacy single-direction latch (kept for IBuildInput.Rotate compat)
        private bool _rotateCwLatched;         // raised by the "Rotate Right" button (WO-673 L5: +45°)
        private bool _rotateCcwLatched;        // raised by the "Rotate Left" button (WO-673 L5: -45°)

        // ── On-screen verb bar (code-built uGUI, WO-677) ─────────────────────
        private GameObject _barRoot;

        // =====================================================================
        //  Install / Uninstall (called by BuildModeController)
        // =====================================================================

        /// <summary>
        /// Wire the driver to the live overview camera and show the verb bar.
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
            if (_barRoot != null) _barRoot.SetActive(true);
            // WO-683 §12 — the d-pad-SHOWN decision, named: the pad rides the touch
            // bar, so it shows exactly when the touch driver installs (never desktop).
            FlowTrace.Step("Build", "TouchBar SHOWN — verb bar + kit d-pad visible for this build session " +
                "(WO-683 d-pad-shown decision: touch driver installed).");
            enabled = true;
        }

        /// <summary>Hide the verb bar and stop driving (controller calls on Exit).</summary>
        public void Uninstall()
        {
            if (_barRoot != null) _barRoot.SetActive(false);
            // WO-683 — zero the published move so a chevron still held at Exit can't
            // keep feeding HeroLocomotion (the same HudMoveInput static) after the
            // pad is hidden (its onUp never fires once the GO deactivates).
            PublishDpadMove(Vector2.zero);
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

        // WO-673 L5 (45° steps) — the two directed rotate intents the controller now
        // polls. Same latch-and-clear shape as the legacy Rotate so a button tap fires
        // exactly one 45° step per poll.
        public bool RotateCw  { get { bool v = _rotateCwLatched;  _rotateCwLatched  = false; return v; } }

        public bool RotateCcw { get { bool v = _rotateCcwLatched; _rotateCcwLatched = false; return v; } }

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
        /// ghost actually sits. Taps over the GUI (the verb bar's uGUI buttons — the
        /// EventSystem raycast counts GraphicRaycaster hits) are ignored so the bar
        /// can't double-fire a placement.
        /// </summary>
        private void HandleFingerTap(LeanFinger finger)
        {
            if (finger == null || finger.Index < 0) return;   // skip simulated mouse
            FlowTrace.Step("Build", $"finger tap idx={finger.Index} overGui={finger.IsOverGui} screen={finger.ScreenPosition}");
            if (finger.IsOverGui) return;                      // don't place through the verb bar
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
        //  Code-built uGUI verb bar (WO-677 — the BuildPlaceButton pattern)
        // =====================================================================

        /// <summary>
        /// Build the right-edge vertical verb stack once: the Rotate Left / Rotate Right
        /// pair (45° steps, WO-673 L5; ASCII text labels, WO-683) over Cancel, plus the
        /// WO-683 kit d-pad on the left. Own overlay canvas + GraphicRaycaster so the bar
        /// renders with NO external dependency and its taps register as GUI. Done/Exit
        /// stays on the palette's top bar; these are the in-placement verbs the
        /// keyboard owned. Seated x 0.845-0.985 / y 0.16-0.435 — clear of the centred
        /// 540px palette dock and of the PLACE button at x 0.66-0.80 (WO-677 Lane C).
        /// </summary>
        private void EnsureBuilt()
        {
            if (_barRoot != null) return;

            _barRoot = new GameObject("BuildTouchBarCanvas");
            _barRoot.transform.SetParent(transform, false);
            var canvas = _barRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5001;   // just above the PLACE button (5000), below modals
            var scaler = _barRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            _barRoot.AddComponent<GraphicRaycaster>();

            // Kit buttons (STYLE EVERYTHING OBSIDIAN — never hand-roll uGUI widgets).
            // Text labels carry the meaning, never color alone (owner colorblind).
            // WO-683 (owner ruling + device screenshot 2026-07-12): the ⟲/⟳ glyphs render
            // as tofu boxes on the shipped TMP font ("square symbol rotate") — labels are
            // plain ASCII TEXT: "Rotate Left" / "Rotate Right" (WO-611 landmine rule).
            DeNelle.Core.UI.ElarionUiKit.BuildObsidianButton(_barRoot.transform, "Rotate Left",
                DeNelle.Core.UI.ElarionUiKit.ObsidianButtonStyle.Style1,
                DeNelle.Core.UI.ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.845f, 0.36f), new Vector2(0.985f, 0.435f),
                () => { FlowTrace.Step("Build", "TouchBar: Rotate Left pressed"); _rotateCcwLatched = true; });

            DeNelle.Core.UI.ElarionUiKit.BuildObsidianButton(_barRoot.transform, "Rotate Right",
                DeNelle.Core.UI.ElarionUiKit.ObsidianButtonStyle.Style1,
                DeNelle.Core.UI.ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.845f, 0.26f), new Vector2(0.985f, 0.335f),
                () => { FlowTrace.Step("Build", "TouchBar: Rotate Right pressed"); _rotateCwLatched = true; });

            var cancelBtn = DeNelle.Core.UI.ElarionUiKit.BuildObsidianButton(_barRoot.transform, "Cancel",
                DeNelle.Core.UI.ElarionUiKit.ObsidianButtonStyle.Style1,
                DeNelle.Core.UI.ElarionUiKit.ObsidianButtonColor.Yellow,
                new Vector2(0.845f, 0.16f), new Vector2(0.985f, 0.235f),
                () => { FlowTrace.Step("Build", "TouchBar: Cancel pressed"); _cancelLatched = true; });
            // Stable name — the fleet's AssertTouchVerbBarRenderable probe finds the bar
            // by this GO name (DevTools has no TMPro ref to read the label).
            cancelBtn.gameObject.name = "BuildTouchCancel";

            // ── WO-683: THE kit d-pad on the build screen ─────────────────────
            // Owner ruling 2026-07-12: the d-pad from the combat/friendly HUD must
            // show in build mode and move the asset. BuildModeHudBridge hides the
            // WHOLE HUD kit while building (root CanvasGroup fade), so exempting one
            // widget from that fade would mean HUD-side surgery; instead the build
            // overlay hosts its OWN instance of the SAME kit builder the HUD uses
            // (same component, same chrome — mirrors HudKitController's moveCluster
            // branch, incl. the CombatHud611 cross-vs-cluster flag). It publishes
            // into DeNelle.HUD.Kit.HudMoveInput.Set by loose reflection (no
            // Village->HUD asmdef edge, §5 — the HeroLocomotion pattern's write
            // side); BuildModeController merges HudMoveInput.Move into the SAME
            // move vector as the arrow keys. Seated left side, ABOVE the hero
            // stick's bottom-left engage zone (VirtualJoystick.IsInZone) and clear
            // of the centred palette dock. Its chevron zones are uGUI buttons on
            // this canvas, so presses register as GUI (finger.IsOverGui) and can
            // never fall through as world taps. Direction is carried by the
            // chevron SHAPES + position, never color alone (owner colorblind).
            var dpad = DeNelle.Core.FeatureFlags.CombatHud611
                ? DeNelle.Core.UI.ElarionUiKit.BuildVirtualDPad(
                    _barRoot.transform, new Vector2(0.11f, 0.60f), PublishDpadMove)
                : DeNelle.Core.UI.ElarionUiKit.BuildControllerCluster(
                    _barRoot.transform, new Vector2(0.11f, 0.60f), PublishDpadMove);
            dpad.root.name = "BuildDPad";   // stable probe/debug name (BuildTouchCancel precedent)
            FlowTrace.Step("Build", "TouchBar: kit d-pad BUILT on the build overlay (WO-683) — " +
                (DeNelle.Core.FeatureFlags.CombatHud611 ? "VirtualDPad cross" : "controller cluster") +
                ", same builder as the combat/town HUD moveCluster.");

            _barRoot.SetActive(false);   // shown by Install, hidden by Uninstall
        }

        // ── WO-683: HudMoveInput publish seam (loose reflection, cached once) ──
        private static MethodInfo s_hudMoveSet;
        private static bool s_hudMoveSetResolved;

        /// <summary>
        /// Publish the build d-pad's held direction into DeNelle.HUD.Kit.HudMoveInput.Set —
        /// the SAME static the combat/town HUD d-pad writes and that BuildModeController /
        /// HeroLocomotion read by name (no Village->HUD asmdef edge, §5). Resolution is
        /// cached once; a miss or a throw WARNS (§12 — never a silent catch) and the pad
        /// goes inert rather than blanking the bar.
        /// </summary>
        private static void PublishDpadMove(Vector2 v)
        {
            if (!s_hudMoveSetResolved)
            {
                s_hudMoveSetResolved = true;
                try
                {
                    var t = System.Type.GetType("DeNelle.HUD.Kit.HudMoveInput, DeNelle.HUD");
                    s_hudMoveSet = t != null
                        ? t.GetMethod("Set", BindingFlags.Public | BindingFlags.Static,
                            null, new[] { typeof(Vector2) }, null)
                        : null;
                }
                catch (System.Exception ex)
                {
                    s_hudMoveSet = null;
                    FlowTrace.Warn("Build", "HudMoveInput.Set reflection resolve threw: " + ex.Message);
                }
                if (s_hudMoveSet == null)
                    FlowTrace.Warn("Build", "HudMoveInput.Set reflection MISS " +
                        "('DeNelle.HUD.Kit.HudMoveInput, DeNelle.HUD') — build d-pad presses publish nowhere (WO-683).");
            }
            if (s_hudMoveSet == null) return;
            try { s_hudMoveSet.Invoke(null, new object[] { v }); }
            catch (System.Exception ex)
            {
                FlowTrace.Warn("Build", "HudMoveInput.Set invoke threw: " + ex.Message);
            }
        }
    }
}
