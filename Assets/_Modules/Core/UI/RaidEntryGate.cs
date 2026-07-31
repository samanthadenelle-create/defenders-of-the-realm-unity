// =============================================================================
// RaidEntryGate — the Core seam between the HudKit "Raids" button and the
// Village-side raid selection screen (owner F8 2026-07-30 "there is no raid
// option": the old VillageHudController crossed-swords icon raised RaidRequested,
// but the live HudKit HUD renders no raid widget at all — the raid loop had no
// visible door).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.UI
//
// Mirrors ObsidianQueueGate's toggle seam: the HUD (DeNelle.HUD, Core-only per
// the asmdef law) fires RequestOpen; the Village-side RaidEntryBridge subscribes
// and opens RaidSelectionScreen (whose Open() carries the WO-813 zero-troops
// safety net). No cross-assembly reference either direction.
// =============================================================================

using System;

namespace DeNelle.Core.UI
{
    /// <summary>Core seam: the HUD's Raids button requests; Village opens the screen.</summary>
    public static class RaidEntryGate
    {
        /// <summary>Raised by the HUD Raids button. Village-side RaidEntryBridge subscribes.</summary>
        public static event Action OpenRequested;

        /// <summary>Fired by the HudKit Raids button. Warns via the subscriber count being
        /// zero only implicitly — the bridge logs its own subscription, so a dead tap is
        /// visible as fire-with-no-open in the [Flow:Raid] trace.</summary>
        public static void RequestOpen() => OpenRequested?.Invoke();
    }
}
