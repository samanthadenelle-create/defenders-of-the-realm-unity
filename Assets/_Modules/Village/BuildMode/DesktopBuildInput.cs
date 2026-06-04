// =============================================================================
// DesktopBuildInput — the mouse/keyboard IBuildInput (Build Mode S6).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The desktop placement path, preserved 1:1 from the old BuildModeController
// Update loops so in-editor / Windows-build iteration is UNCHANGED:
//   ScreenPoint   = Input.mousePosition
//   PlaceOrSelect = Input.GetMouseButtonDown(0)            (left-click)
//   Cancel        = Input.GetMouseButtonDown(1) || Escape  (right-click / Escape)
//   Rotate        = Input.GetKeyDown(KeyCode.R)
//
// This is the controller's DEFAULT input source. The Lean.Touch driver replaces
// it on a touch device via BuildModeController.SetInput(); when it is absent the
// behaviour is exactly what it was before the seam was extracted.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Plain mouse+keyboard <see cref="IBuildInput"/>. Reads the legacy
    /// <c>Input.*</c> the old placement loops used, so the desktop path is byte-for-
    /// byte identical. Pure (no MonoBehaviour, no allocations) — the controller holds
    /// one instance.
    /// </summary>
    public sealed class DesktopBuildInput : IBuildInput
    {
        public Vector2 ScreenPoint => Input.mousePosition;

        public bool PlaceOrSelect => Input.GetMouseButtonDown(0);

        public bool Cancel => Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape);

        public bool Rotate => Input.GetKeyDown(KeyCode.R);
    }
}
