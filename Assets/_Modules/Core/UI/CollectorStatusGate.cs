// =============================================================================
// CollectorStatusGate — the Core-level "how full are my collectors" seam (WO-900 §4).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.UI
//
// WHY A GATE AND NOT IVillageHud (WO-900 §4, decided at source):
//   IVillageHud is an IMPERATIVE PUSH interface (SetWave / SetCrystals / SetResources).
//   This is a POLLED STATUS SNAPSHOT, which is what the queue chip already does through
//   ObsidianQueueGate — two live precedents (ObsidianQueueGate, HarvestPanelGate) and one
//   live consumer pattern (HudKitController's chip poll). Adding a member to IVillageHud
//   would NOT let any existing reflection-bridge allowlist row be deleted, so the "right
//   end state" argument does not apply. No reflection is added by this file, so nothing
//   goes into tools/regression/static_gate.py.
//
// WHAT THE PLAYER SEES WITHOUT THIS: nothing. ResourceCollector.Accrue clamps silently at
// capacity and the wallet number simply stops moving. CollectorStackView (WO-900 §3) is the
// DIEGETIC tell on the building itself; this gate is the AMBIENT one, readable from
// anywhere in town without opening a modal or walking to the farm.
//
// ⚠ COPY LAW — the two-"full"s problem (WO-900 §4).
//   "Storage" / "Bank" / current-max belongs to the WALLET (WO-857).
//   "Collectors" belongs HERE. The chip says "Collectors 2/3 full" + "Tap to collect".
//   The word "Storage" must never appear on this surface.
//   Cross-WO dependency, named and NOT built here: once the bank has a headroom check, a
//   full bank means the Collect tap cannot bank, and the tell must then read "Bank full"
//   instead of "Tap to collect". WO-857 owns adding that; WO-900 ships the collect wording.
//
// ASCII only. TEXT-ENCODED STATE, NEVER COLOUR ALONE (the owner is red/green colourblind).
// =============================================================================

using System;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.UI
{
    /// <summary>
    /// Village publishes a presentation-ready collector snapshot; the HUD polls it. Same
    /// cross-assembly shape as <see cref="ObsidianQueueGate"/> — DeNelle.HUD and
    /// DeNelle.Village cannot see each other (CLAUDE.md §5), so the snapshot meets in Core.
    /// </summary>
    public static class CollectorStatusGate
    {
        /// <summary>Presentation-ready collector summary for the ambient HUD chip.</summary>
        public struct CollectorStatus
        {
            /// <summary>False until the Village publisher has run once. A chip must show the bare
            /// word rather than invent "0/0 full" for a town it has heard nothing about.</summary>
            public bool Available;
            /// <summary>Collectors sitting at capacity — the ones that have STOPPED earning.</summary>
            public int FullCount;
            /// <summary>Collectors that exist at all (placed + the DDOL logical fallbacks).</summary>
            public int TotalCount;
            /// <summary>Fullest collector, 0..100. The near-full warning band lives here, so the
            /// chip can say "85%" before anything is actually wasted.</summary>
            public int MaxFillPct;
            /// <summary>Whole resources waiting to be banked across every collector.</summary>
            public int TotalPending;
            /// <summary>Bumps per publish — the HUD repaints only when this moves.</summary>
            public int Version;
        }

        private static int s_version;

        /// <summary>Latest published snapshot (Available=false before the first publish).</summary>
        public static CollectorStatus Status { get; private set; }

        /// <summary>Village-side publisher (CollectorStatusPublisher). Bumps Version.</summary>
        public static void PublishStatus(CollectorStatus s)
        {
            s.Version = ++s_version;
            Status = s;
        }

        /// <summary>Raised when the player taps the ambient chip. The Village side answers by
        /// calling the EXISTING ResourceCollectorService.CollectAll() — this gate never adds a
        /// second collect command, it only carries the tap across the assembly wall.</summary>
        public static event Action CollectAllRequested;

        /// <summary>True when a Village listener is installed (a boot race must not read as a
        /// broken button — the same guard ObsidianQueueGate.HasSubscriber exists for).</summary>
        public static bool HasSubscriber => CollectAllRequested != null;

        /// <summary>Raise the collect-all request (null-safe from any assembly).</summary>
        public static void RequestCollectAll()
        {
            FlowTrace.Step("HUD", "CollectorStatusGate.RequestCollectAll - ambient collector chip tapped " +
                                  "(subscriber=" + (HasSubscriber ? "yes" : "NO - the Village bridge is not installed") + ")");
            CollectAllRequested?.Invoke();
        }
    }
}
