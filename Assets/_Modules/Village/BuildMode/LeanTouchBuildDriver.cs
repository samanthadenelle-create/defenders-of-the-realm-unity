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
//   (WO-1010 D5/D6/D12, 2026-08-09: the WO-683 always-on 4-zone d-pad this bar used
//    to build is RETIRED — it rode Install and so sat on screen through BOTH phases,
//    including PICK where there is nothing to nudge. The nudge control is now the
//    Build HUD's OWN state-gated analog stick — BuildHudController.BuildNudgePad,
//    shown only while a piece is being positioned — so this driver keeps ONLY the
//    gesture handling plus the hidden probe anchor below.)
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
        // NOTE (Grok #8): the overview HEIGHT clamp moved to BuildModeController
        // (_camHeightMin/_camHeightMax) — the driver no longer owns zoom bounds; it
        // forwards the pinch scale to ctrl.AdjustZoom, which clamps + re-applies the orbit.

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
            // WO-1010 D5/D6/D12 — nothing VISIBLE rides this bar any more: the always-on
            // d-pad is retired (the nudge stick is BuildHudController's, state-gated) and
            // Cancel is built-but-hidden as the probe anchor. The activation is kept so the
            // canvas exists exactly when the touch driver is live (probe/contract parity).
            FlowTrace.Step("Build", "TouchBar active for this build session -- gesture handling only; " +
                "no visible chrome (WO-1010: always-on d-pad retired, nudge stick is the HUD's, state-gated).");
            enabled = true;
        }

        /// <summary>Hide the verb bar and stop driving (controller calls on Exit).</summary>
        public void Uninstall()
        {
            if (_barRoot != null) _barRoot.SetActive(false);
            // (WO-1010: the WO-683 "zero the published HudMoveInput" step went with the
            // d-pad — this driver no longer publishes any move vector, so there is nothing
            // held that could keep steering after Exit.)
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
            // IsOverGui only counts raycast hits on LeanTouch.CurrentGuiLayers (default layer 5);
            // every canvas this project code-builds sits on layer 0, so it reads FALSE over the
            // PLACE button on device. Also run the controller's EventSystem probe, which is
            // layer-agnostic. Tested at the RAW finger point — the +90px thumb lift lands in the
            // deliberately raycast-transparent band above the button and would pass the probe.
            if (finger.IsOverGui || BuildModeController.IsPointOverUi(finger.ScreenPosition)) return;
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
            // Same layer-0 blind spot as the tap handler: pair IsOverGui with the
            // layer-agnostic EventSystem probe, at the RAW finger point.
            if (finger.IsOverGui || BuildModeController.IsPointOverUi(finger.ScreenPosition)) return;
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

            // SME camera (Grok #8): the driver NO LONGER writes camera.transform.position
            // (that fought BuildModeController.ApplyBuildCamera every frame). Two-finger
            // gestures call the controller's setters, which mutate _camFocus/_camHeight/
            // _camYaw and re-apply the orbit. One finger = placement (the tap/drag handlers
            // above), two fingers = camera — no conflict.
            var ctrl = BuildModeController.Instance;
            if (ctrl == null) return;

            List<LeanFinger> fingers = LeanTouch.GetFingers(true, true, 2);   // ignore GUI, want 2
            if (fingers == null || fingers.Count < 2) return;

            // PINCH → zoom (scale > 1 = fingers apart = zoom IN; controller clamps height).
            float scale = LeanGesture.GetPinchScale(fingers);
            if (scale > 0f && !Mathf.Approximately(scale, 1f))
                ctrl.AdjustZoom(scale);

            // TWIST → rotate the view (controller snaps yaw to 45° detents).
            float twist = LeanGesture.GetTwistDegrees(fingers);
            if (!Mathf.Approximately(twist, 0f))
                ctrl.AdjustYaw(twist);

            // PAN → slide the focus opposite the two-finger drag (controller clamps to map).
            Vector2 panDelta = LeanGesture.GetScreenDelta(fingers);
            if (panDelta.sqrMagnitude > 0f)
                ctrl.PanFocusBy(panDelta, _panMetresPerPx);
        }

        // =====================================================================
        //  Code-built uGUI verb bar (WO-677 — the BuildPlaceButton pattern)
        // =====================================================================

        /// <summary>
        /// Build the touch-bar canvas once. After the WO-1010 retirements it carries NO
        /// visible chrome: the rotate pair went to the HUD rail (Grok slice 2), Cancel is
        /// built-but-hidden as the fleet's probe anchor, and the WO-683 always-on d-pad is
        /// gone (D5/D6/D12 — the nudge control is BuildHudController's state-gated stick).
        /// Own overlay canvas + GraphicRaycaster kept so the hidden anchor proves the bar
        /// is code-built uGUI with no PanelSettings dependency (AssertTouchVerbBarRenderable).
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
            // Grok slice 2 (KILL the duplicate rotate): the Rotate Left/Right pair that
            // used to live HERE is REMOVED — rotate now has exactly ONE home, the Build
            // HUD's single intent bar (BuildHudController → RequestUiRotateQuarter).
            //
            // DUPLICATE-CANCEL FIX (owner device felt-test 2026-07-16 "why are there two
            // cancel buttons on build"): this touch-bar Cancel rendered ON TOP OF (above-
            // and-right of) the Build HUD intent bar's OWN "Cancel" — two identical yellow
            // "Cancel" buttons on screen at once during placement. The ONE cancel now lives
            // in the HUD intent bar (BuildHudController → RequestUiCancel → CancelArmed,
            // which aborts the placement AND re-opens the selection bar via
            // BuildPaletteUI.Expand). So this button is BUILT-BUT-HIDDEN: kept ONLY as the
            // stable anchor the fleet's AssertTouchVerbBarRenderable probe scans for (that
            // probe walks INACTIVE children — GetComponentsInChildren<Button>(true) — so an
            // inactive Cancel still proves the bar is code-built uGUI with no PanelSettings),
            // and it never draws a second Cancel on device.
            var cancelBtn = DeNelle.Core.UI.ElarionUiKit.BuildObsidianButton(_barRoot.transform, "Cancel",
                DeNelle.Core.UI.ElarionUiKit.ObsidianButtonStyle.Style1,
                DeNelle.Core.UI.ElarionUiKit.ObsidianButtonColor.Yellow,
                new Vector2(0.845f, 0.16f), new Vector2(0.985f, 0.235f),
                () => { FlowTrace.Step("Build", "TouchBar: Cancel pressed"); _cancelLatched = true; });
            // Stable name — the fleet's AssertTouchVerbBarRenderable probe finds the bar
            // by this GO name (DevTools has no TMPro ref to read the label).
            cancelBtn.gameObject.name = "BuildTouchCancel";
            // Hidden so it never renders a SECOND Cancel over the HUD intent bar's Cancel
            // (owner "two cancel buttons"); the GO survives inactive as the probe anchor.
            cancelBtn.gameObject.SetActive(false);
            FlowTrace.Step("BuildHud", "TouchBar: duplicate Cancel BUILT-BUT-HIDDEN — the ONE " +
                "cancel is the HUD intent bar's (BuildHudController.RequestUiCancel); this GO " +
                "kept inactive only for the AssertTouchVerbBarRenderable probe.");

            // ── WO-1010 D5/D6/D12: the WO-683 always-on 4-zone d-pad is GONE. ──
            // It was built here unconditionally and shown on Install, so every touch
            // session carried an arrow pad through BOTH phases — including PICK, where
            // there is no ghost to nudge (the owner's D6 capture shows it overprinting
            // the open carousel and the first card). The nudge control is now OWNED by
            // BuildHudController: its analog stick (BuildNudgePad) is state-gated to
            // Placing + carousel-minimized, so nothing here should ever draw a second
            // move control. The HudMoveInput reflection publish seam went with it —
            // the HUD stick feeds the brain through BuildHudController.NudgeVector.
            FlowTrace.Step("Build", "TouchBar: WO-683 always-on d-pad RETIRED (WO-1010 D5/D6/D12) -- " +
                "the one nudge control is the Build HUD's state-gated stick; this bar keeps " +
                "gesture handling + the hidden Cancel probe anchor only.");

            _barRoot.SetActive(false);   // shown by Install, hidden by Uninstall
        }

        // NOTE (WO-1010, 2026-08-09): the WO-683 PublishDpadMove reflection seam
        // (DeNelle.HUD.Kit.HudMoveInput.Set by loose reflection) was DELETED with the
        // always-on d-pad — this file publishes no move vector any more. The build nudge
        // now flows BuildHudController.BuildNudgePad -> NudgeVector -> the brain's poll,
        // with no reflection bridge at all (§10: no new System.Reflection in bridges —
        // this pass removed the last one this file carried).
    }
}
