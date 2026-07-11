// =============================================================================
// DesktopBuildInput — the mouse/keyboard IBuildInput (Build Mode S6).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Reads the NEW Input System (Mouse.current / Keyboard.current):
//   ScreenPoint   = Mouse.current.position
//   PlaceOrSelect = Mouse.current.leftButton.wasPressedThisFrame   (left-click)
//   Cancel        = right-click || Escape
//   Rotate        = (removed — mobile-first; use the touch Rotate button)
//   RotateCcw/Cw  = Q / E keys (±45° ghost yaw — WO-673 L5)
//
// WHY NOT legacy Input.*: this project runs the Input System package with the
// legacy Input Manager DISABLED, so Input.GetMouseButtonDown(0) / Input.mousePosition
// silently no-op on desktop + WebGL. That was the "build grid shows but a click never
// LOCKS IN a placement" bug — the ghost could follow but nothing ever constructed.
// Reading the new devices restores click-to-place / click-to-confirm on desktop + web.
//
// This is the controller's DEFAULT input source; the Lean.Touch driver replaces it on
// a touch device via BuildModeController.SetInput().
// =============================================================================

using UnityEngine;
using UnityEngine.InputSystem;
using DeNelle.Core.Diagnostics;   // EDIT-ONLY: FlowTrace breadcrumb (WebGL Mouse.current probe)

namespace DeNelle.Village
{
    /// <summary>
    /// Plain mouse+keyboard <see cref="IBuildInput"/> via the new Input System.
    /// Pure (no MonoBehaviour, no allocations) — the controller holds one instance.
    /// Every device read is null-guarded so a missing mouse/keyboard never throws.
    /// </summary>
    public sealed class DesktopBuildInput : IBuildInput
    {
        public Vector2 ScreenPoint =>
            Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        public bool PlaceOrSelect
        {
            get
            {
                // TGVRU §12 (EDIT-ONLY instrumentation) — the SUSPECTED WebGL-desktop cause: the
                // new Input System's Mouse.current can be null in a WebGL player (no HID mouse
                // device bound), which makes every PlaceOrSelect read false, so a click never LOCKS
                // IN a placement. Log the device state ONCE per session so a web trace proves it.
                // Once() (Info) is used — the existing available once-per-session primitive.
                FlowTrace.Once("Build", "desktop-mouse-state",
                    $"DesktopBuildInput active; Mouse.current={(Mouse.current != null)}");
                return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            }
        }

        public bool Cancel =>
            (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) ||
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame);

        // Mobile-first: the R-key rotate trigger is removed. Rotate is reached by the
        // on-screen Rotate button in LeanTouchBuildDriver (the touch IBuildInput impl).
        public bool Rotate => false;

        // WO-673 L5 (owner ruling 2026-07-11: 45° steps, 8 facings) — desktop rotate keys.
        // Q = counter-clockwise, E = clockwise (the project's WASD-adjacent convention;
        // WASD/arrows already pan the build camera, so Q/E are free). Single-frame
        // wasPressedThisFrame edges, null-guarded like every other device read here.
        public bool RotateCcw =>
            Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;

        public bool RotateCw =>
            Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
    }
}
