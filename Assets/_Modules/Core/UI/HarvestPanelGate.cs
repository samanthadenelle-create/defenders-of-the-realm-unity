// =============================================================================
// HarvestPanelGate — the Core-level "open/close the Echo harvest panel" seam.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.UI
//
// OWNER F8 (2026-06-28): the Echo / offline-harvest readout was ALWAYS-ON chrome
// (top-left widget). It is a side thought, not the main idea, so it must be tucked
// behind a button next to Settings. The button lives in the main HUD
// (DeNelle.HUD, VillageHudController); the panel lives with the harvest logic
// (DeNelle.Village, EchoWorkforceHud). Those two assemblies cannot reference each
// other (CLAUDE.md §5 — both reference DeNelle.Core only), so the toggle request
// routes through this tiny static gate in Core — the SAME cross-assembly pattern as
// PauseGate / PanelManager:
//   • The HUD's harvest icon button (next to Settings) calls RequestToggle().
//   • EchoWorkforceHud (Village) subscribes to ToggleRequested and shows/hides its
//     Obsidian panel.
//
// Pure static state (reset on domain reload). No MonoBehaviour / scene object —
// alive across additive scene loads, same as PauseGate.
// =============================================================================

using System;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.UI
{
    /// <summary>
    /// Static toggle arbiter for the Echo harvest panel. The HUD's harvest icon button
    /// (next to Settings) calls <see cref="RequestToggle"/>; the Echo panel
    /// (EchoWorkforceHud, in DeNelle.Village) subscribes to <see cref="ToggleRequested"/>
    /// and flips its own visibility. Event-based so Core never references DeNelle.Village.
    /// </summary>
    public static class HarvestPanelGate
    {
        /// <summary>
        /// Raised when the harvest button is tapped. The Echo panel subscribes and toggles
        /// its open/closed state. Kept event-based (not a direct call) so Core never
        /// references the Village panel that owns the UI.
        /// </summary>
        public static event Action ToggleRequested;

        /// <summary>
        /// The single "open/close the harvest panel" action (the HUD harvest button next to
        /// Settings). Raises <see cref="ToggleRequested"/>. Null-safe; safe to call from any
        /// assembly that references DeNelle.Core.
        /// </summary>
        public static void RequestToggle()
        {
            FlowTrace.Step("HUD", "HarvestPanelGate.RequestToggle — harvest panel toggle requested");
            ToggleRequested?.Invoke();
        }
    }
}
