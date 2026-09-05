// =============================================================================
// SettingsGate - the Core-level "open Settings" seam (WO-1399). PauseGate's twin.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.UI
//
// THE PROBLEM: the gear dock's row labelled "Settings" (HudKitController.AddDockTab)
// opened the HELP menu (HelpMenu.ToggleOverlay) - Report a Bug / Controls / Credits -
// because the HUD had no way to reach the real options screen. SettingsController
// (quality / difficulty / wallet / privacy / offline) lives in DeNelle.Settings, and
// DeNelle.HUD.asmdef references Core + Data only; DeNelle.Settings.asmdef references
// Core only. Neither may reference the other, so the only real Settings door was
// Pause -> Settings: a door hidden behind another door
// (docs/qa/UI_SCREEN_GRAPH_2026-09-04.md dead end 8).
//
// THE SEAM: exactly the PauseGate shape. The HUD calls RequestOpen(source); this gate
// raises SettingsOpenRequested; SettingsController subscribes in OnEnable (as
// PauseController does for PauseGate.PauseToggleRequested) and calls its own Open().
// Kept event-based so Core never references DeNelle.Settings.
//
// NO SILENT FAILURE (CLAUDE.md section 12): a request with no subscriber is a
// FlowTrace.Fail, never a swallowed no-op - a scene without PauseHudBootstrap's
// SettingsController would otherwise present a dead "Settings" row with no error.
//
// Pure static state (reset on domain reload). No MonoBehaviour / scene object - alive
// across additive scene loads, same as PauseGate and PanelManager.
// =============================================================================

using System;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.UI
{
    /// <summary>
    /// Static "open Settings" seam. The HUD's gear-dock "Settings" row calls
    /// <see cref="RequestOpen"/>; <see cref="SettingsOpenRequested"/> is raised for the
    /// options screen (SettingsController, DeNelle.Settings) to open itself. The string
    /// argument names the door that asked ("dock", ...) so the trace can tell doors apart.
    /// </summary>
    public static class SettingsGate
    {
        /// <summary>
        /// Raised by <see cref="RequestOpen"/>. SettingsController subscribes to this and
        /// calls its Open(). Kept event-based (not a direct call) so Core never references
        /// DeNelle.Settings. The argument is the requesting door, for the trace.
        /// </summary>
        public static event Action<string> SettingsOpenRequested;

        /// <summary>True while at least one listener is attached (the options screen is installed).</summary>
        public static bool HasSubscriber => SettingsOpenRequested != null;

        /// <summary>
        /// The single cross-assembly "open Settings" action. Null-safe; safe to call from any
        /// assembly that references DeNelle.Core. A request that finds NO subscriber is a
        /// FlowTrace.Fail (the row would otherwise be a dead button with no error).
        /// </summary>
        public static void RequestOpen(string source)
        {
            string from = string.IsNullOrEmpty(source) ? "<unknown>" : source;
            var handlers = SettingsOpenRequested;
            if (handlers == null)
            {
                FlowTrace.Fail("Settings",
                    "SettingsGate.RequestOpen from=" + from + " had NO subscriber - SettingsController " +
                    "not installed in this scene (PauseHudBootstrap skips front-end scenes).");
                return;
            }
            FlowTrace.Step("Settings", "SettingsGate.RequestOpen from=" + from + " -> raising SettingsOpenRequested");
            handlers.Invoke(from);
        }
    }
}
