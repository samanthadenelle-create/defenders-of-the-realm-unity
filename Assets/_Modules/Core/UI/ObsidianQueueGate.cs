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

        // ── WO-778: persistent status snapshot (Village publishes, HUD polls) ──
        // BuildTimerService (DeNelle.Village) owns queue + clock; it pushes a
        // presentation-ready snapshot here on QueueChanged + its 1s tick. The HUD
        // chip polls Status (the HudBuildingFocus precedent) — no cross-assembly read.

        /// <summary>Presentation-ready queue summary for the persistent HUD chip.</summary>
        public struct WorkQueueStatus
        {
            public bool Available;                       // false until the service publishes
            public int BuilderBusy, BuilderSlots, BuilderQueued;
            public int TrainBusy, TrainSlots, TrainQueued;
            public int ResearchBusy, ResearchSlots, ResearchQueued;
            public int SoonestRemainingSec;              // min across all channels; -1 = idle
            public int Version;                          // bumps per publish (change-detect)
        }

        private static int _statusVersion;

        /// <summary>Latest published snapshot (default/Available=false before first publish).</summary>
        public static WorkQueueStatus Status { get; private set; }

        /// <summary>Village-side publisher (BuildTimerService). Bumps Version.</summary>
        public static void PublishStatus(WorkQueueStatus s)
        {
            s.Version = ++_statusVersion;
            Status = s;
        }
    }
}
