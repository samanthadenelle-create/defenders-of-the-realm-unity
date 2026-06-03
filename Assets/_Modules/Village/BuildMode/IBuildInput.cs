// =============================================================================
// IBuildInput — the input seam for Build Mode placement (Build Mode S6).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// BuildModeController used to read Input.GetMouseButton* / KeyCode.R / Escape and
// Input.mousePosition directly in its Update loops — a desktop-only path that is
// unusable on a phone. Following the Defend-the-Tower precedent (TowerAimSystem =
// input-agnostic core, LeanTouchAimDriver = the ONLY Lean.Touch file), the
// controller now asks an IBuildInput source for HIGH-LEVEL INTENTS each frame
// instead of reading raw devices:
//
//   ScreenPoint  — the cursor/finger screen position the ground ray casts through.
//   PlaceOrSelect— a confirm this frame: place the ghost / select a structure /
//                  commit a move (the old left-click-down).
//   Cancel       — back out the armed entry or an in-progress move (old right-click
//                  / Escape).
//   Rotate       — yaw the ghost +90° (old R key; on mobile an on-screen button).
//
// Two implementations ship:
//   • DesktopBuildInput  — wraps Input.* EXACTLY as the old loops did (desktop
//                          mouse/keyboard path is unchanged).
//   • LeanTouchBuildDriver — the ONLY Lean.Touch-dependent Build Mode file; maps
//                          touch gestures + code-built Rotate/Done buttons onto
//                          these same intents.
//
// The controller defaults to DesktopBuildInput; the touch driver installs itself
// via SetInput() so adding/removing it changes nothing on desktop.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// High-level, device-agnostic Build Mode placement intents. The controller
    /// polls these each frame in place of reading <c>Input.*</c> directly, so the
    /// same place / move / select / rotate / cancel loops drive from mouse+keyboard
    /// (desktop) or touch (mobile) with no per-loop branching.
    /// </summary>
    public interface IBuildInput
    {
        /// <summary>
        /// Per-frame, the screen-space point the ground ray should be cast through
        /// (the cursor on desktop; the active finger — offset above the fingertip so
        /// the ghost isn't hidden — on touch). Pixels, origin bottom-left, matching
        /// <see cref="Input.mousePosition"/> so existing ScreenPointToRay calls are
        /// unchanged.
        /// </summary>
        Vector2 ScreenPoint { get; }

        /// <summary>
        /// True on the single frame a place/select/commit is requested (a tap that
        /// went down this frame on desktop's left button; a Lean tap on touch). The
        /// controller acts on it only when the placement under <see cref="ScreenPoint"/>
        /// is valid, so a spurious true is harmless.
        /// </summary>
        bool PlaceOrSelect { get; }

        /// <summary>
        /// True on the single frame the player backs out the armed entry / move
        /// (old right-click or Escape; a Cancel button on touch). Does NOT exit Build
        /// Mode — that is the palette's Done button.
        /// </summary>
        bool Cancel { get; }

        /// <summary>
        /// True on the single frame a +90° yaw is requested (old R key; an on-screen
        /// Rotate button on touch). Free, so it applies in both the place and move loops.
        /// </summary>
        bool Rotate { get; }
    }
}
