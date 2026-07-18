// =============================================================================
// UICaptureLaunch -- editor launch hook for the UICaptureMode UI screenshot harness.
// -----------------------------------------------------------------------------
// The capture itself is a RUNTIME MonoBehaviour (UICaptureMode, DeNelle.Core) that
// only works in Play mode (panels register with PanelRouter in Awake / boot hooks).
// -executeMethod runs in EDIT mode, so this editor-assembly entry sets the one-shot
// SessionState request flag and ENTERS Play mode; UICaptureMode's boot hook sees the
// flag across the domain reload and runs, then exits the editor itself.
//
// INVOKE (keep the editor CLOSED so the project isn't locked; do NOT pass -quit --
// the harness calls EditorApplication.Exit(0) when done):
//   "<Unity>\Unity.exe" -projectPath D:\eoa -batchmode ^
//     -executeMethod DeNelle.Editor.UICaptureLaunch.RunCapture -logFile -
//   (omit -nographics for REAL pixels; -nographics logs the drive with blank frames)
// Or in-editor: menu  Defenders/UI/Capture UI Panels.
// =============================================================================

using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>Editor entry that flips into Play mode with the UI-capture flag set.</summary>
    public static class UICaptureLaunch
    {
        [MenuItem("Defenders/UI/Capture UI Panels")]
        public static void RunCapture()
        {
            // Same key UICaptureMode reads at boot (kept as a public const there).
            SessionState.SetBool(DeNelle.Diagnostics.UICaptureMode.EditorRequestKey, true);
            Debug.Log("[UICap] capture requested -> entering Play mode (graphics run = real pixels; " +
                      "-nographics = blank frames + drive log).");
            if (!EditorApplication.isPlaying)
                EditorApplication.EnterPlaymode();
        }
    }
}
