// =============================================================================
// DesktopBuildInput — the mouse/keyboard IBuildInput (Build Mode S6).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Reads the NEW Input System (Mouse.current / Keyboard.current):
//   ScreenPoint   = Mouse.current.position
//   PlaceOrSelect = Mouse.current.leftButton.wasPressedThisFrame   (left-click)
//   Cancel        = right-click || Escape
//   Rotate        = R key
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

        public bool PlaceOrSelect =>
            Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

        public bool Cancel =>
            (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) ||
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame);

        public bool Rotate =>
            Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
    }
}
