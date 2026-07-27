// =============================================================================
// ObsidianQueueGate — the Core-level "open/close the work-queue panel" seam (WO-773).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.UI
//
// The common work-queue HUD (ObsidianQueueHud, in DeNelle.Village) is opened by a
// HUD button (DeNelle.HUD, VillageHudController). Those two assemblies cannot
// reference each other (CLAUDE.md §5 — both reference DeNelle.Core only), so the
// toggle request routes through this tiny static gate in Core — the SAME cross-
// assembly pattern as HarvestPanelGate / PauseGate / PanelManager.
//
// PLAYER-FACING NAMING: the panel title/copy says "Builders" / "Training" — never
// "Obsidian" (internal code name only).
// =============================================================================

using System;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.UI
{
    /// <summary>
    /// Static toggle arbiter for the common work-queue panel. A HUD button calls
    /// <see cref="RequestToggle"/>; the panel (ObsidianQueueHud, in DeNelle.Village)
    /// subscribes to <see cref="ToggleRequested"/> and flips its own visibility.
    /// </summary>
    public static class ObsidianQueueGate
    {
        /// <summary>Raised when the work-queue button is tapped. The panel subscribes and toggles.</summary>
        public static event Action ToggleRequested;

        /// <summary>Raise the toggle request (null-safe; safe from any assembly referencing DeNelle.Core).</summary>
        public static void RequestToggle()
        {
            FlowTrace.Step("HUD", "ObsidianQueueGate.RequestToggle — work-queue panel toggle requested");
            ToggleRequested?.Invoke();
        }
    }
}
